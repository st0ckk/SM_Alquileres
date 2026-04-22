namespace SGA.Infrastructure.Services;

public interface ISgaDataService
{
    Task<LoginResult?> ValidateCredentialsAsync(string email, string plainPassword);
    Task<bool> RegisterClientUserAsync(string nombreCompleto, string cedula, string telefono, string email, string plainPassword);
    Task<ClienteLookupResult?> GetClientByCedulaAsync(string cedula);
    Task<bool> ValidateClientRecoveryAsync(string email, string cedula);
    Task UpdatePasswordByEmailAsync(string email, string newPlainPassword);
    Task LogSystemErrorAsync(string codigo, string mensaje, string severidad, string? email = null);
    Task SaveContactMessageAsync(string nombreCompleto, string email, string asunto, string mensaje);
    Task SaveClientMessageAsync(string email, string asunto, string mensaje);
    Task ScheduleAppointmentAsync(string nombreCompleto, string email, string telefono, DateTime fechaVisita, string propiedadInteres, string mensaje);
    Task<IReadOnlyList<CitaResumen>> GetCitasAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null, string? estado = null);
    Task UpdateCitaStatusAsync(int idCita, string estado);
    Task<IReadOnlyList<CitaResumen>> GetCitasByClientEmailAsync(string email);
    Task<IReadOnlyList<ClienteResumen>> GetClientesAsync();
    Task<IReadOnlyList<MensajeResumen>> GetMensajesContactoAsync();
}

public sealed class LoginResult
{
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
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
