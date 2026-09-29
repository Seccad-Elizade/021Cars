# ============================================================================
#  021cars.az  -  DNS DIAQNOSTIKASI
# ============================================================================

$ErrorActionPreference = 'SilentlyContinue'
$Host.UI.RawUI.WindowTitle = '021cars.az - DNS Diaqnostikasi'

function OK($t)   { Write-Host "   [OK]    $t" -ForegroundColor Green }
function BAD($t)  { Write-Host "   [XETA]  $t" -ForegroundColor Red }
function WARN($t) { Write-Host "   [!]     $t" -ForegroundColor Yellow }

function Build-Query([string]$name) {
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    foreach ($v in 0x12,0x34,0x01,0x00,0x00,0x01,0x00,0x00,0x00,0x00,0x00,0x00) { $bw.Write([byte]$v) }
    foreach ($part in $name.Split('.')) {
        $bw.Write([byte]$part.Length)
        $bw.Write([System.Text.Encoding]::ASCII.GetBytes($part))
    }
    $bw.Write([byte]0x00)
    foreach ($v in 0x00,0x01, 0x00,0x01) { $bw.Write([byte]$v) }
    $bw.Flush()
    return $ms.ToArray()
}

function Query([string]$server, [string]$name) {
    try {
        $q = Build-Query $name
        $u = New-Object System.Net.Sockets.UdpClient
        $u.Client.ReceiveTimeout = 6000
        $u.Connect($server, 53)
        [void]$u.Send($q, $q.Length)
        $ep = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        $r = $u.Receive([ref]$ep)
        $u.Close()
        return "$($r[$r.Length-4]).$($r[$r.Length-3]).$($r[$r.Length-2]).$($r[$r.Length-1])"
    } catch {
        return $null
    }
}

$lanIp = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object {
        $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and
        (($_.Name + ' ' + $_.InterfaceAlias) -notmatch 'vEthernet|Virtual|VMware|VirtualBox|Bluetooth|Loopback|WSL|Docker')
    } | Select-Object -ExpandProperty IPAddress -Unique |
    Sort-Object { if ($_ -like '192.168.*') { 0 } elseif ($_ -like '10.*') { 1 } else { 2 } })[0]

Write-Host ""
Write-Host "   Bu komputerin IP-si :  $lanIp" -ForegroundColor Cyan

# ---------------------------------------------------------- 1) VEB SERVER
Write-Host ""
Write-Host "   1) VEB SERVER" -ForegroundColor White

$srv = Get-Process EnterpriseAeroStudio.Web -ErrorAction SilentlyContinue
if ($srv) { OK "Isleyir (PID $($srv.Id -join ','))" } else { BAD "ISLEMIR - AVTOPARK.bat faylini isledin" }

foreach ($p in 80, 5000) {
    if ((netstat -ano -p TCP | Select-String ":$p\s+.*LISTENING")) { OK "Port $p dinlenilir" } else { WARN "Port $p bos" }
}

# ---------------------------------------------------------- 2) DNS SERVERI
Write-Host ""
Write-Host "   2) DNS SERVERI (port 53)" -ForegroundColor White

if ((netstat -ano -p UDP | Select-String ':53\s')) { OK "Port 53 (UDP) dinlenilir" } else { BAD "Port 53 BOS - DNS islemir" }

# ---------------------------------------------------------- 3) CAVAB VERIR?
Write-Host ""
Write-Host "   3) 021cars.az CAVABI" -ForegroundColor White

$r1 = Query '127.0.0.1' '021cars.az'
if ($r1) { OK "127.0.0.1        ->  $r1" } else { BAD "127.0.0.1        ->  cavab yoxdur" }

$r2 = $null
if ($lanIp) {
    $r2 = Query $lanIp '021cars.az'
    if ($r2) { OK "$lanIp     ->  $r2" } else { BAD "$lanIp     ->  cavab yoxdur (FIREWALL!)" }
}

if ($lanIp) {
    $r3 = Query $lanIp 'google.com'
    if ($r3) { OK "Oturme testi     ->  google.com = $r3" } else { WARN "Oturme islemiyor" }
}

# ---------------------------------------------------------- 4) WINDOWS DNS
Write-Host ""
Write-Host "   4) WINDOWS HANSI DNS-I ISTIFADE EDIR" -ForegroundColor White

Get-DnsClientServerAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.ServerAddresses.Count -gt 0 -and $_.InterfaceAlias -notmatch 'Loopback' } |
    ForEach-Object {
        $list = $_.ServerAddresses -join ', '
        if ($list -like "*$lanIp*") { OK "$($_.InterfaceAlias)  ->  $list   (BIZIM DNS)" }
        else { WARN "$($_.InterfaceAlias)  ->  $list" }
    }

# ---------------------------------------------------------- 5) FIREWALL
Write-Host ""
Write-Host "   5) FIREWALL QAYDALARI" -ForegroundColor White

$rules = Get-NetFirewallRule -DisplayName 'AvtoPark*' -ErrorAction SilentlyContinue
if ($rules) {
    foreach ($x in $rules) {
        $pf = $x | Get-NetFirewallPortFilter
        OK "$($x.DisplayName)  [$($pf.Protocol) $($pf.LocalPort -join ',')]"
    }
} else {
    BAD "Qayda yoxdur - AVTOPARK.bat-i ADMIN kimi isledin"
}

# ============================================================== YEKUN
Write-Host ""
Write-Host ("=" * 72) -ForegroundColor DarkCyan
Write-Host "   YEKUN" -ForegroundColor Cyan
Write-Host ("=" * 72) -ForegroundColor DarkCyan
Write-Host ""

if ($r2) {
    Write-Host "   SERVER TEREHI TAM HAZIRDIR!" -ForegroundColor Green
    Write-Host ""
    Write-Host "   Telefonda helede islemirse - sebeb BUDUR:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "     Telefon KOHNE DNS-i saxlayir (DHCP lease 120 deqiqe)." -ForegroundColor White
    Write-Host ""
    Write-Host "     HELLI (10 saniye):" -ForegroundColor Cyan
    Write-Host "       1) Telefonda WiFi-i SONDUReN ve YANDIRIN" -ForegroundColor White
    Write-Host "       2) 10 saniye gozleyin" -ForegroundColor White
    Write-Host "       3) Brauzerde yazin:   http://021cars.az" -ForegroundColor Green
    Write-Host ""
    Write-Host "     Alternativ (daha surətli):" -ForegroundColor Gray
    Write-Host "       Telefonun brauzerinde:  http://$lanIp" -ForegroundColor Gray
    Write-Host ""
    Write-Host "     YOXLAMAQ:" -ForegroundColor Gray
    Write-Host "       Telefonda WiFi -> ag adi -> DNS = $lanIp olmalidir" -ForegroundColor Gray
} else {
    Write-Host "   PROBLEM: DNS serveri sebeke uzre cavab VERMIR" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   HELLI:" -ForegroundColor Cyan
    Write-Host "       1) AVTOPARK.bat faylini ADMIN kimi isledin" -ForegroundColor White
    Write-Host "       2) Bu skripti yeniden yoxlayin" -ForegroundColor White
}

Write-Host ""
Write-Host ("=" * 72) -ForegroundColor DarkCyan
Write-Host ""
Read-Host "   Cixmaq ucun Enter basin"
