using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using RepartoAlfajores.Models;

namespace RepartoAlfajores.ViewModels;

public class VentaViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Seleccione un cliente")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un cliente")]
    public int ClienteId { get; set; }

    public DateTime Fecha { get; set; } = DateTime.UtcNow;

    public MetodoPago MetodoPago { get; set; }

    public string? Nota { get; set; }

    public List<DetalleVentaViewModel> Detalles { get; set; } = new();

    public IEnumerable<SelectListItem> Clientes { get; set; } = new List<SelectListItem>();
    public IEnumerable<SelectListItem> Productos { get; set; } = new List<SelectListItem>();
    public string ProductosPreciosJson { get; set; } = "{}";
}
