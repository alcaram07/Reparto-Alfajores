using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.Utils;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class CobroService : ICobroService
{
    private readonly AppDbContext _db;
    private readonly ICuentaCorrienteService _cuentaCorriente;

    public CobroService(AppDbContext db, ICuentaCorrienteService cuentaCorriente)
    {
        _db = db;
        _cuentaCorriente = cuentaCorriente;
    }

    public async Task<IEnumerable<Cobro>> GetAllAsync(DateTime? fecha = null)
    {
        var (desde, hasta) = FechaAr.RangoDia(fecha ?? FechaAr.Hoy);
        return await _db.Cobros
            .Include(c => c.Cliente)
            .Where(c => c.Fecha >= desde && c.Fecha < hasta)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();
    }

    public async Task<Cobro> CreateAsync(CobroViewModel vm)
    {
        var clienteExiste = await _db.Clientes.AnyAsync(c => c.Id == vm.ClienteId);
        if (!clienteExiste)
            throw new NegocioException("El cliente indicado no existe.");

        if (vm.Monto <= 0)
            throw new NegocioException("El monto del cobro debe ser mayor a cero.");

        var cobro = new Cobro
        {
            ClienteId = vm.ClienteId,
            Monto = vm.Monto,
            MetodoPago = vm.MetodoPago,
            Fecha = DateTime.UtcNow,
            Nota = vm.Nota?.Trim()
        };

        // El cobro y su abono en cuenta corriente van juntos: si sólo entrara el cobro,
        // el cliente seguiría figurando como deudor de algo que ya pagó.
        // EnableRetryOnFailure exige abrir la transacción a través de la execution strategy.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            await _cuentaCorriente.BloquearClienteAsync(cobro.ClienteId);

            _db.Cobros.Add(cobro);
            await _db.SaveChangesAsync();

            await _cuentaCorriente.RegistrarMovimientoAsync(
                cobro.ClienteId, TipoMovimientoCC.Abono, cobro.Monto,
                $"Cobro #{cobro.Id}", cobro.Fecha, cobroId: cobro.Id);

            await tx.CommitAsync();
        });

        return cobro;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var cobro = await _db.Cobros.FirstOrDefaultAsync(c => c.Id == id);
            if (cobro == null) return false;

            var clienteId = cobro.ClienteId;
            await _cuentaCorriente.BloquearClienteAsync(clienteId);

            // MovimientosCC referencia el cobro con FK Restrict: sin borrar el abono primero,
            // Postgres rechaza el DELETE.
            var movimiento = await _db.MovimientosCC.FirstOrDefaultAsync(m => m.CobroId == id);
            if (movimiento != null)
                _db.MovimientosCC.Remove(movimiento);

            _db.Cobros.Remove(cobro);
            await _db.SaveChangesAsync();

            // El abono sale del medio de la cadena, así que los saldos posteriores quedan
            // calculados sobre un pago que ya no existe.
            await _cuentaCorriente.RecalcularSaldosAsync(clienteId);

            await tx.CommitAsync();
            return true;
        });
    }

    public async Task<IEnumerable<DeudorViewModel>> GetDeudoresAsync()
    {
        var saldos = await _cuentaCorriente.GetSaldosAsync();
        var deudores = saldos.Where(s => s.Value > 0).ToList();

        if (deudores.Count == 0) return [];

        var clienteIds = deudores.Select(d => d.Key).ToList();

        var clientes = await _db.Clientes
            .Include(c => c.Zona)
            .Where(c => clienteIds.Contains(c.Id))
            .ToListAsync();

        var ultimosPagos = await _db.Cobros
            .Where(c => clienteIds.Contains(c.ClienteId))
            .GroupBy(c => c.ClienteId)
            .Select(g => new { ClienteId = g.Key, UltimoPago = g.Max(c => c.Fecha) })
            .ToListAsync();

        // Antes se medía desde la primera venta en cuenta corriente de toda la historia, aunque
        // estuviera paga: un cliente que compra a cuenta hace un año y paga puntual figuraba con
        // "365 días". Ahora se mide desde que arrancó la deuda que sigue abierta.
        var inicioDeuda = await _cuentaCorriente.GetInicioDeudaAsync();
        var ahora = DateTime.UtcNow;

        var resultado = deudores.Select(d =>
        {
            var cliente = clientes.First(c => c.Id == d.Key);
            var dias = inicioDeuda.TryGetValue(d.Key, out var desde)
                ? Math.Max(0, (ahora - desde).Days)
                : 0;

            return new DeudorViewModel
            {
                ClienteId = d.Key,
                Nombre = cliente.Nombre,
                Zona = cliente.Zona.Nombre,
                Saldo = d.Value,
                DiasDeuda = dias,
                UltimoPago = ultimosPagos.FirstOrDefault(u => u.ClienteId == d.Key)?.UltimoPago
            };
        });

        return resultado.OrderByDescending(d => d.DiasDeuda);
    }

    public async Task<decimal> GetTotalPorCobrarAsync()
    {
        var saldos = await _cuentaCorriente.GetSaldosAsync();
        // Sólo los saldos deudores: un cliente con crédito a favor no debe descontar de lo
        // que el resto adeuda.
        return saldos.Values.Where(s => s > 0).Sum();
    }

    public async Task<decimal> GetTotalCobradoHoyAsync()
    {
        var (desde, hasta) = FechaAr.RangoDia(FechaAr.Hoy);
        return await _db.Cobros
            .Where(c => c.Fecha >= desde && c.Fecha < hasta)
            .SumAsync(c => (decimal?)c.Monto) ?? 0;
    }
}
