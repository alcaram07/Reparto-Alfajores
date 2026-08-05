using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Models;

namespace RepartoAlfajores.Tests;

public class CuentaCorrienteServiceTests : DbTestBase
{
    public CuentaCorrienteServiceTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Saldo_de_cliente_sin_movimientos_es_cero()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        Assert.Equal(0m, await cc.GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Un_cargo_aumenta_el_saldo()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(
            Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "Venta test", DateTime.UtcNow);

        Assert.Equal(1000m, await cc.GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Un_abono_reduce_el_saldo()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "Venta", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 300m, "Cobro", DateTime.UtcNow);

        Assert.Equal(700m, await cc.GetSaldoAsync(Datos.ClienteId));
    }

    /// <summary>
    /// Regresión: antes se guardaba <c>Math.Max(0, saldo - monto)</c>, así que un pago mayor
    /// a la deuda dejaba el saldo en cero y el excedente desaparecía del libro mayor.
    /// </summary>
    [Fact]
    public async Task Un_abono_mayor_a_la_deuda_deja_saldo_negativo_como_credito_a_favor()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 900m, "Venta", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 1000m, "Cobro", DateTime.UtcNow);

        Assert.Equal(-100m, await cc.GetSaldoAsync(Datos.ClienteId));
    }

    /// <summary>El libro mayor tiene que poder auditarse: saldo previo ± monto == saldo nuevo.</summary>
    [Fact]
    public async Task La_cadena_de_saldos_es_consistente_movimiento_a_movimiento()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "V1", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 500m, "V2", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 200m, "C1", DateTime.UtcNow);

        var movimientos = await db.MovimientosCC
            .Where(m => m.ClienteId == Datos.ClienteId)
            .OrderBy(m => m.Id)
            .ToListAsync();

        var esperado = 0m;
        foreach (var m in movimientos)
        {
            esperado += m.Tipo == TipoMovimientoCC.Cargo ? m.Monto : -m.Monto;
            Assert.Equal(esperado, m.SaldoAcumulado);
        }

        Assert.Equal(new[] { 1000m, 1500m, 1300m }, movimientos.Select(m => m.SaldoAcumulado));
    }

    [Fact]
    public async Task Los_saldos_de_clientes_distintos_no_se_mezclan()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "V1", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId2, TipoMovimientoCC.Cargo, 250m, "V2", DateTime.UtcNow);

        Assert.Equal(1000m, await cc.GetSaldoAsync(Datos.ClienteId));
        Assert.Equal(250m, await cc.GetSaldoAsync(Datos.ClienteId2));
    }

    /// <summary>
    /// Regresión: <c>SaldoAcumulado</c> es un valor materializado, así que al sacar un
    /// movimiento del medio hay que rehacer toda la cadena posterior.
    /// </summary>
    [Fact]
    public async Task Recalcular_rehace_la_cadena_tras_eliminar_un_movimiento_del_medio()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        var m1 = await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "V1", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 900m, "V2", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 500m, "C1", DateTime.UtcNow);

        db.MovimientosCC.Remove(m1);
        await db.SaveChangesAsync();

        await cc.RecalcularSaldosAsync(Datos.ClienteId);

        var saldos = await db.MovimientosCC
            .Where(m => m.ClienteId == Datos.ClienteId)
            .OrderBy(m => m.Id)
            .Select(m => m.SaldoAcumulado)
            .ToListAsync();

        Assert.Equal(new[] { 900m, 400m }, saldos);
        Assert.Equal(400m, await cc.GetSaldoAsync(Datos.ClienteId));
    }

    [Fact]
    public async Task Los_saldos_de_todos_los_clientes_se_traen_en_una_consulta()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "V1", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 400m, "C1", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId2, TipoMovimientoCC.Cargo, 250m, "V2", DateTime.UtcNow);

        var saldos = await cc.GetSaldosAsync();

        Assert.Equal(600m, saldos[Datos.ClienteId]);
        Assert.Equal(250m, saldos[Datos.ClienteId2]);
    }

    [Fact]
    public async Task Un_cliente_sin_movimientos_no_aparece_en_los_saldos()
    {
        await using var db = Fixture.CreateContext();

        Assert.Empty(await NuevoCuentaCorriente(db).GetSaldosAsync());
    }

    /// <summary>
    /// Regresión: se medía desde la primera venta en cuenta corriente de toda la historia,
    /// aunque estuviera paga, así que un cliente puntual figuraba con cientos de días.
    /// </summary>
    [Fact]
    public async Task La_deuda_se_cuenta_desde_que_el_saldo_dejo_de_estar_en_cero()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        var hace100dias = DateTime.UtcNow.AddDays(-100);
        var hace90dias = DateTime.UtcNow.AddDays(-90);
        var hace10dias = DateTime.UtcNow.AddDays(-10);

        // Compró hace 100 días y pagó todo hace 90: esa deuda ya está cerrada.
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "V1", hace100dias);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 1000m, "C1", hace90dias);
        // La deuda vigente arranca acá.
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 500m, "V2", hace10dias);

        var inicio = await cc.GetInicioDeudaAsync();

        Assert.Equal(hace10dias.Date, inicio[Datos.ClienteId].Date);
    }

    [Fact]
    public async Task Un_cliente_que_nunca_salda_cuenta_desde_su_primer_cargo()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        var hace30dias = DateTime.UtcNow.AddDays(-30);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 1000m, "V1", hace30dias);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 300m, "C1", DateTime.UtcNow);

        var inicio = await cc.GetInicioDeudaAsync();

        Assert.Equal(hace30dias.Date, inicio[Datos.ClienteId].Date);
    }

    [Fact]
    public async Task Un_cliente_al_dia_no_tiene_inicio_de_deuda()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 500m, "V1", DateTime.UtcNow.AddDays(-5));
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 500m, "C1", DateTime.UtcNow);

        Assert.DoesNotContain(Datos.ClienteId, (await cc.GetInicioDeudaAsync()).Keys);
    }

    [Fact]
    public async Task Un_cliente_con_credito_a_favor_no_tiene_inicio_de_deuda()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 500m, "V1", DateTime.UtcNow.AddDays(-5));
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 800m, "C1", DateTime.UtcNow);

        Assert.DoesNotContain(Datos.ClienteId, (await cc.GetInicioDeudaAsync()).Keys);
    }

    [Fact]
    public async Task Recalcular_sobre_un_cliente_sin_movimientos_no_falla()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RecalcularSaldosAsync(Datos.ClienteId);

        Assert.Equal(0m, await cc.GetSaldoAsync(Datos.ClienteId));
    }
}
