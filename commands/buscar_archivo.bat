@echo off
chcp 65001 > nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "$codigo = Get-Content '%~f0' -Encoding UTF8 | Select-Object -Skip 4 | Out-String; Invoke-Expression $codigo"
exit /b

# Código de PowerShell en adelante
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "       BUSCADOR DE REGISTROS EN AUDITORIA" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

$nomArchivo = Read-Host "Ingrese el nombre del archivo o PDF a buscar"
if ([string]::IsNullOrWhiteSpace($nomArchivo)) {
    Write-Host "[ERROR] No se ingresó ningún nombre de archivo." -ForegroundColor Red
    Write-Host "Presione cualquier tecla para salir..."
    $null = [Console]::ReadKey()
    exit
}

Write-Host "`nIniciando búsqueda de: $nomArchivo" -ForegroundColor Cyan
$encontrado = $false

# Buscar en red
if (Test-Path "C:\NOTARIAS") {
    $archivosJson = Get-ChildItem -Path "C:\NOTARIAS" -Filter "auditoria*.json" -Recurse -File -ErrorAction SilentlyContinue
    foreach ($archivo in $archivosJson) {
        try {
            $contenido = Get-Content $archivo.FullName -Raw | ConvertFrom-Json
            if ($contenido -and $contenido.Registros) {
                foreach ($r in $contenido.Registros) {
                    if ($r.ArchivoOriginal -like "*$nomArchivo*") {
                        Write-Host "`n[ENCONTRADO EN RED]" -ForegroundColor Green
                        Write-Host "Archivo de origen: $($archivo.FullName)" -ForegroundColor Gray
                        $r | Format-List | Out-String | Write-Host
                        $encontrado = $true
                    }
                }
            }
        } catch {}
    }
}

# Buscar en local
$localJson = Join-Path $env:APPDATA "CapturaNotarias\auditoria_local.json"
if (Test-Path $localJson) {
    try {
        $contenido = Get-Content $localJson -Raw | ConvertFrom-Json
        if ($contenido -and $contenido.Registros) {
            foreach ($r in $contenido.Registros) {
                if ($r.ArchivoOriginal -like "*$nomArchivo*") {
                    Write-Host "`n[ENCONTRADO EN LOCAL]" -ForegroundColor Green
                    Write-Host "Archivo de origen: $localJson" -ForegroundColor Gray
                    $r | Format-List | Out-String | Write-Host
                    $encontrado = $true
                }
            }
        }
    } catch {}
}

Write-Host ""
if (-not $encontrado) {
    Write-Host "No se encontró ningún registro para ese archivo en ninguna auditoría." -ForegroundColor Red
} else {
    Write-Host "Búsqueda completada." -ForegroundColor Cyan
}

Write-Host "`nPresione cualquier tecla para salir..."
$null = [Console]::ReadKey()
