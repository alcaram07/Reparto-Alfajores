using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class ProductosController : Controller
{
    private readonly IProductoService _productoService;
    private readonly ICategoriaService _categoriaService;

    public ProductosController(IProductoService productoService, ICategoriaService categoriaService)
    {
        _productoService = productoService;
        _categoriaService = categoriaService;
    }

    public async Task<IActionResult> Index(string? busqueda, int? categoriaId, bool? activo)
    {
        var productos = await _productoService.GetAllAsync(busqueda, categoriaId, activo);
        var categorias = await _categoriaService.GetAllAsync();
        var vmNuevo = new ProductoViewModel
        {
            Categorias = await _categoriaService.GetSelectListAsync()
        };
        ViewBag.Categorias = categorias;
        ViewBag.Busqueda = busqueda;
        ViewBag.CategoriaId = categoriaId;
        ViewBag.Activo = activo;
        ViewBag.VmNuevo = vmNuevo;
        return View(productos);
    }

    [HttpPost]
    public async Task<IActionResult> Nuevo(ProductoViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos del producto inválidos";
            return RedirectToAction(nameof(Index));
        }
        await _productoService.CreateAsync(vm);
        TempData["Success"] = "Producto creado correctamente";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Editar(int id)
    {
        var producto = await _productoService.GetByIdAsync(id);
        if (producto == null) return NotFound();

        var vm = new ProductoViewModel
        {
            Id = producto.Id,
            Nombre = producto.Nombre,
            CategoriaId = producto.CategoriaId,
            PrecioUnitario = producto.PrecioUnitario,
            Activo = producto.Activo,
            Categorias = await _categoriaService.GetSelectListAsync()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Editar(ProductoViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Categorias = await _categoriaService.GetSelectListAsync();
            return View(vm);
        }
        await _productoService.UpdateAsync(vm);
        TempData["Success"] = "Producto actualizado correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActivo(int id)
    {
        await _productoService.ToggleActivoAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
