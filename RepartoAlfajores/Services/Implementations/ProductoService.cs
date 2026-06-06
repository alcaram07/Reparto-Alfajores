using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class ProductoService : IProductoService
{
    private readonly AppDbContext _db;
    public ProductoService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Producto>> GetAllAsync(string? busqueda = null, int? categoriaId = null, bool? activo = null)
    {
        var q = _db.Productos.Include(p => p.Categoria).AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            q = q.Where(p => p.Nombre.Contains(busqueda));

        if (categoriaId.HasValue)
            q = q.Where(p => p.CategoriaId == categoriaId);

        if (activo.HasValue)
            q = q.Where(p => p.Activo == activo);

        return await q.OrderBy(p => p.Nombre).ToListAsync();
    }

    public async Task<Producto?> GetByIdAsync(int id) =>
        await _db.Productos.Include(p => p.Categoria).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Producto> CreateAsync(ProductoViewModel vm)
    {
        var producto = new Producto
        {
            Nombre = vm.Nombre.Trim(),
            CategoriaId = vm.CategoriaId,
            PrecioUnitario = vm.PrecioUnitario,
            Activo = vm.Activo
        };
        _db.Productos.Add(producto);
        await _db.SaveChangesAsync();
        return producto;
    }

    public async Task UpdateAsync(ProductoViewModel vm)
    {
        var producto = await _db.Productos.FindAsync(vm.Id)
            ?? throw new InvalidOperationException("Producto no encontrado");
        producto.Nombre = vm.Nombre.Trim();
        producto.CategoriaId = vm.CategoriaId;
        producto.PrecioUnitario = vm.PrecioUnitario;
        producto.Activo = vm.Activo;
        await _db.SaveChangesAsync();
    }

    public async Task ToggleActivoAsync(int id)
    {
        var producto = await _db.Productos.FindAsync(id)
            ?? throw new InvalidOperationException("Producto no encontrado");
        producto.Activo = !producto.Activo;
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<SelectListItem>> GetSelectListActivosAsync() =>
        await _db.Productos
            .Include(p => p.Categoria)
            .Where(p => p.Activo)
            .OrderBy(p => p.Categoria.Nombre).ThenBy(p => p.Nombre)
            .Select(p => new SelectListItem($"{p.Nombre} (${p.PrecioUnitario:N2})", p.Id.ToString()))
            .ToListAsync();

    public async Task<Dictionary<int, decimal>> GetPreciosDictionaryAsync() =>
        await _db.Productos
            .Where(p => p.Activo)
            .ToDictionaryAsync(p => p.Id, p => p.PrecioUnitario);
}
