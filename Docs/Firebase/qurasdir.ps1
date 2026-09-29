# ============================================================================
#  ☁️ 021Cars — FIREBASE KODUNU TƏTBİQƏ QURAŞDIRAN SKRİPT
# ----------------------------------------------------------------------------
#  İSTİFADƏ:
#    ① Yoxlama (HEÇ NƏ DƏYİŞMİR ✓):   .\qurasdir.ps1
#    ② Quraşdırma:                     .\qurasdir.ps1 -TetbiqEt
#
#  NƏ EDİR:
#    Docs\Firebase\*.cs.txt  →  Services\Firebase\*.cs   (9 fayl ✓)
#    ⚠ App.xaml.cs-i DƏYİŞMİR ✗ — sonda göstərilən 3 sətri əl ilə əlavə edin ✓
# ============================================================================

param([switch]$TetbiqEt)

$ErrorActionPreference = 'Stop'

$kok   = Split-Path -Parent $MyInvocation.MyCommand.Path   # Docs\Firebase
$kok   = Split-Path -Parent $kok                           # Docs
$kok   = Split-Path -Parent $kok                           # Autocode (kök)

$menbe = Join-Path $kok 'Docs\Firebase'
$hedef = Join-Path $kok 'Services\Firebase'

# 📋 SƏNƏD NÖMRƏSİ → HƏDƏF FAYL ADI ✓
$xəritə = [ordered]@{
    '01_FirebaseOptions.cs.txt'    = 'FirebaseOptions.cs'
    '02_FirebaseEntity.cs.txt'     = 'FirebaseEntity.cs'
    '03_AppLogger.cs.txt'          = 'AppLogger.cs'
    '04_FirebaseRestClient.cs.txt' = 'FirebaseRestClient.cs'
    '05_UsbDriveDetector.cs.txt'   = 'UsbDriveDetector.cs'
    '06_FirebaseRepository.cs.txt' = 'FirebaseRepository.cs'
    '07_Modeller.cs.txt'           = 'Modeller.cs'
    '08_BuludKonteksti.cs.txt'     = 'BuludKonteksti.cs'
    '09_MediaXidmeti.cs.txt'       = 'MediaXidmeti.cs'
    '10_BuludKopru.cs.txt'         = 'BuludKopru.cs'
}

Write-Host ''
Write-Host '  ☁️  021Cars — FIREBASE QURAŞDIRMA' -ForegroundColor Cyan
Write-Host '  ═══════════════════════════════════════════════════════' -ForegroundColor DarkCyan
Write-Host ("  📂 MƏNBƏ : " + $menbe)
Write-Host ("  🎯 HƏDƏF : " + $hedef)
Write-Host ("  ⚙️  REJİM : " + $(if ($TetbiqEt) { 'TƏTBİQ ET ✓ (fayllar kopyalanacaq)' } else { 'YOXLAMA ✓ (heç nə dəyişmir ✗)' }))
Write-Host ''

$çatışmayan = 0

foreach ($cüt in $xəritə.GetEnumerator())
{
    $m = Join-Path $menbe $cüt.Key
    $h = Join-Path $hedef $cüt.Value

    if (Test-Path $m)
    {
        $kb = [math]::Round((Get-Item $m).Length / 1KB, 1)
        $mövcud = if (Test-Path $h) { ' (KÖHNƏSİNİN ÜZƏRİNƏ YAZILACAQ ⚠️)' } else { '' }
        Write-Host ("  ✅ {0,-32} → {1,-26} {2,6} KB{3}" -f $cüt.Key, $cüt.Value, $kb, $mövcud) -ForegroundColor Green

        if ($TetbiqEt)
        {
            if (-not (Test-Path $hedef)) { New-Item -ItemType Directory -Path $hedef -Force | Out-Null }
            Copy-Item $m $h -Force
        }
    }
    else
    {
        Write-Host ("  ❌ TAPILMADI: " + $cüt.Key) -ForegroundColor Red
        $çatışmayan++
    }
}

Write-Host ''

if ($TetbiqEt -and $çatışmayan -eq 0)
{
    Write-Host '  ✅ 9 FAYL QURAŞDIRILDI ✓ → Services\Firebase\' -ForegroundColor Green
    Write-Host ''
    Write-Host '  ── SON ADDIM: App.xaml.cs faylına BU 3 SƏTRİ əlavə edin ──' -ForegroundColor Yellow
    Write-Host '     ①  using Cas0201.Firebase;'
    Write-Host '     ②  public static BuludKonteksti Bulud { get; private set; } = null!;'
    Write-Host '     ③  OnStartup daxilində:   Bulud = new BuludKonteksti();  await Bulud.BaslatAsync();'
    Write-Host ''
    Write-Host '  Sonra:  dotnet build   →   tətbiqi işə salın ✓' -ForegroundColor Cyan
}
elseif (-not $TetbiqEt)
{
    Write-Host '  ℹ️  YOXLAMA REJİMİ — quraşdırmaq üçün:' -ForegroundColor Yellow
    Write-Host '        .\qurasdir.ps1 -TetbiqEt' -ForegroundColor White
}

Write-Host ''
