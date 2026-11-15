<#
PruebaIntegridad.ps1 - altera la base del sistema POR FUERA de la aplicacion, para
comprobar que la verificacion de los digitos verificadores lo detecta.

Es la prueba de la catedra: se "rompe" la base, al abrir EvenTech aparece la alerta
de integridad y, al ingresar con una cuenta que tenga permiso (admin / admin123), el
sistema ofrece las dos salidas:
  - Restaurar un respaldo: la base vuelve al respaldo que este guion toma ANTES de
    alterarla (motivo "Antes de alterar los datos", en la pestana Respaldos).
  - Recalcular los digitos verificadores: los datos alterados pasan a ser la
    referencia nueva.

Que altera (se elige en el menu, o con -Alteracion_704ILR):
  Monto    suma 1000 al monto de una reserva confirmada: delata el DV horizontal de
           esa reserva ("Reserva #N: el DV horizontal no coincide").
  Pago     borra el ultimo pago registrado: delata el DV vertical de los pagos ("El DV
           vertical de Pagos no coincide").
  Ambos    las dos cosas.

La base es la misma con la que trabaja la aplicacion en este equipo y con este
usuario de Windows (la conexion que guardo el instalador, %APPDATA%\EvenTech\
connection.cfg); sin conexion guardada, la de fabrica (localhost\SQLEXPRESS,
EvenTechDB). Con -Instancia_704ILR y -Base_704ILR se elige otra.

Antes de alterar toma un respaldo completo de la base en la carpeta de respaldos del
sistema (%ProgramData%\EvenTech\respaldos), la misma que lista la aplicacion. Con
-SinRespaldo_704ILR no lo toma (para probar que sin respaldo queda recalcular).

Uso (o doble clic en "Prueba de integridad.cmd"):
    powershell -ExecutionPolicy Bypass -File PruebaIntegridad.ps1
    powershell -ExecutionPolicy Bypass -File PruebaIntegridad.ps1 -Alteracion_704ILR Ambos -SinConfirmar_704ILR

No necesita permisos de administrador del equipo: alcanza con que el usuario de
Windows pueda escribir en la base (el que instalo el sistema puede). Usa el proveedor
de SQL Server de .NET Framework, que viene con Windows (no hace falta sqlcmd).
Guardado en ASCII (Windows PowerShell 5.1 lee un UTF-8 sin BOM como ANSI).
#>
param(
    [string]$Instancia_704ILR = '',
    [string]$Base_704ILR = '',
    [ValidateSet('', 'Monto', 'Pago', 'Ambos')]
    [string]$Alteracion_704ILR = '',
    [switch]$SinRespaldo_704ILR,
    [switch]$SinConfirmar_704ILR
)

$ErrorActionPreference = 'Stop'
$InstanciaDeFabrica_704ILR = 'localhost\SQLEXPRESS'
$BaseDeFabrica_704ILR = 'EvenTechDB'

function Titulo_704ILR([string]$texto_704ILR) {
    Write-Host ''
    Write-Host ('== ' + $texto_704ILR) -ForegroundColor Cyan
}

# ------------------------------------------------------------------ conexion
# La de la aplicacion: %APPDATA%\EvenTech\connection.cfg, la cadena en UTF-8 cifrada con
# DPAPI del usuario (EvenTech.Services\ConfiguracionConexion_704ILR.cs).
function ConexionDeLaAplicacion_704ILR {
    $archivo_704ILR = Join-Path ([System.Environment]::GetFolderPath('ApplicationData')) 'EvenTech\connection.cfg'
    if (-not (Test-Path -LiteralPath $archivo_704ILR)) { return $null }
    try {
        Add-Type -AssemblyName System.Security
        $plano_704ILR = [System.Security.Cryptography.ProtectedData]::Unprotect(
            [System.IO.File]::ReadAllBytes($archivo_704ILR), $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
        $cadena_704ILR = New-Object System.Data.SqlClient.SqlConnectionStringBuilder([System.Text.Encoding]::UTF8.GetString($plano_704ILR))
        return @{ Instancia = $cadena_704ILR.DataSource; Base = $cadena_704ILR.InitialCatalog }
    }
    catch {
        Write-Warning ('No se pudo leer la conexion guardada (' + $archivo_704ILR + '): se usa la de fabrica.')
        return $null
    }
}

function Conectar_704ILR([string]$catalogo_704ILR) {
    $cadena_704ILR = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $cadena_704ILR['Data Source'] = $Instancia_704ILR
    $cadena_704ILR['Initial Catalog'] = $catalogo_704ILR
    $cadena_704ILR['Integrated Security'] = $true
    $cadena_704ILR['TrustServerCertificate'] = $true
    $cadena_704ILR['Connect Timeout'] = 8
    $cadena_704ILR['Pooling'] = $false
    $cadena_704ILR['Application Name'] = 'EvenTech - prueba de integridad'
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
            [void]$comando_704ILR.Parameters.AddWithValue('@' + $nombre_704ILR, $parametros_704ILR[$nombre_704ILR])
        }
    }
    return $comando_704ILR
}

