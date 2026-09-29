@echo off
chcp 65001 >nul
title 021cars.az  -  Router DNS ayari (butun cihazlar ucun)

:: ==========================================================================
::   BUTUN CİHAZLAR UCUN "021cars.az"
:: --------------------------------------------------------------------------
::   Telefon/planset DNS-i elle deyismek istemirsinizse:
::   ROUTER-in DHCP DNS ayarini bu komputere yoneldin.
::   Bundan sonra WiFi-e qosulan BUTUN cihazlar 021cars.az yaza biler.
::
::   Bu skript hec ne deyismir - yalniz melumat verir ve
::   router panelini brauzerde acir.
:: ==========================================================================

echo.
echo ======================================================================
echo    021cars.az  -  BUTUN CİHAZLAR UCUN AYAR
echo ======================================================================
echo.
echo    Bu skript ROUTER-in konfiqurasiyasini gosterecek.
echo    Hec ne deyismir - her seyi ozunuz edirsiniz.
echo.
echo ======================================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -Command "$gw=(Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue|Sort-Object RouteMetric|Select-Object -First 1).NextHop; $si=(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue|Where-Object{$_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and (($_.Name+' '+$_.InterfaceAlias) -notmatch 'vEthernet|Virtual|VMware|VirtualBox|Bluetooth|Loopback|WSL|Docker')}|Select-Object -ExpandProperty IPAddress -Unique); $ip=@($si|Sort-Object{if($_ -like '192.168.*'){0}elseif($_ -like '10.*'){1}else{2}})[0]; Write-Host ''; Write-Host ('   Bu komputerin IP-si :  '+$ip) -ForegroundColor Green; Write-Host ('   Router-in IP-si     :  '+$gw) -ForegroundColor Cyan; Write-Host ''; $gw | Out-File ($env:TEMP+'\avtopark-gw.txt') -Encoding ASCII; $ip | Out-File ($env:TEMP+'\avtopark-ip.txt') -Encoding ASCII"

set /p GW=<"%TEMP%\avtopark-gw.txt"
set /p SRVIP=<"%TEMP%\avtopark-ip.txt"

echo.
echo ======================================================================
echo    ADIM-ADIM
echo ======================================================================
echo.
echo    1) Router panelini acin (asagida brauzer acilacaq):
echo            http://%GW%
echo.
echo       Istifadeci adi / sifre adeten router-in altindaki etiketde olur.
echo       (cox vaxt:  admin / admin     ve ya     admin / 1234)
echo.
echo    2) Bu bolmelerden birini tapin:
echo.
echo          . TP-Link   -^>  DHCP  -^>  DHCP Settings  -^>  Primary DNS
echo          . Keenetic  -^>  Ev Sebeke  -^>  Parametrler  -^>  DNS
echo          . Asus      -^>  LAN  -^>  DHCP Server  -^>  DNS Server
echo          . D-Link    -^>  Setup  -^>  Network Settings  -^>  DNS
echo          . Huawei    -^>  Sebeke  -^>  LAN  -^>  DHCP  -^>  DNS
echo          . ZTE       -^>  LAN  -^>  DHCP  -^>  DNS
echo          . Mi / Xiaomi -^>  Komut Ayarlari  -^>  DHCP  -^>  DNS 1
echo.
echo    3) DNS deyerlerini yazin:
echo.
echo            Birinci DNS  :  %SRVIP%
echo            Ikinci DNS   :  8.8.8.8
echo.
echo    4) "Yadda saxla" / "Save" basin.
echo.
echo    5) Simdi WiFi-e qosulan BUTUN cihazlar:
echo.
echo            http://021cars.az
echo.
echo       yaza biler! (DNS-i elle deyismeye ehtiyac yoxdur)
echo.
echo ======================================================================
echo    XEBARDARLIQ
echo ======================================================================
echo.
echo    Server komputeri SONDUKDE internet itmesin deye
echo    MUTLEQ "Ikinci DNS = 8.8.8.8" yazmaginiz vacibdir!
echo.
echo ======================================================================
echo.

set /p CAVAB="   Router panelini brauzerde acim? (b/x): "
if /i "%CAVAB%"=="b" start "" "http://%GW%"

echo.
echo    Cixildi. Ugurlar!
echo.
pause
