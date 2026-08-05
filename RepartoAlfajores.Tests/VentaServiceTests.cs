using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Tests;

public class VentaServiceTests : DbTestBase
{
    public VentaServiceTests(PostgresFixture fixture) : base(fixture) { }

    private VentaViewModel NuevaVentaVm(MetodoPago metodo, int cantidad = 2) => new()
    {
        ClienteId = Datos.ClienteId,
        MetodoPago = metodo,
        Detalles = [new DetalleVentaViewModel { ProductoId = Datos.ProductoId, Cantidad = cantidad }]
    };

    [Fact]
    public async Task Venta_en_cuenta_corriente_genera_un_cargo_por_el_total()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);

        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente));

        Assert.Equal(1000m, venta.Total);
        Assert.Equal(EstadoCobro.CuentaCorriente, venta.EstadoCobro);

        var movimiento = await db.MovimientosCC.SingleAsync(m => m.VentaId == venta.Id);
        Assert.Equal(TipoMovimientoCC.Cargo, movimiento.Tipo);
        Assert.Equal(1000m, movimiento.Monto);
        Assert.Equal(1000m, movimiento.SaldoAcumulado);
    }

    [Theory]
    [InlineData(MetodoPago.Efectivo)]
    [InlineData(MetodoPago.Transferencia)]
    [InlineData(MetodoPago.QR)]
    public async Task Venta_cobrada_al_contado_no_genera_movimiento_de_cuenta_corriente(MetodoPago metodo)
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);

        var venta = await service.CreateAsync(NuevaVentaVm(metodo));

        Assert.Equal(EstadoCobro.Cobrado, venta.EstadoCobro);
        Assert.False(await db.MovimientosCC.AnyAsync(m => m.ClienteId == Datos.ClienteId));
    }

    [Fact]
    public async Task El_precio_del_detalle_se_congela_al_momento_de_la_venta()
    {
        await using var db = Fixture.CreateContext();
        var venta = await NuevaVenta(db).CreateAsync(NuevaVentaVm(MetodoPago.Efectivo));

        var producto = await db.Productos.SingleAsync(p => p.Id == Datos.ProductoId);
        producto.PrecioUnitario = 9999m;
        await db.SaveChangesAsync();

        var detalle = await db.DetalleVentas.SingleAsync(d => d.VentaId == venta.Id);
        Assert.Equal(Datos.PrecioProducto, detalle.PrecioUnitario);
    }

    /// <summary>
    /// Regresión del fix de atomicidad: la venta y su cargo se guardaban con dos SaveChanges
    /// sueltos, así que si el segundo fallaba quedaba la venta registrada y la deuda perdida.
    /// Se simula ese fallo exacto —ya persistida la venta— y se exige que la transacción
    /// revierta todo. Sin transacción, este test deja una venta huérfana.
    /// </summary>
    [Fact]
    public async Task Si_falla_el_cargo_en_cuenta_corriente_no_queda_la_venta_registrada()
    {
        await using var db = Fixture.CreateContext();
        var cuentaCorrienteRota = new CuentaCorrienteQueFallaAlRegistrar(NuevoCuentaCorriente(db));
        var service = new RepartoAlfajores.Services.Implementations.VentaService(db, cuentaCorrienteRota);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente)));

        await using var verificacion = Fixture.CreateContext();
        Assert.False(await verificacion.Ventas.AnyAsync());
        Assert.False(await verificacion.DetalleVentas.AnyAsync());
        Assert.False(await verificacion.MovimientosCC.AnyAsync());
    }

    [Fact]
    public async Task Una_venta_con_producto_inexistente_no_deja_registros_a_medias()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);

        var vm = new VentaViewModel
        {
            ClienteId = Datos.ClienteId,
            MetodoPago = MetodoPago.CuentaCorriente,
            Detalles =
            [
                new DetalleVentaViewModel { ProductoId = Datos.ProductoId, Cantidad = 1 },
                new DetalleVentaViewModel { ProductoId = 999_999, Cantidad = 1 }
            ]
        };

        await Assert.ThrowsAsync<NegocioException>(() => service.CreateAsync(vm));

        await using var verificacion = Fixture.CreateContext();
        Assert.False(await verificacion.Ventas.AnyAsync());
        Assert.False(await verificacion.MovimientosCC.AnyAsync());
    }

    /// <summary>
    /// Regresión: <c>MovimientosCC</c> referencia la venta con FK RESTRICT, así que borrar una
    /// venta de cuenta corriente terminaba en DbUpdateException (error 500 en pantalla).
    /// </summary>
    [Fact]
    public async Task Se_puede_eliminar_una_venta_de_cuenta_corriente()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente));

        var eliminada = await service.DeleteAsync(venta.Id);

        Assert.True(eliminada);

        await using var verificacion = Fixture.CreateContext();
        Assert.False(await verificacion.Ventas.AnyAsync(v => v.Id == venta.Id));
        Assert.False(await verificacion.MovimientosCC.AnyAsync(m => m.VentaId == venta.Id));
        Assert.Equal(0m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
    }

    /// <summary>
    /// Regresión: al eliminar una venta del medio, los saldos acumulados posteriores quedaban
    /// calculados sobre una deuda que ya no existe.
    /// </summary>
    [Fact]
    public async Task Eliminar_una_venta_recalcula_los_saldos_posteriores()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var cc = NuevoCuentaCorriente(db);

        var primera = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente, cantidad: 2)); // 1000
        await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente, cantidad: 1));               // 500
        Assert.Equal(1500m, await cc.GetSaldoAsync(Datos.ClienteId));

        await service.DeleteAsync(primera.Id);

        await using var verificacion = Fixture.CreateContext();
        var saldos = await verificacion.MovimientosCC
            .Where(m => m.ClienteId == Datos.ClienteId)
            .OrderBy(m => m.Id)
            .Select(m => m.SaldoAcumulado)
            .ToListAsync();

        Assert.Equal(new[] { 500m }, saldos);
        Assert.Equal(500m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Eliminar_una_venta_al_contado_no_toca_la_cuenta_corriente()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var cc = NuevoCuentaCorriente(db);

        await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente)); // 1000 en CC
        var contado = await service.CreateAsync(NuevaVentaVm(MetodoPago.Efectivo));

        Assert.True(await service.DeleteAsync(contado.Id));
        Assert.Equal(1000m, await cc.GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Eliminar_una_venta_inexistente_devuelve_false()
    {
        await using var db = Fixture.CreateContext();

        Assert.False(await NuevaVenta(db).DeleteAsync(999_999));
    }

    /// <summary>
    /// Delega todo al servicio real salvo el alta del movimiento, que falla. Reproduce el
    /// momento exacto en que se rompía la consistencia: venta ya guardada, cargo no.
    /// </summary>
    private sealed class CuentaCorrienteQueFallaAlRegistrar(ICuentaCorrienteService inner)
        : ICuentaCorrienteService
    {
        public Task<decimal> GetSaldoAsync(int clienteId) => inner.GetSaldoAsync(clienteId);

        public Task BloquearClienteAsync(int clienteId) => inner.BloquearClienteAsync(clienteId);

        public Task RecalcularSaldosAsync(int clienteId) => inner.RecalcularSaldosAsync(clienteId);

        public Task<MovimientoCC> RegistrarMovimientoAsync(
            int clienteId, TipoMovimientoCC tipo, decimal monto, string descripcion,
            DateTime fecha, int? ventaId = null, int? cobroId = null) =>
            throw new InvalidOperationException("fallo simulado al registrar el cargo");
    }
}
