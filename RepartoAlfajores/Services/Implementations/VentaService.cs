using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.Utils;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Services.Implementations;

public class VentaService : IVentaService
{
    private readonly AppDbContext _db;
    private readonly ICuentaCorrienteService _cuentaCorriente;

    public VentaService(AppDbContext db, ICuentaCorrienteService cuentaCorriente)
    {
        _db = db;
        _cuentaCorriente = cuentaCorriente;
    }

    public async Task<IEnumerable<Venta>> GetAllAsync(DateTime? fecha = null, string? busqueda = null, EstadoCobro? estado = null, int? zonaId = null)
    {
        var q = _db.Ventas
            .Include(v => v.Cliente).ThenInclude(c => c.Zona)
            .Include(v => v.Detalles).ThenInclude(d => d.Producto)
            .AsQueryable();

        var (desde, hasta) = FechaAr.RangoDia(fecha ?? FechaAr.Hoy);
        q = q.Where(v => v.Fecha >= desde && v.Fecha < hasta);

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
                ?? throw new NegocioException($"Producto {d.ProductoId} no encontrado");

            venta.Detalles.Add(new DetalleVenta
            {
                ProductoId = d.ProductoId,
                Cantidad = d.Cantidad,
                PrecioUnitario = producto.PrecioUnitario
            });
        }

        venta.Total = venta.Detalles.Sum(d => d.Cantidad * d.PrecioUnitario);

        // La venta y su cargo en cuenta corriente tienen que persistirse juntos: si sólo
        // entrara la venta, la deuda del cliente se perdería sin dejar rastro.
        // EnableRetryOnFailure exige abrir la transacción a través de la execution strategy.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            if (venta.EstadoCobro == EstadoCobro.CuentaCorriente)
                await _cuentaCorriente.BloquearClienteAsync(venta.ClienteId);

            _db.Ventas.Add(venta);
            await _db.SaveChangesAsync();

            if (venta.EstadoCobro == EstadoCobro.CuentaCorriente)
            {
                await _cuentaCorriente.RegistrarMovimientoAsync(
                    venta.ClienteId, TipoMovimientoCC.Cargo, venta.Total,
                    $"Venta #{venta.Id}", venta.Fecha, ventaId: venta.Id);
            }

            await tx.CommitAsync();
        });

        return venta;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var venta = await _db.Ventas.Include(v => v.Detalles).FirstOrDefaultAsync(v => v.Id == id);
            if (venta == null) return false;

            var clienteId = venta.ClienteId;
            await _cuentaCorriente.BloquearClienteAsync(clienteId);

            // MovimientosCC referencia la venta con FK Restrict: sin borrar el movimiento
            // primero, Postgres rechaza el DELETE y la operación termina en error 500.
            var movimiento = await _db.MovimientosCC.FirstOrDefaultAsync(m => m.VentaId == id);
            var eraCuentaCorriente = movimiento != null;
            if (movimiento != null)
                _db.MovimientosCC.Remove(movimiento);

            _db.Ventas.Remove(venta);
            await _db.SaveChangesAsync();

            // Al sacar un movimiento del medio hay que rehacer la cadena de saldos posteriores.
            if (eraCuentaCorriente)
                await _cuentaCorriente.RecalcularSaldosAsync(clienteId);

            await tx.CommitAsync();
            return true;
        });
    }
}
