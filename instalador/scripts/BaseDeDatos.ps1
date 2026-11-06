<#
BaseDeDatos.ps1 - operaciones del instalador sobre la instancia de SQL Server elegida.

    Cuentas        informa las cuentas de Windows que administran una instancia nueva de
                   SQL Server Express: el grupo de administradores del equipo (con su
                   nombre en el idioma de Windows) y quien inicio la instalacion.
    Diagnosticar   comprueba que la instancia sirva para una primera instalacion y decide
                   como se crea la base (MODO):
                     RESPALDO  la base no existe y el motor es SQL Server 2019 o superior:
                               se restaura el respaldo con los datos de demostracion;
                     ESQUEMA   la base no existe y el motor es anterior a 2019 (no puede
                               leer el respaldo): se la crea con db\schema.sql;
                     VACIA     la base ya existe pero esta vacia (la creo el administrador
                               de SQL Server para quien no puede crear bases) y el usuario
                               es su propietario: se le aplica db\schema.sql.
    Restaurar      crea la base restaurando el respaldo de demostracion (MODO RESPALDO).
    Crear          crea la base vacia y le aplica el esquema (MODO ESQUEMA).
    Cargar         aplica el esquema a la base vacia existente (MODO VACIA).
    Verificar      abre la base creada y cuenta sus tablas y sus cuentas.
    Quitar         deshace lo que hizo ESTA ejecucion (instalacion cancelada): elimina la
                   base que creo o, si cargo una base vacia existente, la deja vacia.

El resultado se escribe en el archivo -Salida_704ILR como lineas CLAVE=valor (UTF-8
con marca de orden de bytes, que es como lo lee el asistente) y en el codigo de
salida: 0 correcto, 1 condicion prevista que impide continuar, 2 error no previsto.

Ninguna operacion necesita que Windows de permisos de administrador: alcanza con que
el usuario pueda crear bases en la instancia (o sea propietario de la base vacia).

Usa el proveedor de SQL Server de .NET Framework, que viene con Windows: el equipo
no necesita sqlcmd ni ninguna otra herramienta. La conexion es siempre con la
autenticacion integrada de Windows, como la de la aplicacion.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Cuentas', 'Diagnosticar', 'Restaurar', 'Crear', 'Cargar', 'Verificar', 'Quitar')]
    [string]$Accion_704ILR,

    [Parameter(Mandatory = $true)]
    [string]$Instancia_704ILR,

    [Parameter(Mandatory = $true)]
    [string]$Salida_704ILR,

    # Restaurar: el respaldo, en una carpeta que el servicio de SQL Server pueda leer.
    [string]$Respaldo_704ILR = '',

    # Crear y Cargar: el guion del esquema (db\schema.sql).
    [string]$Esquema_704ILR = '',

    # Quitar: la marca que informo Restaurar, Crear o Cargar. Solo se deshace lo que la tenga.
    [string]$Marca_704ILR = ''
)

$ErrorActionPreference = 'Stop'

$Base_704ILR = 'EvenTechDB'
# El respaldo se genera con SQL Server 2019 y un motor anterior no puede restaurarlo:
# con un motor anterior la base se crea con el esquema, que pide SQL Server 2012.
$VersionDelRespaldo_704ILR = 15
$VersionMinima_704ILR = 11
$CodigoBaseExistente_704ILR = 'BASE_EXISTENTE'
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

