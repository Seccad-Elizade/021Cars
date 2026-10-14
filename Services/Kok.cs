// ============================================================================
//  🏠 021Cars — KÖK QOVLUQ (13)  ★ BÜTÜN FAYLLARIN YERİ ★
// ----------------------------------------------------------------------------
//  ✅ BÜTÜN fayllar (baza · log · media · yedək · ayar · istifadəçilər)
//     ★ QURAŞDIRMA QOVLUĞUNDA ★ olur ✓✓✓
//  ✅ Default:  C:\Program Files\021Cars\   ✓ (installer-də seçilir ✓)
//  ❌ AppData / MyDocuments / səpələnmiş fayllar — HEÇ BİRİ YOXDUR ✗✓✓
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using EnterpriseAeroStudio.Services;

namespace Cas0201
{
    /// <summary>
    /// 🏠 <b>KÖK QOVLUQ</b> ✓✓✓ — tətbiqin BÜTÜN faylları burada saxlanılır ✓
    /// <para>
    /// <b>Necə tapılır?</b>
    /// ① <c>EnterpriseAeroStudio.exe</c> yanındaki qovluq ✓ (masaüstü ✓)<br/>
    /// ② exe alt qovluqdadırsa (Veb server ✓) → yuxarıda masaüstü exe axtarılır ✓<br/>
    /// ③ heç biri olmazsa → exe qovluğu ✓
    /// </para>
    /// <para>⚠ Yazma icazəsi yoxdursa ✗ (Program Files ✓) → installer <c>Modify</c> icazəsi verir ✓✓✓</para>
    /// </summary>
    public static class Kok
    {
        private static string? _qovluq;

        /// <summary>🏠 Tətbiqin kök (data) qovluğu ✓</summary>
        public static string Qovluq
        {
            get
            {
                if (_qovluq is not null) return _qovluq;

                try
                {
                    var exe = AppContext.BaseDirectory.TrimEnd('\\', '/');

                    // ② yuxarı qovluqda masaüstü exe varsa → ORASI kökdür ✓ (Veb alt qovluqdadır ✓)
                    var üst = Path.GetDirectoryName(exe);

                    if (!string.IsNullOrWhiteSpace(üst) &&
                        File.Exists(Path.Combine(üst, "EnterpriseAeroStudio.exe")))
                    {
                        _qovluq = üst;
                        return _qovluq;
                    }

                    // ① exe qovluğu ✓
                    _qovluq = exe;
                }
                catch
                {
                    _qovluq = AppContext.BaseDirectory;
                }

                return _qovluq!;
            }
        }

        /// <summary>📁 DATA alt qovluğu ✓ — <c>{kök}\EnterpriseAeroStudio</c> ✓ (baza · USB qeydiyyatı ✓)</summary>
        public static string DataQovlugu =>
            Path.Combine(Qovluq, "EnterpriseAeroStudio");

        /// <summary>
        /// 💾 <b>YERLİ BAZANIN TAM YOLU</b> ✓✓✓ — <c>{kök}\EnterpriseAeroStudio\avtopark.db</c> ✓
        /// <para>
        /// ⚠️ <b>VAHİD MƏNBƏ</b> ✗✓✓ — həm tətbiqin özü (<c>App.xaml.cs</c> ✓), həm də
        /// USB bərpa/birləşdirmə (<c>DataUsbService</c> ✓) EYNİ yolu işlətməlidir ✓.
        /// Əks halda USB-dən gələn məlumat <b>GÖRÜNMƏZ</b> qalır ✗✓✓
        /// (bərpa bir fayla yazır ✗, proqram başqa faylı açır ✗ ✓¡).
        /// </para>
        /// </summary>
        public static string BazaYolu
        {
            get
            {
                // 🛡️ v6.2.42 — ⚠ test BİR DƏFƏ işləməlidir ✗✓✓ (hər çağırışda YOX ✗)
                //   • `lock` → çoxsaylı thread eyni anda çağırsa da TƏK karar ✓
                if (_bazaYolu is not null)
                {
                    return _bazaYolu;
                }

                lock (BazaQapisi)
                {
                    return _bazaYolu ??= BazaYolunuTap();
                }
            }
        }

        /// <summary>🔒 Baza yolu qərarı üçün kilid ✓ (thread-safe ✓)</summary>
        private static readonly object BazaQapisi = new();

