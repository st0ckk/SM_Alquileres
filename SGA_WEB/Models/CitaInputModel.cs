using System.ComponentModel.DataAnnotations;

namespace SGA_WEB.Models;

public class CitaInputModel
{
    [Required]
    [MaxLength(150)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Telefono { get; set; } = string.Empty;

    [Required]
    public DateTime FechaVisita { get; set; }

    [Required]
    [MaxLength(200)]
    public string PropiedadInteres { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Mensaje { get; set; } = string.Empty;
}
