<#
Conexion.ps1 - deja configurada la conexion de la aplicacion a la instancia elegida.

Escribe %APPDATA%\EvenTech\connection.cfg con el mismo formato que usa la aplicacion
(EvenTech.Services\ConfiguracionConexion_704ILR.cs): la cadena de conexion en UTF-8,
cifrada con DPAPI en el ambito del usuario de Windows. Por eso el asistente lo
ejecuta con la cuenta de quien inicio la instalacion, no con la elevada: el archivo
tiene que quedar en el perfil de quien va a usar el sistema, que es el unico que
puede leerlo.

La cadena se arma igual que en la aplicacion: autenticacion integrada de Windows,
sin usuario ni clave de SQL Server.

Con -Conservar_704ILR (reinstalacion): si el usuario ya tiene una conexion guardada que
apunta a la misma instancia y a la misma base, se la deja como esta (CONSERVADA=1).

Resultado: archivo -Salida_704ILR con lineas CLAVE=valor y codigo de salida 0 / 2.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Instancia_704ILR,

    [Parameter(Mandatory = $true)]
    [string]$Salida_704ILR,

    [string]$Base_704ILR = 'EvenTechDB',

    [switch]$Conservar_704ILR
)

$ErrorActionPreference = 'Stop'
$Resultado_704ILR = New-Object System.Collections.Generic.List[string]

function Terminar_704ILR([int]$codigoDeSalida_704ILR) {
    $utf8_704ILR = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllLines($Salida_704ILR, $Resultado_704ILR, $utf8_704ILR)
    exit $codigoDeSalida_704ILR
}

try {
    Add-Type -AssemblyName System.Security

    $cadena_704ILR = New-Object System.Data.Common.DbConnectionStringBuilder
    $cadena_704ILR['Data Source'] = $Instancia_704ILR.Trim()
    $cadena_704ILR['Initial Catalog'] = $Base_704ILR.Trim()
    $cadena_704ILR['Integrated Security'] = 'True'
    $cadena_704ILR['TrustServerCertificate'] = 'True'

    $carpeta_704ILR = Join-Path ([System.Environment]::GetFolderPath('ApplicationData')) 'EvenTech'
    $archivo_704ILR = Join-Path $carpeta_704ILR 'connection.cfg'
    [System.IO.Directory]::CreateDirectory($carpeta_704ILR) | Out-Null

    if ($Conservar_704ILR -and (Test-Path -LiteralPath $archivo_704ILR)) {
        try {
            $previo_704ILR = [System.Text.Encoding]::UTF8.GetString([System.Security.Cryptography.ProtectedData]::Unprotect(
                [System.IO.File]::ReadAllBytes($archivo_704ILR), $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser))
            $leida_704ILR = New-Object System.Data.Common.DbConnectionStringBuilder
            # Es un diccionario para PowerShell: asignar .ConnectionString crearia una clave con
            # ese nombre. Se usa el metodo de la propiedad.
            $leida_704ILR.set_ConnectionString($previo_704ILR)
            $servidor_704ILR = ''; $base_704ILR = ''
            foreach ($k_704ILR in @('Data Source', 'Server', 'Address', 'Addr', 'Network Address')) { if ($leida_704ILR.ContainsKey($k_704ILR)) { $servidor_704ILR = [string]$leida_704ILR[$k_704ILR] } }
            foreach ($k_704ILR in @('Initial Catalog', 'Database')) { if ($leida_704ILR.ContainsKey($k_704ILR)) { $base_704ILR = [string]$leida_704ILR[$k_704ILR] } }
            if (($servidor_704ILR.Trim() -ieq $Instancia_704ILR.Trim()) -and ($base_704ILR.Trim() -ieq $Base_704ILR.Trim())) {
                $Resultado_704ILR.Add('ESTADO=CORRECTO')
                $Resultado_704ILR.Add('CONSERVADA=1')
                $Resultado_704ILR.Add('ARCHIVO=' + $archivo_704ILR)
                Terminar_704ILR 0
            }
        }
        catch { }   # ilegible (de otro usuario o danado): se escribe de nuevo
    }

    $plano_704ILR = [System.Text.Encoding]::UTF8.GetBytes($cadena_704ILR.ConnectionString)
    $cifrado_704ILR = [System.Security.Cryptography.ProtectedData]::Protect(
        $plano_704ILR, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    [System.IO.File]::WriteAllBytes($archivo_704ILR, $cifrado_704ILR)

    $Resultado_704ILR.Add('ESTADO=CORRECTO')
    $Resultado_704ILR.Add('CONSERVADA=0')
    $Resultado_704ILR.Add('ARCHIVO=' + $archivo_704ILR)
    Terminar_704ILR 0
}
catch {
    $Resultado_704ILR.Add('ESTADO=ERROR')
    $Resultado_704ILR.Add('CODIGO=CONEXION_NO_GUARDADA')
    $Resultado_704ILR.Add('DETALLE=' + (($_.Exception.GetBaseException().Message) -replace '\s+', ' '))
    Terminar_704ILR 2
}
