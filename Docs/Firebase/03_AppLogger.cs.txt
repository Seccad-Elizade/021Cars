// ============================================================================
//  📜 021Cars — FAIL-SAFE LOQ SİSTEMİ (3/6)
//  ✅ Xəta HEÇ VAXT proqramı çökdürmür ✗ — səssizcə tutulur ✓
//  ✅ app_errors.log  (5 MB-dan sonra → app_errors.old.log ✓)
//  ✅ Son 200 mesaj yaddaşda saxlanılır ✓ (UI-də göstərmək üçün ✓)
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Cas0201.Firebase
{
    /// <summary>
    /// 🛡️ <b>QÜSURSUZ XƏTA TUTUCU</b> ✓✓✓
    /// <para>
    /// Prinsip: <b>heç bir loq əməliyyatı ÖZÜ xəta verə bilməz</b> ✗✓✓
    /// Buna görə <b>hər metod daxildən try-catch ilə sarılıb</b> ✓.
    /// </para>
    /// </summary>
    public static class AppLogger
    {
        private static readonly object Qapi = new();
        private static readonly ConcurrentQueue<string> Yaddaş = new();

        /// <summary>📏 Maksimum fayl ölçüsü (5 MB ✓)</summary>
        public const long MaksBayt = 5 * 1024 * 1024;

        /// <summary>💾 Yaddaşda saxlanılan son mesaj sayı ✓</summary>
        public const int YaddaşLimiti = 200;

        /// <summary>📄 Loq faylının adı ✓ (DƏYİŞMƏZ ✗)</summary>
        public const string FaylAdi = "app_errors.log";

        /// <summary>
        /// 📂 <b>LOQ QOVLUĞU</b> ✓✓✓ — <c>%LOCALAPPDATA%\EnterpriseAeroStudio\Logs</c> ✓
        /// <para>
        /// ① <c>C:\Users\&lt;İstifadəçi&gt;\AppData\Local\EnterpriseAeroStudio\Logs</c> ✓ (layihə standartı ✓)<br/>
        /// ② olmazsa → <c>{exe qovluğu}\Logs</c> ✓ (fallback ✓)<br/>
        /// ③ o da olmazsa → <c>%TEMP%\021Cars\Logs</c> ✓ (son çarə ✓)
        /// </para>
        /// ⚠ Program Files-a quraşdırıldıqda orada yazmaq olmur ✗ → avtomatik keçir ✓✓✓
        /// </summary>
        public static string LogQovlugu { get; } = LogQovlugunuTap();

        /// <summary>
        /// 📜 <b>LOQ FAYLININ TAM YOLU</b> ✓✓✓
        /// <example><c>C:\Users\021Cars_User\AppData\Local\EnterpriseAeroStudio\Logs\app_errors.log</c> ✓</example>
        /// </summary>
        public static string FaylYolu { get; set; } = Path.Combine(LogQovlugu, FaylAdi);

        /// <summary>🌐 İnternet vəziyyəti ✓ (UI indikatoru üçün ✓)</summary>
        public static bool Onlayn { get; set; }

        /// <summary>🕒 Son uğurlu sinxronizasiya vaxtı ✓</summary>
        public static DateTime? SonSinxron { get; set; }

        // --------------------------------------------------------------------
        //  ✍️ YAZMA — heç biri exception ata BİLMƏZ ✗✓✓
        // --------------------------------------------------------------------

        /// <summary>❌ Xətanı yazır ✓ (app_errors.log ✓)</summary>
        public static void Xeta(Exception? ex, string kontekst = "")
        {
            try
            {
                var m = new StringBuilder();
                m.AppendLine("═══════════════════════════════════════════════════");
                m.AppendLine($"❌ XƏTA     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                m.AppendLine($"📌 KONTEKST : {kontekst}");
                m.AppendLine($"💥 TİP      : {ex?.GetType().FullName ?? "naməlum"}");
                m.AppendLine($"📝 MESAJ    : {ex?.Message}");
                m.AppendLine($"🧵 İZ       : {Kes(ex?.StackTrace, 900)}");
                m.AppendLine($"🔗 DAXİLİ   : {Kes(ex?.InnerException?.Message, 300)}");
                m.AppendLine($"🖥️ CİHAZ    : {Environment.MachineName} · 🌐 {Onlayn}");

                YaddaşaƏlavə("❌ " + kontekst + " → " + ex?.Message);
                Yaz(m.ToString(), "❌");
            }
            catch
            {
                // 🛡️ Loq yazmaq mümkün olmadı ✗ → SƏSSİZ keçir ✓
            }
        }

        /// <summary>ℹ️ Məlumat yazır ✓</summary>
        public static void Melumat(string mesaj)
        {
            try
            {
                YaddaşaƏlavə("ℹ️ " + mesaj);
                Yaz($"ℹ️ {DateTime.Now:yyyy-MM-dd HH:mm:ss}  {mesaj}", "ℹ️");
            }
            catch { }
        }

        /// <summary>⚠️ Xəbərdarlıq yazır ✓</summary>
        public static void Xeberdarliq(string mesaj)
        {
            try
            {
                YaddaşaƏlavə("⚠️ " + mesaj);
                Yaz($"⚠️ {DateTime.Now:yyyy-MM-dd HH:mm:ss}  {mesaj}", "⚠️");
            }
            catch { }
        }

        /// <summary>🔌 Şəbəkə xətası ✓ (internet qopdu ✓ — GÖZLƏNİLƏN hal ✓)</summary>
        public static void Sebeke(Exception ex, string kontekst = "şəbəkə")
        {
            try
            {
                Onlayn = false;
                YaddaşaƏlavə($"🔌 OFFLINE: {kontekst} → {ex.Message}");
                Yaz($"🔌 {DateTime.Now:yyyy-MM-dd HH:mm:ss} OFFLINE {kontekst} → {ex.Message}", "🔌");
            }
            catch { }
        }

        // --------------------------------------------------------------------
        //  📖 OXUMA — UI üçün ✓
        // --------------------------------------------------------------------

        /// <summary>📋 Son mesajları qaytarır ✓ (ən yeni əvvəldə ✓)</summary>
        public static IReadOnlyList<string> SonMesajlar() =>
            Yaddaş.Reverse().ToList();

        /// <summary>🧹 Bütün loqları təmizləyir ✓</summary>
        public static void Temizle()
        {
            try
            {
                while (Yaddaş.TryDequeue(out _)) { }

                if (File.Exists(FaylYolu))
                {
                    File.Delete(FaylYolu);
                }
            }
            catch { }
        }

        // --------------------------------------------------------------------
        //  🔧 DAXİLİ KÖMƏKÇİLƏR ✓
        // --------------------------------------------------------------------

        /// <summary>📂 Loq qovluğunu tapır ✓ (yazma icazəsini REAL test edir ✓)</summary>
        private static string LogQovlugunuTap()
        {
            var namizədlər = new[]
            {
                // ① ★ QURAŞDIRMA QOVLUĞU ★ ✓✓✓ (installer-də seçilən yer ✓ —
                //    default: C:\Program Files\021Cars ✓ · AppData İSTİFADƏ OLUNMUR ✗)
                Path.Combine(AppContext.BaseDirectory, "Logs"),

                // ② exe-nin valideyn qovluğu ✓ (exe alt qovluqda olarsa ✓)
                Path.Combine(
                    Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd('\\'))
                        ?? AppContext.BaseDirectory,
                    "Logs"),

                // ③ son çarə — TEMP ✓ (AppData DEYİL ✗)
                Path.Combine(Path.GetTempPath(), "021Cars", "Logs")
            };

            foreach (var namizəd in namizədlər)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(namizəd)) continue;

                    Directory.CreateDirectory(namizəd);

                    // 🧪 Yazma testi ✓ (Program Files → xəta verir ✗ → növbəti namizəd ✓)
                    var test = Path.Combine(namizəd, ".yazma_testi.tmp");
                    File.WriteAllText(test, "ok");
                    File.Delete(test);

                    return namizəd; // ✅ bura yazmaq mümkündür ✓
                }
                catch { }
            }

            return Path.Combine(AppContext.BaseDirectory, "Logs");
        }

        /// <summary>📂 Loq qovluğunu yaradır ✓ (mövcuddursa heç nə etmir ✓)</summary>
        public static bool QovluqTeminEt()
        {
            try
            {
                Directory.CreateDirectory(LogQovlugu);
                return true;
            }
            catch { return false; }
        }

        /// <summary>🖥️ Loq qovluğunu <b>Explorer-də açır</b> ✓ (UI «📜 Loqnu aç» düyməsi ✓)</summary>
        public static void LoqlariAc()
        {
            try
            {
                QovluqTeminEt();

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{LogQovlugu}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        /// <summary>📄 Loq faylı mövcuddur? ✓</summary>
        public static bool LoqVar => File.Exists(FaylYolu);

        /// <summary>📏 Loq faylının ölçüsü (KB ✓)</summary>
        public static double OlcuKB
        {
            get
            {
                try { return File.Exists(FaylYolu) ? new FileInfo(FaylYolu).Length / 1024.0 : 0; }
                catch { return 0; }
            }
        }

        /// <summary>📜 Loqun sonuncu sətirlərini oxuyur ✓ (UI pəncərəsi üçün ✓)</summary>
        public static string SonSetirler(int say = 200)
        {
            try
            {
                if (!File.Exists(FaylYolu)) return "";

                var sətirlər = File.ReadAllLines(FaylYolu);
                return string.Join(Environment.NewLine,
                    sətirlər.Skip(Math.Max(0, sətirlər.Length - say)));
            }
            catch { return ""; }
        }

        /// <summary>🧹 Köhnə loqu təmizləyir ✓ (fayl silinir ✓ · veni yaranır ✓)</summary>
        public static void LoquTemizle()
        {
            try
            {
                while (Yaddaş.TryDequeue(out _)) { }

                if (File.Exists(FaylYolu)) File.Delete(FaylYolu);

                Melumat($"📜 Loq təmizləndi ✓ — {FaylYolu}");
            }
            catch { }
        }

        private static void YaddaşaƏlavə(string s)
        {
            try
            {
                Yaddaş.Enqueue($"{DateTime.Now:HH:mm:ss}  {s}");

                while (Yaddaş.Count > YaddaşLimiti)
                {
                    Yaddaş.TryDequeue(out _);
                }
            }
            catch { }
        }

        private static string? Kes(string? s, int maks) =>
            s is null ? null : (s.Length <= maks ? s : s[..maks] + "…");

        private static void Yaz(string mətn, string ikon)
        {
            try
            {
                lock (Qapi)
                {
                    var qovluq = Path.GetDirectoryName(FaylYolu);

                    if (!string.IsNullOrWhiteSpace(qovluq) && !Directory.Exists(qovluq))
                    {
                        Directory.CreateDirectory(qovluq!);
                    }

                    // ♻️ ROTASİYA — fayl böyüyübsə .old-a köçür ✓
                    if (File.Exists(FaylYolu) && new FileInfo(FaylYolu).Length > MaksBayt)
                    {
                        var kohne = Path.Combine(
                            Path.GetDirectoryName(FaylYolu)!,
                            Path.GetFileNameWithoutExtension(FaylYolu) + ".old" +
                            Path.GetExtension(FaylYolu));

                        File.Delete(kohne);
                        File.Move(FaylYolu, kohne);
                    }

                    File.AppendAllText(FaylYolu, mətn + Environment.NewLine, Encoding.UTF8);

                    // 💻 Konsola da yaz ✓ (debug üçün ✓)
                    var ilkSətr = mətn.Split('\n')[0];
                    Console.WriteLine($"{ikon} [021Cars] {ilkSətr}");
                }
            }
            catch
            {
                // 🛡️ Disk dolu / icazə yoxdur ✗ → səssiz keçir ✓
            }
        }

    }
}
