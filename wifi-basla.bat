@echo off
chcp 65001 >nul
title AvtoPark - WiFi rejimi (yalniz sebeke)

:: ==========================================================================
::  Bu skript 2 sey edir:
::    1) Windows Firewall-da 5000 portunu sebeke ucun ACIR
::    2) Veb serveri basladir
::
::  Netice: eyni WiFi-ye qosulan butun cihazlar (telefon, planset, komputer)
::          http://<BU-KOMPUTERIN-IP>:5000  unvani ile daxil ola biler.
::
::  DOMEN LAZIM DEYIL. INTERNET LAZIM DEYIL. PULSUZDUR.
:: ==========================================================================

cd /d "%~dp0"

set "PORT=5000"
set "LOG=%TEMP%\avtopark-wifi.log"
del "%LOG%" >nul 2>&1

:: ---- Portu appsettings.json-dan oxu (PowerShell ile - etibarlidir) ----
set "PORT=5000"
for /f "delims=" %%a in ('powershell -NoProfile -Command "try{(Get-Content 'appsettings.json' -Raw|ConvertFrom-Json).AvtoPark.Port}catch{5000}" 2^>nul') do set "PORT=%%a"
if "%PORT%"=="" set "PORT=5000"

echo.
echo ======================================================================
echo    AVTOMOBIL PARKI v6.0  -  WiFi REJIMI
echo ======================================================================
echo.

:: ======================= FIREWALL QAYDASI (admin lazimdir) ===============
netsh advfirewall firewall show rule name="AvtoPark Web %PORT%" >nul 2>&1
if %errorlevel% equ 0 goto :fwiready

echo [1/2] Firewall qaydasi elave edilir (admin huququ lazimdir)...

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo       ADMIN huququ teleb olunur - Windows size sorgu gonderecek.
    echo       "Beli" / "Yes" duymesini basin.
    echo.
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

netsh advfirewall firewall add rule name="AvtoPark Web %PORT%" dir=in action=allow protocol=TCP localport=%PORT% >nul
if %errorlevel% equ 0 (
    echo       [OK] Firewall qaydasi elave edildi.
) else (
    echo       [!] Firewall qaydasi elave edile bilmedi.
)
goto :fwindone

:fwiready
echo [1/2] Firewall qaydasi artiq movcuddur.

:fwindone
echo.
echo [2/2] Veb server basladilir...

taskkill /f /im EnterpriseAeroStudio.Web.exe >nul 2>&1
timeout /t 2 /nobreak >nul

start "AvtoPark - Veb Server" cmd /k call "%~dp0ishe-sal.bat"

echo       Server hazir olmasini gozleyirik (14 saniye)...
timeout /t 14 /nobreak >nul

:: ======================= YERLI IP UNVANLARINI TAP =======================
echo.
echo ======================================================================
echo    HAZIRDIR!  -  Bu unvani WiFi-deki cihazlarda acin
echo ======================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.PrefixOrigin -ne 'WellKnown' } | Select-Object -ExpandProperty IPAddress -Unique; if (-not $ips) { Write-Host '   [!] Sebeke unvani tapilmadi - WiFi-e qosuldugunuzu yoxlayin' -ForegroundColor Yellow } else { foreach ($ip in $ips) { Write-Host ('        http://' + $ip + ':%PORT%') -ForegroundColor Green } }; Write-Host ''; Write-Host '   Telefonunuzdan bu unvani yazin (eyni WiFi-de olmalidir).' -ForegroundColor Gray"

echo.
echo ======================================================================
echo    Bu kompuede  :  http://localhost:%PORT%
echo    Giris sifresi:  appsettings.json -^> AdminPassword
echo.
echo    Server penceresi ACIQ QALMALIDIR.
echo    Dayandirmaq ucun: o pencerede Ctrl+C
echo ======================================================================
echo.
pause
