@echo off
chcp 65001 >nul
title 021cars.az  -  DNS Diaqnostikasi
cd /d "%~dp0"

echo.
echo ======================================================================
echo    021cars.az  -  DNS DIAQNOSTIKASI
echo ======================================================================
echo.
echo    Bu skript yoxlayir:
echo      1) Veb server isleyirmi?
echo      2) DNS serveri (port 53) isleyirmi?
echo      3) 021cars.az dogru cavab verirmi?
echo      4) Windows hansi DNS-i istifade edir?
echo.
echo ======================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0dns-yoxla.ps1"

echo.
pause
