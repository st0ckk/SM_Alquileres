using SGA.Infrastructure.Services;

namespace SGA_WEB.Models;

public sealed class HomeIndexViewModel
{
    public IReadOnlyList<PropiedadListaItem> Propiedades { get; init; } = [];
    public string PropiedadSeleccionadaQuery { get; init; } = string.Empty;
    public int? IdPropiedadParaCita { get; init; }
    public int PaginaActual { get; init; } = 1;
    public int TotalRegistros { get; init; }
    public int TamanoPagina { get; init; } = 10;

    public int TotalPaginas =>
        TamanoPagina <= 0 ? 0 : (int)Math.Ceiling(TotalRegistros / (double)TamanoPagina);
}
