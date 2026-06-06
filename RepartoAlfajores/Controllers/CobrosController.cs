using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class CobrosController : Controller
{
    private readonly ICobroService _cobroService;
    private readonly IClienteService _clienteService;

    public CobrosController(ICobroService cobroService, IClienteService clienteService)
    {
        _cobroService = cobroService;
        _clienteService = clienteService;
    }

    public async Task<IActionResult> Index(int? clienteId)
    {
        var deudores = await _cobroService.GetDeudoresAsync();
        var cobrosHoy = await _cobroService.GetAllAsync();
        var totalPorCobrar = await _cobroService.GetTotalPorCobrarAsync();
        var totalCobradoHoy = await _cobroService.GetTotalCobradoHoyAsync();

        var deudorMasAntiguo = deudores.OrderByDescending(d => d.DiasDeuda).FirstOrDefault();

        var vmCobro = new CobroViewModel
        {
            ClienteId = clienteId ?? 0,
            Clientes = await _clienteService.GetSelectListConDeudaAsync()
        };

        ViewBag.Deudores = deudores;
        ViewBag.CobrosHoy = cobrosHoy;
        ViewBag.TotalPorCobrar = totalPorCobrar;
        ViewBag.TotalCobradoHoy = totalCobradoHoy;
        ViewBag.DeudorMasAntiguo = deudorMasAntiguo;
        ViewBag.VmCobro = vmCobro;

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Registrar(CobroViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos del cobro inválidos";
            return RedirectToAction(nameof(Index));
        }
        await _cobroService.CreateAsync(vm);
        TempData["Success"] = "Cobro registrado correctamente";
        return RedirectToAction(nameof(Index));
    }
}
