@echo off
chcp 65001 >nul
title AVTOMOBIL PARKI v6.0  -  HER SEYI BASLAT
cd /d "%~dp0"

:: ==========================================================================
::   AVTOPARK.bat  -  BIR FAYLDA BUTUN FUNKSIYALAR
:: --------------------------------------------------------------------------
::   1) Admin huququnu alir (ozunu yukseldir)
::   2) Kohne serveri dayandirir
::   3) Windows Firewall qaydalarini yaradir (80, 5000 TCP + 53 UDP)
::   4) "021cars.az" adini bu komputere yoneldir (hosts fayli)
::   5) Veb serveri basladir (port 80 + 5000 + yerli DNS serveri)
::   6) Her seyi yoxlayir ve unvanlari gosterir
::
::   DOMEN ALMAQ LAZIM DEYIL. PUL LAZIM DEYIL.
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
set "PORT=5000"
set "DOMAIN=021cars.az"

echo.
echo ======================================================================
echo    AVTOMOBIL PARKI v6.0   -   HER SEYI BASLAT
echo ======================================================================
echo.

:: ---------------------------------------------------- 2) KOHNE PROSES
echo [1/6] Kohne server dayandirilir...
taskkill /f /im EnterpriseAeroStudio.Web.exe >nul 2>&1
timeout /t 2 /nobreak >nul
echo       [OK]

:: ---------------------------------------------------- 3) FIREWALL
echo [2/6] Windows Firewall qaydalari...
netsh advfirewall firewall delete rule name="AvtoPark Web" >nul 2>&1
netsh advfirewall firewall delete rule name="AvtoPark DNS" >nul 2>&1
netsh advfirewall firewall add rule name="AvtoPark Web" dir=in action=allow protocol=TCP localport=80,5000 >nul
netsh advfirewall firewall add rule name="AvtoPark DNS" dir=in action=allow protocol=UDP localport=53 >nul
echo       [OK]  80, 5000 (TCP) ve 53 (UDP) acildi

:: ---------------------------------------------------- 4) HOSTS + IP
echo [3/6] "%DOMAIN%" bu komputere yonlendirilir...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ips=Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue|Where-Object{$_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and (($_.Name+' '+$_.InterfaceAlias) -notmatch 'vEthernet|Virtual|VMware|VirtualBox|Bluetooth|Loopback|WSL|Docker')}|Select-Object -ExpandProperty IPAddress -Unique; $ip=@($ips|Sort-Object{if($_ -like '192.168.*'){0}elseif($_ -like '10.*'){1}else{2}})[0]; if(-not $ip){Write-Host '      [!] Sebeke IP tapilmadi - WiFi-e qosulun' -ForegroundColor Yellow; exit 1}; $h=$env:SystemRoot+'\System32\drivers\etc\hosts'; $c=@(Get-Content $h -ErrorAction SilentlyContinue|Where-Object{$_ -notmatch '021cars\.az' -and $_ -notmatch 'avtopark\.local'}); $c+=$ip+'  021cars.az'; $c+=$ip+'  www.021cars.az'; $c+=$ip+'  avtopark.local'; Set-Content -Path $h -Value $c -Encoding ASCII -ErrorAction Stop; Write-Host ('      [OK]  '+$ip+'  =  021cars.az') -ForegroundColor Green"

if %errorlevel% neq 0 echo       [!] hosts faylina yazila bilmedi - davam edilir

:: ---------------------------------------------------- 5) SERVER
echo [4/6] Veb server basladilir (port 80 + 5000 + DNS)...
start "AvtoPark - Server (ACIQ QALMALIDIR)" cmd /k call "%~dp0ishe-sal.bat"

echo       Server hazir olmasini gozleyirik (16 saniye)...
timeout /t 16 /nobreak >nul

:: ---------------------------------------------------- 6) YOXLAMA
echo [5/6] Yoxlanilir...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$a=$false;$b=$false; try{$r=Invoke-WebRequest 'http://localhost:5000/health' -UseBasicParsing -TimeoutSec 12;$a=$r.StatusCode -eq 200}catch{}; try{$r=Invoke-WebRequest 'http://localhost/health' -UseBasicParsing -TimeoutSec 12;$b=$r.StatusCode -eq 200}catch{}; if($a){Write-Host '      [OK]    Server isleyir  (port 5000)' -ForegroundColor Green}else{Write-Host '      [XETA]  Server baslamadi!' -ForegroundColor Red}; if($b){Write-Host '      [OK]    Port 80 de isleyir' -ForegroundColor Green}else{Write-Host '      [!]     Port 80 islemir - basqa proqram tutub' -ForegroundColor Yellow}; try{$c=New-Object System.Net.Sockets.UdpClient(53);$c.Close();Write-Host '      [XETA]  DNS portu (53) tutulub!' -ForegroundColor Red}catch{Write-Host '      [OK]    DNS portu (53) hazir' -ForegroundColor Green}"

:: ---------------------------------------------------- 7) MELUMAT
echo [6/6] Hazirdir
echo.
echo ======================================================================
echo    HAZIRDIR!
echo ======================================================================
echo.
echo    BU KOMPUTERDE:
echo        http://%DOMAIN%
echo        http://localhost
echo.
echo    WIFI-DEKI CIHAZLAR ucun (telefon / planset / basqa komputer):
echo.
powershell -NoProfile -ExecutionPolicy Bypass -Command "$si=Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue|Where-Object{$_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and (($_.Name+' '+$_.InterfaceAlias) -notmatch 'vEthernet|Virtual|VMware|VirtualBox|Bluetooth|Loopback|WSL|Docker')}|Select-Object -ExpandProperty IPAddress -Unique; $ip=@($si|Sort-Object{if($_ -like '192.168.*'){0}elseif($_ -like '10.*'){1}else{2}})[0]; if($ip){Write-Host '   DERHAL ISLEYEN  (hec bir ayar lazim deyil):' -ForegroundColor White; Write-Host ('        http://'+$ip) -ForegroundColor Green; Write-Host ''; Write-Host '   021cars.az YAZMAQ ISTEYIRSINIZSE - 2 SECIM:' -ForegroundColor White; Write-Host ''; Write-Host '        A) BUTUN cihazlar ucun (BIR DEFE, TOVSIYE OLUNUR)' -ForegroundColor Cyan; Write-Host '           ->  router-dns.bat  faylini isledin' -ForegroundColor Cyan; Write-Host ''; Write-Host '        B) Yalniz bir cihaz ucun (telefon ayarlarindan)' -ForegroundColor Cyan; Write-Host ('           ->  cihazin DNS-ini  '+$ip+'  et') -ForegroundColor Cyan}else{Write-Host '   [!] Sebeke IP tapilmadi - WiFi-e qosulun' -ForegroundColor Yellow}"

echo.
echo ======================================================================
echo    GIRIS SIFRESI :  appsettings.json  -^>  AdminPassword
echo.
echo    "AvtoPark - Server" penceresi ACIQ QALMALIDIR!
echo    Dayandirmaq ucun: hemin pencerede Ctrl+C
echo.
echo    NOT: Bu skripti ADMIN kimi isledin - yerli DNS serveri
echo         (port 53) ve port 80 yalniz bu halda isleyir.
echo ======================================================================
echo.
pause