# ------------------------------------------------------------------------ Cuentas
function Cuentas_704ILR {
    # El grupo se busca por su identificador (S-1-5-32-544): su nombre cambia con el
    # idioma de Windows (Administradores, Administrators...).
    $administradores_704ILR = (New-Object System.Security.Principal.SecurityIdentifier('S-1-5-32-544')).Translate([System.Security.Principal.NTAccount]).Value
    Anotar_704ILR 'ADMINISTRADORES' $administradores_704ILR
    Anotar_704ILR 'USUARIO' ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

# ----------------------------------------------------------------- Diagnosticar
function Diagnosticar_704ILR {
    try { $conexion_704ILR = Conectar_704ILR 'master' }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }

    $usuario_704ILR = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
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
        $puedeCrear_704ILR = ([int]$fila_704ILR['EsAdministrador'] -eq 1) -or ([int]$fila_704ILR['CreaBases'] -eq 1)

        if ([int]$fila_704ILR['BaseExiste'] -eq 1) {
            # Una base existente solo se usa si esta vacia y el usuario es su propietario
            # (o administrador de la instancia): es la que prepara el administrador de SQL
            # Server para quien no puede crear bases. Cualquier otra no se toca.
            $vacia_704ILR = $false
            try {
                $enLaBase_704ILR = Conectar_704ILR $Base_704ILR
                try {
                    $propietario_704ILR = [int](Escalar_704ILR $enLaBase_704ILR "SELECT ISNULL(IS_ROLEMEMBER('db_owner'), 0)" $null)
                    $vacia_704ILR = ((ObjetosPropios_704ILR $enLaBase_704ILR) -eq 0) -and
                        (($propietario_704ILR -eq 1) -or ([int]$fila_704ILR['EsAdministrador'] -eq 1))
                }
                finally { $enLaBase_704ILR.Dispose() }
            }
            catch { $vacia_704ILR = $false }
            if (-not $vacia_704ILR) { Rechazar_704ILR $CodigoBaseExistente_704ILR $Base_704ILR }
            Anotar_704ILR 'MODO' 'VACIA'
        }
        elseif (-not $puedeCrear_704ILR) {
            Rechazar_704ILR 'SIN_PERMISO' $usuario_704ILR
        }
        elseif ($mayor_704ILR -lt $VersionDelRespaldo_704ILR) {
            Anotar_704ILR 'MODO' 'ESQUEMA'
        }
        else {
            Anotar_704ILR 'MODO' 'RESPALDO'
        }
    }
    finally { $conexion_704ILR.Dispose() }

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
    # RESTORE no la reemplaza por si solo una base distinta, pero si la existente es la
    # misma base del respaldo y esta en modo de recuperacion simple, la pisaria sin
    # avisar: esta version del asistente nunca modifica una base existente.
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

# Quien lee el respaldo no es quien instala sino la cuenta del servicio de SQL Server.
# El asistente lo deja en una carpeta de datos de programas del equipo; aca se le agrega
# permiso de lectura para todos (el respaldo solo tiene los datos de demostracion y se
# borra al terminar), asi lo lee cualquiera sea la cuenta con que corre el servicio.
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

        # Si quien instala es administrador del equipo, el respaldo se copia a la carpeta
        # de respaldos de la propia instancia, que el motor siempre puede leer (y se borra
        # esa copia al terminar). Si no lo es, no puede escribir ahi: se restaura desde la
        # carpeta donde lo dejo el asistente, con permiso de lectura para el servicio.
        PermitirLectura_704ILR $Respaldo_704ILR
        $origen_704ILR = $Respaldo_704ILR
        $copia_704ILR = $null
        $carpeta_704ILR = [string](Escalar_704ILR $conexion_704ILR "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(400))" $null)
        if ($carpeta_704ILR) {
            $destino_704ILR = $carpeta_704ILR.TrimEnd('\') + '\EvenTech_instalacion_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.bak'
            try {
                Copy-Item -LiteralPath $Respaldo_704ILR -Destination $destino_704ILR -Force
                $copia_704ILR = $destino_704ILR
                $origen_704ILR = $destino_704ILR
            }
            catch { }
        }

        $falla_704ILR = $null
        try { RestaurarDesde_704ILR $conexion_704ILR $origen_704ILR }
        catch { $falla_704ILR = $_.Exception.GetBaseException() }

        if ($copia_704ILR -and (Test-Path -LiteralPath $copia_704ILR)) {
            Remove-Item -LiteralPath $copia_704ILR -Force -ErrorAction SilentlyContinue
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
        Anotar_704ILR 'ORIGEN' $(if ($copia_704ILR) { 'CARPETA_DE_RESPALDOS' } else { 'CARPETA_DE_APOYO' })
        Anotar_704ILR 'MARCA' (MarcaDeCreacion_704ILR $conexion_704ILR)
    }
    finally { $conexion_704ILR.Dispose() }

    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

function MarcaDeCreacion_704ILR($conexion_704ILR) {
    return [string](Escalar_704ILR $conexion_704ILR 'SELECT CONVERT(NVARCHAR(30), create_date, 126) FROM sys.databases WHERE name = @base' @{ base = $Base_704ILR })
}

# ---------------------------------------------------------------- Crear y Cargar
# Ejecuta db\schema.sql sobre la base de la conexion, lote por lote (los lotes se separan
# con GO, como en sqlcmd). Un error corta la ejecucion.
function AplicarEsquema_704ILR($conexion_704ILR) {
    if (-not $Esquema_704ILR -or -not (Test-Path -LiteralPath $Esquema_704ILR)) {
        throw ('No se encuentra el guion del esquema: ' + $Esquema_704ILR)
    }
    $texto_704ILR = [System.IO.File]::ReadAllText($Esquema_704ILR, [System.Text.Encoding]::UTF8)
    $lotes_704ILR = [System.Text.RegularExpressions.Regex]::Split($texto_704ILR, '^[ \t]*GO[ \t]*\r?$',
        [System.Text.RegularExpressions.RegexOptions]::Multiline -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $n_704ILR = 0
    foreach ($lote_704ILR in $lotes_704ILR) {
        if ($lote_704ILR.Trim().Length -eq 0) { continue }
        $n_704ILR++
        try { (Comando_704ILR $conexion_704ILR $lote_704ILR $null).ExecuteNonQuery() | Out-Null }
        catch { throw ('Lote ' + $n_704ILR + ' del esquema: ' + $_.Exception.GetBaseException().Message) }
    }
    Anotar_704ILR 'LOTES' $n_704ILR
}

function Crear_704ILR {
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
            Rechazar_704ILR 'CREACION_FALLIDA' $_.Exception.GetBaseException().Message
        }
        $marca_704ILR = MarcaDeCreacion_704ILR $conexion_704ILR
    }
    finally { $conexion_704ILR.Dispose() }

    $falla_704ILR = $null
    try {
        $enLaBase_704ILR = Conectar_704ILR $Base_704ILR
        try { AplicarEsquema_704ILR $enLaBase_704ILR } finally { $enLaBase_704ILR.Dispose() }
    }
    catch { $falla_704ILR = $_.Exception.GetBaseException().Message }

    if ($falla_704ILR) {
        # La base la creo este paso: si el esquema no se pudo aplicar, se la quita.
        try {
            $conexion_704ILR = Conectar_704ILR 'master'
            try { QuitarBase_704ILR $conexion_704ILR $marca_704ILR | Out-Null } finally { $conexion_704ILR.Dispose() }
        }
        catch { }
        Rechazar_704ILR 'CREACION_FALLIDA' $falla_704ILR
    }

    Anotar_704ILR 'MARCA' $marca_704ILR
    Anotar_704ILR 'ESTADO' 'CORRECTO'
    Terminar_704ILR 0
}

