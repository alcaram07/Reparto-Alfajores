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
    private readonly IVentaVozService _ventaVozService;

    public VentasController(IVentaService ventaService, IClienteService clienteService,
        IProductoService productoService, IZonaService zonaService, IVentaVozService ventaVozService)
    {
        _ventaService = ventaService;
        _clienteService = clienteService;
        _productoService = productoService;
        _zonaService = zonaService;
        _ventaVozService = ventaVozService;
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

    [HttpPost]
    public async Task<IActionResult> InterpretarVoz(IFormFile? audio)
    {
        if (audio == null || audio.Length == 0)
            return Json(new VozResultado { Error = "No se recibió audio." });

        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var mime = string.IsNullOrEmpty(audio.ContentType) ? "audio/webm" : audio.ContentType;
        var dataUri = $"data:{mime};base64,{base64}";

        var resultado = await _ventaVozService.ProcesarAudioAsync(dataUri);
        return Json(resultado);
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
