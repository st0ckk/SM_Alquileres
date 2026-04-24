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

            return new LoginResult
            {
                Email = email,
                Rol = result.Rol,
                NombreCompleto = await GetNombreCompletoByEmailAsync(conn, email)
            };
        }

        return BCrypt.Net.BCrypt.Verify(plainPassword, result.Contrasenna)
            ? new LoginResult
            {
                Email = email,
                Rol = result.Rol,
                NombreCompleto = await GetNombreCompletoByEmailAsync(conn, email)
            }
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

    public async Task<bool> CedulaExistsAsync(string cedula)
    {
        await using var conn = CreateDbConnection();
        const string sql = "SELECT CASE WHEN EXISTS(SELECT 1 FROM clientes WHERE cedula = @Cedula) THEN 1 ELSE 0 END";
        var exists = await conn.ExecuteScalarAsync<int>(sql, new { Cedula = cedula });
        return exists == 1;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        await using var conn = CreateDbConnection();
        const string sql = "SELECT CASE WHEN EXISTS(SELECT 1 FROM usuarios WHERE LOWER(email) = LOWER(@Email)) THEN 1 ELSE 0 END";
        var exists = await conn.ExecuteScalarAsync<int>(sql, new { Email = email });
        return exists == 1;
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

    public async Task<bool> ScheduleAppointmentAsync(string nombreCompleto, string email, string telefono, DateTime fechaVisita, string propiedadInteres, string mensaje, int? idPropiedad = null)
    {
        await using var conn = CreateDbConnection();
        var resultado = await conn.QuerySingleAsync<int>(
            "sp_AgendarCita",
            new
            {
                NombreCompleto = nombreCompleto,
                Email = email.Trim().ToLowerInvariant(),
                Telefono = telefono,
                FechaVisita = fechaVisita,
                PropiedadInteres = propiedadInteres,
                Mensaje = mensaje,
                IdPropiedad = idPropiedad
            },
            commandType: CommandType.StoredProcedure);
        return resultado == 1;
    }

    public async Task<IReadOnlyList<int>> GetHorasOcupadasCitaAsync(string propiedadInteres, DateTime fechaSoloDia, int? idPropiedad = null)
    {
        await using var conn = CreateDbConnection();
        var horas = await conn.QueryAsync<int>(
            "sp_ListarHorasOcupadasCita",
            new { PropiedadInteres = propiedadInteres, Fecha = fechaSoloDia.Date, IdPropiedad = idPropiedad },
            commandType: CommandType.StoredProcedure);
        return horas.ToList();
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

    public async Task<int?> GetUsuarioIdByEmailAsync(string email)
    {
        await using var conn = CreateDbConnection();
        return await conn.ExecuteScalarAsync<int?>(
            "sp_ObtenerIdUsuarioPorEmail",
            new { Email = email.Trim() },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<(IReadOnlyList<PropiedadListaItem> Items, int TotalRegistros)> GetPropiedadesActivasPaginadoAsync(int pagina, int tamanoPagina)
    {
        if (pagina < 1)
        {
            pagina = 1;
        }

        if (tamanoPagina < 1)
        {
            tamanoPagina = 10;
        }

        if (tamanoPagina > 100)
        {
            tamanoPagina = 100;
        }

        await using var conn = CreateDbConnection();
        var total = await conn.ExecuteScalarAsync<int>("sp_ContarPropiedadesActivas", commandType: CommandType.StoredProcedure);
        var data = await conn.QueryAsync<PropiedadListaItem>(
            "sp_ListarPropiedadesActivasPaginado",
            new { Pagina = pagina, TamanoPagina = tamanoPagina },
            commandType: CommandType.StoredProcedure);
        return (data.ToList(), total);
    }

    public async Task<IReadOnlyList<PropiedadListaItem>> GetPropiedadesAdminAsync()
    {
        await using var conn = CreateDbConnection();
        var data = await conn.QueryAsync<PropiedadListaItem>("sp_ListarPropiedadesAdmin", commandType: CommandType.StoredProcedure);
        return data.ToList();
    }

    public async Task<int> InsertarPropiedadAsync(
        int idPropietario,
        string tipo,
        string descripcion,
        decimal precioMensual,
        string ubicacion,
        int espacioTotalM2,
        string numeroPiso,
        int numeroHabitaciones,
        bool estacionamientoDisponible,
        string procesoPago,
        string? urlFoto)
    {
        await using var conn = CreateDbConnection();
        return await conn.QuerySingleAsync<int>(
            "sp_InsertarPropiedad",
            new
            {
                IdPropietario = idPropietario,
                Tipo = tipo,
                Descripcion = descripcion,
                PrecioMensual = precioMensual,
                Ubicacion = ubicacion,
                EspacioTotalM2 = espacioTotalM2,
                NumeroPiso = numeroPiso,
                NumeroHabitaciones = numeroHabitaciones,
                EstacionamientoDisponible = estacionamientoDisponible,
                ProcesoPago = procesoPago,
                UrlFoto = urlFoto
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task ActualizarPropiedadAsync(
        int idPropiedad,
        string tipo,
        string descripcion,
        decimal precioMensual,
        string ubicacion,
        int espacioTotalM2,
        string numeroPiso,
        int numeroHabitaciones,
        bool estacionamientoDisponible,
        string procesoPago,
        string? urlFoto)
    {
        await using var conn = CreateDbConnection();
        await conn.ExecuteAsync(
            "sp_ActualizarPropiedad",
            new
            {
                IdPropiedad = idPropiedad,
                Tipo = tipo,
                Descripcion = descripcion,
                PrecioMensual = precioMensual,
                Ubicacion = ubicacion,
                EspacioTotalM2 = espacioTotalM2,
                NumeroPiso = numeroPiso,
                NumeroHabitaciones = numeroHabitaciones,
                EstacionamientoDisponible = estacionamientoDisponible,
                ProcesoPago = procesoPago,
                UrlFoto = urlFoto
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task SetPropiedadDisponibleAsync(int idPropiedad, bool disponible)
    {
        await using var conn = CreateDbConnection();
        await conn.ExecuteAsync(
            "sp_SetPropiedadDisponible",
            new { IdPropiedad = idPropiedad, Disponible = disponible },
            commandType: CommandType.StoredProcedure);
    }

    private SqlConnection CreateDbConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("No existe ConnectionStrings:DefaultConnection.");
        return new SqlConnection(connectionString);
    }

    private sealed class LoginUserRow
    {
        public string Contrasenna { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
    }

    private static async Task<string> GetNombreCompletoByEmailAsync(SqlConnection conn, string email)
    {
        const string sql = "SELECT TOP 1 nombre FROM usuarios WHERE LOWER(email) = LOWER(@Email)";
        var nombre = await conn.ExecuteScalarAsync<string?>(sql, new { Email = email });
        return string.IsNullOrWhiteSpace(nombre) ? email : nombre;
    }
}
