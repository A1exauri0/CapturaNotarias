# Script para corregir "Paginas": 0 en auditoria.json sin requerir dependencias externas (usa PowerShell nativo)
$jsonPath = "C:\NOTARIAS\MonitoreoCaptura\PC-09\auditoria.json"
if (-not (Test-Path $jsonPath)) {
    $jsonPath = Join-Path $PSScriptRoot "auditoria.json"
}

if (-not (Test-Path $jsonPath)) {
    Write-Host "[ERROR] No se encontro el archivo auditoria.json en su ubicacion del servidor ni en: $jsonPath" -ForegroundColor Red
    return
}

Write-Host "Cargando auditoria.json..." -ForegroundColor Cyan
$jsonRaw = [System.IO.File]::ReadAllText($jsonPath, [System.Text.Encoding]::UTF8)
$data = ConvertFrom-Json $jsonRaw

if (-not $data -or -not $data.Registros) {
    Write-Host "[ERROR] El formato de auditoria.json no es valido." -ForegroundColor Red
    return
}

function Get-PdfPageCount($path) {
    if (-not (Test-Path $path)) { return $null }
    try {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $text = [System.Text.Encoding]::ASCII.GetString($bytes)
        
        $max = 0
        $matches = [regex]::Matches($text, '/Count\s+(\d+)')
        foreach ($match in $matches) {
            $val = [int]$match.Groups[1].Value
            if ($val -gt $max) { $max = $val }
        }
        
        if ($max -gt 0) { return $max }
        
        # Alternativa si no encuentra /Count: contar bloques /Type /Page
        $matchesPage = [regex]::Matches($text, '/Type\s*/Page\b')
        if ($matchesPage.Count -gt 0) { return $matchesPage.Count }
    } catch {}
    return 1
}

$rutaAlternativa = $args[0]
if ($rutaAlternativa) {
    $rutaAlternativa = $rutaAlternativa.TrimEnd('\')
}

$corregidos = 0
$noEncontrados = 0

Write-Host "Iniciando analisis de archivos..." -ForegroundColor Green

foreach ($registro in $data.Registros) {
    if ($registro.Paginas -eq 0) {
        $rutaLocal = $registro.RutaLocal
        if (-not $rutaLocal) { continue }
        
        $rutaFinal = $null
        
        # 1. Ruta original tal cual (ej. Z:\NOTARIAS\...)
        if (Test-Path $rutaLocal) {
            $rutaFinal = $rutaLocal
        }
        # 2. Ruta alternativa si fue especificada
        elseif ($rutaAlternativa -and $rutaLocal -match '^[a-zA-Z]:(.*)$') {
            $candidato = $rutaAlternativa + $Matches[1]
            if (Test-Path $candidato) {
                $rutaFinal = $candidato
            }
        }
        # 3. Reemplazar unidad con C:\NOTARIAS (ej. C:\NOTARIAS\NOTARIAS\...)
        elseif ($rutaLocal -match '^[a-zA-Z]:(.*)$' -and (Test-Path ("C:\NOTARIAS" + $Matches[1]))) {
            $rutaFinal = "C:\NOTARIAS" + $Matches[1]
        }
        # 4. Reemplazar unidad con ruta de red por defecto
        elseif ($rutaLocal -match '^[a-zA-Z]:(.*)$' -and (Test-Path ("\\172.40.5.84\ssdirec" + $Matches[1]))) {
            $rutaFinal = "\\172.40.5.84\ssdirec" + $Matches[1]
        }
        # 5. Reemplazar unidad con C:\ directamente (ej. C:\NOTARIAS\...)
        elseif ($rutaLocal -match '^[a-zA-Z]:(.*)$' -and (Test-Path ("C:" + $Matches[1]))) {
            $rutaFinal = "C:" + $Matches[1]
        }
        
        if ($rutaFinal) {
            $paginas = Get-PdfPageCount $rutaFinal
            if ($paginas -ne $null -and $paginas -gt 0) {
                $registro.Paginas = $paginas
                Write-Host "Corregido: $($registro.ArchivoOriginal) -> $paginas paginas" -ForegroundColor Gray
                $corregidos++
            } else {
                Write-Host "Error al leer PDF: $($registro.ArchivoOriginal) ($rutaFinal)" -ForegroundColor Yellow
                $noEncontrados++
            }
        } else {
            Write-Host "No encontrado: $($registro.ArchivoOriginal) (Ruta: $rutaLocal)" -ForegroundColor DarkGray
            $noEncontrados++
        }
    }
}

if ($corregidos -gt 0) {
    Write-Host "Guardando cambios en auditoria.json..." -ForegroundColor Cyan
    $nuevoJson = ConvertTo-Json $data -Depth 100
    [System.IO.File]::WriteAllText($jsonPath, $nuevoJson, [System.Text.Encoding]::UTF8)
    
    Write-Host ""
    Write-Host "Proceso completado con exito." -ForegroundColor Green
    Write-Host "Registros corregidos: $corregidos" -ForegroundColor Green
    Write-Host "Archivos no encontrados/omitidos: $noEncontrados" -ForegroundColor Yellow
} else {
    Write-Host ""
    Write-Host "No se realizaron cambios. Todos los archivos estan correctos o no se encontro ninguno fisicamente." -ForegroundColor Yellow
}
