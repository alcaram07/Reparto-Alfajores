namespace RepartoAlfajores.Models;

public class MovimientoCC
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public TipoMovimientoCC Tipo { get; set; }
    public decimal Monto { get; set; }
    public decimal SaldoAcumulado { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int? VentaId { get; set; }
    public Venta? Venta { get; set; }
    public int? CobroId { get; set; }
    public Cobro? Cobro { get; set; }
}
