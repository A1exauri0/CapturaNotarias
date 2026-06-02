# Script para limpiar registros erroneos de auditoria global
# Elimina registros del 2026-05-20 y anteriores

$archivo = "\\172.40.5.84\ssdirec\NOTARIAS\auditoria.json"
$fechaLimite = "2026-05-21"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " LIMPIEZA DE AUDITORIA GLOBAL" -ForegroundColor Cyan
Write-Host " Archivo: $archivo" -ForegroundColor Cyan
Write-Host " Eliminando registros del 20-mayo-2026 y anteriores" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $archivo)) {
    Write-Host "[ERROR] No se encontro el archivo: $archivo" -ForegroundColor Red
    exit 1
}

try {
    Write-Host "Leyendo archivo..." -ForegroundColor Cyan
    $json = Get-Content $archivo -Raw -Encoding UTF8 | ConvertFrom-Json
    $totalAntes = $json.Registros.Count
    Write-Host "Total de registros actuales: $totalAntes" -ForegroundColor Yellow

    $erroneos = @($json.Registros | Where-Object { $_.FechaHora -lt $fechaLimite })
    Write-Host "Registros erroneos (20-mayo y anteriores): $($erroneos.Count)" -ForegroundColor Red

    if ($erroneos.Count -eq 0) {
        Write-Host "No hay registros que eliminar." -ForegroundColor Green
        exit 0
    }

    # Crear respaldo
    $respaldo = $archivo -replace '\.json$', ("_RESPALDO_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".json")
    Write-Host "Creando respaldo..." -ForegroundColor Cyan
    Copy-Item $archivo $respaldo
    Write-Host "Respaldo creado en: $respaldo" -ForegroundColor Green

    # Filtrar solo los registros validos
    $limpios = @($json.Registros | Where-Object { $_.FechaHora -ge $fechaLimite })
    $json.Registros = $limpios

    # Guardar archivo limpio
    Write-Host "Guardando archivo limpio..." -ForegroundColor Cyan
    $jsonTexto = $json | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($archivo, $jsonTexto, [System.Text.Encoding]::UTF8)

    Write-Host ""
    Write-Host "=== RESUMEN ===" -ForegroundColor Cyan
    Write-Host "Registros antes:      $totalAntes" -ForegroundColor Yellow
    Write-Host "Registros eliminados: $($erroneos.Count)" -ForegroundColor Red
    Write-Host "Registros despues:    $($limpios.Count)" -ForegroundColor Green
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}
