# ============================================================================
#  🏗️ 021Cars — BUILD + QURAŞDIRMA + SİLMƏ skripti ✓✓✓
# ----------------------------------------------------------------------------
#  İSTİFADƏ:
#    ①  .\qur.ps1                         → 🏗️ PUBLISH (publish\ qovluğuna ✓)
#    ②  .\qur.ps1 -Qurasdir                → 📦 QURAŞDIR (yeri soruşur ✓ · default C:\Program Files\021Cars ✓)
#    ③  .\qur.ps1 -Qurasdir "D:\021Cars"   → 📦 Verilmiş yerə quraşdır ✓
#    ④  .\qur.ps1 -Sil                     → ❌ SİL (uninstall ✓)
# ----------------------------------------------------------------------------
#  ✅ Bütün fayllar QURAŞDIRMA QOVLUĞUNDA ✓ (AppData-ya heç nə yazılmır ✗)
#  ✅ Uninstaller «Proqramlar və Xüsusiyyətlər»-də görünür ✓
# ============================================================================

param(
    [switch]$Qurasdir,
    [switch]$Sil,
    [string]$Yer = "",
    [string]$Konfiq = "Release"
)

$ErrorActionPreference = "Stop"

$kök     = Split-Path -Parent $MyInvocation.MyCommand.Path   # Docs\Qurasdirma
$kök     = Split-Path -Parent $kök                            # Docs
$kök     = Split-Path -Parent $kök                            # Autocode (kök)
$publish = Join-Path $kök "publish"

$AppAd   = "021Cars"
$TamAd   = "021Cars — Avtomobil Parkı"
$Exe     = "EnterpriseAeroStudio.exe"

function Başlıq($m) {
    Write-Host ""
    Write-Host "  ═══════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
    Write-Host "  $m" -ForegroundColor Cyan
    Write-Host "  ═══════════════════════════════════════════════════════════" -ForegroundColor DarkCyan
}

