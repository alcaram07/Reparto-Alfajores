using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RepartoAlfajores.ViewModels;

public class ClienteViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
    public string Nombre { get; set; } = null!;

    [Phone(ErrorMessage = "Teléfono inválido")]
    [StringLength(20)]
    public string? Telefono { get; set; }

    [StringLength(200)]
    public string? Direccion { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una zona")]
    public int ZonaId { get; set; }

    public bool Activo { get; set; } = true;

    public IEnumerable<SelectListItem> Zonas { get; set; } = new List<SelectListItem>();
}