        // ====================================================================
        //  💾 v6.2.38 — ÖZÜNÜ BƏRPA EDƏN BAZA YOLU ✓✓✓
        // --------------------------------------------------------------------
        //  ★ İstifadəçi xətası: «Kritik xəta — SQLite Error 14: unable to
        //    open database file» ✗✓✓ — proqram HEÇ AÇILMIRDI ✗
        //  ⚠ SƏBƏB: installer YENİLƏMƏSİNDƏN sonra «C:\Program Files\021Cars»
        //    qovluğunun YAZMA İCAZƏSİ İTİR ✗ → baza faylı yaradıla/açıla
        //    bilmir ✗✓✓
        //  ✅ İNDİ: yol BİR DƏFƏ yoxlanılır ✓ —
        //    ① ƏVVƏLCƏ adi qovluq yoxlanılır ✓ (yazma testi ✓)
        //    ② yazıla BİLMİRSE → köçürülür: %LOCALAPPDATA%\021Cars ✓
        //       (həmişə yazıla bilir ✓) və MÖVCUD BAZA KÖÇÜRÜLÜR ✓
        //       → məlumat İTMİR ✗✓✓ və proqram HƏMİŞƏ açılır ✓✓✓
        // ====================================================================

        private static string? _bazaYolu;

        /// <summary>💾 Baza yolunu tapır və YAZA BİLDİYİNİ SÜBUT EDİR ✓✓✓ — bir dəfə ✓.</summary>
        private static string BazaYolunuTap()
        {
            if (_bazaYolu is not null)
            {
                return _bazaYolu;
            }

            var qovluqlar = new List<(string Qovluq, string Ad)>
            {
                (DataQovlugu, "quraşdırma qovluğu"),
                (Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "021Cars"), "%LOCALAPPDATA%\\021Cars"),
                (Path.Combine(Path.GetTempPath(), "021Cars"), "%TEMP%\\021Cars")
            };

            var esasFayl = Path.Combine(qovluqlar[0].Qovluq, "avtopark.db");
            var problemlər = new List<string>();

            for (var i = 0; i < qovluqlar.Count; i++)
            {
                var (qovluq, ad) = qovluqlar[i];
                var fayl = Path.Combine(qovluq, "avtopark.db");

                // ① 📁 QOVLUQ YAZILA BİLİRMİ? ✓ (SQLite jurnal/WAL faylı buraya yazır ✓)
                if (!QovluqYazilaBilir(qovluq))
                {
                    problemlər.Add($"{ad} — qovluq YAZILA BİLMİR ✗");
                    continue;
                }

                // ② 📦 Ehtiyat yola keçiriksə → MÖVCUD BAZA ƏVVƏLCƏ KÖÇÜRÜLÜR ✓✓✓
                //   (beləliklə istifadəçinin MƏLUMATI İTMİR ✗✓✓)
                var köçürüldü = i > 0 && BazaKopyala(esasFayl, fayl);

                // ③ 📄 BAZA FAYLI AÇILA BİLİRMİ? ✓ (mövcud baza TOXUNULMUR ✗✓✓)
                if (!FaylAcilir(fayl))
                {
                    problemlər.Add($"{ad} — baza faylı AÇILA BİLMİR ✗");
                    continue;
                }

                // ④ 🗄️ ƏSL SQLITE YAZMA TESTİ ✓✓✓
                //   ★ «Error 8: attempt to write a readonly database» MƏHZ BURADA tutulur ✓
                if (!SqliteYazir(fayl))
                {
                    problemlər.Add($"{ad} — SQLite READ-ONLY ✗ (Error 8/14)");
                    continue;
                }

                if (i == 0)
                {
                    AppLogger.Melumat($"💾 Baza hazırdır ✓ → «{fayl}»");
                }
                else
                {
                    AppLogger.Xeberdarliq(
                        $"🩺 BAZA ÖZÜNÜ BƏRPA ETDİ ✓ → «{fayl}»" +
                        $"{(köçürüldü ? " · məlumat köçürüldü ✓" : "")}" +
                        $" · səbəb: {problemlər[0]}");
                }

                return fayl;
            }

            AppLogger.Xeta(
                null,
                "⚠️ YAZILA BİLƏN BAZA YOLU TAPILMADI ✗ — " + string.Join(" · ", problemlər));

            return esasFayl;

            // ================================================================
            //  🔎 KÖMƏKÇİ YOXLAMALAR ✓✓✓ (hamısı UĞURSUZLUQDA `false` qaytarır ✓)
            // ================================================================

