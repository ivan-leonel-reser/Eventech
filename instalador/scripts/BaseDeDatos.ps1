<#
BaseDeDatos.ps1 - operaciones del instalador sobre la instancia de SQL Server elegida.

    Diagnosticar   comprueba que la instancia sirva: que responda, que este en este
                   equipo, que sea SQL Server 2012 o superior y que el usuario pueda
                   crear la base o, si ya existe, actualizarla. Si la base del sistema
                   ya existe, informa su version, sus cuentas, clientes y reservas, y
                   las sesiones abiertas (lo usa la reinstalacion para mostrar lo que
                   esta y lo que viene). Para una instalacion inicial decide como se
                   crea la base (MODO):
                     RESPALDO  la base no existe y el motor es SQL Server 2019 o superior:
                               se restaura el respaldo con los datos de demostracion;
                     ESQUEMA   la base no existe y el motor es anterior a 2019 (no puede
                               leer el respaldo): se la crea con db\schema.sql;
                     VACIA     la base ya existe pero esta vacia (la creo el administrador
                               de SQL Server para quien no puede crear bases) y el usuario
                               es su propietario: se le aplica db\schema.sql.
    Restaurar      crea la base restaurando el respaldo de demostracion (MODO RESPALDO).
                   Solo si la base no existe.
    Crear          crea la base vacia y le aplica el esquema (MODO ESQUEMA).
    Cargar         aplica el esquema a la base vacia existente (MODO VACIA).
    Actualizar     reinstalacion conservando los datos: respalda la base, le aplica
                   db\schema.sql (idempotente: agrega lo que falta y no pisa datos) y,
                   si algo falla, la devuelve al respaldo.
    Reemplazar     reinstalacion con la base de demostracion: respalda la base actual y
                   la reemplaza por el respaldo de demostracion.
    Revertir       devuelve la base al respaldo que tomo Actualizar o Reemplazar (la
                   instalacion se cancelo despues de modificar la base).
    Verificar      abre la base y cuenta sus tablas y sus cuentas.
    Quitar         deshace lo que hizo ESTA ejecucion (instalacion cancelada): elimina la
                   base que creo o, si cargo una base vacia existente, la deja vacia.
    Cuentas        no se conecta: informa las cuentas de Windows que administran la
                   instancia que crea el asistente al instalar SQL Server Express (el
                   usuario que corre el guion y el grupo de administradores del equipo,
                   con el nombre que tiene en el idioma de Windows).

El resultado se escribe en el archivo -Salida_704ILR como lineas CLAVE=valor (UTF-8
con marca de orden de bytes, que es como lo lee el asistente) y en el codigo de
salida: 0 correcto, 1 condicion prevista que impide continuar, 2 error no previsto.

Ninguna operacion necesita que Windows de permisos de administrador: alcanza con que
el usuario pueda crear bases en la instancia (o sea propietario de la base). Los
respaldos los escribe y los lee la cuenta del servicio de SQL Server.

Usa el proveedor de SQL Server de .NET Framework, que viene con Windows: el equipo
no necesita sqlcmd ni ninguna otra herramienta. La conexion es siempre con la
autenticacion integrada de Windows, como la de la aplicacion.

La version de la base que dejo el asistente queda en la propiedad extendida
EvenTech_Version de la propia base. Una base sin esa marca se reconoce por su
esquema (2.0 si tiene las tablas del personal, 1.0 si solo las de reservas).
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Diagnosticar', 'Restaurar', 'Crear', 'Cargar', 'Actualizar', 'Reemplazar', 'Revertir', 'Verificar', 'Quitar', 'Cuentas')]
    [string]$Accion_704ILR,

    [Parameter(Mandatory = $true)]
    [string]$Instancia_704ILR,

    [Parameter(Mandatory = $true)]
    [string]$Salida_704ILR,

    # Restaurar / Reemplazar: el respaldo de demostracion. Revertir: el respaldo previo.
    [string]$Respaldo_704ILR = '',

    # Actualizar (y, si se indica, Restaurar y Reemplazar): el script del esquema
    # (db\schema.sql), que lleva la base a esta version.
    [string]$Esquema_704ILR = '',

    # Version que instala este asistente ("3.0"): se graba en la base y se compara.
    [string]$VersionNueva_704ILR = '',

    # Quitar: la marca que informo Restaurar, Crear o Cargar. Solo se deshace lo que la tenga.
    [string]$Marca_704ILR = '',

    # Revertir: huella de uso que informo Actualizar o Reemplazar al terminar. Si la base
    # cambio desde entonces (alguien la uso), no se la devuelve al respaldo.
    [string]$Huella_704ILR = ''
)

$ErrorActionPreference = 'Stop'

$Base_704ILR = 'EvenTechDB'
# El respaldo se genera con SQL Server 2019 y un motor anterior no puede restaurarlo:
# con un motor anterior la base se crea con el esquema, que pide SQL Server 2012.
$VersionDelRespaldo_704ILR = 15
$VersionMinima_704ILR = 11
$CodigoBaseExistente_704ILR = 'BASE_EXISTENTE'
$PropiedadVersion_704ILR = 'EvenTech_Version'
# Marca de Quitar para una base vacia existente que cargo esta ejecucion.
$PrefijoVacia_704ILR = 'VACIA:'
$Resultado_704ILR = New-Object System.Collections.Generic.List[string]

function Anotar_704ILR([string]$clave_704ILR, [string]$valor_704ILR) {
    $limpio_704ILR = ([string]$valor_704ILR) -replace '\s+', ' '
    $Resultado_704ILR.Add($clave_704ILR + '=' + $limpio_704ILR.Trim())
}

function Terminar_704ILR([int]$codigoDeSalida_704ILR) {
    $utf8_704ILR = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllLines($Salida_704ILR, $Resultado_704ILR, $utf8_704ILR)
    exit $codigoDeSalida_704ILR
}

# Condicion prevista: el asistente la traduce a un mensaje para quien instala.
function Rechazar_704ILR([string]$codigo_704ILR, [string]$detalle_704ILR) {
    Anotar_704ILR 'ESTADO' 'RECHAZADO'
    Anotar_704ILR 'CODIGO' $codigo_704ILR
    Anotar_704ILR 'DETALLE' $detalle_704ILR
    Terminar_704ILR 1
}

