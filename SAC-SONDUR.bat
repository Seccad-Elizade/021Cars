@echo off
chcp 65001 >nul
title Smart App Control - SONDURMEK (AvtoPark ucun)
cd /d "%~dp0"

:: ==========================================================================
::   SMART APP CONTROL  -  SONDURME
:: --------------------------------------------------------------------------
::   PROBLEM:
::     Windows 11 "Smart App Control" imzasiz tetbiqleri bloklayir.
::     EnterpriseAeroStudio.exe lokal yigildigi ucun imzasi yoxdur:
::         "Smart App Control has blocked part of this app"
::
::   BU SKRIPT:
::     1) Admin huququnu alir
::     2) Veziyyeti yoxlayir
::     3) XEBERDARLIQ gosterir ve TESDIQ isteyir
::     4) Smart App Control-u SONDUYUR
::     5) Restart teleb etdiyini bildirir
::
::   !!! XEBERDARLIQ !!!
::     Smart App Control BIR DEFE SONDURULDUKDEN SONRA
::     YENIDEN QOSULA BILMEZ - Windows-u yeniden qurmaq lazim olar.
::     Bu, Microsoft-un oz qeraridir.
:: ==========================================================================

:: ---------------------------------------------------- 1) ADMIN HUQUQU
net session >nul 2>&1
if %errorlevel% equ 0 goto :adminok

echo.
echo ======================================================================
echo    ADMIN HUQUQU TELEB OLUNUR
echo ======================================================================
echo.
echo    Windows size sorgu gonderecek - "Beli" / "Yes" duymesini basin.
echo.
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b

:adminok
echo.
echo ======================================================================
echo    SMART APP CONTROL  -  VEZIYYET YOXLANILIR
echo ======================================================================
echo.

:: ---------------------------------------------------- 2) VEZIYYET
for /f "tokens=3" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState 2^>nul') do set "SAC=%%a"

if "%SAC%"=="" (
    echo    [!] Ayar tapilmadi - Smart App Control movcud deyil.
    echo        Bu Windows versiyasinda problem yoxdur.
    goto :son
)

if "%SAC%"=="0" (
    echo    [OK]  Smart App Control ARTIQ SONUDUR.
    echo.
    echo          Tetbiq bloklanmamalidir.
    echo          Eger hele de acilmirsa - komputere RESTART edin.
    goto :son
)

if "%SAC%"=="2" (
    echo    [i]   Smart App Control QIYMETLENDIRME rejimindedir.
    echo          Bloklamir - davam etmek olar.
    goto :son
)

echo    [XETA] Smart App Control AKTIVDIR  ^(rejim: %SAC%^)
echo.
echo ======================================================================
echo    NE BAS VERIR?
echo ======================================================================
echo.
echo    Windows 11 imzasiz tetbiqleri bloklayir:
echo.
echo        "Smart App Control has blocked part of this app"
echo.
echo    Sizin AvtoPark tetbiqi lokal yigildigi ucun
echo    reqemsal imzasi yoxdur - ona gore bloklanir.
echo.
echo ======================================================================
echo    !!!    VACIB XEBERDARLIQ    !!!
echo ======================================================================
echo.
echo    Smart App Control BIR DEFE SONDURULDUKDEN SONRA
echo    YENIDEN QOSULA BILMEZ.
echo.
echo    Geri qosmaq ucun Windows-u YENIDEN QURMAQ lazim olar.
echo    Bu, Microsoft-un oz qeraridir - deyisdirile bilmez.
echo.
echo    ------------------------------------------------------------------
echo    ALTERNATIVLER (Smart App Control-u sondurmeden):
echo    ------------------------------------------------------------------
echo      1) Tetbiqi BASQA komputere kocurun  ^(Smart App Control olmayan^)
echo      2) Tetbiqi reqemsal imzali edin
echo         ^(illik 200-400 USD sertifikat lazimdir^)
echo    ------------------------------------------------------------------
echo.
echo    Sondurme riski: namelum menbali proqramlar
echo    artiq bloklanmayacaq. Windows Defender islemeye davam edir.
echo.
echo ======================================================================
echo.

set /p CAVAB="   Smart App Control SONDURULSUN? (b/x): "
if /i not "%CAVAB%"=="b" (
    echo.
    echo    Legv edildi - hec ne deyismedi.
    goto :son
)

:: ---------------------------------------------------- 3) SONDURME
echo.
echo    Sondurulur...
reg add "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState /t REG_DWORD /d 0 /f >nul

if %errorlevel% neq 0 (
    echo.
    echo    [XETA] Alinmadi! Skripti ADMIN kimi isledin.
    goto :son
)

echo    [OK]  Smart App Control SONDU.
echo.

:: ---------------------------------------------------- 4) YOXLAMA
for /f "tokens=3" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy" /v VerifiedAndReputablePolicyState 2^>nul') do set "YENI=%%a"
echo    Yeni veziyyet: %YENI%   ^(0 = sondurulub^)
echo.

echo ======================================================================
echo    ARTIQ RESTART LAZIMDIR
echo ======================================================================
echo.
echo    1) Komputere RESTART edin
echo    2) AVTOPARK.bat faylini ADMIN kimi isledin
echo    3) Tetbiq acilacaq
echo.
echo    Eger restartdan sonra da problem olsa:
echo       - Windows Security -^> App ^& browser control
echo       - Smart App Control veziyyetini yoxlayin
echo.

:son
echo.
pause
