using SGA.Infrastructure.Services;

namespace SGA_WEB.Models;

public class ClienteNotificacionesViewModel
{
    public IReadOnlyList<CitaResumen> Citas { get; set; } = [];
    public ClienteMensajeInputModel Mensaje { get; set; } = new();
}