function Conectar_704ILR([string]$catalogo_704ILR) {
    $cadena_704ILR = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $cadena_704ILR['Data Source'] = $Instancia_704ILR.Trim()
    $cadena_704ILR['Initial Catalog'] = $catalogo_704ILR
    $cadena_704ILR['Integrated Security'] = $true
    $cadena_704ILR['TrustServerCertificate'] = $true
    $cadena_704ILR['Connect Timeout'] = 8
    $cadena_704ILR['Pooling'] = $false
    $cadena_704ILR['Application Name'] = 'Instalador de EvenTech'
    $conexion_704ILR = New-Object System.Data.SqlClient.SqlConnection($cadena_704ILR.ConnectionString)
    $conexion_704ILR.Open()
    return $conexion_704ILR
}

function Comando_704ILR($conexion_704ILR, [string]$sql_704ILR, [hashtable]$parametros_704ILR) {
    $comando_704ILR = $conexion_704ILR.CreateCommand()
    $comando_704ILR.CommandText = $sql_704ILR
    $comando_704ILR.CommandTimeout = 600
    if ($parametros_704ILR) {
        foreach ($nombre_704ILR in $parametros_704ILR.Keys) {
            $p_704ILR = $comando_704ILR.Parameters.Add('@' + $nombre_704ILR, [System.Data.SqlDbType]::NVarChar, 400)
            $p_704ILR.Value = [string]$parametros_704ILR[$nombre_704ILR]
        }
    }
    return $comando_704ILR
}

function Escalar_704ILR($conexion_704ILR, [string]$sql_704ILR, [hashtable]$parametros_704ILR) {
    $valor_704ILR = (Comando_704ILR $conexion_704ILR $sql_704ILR $parametros_704ILR).ExecuteScalar()
    if ($null -eq $valor_704ILR -or $valor_704ILR -is [System.DBNull]) { return $null }
    return $valor_704ILR
}

function Filas_704ILR($conexion_704ILR, [string]$sql_704ILR, [hashtable]$parametros_704ILR) {
    $tabla_704ILR = New-Object System.Data.DataTable
    $lector_704ILR = (Comando_704ILR $conexion_704ILR $sql_704ILR $parametros_704ILR).ExecuteReader()
    try { $tabla_704ILR.Load($lector_704ILR) } finally { $lector_704ILR.Close() }
    return , $tabla_704ILR
}

# Nombre comercial del motor a partir del numero mayor de su version.
function NombreDelMotor_704ILR([int]$mayor_704ILR) {
    switch ($mayor_704ILR) {
        10 { return 'SQL Server 2008' }
        11 { return 'SQL Server 2012' }
        12 { return 'SQL Server 2014' }
        13 { return 'SQL Server 2016' }
        14 { return 'SQL Server 2017' }
        15 { return 'SQL Server 2019' }
        16 { return 'SQL Server 2022' }
        17 { return 'SQL Server 2025' }
        default { return 'SQL Server (version ' + $mayor_704ILR + ')' }
    }
}

# "3.0" -> [version]3.0; lo que no se puede leer, 0.0.
function ComoVersion_704ILR([string]$texto_704ILR) {
    $v_704ILR = $null
    if ([System.Version]::TryParse(([string]$texto_704ILR).Trim(), [ref]$v_704ILR)) { return $v_704ILR }
    return [System.Version]'0.0'
}

function ExisteEnElServidor_704ILR($conexion_704ILR, [string]$ruta_704ILR) {
    try {
        $fila_704ILR = (Filas_704ILR $conexion_704ILR 'EXEC master.dbo.xp_fileexist @ruta' @{ ruta = $ruta_704ILR }).Rows[0]
        return ([int]$fila_704ILR[0] -eq 1)
    }
    catch { return $false }     # sin permiso para consultarlo: RESTORE informa si el archivo existe
}

