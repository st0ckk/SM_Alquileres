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

    [Required(ErrorMessage = "Seleccione la fecha de la visita.")]
    public DateTime FechaCita { get; set; }

    [Required(ErrorMessage = "Seleccione el horario de la visita.")]
    [Range(8, 17, ErrorMessage = "El horario debe estar entre las 8:00 y las 17:00.")]
    public int HoraCita { get; set; }

    [Required]
    [MaxLength(200)]
    public string PropiedadInteres { get; set; } = string.Empty;

    public int? IdPropiedadCita { get; set; }

    [MaxLength(1000)]
    public string Mensaje { get; set; } = string.Empty;
}