function Escalar_704ILR($conexion_704ILR, [string]$sql_704ILR, [hashtable]$parametros_704ILR) {
    $valor_704ILR = (Comando_704ILR $conexion_704ILR $sql_704ILR $parametros_704ILR).ExecuteScalar()
    if ($null -eq $valor_704ILR -or $valor_704ILR -is [System.DBNull]) { return $null }
    return $valor_704ILR
}

function Fila_704ILR($conexion_704ILR, [string]$sql_704ILR, [hashtable]$parametros_704ILR) {
    $tabla_704ILR = New-Object System.Data.DataTable
    $lector_704ILR = (Comando_704ILR $conexion_704ILR $sql_704ILR $parametros_704ILR).ExecuteReader()
    try { $tabla_704ILR.Load($lector_704ILR) } finally { $lector_704ILR.Close() }
    if ($tabla_704ILR.Rows.Count -eq 0) { return $null }
    return $tabla_704ILR.Rows[0]
}

# --------------------------------------------------------------------- respaldo
# Misma carpeta y mismo permiso que la aplicacion (BLL_Respaldo_704ILR): el archivo lo
# escribe la cuenta del servicio de SQL Server, a la que se le da permiso en la carpeta.
function PrepararCarpeta_704ILR($master_704ILR) {
    $carpeta_704ILR = Join-Path ([System.Environment]::GetFolderPath('CommonApplicationData')) 'EvenTech\respaldos'
    [System.IO.Directory]::CreateDirectory($carpeta_704ILR) | Out-Null
    $instancia_704ILR = [string](Escalar_704ILR $master_704ILR "SELECT CAST(SERVERPROPERTY('InstanceName') AS NVARCHAR(128))" $null)
    $servicio_704ILR = if ($instancia_704ILR) { 'NT SERVICE\MSSQL$' + $instancia_704ILR } else { 'NT SERVICE\MSSQLSERVER' }
    try {
        $sid_704ILR = (New-Object System.Security.Principal.NTAccount($servicio_704ILR)).Translate([System.Security.Principal.SecurityIdentifier])
        $acl_704ILR = Get-Acl -LiteralPath $carpeta_704ILR
        $regla_704ILR = New-Object System.Security.AccessControl.FileSystemAccessRule($sid_704ILR, 'Modify', 'ContainerInherit, ObjectInherit', 'None', 'Allow')
        $acl_704ILR.AddAccessRule($regla_704ILR)
        Set-Acl -LiteralPath $carpeta_704ILR -AclObject $acl_704ILR
    }
    catch { }   # si no se pudo, BACKUP informa que no puede escribir
    return $carpeta_704ILR
}

