using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using RepartoAlfajores.Models;

namespace RepartoAlfajores.ViewModels;

public class CobroViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Seleccione un cliente")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un cliente")]
    public int ClienteId { get; set; }

    [Required(ErrorMessage = "Ingrese un monto")]
    // Techo de cordura: frena el cero de más al tipear, que si no entra sin aviso y deja
    // al cliente con un crédito a favor enorme.
    [Range(0.01, 10_000_000, ErrorMessage = "El monto debe estar entre $0,01 y $10.000.000")]
    public decimal Monto { get; set; }

    public MetodoPago MetodoPago { get; set; }

    public string? Nota { get; set; }

    public IEnumerable<SelectListItem> Clientes { get; set; } = new List<SelectListItem>();
}
