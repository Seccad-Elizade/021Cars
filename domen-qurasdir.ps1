# ============================================================================
#  021cars.az  —  DOMEN QURULUMU (BİR DƏFƏ İŞƏ SALINIR)
# ----------------------------------------------------------------------------
#  Cloudflare Tunnel vasitəsilə 021cars.az domenini bu kompüterdəki veb
#  serverə bağlayır. Router-də port açmaq LAZIM DEYİL, HTTPS avtomatik gəlir.
#
#  TƏLƏB: 021cars.az domeni Cloudflare hesabınıza əlavə edilmiş olmalıdır
#         (nameserver-lər Cloudflare-ə yönəldilmiş).
# ============================================================================

$ErrorActionPreference = 'Stop'
$Host.UI.RawUI.WindowTitle = '021cars.az - Domen Qurulumu'

function Line { Write-Host ("=" * 70) -ForegroundColor DarkCyan }

Line
Write-Host "   021cars.az  —  DOMEN QURULUMU" -ForegroundColor Cyan
Line
Write-Host ""
Write-Host "   Bu skript ADMIN huququ TELEB ETMIR." -ForegroundColor Green
Write-Host ""

$domain = '021cars.az'

# ------------------------------------------- ÖN YOXLAMA: domen mövcuddur?
Write-Host "   On yoxlama: $domain domeni DNS-de axtarilir..." -ForegroundColor Yellow
Write-Host ""

$dnsOk = $false
try {
    $res = Resolve-DnsName $domain -Type NS -ErrorAction Stop
    if ($res) { $dnsOk = $true }
} catch { }

if (-not $dnsOk) {
    Write-Host "   ************************************************************" -ForegroundColor Yellow
    Write-Host "   *  $domain  DNS-DE TAPILMADI!            *" -ForegroundColor Yellow
    Write-Host "   ************************************************************" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   Bu, domen hele Cloudflare-e elave edilmediyi ucun ola biler." -ForegroundColor Gray
    Write-Host ""
    Write-Host "   MUTLEQ ON SERD (3 addim):" -ForegroundColor Cyan
    Write-Host "     1. $domain domeni SIZIN olmalidir (qeydiyyatdan kecmeli)" -ForegroundColor White
    Write-Host "        .az domenleri ->  https://www.nic.az" -ForegroundColor Gray
    Write-Host "     2. Domeni Cloudflare hesabiniza elave edin" -ForegroundColor White
    Write-Host "        https://dash.cloudflare.com  ->  Add site" -ForegroundColor Gray
    Write-Host "     3. Registratorunuzda NAMESERVER-leri Cloudflare-inkilerle" -ForegroundColor White
    Write-Host "        deyisin" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Bu 3 addim olmadan TUNEL ISLEMEYECEK." -ForegroundColor Yellow
    Write-Host ""

    $cavab = Read-Host "   Buna baxmayaraq davam etmek isteyirsiniz? (b/x)"
    if ($cavab -ne 'b' -and $cavab -ne 'B') {
        Write-Host ""
        Write-Host "   Cixildi. Evvelce domeni qeydiyyatdan kecirin." -ForegroundColor Gray
        Write-Host ""
        Write-Host "   Ani test ucun (domen LAZIM DEYIL):  test-tunel.bat" -ForegroundColor Cyan
        Write-Host ""
        Read-Host "Cixmaq ucun Enter basin"
        exit 0
    }
    Write-Host ""
}

# ------------------------------------------------------------ Portu appsettings-den
$port = 5000
try {
    $cfg = Get-Content (Join-Path $PSScriptRoot 'appsettings.json') -Raw -Encoding UTF8 |
           ConvertFrom-Json
    if ($cfg.AvtoPark.Port) { $port = [int]$cfg.AvtoPark.Port }
} catch { }
Write-Host "   Veb server portu : $port" -ForegroundColor Gray

# ------------------------------------------------------- 1) cloudflared quraşdır
Write-Host ""
Write-Host "[1/5] cloudflared aləti yoxlanılır..." -ForegroundColor Yellow

$cf = "$env:LOCALAPPDATA\cloudflared\cloudflared.exe"

if (Test-Path $cf) {
    Write-Host "      -> Artiq qurasdirilib: $cf" -ForegroundColor Green
} else {
    Write-Host "      -> Endirilir (admin lazim deyil, ~50 MB)..." -ForegroundColor Gray

    $installDir = "$env:LOCALAPPDATA\cloudflared"
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null

    $url = 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe'
    Invoke-WebRequest -Uri $url -OutFile $cf -UseBasicParsing -TimeoutSec 300

    Write-Host "      -> Quraşdırıldı: $cf" -ForegroundColor Green

    # Istifadeci PATH-a elave et (gelecek istifade ucun)
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($userPath -notlike "*$installDir*") {
        [Environment]::SetEnvironmentVariable('Path', "$userPath;$installDir", 'User')
    }
}

