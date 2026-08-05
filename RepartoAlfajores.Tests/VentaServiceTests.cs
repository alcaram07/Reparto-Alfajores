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

    // ── Edición ──────────────────────────────────────────────────────────────

    private VentaViewModel EditarVm(int ventaId, MetodoPago metodo, params (int ProductoId, int Cantidad)[] lineas) => new()
    {
        Id = ventaId,
        ClienteId = Datos.ClienteId,
        MetodoPago = metodo,
        Detalles = lineas.Select(l => new DetalleVentaViewModel
        {
            ProductoId = l.ProductoId,
            Cantidad = l.Cantidad
        }).ToList()
    };

    [Fact]
    public async Task Editar_la_cantidad_actualiza_el_total_y_el_cargo_conservando_el_movimiento()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente, cantidad: 2)); // 1000
        var movimientoId = (await db.MovimientosCC.SingleAsync(m => m.VentaId == venta.Id)).Id;

        Assert.True(await service.UpdateAsync(
            EditarVm(venta.Id, MetodoPago.CuentaCorriente, (Datos.ProductoId, 3))));

        await using var verificacion = Fixture.CreateContext();
        var actualizada = await verificacion.Ventas.SingleAsync(v => v.Id == venta.Id);
        Assert.Equal(1500m, actualizada.Total);

        var movimiento = await verificacion.MovimientosCC.SingleAsync(m => m.VentaId == venta.Id);
        // Conservar el Id es lo que mantiene el cargo en su lugar dentro del libro mayor:
        // RecalcularSaldosAsync ordena por Id.
        Assert.Equal(movimientoId, movimiento.Id);
        Assert.Equal(1500m, movimiento.Monto);
        Assert.Equal(1500m, movimiento.SaldoAcumulado);
    }

    /// <summary>
    /// El caso que más puede corromper el libro mayor: editar un cargo que tiene movimientos
    /// posteriores. Si se borrara y recreara, el cargo saltaría al final de la cadena.
    /// </summary>
    [Fact]
    public async Task Editar_una_venta_intermedia_recalcula_la_cadena_sin_reordenarla()
    {
        await using var db = Fixture.CreateContext();
        var ventas = NuevaVenta(db);
        var cobros = NuevoCobro(db);

        var primera = await ventas.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente, cantidad: 2)); // 1000
        await ventas.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente, cantidad: 1));                // 500 → 1500
        await cobros.CreateAsync(new CobroViewModel
        {
            ClienteId = Datos.ClienteId,
            Monto = 200m,
            MetodoPago = MetodoPago.Efectivo
        });                                                                                            // → 1300

        // La primera venta pasa de 1000 a 500.
        Assert.True(await ventas.UpdateAsync(
            EditarVm(primera.Id, MetodoPago.CuentaCorriente, (Datos.ProductoId, 1))));

        await using var verificacion = Fixture.CreateContext();
        var saldos = await verificacion.MovimientosCC
            .Where(m => m.ClienteId == Datos.ClienteId)
            .OrderBy(m => m.Id)
            .Select(m => m.SaldoAcumulado)
            .ToListAsync();

        Assert.Equal(new[] { 500m, 1000m, 800m }, saldos);
    }

    [Fact]
    public async Task Pasar_una_venta_de_cuenta_corriente_a_contado_borra_el_cargo()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente));

        Assert.True(await service.UpdateAsync(
            EditarVm(venta.Id, MetodoPago.Efectivo, (Datos.ProductoId, 2))));

        await using var verificacion = Fixture.CreateContext();
        Assert.False(await verificacion.MovimientosCC.AnyAsync(m => m.VentaId == venta.Id));
        Assert.Equal(0m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
        Assert.Equal(EstadoCobro.Cobrado,
            (await verificacion.Ventas.SingleAsync(v => v.Id == venta.Id)).EstadoCobro);
    }

    [Fact]
    public async Task Pasar_una_venta_de_contado_a_cuenta_corriente_crea_el_cargo()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.Efectivo));

        Assert.True(await service.UpdateAsync(
            EditarVm(venta.Id, MetodoPago.CuentaCorriente, (Datos.ProductoId, 2))));

        await using var verificacion = Fixture.CreateContext();
        var movimiento = await verificacion.MovimientosCC.SingleAsync(m => m.VentaId == venta.Id);
        Assert.Equal(TipoMovimientoCC.Cargo, movimiento.Tipo);
        Assert.Equal(1000m, movimiento.Monto);
        Assert.Equal(1000m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
    }

    /// <summary>
    /// Si al editar se tomara el precio actual del catálogo, cambiar una cantidad reescribiría
    /// el importe de una venta ya cobrada.
    /// </summary>
    [Fact]
    public async Task Editar_conserva_el_precio_congelado_de_las_lineas_que_ya_estaban()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.Efectivo, cantidad: 2)); // 2 × 500

        var producto = await db.Productos.SingleAsync(p => p.Id == Datos.ProductoId);
        producto.PrecioUnitario = 800m;
        await db.SaveChangesAsync();

        Assert.True(await service.UpdateAsync(
            EditarVm(venta.Id, MetodoPago.Efectivo, (Datos.ProductoId, 3))));

        await using var verificacion = Fixture.CreateContext();
        var detalle = await verificacion.DetalleVentas.SingleAsync(d => d.VentaId == venta.Id);
        Assert.Equal(Datos.PrecioProducto, detalle.PrecioUnitario);
        Assert.Equal(1500m, (await verificacion.Ventas.SingleAsync(v => v.Id == venta.Id)).Total);
    }

    [Fact]
    public async Task Una_linea_agregada_al_editar_toma_el_precio_actual_del_catalogo()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.Efectivo, cantidad: 1));

        var otro = new Producto
        {
            Nombre = "Producto nuevo",
            CategoriaId = (await db.CategoriaProductos.FirstAsync()).Id,
            PrecioUnitario = 250m,
            Activo = true
        };
        db.Productos.Add(otro);
        await db.SaveChangesAsync();

        Assert.True(await service.UpdateAsync(
            EditarVm(venta.Id, MetodoPago.Efectivo, (Datos.ProductoId, 1), (otro.Id, 2))));

        await using var verificacion = Fixture.CreateContext();
        var nueva = await verificacion.DetalleVentas
            .SingleAsync(d => d.VentaId == venta.Id && d.ProductoId == otro.Id);
        Assert.Equal(250m, nueva.PrecioUnitario);
        Assert.Equal(1000m, (await verificacion.Ventas.SingleAsync(v => v.Id == venta.Id)).Total);
    }

    [Fact]
    public async Task No_se_puede_cambiar_el_cliente_al_editar()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente));

        var vm = EditarVm(venta.Id, MetodoPago.CuentaCorriente, (Datos.ProductoId, 2));
        vm.ClienteId = Datos.ClienteId2;

        await Assert.ThrowsAsync<NegocioException>(() => service.UpdateAsync(vm));
    }

    [Fact]
    public async Task Editar_dejando_la_venta_sin_productos_es_rechazado()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente));

        await Assert.ThrowsAsync<NegocioException>(
            () => service.UpdateAsync(EditarVm(venta.Id, MetodoPago.CuentaCorriente)));
    }

    [Fact]
    public async Task Editar_con_un_producto_inexistente_no_persiste_nada()
    {
        await using var db = Fixture.CreateContext();
        var service = NuevaVenta(db);
        var venta = await service.CreateAsync(NuevaVentaVm(MetodoPago.CuentaCorriente, cantidad: 2));

        await Assert.ThrowsAsync<NegocioException>(() => service.UpdateAsync(
            EditarVm(venta.Id, MetodoPago.CuentaCorriente, (Datos.ProductoId, 5), (999_999, 1))));

        await using var verificacion = Fixture.CreateContext();
        var sinCambios = await verificacion.Ventas.SingleAsync(v => v.Id == venta.Id);
        Assert.Equal(1000m, sinCambios.Total);
        Assert.Equal(1000m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Editar_una_venta_inexistente_devuelve_false()
    {
        await using var db = Fixture.CreateContext();

        Assert.False(await NuevaVenta(db).UpdateAsync(
            EditarVm(999_999, MetodoPago.Efectivo, (Datos.ProductoId, 1))));
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
