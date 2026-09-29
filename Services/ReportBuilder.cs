using System.Globalization;
using System.Text;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Peşəkar MALİYYƏ HESABATI («Açot») qurucusu.
    /// <para>
    /// Hesabat HTML kimi hazırlanır — brauzerdə <b>Ctrl+P → PDF kimi saxla</b>
    /// ilə və ya birbaşa PDF ixracı ilə çıxarıla bilər. Excel (CSV) variantı da var.
    /// </para>
    /// <para>
    /// Bölmələr sıra ilə:
    /// <list type="number">
    ///   <item>Ümumi göstəricilər</item>
    ///   <item>Dövrün maliyyə nəticəsi (gəlir / xərc / mənfəət)</item>
    ///   <item>👥 Tərəfdaş bölgüləri — ADLARI İLƏ ayrı-ayrı</item>
    ///   <item>Satışlar</item>
    ///   <item>Kreditlər</item>
    ///   <item>Xərclər</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ReportBuilder
    {
        /// <summary>Hesabat üçün bütün məlumat (VM tərəfindən doldurulur).</summary>
        public sealed record ReportInput(
            DateTime From,
            DateTime To,
            int TotalCars,
            int SoldCars,
            int ActiveCredits,
            int CompletedCredits,
            decimal AllExpenses,
            decimal CreditPortfolio,
            decimal SalesProfit,
            decimal RangeIncome,
            decimal RangeExpense,
            decimal RangeProfit,
            decimal RangeSalesProfit,
            decimal RangeCostOfSold,
            decimal RangeOfficeExpense,
            decimal RangeCreditPayments,
            decimal RangeCreditProfit,
            decimal RangeStockAdditions,
            int RangeSalesCount,
            IReadOnlyList<Sale> Sales,
            IReadOnlyList<Credit> Credits,
            IReadOnlyList<ExpenseItem> Expenses,
            IReadOnlyList<CreditTransaction> Transactions,
            IReadOnlyList<PartnerTotal> Partners);

        /// <summary>Azərbaycan formatında pul: «1 590,00 ₼».</summary>
        public static string Money(decimal value)
            => $"{value:N2}".Replace(",", " ").Replace(".", ",") + " ₼";

        /// <summary>Azərbaycan formatında tam ədəd: «1 590».</summary>
        public static string Num(decimal value)
            => $"{value:N2}".Replace(",", " ").Replace(".", ",");

        /// <summary>Tarix: «21.09.2026».</summary>
        public static string Date(DateTime value) => value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        /// <summary>HTML üçün təhlükəsiz mətn.</summary>
        public static string Esc(string? text)
            => string.IsNullOrEmpty(text)
                ? "—"
                : text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        // ====================================================================
        //  HTML HESABAT  (çap / PDF üçün hazır)
        // ====================================================================
        public static string BuildHtml(ReportInput d)
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html><html lang=""az""><head><meta charset=""utf-8"">
<title>Maliyyə Hesabatı</title>
<style>
@page { size: A4; margin: 14mm 12mm; }
* { box-sizing: border-box; }
body { font-family: 'Segoe UI', Arial, sans-serif; color: #1e293b; margin: 0; font-size: 11px; line-height: 1.45; }
h1 { font-size: 19px; margin: 0; letter-spacing: .3px; }
h2 { font-size: 13px; margin: 20px 0 8px; padding: 7px 11px; color: #fff;
     background: #1e293b; border-radius: 6px; letter-spacing: .4px; }
.head { border-bottom: 3px solid #1e293b; padding-bottom: 11px; margin-bottom: 6px; display: flex;
        justify-content: space-between; align-items: flex-end; }
.sub { color: #64748b; font-size: 11px; margin-top: 3px; }
.right { text-align: right; color: #64748b; font-size: 10px; }
table { width: 100%; border-collapse: collapse; margin-top: 5px; }
th { background: #f1f5f9; text-align: left; padding: 6px 8px; font-size: 10px;
     text-transform: uppercase; letter-spacing: .4px; color: #475569; border-bottom: 2px solid #cbd5e1; }
td { padding: 5px 8px; border-bottom: 1px solid #e2e8f0; font-size: 11px; }
.num { text-align: right; font-variant-numeric: tabular-nums; }
.row { display: flex; gap: 22px; }
.col { flex: 1; }
.kv { display: flex; justify-content: space-between; padding: 4px 0; border-bottom: 1px dotted #cbd5e1; }
.kv span:last-child { font-weight: 600; }
.total { background: #0f172a; color: #fff !important; font-weight: 700; font-size: 12.5px;
         padding: 9px 11px; border-radius: 6px; display: flex; justify-content: space-between; margin-top: 7px; }
.totals td { font-weight: 700; background: #f8fafc; border-top: 2px solid #94a3b8; }
.pos { color: #047857; font-weight: 600; }
.neg { color: #b91c1c; font-weight: 600; }
.note { font-size: 10px; color: #64748b; margin-top: 4px; }
.foot { margin-top: 22px; padding-top: 9px; border-top: 2px solid #1e293b;
        display: flex; justify-content: space-between; font-size: 10px; color: #64748b; }
.empty { color: #94a3b8; font-style: italic; padding: 7px 0; }
</style></head><body>");

            // ---------------- BAŞLIQ ----------------
            sb.Append("<div class=\"head\"><div>");
            sb.Append("<h1>🚗 AVTOMOBİL PARKI — MALİYYƏ HESABATI</h1>");
            sb.Append($"<div class=\"sub\">Dövr: <b>{Date(d.From)} — {Date(d.To)}</b></div>");
            sb.Append("</div><div class=\"right\">");
            sb.Append($"Hazırlandı: {DateTime.Now:dd.MM.yyyy HH:mm}<br>Avtomobil Parkı v6.0");
            sb.Append("</div></div>");

            // ---------------- 1. ÜMUMİ GÖSTƏRİCİLƏR ----------------
            sb.Append("<h2>1 · ÜMUMİ GÖSTƏRİCİLƏR</h2><div class=\"row\"><div class=\"col\">");
            Kv(sb, "Avtomobil (cəmi)", d.TotalCars.ToString());
            Kv(sb, "Satılan avtomobil", d.SoldCars.ToString());
            Kv(sb, "Aktiv kreditlər", d.ActiveCredits.ToString());
            Kv(sb, "Bağlı kreditlər", d.CompletedCredits.ToString());
            sb.Append("</div><div class=\"col\">");
            Kv(sb, "Ümumi xərc (bütün vaxt)", Money(d.AllExpenses));
            Kv(sb, "Kredit qalıq borcu", Money(d.CreditPortfolio));
            Kv(sb, "Satışlardan mənfəət", Money(d.SalesProfit));
            Kv(sb, "Kredit faiz mənfəəti", Money(d.RangeCreditProfit));
            sb.Append("</div></div>");
            sb.Append("<div class=\"note\">Kredit qalıq borcu = <b>kreditlərin tam qiyməti " +
                      "(əsas borc + faiz)</b> − toplanmış ödənişlər.  Faiz də daxildir.</div>");

            // ---------------- 2. DÖVRÜN NƏTİCƏSİ ----------------
            sb.Append("<h2>2 · DÖVRÜN MALİYYƏ NƏTİCƏSİ</h2>");
            sb.Append("<table><tbody>");
            Row(sb, "Satış gəliri (nağd / köçürmə)", "", Money(d.RangeIncome - d.RangeCreditPayments), "num");
            Row(sb, "Kredit ödənişləri (daxil olan)", "", Money(d.RangeCreditPayments), "num pos");
            Row(sb, "<b>ÜMUMİ GƏLİR</b>", "", Money(d.RangeIncome), "num pos");
            Row(sb, "Satılan malların maya dəyəri", "", Money(d.RangeCostOfSold), "num neg");
            Row(sb, "Ofis / inzibati xərclər", "", Money(d.RangeOfficeExpense), "num neg");
            Row(sb, "Kreditə bağlı əlavə xərclər",
                "", Money(d.RangeExpense - d.RangeCostOfSold - d.RangeOfficeExpense), "num neg");
            Row(sb, "<b>ÜMUMİ XƏRC</b>", "", Money(d.RangeExpense), "num neg");
            sb.Append("</tbody></table>");
            sb.Append($"<div class=\"total\"><span>DÖVR MƏNFƏƏTİ</span><span>{Money(d.RangeProfit)}</span></div>");
            sb.Append($"<div class=\"note\">Rentabellik: <b>{(d.RangeIncome <= 0m ? "—" : $"{d.RangeProfit / d.RangeIncome * 100m:N1}%")}</b> · " +
                      $"Anbara yönəldilən vəsait (XƏRC DEYİL — AKTİV): <b>{Money(d.RangeStockAdditions)}</b> · " +
                      $"Satış sayı: <b>{d.RangeSalesCount}</b> · Brüt satış mənfəəti: <b>{Money(d.RangeSalesProfit)}</b></div>");

            // ---------------- 3. 👥 TƏRƏFDAŞ BÖLGÜLƏRİ ----------------
            sb.Append("<h2>3 · 👥 TƏRƏFDAŞ MƏNFƏƏT BÖLGÜLƏRİ (dövr üzrə)</h2>");

            if (d.Partners.Count == 0)
            {
                sb.Append("<div class=\"empty\">Bu dövrdə tərəfdaş bölgüsü qeydə alınmayıb.</div>");
            }
            else
            {
                sb.Append("<table><thead><tr><th>Tərəfdaş</th><th>Faiz</th><th class=\"num\">Bölgü sayı</th>" +
                          "<th class=\"num\">Məbləğ</th><th class=\"num\">Pay (%)</th></tr></thead><tbody>");

                foreach (var p in d.Partners)
                {
                    var share = d.Partners.Sum(x => x.Mebleg);
                    var faiz = share <= 0m ? 0m : p.Mebleg * 100m / share;

                    sb.Append($"<tr><td><b>{Esc(p.Terefdas)}</b></td><td>{Esc(p.FaizMetni)}</td>" +
                              $"<td class=\"num\">{p.Sayi}</td><td class=\"num\">{Money(p.Mebleg)}</td>" +
                              $"<td class=\"num\">{faiz:N2}%</td></tr>");
                }

                sb.Append($"<tr class=\"totals\"><td colspan=\"3\">CƏMİ</td>" +
                          $"<td class=\"num\">{Money(d.Partners.Sum(p => p.Mebleg))}</td><td class=\"num\">100,00%</td></tr>");
                sb.Append("</tbody></table>");
                sb.Append("<div class=\"note\">Faiz payçıları: Zaur %6 · Eşqin %5 · Asiman %5. " +
                          "Asif və Musa qalan məbləği yarı-yarıya bölürlər.</div>");
            }

            // ---------------- 4. SATIŞLAR ----------------
            sb.Append("<h2>4 · SATIŞLAR</h2>");
            if (d.Sales.Count == 0)
            {
                sb.Append("<div class=\"empty\">Bu dövrdə satış olmayıb.</div>");
            }
            else
            {
                sb.Append("<table><thead><tr><th>№</th><th>Tarix</th><th>Avtomobil</th><th>Müştəri</th>" +
                          "<th class=\"num\">Satış qiyməti</th><th class=\"num\">Maya</th><th class=\"num\">Mənfəət</th></tr></thead><tbody>");

                var i = 0;
                foreach (var s in d.Sales.OrderBy(s => s.SatisTarixi))
                {
                    i++;
                    var name = s.Car is not null ? s.Car.DisplayName : "—";
                    sb.Append($"<tr><td>{i}</td><td>{Date(s.SatisTarixi)}</td><td>{Esc(name)}</td>" +
                              $"<td>{Esc(s.Mustəri)}</td><td class=\"num\">{Money(s.SatisQiymeti)}</td>" +
                              $"<td class=\"num\">{Money(s.MayaDeyeri)}</td>" +
                              $"<td class=\"num {(s.Menfeet >= 0 ? "pos" : "neg")}\">{Money(s.Menfeet)}</td></tr>");
                }

                sb.Append($"<tr class=\"totals\"><td colspan=\"4\">CƏMİ ({i} satış)</td>" +
                          $"<td class=\"num\">{Money(d.Sales.Sum(s => s.SatisQiymeti))}</td>" +
                          $"<td class=\"num\">{Money(d.Sales.Sum(s => s.MayaDeyeri))}</td>" +
                          $"<td class=\"num\">{Money(d.Sales.Sum(s => s.Menfeet))}</td></tr>");
                sb.Append("</tbody></table>");
            }

            // ---------------- 5. KREDİTLƏR ----------------
            sb.Append("<h2>5 · VERİLƏN KREDİTLƏR</h2>");
            if (d.Credits.Count == 0)
            {
                sb.Append("<div class=\"empty\">Bu dövrdə kredit verilməyib.</div>");
            }
            else
            {
                sb.Append("<table><thead><tr><th>Müqavilə</th><th>Müştəri</th><th>Başlama</th>" +
                          "<th class=\"num\">Məbləğ</th><th class=\"num\">İlkin</th><th class=\"num\">Kreditləşdirilən</th>" +
                          "<th class=\"num\">Faiz</th><th class=\"num\">Müddət</th><th class=\"num\">Aylıq</th></tr></thead><tbody>");

                foreach (var c in d.Credits.OrderBy(c => c.BaslamaTarixi))
                {
                    sb.Append($"<tr><td>{Esc(c.MuqavileNomresi)}</td><td>{Esc(c.Mustəri)}</td>" +
                              $"<td>{Date(c.BaslamaTarixi)}</td><td class=\"num\">{Money(c.Mebleg)}</td>" +
                              $"<td class=\"num\">{Money(c.IlkinOdenis)}</td>" +
                              $"<td class=\"num\">{Money(c.Kreditlesdirilen)}</td>" +
                              $"<td class=\"num\">%{Num(c.FaizDerecesi)}</td><td class=\"num\">{c.MuddetAy} ay</td>" +
                              $"<td class=\"num\">{Money(c.AylıqOdenis)}</td></tr>");
                }

                sb.Append($"<tr class=\"totals\"><td colspan=\"3\">CƏMİ ({d.Credits.Count} kredit)</td>" +
                          $"<td class=\"num\">{Money(d.Credits.Sum(c => c.Mebleg))}</td>" +
                          $"<td class=\"num\">{Money(d.Credits.Sum(c => c.IlkinOdenis))}</td>" +
                          $"<td class=\"num\">{Money(d.Credits.Sum(c => c.Kreditlesdirilen))}</td>" +
                          $"<td colspan=\"3\"></td></tr>");
                sb.Append("</tbody></table>");

                var aylıq = d.Credits.Sum(c => c.AylıqOdenis);
                sb.Append($"<div class=\"note\">Aylıq ödəniş düsturu: <b>Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət</b> · " +
                          $"Cəmi aylıq daxilolma: <b>{Money(aylıq)}</b></div>");
            }

            // ---------------- 6. XƏRCLƏR ----------------
            sb.Append("<h2>6 · XƏRCLƏR (qruplar üzrə)</h2>");
            if (d.Expenses.Count == 0)
            {
                sb.Append("<div class=\"empty\">Bu dövrdə xərc qeydə alınmayıb.</div>");
            }
            else
            {
                var cem = d.Expenses.Sum(e => e.Mebleg);

                sb.Append("<table><thead><tr><th>Qrup</th><th class=\"num\">Qeyd sayı</th>" +
                          "<th class=\"num\">Məbləğ</th><th class=\"num\">Pay (%)</th></tr></thead><tbody>");

                var qruplar = d.Expenses
                    .GroupBy(e => string.IsNullOrWhiteSpace(e.Qrup) ? "Digər" : e.Qrup)
                    .Select(g => new { Qrup = g.Key, Say = g.Count(), Mebleg = g.Sum(e => e.Mebleg) })
                    .OrderByDescending(g => g.Mebleg)
                    .ToList();

                foreach (var g in qruplar)
                {
                    var pay = cem <= 0m ? 0m : g.Mebleg * 100m / cem;
                    sb.Append($"<tr><td>{Esc(g.Qrup)}</td><td class=\"num\">{g.Say}</td>" +
                              $"<td class=\"num\">{Money(g.Mebleg)}</td><td class=\"num\">{pay:N2}%</td></tr>");
                }

                sb.Append($"<tr class=\"totals\"><td>CƏMİ ({d.Expenses.Count} qeyd)</td><td></td>" +
                          $"<td class=\"num\">{Money(cem)}</td><td class=\"num\">100,00%</td></tr>");
                sb.Append("</tbody></table>");
            }

            // ================================================================
            //  7 · 🏦 KREDİT PORTFELİ — DƏRİNLİYİNƏ ✓✓✓
            // ================================================================
            var aktivKreditler = d.Credits
                .Where(c => !string.Equals(c.Status, "Bağlı", StringComparison.Ordinal)).ToList();

            var bagliKreditler = d.Credits
                .Where(c => string.Equals(c.Status, "Bağlı", StringComparison.Ordinal)).ToList();

            var esasBorc = aktivKreditler.Sum(c => c.Kreditlesdirilen);
            var avansCemi = d.Credits.Sum(c => c.IlkinOdenis);
            var faizCemi = aktivKreditler.Sum(c => c.FaizMeblegi);
            var portfelBorcu = esasBorc + faizCemi;

            var odenilmis = d.Transactions
                .Where(t => t.Nov == "Gəlir"
                            || (t.Nov == "Gecikmə" && t.Odenilib)
                            || t.Nov == "Vaxtından tez bağlama")
                .Sum(t => t.Mebleg);

            var qaliqBorc = Math.Max(0m, portfelBorcu - odenilmis);

            sb.Append("<h2>7 · 🏦 KREDİT PORTFELİ (DƏRİNLİYİNƏ)</h2><div class=\"row\"><div class=\"col\">");
            Kv(sb, "📐 ƏSAS BORC (kreditləşdirilən)", Money(esasBorc));
            Kv(sb, "📐 FAİZ MƏBLƏĞİ", Money(faizCemi));
            Kv(sb, "📐 PORTFEL BORCU", $"<b>{Money(portfelBorcu)}</b>");
            sb.Append("</div><div class=\"col\">");
            Kv(sb, "💰 İLKİN ÖDƏNİŞLƏR (avanslar)", Money(avansCemi));
            Kv(sb, "✅ ÖDƏNİLMİŞ", $"<b class=\"pos\">{Money(odenilmis)}</b>");
            Kv(sb, "🔻 QALIQ BORC", $"<b class=\"{(qaliqBorc <= 0m ? "pos" : "neg")}\">{Money(qaliqBorc)}</b>");
            sb.Append("</div></div>");
            sb.Append($"<div class=\"note\">📐 DÜSTUR: Portfel borcu = Əsas borc ({Money(esasBorc)}) + Faiz ({Money(faizCemi)})" +
                      $" — aktiv {aktivKreditler.Count} kredit ✓ · ilkin ödəniş portfelə qarışdırılmır ✓</div>");
            sb.Append($"<div class=\"note\">🔻 QALIQ = Portfel borcu ({Money(portfelBorcu)}) − Ödənilmiş ({Money(odenilmis)}) = " +
                      $"<b>{Money(qaliqBorc)}</b> ✓</div>");

            // ================================================================
            //  8 · ✅ BAĞLI / VAXTINDAN TEZ BAĞLANMIŞ KREDİTLƏR ✓✓✓
            // ================================================================
            sb.Append("<h2>8 · ✅ BAĞLI / VAXTINDAN TEZ BAĞLANMIŞ KREDİTLƏR</h2>");

            if (bagliKreditler.Count == 0)
            {
                sb.Append("<div class=\"empty\">Bağlı kredit yoxdur.</div>");
            }
            else
            {
                sb.Append("<table><thead><tr><th>Müqavilə</th><th>Müştəri</th><th>Maşın</th>" +
                          "<th class=\"num\">Məbləğ</th><th class=\"num\">İlkin ödəniş</th>" +
                          "<th class=\"num\">Kreditləşdirilən</th><th class=\"num\">ÖDƏNİLMİŞ</th>" +
                          "</tr></thead><tbody>");

                decimal bagliOdenilmis = 0m;
                decimal bagliAvans = 0m;

                foreach (var k in bagliKreditler.OrderBy(k => k.MuqavileNomresi))
                {
                    var oden = d.Transactions
                        .Where(t => t.CreditId == k.Id
                                    && (t.Nov == "Gəlir" || (t.Nov == "Gecikmə" && t.Odenilib)
                                        || t.Nov == "Vaxtından tez bağlama"))
                        .Sum(t => t.Mebleg);

                    bagliOdenilmis += oden;
                    bagliAvans += k.IlkinOdenis;

                    sb.Append($"<tr><td><b>{Esc(k.MuqavileNomresi)}</b></td><td>{Esc(k.Mustəri)}</td>" +
                              $"<td>{Esc(k.Car?.DisplayName ?? "—")}</td>" +
                              $"<td class=\"num\">{Money(k.Mebleg)}</td>" +
                              $"<td class=\"num\">{Money(k.IlkinOdenis)}</td>" +
                              $"<td class=\"num\">{Money(k.Kreditlesdirilen)}</td>" +
                              $"<td class=\"num\"><b class=\"pos\">{Money(oden)}</b></td></tr>");
                }

                sb.Append($"<tr class=\"totals\"><td>CƏMİ ({bagliKreditler.Count} kredit)</td><td></td><td></td><td></td>" +
                          $"<td class=\"num\">{Money(bagliAvans)}</td><td></td>" +
                          $"<td class=\"num\">{Money(bagliOdenilmis)}</td></tr>");
                sb.Append("</tbody></table>");
                sb.Append("<div class=\"note\">ℹ️ Bu kreditlər portfel BORCUNDAN çıxarılır ✗, amma ödənilmiş pulları " +
                          "və mənfəətləri BURADA tam görünür ✓ (heç nə itmir ✗) ✓</div>");
            }

            // ================================================================
            //  9 · 📅 GECİKMƏ (CƏRİMƏ) — ÖDƏNİLMİŞ / ÖDƏNİLMƏMİŞ ✓✓✓
            // ================================================================
            var gecikmeler = d.Transactions.Where(t => t.Nov == "Gecikmə").ToList();
            var gecikmeCemi = gecikmeler.Sum(t => t.Mebleg);
            var gecikmeOdenilmis = gecikmeler.Where(t => t.Odenilib).Sum(t => t.Mebleg);
            var gecikmeQaliq = Math.Max(0m, gecikmeCemi - gecikmeOdenilmis);

            sb.Append("<h2>9 · 📅 GECİKMƏ (CƏRİMƏ) — ÖDƏNİLMİŞ / ÖDƏNİLMƏMİŞ</h2>");
            sb.Append("<div class=\"row\"><div class=\"col\">");
            Kv(sb, "📋 Gecikmə sayı", gecikmeler.Count.ToString());
            Kv(sb, "💵 Cərimə cəmi", Money(gecikmeCemi));
            sb.Append("</div><div class=\"col\">");
            Kv(sb, "✅ Ödənilmiş", $"<b class=\"pos\">{Money(gecikmeOdenilmis)}</b>");
            Kv(sb, "⏳ ÖDƏNİLMƏMİŞ", $"<b class=\"{(gecikmeQaliq > 0m ? "neg" : "pos")}\">{Money(gecikmeQaliq)}</b>");
            sb.Append("</div></div>");

            if (gecikmeler.Count > 0)
            {
                sb.Append("<table><thead><tr><th>Tarix</th><th>Müqavilə</th><th class=\"num\">Taksit</th>" +
                          "<th class=\"num\">Cərimə</th><th>Vəziyyət</th><th>Təsvir</th></tr></thead><tbody>");

                foreach (var t in gecikmeler.OrderBy(t => t.Tarix))
                {
                    var gk = d.Credits.FirstOrDefault(c => c.Id == t.CreditId);

                    sb.Append($"<tr><td>{Date(t.GecikmeTarixi ?? t.Tarix)}</td>" +
                              $"<td>{Esc(gk?.MuqavileNomresi ?? "—")}</td>" +
                              $"<td class=\"num\">{t.InstallmentNo ?? 0}</td>" +
                              $"<td class=\"num\">{Money(t.Mebleg)}</td>" +
                              $"<td class=\"{(t.Odenilib ? "pos" : "neg")}\">{(t.Odenilib ? "✅ ÖDƏNİLDİ" : "⏳ ÖDƏNİLMƏDİ")}</td>" +
                              $"<td>{Esc(t.Tesvir)}</td></tr>");
                }

                sb.Append("</tbody></table>");
            }
            else
            {
                sb.Append("<div class=\"empty\">Bu dövrdə gecikmə cəriməsi qeydə alınmayıb ✓</div>");
            }

            // ---------------- ALTLIQ ----------------
            sb.Append("<div class=\"foot\"><div>Avtomobil Parkı v6.0 — avtomatik hesabat</div>" +
                      $"<div>{Date(DateTime.Today)}</div></div>");
            sb.Append("</body></html>");

            return sb.ToString();
        }

        /// <summary>Açar-dəyər sətri (iki sütunlu siyahılar üçün).</summary>
        private static void Kv(StringBuilder sb, string key, string value)
            => sb.Append($"<div class=\"kv\"><span>{Esc(key)}</span><span>{value}</span></div>");

        /// <summary>Üç sütunlu cədvəl sətri.</summary>
        private static void Row(StringBuilder sb, string left, string mid, string right, string rightClass)
            => sb.Append($"<tr><td>{left}</td><td>{mid}</td><td class=\"{rightClass}\">{right}</td></tr>");

        // ====================================================================
        //  EXCEL (CSV) HESABAT  —  Excel-də səliqəli açılan bölməli fayl
        // ====================================================================
        public static string BuildCsv(ReportInput d)
        {
            var sb = new StringBuilder();
            const char S = ';';   // Excel (AZ/EU) üçün nöqtəli vergül

            void Xett(params string[] cells) => sb.AppendLine(string.Join(S, cells.Select(C)));
            void Bos() => sb.AppendLine();

            string C(string? v) => (v ?? string.Empty).Replace("\"", "\"\"").Contains(S) || (v ?? "").Contains('\n')
                ? "\"" + (v ?? string.Empty).Replace("\"", "\"\"") + "\""
                : (v ?? string.Empty);

            Xett("AVTOMOBIL PARKI - MALIYYE HESABATI");
            Xett("Dovr", $"{Date(d.From)} - {Date(d.To)}");
            Xett("Hazirlandi", DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
            Bos();

            Xett("1. UMUMI GÖSTƏRİCİLƏR");
            Xett("Göstərici", "Dəyər");
            Xett("Avtomobil (cəmi)", d.TotalCars.ToString());
            Xett("Satılan avtomobil", d.SoldCars.ToString());
            Xett("Aktiv kreditlər", d.ActiveCredits.ToString());
            Xett("Bağlı kreditlər", d.CompletedCredits.ToString());
            Xett("Ümumi xərc (bütün vaxt)", Num(d.AllExpenses));
            Xett("Kredit portfeli", Num(d.CreditPortfolio));
            Xett("Satışlardan mənfəət", Num(d.SalesProfit));
            Bos();

            Xett("2. DÖVRÜN MALİYYƏ NƏTİCƏSİ");
            Xett("Göstərici", "Məbləğ (AZN)");
            Xett("Ümumi gəlir", Num(d.RangeIncome));
            Xett("Kredit ödənişləri", Num(d.RangeCreditPayments));
            Xett("Ümumi xərc", Num(d.RangeExpense));
            Xett("  Satılan malların mayası", Num(d.RangeCostOfSold));
            Xett("  Ofis / inzibati xərclər", Num(d.RangeOfficeExpense));
            Xett("DÖVR MƏNFƏƏTİ", Num(d.RangeProfit));
            Xett("Anbara yönəldilən (aktiv, xərc deyil)", Num(d.RangeStockAdditions));
            Bos();

            Xett("3. TƏRƏFDAŞ MƏNFƏƏT BÖLGÜLƏRİ");
            Xett("Tərəfdaş", "Faiz", "Bölgü sayı", "Məbləğ (AZN)");
            if (d.Partners.Count == 0)
            {
                Xett("Bu dövrdə bölgü yoxdur");
            }
            else
            {
                foreach (var p in d.Partners)
                {
                    Xett(p.Terefdas, p.FaizMetni, p.Sayi.ToString(), Num(p.Mebleg));
                }

                Xett("CƏMİ", "", "", Num(d.Partners.Sum(p => p.Mebleg)));
            }

            Bos();

            Xett("4. SATIŞLAR");
            Xett("№", "Tarix", "Avtomobil", "Müştəri", "Satış qiyməti", "Maya", "Mənfəət");
            var i = 0;
            foreach (var s in d.Sales.OrderBy(x => x.SatisTarixi))
            {
                i++;
                Xett(i.ToString(), Date(s.SatisTarixi), s.Car?.DisplayName ?? "—", s.Mustəri,
                     Num(s.SatisQiymeti), Num(s.MayaDeyeri), Num(s.Menfeet));
            }

            Xett("CƏMİ", "", "", "", Num(d.Sales.Sum(s => s.SatisQiymeti)),
                 Num(d.Sales.Sum(s => s.MayaDeyeri)), Num(d.Sales.Sum(s => s.Menfeet)));
            Bos();

            Xett("5. VERİLƏN KREDİTLƏR");
            Xett("Müqavilə", "Müştəri", "Başlama", "Məbləğ", "İlkin", "Kreditləşdirilən", "Faiz %", "Müddət (ay)", "Aylıq");
            foreach (var c in d.Credits.OrderBy(x => x.BaslamaTarixi))
            {
                Xett(c.MuqavileNomresi, c.Mustəri, Date(c.BaslamaTarixi), Num(c.Mebleg), Num(c.IlkinOdenis),
                     Num(c.Kreditlesdirilen), Num(c.FaizDerecesi), c.MuddetAy.ToString(), Num(c.AylıqOdenis));
            }

            Xett("CƏMİ", "", "", Num(d.Credits.Sum(c => c.Mebleg)), Num(d.Credits.Sum(c => c.IlkinOdenis)),
                 Num(d.Credits.Sum(c => c.Kreditlesdirilen)), "", "", Num(d.Credits.Sum(c => c.AylıqOdenis)));
            Bos();

            Xett("6. XƏRCLƏR");
            Xett("№", "Tarix", "Təyinat", "Qrup", "Kateqoriya", "Avtomobil", "Məbləğ");
            var j = 0;
            foreach (var e in d.Expenses.OrderBy(x => x.Tarix).ThenBy(x => x.Id))
            {
                j++;
                Xett(j.ToString(), Date(e.Tarix), e.Teyinat, e.Qrup, e.Kategoriya,
                     e.Car?.DisplayName ?? "—", Num(e.Mebleg));
            }

            Xett("CƏMİ", "", "", "", "", "", Num(d.Expenses.Sum(e => e.Mebleg)));

            return sb.ToString();
        }
    }
}
