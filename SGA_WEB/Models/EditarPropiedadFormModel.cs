using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SGA_WEB.Models;

public sealed class EditarPropiedadFormModel
{
    [Required]
    [Range(1, int.MaxValue)]
    public int IdPropiedad { get; set; }

    [Required(ErrorMessage = "Seleccione el tipo.")]
    [MaxLength(50)]
    public string Tipo { get; set; } = "Apartamento";

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [MaxLength(2000)]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indique el precio mensual.")]
    [MaxLength(32, ErrorMessage = "Precio demasiado largo.")]
    public string PrecioMensualTexto { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ubicación es obligatoria.")]
    [MaxLength(200)]
    public string Ubicacion { get; set; } = string.Empty;

    [Range(0, 50000, ErrorMessage = "Espacio total entre 0 y 50000 m².")]
    public int EspacioTotalM2 { get; set; }

    [Required]
    [MaxLength(40)]
    public string NumeroPiso { get; set; } = "N/A";

    [Range(1, 50, ErrorMessage = "Habitaciones entre 1 y 50.")]
    public int NumeroHabitaciones { get; set; } = 1;

    public bool EstacionamientoDisponible { get; set; } = true;

    [Required]
    [MaxLength(80)]
    public string ProcesoPago { get; set; } = "Banco";

    [MaxLength(500)]
    public string? UrlFotoActual { get; set; }

    public IFormFile? Foto { get; set; }
}
