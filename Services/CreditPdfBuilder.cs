using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Kredit üzrə <b>ÇAP/PDF</b> sənədi hazırlayır.
    /// <para>
    /// Xarici paket LAZIM DEYİL ✗ — <b>çap oluna bilən HTML</b> yaradılıb default
    /// brauzerdə açılır ✓, istifadəçi <b>Ctrl+P → «Microsoft Print to PDF»</b> ilə
    /// real PDF alır ✓ (Windows-un öz funksiyası ✓).
    /// </para>
    /// <para>
    /// Sənədə daxildir: müqavilə · müştəri · avtomobil · kredit şərtləri ·
    /// 🤝 <b>barter</b> · 📤 <b>transferlər/beh</b> · <b>TAM ÖDƏNİŞ CƏDVƏLİ</b> ·
    /// tərəfdaş bölgüsü · yekun göstəricilər ✓
    /// </para>
    /// </summary>
    public static class CreditPdfBuilder
    {
        /// <summary>
        /// HTML sənədini qurur.
        /// <para>
        /// <paramref name="musteriRejimi"/> = <c>true</c> → <b>ŞƏXSƏ VERİLƏN
        /// SƏNƏD</b> ✓: salonun DAXİLİ göstəriciləri gizlədilir ✗:
        /// «Kreditin tam qiyməti» · «Maşının mənfəət bölgüsü» ·
        /// «Transferlər — maşının verildiyi şəxslər» ✓✓✓
        /// </para>
        /// </summary>
        public static string Build(
            Credit credit,
            IReadOnlyList<PaymentRow> schedule,
            IReadOnlyList<CreditTransaction> barterler,
            IReadOnlyList<CreditTransaction> transferler,
            IReadOnlyList<PartnerShare> xeyirPaylari,
            decimal barterCemi,
            decimal transferCemi,
            bool musteriRejimi = false,
            DateTime? senedTarixi = null)
        {
            // 📅 SƏNƏD TARİXİ — şəxsə verilən sənəddə TRANSFER TARİXİ ✓✓✓
            //  (təyin olunmasa bugün ✓)
            var sened = senedTarixi ?? DateTime.Today;

            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html><html lang=\"az\"><head><meta charset=\"utf-8\"/>");
            sb.Append($"<title>Kredit {Enc(credit.MuqavileNomresi)} - {Enc(credit.Mustəri)}</title>");
            sb.Append("<style>");
            sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;margin:26px;color:#111}");
            sb.Append("h1{font-size:20px;margin:0 0 4px}h2{font-size:14px;margin:20px 0 6px;border-bottom:2px solid #111;padding-bottom:3px}");
            sb.Append(".sub{color:#555;font-size:11px;margin-bottom:14px}");
            sb.Append("table{width:100%;border-collapse:collapse;font-size:11px;margin-bottom:8px}");
            sb.Append("th{background:#1f2937;color:#fff;text-align:left;padding:6px 5px;font-size:10.5px}");
            sb.Append("td{padding:5px;border-bottom:1px solid #e5e7eb}");
            sb.Append("tr:nth-child(even) td{background:#f9fafb}");
            // ================================================================
            //  🖨️ PEŞƏKAR ÇAP — CƏDVƏL KƏSİLMƏSİN ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ Landşaft (A4 landscape) → 9 sütunlu ödəniş cədvəli TAM
            //  sığır ✓ (əvvəl dar səhifədə sağdan kəsilirdi ✗✓✓).
            // ================================================================
            sb.Append("@page{size:A4 landscape;margin:10mm}");
            sb.Append("table{table-layout:fixed;width:100%;word-wrap:break-word}");
            sb.Append("thead{display:table-header-group}");
            sb.Append("tr{page-break-inside:avoid}");
            sb.Append(".sched td,.sched th{font-size:9.5px;padding:4px 3px}");
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
            sb.Append("<h1>KREDİT MÜQAVİLƏSİ VƏ ÖDƏNİŞ CƏDVƏLİ</h1>");
            sb.Append($"<div class=\"sub\">Müqavilə: <b>{Enc(credit.MuqavileNomresi)}</b> · " +
                      $"Müştəri: <b>{Enc(credit.Mustəri)}</b> · " +
                      $"Avtomobil: <b>{Enc(credit.Car?.DisplayName ?? "-")}</b> · " +
                      $"Sənəd tarixi: <b>{sened:dd.MM.yyyy}</b></div>");

            // ---------------- KPI ----------------
            sb.Append("<div class=\"kpi\">");
            sb.Append(Card("NİSYƏ (BARTER/TRANSFER ÇIXILMIŞ)", $"{credit.Kreditlesdirilen - barterCemi - transferCemi:N2} AZN"));
            sb.Append(Card("FAİZ DƏRƏCƏSİ", $"{credit.FaizDerecesi:N2} %"));
            sb.Append(Card("MÜDDƏT", $"{credit.MuddetAy} ay"));
            sb.Append(Card("AYLIQ ÖDƏNİŞ", $"{credit.AylıqOdenis:N2} AZN"));

            // ⚠ «KREDİTİN TAM QİYMƏTİ» — YALNIZ DAXİLİ hesabatda ✓
            //  (şəxsə verilən sənəddə gizlədilir ✗✓✓)
            if (!musteriRejimi)
            {
                sb.Append(Card("KREDİTİN TAM QİYMƏTİ", $"{credit.KreditQiymeti:N2} AZN"));
            }

            sb.Append(Card("BAŞLAMA TARİXİ", $"{credit.BaslamaTarixi:dd.MM.yyyy}"));
            sb.Append("</div>");

            // ---------------- BARTER ----------------
            if (barterCemi > 0m && barterler.Count > 0)
            {
                sb.Append("<h2>BARTER - NİSYƏDƏN ÇIXILAN MAŞIN</h2>");
                sb.Append("<table><tr><th>Sənəd / Maşın</th><th>Tarix</th><th>Maya dəyəri</th></tr>");
                foreach (var b in barterler)
                {
                    sb.Append($"<tr><td>{Enc(b.Tesvir)}</td><td>{b.Tarix:dd.MM.yyyy}</td>" +
                              $"<td><b>{b.Mebleg:N2} AZN</b></td></tr>");
                }

                sb.Append("<tr><td colspan=\"2\"><b>CƏMİ BARTER</b></td>" +
                          $"<td><b>{barterCemi:N2} AZN</b></td></tr></table>");
            }

            // ================================================================
            //  📤 «TRANSFERLƏR — MAŞININ VERİLDİYİ ŞƏXSLƏR» ve
            //  👥 «MAŞININ MƏNFƏƏT BÖLGÜSÜ» —
            //  YALNIZ DAXİLİ hesabatda ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ Şəxsə verilən sənəddə (📤 Transfer Olunan → PDF) bunlar
            //  GÖSTƏRİLMİR ✗ — avto salonun daxili məlumatıdır ✓
            // ================================================================
            if (!musteriRejimi)
            {
                // 💳 DAXİLİ hesabat — tam transfer bölməsi ✓
                sb.Append(TransferSection(transferler, transferCemi));
            }
            else if (transferler.Count > 0)
            {
                // ============================================================
                //  💰 ŞƏXSƏ VERİLƏN SƏNƏD — şəxsin VERDİYİ PUL gösterilir ✓
                //  (özünün ödədiyi məbləğdir ✓ — istifadəçi tələbi ✓✓✓)
                // ============================================================
                sb.Append(TransferPulSection(transferler, transferCemi));
            }

            sb.Append(ScheduleSection(schedule, credit));

            if (!musteriRejimi)
            {
                sb.Append(PartnerSection(xeyirPaylari));
            }

            // ---------------- YEKUN ----------------
            var odenilmis = schedule.Sum(r => r.OdenilenMebleg);
            sb.Append("<div class=\"kpi\">");
            sb.Append(Card("ÖDƏNİLMİŞ", $"{odenilmis:N2} AZN"));
            sb.Append(Card("QALIQ BORC", $"{Math.Max(0m, (credit.KreditQiymeti - barterCemi - transferCemi) - odenilmis):N2} AZN"));
            sb.Append(Card("STATUS", Enc(credit.Status)));
            sb.Append("</div>");

            // ================================================================
            //  ✍️ İMZA + TARİX BLOKU — YALNIZ DAXİLİ hesabatda ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ Şəxsə verilən sənəddə (📤 Transfer Olunan → PDF) İMZA və
            //  SON TARİX GÖSTƏRİLMİR ✗✓✓ (istifadəçi tələbi ✓).
            // ================================================================
            if (!musteriRejimi)
            {
                sb.Append("<div class=\"sub\" style=\"margin-top:20px\">" +
                          "İmza: ____________________________ &nbsp;&nbsp;&nbsp; Müştəri: " +
                          $"<b>{Enc(credit.Mustəri)}</b> &nbsp;&nbsp;&nbsp; Tarix: {sened:dd.MM.yyyy}</div>");
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// 💰 <b>ŞƏXSƏ VERİLƏN SƏNƏD — «VERİLƏN PUL» BÖLMƏSİ</b> ✓✓✓
        /// <para>
        /// Yalnız şəxsin <b>ÖZ verdiyi məbləğ</b> göstərilir ✓
        /// (şəxs · tarix · verdiyi PUL ✓) — salonun daxili məlumatları
        /// (mənfəət bölgüsü, maya, kreditin tam qiyməti) GÖSTƏRİLMİR ✗✓✓
        /// </para>
        /// </summary>
        private static string TransferPulSection(
            IReadOnlyList<CreditTransaction> transferler,
            decimal transferCemi)
        {
            var sb = new StringBuilder();
            sb.Append("<h2>📤 TRANSFER — VERİLƏN PUL</h2>");
            sb.Append("<table><tr><th>ŞƏXS</th><th>TARIX</th><th>VERİLƏN PUL</th></tr>");

            foreach (var t in transferler)
            {
                sb.Append($"<tr><td><b>{Enc(t.Tesvir)}</b></td>" +
                          $"<td>{t.Tarix:dd.MM.yyyy}</td>" +
                          $"<td><b>{t.Mebleg:N2} AZN</b></td></tr>");
            }

            sb.Append("<tr><td colspan=\"2\"><b>CƏMİ VERİLƏN PUL</b></td>" +
                      $"<td><b>{transferCemi:N2} AZN</b></td></tr></table>");

            return sb.ToString();
        }

        /// <summary>📤 Transfer/beh bölməsi — «maşın hansı şəxsə verilib».</summary>
        private static string TransferSection(
            IReadOnlyList<CreditTransaction> transferler,
            decimal transferCemi)
        {
            if (transferler.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append("<h2>TRANSFERLER - MAŞININ VERİLDİYİ ŞƏXSLƏR</h2>");
            sb.Append("<table><tr><th>Şəxs</th><th>Tarix</th><th>Beh / məbləğ</th><th>Detallar</th></tr>");

            foreach (var t in transferler)
            {
                sb.Append($"<tr><td><b>{Enc(t.Tesvir)}</b></td><td>{t.Tarix:dd.MM.yyyy}</td>" +
                          $"<td>{t.Mebleg:N2} AZN</td>" +
                          $"<td>{(t.Odenilib ? "<span class=\"ok\">odenilib</span>" : "<span class=\"warn\">gozleyir</span>")}</td></tr>");
            }

            sb.Append("<tr><td colspan=\"2\"><b>CƏMİ TRANSFER / BEH</b></td>" +
                      $"<td><b>{transferCemi:N2} AZN</b></td><td></td></tr></table>");
            return sb.ToString();
        }

        /// <summary>
        /// 📅 Tam ödəniş cədvəli — <b>BOŞ OLSA kredit məlumatından QURULUR</b> ✓✓✓
        /// <para>
        /// ⚠ ƏVVƏLKİ XƏTA: cədvəl boş gələrsə PDF-də <b>yalnız başlıq</b>
        /// görünürdü ✗ (sətirlər yox ✗✓✓). İndi ehtiyat qurucu işə düşür ✓.
        /// </para>
        /// </summary>
        private static string ScheduleSection(IReadOnlyList<PaymentRow> schedule, Credit credit)
        {
            // 🩺 Ehtiyat: VM qrafiki boşdursa kreditin özündən qurulur ✓
            var cedvel = schedule.Count > 0 ? schedule : PlanCedveliQur(credit);

            var sb = new StringBuilder();
            sb.Append($"<h2>TAM ÖDƏNİŞ CƏDVƏLİ — {cedvel.Count} sətir · {credit.MuddetAy} ay</h2>");
            sb.Append("<table class=\"sched\"><tr><th>№</th><th>PLAN TARİXİ</th><th>Ödəniş</th><th>Vəziyyət</th>" +
                      "<th>Faktiki tarix</th><th>Əsas borc</th><th>Faiz</th><th>Qalıq</th><th>Gecikmə</th></tr>");

            foreach (var row in cedvel)
            {
                var veziyyet = row.Odenilib
                    ? "<span class=\"ok\">odenilib</span>"
                    : "<span class=\"bad\">odenilmeyib</span>";

                sb.Append("<tr>" +
                          $"<td>{Enc(row.NoText)}</td>" +
                          $"<td>{row.Tarix:dd.MM.yyyy}</td>" +
                          $"<td>{row.NetOdenis:N2} AZN</td>" +
                          $"<td>{veziyyet}</td>" +
                          $"<td>{Enc(row.OdenisTarixleri)}</td>" +
                          $"<td>{row.EsasBorclu:N2}</td>" +
                          $"<td>{row.Faiz:N2}</td>" +
                          $"<td>{row.Qaliq:N2}</td>" +
                          $"<td>{Enc(row.GecikmeMetni)}</td>" +
                          "</tr>");
            }

            sb.Append("</table>");
            return sb.ToString();
        }

        /// <summary>👥 Maşın mənfəəti (xeyir) bölgüsü.</summary>
        private static string PartnerSection(IReadOnlyList<PartnerShare> xeyirPaylari)
        {
            if (xeyirPaylari.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.Append("<h2>MAŞIN MƏNFƏƏTİ (XEYİR) BÖLGÜSÜ</h2>");
            sb.Append("<table><tr><th>Tərəfdaş</th><th>Faiz %</th><th>Pay</th></tr>");

            foreach (var s in xeyirPaylari)
            {
                sb.Append($"<tr><td>{Enc(s.Terefdas)}</td><td>{s.Faiz:N2}</td>" +
                          $"<td><b>{s.Mebleg:N2} AZN</b></td></tr>");
            }

            sb.Append("<tr><td colspan=\"2\"><b>CƏMİ</b></td>" +
                      $"<td><b>{xeyirPaylari.Sum(x => x.Mebleg):N2} AZN</b></td></tr></table>");
            return sb.ToString();
        }

        /// <summary>
        /// 💾 Təklif olunan <b>PDF</b> fayl adı ✓:
        /// «Kredit_M0001_Tural_20260925_1645.pdf» ✓
        /// </summary>
        public static string FaylAdi(string musteri, string muqavile)
            => $"Kredit_{Temiz(muqavile)}_{Temiz(musteri)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

        /// <summary>HTML-i fayla yazır və default brauzerdə açır ✓.</summary>
        public static string Save(string html, string musteri, string muqavile)
        {
            var ad = $"Kredit_{Temiz(muqavile)}_{Temiz(musteri)}_{DateTime.Now:yyyyMMdd_HHmm}.html";
            var qovluq = Path.Combine(
                Cas0201.Kok.Qovluq, "AutocodePDF");

            Directory.CreateDirectory(qovluq);
            var yol = Path.Combine(qovluq, ad);

            File.WriteAllText(yol, html, new UTF8Encoding(true));

            Process.Start(new ProcessStartInfo(yol) { UseShellExecute = true });

            return yol;
        }

        /// <summary>
        /// 🩺 <b>EHTİYAT PLAN CƏDVƏLİ</b> ✓✓✓
        /// <para>
        /// VM-dəki ödəniş qrafiki boş gəldikdə kreditin <b>öz məlumatından</b>
        /// (müddət · faiz · kreditləşdirilən) tam plan cədvəli qurulur ✓.
        /// Bu, PDF-də cədvəlin <b>HƏMİŞƏ</b> görünməsini təmin edir ✓.
        /// </para>
        /// </summary>
        private static List<PaymentRow> PlanCedveliQur(Credit credit)
        {
            var cedvel = new List<PaymentRow>();
            var muddet = Math.Max(1, credit.MuddetAy);

            var esas = credit.Kreditlesdirilen;
            var faizCemi = CreditMath.TotalInterest(esas, credit.FaizDerecesi);
            var esasAy = esas / muddet;
            var faizAy = faizCemi / muddet;

            var qaliq = esas + faizCemi;

            for (var i = 1; i <= muddet; i++)
            {
                qaliq -= esasAy + faizAy;

                cedvel.Add(new PaymentRow
                {
                    No = i,
                    Tarix = credit.BaslamaTarixi.AddMonths(i - 1),
                    Odenis = esasAy + faizAy,
                    EsasBorclu = esasAy,
                    Faiz = faizAy,
                    Qaliq = Math.Max(0m, qaliq)
                });
            }

            return cedvel;
        }

        private static string Card(string lbl, string value)
            => $"<div class=\"card\"><span class=\"lbl\">{Enc(lbl)}</span><b>{Enc(value)}</b></div>";

        private static string Enc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        private static string Temiz(string? s)
        {
            var sb = new StringBuilder();

            foreach (var ch in s ?? string.Empty)
            {
                sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
            }

            return sb.Length == 0 ? "sened" : sb.ToString();
        }
    }
}
