using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Services.Interfaces;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly IDashboardService _dashboardService;

    public HomeController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var vm = await _dashboardService.GetDashboardDataAsync();
        return View(vm);
    }

    // Sin AllowAnonymous, el [Authorize] de la clase redirige al login en vez de mostrar
    // el error — justo cuando algo falló y el usuario necesita entender qué pasó.
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Error(int? code = null)
    {
        ViewBag.Codigo = code;
        return View();
    }
}
