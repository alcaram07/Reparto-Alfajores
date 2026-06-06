namespace RepartoAlfajores.ViewModels;

public class DeudorViewModel
{
    public int ClienteId { get; set; }
    public string Nombre { get; set; } = null!;
    public string Zona { get; set; } = null!;
    public decimal Saldo { get; set; }
    public int DiasDeuda { get; set; }
    public DateTime? UltimoPago { get; set; }
}
