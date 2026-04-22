using System.ComponentModel.DataAnnotations;

namespace SGA_WEB.Models;

public class CambiarContrasennaInputModel
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    public string ContrasennaActual { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La nueva contraseña debe tener al menos 6 caracteres.")]
    public string NuevaContrasenna { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la nueva contraseña.")]
    [Compare(nameof(NuevaContrasenna), ErrorMessage = "La confirmación no coincide.")]
    public string ConfirmarNuevaContrasenna { get; set; } = string.Empty;
}
