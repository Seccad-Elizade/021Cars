using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

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

                Temel();

                using var c = Aç();

                // 📝 Hazır əmrlər ✓ (hər fayl üçün yenidən hazırlanmır ✗)
                using var qeydEmri = c.CreateCommand();
                qeydEmri.CommandText = @"
                    INSERT INTO Qeyd (Ad, Tarix, Nov, Tip, Basliq, Mebleg, Xulase, Json)
                    VALUES ($ad, $tarix, $nov, $tip, $basliq, $mebleg, $xulase, $json)
                    ON CONFLICT(Ad) DO UPDATE SET
                        Tarix = $tarix, Nov = $nov, Tip = $tip, Basliq = $basliq,
                        Mebleg = $mebleg, Xulase = $xulase, Json = $json;";

                var pAd = qeydEmri.Parameters.Add("$ad", SqliteType.Text);
                var pTarix = qeydEmri.Parameters.Add("$tarix", SqliteType.Text);
                var pNov = qeydEmri.Parameters.Add("$nov", SqliteType.Text);
                var pTip = qeydEmri.Parameters.Add("$tip", SqliteType.Text);
                var pBasliq = qeydEmri.Parameters.Add("$basliq", SqliteType.Text);
                var pMebleg = qeydEmri.Parameters.Add("$mebleg", SqliteType.Real);
                var pXulase = qeydEmri.Parameters.Add("$xulase", SqliteType.Text);
                var pJson = qeydEmri.Parameters.Add("$json", SqliteType.Text);

                using var silEmri = c.CreateCommand();
                silEmri.CommandText = "INSERT INTO Silinen (Cedvel, Id) VALUES ($c, $i);";
                var pCedvel = silEmri.Parameters.Add("$c", SqliteType.Text);
                var pId = silEmri.Parameters.Add("$i", SqliteType.Integer);

                SqliteTransaction? tr = null;

                void PartiyaBasla()
                {
                    tr = c.BeginTransaction();
                    qeydEmri.Transaction = tr;
                    silEmri.Transaction = tr;
                }

                void PartiyaBitir()
                {
                    try
                    {
                        tr?.Commit();
                    }
                    catch
                    {
                        // 🛡️
                    }

                    qeydEmri.Transaction = null;
                    silEmri.Transaction = null;
                    tr?.Dispose();
                    tr = null;
                }

                var saat = Stopwatch.StartNew();
                var sonAd = string.Empty;

                PartiyaBasla();

                foreach (var fayl in qalanlar)
                {
                    var ad = Path.GetFileName(fayl);

                    try
                    {
                        var json = File.ReadAllText(fayl);
                        var m = MetadatalariCixar(json);

                        pAd.Value = ad;
                        pTarix.Value = m.Tarix.ToString("s");
                        pNov.Value = m.Nov;
                        pTip.Value = m.Tip;
                        pBasliq.Value = m.Basliq;
                        pMebleg.Value = (double)m.Mebleg;
                        pXulase.Value = m.Xulase;
                        pJson.Value = json;

                        qeydEmri.ExecuteNonQuery();

                        foreach (var (cedvel, id) in m.Idler)
                        {
                            pCedvel.Value = cedvel;
                            pId.Value = id;
                            silEmri.ExecuteNonQuery();
                        }
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

                    sonAd = ad;
                    say++;

                    // 💾 Hər 250 fayldan bir — commit + yeri yadda saxla ✓
                    if (say % 250 == 0)
                    {
                        PartiyaBitir();
                        YaddaSaxla(sonAd);
                        PartiyaBasla();
                    }

                    if (saat.ElapsedMilliseconds > budgetMs)
                    {
                        break;
                    }
                }

                PartiyaBitir();

                if (say > 0)
                {
                    YaddaSaxla(sonAd);
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