# Objetos creados por el usuario en la base de la conexion (tablas, vistas, procedimientos,
# funciones, tipos y esquemas propios). Cero: la base esta vacia.
function ObjetosPropios_704ILR($conexion_704ILR) {
    return [int](Escalar_704ILR $conexion_704ILR @"
SELECT (SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped = 0 AND parent_object_id = 0)
     + (SELECT COUNT(*) FROM sys.types WHERE is_user_defined = 1)
     + (SELECT COUNT(*) FROM sys.schemas WHERE schema_id BETWEEN 5 AND 16383)
"@ $null)
}

# Quien lee el respaldo de demostracion no es quien instala sino la cuenta del servicio de
# SQL Server. El asistente lo deja en una carpeta de datos de programas del equipo; aca se
# le agrega permiso de lectura para todos (el respaldo solo tiene los datos de demostracion
# y se borra al terminar), asi lo lee cualquiera sea la cuenta con que corre el servicio.
function PermitirLectura_704ILR([string]$ruta_704ILR) {
    try {
        $todos_704ILR = New-Object System.Security.Principal.SecurityIdentifier('S-1-1-0')
        $regla_704ILR = New-Object System.Security.AccessControl.FileSystemAccessRule($todos_704ILR, 'Read', 'Allow')
        $acl_704ILR = Get-Acl -LiteralPath $ruta_704ILR
        $acl_704ILR.AddAccessRule($regla_704ILR)
        Set-Acl -LiteralPath $ruta_704ILR -AclObject $acl_704ILR
    }
    catch { }   # si no se pudo, RESTORE informa que no puede leer el archivo
}

function MarcaDeCreacion_704ILR($conexionMaster_704ILR) {
    return [string](Escalar_704ILR $conexionMaster_704ILR 'SELECT CONVERT(NVARCHAR(30), create_date, 126) FROM sys.databases WHERE name = @base' @{ base = $Base_704ILR })
}

# Version de una base existente: la marca que dejo el asistente o, sin marca, la que
# se deduce del esquema (y $script:VersionDeducida_704ILR queda en $true). Vacio: la base
# no es del sistema.
$VersionDeducida_704ILR = $false
function VersionDeLaBase_704ILR($conexionBase_704ILR) {
    $script:VersionDeducida_704ILR = $false
    $marca_704ILR = Escalar_704ILR $conexionBase_704ILR "SELECT CAST(value AS NVARCHAR(40)) FROM sys.extended_properties WHERE class = 0 AND name = @n" @{ n = $PropiedadVersion_704ILR }
    if ($marca_704ILR) { return [string]$marca_704ILR }
    $script:VersionDeducida_704ILR = $true
    $tieneReservas_704ILR = [int](Escalar_704ILR $conexionBase_704ILR "SELECT CASE WHEN OBJECT_ID(N'dbo.Reservas', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL THEN 1 ELSE 0 END" $null)
    if ($tieneReservas_704ILR -ne 1) { return '' }
    $tienePermisos_704ILR = [int](Escalar_704ILR $conexionBase_704ILR "SELECT CASE WHEN OBJECT_ID(N'dbo.Permisos', N'U') IS NOT NULL THEN 1 ELSE 0 END" $null)
    if ($tienePermisos_704ILR -eq 1) {
        $e3_704ILR = [int](Escalar_704ILR $conexionBase_704ILR "SELECT COUNT(*) FROM dbo.Permisos WHERE Clave = N'INFORMES_VER'" $null)
        if ($e3_704ILR -gt 0) { return '3.0' }
    }
    $e2_704ILR = [int](Escalar_704ILR $conexionBase_704ILR "SELECT CASE WHEN OBJECT_ID(N'dbo.Especialidades', N'U') IS NOT NULL THEN 1 ELSE 0 END" $null)
    if ($e2_704ILR -eq 1) { return '2.0' }
    return '1.0'
}

function GrabarVersion_704ILR([string]$version_704ILR) {
    if (-not $version_704ILR) { return }
    # La marca es un dato de apoyo (sin ella la version se deduce del esquema): si no se
    # puede grabar, se informa y la operacion, que ya termino bien, sigue valida.
    try { $conexion_704ILR = Conectar_704ILR $Base_704ILR }
    catch { Anotar_704ILR 'VERSION_GRABADA' 0; return }
    try {
        (Comando_704ILR $conexion_704ILR @"
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = @n)
    EXEC sys.sp_updateextendedproperty @name = @n, @value = @v;
ELSE
    EXEC sys.sp_addextendedproperty @name = @n, @value = @v;
"@ @{ n = $PropiedadVersion_704ILR; v = $version_704ILR }).ExecuteNonQuery() | Out-Null
        Anotar_704ILR 'VERSION_GRABADA' 1
    }
    catch { Anotar_704ILR 'VERSION_GRABADA' 0 }
    finally { $conexion_704ILR.Dispose() }
}

# Huella de uso de la base: el ultimo asiento de la bitacora y el ultimo de la auditoria de
# accesos. Toda operacion del sistema (y todo ingreso) agrega una fila a alguna de las dos.
function HuellaDeUso_704ILR {
    $conexion_704ILR = Conectar_704ILR $Base_704ILR
    try {
        return [string](Escalar_704ILR $conexion_704ILR "SELECT CONCAT((SELECT ISNULL(MAX(Id), 0) FROM dbo.Bitacora), '-', (SELECT ISNULL(MAX(Id), 0) FROM dbo.LoginAuditLog))" $null)
    }
    finally { $conexion_704ILR.Dispose() }
}

# Otras sesiones abiertas sobre la base (el sistema abierto en otro puesto, una
# consulta de SQL Server Management Studio): con ellas no se respalda ni se migra.
function SesionesAbiertas_704ILR($conexionMaster_704ILR) {
    return [int](Escalar_704ILR $conexionMaster_704ILR "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE database_id = DB_ID(@b) AND session_id <> @@SPID AND is_user_process = 1" @{ b = $Base_704ILR })
}

function CarpetaDeRespaldos_704ILR($conexionMaster_704ILR) {
    $carpeta_704ILR = [string](Escalar_704ILR $conexionMaster_704ILR "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(400))" $null)
    if (-not $carpeta_704ILR) { $carpeta_704ILR = [string](Escalar_704ILR $conexionMaster_704ILR "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(400))" $null) }
    if (-not $carpeta_704ILR) { Rechazar_704ILR 'SIN_CARPETA_DE_RESPALDO' $Instancia_704ILR }
    return $carpeta_704ILR.TrimEnd('\')
}

# Respaldo completo de la base actual, verificado. Devuelve la ruta (en el servidor).
function RespaldarActual_704ILR($conexionMaster_704ILR, [string]$motivo_704ILR) {
    $etiqueta_704ILR = if ($VersionNueva_704ILR) { $VersionNueva_704ILR } else { 'nueva' }
    $ruta_704ILR = (CarpetaDeRespaldos_704ILR $conexionMaster_704ILR) + '\' + $Base_704ILR + '_antes_de_' + $etiqueta_704ILR + '_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.bak'
    (Comando_704ILR $conexionMaster_704ILR "BACKUP DATABASE [$Base_704ILR] TO DISK = @ruta WITH COPY_ONLY, CHECKSUM, INIT, NAME = @nombre" @{ ruta = $ruta_704ILR; nombre = $motivo_704ILR }).ExecuteNonQuery() | Out-Null
    (Comando_704ILR $conexionMaster_704ILR 'RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM' @{ ruta = $ruta_704ILR }).ExecuteNonQuery() | Out-Null
    Anotar_704ILR 'RESPALDO' $ruta_704ILR
    return $ruta_704ILR
}

# Restaura sobre la base existente (con sus mismos archivos). La deja en uso exclusivo
# mientras tanto y la devuelve al uso normal aunque la restauracion falle.
function RestaurarSobreLaBase_704ILR($conexionMaster_704ILR, [string]$respaldo_704ILR) {
    $archivos_704ILR = Filas_704ILR $conexionMaster_704ILR 'RESTORE FILELISTONLY FROM DISK = @r' @{ r = $respaldo_704ILR }
    $actuales_704ILR = Filas_704ILR $conexionMaster_704ILR "SELECT type_desc, physical_name FROM sys.master_files WHERE database_id = DB_ID(@b) ORDER BY file_id" @{ b = $Base_704ILR }
    $datos_704ILR = ($actuales_704ILR.Rows | Where-Object { $_['type_desc'] -eq 'ROWS' } | Select-Object -First 1)['physical_name']
    $registro_704ILR = ($actuales_704ILR.Rows | Where-Object { $_['type_desc'] -eq 'LOG' } | Select-Object -First 1)['physical_name']
    $logicoDatos_704ILR = ($archivos_704ILR.Rows | Where-Object { $_['Type'] -eq 'D' } | Select-Object -First 1)['LogicalName']
    $logicoRegistro_704ILR = ($archivos_704ILR.Rows | Where-Object { $_['Type'] -eq 'L' } | Select-Object -First 1)['LogicalName']
    if (-not $datos_704ILR -or -not $registro_704ILR -or -not $logicoDatos_704ILR -or -not $logicoRegistro_704ILR) {
        throw 'No se pudieron leer los archivos de la base o del respaldo.'
    }
    try {
        (Comando_704ILR $conexionMaster_704ILR @"
ALTER DATABASE [$Base_704ILR] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Base_704ILR] FROM DISK = @r
WITH REPLACE, RECOVERY, MOVE @ld TO @d, MOVE @ll TO @l;
"@ @{ r = $respaldo_704ILR; ld = $logicoDatos_704ILR; d = $datos_704ILR; ll = $logicoRegistro_704ILR; l = $registro_704ILR }).ExecuteNonQuery() | Out-Null
    }
    finally {
        try { (Comando_704ILR $conexionMaster_704ILR "IF DB_ID(@b) IS NOT NULL ALTER DATABASE [$Base_704ILR] SET MULTI_USER" @{ b = $Base_704ILR }).ExecuteNonQuery() | Out-Null } catch { }
    }
}

# Copia un respaldo a la carpeta de respaldos de la instancia (la cuenta del servicio de
# SQL Server no lee la carpeta temporal del asistente). Devuelve la copia o el original.
function CopiaLegible_704ILR($conexionMaster_704ILR, [string]$respaldo_704ILR) {
    try {
        $destino_704ILR = (CarpetaDeRespaldos_704ILR $conexionMaster_704ILR) + '\EvenTech_instalacion_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.bak'
        Copy-Item -LiteralPath $respaldo_704ILR -Destination $destino_704ILR -Force
        return $destino_704ILR
    }
    catch { return $respaldo_704ILR }
}

# Lotes de un script con separadores GO (una linea que solo dice GO).
function LotesDe_704ILR([string]$ruta_704ILR) {
    $texto_704ILR = [System.IO.File]::ReadAllText($ruta_704ILR, [System.Text.Encoding]::UTF8)
    $lotes_704ILR = New-Object System.Collections.Generic.List[string]
    $actual_704ILR = New-Object System.Text.StringBuilder
    foreach ($linea_704ILR in ($texto_704ILR -split "`r?`n")) {
        if ($linea_704ILR -match '^\s*GO\s*$') {
            if ($actual_704ILR.ToString().Trim()) { $lotes_704ILR.Add($actual_704ILR.ToString()) }
            [void]$actual_704ILR.Clear()
        }
        else { [void]$actual_704ILR.AppendLine($linea_704ILR) }
    }
    if ($actual_704ILR.ToString().Trim()) { $lotes_704ILR.Add($actual_704ILR.ToString()) }
    return , $lotes_704ILR
}

# Aplica db\schema.sql sobre la base, en UNA sesion (las opciones SET valen para todos
# los lotes), lote por lote; el primero que falla corta. Devuelve $null si termino bien o
# el texto de la falla.
function AplicarEsquema_704ILR {
    $lote_704ILR = 0
    $lotes_704ILR = LotesDe_704ILR $Esquema_704ILR
    $conexionBase_704ILR = Conectar_704ILR $Base_704ILR
    try {
        foreach ($sql_704ILR in $lotes_704ILR) {
            $lote_704ILR++
            (Comando_704ILR $conexionBase_704ILR $sql_704ILR $null).ExecuteNonQuery() | Out-Null
        }
    }
    catch { return ('Lote ' + $lote_704ILR + ' de ' + $lotes_704ILR.Count + ': ' + $_.Exception.GetBaseException().Message) }
    finally { $conexionBase_704ILR.Dispose() }
    Anotar_704ILR 'LOTES' $lotes_704ILR.Count
    return $null
}

# ----------------------------------------------------------------- Diagnosticar
function Diagnosticar_704ILR {
    try { $conexion_704ILR = Conectar_704ILR 'master' }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }

    $baseExiste_704ILR = $false
    try {
        $fila_704ILR = (Filas_704ILR $conexion_704ILR @"
SELECT CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(40))             AS Version,
       CAST(SERVERPROPERTY('Edition') AS NVARCHAR(128))                   AS Edicion,
       CAST(SERVERPROPERTY('ComputerNamePhysicalNetBIOS') AS NVARCHAR(128)) AS Equipo,
       ISNULL(IS_SRVROLEMEMBER('sysadmin'), 0)                            AS EsAdministrador,
       ISNULL(IS_SRVROLEMEMBER('dbcreator'), 0)                           AS CreaBases,
       CASE WHEN DB_ID(@base) IS NULL THEN 0 ELSE 1 END                   AS BaseExiste
"@ @{ base = $Base_704ILR }).Rows[0]

        $version_704ILR = [string]$fila_704ILR['Version']
        $mayor_704ILR = [int]($version_704ILR.Split('.')[0])
        $motor_704ILR = (NombreDelMotor_704ILR $mayor_704ILR) + ' (' + $version_704ILR + ')'
        Anotar_704ILR 'MOTOR' $motor_704ILR
        Anotar_704ILR 'EDICION' ([string]$fila_704ILR['Edicion'])

        $equipo_704ILR = [string]$fila_704ILR['Equipo']
        if ($equipo_704ILR -and ($equipo_704ILR -ne $env:COMPUTERNAME)) {
            Rechazar_704ILR 'INSTANCIA_REMOTA' $equipo_704ILR
        }
        if ($mayor_704ILR -lt $VersionMinima_704ILR) {
            Rechazar_704ILR 'VERSION_ANTERIOR' $motor_704ILR
        }
        # Reemplazar restaura el respaldo de demostracion: un motor anterior no puede leerlo.
        Anotar_704ILR 'REEMPLAZO' ([int]($mayor_704ILR -ge $VersionDelRespaldo_704ILR))
        $esAdministrador_704ILR = ([int]$fila_704ILR['EsAdministrador'] -eq 1)
        $puedeCrear_704ILR = $esAdministrador_704ILR -or ([int]$fila_704ILR['CreaBases'] -eq 1)
        $baseExiste_704ILR = ([int]$fila_704ILR['BaseExiste'] -eq 1)
        if ($baseExiste_704ILR) { $sesiones_704ILR = SesionesAbiertas_704ILR $conexion_704ILR }
    }
    finally { $conexion_704ILR.Dispose() }

    $usuario_704ILR = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    if (-not $baseExiste_704ILR) {
        # Instalacion inicial: se crea la base (restaurando el respaldo o con el esquema).
        if (-not $puedeCrear_704ILR) { Rechazar_704ILR 'SIN_PERMISO' $usuario_704ILR }
        Anotar_704ILR 'BASE_EXISTE' 0
        Anotar_704ILR 'MODO' $(if ($mayor_704ILR -ge $VersionDelRespaldo_704ILR) { 'RESPALDO' } else { 'ESQUEMA' })
        Anotar_704ILR 'ESTADO' 'CORRECTO'
        Terminar_704ILR 0
    }

    # La base existe. Lo que esta: version y contenido.
    try { $conexionBase_704ILR = Conectar_704ILR $Base_704ILR }
    catch { Rechazar_704ILR 'SIN_ACCESO_A_LA_BASE' $_.Exception.GetBaseException().Message }
    try {
        $propietario_704ILR = ([int](Escalar_704ILR $conexionBase_704ILR "SELECT ISNULL(IS_ROLEMEMBER('db_owner'), 0)" $null) -eq 1) -or $esAdministrador_704ILR
        if ((ObjetosPropios_704ILR $conexionBase_704ILR) -eq 0) {
            # Vacia: la preparo el administrador de SQL Server para quien no puede crear
            # bases. Es una instalacion inicial sobre esa base, si el usuario es su propietario.
            if (-not $propietario_704ILR) { Rechazar_704ILR 'SIN_PERMISO' $usuario_704ILR }
            Anotar_704ILR 'BASE_EXISTE' 0
            Anotar_704ILR 'MODO' 'VACIA'
            Anotar_704ILR 'ESTADO' 'CORRECTO'
            Terminar_704ILR 0
        }
        $versionBase_704ILR = VersionDeLaBase_704ILR $conexionBase_704ILR
        if (-not $versionBase_704ILR) { Rechazar_704ILR 'BASE_AJENA' $Base_704ILR }
        Anotar_704ILR 'VERSION_BASE' $versionBase_704ILR
        Anotar_704ILR 'VERSION_DEDUCIDA' ([int]$VersionDeducida_704ILR)
        if ($VersionNueva_704ILR -and ((ComoVersion_704ILR $versionBase_704ILR) -gt (ComoVersion_704ILR $VersionNueva_704ILR))) {
            Rechazar_704ILR 'BASE_MAS_NUEVA' $versionBase_704ILR
        }
        # Actualizarla (respaldo, esquema) pide ser su propietario o administrador de la instancia.
        if (-not $propietario_704ILR) { Rechazar_704ILR 'SIN_PERMISO_BASE' $usuario_704ILR }
        Anotar_704ILR 'BASE_EXISTE' 1
        Anotar_704ILR 'SESIONES' $sesiones_704ILR
        Anotar_704ILR 'CUENTAS' (Escalar_704ILR $conexionBase_704ILR 'SELECT COUNT(*) FROM dbo.Users' $null)
        Anotar_704ILR 'CLIENTES' (Escalar_704ILR $conexionBase_704ILR "SELECT CASE WHEN OBJECT_ID(N'dbo.Clientes', N'U') IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM dbo.Clientes) END" $null)
        Anotar_704ILR 'RESERVAS' (Escalar_704ILR $conexionBase_704ILR 'SELECT COUNT(*) FROM dbo.Reservas' $null)
    }
    finally { $conexionBase_704ILR.Dispose() }

    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# -------------------------------------------------------------------- Restaurar
function RestaurarDesde_704ILR($conexion_704ILR, [string]$respaldo_704ILR) {
    $archivos_704ILR = Filas_704ILR $conexion_704ILR 'RESTORE FILELISTONLY FROM DISK = @respaldo' @{ respaldo = $respaldo_704ILR }
    $logicoDatos_704ILR = $null
    $logicoRegistro_704ILR = $null
    foreach ($archivo_704ILR in $archivos_704ILR.Rows) {
        if (([string]$archivo_704ILR['Type'] -eq 'D') -and -not $logicoDatos_704ILR) { $logicoDatos_704ILR = [string]$archivo_704ILR['LogicalName'] }
        if (([string]$archivo_704ILR['Type'] -eq 'L') -and -not $logicoRegistro_704ILR) { $logicoRegistro_704ILR = [string]$archivo_704ILR['LogicalName'] }
    }
    if (-not $logicoDatos_704ILR -or -not $logicoRegistro_704ILR) {
        throw 'El respaldo no trae un archivo de datos y uno de registro.'
    }

    $carpetaDatos_704ILR = [string](Escalar_704ILR $conexion_704ILR "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(400))" $null)
    $carpetaRegistro_704ILR = [string](Escalar_704ILR $conexion_704ILR "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS NVARCHAR(400))" $null)
    if (-not $carpetaDatos_704ILR) { throw 'La instancia no informa su carpeta de datos.' }
    if (-not $carpetaRegistro_704ILR) { $carpetaRegistro_704ILR = $carpetaDatos_704ILR }

    $carpetaDatos_704ILR = $carpetaDatos_704ILR.TrimEnd('\') + '\'
    $carpetaRegistro_704ILR = $carpetaRegistro_704ILR.TrimEnd('\') + '\'

    # Si quedaron archivos de una base separada con el mismo nombre, no se pisan: la
    # base nueva usa otro nombre de archivo.
    $nombre_704ILR = $Base_704ILR
    $datos_704ILR = $carpetaDatos_704ILR + $nombre_704ILR + '.mdf'
    $registro_704ILR = $carpetaRegistro_704ILR + $nombre_704ILR + '_log.ldf'
    if ((ExisteEnElServidor_704ILR $conexion_704ILR $datos_704ILR) -or (ExisteEnElServidor_704ILR $conexion_704ILR $registro_704ILR)) {
        $nombre_704ILR = $Base_704ILR + '_' + (Get-Date -Format 'yyyyMMdd_HHmmss')
        $datos_704ILR = $carpetaDatos_704ILR + $nombre_704ILR + '.mdf'
        $registro_704ILR = $carpetaRegistro_704ILR + $nombre_704ILR + '_log.ldf'
    }

    # La comprobacion de que la base no existe va en el MISMO lote que la restauracion.
    # RESTORE no reemplaza por si solo una base distinta, pero si la existente es la
    # misma base del respaldo y esta en modo de recuperacion simple, la pisaria sin
    # avisar: la instalacion inicial nunca modifica una base existente (para eso estan
    # Actualizar y Reemplazar, que la respaldan antes).
    (Comando_704ILR $conexion_704ILR @"
IF DB_ID(@base) IS NOT NULL
    RAISERROR(N'$CodigoBaseExistente_704ILR', 16, 1);
ELSE
    RESTORE DATABASE @base FROM DISK = @respaldo
    WITH MOVE @logicoDatos TO @datos, MOVE @logicoRegistro TO @registro, RECOVERY;
"@ @{
            base = $Base_704ILR; respaldo = $respaldo_704ILR
            logicoDatos = $logicoDatos_704ILR; datos = $datos_704ILR
            logicoRegistro = $logicoRegistro_704ILR; registro = $registro_704ILR
        }).ExecuteNonQuery() | Out-Null

    Anotar_704ILR 'ARCHIVO_DATOS' $datos_704ILR
}

function Restaurar_704ILR {
    if (-not $Respaldo_704ILR -or -not (Test-Path -LiteralPath $Respaldo_704ILR)) {
        Rechazar_704ILR 'RESPALDO_AUSENTE' $Respaldo_704ILR
    }
    try { $conexion_704ILR = Conectar_704ILR 'master' }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }

    try {
        # Entre el diagnostico y este paso otra sesion pudo haber creado la base.
        if ($null -ne (Escalar_704ILR $conexion_704ILR 'SELECT DB_ID(@base)' @{ base = $Base_704ILR })) {
            Rechazar_704ILR $CodigoBaseExistente_704ILR $Base_704ILR
        }

        # Quien lee el respaldo no es quien instala sino la cuenta del servicio de SQL
        # Server. Si quien instala puede escribir en la carpeta de respaldos de la propia
        # instancia (un administrador del equipo), se lo copia ahi, que el motor siempre
        # puede leer, y se borra esa copia al terminar. Si no, se restaura desde la
        # carpeta de apoyo donde lo dejo el asistente, con permiso de lectura.
        PermitirLectura_704ILR $Respaldo_704ILR
        $origen_704ILR = CopiaLegible_704ILR $conexion_704ILR $Respaldo_704ILR

        $falla_704ILR = $null
        try { RestaurarDesde_704ILR $conexion_704ILR $origen_704ILR }
        catch { $falla_704ILR = $_.Exception.GetBaseException() }

        if ($origen_704ILR -ne $Respaldo_704ILR -and (Test-Path -LiteralPath $origen_704ILR)) {
            Remove-Item -LiteralPath $origen_704ILR -Force -ErrorAction SilentlyContinue
        }
        if ($falla_704ILR -and ($falla_704ILR.Message -eq $CodigoBaseExistente_704ILR)) {
            # Otra sesion creo la base entre la comprobacion y la restauracion: no se toca.
            Rechazar_704ILR $CodigoBaseExistente_704ILR $Base_704ILR
        }
        if ($falla_704ILR) {
            # Una restauracion que falla a mitad de camino puede dejar la base a medio
            # crear (antes de este paso no existia): se la quita para que un nuevo
            # intento no la encuentre "existente".
            try {
                (Comando_704ILR $conexion_704ILR "IF DB_ID(@base) IS NOT NULL AND CAST(DATABASEPROPERTYEX(@base, 'Status') AS NVARCHAR(60)) <> 'ONLINE' DROP DATABASE [$Base_704ILR]" @{ base = $Base_704ILR }).ExecuteNonQuery() | Out-Null
            }
            catch { }
            Rechazar_704ILR 'RESTAURACION_FALLIDA' $falla_704ILR.Message
        }

        # La fecha de creacion identifica la base creada por esta ejecucion: es lo unico
        # que Quitar acepta eliminar.
        Anotar_704ILR 'ORIGEN' $(if ($origen_704ILR -ne $Respaldo_704ILR) { 'CARPETA_DE_RESPALDOS' } else { 'CARPETA_DE_APOYO' })
        Anotar_704ILR 'MARCA' (MarcaDeCreacion_704ILR $conexion_704ILR)

        # El respaldo de demostracion se lleva al esquema de esta version (si es el de
        # la misma version, no cambia nada). Si falla, la base recien creada se quita.
        if ($Esquema_704ILR) {
            $fallaEsquema_704ILR = AplicarEsquema_704ILR
            if ($fallaEsquema_704ILR) {
                try { (Comando_704ILR $conexion_704ILR "ALTER DATABASE [$Base_704ILR] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Base_704ILR];" $null).ExecuteNonQuery() | Out-Null } catch { }
                Rechazar_704ILR 'RESTAURACION_FALLIDA' $fallaEsquema_704ILR
            }
        }
    }
    finally { $conexion_704ILR.Dispose() }

    GrabarVersion_704ILR $VersionNueva_704ILR
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# ---------------------------------------------------------------- Crear y Cargar
function Crear_704ILR {
    if (-not $Esquema_704ILR -or -not (Test-Path -LiteralPath $Esquema_704ILR)) {
        Rechazar_704ILR 'ESQUEMA_AUSENTE' $Esquema_704ILR
    }
    try { $conexion_704ILR = Conectar_704ILR 'master' }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }

    $marca_704ILR = ''
    try {
        # Misma guarda que la restauracion: en el mismo lote que la creacion.
        try {
            (Comando_704ILR $conexion_704ILR @"
IF DB_ID(@base) IS NOT NULL
    RAISERROR(N'$CodigoBaseExistente_704ILR', 16, 1);
ELSE
    EXEC (N'CREATE DATABASE [$Base_704ILR]');
"@ @{ base = $Base_704ILR }).ExecuteNonQuery() | Out-Null
        }
        catch {
            if ($_.Exception.GetBaseException().Message -eq $CodigoBaseExistente_704ILR) { Rechazar_704ILR $CodigoBaseExistente_704ILR $Base_704ILR }
            Rechazar_704ILR 'RESTAURACION_FALLIDA' $_.Exception.GetBaseException().Message
        }
        $marca_704ILR = MarcaDeCreacion_704ILR $conexion_704ILR

        $falla_704ILR = AplicarEsquema_704ILR
        if ($falla_704ILR) {
            # La base la creo este paso: si el esquema no se pudo aplicar, se la quita.
            try { QuitarBase_704ILR $conexion_704ILR $marca_704ILR | Out-Null } catch { }
            Rechazar_704ILR 'RESTAURACION_FALLIDA' $falla_704ILR
        }
    }
    finally { $conexion_704ILR.Dispose() }

    Anotar_704ILR 'MARCA' $marca_704ILR
    GrabarVersion_704ILR $VersionNueva_704ILR
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

function Cargar_704ILR {
    if (-not $Esquema_704ILR -or -not (Test-Path -LiteralPath $Esquema_704ILR)) {
        Rechazar_704ILR 'ESQUEMA_AUSENTE' $Esquema_704ILR
    }
    try { $conexion_704ILR = Conectar_704ILR $Base_704ILR }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }

    try {
        # Entre el diagnostico y este paso alguien pudo haber creado algo en la base.
        if ((ObjetosPropios_704ILR $conexion_704ILR) -ne 0) { Rechazar_704ILR $CodigoBaseExistente_704ILR $Base_704ILR }
        # Todo objeto creado desde esta hora es de esta ejecucion (lo que Quitar deshace).
        $desde_704ILR = [string](Escalar_704ILR $conexion_704ILR 'SELECT CONVERT(NVARCHAR(30), DATEADD(SECOND, -1, GETDATE()), 126)' $null)
        $falla_704ILR = AplicarEsquema_704ILR
        if ($falla_704ILR) {
            try { VaciarBase_704ILR $conexion_704ILR $desde_704ILR | Out-Null } catch { }
            Rechazar_704ILR 'RESTAURACION_FALLIDA' $falla_704ILR
        }
    }
    finally { $conexion_704ILR.Dispose() }

    Anotar_704ILR 'MARCA' ($PrefijoVacia_704ILR + $desde_704ILR)
    GrabarVersion_704ILR $VersionNueva_704ILR
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# ------------------------------------------------------------------- Actualizar
function Actualizar_704ILR {
    if (-not $Esquema_704ILR -or -not (Test-Path -LiteralPath $Esquema_704ILR)) {
        Rechazar_704ILR 'ESQUEMA_AUSENTE' $Esquema_704ILR
    }
    try { $master_704ILR = Conectar_704ILR 'master' }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }
    try {
        if ($null -eq (Escalar_704ILR $master_704ILR 'SELECT DB_ID(@b)' @{ b = $Base_704ILR })) { Rechazar_704ILR 'BASE_INEXISTENTE' $Base_704ILR }
        $sesiones_704ILR = SesionesAbiertas_704ILR $master_704ILR
        if ($sesiones_704ILR -gt 0) { Rechazar_704ILR 'BASE_EN_USO' $sesiones_704ILR }

        $conexionBase_704ILR = Conectar_704ILR $Base_704ILR
        try { $anterior_704ILR = VersionDeLaBase_704ILR $conexionBase_704ILR } finally { $conexionBase_704ILR.Dispose() }
        if (-not $anterior_704ILR) { Rechazar_704ILR 'BASE_AJENA' $Base_704ILR }
        Anotar_704ILR 'VERSION_ANTERIOR' $anterior_704ILR

        $respaldo_704ILR = RespaldarActual_704ILR $master_704ILR ('EvenTech ' + $anterior_704ILR + ' antes de actualizar')

        $falla_704ILR = AplicarEsquema_704ILR

        if ($falla_704ILR) {
            # La base vuelve a quedar exactamente como estaba antes de actualizar.
            $restaurada_704ILR = 0
            try { RestaurarSobreLaBase_704ILR $master_704ILR $respaldo_704ILR; $restaurada_704ILR = 1 }
            catch { $falla_704ILR += ' | No se pudo volver al respaldo: ' + $_.Exception.GetBaseException().Message }
            Anotar_704ILR 'RESTAURADA' $restaurada_704ILR
            Rechazar_704ILR 'ACTUALIZACION_FALLIDA' $falla_704ILR
        }
    }
    finally { $master_704ILR.Dispose() }

    GrabarVersion_704ILR $VersionNueva_704ILR
    try { Anotar_704ILR 'HUELLA' (HuellaDeUso_704ILR) } catch { }
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# ------------------------------------------------------------------- Reemplazar
function Reemplazar_704ILR {
    if (-not $Respaldo_704ILR -or -not (Test-Path -LiteralPath $Respaldo_704ILR)) {
        Rechazar_704ILR 'RESPALDO_AUSENTE' $Respaldo_704ILR
    }
    try { $master_704ILR = Conectar_704ILR 'master' }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }
    try {
        if ($null -eq (Escalar_704ILR $master_704ILR 'SELECT DB_ID(@b)' @{ b = $Base_704ILR })) { Rechazar_704ILR 'BASE_INEXISTENTE' $Base_704ILR }
        $sesiones_704ILR = SesionesAbiertas_704ILR $master_704ILR
        if ($sesiones_704ILR -gt 0) { Rechazar_704ILR 'BASE_EN_USO' $sesiones_704ILR }

        $previo_704ILR = RespaldarActual_704ILR $master_704ILR 'EvenTech antes de reemplazar por la demostracion'
        PermitirLectura_704ILR $Respaldo_704ILR
        $origen_704ILR = CopiaLegible_704ILR $master_704ILR $Respaldo_704ILR
        $falla_704ILR = $null
        try { RestaurarSobreLaBase_704ILR $master_704ILR $origen_704ILR }
        catch { $falla_704ILR = $_.Exception.GetBaseException().Message }
        if ($origen_704ILR -ne $Respaldo_704ILR -and (Test-Path -LiteralPath $origen_704ILR)) {
            Remove-Item -LiteralPath $origen_704ILR -Force -ErrorAction SilentlyContinue
        }
        if (-not $falla_704ILR -and $Esquema_704ILR) { $falla_704ILR = AplicarEsquema_704ILR }
        if ($falla_704ILR) {
            $restaurada_704ILR = 0
            try { RestaurarSobreLaBase_704ILR $master_704ILR $previo_704ILR; $restaurada_704ILR = 1 }
            catch { $falla_704ILR += ' | No se pudo volver al respaldo: ' + $_.Exception.GetBaseException().Message }
            Anotar_704ILR 'RESTAURADA' $restaurada_704ILR
            Rechazar_704ILR 'RESTAURACION_FALLIDA' $falla_704ILR
        }
    }
    finally { $master_704ILR.Dispose() }

    GrabarVersion_704ILR $VersionNueva_704ILR
    try { Anotar_704ILR 'HUELLA' (HuellaDeUso_704ILR) } catch { }
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# --------------------------------------------------------------------- Revertir
function Revertir_704ILR {
    if (-not $Respaldo_704ILR) { Rechazar_704ILR 'RESPALDO_AUSENTE' '' }
    $master_704ILR = Conectar_704ILR 'master'
    try {
        # Una cancelacion tardia no puede borrar trabajo nuevo: si la base tiene otras
        # conexiones o cambio desde que el asistente la dejo lista (un ingreso, una
        # operacion), se la deja como esta y se informa el respaldo.
        $sesiones_704ILR = SesionesAbiertas_704ILR $master_704ILR
        $actual_704ILR = ''
        try { $actual_704ILR = HuellaDeUso_704ILR } catch { }
        if ($sesiones_704ILR -gt 0 -or ($Huella_704ILR -and $actual_704ILR -ne $Huella_704ILR)) {
            Anotar_704ILR 'REVERTIDA' 0
            Anotar_704ILR 'RESPALDO' $Respaldo_704ILR
            Rechazar_704ILR 'BASE_USADA' ("sesiones=$sesiones_704ILR huella=$Huella_704ILR actual=$actual_704ILR")
        }
        RestaurarSobreLaBase_704ILR $master_704ILR $Respaldo_704ILR
        Anotar_704ILR 'REVERTIDA' 1
    }
    finally { $master_704ILR.Dispose() }
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# -------------------------------------------------------------------- Verificar
function Verificar_704ILR {
    try { $conexion_704ILR = Conectar_704ILR $Base_704ILR }
    catch { Rechazar_704ILR 'SIN_ACCESO_A_LA_BASE' $_.Exception.GetBaseException().Message }

    try {
        $tablas_704ILR = [int](Escalar_704ILR $conexion_704ILR 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0' $null)
        $cuentas_704ILR = [int](Escalar_704ILR $conexion_704ILR 'SELECT COUNT(*) FROM dbo.Users' $null)
        Anotar_704ILR 'TABLAS' $tablas_704ILR
        Anotar_704ILR 'CUENTAS' $cuentas_704ILR
    }
    catch { Rechazar_704ILR 'SIN_ACCESO_A_LA_BASE' $_.Exception.GetBaseException().Message }
    finally { $conexion_704ILR.Dispose() }

    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# ----------------------------------------------------------------------- Quitar
# Elimina la base solo si su fecha de creacion es la marca. Devuelve si la elimino.
function QuitarBase_704ILR($conexionMaster_704ILR, [string]$marca_704ILR) {
    if (-not $marca_704ILR) { return $false }
    $coincide_704ILR = Escalar_704ILR $conexionMaster_704ILR 'SELECT COUNT(*) FROM sys.databases WHERE name = @base AND CONVERT(NVARCHAR(30), create_date, 126) = @marca' @{ base = $Base_704ILR; marca = $marca_704ILR }
    if ([int]$coincide_704ILR -ne 1) { return $false }
    (Comando_704ILR $conexionMaster_704ILR "ALTER DATABASE [$Base_704ILR] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Base_704ILR];" $null).ExecuteNonQuery() | Out-Null
    return $true
}

# Deja vacia la base existente que cargo esta ejecucion: elimina las claves foraneas y las
# tablas creadas desde la marca, y solo si no hay ningun objeto propio anterior a ella.
function VaciarBase_704ILR($conexionBase_704ILR, [string]$desde_704ILR) {
    $anteriores_704ILR = [int](Escalar_704ILR $conexionBase_704ILR 'SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped = 0 AND parent_object_id = 0 AND create_date < CONVERT(DATETIME, @desde, 126)' @{ desde = $desde_704ILR })
    if ($anteriores_704ILR -ne 0) { return $false }
    (Comando_704ILR $conexionBase_704ILR @"
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name) +
              N' DROP CONSTRAINT ' + QUOTENAME(f.name) + N';'
FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id = f.parent_object_id;
EXEC (@sql);
SET @sql = N'';
SELECT @sql = @sql + N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
FROM sys.tables WHERE is_ms_shipped = 0;
EXEC (@sql);
SET @sql = N'';
SELECT @sql = @sql + N'DROP ' + CASE type WHEN 'V' THEN N'VIEW ' WHEN 'P' THEN N'PROCEDURE ' ELSE N'FUNCTION ' END +
              QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';'
FROM sys.objects WHERE is_ms_shipped = 0 AND parent_object_id = 0 AND type IN ('V', 'P', 'FN', 'IF', 'TF');
EXEC (@sql);
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND name = N'$PropiedadVersion_704ILR')
    EXEC sys.sp_dropextendedproperty @name = N'$PropiedadVersion_704ILR';
"@ $null).ExecuteNonQuery() | Out-Null
    return $true
}

function Quitar_704ILR {
    if (-not $Marca_704ILR) { Rechazar_704ILR 'SIN_MARCA' '' }
    if ($Marca_704ILR.StartsWith($PrefijoVacia_704ILR)) {
        $conexion_704ILR = Conectar_704ILR $Base_704ILR
        try {
            if (-not (VaciarBase_704ILR $conexion_704ILR $Marca_704ILR.Substring($PrefijoVacia_704ILR.Length))) {
                Rechazar_704ILR 'NO_ES_LA_BASE_CREADA' $Marca_704ILR
            }
        }
        finally { $conexion_704ILR.Dispose() }
    }
    else {
        $conexion_704ILR = Conectar_704ILR 'master'
        try {
            if (-not (QuitarBase_704ILR $conexion_704ILR $Marca_704ILR)) { Rechazar_704ILR 'NO_ES_LA_BASE_CREADA' $Marca_704ILR }
        }
        finally { $conexion_704ILR.Dispose() }
    }

    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# ---------------------------------------------------------------------- Cuentas
# El grupo de administradores se busca por su identificador (S-1-5-32-544) y no por su
# nombre: en un Windows en espanol se llama "BUILTIN\Administradores" y el instalador de
# SQL Server rechaza una cuenta que no existe. El asistente corre esta accion con la
# cuenta de quien inicio la instalacion, que es la que va a usar el sistema.
function Cuentas_704ILR {
    $administradores_704ILR = (New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')).Translate([System.Security.Principal.NTAccount]).Value
    Anotar_704ILR 'ADMINISTRADORES' $administradores_704ILR
    Anotar_704ILR 'USUARIO' ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

try {
    switch ($Accion_704ILR) {
        'Cuentas' { Cuentas_704ILR }
        'Diagnosticar' { Diagnosticar_704ILR }
        'Restaurar' { Restaurar_704ILR }
        'Crear' { Crear_704ILR }
        'Cargar' { Cargar_704ILR }
        'Actualizar' { Actualizar_704ILR }
        'Reemplazar' { Reemplazar_704ILR }
        'Revertir' { Revertir_704ILR }
        'Verificar' { Verificar_704ILR }
        'Quitar' { Quitar_704ILR }
    }
}
catch {
    Anotar_704ILR 'ESTADO' 'ERROR'
    Anotar_704ILR 'CODIGO' 'ERROR_NO_PREVISTO'
    Anotar_704ILR 'DETALLE' $_.Exception.GetBaseException().Message
    Terminar_704ILR 2
}
