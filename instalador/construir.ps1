<#
construir.ps1 - genera el instalador de EvenTech.

    1. Publica la aplicacion para Windows de 64 bits con el entorno de ejecucion de
       .NET 8 incluido (carpeta "publicado"): quien instala no necesita tener .NET.
    2. Compila EvenTech.iss con Inno Setup 6. El instalador lleva adentro la
       aplicacion publicada, el respaldo db\EvenTechDB.bak y SQL Server Express, que
       instala si el equipo no tiene SQL Server: quien instala no necesita nada mas.
    3. Deja el instalador en la carpeta "salida" e informa su tamano y su SHA-256.

Uso (PowerShell, desde cualquier carpeta):
    powershell -ExecutionPolicy Bypass -File construir.ps1

Requisitos: SDK de .NET (8 o superior), Inno Setup 6 (winget install JRSoftware.InnoSetup)
y los medios de SQL Server Express en la carpeta "motor\SQLServerExpress" (excluida del
repositorio; ver README.md): la extraccion, sin modificar, de los medios "Core" que baja el
instalador de SQL Server 2022 Express de Microsoft ("Descargar medios" > "Express Core").
Sin ellos el guion se detiene, salvo con -SinMotor_704ILR.

El instalador se genera DESPUES de regenerar db\EvenTechDB.bak: el respaldo viaja
adentro, y sus cotizaciones y reservas pendientes vencen por fecha (regla RN-01).
#>
param(
    # Compila el instalador con la carpeta "publicado" tal como esta, sin volver a publicar.
    [switch]$SinPublicar_704ILR,

    # Carpeta con los medios de SQL Server Express que el instalador lleva adentro.
    [string]$MediosSqlExpress_704ILR = '',

    # Genera el instalador SIN SQL Server Express (solo para pruebas: un equipo sin SQL
    # Server no lo podria usar).
    [switch]$SinMotor_704ILR
)

$ErrorActionPreference = 'Stop'

$Aqui_704ILR = $PSScriptRoot
$Repositorio_704ILR = Split-Path $Aqui_704ILR -Parent
$Proyecto_704ILR = Join-Path $Repositorio_704ILR 'EvenTech.UI\EvenTech.UI.csproj'
$Respaldo_704ILR = Join-Path $Repositorio_704ILR 'db\EvenTechDB.bak'
$Guion_704ILR = Join-Path $Aqui_704ILR 'EvenTech.iss'
$Publicado_704ILR = Join-Path $Aqui_704ILR 'publicado'
$Salida_704ILR = Join-Path $Aqui_704ILR 'salida'

function Paso_704ILR([string]$titulo_704ILR) {
    Write-Host ''
    Write-Host ('=== ' + $titulo_704ILR) -ForegroundColor Cyan
}

foreach ($ruta_704ILR in @($Proyecto_704ILR, $Respaldo_704ILR, $Guion_704ILR)) {
    if (-not (Test-Path -LiteralPath $ruta_704ILR)) { throw "No se encuentra: $ruta_704ILR" }
}

# Los textos del asistente llevan tildes: Inno Setup solo las lee bien si el guion
# esta guardado como UTF-8 con marca de orden de bytes.
$inicio_704ILR = [System.IO.File]::ReadAllBytes($Guion_704ILR) | Select-Object -First 3
if (-not ($inicio_704ILR.Count -eq 3 -and $inicio_704ILR[0] -eq 0xEF -and $inicio_704ILR[1] -eq 0xBB -and $inicio_704ILR[2] -eq 0xBF)) {
    throw 'EvenTech.iss tiene que estar guardado como UTF-8 con marca de orden de bytes (BOM).'
}

$Compilador_704ILR = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $Compilador_704ILR) {
    $enRuta_704ILR = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($enRuta_704ILR) { $Compilador_704ILR = $enRuta_704ILR.Source }
}
if (-not $Compilador_704ILR) { throw 'No se encuentra Inno Setup 6 (ISCC.exe). Se instala con: winget install JRSoftware.InnoSetup' }

# Medios de SQL Server Express: se incluyen tal como los distribuye Microsoft (su licencia
# lo exige), asi que antes se comprueba que esten completos y firmados por Microsoft.
if (-not $MediosSqlExpress_704ILR) { $MediosSqlExpress_704ILR = Join-Path $Aqui_704ILR 'motor\SQLServerExpress' }
$DefinicionMotor_704ILR = $null
if ($SinMotor_704ILR) {
    Write-Warning 'Instalador SIN SQL Server Express (-SinMotor_704ILR): en un equipo sin SQL Server no se puede usar.'
}
else {
    $Setup_704ILR = Join-Path $MediosSqlExpress_704ILR 'SETUP.EXE'
    $Info_704ILR = Join-Path $MediosSqlExpress_704ILR 'MEDIAINFO.XML'
    if (-not (Test-Path -LiteralPath $Setup_704ILR) -or -not (Test-Path -LiteralPath $Info_704ILR)) {
        throw "No se encuentran los medios de SQL Server Express en $MediosSqlExpress_704ILR (SETUP.EXE y MEDIAINFO.XML). Ver instalador\README.md, o usar -SinMotor_704ILR para un instalador de prueba."
    }
    $firmados_704ILR = @(Get-ChildItem -LiteralPath $MediosSqlExpress_704ILR -Recurse -File |
        Where-Object { $_.Extension -in '.exe', '.dll', '.msi', '.cab' })
    $sinFirma_704ILR = @($firmados_704ILR | Where-Object {
        $f_704ILR = Get-AuthenticodeSignature -LiteralPath $_.FullName
        $f_704ILR.Status -ne 'Valid' -or $f_704ILR.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation'
    })
    if ($sinFirma_704ILR.Count -gt 0) {
        throw ('Hay medios de SQL Server Express sin una firma valida de Microsoft: ' + (($sinFirma_704ILR | Select-Object -First 5 | ForEach-Object { $_.Name }) -join ', '))
    }
    $sobrantes_704ILR = @(Get-ChildItem -LiteralPath $MediosSqlExpress_704ILR -Filter 'SqlServerInstallConfig.ini' -Recurse -File)
    if ($sobrantes_704ILR.Count -gt 0) {
        throw 'Los medios traen SqlServerInstallConfig.ini, que escribe una instalacion local (lleva la cuenta de ese equipo): no es parte de los medios de Microsoft.'
    }
    $DefinicionMotor_704ILR = '/DMediosMotor_704ILR=' + (Resolve-Path -LiteralPath $MediosSqlExpress_704ILR).Path
}

