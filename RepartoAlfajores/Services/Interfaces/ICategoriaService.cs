using Microsoft.AspNetCore.Mvc.Rendering;
using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface ICategoriaService
{
    Task<IEnumerable<CategoriaProducto>> GetAllAsync();
    Task<CategoriaProducto?> GetByIdAsync(int id);
    Task<CategoriaProducto> CreateAsync(CategoriaViewModel vm);
    Task UpdateAsync(CategoriaViewModel vm);
    Task<IEnumerable<SelectListItem>> GetSelectListAsync();
}
