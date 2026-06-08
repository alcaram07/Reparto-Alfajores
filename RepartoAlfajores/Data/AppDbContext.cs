using Microsoft.EntityFrameworkCore;
using RepartoAlfajores.Models;

namespace RepartoAlfajores.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Zona> Zonas => Set<Zona>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<CategoriaProducto> CategoriaProductos => Set<CategoriaProducto>();
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Venta> Ventas => Set<Venta>();
    public DbSet<DetalleVenta> DetalleVentas => Set<DetalleVenta>();
    public DbSet<Cobro> Cobros => Set<Cobro>();
    public DbSet<Configuracion> Configuraciones => Set<Configuracion>();
    public DbSet<MovimientoCC> MovimientosCC => Set<MovimientoCC>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Configuracion>()
            .HasIndex(c => c.Clave).IsUnique();

        modelBuilder.Entity<Venta>()
            .Property(v => v.Total).HasPrecision(18, 2);

        modelBuilder.Entity<DetalleVenta>()
            .Property(d => d.PrecioUnitario).HasPrecision(18, 2);

        modelBuilder.Entity<Producto>()
            .Property(p => p.PrecioUnitario).HasPrecision(18, 2);

        modelBuilder.Entity<Cobro>()
            .Property(c => c.Monto).HasPrecision(18, 2);

        modelBuilder.Entity<MovimientoCC>()
            .Property(m => m.Monto).HasPrecision(18, 2);

        modelBuilder.Entity<MovimientoCC>()
            .Property(m => m.SaldoAcumulado).HasPrecision(18, 2);

        modelBuilder.Entity<Cliente>()
            .HasMany(c => c.Ventas)
            .WithOne(v => v.Cliente)
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cliente>()
            .HasMany<Cobro>()
            .WithOne(c => c.Cliente)
            .HasForeignKey(c => c.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Cliente>()
            .HasMany<MovimientoCC>()
            .WithOne(m => m.Cliente)
            .HasForeignKey(m => m.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MovimientoCC>()
            .HasOne(m => m.Venta)
            .WithMany()
            .HasForeignKey(m => m.VentaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MovimientoCC>()
            .HasOne(m => m.Cobro)
            .WithMany()
            .HasForeignKey(m => m.CobroId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Venta>()
            .HasMany(v => v.Detalles)
            .WithOne(d => d.Venta)
            .HasForeignKey(d => d.VentaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
