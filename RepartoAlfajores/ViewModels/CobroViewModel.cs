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
    [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal Monto { get; set; }

    public MetodoPago MetodoPago { get; set; }

    public string? Nota { get; set; }

    public IEnumerable<SelectListItem> Clientes { get; set; } = new List<SelectListItem>();
}
