using System.ComponentModel.DataAnnotations;

namespace SGA_WEB.Models;

public class RecuperarContrasennaInputModel
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "Correo inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La cédula es obligatoria.")]
    [RegularExpression(@"^\d-\d{4}-\d{4}$", ErrorMessage = "La cédula debe tener formato x-xxxx-xxxx.")]
    public string Cedula { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres.")]
    public string NuevaContrasenna { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la nueva contraseña.")]
    [Compare(nameof(NuevaContrasenna), ErrorMessage = "La confirmación no coincide.")]
    public string ConfirmarNuevaContrasenna { get; set; } = string.Empty;
}
