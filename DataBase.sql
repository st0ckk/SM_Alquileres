CREATE DATABASE ProyectoAlquileres;
GO

USE ProyectoAlquileres;
GO

CREATE TABLE roles (
    id_rol INT IDENTITY(1,1) PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL UNIQUE
);
GO

INSERT INTO roles (nombre) VALUES (N'Invitado');
INSERT INTO roles (nombre) VALUES (N'Administrador');
GO

CREATE TABLE usuarios (
    id_usuario INT IDENTITY(1,1) PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    email VARCHAR(150) NOT NULL UNIQUE,
    contrasenna VARCHAR(255) NOT NULL,
    foto VARCHAR(255) NULL,
    preferencias_contacto VARCHAR(200) NULL,
    id_rol INT NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT N'ACTIVO',
    CONSTRAINT fk_usuario_rol FOREIGN KEY (id_rol) REFERENCES roles (id_rol)
);
GO

-- Único usuario administrador precargado (presentación): correo admin@gmail.com, contraseña 123456
INSERT INTO usuarios (nombre, email, contrasenna, foto, preferencias_contacto, id_rol, estado)
SELECT
    N'Administrador del sistema',
    N'admin@gmail.com',
    N'$2a$11$8ELHP3cAlwAdvhibPIuQUOMLUWOpQNPHv80hqZJg2VS50lCgc/rum',
    NULL,
    NULL,
    r.id_rol,
    N'ACTIVO'
FROM roles r
WHERE r.nombre = N'Administrador'
  AND NOT EXISTS (SELECT 1 FROM usuarios u WHERE LOWER(u.email) = LOWER(N'admin@gmail.com'));
GO

CREATE TABLE clientes (
    id_cliente INT IDENTITY(1,1) PRIMARY KEY,
    nombre_completo VARCHAR(150) NOT NULL,
    cedula VARCHAR(15) NULL,
    email VARCHAR(150) NOT NULL UNIQUE,
    telefono VARCHAR(30) NULL,
    fecha_registro DATETIME NOT NULL DEFAULT GETDATE()
);
GO

CREATE UNIQUE INDEX UX_clientes_cedula ON clientes (cedula) WHERE cedula IS NOT NULL;
GO

CREATE TABLE mensajes_contacto (
    id_mensaje INT IDENTITY(1,1) PRIMARY KEY,
    nombre_completo VARCHAR(150) NOT NULL,
    email VARCHAR(150) NOT NULL,
    asunto VARCHAR(200) NOT NULL,
    mensaje VARCHAR(1000) NOT NULL,
    fecha_registro DATETIME NOT NULL DEFAULT GETDATE()
);
GO

CREATE TABLE citas (
    id_cita INT IDENTITY(1,1) PRIMARY KEY,
    id_cliente INT NOT NULL,
    propiedad_interes VARCHAR(200) NOT NULL,
    fecha_visita DATETIME NOT NULL,
    mensaje VARCHAR(1000) NULL,
    estado VARCHAR(20) NOT NULL DEFAULT N'PENDIENTE',
    fecha_registro DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_cita_cliente FOREIGN KEY (id_cliente) REFERENCES clientes (id_cliente)
);
GO

CREATE TABLE propiedades (
    id_propiedad INT IDENTITY(1,1) PRIMARY KEY,
    id_propietario INT NOT NULL,
    tipo VARCHAR(50) NOT NULL,
    descripcion VARCHAR(2000) NOT NULL,
    precio_mensual DECIMAL(10,2) NOT NULL,
    ubicacion VARCHAR(200) NOT NULL,
    espacio_total_m2 INT NOT NULL DEFAULT 0,
    numero_piso VARCHAR(40) NOT NULL DEFAULT N'N/A',
    numero_habitaciones INT NOT NULL,
    estacionamiento_disponible BIT NOT NULL DEFAULT 1,
    proceso_pago VARCHAR(80) NOT NULL DEFAULT N'Banco',
    url_foto VARCHAR(500) NULL,
    disponible BIT NOT NULL DEFAULT 1,
    CONSTRAINT fk_propiedad_propietario FOREIGN KEY (id_propietario) REFERENCES usuarios (id_usuario)
);
GO

