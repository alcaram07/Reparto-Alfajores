using System.ComponentModel.DataAnnotations;

namespace RepartoAlfajores.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "La contraseña es obligatoria")]
    public string Password { get; set; } = null!;
}
