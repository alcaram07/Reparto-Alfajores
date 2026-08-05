using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.Utils;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class CobrosController : Controller
{
    private readonly ICobroService _cobroService;
    private readonly IClienteService _clienteService;
    private readonly ICuentaCorrienteService _cuentaCorriente;

    public CobrosController(ICobroService cobroService, IClienteService clienteService,
        ICuentaCorrienteService cuentaCorriente)
    {
        _cobroService = cobroService;
        _clienteService = clienteService;
        _cuentaCorriente = cuentaCorriente;
    }

    public async Task<IActionResult> Index(int? clienteId, DateTime? fecha)
    {
        var deudores = (await _cobroService.GetDeudoresAsync()).ToList();
        var dia = fecha ?? FechaAr.Hoy;
        var cobrosDelDia = await _cobroService.GetAllAsync(dia);
        var totalPorCobrar = await _cobroService.GetTotalPorCobrarAsync();
        var totalCobradoHoy = await _cobroService.GetTotalCobradoHoyAsync();

        var deudorMasAntiguo = deudores.OrderByDescending(d => d.DiasDeuda).FirstOrDefault();

        var vmCobro = new CobroViewModel
        {
            ClienteId = clienteId ?? 0,
            Clientes = await _clienteService.GetSelectListConDeudaAsync()
        };

        ViewBag.Deudores = deudores;
        ViewBag.CobrosHoy = cobrosDelDia;
        ViewBag.TotalPorCobrar = totalPorCobrar;
        ViewBag.TotalCobradoHoy = totalCobradoHoy;
        ViewBag.DeudorMasAntiguo = deudorMasAntiguo;
        ViewBag.VmCobro = vmCobro;
        ViewBag.Fecha = dia.ToString("yyyy-MM-dd");
        ViewBag.EsHoy = dia.Date == FechaAr.Hoy;
        // Para mostrar el saldo al elegir cliente y ofrecer "saldar todo".
        ViewBag.SaldosJson = JsonSerializer.Serialize(
            deudores.ToDictionary(d => d.ClienteId.ToString(), d => d.Saldo));

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, DateTime? fecha)
    {
        if (await _cobroService.DeleteAsync(id))
            TempData["Success"] = "Cobro eliminado y saldo recalculado";
        else
            TempData["Error"] = "El cobro no existe o ya fue eliminado";

        return RedirectToAction(nameof(Index), new { fecha = fecha?.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    public async Task<IActionResult> Registrar(CobroViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            // Sin los mensajes concretos, un monto por encima del tope parece un bug.
            var errores = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct();

            TempData["Error"] = errores.Any()
                ? string.Join(" ", errores)
                : "Datos del cobro inválidos";

            return RedirectToAction(nameof(Index));
        }
        // Las NegocioException las traduce ManejadorDeErroresFilter; acá no hace falta try/catch.
        await _cobroService.CreateAsync(vm);

        // Un pago mayor a la deuda deja saldo negativo: se avisa para que no parezca un error.
        var saldo = await _cuentaCorriente.GetSaldoAsync(vm.ClienteId);
        TempData["Success"] = saldo < 0
            ? $"Cobro registrado. El cliente queda con ${-saldo:N2} a favor."
            : "Cobro registrado correctamente";

        return RedirectToAction(nameof(Index));
    }
}
