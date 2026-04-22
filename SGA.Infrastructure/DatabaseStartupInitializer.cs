using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

namespace SGA.Infrastructure;

/// <summary>
/// Crea la base de datos y aplica el script inicial (misma idea que el arranque en proyecto de referencia).
/// </summary>
public class DatabaseStartupInitializer
{
    private static readonly System.Text.RegularExpressions.Regex GoSplitter = new(
        @"^\s*GO\s*(?:--.*)?$",
        System.Text.RegularExpressions.RegexOptions.Multiline
        | System.Text.RegularExpressions.RegexOptions.IgnoreCase
        | System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DatabaseStartupInitializer> _logger;

    public DatabaseStartupInitializer(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<DatabaseStartupInitializer> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var masterConnection = _configuration.GetConnectionString("MasterConnection");
        var databaseName = _configuration["DatabaseInitialization:DatabaseName"];
        var scriptRelativePath = _configuration["DatabaseInitialization:ScriptRelativePath"];

        if (string.IsNullOrWhiteSpace(masterConnection) ||
            string.IsNullOrWhiteSpace(databaseName) ||
            string.IsNullOrWhiteSpace(scriptRelativePath))
        {
            _logger.LogWarning("Se omite inicializacion de BD por configuracion incompleta.");
            return;
        }

        var scriptAbsolutePath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, scriptRelativePath));
        if (!File.Exists(scriptAbsolutePath))
        {
            _logger.LogWarning("No se encontro el script SQL en la ruta: {Path}", scriptAbsolutePath);
            return;
        }

        var fullScript = await File.ReadAllTextAsync(scriptAbsolutePath, cancellationToken);
        var normalizedScript = RemoveCreateDatabaseAndUseStatements(fullScript);

        await EnsureDatabaseExistsAsync(masterConnection, databaseName, cancellationToken);
        await EnsureSchemaAsync(masterConnection, databaseName, normalizedScript, cancellationToken);
        await EnsureSupportTablesAndProceduresAsync(masterConnection, databaseName, cancellationToken);
        await EnsureDefaultAdminUserAsync(masterConnection, databaseName, cancellationToken);

        _logger.LogInformation("Inicializacion de BD completada para {DatabaseName}.", databaseName);
    }

    private async Task EnsureSchemaAsync(
        string masterConnection,
        string databaseName,
        string normalizedScript,
        CancellationToken cancellationToken)
    {
        if (await TableExistsAsync(masterConnection, databaseName, "roles", cancellationToken))
        {
            _logger.LogInformation("El esquema principal ya existe en {DatabaseName}.", databaseName);
            return;
        }

        await ExecuteScriptInDatabaseAsync(masterConnection, databaseName, normalizedScript, cancellationToken);
        _logger.LogInformation("Esquema SQL base aplicado en {DatabaseName}.", databaseName);
    }

    private static async Task<bool> TableExistsAsync(
        string masterConnection,
        string databaseName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(masterConnection)
        {
            InitialCatalog = databaseName
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @tableName
) THEN 1 ELSE 0 END
""";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@tableName", tableName);
        var result = (int)await cmd.ExecuteScalarAsync(cancellationToken);
        return result == 1;
    }

    private static async Task EnsureDatabaseExistsAsync(
        string masterConnection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(masterConnection);
        await conn.OpenAsync(cancellationToken);

        const string commandText = """
IF DB_ID(@dbName) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE [' + REPLACE(@dbName, ']', ']]') + N']';
    EXEC(@sql);
