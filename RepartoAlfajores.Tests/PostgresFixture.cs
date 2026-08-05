using Microsoft.EntityFrameworkCore;
using Npgsql;
using RepartoAlfajores.Data;
using RepartoAlfajores.Models;
using RepartoAlfajores.Services.Implementations;

namespace RepartoAlfajores.Tests;

/// <summary>
/// Crea una base PostgreSQL descartable para toda la corrida de tests y le aplica las
/// migraciones reales. Se usa Postgres y no un proveedor en memoria porque los fixes que
/// estos tests cubren dependen de comportamiento específico del motor: transacciones sobre
/// la execution strategy de reintentos, <c>SELECT … FOR UPDATE</c> y FKs con RESTRICT.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string _dbName = $"reparto_tests_{Guid.NewGuid():N}";
    private string _adminConnectionString = null!;

    public string ConnectionString { get; private set; } = null!;

    static PostgresFixture()
    {
        // Program.cs lo activa al arrancar la app; los tests no pasan por ahí.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
    }

    public async Task InitializeAsync()
    {
        var host = Environment.GetEnvironmentVariable("TEST_PG_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("TEST_PG_PORT") ?? "5432";
        var user = Environment.GetEnvironmentVariable("TEST_PG_USER") ?? "postgres";
        var pass = Environment.GetEnvironmentVariable("TEST_PG_PASSWORD") ?? "postgres";

        var baseConn = $"Host={host};Port={port};Username={user};Password={pass}";
        _adminConnectionString = $"{baseConn};Database=postgres";
        ConnectionString = $"{baseConn};Database={_dbName}";

        try
        {
            await using var admin = new NpgsqlConnection(_adminConnectionString);
            await admin.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_dbName}\"", admin);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"No se pudo crear la base de test en {host}:{port}. " +
                "Los tests necesitan un PostgreSQL accesible; configurá TEST_PG_HOST / TEST_PG_PORT / " +
                $"TEST_PG_USER / TEST_PG_PASSWORD si no es el default. Detalle: {ex.Message}", ex);
        }

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)", admin);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Contexto configurado igual que en producción, reintentos incluidos.</summary>
    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null))
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>Deja la base vacía para que cada test parta de un estado conocido.</summary>
    public async Task LimpiarAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "MovimientosCC", "DetalleVentas", "Ventas", "Cobros",
                           "Clientes", "Productos", "CategoriaProductos", "Zonas"
            RESTART IDENTITY CASCADE
            """);
    }
}

/// <summary>Datos mínimos para operar: una zona, una categoría, un cliente y un producto.</summary>
public record DatosBase(int ZonaId, int ClienteId, int ClienteId2, int ProductoId, decimal PrecioProducto);

[CollectionDefinition(Nombre)]
public class DbCollection : ICollectionFixture<PostgresFixture>
{
    public const string Nombre = "postgres";
}

/// <summary>Base común: limpia la base y siembra datos antes de cada test.</summary>
[Collection(DbCollection.Nombre)]
public abstract class DbTestBase : IAsyncLifetime
{
    protected readonly PostgresFixture Fixture;
    protected DatosBase Datos = null!;

    protected DbTestBase(PostgresFixture fixture) => Fixture = fixture;

    public async Task InitializeAsync()
    {
        await Fixture.LimpiarAsync();

        await using var db = Fixture.CreateContext();

        var zona = new Zona { Nombre = "Centro", Activa = true };
        var categoria = new CategoriaProducto { Nombre = "Alfajores" };
        db.Zonas.Add(zona);
        db.CategoriaProductos.Add(categoria);
        await db.SaveChangesAsync();

        var cliente = new Cliente { Nombre = "Cliente Test", ZonaId = zona.Id, Activo = true, FechaAlta = DateTime.UtcNow };
        var cliente2 = new Cliente { Nombre = "Otro Cliente", ZonaId = zona.Id, Activo = true, FechaAlta = DateTime.UtcNow };
        var producto = new Producto { Nombre = "Alfajor Test", CategoriaId = categoria.Id, PrecioUnitario = 500m, Activo = true };
        db.Clientes.AddRange(cliente, cliente2);
        db.Productos.Add(producto);
        await db.SaveChangesAsync();

        Datos = new DatosBase(zona.Id, cliente.Id, cliente2.Id, producto.Id, producto.PrecioUnitario);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Fábricas: cada servicio recibe su propio contexto, como haría el scope de una request.
    protected CuentaCorrienteService NuevoCuentaCorriente(AppDbContext db) => new(db);
    protected VentaService NuevaVenta(AppDbContext db) => new(db, NuevoCuentaCorriente(db));
    protected CobroService NuevoCobro(AppDbContext db) => new(db, NuevoCuentaCorriente(db));
}
