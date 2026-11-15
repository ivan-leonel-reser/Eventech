@echo off
rem Prueba de los digitos verificadores de EvenTech: altera la base por fuera del
rem sistema (antes toma un respaldo) para ver la alerta de integridad al abrirlo.
rem Ver PruebaIntegridad.ps1 y README.md de esta carpeta.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PruebaIntegridad.ps1" %*
echo.
pause