# ------------------------------------------------------------------ 2) Giriş (login)
Write-Host ""
Write-Host "[2/5] Cloudflare hesabına giriş..." -ForegroundColor Yellow
Write-Host "      Brauzer açılacaq -> 021cars.az seçin -> 'Authorize' basın." -ForegroundColor Gray
Write-Host ""

& $cf tunnel login

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "   [XETA] Giris ugursuz oldu." -ForegroundColor Red
    Write-Host "          Emin olun ki 021cars.az Cloudflare hesabinizdadir." -ForegroundColor Yellow
    Read-Host "Cixmaq ucun Enter basin"
    exit 1
}

$cfDir = Join-Path $env:USERPROFILE '.cloudflared'

# ------------------------------------------------------------- 3) Tunel yarat
Write-Host ""
Write-Host "[3/5] 'avtopark' tuneli yaradılır..." -ForegroundColor Yellow

$tunnels = & $cf tunnel list --output json 2>$null | ConvertFrom-Json
$existing = $tunnels | Where-Object { $_.name -eq 'avtopark' } | Select-Object -First 1

if ($existing) {
    $tunnelId = $existing.id
    Write-Host "      -> Tunel artiq movcuddur: $tunnelId" -ForegroundColor Green
} else {
    & $cf tunnel create avtopark 2>&1 | Out-String | Write-Host -ForegroundColor Gray

    Start-Sleep -Seconds 2
    $tunnels = & $cf tunnel list --output json 2>$null | ConvertFrom-Json
    $created = $tunnels | Where-Object { $_.name -eq 'avtopark' } | Select-Object -First 1

    if (-not $created) {
        Write-Host "   [XETA] Tunel yaradila bilmedi." -ForegroundColor Red
        Read-Host "Cixmaq ucun Enter basin"
        exit 1
    }

    $tunnelId = $created.id
    Write-Host "      -> YENI tunel yaradildi: $tunnelId" -ForegroundColor Green
}

$credFile = Join-Path $cfDir "$tunnelId.json"

# --------------------------------------------------------------- 4) DNS qeydləri
Write-Host ""
Write-Host "[4/5] DNS qeydləri bağlanır..." -ForegroundColor Yellow

foreach ($host_ in @($domain, "www.$domain")) {
    & $cf tunnel route dns avtopark $host_ 2>&1 | Out-String | Write-Host -ForegroundColor Gray
    Write-Host "      -> $host_  OK" -ForegroundColor Green
}

Write-Host ""
Write-Host "[5/5] Konfiqurasiya faylı yazılır..." -ForegroundColor Yellow

$configYml = @"
# 021cars.az - Avtomobil Parki veb serveri
# Avtomatik yaradildi: $(Get-Date -Format 'dd.MM.yyyy HH:mm')
tunnel: $tunnelId
credentials-file: '$credFile'

originRequest:
  connectTimeout: 30s
  noTLSVerify: true
  keepAliveTimeout: 90s

ingress:
  - hostname: $domain
    service: http://localhost:$port
  - hostname: www.$domain
    service: http://localhost:$port
  - service: http_status:404
"@

$configFile = Join-Path $cfDir 'config.yml'
Set-Content -Path $configFile -Value $configYml -Encoding UTF8

Write-Host "      -> Yazildi: $configFile" -ForegroundColor Green

# ---------------------------------------------------------------------- NƏTİCƏ
Write-Host ""
Line
Write-Host "   QURULUM TAMAMLANDI!" -ForegroundColor Green
Line
Write-Host ""
Write-Host "   Tunel ID        : $tunnelId" -ForegroundColor White
Write-Host "   Konfiqurasiya   : $configFile" -ForegroundColor White
Write-Host "   Domen           : https://$domain" -ForegroundColor White
Write-Host ""
Write-Host "   INDI NE ETMELI:" -ForegroundColor Cyan
Write-Host ""
Write-Host "   1) Serveri basladin   :  ishe-sal.bat" -ForegroundColor White
Write-Host "   2) Tuneli basladin    :  tunel-baslat.bat" -ForegroundColor White
Write-Host ""
Write-Host "   Her ikisini birlikde  :  her-seyi-baslat.bat" -ForegroundColor Yellow
Write-Host ""
Write-Host "   Sonra brauzerde acin  :  https://$domain" -ForegroundColor Green
Write-Host ""
Line
Read-Host "Cixmaq ucun Enter basin"
