@echo off
chcp 65001 >nul
title Limpieza de Auditorias por PC
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0LimpiarAuditoriaPCs.ps1"
echo.
pause