IF COL_LENGTH('dbo.citas', 'id_propiedad') IS NULL
BEGIN
    ALTER TABLE dbo.citas ADD id_propiedad INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'fk_cita_propiedad'
      AND parent_object_id = OBJECT_ID(N'dbo.citas')
)
BEGIN
    ALTER TABLE dbo.citas WITH CHECK ADD CONSTRAINT fk_cita_propiedad FOREIGN KEY (id_propiedad) REFERENCES dbo.propiedades (id_propiedad);
END
GO

CREATE TABLE errores_sistema (
    id_error INT IDENTITY(1,1) PRIMARY KEY,
    id_usuario INT NULL,
    codigo VARCHAR(50) NOT NULL,
    mensaje VARCHAR(500) NOT NULL,
    severidad VARCHAR(20) NOT NULL,
    fecha_error DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_error_usuario FOREIGN KEY (id_usuario) REFERENCES usuarios (id_usuario)
);
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerUsuarioPorEmail
    @Email VARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 u.contrasenna, r.nombre AS Rol
    FROM usuarios u
    INNER JOIN roles r ON r.id_rol = u.id_rol
    WHERE u.email = @Email
      AND u.estado = N'ACTIVO';
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ObtenerIdUsuarioPorEmail
    @Email VARCHAR(150)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 u.id_usuario AS IdUsuario
    FROM dbo.usuarios u
    WHERE LOWER(u.email) = LOWER(@Email)
      AND u.estado = N'ACTIVO';
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ActualizarHashUsuario
    @Email VARCHAR(150),
    @PasswordHash VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios SET contrasenna = @PasswordHash WHERE email = @Email;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_CambiarContrasennaPorEmail
    @Email VARCHAR(150),
    @PasswordHash VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE usuarios SET contrasenna = @PasswordHash WHERE LOWER(email) = LOWER(@Email);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ValidarClienteRecuperacion
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
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarErrorSistema
    @Codigo VARCHAR(50),
    @Mensaje VARCHAR(500),
    @Severidad VARCHAR(20),
    @Email VARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @IdUsuario INT = NULL;
    IF @Email IS NOT NULL
        SELECT TOP 1 @IdUsuario = id_usuario FROM usuarios WHERE LOWER(email) = LOWER(@Email);
    INSERT INTO errores_sistema (id_usuario, codigo, mensaje, severidad)
    VALUES (@IdUsuario, @Codigo, @Mensaje, @Severidad);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarMensajeContacto
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
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarMensajeCliente
    @Email VARCHAR(150),
    @Asunto VARCHAR(200),
    @Mensaje VARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @NombreCompleto VARCHAR(150);
    SELECT TOP 1 @NombreCompleto = c.nombre_completo FROM clientes c WHERE LOWER(c.email) = LOWER(@Email);
    IF @NombreCompleto IS NULL SET @NombreCompleto = N'Cliente';
    INSERT INTO mensajes_contacto (nombre_completo, email, asunto, mensaje)
    VALUES (@NombreCompleto, @Email, @Asunto, @Mensaje);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_BuscarClientePorCedula
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
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarHorasOcupadasCita
    @PropiedadInteres VARCHAR(200),
    @Fecha DATE,
    @IdPropiedad INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DATEPART(HOUR, ci.fecha_visita) AS Hora
    FROM citas ci
    WHERE CAST(ci.fecha_visita AS DATE) = @Fecha
      AND LTRIM(RTRIM(ISNULL(ci.estado, N''))) NOT IN (N'CANCELADA', N'RECHAZADA')
      AND (
          (@IdPropiedad IS NOT NULL
              AND (
                  ci.id_propiedad = @IdPropiedad
                  OR (
                      ci.id_propiedad IS NULL
                      AND LOWER(LTRIM(RTRIM(ci.propiedad_interes))) = LOWER(LTRIM(RTRIM(@PropiedadInteres)))
                  )
              ))
          OR (
              @IdPropiedad IS NULL
              AND LOWER(LTRIM(RTRIM(ci.propiedad_interes))) = LOWER(LTRIM(RTRIM(@PropiedadInteres)))
          )
      );
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_AgendarCita
    @NombreCompleto VARCHAR(150),
    @Email VARCHAR(150),
    @Telefono VARCHAR(30),
    @FechaVisita DATETIME,
    @PropiedadInteres VARCHAR(200),
    @Mensaje VARCHAR(1000),
    @IdPropiedad INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM citas ci
        WHERE CAST(ci.fecha_visita AS DATE) = CAST(@FechaVisita AS DATE)
          AND DATEPART(HOUR, ci.fecha_visita) = DATEPART(HOUR, @FechaVisita)
          AND LTRIM(RTRIM(ISNULL(ci.estado, N''))) NOT IN (N'CANCELADA', N'RECHAZADA')
          AND (
              (@IdPropiedad IS NOT NULL
                  AND (
                      ci.id_propiedad = @IdPropiedad
                      OR (
                          ci.id_propiedad IS NULL
                          AND LOWER(LTRIM(RTRIM(ci.propiedad_interes))) = LOWER(LTRIM(RTRIM(@PropiedadInteres)))
                      )
                  ))
              OR (
                  @IdPropiedad IS NULL
                  AND LOWER(LTRIM(RTRIM(ci.propiedad_interes))) = LOWER(LTRIM(RTRIM(@PropiedadInteres)))
              )
          )
    )
    BEGIN
        SELECT CAST(0 AS INT) AS Resultado;
        RETURN;
    END
    DECLARE @IdCliente INT;
    SELECT TOP 1 @IdCliente = id_cliente FROM clientes WHERE email = @Email;
    IF @IdCliente IS NULL
    BEGIN
        INSERT INTO clientes (nombre_completo, email, telefono) VALUES (@NombreCompleto, @Email, @Telefono);
        SET @IdCliente = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE clientes
        SET nombre_completo = CASE WHEN ISNULL(LTRIM(RTRIM(cedula)), N'') = N'' THEN @NombreCompleto ELSE nombre_completo END,
            telefono = CASE WHEN ISNULL(LTRIM(RTRIM(cedula)), N'') = N'' THEN @Telefono ELSE telefono END
        WHERE id_cliente = @IdCliente;
    END
    INSERT INTO citas (id_cliente, propiedad_interes, fecha_visita, mensaje, id_propiedad)
    VALUES (@IdCliente, @PropiedadInteres, @FechaVisita, @Mensaje, @IdPropiedad);
    SELECT CAST(1 AS INT) AS Resultado;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RegistrarUsuarioCliente
    @NombreCompleto VARCHAR(150),
    @Cedula VARCHAR(15),
    @Telefono VARCHAR(30),
    @Email VARCHAR(150),
    @PasswordHash VARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;
    IF @Cedula IS NULL OR @Cedula NOT LIKE '[0-9]-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'
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
    DECLARE @RolCliente INT = (SELECT TOP 1 id_rol FROM roles WHERE nombre = N'Invitado');
    IF @RolCliente IS NULL
    BEGIN
        SELECT 0;
        RETURN;
    END
    INSERT INTO usuarios (nombre, email, contrasenna, foto, preferencias_contacto, id_rol, estado)
    VALUES (@NombreCompleto, @Email, @PasswordHash, NULL, NULL, @RolCliente, N'ACTIVO');
    IF NOT EXISTS (SELECT 1 FROM clientes WHERE email = @Email)
        INSERT INTO clientes (nombre_completo, cedula, email, telefono)
        VALUES (@NombreCompleto, @Cedula, @Email, @Telefono);
    SELECT 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarClientes
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
    WHERE r.nombre = N'Invitado'
      AND c.cedula IS NOT NULL
      AND LTRIM(RTRIM(c.cedula)) <> N''
    ORDER BY c.fecha_registro DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ContarPropiedadesActivas
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(1) AS Total FROM dbo.propiedades WHERE disponible = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarPropiedadesActivasPaginado
    @Pagina INT,
    @TamanoPagina INT