function Respaldar_704ILR($master_704ILR) {
    $carpeta_704ILR = PrepararCarpeta_704ILR $master_704ILR
    $ruta_704ILR = Join-Path $carpeta_704ILR ($Base_704ILR + '_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '_antes_de_alterar.bak')
    $nombreBase_704ILR = '[' + $Base_704ILR.Replace(']', ']]') + ']'
    (Comando_704ILR $master_704ILR ("BACKUP DATABASE " + $nombreBase_704ILR + " TO DISK = @ruta WITH COPY_ONLY, CHECKSUM, INIT, FORMAT, NAME = @nombre, DESCRIPTION = @descripcion") @{
        ruta = $ruta_704ILR; nombre = 'EvenTech - antes de alterar los datos'; descripcion = 'ANTES_DE_ALTERAR' }).ExecuteNonQuery() | Out-Null
    (Comando_704ILR $master_704ILR 'RESTORE VERIFYONLY FROM DISK = @ruta WITH CHECKSUM' @{ ruta = $ruta_704ILR }).ExecuteNonQuery() | Out-Null
    return $ruta_704ILR
}

# ------------------------------------------------------------------- principal
try {
    if (-not $Instancia_704ILR -or -not $Base_704ILR) {
        $app_704ILR = ConexionDeLaAplicacion_704ILR
        if (-not $Instancia_704ILR) { $Instancia_704ILR = if ($app_704ILR -and $app_704ILR.Instancia) { $app_704ILR.Instancia } else { $InstanciaDeFabrica_704ILR } }
        if (-not $Base_704ILR) { $Base_704ILR = if ($app_704ILR -and $app_704ILR.Base) { $app_704ILR.Base } else { $BaseDeFabrica_704ILR } }
    }

    Titulo_704ILR 'EvenTech - prueba de los digitos verificadores'
    Write-Host ('Instancia de SQL Server: ' + $Instancia_704ILR)
    Write-Host ('Base de datos:           ' + $Base_704ILR)

    try { $master_704ILR = Conectar_704ILR 'master' }
    catch { throw ('No se pudo conectar con la instancia ' + $Instancia_704ILR + ': ' + $_.Exception.GetBaseException().Message) }
    try {
        if ($null -eq (Escalar_704ILR $master_704ILR 'SELECT DB_ID(@b)' @{ b = $Base_704ILR })) {
            throw ('La instancia ' + $Instancia_704ILR + ' no tiene la base ' + $Base_704ILR + '.')
        }
        $conexionBase_704ILR = Conectar_704ILR $Base_704ILR
        try {
            $esDelSistema_704ILR = [int](Escalar_704ILR $conexionBase_704ILR "SELECT CASE WHEN OBJECT_ID(N'dbo.Reservas', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Pagos', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.DVVertical', N'U') IS NOT NULL THEN 1 ELSE 0 END" $null)
            if ($esDelSistema_704ILR -ne 1) { throw ('La base ' + $Base_704ILR + ' no es una base de EvenTech.') }

            $reserva_704ILR = Fila_704ILR $conexionBase_704ILR "SELECT TOP 1 Id, Monto, Estado FROM dbo.Reservas WHERE Dvh IS NOT NULL ORDER BY CASE Estado WHEN N'CONFIRMADA' THEN 0 ELSE 1 END, Id" $null
            $pago_704ILR = Fila_704ILR $conexionBase_704ILR "SELECT TOP 1 Id, ReservaId, Monto FROM dbo.Pagos ORDER BY Id DESC" $null
            Write-Host ('Reservas: ' + (Escalar_704ILR $conexionBase_704ILR 'SELECT COUNT(*) FROM dbo.Reservas' $null) + ' | Pagos: ' + (Escalar_704ILR $conexionBase_704ILR 'SELECT COUNT(*) FROM dbo.Pagos' $null))

            if (-not $Alteracion_704ILR) {
                Titulo_704ILR 'Que se altera'
                if ($reserva_704ILR) { Write-Host ('  1) El monto de la reserva #' + $reserva_704ILR['Id'] + ' (' + $reserva_704ILR['Estado'] + ', $ ' + $reserva_704ILR['Monto'] + '): se le suman $ 1000. Lo delata el DV horizontal.') }
                if ($pago_704ILR) { Write-Host ('  2) Se borra el pago #' + $pago_704ILR['Id'] + ' (reserva #' + $pago_704ILR['ReservaId'] + ', $ ' + $pago_704ILR['Monto'] + '). Lo delata el DV vertical de los pagos.') }
                Write-Host '  3) Las dos cosas.'
                Write-Host '  0) Salir sin alterar nada.'
                $opcion_704ILR = Read-Host 'Opcion'
                switch ($opcion_704ILR.Trim()) {
                    '1' { $Alteracion_704ILR = 'Monto' }
                    '2' { $Alteracion_704ILR = 'Pago' }
                    '3' { $Alteracion_704ILR = 'Ambos' }
                    default { Write-Host 'No se altero nada.'; exit 0 }
                }
            }
            $alteraMonto_704ILR = $Alteracion_704ILR -in @('Monto', 'Ambos')
            $alteraPago_704ILR = $Alteracion_704ILR -in @('Pago', 'Ambos')
            if ($alteraMonto_704ILR -and -not $reserva_704ILR) { throw 'La base no tiene ninguna reserva con digito verificador.' }
            if ($alteraPago_704ILR -and -not $pago_704ILR) { throw 'La base no tiene ningun pago.' }

            if (-not $SinConfirmar_704ILR) {
                $si_704ILR = Read-Host ('Se va a alterar la base ' + $Base_704ILR + ' de la instancia ' + $Instancia_704ILR + ' por fuera del sistema. Escriba SI para continuar')
                if ($si_704ILR.Trim().ToUpperInvariant() -ne 'SI') { Write-Host 'No se altero nada.'; exit 0 }
            }

            if ($SinRespaldo_704ILR) {
                Titulo_704ILR 'Respaldo previo: omitido (-SinRespaldo_704ILR)'
            }
            else {
                Titulo_704ILR 'Respaldo previo'
                $respaldo_704ILR = Respaldar_704ILR $master_704ILR
                Write-Host ('Respaldo completo y verificado: ' + $respaldo_704ILR)
            }

            Titulo_704ILR 'Alteracion por fuera del sistema'
            if ($alteraMonto_704ILR) {
                (Comando_704ILR $conexionBase_704ILR 'UPDATE dbo.Reservas SET Monto = Monto + 1000 WHERE Id = @id' @{ id = [int]$reserva_704ILR['Id'] }).ExecuteNonQuery() | Out-Null
                $nuevo_704ILR = Escalar_704ILR $conexionBase_704ILR 'SELECT Monto FROM dbo.Reservas WHERE Id = @id' @{ id = [int]$reserva_704ILR['Id'] }
                Write-Host ('Reserva #' + $reserva_704ILR['Id'] + ': monto $ ' + $reserva_704ILR['Monto'] + ' -> $ ' + $nuevo_704ILR + ' (el DV horizontal guardado ya no coincide).')
            }
            if ($alteraPago_704ILR) {
                (Comando_704ILR $conexionBase_704ILR 'DELETE FROM dbo.Pagos WHERE Id = @id' @{ id = [int]$pago_704ILR['Id'] }).ExecuteNonQuery() | Out-Null
                Write-Host ('Pago #' + $pago_704ILR['Id'] + ' borrado (el DV vertical de los pagos guardado ya no coincide).')
            }
        }
        finally { $conexionBase_704ILR.Dispose() }
    }
    finally { $master_704ILR.Dispose() }

    Titulo_704ILR 'Y ahora'
    Write-Host '1. Abra EvenTech: al iniciar aparece la alerta "Se detectaron problemas de integridad",'
    Write-Host '   con lo que se altero. Presione "Continuar al ingreso".'
    Write-Host '2. Una cuenta sin permiso no puede ingresar. Ingrese con admin / admin123.'
    Write-Host '3. Elija una de las dos salidas:'
    Write-Host '   - "Restaurar un respaldo...": elija el respaldo "Antes de alterar los datos" y'
    Write-Host '     confirme. La base vuelve a ese momento y el sistema se reinicia sin alerta.'
    Write-Host '   - "Recalcular digitos verificadores": los datos alterados pasan a ser la'
    Write-Host '     referencia nueva y se ingresa al sistema.'
    Write-Host '   La bitacora (Auditoria) registra la verificacion fallida y lo que se hizo.'
    exit 0
}
catch {
    Write-Host ''
    Write-Host ('ERROR: ' + $_.Exception.GetBaseException().Message) -ForegroundColor Red
    exit 1
}
