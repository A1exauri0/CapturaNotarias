@echo off
echo ==================================================
echo   Compilando CapturaNotarias para Distribucion
echo ==================================================
echo.

echo Limpiando versiones anteriores...
rmdir /s /q "Compartir" 2>nul
mkdir "Compartir"

echo Construyendo ejecutable unico...
dotnet publish "CapturaNotarias\CapturaNotarias\CapturaNotarias.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "Compartir"

echo.
echo ==================================================
echo   PROCESO TERMINADO
echo   Tu archivo CapturaNotarias.exe esta listo en:
echo   %CD%\Compartir
echo ==================================================
pause