END
""";

        await using var cmd = new SqlCommand(commandText, conn);
        cmd.Parameters.AddWithValue("@dbName", databaseName);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteScriptInDatabaseAsync(
        string masterConnection,
        string databaseName,
        string script,
        CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(masterConnection)
        {
            InitialCatalog = databaseName
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        var batches = GoSplitter
            .Split(script)
            .Select(batch => batch.Trim())
            .Where(batch => !string.IsNullOrWhiteSpace(batch))
            .ToList();

        foreach (var batch in batches)
        {
            await using var cmd = new SqlCommand(batch, conn);
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task EnsureDefaultAdminUserAsync(
        string masterConnection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(masterConnection)
        {
            InitialCatalog = databaseName
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
IF OBJECT_ID('roles','U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM roles WHERE nombre = 'Administrador')
        INSERT INTO roles (nombre) VALUES ('Administrador');
END

IF OBJECT_ID('usuarios','U') IS NOT NULL
BEGIN
    DECLARE @rolAdmin INT = (SELECT TOP 1 id_rol FROM roles WHERE nombre = 'Administrador');
    IF @rolAdmin IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM usuarios WHERE email = 'admin@gmail.com')
        BEGIN
            INSERT INTO usuarios
            (
                nombre, email, contrasenna, foto, preferencias_contacto, id_rol, estado
            )
            VALUES
            (
                'Administrador',
                'admin@gmail.com',
                @PasswordHash,
                NULL,
                NULL,
                @rolAdmin,
                'ACTIVO'
            );
        END
        ELSE
        BEGIN
            UPDATE usuarios
            SET contrasenna = @PasswordHash,
                id_rol = @rolAdmin,
                estado = 'ACTIVO'
            WHERE email = 'admin@gmail.com';
        END
    END
END
""";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@PasswordHash", BCrypt.Net.BCrypt.HashPassword("123456"));
        cmd.CommandTimeout = 120;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSupportTablesAndProceduresAsync(
        string masterConnection,
        string databaseName,
        CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(masterConnection)
        {
            InitialCatalog = databaseName
        };

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(cancellationToken);

        const string sql = """
IF OBJECT_ID('pacientes','U') IS NOT NULL AND OBJECT_ID('clientes','U') IS NULL
BEGIN
    EXEC sp_rename 'pacientes', 'clientes';
END

IF OBJECT_ID('citas','U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('citas') AND name = 'id_paciente'
    )
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_cita_paciente')
        BEGIN
            ALTER TABLE citas DROP CONSTRAINT fk_cita_paciente;
        END

        EXEC sp_rename 'citas.id_paciente', 'id_cliente', 'COLUMN';
    END
END

IF OBJECT_ID('clientes','U') IS NULL
BEGIN
    CREATE TABLE clientes (
        id_cliente INT IDENTITY(1,1) PRIMARY KEY,
        nombre_completo VARCHAR(150) NOT NULL,
        cedula VARCHAR(15) NULL,
        email VARCHAR(150) NOT NULL UNIQUE,
        telefono VARCHAR(30) NULL,
        fecha_registro DATETIME NOT NULL DEFAULT GETDATE()
    );
END

IF OBJECT_ID('clientes','U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID('clientes') AND name = 'cedula'
    )
    BEGIN
        ALTER TABLE clientes ADD cedula VARCHAR(15) NULL;
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_clientes_cedula')
    BEGIN
        CREATE UNIQUE INDEX UX_clientes_cedula
            ON clientes(cedula)
            WHERE cedula IS NOT NULL;
    END
END

IF OBJECT_ID('usuarios','U') IS NOT NULL AND OBJECT_ID('roles','U') IS NOT NULL
BEGIN
    INSERT INTO clientes (nombre_completo, cedula, email, telefono)
    SELECT u.nombre, NULL, u.email, NULL
    FROM usuarios u
    INNER JOIN roles r ON r.id_rol = u.id_rol
    WHERE r.nombre <> 'Administrador'
      AND NOT EXISTS (SELECT 1 FROM clientes c WHERE c.email = u.email);
END

IF OBJECT_ID('mensajes_contacto','U') IS NULL
BEGIN
    CREATE TABLE mensajes_contacto (
        id_mensaje INT IDENTITY(1,1) PRIMARY KEY,
        nombre_completo VARCHAR(150) NOT NULL,
        email VARCHAR(150) NOT NULL,
        asunto VARCHAR(200) NOT NULL,
        mensaje VARCHAR(1000) NOT NULL,
        fecha_registro DATETIME NOT NULL DEFAULT GETDATE()
    );
END

IF OBJECT_ID('citas','U') IS NULL
BEGIN
    CREATE TABLE citas (
        id_cita INT IDENTITY(1,1) PRIMARY KEY,
        id_cliente INT NOT NULL,
        propiedad_interes VARCHAR(200) NOT NULL,
        fecha_visita DATETIME NOT NULL,
        mensaje VARCHAR(1000) NULL,
        estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
        fecha_registro DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT fk_cita_cliente FOREIGN KEY (id_cliente) REFERENCES clientes(id_cliente)
    );
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'fk_cita_cliente')
    BEGIN
        ALTER TABLE citas
        ADD CONSTRAINT fk_cita_cliente FOREIGN KEY (id_cliente) REFERENCES clientes(id_cliente);
    END
END

IF OBJECT_ID('sp_ObtenerUsuarioPorEmail','P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerUsuarioPorEmail;
EXEC('
CREATE PROCEDURE sp_ObtenerUsuarioPorEmail
    @Email VARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 u.contrasenna, r.nombre AS Rol
    FROM usuarios u
    INNER JOIN roles r ON r.id_rol = u.id_rol
    WHERE email = @Email
      AND u.estado = ''ACTIVO'';
END
');

IF OBJECT_ID('sp_ActualizarHashUsuario','P') IS NOT NULL
    DROP PROCEDURE sp_ActualizarHashUsuario;
EXEC('
CREATE PROCEDURE sp_ActualizarHashUsuario
    @Email VARCHAR(150),
    @PasswordHash VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios
    SET contrasenna = @PasswordHash
    WHERE email = @Email;
END
');

IF OBJECT_ID('sp_CambiarContrasennaPorEmail','P') IS NOT NULL
    DROP PROCEDURE sp_CambiarContrasennaPorEmail;
EXEC('
CREATE PROCEDURE sp_CambiarContrasennaPorEmail
    @Email VARCHAR(150),
    @PasswordHash VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios
    SET contrasenna = @PasswordHash
    WHERE LOWER(email) = LOWER(@Email);
END
');

IF OBJECT_ID('sp_ValidarClienteRecuperacion','P') IS NOT NULL
    DROP PROCEDURE sp_ValidarClienteRecuperacion;
EXEC('
CREATE PROCEDURE sp_ValidarClienteRecuperacion
    @Email VARCHAR(150),
    @Cedula VARCHAR(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM clientes c
        INNER JOIN usuarios u ON LOWER(u.email) = LOWER(c.email)
        WHERE LOWER(c.email) = LOWER(@Email)
          AND c.cedula = @Cedula
    ) THEN 1 ELSE 0 END;
END
');

IF OBJECT_ID('sp_RegistrarErrorSistema','P') IS NOT NULL
    DROP PROCEDURE sp_RegistrarErrorSistema;
EXEC('
CREATE PROCEDURE sp_RegistrarErrorSistema
    @Codigo VARCHAR(50),
    @Mensaje VARCHAR(500),
    @Severidad VARCHAR(20),
    @Email VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdUsuario INT = NULL;
    IF @Email IS NOT NULL
    BEGIN
        SELECT TOP 1 @IdUsuario = id_usuario
        FROM usuarios
        WHERE LOWER(email) = LOWER(@Email);
    END

    INSERT INTO errores_sistema (id_usuario, codigo, mensaje, severidad)
    VALUES (@IdUsuario, @Codigo, @Mensaje, @Severidad);
END
');

IF OBJECT_ID('sp_RegistrarMensajeContacto','P') IS NOT NULL
    DROP PROCEDURE sp_RegistrarMensajeContacto;
EXEC('
CREATE PROCEDURE sp_RegistrarMensajeContacto
    @NombreCompleto VARCHAR(150),
    @Email VARCHAR(150),
    @Asunto VARCHAR(200),
    @Mensaje VARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO mensajes_contacto (nombre_completo, email, asunto, mensaje)
    VALUES (@NombreCompleto, @Email, @Asunto, @Mensaje);
END
');

IF OBJECT_ID('sp_RegistrarMensajeCliente','P') IS NOT NULL
    DROP PROCEDURE sp_RegistrarMensajeCliente;
EXEC('
CREATE PROCEDURE sp_RegistrarMensajeCliente
    @Email VARCHAR(150),
    @Asunto VARCHAR(200),
    @Mensaje VARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NombreCompleto VARCHAR(150);
    SELECT TOP 1 @NombreCompleto = c.nombre_completo
    FROM clientes c
    WHERE LOWER(c.email) = LOWER(@Email);

    IF @NombreCompleto IS NULL
        SET @NombreCompleto = ''Cliente'';

    INSERT INTO mensajes_contacto (nombre_completo, email, asunto, mensaje)
    VALUES (@NombreCompleto, @Email, @Asunto, @Mensaje);
END
');

IF OBJECT_ID('sp_BuscarClientePorCedula','P') IS NOT NULL
    DROP PROCEDURE sp_BuscarClientePorCedula;
EXEC('
CREATE PROCEDURE sp_BuscarClientePorCedula
    @Cedula VARCHAR(15)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        c.cedula AS Cedula,
        c.nombre_completo AS NombreCompleto,
        c.telefono AS Telefono,
        c.email AS Email
    FROM clientes c
    WHERE c.cedula = @Cedula;
END
');

IF OBJECT_ID('sp_AgendarCita','P') IS NOT NULL
    DROP PROCEDURE sp_AgendarCita;
EXEC('
CREATE PROCEDURE sp_AgendarCita
    @NombreCompleto VARCHAR(150),
    @Email VARCHAR(150),
    @Telefono VARCHAR(30),
    @FechaVisita DATETIME,
    @PropiedadInteres VARCHAR(200),
    @Mensaje VARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdCliente INT;

    SELECT TOP 1 @IdCliente = id_cliente
    FROM clientes
    WHERE email = @Email;

    IF @IdCliente IS NULL
    BEGIN
        INSERT INTO clientes (nombre_completo, email, telefono)
        VALUES (@NombreCompleto, @Email, @Telefono);

        SET @IdCliente = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        -- Si el cliente viene de registro formal (tiene cédula), no se pisa su perfil
        -- con datos del formulario de cita.
        UPDATE clientes
        SET nombre_completo = CASE WHEN ISNULL(LTRIM(RTRIM(cedula)), '''') = '''' THEN @NombreCompleto ELSE nombre_completo END,
            telefono = CASE WHEN ISNULL(LTRIM(RTRIM(cedula)), '''') = '''' THEN @Telefono ELSE telefono END
        WHERE id_cliente = @IdCliente;
    END

    INSERT INTO citas (id_cliente, propiedad_interes, fecha_visita, mensaje)
    VALUES (@IdCliente, @PropiedadInteres, @FechaVisita, @Mensaje);
END
');

IF OBJECT_ID('sp_RegistrarUsuarioCliente','P') IS NOT NULL
    DROP PROCEDURE sp_RegistrarUsuarioCliente;
EXEC('
CREATE PROCEDURE sp_RegistrarUsuarioCliente
    @NombreCompleto VARCHAR(150),
    @Cedula VARCHAR(15),
    @Telefono VARCHAR(30),
    @Email VARCHAR(150),
    @PasswordHash VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Cedula IS NULL OR @Cedula NOT LIKE ''[0-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]''
    BEGIN
        SELECT 0;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM usuarios WHERE email = @Email)
    BEGIN
        SELECT 0;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM clientes WHERE cedula = @Cedula)
    BEGIN
        SELECT 0;
        RETURN;
    END

    DECLARE @RolCliente INT = (
        SELECT TOP 1 id_rol
        FROM roles
        WHERE nombre IN (''Inquilino'', ''Invitado'')
        ORDER BY CASE WHEN nombre = ''Inquilino'' THEN 0 ELSE 1 END
    );

    IF @RolCliente IS NULL
    BEGIN
        INSERT INTO roles (nombre) VALUES (''Inquilino'');
        SET @RolCliente = SCOPE_IDENTITY();
    END

    INSERT INTO usuarios
    (
        nombre, email, contrasenna, foto, preferencias_contacto, id_rol, estado
    )
    VALUES
    (
        @NombreCompleto, @Email, @PasswordHash, NULL, NULL, @RolCliente, ''ACTIVO''
    );

    IF NOT EXISTS (SELECT 1 FROM clientes WHERE email = @Email)
    BEGIN
        INSERT INTO clientes (nombre_completo, cedula, email, telefono)
        VALUES (@NombreCompleto, @Cedula, @Email, @Telefono);
    END

    SELECT 1;
END
');

IF OBJECT_ID('sp_ListarClientes','P') IS NOT NULL
    DROP PROCEDURE sp_ListarClientes;
EXEC('
CREATE PROCEDURE sp_ListarClientes
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        c.nombre_completo AS NombreCompleto,
        c.cedula AS Cedula,
        c.email AS Email,
        c.telefono AS Telefono,
        c.fecha_registro AS FechaRegistro
    FROM clientes c
    INNER JOIN usuarios u ON u.email = c.email
    INNER JOIN roles r ON r.id_rol = u.id_rol
    WHERE r.nombre IN (''Inquilino'', ''Invitado'')
      AND c.cedula IS NOT NULL
      AND LTRIM(RTRIM(c.cedula)) <> ''''
    ORDER BY c.fecha_registro DESC;
END
');

IF OBJECT_ID('sp_ListarCitas','P') IS NOT NULL
    DROP PROCEDURE sp_ListarCitas;
EXEC('
CREATE PROCEDURE sp_ListarCitas
    @FechaDesde DATETIME = NULL,
    @FechaHasta DATETIME = NULL,
    @Estado VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ci.id_cita AS IdCita,
        c.nombre_completo AS Cliente,
        c.email AS Email,
        c.telefono AS Telefono,
        ci.propiedad_interes AS PropiedadInteres,
        ci.fecha_visita AS FechaVisita,
        ci.estado AS Estado
    FROM citas ci
    INNER JOIN clientes c ON c.id_cliente = ci.id_cliente
    WHERE (@FechaDesde IS NULL OR ci.fecha_visita >= @FechaDesde)
      AND (@FechaHasta IS NULL OR ci.fecha_visita < DATEADD(DAY, 1, @FechaHasta))
      AND (@Estado IS NULL OR LTRIM(RTRIM(@Estado)) = '''' OR ci.estado = @Estado)
    ORDER BY ci.fecha_visita DESC;
END
');

IF OBJECT_ID('sp_ActualizarEstadoCita','P') IS NOT NULL
    DROP PROCEDURE sp_ActualizarEstadoCita;
EXEC('
CREATE PROCEDURE sp_ActualizarEstadoCita
    @IdCita INT,
    @Estado VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE citas
    SET estado = @Estado
    WHERE id_cita = @IdCita;
END
');

IF OBJECT_ID('sp_ListarCitasPorClienteEmail','P') IS NOT NULL
    DROP PROCEDURE sp_ListarCitasPorClienteEmail;
EXEC('
CREATE PROCEDURE sp_ListarCitasPorClienteEmail
    @Email VARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        ci.id_cita AS IdCita,
        c.nombre_completo AS Cliente,
        c.email AS Email,
        c.telefono AS Telefono,
        ci.propiedad_interes AS PropiedadInteres,
        ci.fecha_visita AS FechaVisita,
        ci.estado AS Estado
    FROM citas ci
    INNER JOIN clientes c ON c.id_cliente = ci.id_cliente
    WHERE LOWER(c.email) = LOWER(@Email)
    ORDER BY ci.fecha_visita DESC;
END
');

IF OBJECT_ID('sp_ListarMensajesContacto','P') IS NOT NULL
    DROP PROCEDURE sp_ListarMensajesContacto;
EXEC('
CREATE PROCEDURE sp_ListarMensajesContacto
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.nombre_completo AS NombreCompleto,
        ISNULL(c.cedula, '''') AS Cedula,
        m.email AS Email,
        c.telefono AS Telefono,
        m.asunto AS Asunto,
        m.mensaje AS Mensaje,
        m.fecha_registro AS FechaRegistro
    FROM mensajes_contacto m
    LEFT JOIN clientes c ON LOWER(c.email) = LOWER(m.email)
    ORDER BY m.fecha_registro DESC;
END
');
""";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 120;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string RemoveCreateDatabaseAndUseStatements(string script)
    {
        var lines = script.Split(["\r\n", "\n"], StringSplitOptions.None);
        var filtered = lines.Where(line =>
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("CREATE DATABASE", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (trimmed.StartsWith("USE ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        });

        return string.Join(Environment.NewLine, filtered);
    }
}
