@echo off
chcp 65001 >nul
title 021cars.az - Tunel

set "CF=%ProgramFiles%\cloudflared\cloudflared.exe"
if not exist "%CF%" set "CF=%ProgramFiles(x86)%\cloudflared\cloudflared.exe"
if not exist "%CF%" set "CF=%LOCALAPPDATA%\cloudflared\cloudflared.exe"

echo.
echo ======================================================================
echo    021cars.az  -  CLOUDFLARE TUNEL
echo ======================================================================
echo.

if not exist "%CF%" goto YOXDUR

if not exist "%USERPROFILE%\.cloudflared\config.yml" goto TUNELYOX

echo    Tunel basladilir:  021cars.az  -^>  http://localhost:5000
echo    Bu pencere ACIQ QALMALIDIR.
echo    Dayandirmaq ucun: Ctrl + C
echo.
echo ======================================================================
echo.

"%CF%" tunnel run avtopark

echo.
echo [!] Tunel dayandi.
echo.
pause
goto :EOF


:YOXDUR
echo    [XETA] cloudflared QURASDIRILMAYIB!
echo.
echo ======================================================================
echo    COZUM - BIR DEFE:
echo.
echo       domen-qurasdir.bat faylina SAG KLIK edin
echo       -^>  "Run as administrator"
echo.
echo    O skript cloudflared-i endirib quraşdiracaq, tunel yaradacaq
echo    ve 021cars.az DNS qeydlerini baglayacaq.
echo ======================================================================
echo.
pause
goto :EOF


:TUNELYOX
echo    [XETA] Tunel konfiqurasiyasi tapilmadi!
echo.
echo    Gozlenilen fayl:
echo       %USERPROFILE%\.cloudflared\config.yml
echo.
echo ======================================================================
echo    COZUM - BIR DEFE:
echo.
echo       domen-qurasdir.bat faylina SAG KLIK edin
echo       -^>  "Run as administrator"
echo ======================================================================
echo.
echo    QEYD: Bu addimin islemesi ucun 021cars.az domeninin
echo          CLOUDFLARE hesabinizda olmasi MUTLEQDIR.
echo          (dash.cloudflare.com -^> Add site)
echo.
pause

