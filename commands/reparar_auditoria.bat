@echo off
title Reparador de Auditorias
echo === REPARADOR DE AUDITORIA JSON ===
echo.
set /p pcnum="Introduce el numero de PC a reparar (ej. 08, 13): "
if "%pcnum%"=="" (
    set "jsonPath=C:\NOTARIAS\MonitoreoCaptura\PC-08\auditoria.json"
) else (
    set "jsonPath=C:\NOTARIAS\MonitoreoCaptura\PC-%pcnum%\auditoria.json"
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "iex ((Get-Content '%~f0') | Select-Object -Skip 15 | Out-String)"
echo.
echo Presiona cualquier tecla para salir...
pause >nul
exit /b

# PowerShell script starts here
try {
    $jsonPath = $env:jsonPath
    if (-not (Test-Path $jsonPath)) {
        Write-Host "No se encontro en la ruta por defecto: $jsonPath" -ForegroundColor Yellow
        $jsonPath = Read-Host "Por favor, introduce la ruta completa del archivo auditoria.json"
        if (-not (Test-Path $jsonPath)) {
            Write-Host "Error: El archivo no existe." -ForegroundColor Red
            return
        }
    }

    Write-Host "Leyendo y procesando el archivo $jsonPath..."

    try {
        [System.Text.Encoding]::RegisterProvider([System.Text.CodePagesEncodingProvider]::Instance)
    } catch {}

    $content = [System.IO.File]::ReadAllText($jsonPath, [System.Text.Encoding]::UTF8)

    # Codigos Unicode ASCII seguros para caracteres especiales
    $charA = [char]193  # Á
    $charN = [char]209  # Ñ
    $charO = [char]211  # Ó
    $charAtild = [char]195  # Ã
    $charCent = [char]162  # ¢
    
    $leonaClean = "LEONARDO JOHANAN CASTA" + $charN + $charO + "N DE LA CRUZ"
    $caroClean = "CAROLINA HERN" + $charA + "NDEZ TORREZ"

    function Limpiar-Mojibake($str) {
        if ([string]::IsNullOrEmpty($str)) { return $str }
        
        # Correccion especifica para Leonardo
        if ($str.Contains('LEONARDO') -and ($str.Contains('CASTA') -or $str.Contains($charCent))) {
            return $leonaClean
        }
        if ($str.Contains('CASTA') -and ($str.Contains($charCent) -or $str.Contains($charAtild))) {
            return $leonaClean
        }
        
        # Correccion especifica para Carolina
        if ($str.Contains('CAROLINA') -and ($str.Contains('HERN') -or $str.Contains($charCent) -or $str.Contains($charAtild))) {
            return $caroClean
        }

        if (-not ($str.Contains($charAtild) -or $str.Contains($charCent))) { return $str }
        try {
            $anterior = $str
            $encoding1252 = [System.Text.Encoding]::GetEncoding(1252)
            $encodingUtf8 = [System.Text.Encoding]::UTF8
            for ($i = 0; $i -lt 10; $i++) {
                $bytes = $encoding1252.GetBytes($anterior)
                $decodificado = $encodingUtf8.GetString($bytes)
                if ($decodificado -eq $anterior) { break }
                $anterior = $decodificado
                if (-not ($decodificado.Contains($charAtild) -or $decodificado.Contains($charCent))) { break }
            }
            return $anterior
        } catch {
            return $str
        }
    }

    $data = ConvertFrom-Json $content
    if ($data -and $data.Registros) {
        foreach ($reg in $data.Registros) {
            $reg.Usuario = Limpiar-Mojibake $reg.Usuario
            $reg.NombreCompleto = Limpiar-Mojibake $reg.NombreCompleto
            $reg.Notaria = Limpiar-Mojibake $reg.Notaria
            $reg.ArchivoOriginal = Limpiar-Mojibake $reg.ArchivoOriginal
            $reg.Detalles = Limpiar-Mojibake $reg.Detalles
            $reg.LugarTrabajo = Limpiar-Mojibake $reg.LugarTrabajo
            $reg.RutaLocal = Limpiar-Mojibake $reg.RutaLocal
        }
    }

    $newContent = ConvertTo-Json $data -Depth 100
    [System.IO.File]::WriteAllText($jsonPath, $newContent, [System.Text.Encoding]::UTF8)
    Write-Host "¡Reparacion completada con exito en $jsonPath!" -ForegroundColor Green
} catch {
    Write-Host "Ocurrio un error critico durante la ejecucion:" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Red
}
