using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class ClientesController : Controller
{
    private readonly IClienteService _clienteService;
    private readonly IZonaService _zonaService;

    public ClientesController(IClienteService clienteService, IZonaService zonaService)
    {
        _clienteService = clienteService;
        _zonaService = zonaService;
    }

    public async Task<IActionResult> Index(string? busqueda, int? zonaId, string? estadoDeuda)
    {
        var clientes = await _clienteService.GetAllAsync(busqueda, zonaId, estadoDeuda);
        var zonas = await _zonaService.GetAllAsync();
        ViewBag.Zonas = zonas;
        ViewBag.Busqueda = busqueda;
        ViewBag.ZonaId = zonaId;
        ViewBag.EstadoDeuda = estadoDeuda;
        return View(clientes);
    }

    public async Task<IActionResult> Nuevo()
    {
        var vm = new ClienteViewModel
        {
            Zonas = await _zonaService.GetSelectListAsync()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nuevo(ClienteViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Zonas = await _zonaService.GetSelectListAsync();
            return View(vm);
        }
        await _clienteService.CreateAsync(vm);
        TempData["Success"] = "Cliente creado correctamente";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Editar(int id)
    {
        var cliente = await _clienteService.GetByIdAsync(id);
        if (cliente == null) return NotFound();

        var vm = new ClienteViewModel
        {
            Id = cliente.Id,
            Nombre = cliente.Nombre,
            Telefono = cliente.Telefono,
            Direccion = cliente.Direccion,
            ZonaId = cliente.ZonaId,
            Activo = cliente.Activo,
            Zonas = await _zonaService.GetSelectListAsync()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Editar(ClienteViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Zonas = await _zonaService.GetSelectListAsync();
            return View(vm);
        }
        await _clienteService.UpdateAsync(vm);
        TempData["Success"] = "Cliente actualizado correctamente";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var cliente = await _clienteService.GetByIdAsync(id);
        if (cliente == null) return NotFound();

        var saldo = await _clienteService.GetSaldoPendienteAsync(id);
        var ventas = await _clienteService.GetVentasByClienteAsync(id);
        var cobros = await _clienteService.GetCobrosByClienteAsync(id);

        ViewBag.Saldo = saldo;
        ViewBag.Ventas = ventas;
        ViewBag.Cobros = cobros;

        return View(cliente);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActivo(int id)
    {
        await _clienteService.ToggleActivoAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
