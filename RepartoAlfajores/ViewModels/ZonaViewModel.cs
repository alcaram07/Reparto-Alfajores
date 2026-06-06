using System.ComponentModel.DataAnnotations;

namespace RepartoAlfajores.ViewModels;

public class ZonaViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres")]
    public string Nombre { get; set; } = null!;

    public bool Activa { get; set; } = true;
}
