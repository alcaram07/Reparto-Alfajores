using RepartoAlfajores.Models;
using RepartoAlfajores.Utils;
using RepartoAlfajores.ViewModels;

namespace RepartoAlfajores.Tests;

/// <summary>
/// Argentina es UTC-3 y no usa horario de verano, así que el día del calendario local
/// arranca a las 03:00 UTC. Antes se usaba <c>DateTime.UtcNow.Date</c> como si fuese la
/// fecha local y las ventas cargadas después de las 21:00 caían en el día siguiente.
/// </summary>
public class FechaArTests
{
    [Fact]
    public void El_dia_local_arranca_a_las_tres_utc()
    {
        var (desde, hasta) = FechaAr.RangoDia(new DateTime(2026, 8, 5));

        Assert.Equal(new DateTime(2026, 8, 5, 3, 0, 0), desde);
        Assert.Equal(new DateTime(2026, 8, 6, 3, 0, 0), hasta);
    }

    [Fact]
    public void Un_rango_de_varios_dias_incluye_el_ultimo_dia_completo()
    {
        var (desde, hasta) = FechaAr.Rango(new DateTime(2026, 8, 1), new DateTime(2026, 8, 3));

        Assert.Equal(new DateTime(2026, 8, 1, 3, 0, 0), desde);
        Assert.Equal(new DateTime(2026, 8, 4, 3, 0, 0), hasta);
    }

    [Fact]
    public void Una_fecha_utc_se_muestra_tres_horas_antes_en_hora_argentina()
    {
        var utc = new DateTime(2026, 8, 6, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 8, 5, 22, 0, 0), utc.ALocal());
    }

    [Fact]
    public void Una_venta_de_las_22_pertenece_al_dia_argentino_y_no_al_siguiente()
    {
        // 22:00 del 5/8 en Argentina son las 01:00 del 6/8 en UTC.
        var ventaUtc = new DateTime(2026, 8, 6, 1, 0, 0, DateTimeKind.Utc);

        var (desdeDia5, hastaDia5) = FechaAr.RangoDia(new DateTime(2026, 8, 5));
        var (desdeDia6, hastaDia6) = FechaAr.RangoDia(new DateTime(2026, 8, 6));

        Assert.InRange(ventaUtc, desdeDia5, hastaDia5.AddTicks(-1));
        Assert.False(ventaUtc >= desdeDia6 && ventaUtc < hastaDia6);
    }
}

/// <summary>Verifica el filtro de fechas contra la base, no sólo la aritmética del helper.</summary>
public class VentasPorFechaTests : DbTestBase
{
    public VentasPorFechaTests(PostgresFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Una_venta_de_las_22_se_lista_en_el_dia_argentino_correcto()
    {
        await using var db = Fixture.CreateContext();

        // Se inserta directo para poder fijar la hora: CreateAsync siempre usa UtcNow.
        db.Ventas.Add(new Venta
        {
            ClienteId = Datos.ClienteId,
            Fecha = new DateTime(2026, 8, 6, 1, 0, 0), // 5/8 22:00 hora argentina
            Total = 777m,
            EstadoCobro = EstadoCobro.Cobrado,
            MetodoPago = MetodoPago.Efectivo,
            Detalles = []
        });
        await db.SaveChangesAsync();

        var service = NuevaVenta(db);

        var delDia5 = await service.GetAllAsync(new DateTime(2026, 8, 5));
        var delDia6 = await service.GetAllAsync(new DateTime(2026, 8, 6));

        Assert.Single(delDia5);
        Assert.Empty(delDia6);
    }

    [Fact]
    public async Task Una_venta_de_la_madrugada_no_se_adelanta_al_dia_anterior()
    {
        await using var db = Fixture.CreateContext();

        db.Ventas.Add(new Venta
        {
            ClienteId = Datos.ClienteId,
            Fecha = new DateTime(2026, 8, 5, 13, 0, 0), // 5/8 10:00 hora argentina
            Total = 100m,
            EstadoCobro = EstadoCobro.Cobrado,
            MetodoPago = MetodoPago.Efectivo,
            Detalles = []
        });
        await db.SaveChangesAsync();

        var service = NuevaVenta(db);

        Assert.Single(await service.GetAllAsync(new DateTime(2026, 8, 5)));
        Assert.Empty(await service.GetAllAsync(new DateTime(2026, 8, 4)));
    }
}
