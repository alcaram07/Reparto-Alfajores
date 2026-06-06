using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class ClienteService : IClienteService
{
    private readonly AppDbContext _db;
    public ClienteService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Cliente>> GetAllAsync(string? busqueda = null, int? zonaId = null, string? estadoDeuda = null)
    {
        var q = _db.Clientes.Include(c => c.Zona).AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            q = q.Where(c => c.Nombre.Contains(busqueda) || (c.Telefono != null && c.Telefono.Contains(busqueda)));

        if (zonaId.HasValue)
            q = q.Where(c => c.ZonaId == zonaId);

        var clientes = await q.OrderBy(c => c.Nombre).ToListAsync();

        if (estadoDeuda == "conDeuda" || estadoDeuda == "sinDeuda")
        {
            var saldos = new Dictionary<int, decimal>();
            foreach (var c in clientes)
                saldos[c.Id] = await GetSaldoPendienteAsync(c.Id);

            clientes = estadoDeuda == "conDeuda"
                ? clientes.Where(c => saldos[c.Id] > 0).ToList()
                : clientes.Where(c => saldos[c.Id] <= 0).ToList();
        }

        return clientes;
    }

    public async Task<Cliente?> GetByIdAsync(int id) =>
        await _db.Clientes.Include(c => c.Zona).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Cliente> CreateAsync(ClienteViewModel vm)
    {
        var cliente = new Cliente
        {
            Nombre = vm.Nombre.Trim(),
            Telefono = vm.Telefono?.Trim(),
            Direccion = vm.Direccion?.Trim(),
            ZonaId = vm.ZonaId,
            Activo = vm.Activo,
            FechaAlta = DateTime.UtcNow
        };
        _db.Clientes.Add(cliente);
        await _db.SaveChangesAsync();
        return cliente;
    }

    public async Task UpdateAsync(ClienteViewModel vm)
    {
        var cliente = await _db.Clientes.FindAsync(vm.Id)
            ?? throw new InvalidOperationException("Cliente no encontrado");
        cliente.Nombre = vm.Nombre.Trim();
        cliente.Telefono = vm.Telefono?.Trim();
        cliente.Direccion = vm.Direccion?.Trim();
        cliente.ZonaId = vm.ZonaId;
        cliente.Activo = vm.Activo;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ToggleActivoAsync(int id)
    {
        var cliente = await _db.Clientes.FindAsync(id);
        if (cliente == null) return false;
        cliente.Activo = !cliente.Activo;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<decimal> GetSaldoPendienteAsync(int clienteId)
    {
        var totalVentasCC = await _db.Ventas
            .Where(v => v.ClienteId == clienteId && v.EstadoCobro == EstadoCobro.CuentaCorriente)
            .SumAsync(v => (decimal?)v.Total) ?? 0;

        var totalCobros = await _db.Cobros
            .Where(c => c.ClienteId == clienteId)
            .SumAsync(c => (decimal?)c.Monto) ?? 0;

        return totalVentasCC - totalCobros;
    }

    public async Task<IEnumerable<Venta>> GetVentasByClienteAsync(int clienteId) =>
        await _db.Ventas
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .Where(v => v.ClienteId == clienteId)
            .OrderByDescending(v => v.Fecha)
            .ToListAsync();

    public async Task<IEnumerable<Cobro>> GetCobrosByClienteAsync(int clienteId) =>
        await _db.Cobros
            .Where(c => c.ClienteId == clienteId)
            .OrderByDescending(c => c.Fecha)
            .ToListAsync();

    public async Task<IEnumerable<SelectListItem>> GetSelectListAsync() =>
        await _db.Clientes
            .Include(c => c.Zona)
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .Select(c => new SelectListItem($"{c.Nombre} ({c.Zona.Nombre})", c.Id.ToString()))
            .ToListAsync();

    public async Task<IEnumerable<SelectListItem>> GetSelectListConDeudaAsync()
    {
        var clientes = await _db.Clientes
            .Include(c => c.Zona)
            .Where(c => c.Activo)
            .OrderBy(c => c.Nombre)
            .ToListAsync();

        var result = new List<SelectListItem>();
        foreach (var c in clientes)
        {
            var saldo = await GetSaldoPendienteAsync(c.Id);
            if (saldo > 0)
                result.Add(new SelectListItem($"{c.Nombre} (debe ${saldo:N2})", c.Id.ToString()));
        }
        return result;
    }
}
