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
        return cobro;
    }

    public async Task<IEnumerable<DeudorViewModel>> GetDeudoresAsync()
    {
        var ventasCC = await _db.Ventas
            .Where(v => v.EstadoCobro == EstadoCobro.CuentaCorriente)
            .GroupBy(v => v.ClienteId)
            .Select(g => new { ClienteId = g.Key, Total = g.Sum(v => v.Total) })
            .ToListAsync();

        var cobros = await _db.Cobros
            .GroupBy(c => c.ClienteId)
            .Select(g => new { ClienteId = g.Key, Total = g.Sum(c => c.Monto) })
            .ToListAsync();

        var ultimosPagos = await _db.Cobros
            .GroupBy(c => c.ClienteId)
            .Select(g => new { ClienteId = g.Key, UltimoPago = g.Max(c => c.Fecha) })
            .ToListAsync();

        var primerasVentasSinCobrar = await _db.Ventas
            .Where(v => v.EstadoCobro == EstadoCobro.CuentaCorriente)
            .GroupBy(v => v.ClienteId)
            .Select(g => new { ClienteId = g.Key, Primera = g.Min(v => v.Fecha) })
            .ToListAsync();

        var clientes = await _db.Clientes.Include(c => c.Zona).ToListAsync();

        var deudores = new List<DeudorViewModel>();
        foreach (var vc in ventasCC)
        {
            var totalCobros = cobros.FirstOrDefault(c => c.ClienteId == vc.ClienteId)?.Total ?? 0;
            var saldo = vc.Total - totalCobros;
            if (saldo <= 0) continue;

            var cliente = clientes.FirstOrDefault(c => c.Id == vc.ClienteId);
            if (cliente == null) continue;

            var primeraVenta = primerasVentasSinCobrar.FirstOrDefault(p => p.ClienteId == vc.ClienteId);
            var dias = primeraVenta != null ? (DateTime.UtcNow - primeraVenta.Primera).Days : 0;
            var ultimoPago = ultimosPagos.FirstOrDefault(u => u.ClienteId == vc.ClienteId)?.UltimoPago;

            deudores.Add(new DeudorViewModel
            {
                ClienteId = vc.ClienteId,
                Nombre = cliente.Nombre,
                Zona = cliente.Zona.Nombre,
                Saldo = saldo,
                DiasDeuda = dias,
                UltimoPago = ultimoPago
            });
        }

        return deudores.OrderByDescending(d => d.DiasDeuda);
    }

    public async Task<decimal> GetTotalPorCobrarAsync()
    {
        var ventasCC = await _db.Ventas
            .Where(v => v.EstadoCobro == EstadoCobro.CuentaCorriente)
            .SumAsync(v => (decimal?)v.Total) ?? 0;

        var totalCobros = await _db.Cobros.SumAsync(c => (decimal?)c.Monto) ?? 0;
        return Math.Max(0, ventasCC - totalCobros);
    }

    public async Task<decimal> GetTotalCobradoHoyAsync()
    {
        var hoy = DateTime.UtcNow.Date;
        var siguiente = hoy.AddDays(1);
        return await _db.Cobros
            .Where(c => c.Fecha >= hoy && c.Fecha < siguiente)
            .SumAsync(c => (decimal?)c.Monto) ?? 0;
    }
}
