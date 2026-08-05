using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services;
using RepartoAlfajores.Services.Interfaces;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Tests;

public class CobroServiceTests : DbTestBase
{
    public CobroServiceTests(PostgresFixture fixture) : base(fixture) { }

    private CobroViewModel NuevoCobroVm(decimal monto, int? clienteId = null) => new()
    {
        ClienteId = clienteId ?? Datos.ClienteId,
        Monto = monto,
        MetodoPago = MetodoPago.Efectivo
    };

    private VentaViewModel VentaCC(int cantidad) => new()
    {
        ClienteId = Datos.ClienteId,
        MetodoPago = MetodoPago.CuentaCorriente,
        Detalles = [new DetalleVentaViewModel { ProductoId = Datos.ProductoId, Cantidad = cantidad }]
    };

    [Fact]
    public async Task Un_cobro_genera_un_abono_y_reduce_la_deuda()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2)); // deuda 1000

        var cobro = await NuevoCobro(db).CreateAsync(NuevoCobroVm(400m));

        var movimiento = await db.MovimientosCC.SingleAsync(m => m.CobroId == cobro.Id);
        Assert.Equal(TipoMovimientoCC.Abono, movimiento.Tipo);
        Assert.Equal(600m, movimiento.SaldoAcumulado);
        Assert.Equal(600m, await NuevoCuentaCorriente(db).GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Un_cobro_que_cancela_la_deuda_deja_el_saldo_en_cero()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2)); // deuda 1000

        await NuevoCobro(db).CreateAsync(NuevoCobroVm(1000m));

        Assert.Equal(0m, await NuevoCuentaCorriente(db).GetSaldoAsync(Datos.ClienteId));
    }

    /// <summary>Regresión del <c>Math.Max(0, …)</c>: el excedente se perdía en silencio.</summary>
    [Fact]
    public async Task Un_cobro_mayor_a_la_deuda_deja_credito_a_favor()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2)); // deuda 1000

        await NuevoCobro(db).CreateAsync(NuevoCobroVm(1200m));

        Assert.Equal(-200m, await NuevoCuentaCorriente(db).GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Un_cliente_con_credito_a_favor_no_figura_como_deudor()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2));
        var service = NuevoCobro(db);
        await service.CreateAsync(NuevoCobroVm(1200m));

        var deudores = await service.GetDeudoresAsync();

        Assert.DoesNotContain(deudores, d => d.ClienteId == Datos.ClienteId);
    }

    /// <summary>Un saldo a favor no debe descontar de lo que adeuda el resto de los clientes.</summary>
    [Fact]
    public async Task El_total_por_cobrar_ignora_los_saldos_a_favor()
    {
        await using var db = Fixture.CreateContext();
        var ventas = NuevaVenta(db);
        var cobros = NuevoCobro(db);
        var cc = NuevoCuentaCorriente(db);

        await ventas.CreateAsync(VentaCC(2));                       // cliente 1 debe 1000
        await cobros.CreateAsync(NuevoCobroVm(1500m));              // queda con 500 a favor
        await cc.RegistrarMovimientoAsync(                          // cliente 2 debe 800
            Datos.ClienteId2, TipoMovimientoCC.Cargo, 800m, "Venta", DateTime.UtcNow);

        Assert.Equal(-500m, await cc.GetSaldoAsync(Datos.ClienteId));
        Assert.Equal(800m, await cobros.GetTotalPorCobrarAsync());
    }

    [Fact]
    public async Task Un_deudor_aparece_con_su_saldo_y_su_zona()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2));

        var deudores = (await NuevoCobro(db).GetDeudoresAsync()).ToList();

        var deudor = Assert.Single(deudores);
        Assert.Equal(Datos.ClienteId, deudor.ClienteId);
        Assert.Equal(1000m, deudor.Saldo);
        Assert.Equal("Centro", deudor.Zona);
    }

    /// <summary>
    /// El caso que motivó el arreglo: un cliente que compra a cuenta desde hace mucho pero paga
    /// puntual aparecía con cientos de días de mora y encabezaba la lista de deudores.
    /// </summary>
    [Fact]
    public async Task Los_dias_de_deuda_no_cuentan_las_deudas_ya_saldadas()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "V1", DateTime.UtcNow.AddDays(-200));
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 1000m, "C1", DateTime.UtcNow.AddDays(-190));
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 400m, "V2", DateTime.UtcNow.AddDays(-3));

        var deudor = Assert.Single(await NuevoCobro(db).GetDeudoresAsync());

        Assert.Equal(400m, deudor.Saldo);
        Assert.InRange(deudor.DiasDeuda, 2, 4); // no 200
    }

    [Fact]
    public async Task El_deudor_mas_antiguo_encabeza_la_lista()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 500m, "V1", DateTime.UtcNow.AddDays(-2));
        await cc.RegistrarMovimientoAsync(Datos.ClienteId2, TipoMovimientoCC.Cargo, 300m, "V2", DateTime.UtcNow.AddDays(-45));

        var deudores = (await NuevoCobro(db).GetDeudoresAsync()).ToList();

        Assert.Equal(Datos.ClienteId2, deudores[0].ClienteId);
        Assert.InRange(deudores[0].DiasDeuda, 44, 46);
    }

    [Fact]
    public async Task Cobrar_a_un_cliente_inexistente_falla_con_mensaje_claro()
    {
        await using var db = Fixture.CreateContext();

        var ex = await Assert.ThrowsAsync<NegocioException>(
            () => NuevoCobro(db).CreateAsync(NuevoCobroVm(100m, clienteId: 999_999)));

        Assert.Contains("no existe", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task Un_cobro_de_monto_no_positivo_es_rechazado(decimal monto)
    {
        await using var db = Fixture.CreateContext();

        await Assert.ThrowsAsync<NegocioException>(
            () => NuevoCobro(db).CreateAsync(NuevoCobroVm(monto)));

        await using var verificacion = Fixture.CreateContext();
        Assert.False(await verificacion.Cobros.AnyAsync());
        Assert.False(await verificacion.MovimientosCC.AnyAsync());
    }

    [Fact]
    public async Task El_cobro_y_su_abono_se_guardan_juntos()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2));
        await NuevoCobro(db).CreateAsync(NuevoCobroVm(300m));

        await using var verificacion = Fixture.CreateContext();
        var cobro = await verificacion.Cobros.SingleAsync();
        Assert.True(await verificacion.MovimientosCC.AnyAsync(m => m.CobroId == cobro.Id));
    }

    /// <summary>
    /// Regresión del fix de atomicidad: sin transacción, un fallo al registrar el abono dejaba
    /// el cobro guardado y al cliente debiendo plata que ya había pagado.
    /// </summary>
    [Fact]
    public async Task Si_falla_el_abono_no_queda_el_cobro_registrado()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2)); // deuda 1000

        var cuentaCorrienteRota = new CuentaCorrienteQueFallaAlRegistrar(NuevoCuentaCorriente(db));
        var service = new RepartoAlfajores.Services.Implementations.CobroService(db, cuentaCorrienteRota);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(NuevoCobroVm(400m)));

        await using var verificacion = Fixture.CreateContext();
        Assert.False(await verificacion.Cobros.AnyAsync());
        Assert.Equal(1000m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Eliminar_un_cobro_devuelve_la_deuda_al_valor_previo()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2)); // deuda 1000
        var service = NuevoCobro(db);
        var cobro = await service.CreateAsync(NuevoCobroVm(400m));
        Assert.Equal(600m, await NuevoCuentaCorriente(db).GetSaldoAsync(Datos.ClienteId));

        Assert.True(await service.DeleteAsync(cobro.Id));

        await using var verificacion = Fixture.CreateContext();
        Assert.False(await verificacion.Cobros.AnyAsync(c => c.Id == cobro.Id));
        Assert.False(await verificacion.MovimientosCC.AnyAsync(m => m.CobroId == cobro.Id));
        Assert.Equal(1000m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
    }

    /// <summary>
    /// Al sacar un abono del medio, los saldos posteriores quedan calculados sobre un pago
    /// que ya no existe.
    /// </summary>
    [Fact]
    public async Task Eliminar_un_cobro_del_medio_recalcula_la_cadena()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaCC(2)); // cargo 1000
        var service = NuevoCobro(db);
        var primero = await service.CreateAsync(NuevoCobroVm(300m)); // saldo 700
        await service.CreateAsync(NuevoCobroVm(200m));               // saldo 500

        Assert.True(await service.DeleteAsync(primero.Id));

        await using var verificacion = Fixture.CreateContext();
        var saldos = await verificacion.MovimientosCC
            .Where(m => m.ClienteId == Datos.ClienteId)
            .OrderBy(m => m.Id)
            .Select(m => m.SaldoAcumulado)
            .ToListAsync();

        Assert.Equal(new[] { 1000m, 800m }, saldos);
        Assert.Equal(800m, await NuevoCuentaCorriente(verificacion).GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Eliminar_un_cobro_inexistente_devuelve_false()
    {
        await using var db = Fixture.CreateContext();

        Assert.False(await NuevoCobro(db).DeleteAsync(999_999));
    }

    [Fact]
    public async Task Eliminar_un_cobro_no_altera_el_saldo_de_otro_cliente()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);
        var service = NuevoCobro(db);

        await NuevaVenta(db).CreateAsync(VentaCC(2));                    // cliente 1 debe 1000
        var cobro = await service.CreateAsync(NuevoCobroVm(400m));
        await cc.RegistrarMovimientoAsync(                               // cliente 2 debe 800
            Datos.ClienteId2, TipoMovimientoCC.Cargo, 800m, "Venta", DateTime.UtcNow);

        await service.DeleteAsync(cobro.Id);

        await using var verificacion = Fixture.CreateContext();
        var ccVerif = NuevoCuentaCorriente(verificacion);
        Assert.Equal(1000m, await ccVerif.GetSaldoAsync(Datos.ClienteId));
        Assert.Equal(800m, await ccVerif.GetSaldoAsync(Datos.ClienteId2));
    }

    private sealed class CuentaCorrienteQueFallaAlRegistrar(ICuentaCorrienteService inner)
        : ICuentaCorrienteService
    {
        public Task<decimal> GetSaldoAsync(int clienteId) => inner.GetSaldoAsync(clienteId);

        public Task<Dictionary<int, decimal>> GetSaldosAsync() => inner.GetSaldosAsync();

        public Task<Dictionary<int, DateTime>> GetInicioDeudaAsync() => inner.GetInicioDeudaAsync();

        public Task BloquearClienteAsync(int clienteId) => inner.BloquearClienteAsync(clienteId);

        public Task RecalcularSaldosAsync(int clienteId) => inner.RecalcularSaldosAsync(clienteId);

        public Task<MovimientoCC> RegistrarMovimientoAsync(
            int clienteId, TipoMovimientoCC tipo, decimal monto, string descripcion,
            DateTime fecha, int? ventaId = null, int? cobroId = null) =>
            throw new InvalidOperationException("fallo simulado al registrar el abono");
    }
}
