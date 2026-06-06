using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _db;
    private readonly ICobroService _cobroService;

    public DashboardService(AppDbContext db, ICobroService cobroService)
    {
        _db = db;
        _cobroService = cobroService;
    }

    public async Task<DashboardViewModel> GetDashboardDataAsync()
    {
        var hoy = DateTime.UtcNow.Date;
        var siguiente = hoy.AddDays(1);

        var ventasHoy = await _db.Ventas
            .Include(v => v.Cliente).ThenInclude(c => c.Zona)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .Where(v => v.Fecha >= hoy && v.Fecha < siguiente)
            .OrderByDescending(v => v.Fecha)
            .Take(10)
            .ToListAsync();

        var totalVendidoHoy = await _db.Ventas
            .Where(v => v.Fecha >= hoy && v.Fecha < siguiente)
            .SumAsync(v => (decimal?)v.Total) ?? 0;

        var cantidadVentasHoy = await _db.Ventas
            .Where(v => v.Fecha >= hoy && v.Fecha < siguiente)
            .CountAsync();

        var totalPorCobrar = await _cobroService.GetTotalPorCobrarAsync();
        var totalCobradoHoy = await _cobroService.GetTotalCobradoHoyAsync();
        var topDeudores = (await _cobroService.GetDeudoresAsync()).Take(5);

        return new DashboardViewModel
        {
            TotalVendidoHoy = totalVendidoHoy,
            CantidadVentasHoy = cantidadVentasHoy,
            TotalPorCobrar = totalPorCobrar,
            TotalCobradoHoy = totalCobradoHoy,
            VentasHoy = ventasHoy,
            TopDeudores = topDeudores
        };
    }
}
