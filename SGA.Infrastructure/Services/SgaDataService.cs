using System.Data;
using BCrypt.Net;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SGA.Infrastructure.Services;

public class SgaDataService : ISgaDataService
{
    private readonly IConfiguration _configuration;

    public SgaDataService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<LoginResult?> ValidateCredentialsAsync(string email, string plainPassword)
    {
        await using var conn = CreateDbConnection();

        var result = await conn.QueryFirstOrDefaultAsync<LoginUserRow>(
            "sp_ObtenerUsuarioPorEmail",
            new { Email = email },
            commandType: CommandType.StoredProcedure);

        if (result is null || string.IsNullOrWhiteSpace(result.Contrasenna))
        {
            return null;
        }

        // Compatibilidad: si existiera contraseña legacy en texto plano, se migra al hash al autenticarse.
        if (!result.Contrasenna.StartsWith("$2"))
        {
            if (result.Contrasenna != plainPassword)
            {
                return null;
            }

            var newHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
            await conn.ExecuteAsync(
                "sp_ActualizarHashUsuario",
                new { Email = email, PasswordHash = newHash },
                commandType: CommandType.StoredProcedure);

            return new LoginResult { Email = email, Rol = result.Rol };
        }

        return BCrypt.Net.BCrypt.Verify(plainPassword, result.Contrasenna)
            ? new LoginResult { Email = email, Rol = result.Rol }
            : null;
    }

    public async Task<bool> RegisterClientUserAsync(string nombreCompleto, string cedula, string telefono, string email, string plainPassword)
    {
        await using var conn = CreateDbConnection();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        var affected = await conn.ExecuteScalarAsync<int>(
            "sp_RegistrarUsuarioCliente",
            new { NombreCompleto = nombreCompleto, Cedula = cedula, Telefono = telefono, Email = email, PasswordHash = passwordHash },
            commandType: CommandType.StoredProcedure);
        return affected == 1;
    }

    public async Task<ClienteLookupResult?> GetClientByCedulaAsync(string cedula)
    {
        await using var conn = CreateDbConnection();
        return await conn.QueryFirstOrDefaultAsync<ClienteLookupResult>(
            "sp_BuscarClientePorCedula",
            new { Cedula = cedula },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> ValidateClientRecoveryAsync(string email, string cedula)
    {
        await using var conn = CreateDbConnection();
        var result = await conn.ExecuteScalarAsync<int>(
            "sp_ValidarClienteRecuperacion",
            new { Email = email, Cedula = cedula },
            commandType: CommandType.StoredProcedure);
        return result == 1;
    }

    public async Task UpdatePasswordByEmailAsync(string email, string newPlainPassword)
    {
        await using var conn = CreateDbConnection();
        var hash = BCrypt.Net.BCrypt.HashPassword(newPlainPassword);
        await conn.ExecuteAsync(
            "sp_CambiarContrasennaPorEmail",
            new { Email = email, PasswordHash = hash },
            commandType: CommandType.StoredProcedure);
    }

    public async Task LogSystemErrorAsync(string codigo, string mensaje, string severidad, string? email = null)
    {
        await using var conn = CreateDbConnection();
        await conn.ExecuteAsync(
            "sp_RegistrarErrorSistema",
            new
            {
                Codigo = codigo,
                Mensaje = mensaje.Length > 500 ? mensaje[..500] : mensaje,
                Severidad = severidad,
                Email = email
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task SaveContactMessageAsync(string nombreCompleto, string email, string asunto, string mensaje)
    {
        await using var conn = CreateDbConnection();
        await conn.ExecuteAsync(
            "sp_RegistrarMensajeContacto",
            new
            {
                NombreCompleto = nombreCompleto,
                Email = email,
                Asunto = asunto,
                Mensaje = mensaje
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task SaveClientMessageAsync(string email, string asunto, string mensaje)
    {
        await using var conn = CreateDbConnection();
        await conn.ExecuteAsync(
            "sp_RegistrarMensajeCliente",
            new { Email = email, Asunto = asunto, Mensaje = mensaje },
            commandType: CommandType.StoredProcedure);
    }

    public async Task ScheduleAppointmentAsync(string nombreCompleto, string email, string telefono, DateTime fechaVisita, string propiedadInteres, string mensaje)
    {
        await using var conn = CreateDbConnection();
        await conn.ExecuteAsync(
            "sp_AgendarCita",
            new
            {
                NombreCompleto = nombreCompleto,
                Email = email.Trim().ToLowerInvariant(),
                Telefono = telefono,
                FechaVisita = fechaVisita,
                PropiedadInteres = propiedadInteres,
                Mensaje = mensaje
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<CitaResumen>> GetCitasAsync(DateTime? fechaDesde = null, DateTime? fechaHasta = null, string? estado = null)
    {
        await using var conn = CreateDbConnection();
        var data = await conn.QueryAsync<CitaResumen>(
            "sp_ListarCitas",
            new { FechaDesde = fechaDesde, FechaHasta = fechaHasta, Estado = estado },
            commandType: CommandType.StoredProcedure);
        return data.ToList();
    }

    public async Task UpdateCitaStatusAsync(int idCita, string estado)
    {
        await using var conn = CreateDbConnection();
        await conn.ExecuteAsync(
            "sp_ActualizarEstadoCita",
            new { IdCita = idCita, Estado = estado },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<CitaResumen>> GetCitasByClientEmailAsync(string email)
    {
        await using var conn = CreateDbConnection();
        var data = await conn.QueryAsync<CitaResumen>(
            "sp_ListarCitasPorClienteEmail",
            new { Email = email },
            commandType: CommandType.StoredProcedure);
        return data.ToList();
    }

    public async Task<IReadOnlyList<ClienteResumen>> GetClientesAsync()
    {
        await using var conn = CreateDbConnection();
        var data = await conn.QueryAsync<ClienteResumen>("sp_ListarClientes", commandType: CommandType.StoredProcedure);
        return data.ToList();
    }

    public async Task<IReadOnlyList<MensajeResumen>> GetMensajesContactoAsync()
    {
        await using var conn = CreateDbConnection();
        var data = await conn.QueryAsync<MensajeResumen>("sp_ListarMensajesContacto", commandType: CommandType.StoredProcedure);
        return data.ToList();
    }

    private SqlConnection CreateDbConnection()
    {
        var masterConnection = _configuration.GetConnectionString("MasterConnection")
            ?? throw new InvalidOperationException("No existe ConnectionStrings:MasterConnection.");
        var databaseName = _configuration["DatabaseInitialization:DatabaseName"]
            ?? throw new InvalidOperationException("No existe DatabaseInitialization:DatabaseName.");

        var builder = new SqlConnectionStringBuilder(masterConnection)
        {
            InitialCatalog = databaseName
        };

        return new SqlConnection(builder.ConnectionString);
    }

    private sealed class LoginUserRow
    {
        public string Contrasenna { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
    }
}
