using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Services.Interfaces;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class ReportesController : Controller
{
    private readonly IReporteService _reporteService;

    public ReportesController(IReporteService reporteService)
    {
        _reporteService = reporteService;
    }

    public async Task<IActionResult> Index(string tab = "mes", DateTime? desde = null, DateTime? hasta = null)
    {
        var (fechaDesde, fechaHasta) = ResolverRango(tab, desde, hasta);
        var vm = await _reporteService.GetReporteAsync(fechaDesde, fechaHasta);
        vm.Tab = tab;

        vm.VentasDiaJson = JsonSerializer.Serialize(
            vm.VentasDia.Select(v => new { fecha = v.Fecha.ToString("dd/MM"), total = v.Total }));
        vm.VentasZonaJson = JsonSerializer.Serialize(
            vm.VentasPorZona.Select(z => new { zona = z.Nombre, total = z.Total }));

        return View(vm);
    }

    private static (DateTime Desde, DateTime Hasta) ResolverRango(string tab, DateTime? desde, DateTime? hasta)
    {
        var hoy = DateTime.UtcNow.Date;
        return tab switch
        {
            "hoy" => (hoy, hoy),
            "semana" => (hoy.AddDays(-6), hoy),
            _ => desde.HasValue && hasta.HasValue
                ? (desde.Value.Date, hasta.Value.Date)
                : (new DateTime(hoy.Year, hoy.Month, 1), hoy)
        };
    }
}
