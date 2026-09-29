@echo off
chcp 65001 >nul
title AvtoPark - Veb Server
cd /d "%~dp0"

echo.
echo ======================================================================
echo    AVTOMOBIL PARKI v6.0  -  VEB SERVER
echo ======================================================================
echo.
echo    Server basladilir... Bu pencere ACIQ QALMALIDIR.
echo    Dayandirmaq ucun: Ctrl + C
echo.
echo ======================================================================
echo.

REM --- Kohne server prosesini dayandir (port konflikti olmasin) ---
taskkill /f /im EnterpriseAeroStudio.Web.exe >nul 2>&1
timeout /t 2 /nobreak >nul

REM ==========================================================================
REM   KRITIK: evvelce BUILD edilir.
REM   Sebeb: server isleyerken DLL kilidlenir. Eger build xeta verse ve biz
REM   yene de kohne DLL-i ise salsaq, istifadeci SEHVNETICE olaraq kohne kodu
REM   gorur ("duymeler islemir" kimi problemler yaranir).
REM ==========================================================================
echo    Kod yigilir (build)...
echo.
dotnet build "%~dp0Web\Autocode.Web.csproj" -v q --nologo
if errorlevel 1 goto :buildfail

echo.
echo    Build ugurlu. Server basladilir...
echo.
dotnet run --project "%~dp0Web\Autocode.Web.csproj" --no-build
goto :ended

:buildfail
echo.
echo ======================================================================
echo    [XETA]  BUILD ALINMADI!
echo ======================================================================
echo.
echo    Server ISE DUSMEYECEK - cunki kodda xeta var.
echo.
echo    EN COX GUMAN SEBEB:
echo       Kohne server penceresi hele de ACIQDIR ve DLL faylini tutur.
echo.
echo    HELLI:
echo       1) Butun "AvtoPark - Server" pencerelerini baglayin (Ctrl+C)
echo       2) Bu fayli yeniden isledin
echo.
echo    Yuxaridaki xeta setirlerini oxuyun.
echo ======================================================================
echo.
pause
exit /b 1

:ended
echo.
echo Server dayandi.
pause

