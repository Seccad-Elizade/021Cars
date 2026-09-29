# ============================================================================
#  🚀 021Cars — GITHUB-A PAYLAŞMA VƏ GÜNCƏLLƏMƏ BURAXMA SKRİPTİ ✓✓✓
# ----------------------------------------------------------------------------
#  İSTİFADƏ:
#      cd C:\Users\021Cars_User\Desktop\Autocode\Docs\Qurasdirma
#      .\GitHub-Paylash.ps1                 # kodu push edir ✓
#      .\GitHub-Paylash.ps1 -Burax          # + RELEASE yaradır ✓✓✓
#
#  NƏ EDİR:
#    ① 🔢 Versiyanı oxuyur  (EnterpriseAeroStudio.csproj → <Version>)
#    ② 📤 Kodu GitHub-a push edir  (github.com/Seccad-Elizade/021Cars ✓)
#    ③ 🏷️ Teq yaradır:  v6.1
#    ④ 📦 RELEASE yaradır və 021Cars_Installer.exe-ni QOŞUR ✓✓✓
#         → bütün kompüterlər avtomatik görür və «🚀 GÜNCƏLLƏ» popup-u çıxır ✓
#
#  ⚠️ VACİB ✗: INSTALLER GITHUB-A «FAYL» KİMİ YÜKLƏNMİR ✗
#     (GitHub-ın fayl limiti 100 MB-dır ✗ — installer 146 MB-dır ✗)
#     → o, RELEASE-ə «ASSET» kimi əlavə olunur ✓✓✓
#
#  🔐 TOKEN (bir dəfə ✓):
#     1) https://github.com/settings/tokens → «Generate new token (classic)»
#     2) «repo» sahəsini işarələyin ✓
#     3) PowerShell-də:   $env:GITHUB_TOKEN = "ghp_xxxxxxxxxx"
# ============================================================================

param(
    [switch]$Burax,                 # 📦 GitHub Release yarat ✓
    [string]$Qeyd = "",             # 📝 Buraxılış qeydləri (boş = avtomatik ✓)
    [switch]$TəmizGit               # 🧹 .git qovluğunu yenidən qur ✓
)

$ErrorActionPreference = "Stop"

$kök     = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))
$instDir = Join-Path $kök "Docs\Qurasdirma\Installer"
$setup   = Join-Path $kök "publish_setup"
$inst    = Join-Path $setup "021Cars_Installer.exe"
$proj    = Join-Path $kök "EnterpriseAeroStudio.csproj"

$sahib = "Seccad-Elizade"
$depo  = "021Cars"
$url   = "https://github.com/$sahib/$depo.git"

Write-Host ""
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host "  🚀 021Cars - GITHUB PAYLASMA" -ForegroundColor Cyan
Write-Host "  ===========================================================" -ForegroundColor DarkCyan

# ────────────────────────────────────────────────────────────────────────────
#  ① 🔢 VERSİYA ✓
# ────────────────────────────────────────────────────────────────────────────
if (-not (Test-Path $proj)) { Write-Host "  XETA: csproj tapilmadi ✗" -ForegroundColor Red; return }

$mətn = Get-Content $proj -Raw

if ($mətn -notmatch '<Version>\s*([0-9]+(?:\.[0-9]+)+)\s*</Version>') {
    Write-Host "  XETA: <Version> tapilmadi ✗" -ForegroundColor Red
    return
}

$versiya = $Matches[1]
$teq     = "v" + $versiya

Write-Host ("  📦 Versiya : " + $versiya) -ForegroundColor White
Write-Host ("  🏷️ Teq     : " + $teq) -ForegroundColor White

# ────────────────────────────────────────────────────────────────────────────
#  ② 🧹 GIT HAZIRLIQ ✓
# ────────────────────────────────────────────────────────────────────────────
Set-Location $kök

# 🔧 GIT VAR? ✓ (yoxdursa ✗ → yalnız RELEASE hissəsi işləyir ✓)
$gitVar = [bool](Get-Command git -ErrorAction SilentlyContinue)

