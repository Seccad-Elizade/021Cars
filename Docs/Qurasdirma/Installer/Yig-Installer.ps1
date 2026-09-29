# ============================================================================
#  📦 021Cars — 021Cars_Installer.exe YIĞAN SKRİPT ✓✓✓
# ----------------------------------------------------------------------------
#  İSTİFADƏ:   cd Docs\Qurasdirma\Installer ; .\Yig-Installer.ps1
#
#  NƏ EDİR:
#    ⓪ 🧹 köhnə publish\ silinir ✓
#    ① 🌐 Web tətbiqini publish edir   → publish\   ★ ƏVVƏL ★ ✓✓✓
#    ② 🏗️ Masaüstü tətbiqi publish edir → publish\   ★ SONRA ★ ✓✓✓
#       (SİRA VACİBDİR ✗: Web əvvəl yığılmalıdır ✓ — yoxsa WPF «ref» faylları
#        əsl WindowsBase.dll-in üstünə yazır ✗ → tətbiq heç açılmır ✗)
#    ③ 📦 payload.zip yaradır →  Installer\payload.zip   ★ MÜTLƏQ BU QOVLUQDA ★ ✓
#    ④ 🏗️ TƏK FAYLLI installer-i yığır (payload.zip .exe-in İÇİNƏ gömülür ✓)
#
#  NƏTİCƏ:  Autocode\publish_setup\021Cars_Installer.exe  ← 📦 YALNIZ BU FAYL ✓✓✓
#           (hər şey İÇİNDƏDİR ✓ — yanında payload\ qovluğu lazım DEYİL ✗)
# ============================================================================

$ErrorActionPreference = "Stop"

$instDir = Split-Path -Parent $MyInvocation.MyCommand.Path          # Docs\Qurasdirma\Installer
$kök     = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $instDir))   # Autocode

$tətbiq  = Join-Path $kök "EnterpriseAeroStudio.csproj"
$web     = Join-Path $kök "Web\Autocode.Web.csproj"
$publish = Join-Path $kök "publish"
$setup   = Join-Path $kök "publish_setup"
$webPublish = Join-Path $kök "publish_web"                           # 🌐 Web üçün AYRI qovluq ✓
$zip     = Join-Path $instDir "payload.zip"                          # ★ .csproj-un YANINDA ★ ✓

Write-Host ""
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host "  📦 021Cars - QURASDIRICI YIGILIR ✓✓✓" -ForegroundColor Cyan
Write-Host "  ===========================================================" -ForegroundColor DarkCyan

# ────────────────────────────────────────────────────────────────────────────
#  ⓪ 🧹 KÖHNƏ PUBLISH SİLİNİR ✓✓✓
#     (vacib ✗: köhnə «ref» fayllar qalsa → dotnet publish onları KEÇİR ✗
#      → WindowsBase yüklənmir ✗ → proqram heç açılmır ✗✓✓✓)
# ────────────────────────────────────────────────────────────────────────────
Write-Host "  ⓪  Köhnə publish təmizlənir…" -ForegroundColor DarkGray

foreach ($q in @($publish, $webPublish)) {
    if (Test-Path $q) { Remove-Item $q -Recurse -Force }
}

# ────────────────────────────────────────────────────────────────────────────
#  ① 🏗️ MASAÜSTÜ TƏTBİQ → publish\ ✓ (TƏMİZ qovluğa ✓✓✓)
#     Yalnız bu publish əsl self-contained WPF fayllarını verir ✓
# ────────────────────────────────────────────────────────────────────────────
Write-Host "  ①  Masaüstü tətbiq publish edilir…" -ForegroundColor Yellow

dotnet publish $tətbiq -c Release -r win-x64 --self-contained true -o $publish --nologo | Out-Host

if (-not (Test-Path (Join-Path $publish "EnterpriseAeroStudio.exe"))) {
    Write-Host "  XETA: Tətbiq publish alınmadı ✗" -ForegroundColor Red
    return
}

# ────────────────────────────────────────────────────────────────────────────
#  ② 🌐 WEB TƏTBİQİ → AYRI QOVLUQ ✓✓✓
#     (eyni qovluğa yazılsa ✗ → yüngül «ref» WPF fayllarını gətirir ✗ →
#      əsl WindowsBase.dll-in üstünə yazır ✗ → tətbiq açılmır ✗✓✓✓)
# ────────────────────────────────────────────────────────────────────────────
if (Test-Path $web) {
    Write-Host "  ②  Web tətbiqi AYRI qovluğa publish edilir…" -ForegroundColor Yellow
    dotnet publish $web -c Release -r win-x64 --self-contained true -o $webPublish --nologo | Out-Host
}

# ────────────────────────────────────────────────────────────────────────────
#  ②ᵇ 🔀 BİRLƏŞDİRMƏ ✓✓✓ — Web-in YALNIZ ÇATIŞMAYAN faylları köçürülür ✓
#      (mövcud fayllar TOXUNULMUR ✗ → tətbiqin əsl WPF faylları qorunur ✓✓✓)
# ────────────────────────────────────────────────────────────────────────────
$köçürülən = 0