# ============================================================================
#  ① 🏗️ PUBLISH — TƏK QOVLUĞA YIĞMA ✓
# ============================================================================
function PublishEt {
    Başlıq "🏗️ 021Cars PUBLISH edilir ($Konfiq)"

    if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
    New-Item -ItemType Directory -Path $publish -Force | Out-Null

    # 🖥️ Masaüstü tətbiq (öz-özünə işləyən ✓ — .NET quraşdırmaq lazım DEYİL ✗✓✓)
    Write-Host "  🖥️ Masaüstü tətbiq..." -ForegroundColor Yellow
    dotnet publish (Join-Path $kök "EnterpriseAeroStudio.csproj") `
        -c $Konfiq -r win-x64 --self-contained true `
        -p:PublishSingleFile=false `
        -o $publish | Out-Host

    # 🌐 Veb tətbiq (masaüstü onu avtomatik işə salır ✓)
    $web = Join-Path $kök "Web\Autocode.Web.csproj"

    if (Test-Path $web) {
        Write-Host "  🌐 Veb tətbiq..." -ForegroundColor Yellow
        $webOut = Join-Path $kök "publish_web"

        dotnet publish $web -c $Konfiq -r win-x64 --self-contained true -o $webOut | Out-Host

        if (Test-Path $webOut) {
            # ⚠ Veb faylları EYNİ qovluğa ✓ (masaüstü .Web.exe-ni buradan tapır ✓)
            Copy-Item (Join-Path $webOut "*") $publish -Recurse -Force
            Remove-Item $webOut -Recurse -Force -EA 0
        }
    }

    Write-Host ""
    Write-Host "  ✅ PUBLISH HAZIR ✓ → $publish" -ForegroundColor Green
    Write-Host ("  📦 " + (Get-ChildItem $publish -File | Measure-Object).Count + " fayl ✓") -ForegroundColor Gray
}

# ============================================================================
#  ② 📦 QURAŞDIR — seçilmiş qovluğa ✓ (default: C:\Program Files\021Cars ✓)
# ============================================================================
function QurasdirEt {
    Başlıq "📦 021Cars quraşdırılır"

    if (-not (Test-Path (Join-Path $publish $Exe))) {
        Write-Host "  ⚠️ Əvvəlcə publish edin:  .\qur.ps1" -ForegroundColor Red
        return
    }

    if ([string]::IsNullOrWhiteSpace($Yer)) {
        $Yer = "C:\Program Files\021Cars"
        Write-Host "  📍 Quraşdırma yeri (Enter = default):" -ForegroundColor Yellow
        $cavab = Read-Host "     [$Yer]"
        if (-not [string]::IsNullOrWhiteSpace($cavab)) { $Yer = $cavab.Trim('"') }
    }

    Write-Host "  📂 HƏDƏF: $Yer" -ForegroundColor Cyan

    if (-not (Test-Path $Yer)) { New-Item -ItemType Directory -Path $Yer -Force | Out-Null }

    # 📁 data qovluqları ✓ (hamısı QURAŞDIRMA yerində ✓ — AppData DEYİL ✗)
    foreach ($alt in @("Logs", "Yedekler\Json", "Media", "Hesabatlar", "AutocodePDF")) {
        New-Item -ItemType Directory -Path (Join-Path $Yer $alt) -Force | Out-Null
    }

    Copy-Item (Join-Path $publish "*") $Yer -Recurse -Force

    # 🔐 YAZMA İCAZƏSİ ✓ (Program Files adi istifadəçiyə yazıla bilmir ✗✓✓)
    Write-Host "  🔐 Yazma icazəsi verilir (Users → Modify)..." -ForegroundColor Yellow
    try {
        $acl = Get-Acl $Yer
        $qayda = New-Object System.Security.AccessControl.FileSystemAccessRule(
            "BUILTIN\Users", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow")
        $acl.SetAccessRule($qayda)
        Set-Acl $Yer $acl
        Write-Host "  ✅ İcazə verildi ✓" -ForegroundColor Green
    }
    catch { Write-Host "  ⚠️ İcazə verilə bilmədi ✗ (admin kimi işlədin ✓)" -ForegroundColor Red }

    # 🔗 Qısayollar ✓
    foreach ($hədəf in @(
        (Join-Path $env:PUBLIC "Desktop"),
        (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"))) {

        try {
            $ws = New-Object -ComObject WScript.Shell
            $lnk = $ws.CreateShortcut((Join-Path $hədəf "$TamAd.lnk"))
            $lnk.TargetPath = Join-Path $Yer $Exe
            $lnk.WorkingDirectory = $Yer
            $lnk.Save()
        }
        catch { }
    }

    # 🗑️ Uninstaller + «Proqramlar və Xüsusiyyətlər» qeydi ✓✓✓
    $sil = Join-Path $Yer "Uninstall-021Cars.ps1"
    $silMətn = "Remove-Item '" + $Yer + "' -Recurse -Force -EA 0; " +
               "Remove-Item 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\021Cars' -Recurse -EA 0; " +
               "Remove-Item (Join-Path `$env:PUBLIC 'Desktop\$TamAd.lnk') -EA 0; " +
               "Write-Host '021Cars silindi ✓'"
    Set-Content -Path $sil -Value $silMətn -Encoding UTF8

    try {
        $reg = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\021Cars"
        New-Item -Path $reg -Force | Out-Null
        Set-ItemProperty $reg "DisplayName"     "021Cars — Avtomobil Parkı"
        Set-ItemProperty $reg "DisplayVersion"  "6.0"
        Set-ItemProperty $reg "Publisher"       "021Cars"
        Set-ItemProperty $reg "InstallLocation" $Yer
        Set-ItemProperty $reg "UninstallString" "powershell.exe -ExecutionPolicy Bypass -File `"$sil`""
        Set-ItemProperty $reg "NoModify" 1
        Set-ItemProperty $reg "NoRepair" 1
        Write-Host "  ✅ «Proqramlar və Xüsusiyyətlər»-də görünür ✓" -ForegroundColor Green
    }
    catch { Write-Host "  ⚠️ Reyestr yazıla bilmədi ✗ (admin lazımdır ✓)" -ForegroundColor Red }

    Write-Host ""
    Write-Host "  ✅ QURAŞDIRILDI ✓ → $Yer" -ForegroundColor Green
    Write-Host "  🚀 İşə salın: $Yer\$Exe" -ForegroundColor White
    Write-Host "  📜 Loqlar    : $Yer\Logs" -ForegroundColor Gray
    Write-Host "  💾 Yedəklər  : $Yer\Yedekler\Json" -ForegroundColor Gray
    Write-Host "  📎 Media     : $Yer\Media" -ForegroundColor Gray
}

# ============================================================================
#  ③ ❌ SİL (UNINSTALL) ✓
# ============================================================================
function SilEt {
    Başlıq "❌ 021Cars SİLİNİR"

    foreach ($y in @("C:\Program Files\021Cars", "C:\Program Files (x86)\021Cars")) {
        if (Test-Path (Join-Path $y $Exe)) {
            Write-Host "  📂 Tapıldı: $y" -ForegroundColor Yellow
            Remove-Item $y -Recurse -Force -EA 0
            Write-Host "  ✅ Silindi ✓" -ForegroundColor Green
        }
    }

    Remove-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\021Cars" -Recurse -EA 0
    Remove-Item (Join-Path $env:PUBLIC "Desktop\$TamAd.lnk") -EA 0
    Remove-Item (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$TamAd.lnk") -EA 0

    Write-Host ""
    Write-Host "  ✅ 021Cars silindi ✓" -ForegroundColor Green
}

# ============================================================================
#  ▶️ İCRA ✓
# ============================================================================
if ($Sil)          { SilEt }
elseif ($Qurasdir) { PublishEt; QurasdirEt }
else               { PublishEt }

Write-Host ""