if (-not $gitVar) {
    Write-Host ""
    Write-Host "  ⚠️ GIT QURAŞDIRILMAYIB ✗ — kod GitHub-a push edilə bilməz ✗" -ForegroundColor Red
    Write-Host "     💡 Quraşdırın (bir əmr ✓):" -ForegroundColor Yellow
    Write-Host "          winget install Git.Git" -ForegroundColor Cyan
    Write-Host "        və ya: https://git-scm.com/download/win" -ForegroundColor Cyan
    Write-Host "     ✅ RELEASE + installer yenə yaradıla bilər ✓ (aşağıda davam edir ✓)" -ForegroundColor Gray
    Write-Host ""
}

if ($gitVar) {

if ($TəmizGit -and (Test-Path (Join-Path $kök ".git"))) {
    Write-Host "  🧹 .git silinir…" -ForegroundColor DarkYellow
    Remove-Item (Join-Path $kök ".git") -Recurse -Force
}

# 📄 .gitignore — BÖYÜK və GEREKSİZ fayllar GİTMƏSİN ✗✓✓
$gitignore = @"
# ============================================================
#  🚀 021Cars — GitHub-a GİTMƏYƏN fayllar ✗
# ============================================================

# 🔨 Build nəticələri
bin/
obj/
publish/
publish_web/
publish_setup/
publish_test/

# 📦 Installer paketi (146 MB ✗ — Release-ə asset kimi gedir ✓)
payload.zip
Docs/Qurasdirma/Installer/payload.zip

# 🗄️ İSTİFADƏÇİ MƏLUMATLARI ✗✓✓✓ (HEÇ VAXT ✗)
*.db
*.db-wal
*.db-shm
*.db-journal
Media/
Logs/
Yedekler/
Hesabatlar/
AutocodePDF/
app_errors.log
app_errors.old.log
istifadeciler.json
bulud_ayarlari.json
transfer-sexler.json
data_usb.json
offline_queue.json

# 🔑 Açarlar / tokenlər
*.token
.env
secrets.json

# 🖥️ IDE
.vs/
.vscode/
*.user
*.suo
*.bak

# 📄 Müvəqqəti
*.log
*.tmp
*.zip
021Cars_GSAP_*.html
"@

Set-Content (Join-Path $kök ".gitignore") -Value $gitignore -Encoding UTF8

if (-not (Test-Path (Join-Path $kök ".git"))) {
    Write-Host "  🆕 git deposu qurulur…" -ForegroundColor Yellow
    git init | Out-Host
    git branch -M main | Out-Host
}

# 🌐 «remote» yoxsa əlavə edilir ✓
$remote = (git remote 2>$null) -join " "

if ($remote -notmatch [regex]::Escape($depo)) {
    Write-Host "  🌐 remote əlavə edilir…" -ForegroundColor Yellow
    git remote remove origin 2>$null | Out-Null
    git remote add origin $url | Out-Host
}

# ────────────────────────────────────────────────────────────────────────────
#  ③ 📤 COMMIT + PUSH + TEQ ✓
# ────────────────────────────────────────────────────────────────────────────
Write-Host "  📤 Kod GitHub-a göndərilir…" -ForegroundColor Yellow

git add -A | Out-Host

$dəyişiklik = (git status --porcelain) -join "`n"

if ([string]::IsNullOrWhiteSpace($dəyişiklik)) {
    Write-Host "  ℹ️ Yeni dəyişiklik yoxdur ✓" -ForegroundColor DarkGray
} else {
    git commit -m "021Cars v$versiya" | Out-Host
}

git push -u origin main | Out-Host

Write-Host "  🏷️ Teq göndərilir…" -ForegroundColor Yellow
git tag -f $teq | Out-Host
git push -f origin $teq | Out-Host

Write-Host ("  ✅ Kod + teq hazırdır ✓ — github.com/" + $sahib + "/" + $depo) -ForegroundColor Green

}   # ← 🧹 GIT BLOKUNUN SONU ✓ (git yoxdursa bura atlanır ✗)

