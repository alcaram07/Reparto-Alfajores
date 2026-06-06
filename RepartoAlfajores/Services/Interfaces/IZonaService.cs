using Microsoft.AspNetCore.Mvc.Rendering;
using RepartoAlfajores.Models;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Interfaces;

public interface IZonaService
{
    Task<IEnumerable<Zona>> GetAllAsync();
    Task<Zona?> GetByIdAsync(int id);
    Task<Zona> CreateAsync(ZonaViewModel vm);
    Task UpdateAsync(ZonaViewModel vm);
    Task ToggleActivaAsync(int id);
    Task<IEnumerable<SelectListItem>> GetSelectListAsync();
}
