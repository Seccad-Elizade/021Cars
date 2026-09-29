# ============================================================================
#  021Cars - IN-PLACE UPDATE (self-elevating)
# ----------------------------------------------------------------------------
#  WHAT IT DOES (in the ELEVATED process):
#    1) closes 021Cars (it runs elevated, so admin is required)
#    2) copies the fresh build from <Autocode>\publish  ->  C:\Program Files\021Cars
#    3) verifies the new EnterpriseAeroStudio.dll / .exe timestamps
#  It writes a log to %TEMP%\guncelle.txt
#
#  USAGE: right-click -> Run with PowerShell   (UAC -> Yes)
#         or run it and click "Yes" on the UAC prompt
# ============================================================================

$ErrorActionPreference = 'Continue'

$src = 'c:\Users\021Cars_User\Desktop\Autocode\publish'
$dst = 'C:\Program Files\021Cars'
$out = Join-Path $env:TEMP 'guncelle.txt'

# ---- self-elevate ----
$isAdmin = ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath)
    Write-Host 'UAC prompt opened - please click Yes.'
    exit
}

$l = @()
$l += ('=== IN-PLACE UPDATE === ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
$l += ('ADMIN = ' + $isAdmin)

if (-not (Test-Path $src)) { $l += 'ERROR: publish folder missing'; $l | Set-Content $out -Encoding UTF8; return }
if (-not (Test-Path $dst)) { $l += 'ERROR: install folder missing'; $l | Set-Content $out -Encoding UTF8; return }

# ---- 1) close the app ----
$l += '=== STOPPING APP ==='
for ($i = 0; $i -lt 12; $i++) {
    $p = Get-Process -Name 'EnterpriseAeroStudio' -EA 0
    if (-not $p) { break }
    foreach ($x in @($p)) { try { Stop-Process -Id $x.Id -Force -EA 0 } catch { } }
    Start-Sleep -Seconds 2
}
$p = Get-Process -Name 'EnterpriseAeroStudio' -EA 0
if ($p) { $l += ('  STILL RUNNING: ' + (@($p).Count)) } else { $l += '  APP CLOSED OK' }

# ---- 2) copy ----
$l += '=== COPYING ==='
$n = 0
$fail = 0
Get-ChildItem $src -Recurse -File | Where-Object {
        $_.Extension -ne '.pdb' -and $_.Name -ne 'appsettings.json' -and $_.Name -ne 'bulud_ayarlari.json'
    } | ForEach-Object {
        $rel = $_.FullName.Substring($src.Length + 1)
        $t   = Join-Path $dst $rel
        $d   = Split-Path $t -Parent
        if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
        try { Copy-Item $_.FullName $t -Force -EA Stop; $n++ }
        catch { $fail++; if ($fail -le 12) { $l += ('  FAIL ' + $rel) } }
    }
$l += ('  copied = ' + $n + ' | failed = ' + $fail)

# ---- 3) verify ----
$l += '=== VERIFY ==='
foreach ($v in @('EnterpriseAeroStudio.dll', 'EnterpriseAeroStudio.exe', 'EnterpriseAeroStudio.deps.json')) {
    $f = Get-Item (Join-Path $dst $v) -EA 0
    if ($f) { $l += ('  ' + $v + ' | ' + $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') + ' | ' + $f.Length) }
    else    { $l += ('  ' + $v + ' | MISSING') }
}

$f = Get-Item (Join-Path $dst 'bulud_ayarlari.json') -EA 0
if ($f) { $l += ('  ayar: ' + ((Get-Content $f.FullName -Raw) -replace '\s+', ' ')) }

$l += '=== DONE - you can start 021Cars now ==='

$l | Set-Content -Path $out -Encoding UTF8
Write-Host ('UPDATED - log: ' + $out)
Start-Sleep -Seconds 3
