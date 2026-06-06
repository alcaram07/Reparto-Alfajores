namespace RepartoAlfajores.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public int ZonaId { get; set; }
    public Zona Zona { get; set; } = null!;
    public bool Activo { get; set; } = true;
    public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
    public ICollection<Venta> Ventas { get; set; } = null!;
}
