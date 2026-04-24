namespace SGA.Infrastructure.Services;

public interface ISgaDataService
{
    Task<LoginResult?> ValidateCredentialsAsync(string email, string plainPassword);
    Task<bool> RegisterClientUserAsync(string nombreCompleto, string cedula, string telefono, string email, string plainPassword);
    Task<ClienteLookupResult?> GetClientByCedulaAsync(string cedula);
    Task<bool> CedulaExistsAsync(string cedula);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> ValidateClientRecoveryAsync(string email, string cedula);
    Task UpdatePasswordByEmailAsync(string email, string newPlainPassword);
    Task LogSystemErrorAsync(string codigo, string mensaje, string severidad, string? email = null);
    Task SaveContactMessageAsync(string nombreCompleto, string email, string asunto, string mensaje);
    Task SaveClientMessageAsync(string email, string asunto, string mensaje);
    Task<bool> ScheduleAppointmentAsync(string nombreCompleto, string email, string telefono, DateTime fechaVisita, string propiedadInteres, string mensaje, int? idPropiedad = null);
    Task<IReadOnlyList<int>> GetHorasOcupadasCitaAsync(string propiedadInteres, DateTime fechaSoloDia, int? idPropiedad = null);
    Task<IReadOnlyList<CitaResumen>> GetCitasAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null, string? estado = null);
    Task UpdateCitaStatusAsync(int idCita, string estado);
    Task<IReadOnlyList<CitaResumen>> GetCitasByClientEmailAsync(string email);
    Task<IReadOnlyList<ClienteResumen>> GetClientesAsync();
    Task<IReadOnlyList<MensajeResumen>> GetMensajesContactoAsync();
    Task<int?> GetUsuarioIdByEmailAsync(string email);
    Task<(IReadOnlyList<PropiedadListaItem> Items, int TotalRegistros)> GetPropiedadesActivasPaginadoAsync(int pagina, int tamanoPagina);
    Task<IReadOnlyList<PropiedadListaItem>> GetPropiedadesAdminAsync();
    Task<int> InsertarPropiedadAsync(int idPropietario, string tipo, string descripcion, decimal precioMensual, string ubicacion, int espacioTotalM2, string numeroPiso, int numeroHabitaciones, bool estacionamientoDisponible, string procesoPago, string? urlFoto);
    Task ActualizarPropiedadAsync(int idPropiedad, string tipo, string descripcion, decimal precioMensual, string ubicacion, int espacioTotalM2, string numeroPiso, int numeroHabitaciones, bool estacionamientoDisponible, string procesoPago, string? urlFoto);
    Task SetPropiedadDisponibleAsync(int idPropiedad, bool disponible);
}

public sealed class LoginResult
{
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
}

public sealed class ClienteResumen
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public DateTime FechaRegistro { get; set; }
}

public sealed class ClienteLookupResult
{
    public string Cedula { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string Email { get; set; } = string.Empty;
}

public sealed class MensajeResumen
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Cedula { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string Asunto { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public DateTime FechaRegistro { get; set; }
}

public sealed class CitaResumen
{
    public int IdCita { get; set; }
    public string Cliente { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string PropiedadInteres { get; set; } = string.Empty;
    public DateTime FechaVisita { get; set; }
    public string Estado { get; set; } = string.Empty;
}

public sealed class PropiedadListaItem
{
    public int IdPropiedad { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal PrecioMensual { get; set; }
    public string Ubicacion { get; set; } = string.Empty;
    public int EspacioTotalM2 { get; set; }
    public string NumeroPiso { get; set; } = string.Empty;
    public int NumeroHabitaciones { get; set; }
    public bool EstacionamientoDisponible { get; set; }
    public string ProcesoPago { get; set; } = string.Empty;
    public string? UrlFoto { get; set; }
    public bool? Disponible { get; set; }

    public string TextoPropiedadInteres => $"Ref {IdPropiedad} - {Tipo} - {Ubicacion}";
}
