using RepartoAlfajores.Models;

namespace RepartoAlfajores.Data;

public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (db.Zonas.Any()) return;

        var zonas = new[]
        {
            new Zona { Nombre = "Norte", Activa = true },
            new Zona { Nombre = "Centro", Activa = true },
            new Zona { Nombre = "Sur", Activa = true },
            new Zona { Nombre = "Oeste", Activa = true }
        };
        db.Zonas.AddRange(zonas);

        var categorias = new[]
        {
            new CategoriaProducto { Nombre = "Alfajores" },
            new CategoriaProducto { Nombre = "Tortas" },
            new CategoriaProducto { Nombre = "Masas" },
            new CategoriaProducto { Nombre = "Otros" }
        };
        db.CategoriaProductos.AddRange(categorias);
        await db.SaveChangesAsync();

        db.Productos.AddRange(
            new Producto { Nombre = "Alfajor Clásico", CategoriaId = categorias[0].Id, PrecioUnitario = 500, Activo = true },
            new Producto { Nombre = "Alfajor de Maicena", CategoriaId = categorias[0].Id, PrecioUnitario = 450, Activo = true },
            new Producto { Nombre = "Torta de Ricota", CategoriaId = categorias[1].Id, PrecioUnitario = 3500, Activo = true },
            new Producto { Nombre = "Masas Finas (docena)", CategoriaId = categorias[2].Id, PrecioUnitario = 1200, Activo = true },
            new Producto { Nombre = "Medialunas (docena)", CategoriaId = categorias[2].Id, PrecioUnitario = 900, Activo = true }
        );

        db.Clientes.AddRange(
            new Cliente { Nombre = "María García", Telefono = "11-1234-5678", Direccion = "Av. Corrientes 1234", ZonaId = zonas[1].Id, Activo = true },
            new Cliente { Nombre = "Juan Pérez", Telefono = "11-8765-4321", Direccion = "Calle San Martín 456", ZonaId = zonas[0].Id, Activo = true },
            new Cliente { Nombre = "Ana Rodríguez", Telefono = "11-5555-1111", Direccion = "Boulevard Oroño 789", ZonaId = zonas[2].Id, Activo = true }
        );

        await db.SaveChangesAsync();
    }
}
