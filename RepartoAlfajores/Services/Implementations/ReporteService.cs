using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class ReporteService : IReporteService
{
    private readonly AppDbContext _db;
    public ReporteService(AppDbContext db) => _db = db;

    public async Task<ReporteViewModel> GetReporteAsync(DateTime desde, DateTime hasta)
    {
        var hastaFin = hasta.Date.AddDays(1);

        var ventas = await _db.Ventas
            .Include(v => v.Cliente).ThenInclude(c => c.Zona)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .Where(v => v.Fecha >= desde.Date && v.Fecha < hastaFin)
            .ToListAsync();

        var cobros = await _db.Cobros
            .Where(c => c.Fecha >= desde.Date && c.Fecha < hastaFin)
            .ToListAsync();

        var totalVendido = ventas.Sum(v => v.Total);
        var cantidadVentas = ventas.Count;
        var ticketPromedio = cantidadVentas > 0 ? totalVendido / cantidadVentas : 0;
        var totalCobrado = cobros.Sum(c => c.Monto);
        var totalPendiente = ventas
            .Where(v => v.EstadoCobro == EstadoCobro.CuentaCorriente)
            .Sum(v => v.Total) - cobros.Sum(c => c.Monto);

        var ventasDia = ventas
            .GroupBy(v => v.Fecha.Date)
            .Select(g => new VentaDiaDto
            {
                Fecha = g.Key,
                Total = g.Sum(v => v.Total),
                Cantidad = g.Count()
            })
            .OrderBy(d => d.Fecha)
            .ToList();

        var ventasPorZona = ventas
            .Where(v => v.Cliente?.Zona != null)
            .GroupBy(v => v.Cliente.Zona.Nombre)
            .Select(g => new VentaZonaDto
            {
                Nombre = g.Key,
                Total = g.Sum(v => v.Total),
                Cantidad = g.Count()
            })
            .OrderByDescending(z => z.Total)
            .ToList();

        var topProductos = ventas
            .SelectMany(v => v.Detalles)
            .GroupBy(d => d.Producto?.Nombre ?? "Desconocido")
            .Select(g => new ProductoRankingDto
            {
                Nombre = g.Key,
                TotalCantidad = g.Sum(d => d.Cantidad),
                TotalMonto = g.Sum(d => d.Cantidad * d.PrecioUnitario)
            })
            .OrderByDescending(p => p.TotalCantidad)
            .Take(8)
            .ToList();

        var topClientes = ventas
            .Where(v => v.Cliente != null)
            .GroupBy(v => v.Cliente.Nombre)
            .Select(g => new ClienteRankingDto
            {
                Nombre = g.Key,
                TotalMonto = g.Sum(v => v.Total),
                TotalVentas = g.Count()
            })
            .OrderByDescending(c => c.TotalMonto)
            .Take(5)
            .ToList();

        return new ReporteViewModel
        {
            Desde = desde,
            Hasta = hasta,
            TotalVendido = totalVendido,
            CantidadVentas = cantidadVentas,
            TicketPromedio = ticketPromedio,
            TotalCobrado = totalCobrado,
            TotalPendiente = Math.Max(0, totalPendiente),
            VentasDia = ventasDia,
            VentasPorZona = ventasPorZona,
            TopProductos = topProductos,
            TopClientes = topClientes
        };
    }
}
