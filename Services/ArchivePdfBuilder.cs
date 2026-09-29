using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🗄️ <b>«Satılan &amp; Krediti Bitmiş»</b> arxivindəki avtomobil/kredit üçün
    /// <b>PEŞƏKAR ÇAP / PDF</b> sənədi hazırlayır ✓✓✓
    /// <para>
    /// Xarici paket LAZIM DEYİL ✗ — çap oluna bilən <b>HTML</b> yaradılır ✓,
    /// istifadəçi <b>Ctrl+P → «Microsoft Print to PDF»</b> ilə real PDF alır ✓
    /// (Windows-un öz funksiyası ✓ — <see cref="CreditPdfBuilder"/> ilə eyni üsul ✓).
    /// </para>
    /// <para>
    /// Sənədə daxildir ✓: 🚘 avtomobilin BÜTÜN məlumatı · 💳 kredit şərtləri ·
    /// 🧾 xərclər · 📋 bütün hərəkətlər · 👥 tərəfdaş bölgüsü · 📊 yekunlar ✓✓✓
    /// </para>
    /// </summary>
    public static class ArchivePdfBuilder
    {
        /// <summary>Sənəd üçün təklif olunan fayl adı ✓.</summary>
        public static string FaylAdi(CarItem car)
        {
            var ad = $"{car.Marka}_{car.QeydiyyatNisani}".Replace(' ', '_');
            ad = string.Join("_", ad.Split(Path.GetInvalidFileNameChars()));

            return $"Arxiv_{ad}_{DateTime.Now:yyyyMMdd}.html";
        }

        /// <summary>
        /// HTML sənədinin <b>BAŞLIĞI + STİLİ + KPI</b> hissəsini qurur ✓.
        /// </summary>
        public static string Build(
            CarItem car,
            Credit? credit,
            IReadOnlyList<CreditTransaction> hereketler,
            IReadOnlyList<PartnerShare> paylar,
            IReadOnlyList<Sale>? satislar = null)
        {
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html><html lang=\"az\"><head><meta charset=\"utf-8\"/>");
            sb.Append($"<title>Arxiv · {Enc(car.DisplayName)}</title>");
            sb.Append("<style>");
            sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;margin:26px;color:#111}");
            sb.Append("h1{font-size:20px;margin:0 0 4px}");
            sb.Append("h2{font-size:14px;margin:20px 0 6px;border-bottom:2px solid #111;padding-bottom:3px}");
            sb.Append(".sub{color:#555;font-size:11px;margin-bottom:14px}");
            sb.Append("table{width:100%;border-collapse:collapse;font-size:11px;margin-bottom:8px}");
            sb.Append("th{background:#1f2937;color:#fff;text-align:left;padding:6px 5px;font-size:10.5px}");
            sb.Append("td{padding:5px;border-bottom:1px solid #e5e7eb}");
            sb.Append("tr:nth-child(even) td{background:#f9fafb}");
            sb.Append("@page{size:A4 landscape;margin:10mm}");
            sb.Append("table{table-layout:fixed;width:100%;word-wrap:break-word}");
            sb.Append("thead{display:table-header-group}tr{page-break-inside:avoid}");
            sb.Append("h2{page-break-after:avoid}");
            sb.Append(".kpi{display:flex;gap:10px;flex-wrap:wrap;margin:10px 0}");
            sb.Append(".card{flex:1;min-width:150px;border:1px solid #d1d5db;border-radius:7px;padding:9px 11px}");
            sb.Append(".card b{display:block;font-size:16px;margin-top:2px}");
            sb.Append(".lbl{font-size:10px;color:#6b7280;text-transform:uppercase;letter-spacing:.4px}");
            sb.Append(".ok{color:#047857}.warn{color:#b45309}.bad{color:#b91c1c}");
            sb.Append(".print{position:fixed;top:14px;right:14px;padding:9px 16px;background:#111;color:#fff;border:0;border-radius:6px;font-size:13px;cursor:pointer}");
            sb.Append("@media print{.print{display:none}}");
            sb.Append("</style></head><body>");

            sb.Append("<button class=\"print\" onclick=\"window.print()\">ÇAP ET / PDF</button>");

            // ---------------- BAŞLIQ ----------------
            sb.Append("<h1>ARXİV SƏNƏDİ — SATILAN / KREDİTİ BİTMİŞ AVTOMOBİL</h1>");
            sb.Append($"<div class=\"sub\">Avtomobil: <b>{Enc(car.DisplayName)}</b> · " +
                      $"Status: <b>{Enc(car.Status)}</b> · " +
                      (credit is not null
                          ? $"Müqavilə: <b>{Enc(credit.MuqavileNomresi)}</b> · Müştəri: <b>{Enc(credit.Mustəri)}</b> · "
                          : string.Empty) +
                      $"Sənəd tarixi: <b>{DateTime.Today:dd.MM.yyyy}</b></div>");

            sb.Append(Kpi(car, credit, satislar));
            sb.Append(BlokSatis(car, credit, satislar));   // 💰 SATIŞ MƏLUMATLARI ✓✓✓
            sb.Append(Body(car, credit, hereketler, paylar));
            sb.Append("</body></html>");

            return sb.ToString();
        }

        /// <summary>
        /// 📄 Sənədin <b>ƏSAS HİSSƏSİ</b> — bütün məlumat bölmələri ✓✓✓
        /// </summary>
        public static string Body(
            CarItem car,
            Credit? credit,
            IReadOnlyList<CreditTransaction> hereketler,
            IReadOnlyList<PartnerShare> paylar)
        {
            var sb = new StringBuilder();

            BlokAvtomobil(sb, car);
            BlokXercler(sb, car);
            BlokKredit(sb, credit);
            BlokQrafik(sb, credit, hereketler);      // 📅 ödəniş qrafiki ✓✓✓
            BlokHereketler(sb, hereketler);
            BlokPaylar(sb, paylar);

            return sb.ToString();
        }

        /// <summary>🚘 ① AVTOMOBİL MƏLUMATLARI ✓</summary>
        private static void BlokAvtomobil(StringBuilder sb, CarItem car)
        {
            sb.Append("<h2>AVTOMOBİL MƏLUMATLARI</h2>");
            sb.Append("<table><tr><th style=\"width:24%\">Göstərici</th><th>Dəyər</th></tr>");
            S(sb, "Marka / Model", car.Marka);
            S(sb, "Dövlət nömrəsi", car.QeydiyyatNisani);
            S(sb, "VIN", car.Vin);
            S(sb, "İl", car.Il > 0 ? car.Il.ToString() : "—");
            S(sb, "Yürüş", car.Yurus > 0 ? $"{car.Yurus:N0} km" : "—");
            S(sb, "Yanacaq", car.Yanacaq);
            S(sb, "Alış tarixi", car.AlisTarixiTamMetni);
            S(sb, "Alış üsulu", car.AlisUsuluMetni);
            S(sb, "Status", car.Status);
            S(sb, "Sənəd sayı", car.SenedSayi.ToString());
            S(sb, "Alış qiyməti", $"{car.AlisQiymeti:N2} AZN");
            S(sb, "Xərclər", $"{car.Xercler:N2} AZN");
            SBold(sb, "MAYA DƏYƏRİ", $"{car.MayaDeyeri:N2} AZN");
            sb.Append("</table>");
        }

        /// <summary>
        /// 💰 <b>SATIŞ MƏLUMATLARI — NECƏYƏ SATILIB ✓✓✓</b>
        /// <list type="bullet">
        ///   <item>Nağd satışda: **satış qiyməti** · tarix · müştəri · ödəniş üsulu · barter · mənfəət ✓</item>
        ///   <item>Kreditlə satışda: kredit qiyməti · avans · faiz · aylıq ödəniş ✓</item>
        /// </list>
        /// </summary>
        private static string BlokSatis(CarItem car, Credit? credit, IReadOnlyList<Sale>? satislar)
        {
            var sb = new StringBuilder();

            if (satislar is { Count: > 0 })
            {
                sb.Append("<h2>💰 SATIŞ MƏLUMATLARI (NECƏYƏ SATILIB)</h2>");
                sb.Append("<table><tr><th style=\"width:11%\">Tarix</th>" +
                          "<th style=\"width:12%\">Müqavilə</th><th style=\"width:16%\">Müştəri</th>" +
                          "<th style=\"width:15%\">SATIŞ QİYMƏTİ</th><th style=\"width:13%\">Maya dəyəri</th>" +
                          "<th style=\"width:13%\">MƏNFƏƏT</th><th>Ödəniş üsulu · qeyd</th></tr>");

                foreach (var s in satislar.OrderBy(s => s.SatisTarixi))
                {
                    sb.Append($"<tr><td>{s.SatisTarixi:dd.MM.yyyy}</td>" +
                              $"<td><b>{Enc(s.MuqavileNomresi)}</b></td>" +
                              $"<td>{Enc(s.Mustəri)}</td>" +
                              $"<td><b class=\"pos\">{s.SatisQiymeti:N2} AZN</b></td>" +
                              $"<td>{s.MayaDeyeri:N2} AZN</td>" +
                              $"<td class=\"{(s.Menfeet >= 0m ? "pos" : "neg")}\"><b>{s.Menfeet:N2} AZN</b></td>" +
                              $"<td>{Enc(s.OdenisUsuluMetni)}" +
                              (s.IsBarter ? $" · 🤝 barter {s.BarterMebleg:N2} AZN" : string.Empty) +
                              (string.IsNullOrWhiteSpace(s.Qeyd) ? string.Empty : $" · {Enc(s.Qeyd)}") +
                              $"</td></tr>");
                }

                var cem = satislar.Sum(s => s.SatisQiymeti);
                var menfeetCem = satislar.Sum(s => s.Menfeet);

                sb.Append($"<tr><td colspan=\"3\"><b>CƏMİ</b></td>" +
                          $"<td><b>{cem:N2} AZN</b></td><td></td>" +
                          $"<td class=\"{(menfeetCem >= 0m ? "pos" : "neg")}\"><b>{menfeetCem:N2} AZN</b></td>" +
                          "<td></td></tr></table>");

                sb.Append("<div class=\"note\">ℹ️ Satış qiyməti MÜŞTƏRİNİN ödədiyi məbləğdir ✓ — " +
                          "mənfəət = satış qiyməti − maya dəyəri ✓</div>");
            }
            else if (credit is not null)
            {
                sb.Append("<h2>💰 SATIŞ MƏLUMATLARI (NECƏYƏ SATILIB)</h2>");
                sb.Append("<table><tr><th style=\"width:32%\">Göstərici</th><th>Dəyər</th></tr>");
                SBold(sb, "SATIŞ QİYMƏTİ (kreditlə)",
                    $"{credit.KreditQiymeti + credit.IlkinOdenis:N2} AZN");
                SBold(sb, "MÜQAVİLƏ MƏBLƏĞİ", $"{credit.Mebleg:N2} AZN");
                S(sb, "İlkin ödəniş (avans)", $"{credit.IlkinOdenis:N2} AZN");
                S(sb, "Kreditləşdirilən", $"{credit.Kreditlesdirilen:N2} AZN");
                S(sb, "Faiz məbləği", $"{credit.FaizMeblegi:N2} AZN");
                S(sb, "Müddət · aylıq ödəniş", $"{credit.MuddetAy} ay · {credit.AylıqOdenis:N2} AZN");
                sb.Append("</table>");
            }
            else if (car.SatisQiymeti > 0m)
            {
                sb.Append("<h2>💰 SATIŞ MƏLUMATLARI (NECƏYƏ SATILIB)</h2>");
                sb.Append("<table><tr><th style=\"width:32%\">Göstərici</th><th>Dəyər</th></tr>");
                SBold(sb, "SATIŞ QİYMƏTİ", $"{car.SatisQiymeti:N2} AZN");
                S(sb, "Maya dəyəri", $"{car.MayaDeyeri:N2} AZN");
                SBold(sb, "MƏNFƏƏT", $"{car.SatisQiymeti - car.MayaDeyeri:N2} AZN");
                S(sb, "Status", car.Status);
                sb.Append("</table>");
            }

            return sb.ToString();
        }

        /// <summary>🧾 ② XƏRCLƏR — DETALLI siyahı (hər xərc ayrıca ✓) + kateqoriya yekunu ✓✓✓</summary>
        private static void BlokXercler(StringBuilder sb, CarItem car)
        {
            var xercler = (car.Expenses ?? new List<ExpenseItem>())
                .Where(x => !string.Equals(x.Kategoriya, "Alış", StringComparison.Ordinal))
                .OrderBy(x => x.Tarix)
                .ToList();

            sb.Append("<h2>XƏRCLƏR — DETALLI SİYAHI (NƏLƏR XƏRCLƏNİB)</h2>");

            if (xercler.Count == 0)
            {
                sb.Append("<div class=\"empty\">Bu avtomobil üzrə xərc qeydə alınmayıb.</div>");
                sb.Append($"<div class=\"note\">📐 MAYA DƏYƏRİ = Alış qiyməti ({car.AlisQiymeti:N2} AZN) + " +
                          $"Xərclər (0,00 AZN) = <b>{car.MayaDeyeri:N2} AZN</b> ✓</div>");
                return;
            }

            // ================================================================
            //  ✅ HƏR XƏRC AYRICA — tarix · qrup · kateqoriya · təyinat ·
            //     ödəniş üsulu · məbləğ · qeyd ✓✓✓
            // ================================================================
            sb.Append("<table><tr><th style=\"width:10%\">Tarix</th><th style=\"width:14%\">Qrup</th>" +
                      "<th style=\"width:16%\">Kateqoriya</th><th style=\"width:19%\">Təyinat</th>" +
                      "<th style=\"width:13%\">Ödəniş üsulu</th><th style=\"width:13%\">Məbləğ</th><th>Qeyd</th></tr>");

            foreach (var x in xercler)
            {
                sb.Append($"<tr><td>{x.Tarix:dd.MM.yyyy}</td>" +
                          $"<td>{Enc(x.Qrup)}</td>" +
                          $"<td><b>{Enc(x.Kategoriya)}</b></td>" +
                          $"<td>{Enc(x.Teyinat)}</td>" +
                          $"<td>{Enc(x.OdenisUsulu)}</td>" +
                          $"<td><b>{x.Mebleg:N2} AZN</b></td>" +
                          $"<td>{Enc(x.Qeyd)}</td></tr>");
            }

            var cem = xercler.Sum(x => x.Mebleg);

            sb.Append($"<tr><td colspan=\"5\"><b>CƏMİ XƏRC ({xercler.Count} qeyd)</b></td>" +
                      $"<td><b>{cem:N2} AZN</b></td><td></td></tr></table>");

            // ================================================================
            //  ✅ KATEQORİYA ÜZRƏ YEKUN + pay faizi ✓
            // ================================================================
            sb.Append("<h2>XƏRCLƏR — KATEQORİYA ÜZRƏ YEKUN</h2>");
            sb.Append("<table><tr><th style=\"width:45%\">Kateqoriya</th>" +
                      "<th style=\"width:18%\">Qeyd sayı</th><th style=\"width:17%\">Məbləğ</th>" +
                      "<th>Pay (%)</th></tr>");

            foreach (var qrup in xercler
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Kategoriya) ? "Digər" : x.Kategoriya)
                .OrderByDescending(g => g.Sum(x => x.Mebleg)))
            {
                var mebleg = qrup.Sum(x => x.Mebleg);

                sb.Append($"<tr><td>{Enc(qrup.Key)}</td><td>{qrup.Count()}</td>" +
                          $"<td><b>{mebleg:N2} AZN</b></td>" +
                          $"<td>{(cem <= 0m ? 0m : mebleg * 100m / cem):N2}%</td></tr>");
            }

            sb.Append($"<tr><td><b>CƏMİ</b></td><td>{xercler.Count}</td>" +
                      $"<td><b>{cem:N2} AZN</b></td><td>100,00%</td></tr></table>");

            sb.Append($"<div class=\"note\">📐 MAYA DƏYƏRİ = Alış qiyməti ({car.AlisQiymeti:N2} AZN) + " +
                      $"Xərclər ({cem:N2} AZN) = <b>{car.MayaDeyeri:N2} AZN</b> ✓</div>");
        }

        /// <summary>💳 ③ KREDİT MÜQAVİLƏSİ ✓</summary>
        private static void BlokKredit(StringBuilder sb, Credit? credit)
        {
            if (credit is null)
            {
                return;
            }

            sb.Append("<h2>KREDİT MÜQAVİLƏSİ</h2>");
            sb.Append("<table><tr><th style=\"width:24%\">Göstərici</th><th>Dəyər</th></tr>");
            S(sb, "Müqavilə №", credit.MuqavileNomresi);
            S(sb, "Müştəri", credit.Mustəri);
            S(sb, "Status", credit.Status);
            S(sb, "Başlama tarixi", $"{credit.BaslamaTarixi:dd.MM.yyyy}");
            S(sb, "Müddət", $"{credit.MuddetAy} ay");
            S(sb, "Aylıq ödəniş", $"{credit.AylıqOdenis:N2} AZN");
            S(sb, "Faiz dərəcəsi", $"{credit.FaizDerecesi:N2} %");

            // ================================================================
            //  💰 MALİYYƏ BLOKU — İLKİN ÖDƏNİŞ DƏ DAXİL ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL ilkin ödəniş (avans) sənəddə GÖRÜNMÜRDÜ ✗ → müştərinin
            //    kredit götürəndə verdiyi pul sənəddən itirdi ✗✓✓
            //  ✅ İNDİ peşəkar ardıcıllıqla:
            //     Müqavilə məbləği = İLKİN ÖDƏNİŞ + Kreditləşdirilən ✓
            //     Kreditin tam qiyməti = Kreditləşdirilən + Faiz ✓✓✓
            // ================================================================
            SBold(sb, "MÜQAVİLƏ MƏBLƏĞİ (MAŞININ QİYMƏTİ)", $"{credit.Mebleg:N2} AZN");
            SBold(sb, "İLKİN ÖDƏNİŞ (AVANS) ✓", $"{credit.IlkinOdenis:N2} AZN");
            S(sb, "KREDİTLƏŞDİRİLƏN (BORC)", $"{credit.Kreditlesdirilen:N2} AZN");
            S(sb, "FAİZ MƏBLƏĞİ", $"{credit.FaizMeblegi:N2} AZN");
            SBold(sb, "KREDİTİN TAM QİYMƏTİ", $"{credit.KreditQiymeti:N2} AZN");
            SBold(sb, "MÜŞTƏRİNİN ÜMUMİ ÖDƏDİYİ",
                $"{credit.Mebleg + credit.FaizMeblegi:N2} AZN   (avans {credit.IlkinOdenis:N2} + kredit {credit.KreditQiymeti:N2})");

            if (!string.IsNullOrWhiteSpace(credit.Qeyd))
            {
                S(sb, "Qeyd", credit.Qeyd);
            }

            sb.Append("</table>");
        }

        /// <summary>📊 YEKUN KPI kartları ✓ — satış qiyməti SATIŞ QEYDİNDƏN ✓✓✓</summary>
        public static string Kpi(CarItem car, Credit? credit, IReadOnlyList<Sale>? satislar = null)
        {
            // ================================================================
            //  ✅ SATIŞ QİYMƏTİ — ƏVVƏLCƏ satış qeydindən ✓
            //  ⚠ ƏVVƏL yalnız `car.SatisQiymeti` oxunurdu ✗ → satış qeydi olan
            //    maşınlarda PDF-də «0,00 AZN» görünürdü ✗✓✓
            // ================================================================
            var satisQiymeti = satislar is { Count: > 0 }
                ? satislar.Sum(s => s.SatisQiymeti)
                : car.SatisQiymeti;

            var sb = new StringBuilder();
            sb.Append("<div class=\"kpi\">");
            sb.Append(Card("MAYA DƏYƏRİ", $"{car.MayaDeyeri:N2} AZN"));
            sb.Append(Card("ALIŞ QİYMƏTİ", $"{car.AlisQiymeti:N2} AZN"));
            sb.Append(Card("XƏRCLƏR", $"{car.Xercler:N2} AZN"));

            if (credit is not null)
            {
                // 💰 İLKİN ÖDƏNİŞ (AVANS) — MÜQAVİLƏ MƏBLƏĞİNİN BİR HİSSƏSİ ✓✓✓
                sb.Append(Card("İLKİN ÖDƏNİŞ (AVANS)", $"{credit.IlkinOdenis:N2} AZN"));
                sb.Append(Card("KREDİTLƏŞDİRİLƏN (BORC)", $"{credit.Kreditlesdirilen:N2} AZN"));
                sb.Append(Card("FAİZ MƏNFƏƏTİ", $"{credit.FaizMeblegi:N2} AZN"));
                sb.Append(Card("SATIŞ (KREDİT) QİYMƏTİ", $"{credit.KreditQiymeti:N2} AZN"));
                sb.Append(MenfeetCardi(credit.KreditQiymeti - car.MayaDeyeri));
            }
            else if (satisQiymeti > 0m)
            {
                sb.Append(Card("SATIŞ QİYMƏTİ (NECƏYƏ SATILIB)", $"{satisQiymeti:N2} AZN"));
                sb.Append(MenfeetCardi(satisQiymeti - car.MayaDeyeri));
            }

            sb.Append("</div>");
            return sb.ToString();
        }

        /// <summary>📅 ⑥ <b>ÖDƏNİŞ QRAFİKİ</b> — krediti bağlanmış maşın üçün TAM cədvəl ✓✓✓</summary>
        private static void BlokQrafik(
            StringBuilder sb,
            Credit? credit,
            IReadOnlyList<CreditTransaction> hereketler)
        {
            if (credit is null || credit.MuddetAy <= 0)
            {
                return;
            }

            // ================================================================
            //  ✅ ÖDƏNİLƏN PUL — «Gəlir» + «VAXTINDAN TEZ BAĞLAMA» ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL yalnız «Gəlir» sayılırdı ✗ → müştəri heç bir ay ödəniş
            //    etmədən maşını BİR DƏFƏYƏ bağlayanda qrafikdə «ödənilməmiş»
            //    görünürdü ✗✓✓
            //  ✅ İNDİ: bir dəfəyə ödənilən qalıq da sayılır ✓ → ay-ay cədvəl
            //    düzgün «✅ ÖDƏNİLDİ» göstərir ✓✓✓
            // ================================================================
            var odenilen = hereketler
                .Where(t => string.Equals(t.Nov, "Gəlir", StringComparison.Ordinal)
                            || string.Equals(t.Nov, "Vaxtından tez bağlama", StringComparison.Ordinal))
                .Sum(t => t.Mebleg)

                // ✅ İLKİN ÖDƏNİŞ (AVANS) DƏ ÖDƏNİLMİŞ SAYILIR ✓✓✓
                //  (müştəri kredit götürəndə DƏRHAL ödəyib ✓ — sənəddə görünməlidir ✓)
                + credit.IlkinOdenis;

            const decimal yuvarlaq = 0.50m;   // ✓ kiçik yuvarlaqlaşdırma toleransı

            var aylig = credit.AylıqOdenis > 0m
                ? credit.AylıqOdenis
                : decimal.Round(credit.KreditQiymeti / credit.MuddetAy, 2);

            sb.Append("<h2>ÖDƏNİŞ QRAFİKİ (AY-AY)</h2>");
            sb.Append("<table><tr><th style=\"width:7%\">№</th><th style=\"width:16%\">Ödəniş tarixi</th>" +
                      "<th style=\"width:15%\">Məbləğ</th><th style=\"width:15%\">Ödənilib</th>" +
                      "<th style=\"width:15%\">Vəziyyət</th><th>Qeyd</th></tr>");

            // ================================================================
            //  ✅ SƏTİR 0 — İLKİN ÖDƏNİŞ (AVANS) ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL sənəddə ilkin ödəniş GÖRÜNMÜRDÜ ✗ (qrafik yalnız
            //    ay-ay ödənişlərdən başlayırdı ✗✓✓)
            //  ✅ İNDİ qrafikin ƏN ÜSTÜNDƏ «0» nömrəsi ilə avans göstərilir ✓
            //     (müştəri onu müqavilə günü ödəyib ✓ → ✅ ÖDƏNİLDİ ✓✓✓)
            // ================================================================
            sb.Append($"<tr><td><b>0</b></td><td>{credit.BaslamaTarixi:dd.MM.yyyy}</td>" +
                      $"<td><b>{credit.IlkinOdenis:N2} AZN</b></td>" +
                      $"<td><b>{credit.IlkinOdenis:N2} AZN</b></td>" +
                      $"<td class=\"ok\"><b>✅ ÖDƏNİLDİ</b></td>" +
                      $"<td><b>İLKİN ÖDƏNİŞ (AVANS) ✓</b></td></tr>");

            var qaliq = credit.IlkinOdenis;   // ✅ avans artıq ödənilib ✓
            var odMuddeti = 0;

            for (var i = 1; i <= credit.MuddetAy; i++)
            {
                var tarix = credit.BaslamaTarixi.AddMonths(i);
                var odenilib = odenilen >= qaliq + aylig - yuvarlaq;

                if (odenilib)
                {
                    qaliq += aylig;
                    odMuddeti = i;
                }

                sb.Append($"<tr><td>{i}</td><td>{tarix:dd.MM.yyyy}</td>" +
                          $"<td>{aylig:N2} AZN</td>" +
                          $"<td>{(odenilib ? $"{aylig:N2} AZN" : "0,00 AZN")}</td>" +
                          $"<td class=\"{(odenilib ? "ok" : "warn")}\">" +
                          $"{(odenilib ? "✅ ÖDƏNİLDİ" : "⏳ ÖDƏNİLMƏDİ")}</td>" +
                          $"<td>{(i == odMuddeti ? "📕 vaxtından tez bağlama" : string.Empty)}</td></tr>");
            }

            // ================================================================
            //  ✅ YEKUN — İLKİN ÖDƏNİŞ DƏ DAXİL (PROFESSIONAL) ✓✓✓
            // ----------------------------------------------------------------
            //  Müqavilə məbləği = İLKİN ÖDƏNİŞ + KREDİTİN TAM QİYMƏTİ ✓
            //  Ödənilmiş       = AVANS + REAL ÖDƏNİŞLƏR ✓✓✓
            // ================================================================
            var umumiMebleg = credit.IlkinOdenis + credit.KreditQiymeti;

            sb.Append($"<tr><td colspan=\"2\"><b>Y E K U N — ÜMUMİ ÖDƏNİLMƏLİ</b></td>" +
                      $"<td><b>{umumiMebleg:N2} AZN</b></td>" +
                      $"<td><b>{odenilen:N2} AZN</b></td>" +
                      $"<td colspan=\"2\"><b>{(odenilen >= umumiMebleg - yuvarlaq ? "✅ KREDİT BAĞLIDIR" : "⏳ DAVAM EDİR")}</b></td></tr>");

            sb.Append($"<tr><td colspan=\"3\"><i>İlkin ödəniş {credit.IlkinOdenis:N2} ₼ + kreditin tam qiyməti {credit.KreditQiymeti:N2} ₼</i></td>" +
                      $"<td colspan=\"3\"><i>Ödənilmiş: avans {credit.IlkinOdenis:N2} ₼ + ödənişlər {(odenilen - credit.IlkinOdenis):N2} ₼</i></td></tr>");

            sb.Append("</table>");
        }

        /// <summary>📋 ④ BÜTÜN HƏRƏKƏTLƏR (ödəniş · gecikmə · barter · transfer) ✓</summary>
        private static void BlokHereketler(StringBuilder sb, IReadOnlyList<CreditTransaction> hereketler)
        {
            if (hereketler.Count == 0)
            {
                return;
            }

            sb.Append("<h2>BÜTÜN HƏRƏKƏTLƏR (ÖDƏNİŞ · GECİKMƏ · BARTER · TRANSFER)</h2>");
            sb.Append("<table><tr><th style=\"width:12%\">Tarix</th><th style=\"width:18%\">Növ</th>" +
                      "<th style=\"width:14%\">Məbləğ</th><th style=\"width:12%\">Ödənilib</th><th>Təsvir</th></tr>");

            foreach (var t in hereketler.OrderBy(t => t.Tarix))
            {
                sb.Append($"<tr><td>{t.Tarix:dd.MM.yyyy}</td><td>{Enc(t.Nov)}</td>" +
                          $"<td><b>{t.Mebleg:N2} AZN</b></td>" +
                          $"<td class=\"{(t.Odenilib ? "ok" : "warn")}\">{(t.Odenilib ? "Bəli ✓" : "Xeyr ✗")}</td>" +
                          $"<td>{Enc(t.Tesvir)}</td></tr>");
            }

            sb.Append($"<tr><td colspan=\"4\"><b>CƏMİ HƏRƏKƏT</b></td>" +
                      $"<td><b>{hereketler.Sum(t => t.Mebleg):N2} AZN</b></td></tr></table>");
        }

        /// <summary>👥 ⑤ TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ ✓</summary>
        private static void BlokPaylar(StringBuilder sb, IReadOnlyList<PartnerShare> paylar)
        {
            if (paylar.Count == 0)
            {
                return;
            }

            sb.Append("<h2>TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ</h2>");
            sb.Append("<table><tr><th style=\"width:45%\">Tərəfdaş</th>" +
                      "<th style=\"width:20%\">Faiz %</th><th>Məbləğ</th></tr>");

            foreach (var p in paylar.OrderBy(p => p.Sira))
            {
                sb.Append($"<tr><td>{Enc(p.Terefdas)}</td><td>{p.Faiz:N2}</td>" +
                          $"<td><b>{p.Mebleg:N2} AZN</b></td></tr>");
            }

            sb.Append($"<tr><td colspan=\"2\"><b>PAYLARIN CƏMİ</b></td>" +
                      $"<td><b>{paylar.Sum(p => p.Mebleg):N2} AZN</b></td></tr></table>");
        }

        /// <summary>💾 HTML sənədini verilən yola yazır və default brauzerdə açır ✓✓✓</summary>
        public static void YazVeAc(string html, string yol)
        {
            File.WriteAllText(yol, html, new UTF8Encoding(true));
            Process.Start(new ProcessStartInfo(yol) { UseShellExecute = true });
        }

        /// <summary>➡️ «göstərici · dəyər» sətri (mətn təhlükəsiz ✓)</summary>
        private static void S(StringBuilder sb, string ad, string? deyer)
            => sb.Append($"<tr><td>{Enc(ad)}</td><td><b>{Enc(deyer)}</b></td></tr>");

        /// <summary>➡️ QALIN «göstərici · dəyər» sətri ✓</summary>
        private static void SBold(StringBuilder sb, string ad, string? deyer)
            => sb.Append($"<tr><td><b>{Enc(ad)}</b></td><td><b>{Enc(deyer)}</b></td></tr>");

        /// <summary>⚫ HTML üçün təhlükəsiz mətn ✓</summary>
        private static string Enc(string? deyer)
            => WebUtility.HtmlEncode(deyer ?? string.Empty);

        /// <summary>📊 KPI kartı ✓</summary>
        private static string Card(string lbl, string value)
            => $"<div class=\"card\"><span class=\"lbl\">{Enc(lbl)}</span><b>{value}</b></div>";

        /// <summary>🎯 Mənfəət / zərər kartı ✓</summary>
        private static string MenfeetCardi(decimal menfeet)
            => Card(
                menfeet >= 0m ? "MƏNFƏƏT" : "ZƏRƏR",
                $"<span class=\"{(menfeet >= 0m ? "ok" : "bad")}\">{menfeet:N2} AZN</span>");
    }
}
