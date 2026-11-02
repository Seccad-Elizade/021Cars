using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>🧮 Maya analitikasının nəticəsi ✓ (PDF/hesabat üçün vahid model ✓)</summary>
    public sealed record MayaNeticesi(
        decimal Kok,
        decimal PlanMaya,
        decimal FaktikiMaya,
        decimal Ferq,
        decimal Menfeet,
        decimal QaligKok,
        int OdenisSayi,
        decimal OdenilmisGecikme)
    {
        /// <summary>📊 KÖK-ün ödənilmə faizi (0–1) ✓</summary>
        public double Faiz => Kok <= 0m ? 0d : (double)Math.Clamp(FaktikiMaya / Kok, 0m, 1m);

        /// <summary>➕ Fərq mətni: «+50,00 ₼ çox» / «−30,00 ₼ az» / «plan üzrə» ✓</summary>
        public string FerqMetni => Math.Abs(Ferq) < 0.01m
            ? "plan üzrə ✓"
            : (Ferq > 0m ? $"+{Ferq:N2} ₼ çox" : $"−{Math.Abs(Ferq):N2} ₼ az");
    }

    /// <summary>
    /// 🧮 <b>MAYA ANALİTİKASI — VAHİD HESABLAMA</b> ✓✓✓ (v6.2.90)
    /// <para>
    /// ⚠ PDF hesabatları əvvəl bu rəqəmləri <b>ÜMUMİYYƏTLƏ göstərmirdi</b> ✗
    /// («köhnə şablon» ✗) → istifadəçi: «yeni funksiyalar pdflərdə yoxdur ✗»
    /// </para>
    /// <para>
    /// ✅ İndi bütün PDF-lər (arxiv ✓ kredit ✓ maliyyə hesabatı ✓) <b>EYNİ</b>
    /// düsturdan istifadə edir ✓ — ekrandakı rəqəmlərlə <b>BİR-BİRDİR</b> ✓✓✓
    /// </para>
    /// <para>📌 Düstur: <see cref="Models.Credit.OdenisKokPayi"/> (maya payı) ✓
    /// + ödənilmiş gecikmə tamamilə mayaya ✓ (v6.2.88 qaydası ✓)</para>
    /// </summary>
    public static class MayaAnalizi
    {
        /// <summary>🧮 Kredit üzrə maya analitikasını hesablayır ✓</summary>
        public static MayaNeticesi Hesabla(
            Models.Credit? credit,
            IReadOnlyList<Models.CreditTransaction>? hereketler)
        {
            if (credit is null)
            {
                return new MayaNeticesi(0m, 0m, 0m, 0m, 0m, 0m, 0, 0m);
            }

            var gelirler = (hereketler ?? Array.Empty<Models.CreditTransaction>())
                .Where(t => string.Equals(t.Nov, "Gəlir", StringComparison.Ordinal))
                .OrderBy(t => t.Tarix)
                .ThenBy(t => t.Id)
                .ToList();

            var odenilmisKok = 0m;
            var menfeet = 0m;

            foreach (var t in gelirler)
            {
                // ⚖️ v6.2.91 — bölgü işlədilmirsə → TAM məbləğ mayaya ✓✓✓
                if (!t.TerefdasBolguTetbiqOlunub)
                {
                    odenilmisKok = Math.Round(odenilmisKok + t.Mebleg, 2);
                    continue;
                }

                var pay = credit.OdenisMayaPayi(t, odenilmisKok);

                odenilmisKok = Math.Round(odenilmisKok + pay, 2);
                menfeet += Math.Round(Math.Max(0m, t.Mebleg - pay), 2);
            }

            // ⚠️ v6.2.88 — ÖDƏNİLMİŞ GECİKMƏ tamamilə MAYAYA ✓ (ekranla eyni ✓)
            var gecikme = (hereketler ?? Array.Empty<Models.CreditTransaction>())
                .Where(t => string.Equals(t.Nov, "Gecikmə", StringComparison.Ordinal) && t.Odenilib)
                .Sum(t => t.Mebleg);

            var faktikiMaya = Math.Round(odenilmisKok + gecikme, 2);
            var planMaya = Math.Round(credit.Kok, 2);

            return new MayaNeticesi(
                Kok: planMaya,
                PlanMaya: planMaya,
                FaktikiMaya: faktikiMaya,
                Ferq: Math.Round(faktikiMaya - planMaya, 2),
                Menfeet: Math.Round(menfeet, 2),
                QaligKok: Math.Max(0m, Math.Round(planMaya - faktikiMaya, 2)),
                OdenisSayi: gelirler.Count,
                OdenilmisGecikme: Math.Round(gecikme, 2));
        }
    }
}
