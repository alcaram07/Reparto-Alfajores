using System.ComponentModel.DataAnnotations;

namespace RepartoAlfajores.ViewModels;

public class DetalleVentaViewModel
{
    [Required]
    public int ProductoId { get; set; }

    [Required]
    [Range(1, 9999, ErrorMessage = "La cantidad debe ser al menos 1")]
    public int Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }
    public string? ProductoNombre { get; set; }
}
