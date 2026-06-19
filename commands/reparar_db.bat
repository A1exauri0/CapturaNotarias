@echo off
title Reparador de Base de Datos SQLite
echo === REPARADOR DE BASE DE DATOS LOCAL ===
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "iex ((Get-Content '%~f0') | Select-Object -Skip 9 | Out-String)"
echo.
echo Presiona cualquier tecla para salir...
pause >nul
exit /b

# PowerShell script starts here
try {
    # Buscar base de datos en AppData o en la carpeta local
    $dbPath = "$env:APPDATA\CapturaNotarias\captura_notarias.db"
    if (-not (Test-Path $dbPath)) {
        $dbPath = Join-Path (Get-Location) "captura_notarias.db"
    }

    if (-not (Test-Path $dbPath)) {
        Write-Host "No se encontro la BD en la ruta por defecto." -ForegroundColor Yellow
        $dbPath = Read-Host "Por favor, introduce la ruta completa del archivo captura_notarias.db"
        if (-not (Test-Path $dbPath)) {
            Write-Host "Error: El archivo no existe." -ForegroundColor Red
            return
        }
    }

    # Intentar buscar la DLL Microsoft.Data.Sqlite.dll en la carpeta actual o subcarpetas
    $dllPath = Join-Path (Get-Location) "Microsoft.Data.Sqlite.dll"
    if (-not (Test-Path $dllPath)) {
        $dllPath = Join-Path (Get-Location) "CapturaNotarias\bin\Debug\net8.0-windows\Microsoft.Data.Sqlite.dll"
    }
    if (-not (Test-Path $dllPath)) {
        $dllPath = Join-Path (Get-Location) "bin\Debug\net8.0-windows\Microsoft.Data.Sqlite.dll"
    }

    if (-not (Test-Path $dllPath)) {
        Write-Host "Buscando Microsoft.Data.Sqlite.dll en el directorio..." -ForegroundColor Yellow
        $found = Get-ChildItem -Filter "Microsoft.Data.Sqlite.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $dllPath = $found.FullName
        } else {
            Write-Host "Error: No se encontro Microsoft.Data.Sqlite.dll." -ForegroundColor Red
            Write-Host "Asegurate de ejecutar este archivo .bat en la carpeta donde esta instalado el programa." -ForegroundColor Yellow
            return
        }
    }

    Write-Host "Cargando DLL de SQLite desde: $dllPath" -ForegroundColor Cyan
    [System.Reflection.Assembly]::LoadFrom($dllPath) | Out-Null

    Write-Host "Abriendo base de datos: $dbPath" -ForegroundColor Cyan
    $conn = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$dbPath")
    $conn.Open()

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
        if ($str.Contains('LEONARDO') -and ($str.Contains('CASTA') -or $str.Contains($charCent))) {
            return $leonaClean
        }
        if ($str.Contains('CASTA') -and ($str.Contains($charCent) -or $str.Contains($charAtild))) {
            return $leonaClean
        }
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

    Write-Host "Leyendo registros..." -ForegroundColor Cyan
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT id, usuario, nombre_completo, notaria, archivo_original, detalles, lugar_trabajo, ruta_local FROM registros_auditoria"
    $reader = $cmd.ExecuteReader()

    $rows = @()
    while ($reader.Read()) {
        $rows += [PSCustomObject]@{
            id = $reader.GetInt64(0)
            usuario = $reader.GetValue(1)
            nombre_completo = $reader.GetValue(2)
            notaria = $reader.GetValue(3)
            archivo_original = $reader.GetValue(4)
            detalles = $reader.GetValue(5)
            lugar_trabajo = $reader.GetValue(6)
            ruta_local = $reader.GetValue(7)
        }
    }
    $reader.Close()

    Write-Host "Reparando $($rows.Count) registros en la BD..." -ForegroundColor Cyan
    $transaction = $conn.BeginTransaction()

    $updateCmd = $conn.CreateCommand()
    $updateCmd.CommandText = "UPDATE registros_auditoria SET usuario = @usuario, nombre_completo = @nombre_completo, notaria = @notaria, archivo_original = @archivo_original, detalles = @detalles, lugar_trabajo = @lugar_trabajo, ruta_local = @ruta_local WHERE id = @id"

    $pUsuario = $updateCmd.CreateParameter(); $pUsuario.ParameterName = "@usuario"; $updateCmd.Parameters.Add($pUsuario) | Out-Null
    $pNombre = $updateCmd.CreateParameter(); $pNombre.ParameterName = "@nombre_completo"; $updateCmd.Parameters.Add($pNombre) | Out-Null
    $pNotaria = $updateCmd.CreateParameter(); $pNotaria.ParameterName = "@notaria"; $updateCmd.Parameters.Add($pNotaria) | Out-Null
    $pArchivo = $updateCmd.CreateParameter(); $pArchivo.ParameterName = "@archivo_original"; $updateCmd.Parameters.Add($pArchivo) | Out-Null
    $pDetalles = $updateCmd.CreateParameter(); $pDetalles.ParameterName = "@detalles"; $updateCmd.Parameters.Add($pDetalles) | Out-Null
    $pLugar = $updateCmd.CreateParameter(); $pLugar.ParameterName = "@lugar_trabajo"; $updateCmd.Parameters.Add($pLugar) | Out-Null
    $pRuta = $updateCmd.CreateParameter(); $pRuta.ParameterName = "@ruta_local"; $updateCmd.Parameters.Add($pRuta) | Out-Null
    $pId = $updateCmd.CreateParameter(); $pId.ParameterName = "@id"; $updateCmd.Parameters.Add($pId) | Out-Null

    $count = 0
    foreach ($row in $rows) {
        $pUsuario.Value = Limpiar-Mojibake $row.usuario
        $pNombre.Value = Limpiar-Mojibake $row.nombre_completo
        $pNotaria.Value = Limpiar-Mojibake $row.notaria
        $pArchivo.Value = Limpiar-Mojibake $row.archivo_original
        $pDetalles.Value = Limpiar-Mojibake $row.detalles
        $pLugar.Value = Limpiar-Mojibake $row.lugar_trabajo
        $pRuta.Value = Limpiar-Mojibake $row.ruta_local
        $pId.Value = $row.id

        $updateCmd.ExecuteNonQuery() | Out-Null
        $count++
    }

    $transaction.Commit()
    $conn.Close()

    Write-Host "¡Base de datos SQLite reparada con exito! Se actualizaron $count registros." -ForegroundColor Green
} catch {
    Write-Host "Ocurrio un error critico durante la ejecucion:" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Red
}
