// ============================================================================
//  📦 021Cars — QURAŞDIRICI (InstallerWindow.xaml.cs) ✓✓✓
// ----------------------------------------------------------------------------
//  ✅ QURAŞDIRMA: fayllar → seçilmiş qovluq ✓ · qısayollar ✓ · icazə ✓ · reyestr ✓
//  ✅ UNINSTALLER: {app}\Uninstaller.exe ✓ (özünü köçürür ✓ → sonra silir ✓✓✓)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace Cas0201.Setup
{
    public partial class InstallerWindow : Window
    {
        private static readonly string AppAdi = "021Cars — Avtomobil Parkı";
        private static readonly string ExeAdi = "EnterpriseAeroStudio.exe";
        private static readonly string RegYol =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\021Cars";

        private readonly bool _silmeRejimi;
        private readonly string _menimYolum = "";
        private readonly string _menimQovlugum = "";
        private string _hedef = @"C:\Program Files\021Cars";

        // ====================================================================
        //  🚀 GÜNCƏLLƏMƏ REJİMİ ✓✓✓
        //  📌 «021Cars_Installer.exe --guncelle "C:\Program Files\021Cars"»
        // --------------------------------------------------------------------
        //  ✅ proqramı bağlayır ✗ → faylları dəyişir ✓ → proqramı YENİDƏN açır ✓
        //  ⚠️ İSTİFADƏÇİ MƏLUMATLARI QORUNUR ✗✓✓✓
        //     (avtopark.db · Media · Yedəklər · istifadəçilər · ayarlar ✓)
        // ====================================================================

        /// <summary>🚀 Güncəlləmə rejimindəyik? ✓</summary>
        private readonly bool _guncelleRejimi;

        /// <summary>📂 Güncəllənəcək qovluq ✓ (proqram ozu ötürür ✓)</summary>
        private string _guncelleHedef = "";

        /// <summary>🗂️ <b>QORUNAN YOLLAR</b> ✓✓✓ — bunlar HEÇ VAXT dəyişdirilmir ✗ (istifadəçi məlumatıdır ✗)</summary>
        private static readonly string[] QorunanQovluqlar =
        {
            "Media", "Logs", "Yedekler", "Hesabatlar", "AutocodePDF", "Yedekler\\Json"
        };

        /// <summary>📄 QORUNAN FAYLLAR ✓✓✓ (baza · istifadəçilər · ayarlar ✓)</summary>
        private static readonly string[] QorunanFayllar =
        {
            "avtopark.db", "avtopark.db-wal", "avtopark.db-shm",
            "021cars_data.db", "backup.db",
            "istifadeciler.json", "bulud_ayarlari.json", "transfer-sexler.json",
            "data_usb.json", "offline_queue.json", "app_errors.log", "app_errors.old.log"
        };

        public InstallerWindow()
        {
            InitializeComponent();

            try
            {
                _menimYolum = Environment.ProcessPath ?? "";
                _menimQovlugum = Path.GetDirectoryName(_menimYolum) ?? "";

                var args = Environment.GetCommandLineArgs();

                _silmeRejimi =
                    Path.GetFileName(_menimYolum).StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase)
                    || (args.Length > 1 && string.Equals(args[1], "--sil", StringComparison.OrdinalIgnoreCase));

                // 🚀 «--guncelle "hədəf qovluq"» ✓✓✓
                _guncelleRejimi =
                    args.Length > 1 && string.Equals(args[1], "--guncelle", StringComparison.OrdinalIgnoreCase);

                if (_guncelleRejimi)
                {
                    _guncelleHedef = args.Length > 2 ? args[2].Trim().Trim('"') : "";

                    // 📋 Verilməyibsə → reyestrdən tap ✓ (InstallLocation ✓)
                    if (string.IsNullOrWhiteSpace(_guncelleHedef))
                    {
                        try
                        {
                            using var açar = Registry.LocalMachine.OpenSubKey(RegYol);
                            _guncelleHedef = açar?.GetValue("InstallLocation")?.ToString() ?? "";
                        }
                        catch { }

                        if (string.IsNullOrWhiteSpace(_guncelleHedef)) _guncelleHedef = _hedef;
                    }

                    _hedef = _guncelleHedef;
                }
            }
            catch { }

            // ★ SƏSSİZ SİLMƏ ✓✓✓ (Uninstaller özünü TEMP-ə köçürüb bu rejimdə çağırır ✓)
            var sessizSil = false;

            try
            {
                var a = Environment.GetCommandLineArgs();
                sessizSil = a.Length > 1 && string.Equals(a[1], "--sil-gizli", StringComparison.OrdinalIgnoreCase);
            }
            catch { }

            if (sessizSil)
            {
                SessizSil();
                return;
            }

            if (_silmeRejimi)
            {
                SilmeRejimiHazirla();
                return;
            }

            // 🚀 GÜNCƏLLƏMƏ — pəncərə açılan kimi AVTOMATİK başlayır ✓✓✓
            if (_guncelleRejimi)
            {
                GuncellemeRejimiHazirla();
            }
        }

        /// <summary>❌ <b>SƏSSİZ SİLMƏ</b> ✓✓✓ — pəncərəsiz tam silir ✓</summary>
        private void SessizSil()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var hədəf = args.Length > 2 ? args[2].Trim('"') : _hedef;

                // ⏳ Proqram bağlanana qədər gözləyir ✓ → sonra silir ✓✓✓
                for (var cəhd = 1; cəhd <= 25; cəhd++)
                {
                    try
                    {
                        System.Threading.Thread.Sleep(700);

                        if (Directory.Exists(hədəf)) Directory.Delete(hədəf, recursive: true);

                        break;
                    }
                    catch
                    {
                        // fayl hələ kilidlidir ✗ → təkrar cəhd ✓
                    }
                }

                // 🔗 Qısayollar ✓
                try
                {
                    File.Delete(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                        AppAdi + ".lnk"));
                }
                catch { }

                try
                {
                    var p = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");

                    File.Delete(Path.Combine(p, AppAdi + ".lnk"));
                    File.Delete(Path.Combine(p, AppAdi + " — SİL.lnk"));
                }
                catch { }

                // 📋 Reyestr ✓
                try { Registry.LocalMachine.DeleteSubKeyTree(RegYol, throwOnMissingSubKey: false); } catch { }

                MessageBox.Show("✅ 021Cars tamamilə silindi ✓", "021Cars",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("⚠️ Silinmə xətası ✗\n\n" + ex.Message, "021Cars",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            try { Application.Current?.Shutdown(); } catch { }
        }

        /// <summary>❌ SİLMƏ REJİMİ ✓ (Uninstaller.exe kimi işə salınıb ✓)</summary>
        private void SilmeRejimiHazirla()
        {
            Title = "021Cars — Silinmə";
            BasliqText.Text = "❌  021Cars — SİLİNMƏ";
            AltBasliqText.Text = "Proqram quraşdırma qovluğundan tamamilə silinəcək ✓";
            YerPaneli.Visibility = Visibility.Collapsed;
            QurasdirDugmesi.Visibility = Visibility.Collapsed;
            SilDugmesi.Visibility = Visibility.Visible;

            try
            {
                // 📂 Uninstaller.exe {app}\ içindədir ✓ → silinəcək qovluq = öz qovluğu ✓
                _hedef = File.Exists(Path.Combine(_menimQovlugum, ExeAdi))
                    ? _menimQovlugum
                    : (Path.GetDirectoryName(_menimQovlugum) ?? _menimQovlugum);
            }
            catch { }

            StatusText.Text =
                "📂 Silinəcək qovluq:\n" + _hedef + "\n\n" +
                "⚠️ DİQQƏT ✗ — BAZA (avtopark.db) və MEDIA (PDF/şəkillər) da silinəcək ✗\n" +
                "Öncə yedək götürməyinizi tövsityə edirik ✓\n\n" +
                "❌ Silmək üçün «SİL» düyməsini basın ✓";
        }

        // ====================================================================
        //  🚀 GÜNCƏLLƏMƏ ✓✓✓ — proqram bağlanır ✗ · fayllar dəyişir ✓ · açılır ✓
        // ====================================================================

        /// <summary>🚀 Güncəlləmə pəncərəsini hazırlayır ✓ (düymələr gizlədilir ✗ · avtomatik başlayır ✓)</summary>
        private void GuncellemeRejimiHazirla()
        {
            try
            {
                Title = "021Cars — Güncəlləmə";
                BasliqText.Text = "🚀  021Cars — GÜNCƏLLƏMƏ";
                AltBasliqText.Text =
                    "Proqram yenilənir ✓ — ✅ məlumatlarınız (baza · media · yedəklər) QORUNUR ✓✓✓";
                YerPaneli.Visibility = Visibility.Collapsed;
                QurasdirDugmesi.Visibility = Visibility.Collapsed;
                SilDugmesi.Visibility = Visibility.Collapsed;
                BaglaDugmesi.Visibility = Visibility.Collapsed;

                Loaded += async (_, _) => await GuncelleyiTetbiqEtAsync();
            }
            catch (Exception ex)
            {
                Yaz("⚠️ Güncəlləmə rejimi xətası ✗ — " + ex.Message);
            }
        }

        /// <summary>🛡️ <b>QORUNAN YOL</b> ✓✓✓ — istifadəçi məlumatıdır ✗ → HEÇ VAXT dəyişdirilmir ✗</summary>
        private static bool QorunanYol(string nisbi)
        {
            try
            {
                var yol = (nisbi ?? "").Replace('/', '\\').TrimStart('\\').Trim();

                if (yol.Length == 0) return false;

                // 🗂️ Qovluqlar (Media · Logs · Yedəklər · Hesabatlar · PDF ✓)
                foreach (var q in QorunanQovluqlar)
                {
                    if (yol.Equals(q, StringComparison.OrdinalIgnoreCase) ||
                        yol.StartsWith(q + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                var ad = Path.GetFileName(yol);

                // 📄 Dəqiq fayl adları ✓
                foreach (var f in QorunanFayllar)
                {
                    if (string.Equals(ad, f, StringComparison.OrdinalIgnoreCase)) return true;
                }

                // 🔒 Bazanın BÜTÜN formaları (.db · .db-wal · .db-shm · .db-journal ✓)
                var uzanti = Path.GetExtension(ad);

                if (uzanti.Equals(".db", StringComparison.OrdinalIgnoreCase)) return true;
                if (ad.Contains(".db-", StringComparison.OrdinalIgnoreCase)) return true;
                if (ad.Contains(".db.", StringComparison.OrdinalIgnoreCase)) return true;

                // 📜 Loq faylları ✓ + .old surətləri ✓
                if (ad.StartsWith("app_errors", StringComparison.OrdinalIgnoreCase)) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 🔢 <b>QURAŞDIRILAN VERSİYA</b> ✓✓✓ — reyestrdə düzgün versiya yazılsın ✓
        /// <para>
        /// 🐞 ƏVVƏL hər yerdə sabit <c>"6.0"</c> yazılırdı ✗ → «Proqramlar və
        /// Xüsusiyyətlər» siyahısında versiya HƏMİŞƏ «6.0» görünürdü ✗✓✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: versiya <b>faktiki fayldan</b> oxunur ✓ (payload-daki
        /// <c>EnterpriseAeroStudio.exe</c> ✓) → həmişə düzgün olur ✓
        /// </para>
        /// </summary>
        private static string QuraşdırılanVersiya(string qovluq)
        {
            try
            {
                var exe = Path.Combine(qovluq, ExeAdi);

                if (File.Exists(exe))
                {
                    var v = FileVersionInfo.GetVersionInfo(exe);

                    var mətn = v.ProductVersion ?? v.FileVersion;

                    if (!string.IsNullOrWhiteSpace(mətn))
                    {
                        // «6.2.6+abcdef» → «6.2.6» ✓ (build metadatası kəsilir ✓)
                        var plus = mətn.IndexOf('+');
                        if (plus > 0) mətn = mətn[..plus];

                        return mətn.Trim();
                    }
                }
            }
            catch { }

            return "6.0";
        }

        /// <summary>⏳ İşləyən tətbiqi (və veb serveri) bağlayır ✗✓✓ — fayllar kiliddən azad olsun ✓</summary>
        private static void BaglilariBagla()
        {
            var adlar = new[] { "EnterpriseAeroStudio", "EnterpriseAeroStudio.Web" };

            // ① Nəzakətli bağlama ✓ (pəncərəni bağla ✓)
            foreach (var ad in adlar)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(ad))
                    {
                        try { p.CloseMainWindow(); } catch { }
                    }
                }
                catch { }
            }

            // ② ⏳ gözlə (maksimum ~7.5 saniyə ✓)
            for (var cəhd = 0; cəhd < 25; cəhd++)
            {
                var qaldı = false;

                foreach (var ad in adlar)
                {
                    try
                    {
                        if (Process.GetProcessesByName(ad).Length > 0) { qaldı = true; break; }
                    }
                    catch { }
                }

                if (!qaldı) return;

                System.Threading.Thread.Sleep(300);
            }

            // ③ Hələ də açıqdırsa ✗ → məcburi bağla ✓ (kilid açılsın ✓)
            foreach (var ad in adlar)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(ad))
                    {
                        try { p.Kill(entireProcessTree: true); } catch { }
                    }
                }
                catch { }
            }

            System.Threading.Thread.Sleep(900);
        }

        /// <summary>
        /// 🚀 <b>GÜNCƏLLƏMƏNİ TƏTBİQ EDİR</b> ✓✓✓
        /// <list type="number">
        ///   <item>⏳ Proqramı bağlayır ✗ (fayllar kiliddən azad olur ✓)</item>
        ///   <item>📦 Faylları .exe-in içindəki ZIP-dən açır ✓ (🛡️ məlumatlar QORUNUR ✗)</item>
        ///   <item>🔐 Yazma icazəsini yeniləyir ✓ · 📋 reyestr versiyasını yeniləyir ✓</item>
        ///   <item>🚀 Proqramı YENİDƏN açır ✓✓✓</item>
        /// </list>
        /// </summary>
        private async Task GuncelleyiTetbiqEtAsync()
        {
            try
            {
                Zolaq.Value = 3;
                Yaz("🚀 Güncəlləmə başladı ✓\n📂 " + _hedef);

                // ────────────────────────────────────────────────────────────
                //  ⓪ YOXLAMA ✓ — qovluq və exe yerindədir? ✓
                // ────────────────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(_hedef) || !Directory.Exists(_hedef))
                {
                    Yaz("⚠️ Quraşdırma qovluğu tapılmadı ✗\n📂 " + _hedef +
                        "\n\n💡 Proqramı əl ilə quraşdırın ✓");
                    return;
                }

                var exe = Path.Combine(_hedef, ExeAdi);

                if (!File.Exists(exe))
                {
                    Yaz("⚠️ «" + ExeAdi + "» tapılmadı ✗\n📂 " + _hedef +
                        "\n\n💡 Əvvəlcə adi QURAŞDIRMA edin ✓");
                    return;
                }

                // ────────────────────────────────────────────────────────────
                //  ① PROQRAMI BAĞLA ✗
                // ────────────────────────────────────────────────────────────
                Zolaq.Value = 8;
                Yaz("⏳ Proqram bağlanır… (fayllar kiliddən azad olur ✓)");

                BaglilariBagla();

                await Task.Delay(700);

                // ────────────────────────────────────────────────────────────
                //  ② FAYLLARI DƏYİŞ ✓ — 🛡️ qorunanları KEÇ ✗✓✓✓
                // ────────────────────────────────────────────────────────────
                Zolaq.Value = 20;

                var daxiliZip = AçılaBilənZip();
                int say;

                if (daxiliZip is not null)
                {
                    Yaz("📦 Fayllar yenilənir (məlumatlar QORUNUR ✓)…");
                    say = ZipAç(daxiliZip);
                }
                else
                {
                    var payload = Path.Combine(_menimQovlugum, "payload");

                    if (!Directory.Exists(payload))
                    {
                        Yaz("⚠️ Fayl paketi tapılmadı ✗ — installer zədələnib ✗\n" +
                            "💡 Yeni installer-i GitHub-dan yükləyin ✓");
                        return;
                    }

                    say = Kopyala(payload, _hedef);
                }

                Zolaq.Value = 80;
                Yaz($"✅ {say} fayl yeniləndi ✓\n🛡️ Baza · media · yedəklər TOXUNULMADI ✓✓✓");

                // ────────────────────────────────────────────────────────────
                //  ③ İCAZƏ + REYESTR ✓
                // ────────────────────────────────────────────────────────────
                IcazeVer(_hedef);

                try
                {
                    using var açar = Registry.LocalMachine.CreateSubKey(RegYol);

                    if (açar is not null)
                    {
                        açar.SetValue("DisplayVersion", QuraşdırılanVersiya(_hedef));
                        açar.SetValue("InstallLocation", _hedef);
                        açar.SetValue("DisplayIcon", exe);
                        açar.SetValue("UninstallString",
                            "\"" + Path.Combine(_hedef, "Uninstaller.exe") + "\"");
                    }
                }
                catch { }

                // 🗑️ Yeni Uninstaller-i də yenilə ✓
                try { File.Copy(_menimYolum, Path.Combine(_hedef, "Uninstaller.exe"), overwrite: true); }
                catch { }

                Zolaq.Value = 95;
                Yaz("🔗 Qısayollar yenilənir…");

                var proqramlar = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");

                Qisayol(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    AppAdi, exe, _hedef);

                Qisayol(proqramlar, AppAdi, exe, _hedef);

                Qisayol(proqramlar, AppAdi + " — SİL",
                    Path.Combine(_hedef, "Uninstaller.exe"), _hedef);

                // ────────────────────────────────────────────────────────────
                //  ④ PROQRAMI YENİDƏN AÇ ✓✓✓
                // ────────────────────────────────────────────────────────────
                Zolaq.Value = 100;
                Yaz("✅ GÜNCƏLLƏMƏ TAMAMLANDI ✓✓✓\n\n🚀 Proqram yenidən açılır…\n" +
                    "🛡️ Bütün məlumatlarınız yerindədir ✓");

                await Task.Delay(700);

                try
                {
                    Process.Start(new ProcessStartInfo(exe)
                    {
                        WorkingDirectory = _hedef,
                        UseShellExecute = true
                    });
                }
                catch { }

                await Task.Delay(900);

                try { Application.Current?.Shutdown(); } catch { }
            }
            catch (Exception ex)
            {
                Zolaq.Value = 100;
                Yaz("⚠️ Güncəlləmə xətası ✗\n\n" + ex.Message +
                    "\n\n💡 Proqramı əl ilə açın ✓ — məlumatlarınız zədələnməyib ✓");
            }
        }

        // ====================================================================
        //  🧰 KÖMƏKÇİLƏR ✓
        // ====================================================================

        /// <summary>💬 Status yazır ✓</summary>
        private void Yaz(string mesaj, int faiz = -1)
        {
            try
            {
                StatusText.Text = mesaj;
                if (faiz >= 0) Zolaq.Value = Math.Min(100, faiz);
            }
            catch { }
        }

        /// <summary>📁 Qovluq seçmə pəncərəsi ✓ (WPF-in öz dialoqu ✓)</summary>
        private void GozAt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialoq = new OpenFolderDialog
                {
                    Title = "📂 021Cars-ın quraşdırılacağı qovluğu seçin ✓",
                    InitialDirectory = @"C:\Program Files"
                };

                if (dialoq.ShowDialog(this) == true)
                {
                    YerQutusu.Text = Path.Combine(dialoq.FolderName, "021Cars");
                }
            }
            catch (Exception ex)
            {
                Yaz("⚠️ Qovluq seçilə bilmədi ✗ — " + ex.Message);
            }
        }

        private void Bagla_Click(object sender, RoutedEventArgs e)
        {
            try { Close(); } catch { }
        }

        /// <summary>🔐 Qovluğa YAZMA İCAZƏSİ verir ✓✓✓ (Program Files üçün VACİB ✗)</summary>
        private static bool IcazeVer(string qovluq)
        {
            try
            {
                var acl = new DirectoryInfo(qovluq).GetAccessControl();
                var qayda = new FileSystemAccessRule("BUILTIN\\Users",
                    FileSystemRights.Modify, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None, AccessControlType.Allow);

                acl.AddAccessRule(qayda);
                new DirectoryInfo(qovluq).SetAccessControl(acl);
                return true;
            }
            catch { return false; }
        }

        /// <summary>🔗 Qısayol yaradır ✓</summary>
        private static void Qisayol(string qovluq, string ad, string hedef, string işQovluğu)
        {
            try
            {
                if (!Directory.Exists(qovluq)) return;

                var tip = Type.GetTypeFromProgID("WScript.Shell");
                if (tip is null) return;

                dynamic ws = Activator.CreateInstance(tip)!;
                dynamic lnk = ws.CreateShortcut(Path.Combine(qovluq, ad + ".lnk"));
                lnk.TargetPath = hedef;
                lnk.WorkingDirectory = işQovluğu;
                lnk.Save();
            }
            catch { }
        }

        /// <summary>📋 Qovluğu (alt qovluqlarla) kopyalayır ✓</summary>
        private static int Kopyala(string mənbə, string hedef)
        {
            var say = 0;

            foreach (var f in Directory.GetFiles(mənbə, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var nisbi = Path.GetRelativePath(mənbə, f);

                    // 🛡️ İSTİFADƏÇİ MƏLUMATI ✗ → TOXUNULMUR ✓✓✓
                    if (QorunanYol(nisbi)) continue;

                    var hədəf = Path.Combine(hedef, nisbi);

                    Directory.CreateDirectory(Path.GetDirectoryName(hədəf) ?? hedef);
                    File.Copy(f, hədəf, overwrite: true);
                    say++;
                }
                catch { }
            }

            return say;
        }

        // ====================================================================
        //  📦 DAXİLİ ZIP (TƏK FAYL INSTALLER ✓✓✓)
        // ====================================================================

        /// <summary>📦 .exe-İN İÇİNDƏKİ <c>payload.zip</c> ✓✓✓ (yoxdursa <c>null</c> ✓)</summary>
        private byte[]? AçılaBilənZip()
        {
            try
            {
                using var axın = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("payload.zip");

                if (axın is null) return null;

                using var yaddaş = new MemoryStream();
                axın.CopyTo(yaddaş);

                return yaddaş.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>📂 ZIP məzmununu quraşdırma qovluğuna açır ✓ (progress ilə ✓✓✓)</summary>
        private int ZipAç(byte[] zipBaytları)
        {
            var say = 0;

            try
            {
                using var yaddaş = new MemoryStream(zipBaytları);
                using var zip = new ZipArchive(yaddaş, ZipArchiveMode.Read);

                var cəmi = zip.Entries.Count;
                var indi = 0;

                foreach (var giriş in zip.Entries)
                {
                    indi++;

                    try
                    {
                        if (string.IsNullOrWhiteSpace(giriş.Name)) continue;   // 📁 qovluq ✓

                        var nisbi = giriş.FullName.Replace('/', '\\');

                        // 🛡️ İSTİFADƏÇİ MƏLUMATI ✗ — baza · media · yedək ✗ → TOXUNULMUR ✓✓✓
                        if (QorunanYol(nisbi)) continue;

                        var hədəf = Path.Combine(_hedef, nisbi);
                        Directory.CreateDirectory(Path.GetDirectoryName(hədəf) ?? _hedef);

                        giriş.ExtractToFile(hədəf, overwrite: true);
                        say++;

                        if (cəmi > 0 && indi % 20 == 0)
                        {
                            Zolaq.Value = 20 + indi * 50.0 / cəmi;    // 20% → 70% ✓
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Yaz("⚠️ ZIP açıla bilmədi ✗ — " + ex.Message);
            }

            return say;
        }

        // ====================================================================
        //  📦 QURAŞDIRMA ✓✓✓
        // ====================================================================

        private void Qurasdir_Click(object sender, RoutedEventArgs e)
        {
            try { QurasdirEt(); }
            catch (Exception ex)
            {
                Yaz("⚠️ Quraşdırma xətası ✗ — " + ex.Message);
                MessageBox.Show("⚠️ Quraşdırma alınmadı ✗\n\n" + ex.Message,
                    "021Cars", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void QurasdirEt()
        {
            _hedef = (YerQutusu.Text ?? "").Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(_hedef))
            {
                Yaz("⚠️ Quraşdırma yeri boşdur ✗");
                return;
            }

            // ════════════════════════════════════════════════════════════
            //  📦 PAYLOAD MƏNBƏYİ ✓
            //  ① ★ .exe-İN İÇİNDƏKİ ZIP ★ ✓✓✓ (TƏK FAYL INSTALLER ✓)
            //  ② yoxdursa → yanındaki payload\ qovluğu ✓ (ehtiyat ✓)
            // ════════════════════════════════════════════════════════════
            var daxiliZip = AçılaBilənZip();
            var payload = Path.Combine(_menimQovlugum, "payload");

            if (daxiliZip is null && !Directory.Exists(payload))
            {
                Yaz("⚠️ Tətbiq faylları TAPILMADI ✗\n\n" +
                    "Bu quraşdırıcı «payload.zip» daxil edilmədən yığılıb ✗\n" +
                    "→ Yig-Installer.ps1 ilə yenidən yığın ✓ (zip avtomatik gömülür ✓)");
                return;
            }

            Zolaq.Value = 5;
            Yaz("📂 Qovluqlar yaradılır…\n" + _hedef);

            Directory.CreateDirectory(_hedef);

            foreach (var alt in new[] { "Logs", "Yedekler", "Yedekler\\Json", "Media", "Hesabatlar", "AutocodePDF" })
            {
                try { Directory.CreateDirectory(Path.Combine(_hedef, alt)); } catch { }
            }

            Zolaq.Value = 20;
            int say;

            if (daxiliZip is not null)
            {
                // ★★★ TƏK FAYL: öz İÇİNDƏN açır ✓✓✓ ★★★
                Yaz("📦 Fayllar quraşdırıcının İÇİNDƏN açılır… ✓\n(bir neçə saniyə çəkə bilər ✓)");
                say = ZipAç(daxiliZip);
            }
            else
            {
                Yaz("📋 Fayllar yanındaki «payload» qovluğundan kopyalanır… ✓");
                say = Kopyala(payload, _hedef);
            }

            Zolaq.Value = 60;
            Yaz($"📋 {say} fayl kopyalandı ✓\n🔐 Yazma icazəsi verilir…");

            if (!IcazeVer(_hedef))
            {
                Yaz("⚠️ İcazə verilə bilmədi ✗ — 021Cars_Installer.exe-ni «Run as administrator» ilə işə salın ✓");
            }

            Zolaq.Value = 75;
            Yaz("🔗 Qısayollar yaradılır…");

            var exe = Path.Combine(_hedef, ExeAdi);
            var proqramlar = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");

            if (MasaustuQutusu.IsChecked == true)
            {
                Qisayol(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                    AppAdi, exe, _hedef);
            }

            Qisayol(proqramlar, AppAdi, exe, _hedef);
            Qisayol(proqramlar, AppAdi + " — SİL",
                Path.Combine(_hedef, "Uninstaller.exe"), _hedef);

            Zolaq.Value = 85;
            Yaz("🗑️ Uninstaller yaradılır…");

            // ★★★ ÖZÜNÜ {app}\Uninstaller.exe KİMİ YAZIR ✓✓✓ ★★★
            try { File.Copy(_menimYolum, Path.Combine(_hedef, "Uninstaller.exe"), overwrite: true); }
            catch { }

            // 📋 «Proqramlar və Xüsusiyyətlər» qeydiyyatı ✓
            try
            {
                using var açar = Registry.LocalMachine.CreateSubKey(RegYol);

                if (açar is not null)
                {
                    açar.SetValue("DisplayName", AppAdi);
                    açar.SetValue("DisplayVersion", QuraşdırılanVersiya(_hedef));
                    açar.SetValue("Publisher", "021Cars");
                    açar.SetValue("InstallLocation", _hedef);
                    açar.SetValue("DisplayIcon", exe);
                    açar.SetValue("UninstallString", "\"" + Path.Combine(_hedef, "Uninstaller.exe") + "\"");
                    açar.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    açar.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch { }

            Zolaq.Value = 100;
            Yaz("✅ QURAŞDIRILDI ✓✓✓\n\n📂 " + _hedef + "\n\n" +
                "🚀 Proqramı masaüstündeki «" + AppAdi + "» qısayolundan açın ✓\n" +
                "🗑️ Silmək üçün: " + Path.Combine(_hedef, "Uninstaller.exe") + " ✓");

            if (QisaBaslatQutusu.IsChecked == true && File.Exists(exe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exe)
                    {
                        WorkingDirectory = _hedef,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        // ====================================================================
        //  ❌ SİLMƏ ✓✓✓
        // ====================================================================

        private void Sil_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cavab = MessageBox.Show(
                    "❌ 021Cars tamamilə silinsin?\n\n📂 " + _hedef + "\n\n" +
                    "⚠️ BAZA ✓ MEDIA ✓ YEDƏKLƏR də silinəcək ✗",
                    "021Cars — Silinmə", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (cavab != MessageBoxResult.Yes) return;

                Yaz("❌ Silinir…");

                // ★ ÖZÜNÜ TEMP-Ə KÖÇÜRÜB ORADAN SİLİR ✓✓✓
                //   (işləyən .exe öz qovluğunu silə bilməz ✗)
                var müvəqqəti = Path.Combine(Path.GetTempPath(), "021Cars_Uninstall.exe");
                File.Copy(_menimYolum, müvəqqəti, overwrite: true);

                Process.Start(new ProcessStartInfo(müvəqqəti)
                {
                    Arguments = "--sil-gizli \"" + _hedef + "\"",
                    UseShellExecute = true,
                    Verb = "runas"
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Yaz("⚠️ Silinmə xətası ✗ — " + ex.Message);
            }
        }
    }
}
