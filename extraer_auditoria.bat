@echo off
chcp 65001 > nul
powershell -NoProfile -ExecutionPolicy Bypass -Command "$codigo = Get-Content '%~f0' -Encoding UTF8 | Select-Object -Skip 4 | Out-String; Invoke-Expression $codigo"
exit /b

# Codigo de PowerShell en adelante
$rutaConfig = "$env:APPDATA\CapturaNotarias\config.json"
if (-not (Test-Path $rutaConfig)) {
    Write-Host "[ERROR] No se encontro el archivo de configuracion local en $rutaConfig." -ForegroundColor Red
    Write-Host "Presione cualquier tecla para salir..."
    $null = [Console]::ReadKey()
    exit
}

# Cargar configuracion
try {
    $configContent = Get-Content $rutaConfig -Raw
    $config = ConvertFrom-Json $configContent
} catch {
    Write-Host "[ERROR] No se pudo leer el archivo de configuracion: $_" -ForegroundColor Red
    Write-Host "Presione cualquier tecla para salir..."
    $null = [Console]::ReadKey()
    exit
}

$nombrePC = $config.NombrePC
$lugarTrabajo = $config.LugarTrabajo
$tipoCaptura = if ($config.TipoCaptura) { $config.TipoCaptura } else { "NOTARIAS" }
$rutaServidorAuditoria = $config.RutaServidorAuditoria
$urlApi = if ($config.UrlApi) { $config.UrlApi } else { "https://app.astronmx.cloud/api/digitalizacion/registrar" }

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "   EXTRACCION DE AUDITORIA Y ARCHIVOS A DISCO" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "PC: $nombrePC"
Write-Host "Lugar de Trabajo: $lugarTrabajo"
Write-Host "Tipo de Captura: $tipoCaptura"
Write-Host "API Destino: $urlApi"
Write-Host "==================================================" -ForegroundColor Cyan

# Solicitar disco de destino
Write-Host "Abriendo selector de carpetas..." -ForegroundColor Cyan
Add-Type -AssemblyName System.Windows.Forms
$dialogo = New-Object System.Windows.Forms.FolderBrowserDialog
$dialogo.Description = "Seleccione la carpeta o unidad del disco externo para la extraccion"
$dialogo.ShowNewFolderButton = $true

# Crear un formulario invisible para forzar que el dialogo aparezca al frente
$formularioBase = New-Object System.Windows.Forms.Form
$formularioBase.TopMost = $true

$resultado = $dialogo.ShowDialog($formularioBase)
$formularioBase.Dispose()

if ($resultado -ne [System.Windows.Forms.DialogResult]::OK -or [string]::IsNullOrEmpty($dialogo.SelectedPath)) {
    Write-Host "[ERROR] No se selecciono ninguna carpeta de destino." -ForegroundColor Red
    Write-Host "Presione cualquier tecla para salir..."
    $null = [Console]::ReadKey()
    exit
}

$rutaDestino = $dialogo.SelectedPath
Write-Host "Ruta seleccionada: $rutaDestino" -ForegroundColor Green








if (-not (Test-Path $rutaDestino)) {
    try {
        New-Item -ItemType Directory -Force -Path $rutaDestino | Out-Null
        Write-Host "Carpeta de destino creada: $rutaDestino" -ForegroundColor Gray
    } catch {
        Write-Host "[ERROR] No se pudo crear o acceder a la carpeta de destino: $_" -ForegroundColor Red
        Write-Host "Presione cualquier tecla para salir..."
        $null = [Console]::ReadKey()
        exit
    }
}

# Localizar los archivos JSON de auditoria
$rutaLocalJson = "$env:APPDATA\CapturaNotarias\auditoria_local.json"
$rutaServerJson = ""

if (-not [string]::IsNullOrEmpty($rutaServidorAuditoria) -and (Test-Path $rutaServidorAuditoria)) {
    $rutaServerJson = Join-Path $rutaServidorAuditoria "MonitoreoCaptura\$nombrePC\auditoria.json"
}

# Funcion para obtener registros
function ObtenerRegistros($rutaJson) {
    if (Test-Path $rutaJson) {
        try {
            $contenido = Get-Content $rutaJson -Raw -ErrorAction Stop
            if (-not [string]::IsNullOrWhiteSpace($contenido)) {
                $objeto = ConvertFrom-Json $contenido
                if ($objeto -and $objeto.Registros) {
                    return $objeto.Registros
                }
            }
        } catch {
            Write-Host "[ADVERTENCIA] Error al leer $($rutaJson): $_" -ForegroundColor Yellow
        }
    }
    return @()
}

$todosLosLogs = [System.Collections.Generic.List[PSObject]]::new()

