CREATE DATABASE SGAP;
GO

USE SGAP;
GO

CREATE TABLE roles (
    id_rol INT IDENTITY(1,1) PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL UNIQUE
);
GO

INSERT INTO roles (nombre) VALUES ('Propietario');
INSERT INTO roles (nombre) VALUES ('Inquilino');
INSERT INTO roles (nombre) VALUES ('Invitado');
INSERT INTO roles (nombre) VALUES ('Administrador');
GO

CREATE TABLE usuarios (
    id_usuario INT IDENTITY(1,1) PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    email VARCHAR(150) NOT NULL UNIQUE,
    contrasenna VARCHAR(255) NOT NULL,
    foto VARCHAR(255),
    preferencias_contacto VARCHAR(200),
    id_rol INT NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'ACTIVO',
    CONSTRAINT fk_usuario_rol 
        FOREIGN KEY (id_rol) REFERENCES roles(id_rol)
);
GO

CREATE TABLE propiedades (
    id_propiedad INT IDENTITY(1,1) PRIMARY KEY,
    id_propietario INT NOT NULL,
    descripcion VARCHAR(500) NOT NULL,
    precio_mensual DECIMAL(10,2) NOT NULL,
    ubicacion VARCHAR(200) NOT NULL,
    tipo VARCHAR(50) NOT NULL,
    numero_habitaciones INT NOT NULL,
    disponible BIT NOT NULL,
    CONSTRAINT fk_propiedad_propietario 
        FOREIGN KEY (id_propietario) REFERENCES usuarios(id_usuario)
);
GO

CREATE TABLE fotos_propiedad (
    id_foto INT IDENTITY(1,1) PRIMARY KEY,
    id_propiedad INT NOT NULL,
    url_foto VARCHAR(255) NOT NULL,
    CONSTRAINT fk_foto_propiedad 
        FOREIGN KEY (id_propiedad) REFERENCES propiedades(id_propiedad)
);
GO


CREATE TABLE reservas (
    id_reserva INT IDENTITY(1,1) PRIMARY KEY,
    id_propiedad INT NOT NULL,
    id_inquilino INT NOT NULL,
    fecha_inicio DATE NOT NULL,
    fecha_fin DATE NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    justificacion VARCHAR(500),
    CONSTRAINT fk_reserva_propiedad 
        FOREIGN KEY (id_propiedad) REFERENCES propiedades(id_propiedad),
    CONSTRAINT fk_reserva_inquilino 
        FOREIGN KEY (id_inquilino) REFERENCES usuarios(id_usuario)
);
GO

CREATE TABLE contratos (
    id_contrato INT IDENTITY(1,1) PRIMARY KEY,
    id_reserva INT NOT NULL UNIQUE,
    fecha_inicio DATE NOT NULL,
    fecha_fin DATE NOT NULL,
    monto_mensual DECIMAL(10,2) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'ACTIVO',
    CONSTRAINT fk_contrato_reserva 
        FOREIGN KEY (id_reserva) REFERENCES reservas(id_reserva)
);
GO

CREATE TABLE pagos (
    id_pago INT IDENTITY(1,1) PRIMARY KEY,
    id_contrato INT NOT NULL,
    fecha_pago DATE NOT NULL,
    monto DECIMAL(10,2) NOT NULL,
    CONSTRAINT fk_pago_contrato 
        FOREIGN KEY (id_contrato) REFERENCES contratos(id_contrato)
);
GO

CREATE TABLE reportes_mantenimiento (
    id_reporte INT IDENTITY(1,1) PRIMARY KEY,
    id_propiedad INT NOT NULL,
    id_inquilino INT NOT NULL,
    descripcion VARCHAR(500) NOT NULL,
    categoria VARCHAR(100) NOT NULL,
    foto VARCHAR(255),
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE',
    CONSTRAINT fk_reporte_propiedad 
        FOREIGN KEY (id_propiedad) REFERENCES propiedades(id_propiedad),
    CONSTRAINT fk_reporte_inquilino 
        FOREIGN KEY (id_inquilino) REFERENCES usuarios(id_usuario)
);
GO

CREATE TABLE notificaciones (
    id_notificacion INT IDENTITY(1,1) PRIMARY KEY,
    id_usuario INT NOT NULL,
    mensaje VARCHAR(500) NOT NULL,
    fecha_envio DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_notificacion_usuario 
        FOREIGN KEY (id_usuario) REFERENCES usuarios(id_usuario)
);
GO

CREATE TABLE errores_sistema (
    id_error INT IDENTITY(1,1) PRIMARY KEY,
    id_usuario INT NULL,
    codigo VARCHAR(50) NOT NULL,
    mensaje VARCHAR(500) NOT NULL,
    severidad VARCHAR(20) NOT NULL,
    fecha_error DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT fk_error_usuario
        FOREIGN KEY (id_usuario) REFERENCES usuarios(id_usuario)
);
GO