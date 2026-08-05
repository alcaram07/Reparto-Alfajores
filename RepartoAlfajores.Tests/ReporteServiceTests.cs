using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Implementations;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Tests;

public class ReporteServiceTests : DbTestBase
{
    public ReporteServiceTests(PostgresFixture fixture) : base(fixture) { }

    private ReporteService NuevoReporte(RepartoAlfajores.Data.AppDbContext db) =>
        new(db, NuevoCuentaCorriente(db));

    private VentaViewModel VentaVm(MetodoPago metodo, int cantidad) => new()
    {
        ClienteId = Datos.ClienteId,
        MetodoPago = metodo,
        Detalles = [new DetalleVentaViewModel { ProductoId = Datos.ProductoId, Cantidad = cantidad }]
    };

    /// <summary>
    /// Regresión: se restaban todos los cobros del rango a las ventas en cuenta corriente del
    /// rango. Un pago que cancelaba una deuda vieja descontaba ventas nuevas, y el resultado
    /// negativo quedaba escondido por un Math.Max(0, …).
    /// </summary>
    [Fact]
    public async Task La_deuda_del_reporte_no_la_distorsiona_un_cobro_de_una_deuda_vieja()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        // Deuda del mes pasado, ya saldada — fuera del rango del reporte.
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 5000m, "vieja", DateTime.UtcNow.AddDays(-40));
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 5000m, "pago", DateTime.UtcNow.AddDays(-1));

        // Venta a cuenta corriente de hoy, impaga.
        await NuevaVenta(db).CreateAsync(VentaVm(MetodoPago.CuentaCorriente, 2)); // 1000

        var hoy = DateTime.UtcNow.Date;
        var reporte = await NuevoReporte(db).GetReporteAsync(hoy, hoy);

        // Con el cálculo viejo daba 1000 − 5000 = −4000, recortado a 0.
        Assert.Equal(1000m, reporte.TotalPendiente);
    }

    [Fact]
    public async Task La_deuda_del_reporte_ignora_los_saldos_a_favor()
    {
        await using var db = Fixture.CreateContext();
        var cc = NuevoCuentaCorriente(db);

        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Cargo, 300m, "V1", DateTime.UtcNow);
        await cc.RegistrarMovimientoAsync(Datos.ClienteId, TipoMovimientoCC.Abono, 900m, "C1", DateTime.UtcNow); // −600
        await cc.RegistrarMovimientoAsync(Datos.ClienteId2, TipoMovimientoCC.Cargo, 800m, "V2", DateTime.UtcNow);

        var hoy = DateTime.UtcNow.Date;
        var reporte = await NuevoReporte(db).GetReporteAsync(hoy, hoy);

        Assert.Equal(800m, reporte.TotalPendiente);
    }

    [Fact]
    public async Task Sin_deudores_la_deuda_del_reporte_es_cero()
    {
        await using var db = Fixture.CreateContext();
        await NuevaVenta(db).CreateAsync(VentaVm(MetodoPago.Efectivo, 2));

        var hoy = DateTime.UtcNow.Date;
        var reporte = await NuevoReporte(db).GetReporteAsync(hoy, hoy);

        Assert.Equal(0m, reporte.TotalPendiente);
        Assert.Equal(1000m, reporte.TotalVendido);
    }

    [Fact]
    public async Task El_reporte_resume_las_ventas_del_periodo()
    {
        await using var db = Fixture.CreateContext();
        var ventas = NuevaVenta(db);
        await ventas.CreateAsync(VentaVm(MetodoPago.Efectivo, 2));  // 1000
        await ventas.CreateAsync(VentaVm(MetodoPago.QR, 1));        // 500

        var hoy = DateTime.UtcNow.Date;
        var reporte = await NuevoReporte(db).GetReporteAsync(hoy, hoy);

        Assert.Equal(1500m, reporte.TotalVendido);
        Assert.Equal(2, reporte.CantidadVentas);
        Assert.Equal(750m, reporte.TicketPromedio);
        Assert.Equal("Centro", Assert.Single(reporte.VentasPorZona).Nombre);
    }
}
