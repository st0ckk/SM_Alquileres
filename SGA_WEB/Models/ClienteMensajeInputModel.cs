using System.ComponentModel.DataAnnotations;

namespace SGA_WEB.Models;

public class ClienteMensajeInputModel
{
    [Required]
    [MaxLength(200)]
    public string Asunto { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Mensaje { get; set; } = string.Empty;
}
