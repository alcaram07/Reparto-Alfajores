using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RepartoAlfajores.ViewModels;

public class ProductoViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
    public string Nombre { get; set; } = null!;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una categoría")]
    public int CategoriaId { get; set; }

    [Required(ErrorMessage = "El precio es obligatorio")]
    [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
    public decimal PrecioUnitario { get; set; }

    public bool Activo { get; set; } = true;

    public IEnumerable<SelectListItem> Categorias { get; set; } = new List<SelectListItem>();
}
