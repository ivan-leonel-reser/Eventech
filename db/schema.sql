-- EvenTech - Esquema de la base de datos (db/schema.sql)
--
-- Script idempotente: crea lo que falte y migra lo que exista, asi que sirve
-- tanto para levantar una base nueva como para actualizar una base anterior.
-- Se ejecuta SOBRE la base que indica -d (no crea la base ni cambia de
-- contexto); la base se crea aparte, ver db/README.md.
--
-- Inventario (27 tablas):
--   Seguridad y acceso ...... Users, LoginAuditLog, Perfiles, Permisos,
--                             PerfilPermiso, PerfilIncluido
--   Negocio (RFN1) .......... Clientes, Salones, Reservas, Servicios,
--                             ReservaServicio, MetodosPago, Pagos
--   Operaciones (RFN2) ...... Especialidades, Empleados, AsignacionesPersonal,
--                             Cronogramas, CronogramaActividades, Tareas,
--                             Incidencias
--   Auditoria e integridad .. Bitacora, HistorialCambios, ReservaMemento,
--                             ReservaMementoServicio, DVVertical
--   Idiomas ................. Idiomas, Traducciones
-- Semillas: usuario admin/admin123, arbol de permisos, perfiles Administrador,
-- Vendedor, Supervisor, Gerencial, Coordinador y Empleado, catalogos de ejemplo
-- (salones, servicios, metodos de pago, especialidades del personal, dos
-- clientes) y las traducciones ES/EN/PT de la interfaz.

-- Los indices filtrados (UX_Clientes_Dni, UX_Reservas_SalonFecha_Confirmada,
-- UX_Empleados_UserId) exigen QUOTED_IDENTIFIER ON. El sqlcmd ODBC (el de las
-- utilidades de linea de comandos de SQL Server) arranca con OFF salvo que se
-- pase -I, asi que se fija aca para no depender del cliente.
-- NOEXEC OFF va primero: si en la misma sesion (una ventana de SSMS) una corrida
-- anterior cayo en la guarda de abajo, la sesion quedo con NOEXEC ON y todo lo
-- que sigue se compilaria sin ejecutarse, guarda incluida: volver a ejecutar
-- terminaria "sin errores" sin crear nada. Asi la guarda se evalua de nuevo.
SET NOEXEC OFF;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Guarda: el script no debe correr sobre una base del sistema (pasa al
-- olvidar -d). Con sqlcmd -b el error corta la ejecucion; sin -b, NOEXEC deja
-- el resto del script sin ejecutar. El ultimo lote del script vuelve a SET
-- NOEXEC OFF, asi que en SSMS alcanza con elegir la base en el combo y volver
-- a ejecutar (verificado en una misma sesion: la segunda corrida crea el esquema).
IF DB_NAME() IN (N'master', N'tempdb', N'model', N'msdb')
BEGIN
    DECLARE @baseActual SYSNAME = DB_NAME();
    RAISERROR(N'schema.sql: la base actual es "%s". Ejecutar con -d <base> sobre la base de EvenTech; en SSMS, elegir esa base en el combo y volver a ejecutar (ver db/README.md).', 16, 1, @baseActual);
    SET NOEXEC ON;
END
GO

-- Lecturas por version (READ_COMMITTED_SNAPSHOT). El sistema se usa desde mas de
-- un puesto: las operaciones que validan y escriben leen la cabecera de la
-- reserva con bloqueo y se ejecutan una detras de la otra; las consultas de las
-- pantallas (operaciones, agenda, equipo del evento) solo leen. Con el
-- aislamiento por bloqueos, una de esas consultas y una escritura sobre el mismo
-- evento podian esperarse entre si y el motor cortaba una de las dos (error
-- 1205). Con las lecturas por version, quien consulta ve el ultimo dato
-- confirmado sin esperar a quien escribe, y los bloqueos pedidos de forma
-- explicita por las escrituras se siguen respetando.
-- Es una opcion de la base (viaja con el respaldo). El cambio exige que no haya
-- otra sesion a mitad de una transaccion: ROLLBACK IMMEDIATE la deshace en lugar
-- de dejar el script esperando. Solo se aplica si todavia no esta activa.
IF EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID() AND is_read_committed_snapshot_on = 0)
    ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
GO

-- Tabla de usuarios.
-- PasswordHash: SHA-256 hex (64 chars) generado en cliente. La password en claro
-- nunca viaja a la DB.
IF OBJECT_ID('dbo.Users','U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
        Username      NVARCHAR(50)      NOT NULL,
        PasswordHash  NVARCHAR(64)      NOT NULL,
        CreatedAt     DATETIME          NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT UQ_Users_Username UNIQUE (Username)
    );
END
GO

-- Estado de cuenta + control de intentos fallidos (RF01.3 / RF01.4). Idempotente.
IF COL_LENGTH('dbo.Users','Activo') IS NULL
    ALTER TABLE dbo.Users ADD Activo BIT NOT NULL CONSTRAINT DF_Users_Activo DEFAULT 1;
GO
IF COL_LENGTH('dbo.Users','Blocked') IS NULL
    ALTER TABLE dbo.Users ADD Blocked BIT NOT NULL CONSTRAINT DF_Users_Blocked DEFAULT 0;
GO
IF COL_LENGTH('dbo.Users','FailedAttempts') IS NULL
    ALTER TABLE dbo.Users ADD FailedAttempts INT NOT NULL CONSTRAINT DF_Users_FailedAttempts DEFAULT 0;
GO

-- Bitacora de logins / logouts. Se registra cada intento (exitoso o fallido).
IF OBJECT_ID('dbo.LoginAuditLog','U') IS NULL
BEGIN
    CREATE TABLE dbo.LoginAuditLog (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LoginAuditLog PRIMARY KEY,
        Username     NVARCHAR(50)      NOT NULL,
        [Action]     NVARCHAR(20)      NOT NULL,  -- LOGIN_OK, LOGIN_FAIL, LOGOUT
        [Timestamp]  DATETIME          NOT NULL CONSTRAINT DF_LoginAuditLog_Timestamp DEFAULT GETDATE(),
        MachineName  NVARCHAR(100)     NULL,
        Details      NVARCHAR(500)     NULL
    );

    CREATE INDEX IX_LoginAuditLog_Username ON dbo.LoginAuditLog(Username);
    CREATE INDEX IX_LoginAuditLog_Timestamp ON dbo.LoginAuditLog([Timestamp] DESC);
END
GO

-- Seed: usuario admin con password 'admin123'.
-- Hash SHA-256 de 'admin123' = 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash)
    VALUES ('admin', '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a9');
END
GO

-- ===========================================================================
-- Negocio: Salones + Clientes + Reservas
-- ===========================================================================

-- Catalogo de salones donde se realizan los eventos.
IF OBJECT_ID('dbo.Salones','U') IS NULL
BEGIN
    CREATE TABLE dbo.Salones (
        Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Salones PRIMARY KEY,
        Nombre     NVARCHAR(100)     NOT NULL,
        Capacidad  INT               NOT NULL CONSTRAINT DF_Salones_Capacidad DEFAULT 0,
        CONSTRAINT UQ_Salones_Nombre UNIQUE (Nombre)
    );
END
GO

-- Clientes (Proceso 1). Toda reserva pertenece a un cliente registrado.
IF OBJECT_ID('dbo.Clientes','U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clientes PRIMARY KEY,
        Nombre    NVARCHAR(60)      NOT NULL,
        Apellido  NVARCHAR(60)      NULL,
        Dni       NVARCHAR(20)      NULL,
        Email     NVARCHAR(400)     NULL,  -- cifrado AES + Base64 (CryptoService)
        Telefono  NVARCHAR(200)     NULL,  -- cifrado AES + Base64 (CryptoService)
        CreatedAt DATETIME          NOT NULL CONSTRAINT DF_Clientes_CreatedAt DEFAULT GETDATE()
    );
END
GO

-- DNI unico solo cuando esta cargado (permite varios clientes sin DNI). Va en
-- bloque propio y no dentro del CREATE TABLE: si el indice no pudo crearse en
-- una corrida anterior (QUOTED_IDENTIFIER OFF), la tabla ya existia y el
-- bloque de creacion no volvia a intentarlo.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Clientes_Dni' AND object_id = OBJECT_ID('dbo.Clientes'))
    CREATE UNIQUE INDEX UX_Clientes_Dni ON dbo.Clientes(Dni) WHERE Dni IS NOT NULL;
GO

-- Reserva de un evento sobre un salon. Entidad central del dominio.
-- Dvh: digito verificador horizontal (T07/T08). Lo calcula y graba la capa de
-- negocio en cada alta o modificacion; admite NULL para que una fila cargada
-- por fuera de la aplicacion no rompa el alta y quede detectable como
-- inconsistencia en la verificacion de arranque.
IF OBJECT_ID('dbo.Reservas','U') IS NULL
BEGIN
    CREATE TABLE dbo.Reservas (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Reservas PRIMARY KEY,
        ClienteId     INT               NOT NULL,
        SalonId       INT               NOT NULL,
        FechaEvento   DATETIME          NOT NULL,
        Estado        NVARCHAR(20)      NOT NULL CONSTRAINT DF_Reservas_Estado DEFAULT 'COTIZACION',
        Monto         DECIMAL(12,2)     NOT NULL CONSTRAINT DF_Reservas_Monto DEFAULT 0,
        CreatedAt     DATETIME          NOT NULL CONSTRAINT DF_Reservas_CreatedAt DEFAULT GETDATE(),
        Dvh           NVARCHAR(64)      NULL,
        CONSTRAINT FK_Reservas_Clientes FOREIGN KEY (ClienteId) REFERENCES dbo.Clientes(Id),
        CONSTRAINT FK_Reservas_Salones  FOREIGN KEY (SalonId)   REFERENCES dbo.Salones(Id)
    );

    CREATE INDEX IX_Reservas_FechaEvento ON dbo.Reservas(FechaEvento DESC);
    CREATE INDEX IX_Reservas_SalonId ON dbo.Reservas(SalonId);
END
GO

-- ===========================================================================
-- Migraciones de Reservas para bases anteriores (idempotentes)
-- ===========================================================================

-- Email/Telefono se almacenan cifrados (AES-256 + Base64, prefijo 'ENC:'), lo que
-- requiere mas ancho que el texto plano. Idempotente: solo amplia si estan cortas.
-- max_length de sys.columns esta en bytes (NVARCHAR usa 2 por caracter).
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Clientes') AND name = 'Email' AND max_length < 800)
    ALTER TABLE dbo.Clientes ALTER COLUMN Email NVARCHAR(400) NULL;
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Clientes') AND name = 'Telefono' AND max_length < 400)
    ALTER TABLE dbo.Clientes ALTER COLUMN Telefono NVARCHAR(200) NULL;
GO

-- Bases creadas con la primera version de Reservas (ClienteNombre en texto):
-- se agrega ClienteId (FK -> Clientes), nace NULL para poder migrar las filas.
IF COL_LENGTH('dbo.Reservas','ClienteId') IS NULL
    ALTER TABLE dbo.Reservas ADD ClienteId INT NULL
        CONSTRAINT FK_Reservas_Clientes FOREIGN KEY REFERENCES dbo.Clientes(Id);
GO

-- Migracion: crea un Cliente por cada ClienteNombre existente, enlaza la reserva
-- y luego elimina la columna ClienteNombre (queda normalizado en Clientes / 3FN).
-- Se usa EXEC (dynamic SQL) para que la referencia a ClienteNombre no se compile
-- cuando la columna ya no existe (si no, el re-run del script fallaria).
IF COL_LENGTH('dbo.Reservas','ClienteNombre') IS NOT NULL
BEGIN
    EXEC('INSERT INTO dbo.Clientes (Nombre)
            SELECT DISTINCT LTRIM(RTRIM(r.ClienteNombre))
            FROM dbo.Reservas r
            WHERE r.ClienteNombre IS NOT NULL AND LTRIM(RTRIM(r.ClienteNombre)) <> ''''
              AND NOT EXISTS (SELECT 1 FROM dbo.Clientes c
                  WHERE c.Nombre = LTRIM(RTRIM(r.ClienteNombre)) AND c.Apellido IS NULL AND c.Dni IS NULL);');

    EXEC('UPDATE r SET r.ClienteId = c.Id
            FROM dbo.Reservas r
            JOIN dbo.Clientes c ON c.Nombre = LTRIM(RTRIM(r.ClienteNombre)) AND c.Apellido IS NULL AND c.Dni IS NULL
            WHERE r.ClienteId IS NULL;');

    EXEC('ALTER TABLE dbo.Reservas DROP COLUMN ClienteNombre;');
END
GO

-- Toda reserva pertenece a un cliente registrado (regla de negocio del PN1: el
-- vendedor selecciona o da de alta al cliente antes de cotizar). La columna nace
-- NULL solo para poder migrar las filas heredadas; una vez enlazadas se endurece
-- a NOT NULL para que la restriccion viva tambien en el modelo de datos y no solo
-- en la validacion de BLL_Reserva. Idempotente y defensivo: si quedara alguna
-- reserva huerfana no se fuerza el cambio (el script no debe romper una base real).
IF COL_LENGTH('dbo.Reservas','ClienteId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Reservas') AND name = 'ClienteId' AND is_nullable = 1)
   AND NOT EXISTS (SELECT 1 FROM dbo.Reservas WHERE ClienteId IS NULL)
BEGIN
    ALTER TABLE dbo.Reservas ALTER COLUMN ClienteId INT NOT NULL;
END
GO

-- Vencimiento de la operacion (RN-01): una COTIZACION o una reserva PENDIENTE
-- tienen un plazo de validez. Al confirmarse o cancelarse deja de aplicar y queda
-- en NULL. Es un dato administrativo: NO entra en el digito verificador.
IF COL_LENGTH('dbo.Reservas','VenceEl') IS NULL
    ALTER TABLE dbo.Reservas ADD VenceEl DATETIME NULL;
GO

-- Cantidad de invitados estimada (PN1: "Cantidad_Invitados"). Es el dato que el
-- vendedor usa para consultar disponibilidad y el que sostiene la RN-06: al
-- confirmar, el salon elegido tiene que poder alojar a los invitados. Se persiste
-- en la reserva porque forma parte de la operacion contratada, no solo de la
-- consulta previa.
-- NO entra en el digito verificador: igual que VenceEl, sumarlo invalidaria los DV
-- ya calculados sobre las reservas existentes. El DV protege lo que define el
-- compromiso comercial (cliente, salon, fecha, estado y monto).
IF COL_LENGTH('dbo.Reservas','CantidadInvitados') IS NULL
    ALTER TABLE dbo.Reservas ADD CantidadInvitados INT NOT NULL
        CONSTRAINT DF_Reservas_CantidadInvitados DEFAULT 0;
GO

-- Anti-doble-reserva a nivel de motor (RN-03): no puede haber dos reservas
-- CONFIRMADA para el mismo salon y fecha. Es la red de seguridad ante una
-- carrera entre dos confirmaciones simultaneas (la BLL igual lo pre-valida por
-- dia con CAST AS DATE). Indice UNICO filtrado: solo aplica a las CONFIRMADA;
-- cotizaciones, pendientes y canceladas no compiten. La app guarda FechaEvento
-- con hora 00:00, asi (SalonId, FechaEvento) = (salon, dia).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Reservas_SalonFecha_Confirmada' AND object_id = OBJECT_ID('dbo.Reservas'))
    CREATE UNIQUE INDEX UX_Reservas_SalonFecha_Confirmada
        ON dbo.Reservas(SalonId, FechaEvento) WHERE Estado = 'CONFIRMADA';
GO

-- ---------------------------------------------------------------------------
-- Reconciliacion de definiciones que viven dentro de un CREATE TABLE.
-- Un CREATE TABLE bajo IF OBJECT_ID solo corre la primera vez: editarlo NO
-- cambia una base ya creada. Por eso cada correccion de una columna o de un
-- DEFAULT necesita su ALTER de acompanamiento, idempotente, aca abajo.
-- ---------------------------------------------------------------------------

-- Estado inicial de la reserva: COTIZACION (tabla de estados del proceso de
-- negocio). Una base creada con la version anterior del script quedo con otro
-- valor por defecto; se repone el correcto sin tocar los datos existentes.
IF EXISTS (SELECT 1
           FROM sys.default_constraints dc
           JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
           WHERE dc.parent_object_id = OBJECT_ID('dbo.Reservas')
             AND c.name = 'Estado'
             AND dc.definition <> '(''COTIZACION'')')
BEGIN
    DECLARE @dfEstado SYSNAME = (
        SELECT dc.name
        FROM sys.default_constraints dc
        JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Reservas') AND c.name = 'Estado');
    EXEC('ALTER TABLE dbo.Reservas DROP CONSTRAINT ' + @dfEstado);
    ALTER TABLE dbo.Reservas ADD CONSTRAINT DF_Reservas_Estado DEFAULT 'COTIZACION' FOR Estado;
END
GO

-- Users.PasswordHash guarda un SHA-256 en hexadecimal: 64 caracteres exactos
-- (max_length = 128 bytes en NVARCHAR). El ajuste es defensivo: solo se aplica
-- si todos los valores guardados entran, para no romper una base real.
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Users') AND name = 'PasswordHash' AND max_length <> 128)
   AND NOT EXISTS (SELECT 1 FROM dbo.Users WHERE LEN(PasswordHash) > 64)
    ALTER TABLE dbo.Users ALTER COLUMN PasswordHash NVARCHAR(64) NOT NULL;
GO

-- Seed de clientes de ejemplo (solo si la tabla quedo vacia).
IF NOT EXISTS (SELECT 1 FROM dbo.Clientes)
BEGIN
    INSERT INTO dbo.Clientes (Nombre, Apellido, Dni, Email, Telefono) VALUES
        (N'Juan',  N'Pérez', N'30111222', N'juan.perez@mail.com',  N'11-5555-1111'),
        (N'María', N'Gómez', N'28999333', N'maria.gomez@mail.com', N'11-5555-2222');
END
GO

-- ===========================================================================
-- Servicios (catalogo, Proceso 1) + ReservaServicio (M:N reserva <-> servicios)
-- ===========================================================================
IF OBJECT_ID('dbo.Servicios','U') IS NULL
BEGIN
    CREATE TABLE dbo.Servicios (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Servicios PRIMARY KEY,
        Nombre      NVARCHAR(80)      NOT NULL,
        Descripcion NVARCHAR(250)     NULL,
        Precio      DECIMAL(12,2)     NOT NULL CONSTRAINT DF_Servicios_Precio DEFAULT 0,
        Activo      BIT               NOT NULL CONSTRAINT DF_Servicios_Activo DEFAULT 1,
        CreatedAt   DATETIME          NOT NULL CONSTRAINT DF_Servicios_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT UQ_Servicios_Nombre UNIQUE (Nombre)
    );
END
GO

-- Servicios contratados por reserva (precio congelado al momento de contratar).
IF OBJECT_ID('dbo.ReservaServicio','U') IS NULL
BEGIN
    CREATE TABLE dbo.ReservaServicio (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReservaServicio PRIMARY KEY,
        ReservaId      INT NOT NULL,
        ServicioId     INT NOT NULL,
        Cantidad       INT NOT NULL CONSTRAINT DF_ReservaServicio_Cantidad DEFAULT 1,
        PrecioUnitario DECIMAL(12,2) NOT NULL CONSTRAINT DF_ReservaServicio_Precio DEFAULT 0,
        CONSTRAINT FK_ReservaServicio_Reserva  FOREIGN KEY (ReservaId)  REFERENCES dbo.Reservas(Id),
        CONSTRAINT FK_ReservaServicio_Servicio FOREIGN KEY (ServicioId) REFERENCES dbo.Servicios(Id)
    );
    CREATE INDEX IX_ReservaServicio_Reserva ON dbo.ReservaServicio(ReservaId);
END
GO

-- Seed de servicios de ejemplo.
IF NOT EXISTS (SELECT 1 FROM dbo.Servicios)
BEGIN
    INSERT INTO dbo.Servicios (Nombre, Descripcion, Precio) VALUES
        (N'Catering por persona', N'Menú completo por invitado',        8500),
        (N'Decoración temática',  N'Ambientación del salón',           60000),
        (N'DJ y sonido',          N'Servicio de música y sonido',      90000),
        (N'Fotografía y video',   N'Cobertura del evento',            120000),
        (N'Barra de tragos',      N'Barra libre de bebidas',           75000),
        (N'Servicio de mozos',    N'Personal de atención (por mozo)',  25000);
END
GO

-- ===========================================================================
-- Pagos de reservas (Proceso 1, paso 5) + metodos de pago
-- ===========================================================================
IF OBJECT_ID('dbo.MetodosPago','U') IS NULL
BEGIN
    CREATE TABLE dbo.MetodosPago (
        Id     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MetodosPago PRIMARY KEY,
        Nombre NVARCHAR(50)      NOT NULL,
        CONSTRAINT UQ_MetodosPago_Nombre UNIQUE (Nombre)
    );
    INSERT INTO dbo.MetodosPago (Nombre) VALUES
        (N'Efectivo'), (N'Tarjeta de crédito'), (N'Tarjeta de débito'), (N'Transferencia'), (N'MercadoPago');
END
GO

IF OBJECT_ID('dbo.Pagos','U') IS NULL
BEGIN
    CREATE TABLE dbo.Pagos (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Pagos PRIMARY KEY,
        ReservaId    INT NOT NULL,
        MetodoPagoId INT NOT NULL,
        Monto        DECIMAL(12,2) NOT NULL,
        Fecha        DATETIME NOT NULL CONSTRAINT DF_Pagos_Fecha DEFAULT GETDATE(),
        Observacion  NVARCHAR(200) NULL,
        Dvh          NVARCHAR(64) NULL,
        CONSTRAINT FK_Pagos_Reserva FOREIGN KEY (ReservaId)    REFERENCES dbo.Reservas(Id),
        CONSTRAINT FK_Pagos_Metodo  FOREIGN KEY (MetodoPagoId) REFERENCES dbo.MetodosPago(Id)
    );
    CREATE INDEX IX_Pagos_Reserva ON dbo.Pagos(ReservaId);
END
GO

-- Dvh: digito verificador horizontal del pago (T07/T08). Lo calcula y graba la
-- capa de negocio en la misma transaccion del cobro; el vertical de la tabla va
-- en DVVertical. Una base anterior recibe la columna vacia: la aplicacion
-- establece la linea base de esos pagos la primera vez que calcula el digito
-- vertical de la tabla, y lo deja asentado en la bitacora.
IF COL_LENGTH('dbo.Pagos','Dvh') IS NULL
    ALTER TABLE dbo.Pagos ADD Dvh NVARCHAR(64) NULL;
GO

-- Seed de salones de ejemplo para poder operar.
IF NOT EXISTS (SELECT 1 FROM dbo.Salones)
BEGIN
    INSERT INTO dbo.Salones (Nombre, Capacidad) VALUES
        (N'Salón Principal', 250),
        (N'Salón Jardín',    120),
        (N'Terraza',          80);
END
GO

-- ===========================================================================
-- Auditoria: Bitacora general (T06) + Control de Cambios (T06b)
-- ===========================================================================

-- Bitacora general del sistema: cualquier operacion de negocio, no solo login.
-- Criticidad: 1=Info, 2=Advertencia, 3=Error.
IF OBJECT_ID('dbo.Bitacora','U') IS NULL
BEGIN
    CREATE TABLE dbo.Bitacora (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bitacora PRIMARY KEY,
        Fecha       DATETIME          NOT NULL CONSTRAINT DF_Bitacora_Fecha DEFAULT GETDATE(),
        Usuario     NVARCHAR(50)      NOT NULL,
        Modulo      NVARCHAR(50)      NULL,
        Accion      NVARCHAR(100)     NULL,
        Criticidad  TINYINT           NOT NULL CONSTRAINT DF_Bitacora_Criticidad DEFAULT 1,
        Detalle     NVARCHAR(1000)    NULL
    );

    CREATE INDEX IX_Bitacora_Fecha ON dbo.Bitacora(Fecha DESC);
    CREATE INDEX IX_Bitacora_Usuario ON dbo.Bitacora(Usuario);
    CREATE INDEX IX_Bitacora_Modulo ON dbo.Bitacora(Modulo);
END
GO

-- Control de cambios fino: una fila por campo modificado de una entidad.
-- Permite reconstruir el estado anterior campo por campo (quien/cuando/que).
IF OBJECT_ID('dbo.HistorialCambios','U') IS NULL
BEGIN
    CREATE TABLE dbo.HistorialCambios (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_HistorialCambios PRIMARY KEY,
        Entidad        NVARCHAR(50)      NOT NULL,
        EntidadId      INT               NOT NULL,
        NombreCampo    NVARCHAR(100)     NOT NULL,
        ValorAnterior  NVARCHAR(500)     NULL,
        ValorNuevo     NVARCHAR(500)     NULL,
        Usuario        NVARCHAR(50)      NOT NULL,
        Fecha          DATETIME          NOT NULL CONSTRAINT DF_HistorialCambios_Fecha DEFAULT GETDATE()
    );

    CREATE INDEX IX_HistorialCambios_Entidad ON dbo.HistorialCambios(Entidad, EntidadId);
END
GO

-- RN-01 en bases anteriores a la columna VenceEl: las operaciones activas
-- quedaban sin plazo y NULL se lee como "no vence", asi que avanzaban de estado
-- sin renovar. El plazo se cuenta desde la emision (CreatedAt) para COTIZACION
-- (15 dias) y, para PENDIENTE (72 horas), desde el ultimo pase a ese estado
-- asentado en HistorialCambios o, si no hay asiento, desde la emision, que es
-- la cota mas conservadora (una operacion realmente vieja queda vencida y la
-- aplicacion exige renovarla). Las constantes son las de BLL_Reserva
-- (DiasValidezCotizacion / HorasValidezPendiente). Solo toca filas sin plazo:
-- re-ejecutar no prolonga nada. VenceEl no integra el digito verificador.
UPDATE r SET r.VenceEl = CASE r.Estado
        WHEN 'COTIZACION' THEN DATEADD(DAY, 15, r.CreatedAt)
        WHEN 'PENDIENTE'  THEN DATEADD(HOUR, 72, COALESCE(
            (SELECT MAX(h.Fecha) FROM dbo.HistorialCambios h
              WHERE h.Entidad = N'Reserva' AND h.EntidadId = r.Id
                AND h.NombreCampo = N'Estado' AND h.ValorNuevo = N'PENDIENTE'), r.CreatedAt)) END
FROM dbo.Reservas r
WHERE r.VenceEl IS NULL AND r.Estado IN ('COTIZACION', 'PENDIENTE');
GO

-- ===========================================================================
-- Perfiles de usuario (T04 - patron Composite)
-- ===========================================================================

-- Arbol de permisos. Una sola tabla con relacion REFLEXIVA (PermisoPadreId):
--   EsGrupo = 1  -> nodo compuesto (grupo que agrupa otros permisos/grupos)
--   EsGrupo = 0  -> hoja (permiso concreto, identificado por Clave)
IF OBJECT_ID('dbo.Permisos','U') IS NULL
BEGIN
    CREATE TABLE dbo.Permisos (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permisos PRIMARY KEY,
        Nombre          NVARCHAR(100)     NOT NULL,
        Descripcion     NVARCHAR(250)     NULL,
        EsGrupo         BIT               NOT NULL CONSTRAINT DF_Permisos_EsGrupo DEFAULT 0,
        Clave           NVARCHAR(50)      NULL,
        PermisoPadreId  INT               NULL,
        CONSTRAINT FK_Permisos_Padre FOREIGN KEY (PermisoPadreId) REFERENCES dbo.Permisos(Id)
    );
END
GO

IF OBJECT_ID('dbo.Perfiles','U') IS NULL
BEGIN
    CREATE TABLE dbo.Perfiles (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Perfiles PRIMARY KEY,
        Nombre      NVARCHAR(80)      NOT NULL,
        Descripcion NVARCHAR(250)     NULL,
        CONSTRAINT UQ_Perfiles_Nombre UNIQUE (Nombre)
    );
END
GO

-- Asignacion N:M entre perfil y los componentes (grupos u hojas) que tiene.
IF OBJECT_ID('dbo.PerfilPermiso','U') IS NULL
BEGIN
    CREATE TABLE dbo.PerfilPermiso (
        PerfilId   INT NOT NULL,
        PermisoId  INT NOT NULL,
        CONSTRAINT PK_PerfilPermiso PRIMARY KEY (PerfilId, PermisoId),
        CONSTRAINT FK_PerfilPermiso_Perfil  FOREIGN KEY (PerfilId)  REFERENCES dbo.Perfiles(Id),
        CONSTRAINT FK_PerfilPermiso_Permiso FOREIGN KEY (PermisoId) REFERENCES dbo.Permisos(Id)
    );
END
GO

-- Relacion del usuario con su perfil.
IF COL_LENGTH('dbo.Users','PerfilId') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD PerfilId INT NULL
        CONSTRAINT FK_Users_Perfil FOREIGN KEY (PerfilId) REFERENCES dbo.Perfiles(Id);
END
GO

-- Seed del arbol de permisos (grupos anidados + hojas).
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos)
BEGIN
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Administracion', N'Grupo raiz de administracion', 1, NULL, NULL);
    DECLARE @raiz INT = SCOPE_IDENTITY();

    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Gestion de Reservas', N'Permisos sobre reservas', 1, NULL, @raiz);
    DECLARE @gReservas INT = SCOPE_IDENTITY();

    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId) VALUES
        (N'Crear Reserva',          NULL, 0, N'RESERVA_CREAR',      @gReservas),
        (N'Editar Reserva',         NULL, 0, N'RESERVA_EDITAR',     @gReservas),
        (N'Ver Historial Reserva',  NULL, 0, N'RESERVA_HISTORIAL',  @gReservas);

    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Auditoria', N'Permisos de auditoria', 1, NULL, @raiz);
    DECLARE @gAudit INT = SCOPE_IDENTITY();

    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId) VALUES
        (N'Ver Bitacora',           NULL, 0, N'BITACORA_VER',       @gAudit),
        (N'Ver Auditoria Login',    NULL, 0, N'AUDIT_LOGIN_VER',    @gAudit);
END
GO

-- Seed del perfil Administrador con todos los permisos + vincular al usuario admin.
IF NOT EXISTS (SELECT 1 FROM dbo.Perfiles)
BEGIN
    INSERT INTO dbo.Perfiles (Nombre, Descripcion) VALUES (N'Administrador', N'Acceso total al sistema');
    DECLARE @pAdmin INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PerfilPermiso (PerfilId, PermisoId)
        SELECT @pAdmin, Id FROM dbo.Permisos;

    UPDATE dbo.Users SET PerfilId = @pAdmin WHERE Username = 'admin' AND PerfilId IS NULL;
END
GO

-- ===========================================================================
-- Permisos agregados despues del seed inicial (idempotente): el bloque de
-- arriba solo corre con la tabla vacia, asi que los permisos nuevos se
-- insertan aca solo si faltan. El perfil Administrador recibe todo lo nuevo.
-- ===========================================================================
DECLARE @raizP INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Administracion' AND EsGrupo = 1);

DECLARE @gAdminSys INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Administracion del sistema' AND EsGrupo = 1);
IF @gAdminSys IS NULL
BEGIN
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Administracion del sistema', N'Gestion de catalogos y configuracion', 1, NULL, @raizP);
    SET @gAdminSys = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'CLIENTES_GESTION')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Gestion de Clientes', NULL, 0, N'CLIENTES_GESTION', @gAdminSys);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'SERVICIOS_GESTION')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Gestion de Servicios', NULL, 0, N'SERVICIOS_GESTION', @gAdminSys);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'PERFILES_GESTION')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Gestion de Perfiles', NULL, 0, N'PERFILES_GESTION', @gAdminSys);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'IDIOMAS_GESTION')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Gestion de Idiomas', NULL, 0, N'IDIOMAS_GESTION', @gAdminSys);

-- Recalculo de linea base de DV (T08): accion administrativa, cuelga de Auditoria.
DECLARE @gAuditP INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Auditoria' AND EsGrupo = 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'INTEGRIDAD_RECALC')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Recalcular linea base', N'Reestablecer digitos verificadores tras corregir datos', 0, N'INTEGRIDAD_RECALC', @gAuditP);

-- Ventas: operaciones de cobro. Anular un pago es destructivo (no hay versionado
-- de pagos), asi que se separa del alta y se concede aparte.
DECLARE @gVentas INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Ventas' AND EsGrupo = 1);
IF @gVentas IS NULL
BEGIN
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Ventas', N'Permisos de cobro y facturacion', 1, NULL, @raizP);
    SET @gVentas = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'PAGOS_REGISTRAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Registrar Pagos', N'Cobrar adelantos y saldos de una reserva', 0, N'PAGOS_REGISTRAR', @gVentas);

IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'PAGOS_ANULAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Anular Pagos', N'Eliminar un pago ya registrado de una reserva', 0, N'PAGOS_ANULAR', @gVentas);

-- Restaurar una version previa es una correccion ADMINISTRATIVA: no respeta la
-- tabla de transiciones (RN-05) y puede deshacer una confirmacion, asi que no
-- alcanza con el permiso de edicion del vendedor. Lleva permiso propio.
DECLARE @gReservasR INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Gestion de Reservas' AND EsGrupo = 1);
IF @gReservasR IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'RESERVA_RESTAURAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Restaurar Version de Reserva',
                N'Reponer una version previa de la reserva (correccion administrativa)',
                0, N'RESERVA_RESTAURAR', @gReservasR);

-- Acceso total del Administrador: se le asigna todo permiso que le falte.
INSERT INTO dbo.PerfilPermiso (PerfilId, PermisoId)
SELECT p.Id, pe.Id
FROM dbo.Perfiles p
CROSS JOIN dbo.Permisos pe
WHERE p.Nombre = N'Administrador'
  AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermiso pp
                  WHERE pp.PerfilId = p.Id AND pp.PermisoId = pe.Id);
GO

-- ===========================================================================
-- Multiples idiomas (T05 - patron Observer). Modelo propio en BD, sin .resx.
-- ===========================================================================

IF OBJECT_ID('dbo.Idiomas','U') IS NULL
BEGIN
    CREATE TABLE dbo.Idiomas (
        Id      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Idiomas PRIMARY KEY,
        Codigo  NVARCHAR(5)       NOT NULL,
        Nombre  NVARCHAR(50)      NOT NULL,
        CONSTRAINT UQ_Idiomas_Codigo UNIQUE (Codigo)
    );
END
GO

IF OBJECT_ID('dbo.Traducciones','U') IS NULL
BEGIN
    CREATE TABLE dbo.Traducciones (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Traducciones PRIMARY KEY,
        IdiomaId  INT               NOT NULL,
        Clave     NVARCHAR(60)      NOT NULL,
        Texto     NVARCHAR(250)     NOT NULL,
        CONSTRAINT FK_Traducciones_Idioma FOREIGN KEY (IdiomaId) REFERENCES dbo.Idiomas(Id),
        CONSTRAINT UQ_Traducciones UNIQUE (IdiomaId, Clave)
    );
END
GO

-- Seed de idiomas + leyendas (ES por defecto, EN).
IF NOT EXISTS (SELECT 1 FROM dbo.Idiomas)
BEGIN
    INSERT INTO dbo.Idiomas (Codigo, Nombre) VALUES (N'ES', N'Español');
    DECLARE @es INT = SCOPE_IDENTITY();
    INSERT INTO dbo.Idiomas (Codigo, Nombre) VALUES (N'EN', N'English');
    DECLARE @en INT = SCOPE_IDENTITY();

    INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto) VALUES
        (@es, N'MENU_INICIO',    N'Inicio'),
        (@es, N'MENU_RESERVAS',  N'Reservas'),
        (@es, N'MENU_PERFILES',  N'Perfiles'),
        (@es, N'MENU_IDIOMAS',   N'Idiomas'),
        (@es, N'MENU_BITACORA', N'Bitácora'),
        (@es, N'MENU_AUDITORIA', N'Auditoría'),
        (@es, N'MENU_SALIR', N'Cerrar sesión'),
        (@es, N'MAIN_WELCOME',   N'Bienvenido a EvenTech'),
        (@es, N'LOGIN_USER',     N'Usuario'),
        (@es, N'LOGIN_PASS', N'Contraseña'),
        (@es, N'LOGIN_ENTER',    N'Ingresar'),
        (@es, N'LOGIN_CREATE', N'¿No tenés cuenta? Crear');

    INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto) VALUES
        (@en, N'MENU_INICIO',    N'Home'),
        (@en, N'MENU_RESERVAS',  N'Reservations'),
        (@en, N'MENU_PERFILES',  N'Profiles'),
        (@en, N'MENU_IDIOMAS',   N'Languages'),
        (@en, N'MENU_BITACORA',  N'Audit log'),
        (@en, N'MENU_AUDITORIA', N'Audit'),
        (@en, N'MENU_SALIR',     N'Log out'),
        (@en, N'MAIN_WELCOME',   N'Welcome to EvenTech'),
        (@en, N'LOGIN_USER',     N'Username'),
        (@en, N'LOGIN_PASS',     N'Password'),
        (@en, N'LOGIN_ENTER',    N'Sign in'),
        (@en, N'LOGIN_CREATE',   N'No account? Create one');
END
GO

-- Portugues. Bloque idempotente: se agrega solo si todavia no existe, asi
-- funciona tanto en base nueva como en una ya creada con ES/EN.
IF NOT EXISTS (SELECT 1 FROM dbo.Idiomas WHERE Codigo = 'PT')
BEGIN
    INSERT INTO dbo.Idiomas (Codigo, Nombre) VALUES (N'PT', N'Português');
    DECLARE @pt INT = SCOPE_IDENTITY();

    INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto) VALUES
        (@pt, N'MENU_INICIO', N'Início'),
        (@pt, N'MENU_RESERVAS',  N'Reservas'),
        (@pt, N'MENU_PERFILES',  N'Perfis'),
        (@pt, N'MENU_IDIOMAS',   N'Idiomas'),
        (@pt, N'MENU_BITACORA',  N'Registro'),
        (@pt, N'MENU_AUDITORIA', N'Auditoria'),
        (@pt, N'MENU_SALIR',     N'Sair'),
        (@pt, N'MAIN_WELCOME',   N'Bem-vindo ao EvenTech'),
        (@pt, N'LOGIN_USER', N'Usuário'),
        (@pt, N'LOGIN_PASS',     N'Senha'),
        (@pt, N'LOGIN_ENTER',    N'Entrar'),
        (@pt, N'LOGIN_CREATE', N'Não tem conta? Criar');
END
GO

-- ===========================================================================
-- Digitos verificadores (T07/T08). El DV horizontal vive en la propia fila
-- (Reservas.Dvh y Pagos.Dvh); el DV vertical (uno por tabla protegida: Reservas
-- y Pagos) en esta tabla.
-- ===========================================================================
IF OBJECT_ID('dbo.DVVertical','U') IS NULL
BEGIN
    CREATE TABLE dbo.DVVertical (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DVVertical PRIMARY KEY,
        Tabla       NVARCHAR(50)      NOT NULL,
        Dvv         NVARCHAR(64)      NOT NULL,
        CalculadoEn DATETIME          NOT NULL CONSTRAINT DF_DVVertical_CalculadoEn DEFAULT GETDATE(),
        CONSTRAINT UQ_DVVertical_Tabla UNIQUE (Tabla)
    );
END
GO

-- ===========================================================================
-- Traducciones de TODAS las vistas (ES/EN/PT). Idempotente: inserta solo las
-- claves que falten para cada idioma, sin pisar lo existente.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Inicio
        (N'ES', N'MAIN_SESSION', N'Sesión iniciada por:'), (N'EN', N'MAIN_SESSION',  N'Signed in as:'),               (N'PT', N'MAIN_SESSION', N'Sessão iniciada por:'),
        (N'ES', N'MAIN_SUBTITLE', N'Usa el menú de la izquierda para gestionar el sistema.'), (N'EN', N'MAIN_SUBTITLE', N'Use the left menu to manage the system.'), (N'PT', N'MAIN_SUBTITLE', N'Use o menu à esquerda para gerenciar o sistema.'),
        (N'ES', N'MAIN_HELLO',    N'Bienvenido'), (N'EN', N'MAIN_HELLO',    N'Welcome'), (N'PT', N'MAIN_HELLO',    N'Bem-vindo'),
        (N'ES', N'MAIN_SIN_ROL_TIT', N'Acceso restringido'), (N'EN', N'MAIN_SIN_ROL_TIT', N'Access restricted'), (N'PT', N'MAIN_SIN_ROL_TIT', N'Acesso restrito'),
        (N'ES', N'MAIN_SIN_ROL', N'Tu cuenta todavía no tiene un perfil asignado. Contactate con un administrador para que te asigne uno.'), (N'EN', N'MAIN_SIN_ROL', N'Your account does not have a profile assigned yet. Contact an administrator to get one.'), (N'PT', N'MAIN_SIN_ROL', N'Sua conta ainda não tem um perfil atribuído. Entre em contato com um administrador para receber um.'),
        -- Columnas compartidas
        (N'ES', N'COL_ID',         N'Id'),        (N'EN', N'COL_ID',         N'Id'),        (N'PT', N'COL_ID',         N'Id'),
        (N'ES', N'COL_CLIENTE',    N'Cliente'),   (N'EN', N'COL_CLIENTE',    N'Client'),    (N'PT', N'COL_CLIENTE',    N'Cliente'),
        (N'ES', N'COL_SALON', N'Salón'),     (N'EN', N'COL_SALON',      N'Venue'),      (N'PT', N'COL_SALON', N'Salão'),
        (N'ES', N'COL_FECHA',      N'Fecha'),     (N'EN', N'COL_FECHA',      N'Date'),      (N'PT', N'COL_FECHA',      N'Data'),
        (N'ES', N'COL_ESTADO',     N'Estado'),    (N'EN', N'COL_ESTADO',     N'Status'),    (N'PT', N'COL_ESTADO',     N'Estado'),
        (N'ES', N'COL_MONTO',      N'Monto'),     (N'EN', N'COL_MONTO',      N'Amount'),    (N'PT', N'COL_MONTO',      N'Valor'),
        (N'ES', N'COL_USUARIO',    N'Usuario'),   (N'EN', N'COL_USUARIO',    N'User'),      (N'PT', N'COL_USUARIO', N'Usuário'),
        (N'ES', N'COL_MODULO', N'Módulo'),    (N'EN', N'COL_MODULO',     N'Module'),    (N'PT', N'COL_MODULO', N'Módulo'),
        (N'ES', N'COL_ACCION', N'Acción'),    (N'EN', N'COL_ACCION',     N'Action'),    (N'PT', N'COL_ACCION', N'Ação'),
        (N'ES', N'COL_CRITICIDAD', N'Criticidad'),(N'EN', N'COL_CRITICIDAD', N'Severity'),  (N'PT', N'COL_CRITICIDAD', N'Criticidade'),
        (N'ES', N'COL_DETALLE',    N'Detalle'),   (N'EN', N'COL_DETALLE',    N'Detail'),    (N'PT', N'COL_DETALLE',    N'Detalhe'),
        (N'ES', N'COL_MAQUINA', N'Máquina'),   (N'EN', N'COL_MAQUINA',    N'Machine'),   (N'PT', N'COL_MAQUINA', N'Máquina'),
        (N'ES', N'COL_CAMPO',      N'Campo'),     (N'EN', N'COL_CAMPO',      N'Field'),     (N'PT', N'COL_CAMPO',      N'Campo'),
        (N'ES', N'COL_ANTERIOR',   N'Anterior'),  (N'EN', N'COL_ANTERIOR',   N'Previous'),  (N'PT', N'COL_ANTERIOR',   N'Anterior'),
        (N'ES', N'COL_NUEVO',      N'Nuevo'),     (N'EN', N'COL_NUEVO',      N'New'),       (N'PT', N'COL_NUEVO',      N'Novo'),
        (N'ES', N'COL_CLAVE',      N'Clave'),     (N'EN', N'COL_CLAVE',      N'Key'),       (N'PT', N'COL_CLAVE',      N'Chave'),
        (N'ES', N'COL_TEXTO',      N'Texto'),     (N'EN', N'COL_TEXTO',      N'Text'),      (N'PT', N'COL_TEXTO',      N'Texto'),
        (N'ES', N'OPT_TODOS',      N'(Todos)'),   (N'EN', N'OPT_TODOS',      N'(All)'),     (N'PT', N'OPT_TODOS',      N'(Todos)'),
        (N'ES', N'OPT_TODAS',      N'(Todas)'),   (N'EN', N'OPT_TODAS',      N'(All)'),     (N'PT', N'OPT_TODAS',      N'(Todas)'),
        -- Botones compartidos
        (N'ES', N'BTN_NUEVA',      N'Nueva'),     (N'EN', N'BTN_NUEVA',      N'New'),       (N'PT', N'BTN_NUEVA',      N'Nova'),
        (N'ES', N'BTN_GUARDAR',    N'Guardar'),   (N'EN', N'BTN_GUARDAR',    N'Save'),      (N'PT', N'BTN_GUARDAR',    N'Salvar'),
        (N'ES', N'BTN_BUSCAR',     N'Buscar'),    (N'EN', N'BTN_BUSCAR',     N'Search'),    (N'PT', N'BTN_BUSCAR',     N'Buscar'),
        (N'ES', N'BTN_LIMPIAR',    N'Limpiar'),   (N'EN', N'BTN_LIMPIAR',    N'Clear'),     (N'PT', N'BTN_LIMPIAR',    N'Limpar'),
        -- Reservas
        (N'ES', N'RES_TITULO', N'Gestión de Reservas'),       (N'EN', N'RES_TITULO',     N'Reservations Management'),  (N'PT', N'RES_TITULO', N'Gestão de Reservas'),
        (N'ES', N'RES_HISTORIAL',  N'Historial'),                 (N'EN', N'RES_HISTORIAL',  N'History'),                  (N'PT', N'RES_HISTORIAL', N'Histórico'),
        (N'ES', N'RES_FORM_NUEVA', N'Nueva reserva'),             (N'EN', N'RES_FORM_NUEVA', N'New reservation'),          (N'PT', N'RES_FORM_NUEVA', N'Nova reserva'),
        (N'ES', N'RES_FORM_EDITAR',N'Editar reserva'),            (N'EN', N'RES_FORM_EDITAR',N'Edit reservation'),         (N'PT', N'RES_FORM_EDITAR',N'Editar reserva'),
        (N'ES', N'RES_LBL_FECHA',  N'Fecha del evento'),          (N'EN', N'RES_LBL_FECHA',  N'Event date'),               (N'PT', N'RES_LBL_FECHA',  N'Data do evento'),
        (N'ES', N'RES_COUNT',      N'reservas'),                  (N'EN', N'RES_COUNT',      N'reservations'),             (N'PT', N'RES_COUNT',      N'reservas'),
        -- Bitacora
        (N'ES', N'BIT_TITULO', N'Bitácora del Sistema'),      (N'EN', N'BIT_TITULO',     N'System Audit Log'),         (N'PT', N'BIT_TITULO',     N'Registro do Sistema'),
        (N'ES', N'BIT_DESDE',      N'Desde'),                     (N'EN', N'BIT_DESDE',      N'From'),                     (N'PT', N'BIT_DESDE',      N'De'),
        (N'ES', N'BIT_HASTA',      N'Hasta'),                     (N'EN', N'BIT_HASTA',      N'To'),                       (N'PT', N'BIT_HASTA', N'Até'),
        (N'ES', N'BIT_COUNT',      N'registros'),                 (N'EN', N'BIT_COUNT',      N'records'),                  (N'PT', N'BIT_COUNT',      N'registros'),
        -- Perfiles
        (N'ES', N'PERF_TITULO', N'Gestión de Perfiles'),       (N'EN', N'PERF_TITULO',    N'Profiles Management'),      (N'PT', N'PERF_TITULO', N'Gestão de Perfis'),
        (N'ES', N'PERF_PERFIL',    N'Perfil:'),                   (N'EN', N'PERF_PERFIL',    N'Profile:'),                 (N'PT', N'PERF_PERFIL',    N'Perfil:'),
        (N'ES', N'PERF_HINT',      N'Tilde los permisos del perfil. Marcar un grupo incluye a sus hijos; en "Perfiles incluidos" podés contener otros perfiles y heredar sus permisos.'), (N'EN', N'PERF_HINT', N'Check the profile permissions. Checking a group includes its children; under "Included profiles" you can nest other profiles and inherit their permissions.'), (N'PT', N'PERF_HINT', N'Marque as permissões do perfil. Marcar um grupo inclui seus filhos; em "Perfis incluídos" você pode conter outros perfis e herdar suas permissões.'),
        (N'ES', N'PERF_GUARDAR',   N'Guardar permisos'),          (N'EN', N'PERF_GUARDAR',   N'Save permissions'),         (N'PT', N'PERF_GUARDAR', N'Salvar permissões'),
        (N'ES', N'MSG_PERF_SELECCIONE', N'Seleccione un perfil.'),(N'EN', N'MSG_PERF_SELECCIONE', N'Select a profile.'),  (N'PT', N'MSG_PERF_SELECCIONE', N'Selecione um perfil.'),
        (N'ES', N'MSG_PERF_OK',    N'Permisos guardados. Los cambios rigen desde el próximo inicio de sesión.'),       (N'EN', N'MSG_PERF_OK',    N'Permissions saved. Changes apply from the next sign-in.'),       (N'PT', N'MSG_PERF_OK', N'Permissões salvas. As alterações valem a partir do próximo início de sessão.'),
        -- Idiomas
        (N'ES', N'IDI_TITULO', N'Gestión de Idiomas'),        (N'EN', N'IDI_TITULO',     N'Languages Management'),     (N'PT', N'IDI_TITULO', N'Gestão de Idiomas'),
        (N'ES', N'IDI_NUEVO',      N'Nuevo idioma'),              (N'EN', N'IDI_NUEVO',      N'New language'),             (N'PT', N'IDI_NUEVO',      N'Novo idioma'),
        (N'ES', N'IDI_CODIGO', N'Código (ej. PT)'),           (N'EN', N'IDI_CODIGO',     N'Code (e.g. PT)'),           (N'PT', N'IDI_CODIGO', N'Código (ex. PT)'),
        (N'ES', N'IDI_NOMBRE',     N'Nombre'),                    (N'EN', N'IDI_NOMBRE',     N'Name'),                     (N'PT', N'IDI_NOMBRE',     N'Nome'),
        (N'ES', N'IDI_CREAR',      N'Crear idioma'),              (N'EN', N'IDI_CREAR',      N'Create language'),          (N'PT', N'IDI_CREAR',      N'Criar idioma'),
        (N'ES', N'IDI_IDIOMA',     N'Idioma:'),                   (N'EN', N'IDI_IDIOMA',     N'Language:'),                (N'PT', N'IDI_IDIOMA',     N'Idioma:'),
        (N'ES', N'IDI_GUARDAR',    N'Guardar traducciones'),      (N'EN', N'IDI_GUARDAR',    N'Save translations'),        (N'PT', N'IDI_GUARDAR', N'Salvar traduções'),
        (N'ES', N'MSG_IDI_CREADO', N'Idioma creado. Edite los textos y guarde.'), (N'EN', N'MSG_IDI_CREADO', N'Language created. Edit the texts and save.'), (N'PT', N'MSG_IDI_CREADO', N'Idioma criado. Edite os textos e salve.'),
        (N'ES', N'MSG_IDI_SELECCIONE', N'Seleccione un idioma.'), (N'EN', N'MSG_IDI_SELECCIONE', N'Select a language.'),   (N'PT', N'MSG_IDI_SELECCIONE', N'Selecione um idioma.'),
        (N'ES', N'MSG_IDI_GUARDADO', N'Traducciones guardadas.'), (N'EN', N'MSG_IDI_GUARDADO', N'Translations saved.'),    (N'PT', N'MSG_IDI_GUARDADO', N'Traduções salvas.'),
        (N'ES', N'MSG_IDI_COD_INV', N'Código inválido (1 a 5 caracteres).'), (N'EN', N'MSG_IDI_COD_INV', N'Invalid code (1 to 5 chars).'), (N'PT', N'MSG_IDI_COD_INV', N'Código inválido (1 a 5 caracteres).'),
        (N'ES', N'MSG_IDI_NOM_INV', N'Ingrese el nombre del idioma.'), (N'EN', N'MSG_IDI_NOM_INV', N'Enter the language name.'), (N'PT', N'MSG_IDI_NOM_INV', N'Informe o nome do idioma.'),
        (N'ES', N'MSG_IDI_DUP', N'Ya existe un idioma con ese código.'), (N'EN', N'MSG_IDI_DUP', N'A language with that code already exists.'), (N'PT', N'MSG_IDI_DUP', N'Já existe um idioma com esse código.'),
        (N'ES', N'MSG_IDI_ERROR',  N'No se pudo crear el idioma.'),(N'EN', N'MSG_IDI_ERROR',  N'Could not create the language.'), (N'PT', N'MSG_IDI_ERROR', N'Não foi possível criar o idioma.'),
        (N'ES', N'IDI_PLANTILLA_INVALIDA', N'El texto de ''{0}'' tiene llaves sin cerrar o marcadores que la clave no admite.'), (N'EN', N'IDI_PLANTILLA_INVALIDA', N'The text for ''{0}'' has unclosed braces or placeholders that the key does not allow.'), (N'PT', N'IDI_PLANTILLA_INVALIDA', N'O texto de ''{0}'' tem chaves sem fechar ou marcadores que a chave não admite.'),
        -- Auditoria
        (N'ES', N'AUD_TITULO', N'Registro de Auditoría'),     (N'EN', N'AUD_TITULO',     N'Login Audit Log'),          (N'PT', N'AUD_TITULO',     N'Registro de Auditoria'),
        (N'ES', N'AUD_COUNT',      N'registros'),                 (N'EN', N'AUD_COUNT',      N'records'),                  (N'PT', N'AUD_COUNT',      N'registros'),
        (N'ES', N'AUD_SIN_CONSULTA', N'El perfil no tiene permisos de consulta de auditoría.'), (N'EN', N'AUD_SIN_CONSULTA', N'The profile has no audit viewing permissions.'), (N'PT', N'AUD_SIN_CONSULTA', N'O perfil não tem permissões de consulta de auditoria.'),
        -- Historial de reserva
        (N'ES', N'HIST_TITULO',    N'Historial de la reserva'),   (N'EN', N'HIST_TITULO',    N'Reservation history'),      (N'PT', N'HIST_TITULO', N'Histórico da reserva'),
        -- Alerta de integridad
        (N'ES', N'ALERT_TITULO',   N'Se detectaron problemas de integridad'), (N'EN', N'ALERT_TITULO', N'Integrity problems detected'), (N'PT', N'ALERT_TITULO', N'Problemas de integridade detectados'),
        (N'ES', N'ALERT_HINT', N'La verificación de dígitos verificadores encontró datos alterados por fuera del sistema. Avise al administrador antes de operar.'), (N'EN', N'ALERT_HINT', N'The check-digit verification found data altered outside the system. Notify the administrator before operating.'), (N'PT', N'ALERT_HINT', N'A verificação de dígitos verificadores encontrou dados alterados fora do sistema. Avise o administrador antes de operar.'),
        (N'ES', N'ALERT_BTN',      N'Revisado, continuar'),       (N'EN', N'ALERT_BTN',      N'Reviewed, continue'),       (N'PT', N'ALERT_BTN',      N'Revisado, continuar'),
        (N'ES', N'ALERT_NO_VERIFICADA', N'La verificación de integridad no pudo ejecutarse: {0}'), (N'EN', N'ALERT_NO_VERIFICADA', N'The integrity check could not run: {0}'), (N'PT', N'ALERT_NO_VERIFICADA', N'A verificação de integridade não pôde ser executada: {0}'),
        -- Crear cuenta
        (N'ES', N'CC_TITULO',      N'Crear cuenta'),              (N'EN', N'CC_TITULO',      N'Create account'),           (N'PT', N'CC_TITULO',      N'Criar conta'),
        (N'ES', N'CC_USER',        N'Usuario'),                   (N'EN', N'CC_USER',        N'Username'),                 (N'PT', N'CC_USER', N'Usuário'),
        (N'ES', N'CC_PASS', N'Contraseña'),                (N'EN', N'CC_PASS',        N'Password'),                 (N'PT', N'CC_PASS',        N'Senha'),
        (N'ES', N'CC_PASS2', N'Repetir contraseña'),        (N'EN', N'CC_PASS2',       N'Repeat password'),          (N'PT', N'CC_PASS2',       N'Repetir senha'),
        (N'ES', N'CC_CREAR',       N'Crear'),                     (N'EN', N'CC_CREAR',       N'Create'),                   (N'PT', N'CC_CREAR',       N'Criar'),
        -- Mensajes de reservas
        (N'ES', N'MSG_MONTO_INVALIDO', N'El monto no es un número válido.'), (N'EN', N'MSG_MONTO_INVALIDO', N'The amount is not a valid number.'), (N'PT', N'MSG_MONTO_INVALIDO', N'O valor não é um número válido.'),
        (N'ES', N'MSG_RES_CLIENTE', N'Seleccione un cliente válido.'), (N'EN', N'MSG_RES_CLIENTE', N'Select a valid client.'), (N'PT', N'MSG_RES_CLIENTE', N'Selecione um cliente válido.'),
        (N'ES', N'MSG_RES_SALON', N'Seleccione un salón válido.'),(N'EN', N'MSG_RES_SALON',  N'Select a valid venue.'),     (N'PT', N'MSG_RES_SALON', N'Selecione um salão válido.'),
        (N'ES', N'MSG_RES_FECHA',  N'La fecha del evento no puede ser anterior a hoy.'), (N'EN', N'MSG_RES_FECHA', N'The event date cannot be before today.'), (N'PT', N'MSG_RES_FECHA', N'A data do evento não pode ser anterior a hoje.'),
        (N'ES', N'MSG_RES_MONTO',  N'El monto no puede ser negativo.'), (N'EN', N'MSG_RES_MONTO', N'The amount cannot be negative.'), (N'PT', N'MSG_RES_MONTO', N'O valor não pode ser negativo.'),
        (N'ES', N'MSG_RES_NOTFOUND', N'La reserva ya no existe.'),(N'EN', N'MSG_RES_NOTFOUND', N'The reservation no longer exists.'), (N'PT', N'MSG_RES_NOTFOUND', N'A reserva não existe mais.'),
        (N'ES', N'MSG_RES_ERROR',  N'No se pudo guardar la reserva.'), (N'EN', N'MSG_RES_ERROR', N'Could not save the reservation.'), (N'PT', N'MSG_RES_ERROR', N'Não foi possível salvar a reserva.'),
        (N'ES', N'MSG_RES_SELECCIONE', N'Seleccione una reserva existente para ver su historial.'), (N'EN', N'MSG_RES_SELECCIONE', N'Select an existing reservation to view its history.'), (N'PT', N'MSG_RES_SELECCIONE', N'Selecione uma reserva existente para ver seu histórico.'),
        -- Login (tagline, recordar, mensajes)
        (N'ES', N'LOGIN_TAGLINE', N'Gestión de eventos y reservas'), (N'EN', N'LOGIN_TAGLINE',  N'Event and booking management'), (N'PT', N'LOGIN_TAGLINE', N'Gestão de eventos e reservas'),
        (N'ES', N'LOGIN_REMEMBER', N'Recordar cuenta'),                (N'EN', N'LOGIN_REMEMBER', N'Remember me'),                   (N'PT', N'LOGIN_REMEMBER', N'Lembrar conta'),
        (N'ES', N'LOGIN_COMPLETAR', N'Completar usuario y contraseña.'), (N'EN', N'LOGIN_COMPLETAR', N'Enter username and password.'), (N'PT', N'LOGIN_COMPLETAR', N'Preencha usuário e senha.'),
        -- Un solo mensaje para usuario inexistente y clave incorrecta: no revela cual de los dos fallo.
        (N'ES', N'LOGIN_ERR_CREDENCIALES', N'Usuario o contraseña incorrectos.'), (N'EN', N'LOGIN_ERR_CREDENCIALES', N'Incorrect username or password.'), (N'PT', N'LOGIN_ERR_CREDENCIALES', N'Usuário ou senha incorretos.'),
        -- Crear cuenta (mensajes)
        (N'ES', N'CC_MSG_COMPLETAR', N'Completar todos los campos.'),   (N'EN', N'CC_MSG_COMPLETAR', N'Fill in all fields.'),         (N'PT', N'CC_MSG_COMPLETAR', N'Preencha todos os campos.'),
        (N'ES', N'CC_MSG_NO_COINCIDEN', N'Las contraseñas no coinciden.'), (N'EN', N'CC_MSG_NO_COINCIDEN', N'Passwords do not match.'), (N'PT', N'CC_MSG_NO_COINCIDEN', N'As senhas não coincidem.'),
        (N'ES', N'CC_MSG_PASS_CORTA', N'La contraseña debe tener al menos 4 caracteres.'), (N'EN', N'CC_MSG_PASS_CORTA', N'Password must be at least 4 characters.'), (N'PT', N'CC_MSG_PASS_CORTA', N'A senha deve ter ao menos 4 caracteres.'),
        (N'ES', N'CC_MSG_OK', N'Usuario creado. Ya podés iniciar sesión.'), (N'EN', N'CC_MSG_OK', N'Account created. You can sign in now.'), (N'PT', N'CC_MSG_OK', N'Conta criada. Você já pode entrar.'),
        (N'ES', N'CC_MSG_USER_INVALIDO', N'Usuario inválido (3-50, letras/números/._-).'), (N'EN', N'CC_MSG_USER_INVALIDO', N'Invalid username (3-50, letters/digits/._-).'), (N'PT', N'CC_MSG_USER_INVALIDO', N'Usuário inválido (3-50, letras/números/._-).'),
        (N'ES', N'CC_MSG_USER_EXISTE', N'Ese usuario ya existe.'),      (N'EN', N'CC_MSG_USER_EXISTE', N'That username already exists.'), (N'PT', N'CC_MSG_USER_EXISTE', N'Esse usuário já existe.'),
        (N'ES', N'CC_MSG_PASS_INVALIDA', N'Contraseña inválida.'),      (N'EN', N'CC_MSG_PASS_INVALIDA', N'Invalid password.'),       (N'PT', N'CC_MSG_PASS_INVALIDA', N'Senha inválida.'),
        -- Varios
        (N'ES', N'HIST_VACIO', N'Sin cambios registrados.'),           (N'EN', N'HIST_VACIO', N'No changes recorded.'),              (N'PT', N'HIST_VACIO', N'Sem alterações registradas.'),
        (N'ES', N'BTN_CANCELAR', N'Cancelar'),                         (N'EN', N'BTN_CANCELAR', N'Cancel'),                          (N'PT', N'BTN_CANCELAR', N'Cancelar'),
        (N'ES', N'IDI_GESTION', N'Gestionar idiomas'),                 (N'EN', N'IDI_GESTION', N'Manage languages'),                 (N'PT', N'IDI_GESTION', N'Gerenciar idiomas'),
        -- Perfiles (alta + asignacion a usuarios)
        (N'ES', N'COL_PERFIL', N'Perfil'),                             (N'EN', N'COL_PERFIL', N'Profile'),                           (N'PT', N'COL_PERFIL', N'Perfil'),
        (N'ES', N'PERF_NUEVO', N'Nuevo perfil'),                       (N'EN', N'PERF_NUEVO', N'New profile'),                       (N'PT', N'PERF_NUEVO', N'Novo perfil'),
        (N'ES', N'PERF_DESC', N'Descripción'),                         (N'EN', N'PERF_DESC', N'Description'),                        (N'PT', N'PERF_DESC', N'Descrição'),
        (N'ES', N'PERF_CREAR', N'Crear perfil'),                       (N'EN', N'PERF_CREAR', N'Create profile'),                    (N'PT', N'PERF_CREAR', N'Criar perfil'),
        (N'ES', N'PERF_ASIGNAR_TITULO', N'Asignar perfil a usuarios'), (N'EN', N'PERF_ASIGNAR_TITULO', N'Assign profile to users'),  (N'PT', N'PERF_ASIGNAR_TITULO', N'Atribuir perfil a usuários'),
        (N'ES', N'PERF_GUARDAR_ASIG', N'Guardar asignaciones'),        (N'EN', N'PERF_GUARDAR_ASIG', N'Save assignments'),           (N'PT', N'PERF_GUARDAR_ASIG', N'Salvar atribuições'),
        (N'ES', N'PERF_SIN', N'(sin perfil)'),                         (N'EN', N'PERF_SIN', N'(no profile)'),                        (N'PT', N'PERF_SIN', N'(sem perfil)'),
        (N'ES', N'MSG_PERF_ASIG_OK', N'Asignaciones guardadas. Los cambios rigen desde el próximo inicio de sesión.'),      (N'EN', N'MSG_PERF_ASIG_OK', N'Assignments saved. Changes apply from the next sign-in.'),          (N'PT', N'MSG_PERF_ASIG_OK', N'Atribuições salvas. As alterações valem a partir do próximo início de sessão.'),
        (N'ES', N'MSG_PERF_ASIG_SIN_CAMBIOS', N'No hay cambios de perfil para guardar.'), (N'EN', N'MSG_PERF_ASIG_SIN_CAMBIOS', N'There are no profile changes to save.'), (N'PT', N'MSG_PERF_ASIG_SIN_CAMBIOS', N'Não há alterações de perfil para salvar.'),
        (N'ES', N'MSG_PERF_NOM_INV', N'Ingrese el nombre del perfil.'),(N'EN', N'MSG_PERF_NOM_INV', N'Enter the profile name.'),     (N'PT', N'MSG_PERF_NOM_INV', N'Informe o nome do perfil.'),
        (N'ES', N'MSG_PERF_DUP', N'Ya existe un perfil con ese nombre.'), (N'EN', N'MSG_PERF_DUP', N'A profile with that name already exists.'), (N'PT', N'MSG_PERF_DUP', N'Já existe um perfil com esse nome.'),
        -- Login: bloqueo / estado / intentos
        (N'ES', N'LOGIN_BLOQUEADA', N'Cuenta bloqueada. Contactate con un administrador.'), (N'EN', N'LOGIN_BLOQUEADA', N'Account blocked. Contact an administrator.'), (N'PT', N'LOGIN_BLOQUEADA', N'Conta bloqueada. Entre em contato com um administrador.'),
        (N'ES', N'LOGIN_INACTIVA', N'La cuenta está inactiva. Contactate con un administrador.'), (N'EN', N'LOGIN_INACTIVA', N'The account is inactive. Contact an administrator.'), (N'PT', N'LOGIN_INACTIVA', N'A conta está inativa. Entre em contato com um administrador.'),
        (N'ES', N'LOGIN_INTENTOS', N'Intento {0} de {1}.'), (N'EN', N'LOGIN_INTENTOS', N'Attempt {0} of {1}.'), (N'PT', N'LOGIN_INTENTOS', N'Tentativa {0} de {1}.'),
        -- Estado de usuario (grilla de asignacion). La clave COL_ESTADO ya viene
        -- sembrada mas arriba (encabezados de grilla): repetirla aca hacia que las
        -- dos filas entraran juntas en el mismo INSERT y chocaran contra
        -- UQ_Traducciones al correr el script sobre una base nueva.
        (N'ES', N'EST_ACTIVO', N'Activo'), (N'EN', N'EST_ACTIVO', N'Active'), (N'PT', N'EST_ACTIVO', N'Ativo'),
        (N'ES', N'EST_BLOQUEADO', N'Bloqueado'), (N'EN', N'EST_BLOQUEADO', N'Blocked'), (N'PT', N'EST_BLOQUEADO', N'Bloqueado'),
        (N'ES', N'EST_INACTIVO', N'Inactivo'), (N'EN', N'EST_INACTIVO', N'Inactive'), (N'PT', N'EST_INACTIVO', N'Inativo'),
        (N'ES', N'PERF_DESBLOQUEAR', N'Desbloquear'), (N'EN', N'PERF_DESBLOQUEAR', N'Unblock'), (N'PT', N'PERF_DESBLOQUEAR', N'Desbloquear'),
        (N'ES', N'MSG_PERF_DESBLOQ', N'Usuario desbloqueado.'), (N'EN', N'MSG_PERF_DESBLOQ', N'User unblocked.'), (N'PT', N'MSG_PERF_DESBLOQ', N'Usuário desbloqueado.'),
        -- Clientes (Proceso 1)
        (N'ES', N'MENU_CLIENTES', N'Clientes'), (N'EN', N'MENU_CLIENTES', N'Clients'), (N'PT', N'MENU_CLIENTES', N'Clientes'),
        (N'ES', N'CLI_TITULO', N'Gestión de Clientes'), (N'EN', N'CLI_TITULO', N'Clients Management'), (N'PT', N'CLI_TITULO', N'Gestão de Clientes'),
        (N'ES', N'CLI_NUEVO', N'Nuevo cliente'), (N'EN', N'CLI_NUEVO', N'New client'), (N'PT', N'CLI_NUEVO', N'Novo cliente'),
        (N'ES', N'CLI_FORM_EDITAR', N'Editar cliente'), (N'EN', N'CLI_FORM_EDITAR', N'Edit client'), (N'PT', N'CLI_FORM_EDITAR', N'Editar cliente'),
        (N'ES', N'CLI_COUNT', N'clientes'), (N'EN', N'CLI_COUNT', N'clients'), (N'PT', N'CLI_COUNT', N'clientes'),
        (N'ES', N'COL_NOMBRE', N'Nombre'), (N'EN', N'COL_NOMBRE', N'Name'), (N'PT', N'COL_NOMBRE', N'Nome'),
        (N'ES', N'COL_APELLIDO', N'Apellido'), (N'EN', N'COL_APELLIDO', N'Last name'), (N'PT', N'COL_APELLIDO', N'Sobrenome'),
        (N'ES', N'COL_DNI', N'DNI'), (N'EN', N'COL_DNI', N'ID'), (N'PT', N'COL_DNI', N'DNI'),
        (N'ES', N'COL_EMAIL', N'Email'), (N'EN', N'COL_EMAIL', N'Email'), (N'PT', N'COL_EMAIL', N'Email'),
        (N'ES', N'COL_TELEFONO', N'Teléfono'), (N'EN', N'COL_TELEFONO', N'Phone'), (N'PT', N'COL_TELEFONO', N'Telefone'),
        (N'ES', N'MSG_CLI_NOMBRE', N'Ingrese el nombre del cliente.'), (N'EN', N'MSG_CLI_NOMBRE', N'Enter the client name.'), (N'PT', N'MSG_CLI_NOMBRE', N'Informe o nome do cliente.'),
        (N'ES', N'MSG_CLI_DNI_DUP', N'Ya existe un cliente con ese DNI.'), (N'EN', N'MSG_CLI_DNI_DUP', N'A client with that ID already exists.'), (N'PT', N'MSG_CLI_DNI_DUP', N'Já existe um cliente com esse documento.'),
        (N'ES', N'MSG_CLI_EMAIL', N'El email no es válido.'), (N'EN', N'MSG_CLI_EMAIL', N'The email is not valid.'), (N'PT', N'MSG_CLI_EMAIL', N'O email não é válido.'),
        (N'ES', N'MSG_CLI_OK', N'Cliente guardado.'), (N'EN', N'MSG_CLI_OK', N'Client saved.'), (N'PT', N'MSG_CLI_OK', N'Cliente salvo.'),
        (N'ES', N'MSG_RES_SALON_OCUPADO', N'El salón ya está reservado para esa fecha.'), (N'EN', N'MSG_RES_SALON_OCUPADO', N'The venue is already booked for that date.'), (N'PT', N'MSG_RES_SALON_OCUPADO', N'O salão já está reservado para essa data.'),
        -- Servicios (Proceso 1)
        (N'ES', N'MENU_SERVICIOS', N'Servicios'), (N'EN', N'MENU_SERVICIOS', N'Services'), (N'PT', N'MENU_SERVICIOS', N'Serviços'),
        (N'ES', N'SRV_TITULO', N'Gestión de Servicios'), (N'EN', N'SRV_TITULO', N'Services Management'), (N'PT', N'SRV_TITULO', N'Gestão de Serviços'),
        (N'ES', N'SRV_NUEVO', N'Nuevo servicio'), (N'EN', N'SRV_NUEVO', N'New service'), (N'PT', N'SRV_NUEVO', N'Novo serviço'),
        (N'ES', N'SRV_FORM_EDITAR', N'Editar servicio'), (N'EN', N'SRV_FORM_EDITAR', N'Edit service'), (N'PT', N'SRV_FORM_EDITAR', N'Editar serviço'),
        (N'ES', N'SRV_COUNT', N'servicios'), (N'EN', N'SRV_COUNT', N'services'), (N'PT', N'SRV_COUNT', N'serviços'),
        (N'ES', N'COL_DESCRIPCION', N'Descripción'), (N'EN', N'COL_DESCRIPCION', N'Description'), (N'PT', N'COL_DESCRIPCION', N'Descrição'),
        (N'ES', N'COL_PRECIO', N'Precio'), (N'EN', N'COL_PRECIO', N'Price'), (N'PT', N'COL_PRECIO', N'Preço'),
        (N'ES', N'COL_ACTIVO', N'Activo'), (N'EN', N'COL_ACTIVO', N'Active'), (N'PT', N'COL_ACTIVO', N'Ativo'),
        (N'ES', N'COL_CANTIDAD', N'Cantidad'), (N'EN', N'COL_CANTIDAD', N'Qty'), (N'PT', N'COL_CANTIDAD', N'Qtd'),
        (N'ES', N'COL_SUBTOTAL', N'Subtotal'), (N'EN', N'COL_SUBTOTAL', N'Subtotal'), (N'PT', N'COL_SUBTOTAL', N'Subtotal'),
        (N'ES', N'MSG_SRV_NOMBRE', N'Ingrese el nombre del servicio.'), (N'EN', N'MSG_SRV_NOMBRE', N'Enter the service name.'), (N'PT', N'MSG_SRV_NOMBRE', N'Informe o nome do serviço.'),
        (N'ES', N'MSG_SRV_PRECIO', N'El precio no puede ser negativo.'), (N'EN', N'MSG_SRV_PRECIO', N'The price cannot be negative.'), (N'PT', N'MSG_SRV_PRECIO', N'O preço não pode ser negativo.'),
        (N'ES', N'MSG_SRV_DUP', N'Ya existe un servicio con ese nombre.'), (N'EN', N'MSG_SRV_DUP', N'A service with that name already exists.'), (N'PT', N'MSG_SRV_DUP', N'Já existe um serviço com esse nome.'),
        (N'ES', N'MSG_SRV_OK', N'Servicio guardado.'), (N'EN', N'MSG_SRV_OK', N'Service saved.'), (N'PT', N'MSG_SRV_OK', N'Serviço salvo.'),
        -- Servicios contratados en una reserva (M:N)
        (N'ES', N'RES_SERVICIOS', N'Servicios de la reserva'), (N'EN', N'RES_SERVICIOS', N'Reservation services'), (N'PT', N'RES_SERVICIOS', N'Serviços da reserva'),
        (N'ES', N'COL_SERVICIO', N'Servicio'), (N'EN', N'COL_SERVICIO', N'Service'), (N'PT', N'COL_SERVICIO', N'Serviço'),
        (N'ES', N'LBL_TOTAL', N'Total'), (N'EN', N'LBL_TOTAL', N'Total'), (N'PT', N'LBL_TOTAL', N'Total'),
        (N'ES', N'BTN_AGREGAR', N'Agregar'), (N'EN', N'BTN_AGREGAR', N'Add'), (N'PT', N'BTN_AGREGAR', N'Adicionar'),
        (N'ES', N'BTN_QUITAR', N'Quitar'), (N'EN', N'BTN_QUITAR', N'Remove'), (N'PT', N'BTN_QUITAR', N'Remover'),
        (N'ES', N'BTN_ACEPTAR', N'Aceptar'), (N'EN', N'BTN_ACEPTAR', N'OK'), (N'PT', N'BTN_ACEPTAR', N'OK'),
        -- Pagos de la reserva (Proceso 1, paso 5)
        (N'ES', N'RES_PAGOS', N'Pagos de la reserva'), (N'EN', N'RES_PAGOS', N'Reservation payments'), (N'PT', N'RES_PAGOS', N'Pagamentos da reserva'),
        (N'ES', N'RES_PAGOS_BTN', N'Pagos'), (N'EN', N'RES_PAGOS_BTN', N'Payments'), (N'PT', N'RES_PAGOS_BTN', N'Pagamentos'),
        (N'ES', N'COL_METODO', N'Método'), (N'EN', N'COL_METODO', N'Method'), (N'PT', N'COL_METODO', N'Método'),
        (N'ES', N'COL_OBSERVACION', N'Observación'), (N'EN', N'COL_OBSERVACION', N'Note'), (N'PT', N'COL_OBSERVACION', N'Observação'),
        (N'ES', N'LBL_PAGADO', N'Pagado'), (N'EN', N'LBL_PAGADO', N'Paid'), (N'PT', N'LBL_PAGADO', N'Pago'),
        (N'ES', N'LBL_SALDO', N'Saldo'), (N'EN', N'LBL_SALDO', N'Balance'), (N'PT', N'LBL_SALDO', N'Saldo'),
        (N'ES', N'BTN_REGISTRAR', N'Registrar'), (N'EN', N'BTN_REGISTRAR', N'Add payment'), (N'PT', N'BTN_REGISTRAR', N'Registrar'),
        (N'ES', N'BTN_CERRAR', N'Cerrar'), (N'EN', N'BTN_CERRAR', N'Close'), (N'PT', N'BTN_CERRAR', N'Fechar'),
        (N'ES', N'MSG_PAGO_MONTO', N'Ingrese un monto válido.'), (N'EN', N'MSG_PAGO_MONTO', N'Enter a valid amount.'), (N'PT', N'MSG_PAGO_MONTO', N'Informe um valor válido.'),
        (N'ES', N'MSG_PAGO_METODO', N'Seleccione un método de pago.'), (N'EN', N'MSG_PAGO_METODO', N'Select a payment method.'), (N'PT', N'MSG_PAGO_METODO', N'Selecione um método de pagamento.'),
        (N'ES', N'MSG_PAGO_EXCEDE', N'El pago supera el saldo pendiente.'), (N'EN', N'MSG_PAGO_EXCEDE', N'The payment exceeds the pending balance.'), (N'PT', N'MSG_PAGO_EXCEDE', N'O pagamento excede o saldo pendente.'),
        (N'ES', N'MSG_PAGO_RESERVA', N'Reserva inválida.'), (N'EN', N'MSG_PAGO_RESERVA', N'Invalid reservation.'), (N'PT', N'MSG_PAGO_RESERVA', N'Reserva inválida.'),
        (N'ES', N'MSG_PAGO_GUARDAR_RESERVA', N'Guarde la reserva antes de registrar pagos.'), (N'EN', N'MSG_PAGO_GUARDAR_RESERVA', N'Save the reservation before adding payments.'), (N'PT', N'MSG_PAGO_GUARDAR_RESERVA', N'Salve a reserva antes de registrar pagamentos.'),
        -- Comprobante / presupuesto (Proceso 1, paso 6)
        (N'ES', N'RES_COMPROBANTE_BTN', N'Comprobante'), (N'EN', N'RES_COMPROBANTE_BTN', N'Receipt'), (N'PT', N'RES_COMPROBANTE_BTN', N'Comprovante'),
        (N'ES', N'CMP_TITULO', N'Comprobante de Reserva'), (N'EN', N'CMP_TITULO', N'Reservation Receipt'), (N'PT', N'CMP_TITULO', N'Comprovante de Reserva'),
        (N'ES', N'CMP_TAGLINE', N'GESTIÓN DE EVENTOS'), (N'EN', N'CMP_TAGLINE', N'EVENT MANAGEMENT'), (N'PT', N'CMP_TAGLINE', N'GESTÃO DE EVENTOS'),
        (N'ES', N'CMP_DOC_NRO', N'Comprobante N'), (N'EN', N'CMP_DOC_NRO', N'Receipt No'), (N'PT', N'CMP_DOC_NRO', N'Comprovante N'),
        (N'ES', N'CMP_EMITIDO', N'Emitido'), (N'EN', N'CMP_EMITIDO', N'Issued'), (N'PT', N'CMP_EMITIDO', N'Emitido'),
        (N'ES', N'CMP_EVENTO', N'Evento'), (N'EN', N'CMP_EVENTO', N'Event'), (N'PT', N'CMP_EVENTO', N'Evento'),
        (N'ES', N'CMP_DETALLE_SERVICIOS', N'Detalle de servicios'), (N'EN', N'CMP_DETALLE_SERVICIOS', N'Services detail'), (N'PT', N'CMP_DETALLE_SERVICIOS', N'Detalhe de serviços'),
        (N'ES', N'CMP_SIN_SERVICIOS', N'Sin servicios contratados.'), (N'EN', N'CMP_SIN_SERVICIOS', N'No services added.'), (N'PT', N'CMP_SIN_SERVICIOS', N'Sem serviços contratados.'),
        (N'ES', N'CMP_SIN_PAGOS', N'Sin pagos registrados.'), (N'EN', N'CMP_SIN_PAGOS', N'No payments recorded.'), (N'PT', N'CMP_SIN_PAGOS', N'Sem pagamentos registrados.'),
        (N'ES', N'CMP_GRACIAS', N'Gracias por su reserva.'), (N'EN', N'CMP_GRACIAS', N'Thank you for your reservation.'), (N'PT', N'CMP_GRACIAS', N'Obrigado pela sua reserva.'),
        (N'ES', N'CMP_EST_PAGADO', N'Pagado'), (N'EN', N'CMP_EST_PAGADO', N'Paid'), (N'PT', N'CMP_EST_PAGADO', N'Pago'),
        (N'ES', N'CMP_EST_PARCIAL', N'Pago parcial'), (N'EN', N'CMP_EST_PARCIAL', N'Partially paid'), (N'PT', N'CMP_EST_PARCIAL', N'Pago parcial'),
        (N'ES', N'CMP_EST_PENDIENTE', N'Pendiente'), (N'EN', N'CMP_EST_PENDIENTE', N'Pending'), (N'PT', N'CMP_EST_PENDIENTE', N'Pendente'),
        -- Envio del comprobante por email (Proceso 1, paso 7 - mailto)
        (N'ES', N'RES_EMAIL_BTN', N'Email'), (N'EN', N'RES_EMAIL_BTN', N'Email'), (N'PT', N'RES_EMAIL_BTN', N'Email'),
        (N'ES', N'EMAIL_ASUNTO', N'Comprobante de reserva'), (N'EN', N'EMAIL_ASUNTO', N'Reservation receipt'), (N'PT', N'EMAIL_ASUNTO', N'Comprovante de reserva'),
        (N'ES', N'EMAIL_SALUDO', N'Hola {0},'), (N'EN', N'EMAIL_SALUDO', N'Hello {0},'), (N'PT', N'EMAIL_SALUDO', N'Olá {0},'),
        (N'ES', N'EMAIL_INTRO', N'Le enviamos el comprobante de su reserva #{0}.'), (N'EN', N'EMAIL_INTRO', N'We are sending you the receipt for your reservation #{0}.'), (N'PT', N'EMAIL_INTRO', N'Enviamos o comprovante da sua reserva #{0}.'),
        (N'ES', N'EMAIL_CIERRE', N'Adjuntamos el comprobante. Saludos, EvenTech.'), (N'EN', N'EMAIL_CIERRE', N'The receipt is attached. Regards, EvenTech.'), (N'PT', N'EMAIL_CIERRE', N'O comprovante está anexado. Saudações, EvenTech.'),
        (N'ES', N'MSG_EMAIL_SIN_CORREO', N'El cliente no tiene email cargado.'), (N'EN', N'MSG_EMAIL_SIN_CORREO', N'The client has no email on file.'), (N'PT', N'MSG_EMAIL_SIN_CORREO', N'O cliente não tem email cadastrado.'),
        (N'ES', N'MSG_EMAIL_ADJUNTAR', N'Se abrió tu correo con el mensaje listo. Adjunta el comprobante (abrimos su carpeta) y envialo.'), (N'EN', N'MSG_EMAIL_ADJUNTAR', N'Your email client opened with the message. Attach the receipt (we opened its folder) and send it.'), (N'PT', N'MSG_EMAIL_ADJUNTAR', N'Seu cliente de email abriu com a mensagem. Anexe o comprovante (abrimos a pasta) e envie.'),
        -- Pulido i18n: estados de reserva (mostrados en grilla/combo/comprobante)
        (N'ES', N'EST_COTIZACION', N'Cotización'), (N'EN', N'EST_COTIZACION', N'Quote'), (N'PT', N'EST_COTIZACION', N'Orçamento'),
        (N'ES', N'EST_PENDIENTE', N'Pendiente'), (N'EN', N'EST_PENDIENTE', N'Pending'), (N'PT', N'EST_PENDIENTE', N'Pendente'),
        (N'ES', N'EST_CONFIRMADA', N'Confirmada'), (N'EN', N'EST_CONFIRMADA', N'Confirmed'), (N'PT', N'EST_CONFIRMADA', N'Confirmada'),
        (N'ES', N'EST_CANCELADA', N'Cancelada'), (N'EN', N'EST_CANCELADA', N'Cancelled'), (N'PT', N'EST_CANCELADA', N'Cancelada'),
        -- Pulido i18n: criticidad de bitacora (combo + grilla)
        (N'ES', N'CRIT_INFO', N'Información'), (N'EN', N'CRIT_INFO', N'Information'), (N'PT', N'CRIT_INFO', N'Informação'),
        (N'ES', N'CRIT_ADVERTENCIA', N'Advertencia'), (N'EN', N'CRIT_ADVERTENCIA', N'Warning'), (N'PT', N'CRIT_ADVERTENCIA', N'Aviso'),
        (N'ES', N'CRIT_ERROR', N'Error'), (N'EN', N'CRIT_ERROR', N'Error'), (N'PT', N'CRIT_ERROR', N'Erro'),
        -- Pulido i18n: acciones de auditoria de login (combo + grilla)
        (N'ES', N'ACC_LOGIN_OK', N'Ingreso correcto'), (N'EN', N'ACC_LOGIN_OK', N'Login OK'), (N'PT', N'ACC_LOGIN_OK', N'Login OK'),
        (N'ES', N'ACC_LOGIN_FAIL', N'Ingreso fallido'), (N'EN', N'ACC_LOGIN_FAIL', N'Login failed'), (N'PT', N'ACC_LOGIN_FAIL', N'Falha no login'),
        (N'ES', N'ACC_LOGOUT', N'Cierre de sesión'), (N'EN', N'ACC_LOGOUT', N'Logout'), (N'PT', N'ACC_LOGOUT', N'Fim de sessão'),
        -- Pulido i18n: mensajes de error genericos
        (N'ES', N'MSG_ERROR', N'Error'), (N'EN', N'MSG_ERROR', N'Error'), (N'PT', N'MSG_ERROR', N'Erro'),
        (N'ES', N'MSG_ERROR_PREFIJO', N'Error: '), (N'EN', N'MSG_ERROR_PREFIJO', N'Error: '), (N'PT', N'MSG_ERROR_PREFIJO', N'Erro: '),
        -- Pulido i18n: etiquetas del comprobante y dialogo de guardado
        (N'ES', N'LBL_DNI', N'DNI'), (N'EN', N'LBL_DNI', N'ID'), (N'PT', N'LBL_DNI', N'CPF'),
        (N'ES', N'LBL_EMAIL', N'Email'), (N'EN', N'LBL_EMAIL', N'Email'), (N'PT', N'LBL_EMAIL', N'E-mail'),
        (N'ES', N'LBL_TELEFONO', N'Tel'), (N'EN', N'LBL_TELEFONO', N'Phone'), (N'PT', N'LBL_TELEFONO', N'Tel'),
        (N'ES', N'CMP_FILTER', N'Documento HTML (*.html)|*.html'), (N'EN', N'CMP_FILTER', N'HTML document (*.html)|*.html'), (N'PT', N'CMP_FILTER', N'Documento HTML (*.html)|*.html'),
        (N'ES', N'CMP_FILENAME', N'Comprobante_Reserva_'), (N'EN', N'CMP_FILENAME', N'Reservation_Receipt_'), (N'PT', N'CMP_FILENAME', N'Comprovante_Reserva_'),
        -- Auditoria unificada (tabs)
        (N'ES', N'AUD_TAB_BITACORA', N'Bitácora general'),            (N'EN', N'AUD_TAB_BITACORA', N'General audit log'),           (N'PT', N'AUD_TAB_BITACORA', N'Registro geral'),
        (N'ES', N'AUD_TAB_LOGIN', N'Auditoría de login'),             (N'EN', N'AUD_TAB_LOGIN', N'Login audit'),                    (N'PT', N'AUD_TAB_LOGIN', N'Auditoria de login'),
        -- Integridad (T08): recalculo de linea base desde Auditoria
        (N'ES', N'AUD_RECALC_BTN', N'Recalcular línea base'), (N'EN', N'AUD_RECALC_BTN', N'Recalculate baseline'), (N'PT', N'AUD_RECALC_BTN', N'Recalcular linha de base'),
        (N'ES', N'AUD_RECALC_CONFIRMA', N'¿Recalcular los dígitos verificadores de todas las reservas y de todos los pagos? Usar después de corregir datos alterados: la línea base nueva pasa a ser la referencia de integridad.'), (N'EN', N'AUD_RECALC_CONFIRMA', N'Recalculate the verification digits of all reservations and all payments? Use after fixing altered data: the new baseline becomes the integrity reference.'), (N'PT', N'AUD_RECALC_CONFIRMA', N'Recalcular os dígitos verificadores de todas as reservas e de todos os pagamentos? Usar após corrigir dados alterados: a nova linha de base passa a ser a referência de integridade.'),
        (N'ES', N'AUD_RECALC_OK', N'Línea base recalculada ({0} reservas, con sus pagos). Verificación posterior: {1} inconsistencia(s).'), (N'EN', N'AUD_RECALC_OK', N'Baseline recalculated ({0} reservations, with their payments). Post-check: {1} inconsistency(ies).'), (N'PT', N'AUD_RECALC_OK', N'Linha de base recalculada ({0} reservas, com seus pagamentos). Verificação posterior: {1} inconsistência(s).')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- Correcciones de textos de fabrica para bases sembradas por versiones
-- anteriores. Las semillas de arriba ya traen el texto final (una base nueva
-- no pasa por aca); en una base existente se corrige solo si el texto vigente
-- es exactamente el que sembro una version anterior, asi una traduccion
-- editada por el usuario desde Gestion de Idiomas se conserva. Aplican solo a
-- los tres idiomas del sistema: un idioma agregado por el usuario conserva su
-- traduccion propia.
-- ===========================================================================

-- La seccion de auditoria unifica bitacora general + auditoria de login, por
-- eso el item del menu pasa a llamarse simplemente "Auditoria".
UPDATE t SET Texto = CASE i.Codigo WHEN N'EN' THEN N'Audit' WHEN N'PT' THEN N'Auditoria' ELSE N'Auditoría' END
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
WHERE t.Clave = N'MENU_AUDITORIA'
  AND ((i.Codigo = N'ES' AND t.Texto IN (N'Auditoria login', N'Auditoria'))
    OR (i.Codigo = N'EN' AND t.Texto = N'Login audit')
    OR (i.Codigo = N'PT' AND t.Texto = N'Auditoria de login'));
GO

-- El boton de historial comparte fila con "Pagos" en la ficha de reserva:
-- se acorta para no truncarse a media anchura.
UPDATE t SET Texto = CASE i.Codigo WHEN N'EN' THEN N'History' WHEN N'PT' THEN N'Histórico' ELSE N'Historial' END
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
WHERE t.Clave = N'RES_HISTORIAL'
  AND ((i.Codigo = N'ES' AND t.Texto = N'Ver historial de cambios')
    OR (i.Codigo = N'EN' AND t.Texto = N'View change history')
    OR (i.Codigo = N'PT' AND t.Texto IN (N'Ver historico de alteracoes', N'Historico')));
GO

-- ===========================================================================
-- Patron Memento: versiones de reservas para poder volver a un estado previo.
-- Cada fila de ReservaMemento es la foto completa de la reserva tomada antes
-- de una modificacion; ReservaMementoServicio congela las lineas de servicios
-- de esa foto (el monto se deriva de ellas).
-- ===========================================================================
IF OBJECT_ID('dbo.ReservaMemento','U') IS NULL
BEGIN
    CREATE TABLE dbo.ReservaMemento (
        Id          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReservaMemento PRIMARY KEY,
        ReservaId   INT               NOT NULL,
        ClienteId   INT               NOT NULL,
        SalonId     INT               NOT NULL,
        FechaEvento DATETIME          NOT NULL,
        Estado      NVARCHAR(20)      NOT NULL,
        Monto       DECIMAL(12,2)     NOT NULL,
        Usuario     NVARCHAR(50)      NOT NULL,
        Fecha       DATETIME          NOT NULL CONSTRAINT DF_ReservaMemento_Fecha DEFAULT GETDATE(),
        CONSTRAINT FK_ReservaMemento_Reserva FOREIGN KEY (ReservaId) REFERENCES dbo.Reservas(Id)
    );
    CREATE INDEX IX_ReservaMemento_Reserva ON dbo.ReservaMemento(ReservaId);
END
GO

-- La foto del Memento tiene que conservar TODO el estado de negocio de la reserva,
-- incluida la cantidad de invitados: si no, restaurar una version previa repondria
-- el salon viejo con los invitados nuevos y podria violar la RN-06.
IF COL_LENGTH('dbo.ReservaMemento','CantidadInvitados') IS NULL
    ALTER TABLE dbo.ReservaMemento ADD CantidadInvitados INT NOT NULL
        CONSTRAINT DF_ReservaMemento_CantidadInvitados DEFAULT 0;
GO

IF OBJECT_ID('dbo.ReservaMementoServicio','U') IS NULL
BEGIN
    CREATE TABLE dbo.ReservaMementoServicio (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReservaMementoServicio PRIMARY KEY,
        MementoId      INT           NOT NULL,
        ServicioId     INT           NOT NULL,
        Cantidad       INT           NOT NULL CONSTRAINT DF_ReservaMementoServicio_Cantidad DEFAULT 1,
        PrecioUnitario DECIMAL(12,2) NOT NULL CONSTRAINT DF_ReservaMementoServicio_Precio DEFAULT 0,
        CONSTRAINT FK_ReservaMementoServicio_Memento  FOREIGN KEY (MementoId)  REFERENCES dbo.ReservaMemento(Id),
        CONSTRAINT FK_ReservaMementoServicio_Servicio FOREIGN KEY (ServicioId) REFERENCES dbo.Servicios(Id)
    );
    CREATE INDEX IX_ReservaMementoServicio_Memento ON dbo.ReservaMementoServicio(MementoId);
END
GO

-- ===========================================================================
-- Composite de perfiles: un perfil puede INCLUIR otros perfiles y hereda sus
-- permisos (p.ej. Gerencial contiene a Vendedor). Relacion reflexiva N:M;
-- los ciclos se validan en la capa de negocio (BLL_Perfil).
-- ===========================================================================
IF OBJECT_ID('dbo.PerfilIncluido','U') IS NULL
BEGIN
    CREATE TABLE dbo.PerfilIncluido (
        PerfilPadreId INT NOT NULL,
        PerfilHijoId  INT NOT NULL,
        CONSTRAINT PK_PerfilIncluido PRIMARY KEY (PerfilPadreId, PerfilHijoId),
        CONSTRAINT FK_PerfilIncluido_Padre FOREIGN KEY (PerfilPadreId) REFERENCES dbo.Perfiles(Id),
        CONSTRAINT FK_PerfilIncluido_Hijo  FOREIGN KEY (PerfilHijoId)  REFERENCES dbo.Perfiles(Id),
        CONSTRAINT CK_PerfilIncluido_NoSelf CHECK (PerfilPadreId <> PerfilHijoId)
    );
END
GO

-- La rama "Perfiles incluidos" del arbol de gestion de perfiles (bases que
-- sembraron la leyenda corta o la larga sin tildes; ver la nota sobre las
-- correcciones de textos de fabrica).
UPDATE t SET Texto = CASE i.Codigo
        WHEN N'EN' THEN N'Check the profile permissions. Checking a group includes its children; under "Included profiles" you can nest other profiles and inherit their permissions.'
        WHEN N'PT' THEN N'Marque as permissões do perfil. Marcar um grupo inclui seus filhos; em "Perfis incluídos" você pode conter outros perfis e herdar suas permissões.'
        ELSE N'Tilde los permisos del perfil. Marcar un grupo incluye a sus hijos; en "Perfiles incluidos" podés contener otros perfiles y heredar sus permisos.' END
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
WHERE t.Clave = N'PERF_HINT'
  AND ((i.Codigo = N'ES' AND t.Texto IN (N'Tilde los permisos del perfil. Marcar un grupo incluye a sus hijos.', N'Tilde los permisos del perfil. Marcar un grupo incluye a sus hijos; en "Perfiles incluidos" podes contener otros perfiles y heredar sus permisos.'))
    OR (i.Codigo = N'EN' AND t.Texto = N'Check the profile permissions. Checking a group includes its children.')
    OR (i.Codigo = N'PT' AND t.Texto IN (N'Marque as permissoes do perfil. Marcar um grupo inclui seus filhos.', N'Marque as permissoes do perfil. Marcar um grupo inclui seus filhos; em "Perfis incluidos" voce pode conter outros perfis e herdar suas permissoes.')));
GO

-- Traducciones del modulo de versiones (idempotente: solo inserta las que falten).
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'PERF_INCLUIDOS', N'Perfiles incluidos'), (N'EN', N'PERF_INCLUIDOS', N'Included profiles'), (N'PT', N'PERF_INCLUIDOS', N'Perfis incluídos'),
        (N'ES', N'PERF_HEREDADO', N'(heredado)'), (N'EN', N'PERF_HEREDADO', N'(inherited)'), (N'PT', N'PERF_HEREDADO', N'(herdado)'),
        (N'ES', N'MSG_PERF_CICLO', N'No se puede incluir ese perfil: generaría una referencia circular.'), (N'EN', N'MSG_PERF_CICLO', N'That profile cannot be included: it would create a circular reference.'), (N'PT', N'MSG_PERF_CICLO', N'Não é possível incluir esse perfil: geraria uma referência circular.'),
        (N'ES', N'RES_VERSIONES', N'Versiones'), (N'EN', N'RES_VERSIONES', N'Versions'), (N'PT', N'RES_VERSIONES', N'Versões'),
        (N'ES', N'VER_TITULO', N'Versiones de la reserva'), (N'EN', N'VER_TITULO', N'Reservation versions'), (N'PT', N'VER_TITULO', N'Versões da reserva'),
        (N'ES', N'VER_RESTAURAR', N'Restaurar seleccionada'), (N'EN', N'VER_RESTAURAR', N'Restore selected'), (N'PT', N'VER_RESTAURAR', N'Restaurar selecionada'),
        (N'ES', N'VER_VACIO', N'Sin versiones guardadas. Se crea una automáticamente al modificar la reserva.'), (N'EN', N'VER_VACIO', N'No saved versions. One is created automatically when the reservation is modified.'), (N'PT', N'VER_VACIO', N'Sem versões salvas. Uma é criada automaticamente ao modificar a reserva.'),
        (N'ES', N'VER_CONFIRMA', N'¿Restaurar la reserva al estado de la versión seleccionada? El estado actual se guardará como una nueva versión.'), (N'EN', N'VER_CONFIRMA', N'Restore the reservation to the selected version? The current state will be saved as a new version.'), (N'PT', N'VER_CONFIRMA', N'Restaurar a reserva ao estado da versão selecionada? O estado atual será salvo como uma nova versão.'),
        (N'ES', N'MSG_VER_OK', N'Versión restaurada.'), (N'EN', N'MSG_VER_OK', N'Version restored.'), (N'PT', N'MSG_VER_OK', N'Versão restaurada.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- Control de acceso reforzado y configuracion de conexion
-- (idempotente: solo inserta las claves que falten).
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Segunda capa de permisos
        (N'ES', N'MSG_SIN_PERMISO', N'No tenés permiso para realizar esta acción.'), (N'EN', N'MSG_SIN_PERMISO', N'You do not have permission to perform this action.'), (N'PT', N'MSG_SIN_PERMISO', N'Você não tem permissão para realizar esta ação.'),
        (N'ES', N'MAIN_PERMISOS_ERROR', N'No se pudieron cargar los permisos de tu perfil, así que la sesión quedó sin acceso a las secciones. Volvé a iniciar sesión; si el problema sigue, avisale a un administrador.'), (N'EN', N'MAIN_PERMISOS_ERROR', N'Your profile permissions could not be loaded, so this session has no access to the sections. Sign in again; if the problem persists, contact an administrator.'), (N'PT', N'MAIN_PERMISOS_ERROR', N'Não foi possível carregar as permissões do seu perfil, então a sessão ficou sem acesso às seções. Entre novamente; se o problema continuar, avise um administrador.'),
        -- Reserva cancelada (estado terminal)
        (N'ES', N'MSG_RES_VENCIDA', N'La operación venció: renovala antes de cambiar su estado.'), (N'EN', N'MSG_RES_VENCIDA', N'The operation expired: renew it before changing its status.'), (N'PT', N'MSG_RES_VENCIDA', N'A operação venceu: renove-a antes de mudar seu estado.'),
        (N'ES', N'COL_VENCE', N'Vence'), (N'EN', N'COL_VENCE', N'Expires'), (N'PT', N'COL_VENCE', N'Vence'),
        (N'ES', N'MSG_RES_RENOVADA', N'Vigencia renovada.'), (N'EN', N'MSG_RES_RENOVADA', N'Validity renewed.'), (N'PT', N'MSG_RES_RENOVADA', N'Vigência renovada.'),
        (N'ES', N'MSG_RES_SIN_PLAZO', N'La operación no tiene un plazo de vigencia que renovar.'), (N'EN', N'MSG_RES_SIN_PLAZO', N'The operation has no validity period to renew.'), (N'PT', N'MSG_RES_SIN_PLAZO', N'A operação não possui prazo de validade para renovar.'),
        (N'ES', N'MSG_RES_CANCELAR', N'¿Cancelar la reserva #{0}?'), (N'EN', N'MSG_RES_CANCELAR', N'Cancel reservation #{0}?'), (N'PT', N'MSG_RES_CANCELAR', N'Cancelar a reserva #{0}?'),
        (N'ES', N'MSG_RES_CANCELADA', N'Reserva cancelada. Retenido {0:N2}, reintegro {1:N2}.'), (N'EN', N'MSG_RES_CANCELADA', N'Reservation cancelled. Retained {0:N2}, refund {1:N2}.'), (N'PT', N'MSG_RES_CANCELADA', N'Reserva cancelada. Retido {0:N2}, reembolso {1:N2}.'),
        (N'ES', N'MSG_RES_NO_MODIFICABLE', N'La reserva está cancelada: no admite modificaciones.'), (N'EN', N'MSG_RES_NO_MODIFICABLE', N'The reservation is cancelled: it cannot be modified.'), (N'PT', N'MSG_RES_NO_MODIFICABLE', N'A reserva está cancelada: não admite modificações.'),
        -- Anulacion de pagos
        (N'ES', N'MSG_PAGO_ANULAR_CONF', N'¿Anular el pago de {0}? La operación no se puede deshacer.'), (N'EN', N'MSG_PAGO_ANULAR_CONF', N'Void the payment of {0}? This action cannot be undone.'), (N'PT', N'MSG_PAGO_ANULAR_CONF', N'Anular o pagamento de {0}? A operação não pode ser desfeita.'),
        -- Configuracion de conexion (pre-login)
        (N'ES', N'CONN_TITULO', N'Configuración de conexión'), (N'EN', N'CONN_TITULO', N'Connection settings'), (N'PT', N'CONN_TITULO', N'Configuração de conexão'),
        (N'ES', N'CONN_AYUDA', N'No se pudo conectar a la base de datos. Indica dónde está la instancia de SQL Server y el nombre de la base. La configuración se guarda cifrada en tu perfil de Windows.'), (N'EN', N'CONN_AYUDA', N'Could not connect to the database. Enter the SQL Server instance and the database name. The setting is stored encrypted in your Windows profile.'), (N'PT', N'CONN_AYUDA', N'Não foi possível conectar ao banco de dados. Informe a instância do SQL Server e o nome do banco. A configuração é salva criptografada no seu perfil do Windows.'),
        (N'ES', N'CONN_SERVIDOR', N'Instancia de SQL Server'), (N'EN', N'CONN_SERVIDOR', N'SQL Server instance'), (N'PT', N'CONN_SERVIDOR', N'Instância do SQL Server'),
        (N'ES', N'CONN_BASE', N'Base de datos'), (N'EN', N'CONN_BASE', N'Database'), (N'PT', N'CONN_BASE', N'Banco de dados'),
        (N'ES', N'CONN_PROBAR', N'Probar'), (N'EN', N'CONN_PROBAR', N'Test'), (N'PT', N'CONN_PROBAR', N'Testar'),
        (N'ES', N'CONN_GUARDAR', N'Guardar'), (N'EN', N'CONN_GUARDAR', N'Save'), (N'PT', N'CONN_GUARDAR', N'Salvar'),
        (N'ES', N'CONN_SALIR', N'Salir'), (N'EN', N'CONN_SALIR', N'Exit'), (N'PT', N'CONN_SALIR', N'Sair'),
        (N'ES', N'CONN_PROBANDO', N'Probando conexión...'), (N'EN', N'CONN_PROBANDO', N'Testing connection...'), (N'PT', N'CONN_PROBANDO', N'Testando conexão...'),
        (N'ES', N'CONN_OK', N'Conexión correcta.'), (N'EN', N'CONN_OK', N'Connection successful.'), (N'PT', N'CONN_OK', N'Conexão correta.'),
        (N'ES', N'CONN_ESQUEMA_INCOMPLETO', N'La base ''{0}'' existe pero su esquema está incompleto o es de una versión anterior (faltan: {1}). Completalo con db/schema.sql (agrega solo lo que falta) antes de usarla.'), (N'EN', N'CONN_ESQUEMA_INCOMPLETO', N'Database ''{0}'' exists but its schema is incomplete or from a previous version (missing: {1}). Complete it with db/schema.sql (adds only what is missing) before using it.'), (N'PT', N'CONN_ESQUEMA_INCOMPLETO', N'O banco ''{0}'' existe mas seu esquema está incompleto ou é de uma versão anterior (faltam: {1}). Complete-o com db/schema.sql (adiciona apenas o que falta) antes de usá-lo.'),
        -- Clave AES de los contactos (CryptoService): ilegible o de otra maquina
        (N'ES', N'CRYPTO_CLAVE_INVALIDA', N'La clave de cifrado {0} no se puede leer: está dañada o fue creada en otra máquina. Restaure el archivo original o elimínelo para generar una clave nueva (los contactos ya cifrados quedarán ilegibles).'), (N'EN', N'CRYPTO_CLAVE_INVALIDA', N'The encryption key {0} cannot be read: it is damaged or was created on another machine. Restore the original file or delete it to generate a new key (already encrypted contacts will become unreadable).'), (N'PT', N'CRYPTO_CLAVE_INVALIDA', N'A chave de criptografia {0} não pode ser lida: está danificada ou foi criada em outra máquina. Restaure o arquivo original ou exclua-o para gerar uma nova chave (os contatos já criptografados ficarão ilegíveis).')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- Consulta de disponibilidad (Proceso 1, paso 1) y presupuesto/comprobante
-- (Proceso 1, paso 6). Idempotente: solo inserta lo que falte.
-- ===========================================================================

-- Permiso hoja bajo "Gestion de Reservas": la consulta es el arranque del
-- proceso de venta y se concede por perfil como el resto de las operaciones.
DECLARE @gReservasDisp INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Gestion de Reservas' AND EsGrupo = 1);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'DISPONIBILIDAD_CONSULTAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Consultar Disponibilidad', N'Verificar salones libres por fecha y capacidad', 0, N'DISPONIBILIDAD_CONSULTAR', @gReservasDisp);

-- Acceso total del Administrador: se le asigna todo permiso que le falte.
INSERT INTO dbo.PerfilPermiso (PerfilId, PermisoId)
SELECT p.Id, pe.Id
FROM dbo.Perfiles p
CROSS JOIN dbo.Permisos pe
WHERE p.Nombre = N'Administrador'
  AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermiso pp
                  WHERE pp.PerfilId = p.Id AND pp.PermisoId = pe.Id);
GO


;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Consulta de disponibilidad (Proceso 1, paso 1)
        (N'ES', N'RES_DISPONIBILIDAD_BTN', N'Disponibilidad'), (N'EN', N'RES_DISPONIBILIDAD_BTN', N'Availability'), (N'PT', N'RES_DISPONIBILIDAD_BTN', N'Disponibilidade'),
        (N'ES', N'DISP_TITULO', N'Consulta de disponibilidad'), (N'EN', N'DISP_TITULO', N'Availability check'), (N'PT', N'DISP_TITULO', N'Consulta de disponibilidade'),
        (N'ES', N'DISP_LBL_CAPACIDAD', N'Invitados estimados'), (N'EN', N'DISP_LBL_CAPACIDAD', N'Estimated guests'), (N'PT', N'DISP_LBL_CAPACIDAD', N'Convidados estimados'),
        (N'ES', N'BTN_CONSULTAR', N'Consultar'), (N'EN', N'BTN_CONSULTAR', N'Check'), (N'PT', N'BTN_CONSULTAR', N'Consultar'),
        (N'ES', N'COL_CAPACIDAD', N'Capacidad'), (N'EN', N'COL_CAPACIDAD', N'Capacity'), (N'PT', N'COL_CAPACIDAD', N'Capacidade'),
        (N'ES', N'DISP_COL_PROPUESTA', N'Próxima fecha libre'), (N'EN', N'DISP_COL_PROPUESTA', N'Next free date'), (N'PT', N'DISP_COL_PROPUESTA', N'Próxima data livre'),
        (N'ES', N'DISP_EST_DISPONIBLE', N'Disponible'), (N'EN', N'DISP_EST_DISPONIBLE', N'Available'), (N'PT', N'DISP_EST_DISPONIBLE', N'Disponível'),
        (N'ES', N'DISP_EST_OCUPADO', N'Ocupado'), (N'EN', N'DISP_EST_OCUPADO', N'Booked'), (N'PT', N'DISP_EST_OCUPADO', N'Ocupado'),
        (N'ES', N'DISP_EST_CAPACIDAD', N'Capacidad insuficiente'), (N'EN', N'DISP_EST_CAPACIDAD', N'Insufficient capacity'), (N'PT', N'DISP_EST_CAPACIDAD', N'Capacidade insuficiente'),
        (N'ES', N'DISP_USAR', N'Usar en la reserva'), (N'EN', N'DISP_USAR', N'Use in reservation'), (N'PT', N'DISP_USAR', N'Usar na reserva'),
        (N'ES', N'DISP_RESUMEN_OK', N'{0} salón(es) disponible(s) para la fecha consultada.'), (N'EN', N'DISP_RESUMEN_OK', N'{0} venue(s) available for the requested date.'), (N'PT', N'DISP_RESUMEN_OK', N'{0} salão(ões) disponível(is) para a data consultada.'),
        (N'ES', N'DISP_RESUMEN_ALTERNATIVAS', N'Ningún salón disponible para esa fecha: se proponen fechas alternativas.'), (N'EN', N'DISP_RESUMEN_ALTERNATIVAS', N'No venue available for that date: alternative dates are suggested.'), (N'PT', N'DISP_RESUMEN_ALTERNATIVAS', N'Nenhum salão disponível para essa data: datas alternativas são propostas.'),
        (N'ES', N'DISP_SELECCIONE', N'Seleccione un salón de la grilla.'), (N'EN', N'DISP_SELECCIONE', N'Select a venue from the grid.'), (N'PT', N'DISP_SELECCIONE', N'Selecione um salão da grade.'),
        (N'ES', N'DISP_SIN_PROPUESTA', N'El salón no tiene fechas libres en el horizonte consultado.'), (N'EN', N'DISP_SIN_PROPUESTA', N'The venue has no free dates within the searched range.'), (N'PT', N'DISP_SIN_PROPUESTA', N'O salão não tem datas livres no período consultado.'),
        -- Presupuesto para cotizaciones (Proceso 1, paso 6)
        (N'ES', N'CMP_TITULO_PRESUPUESTO', N'Presupuesto'), (N'EN', N'CMP_TITULO_PRESUPUESTO', N'Quote'), (N'PT', N'CMP_TITULO_PRESUPUESTO', N'Orçamento'),
        (N'ES', N'CMP_DOC_NRO_PRESUPUESTO', N'Presupuesto N'), (N'EN', N'CMP_DOC_NRO_PRESUPUESTO', N'Quote No'), (N'PT', N'CMP_DOC_NRO_PRESUPUESTO', N'Orçamento N'),
        (N'ES', N'CMP_PRESUPUESTO_NOTA', N'Presupuesto sin compromiso de reserva. Sujeto a disponibilidad del salón al momento de confirmar.'), (N'EN', N'CMP_PRESUPUESTO_NOTA', N'Quote with no booking commitment. Subject to venue availability at confirmation time.'), (N'PT', N'CMP_PRESUPUESTO_NOTA', N'Orçamento sem compromisso de reserva. Sujeito à disponibilidade do salão no momento da confirmação.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- Ciclo de vida de la reserva (RN-05 transiciones, RN-06 capacidad) y
-- cantidad de invitados. Idempotente: solo inserta las claves que falten.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Cantidad de invitados (PN1: Cantidad_Invitados)
        (N'ES', N'COL_INVITADOS', N'Invitados'), (N'EN', N'COL_INVITADOS', N'Guests'), (N'PT', N'COL_INVITADOS', N'Convidados'),
        (N'ES', N'RES_LBL_INVITADOS', N'Invitados estimados'), (N'EN', N'RES_LBL_INVITADOS', N'Estimated guests'), (N'PT', N'RES_LBL_INVITADOS', N'Convidados estimados'),
        -- RN-05: transiciones de estado admitidas
        (N'ES', N'MSG_RES_TRANSICION', N'No se admite pasar de {0} a {1}.'), (N'EN', N'MSG_RES_TRANSICION', N'Moving from {0} to {1} is not allowed.'), (N'PT', N'MSG_RES_TRANSICION', N'Não é admitido passar de {0} para {1}.'),
        -- RN-04: el total no puede quedar por debajo de lo ya cobrado
        (N'ES', N'MSG_RES_MONTO_PAGADO', N'El total de la reserva no puede quedar por debajo de lo ya cobrado.'), (N'EN', N'MSG_RES_MONTO_PAGADO', N'The reservation total cannot fall below the amount already collected.'), (N'PT', N'MSG_RES_MONTO_PAGADO', N'O total da reserva não pode ficar abaixo do valor já cobrado.'),
        -- RN-06: el salon tiene que alojar a los invitados al confirmar
        (N'ES', N'MSG_RES_CAPACIDAD', N'El salón no alcanza para la cantidad de invitados indicada.'), (N'EN', N'MSG_RES_CAPACIDAD', N'The venue cannot hold the number of guests entered.'), (N'PT', N'MSG_RES_CAPACIDAD', N'O salão não comporta a quantidade de convidados informada.'),
        (N'ES', N'MSG_RES_INVITADOS', N'Indica la cantidad de invitados estimada: hace falta para confirmar y no puede ser negativa.'), (N'EN', N'MSG_RES_INVITADOS', N'Enter the estimated number of guests: it is required to confirm and cannot be negative.'), (N'PT', N'MSG_RES_INVITADOS', N'Informe a quantidade estimada de convidados: é necessária para confirmar e não pode ser negativa.'),
        (N'ES', N'MSG_RES_TRANSICION_GEN', N'El cambio de estado solicitado no está admitido.'), (N'EN', N'MSG_RES_TRANSICION_GEN', N'The requested status change is not allowed.'), (N'PT', N'MSG_RES_TRANSICION_GEN', N'A mudança de estado solicitada não é admitida.'),
        -- Alta de cliente: confirmacion en pantalla (CUN003, paso 5)
        (N'ES', N'MSG_CLI_CREADO', N'Cliente registrado.'), (N'EN', N'MSG_CLI_CREADO', N'Customer registered.'), (N'PT', N'MSG_CLI_CREADO', N'Cliente registrado.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- El rechazo por invitados cubre dos causas: el dato falta al confirmar (RN-06)
-- o es negativo. El texto sembrado originalmente solo nombraba la segunda; en
-- una base existente se corrige por UPDATE guardado por el texto anterior.
UPDATE t SET Texto = CASE i.Codigo
        WHEN N'EN' THEN N'Enter the estimated number of guests: it is required to confirm and cannot be negative.'
        WHEN N'PT' THEN N'Informe a quantidade estimada de convidados: é necessária para confirmar e não pode ser negativa.'
        ELSE N'Indica la cantidad de invitados estimada: hace falta para confirmar y no puede ser negativa.' END
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
WHERE t.Clave = N'MSG_RES_INVITADOS'
  AND ((i.Codigo = N'ES' AND t.Texto = N'La cantidad de invitados no puede ser negativa.')
    OR (i.Codigo = N'EN' AND t.Texto = N'The number of guests cannot be negative.')
    OR (i.Codigo = N'PT' AND t.Texto IN (N'A quantidade de convidados nao pode ser negativa.', N'Informe a quantidade estimada de convidados: e necessaria para confirmar e nao pode ser negativa.')));
GO

-- ===========================================================================
-- RN-07 (adelanto para confirmar) y avisos de anulacion de pago y de servicios.
-- Idempotente: solo inserta las claves que falten.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- RN-07: la reserva queda firme cuando se registro el adelanto
        (N'ES', N'MSG_RES_SIN_ADELANTO', N'Para confirmar la reserva hay que registrar el adelanto: guardala y cobra el pago desde Pagos.'), (N'EN', N'MSG_RES_SIN_ADELANTO', N'To confirm the reservation the deposit must be recorded: save it and take the payment from Payments.'), (N'PT', N'MSG_RES_SIN_ADELANTO', N'Para confirmar a reserva é preciso registrar o adiantamento: salve-a e cobre o pagamento em Pagamentos.'),
        -- Anulacion de pago rechazada (pago inexistente o de otra reserva)
        (N'ES', N'MSG_PAGO_NO_ANULABLE', N'El pago ya no existe o no pertenece a esta reserva.'), (N'EN', N'MSG_PAGO_NO_ANULABLE', N'The payment no longer exists or does not belong to this reservation.'), (N'PT', N'MSG_PAGO_NO_ANULABLE', N'O pagamento não existe mais ou não pertence a esta reserva.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- ===========================================================================
-- Mensajes propios de cada accion. Varias pantallas reutilizaban un texto de
-- otra operacion ("Guarde la reserva antes de registrar pagos" al pedir el
-- comprobante, "No se pudo guardar la reserva" al consultar disponibilidad),
-- de modo que el aviso no correspondia a lo que el usuario habia pedido.
-- Idempotente: solo inserta las claves que falten.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Comprobante y envio por correo sobre una reserva todavia no guardada
        (N'ES', N'MSG_RES_GUARDAR_PRIMERO', N'Guarde la reserva antes de emitir su documentación.'), (N'EN', N'MSG_RES_GUARDAR_PRIMERO', N'Save the reservation before issuing its paperwork.'), (N'PT', N'MSG_RES_GUARDAR_PRIMERO', N'Salve a reserva antes de emitir sua documentação.'),
        -- Error generico de una operacion que no es un guardado de reserva
        (N'ES', N'MSG_OP_ERROR', N'No se pudo completar la operación.'), (N'EN', N'MSG_OP_ERROR', N'The operation could not be completed.'), (N'PT', N'MSG_OP_ERROR', N'Não foi possível concluir a operação.'),
        -- Seleccion de una reserva para una accion que no es el historial
        (N'ES', N'MSG_RES_SELECCIONE_GEN', N'Seleccione una reserva existente.'), (N'EN', N'MSG_RES_SELECCIONE_GEN', N'Select an existing reservation.'), (N'PT', N'MSG_RES_SELECCIONE_GEN', N'Selecione uma reserva existente.'),
        -- RN-02: lo que se retiene y se reintegra SI se confirma la cancelacion
        (N'ES', N'MSG_RES_CANCELAR_DETALLE', N'Si se cancela hoy se retienen {0:N2} y se reintegran {1:N2}.'), (N'EN', N'MSG_RES_CANCELAR_DETALLE', N'Cancelling today retains {0:N2} and refunds {1:N2}.'), (N'PT', N'MSG_RES_CANCELAR_DETALLE', N'Cancelando hoje são retidos {0:N2} e reembolsados {1:N2}.'),
        -- Alta en las secciones cuyo sustantivo es masculino (Cliente, Servicio)
        (N'ES', N'BTN_NUEVO', N'Nuevo'), (N'EN', N'BTN_NUEVO', N'New'), (N'PT', N'BTN_NUEVO', N'Novo')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- El cliente de la reserva se elige de una lista desplegable, no se escribe: el
-- texto original ("Ingrese el nombre del cliente") no corresponde a ese control.
-- En una base existente se corrige por UPDATE guardado por el texto anterior.
UPDATE t SET Texto = CASE i.Codigo
        WHEN N'EN' THEN N'Select a valid client.'
        WHEN N'PT' THEN N'Selecione um cliente válido.'
        ELSE N'Seleccione un cliente válido.' END
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
WHERE t.Clave = N'MSG_RES_CLIENTE'
  AND ((i.Codigo = N'ES' AND t.Texto IN (N'Ingrese el nombre del cliente.', N'Seleccione un cliente valido.'))
    OR (i.Codigo = N'EN' AND t.Texto = N'Enter the client name.')
    OR (i.Codigo = N'PT' AND t.Texto IN (N'Informe o nome do cliente.', N'Selecione um cliente valido.')));
GO

-- ===========================================================================
-- Ortografia de los textos ES/PT (tildes, enie, cedilla) y reformulacion de
-- MSG_RES_VENCIDA (RN-01 rechaza cualquier cambio de estado de una operacion
-- vencida, no solo la confirmacion). Las semillas de arriba ya llevan el texto
-- final; una base sembrada por una version anterior conserva los textos sin
-- acentos y se corrige aca, solo si el texto vigente es exactamente el que
-- sembro aquella version (una traduccion editada por el usuario se conserva).
-- ===========================================================================
;WITH Fix(Codigo, Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MENU_BITACORA', N'Bitacora', N'Bitácora'),
        (N'ES', N'MENU_SALIR', N'Cerrar sesion', N'Cerrar sesión'),
        (N'ES', N'LOGIN_PASS', N'Contrasena', N'Contraseña'),
        (N'ES', N'LOGIN_CREATE', N'No tenes cuenta? Crear', N'¿No tenés cuenta? Crear'),
        (N'ES', N'MAIN_SESSION', N'Sesion iniciada por:', N'Sesión iniciada por:'),
        (N'ES', N'MAIN_SUBTITLE', N'Usa el menu de la izquierda para gestionar el sistema.', N'Usa el menú de la izquierda para gestionar el sistema.'),
        (N'ES', N'MAIN_SIN_ROL', N'Tu cuenta todavia no tiene un perfil asignado. Contactate con un administrador para que te asigne uno.', N'Tu cuenta todavía no tiene un perfil asignado. Contactate con un administrador para que te asigne uno.'),
        (N'ES', N'COL_SALON', N'Salon', N'Salón'),
        (N'ES', N'COL_MODULO', N'Modulo', N'Módulo'),
        (N'ES', N'COL_ACCION', N'Accion', N'Acción'),
        (N'ES', N'COL_MAQUINA', N'Maquina', N'Máquina'),
        (N'ES', N'RES_TITULO', N'Gestion de Reservas', N'Gestión de Reservas'),
        (N'ES', N'BIT_TITULO', N'Bitacora del Sistema', N'Bitácora del Sistema'),
        (N'ES', N'PERF_TITULO', N'Gestion de Perfiles', N'Gestión de Perfiles'),
        (N'ES', N'IDI_TITULO', N'Gestion de Idiomas', N'Gestión de Idiomas'),
        (N'ES', N'IDI_CODIGO', N'Codigo (ej. PT)', N'Código (ej. PT)'),
        (N'ES', N'MSG_IDI_COD_INV', N'Codigo invalido (1 a 5 caracteres).', N'Código inválido (1 a 5 caracteres).'),
        (N'ES', N'MSG_IDI_DUP', N'Ya existe un idioma con ese codigo.', N'Ya existe un idioma con ese código.'),
        (N'ES', N'AUD_TITULO', N'Registro de Auditoria', N'Registro de Auditoría'),
        (N'ES', N'ALERT_HINT', N'La verificacion de digitos verificadores encontro datos alterados por fuera del sistema. Avise al administrador antes de operar.', N'La verificación de dígitos verificadores encontró datos alterados por fuera del sistema. Avise al administrador antes de operar.'),
        (N'ES', N'CC_PASS', N'Contrasena', N'Contraseña'),
        (N'ES', N'CC_PASS2', N'Repetir contrasena', N'Repetir contraseña'),
        (N'ES', N'MSG_MONTO_INVALIDO', N'El monto no es un numero valido.', N'El monto no es un número válido.'),
        (N'ES', N'MSG_RES_SALON', N'Seleccione un salon valido.', N'Seleccione un salón válido.'),
        (N'ES', N'LOGIN_TAGLINE', N'Gestion de eventos y reservas', N'Gestión de eventos y reservas'),
        (N'ES', N'LOGIN_COMPLETAR', N'Completar usuario y contrasena.', N'Completar usuario y contraseña.'),
        (N'ES', N'CC_MSG_NO_COINCIDEN', N'Las contrasenas no coinciden.', N'Las contraseñas no coinciden.'),
        (N'ES', N'CC_MSG_PASS_CORTA', N'La contrasena debe tener al menos 4 caracteres.', N'La contraseña debe tener al menos 4 caracteres.'),
        (N'ES', N'CC_MSG_OK', N'Usuario creado. Ya podes iniciar sesion.', N'Usuario creado. Ya podés iniciar sesión.'),
        (N'ES', N'CC_MSG_USER_INVALIDO', N'Usuario invalido (3-50, letras/numeros/._-).', N'Usuario inválido (3-50, letras/números/._-).'),
        (N'ES', N'CC_MSG_PASS_INVALIDA', N'Contrasena invalida.', N'Contraseña inválida.'),
        (N'ES', N'PERF_DESC', N'Descripcion', N'Descripción'),
        (N'ES', N'LOGIN_INACTIVA', N'La cuenta esta inactiva. Contactate con un administrador.', N'La cuenta está inactiva. Contactate con un administrador.'),
        (N'ES', N'COL_TELEFONO', N'Telefono', N'Teléfono'),
        (N'ES', N'MSG_CLI_EMAIL', N'El email no es valido.', N'El email no es válido.'),
        (N'ES', N'MSG_RES_SALON_OCUPADO', N'El salon ya esta reservado para esa fecha.', N'El salón ya está reservado para esa fecha.'),
        (N'ES', N'SRV_TITULO', N'Gestion de Servicios', N'Gestión de Servicios'),
        (N'ES', N'CLI_TITULO', N'Gestion de Clientes', N'Gestión de Clientes'),
        (N'ES', N'COL_DESCRIPCION', N'Descripcion', N'Descripción'),
        (N'ES', N'COL_METODO', N'Metodo', N'Método'),
        (N'ES', N'COL_OBSERVACION', N'Observacion', N'Observación'),
        (N'ES', N'MSG_PAGO_MONTO', N'Ingrese un monto valido.', N'Ingrese un monto válido.'),
        (N'ES', N'MSG_PAGO_METODO', N'Seleccione un metodo de pago.', N'Seleccione un método de pago.'),
        (N'ES', N'MSG_PAGO_RESERVA', N'Reserva invalida.', N'Reserva inválida.'),
        (N'ES', N'CMP_TAGLINE', N'GESTION DE EVENTOS', N'GESTIÓN DE EVENTOS'),
        (N'ES', N'MSG_EMAIL_ADJUNTAR', N'Se abrio tu correo con el mensaje listo. Adjunta el comprobante (abrimos su carpeta) y envialo.', N'Se abrió tu correo con el mensaje listo. Adjunta el comprobante (abrimos su carpeta) y envialo.'),
        (N'ES', N'EST_COTIZACION', N'Cotizacion', N'Cotización'),
        (N'ES', N'CRIT_INFO', N'Informacion', N'Información'),
        (N'ES', N'ACC_LOGOUT', N'Cierre de sesion', N'Cierre de sesión'),
        (N'ES', N'AUD_TAB_BITACORA', N'Bitacora general', N'Bitácora general'),
        (N'ES', N'AUD_TAB_LOGIN', N'Auditoria de login', N'Auditoría de login'),
        (N'ES', N'AUD_RECALC_BTN', N'Recalcular linea base', N'Recalcular línea base'),
        (N'ES', N'AUD_RECALC_CONFIRMA', N'Recalcular los digitos verificadores de todas las reservas? Usar despues de corregir datos alterados: la linea base nueva pasa a ser la referencia de integridad.', N'¿Recalcular los dígitos verificadores de todas las reservas? Usar después de corregir datos alterados: la línea base nueva pasa a ser la referencia de integridad.'),
        (N'ES', N'AUD_RECALC_OK', N'Linea base recalculada ({0} reservas). Verificacion posterior: {1} inconsistencia(s).', N'Línea base recalculada ({0} reservas). Verificación posterior: {1} inconsistencia(s).'),
        (N'ES', N'MSG_PERF_CICLO', N'No se puede incluir ese perfil: generaria una referencia circular.', N'No se puede incluir ese perfil: generaría una referencia circular.'),
        (N'ES', N'VER_VACIO', N'Sin versiones guardadas. Se crea una automaticamente al modificar la reserva.', N'Sin versiones guardadas. Se crea una automáticamente al modificar la reserva.'),
        (N'ES', N'VER_CONFIRMA', N'Restaurar la reserva al estado de la version seleccionada? El estado actual se guardara como una nueva version.', N'¿Restaurar la reserva al estado de la versión seleccionada? El estado actual se guardará como una nueva versión.'),
        (N'ES', N'MSG_VER_OK', N'Version restaurada.', N'Versión restaurada.'),
        (N'ES', N'MSG_SIN_PERMISO', N'No tenes permiso para realizar esta accion.', N'No tenés permiso para realizar esta acción.'),
        (N'ES', N'MAIN_PERMISOS_ERROR', N'No se pudieron cargar los permisos de tu perfil, asi que la sesion quedo sin acceso a las secciones. Volve a iniciar sesion; si el problema sigue, avisale a un administrador.', N'No se pudieron cargar los permisos de tu perfil, así que la sesión quedó sin acceso a las secciones. Volvé a iniciar sesión; si el problema sigue, avisale a un administrador.'),
        (N'ES', N'MSG_RES_VENCIDA', N'La operacion vencio: renovala antes de confirmarla.', N'La operación venció: renovala antes de cambiar su estado.'),
        (N'ES', N'MSG_RES_CANCELAR', N'Cancelar la reserva #{0}?', N'¿Cancelar la reserva #{0}?'),
        (N'ES', N'MSG_RES_NO_MODIFICABLE', N'La reserva esta cancelada: no admite modificaciones.', N'La reserva está cancelada: no admite modificaciones.'),
        (N'ES', N'MSG_PAGO_ANULAR_CONF', N'Anular el pago de {0}? La operacion no se puede deshacer.', N'¿Anular el pago de {0}? La operación no se puede deshacer.'),
        (N'ES', N'CONN_TITULO', N'Configuracion de conexion', N'Configuración de conexión'),
        (N'ES', N'CONN_AYUDA', N'No se pudo conectar a la base de datos. Indica donde esta la instancia de SQL Server y el nombre de la base. La configuracion se guarda cifrada en tu perfil de Windows.', N'No se pudo conectar a la base de datos. Indica dónde está la instancia de SQL Server y el nombre de la base. La configuración se guarda cifrada en tu perfil de Windows.'),
        (N'ES', N'CONN_PROBANDO', N'Probando conexion...', N'Probando conexión...'),
        (N'ES', N'CONN_OK', N'Conexion correcta.', N'Conexión correcta.'),
        (N'ES', N'DISP_COL_PROPUESTA', N'Proxima fecha libre', N'Próxima fecha libre'),
        (N'ES', N'DISP_RESUMEN_OK', N'{0} salon(es) disponible(s) para la fecha consultada.', N'{0} salón(es) disponible(s) para la fecha consultada.'),
        (N'ES', N'DISP_RESUMEN_ALTERNATIVAS', N'Ningun salon disponible para esa fecha: se proponen fechas alternativas.', N'Ningún salón disponible para esa fecha: se proponen fechas alternativas.'),
        (N'ES', N'DISP_SELECCIONE', N'Seleccione un salon de la grilla.', N'Seleccione un salón de la grilla.'),
        (N'ES', N'DISP_SIN_PROPUESTA', N'El salon no tiene fechas libres en el horizonte consultado.', N'El salón no tiene fechas libres en el horizonte consultado.'),
        (N'ES', N'CMP_PRESUPUESTO_NOTA', N'Presupuesto sin compromiso de reserva. Sujeto a disponibilidad del salon al momento de confirmar.', N'Presupuesto sin compromiso de reserva. Sujeto a disponibilidad del salón al momento de confirmar.'),
        (N'ES', N'MSG_RES_CAPACIDAD', N'El salon no alcanza para la cantidad de invitados indicada.', N'El salón no alcanza para la cantidad de invitados indicada.'),
        (N'ES', N'MSG_RES_TRANSICION_GEN', N'El cambio de estado solicitado no esta admitido.', N'El cambio de estado solicitado no está admitido.'),
        (N'ES', N'MSG_RES_GUARDAR_PRIMERO', N'Guarde la reserva antes de emitir su documentacion.', N'Guarde la reserva antes de emitir su documentación.'),
        (N'ES', N'MSG_OP_ERROR', N'No se pudo completar la operacion.', N'No se pudo completar la operación.'),
        (N'EN', N'MSG_RES_VENCIDA', N'The quote expired: renew it before confirming.', N'The operation expired: renew it before changing its status.'),
        (N'PT', N'MENU_INICIO', N'Inicio', N'Início'),
        (N'PT', N'LOGIN_USER', N'Usuario', N'Usuário'),
        (N'PT', N'LOGIN_CREATE', N'Nao tem conta? Criar', N'Não tem conta? Criar'),
        (N'PT', N'MAIN_SESSION', N'Sessao iniciada por:', N'Sessão iniciada por:'),
        (N'PT', N'MAIN_SUBTITLE', N'Use o menu a esquerda para gerenciar o sistema.', N'Use o menu à esquerda para gerenciar o sistema.'),
        (N'PT', N'MAIN_SIN_ROL', N'Sua conta ainda nao tem um perfil atribuido. Entre em contato com um administrador para receber um.', N'Sua conta ainda não tem um perfil atribuído. Entre em contato com um administrador para receber um.'),
        (N'PT', N'COL_SALON', N'Salao', N'Salão'),
        (N'PT', N'COL_USUARIO', N'Usuario', N'Usuário'),
        (N'PT', N'COL_MODULO', N'Modulo', N'Módulo'),
        (N'PT', N'COL_ACCION', N'Acao', N'Ação'),
        (N'PT', N'COL_MAQUINA', N'Maquina', N'Máquina'),
        (N'PT', N'RES_TITULO', N'Gestao de Reservas', N'Gestão de Reservas'),
        (N'PT', N'RES_HISTORIAL', N'Historico', N'Histórico'),
        (N'PT', N'BIT_HASTA', N'Ate', N'Até'),
        (N'PT', N'PERF_TITULO', N'Gestao de Perfis', N'Gestão de Perfis'),
        (N'PT', N'PERF_GUARDAR', N'Salvar permissoes', N'Salvar permissões'),
        (N'PT', N'MSG_PERF_OK', N'Permissoes salvas.', N'Permissões salvas.'),
        (N'PT', N'IDI_TITULO', N'Gestao de Idiomas', N'Gestão de Idiomas'),
        (N'PT', N'IDI_CODIGO', N'Codigo (ex. PT)', N'Código (ex. PT)'),
        (N'PT', N'IDI_GUARDAR', N'Salvar traducoes', N'Salvar traduções'),
        (N'PT', N'MSG_IDI_GUARDADO', N'Traducoes salvas.', N'Traduções salvas.'),
        (N'PT', N'MSG_IDI_COD_INV', N'Codigo invalido (1 a 5 caracteres).', N'Código inválido (1 a 5 caracteres).'),
        (N'PT', N'MSG_IDI_DUP', N'Ja existe um idioma com esse codigo.', N'Já existe um idioma com esse código.'),
        (N'PT', N'MSG_IDI_ERROR', N'Nao foi possivel criar o idioma.', N'Não foi possível criar o idioma.'),
        (N'PT', N'HIST_TITULO', N'Historico da reserva', N'Histórico da reserva'),
        (N'PT', N'ALERT_HINT', N'A verificacao de digitos verificadores encontrou dados alterados fora do sistema. Avise o administrador antes de operar.', N'A verificação de dígitos verificadores encontrou dados alterados fora do sistema. Avise o administrador antes de operar.'),
        (N'PT', N'CC_USER', N'Usuario', N'Usuário'),
        (N'PT', N'MSG_MONTO_INVALIDO', N'O valor nao e um numero valido.', N'O valor não é um número válido.'),
        (N'PT', N'MSG_RES_SALON', N'Selecione um salao valido.', N'Selecione um salão válido.'),
        (N'PT', N'MSG_RES_FECHA', N'A data do evento nao pode ser anterior a hoje.', N'A data do evento não pode ser anterior a hoje.'),
        (N'PT', N'MSG_RES_MONTO', N'O valor nao pode ser negativo.', N'O valor não pode ser negativo.'),
        (N'PT', N'MSG_RES_NOTFOUND', N'A reserva nao existe mais.', N'A reserva não existe mais.'),
        (N'PT', N'MSG_RES_ERROR', N'Nao foi possivel salvar a reserva.', N'Não foi possível salvar a reserva.'),
        (N'PT', N'MSG_RES_SELECCIONE', N'Selecione uma reserva existente para ver seu historico.', N'Selecione uma reserva existente para ver seu histórico.'),
        (N'PT', N'LOGIN_TAGLINE', N'Gestao de eventos e reservas', N'Gestão de eventos e reservas'),
        (N'PT', N'LOGIN_COMPLETAR', N'Preencha usuario e senha.', N'Preencha usuário e senha.'),
        (N'PT', N'CC_MSG_NO_COINCIDEN', N'As senhas nao coincidem.', N'As senhas não coincidem.'),
        (N'PT', N'CC_MSG_OK', N'Conta criada. Voce ja pode entrar.', N'Conta criada. Você já pode entrar.'),
        (N'PT', N'CC_MSG_USER_INVALIDO', N'Usuario invalido (3-50, letras/numeros/._-).', N'Usuário inválido (3-50, letras/números/._-).'),
        (N'PT', N'CC_MSG_USER_EXISTE', N'Esse usuario ja existe.', N'Esse usuário já existe.'),
        (N'PT', N'CC_MSG_PASS_INVALIDA', N'Senha invalida.', N'Senha inválida.'),
        (N'PT', N'HIST_VACIO', N'Sem alteracoes registradas.', N'Sem alterações registradas.'),
        (N'PT', N'PERF_DESC', N'Descricao', N'Descrição'),
        (N'PT', N'PERF_ASIGNAR_TITULO', N'Atribuir perfil a usuarios', N'Atribuir perfil a usuários'),
        (N'PT', N'PERF_GUARDAR_ASIG', N'Salvar atribuicoes', N'Salvar atribuições'),
        (N'PT', N'MSG_PERF_ASIG_OK', N'Atribuicoes salvas.', N'Atribuições salvas.'),
        (N'PT', N'MSG_PERF_DUP', N'Ja existe um perfil com esse nome.', N'Já existe um perfil com esse nome.'),
        (N'PT', N'LOGIN_INACTIVA', N'A conta esta inativa. Entre em contato com um administrador.', N'A conta está inativa. Entre em contato com um administrador.'),
        (N'PT', N'MSG_PERF_DESBLOQ', N'Usuario desbloqueado.', N'Usuário desbloqueado.'),
        (N'PT', N'CLI_TITULO', N'Gestao de Clientes', N'Gestão de Clientes'),
        (N'PT', N'MSG_CLI_DNI_DUP', N'Ja existe um cliente com esse documento.', N'Já existe um cliente com esse documento.'),
        (N'PT', N'MSG_CLI_EMAIL', N'O email nao e valido.', N'O email não é válido.'),
        (N'PT', N'MSG_RES_SALON_OCUPADO', N'O salao ja esta reservado para essa data.', N'O salão já está reservado para essa data.'),
        (N'PT', N'MENU_SERVICIOS', N'Servicos', N'Serviços'),
        (N'PT', N'SRV_TITULO', N'Gestao de Servicos', N'Gestão de Serviços'),
        (N'PT', N'SRV_NUEVO', N'Novo servico', N'Novo serviço'),
        (N'PT', N'SRV_FORM_EDITAR', N'Editar servico', N'Editar serviço'),
        (N'PT', N'SRV_COUNT', N'servicos', N'serviços'),
        (N'PT', N'COL_DESCRIPCION', N'Descricao', N'Descrição'),
        (N'PT', N'COL_PRECIO', N'Preco', N'Preço'),
        (N'PT', N'MSG_SRV_NOMBRE', N'Informe o nome do servico.', N'Informe o nome do serviço.'),
        (N'PT', N'MSG_SRV_PRECIO', N'O preco nao pode ser negativo.', N'O preço não pode ser negativo.'),
        (N'PT', N'MSG_SRV_DUP', N'Ja existe um servico com esse nome.', N'Já existe um serviço com esse nome.'),
        (N'PT', N'MSG_SRV_OK', N'Servico salvo.', N'Serviço salvo.'),
        (N'PT', N'RES_SERVICIOS', N'Servicos da reserva', N'Serviços da reserva'),
        (N'PT', N'COL_SERVICIO', N'Servico', N'Serviço'),
        (N'PT', N'COL_METODO', N'Metodo', N'Método'),
        (N'PT', N'COL_OBSERVACION', N'Observacao', N'Observação'),
        (N'PT', N'MSG_PAGO_MONTO', N'Informe um valor valido.', N'Informe um valor válido.'),
        (N'PT', N'MSG_PAGO_METODO', N'Selecione um metodo de pagamento.', N'Selecione um método de pagamento.'),
        (N'PT', N'MSG_PAGO_RESERVA', N'Reserva invalida.', N'Reserva inválida.'),
        (N'PT', N'CMP_TAGLINE', N'GESTAO DE EVENTOS', N'GESTÃO DE EVENTOS'),
        (N'PT', N'CMP_DETALLE_SERVICIOS', N'Detalhe de servicos', N'Detalhe de serviços'),
        (N'PT', N'CMP_SIN_SERVICIOS', N'Sem servicos contratados.', N'Sem serviços contratados.'),
        (N'PT', N'EMAIL_SALUDO', N'Ola {0},', N'Olá {0},'),
        (N'PT', N'EMAIL_CIERRE', N'O comprovante esta anexado. Saudacoes, EvenTech.', N'O comprovante está anexado. Saudações, EvenTech.'),
        (N'PT', N'MSG_EMAIL_SIN_CORREO', N'O cliente nao tem email cadastrado.', N'O cliente não tem email cadastrado.'),
        (N'PT', N'EST_COTIZACION', N'Orcamento', N'Orçamento'),
        (N'PT', N'CRIT_INFO', N'Informacao', N'Informação'),
        (N'PT', N'ACC_LOGOUT', N'Encerramento de sessao', N'Fim de sessão'),
        (N'PT', N'AUD_RECALC_CONFIRMA', N'Recalcular os digitos verificadores de todas as reservas? Usar apos corrigir dados alterados: a nova linha de base passa a ser a referencia de integridade.', N'Recalcular os dígitos verificadores de todas as reservas? Usar após corrigir dados alterados: a nova linha de base passa a ser a referência de integridade.'),
        (N'PT', N'AUD_RECALC_OK', N'Linha de base recalculada ({0} reservas). Verificacao posterior: {1} inconsistencia(s).', N'Linha de base recalculada ({0} reservas). Verificação posterior: {1} inconsistência(s).'),
        (N'PT', N'PERF_INCLUIDOS', N'Perfis incluidos', N'Perfis incluídos'),
        (N'PT', N'MSG_PERF_CICLO', N'Nao e possivel incluir esse perfil: geraria uma referencia circular.', N'Não é possível incluir esse perfil: geraria uma referência circular.'),
        (N'PT', N'RES_VERSIONES', N'Versoes', N'Versões'),
        (N'PT', N'VER_TITULO', N'Versoes da reserva', N'Versões da reserva'),
        (N'PT', N'VER_VACIO', N'Sem versoes salvas. Uma e criada automaticamente ao modificar a reserva.', N'Sem versões salvas. Uma é criada automaticamente ao modificar a reserva.'),
        (N'PT', N'VER_CONFIRMA', N'Restaurar a reserva ao estado da versao selecionada? O estado atual sera salvo como uma nova versao.', N'Restaurar a reserva ao estado da versão selecionada? O estado atual será salvo como uma nova versão.'),
        (N'PT', N'MSG_VER_OK', N'Versao restaurada.', N'Versão restaurada.'),
        (N'PT', N'MSG_SIN_PERMISO', N'Voce nao tem permissao para realizar esta acao.', N'Você não tem permissão para realizar esta ação.'),
        (N'PT', N'MAIN_PERMISOS_ERROR', N'Nao foi possivel carregar as permissoes do seu perfil, entao a sessao ficou sem acesso as secoes. Entre novamente; se o problema continuar, avise um administrador.', N'Não foi possível carregar as permissões do seu perfil, então a sessão ficou sem acesso às seções. Entre novamente; se o problema continuar, avise um administrador.'),
        (N'PT', N'MSG_RES_VENCIDA', N'A operacao venceu: renove antes de confirmar.', N'A operação venceu: renove-a antes de mudar seu estado.'),
        (N'PT', N'MSG_RES_RENOVADA', N'Vigencia renovada.', N'Vigência renovada.'),
        (N'PT', N'MSG_RES_NO_MODIFICABLE', N'A reserva esta cancelada: nao admite modificacoes.', N'A reserva está cancelada: não admite modificações.'),
        (N'PT', N'MSG_PAGO_ANULAR_CONF', N'Anular o pagamento de {0}? A operacao nao pode ser desfeita.', N'Anular o pagamento de {0}? A operação não pode ser desfeita.'),
        (N'PT', N'CONN_TITULO', N'Configuracao de conexao', N'Configuração de conexão'),
        (N'PT', N'CONN_AYUDA', N'Nao foi possivel conectar ao banco de dados. Informe a instancia do SQL Server e o nome do banco. A configuracao e salva criptografada no seu perfil do Windows.', N'Não foi possível conectar ao banco de dados. Informe a instância do SQL Server e o nome do banco. A configuração é salva criptografada no seu perfil do Windows.'),
        (N'PT', N'CONN_SERVIDOR', N'Instancia do SQL Server', N'Instância do SQL Server'),
        (N'PT', N'CONN_PROBANDO', N'Testando conexao...', N'Testando conexão...'),
        (N'PT', N'CONN_OK', N'Conexao correta.', N'Conexão correta.'),
        (N'PT', N'DISP_COL_PROPUESTA', N'Proxima data livre', N'Próxima data livre'),
        (N'PT', N'DISP_EST_DISPONIBLE', N'Disponivel', N'Disponível'),
        (N'PT', N'DISP_RESUMEN_OK', N'{0} salao(oes) disponivel(is) para a data consultada.', N'{0} salão(ões) disponível(is) para a data consultada.'),
        (N'PT', N'DISP_RESUMEN_ALTERNATIVAS', N'Nenhum salao disponivel para essa data: datas alternativas sao propostas.', N'Nenhum salão disponível para essa data: datas alternativas são propostas.'),
        (N'PT', N'DISP_SELECCIONE', N'Selecione um salao da grade.', N'Selecione um salão da grade.'),
        (N'PT', N'DISP_SIN_PROPUESTA', N'O salao nao tem datas livres no periodo consultado.', N'O salão não tem datas livres no período consultado.'),
        (N'PT', N'CMP_TITULO_PRESUPUESTO', N'Orcamento', N'Orçamento'),
        (N'PT', N'CMP_DOC_NRO_PRESUPUESTO', N'Orcamento N', N'Orçamento N'),
        (N'PT', N'CMP_PRESUPUESTO_NOTA', N'Orcamento sem compromisso de reserva. Sujeito a disponibilidade do salao no momento da confirmacao.', N'Orçamento sem compromisso de reserva. Sujeito à disponibilidade do salão no momento da confirmação.'),
        (N'PT', N'MSG_RES_TRANSICION', N'Nao e admitido passar de {0} para {1}.', N'Não é admitido passar de {0} para {1}.'),
        (N'PT', N'MSG_RES_MONTO_PAGADO', N'O total da reserva nao pode ficar abaixo do valor ja cobrado.', N'O total da reserva não pode ficar abaixo do valor já cobrado.'),
        (N'PT', N'MSG_RES_CAPACIDAD', N'O salao nao comporta a quantidade de convidados informada.', N'O salão não comporta a quantidade de convidados informada.'),
        (N'PT', N'MSG_RES_TRANSICION_GEN', N'A mudanca de estado solicitada nao e admitida.', N'A mudança de estado solicitada não é admitida.'),
        (N'PT', N'MSG_RES_SIN_ADELANTO', N'Para confirmar a reserva e preciso registrar o adiantamento: salve-a e cobre o pagamento em Pagamentos.', N'Para confirmar a reserva é preciso registrar o adiantamento: salve-a e cobre o pagamento em Pagamentos.'),
        (N'PT', N'MSG_PAGO_NO_ANULABLE', N'O pagamento nao existe mais ou nao pertence a esta reserva.', N'O pagamento não existe mais ou não pertence a esta reserva.'),
        (N'PT', N'MSG_RES_GUARDAR_PRIMERO', N'Salve a reserva antes de emitir sua documentacao.', N'Salve a reserva antes de emitir sua documentação.'),
        (N'PT', N'MSG_OP_ERROR', N'Nao foi possivel concluir a operacao.', N'Não foi possível concluir a operação.'),
        (N'PT', N'MSG_RES_CANCELAR_DETALLE', N'Cancelando hoje sao retidos {0:N2} e reembolsados {1:N2}.', N'Cancelando hoje são retidos {0:N2} e reembolsados {1:N2}.')
    ) AS v(Codigo, Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
JOIN Fix f ON f.Codigo = i.Codigo AND f.Clave = t.Clave
WHERE t.Texto = f.Anterior;

UPDATE dbo.Idiomas SET Nombre = N'Español'   WHERE Codigo = N'ES' AND Nombre = N'Espanol';
UPDATE dbo.Idiomas SET Nombre = N'Português' WHERE Codigo = N'PT' AND Nombre = N'Portugues';
GO

-- Claves que ninguna pantalla consume: se retiraron de las semillas y se
-- quitan de las bases existentes (en todos los idiomas) para que el editor de
-- traducciones no ofrezca textos que nunca se muestran.
DELETE FROM dbo.Traducciones
 WHERE Clave IN (N'MAIN_USER', N'BTN_REFRESCAR', N'MSG_PERF_CREADO', N'MSG_CLI_SELECCIONE',
                 N'MSG_RES_SERVICIOS_ERROR', N'LOGIN_ERR_USUARIO', N'LOGIN_ERR_PASS',
                 N'LOGIN_ERR_CONEXION', N'CC_MSG_ERROR');
GO

-- ===========================================================================
-- Integridad del modelo de datos. Idempotente; va al final porque todas las
-- tablas ya existen en este punto.
-- ===========================================================================

-- Dominio de Estado en Reservas y en su foto Memento (tabla de estados de
-- G02). Se agrega solo si no hay filas fuera del dominio: el script no debe
-- romper una base real, y va despues de la regularizacion de VenceEl.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Reservas_Estado' AND parent_object_id = OBJECT_ID('dbo.Reservas'))
   AND NOT EXISTS (SELECT 1 FROM dbo.Reservas WHERE Estado NOT IN ('COTIZACION', 'PENDIENTE', 'CONFIRMADA', 'CANCELADA'))
    ALTER TABLE dbo.Reservas WITH CHECK ADD CONSTRAINT CK_Reservas_Estado
        CHECK (Estado IN ('COTIZACION', 'PENDIENTE', 'CONFIRMADA', 'CANCELADA'));
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_ReservaMemento_Estado' AND parent_object_id = OBJECT_ID('dbo.ReservaMemento'))
   AND NOT EXISTS (SELECT 1 FROM dbo.ReservaMemento WHERE Estado NOT IN ('COTIZACION', 'PENDIENTE', 'CONFIRMADA', 'CANCELADA'))
    ALTER TABLE dbo.ReservaMemento WITH CHECK ADD CONSTRAINT CK_ReservaMemento_Estado
        CHECK (Estado IN ('COTIZACION', 'PENDIENTE', 'CONFIRMADA', 'CANCELADA'));
GO

-- Indices de apoyo para las claves foraneas que no eran columna inicial de
-- ningun indice (consultas por cliente, servicio, metodo de pago y perfil).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservas_ClienteId' AND object_id = OBJECT_ID('dbo.Reservas'))
    CREATE INDEX IX_Reservas_ClienteId ON dbo.Reservas(ClienteId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Pagos_MetodoPagoId' AND object_id = OBJECT_ID('dbo.Pagos'))
    CREATE INDEX IX_Pagos_MetodoPagoId ON dbo.Pagos(MetodoPagoId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReservaServicio_ServicioId' AND object_id = OBJECT_ID('dbo.ReservaServicio'))
    CREATE INDEX IX_ReservaServicio_ServicioId ON dbo.ReservaServicio(ServicioId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ReservaMementoServicio_ServicioId' AND object_id = OBJECT_ID('dbo.ReservaMementoServicio'))
    CREATE INDEX IX_ReservaMementoServicio_ServicioId ON dbo.ReservaMementoServicio(ServicioId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_PerfilId' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE INDEX IX_Users_PerfilId ON dbo.Users(PerfilId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permisos_PermisoPadreId' AND object_id = OBJECT_ID('dbo.Permisos'))
    CREATE INDEX IX_Permisos_PermisoPadreId ON dbo.Permisos(PermisoPadreId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PerfilPermiso_PermisoId' AND object_id = OBJECT_ID('dbo.PerfilPermiso'))
    CREATE INDEX IX_PerfilPermiso_PermisoId ON dbo.PerfilPermiso(PermisoId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PerfilIncluido_PerfilHijoId' AND object_id = OBJECT_ID('dbo.PerfilIncluido'))
    CREATE INDEX IX_PerfilIncluido_PerfilHijoId ON dbo.PerfilIncluido(PerfilHijoId);
GO

-- Los permisos efectivos se resuelven al iniciar la sesion: el mensaje de
-- guardado avisa que un cambio rige desde el proximo ingreso (vigencia de la
-- autorizacion, G05 Seguridad). Guardado por el texto anterior de fabrica.
UPDATE t SET Texto = CASE t.Clave
        WHEN N'MSG_PERF_OK' THEN CASE i.Codigo
            WHEN N'EN' THEN N'Permissions saved. Changes apply from the next sign-in.'
            WHEN N'PT' THEN N'Permissões salvas. As alterações valem a partir do próximo início de sessão.'
            ELSE N'Permisos guardados. Los cambios rigen desde el próximo inicio de sesión.' END
        ELSE CASE i.Codigo
            WHEN N'EN' THEN N'Assignments saved. Changes apply from the next sign-in.'
            WHEN N'PT' THEN N'Atribuições salvas. As alterações valem a partir do próximo início de sessão.'
            ELSE N'Asignaciones guardadas. Los cambios rigen desde el próximo inicio de sesión.' END END
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
WHERE t.Clave IN (N'MSG_PERF_OK', N'MSG_PERF_ASIG_OK')
  AND t.Texto IN (N'Permisos guardados.', N'Permissions saved.', N'Permissões salvas.', N'Permissoes salvas.',
                  N'Asignaciones guardadas.', N'Assignments saved.', N'Atribuições salvas.', N'Atribuicoes salvas.');
GO

-- ===========================================================================
-- QA-12/09/2026 F01
-- ===========================================================================
-- ===========================================================================
-- schema_add.sql - paquete F01 (perfiles, usuarios y permisos).
-- Idempotente. Corre sobre la base que indica -d, despues de db/schema.sql
-- (necesita las tablas, los permisos y los perfiles ya sembrados).
--   1. Perfiles operativos: la composicion de fabrica se siembra solo en el
--      perfil que se da de alta (BD-04). REEMPLAZA el bloque "Perfiles
--      operativos" de schema.sql, que hay que quitar: mientras siga ahi, cada
--      corrida repone lo que el administrador quito.
--   2. Cuenta inicial sin perfil y sin nadie que gestione perfiles (BD-13).
--   3. Traducciones nuevas: avisos de Gestion de Perfiles y nombres del arbol de
--      permisos (PERM_<Clave> para las hojas, PERMG_<NOMBRE> para los grupos).
-- Guardado en UTF-8 con BOM (tildes y enie para sqlcmd).
-- ===========================================================================
-- ===========================================================================
-- 1. Perfiles operativos (roles de G04) sobre el Composite de dos niveles:
--   Vendedor   : opera la venta (disponibilidad, clientes, reservas, cobros).
--   Supervisor : incluye a Vendedor y suma auditoria y anulacion de pagos (y, con
--                el Proceso 2, la supervision de la ejecucion de los eventos).
--   Gerencial  : incluye a Supervisor y suma el recalculo de la linea base.
--   Restaurar versiones queda reservado al Administrador (RN-05 de la Carpeta).
-- Idempotente y guardado por nombre: el perfil que falte se da de alta y SOLO ese
-- perfil recibe su composicion de fabrica (permisos directos e inclusion). Un
-- perfil que ya existe no se toca, asi que lo que un administrador le haya
-- quitado o agregado desde Gestion de Perfiles se conserva al volver a correr el
-- script (antes cada corrida reponia todo par perfil/permiso faltante y devolvia
-- lo revocado). Administrador no se toca aca: tiene su bloque de acceso total.
-- Alta y composicion van en una transaccion: un perfil no queda creado sin su
-- composicion si la corrida se corta en el medio.
-- ===========================================================================
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @PerfilesSembrados TABLE (
        Accion NVARCHAR(10) COLLATE DATABASE_DEFAULT NOT NULL,
        Id     INT NOT NULL,
        Nombre NVARCHAR(80) COLLATE DATABASE_DEFAULT NOT NULL
    );

    ;WITH Perf(Nombre, Descripcion) AS (
        SELECT * FROM (VALUES
            (N'Vendedor',   N'Atiende la venta: disponibilidad, clientes, cotizaciones, reservas y cobros'),
            (N'Supervisor', N'Incluye al perfil Vendedor y suma la consulta de auditoría, la anulación de pagos y la supervisión de la ejecución de los eventos'),
            (N'Gerencial',  N'Incluye al perfil Supervisor y suma la corrección administrativa de la línea base de integridad')
        ) AS v(Nombre, Descripcion)
    )
    MERGE dbo.Perfiles AS p
    USING Perf AS s ON p.Nombre = s.Nombre
    WHEN NOT MATCHED THEN INSERT (Nombre, Descripcion) VALUES (s.Nombre, s.Descripcion)
    OUTPUT $action, inserted.Id, inserted.Nombre INTO @PerfilesSembrados (Accion, Id, Nombre);

    ;WITH Asig(Perfil, Clave) AS (
        SELECT * FROM (VALUES
            (N'Vendedor',   N'DISPONIBILIDAD_CONSULTAR'),
            (N'Vendedor',   N'CLIENTES_GESTION'),
            (N'Vendedor',   N'RESERVA_CREAR'),
            (N'Vendedor',   N'RESERVA_EDITAR'),
            (N'Vendedor',   N'RESERVA_HISTORIAL'),
            (N'Vendedor',   N'PAGOS_REGISTRAR'),
            (N'Supervisor', N'BITACORA_VER'),
            (N'Supervisor', N'AUDIT_LOGIN_VER'),
            (N'Supervisor', N'PAGOS_ANULAR'),
            -- Proceso 2. En una base nueva el permiso todavia no existe en este punto y
            -- lo asigna el bloque del Proceso 2; esta fila cubre al perfil que se vuelve
            -- a dar de alta sobre una base que ya lo tiene.
            (N'Supervisor', N'EJECUCION_SUPERVISAR'),
            (N'Gerencial',  N'INTEGRIDAD_RECALC')
        ) AS v(Perfil, Clave)
    )
    INSERT INTO dbo.PerfilPermiso (PerfilId, PermisoId)
    SELECT n.Id, pe.Id
    FROM Asig a
    JOIN @PerfilesSembrados n ON n.Nombre = a.Perfil AND n.Accion = N'INSERT'
    JOIN dbo.Permisos pe ON pe.Clave = a.Clave
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermiso pp
                      WHERE pp.PerfilId = n.Id AND pp.PermisoId = pe.Id);

    -- Composite: Supervisor contiene a Vendedor; Gerencial contiene a Supervisor
    -- (los ciclos los valida BLL_Perfil; estas dos filas no forman ninguno). La
    -- inclusion es parte de la composicion del perfil que incluye: se siembra solo
    -- si ese perfil se dio de alta en esta corrida.
    ;WITH Inc(Padre, Hijo) AS (
        SELECT * FROM (VALUES (N'Supervisor', N'Vendedor'), (N'Gerencial', N'Supervisor')) AS v(Padre, Hijo)
    )
    INSERT INTO dbo.PerfilIncluido (PerfilPadreId, PerfilHijoId)
    SELECT n.Id, hi.Id
    FROM Inc i
    JOIN @PerfilesSembrados n ON n.Nombre = i.Padre AND n.Accion = N'INSERT'
    JOIN dbo.Perfiles hi ON hi.Nombre = i.Hijo
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilIncluido x
                      WHERE x.PerfilPadreId = n.Id AND x.PerfilHijoId = hi.Id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO

-- ===========================================================================
-- 2. Cuenta inicial sin perfil. La siembra de admin la da de alta si falta, pero
-- el perfil solo se le asigna al sembrar la tabla de perfiles vacia: si la cuenta
-- se borro y el script la volvio a sembrar, quedaba sin permisos y sin nadie que
-- pudiera asignarle un perfil. Se le asigna Administrador solo en ese caso de
-- recuperacion: admin sin perfil y ningun usuario activo y no bloqueado que
-- resuelva PERFILES_GESTION por el Composite (asignado directo o por un grupo que
-- lo contiene, en su perfil o en los perfiles que este incluye). Si otra cuenta
-- gestiona los perfiles, la decision de dejar a admin sin perfil se conserva.
-- ===========================================================================
;WITH Ancestros(PermisoId, AncestroId, PadreId, Nivel) AS (
    SELECT Id, Id, PermisoPadreId, 0 FROM dbo.Permisos WHERE Clave = N'PERFILES_GESTION'
    UNION ALL
    SELECT a.PermisoId, pe.Id, pe.PermisoPadreId, a.Nivel + 1
    FROM Ancestros a
    JOIN dbo.Permisos pe ON pe.Id = a.PadreId
    WHERE a.Nivel < 32
),
Contenidos(PerfilId, IncluidoId, Nivel) AS (
    SELECT Id, Id, 0 FROM dbo.Perfiles
    UNION ALL
    SELECT c.PerfilId, i.PerfilHijoId, c.Nivel + 1
    FROM Contenidos c
    JOIN dbo.PerfilIncluido i ON i.PerfilPadreId = c.IncluidoId
    WHERE c.Nivel < 32
),
Gestores(PerfilId) AS (
    SELECT DISTINCT c.PerfilId
    FROM Contenidos c
    JOIN dbo.PerfilPermiso pp ON pp.PerfilId = c.IncluidoId
    JOIN Ancestros a ON a.AncestroId = pp.PermisoId
)
UPDATE u SET PerfilId = pa.Id
FROM dbo.Users u
JOIN dbo.Perfiles pa ON pa.Nombre = N'Administrador'
WHERE u.Username = N'admin'
  AND u.PerfilId IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Users x
                  JOIN Gestores g ON g.PerfilId = x.PerfilId
                  WHERE x.Activo = 1 AND x.Blocked = 0)
OPTION (MAXRECURSION 100);
GO

-- ===========================================================================
-- 3. Traducciones de Gestion de Perfiles (idempotente: solo inserta las que falten).
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Avisos
        (N'ES', N'MSG_PERF_SIN_GESTOR', N'No se puede guardar: ningún usuario activo quedaría con permiso para gestionar perfiles y desbloquear cuentas.'),
        (N'EN', N'MSG_PERF_SIN_GESTOR', N'Cannot save: no active user would keep permission to manage profiles and unblock accounts.'),
        (N'PT', N'MSG_PERF_SIN_GESTOR', N'Não é possível salvar: nenhum usuário ativo ficaria com permissão para gerenciar perfis e desbloquear contas.'),
        (N'ES', N'MSG_PERF_NO_CARGADO', N'No se pudo cargar la composición de este perfil. Vuelva a seleccionarlo antes de guardar.'),
        (N'EN', N'MSG_PERF_NO_CARGADO', N'The composition of this profile could not be loaded. Select it again before saving.'),
        (N'PT', N'MSG_PERF_NO_CARGADO', N'Não foi possível carregar a composição deste perfil. Selecione-o novamente antes de salvar.'),
        -- Grupos del arbol de permisos
        (N'ES', N'PERMG_ADMINISTRACION', N'Administración'),                       (N'EN', N'PERMG_ADMINISTRACION', N'Administration'),                     (N'PT', N'PERMG_ADMINISTRACION', N'Administração'),
        (N'ES', N'PERMG_GESTION_DE_RESERVAS', N'Gestión de Reservas'),             (N'EN', N'PERMG_GESTION_DE_RESERVAS', N'Reservations Management'),       (N'PT', N'PERMG_GESTION_DE_RESERVAS', N'Gestão de Reservas'),
        (N'ES', N'PERMG_AUDITORIA', N'Auditoría'),                                 (N'EN', N'PERMG_AUDITORIA', N'Audit'),                                   (N'PT', N'PERMG_AUDITORIA', N'Auditoria'),
        (N'ES', N'PERMG_ADMINISTRACION_DEL_SISTEMA', N'Administración del sistema'), (N'EN', N'PERMG_ADMINISTRACION_DEL_SISTEMA', N'System administration'), (N'PT', N'PERMG_ADMINISTRACION_DEL_SISTEMA', N'Administração do sistema'),
        (N'ES', N'PERMG_VENTAS', N'Ventas'),                                       (N'EN', N'PERMG_VENTAS', N'Sales'),                                      (N'PT', N'PERMG_VENTAS', N'Vendas'),
        -- Permisos (hojas)
        (N'ES', N'PERM_RESERVA_CREAR', N'Crear Reserva'),                          (N'EN', N'PERM_RESERVA_CREAR', N'Create Reservation'),                   (N'PT', N'PERM_RESERVA_CREAR', N'Criar Reserva'),
        (N'ES', N'PERM_RESERVA_EDITAR', N'Editar Reserva'),                        (N'EN', N'PERM_RESERVA_EDITAR', N'Edit Reservation'),                    (N'PT', N'PERM_RESERVA_EDITAR', N'Editar Reserva'),
        (N'ES', N'PERM_RESERVA_HISTORIAL', N'Ver Historial Reserva'),              (N'EN', N'PERM_RESERVA_HISTORIAL', N'View Reservation History'),         (N'PT', N'PERM_RESERVA_HISTORIAL', N'Ver Histórico da Reserva'),
        (N'ES', N'PERM_RESERVA_RESTAURAR', N'Restaurar Versión de Reserva'),       (N'EN', N'PERM_RESERVA_RESTAURAR', N'Restore Reservation Version'),      (N'PT', N'PERM_RESERVA_RESTAURAR', N'Restaurar Versão da Reserva'),
        (N'ES', N'PERM_DISPONIBILIDAD_CONSULTAR', N'Consultar Disponibilidad'),    (N'EN', N'PERM_DISPONIBILIDAD_CONSULTAR', N'Check Availability'),        (N'PT', N'PERM_DISPONIBILIDAD_CONSULTAR', N'Consultar Disponibilidade'),
        (N'ES', N'PERM_BITACORA_VER', N'Ver Bitácora'),                            (N'EN', N'PERM_BITACORA_VER', N'View Audit Log'),                        (N'PT', N'PERM_BITACORA_VER', N'Ver Registro'),
        (N'ES', N'PERM_AUDIT_LOGIN_VER', N'Ver Auditoría Login'),                  (N'EN', N'PERM_AUDIT_LOGIN_VER', N'View Login Audit'),                   (N'PT', N'PERM_AUDIT_LOGIN_VER', N'Ver Auditoria de Login'),
        (N'ES', N'PERM_INTEGRIDAD_RECALC', N'Recalcular línea base'),              (N'EN', N'PERM_INTEGRIDAD_RECALC', N'Recalculate baseline'),             (N'PT', N'PERM_INTEGRIDAD_RECALC', N'Recalcular linha de base'),
        (N'ES', N'PERM_CLIENTES_GESTION', N'Gestión de Clientes'),                 (N'EN', N'PERM_CLIENTES_GESTION', N'Clients Management'),                (N'PT', N'PERM_CLIENTES_GESTION', N'Gestão de Clientes'),
        (N'ES', N'PERM_SERVICIOS_GESTION', N'Gestión de Servicios'),               (N'EN', N'PERM_SERVICIOS_GESTION', N'Services Management'),              (N'PT', N'PERM_SERVICIOS_GESTION', N'Gestão de Serviços'),
        (N'ES', N'PERM_PERFILES_GESTION', N'Gestión de Perfiles'),                 (N'EN', N'PERM_PERFILES_GESTION', N'Profiles Management'),               (N'PT', N'PERM_PERFILES_GESTION', N'Gestão de Perfis'),
        (N'ES', N'PERM_IDIOMAS_GESTION', N'Gestión de Idiomas'),                   (N'EN', N'PERM_IDIOMAS_GESTION', N'Languages Management'),               (N'PT', N'PERM_IDIOMAS_GESTION', N'Gestão de Idiomas'),
        (N'ES', N'PERM_PAGOS_REGISTRAR', N'Registrar Pagos'),                      (N'EN', N'PERM_PAGOS_REGISTRAR', N'Register Payments'),                  (N'PT', N'PERM_PAGOS_REGISTRAR', N'Registrar Pagamentos'),
        (N'ES', N'PERM_PAGOS_ANULAR', N'Anular Pagos'),                            (N'EN', N'PERM_PAGOS_ANULAR', N'Void Payments'),                         (N'PT', N'PERM_PAGOS_ANULAR', N'Anular Pagamentos')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-12/09/2026 F02
-- ===========================================================================
-- EvenTech - Agregado al esquema del paquete F02 (Clientes).
--
-- Idempotente y con el mismo patron que db/schema.sql: corre sobre la base que
-- indica -d y solo inserta las claves que falten. Se integra al final del bloque
-- de traducciones de Clientes de schema.sql.
--
--   MSG_CLI_NOTFOUND     editar un cliente que ya no existe (antes se mostraba el
--                        texto de Reservas "La reserva ya no existe.").
--   MSG_CLI_DNI_INVALIDO el DNI no es un documento: letras o menos de 7 digitos
--                        (puntos, espacios y guiones se ignoran).
--   MSG_CLI_LARGO        un dato no entra en su columna (antes se guardaba
--                        recortado sin aviso).
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Clientes (Proceso 1): edicion de un cliente borrado y validaciones de DNI y largo
        (N'ES', N'MSG_CLI_NOTFOUND', N'El cliente ya no existe.'), (N'EN', N'MSG_CLI_NOTFOUND', N'The client no longer exists.'), (N'PT', N'MSG_CLI_NOTFOUND', N'O cliente não existe mais.'),
        (N'ES', N'MSG_CLI_DNI_INVALIDO', N'El DNI no es válido: use solo números (7 dígitos o más).'), (N'EN', N'MSG_CLI_DNI_INVALIDO', N'The ID is not valid: use digits only (7 or more).'), (N'PT', N'MSG_CLI_DNI_INVALIDO', N'O documento não é válido: use só números (7 dígitos ou mais).'),
        (N'ES', N'MSG_CLI_LARGO', N'Dato muy largo: nombre y apellido 60, email 120, teléfono 30.'), (N'EN', N'MSG_CLI_LARGO', N'Value too long: name and last name 60, email 120, phone 30.'), (N'PT', N'MSG_CLI_LARGO', N'Dado longo demais: nome e sobrenome 60, email 120, telefone 30.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-12/09/2026 F03
-- ===========================================================================
-- EvenTech - Agregado al esquema del paquete F03 (Servicios: catalogo y servicios de la reserva)
--
-- Idempotente, mismo criterio que db/schema.sql: corre SOBRE la base que indica
-- -d y solo inserta las claves que falten. Guardado en UTF-8 con BOM (tildes para
-- sqlcmd).

-- ===========================================================================
-- Tope del precio del catalogo. dbo.Servicios.Precio es DECIMAL(12,2): un precio
-- mayor a 9.999.999.999,99 no entra en la columna y el motor lo rechazaba con un
-- error de desborde que llegaba a la pantalla sin manejar. La capa de negocio lo
-- rechaza antes con este mensaje (ServicioResult_704ILR.PrecioExcedido_704ILR).
-- Idempotente: solo inserta las claves que falten.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_SRV_PRECIO_MAX', N'El precio no puede superar 9.999.999.999,99.'), (N'EN', N'MSG_SRV_PRECIO_MAX', N'The price cannot exceed 9,999,999,999.99.'), (N'PT', N'MSG_SRV_PRECIO_MAX', N'O preço não pode ultrapassar 9.999.999.999,99.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- ===========================================================================
-- QA-12/09/2026 F04
-- ===========================================================================
-- ===========================================================================
-- F04 - Ficha de reservas (ucReservas): textos nuevos de la interfaz.
-- Idempotente: solo inserta las claves que falten (mismo patron que db/schema.sql,
-- con GROUP BY i.Id, t.Clave para que una clave repetida no rompa UQ_Traducciones).
-- Guardado en UTF-8 con BOM: signos de apertura, tildes y cedilla para sqlcmd.
-- ===========================================================================
SET QUOTED_IDENTIFIER ON;
GO

;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- RN-01: pregunta propia para ofrecer la renovacion de la vigencia (antes se
        -- armaba con el rotulo BTN_RENOVAR y un "?" fijo, sin signo de apertura en ES)
        (N'ES', N'MSG_RES_RENOVAR_PREGUNTA', N'¿Renovar la vigencia?'), (N'EN', N'MSG_RES_RENOVAR_PREGUNTA', N'Renew the validity?'), (N'PT', N'MSG_RES_RENOVAR_PREGUNTA', N'Renovar a vigência?'),
        -- Lectura fallida de los servicios contratados al abrir una reserva: la ficha
        -- queda de solo lectura hasta volver a abrirla
        (N'ES', N'MSG_RES_SERVICIOS_NO_LEIDOS', N'No se pudieron leer los servicios contratados de la reserva: no se admite modificarla. Seleccione otra reserva y vuelva a abrirla.'),
        (N'EN', N'MSG_RES_SERVICIOS_NO_LEIDOS', N'The contracted services of the reservation could not be read: it cannot be modified. Select another reservation and open it again.'),
        (N'PT', N'MSG_RES_SERVICIOS_NO_LEIDOS', N'Não foi possível ler os serviços contratados da reserva: não é possível modificá-la. Selecione outra reserva e abra-a novamente.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- BTN_RENOVAR ya no la consume ninguna pantalla (la pregunta usa MSG_RES_RENOVAR_PREGUNTA):
-- se quita de las bases existentes, igual que las demas claves retiradas de las semillas.
DELETE FROM dbo.Traducciones WHERE Clave = N'BTN_RENOVAR';
GO

-- ===========================================================================
-- QA-12/09/2026 F05
-- ===========================================================================
-- ===========================================================================
-- Paquete F05 - Pagos: aviso de la RN-07 al anular un pago y nombres de los
-- metodos de pago (ortografia y traduccion). Idempotente: se puede correr
-- cualquier cantidad de veces, con sqlcmd ODBC o go-sqlcmd, sobre la base de -d.
-- Guardado como UTF-8 con BOM (tildes para sqlcmd).
-- ===========================================================================

-- Ortografia de los metodos de pago sembrados por versiones anteriores del
-- script. Se corrige solo el valor de fabrica exacto, y solo si el nombre
-- corregido no existe ya (UQ_MetodosPago_Nombre). Los pagos referencian el
-- metodo por Id, asi que ningun pago cambia; MetodosPago no participa de los
-- digitos verificadores.
UPDATE dbo.MetodosPago SET Nombre = N'Tarjeta de crédito'
 WHERE Nombre = N'Tarjeta de credito' COLLATE Latin1_General_CS_AS
   AND NOT EXISTS (SELECT 1 FROM dbo.MetodosPago x WHERE x.Nombre = N'Tarjeta de crédito' COLLATE Latin1_General_CS_AS);
UPDATE dbo.MetodosPago SET Nombre = N'Tarjeta de débito'
 WHERE Nombre = N'Tarjeta de debito' COLLATE Latin1_General_CS_AS
   AND NOT EXISTS (SELECT 1 FROM dbo.MetodosPago x WHERE x.Nombre = N'Tarjeta de débito' COLLATE Latin1_General_CS_AS);
GO

-- Textos nuevos. Solo inserta las claves que falten: una traduccion editada por
-- el usuario desde Gestion de Idiomas se conserva.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- RN-07 al anular: una reserva confirmada no puede quedar sin nada cobrado
        (N'ES', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'La reserva está confirmada: anular este pago la dejaría sin adelanto. Registre primero el pago que lo reemplaza o cancele la reserva.'),
        (N'EN', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'The reservation is confirmed: voiding this payment would leave it without a deposit. Record the replacement payment first or cancel the reservation.'),
        (N'PT', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'A reserva está confirmada: anular este pagamento a deixaria sem adiantamento. Registre primeiro o pagamento que o substitui ou cancele a reserva.'),
        -- Nombres de los metodos de pago: clave MP_ + nombre del catalogo en
        -- mayusculas y sin tildes (frmReservaPagos_704ILR.TextoMetodo_704ILR)
        (N'ES', N'MP_EFECTIVO', N'Efectivo'), (N'EN', N'MP_EFECTIVO', N'Cash'), (N'PT', N'MP_EFECTIVO', N'Dinheiro'),
        (N'ES', N'MP_TARJETA_DE_CREDITO', N'Tarjeta de crédito'), (N'EN', N'MP_TARJETA_DE_CREDITO', N'Credit card'), (N'PT', N'MP_TARJETA_DE_CREDITO', N'Cartão de crédito'),
        (N'ES', N'MP_TARJETA_DE_DEBITO', N'Tarjeta de débito'), (N'EN', N'MP_TARJETA_DE_DEBITO', N'Debit card'), (N'PT', N'MP_TARJETA_DE_DEBITO', N'Cartão de débito'),
        (N'ES', N'MP_TRANSFERENCIA', N'Transferencia'), (N'EN', N'MP_TRANSFERENCIA', N'Bank transfer'), (N'PT', N'MP_TRANSFERENCIA', N'Transferência'),
        (N'ES', N'MP_MERCADOPAGO', N'MercadoPago'), (N'EN', N'MP_MERCADOPAGO', N'MercadoPago'), (N'PT', N'MP_MERCADOPAGO', N'MercadoPago')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-12/09/2026 F06
-- ===========================================================================
-- ===========================================================================
-- Paquete F06 - Reglas de reserva en la BLL. Agregado para db/schema.sql.
-- Idempotente. Va al final del script, despues del bloque "Integridad del
-- modelo de datos" (el que crea CK_Reservas_Estado y CK_ReservaMemento_Estado)
-- y despues de las semillas de traducciones. Guardado en UTF-8 con BOM.
-- ===========================================================================
SET QUOTED_IDENTIFIER ON;
GO

-- Dominio de Estado sensible a mayusculas (Reservas). La intercalacion de la
-- base no distingue mayusculas, asi que el CHECK original admitia 'confirmada'
-- o 'Cotizacion': el motor los trata como estados validos y la aplicacion no.
-- La restriccion se reemplaza por una que compara en binario, solo si todas las
-- filas ya cumplen el dominio exacto: el script no debe romper una base real
-- (la lectura tolerante de la aplicacion y la verificacion de integridad
-- informan esas filas). No hace nada si la restriccion binaria ya existe.
IF NOT EXISTS (SELECT 1 FROM dbo.Reservas
               WHERE Estado COLLATE Latin1_General_BIN2 NOT IN (N'COTIZACION', N'PENDIENTE', N'CONFIRMADA', N'CANCELADA'))
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                   WHERE name = N'CK_Reservas_Estado' AND parent_object_id = OBJECT_ID(N'dbo.Reservas')
                     AND definition LIKE N'%Latin1_General_BIN2%')
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Reservas_Estado' AND parent_object_id = OBJECT_ID(N'dbo.Reservas'))
        ALTER TABLE dbo.Reservas DROP CONSTRAINT CK_Reservas_Estado;
    ALTER TABLE dbo.Reservas WITH CHECK ADD CONSTRAINT CK_Reservas_Estado
        CHECK (Estado COLLATE Latin1_General_BIN2 IN (N'COTIZACION', N'PENDIENTE', N'CONFIRMADA', N'CANCELADA'));
    COMMIT TRANSACTION;
    SET XACT_ABORT OFF;
END
GO

-- Mismo criterio para la foto Memento de la reserva.
IF NOT EXISTS (SELECT 1 FROM dbo.ReservaMemento
               WHERE Estado COLLATE Latin1_General_BIN2 NOT IN (N'COTIZACION', N'PENDIENTE', N'CONFIRMADA', N'CANCELADA'))
   AND NOT EXISTS (SELECT 1 FROM sys.check_constraints
                   WHERE name = N'CK_ReservaMemento_Estado' AND parent_object_id = OBJECT_ID(N'dbo.ReservaMemento')
                     AND definition LIKE N'%Latin1_General_BIN2%')
BEGIN
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ReservaMemento_Estado' AND parent_object_id = OBJECT_ID(N'dbo.ReservaMemento'))
        ALTER TABLE dbo.ReservaMemento DROP CONSTRAINT CK_ReservaMemento_Estado;
    ALTER TABLE dbo.ReservaMemento WITH CHECK ADD CONSTRAINT CK_ReservaMemento_Estado
        CHECK (Estado COLLATE Latin1_General_BIN2 IN (N'COTIZACION', N'PENDIENTE', N'CONFIRMADA', N'CANCELADA'));
    COMMIT TRANSACTION;
    SET XACT_ABORT OFF;
END
GO

-- MSG_RES_MONTO: el rechazo por monto (InvalidMonto) cubre ahora tambien el tope
-- de DECIMAL(12,2) de Reservas.Monto (9.999.999.999,99). El texto sembrado solo
-- nombraba el monto negativo y quedaba falso ante un total excedido. Se corrige
-- solo mientras conserve el valor de fabrica (con y sin tildes).
;WITH Fix(Codigo, Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_RES_MONTO', N'El monto no puede ser negativo.', N'El monto debe estar entre 0,00 y 9.999.999.999,99.'),
        (N'EN', N'MSG_RES_MONTO', N'The amount cannot be negative.',  N'The amount must be between 0.00 and 9,999,999,999.99.'),
        (N'PT', N'MSG_RES_MONTO', N'O valor não pode ser negativo.',   N'O valor deve estar entre 0,00 e 9.999.999.999,99.'),
        (N'PT', N'MSG_RES_MONTO', N'O valor nao pode ser negativo.',   N'O valor deve estar entre 0,00 e 9.999.999.999,99.')
    ) AS v(Codigo, Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
JOIN Fix f ON f.Codigo = i.Codigo AND f.Clave = t.Clave
WHERE t.Texto = f.Anterior;
GO

-- ===========================================================================
-- QA-12/09/2026 F07
-- ===========================================================================
-- F07 (disponibilidad y comprobante): traducciones nuevas y terminologia EN.
--
-- Idempotente, con el mismo patron que db/schema.sql: corre sobre la base que
-- indica -d, despues de las semillas de traducciones, y se puede repetir.
-- Resumen de la consulta de disponibilidad cuando ningun salon esta disponible y
-- no hay propuesta alternativa que informar (CUN002, paso 4 y flujo 4.1): no hay
-- salones registrados, ninguno alcanza en capacidad, o los que alcanzan no tienen
-- una fecha libre dentro del horizonte de busqueda.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'DISP_RESUMEN_SIN_SALONES', N'No hay salones registrados.'), (N'EN', N'DISP_RESUMEN_SIN_SALONES', N'No venues are registered.'), (N'PT', N'DISP_RESUMEN_SIN_SALONES', N'Não há salões cadastrados.'),
        (N'ES', N'DISP_RESUMEN_SIN_CAPACIDAD', N'Ningún salón tiene capacidad para {0} invitados.'), (N'EN', N'DISP_RESUMEN_SIN_CAPACIDAD', N'No venue can hold {0} guests.'), (N'PT', N'DISP_RESUMEN_SIN_CAPACIDAD', N'Nenhum salão tem capacidade para {0} convidados.'),
        (N'ES', N'DISP_RESUMEN_SIN_FECHAS', N'Ningún salón con capacidad suficiente tiene fechas libres en los {0} días siguientes.'), (N'EN', N'DISP_RESUMEN_SIN_FECHAS', N'No venue with enough capacity has free dates within the next {0} days.'), (N'PT', N'DISP_RESUMEN_SIN_FECHAS', N'Nenhum salão com capacidade suficiente tem datas livres nos próximos {0} dias.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- Terminologia EN: un unico termino ("venue") para el salon. Las semillas en
-- ingles decian "hall" en tres claves y "venue" en las demas, y las dos palabras
-- aparecian juntas en la misma pantalla. Solo se pisa el texto mientras siga
-- siendo el sembrado de fabrica: una traduccion editada por el usuario se conserva.
;WITH FixEn(Codigo, Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'EN', N'COL_SALON', N'Hall', N'Venue'),
        (N'EN', N'MSG_RES_SALON', N'Select a valid hall.', N'Select a valid venue.'),
        (N'EN', N'MSG_RES_SALON_OCUPADO', N'The hall is already booked for that date.', N'The venue is already booked for that date.')
    ) AS v(Codigo, Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
JOIN FixEn f ON f.Codigo = i.Codigo AND f.Clave = t.Clave
WHERE t.Texto = f.Anterior;
GO

-- ===========================================================================
-- QA-12/09/2026 F08
-- ===========================================================================
-- ===========================================================================
-- Paquete F08 (auditoria): agregado idempotente para db/schema.sql.
-- Se ejecuta SOBRE la base que indica -d, despues de schema.sql. Guardado en
-- UTF-8 con BOM (tildes, enie y cedilla para sqlcmd).
-- ===========================================================================
-- ===========================================================================
-- Auditoria: textos nuevos de pantalla.
--  * MSG_RANGO_FECHAS: aviso de rango invertido en Bitacora y Auditoria de login.
--  * ALERT_DV*: detalle de la alerta de integridad en el idioma activo (la
--    verificacion lo devuelve en castellano; la pantalla lo traduce).
--  * MOD_* / BACC_*: modulos y acciones que la capa de negocio asienta en la
--    bitacora. Lo guardado en Bitacora no cambia (es dato); la grilla y el
--    combo de modulos muestran la leyenda traducida y, si falta la clave, el
--    valor guardado. La clave se arma con el valor en mayusculas, sin tildes y
--    con '_' en lugar de lo que no es letra o digito.
-- Idempotente: solo inserta las claves que falten.
-- ===========================================================================
;WITH Txt AS (
    SELECT * FROM (VALUES
        -- Rango de fechas invertido
        (N'ES', N'MSG_RANGO_FECHAS', N'La fecha Desde no puede ser posterior a la fecha Hasta.'),
        (N'EN', N'MSG_RANGO_FECHAS', N'The From date cannot be later than the To date.'),
        (N'PT', N'MSG_RANGO_FECHAS', N'A data De não pode ser posterior à data Até.'),
        -- Alerta de integridad: detalle de cada inconsistencia
        (N'ES', N'ALERT_DVH_FALTANTE', N'Reserva #{0}: sin DV horizontal almacenado.'),
        (N'EN', N'ALERT_DVH_FALTANTE', N'Reservation #{0}: no stored horizontal check digit.'),
        (N'PT', N'ALERT_DVH_FALTANTE', N'Reserva #{0}: sem DV horizontal armazenado.'),
        (N'ES', N'ALERT_DVH_NO_COINCIDE', N'Reserva #{0}: el DV horizontal no coincide (posible alteración externa).'),
        (N'EN', N'ALERT_DVH_NO_COINCIDE', N'Reservation #{0}: the horizontal check digit does not match (possible external alteration).'),
        (N'PT', N'ALERT_DVH_NO_COINCIDE', N'Reserva #{0}: o DV horizontal não confere (possível alteração externa).'),
        (N'ES', N'ALERT_DVV_NO_COINCIDE', N'El DV vertical de Reservas no coincide (filas agregadas, quitadas o reordenadas por fuera del sistema).'),
        (N'EN', N'ALERT_DVV_NO_COINCIDE', N'The vertical check digit of Reservations does not match (rows added, removed or reordered outside the system).'),
        (N'PT', N'ALERT_DVV_NO_COINCIDE', N'O DV vertical de Reservas não confere (linhas adicionadas, removidas ou reordenadas fora do sistema).'),
        -- Bitacora: modulos
        (N'ES', N'MOD_AUDITORIA', N'Auditoría'),                 (N'EN', N'MOD_AUDITORIA', N'Audit'),                    (N'PT', N'MOD_AUDITORIA', N'Auditoria'),
        (N'ES', N'MOD_BITACORA', N'Bitácora'),                   (N'EN', N'MOD_BITACORA', N'Audit log'),                 (N'PT', N'MOD_BITACORA', N'Registro do sistema'),
        (N'ES', N'MOD_CLIENTES', N'Clientes'),                   (N'EN', N'MOD_CLIENTES', N'Clients'),                   (N'PT', N'MOD_CLIENTES', N'Clientes'),
        (N'ES', N'MOD_CONEXION', N'Conexión'),                   (N'EN', N'MOD_CONEXION', N'Connection'),                (N'PT', N'MOD_CONEXION', N'Conexão'),
        (N'ES', N'MOD_CREARCUENTA', N'Crear cuenta'),            (N'EN', N'MOD_CREARCUENTA', N'Create account'),         (N'PT', N'MOD_CREARCUENTA', N'Criar conta'),
        (N'ES', N'MOD_HISTORIALRESERVA', N'Historial de la reserva'), (N'EN', N'MOD_HISTORIALRESERVA', N'Reservation history'), (N'PT', N'MOD_HISTORIALRESERVA', N'Histórico da reserva'),
        (N'ES', N'MOD_IDIOMAS', N'Idiomas'),                     (N'EN', N'MOD_IDIOMAS', N'Languages'),                  (N'PT', N'MOD_IDIOMAS', N'Idiomas'),
        (N'ES', N'MOD_INTEGRIDAD', N'Integridad'),               (N'EN', N'MOD_INTEGRIDAD', N'Integrity'),               (N'PT', N'MOD_INTEGRIDAD', N'Integridade'),
        (N'ES', N'MOD_LOGIN', N'Inicio de sesión'),              (N'EN', N'MOD_LOGIN', N'Login'),                        (N'PT', N'MOD_LOGIN', N'Login'),
        (N'ES', N'MOD_PAGOS', N'Pagos'),                         (N'EN', N'MOD_PAGOS', N'Payments'),                     (N'PT', N'MOD_PAGOS', N'Pagamentos'),
        (N'ES', N'MOD_PERFILES', N'Perfiles'),                   (N'EN', N'MOD_PERFILES', N'Profiles'),                  (N'PT', N'MOD_PERFILES', N'Perfis'),
        (N'ES', N'MOD_RESERVAS', N'Reservas'),                   (N'EN', N'MOD_RESERVAS', N'Reservations'),              (N'PT', N'MOD_RESERVAS', N'Reservas'),
        (N'ES', N'MOD_SEGURIDAD', N'Seguridad'),                 (N'EN', N'MOD_SEGURIDAD', N'Security'),                 (N'PT', N'MOD_SEGURIDAD', N'Segurança'),
        (N'ES', N'MOD_SERVICIOS', N'Servicios'),                 (N'EN', N'MOD_SERVICIOS', N'Services'),                 (N'PT', N'MOD_SERVICIOS', N'Serviços'),
        (N'ES', N'MOD_USUARIOS', N'Usuarios'),                   (N'EN', N'MOD_USUARIOS', N'Users'),                     (N'PT', N'MOD_USUARIOS', N'Usuários'),
        -- Bitacora: acciones
        (N'ES', N'BACC_ACCESO_DENEGADO', N'Acceso denegado'),                         (N'EN', N'BACC_ACCESO_DENEGADO', N'Access denied'),                         (N'PT', N'BACC_ACCESO_DENEGADO', N'Acesso negado'),
        (N'ES', N'BACC_ACTUALIZACION_DE_PERMISOS', N'Actualización de permisos'),     (N'EN', N'BACC_ACTUALIZACION_DE_PERMISOS', N'Permissions updated'),         (N'PT', N'BACC_ACTUALIZACION_DE_PERMISOS', N'Permissões atualizadas'),
        (N'ES', N'BACC_ALTA_DE_CLIENTE', N'Alta de cliente'),                         (N'EN', N'BACC_ALTA_DE_CLIENTE', N'Client created'),                        (N'PT', N'BACC_ALTA_DE_CLIENTE', N'Cliente cadastrado'),
        (N'ES', N'BACC_ALTA_DE_CUENTA', N'Alta de cuenta'),                           (N'EN', N'BACC_ALTA_DE_CUENTA', N'Account created'),                        (N'PT', N'BACC_ALTA_DE_CUENTA', N'Conta criada'),
        (N'ES', N'BACC_ALTA_DE_IDIOMA', N'Alta de idioma'),                           (N'EN', N'BACC_ALTA_DE_IDIOMA', N'Language created'),                       (N'PT', N'BACC_ALTA_DE_IDIOMA', N'Idioma cadastrado'),
        (N'ES', N'BACC_ALTA_DE_PERFIL', N'Alta de perfil'),                           (N'EN', N'BACC_ALTA_DE_PERFIL', N'Profile created'),                        (N'PT', N'BACC_ALTA_DE_PERFIL', N'Perfil criado'),
        (N'ES', N'BACC_ALTA_DE_SERVICIO', N'Alta de servicio'),                       (N'EN', N'BACC_ALTA_DE_SERVICIO', N'Service created'),                      (N'PT', N'BACC_ALTA_DE_SERVICIO', N'Serviço cadastrado'),
        (N'ES', N'BACC_ALTA_RECHAZADA', N'Alta rechazada'),                           (N'EN', N'BACC_ALTA_RECHAZADA', N'Creation rejected'),                      (N'PT', N'BACC_ALTA_RECHAZADA', N'Cadastro recusado'),
        (N'ES', N'BACC_ANULACION_DE_PAGO', N'Anulación de pago'),                     (N'EN', N'BACC_ANULACION_DE_PAGO', N'Payment voided'),                      (N'PT', N'BACC_ANULACION_DE_PAGO', N'Pagamento anulado'),
        (N'ES', N'BACC_ANULACION_RECHAZADA', N'Anulación rechazada'),                 (N'EN', N'BACC_ANULACION_RECHAZADA', N'Void rejected'),                     (N'PT', N'BACC_ANULACION_RECHAZADA', N'Anulação recusada'),
        (N'ES', N'BACC_ASIGNACION_DE_PERFIL', N'Asignación de perfil'),               (N'EN', N'BACC_ASIGNACION_DE_PERFIL', N'Profile assigned'),                 (N'PT', N'BACC_ASIGNACION_DE_PERFIL', N'Perfil atribuído'),
        (N'ES', N'BACC_CAMBIO_DE_ESTADO_RECHAZADO', N'Cambio de estado rechazado'),   (N'EN', N'BACC_CAMBIO_DE_ESTADO_RECHAZADO', N'Status change rejected'),     (N'PT', N'BACC_CAMBIO_DE_ESTADO_RECHAZADO', N'Mudança de estado recusada'),
        (N'ES', N'BACC_CANCELACION_DE_RESERVA', N'Cancelación de reserva'),           (N'EN', N'BACC_CANCELACION_DE_RESERVA', N'Reservation cancelled'),          (N'PT', N'BACC_CANCELACION_DE_RESERVA', N'Cancelamento de reserva'),
        (N'ES', N'BACC_CANCELACION_RECHAZADA_POR_VIA_INCORRECTA', N'Cancelación rechazada por vía incorrecta'), (N'EN', N'BACC_CANCELACION_RECHAZADA_POR_VIA_INCORRECTA', N'Cancellation rejected (wrong path)'), (N'PT', N'BACC_CANCELACION_RECHAZADA_POR_VIA_INCORRECTA', N'Cancelamento recusado por via incorreta'),
        (N'ES', N'BACC_COMPROBANTE_GENERADO', N'Comprobante generado'),               (N'EN', N'BACC_COMPROBANTE_GENERADO', N'Receipt generated'),                (N'PT', N'BACC_COMPROBANTE_GENERADO', N'Comprovante gerado'),
        (N'ES', N'BACC_COMPROBANTE_PREPARADO_PARA_ENVIO', N'Comprobante preparado para envío'), (N'EN', N'BACC_COMPROBANTE_PREPARADO_PARA_ENVIO', N'Receipt prepared for sending'), (N'PT', N'BACC_COMPROBANTE_PREPARADO_PARA_ENVIO', N'Comprovante preparado para envio'),
        (N'ES', N'BACC_CONFIGURACION_DE_CONEXION', N'Configuración de conexión'),     (N'EN', N'BACC_CONFIGURACION_DE_CONEXION', N'Connection configured'),       (N'PT', N'BACC_CONFIGURACION_DE_CONEXION', N'Configuração de conexão'),
        (N'ES', N'BACC_CONFIRMACION_RECHAZADA', N'Confirmación rechazada'),           (N'EN', N'BACC_CONFIRMACION_RECHAZADA', N'Confirmation rejected'),          (N'PT', N'BACC_CONFIRMACION_RECHAZADA', N'Confirmação recusada'),
        (N'ES', N'BACC_COTIZACION_GENERADA', N'Cotización generada'),                 (N'EN', N'BACC_COTIZACION_GENERADA', N'Quote created'),                     (N'PT', N'BACC_COTIZACION_GENERADA', N'Orçamento gerado'),
        (N'ES', N'BACC_DESBLOQUEO_DE_CUENTA', N'Desbloqueo de cuenta'),               (N'EN', N'BACC_DESBLOQUEO_DE_CUENTA', N'Account unlocked'),                 (N'PT', N'BACC_DESBLOQUEO_DE_CUENTA', N'Desbloqueio de conta'),
        (N'ES', N'BACC_DISPONIBILIDAD_CONSULTADA', N'Disponibilidad consultada'),     (N'EN', N'BACC_DISPONIBILIDAD_CONSULTADA', N'Availability checked'),        (N'PT', N'BACC_DISPONIBILIDAD_CONSULTADA', N'Disponibilidade consultada'),
        (N'ES', N'BACC_EDICION_DE_TRADUCCIONES', N'Edición de traducciones'),         (N'EN', N'BACC_EDICION_DE_TRADUCCIONES', N'Translations edited'),           (N'PT', N'BACC_EDICION_DE_TRADUCCIONES', N'Edição de traduções'),
        (N'ES', N'BACC_ERROR', N'Error'),                                             (N'EN', N'BACC_ERROR', N'Error'),                                           (N'PT', N'BACC_ERROR', N'Erro'),
        (N'ES', N'BACC_MODIFICACION_DE_CLIENTE', N'Modificación de cliente'),         (N'EN', N'BACC_MODIFICACION_DE_CLIENTE', N'Client updated'),                (N'PT', N'BACC_MODIFICACION_DE_CLIENTE', N'Alteração de cliente'),
        (N'ES', N'BACC_MODIFICACION_DE_RESERVA', N'Modificación de reserva'),         (N'EN', N'BACC_MODIFICACION_DE_RESERVA', N'Reservation updated'),           (N'PT', N'BACC_MODIFICACION_DE_RESERVA', N'Alteração de reserva'),
        (N'ES', N'BACC_MODIFICACION_DE_SERVICIO', N'Modificación de servicio'),       (N'EN', N'BACC_MODIFICACION_DE_SERVICIO', N'Service updated'),              (N'PT', N'BACC_MODIFICACION_DE_SERVICIO', N'Alteração de serviço'),
        (N'ES', N'BACC_MODIFICACION_RECHAZADA', N'Modificación rechazada'),           (N'EN', N'BACC_MODIFICACION_RECHAZADA', N'Update rejected'),                (N'PT', N'BACC_MODIFICACION_RECHAZADA', N'Alteração recusada'),
        (N'ES', N'BACC_PAGO_RECHAZADO', N'Pago rechazado'),                           (N'EN', N'BACC_PAGO_RECHAZADO', N'Payment rejected'),                       (N'PT', N'BACC_PAGO_RECHAZADO', N'Pagamento recusado'),
        (N'ES', N'BACC_PERMISOS_NO_DISPONIBLES', N'Permisos no disponibles'),         (N'EN', N'BACC_PERMISOS_NO_DISPONIBLES', N'Permissions unavailable'),       (N'PT', N'BACC_PERMISOS_NO_DISPONIBLES', N'Permissões indisponíveis'),
        (N'ES', N'BACC_RECALCULO_DE_LINEA_BASE', N'Recálculo de línea base'),         (N'EN', N'BACC_RECALCULO_DE_LINEA_BASE', N'Baseline recalculation'),        (N'PT', N'BACC_RECALCULO_DE_LINEA_BASE', N'Recálculo da linha de base'),
        (N'ES', N'BACC_REGISTRO_DE_PAGO', N'Registro de pago'),                       (N'EN', N'BACC_REGISTRO_DE_PAGO', N'Payment recorded'),                     (N'PT', N'BACC_REGISTRO_DE_PAGO', N'Registro de pagamento'),
        (N'ES', N'BACC_RENOVACION_DE_VIGENCIA', N'Renovación de vigencia'),           (N'EN', N'BACC_RENOVACION_DE_VIGENCIA', N'Validity renewed'),               (N'PT', N'BACC_RENOVACION_DE_VIGENCIA', N'Renovação de validade'),
        (N'ES', N'BACC_RESERVA_GENERADA', N'Reserva generada'),                       (N'EN', N'BACC_RESERVA_GENERADA', N'Reservation created'),                  (N'PT', N'BACC_RESERVA_GENERADA', N'Reserva gerada'),
        (N'ES', N'BACC_RESTABLECER_CONEXION', N'Restablecer conexión'),               (N'EN', N'BACC_RESTABLECER_CONEXION', N'Connection reset'),                 (N'PT', N'BACC_RESTABLECER_CONEXION', N'Restabelecer conexão'),
        (N'ES', N'BACC_RESTAURACION_DE_VERSION', N'Restauración de versión'),         (N'EN', N'BACC_RESTAURACION_DE_VERSION', N'Version restored'),              (N'PT', N'BACC_RESTAURACION_DE_VERSION', N'Restauração de versão'),
        (N'ES', N'BACC_RESTAURACION_RECHAZADA', N'Restauración rechazada'),           (N'EN', N'BACC_RESTAURACION_RECHAZADA', N'Restore rejected'),               (N'PT', N'BACC_RESTAURACION_RECHAZADA', N'Restauração recusada'),
        (N'ES', N'BACC_TRANSICION_RECHAZADA', N'Transición rechazada'),               (N'EN', N'BACC_TRANSICION_RECHAZADA', N'Transition rejected'),              (N'PT', N'BACC_TRANSICION_RECHAZADA', N'Transição recusada'),
        (N'ES', N'BACC_VERIFICACION_DE_INTEGRIDAD_FALLIDA', N'Verificación de integridad fallida'), (N'EN', N'BACC_VERIFICACION_DE_INTEGRIDAD_FALLIDA', N'Integrity check failed'), (N'PT', N'BACC_VERIFICACION_DE_INTEGRIDAD_FALLIDA', N'Falha na verificação de integridade')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- LoginAuditLog.Action: dominio cerrado (LOGIN_OK, LOGIN_FAIL, LOGOUT), igual
-- que el estado de Reservas y de ReservaMemento. La comparacion es binaria:
-- con la intercalacion CI de la base 'login_ok' pasaria el CHECK y la lectura no
-- lo reconoceria como accion. Las filas que ya existan fuera de dominio no se
-- borran (son evidencia de auditoria): en ese caso la restriccion se crea WITH
-- NOCHECK y rige solo para las escrituras nuevas.
-- ===========================================================================
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_LoginAuditLog_Action' AND parent_object_id = OBJECT_ID('dbo.LoginAuditLog'))
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.LoginAuditLog
               WHERE [Action] COLLATE Latin1_General_BIN NOT IN (N'LOGIN_OK', N'LOGIN_FAIL', N'LOGOUT'))
        ALTER TABLE dbo.LoginAuditLog WITH NOCHECK ADD CONSTRAINT CK_LoginAuditLog_Action
            CHECK ([Action] COLLATE Latin1_General_BIN IN (N'LOGIN_OK', N'LOGIN_FAIL', N'LOGOUT'));
    ELSE
        ALTER TABLE dbo.LoginAuditLog WITH CHECK ADD CONSTRAINT CK_LoginAuditLog_Action
            CHECK ([Action] COLLATE Latin1_General_BIN IN (N'LOGIN_OK', N'LOGIN_FAIL', N'LOGOUT'));
END
GO

-- ===========================================================================
-- QA-12/09/2026 F09
-- ===========================================================================
-- ===========================================================================
-- F09 - Login, cuentas, sesion y menu principal. Agregado a db/schema.sql.
-- Idempotente y guardado en UTF-8 con BOM (tildes para sqlcmd). Se ejecuta sobre
-- la base que indica -d, igual que schema.sql.
-- ===========================================================================
SET QUOTED_IDENTIFIER ON;
GO

-- Errores de acceso a la base de datos con un mensaje funcional traducido en lugar
-- del texto crudo del motor (en ingles, con el nombre de la base y el usuario de
-- Windows): LOG-08, ROB-11, AUD-13. Inserta solo lo que falte.
--   LOGIN_ERR_SIN_BASE y CC_MSG_ERR_SIN_BASE: login y alta de cuenta.
--   MSG_ERROR_SIN_BASE: mensaje comun para las demas pantallas que hoy concatenan
--   MSG_ERROR_PREFIJO con el mensaje de la excepcion (Reservas, Clientes, Bitacora,
--   Auditoria, historial y versiones de la reserva, recalculo de digitos
--   verificadores, alta rapida de cliente y de perfil).
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'LOGIN_ERR_SIN_BASE', N'No se pudo acceder a la base de datos. Reintentá o contactate con un administrador.'),
        (N'EN', N'LOGIN_ERR_SIN_BASE', N'The database could not be reached. Try again or contact an administrator.'),
        (N'PT', N'LOGIN_ERR_SIN_BASE', N'Não foi possível acessar o banco de dados. Tente novamente ou contate um administrador.'),
        (N'ES', N'CC_MSG_ERR_SIN_BASE', N'La cuenta no se creó: no se pudo acceder a la base de datos. Reintentá más tarde.'),
        (N'EN', N'CC_MSG_ERR_SIN_BASE', N'The account was not created: the database could not be reached. Try again later.'),
        (N'PT', N'CC_MSG_ERR_SIN_BASE', N'A conta não foi criada: não foi possível acessar o banco de dados. Tente mais tarde.'),
        (N'ES', N'MSG_ERROR_SIN_BASE', N'No se pudo acceder a la base de datos. Reintentá o contactate con un administrador.'),
        (N'EN', N'MSG_ERROR_SIN_BASE', N'The database could not be reached. Try again or contact an administrator.'),
        (N'PT', N'MSG_ERROR_SIN_BASE', N'Não foi possível acessar o banco de dados. Tente novamente ou contate um administrador.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- Claves que ninguna pantalla consume desde este cambio: eran el prefijo que se
-- concatenaba con el mensaje del motor en el login y en el alta. Se quitan de las
-- bases existentes (en todos los idiomas), con el mismo criterio que las claves
-- retiradas en schema.sql; al integrar, retirar tambien sus semillas.
DELETE FROM dbo.Traducciones
 WHERE Clave IN (N'LOGIN_ERR_CONEXION', N'CC_MSG_ERROR');
GO

-- Textos PT que no entraban con la ventana principal en su tamano por defecto (VIS-17):
--   COL_DNI: "Documento" en el encabezado de la grilla de Clientes (99 px en una columna
--   de 95). "DNI" es el nombre del documento del dominio y es el texto que ya usa ES.
--   ACC_LOGOUT: "Encerramento de sessão" en la accion de la auditoria de login (164 px en
--   una columna de 141, y cortado tambien en el combo de filtro de 150 px). "Fim de sessão"
--   es el par de "Cierre de sesión".
-- Solo pisa el valor de fabrica: una traduccion editada por el usuario se conserva. La
-- tercera tupla cubre una base sembrada antes de la correccion de ortografia de schema.sql.
;WITH Fix(Codigo, Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'PT', N'COL_DNI',    N'Documento',              N'DNI'),
        (N'PT', N'ACC_LOGOUT', N'Encerramento de sessão', N'Fim de sessão'),
        (N'PT', N'ACC_LOGOUT', N'Encerramento de sessao', N'Fim de sessão')
    ) AS v(Codigo, Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
JOIN Fix f ON f.Codigo = i.Codigo AND f.Clave = t.Clave
WHERE t.Texto = f.Anterior;
GO

-- ===========================================================================
-- QA-12/09/2026 F10
-- ===========================================================================
-- EvenTech - F10 Idiomas y editor de traducciones (agregado a db/schema.sql)
--
-- Idempotente: se ejecuta sobre la base que indica -d, despues de db/schema.sql,
-- tantas veces como se quiera.
--  * Siembra los textos nuevos del editor de idiomas (ES/EN/PT): aviso de
--    "sin cambios", texto vacio, ediciones pendientes y nombre de idioma duplicado.
--  * Corrige dos textos de fabrica que ya no describen la regla vigente
--    (IDI_PLANTILLA_INVALIDA: ahora tambien se rechazan marcadores faltantes o con
--    otro formato; MSG_IDI_COD_INV: el codigo admite letras, digitos y guion,
--    empezando por letra). Solo se pisan mientras conserven el texto de fabrica:
--    una traduccion editada por el usuario se respeta.

;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Guardar sin haber editado nada no es una edicion: no se informa ni se asienta
        (N'ES', N'IDI_SIN_CAMBIOS', N'No hay cambios para guardar.'), (N'EN', N'IDI_SIN_CAMBIOS', N'There are no changes to save.'), (N'PT', N'IDI_SIN_CAMBIOS', N'Não há alterações para salvar.'),
        -- Una traduccion vacia deja menus, titulos y botones sin texto
        (N'ES', N'IDI_TEXTO_VACIO', N'El texto de ''{0}'' no puede quedar vacío.'), (N'EN', N'IDI_TEXTO_VACIO', N'The text for ''{0}'' cannot be empty.'), (N'PT', N'IDI_TEXTO_VACIO', N'O texto de ''{0}'' não pode ficar vazio.'),
        -- Cambiar de idioma, crear uno o cerrar con ediciones sin guardar
        (N'ES', N'IDI_CAMBIOS_PENDIENTES', N'Hay traducciones sin guardar en ''{0}''. ¿Desea guardarlas antes de continuar?'), (N'EN', N'IDI_CAMBIOS_PENDIENTES', N'There are unsaved translations in ''{0}''. Do you want to save them before continuing?'), (N'PT', N'IDI_CAMBIOS_PENDIENTES', N'Há traduções não salvas em ''{0}''. Deseja salvá-las antes de continuar?'),
        -- Dos idiomas con el mismo nombre son indistinguibles en el selector
        (N'ES', N'MSG_IDI_NOM_DUP', N'Ya existe un idioma con ese nombre.'), (N'EN', N'MSG_IDI_NOM_DUP', N'A language with that name already exists.'), (N'PT', N'MSG_IDI_NOM_DUP', N'Já existe um idioma com esse nome.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- Correcciones de textos de fabrica: solo si el texto vigente es EXACTAMENTE el
-- sembrado (comparacion binaria; tambien la variante sin tildes de las versiones
-- anteriores). Una traduccion editada por el usuario se conserva.
;WITH Fix(Codigo, Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'ES', N'IDI_PLANTILLA_INVALIDA', N'El texto de ''{0}'' tiene llaves sin cerrar o marcadores que la clave no admite.', N'El texto de ''{0}'' tiene llaves sin cerrar o marcadores que no coinciden con los que la clave necesita.'),
        (N'EN', N'IDI_PLANTILLA_INVALIDA', N'The text for ''{0}'' has unclosed braces or placeholders that the key does not allow.', N'The text for ''{0}'' has unclosed braces or placeholders that do not match the ones the key needs.'),
        (N'PT', N'IDI_PLANTILLA_INVALIDA', N'O texto de ''{0}'' tem chaves sem fechar ou marcadores que a chave não admite.', N'O texto de ''{0}'' tem chaves sem fechar ou marcadores que não coincidem com os que a chave precisa.'),
        (N'ES', N'MSG_IDI_COD_INV', N'Código inválido (1 a 5 caracteres).', N'Código inválido: de 1 a 5 letras, números o guiones, empezando por una letra.'),
        (N'ES', N'MSG_IDI_COD_INV', N'Codigo invalido (1 a 5 caracteres).', N'Código inválido: de 1 a 5 letras, números o guiones, empezando por una letra.'),
        (N'EN', N'MSG_IDI_COD_INV', N'Invalid code (1 to 5 chars).', N'Invalid code: 1 to 5 letters, digits or hyphens, starting with a letter.'),
        (N'PT', N'MSG_IDI_COD_INV', N'Código inválido (1 a 5 caracteres).', N'Código inválido: de 1 a 5 letras, números ou hifens, começando por uma letra.'),
        (N'PT', N'MSG_IDI_COD_INV', N'Codigo invalido (1 a 5 caracteres).', N'Código inválido: de 1 a 5 letras, números ou hifens, começando por uma letra.')
    ) AS v(Codigo, Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
JOIN Fix f ON f.Codigo = i.Codigo AND f.Clave = t.Clave
WHERE t.Texto COLLATE Latin1_General_BIN = f.Anterior COLLATE Latin1_General_BIN
  AND DATALENGTH(t.Texto) = DATALENGTH(f.Anterior);   -- '=' ignora los espacios finales
GO

-- ===========================================================================
-- QA-12/09/2026 F11
-- ===========================================================================
-- F11 - Conexion, arranque e instalacion: claves nuevas de la pantalla de conexion.
-- Idempotente (solo inserta las claves que falten), mismo patron que db/schema.sql.
-- Instancia y base son obligatorias: el rechazo reemplaza a la sustitucion
-- silenciosa por la cadena de fabrica (localhost\SQLEXPRESS / EvenTechDB).
-- El texto ES es impersonal ("Falta ..."): la ayuda de la pantalla y los
-- diagnosticos de conexion no usan el mismo trato y el aviso no suma otro.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'CONN_FALTA_SERVIDOR', N'Falta la instancia de SQL Server.'), (N'EN', N'CONN_FALTA_SERVIDOR', N'Enter the SQL Server instance.'), (N'PT', N'CONN_FALTA_SERVIDOR', N'Informe a instância do SQL Server.'),
        (N'ES', N'CONN_FALTA_BASE', N'Falta el nombre de la base de datos.'), (N'EN', N'CONN_FALTA_BASE', N'Enter the database name.'), (N'PT', N'CONN_FALTA_BASE', N'Informe o nome do banco de dados.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- Correccion de fabrica: donde ya se hubiera sembrado el primer texto ES de estas
-- claves ("Indicá ..."), pasa al texto nuevo. Solo pisa mientras el valor siga
-- siendo aquel: un texto editado desde la aplicacion se respeta.
;WITH Fix(Codigo, Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'ES', N'CONN_FALTA_SERVIDOR', N'Indicá la instancia de SQL Server.', N'Falta la instancia de SQL Server.'),
        (N'ES', N'CONN_FALTA_BASE', N'Indicá el nombre de la base de datos.', N'Falta el nombre de la base de datos.')
    ) AS v(Codigo, Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
JOIN Fix f ON f.Codigo = i.Codigo AND f.Clave = t.Clave
WHERE t.Texto = f.Anterior;
GO

-- ===========================================================================
-- QA-12/09/2026 integracion: acciones de bitacora que agregaron los paquetes de
-- correccion (la grilla las traduce por la clave BACC_ derivada del valor
-- guardado) y la inconsistencia de estado fuera del dominio de la alerta de
-- integridad. Idempotente: solo inserta las que falten.
-- ===========================================================================
;WITH Txt AS (
    SELECT * FROM (VALUES
        (N'ES', N'BACC_ASIGNACION_RECHAZADA', N'Asignación rechazada'), (N'EN', N'BACC_ASIGNACION_RECHAZADA', N'Assignment rejected'), (N'PT', N'BACC_ASIGNACION_RECHAZADA', N'Atribuição rejeitada'),
        (N'ES', N'BACC_COMPOSICION_RECHAZADA', N'Composición rechazada'), (N'EN', N'BACC_COMPOSICION_RECHAZADA', N'Composition rejected'), (N'PT', N'BACC_COMPOSICION_RECHAZADA', N'Composição rejeitada'),
        (N'ES', N'BACC_IDIOMA_RECHAZADO', N'Idioma rechazado'), (N'EN', N'BACC_IDIOMA_RECHAZADO', N'Language rejected'), (N'PT', N'BACC_IDIOMA_RECHAZADO', N'Idioma rejeitado'),
        (N'ES', N'BACC_TRADUCCIONES_RECHAZADAS', N'Traducciones rechazadas'), (N'EN', N'BACC_TRADUCCIONES_RECHAZADAS', N'Translations rejected'), (N'PT', N'BACC_TRADUCCIONES_RECHAZADAS', N'Traduções rejeitadas'),
        (N'ES', N'BACC_OPERACION_SOBRE_DATO_ALTERADO', N'Operación sobre dato alterado'), (N'EN', N'BACC_OPERACION_SOBRE_DATO_ALTERADO', N'Operation on altered data'), (N'PT', N'BACC_OPERACION_SOBRE_DATO_ALTERADO', N'Operação sobre dado alterado'),
        (N'ES', N'BACC_CANCELACION_RECHAZADA', N'Cancelación rechazada'), (N'EN', N'BACC_CANCELACION_RECHAZADA', N'Cancellation rejected'), (N'PT', N'BACC_CANCELACION_RECHAZADA', N'Cancelamento rejeitado'),
        (N'ES', N'BACC_EMISION_DE_COMPROBANTE_RECHAZADA', N'Emisión de comprobante rechazada'), (N'EN', N'BACC_EMISION_DE_COMPROBANTE_RECHAZADA', N'Receipt issuing rejected'), (N'PT', N'BACC_EMISION_DE_COMPROBANTE_RECHAZADA', N'Emissão de comprovante rejeitada'),
        (N'ES', N'BACC_ENVIO_DE_COMPROBANTE_RECHAZADO', N'Envío de comprobante rechazado'), (N'EN', N'BACC_ENVIO_DE_COMPROBANTE_RECHAZADO', N'Receipt sending rejected'), (N'PT', N'BACC_ENVIO_DE_COMPROBANTE_RECHAZADO', N'Envio de comprovante rejeitado'),
        (N'ES', N'BACC_ALTA_DE_SERVICIO_RECHAZADA', N'Alta de servicio rechazada'), (N'EN', N'BACC_ALTA_DE_SERVICIO_RECHAZADA', N'Service creation rejected'), (N'PT', N'BACC_ALTA_DE_SERVICIO_RECHAZADA', N'Cadastro de serviço rejeitado'),
        (N'ES', N'BACC_MODIFICACION_DE_SERVICIO_RECHAZADA', N'Modificación de servicio rechazada'), (N'EN', N'BACC_MODIFICACION_DE_SERVICIO_RECHAZADA', N'Service update rejected'), (N'PT', N'BACC_MODIFICACION_DE_SERVICIO_RECHAZADA', N'Alteração de serviço rejeitada'),
        (N'ES', N'ALERT_ESTADO_FUERA_DOMINIO', N'Reserva #{0}: estado almacenado fuera del dominio de la tabla de estados (posible alteración externa).'),
        (N'EN', N'ALERT_ESTADO_FUERA_DOMINIO', N'Reservation #{0}: stored status outside the status table domain (possible external tampering).'),
        (N'PT', N'ALERT_ESTADO_FUERA_DOMINIO', N'Reserva #{0}: estado armazenado fora do domínio da tabela de estados (possível alteração externa).')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-13/09/2026 R03
-- ===========================================================================
-- ===========================================================================
-- R03 - Ficha de reservas (ucReservas): texto nuevo de la interfaz.
--  * MSG_RES_ESTADO_DESCONOCIDO: aviso de la ficha cuando la reserva abierta tiene
--    un estado almacenado que no es ninguno de la tabla de estados (alteracion
--    externa). La ficha queda de solo lectura, sin estado seleccionado, y no admite
--    guardar, cargar servicios, cobrar ni emitir documentos.
-- Idempotente: solo inserta las claves que falten (mismo patron que db/schema.sql,
-- con GROUP BY i.Id, t.Clave para que una clave repetida no rompa UQ_Traducciones).
-- Una traduccion editada por el usuario se conserva.
-- Guardado en UTF-8 con BOM: tildes para sqlcmd.
-- ===========================================================================
SET QUOTED_IDENTIFIER ON;
GO

;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_RES_ESTADO_DESCONOCIDO', N'El estado registrado de la reserva no es válido: no admite modificaciones. Contactate con un administrador.'),
        (N'EN', N'MSG_RES_ESTADO_DESCONOCIDO', N'The recorded status of the reservation is not valid: it cannot be modified. Contact an administrator.'),
        (N'PT', N'MSG_RES_ESTADO_DESCONOCIDO', N'O estado registrado da reserva não é válido: não admite modificações. Contate um administrador.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- ===========================================================================
-- QA-13/09/2026 R04 (Pagos)
-- ===========================================================================
-- El aviso de la anulacion rechazada por la RN-07 (una reserva confirmada no
-- puede quedar sin adelanto) no cita el codigo interno de la regla: como los
-- demas mensajes de la pantalla, esta redactado para el usuario. El codigo de
-- la regla sigue en el asiento de bitacora.
-- Idempotente, mismo patron que db/schema.sql:
--  1. Inserta la clave con el texto nuevo donde falte (una base nueva en la que
--     este bloque corra antes del que siembra la clave queda ya con el texto
--     nuevo, y aquel bloque la saltea por NOT EXISTS).
--  2. Correccion de fabrica: donde la clave conserve EXACTAMENTE el texto
--     sembrado con el codigo (comparacion binaria), pasa al texto nuevo. Una
--     traduccion editada desde Gestion de idiomas se respeta.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'La reserva está confirmada: anular este pago la dejaría sin adelanto. Registre primero el pago que lo reemplaza o cancele la reserva.'),
        (N'EN', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'The reservation is confirmed: voiding this payment would leave it without a deposit. Record the replacement payment first or cancel the reservation.'),
        (N'PT', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'A reserva está confirmada: anular este pagamento a deixaria sem adiantamento. Registre primeiro o pagamento que o substitui ou cancele a reserva.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

;WITH Fix(Codigo, Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'La reserva está confirmada: anular este pago la dejaría sin adelanto (RN-07). Registre primero el pago que lo reemplaza o cancele la reserva.', N'La reserva está confirmada: anular este pago la dejaría sin adelanto. Registre primero el pago que lo reemplaza o cancele la reserva.'),
        (N'EN', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'The reservation is confirmed: voiding this payment would leave it without a deposit (RN-07). Record the replacement payment first or cancel the reservation.', N'The reservation is confirmed: voiding this payment would leave it without a deposit. Record the replacement payment first or cancel the reservation.'),
        (N'PT', N'MSG_PAGO_ANULAR_SIN_ADELANTO', N'A reserva está confirmada: anular este pagamento a deixaria sem adiantamento (RN-07). Registre primeiro o pagamento que o substitui ou cancele a reserva.', N'A reserva está confirmada: anular este pagamento a deixaria sem adiantamento. Registre primeiro o pagamento que o substitui ou cancele a reserva.')
    ) AS v(Codigo, Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN dbo.Idiomas i ON i.Id = t.IdiomaId
JOIN Fix f ON f.Codigo = i.Codigo AND f.Clave = t.Clave
WHERE t.Texto COLLATE Latin1_General_BIN = f.Anterior COLLATE Latin1_General_BIN
  AND DATALENGTH(t.Texto) = DATALENGTH(f.Anterior);   -- '=' ignora los espacios finales
GO

-- ===========================================================================
-- QA-13/09/2026 R06
-- ===========================================================================
-- ===========================================================================
-- R06 - Idiomas, traducciones y mensajes de error comunes (se agrega a db/schema.sql).
-- Idempotente: inserta solo lo que falte, asi una traduccion editada por el
-- usuario desde Gestion de Idiomas se conserva. Guardado en UTF-8 con BOM.
--  * MSG_ERROR_OPERACION: aviso comun cuando la base respondio pero la operacion
--    no se pudo completar (una restriccion, un dato que no entra, un bloqueo). El
--    aviso MSG_ERROR_SIN_BASE queda para los errores de conexion: red, tiempo de
--    espera, base inexistente o fuera de linea, inicio de sesion rechazado.
--  * EST_DESCONOCIDO: leyenda de un estado de reserva almacenado que no es ninguno
--    de la tabla de estados (alteracion externa de la base), en lugar de la clave
--    cruda "EST_-1" en la grilla, en los avisos y en Versiones.
--  * IDI_FILTRO_INVALIDO: el editor de idiomas rechaza un CMP_FILTER que no es un
--    filtro de archivos del cuadro "Guardar como" (descripcion|patron).
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_ERROR_OPERACION', N'No se pudo completar la operación. Reintentá o contactate con un administrador.'),
        (N'EN', N'MSG_ERROR_OPERACION', N'The operation could not be completed. Try again or contact an administrator.'),
        (N'PT', N'MSG_ERROR_OPERACION', N'Não foi possível concluir a operação. Tente novamente ou contate um administrador.'),
        (N'ES', N'EST_DESCONOCIDO', N'(estado desconocido)'),
        (N'EN', N'EST_DESCONOCIDO', N'(unknown status)'),
        (N'PT', N'EST_DESCONOCIDO', N'(estado desconhecido)'),
        (N'ES', N'IDI_FILTRO_INVALIDO', N'El texto de ''{0}'' tiene que ser un filtro de archivos: descripción y patrón separados por ''|'', por ejemplo: Documento HTML (*.html)|*.html'),
        (N'EN', N'IDI_FILTRO_INVALIDO', N'The text for ''{0}'' must be a file filter: description and pattern separated by ''|'', for example: HTML document (*.html)|*.html'),
        (N'PT', N'IDI_FILTRO_INVALIDO', N'O texto de ''{0}'' deve ser um filtro de arquivos: descrição e padrão separados por ''|'', por exemplo: Documento HTML (*.html)|*.html')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-13/09/2026 R08
-- ===========================================================================
-- ----------------------------------------------------------------------------
-- R08: aviso de la ventana principal antes de descartar una vista con cambios sin
-- guardar al cambiar de seccion (volver a pulsar la seccion activa ya no la rearma).
-- Inserta solo lo que falte; una clave repetida no rompe el INSERT (GROUP BY).
-- ----------------------------------------------------------------------------
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MAIN_CAMBIOS_SIN_GUARDAR', N'Hay cambios sin guardar en la sección actual. ¿Descartarlos y continuar?'),
        (N'EN', N'MAIN_CAMBIOS_SIN_GUARDAR', N'There are unsaved changes in the current section. Discard them and continue?'),
        (N'PT', N'MAIN_CAMBIOS_SIN_GUARDAR', N'Há alterações não salvas na seção atual. Descartá-las e continuar?')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- ===========================================================================
-- QA-13/09/2026 R09
-- ===========================================================================
-- EvenTech - Agregado al esquema del paquete R09 (catalogo de servicios)
--
-- Se inserta dentro de db/schema.sql, que ya trae al principio la guarda contra
-- las bases del sistema: este bloque no crea la base, no hace USE ni repite la
-- guarda. Idempotente, mismo criterio que db/schema.sql: solo inserta las claves
-- que falten y corrige un texto de fabrica mientras conserve el valor sembrado.
-- Guardado en UTF-8 con BOM (tildes para sqlcmd).

SET QUOTED_IDENTIFIER ON;
GO

-- ===========================================================================
-- R09 - MSG_SRV_PRECIO_MAX pasa a ser una plantilla con {0}. La ficha del catalogo
-- escribe el tope con la configuracion regional de la estacion (Tr_704ILR.F_704ILR
-- con el importe ya formateado en N2): es el formato de la columna Precio de la
-- grilla y el que la ficha acepta al leer el precio. Hasta ahora cada idioma traia
-- el numero fijo ("9.999.999.999,99" en ES y PT, "9,999,999,999.99" en EN): en una
-- estacion en-US el mensaje en espanol mostraba un numero que la propia ficha
-- rechazaba como invalido. El editor de idiomas admite el marcador porque la clave
-- figura en BLL_Idioma_704ILR.MarcadoresPorClave_704ILR con "{0}".
-- Idempotente: solo inserta las claves que falten.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_SRV_PRECIO_MAX', N'El precio no puede superar {0}.'), (N'EN', N'MSG_SRV_PRECIO_MAX', N'The price cannot exceed {0}.'), (N'PT', N'MSG_SRV_PRECIO_MAX', N'O preço não pode ultrapassar {0}.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-14/09/2026 S01
-- ===========================================================================
-- ===========================================================================
-- QA tercera ronda - paquete S01 (ficha de Reservas)
-- ===========================================================================
-- Agregado al esquema del paquete S01. Idempotente, con el mismo patron que
-- db/schema.sql: corre sobre la base que indica -d (no crea la base, no hace USE
-- ni repite la guarda), despues de las semillas de traducciones, y se puede
-- repetir: solo inserta las claves que falten y corrige un texto de fabrica
-- mientras conserve exactamente ese valor. Guardado en UTF-8 con BOM (tildes y
-- eñe para sqlcmd).

SET QUOTED_IDENTIFIER ON;
GO

-- ---------------------------------------------------------------------------
-- Avisos nuevos de la ficha de Reservas (ucReservas_704ILR):
--  * MSG_RES_DATO_NO_DISPONIBLE: el cliente o el salon de la reserva no figuran en
--    las listas de la ficha ni despues de recargarlas; la ficha queda de solo lectura
--    (antes el combo quedaba en otro cliente y Guardar le reasignaba la reserva).
--  * MSG_RES_FECHA_FUERA_RANGO: la fecha del evento almacenada esta fuera del
--    calendario que admite el selector (alteracion externa: 9999, o 2080 con el
--    calendario de ar-SA); la ficha queda de solo lectura.
--  * MSG_RES_NO_CARGADA: la reserva no se pudo mostrar por una falla; solo lectura.
--  * MSG_RES_CAMBIOS_PAGOS: Pagos con los servicios cambiados y sin guardar (el
--    dialogo cobra contra el total guardado).
--  * MSG_RES_CAMBIOS_DOCUMENTOS: Comprobante o Email con cambios sin guardar (el
--    documento se arma con la reserva guardada).
--  * MSG_RES_CANCELAR_OTROS_CAMBIOS: linea que se suma a la pregunta de cancelacion
--    cuando la ficha tiene, ademas del estado, otros cambios que no se guardan.
-- Inserta solo lo que falte; una clave repetida no rompe el INSERT (GROUP BY).
-- ---------------------------------------------------------------------------
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_RES_DATO_NO_DISPONIBLE', N'El cliente o el salón registrado en la reserva no figura en las listas de la ficha: no admite modificaciones. Contactate con un administrador.'),
        (N'EN', N'MSG_RES_DATO_NO_DISPONIBLE', N'The client or venue recorded in the reservation is not in the form''s lists: it cannot be modified. Contact an administrator.'),
        (N'PT', N'MSG_RES_DATO_NO_DISPONIBLE', N'O cliente ou o salão registrado na reserva não consta nas listas da ficha: não admite modificações. Contate um administrador.'),
        (N'ES', N'MSG_RES_FECHA_FUERA_RANGO', N'La fecha del evento registrada está fuera del calendario admitido: no admite modificaciones. Contactate con un administrador.'),
        (N'EN', N'MSG_RES_FECHA_FUERA_RANGO', N'The recorded event date is outside the supported calendar: it cannot be modified. Contact an administrator.'),
        (N'PT', N'MSG_RES_FECHA_FUERA_RANGO', N'A data do evento registrada está fora do calendário admitido: não admite modificações. Contate um administrador.'),
        (N'ES', N'MSG_RES_NO_CARGADA', N'No se pudo mostrar la reserva: no se admite modificarla. Seleccione otra reserva y vuelva a abrirla.'),
        (N'EN', N'MSG_RES_NO_CARGADA', N'The reservation could not be displayed: it cannot be modified. Select another reservation and open it again.'),
        (N'PT', N'MSG_RES_NO_CARGADA', N'Não foi possível exibir a reserva: não é possível modificá-la. Selecione outra reserva e abra-a novamente.'),
        (N'ES', N'MSG_RES_CAMBIOS_PAGOS', N'Los servicios de la reserva tienen cambios sin guardar: guarde la reserva antes de registrar pagos.'),
        (N'EN', N'MSG_RES_CAMBIOS_PAGOS', N'The reservation''s services have unsaved changes: save the reservation before adding payments.'),
        (N'PT', N'MSG_RES_CAMBIOS_PAGOS', N'Os serviços da reserva têm alterações não salvas: salve a reserva antes de registrar pagamentos.'),
        (N'ES', N'MSG_RES_CAMBIOS_DOCUMENTOS', N'La reserva tiene cambios sin guardar: guárdela antes de emitir su documentación.'),
        (N'EN', N'MSG_RES_CAMBIOS_DOCUMENTOS', N'The reservation has unsaved changes: save it before issuing its paperwork.'),
        (N'PT', N'MSG_RES_CAMBIOS_DOCUMENTOS', N'A reserva tem alterações não salvas: salve-a antes de emitir sua documentação.'),
        (N'ES', N'MSG_RES_CANCELAR_OTROS_CAMBIOS', N'Los demás cambios sin guardar de la ficha se descartarán: solo se aplica la cancelación.'),
        (N'EN', N'MSG_RES_CANCELAR_OTROS_CAMBIOS', N'The other unsaved changes in the form will be discarded: only the cancellation is applied.'),
        (N'PT', N'MSG_RES_CANCELAR_OTROS_CAMBIOS', N'As demais alterações não salvas da ficha serão descartadas: só o cancelamento é aplicado.'),
        (N'ES', N'MSG_RES_MONTO', N'El monto no puede ser negativo ni superar {0}.'),
        (N'EN', N'MSG_RES_MONTO', N'The amount cannot be negative or exceed {0}.'),
        (N'PT', N'MSG_RES_MONTO', N'O valor não pode ser negativo nem ultrapassar {0}.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ---------------------------------------------------------------------------
-- MSG_RES_MONTO pasa a plantilla con {0}: la ficha escribe el tope del monto
-- (BLL_Reserva_704ILR.MontoMaximo_704ILR) con la configuracion regional de la
-- estacion (N2), el mismo formato del campo Monto y de la grilla. Cada idioma traia
-- el numero fijo ("0,00 y 9.999.999.999,99" en ES y PT, "0.00 and 9,999,999,999.99"
-- en EN): en otra configuracion regional el aviso mostraba otros separadores que los
-- de la ficha. El editor de idiomas admite el marcador con la clave registrada en
-- BLL_Idioma_704ILR.MarcadoresPorClave_704ILR.
-- Correccion de fabrica: se aplica a CUALQUIER idioma cuyo texto sea exactamente uno
-- de los textos de fabrica anteriores de la clave (tambien un idioma propio que los
-- copio), con comparacion binaria y del largo en bytes ('=' ignora los espacios
-- finales). Cada texto anterior pasa a la plantilla de su idioma. Una traduccion
-- editada desde la aplicacion se respeta.
-- ---------------------------------------------------------------------------
;WITH Fix(Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'El monto debe estar entre 0,00 y 9.999.999.999,99.',     N'El monto no puede ser negativo ni superar {0}.'),
        (N'El monto no puede ser negativo.',                        N'El monto no puede ser negativo ni superar {0}.'),
        (N'The amount must be between 0.00 and 9,999,999,999.99.',  N'The amount cannot be negative or exceed {0}.'),
        (N'The amount cannot be negative.',                         N'The amount cannot be negative or exceed {0}.'),
        (N'O valor deve estar entre 0,00 e 9.999.999.999,99.',      N'O valor não pode ser negativo nem ultrapassar {0}.'),
        (N'O valor não pode ser negativo.',                         N'O valor não pode ser negativo nem ultrapassar {0}.'),
        (N'O valor nao pode ser negativo.',                         N'O valor não pode ser negativo nem ultrapassar {0}.')
    ) AS v(Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN Fix f ON t.Texto COLLATE Latin1_General_BIN2 = f.Anterior COLLATE Latin1_General_BIN2
          AND DATALENGTH(t.Texto) = DATALENGTH(f.Anterior)
WHERE t.Clave = N'MSG_RES_MONTO';
GO

-- ===========================================================================
-- QA-14/09/2026 S03
-- ===========================================================================
-- EvenTech - Tercera ronda de QA, paquete S03 (Clientes): cambios de base.
--
-- Idempotente, igual que db/schema.sql: se ejecuta sobre la base que indica -d (no crea
-- la base ni cambia de contexto) y se puede volver a correr sin efectos nuevos.

-- Correccion de fabrica: en ingles la confirmacion del alta de un cliente decia
-- "Customer registered.", la unica aparicion de "Customer" en toda la interfaz; el resto
-- de la pantalla (titulo, ficha, contador y avisos) dice "client".
-- Se corrige en CUALQUIER idioma cuyo texto sea EXACTAMENTE el sembrado, tambien un idioma
-- propio que lo copio al crearse: comparacion binaria y el mismo largo en bytes (el '='
-- ignora los espacios finales). Una traduccion editada desde la aplicacion se respeta.
;WITH Fix(Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'MSG_CLI_CREADO', N'Customer registered.', N'Client registered.')
    ) AS v(Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN Fix f ON f.Clave = t.Clave
WHERE t.Texto COLLATE Latin1_General_BIN2 = f.Anterior COLLATE Latin1_General_BIN2
  AND DATALENGTH(t.Texto) = DATALENGTH(f.Anterior);
GO

-- ===========================================================================
-- QA-14/09/2026 S04
-- ===========================================================================
-- ===========================================================================
-- QA ronda 3 - S04 (Perfiles)
-- ===========================================================================
-- Se inserta dentro de db/schema.sql, que ya trae al principio la guarda contra
-- las bases del sistema y SET QUOTED_IDENTIFIER ON: este bloque no crea la base,
-- no hace USE ni repite la guarda. Idempotente, mismo criterio que db/schema.sql:
-- solo inserta las claves que falten (una clave repetida no rompe el INSERT).
-- Guardado en UTF-8 con BOM (tildes para sqlcmd).
-- ----------------------------------------------------------------------------
-- Perfiles: pregunta antes de descartar los permisos tildados y no guardados de un
-- perfil al elegir otro en el combo o al pasar al perfil recién creado. Es otra
-- clave que MAIN_CAMBIOS_SIN_GUARDAR porque cambiar de perfil descarta solo la
-- composición a la vista: las asignaciones de la grilla se conservan.
-- ----------------------------------------------------------------------------
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_PERF_DESCARTAR_PERMISOS', N'Hay cambios sin guardar en los permisos de este perfil. ¿Descartarlos y cambiar de perfil?'),
        (N'EN', N'MSG_PERF_DESCARTAR_PERMISOS', N'There are unsaved changes in this profile''s permissions. Discard them and change profile?'),
        (N'PT', N'MSG_PERF_DESCARTAR_PERMISOS', N'Há alterações não salvas nas permissões deste perfil. Descartá-las e trocar de perfil?')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- ===========================================================================
-- QA-14/09/2026 S05
-- ===========================================================================
-- ===========================================================================
-- S05 - Mensajes de error, idiomas y arranque (se agrega a db/schema.sql).
-- Idempotente: inserta solo lo que falte, asi una traduccion editada por el
-- usuario desde Gestion de Idiomas se conserva. Guardado en UTF-8 con BOM.
--  * CRIT_DESCONOCIDA: leyenda de la criticidad de un asiento de bitacora que no
--    es Info, Advertencia ni Error (Bitacora.Criticidad alterada por fuera de la
--    aplicacion), en lugar de la clave cruda "CRIT_7" en la grilla de Bitacora.
--  * BACC_CARGA_DE_IDIOMAS_RECUPERADA: accion que se asienta cuando la carga de
--    idiomas y traducciones habia fallado (por ejemplo al arrancar, con la base
--    caida un momento) y se completo al reintentarla.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'CRIT_DESCONOCIDA', N'(desconocida)'),
        (N'EN', N'CRIT_DESCONOCIDA', N'(unknown)'),
        (N'PT', N'CRIT_DESCONOCIDA', N'(desconhecida)'),
        (N'ES', N'BACC_CARGA_DE_IDIOMAS_RECUPERADA', N'Carga de idiomas recuperada'),
        (N'EN', N'BACC_CARGA_DE_IDIOMAS_RECUPERADA', N'Language loading recovered'),
        (N'PT', N'BACC_CARGA_DE_IDIOMAS_RECUPERADA', N'Carregamento de idiomas recuperado')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-14/09/2026 S06
-- ===========================================================================
-- ===========================================================================
-- QA-14/09/2026 S06 - Servicios: correccion de fabrica de MSG_SRV_PRECIO_MAX
-- ===========================================================================
-- Se agrega a db/schema.sql (antes del cierre SET NOEXEC OFF). No crea la base,
-- no hace USE ni repite la guarda del principio. Guardado en UTF-8 con BOM.
--
-- MSG_SRV_PRECIO_MAX paso a ser una plantilla con {0}: la ficha del catalogo
-- escribe el tope con la configuracion regional de la estacion. Donde quedo
-- sembrado el texto viejo con el numero fijo, pasa a la plantilla.
--  * Alcanza a CUALQUIER idioma cuyo texto sea exactamente uno de los textos de
--    fabrica anteriores, no solo a ES, EN y PT: un idioma creado desde Gestion de
--    Idiomas copia los textos de ES y conservaba el numero fijo despues de correr
--    el script. Recibe la plantilla del idioma cuyo texto viejo tiene.
--  * La comparacion es exacta: intercalacion binaria (distingue mayusculas,
--    tildes y cualquier caracter) y la misma longitud en bytes (DATALENGTH). El
--    '=' de SQL Server completa con espacios antes de comparar, aun con
--    intercalacion binaria, y daba por igual un texto con espacios al final.
--  * Una traduccion editada por el usuario (cualquier diferencia) se conserva.
-- Idempotente: una vez corregido, el texto ya no es ninguno de los anteriores.
-- ===========================================================================
SET QUOTED_IDENTIFIER ON;
GO

;WITH Fix(Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'MSG_SRV_PRECIO_MAX', N'El precio no puede superar 9.999.999.999,99.', N'El precio no puede superar {0}.'),
        (N'MSG_SRV_PRECIO_MAX', N'The price cannot exceed 9,999,999,999.99.', N'The price cannot exceed {0}.'),
        (N'MSG_SRV_PRECIO_MAX', N'O preço não pode ultrapassar 9.999.999.999,99.', N'O preço não pode ultrapassar {0}.')
    ) AS v(Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN Fix f ON f.Clave = t.Clave
WHERE t.Texto COLLATE Latin1_General_BIN = f.Anterior COLLATE Latin1_General_BIN
  AND DATALENGTH(t.Texto) = DATALENGTH(f.Anterior);
GO

-- ===========================================================================
-- QA-14/09/2026 S09
-- ===========================================================================
-- ===========================================================================
-- QA-14/09/2026 S09 - Pantalla de configuracion de conexion
-- ===========================================================================
-- Aviso que muestra la pantalla cuando se pide cerrarla (Salir, la cruz o Alt+F4)
-- mientras la configuracion elegida ya se esta escribiendo: en lugar de cerrar a
-- mitad del guardado, la pantalla lo termina (connection.cfg y su asiento en la
-- bitacora) y cierra con ese resultado. El codigo trae el mismo texto ES como
-- valor por defecto. Idempotente (solo inserta las claves que falten), mismo
-- patron que db/schema.sql.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'CONN_GUARDANDO', N'Guardando la configuración...'), (N'EN', N'CONN_GUARDANDO', N'Saving the settings...'), (N'PT', N'CONN_GUARDANDO', N'Salvando a configuração...')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-15/09/2026 integracion
-- ===========================================================================
-- Modulo de bitacora de la red de seguridad de la interfaz (Program_704ILR): una
-- excepcion no manejada en un evento de pantalla se asienta con este modulo.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MOD_APLICACION', N'Aplicación'), (N'EN', N'MOD_APLICACION', N'Application'), (N'PT', N'MOD_APLICACION', N'Aplicação')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- ===========================================================================
-- QA-15/09/2026 T04
-- ===========================================================================
-- EvenTech - Agregado al esquema del paquete T04 (Servicios: catalogo).
--
-- Idempotente y con el mismo patron que db/schema.sql: corre sobre la base que
-- indica -d y solo inserta las claves que falten. Guardado en UTF-8 con BOM
-- (tildes para sqlcmd). Se integra en el bloque de traducciones de Servicios.
--
--   MSG_SRV_NOTFOUND  guardar un servicio que se borro por fuera de la pantalla
--                     (antes se mostraba el texto de Reservas "La reserva ya no
--                     existe."). Mismo criterio que MSG_CLI_NOTFOUND en Clientes.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Servicios (Proceso 1): edicion de un servicio borrado
        (N'ES', N'MSG_SRV_NOTFOUND', N'El servicio ya no existe.'), (N'EN', N'MSG_SRV_NOTFOUND', N'The service no longer exists.'), (N'PT', N'MSG_SRV_NOTFOUND', N'O serviço não existe mais.')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-15/09/2026 T05
-- ===========================================================================
-- ===========================================================================
-- Ficha de Reservas, comprobante y correo: textos nuevos.
-- Corre sobre la base que indica -d, igual que db\schema.sql; inserta solo lo que
-- falte, asi que se puede volver a correr sin efecto (una clave repetida tampoco
-- rompe el INSERT: GROUP BY).
--  * MSG_RES_INVITADOS_FUERA_RANGO: la cantidad de invitados registrada esta fuera
--    del rango del campo (alteracion externa); la ficha queda de solo lectura.
--  * MSG_EMAIL_ILEGIBLE: el email del cliente es un paquete cifrado que este equipo
--    no puede abrir (base restaurada en otra PC); no se prepara el envio.
--  * MSG_EMAIL_SIN_PROGRAMA: no se pudo abrir un programa de correo; el aviso agrega
--    la ruta del comprobante guardado para adjuntarlo.
--  * MSG_EMAIL_SIN_CARPETA: se abrio el correo pero no la carpeta del adjunto; el
--    aviso agrega la ruta.
--  * MSG_CMP_NO_ABIERTO: el comprobante se guardo pero no se pudo abrir; el aviso
--    agrega la ruta.
-- ===========================================================================
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'MSG_RES_INVITADOS_FUERA_RANGO', N'La cantidad de invitados registrada está fuera del rango admitido: no admite modificaciones. Contactate con un administrador.'),
        (N'EN', N'MSG_RES_INVITADOS_FUERA_RANGO', N'The recorded number of guests is outside the allowed range: it cannot be modified. Contact an administrator.'),
        (N'PT', N'MSG_RES_INVITADOS_FUERA_RANGO', N'A quantidade de convidados registrada está fora do intervalo admitido: não admite modificações. Contate um administrador.'),
        (N'ES', N'MSG_EMAIL_ILEGIBLE', N'El email del cliente no se puede leer en este equipo (quedó cifrado con la clave de otra instalación): vuelva a cargarlo en la ficha del cliente.'),
        (N'EN', N'MSG_EMAIL_ILEGIBLE', N'The client''s email cannot be read on this computer (it was encrypted with another installation''s key): enter it again in the client''s record.'),
        (N'PT', N'MSG_EMAIL_ILEGIBLE', N'O email do cliente não pode ser lido neste computador (foi criptografado com a chave de outra instalação): cadastre-o novamente na ficha do cliente.'),
        (N'ES', N'MSG_EMAIL_SIN_PROGRAMA', N'No se pudo abrir un programa de correo para preparar el mensaje. El comprobante quedó guardado para adjuntarlo en:'),
        (N'EN', N'MSG_EMAIL_SIN_PROGRAMA', N'No email program could be opened to prepare the message. The receipt was saved so it can be attached from:'),
        (N'PT', N'MSG_EMAIL_SIN_PROGRAMA', N'Não foi possível abrir um programa de email para preparar a mensagem. O comprovante foi salvo para ser anexado a partir de:'),
        (N'ES', N'MSG_EMAIL_SIN_CARPETA', N'Se abrió el correo con el mensaje listo, pero no se pudo abrir la carpeta del comprobante. El archivo para adjuntar quedó en:'),
        (N'EN', N'MSG_EMAIL_SIN_CARPETA', N'The email opened with the message ready, but the receipt''s folder could not be opened. The file to attach is at:'),
        (N'PT', N'MSG_EMAIL_SIN_CARPETA', N'O email foi aberto com a mensagem pronta, mas não foi possível abrir a pasta do comprovante. O arquivo para anexar ficou em:'),
        (N'ES', N'MSG_CMP_NO_ABIERTO', N'El comprobante se guardó, pero no se pudo abrir automáticamente. El archivo quedó en:'),
        (N'EN', N'MSG_CMP_NO_ABIERTO', N'The receipt was saved, but it could not be opened automatically. The file is at:'),
        (N'PT', N'MSG_CMP_NO_ABIERTO', N'O comprovante foi salvo, mas não foi possível abri-lo automaticamente. O arquivo ficou em:')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ===========================================================================
-- QA-15/09/2026 integracion ronda 4
-- ===========================================================================
-- CONN_NO_GUARDADA, CONN_CAUSA_ACCESO y CONN_CAUSA_EN_USO: aviso de la pantalla de conexion
-- cuando la configuracion probada no se puede escribir (antes era un texto fijo en castellano
-- seguido del mensaje de .NET, tambien en EN/PT).
-- CMP_DATO_ILEGIBLE: el comprobante muestra esta leyenda en lugar de un contacto que quedo
-- cifrado con la clave de otra instalacion.
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        (N'ES', N'CONN_NO_GUARDADA', N'No se pudo guardar la configuración.'), (N'EN', N'CONN_NO_GUARDADA', N'The configuration could not be saved.'), (N'PT', N'CONN_NO_GUARDADA', N'Não foi possível salvar a configuração.'),
        (N'ES', N'CONN_CAUSA_ACCESO', N'No hay permiso para escribir el archivo de configuración.'), (N'EN', N'CONN_CAUSA_ACCESO', N'There is no permission to write the configuration file.'), (N'PT', N'CONN_CAUSA_ACCESO', N'Não há permissão para gravar o arquivo de configuração.'),
        (N'ES', N'CONN_CAUSA_EN_USO', N'El archivo de configuración está en uso o no se pudo escribir.'), (N'EN', N'CONN_CAUSA_EN_USO', N'The configuration file is in use or could not be written.'), (N'PT', N'CONN_CAUSA_EN_USO', N'O arquivo de configuração está em uso ou não pôde ser gravado.'),
        (N'ES', N'CMP_DATO_ILEGIBLE', N'(no se puede leer en este equipo)'), (N'EN', N'CMP_DATO_ILEGIBLE', N'(cannot be read on this computer)'), (N'PT', N'CMP_DATO_ILEGIBLE', N'(não pode ser lido neste computador)')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;
GO

-- ===========================================================================
-- Proceso 2 (RFN2): Asignacion de Personal y Cronograma de Eventos.
-- Tablas del personal, de la coordinacion del evento y de su ejecucion, el
-- estado de coordinacion de la reserva, los permisos y los perfiles operativos.
-- Idempotente, con el mismo patron que el resto del script: corre sobre la base
-- que indica -d, crea lo que falte y no toca lo que ya existe.
-- ===========================================================================
SET QUOTED_IDENTIFIER ON;
GO

-- Especialidades del personal (catalogo). Es el criterio con el que el
-- coordinador busca a quien asignar y a quien reemplaza a un empleado que
-- rechazo el turno.
IF OBJECT_ID('dbo.Especialidades','U') IS NULL
BEGIN
    CREATE TABLE dbo.Especialidades (
        Id     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Especialidades PRIMARY KEY,
        Nombre NVARCHAR(60)      NOT NULL,
        CONSTRAINT UQ_Especialidades_Nombre UNIQUE (Nombre)
    );
    INSERT INTO dbo.Especialidades (Nombre) VALUES
        (N'Mozo'), (N'Cocina'), (N'Barra'), (N'DJ y sonido'), (N'Fotografía y video'),
        (N'Decoración'), (N'Recepción'), (N'Seguridad'), (N'Limpieza');
END
GO

-- Empleados que pueden asignarse a un evento. UserId vincula la ficha con la
-- cuenta con la que el empleado ingresa: es lo que le permite confirmar su
-- disponibilidad y consultar sus tareas. Es opcional (un empleado sin cuenta se
-- asigna igual, pero no puede responder por si mismo).
IF OBJECT_ID('dbo.Empleados','U') IS NULL
BEGIN
    CREATE TABLE dbo.Empleados (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Empleados PRIMARY KEY,
        Nombre         NVARCHAR(60)      NOT NULL,
        Apellido       NVARCHAR(60)      NOT NULL,
        Dni            NVARCHAR(20)      NOT NULL,
        EspecialidadId INT               NOT NULL,
        UserId         INT               NULL,
        Activo         BIT               NOT NULL CONSTRAINT DF_Empleados_Activo DEFAULT 1,
        CreatedAt      DATETIME          NOT NULL CONSTRAINT DF_Empleados_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT UQ_Empleados_Dni UNIQUE (Dni),
        CONSTRAINT FK_Empleados_Especialidad FOREIGN KEY (EspecialidadId) REFERENCES dbo.Especialidades(Id),
        CONSTRAINT FK_Empleados_User         FOREIGN KEY (UserId)         REFERENCES dbo.Users(Id)
    );
    CREATE INDEX IX_Empleados_EspecialidadId ON dbo.Empleados(EspecialidadId);
END
GO

-- Una cuenta representa a un solo empleado. Indice unico filtrado, en bloque
-- propio (ver UX_Clientes_Dni): exige QUOTED_IDENTIFIER ON.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Empleados_UserId' AND object_id = OBJECT_ID('dbo.Empleados'))
    CREATE UNIQUE INDEX UX_Empleados_UserId ON dbo.Empleados(UserId) WHERE UserId IS NOT NULL;
GO

-- Estado de coordinacion del evento: segundo eje de la reserva, independiente
-- del estado comercial. Lo mantiene la capa de negocio del Proceso 2; las
-- escrituras de la reserva no lo tocan. Como VenceEl y CantidadInvitados, NO
-- entra en el digito verificador: es un dato operativo y sumarlo invalidaria
-- los DV ya calculados sobre las reservas existentes.
IF COL_LENGTH('dbo.Reservas','EstadoCoordinacion') IS NULL
    ALTER TABLE dbo.Reservas ADD EstadoCoordinacion NVARCHAR(20) NOT NULL
        CONSTRAINT DF_Reservas_EstadoCoordinacion DEFAULT 'SIN_ASIGNAR';
GO

-- Dominio cerrado y sensible a mayusculas, como el de Estado (ver
-- CK_Reservas_Estado). Va en lote propio: la columna tiene que existir antes.
-- Como aquella, se agrega solo si todas las filas ya cumplen el dominio exacto:
-- el script no debe cortarse sobre una base con un valor escrito a mano.
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Reservas_EstadoCoordinacion' AND parent_object_id = OBJECT_ID(N'dbo.Reservas'))
   AND NOT EXISTS (SELECT 1 FROM dbo.Reservas
                   WHERE EstadoCoordinacion COLLATE Latin1_General_BIN2 NOT IN (N'SIN_ASIGNAR', N'EN_COORDINACION', N'LISTO', N'EN_EJECUCION', N'CERRADO'))
    ALTER TABLE dbo.Reservas WITH CHECK ADD CONSTRAINT CK_Reservas_EstadoCoordinacion
        CHECK (EstadoCoordinacion COLLATE Latin1_General_BIN2 IN (N'SIN_ASIGNAR', N'EN_COORDINACION', N'LISTO', N'EN_EJECUCION', N'CERRADO'));
GO

-- Asignacion de un empleado a un evento, con su rol, su franja de trabajo y la
-- respuesta que dio. Hay una sola por empleado y por reserva. La franja se
-- interpreta sobre la fecha del evento; si la hora de fin no es posterior a la
-- de inicio, termina al dia siguiente (un turno de 21:00 a 03:00).
IF OBJECT_ID('dbo.AsignacionesPersonal','U') IS NULL
BEGIN
    CREATE TABLE dbo.AsignacionesPersonal (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AsignacionesPersonal PRIMARY KEY,
        ReservaId         INT               NOT NULL,
        EmpleadoId        INT               NOT NULL,
        RolAsignado       NVARCHAR(60)      NOT NULL,
        HoraInicio        TIME(0)           NOT NULL,
        HoraFin           TIME(0)           NOT NULL,
        Estado            NVARCHAR(20)      NOT NULL CONSTRAINT DF_AsignacionesPersonal_Estado DEFAULT 'PENDIENTE',
        FechaConfirmacion DATETIME          NULL,   -- cuando respondio el empleado (acepto o rechazo)
        MotivoRechazo     NVARCHAR(250)     NULL,
        CreatedAt         DATETIME          NOT NULL CONSTRAINT DF_AsignacionesPersonal_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT UQ_AsignacionesPersonal_ReservaEmpleado UNIQUE (ReservaId, EmpleadoId),
        CONSTRAINT FK_AsignacionesPersonal_Reserva  FOREIGN KEY (ReservaId)  REFERENCES dbo.Reservas(Id),
        CONSTRAINT FK_AsignacionesPersonal_Empleado FOREIGN KEY (EmpleadoId) REFERENCES dbo.Empleados(Id),
        CONSTRAINT CK_AsignacionesPersonal_Estado CHECK (Estado COLLATE Latin1_General_BIN2 IN (N'PENDIENTE', N'CONFIRMADA', N'RECHAZADA')),
        CONSTRAINT CK_AsignacionesPersonal_Franja CHECK (HoraInicio <> HoraFin)
    );
    CREATE INDEX IX_AsignacionesPersonal_EmpleadoId ON dbo.AsignacionesPersonal(EmpleadoId);
END
GO

-- Los conteos que deciden el estado de coordinacion (cuantas asignaciones tiene
-- el evento y cuantas estan confirmadas) se resuelven con las claves de SU
-- reserva: dos escrituras simultaneas sobre eventos distintos no se esperan una
-- a la otra. En bloque propio: se agrega tambien a una base ya creada.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AsignacionesPersonal_Reserva_Estado' AND object_id = OBJECT_ID('dbo.AsignacionesPersonal'))
    CREATE INDEX IX_AsignacionesPersonal_Reserva_Estado ON dbo.AsignacionesPersonal(ReservaId, Estado);
GO

-- Cronograma de la jornada: uno solo por reserva.
IF OBJECT_ID('dbo.Cronogramas','U') IS NULL
BEGIN
    CREATE TABLE dbo.Cronogramas (
        Id        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Cronogramas PRIMARY KEY,
        ReservaId INT               NOT NULL,
        CreatedAt DATETIME          NOT NULL CONSTRAINT DF_Cronogramas_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT UQ_Cronogramas_Reserva UNIQUE (ReservaId),
        CONSTRAINT FK_Cronogramas_Reserva FOREIGN KEY (ReservaId) REFERENCES dbo.Reservas(Id)
    );
END
GO

-- Actividades del cronograma: que pasa, a que hora, cuanto dura y quien
-- responde por el tramo. Orden es el que les dio el coordinador (una jornada
-- que cruza la medianoche no se puede ordenar por hora).
IF OBJECT_ID('dbo.CronogramaActividades','U') IS NULL
BEGIN
    CREATE TABLE dbo.CronogramaActividades (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CronogramaActividades PRIMARY KEY,
        CronogramaId    INT               NOT NULL,
        Orden           INT               NOT NULL,
        Hora            TIME(0)           NOT NULL,
        Descripcion     NVARCHAR(150)     NOT NULL,
        ResponsableId   INT               NOT NULL,
        DuracionMinutos INT               NOT NULL,
        CONSTRAINT FK_CronogramaActividades_Cronograma  FOREIGN KEY (CronogramaId)  REFERENCES dbo.Cronogramas(Id),
        CONSTRAINT FK_CronogramaActividades_Responsable FOREIGN KEY (ResponsableId) REFERENCES dbo.Empleados(Id),
        CONSTRAINT CK_CronogramaActividades_Duracion CHECK (DuracionMinutos BETWEEN 1 AND 1440)
    );
    CREATE INDEX IX_CronogramaActividades_CronogramaId ON dbo.CronogramaActividades(CronogramaId);
    CREATE INDEX IX_CronogramaActividades_ResponsableId ON dbo.CronogramaActividades(ResponsableId);
END
GO

-- Tareas especificas de cada integrante del equipo dentro del cronograma.
IF OBJECT_ID('dbo.Tareas','U') IS NULL
BEGIN
    CREATE TABLE dbo.Tareas (
        Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tareas PRIMARY KEY,
        CronogramaId INT               NOT NULL,
        EmpleadoId   INT               NOT NULL,
        Descripcion  NVARCHAR(200)     NOT NULL,
        HoraInicio   TIME(0)           NOT NULL,
        HoraFin      TIME(0)           NOT NULL,
        Prioridad    NVARCHAR(10)      NOT NULL CONSTRAINT DF_Tareas_Prioridad DEFAULT 'MEDIA',
        Recursos     NVARCHAR(200)     NULL,
        CreatedAt    DATETIME          NOT NULL CONSTRAINT DF_Tareas_CreatedAt DEFAULT GETDATE(),
        CONSTRAINT FK_Tareas_Cronograma FOREIGN KEY (CronogramaId) REFERENCES dbo.Cronogramas(Id),
        CONSTRAINT FK_Tareas_Empleado   FOREIGN KEY (EmpleadoId)   REFERENCES dbo.Empleados(Id),
        CONSTRAINT CK_Tareas_Prioridad CHECK (Prioridad COLLATE Latin1_General_BIN2 IN (N'ALTA', N'MEDIA', N'BAJA')),
        CONSTRAINT CK_Tareas_Franja CHECK (HoraInicio <> HoraFin)
    );
    CREATE INDEX IX_Tareas_CronogramaId ON dbo.Tareas(CronogramaId);
    CREATE INDEX IX_Tareas_EmpleadoId ON dbo.Tareas(EmpleadoId);
END
GO

-- Incidencias: lo que se sale del plan durante la ejecucion del evento.
IF OBJECT_ID('dbo.Incidencias','U') IS NULL
BEGIN
    CREATE TABLE dbo.Incidencias (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Incidencias PRIMARY KEY,
        ReservaId         INT               NOT NULL,
        FechaHora         DATETIME          NOT NULL CONSTRAINT DF_Incidencias_FechaHora DEFAULT GETDATE(),
        Tipo              NVARCHAR(20)      NOT NULL,
        Descripcion       NVARCHAR(500)     NOT NULL,
        EmpleadoReportaId INT               NULL,
        Estado            NVARCHAR(10)      NOT NULL CONSTRAINT DF_Incidencias_Estado DEFAULT 'ABIERTA',
        Resolucion        NVARCHAR(250)     NULL,
        FechaResolucion   DATETIME          NULL,
        CONSTRAINT FK_Incidencias_Reserva  FOREIGN KEY (ReservaId)         REFERENCES dbo.Reservas(Id),
        CONSTRAINT FK_Incidencias_Empleado FOREIGN KEY (EmpleadoReportaId) REFERENCES dbo.Empleados(Id),
        CONSTRAINT CK_Incidencias_Tipo CHECK (Tipo COLLATE Latin1_General_BIN2 IN (N'PERSONAL', N'SERVICIO', N'EQUIPAMIENTO', N'HORARIO', N'INVITADOS', N'OTRO')),
        CONSTRAINT CK_Incidencias_Estado CHECK (Estado COLLATE Latin1_General_BIN2 IN (N'ABIERTA', N'RESUELTA'))
    );
    CREATE INDEX IX_Incidencias_ReservaId ON dbo.Incidencias(ReservaId);
    CREATE INDEX IX_Incidencias_EmpleadoReportaId ON dbo.Incidencias(EmpleadoReportaId);
END
GO

-- ---------------------------------------------------------------------------
-- Permisos del Proceso 2 (hojas del Composite). La gestion de empleados cuelga
-- de "Administracion del sistema", con los demas catalogos; las operaciones
-- sobre el evento, de un grupo propio. Se insertan solo si faltan y el perfil
-- Administrador recibe todo lo nuevo.
-- EJECUCION_SUPERVISAR es la responsabilidad del supervisor de operaciones
-- (G04): el perfil Supervisor lo recibe la vez que el permiso se da de alta, con
-- el mismo criterio que la composicion de fabrica de los perfiles (solo al
-- sembrar: lo que un administrador le quite despues se conserva).
-- ---------------------------------------------------------------------------
-- Todo el lote va en una transaccion: si se corta, no queda el permiso dado de
-- alta sin su asignacion (la corrida siguiente ya no lo veria como nuevo).
BEGIN TRY
BEGIN TRANSACTION;

DECLARE @raizOp INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Administracion' AND EsGrupo = 1);
DECLARE @gAdminSysOp INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Administracion del sistema' AND EsGrupo = 1);

DECLARE @gOperaciones INT = (SELECT TOP 1 Id FROM dbo.Permisos WHERE Nombre = N'Operaciones' AND EsGrupo = 1);
IF @gOperaciones IS NULL
BEGIN
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Operaciones', N'Coordinacion y ejecucion de los eventos', 1, NULL, @raizOp);
    SET @gOperaciones = SCOPE_IDENTITY();
END

IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'EMPLEADOS_GESTION')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Gestion de Empleados', N'Alta y modificacion del personal', 0, N'EMPLEADOS_GESTION', @gAdminSysOp);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'PERSONAL_ASIGNAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Asignar Personal', N'Asignar empleados a un evento confirmado', 0, N'PERSONAL_ASIGNAR', @gOperaciones);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'DISPONIBILIDAD_CONFIRMAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Confirmar Disponibilidad', N'Aceptar o rechazar los turnos propios', 0, N'DISPONIBILIDAD_CONFIRMAR', @gOperaciones);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'CRONOGRAMA_GESTION')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Gestionar Cronograma', N'Generar y modificar el cronograma del evento', 0, N'CRONOGRAMA_GESTION', @gOperaciones);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'TAREAS_ASIGNAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Asignar Tareas', N'Asignar tareas al personal confirmado', 0, N'TAREAS_ASIGNAR', @gOperaciones);
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'AGENDA_CONSULTAR')
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Consultar Agenda', N'Consultar las asignaciones y las tareas propias', 0, N'AGENDA_CONSULTAR', @gOperaciones);

DECLARE @supervisarNuevo BIT = 0;
IF NOT EXISTS (SELECT 1 FROM dbo.Permisos WHERE Clave = N'EJECUCION_SUPERVISAR')
BEGIN
    INSERT INTO dbo.Permisos (Nombre, Descripcion, EsGrupo, Clave, PermisoPadreId)
        VALUES (N'Supervisar Ejecucion', N'Iniciar y cerrar la ejecucion del evento y registrar incidencias', 0, N'EJECUCION_SUPERVISAR', @gOperaciones);
    SET @supervisarNuevo = 1;
END

-- Acceso total del Administrador: se le asigna todo permiso que le falte.
INSERT INTO dbo.PerfilPermiso (PerfilId, PermisoId)
SELECT p.Id, pe.Id
FROM dbo.Perfiles p
CROSS JOIN dbo.Permisos pe
WHERE p.Nombre = N'Administrador'
  AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermiso pp
                  WHERE pp.PerfilId = p.Id AND pp.PermisoId = pe.Id);

IF @supervisarNuevo = 1
    INSERT INTO dbo.PerfilPermiso (PerfilId, PermisoId)
    SELECT p.Id, pe.Id
    FROM dbo.Perfiles p
    JOIN dbo.Permisos pe ON pe.Clave = N'EJECUCION_SUPERVISAR'
    WHERE p.Nombre = N'Supervisor'
      AND NOT EXISTS (SELECT 1 FROM dbo.PerfilPermiso pp
                      WHERE pp.PerfilId = p.Id AND pp.PermisoId = pe.Id);

-- Descripcion de fabrica del perfil Supervisor: ahora nombra tambien la
-- supervision de la ejecucion. Se corrige solo si conserva exactamente el texto
-- sembrado antes del Proceso 2 y el perfil tiene el permiso.
UPDATE p SET Descripcion = N'Incluye al perfil Vendedor y suma la consulta de auditoría, la anulación de pagos y la supervisión de la ejecución de los eventos'
FROM dbo.Perfiles p
WHERE p.Nombre = N'Supervisor'
  AND p.Descripcion = N'Incluye al perfil Vendedor y suma la consulta de auditoría y la anulación de pagos' COLLATE Latin1_General_BIN2
  AND DATALENGTH(p.Descripcion) = DATALENGTH(N'Incluye al perfil Vendedor y suma la consulta de auditoría y la anulación de pagos')
  AND EXISTS (SELECT 1 FROM dbo.PerfilPermiso pp JOIN dbo.Permisos pe ON pe.Id = pp.PermisoId
              WHERE pp.PerfilId = p.Id AND pe.Clave = N'EJECUCION_SUPERVISAR');

COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO

-- ---------------------------------------------------------------------------
-- Perfiles operativos del Proceso 2 (roles de G04):
--   Coordinador : arma el equipo, el cronograma y las tareas de cada evento, y
--                 gestiona la ficha del personal.
--   Empleado    : confirma su disponibilidad y consulta su agenda.
-- La supervision de la ejecucion la tiene el perfil Supervisor (bloque de
-- arriba). Mismo criterio que los perfiles del Proceso 1: el perfil que falte
-- se da de alta y SOLO ese perfil recibe su composicion de fabrica; uno que ya
-- existe no se toca (tampoco su descripcion: estos dos perfiles no tienen una
-- version anterior sin ella). Si la base ya tenia un perfil propio con uno de
-- esos nombres y sin los permisos del Proceso 2, el script lo informa: ese
-- perfil queda como estaba y sus cuentas no ven las pantallas nuevas hasta que
-- un administrador le asigne los permisos.
-- ---------------------------------------------------------------------------
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @PerfilesOperativos TABLE (
        Accion NVARCHAR(10) COLLATE DATABASE_DEFAULT NOT NULL,
        Id     INT NOT NULL,
        Nombre NVARCHAR(80) COLLATE DATABASE_DEFAULT NOT NULL
    );

    ;WITH Perf(Nombre, Descripcion) AS (
        SELECT * FROM (VALUES
            (N'Coordinador', N'Coordina los eventos: personal, asignaciones, cronograma y tareas'),
            (N'Empleado',    N'Confirma su disponibilidad y consulta sus asignaciones y tareas')
        ) AS v(Nombre, Descripcion)
    )
    MERGE dbo.Perfiles AS p
    USING Perf AS s ON p.Nombre = s.Nombre
    WHEN NOT MATCHED THEN INSERT (Nombre, Descripcion) VALUES (s.Nombre, s.Descripcion)
    OUTPUT $action, inserted.Id, inserted.Nombre INTO @PerfilesOperativos (Accion, Id, Nombre);

    -- Un perfil previo con ese nombre se avisa solo si de verdad le falta el permiso:
    -- cuentan los permisos EFECTIVOS, los que le llegan por un grupo asignado o por un
    -- perfil incluido (mirando solo la hoja directa, el aviso salia en cada corrida
    -- para un perfil compuesto con el grupo Operaciones).
    DECLARE @perfilesPrevios NVARCHAR(200) = NULL;
    ;WITH Alcance AS (
        SELECT p.Id AS Raiz, p.Id AS PerfilId
        FROM dbo.Perfiles p
        WHERE p.Nombre IN (N'Coordinador', N'Empleado')
          AND NOT EXISTS (SELECT 1 FROM @PerfilesOperativos n WHERE n.Id = p.Id)
        UNION ALL
        SELECT a.Raiz, pi.PerfilHijoId
        FROM Alcance a JOIN dbo.PerfilIncluido pi ON pi.PerfilPadreId = a.PerfilId
    ),
    Nodos AS (
        SELECT a.Raiz, pp.PermisoId
        FROM Alcance a JOIN dbo.PerfilPermiso pp ON pp.PerfilId = a.PerfilId
        UNION ALL
        SELECT n.Raiz, h.Id
        FROM Nodos n JOIN dbo.Permisos h ON h.PermisoPadreId = n.PermisoId
    )
    SELECT @perfilesPrevios = COALESCE(@perfilesPrevios + N', ', N'') + p.Nombre
    FROM dbo.Perfiles p
    WHERE p.Nombre IN (N'Coordinador', N'Empleado')
      AND NOT EXISTS (SELECT 1 FROM @PerfilesOperativos n WHERE n.Id = p.Id)
      AND NOT EXISTS (SELECT 1 FROM Nodos x JOIN dbo.Permisos pe ON pe.Id = x.PermisoId
                      WHERE x.Raiz = p.Id
                        AND pe.Clave = CASE p.Nombre WHEN N'Coordinador' THEN N'PERSONAL_ASIGNAR'
                                                     ELSE N'DISPONIBILIDAD_CONFIRMAR' END)
    OPTION (MAXRECURSION 200);
    IF @perfilesPrevios IS NOT NULL
        RAISERROR(N'schema.sql: la base ya tenia un perfil con el nombre %s y sin los permisos del Proceso 2. No se modifico: asignele los permisos desde Gestion de Perfiles.', 10, 1, @perfilesPrevios) WITH NOWAIT;

    ;WITH Asig(Perfil, Clave) AS (
        SELECT * FROM (VALUES
            (N'Coordinador', N'EMPLEADOS_GESTION'),
            (N'Coordinador', N'PERSONAL_ASIGNAR'),
            (N'Coordinador', N'CRONOGRAMA_GESTION'),
            (N'Coordinador', N'TAREAS_ASIGNAR'),
            (N'Empleado',    N'DISPONIBILIDAD_CONFIRMAR'),
            (N'Empleado',    N'AGENDA_CONSULTAR')
        ) AS v(Perfil, Clave)
    )
    INSERT INTO dbo.PerfilPermiso (PerfilId, PermisoId)
    SELECT n.Id, pe.Id
    FROM Asig a
    JOIN @PerfilesOperativos n ON n.Nombre = a.Perfil AND n.Accion = N'INSERT'
    JOIN dbo.Permisos pe ON pe.Clave = a.Clave
    WHERE NOT EXISTS (SELECT 1 FROM dbo.PerfilPermiso pp
                      WHERE pp.PerfilId = n.Id AND pp.PermisoId = pe.Id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO

-- ---------------------------------------------------------------------------
-- Proceso 2 (RFN2): traducciones ES/EN/PT de las pantallas de empleados,
-- operaciones, personal, cronograma, tareas, supervision y agenda, de los nombres
-- del arbol de permisos y de los modulos y acciones que asienta la bitacora.
-- Idempotente: solo inserta las claves que falten (una traduccion editada por el
-- usuario se conserva) y una clave repetida no rompe el INSERT (GROUP BY).
-- ---------------------------------------------------------------------------
;WITH Txt(Codigo, Clave, Texto) AS (
    SELECT * FROM (VALUES
        -- Empleados (ucEmpleados)
        (N'ES', N'EMP_COL_CUENTA', N'Cuenta'), (N'EN', N'EMP_COL_CUENTA', N'Account'), (N'PT', N'EMP_COL_CUENTA', N'Conta'),
        (N'ES', N'EMP_COL_EMPLEADO', N'Empleado'), (N'EN', N'EMP_COL_EMPLEADO', N'Employee'), (N'PT', N'EMP_COL_EMPLEADO', N'Funcionário'),
        (N'ES', N'EMP_COUNT', N'empleado(s)'), (N'EN', N'EMP_COUNT', N'employee(s)'), (N'PT', N'EMP_COUNT', N'funcionário(s)'),
        (N'ES', N'EMP_ESPECIALIDAD', N'Especialidad'), (N'EN', N'EMP_ESPECIALIDAD', N'Specialty'), (N'PT', N'EMP_ESPECIALIDAD', N'Especialidade'),
        (N'ES', N'EMP_FORM_EDITAR', N'Editar empleado'), (N'EN', N'EMP_FORM_EDITAR', N'Edit employee'), (N'PT', N'EMP_FORM_EDITAR', N'Editar funcionário'),
        (N'ES', N'EMP_NUEVO', N'Nuevo empleado'), (N'EN', N'EMP_NUEVO', N'New employee'), (N'PT', N'EMP_NUEVO', N'Novo funcionário'),
        (N'ES', N'EMP_SIN_CUENTA', N'(sin cuenta)'), (N'EN', N'EMP_SIN_CUENTA', N'(no account)'), (N'PT', N'EMP_SIN_CUENTA', N'(sem conta)'),
        (N'ES', N'MSG_EMP_CON_TURNOS', N'El empleado tiene turnos en eventos confirmados: quítelo de esos eventos, o espere al cierre de los que están en ejecución, antes de darlo de baja.'), (N'EN', N'MSG_EMP_CON_TURNOS', N'The employee has shifts in confirmed events: remove them from those events, or wait until the events in progress are closed, before deactivating.'), (N'PT', N'MSG_EMP_CON_TURNOS', N'O funcionário tem turnos em eventos confirmados: remova-o desses eventos, ou aguarde o encerramento dos que estão em execução, antes de desativá-lo.'),
        (N'ES', N'MSG_EMP_CUENTA', N'La cuenta elegida ya no existe.'), (N'EN', N'MSG_EMP_CUENTA', N'The chosen account no longer exists.'), (N'PT', N'MSG_EMP_CUENTA', N'A conta escolhida não existe mais.'),
        (N'ES', N'MSG_EMP_CUENTA_DUP', N'Esa cuenta ya está vinculada a otro empleado.'), (N'EN', N'MSG_EMP_CUENTA_DUP', N'That account is already linked to another employee.'), (N'PT', N'MSG_EMP_CUENTA_DUP', N'Essa conta já está vinculada a outro funcionário.'),
        (N'ES', N'MSG_EMP_DNI', N'Ingrese el DNI del empleado: solo números (7 dígitos o más).'), (N'EN', N'MSG_EMP_DNI', N'Enter the employee''s ID number: digits only (7 or more).'), (N'PT', N'MSG_EMP_DNI', N'Informe o documento do funcionário: só números (7 dígitos ou mais).'),
        (N'ES', N'MSG_EMP_DNI_DUP', N'Ya existe un empleado con ese DNI.'), (N'EN', N'MSG_EMP_DNI_DUP', N'An employee with that ID already exists.'), (N'PT', N'MSG_EMP_DNI_DUP', N'Já existe um funcionário com esse documento.'),
        (N'ES', N'MSG_EMP_ESPECIALIDAD', N'Seleccione la especialidad del empleado.'), (N'EN', N'MSG_EMP_ESPECIALIDAD', N'Select the employee''s specialty.'), (N'PT', N'MSG_EMP_ESPECIALIDAD', N'Selecione a especialidade do funcionário.'),
        (N'ES', N'MSG_EMP_LARGO', N'Dato muy largo: nombre y apellido admiten hasta 60 caracteres.'), (N'EN', N'MSG_EMP_LARGO', N'Value too long: first and last name allow up to 60 characters.'), (N'PT', N'MSG_EMP_LARGO', N'Dado longo demais: nome e sobrenome admitem até 60 caracteres.'),
        (N'ES', N'MSG_EMP_NOMBRE', N'Ingrese el nombre y el apellido del empleado.'), (N'EN', N'MSG_EMP_NOMBRE', N'Enter the employee''s first and last name.'), (N'PT', N'MSG_EMP_NOMBRE', N'Informe o nome e o sobrenome do funcionário.'),
        (N'ES', N'MSG_EMP_NOTFOUND', N'El empleado ya no existe.'), (N'EN', N'MSG_EMP_NOTFOUND', N'The employee no longer exists.'), (N'PT', N'MSG_EMP_NOTFOUND', N'O funcionário não existe mais.'),
        (N'ES', N'MSG_EMP_OK', N'Empleado guardado.'), (N'EN', N'MSG_EMP_OK', N'Employee saved.'), (N'PT', N'MSG_EMP_OK', N'Funcionário salvo.'),
        -- Operaciones de eventos (ucOperaciones)
        (N'ES', N'OPE_AVANCE_CRONOGRAMA', N'Cronograma: {0} actividad(es)'), (N'EN', N'OPE_AVANCE_CRONOGRAMA', N'Schedule: {0} activity(ies)'), (N'PT', N'OPE_AVANCE_CRONOGRAMA', N'Cronograma: {0} atividade(s)'),
        (N'ES', N'OPE_AVANCE_INCIDENCIAS', N'Incidencias: {0} ({1} abierta(s))'), (N'EN', N'OPE_AVANCE_INCIDENCIAS', N'Incidents: {0} ({1} open)'), (N'PT', N'OPE_AVANCE_INCIDENCIAS', N'Ocorrências: {0} ({1} aberta(s))'),
        (N'ES', N'OPE_AVANCE_PERSONAL', N'Personal: {0} asignado(s), {1} confirmado(s), {2} pendiente(s), {3} rechazado(s)'), (N'EN', N'OPE_AVANCE_PERSONAL', N'Staff: {0} assigned, {1} confirmed, {2} pending, {3} declined'), (N'PT', N'OPE_AVANCE_PERSONAL', N'Equipe: {0} atribuído(s), {1} confirmado(s), {2} pendente(s), {3} recusado(s)'),
        (N'ES', N'OPE_AVANCE_SIN_CRONOGRAMA', N'Cronograma: sin generar'), (N'EN', N'OPE_AVANCE_SIN_CRONOGRAMA', N'Schedule: not generated'), (N'PT', N'OPE_AVANCE_SIN_CRONOGRAMA', N'Cronograma: não gerado'),
        (N'ES', N'OPE_AVANCE_TAREAS', N'Tareas: {0}'), (N'EN', N'OPE_AVANCE_TAREAS', N'Tasks: {0}'), (N'PT', N'OPE_AVANCE_TAREAS', N'Tarefas: {0}'),
        (N'ES', N'OPE_COL_ESTADO', N'Coordinación'), (N'EN', N'OPE_COL_ESTADO', N'Coordination'), (N'PT', N'OPE_COL_ESTADO', N'Coordenação'),
        (N'ES', N'OPE_COL_PERSONAL', N'Personal'), (N'EN', N'OPE_COL_PERSONAL', N'Staff'), (N'PT', N'OPE_COL_PERSONAL', N'Equipe'),
        (N'ES', N'OPE_COUNT', N'evento(s) confirmado(s)'), (N'EN', N'OPE_COUNT', N'confirmed event(s)'), (N'PT', N'OPE_COUNT', N'evento(s) confirmado(s)'),
        (N'ES', N'OPE_EVENTO', N'Evento de la reserva'), (N'EN', N'OPE_EVENTO', N'Event of reservation'), (N'PT', N'OPE_EVENTO', N'Evento da reserva'),
        (N'ES', N'OPE_SERVICIOS', N'Servicios contratados'), (N'EN', N'OPE_SERVICIOS', N'Contracted services'), (N'PT', N'OPE_SERVICIOS', N'Serviços contratados'),
        (N'ES', N'OPE_SIN_EVENTOS', N'No hay reservas confirmadas para coordinar. Un evento aparece acá cuando su reserva queda confirmada.'), (N'EN', N'OPE_SIN_EVENTOS', N'There are no confirmed reservations to coordinate. An event shows up here once its reservation is confirmed.'), (N'PT', N'OPE_SIN_EVENTOS', N'Não há reservas confirmadas para coordenar. Um evento aparece aqui quando sua reserva fica confirmada.'),
        (N'ES', N'OPE_SIN_SELECCION', N'Seleccione un evento'), (N'EN', N'OPE_SIN_SELECCION', N'Select an event'), (N'PT', N'OPE_SIN_SELECCION', N'Selecione um evento'),
        (N'ES', N'OPE_SIN_SERVICIOS', N'(sin servicios)'), (N'EN', N'OPE_SIN_SERVICIOS', N'(no services)'), (N'PT', N'OPE_SIN_SERVICIOS', N'(sem serviços)'),
        -- Estados de coordinacion, de asignacion, prioridades e incidencias
        (N'ES', N'ASIG_CONFIRMADA', N'Confirmada'), (N'EN', N'ASIG_CONFIRMADA', N'Confirmed'), (N'PT', N'ASIG_CONFIRMADA', N'Confirmada'),
        (N'ES', N'ASIG_PENDIENTE', N'Pendiente'), (N'EN', N'ASIG_PENDIENTE', N'Pending'), (N'PT', N'ASIG_PENDIENTE', N'Pendente'),
        (N'ES', N'ASIG_RECHAZADA', N'Rechazada'), (N'EN', N'ASIG_RECHAZADA', N'Declined'), (N'PT', N'ASIG_RECHAZADA', N'Recusada'),
        (N'ES', N'COORD_CERRADO', N'Cerrado'), (N'EN', N'COORD_CERRADO', N'Closed'), (N'PT', N'COORD_CERRADO', N'Encerrado'),
        (N'ES', N'COORD_EN_COORDINACION', N'En coordinación'), (N'EN', N'COORD_EN_COORDINACION', N'In coordination'), (N'PT', N'COORD_EN_COORDINACION', N'Em coordenação'),
        (N'ES', N'COORD_EN_EJECUCION', N'En ejecución'), (N'EN', N'COORD_EN_EJECUCION', N'In progress'), (N'PT', N'COORD_EN_EJECUCION', N'Em execução'),
        (N'ES', N'COORD_EVENTO', N'Reserva #{0} · {1} · {2} · {3} · {4} invitados'), (N'EN', N'COORD_EVENTO', N'Reservation #{0} · {1} · {2} · {3} · {4} guests'), (N'PT', N'COORD_EVENTO', N'Reserva #{0} · {1} · {2} · {3} · {4} convidados'),
        (N'ES', N'COORD_LISTO', N'Listo'), (N'EN', N'COORD_LISTO', N'Ready'), (N'PT', N'COORD_LISTO', N'Pronto'),
        (N'ES', N'COORD_SIN_ASIGNAR', N'Sin asignar'), (N'EN', N'COORD_SIN_ASIGNAR', N'Unassigned'), (N'PT', N'COORD_SIN_ASIGNAR', N'Sem atribuir'),
        (N'ES', N'INC_EST_ABIERTA', N'Abierta'), (N'EN', N'INC_EST_ABIERTA', N'Open'), (N'PT', N'INC_EST_ABIERTA', N'Aberta'),
        (N'ES', N'INC_EST_RESUELTA', N'Resuelta'), (N'EN', N'INC_EST_RESUELTA', N'Resolved'), (N'PT', N'INC_EST_RESUELTA', N'Resolvida'),
        (N'ES', N'INC_TIPO_EQUIPAMIENTO', N'Equipamiento'), (N'EN', N'INC_TIPO_EQUIPAMIENTO', N'Equipment'), (N'PT', N'INC_TIPO_EQUIPAMIENTO', N'Equipamento'),
        (N'ES', N'INC_TIPO_HORARIO', N'Horario'), (N'EN', N'INC_TIPO_HORARIO', N'Timing'), (N'PT', N'INC_TIPO_HORARIO', N'Horário'),
        (N'ES', N'INC_TIPO_INVITADOS', N'Invitados'), (N'EN', N'INC_TIPO_INVITADOS', N'Guests'), (N'PT', N'INC_TIPO_INVITADOS', N'Convidados'),
        (N'ES', N'INC_TIPO_OTRO', N'Otro'), (N'EN', N'INC_TIPO_OTRO', N'Other'), (N'PT', N'INC_TIPO_OTRO', N'Outro'),
        (N'ES', N'INC_TIPO_PERSONAL', N'Personal'), (N'EN', N'INC_TIPO_PERSONAL', N'Staff'), (N'PT', N'INC_TIPO_PERSONAL', N'Equipe'),
        (N'ES', N'INC_TIPO_SERVICIO', N'Servicio'), (N'EN', N'INC_TIPO_SERVICIO', N'Service'), (N'PT', N'INC_TIPO_SERVICIO', N'Serviço'),
        (N'ES', N'PRIO_ALTA', N'Alta'), (N'EN', N'PRIO_ALTA', N'High'), (N'PT', N'PRIO_ALTA', N'Alta'),
        (N'ES', N'PRIO_BAJA', N'Baja'), (N'EN', N'PRIO_BAJA', N'Low'), (N'PT', N'PRIO_BAJA', N'Baixa'),
        (N'ES', N'PRIO_MEDIA', N'Media'), (N'EN', N'PRIO_MEDIA', N'Medium'), (N'PT', N'PRIO_MEDIA', N'Média'),
        -- Personal del evento (frmAsignarPersonal, CUN006)
        (N'ES', N'ASG_A', N'a'), (N'EN', N'ASG_A', N'to'), (N'PT', N'ASG_A', N'a'),
        (N'ES', N'ASG_ASIGNAR', N'Asignar'), (N'EN', N'ASG_ASIGNAR', N'Assign'), (N'PT', N'ASG_ASIGNAR', N'Atribuir'),
        (N'ES', N'ASG_COL_FRANJA', N'Franja'), (N'EN', N'ASG_COL_FRANJA', N'Time slot'), (N'PT', N'ASG_COL_FRANJA', N'Horário'),
        (N'ES', N'ASG_COL_MOTIVO', N'Motivo del rechazo'), (N'EN', N'ASG_COL_MOTIVO', N'Reason for declining'), (N'PT', N'ASG_COL_MOTIVO', N'Motivo da recusa'),
        (N'ES', N'ASG_COL_RESPUESTA', N'Respondió'), (N'EN', N'ASG_COL_RESPUESTA', N'Answered'), (N'PT', N'ASG_COL_RESPUESTA', N'Respondeu'),
        (N'ES', N'ASG_COL_ROL', N'Rol'), (N'EN', N'ASG_COL_ROL', N'Role'), (N'PT', N'ASG_COL_ROL', N'Função'),
        (N'ES', N'ASG_DE', N'de'), (N'EN', N'ASG_DE', N'from'), (N'PT', N'ASG_DE', N'de'),
        (N'ES', N'ASG_QUITAR_CONF', N'¿Quitar a {0} del equipo de este evento?'), (N'EN', N'ASG_QUITAR_CONF', N'Remove {0} from this event''s team?'), (N'PT', N'ASG_QUITAR_CONF', N'Remover {0} da equipe deste evento?'),
        (N'ES', N'ASG_RESUMEN', N'Asignados: {0}    Confirmados: {1}    Pendientes: {2}    Rechazados: {3}'), (N'EN', N'ASG_RESUMEN', N'Assigned: {0}    Confirmed: {1}    Pending: {2}    Declined: {3}'), (N'PT', N'ASG_RESUMEN', N'Atribuídos: {0}    Confirmados: {1}    Pendentes: {2}    Recusados: {3}'),
        (N'ES', N'ASG_ROL', N'Rol en el evento'), (N'EN', N'ASG_ROL', N'Role in the event'), (N'PT', N'ASG_ROL', N'Função no evento'),
        (N'ES', N'ASG_TITULO', N'Personal del evento'), (N'EN', N'ASG_TITULO', N'Event staff'), (N'PT', N'ASG_TITULO', N'Equipe do evento'),
        (N'ES', N'ASG_TODAS', N'(todas las especialidades)'), (N'EN', N'ASG_TODAS', N'(all specialties)'), (N'PT', N'ASG_TODAS', N'(todas as especialidades)'),
        -- Cronograma del evento (frmCronograma, CUN008)
        (N'ES', N'CRO_ACTIVIDAD', N'Actividad'), (N'EN', N'CRO_ACTIVIDAD', N'Activity'), (N'PT', N'CRO_ACTIVIDAD', N'Atividade'),
        (N'ES', N'CRO_AGREGAR', N'Agregar'), (N'EN', N'CRO_AGREGAR', N'Add'), (N'PT', N'CRO_AGREGAR', N'Adicionar'),
        (N'ES', N'CRO_BAJAR', N'Bajar'), (N'EN', N'CRO_BAJAR', N'Down'), (N'PT', N'CRO_BAJAR', N'Descer'),
        (N'ES', N'CRO_COL_DURACION', N'Duración (min)'), (N'EN', N'CRO_COL_DURACION', N'Duration (min)'), (N'PT', N'CRO_COL_DURACION', N'Duração (min)'),
        (N'ES', N'CRO_COL_HORA', N'Hora'), (N'EN', N'CRO_COL_HORA', N'Time'), (N'PT', N'CRO_COL_HORA', N'Hora'),
        (N'ES', N'CRO_COL_RESPONSABLE', N'Responsable'), (N'EN', N'CRO_COL_RESPONSABLE', N'In charge'), (N'PT', N'CRO_COL_RESPONSABLE', N'Responsável'),
        (N'ES', N'CRO_DESCARTAR', N'El cronograma tiene cambios sin guardar. ¿Descartarlos y cerrar?'), (N'EN', N'CRO_DESCARTAR', N'The schedule has unsaved changes. Discard them and close?'), (N'PT', N'CRO_DESCARTAR', N'O cronograma tem alterações não salvas. Descartá-las e fechar?'),
        (N'ES', N'CRO_ELIMINAR', N'Eliminar cronograma'), (N'EN', N'CRO_ELIMINAR', N'Delete schedule'), (N'PT', N'CRO_ELIMINAR', N'Excluir cronograma'),
        (N'ES', N'CRO_ELIMINAR_CONF', N'¿Eliminar el cronograma de este evento? La operación no se puede deshacer.'), (N'EN', N'CRO_ELIMINAR_CONF', N'Delete this event''s schedule? This cannot be undone.'), (N'PT', N'CRO_ELIMINAR_CONF', N'Excluir o cronograma deste evento? A operação não pode ser desfeita.'),
        (N'ES', N'CRO_GENERAR', N'Generar cronograma'), (N'EN', N'CRO_GENERAR', N'Generate schedule'), (N'PT', N'CRO_GENERAR', N'Gerar cronograma'),
        (N'ES', N'CRO_GUARDAR', N'Guardar cambios'), (N'EN', N'CRO_GUARDAR', N'Save changes'), (N'PT', N'CRO_GUARDAR', N'Salvar alterações'),
        (N'ES', N'CRO_MIN', N'min'), (N'EN', N'CRO_MIN', N'min'), (N'PT', N'CRO_MIN', N'min'),
        (N'ES', N'CRO_SUBIR', N'Subir'), (N'EN', N'CRO_SUBIR', N'Up'), (N'PT', N'CRO_SUBIR', N'Subir'),
        (N'ES', N'CRO_TITULO', N'Cronograma del evento'), (N'EN', N'CRO_TITULO', N'Event schedule'), (N'PT', N'CRO_TITULO', N'Cronograma do evento'),
        (N'ES', N'MSG_CRO_DESCRIPCION', N'Ingrese la descripción de la actividad.'), (N'EN', N'MSG_CRO_DESCRIPCION', N'Enter the activity description.'), (N'PT', N'MSG_CRO_DESCRIPCION', N'Informe a descrição da atividade.'),
        (N'ES', N'MSG_CRO_GENERADO', N'Cronograma generado.'), (N'EN', N'MSG_CRO_GENERADO', N'Schedule generated.'), (N'PT', N'MSG_CRO_GENERADO', N'Cronograma gerado.'),
        (N'ES', N'MSG_CRO_GUARDADO', N'Cronograma guardado.'), (N'EN', N'MSG_CRO_GUARDADO', N'Schedule saved.'), (N'PT', N'MSG_CRO_GUARDADO', N'Cronograma salvo.'),
        -- Tareas del evento (frmTareas, CUN009)
        (N'ES', N'MSG_TAR_DESCRIPCION', N'Ingrese la tarea a realizar.'), (N'EN', N'MSG_TAR_DESCRIPCION', N'Enter the task to be done.'), (N'PT', N'MSG_TAR_DESCRIPCION', N'Informe a tarefa a realizar.'),
        (N'ES', N'MSG_TAR_SIN_EQUIPO', N'No hay personal confirmado al que asignarle tareas.'), (N'EN', N'MSG_TAR_SIN_EQUIPO', N'There is no confirmed staff to assign tasks to.'), (N'PT', N'MSG_TAR_SIN_EQUIPO', N'Não há equipe confirmada à qual atribuir tarefas.'),
        (N'ES', N'TAR_COL_PRIORIDAD', N'Prioridad'), (N'EN', N'TAR_COL_PRIORIDAD', N'Priority'), (N'PT', N'TAR_COL_PRIORIDAD', N'Prioridade'),
        (N'ES', N'TAR_COL_RECURSOS', N'Recursos'), (N'EN', N'TAR_COL_RECURSOS', N'Resources'), (N'PT', N'TAR_COL_RECURSOS', N'Recursos'),
        (N'ES', N'TAR_COL_TAREA', N'Tarea'), (N'EN', N'TAR_COL_TAREA', N'Task'), (N'PT', N'TAR_COL_TAREA', N'Tarefa'),
        (N'ES', N'TAR_DESCRIPCION', N'Tarea a realizar'), (N'EN', N'TAR_DESCRIPCION', N'Task to be done'), (N'PT', N'TAR_DESCRIPCION', N'Tarefa a realizar'),
        (N'ES', N'TAR_QUITAR_CONF', N'¿Quitar la tarea de {0}?'), (N'EN', N'TAR_QUITAR_CONF', N'Remove the task of {0}?'), (N'PT', N'TAR_QUITAR_CONF', N'Remover a tarefa de {0}?'),
        (N'ES', N'TAR_RECURSOS', N'Recursos necesarios (opcional)'), (N'EN', N'TAR_RECURSOS', N'Resources needed (optional)'), (N'PT', N'TAR_RECURSOS', N'Recursos necessários (opcional)'),
        (N'ES', N'TAR_RESUMEN', N'Tareas asignadas: {0}'), (N'EN', N'TAR_RESUMEN', N'Tasks assigned: {0}'), (N'PT', N'TAR_RESUMEN', N'Tarefas atribuídas: {0}'),
        (N'ES', N'TAR_TITULO', N'Tareas del evento'), (N'EN', N'TAR_TITULO', N'Event tasks'), (N'PT', N'TAR_TITULO', N'Tarefas do evento'),
        -- Supervision de la ejecucion (frmSupervision, CUN011)
        (N'ES', N'MSG_SUP_DESCRIPCION', N'Describa la incidencia.'), (N'EN', N'MSG_SUP_DESCRIPCION', N'Describe the incident.'), (N'PT', N'MSG_SUP_DESCRIPCION', N'Descreva a ocorrência.'),
        (N'ES', N'SUP_AVISO_EJECUCION', N'Evento en ejecución: registre lo que se salga del plan.'), (N'EN', N'SUP_AVISO_EJECUCION', N'Event in progress: record anything that goes off plan.'), (N'PT', N'SUP_AVISO_EJECUCION', N'Evento em execução: registre o que sair do plano.'),
        (N'ES', N'SUP_AVISO_LISTO', N'El evento está listo: inicie la ejecución cuando comience.'), (N'EN', N'SUP_AVISO_LISTO', N'The event is ready: start the execution when it begins.'), (N'PT', N'SUP_AVISO_LISTO', N'O evento está pronto: inicie a execução quando começar.'),
        (N'ES', N'SUP_CERRAR_CONF', N'¿Cerrar el evento? Un evento cerrado ya no admite cambios ni incidencias.'), (N'EN', N'SUP_CERRAR_CONF', N'Close the event? A closed event no longer accepts changes or incidents.'), (N'PT', N'SUP_CERRAR_CONF', N'Encerrar o evento? Um evento encerrado não admite mais alterações nem ocorrências.'),
        (N'ES', N'SUP_CERRAR_EVENTO', N'Cerrar evento'), (N'EN', N'SUP_CERRAR_EVENTO', N'Close event'), (N'PT', N'SUP_CERRAR_EVENTO', N'Encerrar evento'),
        (N'ES', N'SUP_COL_REPORTA', N'Informó'), (N'EN', N'SUP_COL_REPORTA', N'Reported by'), (N'PT', N'SUP_COL_REPORTA', N'Informou'),
        (N'ES', N'SUP_COL_RESOLUCION', N'Resolución'), (N'EN', N'SUP_COL_RESOLUCION', N'Resolution'), (N'PT', N'SUP_COL_RESOLUCION', N'Resolução'),
        (N'ES', N'SUP_COL_TIPO', N'Tipo'), (N'EN', N'SUP_COL_TIPO', N'Type'), (N'PT', N'SUP_COL_TIPO', N'Tipo'),
        (N'ES', N'SUP_CRONOGRAMA', N'Cronograma'), (N'EN', N'SUP_CRONOGRAMA', N'Schedule'), (N'PT', N'SUP_CRONOGRAMA', N'Cronograma'),
        (N'ES', N'SUP_DESCRIPCION', N'Qué pasó'), (N'EN', N'SUP_DESCRIPCION', N'What happened'), (N'PT', N'SUP_DESCRIPCION', N'O que aconteceu'),
        (N'ES', N'SUP_FUERA_FECHA', N'El evento está agendado para el {0} y hoy es {1}. Al iniciar la ejecución, el plan y la reserva quedan congelados y no se puede volver atrás. ¿Iniciar igual?'), (N'EN', N'SUP_FUERA_FECHA', N'The event is scheduled for {0} and today is {1}. Once the execution starts, the plan and the booking are frozen and this cannot be undone. Start anyway?'), (N'PT', N'SUP_FUERA_FECHA', N'O evento está agendado para {0} e hoje é {1}. Ao iniciar a execução, o plano e a reserva ficam congelados e não é possível voltar atrás. Iniciar mesmo assim?'),
        (N'ES', N'SUP_INICIAR_CONF', N'¿Iniciar la ejecución del evento? Desde ese momento el plan y la reserva quedan congelados y no se puede volver atrás.'), (N'EN', N'SUP_INICIAR_CONF', N'Start the execution of the event? From then on the plan and the booking are frozen and this cannot be undone.'), (N'PT', N'SUP_INICIAR_CONF', N'Iniciar a execução do evento? A partir desse momento o plano e a reserva ficam congelados e não é possível voltar atrás.'),
        (N'ES', N'SUP_REGISTRAR', N'Registrar'), (N'EN', N'SUP_REGISTRAR', N'Record'), (N'PT', N'SUP_REGISTRAR', N'Registrar'),
        (N'ES', N'ASG_SIN_CUENTA_CONF', N'{0} no tiene una cuenta vinculada: no va a poder confirmar el turno hasta que se la vinculen desde Empleados. ¿Asignar igual?'), (N'EN', N'ASG_SIN_CUENTA_CONF', N'{0} has no linked account: the shift cannot be confirmed until one is linked from Employees. Assign anyway?'), (N'PT', N'ASG_SIN_CUENTA_CONF', N'{0} não tem uma conta vinculada: não poderá confirmar o turno até que seja vinculada em Funcionários. Atribuir mesmo assim?'),
        (N'ES', N'MSG_ASG_FRANJA_TAREAS', N'La franja nueva deja afuera tareas que el empleado ya tiene en este evento: ajuste la franja o quite antes esas tareas.'), (N'EN', N'MSG_ASG_FRANJA_TAREAS', N'The new time slot leaves out tasks the employee already has in this event: adjust the slot or remove those tasks first.'), (N'PT', N'MSG_ASG_FRANJA_TAREAS', N'O novo horário deixa de fora tarefas que o funcionário já tem neste evento: ajuste o horário ou remova antes essas tarefas.'),
        (N'ES', N'MSG_CRO_RESPONSABLE_ACTIVIDAD', N'La actividad «{0}» está a cargo de {1}, que ya no es personal confirmado del evento: quítela y vuelva a agregarla con otro responsable.'), (N'EN', N'MSG_CRO_RESPONSABLE_ACTIVIDAD', N'The activity "{0}" is assigned to {1}, who is no longer confirmed staff of the event: remove it and add it again with another person in charge.'), (N'PT', N'MSG_CRO_RESPONSABLE_ACTIVIDAD', N'A atividade «{0}» está a cargo de {1}, que já não é equipe confirmada do evento: remova-a e adicione-a de novo com outro responsável.'),
        (N'ES', N'AGE_SIN_PERMISO_DETALLE', N'Tu perfil no incluye la consulta de las tareas y del cronograma.'), (N'EN', N'AGE_SIN_PERMISO_DETALLE', N'Your profile does not include viewing tasks and the schedule.'), (N'PT', N'AGE_SIN_PERMISO_DETALLE', N'Seu perfil não inclui a consulta das tarefas e do cronograma.'),
        (N'ES', N'OPE_SERVICIOS_ERROR', N'(no se pudieron leer)'), (N'EN', N'OPE_SERVICIOS_ERROR', N'(could not be read)'), (N'PT', N'OPE_SERVICIOS_ERROR', N'(não foi possível ler)'),
        (N'ES', N'MSG_RES_CONFIRMACIONES_REINICIADAS', N'El evento cambió de fecha: {0} confirmación(es) del personal volvieron a pendiente y el equipo tiene que responder de nuevo.'), (N'EN', N'MSG_RES_CONFIRMACIONES_REINICIADAS', N'The event date changed: {0} staff confirmation(s) went back to pending and the team has to answer again.'), (N'PT', N'MSG_RES_CONFIRMACIONES_REINICIADAS', N'A data do evento mudou: {0} confirmação(ões) da equipe voltaram a pendente e a equipe precisa responder de novo.'),
        (N'ES', N'SUP_INCIDENCIAS', N'Incidencias'), (N'EN', N'SUP_INCIDENCIAS', N'Incidents'), (N'PT', N'SUP_INCIDENCIAS', N'Ocorrências'),
        (N'ES', N'SUP_INICIAR', N'Iniciar ejecución'), (N'EN', N'SUP_INICIAR', N'Start execution'), (N'PT', N'SUP_INICIAR', N'Iniciar execução'),
        (N'ES', N'SUP_REPORTA_NADIE', N'(supervisión)'), (N'EN', N'SUP_REPORTA_NADIE', N'(supervision)'), (N'PT', N'SUP_REPORTA_NADIE', N'(supervisão)'),
        (N'ES', N'SUP_RESOLVER', N'Resolver'), (N'EN', N'SUP_RESOLVER', N'Resolve'), (N'PT', N'SUP_RESOLVER', N'Resolver'),
        (N'ES', N'SUP_RESOLVER_LBL', N'¿Cómo se resolvió?'), (N'EN', N'SUP_RESOLVER_LBL', N'How was it resolved?'), (N'PT', N'SUP_RESOLVER_LBL', N'Como foi resolvida?'),
        (N'ES', N'SUP_RESOLVER_TITULO', N'Resolver incidencia'), (N'EN', N'SUP_RESOLVER_TITULO', N'Resolve incident'), (N'PT', N'SUP_RESOLVER_TITULO', N'Resolver ocorrência'),
        (N'ES', N'SUP_RESUMEN', N'Incidencias: {0}    Abiertas: {1}'), (N'EN', N'SUP_RESUMEN', N'Incidents: {0}    Open: {1}'), (N'PT', N'SUP_RESUMEN', N'Ocorrências: {0}    Abertas: {1}'),
        (N'ES', N'SUP_TAREAS', N'Tareas'), (N'EN', N'SUP_TAREAS', N'Tasks'), (N'PT', N'SUP_TAREAS', N'Tarefas'),
        (N'ES', N'SUP_TITULO', N'Supervisión del evento'), (N'EN', N'SUP_TITULO', N'Event supervision'), (N'PT', N'SUP_TITULO', N'Supervisão do evento'),
        -- Mi agenda (ucMiAgenda, CUN007 y CUN010)
        (N'ES', N'AGE_COL_CRONOGRAMA', N'Cronograma del evento'), (N'EN', N'AGE_COL_CRONOGRAMA', N'Event schedule'), (N'PT', N'AGE_COL_CRONOGRAMA', N'Cronograma do evento'),
        (N'ES', N'AGE_COL_EVENTO', N'Evento'), (N'EN', N'AGE_COL_EVENTO', N'Event'), (N'PT', N'AGE_COL_EVENTO', N'Evento'),
        (N'ES', N'AGE_COL_MI_TAREA', N'Mis tareas'), (N'EN', N'AGE_COL_MI_TAREA', N'My tasks'), (N'PT', N'AGE_COL_MI_TAREA', N'Minhas tarefas'),
        (N'ES', N'AGE_COUNT', N'turno(s)'), (N'EN', N'AGE_COUNT', N'shift(s)'), (N'PT', N'AGE_COUNT', N'turno(s)'),
        (N'ES', N'AGE_EVENTO', N'Evento del {0} en {1} (reserva #{2})'), (N'EN', N'AGE_EVENTO', N'Event on {0} at {1} (reservation #{2})'), (N'PT', N'AGE_EVENTO', N'Evento de {0} em {1} (reserva #{2})'),
        (N'ES', N'AGE_RECHAZAR_LBL', N'Motivo del rechazo'), (N'EN', N'AGE_RECHAZAR_LBL', N'Reason for declining'), (N'PT', N'AGE_RECHAZAR_LBL', N'Motivo da recusa'),
        (N'ES', N'AGE_RECHAZAR_TITULO', N'Rechazar turno'), (N'EN', N'AGE_RECHAZAR_TITULO', N'Decline shift'), (N'PT', N'AGE_RECHAZAR_TITULO', N'Recusar turno'),
        (N'ES', N'AGE_SIN_TURNOS', N'Todavía no tenés turnos asignados.'), (N'EN', N'AGE_SIN_TURNOS', N'You have no shifts assigned yet.'), (N'PT', N'AGE_SIN_TURNOS', N'Você ainda não tem turnos atribuídos.'),
        -- Rechazos de la capa de negocio (CoordinacionResult) y de la reserva congelada (RN-13)
        (N'ES', N'MSG_COORD_ACTIVIDAD', N'Cada actividad lleva una descripción (hasta 150 caracteres) y una duración de 1 a 1440 minutos.'), (N'EN', N'MSG_COORD_ACTIVIDAD', N'Each activity needs a description (up to 150 characters) and a duration from 1 to 1440 minutes.'), (N'PT', N'MSG_COORD_ACTIVIDAD', N'Cada atividade leva uma descrição (até 150 caracteres) e uma duração de 1 a 1440 minutos.'),
        (N'ES', N'MSG_COORD_ASIGNACION', N'La asignación ya no existe.'), (N'EN', N'MSG_COORD_ASIGNACION', N'The assignment no longer exists.'), (N'PT', N'MSG_COORD_ASIGNACION', N'A atribuição não existe mais.'),
        (N'ES', N'MSG_COORD_CERRADO', N'El evento está cerrado: no admite cambios.'), (N'EN', N'MSG_COORD_CERRADO', N'The event is closed: it cannot be changed.'), (N'PT', N'MSG_COORD_CERRADO', N'O evento está encerrado: não admite alterações.'),
        (N'ES', N'MSG_COORD_CRONO_CON_TAREAS', N'El cronograma tiene tareas asignadas: quítelas antes de eliminarlo.'), (N'EN', N'MSG_COORD_CRONO_CON_TAREAS', N'The schedule has tasks assigned: remove them before deleting it.'), (N'PT', N'MSG_COORD_CRONO_CON_TAREAS', N'O cronograma tem tarefas atribuídas: remova-as antes de excluí-lo.'),
        (N'ES', N'MSG_COORD_DESCRIPCION', N'Ingrese la descripción.'), (N'EN', N'MSG_COORD_DESCRIPCION', N'Enter the description.'), (N'PT', N'MSG_COORD_DESCRIPCION', N'Informe a descrição.'),
        (N'ES', N'MSG_COORD_EMPLEADO', N'Seleccione un empleado activo.'), (N'EN', N'MSG_COORD_EMPLEADO', N'Select an active employee.'), (N'PT', N'MSG_COORD_EMPLEADO', N'Selecione um funcionário ativo.'),
        (N'ES', N'MSG_COORD_EMPLEADO_BAJA', N'Tu ficha de empleado está dada de baja: ya no podés responder turnos.'), (N'EN', N'MSG_COORD_EMPLEADO_BAJA', N'Your employee record is inactive: you can no longer answer shifts.'), (N'PT', N'MSG_COORD_EMPLEADO_BAJA', N'Sua ficha de funcionário está desativada: você não pode mais responder turnos.'),
        (N'ES', N'MSG_COORD_EN_EJECUCION', N'El evento está en ejecución: el plan ya no se modifica. Lo que se salga del plan se registra como incidencia.'), (N'EN', N'MSG_COORD_EN_EJECUCION', N'The event is in progress: the plan can no longer be changed. Anything off plan is recorded as an incident.'), (N'PT', N'MSG_COORD_EN_EJECUCION', N'O evento está em execução: o plano não se modifica mais. O que sair do plano é registrado como ocorrência.'),
        (N'ES', N'MSG_COORD_FRANJA', N'La hora de fin tiene que ser distinta de la hora de inicio.'), (N'EN', N'MSG_COORD_FRANJA', N'The end time must be different from the start time.'), (N'PT', N'MSG_COORD_FRANJA', N'A hora de término tem de ser diferente da hora de início.'),
        (N'ES', N'MSG_COORD_FUERA_FRANJA', N'La tarea tiene que caer dentro del turno del empleado.'), (N'EN', N'MSG_COORD_FUERA_FRANJA', N'The task must fall within the employee''s shift.'), (N'PT', N'MSG_COORD_FUERA_FRANJA', N'A tarefa tem de ficar dentro do turno do funcionário.'),
        (N'ES', N'MSG_COORD_INCIDENCIA', N'La incidencia ya no existe.'), (N'EN', N'MSG_COORD_INCIDENCIA', N'The incident no longer exists.'), (N'PT', N'MSG_COORD_INCIDENCIA', N'A ocorrência não existe mais.'),
        (N'ES', N'MSG_COORD_INC_ABIERTAS', N'Quedan incidencias abiertas: resuélvalas antes de cerrar el evento.'), (N'EN', N'MSG_COORD_INC_ABIERTAS', N'There are open incidents: resolve them before closing the event.'), (N'PT', N'MSG_COORD_INC_ABIERTAS', N'Há ocorrências abertas: resolva-as antes de encerrar o evento.'),
        (N'ES', N'MSG_COORD_INC_RESUELTA', N'La incidencia ya estaba resuelta.'), (N'EN', N'MSG_COORD_INC_RESUELTA', N'The incident was already resolved.'), (N'PT', N'MSG_COORD_INC_RESUELTA', N'A ocorrência já estava resolvida.'),
        (N'ES', N'MSG_COORD_MOTIVO', N'Para rechazar el turno hay que indicar el motivo.'), (N'EN', N'MSG_COORD_MOTIVO', N'To decline the shift you must give the reason.'), (N'PT', N'MSG_COORD_MOTIVO', N'Para recusar o turno é preciso indicar o motivo.'),
        (N'ES', N'MSG_COORD_NO_CONFIRMADA', N'La reserva ya no está confirmada: solo se coordinan los eventos de reservas confirmadas.'), (N'EN', N'MSG_COORD_NO_CONFIRMADA', N'The reservation is no longer confirmed: only events of confirmed reservations are coordinated.'), (N'PT', N'MSG_COORD_NO_CONFIRMADA', N'A reserva não está mais confirmada: só se coordenam os eventos de reservas confirmadas.'),
        (N'ES', N'MSG_COORD_NO_EN_EJECUCION', N'El evento no está en ejecución.'), (N'EN', N'MSG_COORD_NO_EN_EJECUCION', N'The event is not in progress.'), (N'PT', N'MSG_COORD_NO_EN_EJECUCION', N'O evento não está em execução.'),
        (N'ES', N'MSG_COORD_NO_ES_EL_EMPLEADO', N'La asignación es de otro empleado: solo él puede responderla.'), (N'EN', N'MSG_COORD_NO_ES_EL_EMPLEADO', N'The assignment belongs to another employee: only they can answer it.'), (N'PT', N'MSG_COORD_NO_ES_EL_EMPLEADO', N'A atribuição é de outro funcionário: só ele pode respondê-la.'),
        (N'ES', N'MSG_COORD_NO_LISTO', N'El evento todavía no está listo: falta que todo el personal confirme o falta el cronograma.'), (N'EN', N'MSG_COORD_NO_LISTO', N'The event is not ready yet: some staff have not confirmed or the schedule is missing.'), (N'PT', N'MSG_COORD_NO_LISTO', N'O evento ainda não está pronto: falta a confirmação de toda a equipe ou falta o cronograma.'),
        (N'ES', N'MSG_COORD_RESOLUCION', N'Indique cómo se resolvió la incidencia.'), (N'EN', N'MSG_COORD_RESOLUCION', N'State how the incident was resolved.'), (N'PT', N'MSG_COORD_RESOLUCION', N'Indique como a ocorrência foi resolvida.'),
        (N'ES', N'MSG_COORD_RESPONSABLE', N'El empleado elegido no es personal confirmado de este evento.'), (N'EN', N'MSG_COORD_RESPONSABLE', N'The chosen employee is not confirmed staff for this event.'), (N'PT', N'MSG_COORD_RESPONSABLE', N'O funcionário escolhido não é equipe confirmada deste evento.'),
        (N'ES', N'MSG_COORD_ROL', N'Ingrese el rol que cumple el empleado en el evento (hasta 60 caracteres).'), (N'EN', N'MSG_COORD_ROL', N'Enter the employee''s role in the event (up to 60 characters).'), (N'PT', N'MSG_COORD_ROL', N'Informe a função do funcionário no evento (até 60 caracteres).'),
        (N'ES', N'MSG_COORD_SIN_ACTIVIDADES', N'Agregue al menos una actividad al cronograma.'), (N'EN', N'MSG_COORD_SIN_ACTIVIDADES', N'Add at least one activity to the schedule.'), (N'PT', N'MSG_COORD_SIN_ACTIVIDADES', N'Adicione pelo menos uma atividade ao cronograma.'),
        (N'ES', N'MSG_COORD_SIN_CONFIRMAR', N'El cronograma se arma con el equipo confirmado: tiene que haber personal confirmado y ninguna respuesta pendiente.'), (N'EN', N'MSG_COORD_SIN_CONFIRMAR', N'The schedule is built with the confirmed team: there must be confirmed staff and no pending answers.'), (N'PT', N'MSG_COORD_SIN_CONFIRMAR', N'O cronograma é montado com a equipe confirmada: tem de haver equipe confirmada e nenhuma resposta pendente.'),
        (N'ES', N'MSG_COORD_SIN_CRONOGRAMA', N'El evento todavía no tiene cronograma: genérelo primero.'), (N'EN', N'MSG_COORD_SIN_CRONOGRAMA', N'The event has no schedule yet: generate it first.'), (N'PT', N'MSG_COORD_SIN_CRONOGRAMA', N'O evento ainda não tem cronograma: gere-o primeiro.'),
        (N'ES', N'MSG_COORD_SIN_EMPLEADO', N'Tu cuenta no está vinculada a un empleado. Pedile a un coordinador que la vincule desde Empleados.'), (N'EN', N'MSG_COORD_SIN_EMPLEADO', N'Your account is not linked to an employee. Ask a coordinator to link it from Employees.'), (N'PT', N'MSG_COORD_SIN_EMPLEADO', N'Sua conta não está vinculada a um funcionário. Peça a um coordenador que a vincule em Funcionários.'),
        (N'ES', N'MSG_COORD_SUPERPOSICION', N'La franja se superpone con otro turno del empleado.'), (N'EN', N'MSG_COORD_SUPERPOSICION', N'The time slot overlaps another shift of the employee.'), (N'PT', N'MSG_COORD_SUPERPOSICION', N'O horário se sobrepõe a outro turno do funcionário.'),
        (N'ES', N'MSG_COORD_SUPERPOSICION_DET', N'La franja se superpone con otro turno de {0}: reserva #{1}, {2}, de {3}.'), (N'EN', N'MSG_COORD_SUPERPOSICION_DET', N'The time slot overlaps another shift of {0}: reservation #{1}, {2}, {3}.'), (N'PT', N'MSG_COORD_SUPERPOSICION_DET', N'O horário se sobrepõe a outro turno de {0}: reserva #{1}, {2}, das {3}.'),
        (N'ES', N'MSG_COORD_TAREA', N'La tarea ya no existe.'), (N'EN', N'MSG_COORD_TAREA', N'The task no longer exists.'), (N'PT', N'MSG_COORD_TAREA', N'A tarefa não existe mais.'),
        (N'ES', N'MSG_COORD_TAREA_SUPERPUESTA', N'La tarea se superpone con otra tarea del mismo empleado.'), (N'EN', N'MSG_COORD_TAREA_SUPERPUESTA', N'The task overlaps another task of the same employee.'), (N'PT', N'MSG_COORD_TAREA_SUPERPUESTA', N'A tarefa se sobrepõe a outra tarefa do mesmo funcionário.'),
        (N'ES', N'MSG_COORD_TIENE_CARGA', N'El empleado tiene actividades del cronograma a cargo o tareas en este evento: reasígnelas antes.'), (N'EN', N'MSG_COORD_TIENE_CARGA', N'The employee is in charge of schedule activities or has tasks in this event: reassign them first.'), (N'PT', N'MSG_COORD_TIENE_CARGA', N'O funcionário tem atividades do cronograma a seu cargo ou tarefas neste evento: reatribua-as antes.'),
        (N'ES', N'MSG_COORD_YA_ASIGNADO', N'El empleado ya está asignado a este evento.'), (N'EN', N'MSG_COORD_YA_ASIGNADO', N'The employee is already assigned to this event.'), (N'PT', N'MSG_COORD_YA_ASIGNADO', N'O funcionário já está atribuído a este evento.'),
        (N'ES', N'MSG_COORD_YA_RESPONDIDA', N'La asignación ya fue respondida.'), (N'EN', N'MSG_COORD_YA_RESPONDIDA', N'The assignment has already been answered.'), (N'PT', N'MSG_COORD_YA_RESPONDIDA', N'A atribuição já foi respondida.'),
        (N'ES', N'MSG_RES_EVENTO_INICIADO', N'El evento de esta reserva está en ejecución o cerrado: la reserva ya no admite modificaciones ni cancelación.'), (N'EN', N'MSG_RES_EVENTO_INICIADO', N'This reservation''s event is in progress or closed: the reservation can no longer be modified or cancelled.'), (N'PT', N'MSG_RES_EVENTO_INICIADO', N'O evento desta reserva está em execução ou encerrado: a reserva não admite mais modificações nem cancelamento.'),
        -- Menu y rotulos de las secciones
        (N'ES', N'MENU_OPERACIONES', N'Operaciones'), (N'EN', N'MENU_OPERACIONES', N'Operations'), (N'PT', N'MENU_OPERACIONES', N'Operações'),
        (N'ES', N'MENU_AGENDA', N'Mi agenda'), (N'EN', N'MENU_AGENDA', N'My schedule'), (N'PT', N'MENU_AGENDA', N'Minha agenda'),
        (N'ES', N'MENU_EMPLEADOS', N'Empleados'), (N'EN', N'MENU_EMPLEADOS', N'Employees'), (N'PT', N'MENU_EMPLEADOS', N'Funcionários'),
        (N'ES', N'EMP_TITULO', N'Gestión de Empleados'), (N'EN', N'EMP_TITULO', N'Employees Management'), (N'PT', N'EMP_TITULO', N'Gestão de Funcionários'),
        (N'ES', N'EMP_CUENTA', N'Cuenta de usuario'), (N'EN', N'EMP_CUENTA', N'User account'), (N'PT', N'EMP_CUENTA', N'Conta de usuário'),
        (N'ES', N'OPE_TITULO', N'Operaciones de eventos'), (N'EN', N'OPE_TITULO', N'Event operations'), (N'PT', N'OPE_TITULO', N'Operações de eventos'),
        (N'ES', N'OPE_BTN_PERSONAL', N'Personal'), (N'EN', N'OPE_BTN_PERSONAL', N'Staff'), (N'PT', N'OPE_BTN_PERSONAL', N'Equipe'),
        (N'ES', N'OPE_BTN_CRONOGRAMA', N'Cronograma'), (N'EN', N'OPE_BTN_CRONOGRAMA', N'Schedule'), (N'PT', N'OPE_BTN_CRONOGRAMA', N'Cronograma'),
        (N'ES', N'OPE_BTN_TAREAS', N'Tareas'), (N'EN', N'OPE_BTN_TAREAS', N'Tasks'), (N'PT', N'OPE_BTN_TAREAS', N'Tarefas'),
        (N'ES', N'OPE_BTN_SUPERVISION', N'Supervisión'), (N'EN', N'OPE_BTN_SUPERVISION', N'Supervision'), (N'PT', N'OPE_BTN_SUPERVISION', N'Supervisão'),
        (N'ES', N'AGE_TITULO', N'Mi agenda'), (N'EN', N'AGE_TITULO', N'My schedule'), (N'PT', N'AGE_TITULO', N'Minha agenda'),
        (N'ES', N'AGE_CONFIRMAR', N'Confirmar'), (N'EN', N'AGE_CONFIRMAR', N'Confirm'), (N'PT', N'AGE_CONFIRMAR', N'Confirmar'),
        (N'ES', N'AGE_RECHAZAR', N'Rechazar'), (N'EN', N'AGE_RECHAZAR', N'Decline'), (N'PT', N'AGE_RECHAZAR', N'Recusar'),
        -- Especialidades del personal (catalogo: ESP_<NOMBRE>)
        (N'ES', N'ESP_MOZO', N'Mozo'), (N'EN', N'ESP_MOZO', N'Waiter'), (N'PT', N'ESP_MOZO', N'Garçom'),
        (N'ES', N'ESP_COCINA', N'Cocina'), (N'EN', N'ESP_COCINA', N'Kitchen'), (N'PT', N'ESP_COCINA', N'Cozinha'),
        (N'ES', N'ESP_BARRA', N'Barra'), (N'EN', N'ESP_BARRA', N'Bar'), (N'PT', N'ESP_BARRA', N'Bar'),
        (N'ES', N'ESP_DJ_Y_SONIDO', N'DJ y sonido'), (N'EN', N'ESP_DJ_Y_SONIDO', N'DJ and sound'), (N'PT', N'ESP_DJ_Y_SONIDO', N'DJ e som'),
        (N'ES', N'ESP_FOTOGRAFIA_Y_VIDEO', N'Fotografía y video'), (N'EN', N'ESP_FOTOGRAFIA_Y_VIDEO', N'Photo and video'), (N'PT', N'ESP_FOTOGRAFIA_Y_VIDEO', N'Fotografia e vídeo'),
        (N'ES', N'ESP_DECORACION', N'Decoración'), (N'EN', N'ESP_DECORACION', N'Decoration'), (N'PT', N'ESP_DECORACION', N'Decoração'),
        (N'ES', N'ESP_RECEPCION', N'Recepción'), (N'EN', N'ESP_RECEPCION', N'Reception'), (N'PT', N'ESP_RECEPCION', N'Recepção'),
        (N'ES', N'ESP_SEGURIDAD', N'Seguridad'), (N'EN', N'ESP_SEGURIDAD', N'Security'), (N'PT', N'ESP_SEGURIDAD', N'Segurança'),
        (N'ES', N'ESP_LIMPIEZA', N'Limpieza'), (N'EN', N'ESP_LIMPIEZA', N'Cleaning'), (N'PT', N'ESP_LIMPIEZA', N'Limpeza'),
        -- Arbol de permisos (PERMG_<NOMBRE> para el grupo, PERM_<Clave> para las hojas)
        (N'ES', N'PERMG_OPERACIONES', N'Operaciones'), (N'EN', N'PERMG_OPERACIONES', N'Operations'), (N'PT', N'PERMG_OPERACIONES', N'Operações'),
        (N'ES', N'PERM_EMPLEADOS_GESTION', N'Gestión de Empleados'), (N'EN', N'PERM_EMPLEADOS_GESTION', N'Employees Management'), (N'PT', N'PERM_EMPLEADOS_GESTION', N'Gestão de Funcionários'),
        (N'ES', N'PERM_PERSONAL_ASIGNAR', N'Asignar Personal'), (N'EN', N'PERM_PERSONAL_ASIGNAR', N'Assign Staff'), (N'PT', N'PERM_PERSONAL_ASIGNAR', N'Atribuir Equipe'),
        (N'ES', N'PERM_DISPONIBILIDAD_CONFIRMAR', N'Confirmar Disponibilidad'), (N'EN', N'PERM_DISPONIBILIDAD_CONFIRMAR', N'Confirm Availability'), (N'PT', N'PERM_DISPONIBILIDAD_CONFIRMAR', N'Confirmar Disponibilidade'),
        (N'ES', N'PERM_CRONOGRAMA_GESTION', N'Gestionar Cronograma'), (N'EN', N'PERM_CRONOGRAMA_GESTION', N'Manage Schedule'), (N'PT', N'PERM_CRONOGRAMA_GESTION', N'Gerenciar Cronograma'),
        (N'ES', N'PERM_TAREAS_ASIGNAR', N'Asignar Tareas'), (N'EN', N'PERM_TAREAS_ASIGNAR', N'Assign Tasks'), (N'PT', N'PERM_TAREAS_ASIGNAR', N'Atribuir Tarefas'),
        (N'ES', N'PERM_AGENDA_CONSULTAR', N'Consultar Agenda'), (N'EN', N'PERM_AGENDA_CONSULTAR', N'View Own Schedule'), (N'PT', N'PERM_AGENDA_CONSULTAR', N'Consultar Agenda'),
        (N'ES', N'PERM_EJECUCION_SUPERVISAR', N'Supervisar Ejecución'), (N'EN', N'PERM_EJECUCION_SUPERVISAR', N'Supervise Execution'), (N'PT', N'PERM_EJECUCION_SUPERVISAR', N'Supervisionar Execução'),
        -- Bitacora: modulos (MOD_) y acciones (BACC_) que asienta el Proceso 2
        (N'ES', N'MOD_EMPLEADOS', N'Empleados'), (N'EN', N'MOD_EMPLEADOS', N'Employees'), (N'PT', N'MOD_EMPLEADOS', N'Funcionários'),
        (N'ES', N'MOD_COORDINACION', N'Coordinación'), (N'EN', N'MOD_COORDINACION', N'Coordination'), (N'PT', N'MOD_COORDINACION', N'Coordenação'),
        (N'ES', N'BACC_ALTA_DE_EMPLEADO', N'Alta de empleado'), (N'EN', N'BACC_ALTA_DE_EMPLEADO', N'Employee created'), (N'PT', N'BACC_ALTA_DE_EMPLEADO', N'Cadastro de funcionário'),
        (N'ES', N'BACC_MODIFICACION_DE_EMPLEADO', N'Modificación de empleado'), (N'EN', N'BACC_MODIFICACION_DE_EMPLEADO', N'Employee updated'), (N'PT', N'BACC_MODIFICACION_DE_EMPLEADO', N'Alteração de funcionário'),
        (N'ES', N'BACC_BAJA_RECHAZADA', N'Baja rechazada'), (N'EN', N'BACC_BAJA_RECHAZADA', N'Deactivation rejected'), (N'PT', N'BACC_BAJA_RECHAZADA', N'Desativação rejeitada'),
        (N'ES', N'BACC_ASIGNACION_DE_PERSONAL', N'Asignación de personal'), (N'EN', N'BACC_ASIGNACION_DE_PERSONAL', N'Staff assignment'), (N'PT', N'BACC_ASIGNACION_DE_PERSONAL', N'Atribuição de equipe'),
        (N'ES', N'BACC_BAJA_DE_ASIGNACION', N'Baja de asignación'), (N'EN', N'BACC_BAJA_DE_ASIGNACION', N'Assignment removed'), (N'PT', N'BACC_BAJA_DE_ASIGNACION', N'Remoção de atribuição'),
        (N'ES', N'BACC_DISPONIBILIDAD_CONFIRMADA', N'Disponibilidad confirmada'), (N'EN', N'BACC_DISPONIBILIDAD_CONFIRMADA', N'Availability confirmed'), (N'PT', N'BACC_DISPONIBILIDAD_CONFIRMADA', N'Disponibilidade confirmada'),
        (N'ES', N'BACC_TURNO_RECHAZADO', N'Turno rechazado'), (N'EN', N'BACC_TURNO_RECHAZADO', N'Shift declined'), (N'PT', N'BACC_TURNO_RECHAZADO', N'Turno recusado'),
        (N'ES', N'BACC_RESPUESTA_RECHAZADA', N'Respuesta rechazada'), (N'EN', N'BACC_RESPUESTA_RECHAZADA', N'Answer rejected'), (N'PT', N'BACC_RESPUESTA_RECHAZADA', N'Resposta rejeitada'),
        (N'ES', N'BACC_GENERACION_DE_CRONOGRAMA', N'Generación de cronograma'), (N'EN', N'BACC_GENERACION_DE_CRONOGRAMA', N'Schedule generated'), (N'PT', N'BACC_GENERACION_DE_CRONOGRAMA', N'Geração de cronograma'),
        (N'ES', N'BACC_MODIFICACION_DE_CRONOGRAMA', N'Modificación de cronograma'), (N'EN', N'BACC_MODIFICACION_DE_CRONOGRAMA', N'Schedule updated'), (N'PT', N'BACC_MODIFICACION_DE_CRONOGRAMA', N'Alteração de cronograma'),
        (N'ES', N'BACC_ELIMINACION_DE_CRONOGRAMA', N'Eliminación de cronograma'), (N'EN', N'BACC_ELIMINACION_DE_CRONOGRAMA', N'Schedule deleted'), (N'PT', N'BACC_ELIMINACION_DE_CRONOGRAMA', N'Exclusão de cronograma'),
        (N'ES', N'BACC_CRONOGRAMA_RECHAZADO', N'Cronograma rechazado'), (N'EN', N'BACC_CRONOGRAMA_RECHAZADO', N'Schedule rejected'), (N'PT', N'BACC_CRONOGRAMA_RECHAZADO', N'Cronograma rejeitado'),
        (N'ES', N'BACC_ASIGNACION_DE_TAREA', N'Asignación de tarea'), (N'EN', N'BACC_ASIGNACION_DE_TAREA', N'Task assignment'), (N'PT', N'BACC_ASIGNACION_DE_TAREA', N'Atribuição de tarefa'),
        (N'ES', N'BACC_BAJA_DE_TAREA', N'Baja de tarea'), (N'EN', N'BACC_BAJA_DE_TAREA', N'Task removed'), (N'PT', N'BACC_BAJA_DE_TAREA', N'Remoção de tarefa'),
        (N'ES', N'BACC_TAREA_RECHAZADA', N'Tarea rechazada'), (N'EN', N'BACC_TAREA_RECHAZADA', N'Task rejected'), (N'PT', N'BACC_TAREA_RECHAZADA', N'Tarefa rejeitada'),
        (N'ES', N'BACC_INICIO_DE_EJECUCION', N'Inicio de ejecución'), (N'EN', N'BACC_INICIO_DE_EJECUCION', N'Execution started'), (N'PT', N'BACC_INICIO_DE_EJECUCION', N'Início de execução'),
        (N'ES', N'BACC_CIERRE_DE_EVENTO', N'Cierre de evento'), (N'EN', N'BACC_CIERRE_DE_EVENTO', N'Event closed'), (N'PT', N'BACC_CIERRE_DE_EVENTO', N'Encerramento de evento'),
        (N'ES', N'BACC_EJECUCION_RECHAZADA', N'Ejecución rechazada'), (N'EN', N'BACC_EJECUCION_RECHAZADA', N'Execution rejected'), (N'PT', N'BACC_EJECUCION_RECHAZADA', N'Execução rejeitada'),
        (N'ES', N'BACC_CIERRE_RECHAZADO', N'Cierre rechazado'), (N'EN', N'BACC_CIERRE_RECHAZADO', N'Closing rejected'), (N'PT', N'BACC_CIERRE_RECHAZADO', N'Encerramento rejeitado'),
        (N'ES', N'BACC_COORDINACION_RECHAZADA', N'Coordinación rechazada'), (N'EN', N'BACC_COORDINACION_RECHAZADA', N'Coordination rejected'), (N'PT', N'BACC_COORDINACION_RECHAZADA', N'Coordenação rejeitada'),
        (N'ES', N'BACC_REGISTRO_DE_INCIDENCIA', N'Registro de incidencia'), (N'EN', N'BACC_REGISTRO_DE_INCIDENCIA', N'Incident recorded'), (N'PT', N'BACC_REGISTRO_DE_INCIDENCIA', N'Registro de ocorrência'),
        (N'ES', N'BACC_RESOLUCION_DE_INCIDENCIA', N'Resolución de incidencia'), (N'EN', N'BACC_RESOLUCION_DE_INCIDENCIA', N'Incident resolved'), (N'PT', N'BACC_RESOLUCION_DE_INCIDENCIA', N'Resolução de ocorrência'),
        (N'ES', N'BACC_CONFIRMACIONES_REINICIADAS', N'Confirmaciones reiniciadas'), (N'EN', N'BACC_CONFIRMACIONES_REINICIADAS', N'Confirmations reset'), (N'PT', N'BACC_CONFIRMACIONES_REINICIADAS', N'Confirmações reiniciadas'),
        -- Cronograma ya generado con respuestas pendientes (RN-11): se puede modificar,
        -- siempre con responsables confirmados
        (N'ES', N'MSG_CRO_PENDIENTES', N'Hay respuestas pendientes: cada actividad tiene que quedar a cargo de personal confirmado.'), (N'EN', N'MSG_CRO_PENDIENTES', N'Some replies are pending: every activity must be assigned to confirmed staff.'), (N'PT', N'MSG_CRO_PENDIENTES', N'Há respostas pendentes: cada atividade deve ficar a cargo de pessoal confirmado.'),
        -- Alerta de integridad: inconsistencias de los pagos (digito verificador de Pagos).
        -- La verificacion las devuelve en castellano y la pantalla las traduce.
        (N'ES', N'ALERT_PAGO_DVH_FALTANTE', N'Pago #{0} (reserva #{1}): sin DV horizontal almacenado.'),
        (N'EN', N'ALERT_PAGO_DVH_FALTANTE', N'Payment #{0} (reservation #{1}): no stored horizontal check digit.'),
        (N'PT', N'ALERT_PAGO_DVH_FALTANTE', N'Pagamento #{0} (reserva #{1}): sem DV horizontal armazenado.'),
        (N'ES', N'ALERT_PAGO_DVH_NO_COINCIDE', N'Pago #{0} (reserva #{1}): el DV horizontal no coincide (posible alteración externa).'),
        (N'EN', N'ALERT_PAGO_DVH_NO_COINCIDE', N'Payment #{0} (reservation #{1}): the horizontal check digit does not match (possible external alteration).'),
        (N'PT', N'ALERT_PAGO_DVH_NO_COINCIDE', N'Pagamento #{0} (reserva #{1}): o DV horizontal não confere (possível alteração externa).'),
        (N'ES', N'ALERT_DVV_PAGOS_NO_COINCIDE', N'El DV vertical de Pagos no coincide (filas agregadas, quitadas o reordenadas por fuera del sistema).'),
        (N'EN', N'ALERT_DVV_PAGOS_NO_COINCIDE', N'The vertical check digit of Payments does not match (rows added, removed or reordered outside the system).'),
        (N'PT', N'ALERT_DVV_PAGOS_NO_COINCIDE', N'O DV vertical de Pagamentos não confere (linhas adicionadas, removidas ou reordenadas fora do sistema).')
    ) AS v(Codigo, Clave, Texto)
)
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, MIN(t.Texto)
FROM Txt t
JOIN dbo.Idiomas i ON i.Codigo = t.Codigo
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave
)
GROUP BY i.Id, t.Clave;   -- una sola fila por idioma+clave: una clave repetida en el
                          -- bloque de arriba no puede romper UQ_Traducciones.
GO

-- ---------------------------------------------------------------------------
-- Idiomas agregados desde Gestion de Idiomas. Al crearlos, la aplicacion les
-- copia todas las claves del espanol; las claves que el script suma despues solo
-- se siembran para ES, EN y PT, asi que a un idioma propio le faltarian: sus
-- pantallas nuevas saldrian en espanol y el editor, que lista las filas del
-- idioma elegido, no las ofreceria para traducir. Se le copian del espanol las
-- que no tenga (lo ya traducido se conserva). Va despues del ultimo bloque de
-- traducciones.
-- ---------------------------------------------------------------------------
INSERT INTO dbo.Traducciones (IdiomaId, Clave, Texto)
SELECT i.Id, t.Clave, t.Texto
FROM dbo.Idiomas i
JOIN dbo.Idiomas es ON es.Codigo = N'ES'
JOIN dbo.Traducciones t ON t.IdiomaId = es.Id
WHERE i.Codigo NOT IN (N'ES', N'EN', N'PT')
  AND NOT EXISTS (SELECT 1 FROM dbo.Traducciones x WHERE x.IdiomaId = i.Id AND x.Clave = t.Clave);
GO

-- ---------------------------------------------------------------------------
-- Ortografia de los catalogos de ejemplo sembrados por versiones anteriores del
-- script (salones, servicios y los dos clientes de ejemplo iban sin tildes).
-- Mismo criterio que los metodos de pago: se corrige solo el valor de fabrica
-- exacto y solo si el nombre corregido no existe ya (los nombres de salones y
-- de servicios son unicos). Las reservas, sus lineas y sus versiones referencian
-- al salon, al servicio y al cliente por Id, asi que ningun dato de negocio
-- cambia, y ninguna de estas tablas participa de los digitos verificadores.
-- ---------------------------------------------------------------------------
UPDATE dbo.Salones SET Nombre = N'Salón Principal'
 WHERE Nombre = N'Salon Principal' COLLATE Latin1_General_CS_AS
   AND NOT EXISTS (SELECT 1 FROM dbo.Salones x WHERE x.Nombre = N'Salón Principal' COLLATE Latin1_General_CS_AS);
UPDATE dbo.Salones SET Nombre = N'Salón Jardín'
 WHERE Nombre = N'Salon Jardin' COLLATE Latin1_General_CS_AS
   AND NOT EXISTS (SELECT 1 FROM dbo.Salones x WHERE x.Nombre = N'Salón Jardín' COLLATE Latin1_General_CS_AS);

UPDATE dbo.Servicios SET Nombre = N'Decoración temática'
 WHERE Nombre = N'Decoracion tematica' COLLATE Latin1_General_CS_AS
   AND NOT EXISTS (SELECT 1 FROM dbo.Servicios x WHERE x.Nombre = N'Decoración temática' COLLATE Latin1_General_CS_AS);
UPDATE dbo.Servicios SET Nombre = N'Fotografía y video'
 WHERE Nombre = N'Fotografia y video' COLLATE Latin1_General_CS_AS
   AND NOT EXISTS (SELECT 1 FROM dbo.Servicios x WHERE x.Nombre = N'Fotografía y video' COLLATE Latin1_General_CS_AS);
-- Las descripciones se corrigen solo en el servicio de fabrica (por su nombre) y
-- con comparacion exacta: un servicio propio con el mismo texto no se toca.
UPDATE dbo.Servicios SET Descripcion = N'Menú completo por invitado'
 WHERE Nombre = N'Catering por persona'
   AND Descripcion = N'Menu completo por invitado' COLLATE Latin1_General_CS_AS
   AND DATALENGTH(Descripcion) = DATALENGTH(N'Menu completo por invitado');
UPDATE dbo.Servicios SET Descripcion = N'Ambientación del salón'
 WHERE Nombre IN (N'Decoración temática', N'Decoracion tematica')
   AND Descripcion = N'Ambientacion del salon' COLLATE Latin1_General_CS_AS
   AND DATALENGTH(Descripcion) = DATALENGTH(N'Ambientacion del salon');
UPDATE dbo.Servicios SET Descripcion = N'Servicio de música y sonido'
 WHERE Nombre = N'DJ y sonido'
   AND Descripcion = N'Servicio de musica y sonido' COLLATE Latin1_General_CS_AS
   AND DATALENGTH(Descripcion) = DATALENGTH(N'Servicio de musica y sonido');
UPDATE dbo.Servicios SET Descripcion = N'Personal de atención (por mozo)'
 WHERE Nombre = N'Servicio de mozos'
   AND Descripcion = N'Personal de atencion (por mozo)' COLLATE Latin1_General_CS_AS
   AND DATALENGTH(Descripcion) = DATALENGTH(N'Personal de atencion (por mozo)');

UPDATE dbo.Clientes SET Apellido = N'Pérez'
 WHERE Dni = N'30111222' AND Nombre = N'Juan' COLLATE Latin1_General_CS_AS AND Apellido = N'Perez' COLLATE Latin1_General_CS_AS;
UPDATE dbo.Clientes SET Nombre = N'María', Apellido = N'Gómez'
 WHERE Dni = N'28999333' AND Nombre = N'Maria' COLLATE Latin1_General_CS_AS AND Apellido = N'Gomez' COLLATE Latin1_General_CS_AS;
GO

-- ===========================================================================
-- Herramienta de integridad: el recalculo de la linea base alcanza tambien a los
-- pagos (digito verificador de Pagos). Los dos textos de la herramienta pasan a
-- decirlo; el segundo sigue informando la cantidad de reservas ({0}), con los
-- mismos marcadores: una traduccion editada, que el script conserva, sigue
-- siendo cierta. Mismo criterio que las demas correcciones de fabrica: alcanza a
-- cualquier idioma cuyo texto sea exactamente el de fabrica anterior
-- (intercalacion binaria y la misma longitud en bytes) y conserva una traduccion
-- editada por el usuario. Idempotente.
-- ===========================================================================
;WITH Fix(Clave, Anterior, Nuevo) AS (
    SELECT * FROM (VALUES
        (N'AUD_RECALC_CONFIRMA', N'¿Recalcular los dígitos verificadores de todas las reservas? Usar después de corregir datos alterados: la línea base nueva pasa a ser la referencia de integridad.', N'¿Recalcular los dígitos verificadores de todas las reservas y de todos los pagos? Usar después de corregir datos alterados: la línea base nueva pasa a ser la referencia de integridad.'),
        (N'AUD_RECALC_CONFIRMA', N'Recalculate the verification digits of all reservations? Use after fixing altered data: the new baseline becomes the integrity reference.', N'Recalculate the verification digits of all reservations and all payments? Use after fixing altered data: the new baseline becomes the integrity reference.'),
        (N'AUD_RECALC_CONFIRMA', N'Recalcular os dígitos verificadores de todas as reservas? Usar após corrigir dados alterados: a nova linha de base passa a ser a referência de integridade.', N'Recalcular os dígitos verificadores de todas as reservas e de todos os pagamentos? Usar após corrigir dados alterados: a nova linha de base passa a ser a referência de integridade.'),
        (N'AUD_RECALC_OK', N'Línea base recalculada ({0} reservas). Verificación posterior: {1} inconsistencia(s).', N'Línea base recalculada ({0} reservas, con sus pagos). Verificación posterior: {1} inconsistencia(s).'),
        (N'AUD_RECALC_OK', N'Baseline recalculated ({0} reservations). Post-check: {1} inconsistency(ies).', N'Baseline recalculated ({0} reservations, with their payments). Post-check: {1} inconsistency(ies).'),
        (N'AUD_RECALC_OK', N'Linha de base recalculada ({0} reservas). Verificação posterior: {1} inconsistência(s).', N'Linha de base recalculada ({0} reservas, com seus pagamentos). Verificação posterior: {1} inconsistência(s).')
    ) AS v(Clave, Anterior, Nuevo)
)
UPDATE t SET Texto = f.Nuevo
FROM dbo.Traducciones t
JOIN Fix f ON f.Clave = t.Clave
WHERE t.Texto COLLATE Latin1_General_BIN = f.Anterior COLLATE Latin1_General_BIN
  AND DATALENGTH(t.Texto) = DATALENGTH(f.Anterior);
GO

-- ===========================================================================
-- Cierre: si la guarda del principio activo NOEXEC (script corrido sobre una base
-- del sistema desde SSMS), la sesion vuelve a ejecutar lotes, asi una nueva
-- corrida en la misma ventana sobre la base correcta no termina vacia.
-- ===========================================================================
SET NOEXEC OFF;
GO
