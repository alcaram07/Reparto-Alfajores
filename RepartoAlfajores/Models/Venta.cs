namespace RepartoAlfajores.Models;

public enum EstadoCobro { Cobrado, CuentaCorriente }
public enum MetodoPago { Efectivo, Transferencia, QR, CuentaCorriente }

public class Venta
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public EstadoCobro EstadoCobro { get; set; }
    public MetodoPago MetodoPago { get; set; }
    public string? Nota { get; set; }
    public ICollection<DetalleVenta> Detalles { get; set; } = null!;
}
