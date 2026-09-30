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

# ⚠⚠ `obj`/`bin` DƏ TƏMİZLƏNMƏLİDİR ✓✓✓ — ★ VACİB ★ (2026-09-29)
#    Köhnə build qalıqları qalsa → VEB layihəsinin (.NET 10) runtime faylları
#    masaüstü publish-inə qarışır ✗ → coreclr 10.x + WindowsBase 8.x ✗ →
#    «Could not load System.Runtime / WindowsBase» ✗ → proqram AÇILMIR ✗✓✓
foreach ($q in @("bin\Release\net8.0-windows", "obj\Release\net8.0-windows")) {
    $tam = Join-Path $kök $q
    if (Test-Path $tam) { Remove-Item $tam -Recurse -Force -EA 0 }
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
#  ②ᵇ 🔀 BİRLƏŞDİRMƏ ✓✓✓ — Web «Web\» ALT QOVLUĞUNA köçürülür ✓✓✓ (2026-09-29)
#      ★ VACİB ★: web .NET 10, masaüstü .NET 8-dir ✗ → EYNİ qovluqda
#      saxlanılsa `coreclr.dll` · `System.Runtime.dll` TOQQUŞUR ✗ →
#      biri MÜTLƏQ çökməli olur ✗✓✓ → indi web ÖZ runtime-ı ilə AYRI
#      «Web\» qovluğunda işləyir ✓✓✓ (masaüstü onu orada tapır ✓)
# ────────────────────────────────────────────────────────────────────────────
$köçürülən = 0
$webHedef  = Join-Path $publish "Web"

if (Test-Path $webPublish) {
    Write-Host "  ②ᵇ Web tətbiqi AYRI «Web\» qovluğuna köçürülür…" -ForegroundColor Yellow

    if (Test-Path $webHedef) { Remove-Item $webHedef -Recurse -Force }

    Get-ChildItem $webPublish -Recurse -File | ForEach-Object {
        $nisbi = $_.FullName.Substring($webPublish.Length + 1)
        $hedef = Join-Path $webHedef $nisbi

        New-Item -ItemType Directory -Path (Split-Path $hedef -Parent) -Force | Out-Null
        Copy-Item $_.FullName $hedef -Force
        $köçürülən++
    }

    Remove-Item $webPublish -Recurse -Force -EA 0
    Write-Host ("     ✓ Web-dən " + $köçürülən + " fayl «Web\» qovluğuna köçürüldü ✓") -ForegroundColor Green
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
#  ②.5 🧹 DATA TƏMİZLİYİ — ★ VACİB ★ ✓✓✓
#     ⚠ Müştəri paketində HEÇ BİR MAŞIN · KREDİT · XƏRC · İSTİFADƏÇİ datası
#       OLMAMALIDIR ✗ (tələb: «içində heç bir maşın datası olmadan» ✓)
#     → publish\ içində baza/JSON tapılsa SİLİNİR ✗ → payload-a DÜŞMÜR ✗✓✓
# ────────────────────────────────────────────────────────────────────────────
Write-Host "  ②.5 Təmizlik: baza / data faylları yoxlanılır…" -ForegroundColor Yellow

$dataAdlari = @(
    "avtopark.db", "avtopark.db-wal", "avtopark.db-shm", "021cars_data.db", "backup.db",
    "istifadeciler.json", "bulud_ayarlari.json", "transfer-sexler.json",
    "data_usb.json", "offline_queue.json", "app_errors.log", "app_errors.old.log"
)

$tapılan = @()

foreach ($ad in $dataAdlari) {
    $tam = Join-Path $publish $ad
    if (Test-Path $tam) { $tapılan += $tam }
}

# həmçinin hər hansı *.db / *.db-* faylı (alt qovluqlar daxil ✓)
$tapılan += (Get-ChildItem $publish -Recurse -File -EA 0 |
    Where-Object { $_.Name -like "*.db" -or $_.Name -like "*.db-*" -or $_.Name -like "*.db.*" } |
    ForEach-Object { $_.FullName })

$tapılan = $tapılan | Sort-Object -Unique

if ($tapılan.Count -gt 0) {
    Write-Host "     ⚠ DATA FAYLLARI TAPILDI → SİLİNİR ✗✓✓" -ForegroundColor Magenta

    foreach ($f in $tapılan) {
        Write-Host ("        ✗ " + $f.Replace($publish + "\", "")) -ForegroundColor DarkMagenta
        Remove-Item $f -Force -EA 0
    }
} else {
    Write-Host "     ✓ Təmizdir — baza / data faylı YOXDUR ✓ (paket BOŞ açılacaq ✓)" -ForegroundColor Green
}

# 📎 Media / Yedəklər / Hesabatlar / Loglar da paketə DÜŞMÜR ✗
foreach ($q in @("Media", "Yedekler", "Hesabatlar", "AutocodePDF", "Logs")) {
    $tam = Join-Path $publish $q

    if (Test-Path $tam) {
        Remove-Item $tam -Recurse -Force -EA 0
        Write-Host ("        ✗ qovluq silindi: " + $q) -ForegroundColor DarkMagenta
    }
}

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
#  ③.5 🔎 YOXLAMA — paketdə BAZA / MAŞIN DATASI YOXDURMU? ★ VACİB ★ ✓✓✓
# ────────────────────────────────────────────────────────────────────────────
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zx = [IO.Compression.ZipFile]::OpenRead($zip)
$bazaIcleri = @($zx.Entries | Where-Object {
    $_.FullName -like "*.db" -or $_.FullName -like "*.db-*" -or $_.FullName -like "*.db.*" -or
    $_.FullName -like "*avtopark*" -or $_.FullName -like "*istifadeciler*" -or
    $_.FullName -like "*bulud_ayarlari*" })
$zexe = @($zx.Entries | Where-Object { $_.Name -eq "EnterpriseAeroStudio.exe" })
$zx.Dispose()

if ($bazaIcleri.Count -gt 0) {
    Write-Host "  XETA: payload.zip içində baza/data faylı VAR ✗ — paket TƏMİZ DEYİL ✗" -ForegroundColor Red

    foreach ($b in $bazaIcleri) { Write-Host ("        ✗ " + $b.FullName) -ForegroundColor Red }

    return
}

if ($zexe.Count -eq 0) {
    Write-Host "  XETA: payload.zip içində EnterpriseAeroStudio.exe YOXDUR ✗" -ForegroundColor Red
    return
}

Write-Host "     ✓ YOXLAMA: paket TƏMİZDİR ✓ — baza · maşın · istifadəçi · bulud datası YOXDUR ✓✓✓" -ForegroundColor Green

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
Write-Host ("  ÖLÇÜ: " + $instMB + " MB   (~180 MB civarı = payload + tətbiq İÇİNDƏDİR ✓)") -ForegroundColor Gray
Write-Host "  🧹 PAKET TƏMİZDİR ✓ — içində BAZA · MAŞIN · KREDİT · İSTİFADƏÇİ datası YOXDUR ✗✓✓" -ForegroundColor Green
Write-Host ""
Write-Host "  İSTİFADƏ:" -ForegroundColor Cyan
Write-Host "     ① YALNIZ bu bir .exe faylını müştəriyə verin ✓" -ForegroundColor Cyan
Write-Host "     ② İki dəfə klikləyin → UAC «Bəli» ✓" -ForegroundColor Cyan
Write-Host "     ③ Yer seçin (default C:\Program Files\021Cars ✓) → [ QURAŞDIR ] ✓" -ForegroundColor Cyan
Write-Host "     ④ Proqram masaüstündeki qısayoldan açılır ✓" -ForegroundColor Cyan
Write-Host "     ⑤ Silmək üçün: {Quraşdırma yeri}\Uninstaller.exe ✓✓✓" -ForegroundColor Cyan
Write-Host ""
