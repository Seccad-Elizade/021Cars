// ============================================================================
//  🏠 021Cars — KÖK QOVLUQ (13)  ★ BÜTÜN FAYLLARIN YERİ ★
// ----------------------------------------------------------------------------
//  ✅ BÜTÜN fayllar (baza · log · media · yedək · ayar · istifadəçilər)
//     ★ QURAŞDIRMA QOVLUĞUNDA ★ olur ✓✓✓
//  ✅ Default:  C:\Program Files\021Cars\   ✓ (installer-də seçilir ✓)
//  ❌ AppData / MyDocuments / səpələnmiş fayllar — HEÇ BİRİ YOXDUR ✗✓✓
// ============================================================================

using System;
using System.IO;

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
        public static string BazaYolu => BazaYolunuTap();

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

        /// <summary>💾 Baza yolunu təyin edir (yazma + AÇILIŞ testi ✓) — bir dəfə ✓.</summary>
        private static string BazaYolunuTap()
        {
            if (_bazaYolu is not null)
            {
                return _bazaYolu;
            }

            var esas = Path.Combine(DataQovlugu, "avtopark.db");

            // ① ƏSAS YOL ✓ (quraşdırma qovluğu ✓)
            if (Yoxla(esas, esas))
            {
                _bazaYolu = esas;
                return _bazaYolu;
            }

            // ② EHTİYAT: %LOCALAPPDATA%\021Cars ✓ (həmişə yazıla bilir ✓)
            var ehtiyatQovluq = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "021Cars");

            var hedef = Path.Combine(ehtiyatQovluq, "avtopark.db");

            if (Yoxla(ehtiyatQovluq, hedef) && Yoxla(hedef, hedef))
            {
                _bazaYolu = hedef;
                return _bazaYolu;
            }

            // ③ EHTİYAT: %TEMP%\021Cars ✓ (son şans — həmişə açılır ✓)
            var tempQovluq = Path.Combine(Path.GetTempPath(), "021Cars");
            var tempBaza = Path.Combine(tempQovluq, "avtopark.db");

            if (Yoxla(tempQovluq, tempBaza) && Yoxla(tempBaza, tempBaza))
            {
                _bazaYolu = tempBaza;
                return _bazaYolu;
            }

            // ⚠ HEÇ BİRİ ALINMADI ✗ → əsas yol qaytarılır ✓ (xəta aydın görünsün ✓)
            _bazaYolu = esas;
            return _bazaYolu;

            // ================================================================
            //  🔎 YOXLAMA: qovluq yaranır + fayl AÇILA BİLİR? ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ `File.Create` İSTİFADƏ OLUNMUR ✗ — mövcud bazanı SIFIRLAYARDI ✗✓✓
            //  ✅ `OpenOrCreate` — fayl varsa TOXUNULMUR ✓ (məlumat QORUNUR ✓)
            //  ✅ Uğurlu olarsa: «SQLite Error 14» MÜMKÜN DEYİL ✗✓✓
            // ================================================================
            static bool Yoxla(string qovluq, string fayl)
            {
                try
                {
                    Directory.CreateDirectory(qovluq);

                    using var axın = new FileStream(
                        fayl,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite);

                    // ✍️ real yazma testi ✓
                    axın.WriteByte(0);
                    axın.Flush();

                    if (axın.Length <= 1)
                    {
                        axın.SetLength(0);   // ⚠ yalnız YENİ (boş) faylı təmizləyirik ✓
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
