<#
.SYNOPSIS
   021Cars — GitHub Release yaradan / yeniləyən skript (installer faylı ilə).

.DESCRIPTION
   ⚠ ƏSAS SƏBƏB: proqramın «🚀 Güncəllə» funksiyası yalnız GitHub RELEASES-i oxuyur
     (https://api.github.com/repos/Seccad-Elizade/021Cars/releases/latest).
     Sadəcə `git tag v6.2.80` + `git push` etmək KİFAYƏT DEYİL ✗ —
     RELEASE yaradılmasa proqram yeni versiyanı GÖRMÜR ✗✗ (v6.2.80-də baş verən hal).

.EXAMPLE
   cd Docs\Qurasdirma\Installer ; .\Yig-Installer.ps1
   cd ..                        ; .\Release-Yarat.ps1
#>
param(
    [string]$Tag,
    [string]$QeydFayl,
    [string]$Asset,
    [switch]$YalnizYoxla
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# ── 0) Yollar + versiya ─────────────────────────────────────────────────────
$qurasdirma = Split-Path -Parent $MyInvocation.MyCommand.Path
$kok        = Split-Path -Parent (Split-Path -Parent $qurasdirma)
$csproj     = Join-Path $kok "EnterpriseAeroStudio.csproj"

if (-not $Tag) {
    $version = ([xml](Get-Content -LiteralPath $csproj -Raw)).Project.PropertyGroup.Version |
               Where-Object { $_ } | Select-Object -First 1
    if (-not $version) { Write-Host "XƏTA: csproj-də <Version> tapılmadı ✗" -ForegroundColor Red; exit 1 }
    $Tag = "v" + $version.Trim()
}

if (-not $Asset)    { $Asset    = Join-Path $kok "publish_setup\021Cars_Installer.exe" }
if (-not $QeydFayl) {
    $namized = Join-Path $qurasdirma ("Buraxilis\" + $Tag + ".md")
    if (Test-Path -LiteralPath $namized) { $QeydFayl = $namized }
}

function Log([string]$m) { Write-Host ((Get-Date).ToString("HH:mm:ss") + "  " + $m) }

Write-Host ""
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host ("  🚀 GitHub RELEASE — " + $Tag) -ForegroundColor Cyan
Write-Host "  ===========================================================" -ForegroundColor DarkCyan

# ── 1) Installer faylı ──────────────────────────────────────────────────────
if (-not (Test-Path -LiteralPath $Asset)) {
    Write-Host ("  XƏTA: installer tapılmadı ✗ → " + $Asset) -ForegroundColor Red
    Write-Host "  Əvvəlcə: cd Docs\Qurasdirma\Installer ; .\Yig-Installer.ps1" -ForegroundColor Yellow
    exit 1
}

$faylVerziya = (Get-Item -LiteralPath $Asset).VersionInfo.FileVersion
$olcuMB      = [math]::Round((Get-Item -LiteralPath $Asset).Length / 1MB, 1)

Log ("Installer: " + $olcuMB + " MB · FileVersion = " + $faylVerziya)

if ($faylVerziya -and ($Tag.TrimStart('v','V') -ne ($faylVerziya -replace '\.0$',''))) {
    Write-Host ("  ⚠ DİQQƏT: teq (" + $Tag + ") ilə installer versiyası (" + $faylVerziya + ") FƏRQLİDİR ✗") -ForegroundColor Yellow
}


# ── 2) Token (git credential manager-dən) ───────────────────────────────────
#  ⚠ PS 5.1-də `... | git credential fill` bəzən stdin-i ötürmür ✗
#    («refusing to work with credential missing protocol field» ✗)
#  ✅ Ona görə sorğu FAYLA yazılır və `cmd /c ... < fayl` ilə verilir ✓
$env:GIT_TERMINAL_PROMPT = "0"

$sorguFayl = Join-Path $env:TEMP "021Cars_gitcred.txt"
[IO.File]::WriteAllText($sorguFayl, "protocol=https`nhost=github.com`n`n")

$setirler = cmd /c "git -C `"$kok`" credential fill < `"$sorguFayl`" 2>nul"
Remove-Item $sorguFayl -Force -EA 0

$tok = ($setirler -split "`n" | Where-Object { $_ -like "password=*" } | Select-Object -First 1) -replace "^password=", ""
$tok = ($tok -as [string])
if ($tok) { $tok = $tok.Trim() }

if ([string]::IsNullOrWhiteSpace($tok)) {
    Write-Host "  XƏTA: GitHub token tapılmadı ✗ (git credential manager)" -ForegroundColor Red
    exit 1
}

$h = @{
    Authorization          = "Bearer $tok"
    Accept                 = "application/vnd.github+json"
    "X-GitHub-Api-Version" = "2022-11-28"
    "User-Agent"           = "021Cars-release-helper"
}

try {
    $men = Invoke-RestMethod -Uri "https://api.github.com/user" -Headers $h -Method Get -TimeoutSec 40
    Log ("GitHub giriş: " + $men.login)
} catch {
    Write-Host ("  XƏTA: token yoxlanmadı ✗ — " + $_.Exception.Message) -ForegroundColor Red
    exit 1
}

# ── 3) Release (varsa istifadə olunur, yoxdursa yaradılır) ──────────────────
$rel = $null

try {
    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/Seccad-Elizade/021Cars/releases/tags/$Tag" -Headers $h -Method Get -TimeoutSec 40
    Log ("Release mövcuddur ✓ — id=" + $rel.id)
} catch {
    if ($YalnizYoxla) { Log "Release YOXDUR ✗ (yalnız yoxlama rejimi)"; exit 2 }

    Log "Release yoxdur → yaradılır…"

    if ($QeydFayl -and (Test-Path -LiteralPath $QeydFayl)) {
        $govde = [IO.File]::ReadAllText($QeydFayl)
    } else {
        $govde = "## 021Cars $Tag`n`nYeni buraxılış."
        Log "Qeyd faylı yoxdur → qısa mətn istifadə olunur"
    }

    # ⚠ PowerShell 5.1-in ConvertTo-Json-u bu mühitdə səhv JSON verir ✗
    #   (body → {"value": "..."} ✗ → GitHub 422) → JavaScriptSerializer ✓
    Add-Type -AssemblyName System.Web.Extensions
    $ser  = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $json = $ser.Serialize(@{ tag_name = $Tag; name = ("021Cars " + $Tag.TrimStart('v','V')); body = $govde; draft = $false; prerelease = $false })

    $jsonYolu = Join-Path $env:TEMP ("021Cars_release_" + $Tag + ".json")
    [IO.File]::WriteAllText($jsonYolu, $json, (New-Object System.Text.UTF8Encoding($false)))

    $rel = Invoke-RestMethod -Uri "https://api.github.com/repos/Seccad-Elizade/021Cars/releases" -Headers $h -Method Post -ContentType "application/json; charset=utf-8" -InFile $jsonYolu -TimeoutSec 90
    Remove-Item $jsonYolu -Force -EA 0

    Log ("Release YARADILDI ✓ — " + $rel.html_url)
}

if ($YalnizYoxla) {
    Log ("Assets: " + (($rel.assets | ForEach-Object { $_.name }) -join ", "))
    exit 0
}

# ── 4) Köhnə asset silinir (eyni ad) ────────────────────────────────────────
$assetAdi = Split-Path -Leaf $Asset

try
{
    $movcud = @()
    try { $movcud = Invoke-RestMethod -Uri "https://api.github.com/repos/Seccad-Elizade/021Cars/releases/$($rel.id)/assets" -Headers $h -Method Get -TimeoutSec 40 } catch { }

    foreach ($a in $movcud) {
        if ($a.name -eq $assetAdi) {
            Log ("Köhnə asset silinir — id=" + $a.id)
            try { Invoke-RestMethod -Uri "https://api.github.com/repos/Seccad-Elizade/021Cars/releases/assets/$($a.id)" -Headers $h -Method Delete -TimeoutSec 90 | Out-Null }
            catch { Log ("Asset silinmədi ✗ — " + $_.Exception.Message) }
        }
    }

    # ── 5) Yükləmə (Invoke-RestMethod -InFile → stream, HttpClient lazım deyil ✓)
    Log ("Yüklənir: " + $assetAdi + " (" + $olcuMB + " MB) …")

    $uri = "https://uploads.github.com/repos/Seccad-Elizade/021Cars/releases/$($rel.id)/assets?name=$assetAdi"

    $cavab = Invoke-RestMethod -Uri $uri -Headers $h -Method Post `
        -ContentType "application/octet-stream" -InFile $Asset -TimeoutSec 1800

    Log ("YÜKLƏNDİ ✓ — asset id=" + $cavab.id + " · ölçü=" + $cavab.size)
}
catch
{
    Log ("XƏTA (yükləmə mərhələsi) ✗ — " + $_.Exception.Message)
    Log ("Sətir: " + $_.InvocationInfo.ScriptLineNumber)
    exit 1
}

# ── 6) Yoxlama ──────────────────────────────────────────────────────────────
$son = Invoke-RestMethod -Uri "https://api.github.com/repos/Seccad-Elizade/021Cars/releases/latest" -Headers $h -Method Get -TimeoutSec 40

Write-Host ""
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host ("  releases/latest → " + $son.tag_name) -ForegroundColor Green
Write-Host ("  assetlər: " + (($son.assets | ForEach-Object { $_.name }) -join ", ")) -ForegroundColor Green
Write-Host ("  səhifə: " + $son.html_url) -ForegroundColor Cyan
Write-Host "  ===========================================================" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "  ✅ Proqram artıq bu versiyanı GÖRÜR ✓ (köhnə versiya → popup çıxacaq)" -ForegroundColor Green
Write-Host ""