# Cargar del local
$logsLocales = ObtenerRegistros $rutaLocalJson
foreach ($l in $logsLocales) {
    if ($l.FechaHora -and $l.ArchivoOriginal) {
        $l | Add-Member -MemberType NoteProperty -Name "OrigenFile" -Value $rutaLocalJson -Force
        $todosLosLogs.Add($l)
    }
}

# Cargar del servidor de red (si esta disponible)
if (-not [string]::IsNullOrEmpty($rutaServerJson) -and (Test-Path $rutaServerJson)) {
    $logsServidor = ObtenerRegistros $rutaServerJson
    foreach ($l in $logsServidor) {
        if ($l.FechaHora -and $l.ArchivoOriginal) {
            $l | Add-Member -MemberType NoteProperty -Name "OrigenFile" -Value $rutaServerJson -Force
            
            # Evitar duplicados basados en FechaHora, ArchivoOriginal y PC
            $existe = $todosLosLogs | Where-Object { 
                $_.FechaHora -eq $l.FechaHora -and 
                $_.ArchivoOriginal -eq $l.ArchivoOriginal -and 
                $_.PC -eq $l.PC 
            }
            if (-not $existe) {
                $todosLosLogs.Add($l)
            }
        }
    }
}

# Filtrar solo pendientes de enviar
$logsPendientes = $todosLosLogs | Where-Object { $_.Enviado -ne $true -and $_.Enviado -ne "true" }

if ($logsPendientes.Count -eq 0) {
    Write-Host "No se encontraron registros de auditoria pendientes para esta PC." -ForegroundColor Green
    Write-Host "Presione cualquier tecla para salir..."
    $null = [Console]::ReadKey()
    exit
}

Write-Host "Se encontraron $($logsPendientes.Count) registros pendientes." -ForegroundColor Green

# Agrupar registros por volumen (Notaria)
$logsAgrupados = $logsPendientes | Group-Object -Property Notaria

