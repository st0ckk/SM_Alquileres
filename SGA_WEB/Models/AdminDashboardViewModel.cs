using SGA.Infrastructure.Services;

namespace SGA_WEB.Models;

public class AdminDashboardViewModel
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string Estado { get; set; } = string.Empty;
    public IReadOnlyList<CitaResumen> Citas { get; set; } = [];
    public IReadOnlyList<ClienteResumen> Clientes { get; set; } = [];
    public IReadOnlyList<MensajeResumen> Mensajes { get; set; } = [];
    public IReadOnlyList<PropiedadListaItem> Propiedades { get; set; } = [];
    public EditarPropiedadFormModel? FormularioEdicion { get; set; }
}
