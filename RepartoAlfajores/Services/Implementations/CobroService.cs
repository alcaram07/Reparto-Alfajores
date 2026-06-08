using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class CobroService : ICobroService
{
    private readonly AppDbContext _db;
    public CobroService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Cobro>> GetAllAsync(DateTime? fecha = null)
    {
        var dia = (fecha ?? DateTime.UtcNow).Date;
        var siguiente = dia.AddDays(1);
        return await _db.Cobros
            .Include(c => c.Cliente)
            .Where(c => c.Fecha >= dia && c.Fecha < siguiente)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();
    }

    public async Task<Cobro> CreateAsync(CobroViewModel vm)
    {
        var cobro = new Cobro
        {
            ClienteId = vm.ClienteId,
            Monto = vm.Monto,
            MetodoPago = vm.MetodoPago,
            Fecha = DateTime.UtcNow,
            Nota = vm.Nota?.Trim()
        };
        _db.Cobros.Add(cobro);
        await _db.SaveChangesAsync();

        var saldoPrevio = await _db.MovimientosCC
            .Where(m => m.ClienteId == vm.ClienteId)
            .OrderByDescending(m => m.Id)
            .Select(m => (decimal?)m.SaldoAcumulado)
            .FirstOrDefaultAsync() ?? 0m;

        _db.MovimientosCC.Add(new MovimientoCC
        {
            ClienteId = vm.ClienteId,
            Fecha = cobro.Fecha,
            Tipo = TipoMovimientoCC.Abono,
            Monto = cobro.Monto,
            SaldoAcumulado = Math.Max(0, saldoPrevio - cobro.Monto),
            Descripcion = $"Cobro #{cobro.Id}",
            CobroId = cobro.Id
        });
        await _db.SaveChangesAsync();

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
            .SumAsync(s => (decimal?)s) ?? 0m;

    public async Task<decimal> GetTotalCobradoHoyAsync()
    {
        var hoy = DateTime.UtcNow.Date;
        var siguiente = hoy.AddDays(1);
        return await _db.Cobros
            .Where(c => c.Fecha >= hoy && c.Fecha < siguiente)
            .SumAsync(c => (decimal?)c.Monto) ?? 0;
    }
}