# --------------------------------------------------------------------------- 1
if ($SinPublicar_704ILR) {
    Paso_704ILR '1. Publicacion omitida (-SinPublicar_704ILR)'
}
else {
    Paso_704ILR '1. Publicar la aplicacion con .NET 8 incluido'
    if (Test-Path -LiteralPath $Publicado_704ILR) { Remove-Item -LiteralPath $Publicado_704ILR -Recurse -Force }
    # SatelliteResourceLanguages: de los textos del propio .NET solo viajan los del espanol.
    & dotnet publish $Proyecto_704ILR -c Release -r win-x64 --self-contained true `
        -p:SatelliteResourceLanguages=es -p:DebugType=none -o $Publicado_704ILR -nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'La publicacion de la aplicacion fallo.' }
}

$Ejecutable_704ILR = Join-Path $Publicado_704ILR 'EvenTech.UI.exe'
if (-not (Test-Path -LiteralPath $Ejecutable_704ILR)) { throw "No se encuentra la aplicacion publicada: $Ejecutable_704ILR" }
$archivos_704ILR = @(Get-ChildItem -LiteralPath $Publicado_704ILR -Recurse -File)
$megas_704ILR = ($archivos_704ILR | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host ('Aplicacion publicada: version {0}, {1} archivos, {2:N0} MB' -f `
    (Get-Item -LiteralPath $Ejecutable_704ILR).VersionInfo.FileVersion, $archivos_704ILR.Count, $megas_704ILR)
# Version de producto: version de la entrega + commit del que salio el codigo (ver
# Directory.Build.targets). Es lo que muestra la aplicacion para compararla con el
# repositorio, asi que el instalador de una entrega se arma con el repositorio limpio.
$Producto_704ILR = (Get-Item -LiteralPath $Ejecutable_704ILR).VersionInfo.ProductVersion
Write-Host ('Version de producto: {0}' -f $Producto_704ILR)
if ($Producto_704ILR -notmatch '\+[0-9a-f]{40}') {
    Write-Warning 'La aplicacion publicada no lleva el commit: no se va a poder comparar con el repositorio.'
}
elseif ($Producto_704ILR -like '*.modificado') {
    Write-Warning 'La aplicacion se publico con cambios sin commitear: no corresponde a ningun commit del repositorio.'
}

# --------------------------------------------------------------------------- 2
Paso_704ILR '2. Compilar el instalador'
Write-Host ('Respaldo incluido: {0} ({1:dd/MM/yyyy HH:mm})' -f $Respaldo_704ILR, (Get-Item -LiteralPath $Respaldo_704ILR).LastWriteTime)
$argumentosIscc_704ILR = @('/Q')
if ($DefinicionMotor_704ILR) {
    $medios_704ILR = @(Get-ChildItem -LiteralPath $MediosSqlExpress_704ILR -Recurse -File)
    Write-Host ('SQL Server Express incluido: version {0}, {1} archivos, {2:N0} MB, {3} con firma de Microsoft' -f `
        (Get-Item -LiteralPath $Setup_704ILR).VersionInfo.ProductVersion, $medios_704ILR.Count,
        (($medios_704ILR | Measure-Object -Property Length -Sum).Sum / 1MB), $firmados_704ILR.Count)
    $argumentosIscc_704ILR += $DefinicionMotor_704ILR
}
& $Compilador_704ILR @argumentosIscc_704ILR $Guion_704ILR
if ($LASTEXITCODE -ne 0) { throw 'La compilacion del instalador fallo.' }

# --------------------------------------------------------------------------- 3
Paso_704ILR '3. Instalador generado'
$Instalador_704ILR = Get-ChildItem -LiteralPath $Salida_704ILR -Filter 'EvenTech_Instalador_v*.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Instalador_704ILR) { throw "No se genero ningun instalador en $Salida_704ILR" }
Write-Host ('Archivo : ' + $Instalador_704ILR.FullName)
Write-Host ('Tamano  : {0:N1} MB' -f ($Instalador_704ILR.Length / 1MB))
Write-Host ('SHA-256 : ' + (Get-FileHash -LiteralPath $Instalador_704ILR.FullName -Algorithm SHA256).Hash)
