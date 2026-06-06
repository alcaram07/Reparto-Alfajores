using Microsoft.AspNetCore.Mvc.Rendering;
using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface IProductoService
{
    Task<IEnumerable<Producto>> GetAllAsync(string? busqueda = null, int? categoriaId = null, bool? activo = null);
    Task<Producto?> GetByIdAsync(int id);
    Task<Producto> CreateAsync(ProductoViewModel vm);
    Task UpdateAsync(ProductoViewModel vm);
    Task ToggleActivoAsync(int id);
    Task<IEnumerable<SelectListItem>> GetSelectListActivosAsync();
    Task<Dictionary<int, decimal>> GetPreciosDictionaryAsync();
}