# ────────────────────────────────────────────────────────────────────────────
#  ④ 📦 RELEASE (installer asset kimi ✓✓✓)
# ────────────────────────────────────────────────────────────────────────────
if (-not $Burax) {
    Write-Host ""
    Write-Host "  📦 RELEASE yaratmaq üçün:" -ForegroundColor Cyan
    Write-Host "     .\GitHub-Paylash.ps1 -Burax -Qeyd `"nə dəyişdi...`"" -ForegroundColor Cyan
    Write-Host ""
    return
}

if (-not (Test-Path $inst)) {
    Write-Host "  XETA: 021Cars_Installer.exe tapılmadı ✗" -ForegroundColor Red
    Write-Host "  💡 Əvvəlcə yığın:  cd Docs\Qurasdirma\Installer ; .\Yig-Installer.ps1" -ForegroundColor Yellow
    return
}

$instMB = [math]::Round((Get-Item $inst).Length / 1MB, 1)

$token = $env:GITHUB_TOKEN

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host ""
    Write-Host "  🔐 GITHUB_TOKEN təyin olunmayıb ✗" -ForegroundColor Red
    Write-Host "     1) https://github.com/settings/tokens  → «Generate new token (classic)»" -ForegroundColor Yellow
    Write-Host "     2) «repo» sahəsini işarələyin ✓" -ForegroundColor Yellow
    Write-Host "     3) PowerShell-də:  `$env:GITHUB_TOKEN = `"ghp_...`"" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  💡 VƏ YA əl ilə:  github.com/$sahib/$depo/releases/new" -ForegroundColor Cyan
    Write-Host ("     · Teq: " + $teq + "  · Fayl: " + $inst) -ForegroundColor Cyan
    Write-Host ""
    return
}

$api     = "https://api.github.com/repos/$sahib/$depo"
$başlıq  = @{
    Authorization = "Bearer $token"
    "User-Agent"  = "021Cars-Release"
    Accept        = "application/vnd.github+json"
}

Write-Host ("  📦 Buraxılış yaradılır — " + $teq + " ✓") -ForegroundColor Yellow

# ♻️ Köhnə eyni teqli buraxılış varsa silinir ✓
try {
    $köhnə = Invoke-RestMethod -Uri "$api/releases/tags/$teq" -Headers $başlıq -EA 0

    if ($köhnə -and $köhnə.id) {
        Write-Host "  ♻️ Köhnə buraxılış silinir…" -ForegroundColor DarkYellow
        Invoke-RestMethod -Method Delete -Uri "$api/releases/$($köhnə.id)" -Headers $başlıq | Out-Null
    }
} catch { }

# 📝 Qeydlər ✓
if ([string]::IsNullOrWhiteSpace($Qeyd)) {
    $Qeyd = "## 🚀 021Cars v$versiya" + "`n`n" +
            "✅ Yeni versiya hazırdır ✓" + "`n" +
            "🖥️ Proqram avtomatik yoxlayır və «🚀 GÜNCƏLLƏ» popup-u göstərir ✓" + "`n" +
            "🛡️ Məlumatlarınız (baza · media · yedəklər) QORUNUR ✓✓✓"
}

$gövdə = @{
    tag_name   = $teq
    name       = "021Cars $versiya"
    body       = $Qeyd
    draft      = $false
    prerelease = $false
} | ConvertTo-Json

$buraxılış = Invoke-RestMethod -Method Post -Uri "$api/releases" `
    -Headers $başlıq -Body $gövdə -ContentType "application/json"

# ⬆️ INSTALLER faylını QOŞ ✓✓✓ (146 MB ✗ — asset limiti 2 GB ✓)
Write-Host ("  ⬆️ 021Cars_Installer.exe yüklənir — " + $instMB + " MB (bir az çəkə bilər ✓)…") -ForegroundColor Yellow

$yükleme = ($buraxılış.upload_url -replace '\{.*\}', '') + "?name=021Cars_Installer.exe"

Invoke-RestMethod -Method Post -Uri $yükleme -Headers $başlıq `
    -InFile $inst -ContentType "application/octet-stream" -TimeoutSec 7200 | Out-Null

Write-Host ""
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host "  ✅ BURAXILIŞ HAZIRDIR ✓✓✓" -ForegroundColor Green
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host ("  🔗 " + $buraxılış.html_url) -ForegroundColor Cyan
Write-Host ""
Write-Host "  🖥️ Bütün kompüterlər:" -ForegroundColor White
Write-Host "     · 10 dəqiqə içində avtomatik görəcək ✓" -ForegroundColor Gray
Write-Host "     · «🚀 GÜNCƏLLƏ» popup-u çıxacaq ✓" -ForegroundColor Gray
Write-Host "     · «GÜNCƏLLƏ» basanda proqram yenilənir və yenidən açılır ✓✓✓" -ForegroundColor Gray
Write-Host ""
