@echo off
chcp 65001 >nul
title Limpieza de Auditoria Global
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0LimpiarAuditoriaGlobal.ps1"
echo.
pause
