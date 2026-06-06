using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class VentasController : Controller
{
    private readonly IVentaService _ventaService;
    private readonly IClienteService _clienteService;
    private readonly IProductoService _productoService;
    private readonly IZonaService _zonaService;

    public VentasController(IVentaService ventaService, IClienteService clienteService,
        IProductoService productoService, IZonaService zonaService)
    {
        _ventaService = ventaService;
        _clienteService = clienteService;
        _productoService = productoService;
        _zonaService = zonaService;
    }

    public async Task<IActionResult> Index(string? busqueda, EstadoCobro? estado, int? zonaId, DateTime? fecha)
    {
        var ventas = await _ventaService.GetAllAsync(fecha, busqueda, estado, zonaId);
        var zonas = await _zonaService.GetAllAsync();
        ViewBag.Zonas = zonas;
        ViewBag.Busqueda = busqueda;
        ViewBag.Estado = estado;
        ViewBag.ZonaId = zonaId;
        ViewBag.Fecha = (fecha ?? DateTime.UtcNow.Date).ToString("yyyy-MM-dd");
        return View(ventas);
    }

    public async Task<IActionResult> Nuevo()
    {
        var precios = await _productoService.GetPreciosDictionaryAsync();
        var vm = new VentaViewModel
        {
            Clientes = await _clienteService.GetSelectListAsync(),
            Productos = await _productoService.GetSelectListActivosAsync(),
            ProductosPreciosJson = JsonSerializer.Serialize(precios)
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Nuevo(VentaViewModel vm)
    {
        if (vm.Detalles == null || vm.Detalles.Count == 0)
            ModelState.AddModelError("Detalles", "Agregue al menos un producto");

        if (!ModelState.IsValid)
        {
            var precios = await _productoService.GetPreciosDictionaryAsync();
            vm.Clientes = await _clienteService.GetSelectListAsync();
            vm.Productos = await _productoService.GetSelectListActivosAsync();
            vm.ProductosPreciosJson = JsonSerializer.Serialize(precios);
            return View(vm);
        }

        var venta = await _ventaService.CreateAsync(vm);
        TempData["Success"] = "Venta registrada correctamente";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var venta = await _ventaService.GetByIdAsync(id);
        if (venta == null) return NotFound();
        return View(venta);
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _ventaService.DeleteAsync(id);
        TempData["Success"] = "Venta eliminada";
        return RedirectToAction(nameof(Index));
    }
}
