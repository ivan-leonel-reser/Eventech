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

Resultado: archivo -Salida_704ILR con lineas CLAVE=valor y codigo de salida 0 / 2.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Instancia_704ILR,

    [Parameter(Mandatory = $true)]
    [string]$Salida_704ILR,

    [string]$Base_704ILR = 'EvenTechDB'
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

    $plano_704ILR = [System.Text.Encoding]::UTF8.GetBytes($cadena_704ILR.ConnectionString)
    $cifrado_704ILR = [System.Security.Cryptography.ProtectedData]::Protect(
        $plano_704ILR, $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
    [System.IO.File]::WriteAllBytes($archivo_704ILR, $cifrado_704ILR)

    $Resultado_704ILR.Add('ESTADO=CORRECTO')
    $Resultado_704ILR.Add('ARCHIVO=' + $archivo_704ILR)
    Terminar_704ILR 0
}
catch {
    $Resultado_704ILR.Add('ESTADO=ERROR')
    $Resultado_704ILR.Add('CODIGO=CONEXION_NO_GUARDADA')
    $Resultado_704ILR.Add('DETALLE=' + (($_.Exception.GetBaseException().Message) -replace '\s+', ' '))
    Terminar_704ILR 2
}
