# ============================================================================
#  🔍 021Cars - XAML DIZAYN YOXLAYICISI
# ----------------------------------------------------------------------------
#  ISTIFADE:  .\Xaml-Yoxla.ps1
#
#  NE YOXLAYIR:
#    ① {StaticResource K} istinadlarinin HAMISI movcuddurmu?  (acar tapilmir?)
#    ② Bu istinadlar TEYINDEN SONRA gəlirmi?  <-- VACIB
#       ⚠ WPF-de «BasedOn» ve ya {StaticResource} IRELI istinad ede BILMEZ
#         (oz faylinda asagida teyin olunan acara basqa yerden baxmaq olmaz)
#       → xeta YALNIZ HE MIN DÜYMƏ ÇIZILƏNDƏ çıxır ✗
#         «Provide value on 'System.Windows.Markup.StaticResourceHolder'
#          threw an exception» ✗ → proqram ACILMIR ✗✗✗
#
#  ⚠ HER DIZAYN DEYISIKLIYINDEN SONRA ISLƏDIN ✓✓✓
# ============================================================================

$ErrorActionPreference = "Stop"
$kok = Split-Path -Parent $MyInvocation.MyCommand.Path
$kok = Split-Path -Parent $kok
$kok = Split-Path -Parent $kok

$fayllar = @()
$fayllar += (Get-ChildItem (Join-Path $kok "Resources") -Filter *.xaml -File | ForEach-Object { $_.FullName })
$fayllar += (Get-ChildItem (Join-Path $kok "Views") -Filter *.xaml -File | ForEach-Object { $_.FullName })
$fayllar += (Join-Path $kok "App.xaml")
$fayllar += (Join-Path $kok "Docs\Qurasdirma\Installer\InstallerWindow.xaml")

$problem = 0
$cemi = 0

foreach ($f in $fayllar) {
    if (-not (Test-Path $f)) { continue }

    $l = Get-Content $f -Encoding UTF8
    $teyin = @{}

    for ($i = 0; $i -lt $l.Count; $i++) {
        if ($l[$i] -match 'x:Key="([A-Za-z0-9_]+)"' -and -not $teyin.ContainsKey($Matches[1])) {
            $teyin[$Matches[1]] = $i + 1
        }
    }

    for ($i = 0; $i -lt $l.Count; $i++) {
        foreach ($h in [regex]::Matches($l[$i], '\{StaticResource ([A-Za-z0-9_]+)\}')) {
            $cemi++
            $acar = $h.Groups[1].Value

            if ($teyin.ContainsKey($acar) -and $teyin[$acar] -gt ($i + 1)) {
                Write-Host ("  XETA (IRELI ISTINAD): " + (Split-Path $f -Leaf) + " setir " + ($i + 1) + " -> " + $acar + " yalniz setir " + $teyin[$acar] + "-de teyin olunub") -ForegroundColor Red
                $problem++
            }
        }
    }
}

Write-Host ""
if ($problem -eq 0) {
    Write-Host ("  OK - " + $cemi + " istinad yoxlanildi, PROBLEM YOXDUR") -ForegroundColor Green
} else {
    Write-Host ("  XETA - " + $problem + " problem tapildi! (yuxariya bax)") -ForegroundColor Red
}
