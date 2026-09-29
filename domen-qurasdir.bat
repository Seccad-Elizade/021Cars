@echo off
chcp 65001 >nul
title 021cars.az - Domen Qurulumu (BIR DEFE)
cd /d "%~dp0"

echo.
echo ======================================================================
echo    021cars.az  -  DOMEN QURULUMU  (BIR DEFE)
echo ======================================================================
echo.
echo    ADMIN HUQUQU TELEB OLUNMUR - adi istifadeci kimi isledin.
echo.
echo    ON SERD: 021cars.az domeni Cloudflare hesabinizda olmalidir.
echo.
echo ======================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0domen-qurasdir.ps1"

