@echo off
echo Iniciando reinicio de estado de envio de auditorias...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path 'C:\NOTARIAS\MonitoreoCaptura' -Filter 'auditoria.json' -Recurse | ForEach-Object { $c = Get-Content $_.FullName -Raw | ConvertFrom-Json; if ($c -and $c.Registros) { $m = $false; foreach ($r in $c.Registros) { if ($r.Enviado -eq $true) { $r.Enviado = $false; $m = $true } }; if ($m) { ConvertTo-Json $c -Depth 100 | Set-Content $_.FullName -Encoding UTF8; Write-Host 'Actualizado:' $_.FullName } } }"
echo Proceso terminado.
pause