AS
BEGIN
    SET NOCOUNT ON;
    IF @Pagina < 1 SET @Pagina = 1;
    IF @TamanoPagina < 1 SET @TamanoPagina = 10;
    IF @TamanoPagina > 100 SET @TamanoPagina = 100;
    DECLARE @Offset INT = (@Pagina - 1) * @TamanoPagina;
    SELECT
        p.id_propiedad AS IdPropiedad,
        p.tipo AS Tipo,
        p.descripcion AS Descripcion,
        p.precio_mensual AS PrecioMensual,
        p.ubicacion AS Ubicacion,
        p.espacio_total_m2 AS EspacioTotalM2,
        p.numero_piso AS NumeroPiso,
        p.numero_habitaciones AS NumeroHabitaciones,
        CAST(p.estacionamiento_disponible AS BIT) AS EstacionamientoDisponible,
        p.proceso_pago AS ProcesoPago,
        p.url_foto AS UrlFoto
    FROM dbo.propiedades p
    WHERE p.disponible = 1
    ORDER BY p.id_propiedad DESC
    OFFSET @Offset ROWS FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarPropiedadesAdmin
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        p.id_propiedad AS IdPropiedad,
        p.tipo AS Tipo,
        p.descripcion AS Descripcion,
        p.precio_mensual AS PrecioMensual,
        p.ubicacion AS Ubicacion,
        p.espacio_total_m2 AS EspacioTotalM2,
        p.numero_piso AS NumeroPiso,
        p.numero_habitaciones AS NumeroHabitaciones,
        CAST(p.estacionamiento_disponible AS BIT) AS EstacionamientoDisponible,
        p.proceso_pago AS ProcesoPago,
        p.url_foto AS UrlFoto,
        CAST(p.disponible AS BIT) AS Disponible
    FROM dbo.propiedades p
    ORDER BY p.disponible DESC, p.id_propiedad DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ActualizarPropiedad
    @IdPropiedad INT,
    @Tipo VARCHAR(50),
    @Descripcion VARCHAR(2000),
    @PrecioMensual DECIMAL(10,2),
    @Ubicacion VARCHAR(200),
    @EspacioTotalM2 INT,
    @NumeroPiso VARCHAR(40),
    @NumeroHabitaciones INT,
    @EstacionamientoDisponible BIT,
    @ProcesoPago VARCHAR(80),
    @UrlFoto VARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.propiedades
    SET tipo = @Tipo,
        descripcion = @Descripcion,
        precio_mensual = @PrecioMensual,
        ubicacion = @Ubicacion,
        espacio_total_m2 = @EspacioTotalM2,
        numero_piso = @NumeroPiso,
        numero_habitaciones = @NumeroHabitaciones,
        estacionamiento_disponible = @EstacionamientoDisponible,
        proceso_pago = @ProcesoPago,
        url_foto = @UrlFoto
    WHERE id_propiedad = @IdPropiedad;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_SetPropiedadDisponible
    @IdPropiedad INT,
    @Disponible BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.propiedades SET disponible = @Disponible WHERE id_propiedad = @IdPropiedad;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_InsertarPropiedad
    @IdPropietario INT,
    @Tipo VARCHAR(50),
    @Descripcion VARCHAR(2000),
    @PrecioMensual DECIMAL(10,2),
    @Ubicacion VARCHAR(200),
    @EspacioTotalM2 INT,
    @NumeroPiso VARCHAR(40),
    @NumeroHabitaciones INT,
    @EstacionamientoDisponible BIT,
    @ProcesoPago VARCHAR(80),
    @UrlFoto VARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.propiedades (
        id_propietario, tipo, descripcion, precio_mensual, ubicacion,
        espacio_total_m2, numero_piso, numero_habitaciones,
        estacionamiento_disponible, proceso_pago, url_foto, disponible
    )
    VALUES (
        @IdPropietario, @Tipo, @Descripcion, @PrecioMensual, @Ubicacion,
        @EspacioTotalM2, @NumeroPiso, @NumeroHabitaciones,
        @EstacionamientoDisponible, @ProcesoPago, @UrlFoto, 1
    );
    SELECT CAST(SCOPE_IDENTITY() AS INT) AS IdPropiedad;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarCitas
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
      AND (@Estado IS NULL OR LTRIM(RTRIM(@Estado)) = N'' OR ci.estado = @Estado)
    ORDER BY ci.fecha_visita DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ActualizarEstadoCita
    @IdCita INT,
    @Estado VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE citas SET estado = @Estado WHERE id_cita = @IdCita;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarCitasPorClienteEmail
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
GO

CREATE OR ALTER PROCEDURE dbo.sp_ListarMensajesContacto
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        m.nombre_completo AS NombreCompleto,
        ISNULL(c.cedula, N'') AS Cedula,
        m.email AS Email,
        c.telefono AS Telefono,
        m.asunto AS Asunto,
        m.mensaje AS Mensaje,
        m.fecha_registro AS FechaRegistro
    FROM mensajes_contacto m
    LEFT JOIN clientes c ON LOWER(c.email) = LOWER(m.email)
    ORDER BY m.fecha_registro DESC;
END
GO