foreach ($grupo in $logsAgrupados) {
    $volumen = $grupo.Name
    if ([string]::IsNullOrEmpty($volumen)) { $volumen = "GENERAL" }

    Write-Host "`n--------------------------------------------------" -ForegroundColor Cyan
    Write-Host "Procesando Volumen: $volumen" -ForegroundColor Yellow
    Write-Host "--------------------------------------------------" -ForegroundColor Cyan

    $logsExitososEnVolumen = [System.Collections.Generic.List[PSObject]]::new()

    foreach ($log in $grupo.Group) {
        $rutaArchivoOriginal = $log.RutaLocal
        $archivoExiste = $false
        $rutaFisicaReal = ""

        # Posibles rutas candidatas
        $candidatos = @(
            $rutaArchivoOriginal,
            (Join-Path "C:\$tipoCaptura" (Join-Path $log.Notaria $log.ArchivoOriginal)),
            (Join-Path "C:\NOTARIAS" (Join-Path $log.Notaria $log.ArchivoOriginal))
        )

        foreach ($c in $candidatos) {
            if (-not [string]::IsNullOrEmpty($c) -and (Test-Path $c)) {
                $rutaFisicaReal = $c
                $archivoExiste = $true
                break
            }
        }

        if (-not $archivoExiste) {
            Write-Host "  [Omitido] No se encontro archivo fisico local para: $($log.ArchivoOriginal)" -ForegroundColor DarkGray
            continue
        }

        # Estructura destino: $rutaDestino\ssdirec\$tipoCaptura\$volumen\$archivo
        $subcarpetaDestino = Join-Path "ssdirec\$tipoCaptura" $log.Notaria
        $directorioCompletoDestino = Join-Path $rutaDestino $subcarpetaDestino
        $archivoDestinoFinal = Join-Path $directorioCompletoDestino $log.ArchivoOriginal

        # Crear carpeta de destino del volumen si no existe
        if (-not (Test-Path $directorioCompletoDestino)) {
            New-Item -ItemType Directory -Force -Path $directorioCompletoDestino | Out-Null
        }

        # Copiar el archivo PDF al disco
        try {
            Copy-Item -Path $rutaFisicaReal -Destination $archivoDestinoFinal -Force -ErrorAction Stop
            
            # Registrar detalle en el log
            $textoExtraccion = "[Extraido fisicamente a disco externo]"
            if (-not [string]::IsNullOrEmpty($log.Detalles)) {
                $log.Detalles = "$textoExtraccion - $($log.Detalles)"
            } else {
                $log.Detalles = $textoExtraccion
            }

            $log.Enviado = $true
            
            # Agregar referencia del archivo fisico a borrar
            $log | Add-Member -MemberType NoteProperty -Name "RutaFisicaABorrar" -Value $rutaFisicaReal -Force
            $logsExitososEnVolumen.Add($log)

            Write-Host "  [Copiado exitoso] $($log.ArchivoOriginal)" -ForegroundColor Green
        } catch {
            Write-Host "  [ERROR] No se pudo copiar $($log.ArchivoOriginal): $_" -ForegroundColor Red
        }
    }

    if ($logsExitososEnVolumen.Count -eq 0) {
        Write-Host "  No se copiaron archivos en este volumen." -ForegroundColor Gray
        continue
    }

    # Mandar el volumen a la API
    Write-Host "  Enviando $($logsExitososEnVolumen.Count) registros del volumen '$volumen' al servidor..." -ForegroundColor Cyan

    $registrosLimpios = @()
    foreach ($log in $logsExitososEnVolumen) {
        $cleanLog = [PSCustomObject]@{
            FechaHora       = $log.FechaHora
            Usuario         = $log.Usuario
            NombreCompleto  = $log.NombreCompleto
            Turno           = $log.Turno
            PC              = $log.PC
            IP              = $log.IP
            Notaria         = $log.Notaria
            Accion          = $log.Accion
            ArchivoOriginal = $log.ArchivoOriginal
            Detalles        = $log.Detalles
            Paginas         = [int]$log.Paginas
            LugarTrabajo    = $log.LugarTrabajo
            Enviado         = $true
            RutaLocal       = $log.RutaLocal
        }
        $registrosLimpios += $cleanLog
    }

    $payload = @{ Registros = $registrosLimpios }
    $payloadJson = ConvertTo-Json $payload -Depth 5 -Compress

    $apiOk = $false
    try {
        $headers = @{ "Content-Type" = "application/json" }
        $respuesta = Invoke-RestMethod -Uri $urlApi -Method Post -Body $payloadJson -Headers $headers -TimeoutSec 15
        
        if ($respuesta -and ($respuesta.ok -eq $true -or $respuesta.ok -eq "true" -or $respuesta.mensaje -like "*exitosa*")) {
            $apiOk = $true
            Write-Host "  [ÉXITO] API registrada correctamente para el volumen '$volumen'." -ForegroundColor Green
        } else {
            Write-Host "  [ADVERTENCIA] La API no retorno un estado exitoso para el volumen '$volumen'." -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  [ERROR] Error al conectar con la API: $_" -ForegroundColor Red
    }

    # Si la API reporto exito, actualizamos localmente y eliminamos los archivos originales
    if ($apiOk) {
        Write-Host "  Marcando registros como enviados y eliminando archivos locales..." -ForegroundColor Cyan

        # Actualizar archivos JSON de auditoria
        $gruposOrigen = $logsExitososEnVolumen | Group-Object -Property OrigenFile
        foreach ($go in $gruposOrigen) {
            $rutaJsonDest = $go.Name
            if (Test-Path $rutaJsonDest) {
                try {
                    $contenidoJson = Get-Content $rutaJsonDest -Raw
                    $datosAuditoria = ConvertFrom-Json $contenidoJson
                    if ($datosAuditoria -and $datosAuditoria.Registros) {
                        foreach ($logEx in $go.Group) {
                            $itemMatch = $datosAuditoria.Registros | Where-Object {
                                $_.FechaHora -eq $logEx.FechaHora -and
                                $_.ArchivoOriginal -eq $logEx.ArchivoOriginal -and
                                $_.PC -eq $logEx.PC
                            }
                            if ($itemMatch) {
                                $itemMatch.Enviado = $true
                                $itemMatch.Detalles = $logEx.Detalles
                            }
                        }
                        # Guardar archivo actualizado
                        $nuevoContenido = ConvertTo-Json $datosAuditoria -Depth 5
                        [System.IO.File]::WriteAllText($rutaJsonDest, $nuevoContenido)
                    }
                } catch {
                    Write-Host "  [ERROR] No se pudo guardar la actualizacion en $($rutaJsonDest): $_" -ForegroundColor Red
                }
            }
        }

        # Eliminar archivos fisicos locales
        foreach ($log in $logsExitososEnVolumen) {
            if (Test-Path $log.RutaFisicaABorrar) {
                try {
                    Remove-Item -Path $log.RutaFisicaABorrar -Force -ErrorAction Stop
                    Write-Host "  [Eliminado local] $($log.ArchivoOriginal)" -ForegroundColor Gray
                } catch {
                    Write-Host "  [ADVERTENCIA] No se pudo borrar el archivo local $($log.RutaFisicaABorrar): $_" -ForegroundColor Yellow
                }
            }
        }
        Write-Host "  [Listo] Volumen '$volumen' completado." -ForegroundColor Green
    } else {
        Write-Host "  [AVISO] Los archivos fisicos de '$volumen' se copiaron al disco, pero NO se borraron de la PC ni se marcaron como enviados porque fallo la sincronizacion con la API web. Se reintentara la proxima vez." -ForegroundColor Yellow
    }
}

Write-Host "`n==================================================" -ForegroundColor Cyan
Write-Host "Proceso terminado. Extraiga su disco externo de forma segura." -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Presione cualquier tecla para salir..."
$null = [Console]::ReadKey()
