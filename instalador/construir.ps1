<#
construir.ps1 - genera el instalador de EvenTech.

    1. Publica la aplicacion para Windows de 64 bits con el entorno de ejecucion de
       .NET 8 incluido (carpeta "publicado"): quien instala no necesita tener .NET.
    2. Compila EvenTech.iss con Inno Setup 6. El instalador lleva adentro la
       aplicacion publicada y el respaldo db\EvenTechDB.bak.
    3. Deja el instalador en la carpeta "salida" e informa su tamano y su SHA-256.

Uso (PowerShell, desde cualquier carpeta):
    powershell -ExecutionPolicy Bypass -File construir.ps1

Requisitos: SDK de .NET (8 o superior) e Inno Setup 6 (winget install JRSoftware.InnoSetup).

El instalador se genera DESPUES de regenerar db\EvenTechDB.bak: el respaldo viaja
adentro, y sus cotizaciones y reservas pendientes vencen por fecha (regla RN-01).
#>
param(
    # Compila el instalador con la carpeta "publicado" tal como esta, sin volver a publicar.
    [switch]$SinPublicar_704ILR
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

# --------------------------------------------------------------------------- 2
Paso_704ILR '2. Compilar el instalador'
Write-Host ('Respaldo incluido: {0} ({1:dd/MM/yyyy HH:mm})' -f $Respaldo_704ILR, (Get-Item -LiteralPath $Respaldo_704ILR).LastWriteTime)
& $Compilador_704ILR '/Q' $Guion_704ILR
if ($LASTEXITCODE -ne 0) { throw 'La compilacion del instalador fallo.' }

# --------------------------------------------------------------------------- 3
Paso_704ILR '3. Instalador generado'
$Instalador_704ILR = Get-ChildItem -LiteralPath $Salida_704ILR -Filter 'EvenTech_Instalador_v*.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $Instalador_704ILR) { throw "No se genero ningun instalador en $Salida_704ILR" }
Write-Host ('Archivo : ' + $Instalador_704ILR.FullName)
Write-Host ('Tamano  : {0:N1} MB' -f ($Instalador_704ILR.Length / 1MB))
Write-Host ('SHA-256 : ' + (Get-FileHash -LiteralPath $Instalador_704ILR.FullName -Algorithm SHA256).Hash)
