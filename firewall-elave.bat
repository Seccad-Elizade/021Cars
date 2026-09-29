@echo off
chcp 65001 >nul
title AvtoPark - Firewall Qaydasi

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo   [XETA] Bu fayl ADMINISTRATOR kimi isledilmelidir!
    echo.
    echo   FAYLA SAG KLIK EDIN  ^>  "Run as administrator"
    echo.
    pause
    exit /b 1
)

echo.
echo ======================================================================
echo    WINDOWS FIREWALL - AvtoPark Web Server ucun icaze
echo ======================================================================
echo.

netsh advfirewall firewall delete rule name="AvtoPark Web 5000" >nul 2>&1
netsh advfirewall firewall add rule name="AvtoPark Web 5000" dir=in action=allow protocol=TCP localport=5000

if %errorlevel% equ 0 (
    echo.
    echo   [OK] 5000 portu sebeke ucun ACILDI.
    echo.
    echo   Artiq eyni WiFi-deki telefon/komputerler bu servere qosula bilər.
    echo.
) else (
    echo.
    echo   [XETA] Qayda elave edile bilmedi.
    echo.
)

pause
