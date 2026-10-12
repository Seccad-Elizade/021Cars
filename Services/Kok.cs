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
        public static string BazaYolu =>
            Path.Combine(DataQovlugu, "avtopark.db");

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
