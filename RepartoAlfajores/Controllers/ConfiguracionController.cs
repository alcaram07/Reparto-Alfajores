using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Controllers;

[Authorize]
public class ConfiguracionController : Controller
{
    private readonly IZonaService _zonaService;
    private readonly ICategoriaService _categoriaService;
    private readonly IConfiguracionService _configuracionService;
    private readonly IConfiguration _config;

    public ConfiguracionController(IZonaService zonaService, ICategoriaService categoriaService,
        IConfiguracionService configuracionService, IConfiguration config)
    {
        _zonaService = zonaService;
        _categoriaService = categoriaService;
        _configuracionService = configuracionService;
        _config = config;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Zonas = await _zonaService.GetAllAsync();
        ViewBag.Categorias = await _categoriaService.GetAllAsync();
        ViewBag.NombreNegocio = _config["NombreNegocio"];

        var groqKey = await _configuracionService.GetValorAsync("GroqApiKey") ?? "";
        ViewBag.GroqKeyConfigurada = !string.IsNullOrWhiteSpace(groqKey);
        ViewBag.GroqKeyMasked = groqKey.Length > 8
            ? groqKey[..4] + new string('•', 8) + groqKey[^4..]
            : (groqKey.Length > 0 ? new string('•', groqKey.Length) : "");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> GuardarApiKey(string groqApiKey)
    {
        if (string.IsNullOrWhiteSpace(groqApiKey))
        {
            TempData["Error"] = "Ingresá una API key válida";
            return RedirectToAction(nameof(Index));
        }
        await _configuracionService.SetValorAsync("GroqApiKey", groqApiKey.Trim());
        TempData["Success"] = "API key de Groq guardada correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> NuevaZona(ZonaViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nombre de zona inválido";
            return RedirectToAction(nameof(Index));
        }
        await _zonaService.CreateAsync(vm);
        TempData["Success"] = "Zona creada correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> EditarZona(ZonaViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos de zona inválidos";
            return RedirectToAction(nameof(Index));
        }
        await _zonaService.UpdateAsync(vm);
        TempData["Success"] = "Zona actualizada";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleZona(int id)
    {
        await _zonaService.ToggleActivaAsync(id);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> NuevaCategoria(CategoriaViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Nombre de categoría inválido";
            return RedirectToAction(nameof(Index));
        }
        await _categoriaService.CreateAsync(vm);
        TempData["Success"] = "Categoría creada correctamente";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> EditarCategoria(CategoriaViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Datos de categoría inválidos";
            return RedirectToAction(nameof(Index));
        }
        await _categoriaService.UpdateAsync(vm);
        TempData["Success"] = "Categoría actualizada";
        return RedirectToAction(nameof(Index));
    }
}