            // ① 📁 QOVLUĞA YAZMAQ MÜMKÜNDÜRMÜ? ✓
            //  ★ SQLite yazarkən jurnal/WAL faylını MƏHZ bu qovluqda yaradır ✓
            //  → qovluq yazıla bilmirsə «Error 8: attempt to write a readonly
            //    database» xətası çıxır ✗✓✓ (fayl özü yazıla bilsə də ✗)
            static bool QovluqYazilaBilir(string qovluq)
            {
                try
                {
                    Directory.CreateDirectory(qovluq);

                    var test = Path.Combine(qovluq, ".yazma_testi.tmp");
                    File.WriteAllText(test, "ok");
                    File.Delete(test);

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // ② 📄 BAZA FAYLI AÇILA BİLİRMİ? ✓
            //  ⚠ MÖVCUD BAZA TOXUNULMUR ✗✓✓ — yalnız AÇILIR ✓ (heç nə yazılmır ✗)
            static bool FaylAcilir(string fayl)
            {
                try
                {
                    using var axın = new FileStream(
                        fayl,
                        FileMode.OpenOrCreate,      // varsa toxunulmur ✓ · yoxdursa yaranır ✓
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite);

                    return axın.CanWrite;
                }
                catch
                {
                    return false;
                }
            }

            // ③ 🗄️ ƏSL SQLITE YAZMA TESTİ ✓✓✓ — ★ ƏN VACİB ADDIM ★
            //  Tam olaraq proqramın etdiyi iş aparılır ✓ (WAL ✓ + real yazma ✓):
            //  (a) bağlantı açılır ✓
            //  (b) `journal_mode = WAL` sınanır ✓  ← diskdə -wal faylı yaranır ✓
            //  (c) cədvəl yaradılır + sətir yazılır + cədvəl silinir ✓
            //  ➜ «Error 8» (readonly) və «Error 14» (unable to open) MƏHZ BURADA
            //     tutulur ✓ → növbəti yola keçilir ✓✓✓
            static bool SqliteYazir(string fayl)
            {
                try
                {
                    var cs = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                    {
                        DataSource = fayl,
                        Pooling = false,        // ⚠ test bağlantısı hovuzda QALMASIN ✗✓✓
                        DefaultTimeout = 5
                    }.ToString();

                    using var bağlantı = new Microsoft.Data.Sqlite.SqliteConnection(cs);
                    bağlantı.Open();

                    using var əmr = bağlantı.CreateCommand();
                    əmr.CommandText =
                        "PRAGMA journal_mode = WAL;" +
                        "CREATE TABLE IF NOT EXISTS \"__021cars_yazma_testi\" (Id INTEGER);" +
                        "INSERT INTO \"__021cars_yazma_testi\" (Id) VALUES (1);" +
                        "DROP TABLE \"__021cars_yazma_testi\";";

                    əmr.ExecuteNonQuery();

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            // ④ 📦 BAZANI EHTİYAT YOLA KÖÇÜRÜR ✓ (MƏLUMAT İTMİR ✗✓✓)
            //  • `-wal` / `-shm` faylları da köçürülür ✓ (WAL rejimində son
            //    yazılan məlumat da İTİR ✗✓✓)
            //  • hədəf artıq varsa TOXUNULMUR ✗ (ən son vəziyyət qalır ✓)
            static bool BazaKopyala(string mənbə, string hədəf)
            {
                try
                {
                    if (!File.Exists(mənbə)) return false;
                    if (File.Exists(hədəf)) return false;

                    Directory.CreateDirectory(Path.GetDirectoryName(hədəf)!);
                    File.Copy(mənbə, hədəf, overwrite: false);

                    foreach (var uzantı in new[] { "-wal", "-shm" })
                    {
                        if (File.Exists(mənbə + uzantı) && !File.Exists(hədəf + uzantı))
                        {
                            File.Copy(mənbə + uzantı, hədəf + uzantı, overwrite: false);
                        }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// ♻️ Köhnə <b>səhv</b> yerdə qalmış baza faylı ✓ — <c>{kök}\avtopark.db</c> ✓
        /// <para>⚠️ Köhnə buraxılışlar bərpanı buraya yazırdı ✗ → məlumat bunun içində qala bilər ✓✓✓</para>
        /// </summary>
        public static string KohneBazaYolu =>
            Path.Combine(Qovluq, "avtopark.db");

        /// <summary>📁 Alt qovluğu yaradıb yolunu qaytarır ✓ (alınmasa <c>null</c> ✓ — çökmə YOX ✗)</summary>
        public static string? Alt(string ad)
        {
            try
            {
                var tam = Path.Combine(Qovluq, ad);
                Directory.CreateDirectory(tam);
                return tam;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>✅ Kök qovluğa yazmaq mümkündür? ✓</summary>
        public static bool YazilaBilir
        {
            get
            {
                try
                {
                    var test = Path.Combine(Qovluq, ".yazma_testi.tmp");
                    File.WriteAllText(test, "ok");
                    File.Delete(test);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
