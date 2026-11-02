using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🗄️ <b>ARXİV — KÖHNƏ FAYLLARIN KÖÇÜRÜLMƏSİ</b> ✓✓✓ (v6.2.86)
    /// <para>
    /// ★ 110 000 <c>.json</c> faylı → <b>BİR</b> <c>Arxiv.db</c> ✓
    /// </para>
    /// <para>
    /// 🛡️ Təhlükəsizlik qaydaları ✓✓✓:
    /// </para>
    /// <list type="bullet">
    ///   <item>Orijinal fayllar <b>SİLİNMİR</b> ✗ — <c>Trash\_kohne</c>-yə köçürülür ✓</item>
    ///   <item>Köçürmə <b>hissə-hissə</b> gedir ✓ — hər açılışda davam edir ✓
    ///         (10 dəqiqəlik donma YOX ✗✓✓)</item>
    ///   <item>Qeyd artıq varsa təkrar yazılmır ✗ (<c>UNIQUE(Ad)</c> ✓)</item>
    /// </list>
    /// </summary>
    public static partial class TrashArxiv
    {
        /// <summary>❓ Köçürüləcək köhnə JSON faylı qalıb? ✓</summary>
        public static bool KohneFaylVar()
        {
            try
            {
                return Directory.Exists(KohneQovluq) &&
                       Directory.EnumerateFiles(KohneQovluq, "*.json").Any();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 📥 Köhnə JSON fayllarını arxivə köçürür ✓ (vaxt büdcəsi ilə ✓)
        /// </summary>
        /// <param name="budgetMs">Bu açılışda bu qədər milisaniyə işləyir ✓ (sonra davam edir ✓)</param>
        /// <param name="hesabat">(işlənən, qalan) — loqa/UI üçün ✓</param>
        /// <returns>Bu dəfə köçürülən fayl sayı ✓</returns>
        public static int KohneFayllariKocur(int budgetMs = 20000, Action<int, int>? hesabat = null)
        {
            if (!KohneFaylVar())
            {
                return 0;
            }

            var say = 0;

            try
            {
                var fayllar = Directory.GetFiles(KohneQovluq, "*.json");
                Array.Sort(fayllar, StringComparer.OrdinalIgnoreCase);

                var sonuncu = OxuProqres();
                var qalanlar = fayllar
                    .Where(f => string.IsNullOrEmpty(sonuncu) ||
                                string.CompareOrdinal(Path.GetFileName(f), sonuncu) > 0)
                    .ToList();

                Directory.CreateDirectory(KohneArxivQovluq);

                var saat = Stopwatch.StartNew();

                foreach (var fayl in qalanlar)
                {
                    var ad = Path.GetFileName(fayl);

                    try
                    {
                        var json = File.ReadAllText(fayl);
                        JsonYaz(ad, json);
                    }
                    catch
                    {
                        // 🛡️ bir fayl oxunmasa — qalanları davam edir ✓
                    }

                    try
                    {
                        File.Move(fayl, Path.Combine(KohneArxivQovluq, ad), overwrite: true);
                    }
                    catch
                    {
                        // 🛡️ köçürülməsə — yerində qalır ✓ (məlumat İTMİR ✗)
                    }

                    say++;

                    if (say % 100 == 0)
                    {
                        YaddaSaxla(ad);
                    }

                    if (saat.ElapsedMilliseconds > budgetMs)
                    {
                        break;
                    }
                }

                if (say > 0)
                {
                    YaddaSaxla(Path.GetFileName(qalanlar[Math.Min(say, qalanlar.Count) - 1]));
                }

                hesabat?.Invoke(say, qalanlar.Count - say);
            }
            catch
            {
                // 🛡️ köçürmə alınmasa proqram işləyir ✓
            }

            return say;
        }

        private static string OxuProqres()
        {
            try
            {
                return File.Exists(ProqresFayli) ? File.ReadAllText(ProqresFayli).Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void YaddaSaxla(string ad)
        {
            try
            {
                File.WriteAllText(ProqresFayli, ad);
            }
            catch
            {
                // 🛡️
            }
        }
    }
}
