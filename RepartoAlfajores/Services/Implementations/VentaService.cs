using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class VentaService : IVentaService
{
    private readonly AppDbContext _db;
    public VentaService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<Venta>> GetAllAsync(DateTime? fecha = null, string? busqueda = null, EstadoCobro? estado = null, int? zonaId = null)
    {
        var q = _db.Ventas
            .Include(v => v.Cliente).ThenInclude(c => c.Zona)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .AsQueryable();

        var fechaFiltro = fecha ?? DateTime.UtcNow.Date;
        var siguiente = fechaFiltro.Date.AddDays(1);
        q = q.Where(v => v.Fecha >= fechaFiltro.Date && v.Fecha < siguiente);

        if (!string.IsNullOrWhiteSpace(busqueda))
            q = q.Where(v => v.Cliente.Nombre.Contains(busqueda));

        if (estado.HasValue)
            q = q.Where(v => v.EstadoCobro == estado);

        if (zonaId.HasValue)
            q = q.Where(v => v.Cliente.ZonaId == zonaId);

        return await q.OrderByDescending(v => v.Fecha).ToListAsync();
    }

    public async Task<Venta?> GetByIdAsync(int id) =>
        await _db.Ventas
            .Include(v => v.Cliente).ThenInclude(c => c.Zona)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .FirstOrDefaultAsync(v => v.Id == id);

    public async Task<Venta> CreateAsync(VentaViewModel vm)
    {
        var venta = new Venta
        {
            ClienteId = vm.ClienteId,
            Fecha = DateTime.UtcNow,
            MetodoPago = vm.MetodoPago,
            EstadoCobro = vm.MetodoPago == MetodoPago.CuentaCorriente
                ? EstadoCobro.CuentaCorriente
                : EstadoCobro.Cobrado,
            Nota = vm.Nota?.Trim(),
            Detalles = new List<DetalleVenta>()
        };

        foreach (var d in vm.Detalles)
        {
            var producto = await _db.Productos.FindAsync(d.ProductoId)
                ?? throw new InvalidOperationException($"Producto {d.ProductoId} no encontrado");

            venta.Detalles.Add(new DetalleVenta
            {
                ProductoId = d.ProductoId,
                Cantidad = d.Cantidad,
                PrecioUnitario = producto.PrecioUnitario
            });
        }

        venta.Total = venta.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);
        _db.Ventas.Add(venta);
        await _db.SaveChangesAsync();

        if (venta.EstadoCobro == EstadoCobro.CuentaCorriente)
        {
            var saldoPrevio = await _db.MovimientosCC
                .Where(m => m.ClienteId == venta.ClienteId)
                .OrderByDescending(m => m.Id)
                .Select(m => (decimal?)m.SaldoAcumulado)
                .FirstOrDefaultAsync() ?? 0m;

            _db.MovimientosCC.Add(new MovimientoCC
            {
                ClienteId = venta.ClienteId,
                Fecha = venta.Fecha,
                Tipo = TipoMovimientoCC.Cargo,
                Monto = venta.Total,
                SaldoAcumulado = saldoPrevio + venta.Total,
                Descripcion = $"Venta #{venta.Id}",
                VentaId = venta.Id
            });
            await _db.SaveChangesAsync();
        }

        return venta;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var venta = await _db.Ventas.Include(v => v.Detalles).FirstOrDefaultAsync(v => v.Id == id);
        if (venta == null) return false;
        _db.Ventas.Remove(venta);
        await _db.SaveChangesAsync();
        return true;
    }
}
