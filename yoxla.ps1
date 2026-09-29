# ============================================================================
#  YOXLA  —  Butun sistemin veziyyeti
# ----------------------------------------------------------------------------
#  Bu skript hec ne deyismir. Yalniz YOXLAYIR ve ne etmek lazim oldugunu deyir.
# ============================================================================

$ErrorActionPreference = 'SilentlyContinue'
$Host.UI.RawUI.WindowTitle = 'AvtoPark - Sistem Yoxlamasi'

$ok = 0; $bad = 0; $todo = @()

function Line { Write-Host ("=" * 72) -ForegroundColor DarkCyan }
function Head($t) { Write-Host ""; Write-Host "  $t" -ForegroundColor Cyan; Write-Host ("  " + ("-" * 68)) -ForegroundColor DarkGray }
function OK($t)   { Write-Host "    [OK]    $t" -ForegroundColor Green;  $script:ok++ }
function BAD($t)  { Write-Host "    [XETA]  $t" -ForegroundColor Red;    $script:bad++ }
function WARN($t) { Write-Host "    [!]     $t" -ForegroundColor Yellow }
function INFO($t) { Write-Host "    ->      $t" -ForegroundColor Gray }

Clear-Host
Line
Write-Host "   AVTOMOBIL PARKI v6.0  -  SISTEM YOXLAMASI" -ForegroundColor Cyan
Line
Write-Host "   Tarix: $(Get-Date -Format 'dd.MM.yyyy HH:mm')" -ForegroundColor DarkGray

# ============================================================ 1) VEB SERVER
Head "1) VEB SERVER (5000 portu)"

$proc = Get-Process EnterpriseAeroStudio.Web -ErrorAction SilentlyContinue

if ($proc) {
    OK "Veb server ISLEYIR (PID $($proc.Id -join ', '))"
} else {
    WARN "Veb server islemir"
    $todo += "Veb serveri basladin:  ishe-sal.bat"
}

$listen = (netstat -ano | Select-String ':5000\s+.*LISTENING') -ne $null
if ($listen) { OK "5000 portu dinlenilir" } else { WARN "5000 portu bos" }

# ============================================================ 2) CLOUDFLARED
Head "2) CLOUDFLARED (domen tuneli ucun)"

$cf = $null
foreach ($p in @(
    "$env:ProgramFiles\cloudflared\cloudflared.exe",
    "${env:ProgramFiles(x86)}\cloudflared\cloudflared.exe",
    "$env:LOCALAPPDATA\cloudflared\cloudflared.exe")) {
    if (Test-Path $p) { $cf = $p; break }
}

if ($cf) {
    OK "Qurasdirilib: $cf"
} else {
    BAD "cloudflared QURASDIRILMAYIB"
    $todo += "domen-qurasdir.bat faylini ADMIN kimi isledin"
}

# ============================================================ 3) TUNEL
Head "3) TUNEL KONFIQURASIYASI"

$cfDir = Join-Path $env:USERPROFILE '.cloudflared'
$cfgFile = Join-Path $cfDir 'config.yml'

if (Test-Path $cfDir) { OK "Qovluq movcuddur: $cfDir" }
else { BAD "Qovluq yoxdur: $cfDir  (tunel yaradilmayib)" }

if (Test-Path $cfgFile) {
    OK "config.yml movcuddur"
    Get-Content $cfgFile -Encoding UTF8 |
        Where-Object { $_ -match 'tunnel:|hostname:|service:' } |
        ForEach-Object { INFO $_.Trim() }
} else {
    BAD "config.yml yoxdur - tunel qurulmayib"
    if (-not ($todo -like '*domen-qurasdir*')) { $todo += "domen-qurasdir.bat faylini ADMIN kimi isledin" }
}

# ============================================================ 4) DOMEN / DNS
Head "4) 021cars.az  DOMENI VE DNS"

$domain = '021cars.az'
$ips = @()

try {
    $res = Resolve-DnsName $domain -Type A -ErrorAction Stop
    $ips = @($res | Where-Object { $_.IPAddress } | ForEach-Object { $_.IPAddress })
} catch { }

if ($ips.Count -gt 0) {
    OK "$domain  ->  $($ips -join ', ')"
} else {
    BAD "$domain  ->  DNS-de TAPILMADI (Non-existent domain)"
    Write-Host ""
    Write-Host "        ***********************************************************" -ForegroundColor Yellow
    Write-Host "        *  021cars.az DOMENI QEYDIYYATDA DEYIL!                  *" -ForegroundColor Yellow
    Write-Host "        *                                                         *" -ForegroundColor Yellow
    Write-Host "        *  Bu domeni ALMAGINIZ LAZIMDIR                           *" -ForegroundColor Yellow
    Write-Host "        *  Qiymet: texminen 20-40 AZN / il                        *" -ForegroundColor Yellow
    Write-Host "        *  .az domenleri:  https://www.nic.az                     *" -ForegroundColor Yellow
    Write-Host "        ***********************************************************" -ForegroundColor Yellow
    $todo += "021cars.az domenini qeydiyyatdan kecirin (www.nic.az)"
}

# ============================================================ 5) MELUMATLAR
Head "5) MELUMATLAR"

$db = Join-Path $env:LOCALAPPDATA 'EnterpriseAeroStudio\avtopark.db'
if (Test-Path $db) {
    OK "Verilenler bazasi: $([math]::Round((Get-Item $db).Length/1KB,1)) KB"
} else {
    WARN "Verilenler bazasi hele yaradilmayib"
}

# ============================================================ YEKUN
Line
Write-Host "   YEKUN:  $ok OK   |   $bad XETA" -ForegroundColor $(if ($bad -gt 0) { 'Red' } else { 'Green' })
Line

if ($todo.Count -gt 0) {
    Write-Host ""
    Write-Host "   NE ETMEK LAZIM:" -ForegroundColor Yellow
    Write-Host ""
    $i = 1
    foreach ($t in ($todo | Select-Object -Unique)) {
        Write-Host "     $i) $t" -ForegroundColor White
        $i++
    }
    Write-Host ""
    Write-Host "   Ani test ucun (domen lazim deyil):  test-tunel.bat" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "   HER SEY HAZIRDIR!  ->  her-seyi-baslat.bat" -ForegroundColor Green
    Write-Host ""
}

Write-Host ""
Read-Host "Cixmaq ucun Enter basin"
