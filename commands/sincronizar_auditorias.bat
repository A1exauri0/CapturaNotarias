@echo off
chcp 65001 > nul

set RUTA_MONITOREO=C:\NOTARIAS\MonitoreoCaptura
set API_URL=https://app.astronmx.cloud/api/digitalizacion/registrar

echo =======================================================
echo Enviando registros de auditoria a Astronmx...
echo Carpeta origen: %RUTA_MONITOREO%
echo URL Destino: %API_URL%
echo =======================================================
echo.

if not exist "%RUTA_MONITOREO%" goto NO_EXISTE

for /f "delims=" %%G in ('dir /b /ad "%RUTA_MONITOREO%"') do (
    call :ENVIAR "%%G"
)

echo.
echo Sincronizacion completada.
pause
exit /b 0

:ENVIAR
set "FOLDER_NAME=%~1"
set "JSON_FILE=%RUTA_MONITOREO%\%FOLDER_NAME%\auditoria.json"
if exist "%JSON_FILE%" (
    echo Procesando: %FOLDER_NAME%
    curl -s -X POST -H "Content-Type: application/json" -H "Accept: application/json" -d @"%JSON_FILE%" "%API_URL%"
    echo.
    echo -------------------------------------------------------
)
goto :eof

:NO_EXISTE
echo [ERROR] No existe la carpeta: %RUTA_MONITOREO%
echo.
pause
exit /b 1
