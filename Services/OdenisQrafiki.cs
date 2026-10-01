using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🔔 <b>ÖDƏNİŞ BİLDİRİŞLƏRİNİN VAHİD HESABLANMASI</b> ✓✓✓ — saf məntiq ✓
    /// <para>
    /// <b>Nə üçün ortaq sinif?</b> — «🔔 Bildirişlər» ✓ və «🗓️ Ödəniş Təqvimi» ✓
    /// tabları (gələcəkdə Veb səhifəsi də ✓) <b>EYNİ rəqəmləri</b> göstərsin ✓✓✓
    /// (düstur TƏKRARLANMIR ✗)
    /// </para>
    /// <para><b>MƏNBƏLƏR:</b></para>
    /// <list type="bullet">
    ///   <item>🏦 <b>Kredit taksitləri</b> — <c>Aktiv</c> kreditlərin aylıq ödənişləri ✓
    ///         (ödənilib? → <c>Nov = "Gəlir"</c> + <c>InstallmentNo</c> ✓)</item>
    ///   <item>⏳ <b>Möhlətlər</b> — <c>OdenisMohlet</c> sətirləri (ilkin ödəniş ✓ nisyə satış ✓)</item>
    ///   <item>⚠️ <b>Gecikmə cərimələri</b> — ödənilməmiş <c>Nov = "Gecikmə"</c> ✓</item>
    /// </list>
    /// <para>
    /// ⚙️ Kanonik taksit cədvəli yenə «📅 Kredit Ödəniş Qrafiki» tabındadır ✓
    /// (barter/transfer çıxmaları ilə ✓) — burada <b>bildiriş</b> üçün sadə plan
    /// işlədilir: <c>başlama tarixi + (taksit − 1) ay</c> · <c>aylıq ödəniş</c> ✓
    /// </para>
    /// </summary>
    public static class OdenisQrafiki
    {
        /// <summary>Kredit «Aktiv» sayılır? (statusu «Bağlı» DEYİLSƏ ✓)</summary>
        public static bool AktivKredit(Credit kredit)
            => !string.Equals(kredit.Status?.Trim(), "Bağlı", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 📅 Tarixə uyğun taksit nömrəsi ✓ (1-ci taksit = başlama tarixinin özü ✓)
        /// — «Kreditlər» tabındaki qayda ilə EYNİ ✓✓✓
        /// </summary>
        public static int AytaksitNo(DateTime tarix, DateTime baslama)
            => (((tarix.Year - baslama.Year) * 12) + (tarix.Month - baslama.Month)) + 1;

        /// <summary>
        /// 🔔 <b>BÜTÜN ÖDƏNİŞ BİLDİRİŞLƏRİ</b> ✓✓✓ — tarixə görə sıralanır ✓
        /// </summary>
        /// <param name="satislar">
        /// Nisyə satışlar ✓ (möhlətlərin müştəri/maşın məlumatı üçün ✓ — <c>null</c> ola bilər ✓)
        /// </param>
        /// <param name="odenilmisDaxil">
        /// Ödənilmişlər də daxil olsun? (<c>true</c> = hamısı ✓ · <c>false</c> = yalnız gözlənilən ✓)
        /// </param>
        public static List<OdenisBildirisi> Qur(
            IReadOnlyList<Credit> kreditler,
            IReadOnlyList<CreditTransaction> emeliyyatlar,
            IReadOnlyList<OdenisMohlet> mohletler,
            IReadOnlyList<Sale>? satislar = null,
            bool odenilmisDaxil = true)
        {
            var netice = new List<OdenisBildirisi>();

            var satisXeritesi = (satislar ?? Array.Empty<Sale>())
                .GroupBy(s => s.Id)
                .ToDictionary(g => g.Key, g => g.First());

            var kreditXeritesi = kreditler
                .GroupBy(k => k.Id)
                .ToDictionary(g => g.Key, g => g.First());

            // ================================================================
            //  ① 🏦 KREDİT TAKSİTLƏRİ ✓✓✓ — aktiv kreditlərin aylıq ödənişləri
            // ================================================================
            foreach (var kredit in kreditler.Where(AktivKredit))
            {
                if (kredit.MuddetAy <= 0 || kredit.AylıqOdenis <= 0m)
                {
                    continue;
                }

                // 📅 Hansı taksitlər ÖDƏNİLİB? (Nov = "Gəlir" ✓)
                var odenilmisTaksitler = new HashSet<int>();

                foreach (var hereket in emeliyyatlar.Where(t =>
                             t.CreditId == kredit.Id
                             && string.Equals(t.Nov, "Gəlir", StringComparison.OrdinalIgnoreCase)))
                {
                    var no = hereket.InstallmentNo ?? AytaksitNo(hereket.Tarix, kredit.BaslamaTarixi);

                    if (no >= 1)
                    {
                        odenilmisTaksitler.Add(no);
                    }
                }

                for (var i = 1; i <= kredit.MuddetAy; i++)
                {
                    var odenilib = odenilmisTaksitler.Contains(i);

                    if (odenilib && !odenilmisDaxil)
                    {
                        continue;
                    }

                    netice.Add(new OdenisBildirisi
                    {
                        Tarix = kredit.BaslamaTarixi.AddMonths(i - 1).Date,
                        Menbe = "Kredit",
                        Nov = "🏦 Kredit taksiti",
                        Muqavile = kredit.MuqavileNomresi ?? string.Empty,
                        Mustar = kredit.Mustəri ?? string.Empty,
                        Avtomobil = kredit.Car?.DisplayName ?? kredit.DisplayText,
                        TaksitNo = i,
                        Mebleg = kredit.AylıqOdenis,
                        Odenilib = odenilib,
                        Qeyd = $"{i}/{kredit.MuddetAy} taksit"
                    });
                }
            }

            // ================================================================
            //  ② ⏳ MÖHLƏTLƏR ✓✓✓ — «X günə qalıb / vaxtı keçib» ✓
            // ================================================================
            foreach (var mohlet in mohletler)
            {
                if (mohlet.Mebleg <= 0m)
                {
                    continue;
                }

                if (mohlet.Odenilib && !odenilmisDaxil)
                {
                    continue;
                }

                var nisyeSatis = mohlet.Menbe == OdenisMohlet.MenbeSatis;

                var muqavile = string.Empty;
                var mustar = string.Empty;
                var avtomobil = string.Empty;

                if (nisyeSatis && mohlet.SaleId is int satisId && satisXeritesi.TryGetValue(satisId, out var satis))
                {
                    muqavile = satis.MuqavileNomresi ?? string.Empty;
                    mustar = satis.Mustəri ?? string.Empty;
                    avtomobil = satis.Car?.DisplayName ?? string.Empty;
                }
                else if (mohlet.CreditId is int kreditId && kreditXeritesi.TryGetValue(kreditId, out var kredit))
                {
                    muqavile = kredit.MuqavileNomresi ?? string.Empty;
                    mustar = kredit.Mustəri ?? string.Empty;
                    avtomobil = kredit.Car?.DisplayName ?? kredit.DisplayText;
                }

                netice.Add(new OdenisBildirisi
                {
                    Tarix = mohlet.Tarix.Date,
                    Menbe = "Möhlət",
                    Nov = nisyeSatis ? "🛒 Satış (nisyə) möhləti" : "⏳ İlkin ödəniş möhləti",
                    Muqavile = muqavile,
                    Mustar = mustar,
                    Avtomobil = avtomobil,
                    Mebleg = mohlet.Mebleg,
                    Odenilib = mohlet.Odenilib,
                    Qeyd = string.IsNullOrWhiteSpace(mohlet.Qeyd) ? "möhlətə verilib" : mohlet.Qeyd
                });
            }

            // ================================================================
            //  ③ ⚠️ GECİKMƏ CƏRİMƏLƏRİ (yalnız ÖDƏNİLMƏMİŞ ✓)
            // ================================================================
            foreach (var cekmek in emeliyyatlar.Where(t =>
                         string.Equals(t.Nov, "Gecikmə", StringComparison.OrdinalIgnoreCase)
                         && !t.Odenilib))
            {
                if (cekmek.CreditId is not int kreditId
                    || !kreditXeritesi.TryGetValue(kreditId, out var kredit))
                {
                    continue;
                }

                netice.Add(new OdenisBildirisi
                {
                    Tarix = cekmek.Tarix.Date,
                    Menbe = "Cərimə",
                    Nov = "⚠ Gecikmə cəriməsi",
                    Muqavile = kredit.MuqavileNomresi ?? string.Empty,
                    Mustar = kredit.Mustəri ?? string.Empty,
                    Avtomobil = kredit.Car?.DisplayName ?? kredit.DisplayText,
                    Mebleg = cekmek.Mebleg,
                    Odenilib = false,
                    Qeyd = string.IsNullOrWhiteSpace(cekmek.Tesvir) ? "gecikmə cəriməsi" : cekmek.Tesvir
                });
            }

            return netice
                .OrderBy(b => b.Tarix)
                .ThenBy(b => b.Mustar)
                .ThenBy(b => b.TaksitNo)
                .ToList();
        }

        /// <summary>
        /// 🗓️ <b>AYLAR ÜZRƏ XÜLASƏ</b> ✓✓✓ — «hansı ay hansı maşınlar pul verəcək» ✓
        /// <para>Yalnız məlumatı olan aylar qaytarılır ✓ (tarixə görə sıralı ✓)</para>
        /// </summary>
        public static List<AyOdenisCemi> Aylar(IEnumerable<OdenisBildirisi> setirler)
        {
            var aylar = new List<AyOdenisCemi>();

            foreach (var qrup in setirler
                         .GroupBy(b => new DateTime(b.Tarix.Year, b.Tarix.Month, 1))
                         .OrderBy(g => g.Key))
            {
                var siyahi = qrup
                    .OrderBy(b => b.Tarix)
                    .ThenBy(b => b.Mustar)
                    .ToList();

                aylar.Add(new AyOdenisCemi
                {
                    Ay = qrup.Key,
                    AyAdi = PartnerMonthlyRow.AyAdi(qrup.Key),
                    Cemi = siyahi.Sum(b => b.Mebleg),
                    Odenilmis = siyahi.Where(b => b.Odenilib).Sum(b => b.Mebleg),
                    Qaliq = siyahi.Where(b => !b.Odenilib).Sum(b => b.Mebleg),
                    Setirler = siyahi
                });
            }

            return aylar;
        }

        /// <summary>
        /// 🚘 <b>BİR AYIN AVTOMOBİLLƏRİ</b> ✓✓✓ — «hansı maşın nə qədər ödəyəcək» ✓
        /// <para>Ödəniş məbləğinə görə böyükdən kiçiyə sıralanır ✓</para>
        /// </summary>
        public static List<AvtomobilOdenisi> Avtomobiller(AyOdenisCemi ay)
        {
            var netice = new List<AvtomobilOdenisi>();

            foreach (var qrup in ay.Setirler
                         .GroupBy(b => string.IsNullOrWhiteSpace(b.Avtomobil) ? "—" : b.Avtomobil)
                         .OrderByDescending(g => g.Sum(b => b.Mebleg)))
            {
                var ilk = qrup.First();

                netice.Add(new AvtomobilOdenisi
                {
                    Avtomobil = qrup.Key,
                    Mustar = ilk.Mustar,
                    Muqavile = ilk.Muqavile,
                    Mebleg = qrup.Sum(b => b.Mebleg),
                    Odenilib = qrup.All(b => b.Odenilib),
                    SetirSayi = qrup.Count(),
                    Tarixler = string.Join(" · ", qrup
                        .Select(b => b.Tarix)
                        .Distinct()
                        .OrderBy(d => d)
                        .Select(d => d.ToString("dd.MM")))
                });
            }

            return netice;
        }
    }

    /// <summary>🚘 Bir avtomobilin bir aydaki ödəniş xülasəsi (təqvim üçün ✓).</summary>
    public sealed class AvtomobilOdenisi
    {
        public string Avtomobil { get; init; } = "—";
        public string Mustar { get; init; } = string.Empty;
        public string Muqavile { get; init; } = string.Empty;
        public decimal Mebleg { get; init; }
        public bool Odenilib { get; init; }
        public int SetirSayi { get; init; }

        /// <summary>Ödəniş günləri: «05.09 · 05.10» ✓.</summary>
        public string Tarixler { get; init; } = string.Empty;

        /// <summary>«1 378,00 ₼».</summary>
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        public string Veziyyet => Odenilib ? "✅ Ödənilib" : "🕓 Gözlənilir";

        public string VeziyyetRengi => Odenilib ? "#34D399" : "#FBBF24";

        /// <summary>Cədvəldə izahat: «100,00 ₼ · 05.09» ✓.</summary>
        public string Aciqlama => $"{MeblegMetni}" + (string.IsNullOrWhiteSpace(Tarixler) ? "" : $" · {Tarixler}");

        public override string ToString() => $"{Avtomobil} · {Mustar} · {MeblegMetni}";
    }
}
