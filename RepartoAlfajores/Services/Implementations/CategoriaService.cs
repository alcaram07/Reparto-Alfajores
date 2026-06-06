using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class CategoriaService : ICategoriaService
{
    private readonly AppDbContext _db;
    public CategoriaService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<CategoriaProducto>> GetAllAsync() =>
        await _db.CategoriaProductos.OrderBy(c => c.Nombre).ToListAsync();

    public async Task<CategoriaProducto?> GetByIdAsync(int id) =>
        await _db.CategoriaProductos.FindAsync(id);

    public async Task<CategoriaProducto> CreateAsync(CategoriaViewModel vm)
    {
        var cat = new CategoriaProducto { Nombre = vm.Nombre.Trim() };
        _db.CategoriaProductos.Add(cat);
        await _db.SaveChangesAsync();
        return cat;
    }

    public async Task UpdateAsync(CategoriaViewModel vm)
    {
        var cat = await _db.CategoriaProductos.FindAsync(vm.Id)
            ?? throw new InvalidOperationException("Categoría no encontrada");
        cat.Nombre = vm.Nombre.Trim();
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<SelectListItem>> GetSelectListAsync() =>
        await _db.CategoriaProductos
            .OrderBy(c => c.Nombre)
            .Select(c => new SelectListItem(c.Nombre, c.Id.ToString()))
            .ToListAsync();
}