if (Test-Path $webPublish) {
    Write-Host "  ②ᵇ Web faylları birləşdirilir (yalnız çatışmayanlar ✓)…" -ForegroundColor Yellow

    Get-ChildItem $webPublish -Recurse -File | ForEach-Object {
        $nisbi = $_.FullName.Substring($webPublish.Length + 1)
        $hedef = Join-Path $publish $nisbi

        if (-not (Test-Path $hedef)) {
            New-Item -ItemType Directory -Path (Split-Path $hedef -Parent) -Force | Out-Null
            Copy-Item $_.FullName $hedef -Force
            $köçürülən++
        }
    }

    Remove-Item $webPublish -Recurse -Force -EA 0
    Write-Host ("     ✓ Web-dən " + $köçürülən + " fayl əlavə olundu ✓") -ForegroundColor Green
}

# ────────────────────────────────────────────────────────────────────────────
#  ②ᶜ 🔎 YOXLAMA ✓✓✓ — WPF faylları ƏSL olmalıdır ✓ (yoxsa proqram açılmaz ✗)
# ────────────────────────────────────────────────────────────────────────────
$wpf   = Join-Path $publish "WindowsBase.dll"
$wpfKB = if (Test-Path $wpf) { [math]::Round((Get-Item $wpf).Length / 1KB, 1) } else { 0 }

if ($wpfKB -lt 500) {
    Write-Host ("  XETA: WindowsBase.dll = " + $wpfKB + " KB ✗ — 'ref' faylıdır ✗ (əsl ~2200 KB olmalıdır ✓)") -ForegroundColor Red
    return
}

Write-Host ("     ✓ WPF faylları düzgündür — WindowsBase.dll = " + $wpfKB + " KB ✓") -ForegroundColor Green

# ────────────────────────────────────────────────────────────────────────────
#  ③ 📦 PAYLOAD.ZIP — INSTALLER QOVLUĞUNDA ✓✓✓
#     (EmbeddedResource məhz bu faylı .exe-in içinə gömür ✓)
# ────────────────────────────────────────────────────────────────────────────
Write-Host "  ③  payload.zip hazırlanır (tətbiq faylları sıxışdırılır)…" -ForegroundColor Yellow

$müvəqqəti = Join-Path $env:TEMP "021Cars_Payload"

if (Test-Path $müvəqqəti) { Remove-Item $müvəqqəti -Recurse -Force }
New-Item -ItemType Directory -Path $müvəqqəti -Force | Out-Null

Get-ChildItem $publish -Recurse -File |
    Where-Object { $_.FullName -notlike "*\_setup\*" -and $_.Extension -ne '.pdb' } |
    ForEach-Object {
        $nisbi = $_.FullName.Replace($publish + "\", "")
        $hedef = Join-Path $müvəqqəti $nisbi
        New-Item -ItemType Directory -Path (Split-Path $hedef -Parent) -Force | Out-Null
        Copy-Item $_.FullName $hedef -Force
    }

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $müvəqqəti "*") -DestinationPath $zip -CompressionLevel Optimal -Force

Write-Host ("     payload.zip hazir ✓ - " + [math]::Round((Get-Item $zip).Length / 1MB, 1) + " MB ✓") -ForegroundColor Green

# ────────────────────────────────────────────────────────────────────────────
#  ④ 🏗️ TƏK FAYLLI INSTALLERİN ÖZÜNÜ YIĞ ✓✓✓
# ────────────────────────────────────────────────────────────────────────────
Write-Host "  ④  021Cars_Installer.exe yığılır (tək fayl ✓)…" -ForegroundColor Yellow

if (Test-Path $setup) { Remove-Item $setup -Recurse -Force }
New-Item -ItemType Directory -Path $setup -Force | Out-Null

dotnet publish (Join-Path $instDir "021Cars_Installer.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $setup --nologo | Out-Host

# yalnız .exe qalsın ✓ (pdb və s. silinir ✗)
Get-ChildItem $setup -File | Where-Object { $_.Extension -ne '.exe' } | Remove-Item -Force -EA 0

$inst   = Join-Path $setup "021Cars_Installer.exe"
$instMB = if (Test-Path $inst) { [math]::Round((Get-Item $inst).Length / 1MB, 1) } else { 0 }

Remove-Item $müvəqqəti -Recurse -Force -EA 0

Write-Host ""
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host "  HAZIRDIR - TƏK FAYL INSTALLER ✓✓✓" -ForegroundColor Green
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host ("  FAYL: " + $inst) -ForegroundColor White
Write-Host ("  ÖLÇÜ: " + $instMB + " MB   (147 MB civarı = payload İÇİNDƏDİR ✓)") -ForegroundColor Gray
Write-Host ""
Write-Host "  İSTİFADƏ:" -ForegroundColor Cyan
Write-Host "     ① YALNIZ bu bir .exe faylını müştəriyə verin ✓" -ForegroundColor Cyan
Write-Host "     ② İki dəfə klikləyin → UAC «Bəli» ✓" -ForegroundColor Cyan
Write-Host "     ③ Yer seçin (default C:\Program Files\021Cars ✓) → [ QURAŞDIR ] ✓" -ForegroundColor Cyan
Write-Host "     ④ Proqram masaüstündeki qısayoldan açılır ✓" -ForegroundColor Cyan
Write-Host "     ⑤ Silmək üçün: {Quraşdırma yeri}\Uninstaller.exe ✓✓✓" -ForegroundColor Cyan
Write-Host ""
