using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class ZonaService : IZonaService
{
    private readonly AppDbContext _db;
    public ZonaService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Zona>> GetAllAsync() =>
        await _db.Zonas.OrderBy(z => z.Nombre).ToListAsync();

    public async Task<Zona?> GetByIdAsync(int id) =>
        await _db.Zonas.FindAsync(id);

    public async Task<Zona> CreateAsync(ZonaViewModel vm)
    {
        var zona = new Zona { Nombre = vm.Nombre.Trim(), Activa = vm.Activa };
        _db.Zonas.Add(zona);
        await _db.SaveChangesAsync();
        return zona;
    }

    public async Task UpdateAsync(ZonaViewModel vm)
    {
        var zona = await _db.Zonas.FindAsync(vm.Id)
            ?? throw new InvalidOperationException("Zona no encontrada");
        zona.Nombre = vm.Nombre.Trim();
        zona.Activa = vm.Activa;
        await _db.SaveChangesAsync();
    }

    public async Task ToggleActivaAsync(int id)
    {
        var zona = await _db.Zonas.FindAsync(id)
            ?? throw new InvalidOperationException("Zona no encontrada");
        zona.Activa = !zona.Activa;
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<SelectListItem>> GetSelectListAsync() =>
        await _db.Zonas
            .Where(z => z.Activa)
            .OrderBy(z => z.Nombre)
            .Select(z => new SelectListItem(z.Nombre, z.Id.ToString()))
            .ToListAsync();
}
