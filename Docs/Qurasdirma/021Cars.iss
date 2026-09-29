; ============================================================================
;  ☁️ 021Cars — QURAŞDIRICI (INSTALLER) — Inno Setup 6 ✓✓✓
; ----------------------------------------------------------------------------
;  ✅ Quraşdırma yerini İSTİFADƏÇİ SEÇİR ✓ (default: C:\Program Files\021Cars ✓)
;  ✅ Uninstaller AVTOMATİK yaranır ✓ ("Proqramlar və Xüsusiyyətlər"-də görünür ✓)
;  ✅ Bütün fayllar (baza · log · media · yedək) QURAŞDIRMA QOVLUĞUNDA ✓
;     → AppData / MyDocuments — HEÇ NƏ YAZILMIR ✗✓✓
;  ✅ Program Files-də yazma icazəsi verilir ✓ (Permissions: users-modify ✓)
; ----------------------------------------------------------------------------
;  NECƏ İSTİFADƏ ETMƏLİ:
;    ① Inno Setup 6 yükləyin (pulsuz): https://jrsoftware.org/isdl.php
;    ② Əvvəlcə publish edin:  powershell -File Docs\Qurasdirma\qur.ps1
;    ③ Inno Setup-da bu faylı açın → «Compile» → 021Cars-Setup.exe yaranır ✓
; ============================================================================

#define AppAd     "021Cars"
#define AppTamAd  "021Cars — Avtomobil Parkı"
#define AppVersiya "6.0"
#define AppNəşrçi "021Cars"
#define ExeAdı    "EnterpriseAeroStudio.exe"
#define PublishQovluq "..\..\publish"

[Setup]
AppId={{7C1A4E20-021C-4A55-9C11-021CARS00001}
AppName={#AppTamAd}
AppVersion={#AppVersiya}
AppPublisher={#AppNəşrçi}
DefaultDirName=C:\Program Files\{#AppAd}
DefaultGroupName={#AppTamAd}
DisableDirPage=no
DisableProgramGroupPage=no
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\..\publish\_setup
OutputBaseFilename=021Cars-Setup-{#AppVersiya}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#AppTamAd}
UninstallDisplayIcon={app}\{#ExeAdı}
AllowNoIcons=yes
SetupLogging=yes

[Languages]
Name: "azerbaijani"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Masaüstündə qısayol yarat"; GroupDescription: "Əlavə tapşırıqlar:"; Flags: checkedonce

[Files]
Source: "{#PublishQovluq}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; ★★★ QURAŞDIRMA QOVLUĞUNA YAZMA İCAZƏSİ ★★★
; (Program Files adi istifadəçiyə yazıla bilmir ✗ → bu sətirlər həll edir ✓✓✓)
Name: "{app}";                    Permissions: users-modify
Name: "{app}\Logs";               Permissions: users-modify
Name: "{app}\Yedekler";           Permissions: users-modify
Name: "{app}\Yedekler\Json";      Permissions: users-modify
Name: "{app}\Media";              Permissions: users-modify
Name: "{app}\Hesabatlar";         Permissions: users-modify
Name: "{app}\AutocodePDF";        Permissions: users-modify

[Icons]
Name: "{group}\{#AppTamAd}";                  Filename: "{app}\{#ExeAdı}"
Name: "{group}\📜 Loq fayllarını aç";          Filename: "{app}\Logs"
Name: "{group}\❌ {#AppTamAd} sil (uninstall)"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppTamAd}";           Filename: "{app}\{#ExeAdı}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeAdı}"; Description: "021Cars-ı indi işə sal"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; ⚠ YALNIZ loqlar silinir ✓ — BAZA ✓ MEDIA ✓ YEDƏKLƏR ✓ QORUNUR ✓✓✓ (təsadüfi data itkisi olmasın ✗)
Type: filesandordirs; Name: "{app}\Logs"

[Code]
// 🛡️ Silinməzdən əvvəl xəbərdarlıq ✓ — data qovluqları ƏL İLƏ silinməlidir ✓
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBox('✅ 021Cars silindi.' + #13#10 + #13#10 +
           '📊 MƏLUMATLARINIZ QORUNUR ✓:' + #13#10 +
           '   • Baza (avtopark.db)' + #13#10 +
           '   • Media / Sənədlər' + #13#10 +
           '   • Yedəklər' + #13#10 + #13#10 +
           'Bunları da silmək istəyirsinizsə, quraşdırma qovluğunu əl ilə silin:' + #13#10 +
           ExpandConstant('{app}'),
           mbInformation, MB_OK);
  end;
end;
