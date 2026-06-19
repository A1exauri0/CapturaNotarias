@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul

echo =======================================================
echo     CORRECTOR DE PÁGINAS PARA AUDITORÍA DE NOTARÍAS
echo =======================================================
echo.
echo Iniciando proceso usando la ruta por defecto (C:\NOTARIAS)...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0reparar_paginas.ps1" "C:\NOTARIAS"

echo.
echo Proceso terminado.
pause
