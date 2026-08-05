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
            throw new InvalidOperationException("El cliente indicado no existe.");

        if (vm.Monto <= 0)
            throw new InvalidOperationException("El monto del cobro debe ser mayor a cero.");

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

    public async Task<IEnumerable<DeudorViewModel>> GetDeudoresAsync()
    {
        // Saldo actual = SaldoAcumulado del último movimiento por cliente
        var saldosPorCliente = await _db.MovimientosCC
            .GroupBy(m => m.ClienteId)
            .Select(g => new
            {
                ClienteId = g.Key,
                Saldo = g.OrderByDescending(m => m.Id).First().SaldoAcumulado
            })
            .Where(x => x.Saldo > 0)
            .ToListAsync();

        if (!saldosPorCliente.Any()) return [];

        var clienteIds = saldosPorCliente.Select(x => x.ClienteId).ToList();

        var clientes = await _db.Clientes
            .Include(c => c.Zona)
            .Where(c => clienteIds.Contains(c.Id))
            .ToListAsync();

        var ultimosPagos = await _db.Cobros
            .Where(c => clienteIds.Contains(c.ClienteId))
            .GroupBy(c => c.ClienteId)
            .Select(g => new { ClienteId = g.Key, UltimoPago = g.Max(c => c.Fecha) })
            .ToListAsync();

        var primerasVentasCC = await _db.Ventas
            .Where(v => v.EstadoCobro == EstadoCobro.CuentaCorriente && clienteIds.Contains(v.ClienteId))
            .GroupBy(v => v.ClienteId)
            .Select(g => new { ClienteId = g.Key, Primera = g.Min(v => v.Fecha) })
            .ToListAsync();

        var deudores = saldosPorCliente.Select(x =>
        {
            var cliente = clientes.First(c => c.Id == x.ClienteId);
            var primeraVenta = primerasVentasCC.FirstOrDefault(p => p.ClienteId == x.ClienteId);
            var dias = primeraVenta != null ? (DateTime.UtcNow - primeraVenta.Primera).Days : 0;
            var ultimoPago = ultimosPagos.FirstOrDefault(u => u.ClienteId == x.ClienteId)?.UltimoPago;

            return new DeudorViewModel
            {
                ClienteId = x.ClienteId,
                Nombre = cliente.Nombre,
                Zona = cliente.Zona.Nombre,
                Saldo = x.Saldo,
                DiasDeuda = dias,
                UltimoPago = ultimoPago
            };
        });

        return deudores.OrderByDescending(d => d.DiasDeuda);
    }

    public async Task<decimal> GetTotalPorCobrarAsync() =>
        await _db.MovimientosCC
            .GroupBy(m => m.ClienteId)
            .Select(g => g.OrderByDescending(m => m.Id).First().SaldoAcumulado)
            // Sólo los saldos deudores: un cliente con crédito a favor no debe
            // descontar de lo que el resto adeuda.
            .Where(s => s > 0)
            .SumAsync(s => (decimal?)s) ?? 0m;

    public async Task<decimal> GetTotalCobradoHoyAsync()
    {
        var (desde, hasta) = FechaAr.RangoDia(FechaAr.Hoy);
        return await _db.Cobros
            .Where(c => c.Fecha >= desde && c.Fecha < hasta)
            .SumAsync(c => (decimal?)c.Monto) ?? 0;
    }
}
