namespace RepartoAlfajores.Models;

public class Cobro
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public decimal Monto { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Nota { get; set; }
}
