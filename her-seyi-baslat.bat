@echo off
chcp 65001 >nul
title AvtoPark - HER SEYI BASLAT
cd /d "%~dp0"

REM --- cloudflared yerini tap ---
set "CF=%ProgramFiles%\cloudflared\cloudflared.exe"
if not exist "%CF%" set "CF=%ProgramFiles(x86)%\cloudflared\cloudflared.exe"
if not exist "%CF%" set "CF=%LOCALAPPDATA%\cloudflared\cloudflared.exe"

echo.
echo ======================================================================
echo    AVTOMOBIL PARKI v6.0  -  HER SEYI BASLAT
echo ======================================================================
echo.
echo    1) Veb server       (oz penceresinde)
echo    2) 021cars.az tuneli (oz penceresinde)
echo.
echo    Her iki pencere ACIQ QALMALIDIR.
echo ======================================================================
echo.

echo [1/2] Kohne server prosesi dayandirilir...
taskkill /f /im EnterpriseAeroStudio.Web.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo [2/2] Veb server basladilir...
start "AvtoPark - Veb Server" cmd /k call "%~dp0ishe-sal.bat"

echo       Server hazir olmasini gozleyirik (14 saniye)...
timeout /t 14 /nobreak >nul

if not exist "%CF%" goto TUNELYOX
if not exist "%USERPROFILE%\.cloudflared\config.yml" goto TUNELYOX

echo       Tunel basladilir...
start "021cars.az - Tunel" cmd /k call "%~dp0tunel-baslat.bat"
goto HAZIR


:TUNELYOX
echo.
echo ======================================================================
echo    [!] TUNEL BASLADILA BILMEDI
echo ======================================================================
echo.
echo    Sebeb: cloudflared quraşdirilmayib ve ya tunel yaradilmayib.
echo.
echo    COZUM (BIR DEFE, ADMIN kimi):
echo.
echo       1) domen-qurasdir.bat  -^>  Run as administrator
echo.
echo    VE MUTLEQ ON SERD:
echo       021cars.az domeni CLOUDFLARE hesabinizda olmalidir.
echo       (dash.cloudflare.com  -^>  Add site)
echo.
echo    Domeniniz yoxdursa - "test-tunel.bat" ile DERHAL pulsuz
echo    muvəqqəti unvan ala bilersiniz.
echo.

:HAZIR
echo.
echo ======================================================================
echo    VEZIYYET
echo ======================================================================
echo.
echo    Yerli            :  http://localhost:5000
echo    Sebekede (WiFi)  :  http://BU-KOMPUTERIN-IP:5000
if exist "%USERPROFILE%\.cloudflared\config.yml" echo    Internet (domen):  https://021cars.az
echo.
echo ======================================================================
echo.
pause

