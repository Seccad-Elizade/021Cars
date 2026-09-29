@echo off
chcp 65001 >nul
title AvtoPark - ANI TEST (pulsuz muvəqqəti unvan)

set "CF=%ProgramFiles%\cloudflared\cloudflared.exe"
if not exist "%CF%" set "CF=%ProgramFiles(x86)%\cloudflared\cloudflared.exe"
if not exist "%CF%" set "CF=%LOCALAPPDATA%\cloudflared\cloudflared.exe"

echo.
echo ======================================================================
echo    ANI TEST  -  PULSUZ MUVVEQQETI INTERNET UNVANI
echo ======================================================================
echo.
echo    Bu skript DERHAL internetden acilan pulsuz bir unvan yaradir.
echo.
echo    XUSUSIYYETLER:
echo      + Domen LAZIM DEYIL
echo      + Cloudflare hesabi LAZIM DEYIL
echo      + PULSUZ (muvveqqeti, her defe deyisir)
echo      - Unvan her ise salmada DEYISIR (muvveqqetidir)
echo.
echo    TELEB: veb server artiq isleyir olmalidir (ishe-sal.bat)
echo    DAYANDIRMAQ: Ctrl + C
echo.
echo ======================================================================
echo.

if not exist "%CF%" goto YOXDUR

"%CF%" tunnel --url http://localhost:5000

echo.
echo [!] Test tuneli dayandi.
echo.
pause
goto :EOF


:YOXDUR
echo    [XETA] cloudflared QURASDIRILMAYIB!
echo.
echo ======================================================================
echo    COZUM:
echo.
echo       domen-qurasdir.bat faylina SAG KLIK edin
echo       -^>  "Run as administrator"
echo.
echo    O skript cloudflared-i quraşdirir. Sonra bu fayli
echo    yeniden ise sala bilersiniz.
echo ======================================================================
echo.
pause
