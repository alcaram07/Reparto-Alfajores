using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.Utils;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class ReporteService : IReporteService
{
    private readonly AppDbContext _db;
    private readonly ICuentaCorrienteService _cuentaCorriente;

    public ReporteService(AppDbContext db, ICuentaCorrienteService cuentaCorriente)
    {
        _db = db;
        _cuentaCorriente = cuentaCorriente;
    }

    public async Task<ReporteViewModel> GetReporteAsync(DateTime desde, DateTime hasta)
    {
        var (desdeUtc, hastaUtc) = FechaAr.Rango(desde, hasta);

        var ventas = await _db.Ventas
            .Include(v => v.Cliente).ThenInclude(c => c.Zona)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .Where(v => v.Fecha >= desdeUtc && v.Fecha < hastaUtc)
            .ToListAsync();

        var cobros = await _db.Cobros
            .Where(c => c.Fecha >= desdeUtc && c.Fecha < hastaUtc)
            .ToListAsync();

        var totalVendido = ventas.Sum(v => v.Total);
        var cantidadVentas = ventas.Count;
        var ticketPromedio = cantidadVentas > 0 ? totalVendido / cantidadVentas : 0;
        var totalCobrado = cobros.Sum(c => c.Monto);

        // Deuda vigente de todos los clientes, no un cálculo acotado al período: antes se
        // restaban todos los cobros del rango a las ventas en cuenta corriente del rango, así
        // que un pago de una deuda vieja descontaba ventas nuevas y el número daba cualquier
        // cosa (y el Math.Max escondía los negativos). Los cobros no están atados a ventas
        // puntuales, así que "lo pendiente de este período" no es calculable; lo que sí tiene
        // sentido —y es lo que se quiere saber— es cuánto se adeuda hoy.
        var saldos = await _cuentaCorriente.GetSaldosAsync();
        var totalPendiente = saldos.Values.Where(s => s > 0).Sum();

        var ventasDia = ventas
            // Agrupado por día del calendario argentino, no por día UTC.
            .GroupBy(v => v.Fecha.ALocal().Date)
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
            TotalPendiente = totalPendiente,
            VentasDia = ventasDia,
            VentasPorZona = ventasPorZona,
            TopProductos = topProductos,
            TopClientes = topClientes
        };
    }
}
