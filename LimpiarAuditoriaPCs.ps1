# Script para limpiar registros erroneos de auditoria por PC
# Elimina registros del 2026-05-20 y anteriores

$rutaBase = "C:\NOTARIAS\MonitoreoCaptura"
$fechaLimite = "2026-05-21"
$totalEliminados = 0
$archivosModificados = 0
$pcsProcesadas = 0

Write-Host "============================================" -ForegroundColor Cyan
Write-Host " LIMPIEZA DE AUDITORIAS POR PC" -ForegroundColor Cyan
Write-Host " Eliminando registros del 20-mayo-2026 y anteriores" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

for ($i = 1; $i -le 20; $i++) {
    $nombrePC = "PC-" + $i.ToString("D2")
    $archivo = Join-Path $rutaBase "$nombrePC\auditoria.json"

    if (-not (Test-Path $archivo)) {
        continue
    }

    $pcsProcesadas++

    try {
        $json = Get-Content $archivo -Raw -Encoding UTF8 | ConvertFrom-Json
        $totalAntes = $json.Registros.Count
        $erroneos = @($json.Registros | Where-Object { $_.FechaHora -lt $fechaLimite })

        if ($erroneos.Count -eq 0) {
            Write-Host "[$nombrePC] Sin registros erroneos ($totalAntes registros)" -ForegroundColor Green
            continue
        }

        # Crear respaldo
        $respaldo = $archivo -replace '\.json$', ("_RESPALDO_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".json")
        Copy-Item $archivo $respaldo

        # Filtrar solo los registros validos
        $limpios = @($json.Registros | Where-Object { $_.FechaHora -ge $fechaLimite })
        $json.Registros = $limpios

        # Guardar archivo limpio
        $jsonTexto = $json | ConvertTo-Json -Depth 10
        [System.IO.File]::WriteAllText($archivo, $jsonTexto, [System.Text.Encoding]::UTF8)

        $totalEliminados += $erroneos.Count
        $archivosModificados++
        Write-Host "[$nombrePC] Eliminados: $($erroneos.Count) | Antes: $totalAntes | Despues: $($limpios.Count) | Respaldo: OK" -ForegroundColor Yellow
    }
    catch {
        Write-Host "[$nombrePC] ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== RESUMEN ===" -ForegroundColor Cyan
Write-Host "PCs encontradas:           $pcsProcesadas" -ForegroundColor White
Write-Host "Archivos modificados:      $archivosModificados" -ForegroundColor Yellow
Write-Host "Total registros eliminados: $totalEliminados" -ForegroundColor Red