function Cargar_704ILR {
    try { $conexion_704ILR = Conectar_704ILR $Base_704ILR }
    catch { Rechazar_704ILR 'SIN_CONEXION' $_.Exception.GetBaseException().Message }

    try {
        # Entre el diagnostico y este paso alguien pudo haber creado algo en la base.
        if ((ObjetosPropios_704ILR $conexion_704ILR) -ne 0) { Rechazar_704ILR $CodigoBaseExistente_704ILR $Base_704ILR }
        # Todo objeto creado desde esta hora es de esta ejecucion (lo que Quitar deshace).
        $desde_704ILR = [string](Escalar_704ILR $conexion_704ILR 'SELECT CONVERT(NVARCHAR(30), DATEADD(SECOND, -1, GETDATE()), 126)' $null)
        try { AplicarEsquema_704ILR $conexion_704ILR }
        catch {
            $falla_704ILR = $_.Exception.GetBaseException().Message
            try { VaciarBase_704ILR $conexion_704ILR $desde_704ILR | Out-Null } catch { }
            Rechazar_704ILR 'CREACION_FALLIDA' $falla_704ILR
        }
    }
    finally { $conexion_704ILR.Dispose() }

    Anotar_704ILR 'MARCA' ($PrefijoVacia_704ILR + $desde_704ILR)
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
function QuitarBase_704ILR($conexion_704ILR, [string]$marca_704ILR) {
    if (-not $marca_704ILR) { return $false }
    $coincide_704ILR = Escalar_704ILR $conexion_704ILR 'SELECT COUNT(*) FROM sys.databases WHERE name = @base AND CONVERT(NVARCHAR(30), create_date, 126) = @marca' @{ base = $Base_704ILR; marca = $marca_704ILR }
    if ([int]$coincide_704ILR -ne 1) { return $false }
    (Comando_704ILR $conexion_704ILR "ALTER DATABASE [$Base_704ILR] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$Base_704ILR];" $null).ExecuteNonQuery() | Out-Null
    return $true
}

# Deja vacia la base existente que cargo esta ejecucion: elimina las claves foraneas y las
# tablas creadas desde la marca, y solo si no hay ningun objeto propio anterior a ella.
function VaciarBase_704ILR($conexion_704ILR, [string]$desde_704ILR) {
    $anteriores_704ILR = [int](Escalar_704ILR $conexion_704ILR 'SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped = 0 AND parent_object_id = 0 AND create_date < CONVERT(DATETIME, @desde, 126)' @{ desde = $desde_704ILR })
    if ($anteriores_704ILR -ne 0) { return $false }
    (Comando_704ILR $conexion_704ILR @"
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

try {
    switch ($Accion_704ILR) {
        'Cuentas' { Cuentas_704ILR }
        'Diagnosticar' { Diagnosticar_704ILR }
        'Restaurar' { Restaurar_704ILR }
        'Crear' { Crear_704ILR }
        'Cargar' { Cargar_704ILR }
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
