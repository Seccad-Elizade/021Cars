using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc cref="IPartnerService" />
    public sealed class PartnerService : IPartnerService
    {
        private readonly IPartnerRepository _partners;
        private readonly IPartnerPaymentRepository _payments;
        private readonly IPartnerShareRepository _shares;
        private readonly ILogger<PartnerService> _logger;

        /// <summary>
        /// Bölgü panellərinin (Satış / Kredit / Əlavə Gəlir) istifadə etdiyi
        /// tərəfdaş siyahısının keşi. <see cref="PartnerMath.PartnersProvider"/>
        /// bu siyahını SINXRON qaytarır — çünki panellər UI axınında işləyir.
        /// </summary>
        private static IReadOnlyList<Partner> _cached = Array.Empty<Partner>();

        private static readonly object CacheLock = new();

        public PartnerService(
            IPartnerRepository partners,
            IPartnerPaymentRepository payments,
            IPartnerShareRepository shares,
            ILogger<PartnerService> logger)
        {
            _partners = partners;
            _payments = payments;
            _shares = shares;
            _logger = logger;
        }

        // ====================================================================
        //  KEŞ — bütün bölgü panelləri eyni siyahını görsün
        // ====================================================================

        /// <summary>Bazadan tərəfdaşları oxuyub keşi yeniləyir.</summary>
        private async Task<IReadOnlyList<Partner>> RefreshCacheAsync(CancellationToken cancellationToken)
        {
            var list = await _partners.GetOrderedAsync(cancellationToken);

            lock (CacheLock)
            {
                _cached = list;
                PartnerMath.PartnersProvider = () => _cached;
            }

            return list;
        }

        /// <summary>
        /// Keşi bazadan yenidən oxuyur — tətbiq açılışında bir dəfə çağırılır.
        /// </summary>
        public async Task RefreshProviderAsync(CancellationToken cancellationToken = default)
        {
            await RefreshCacheAsync(cancellationToken);

            _logger.LogInformation("👥 Tərəfdaş siyahısı yükləndi: {Sayi} nəfər", _cached.Count);
        }

        // ====================================================================
        //  TƏRƏFDAŞ İDARƏSİ
        // ====================================================================

        /// <inheritdoc />
        public async Task<IReadOnlyList<Partner>> GetPartnersAsync(
            CancellationToken cancellationToken = default)
            => await RefreshCacheAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<Partner> AddPartnerAsync(
            string ad,
            decimal faiz,
            bool qaligPayi,
            string qeyd = "",
            CancellationToken cancellationToken = default)
        {
            var name = (ad ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Tərəfdaşın adı boş ola bilməz.");
            }

            if (await _partners.GetByNameAsync(name, cancellationToken) is not null)
            {
                throw new InvalidOperationException($"«{name}» adlı tərəfdaş artıq mövcuddur.");
            }

            var hamisi = await _partners.GetOrderedAsync(cancellationToken);

            var partner = new Partner
            {
                Ad = name,
                Faiz = qaligPayi ? 0m : Math.Max(0m, faiz),
                QaligPayi = qaligPayi,
                Aktiv = true,
                Qeyd = (qeyd ?? string.Empty).Trim(),
                Sira = hamisi.Count == 0 ? 0 : hamisi.Max(p => p.Sira) + 1
            };

            await _partners.AddAsync(partner, cancellationToken);
            await _partners.SaveChangesAsync(cancellationToken);

            var list = await RefreshCacheAsync(cancellationToken);

            _logger.LogInformation("👥 Yeni tərəfdaş əlavə olundu: {Ad} — {Faiz}%", partner.Ad, partner.Faiz);

            return list.FirstOrDefault(p => p.Ad == name) ?? partner;
        }

        /// <inheritdoc />
        public async Task UpdatePartnerAsync(
            Partner partner,
            CancellationToken cancellationToken = default)
        {
            var tracked = await _partners.GetByIdAsync(partner.Id, cancellationToken);

            if (tracked is null)
            {
                throw new InvalidOperationException("Tərəfdaş bazada tapılmadı.");
            }

            tracked.Ad = (partner.Ad ?? string.Empty).Trim();
            tracked.Faiz = partner.QaligPayi ? 0m : Math.Max(0m, partner.Faiz);
            tracked.QaligPayi = partner.QaligPayi;
            tracked.Aktiv = partner.Aktiv;
            tracked.Qeyd = (partner.Qeyd ?? string.Empty).Trim();

            _partners.Update(tracked);
            await _partners.SaveChangesAsync(cancellationToken);
            await RefreshCacheAsync(cancellationToken);

            _logger.LogInformation("👥 Tərəfdaş yeniləndi: {Ad}", tracked.Ad);
        }

        /// <inheritdoc />
        public async Task DeletePartnerAsync(int partnerId, CancellationToken cancellationToken = default)
        {
            var tracked = await _partners.GetByIdAsync(partnerId, cancellationToken);

            if (tracked is null)
            {
                return;
            }

            _partners.Remove(tracked);
            await _partners.SaveChangesAsync(cancellationToken);
            await RefreshCacheAsync(cancellationToken);

            _logger.LogInformation(
                "👥 Tərəfdaş silindi: {Ad} (keçmiş bölgülər tarixçədə saxlanılır)", tracked.Ad);
        }

        /// <inheritdoc />
        public async Task SavePartnersAsync(
            IEnumerable<Partner> partners,
            CancellationToken cancellationToken = default)
        {
            var sayi = 0;

            foreach (var item in partners)
            {
                var tracked = await _partners.GetByIdAsync(item.Id, cancellationToken);

                if (tracked is null)
                {
                    continue;
                }

                tracked.Ad = (item.Ad ?? string.Empty).Trim();
                tracked.Faiz = item.QaligPayi ? 0m : Math.Max(0m, item.Faiz);
                tracked.QaligPayi = item.QaligPayi;
                tracked.Aktiv = item.Aktiv;
                tracked.Qeyd = (item.Qeyd ?? string.Empty).Trim();

                _partners.Update(tracked);
                sayi++;
            }

            await _partners.SaveChangesAsync(cancellationToken);
            await RefreshCacheAsync(cancellationToken);

            _logger.LogInformation("👥 {Sayi} tərəfdaşın faizi yadda saxlanıldı.", sayi);
        }

        // ====================================================================
        //  ÖDƏNİŞLƏR (XARİC EDİLƏN PULLAR)
        // ====================================================================

        /// <inheritdoc />
        public Task<IReadOnlyList<PartnerPayment>> GetPaymentsAsync(
            CancellationToken cancellationToken = default)
            => _payments.GetOrderedAsync(cancellationToken);

        /// <inheritdoc />
        public async Task AddPaymentAsync(
            PartnerPayment payment,
            CancellationToken cancellationToken = default)
        {
            payment.Terefdas = (payment.Terefdas ?? string.Empty).Trim();
            payment.OdenisUsulu = (payment.OdenisUsulu ?? string.Empty).Trim();
            payment.Qeyd = (payment.Qeyd ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(payment.Terefdas))
            {
                throw new InvalidOperationException("Ödəniş üçün tərəfdaş seçilməlidir.");
            }

            if (payment.Mebleg <= 0m)
            {
                throw new InvalidOperationException("Ödəniş məbləği sıfırdan böyük olmalıdır.");
            }

            await _payments.AddAsync(payment, cancellationToken);
            await _payments.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "💸 Tərəfdaşa pul xaric edildi: {Ad} — {Mebleg:N2} ₼ ({Tarix:dd.MM.yyyy})",
                payment.Terefdas, payment.Mebleg, payment.Tarix);
        }

        /// <inheritdoc />
        public async Task DeletePaymentAsync(int paymentId, CancellationToken cancellationToken = default)
        {
            var tracked = await _payments.GetByIdAsync(paymentId, cancellationToken);

            if (tracked is null)
            {
                return;
            }

            _payments.Remove(tracked);
            await _payments.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("💸 Ödəniş silindi: #{Id}", paymentId);
        }

        // ====================================================================
        //  ANALİTİKA — BÜTÜN MƏNBƏLƏRDƏN BÖLGÜ TOPLAMA
        // ====================================================================

        /// <summary>Bir payın mənbə açarı: hansı bölgüyə aid olduğu.</summary>
        private static string SourceKey(PartnerShare s) =>
            s.SaleId is not null ? $"S:{s.SaleId}"
            : s.CreditId is not null ? $"C:{s.CreditId}"
            : $"T:{s.CreditTransactionId}";

        /// <summary>Payın mənbə məlumatı: menbe, maşın, müqavilə, müştəri, tarix.</summary>
        private sealed record ShareInfo(
            string Menbe,
            string Avtomobil,
            string Muqavile,
            string Musteri,
            DateTime Tarix);

        /// <summary>Avtomobilin cədvəl adı: «Mercedes C300 · 77HH717».</summary>
        private static string CarName(CarItem? car)
        {
            if (car is null)
            {
                return "—";
            }

            var name = car.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? "—" : name;
        }

        /// <summary>Payın mənbəyini (satış / kredit / əlavə gəlir) təyin edir.</summary>
        private static ShareInfo InfoFor(PartnerShare s)
        {
            // ---------------- 💰 SATIŞ ----------------
            if (s.SaleId is not null)
            {
                var sale = s.Sale;
                return sale is null
                    ? new ShareInfo("Satış", "—", string.Empty, string.Empty, DateTime.Today)
                    : new ShareInfo(
                        "Satış",
                        CarName(sale.Car),
                        sale.MuqavileNomresi,
                        sale.Mustəri,
                        sale.SatisTarixi);
            }

            // ---------------- 💳 KREDİT ----------------
            if (s.CreditId is not null)
            {
                var credit = s.Credit;
                return credit is null
                    ? new ShareInfo("Kredit", "—", string.Empty, string.Empty, DateTime.Today)
                    : new ShareInfo(
                        "Kredit",
                        CarName(credit.Car),
                        credit.MuqavileNomresi,
                        credit.Mustəri,
                        credit.BaslamaTarixi);
            }

            // ---------------- 🏷️ KREDİT ƏLAVƏ GƏLİR ----------------
            var transaction = s.CreditTransaction;

            if (transaction is null)
            {
                return new ShareInfo("Kredit Əlavə Gəlir", "—", string.Empty, string.Empty, DateTime.Today);
            }

            var txCredit = transaction.Credit;

            // ================================================================
            //  ⚠️ GECİKMƏ — AYRI MƏNBƏ ✓✓✓
            // ----------------------------------------------------------------
            //  Əvvəl BÜTÜN əməliyyat payları `GosterilenTarix` (yəni PLAN
            //  tarixi ✗) ilə filtrələnirdi → «Tərəfdaşlar → Dövr aralığı»
            //  seçiləndə gecikmə qeydləri siyahıdan **SİLİNİRDİ** ✗✓✓
            //
            //  İNDİ: gecikmə REAL tarix üzrə düşür ✓
            //    → ödənilibsə: `GecikmeTarixi` (pulun gəldiyi gün ✓)
            //    → ödənilməyibsə: `Tarix` (qeydin yazıldığı gün ✓)
            //  Beləliklə gecikmələr dövrdən ASILI OLMAYARAQ görünür ✓
            // ================================================================
            var gecikme = transaction.Nov == "Gecikmə";

            return new ShareInfo(
                gecikme ? "Gecikmə" : "Kredit Əlavə Gəlir",
                CarName(txCredit?.Car),
                txCredit?.MuqavileNomresi ?? string.Empty,
                txCredit?.Mustəri ?? string.Empty,
                gecikme
                    ? transaction.GecikmeTarixi ?? transaction.Tarix
                    : transaction.GosterilenTarix);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerLedgerRow>> BuildLedgerAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            var shares = await _shares.GetAllDetailedAsync(cancellationToken);

            // Yalnız MƏNBƏYƏ bağlı (satış / kredit / əməliyyat) paylar və
            // seçilmiş DÖVRƏ düşənlər.
            var ugunlu = shares
                .Where(s => s.SaleId is not null || s.CreditId is not null || s.CreditTransactionId is not null)
                .Select(s => (Share: s, Info: InfoFor(s)))
                // ================================================================
                //  ⚠️ GECİKMƏ CƏRİMƏSİ TƏRƏFDAŞ JURNALINA HEÇ VAXT DAXİL EDİLMİR ✗✓✓
                // ----------------------------------------------------------------  (v6.2.28)
                //  Cərimə pulu YALNIZ «💵 Kassa»ya (gəlirə) yazılır ✓✓✓
                //  (bax: <see cref="KassaHesabi"/> — «Gecikmə + Ödənilib» ✓)
                //
                //  • Tərəfdaşlara BÖLÜNMÜR ✗ → kartlarda, bölgü jurnalında,
                //    «Qazanılmış» hesabında GÖRÜNMÜR ✗
                //  • Köhnə versiyalarda yazılmış 50/50 paylar bazada qalmış olsa da
                //    bu filtr onları DƏRHAL sıradan çıxarır ✓ → self-healing ✓✓✓
                // ================================================================
                .Where(x => !string.Equals(x.Info.Menbe, "Gecikmə", StringComparison.Ordinal))
                .Where(x => x.Info.Tarix.Date >= from.Date && x.Info.Tarix.Date <= to.Date)
                .ToList();

            var jurnal = new List<PartnerLedgerRow>();

            // Bir QRU P = bir BÖLGÜ (bir maşın / bir əməliyyat).
            foreach (var grup in ugunlu.GroupBy(x => SourceKey(x.Share)))
            {
                // Baza = həmin bölgüdə faktiki PAYLANAN məbləğ (iştirak edənlərin cəmi).
                var aktivler = grup.Where(x => x.Share.Aktiv).OrderBy(x => x.Share.Sira).ToList();

                if (aktivler.Count == 0)
                {
                    continue;
                }

                var baza = aktivler.Sum(x => x.Share.Mebleg);
                var info = grup.First().Info;

                foreach (var (share, _) in aktivler)
                {
                    jurnal.Add(new PartnerLedgerRow
                    {
                        Menbe = info.Menbe,
                        Terefdas = share.Terefdas,
                        Avtomobil = info.Avtomobil,
                        MuqavileNomresi = info.Muqavile,
                        Musteri = info.Musteri,
                        Tarix = info.Tarix,
                        Baza = baza,
                        Faiz = share.Faiz,
                        QaligPayi = share.QaligPayi,
                        Mebleg = share.Mebleg
                    });
                }
            }

            return jurnal
                .OrderByDescending(r => r.Tarix)
                .ThenBy(r => r.Menbe)
                .ThenBy(r => r.Terefdas)
                .ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerCardRow>> BuildCardsAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            var jurnal = await BuildLedgerAsync(from, to, cancellationToken);
            var odenisler = await _payments.GetInRangeAsync(from, to, cancellationToken);
            var terefdaslar = await _partners.GetOrderedAsync(cancellationToken);

            // Bütün adlar: baza tərəfdaşları + jurnalda görünənlər + ödənişlər.
            var adlar = terefdaslar.Select(p => p.Ad)
                .Concat(jurnal.Select(r => r.Terefdas))
                .Concat(odenisler.Select(p => p.Terefdas))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var kartlar = new List<PartnerCardRow>();

            foreach (var ad in adlar)
            {
                var terefdas = terefdaslar.FirstOrDefault(
                    p => string.Equals(p.Ad, ad, StringComparison.OrdinalIgnoreCase));

                var paylar = jurnal
                    .Where(r => string.Equals(r.Terefdas, ad, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var odenis = odenisler
                    .Where(p => string.Equals(p.Terefdas, ad, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Dövriyyə ayları (təkrarsız, sıralı).
                var aylar = paylar
                    .Select(r => new DateTime(r.Tarix.Year, r.Tarix.Month, 1))
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                kartlar.Add(new PartnerCardRow
                {
                    Terefdas = ad,
                    Faiz = terefdas?.Faiz ?? 0m,
                    QaligPayi = terefdas?.QaligPayi ?? false,
                    Aktiv = terefdas?.Aktiv ?? false,
                    SatisQazanc = paylar.Where(r => r.Menbe == "Satış").Sum(r => r.Mebleg),
                    KreditQazanc = paylar.Where(r => r.Menbe == "Kredit").Sum(r => r.Mebleg),
                    // ================================================================
                    //  ✅ GECİKMƏ CƏRİMƏSİ PAYLARI «QAZANILMIŞ»A ƏLAVƏ OLUNUR ✓✓✓
                    // ----------------------------------------------------------------
                    //  ⚠ ƏVVƏLKİ SƏHV: yalnız «Kredit Əlavə Gəlir» mənbəyi
                    //  sayılırdı ✗ → «Gecikmə» payları (məs. Asif 50 ₼ ·
                    //  Musa 50 ₼) KARTLARDA GÖRÜNMÜRDÜ ✗✓✓
                    //  (jurnalda və aylıq dövriyyədə isə görünürdü ✓)
                    //
                    //  Nəticə: kart 523,04 ₼ qalırdı ✗ → İNDİ 573,04 ₼ ✓✓✓
                    // ================================================================
                    GelirQazanc = paylar
                        .Where(r => r.Menbe == "Kredit Əlavə Gəlir"
                                    || r.Menbe == "Gecikmə")
                        .Sum(r => r.Mebleg),
                    Verilmis = odenis.Sum(p => p.Mebleg),
                    BolguSayi = paylar.Count,
                    AktivAySayi = aylar.Count,
                    IlkAy = aylar.Count == 0 ? "—" : PartnerMonthlyRow.AyAdi(aylar[0]),
                    SonAy = aylar.Count == 0 ? "—" : PartnerMonthlyRow.AyAdi(aylar[^1])
                });
            }

            return kartlar
                .OrderByDescending(c => c.Qazanilmis)
                .ThenBy(c => c.Terefdas)
                .ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerMonthlyRow>> BuildMonthlyAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            var jurnal = await BuildLedgerAsync(from, to, cancellationToken);
            var odenisler = await _payments.GetInRangeAsync(from, to, cancellationToken);

            var siyahi = new List<PartnerMonthlyRow>();

            // 1) Bölgülərdən gələn qazanclar (bir ay = bir sətir).
            foreach (var grup in jurnal.GroupBy(r => (r.Terefdas, r.Tarix.Year, r.Tarix.Month)))
            {
                siyahi.Add(new PartnerMonthlyRow
                {
                    Terefdas = grup.Key.Terefdas,
                    Il = grup.Key.Year,
                    Ay = grup.Key.Month,
                    BolguSayi = grup.Count(),
                    Qazanilmis = grup.Sum(r => r.Mebleg)
                });
            }

            // 2) Ödənişlər — eyni aya düşənləri birləşdirir, yeni ay yaradır.
            foreach (var grup in odenisler.GroupBy(p => (p.Terefdas, p.Tarix.Year, p.Tarix.Month)))
            {
                var movcud = siyahi.FirstOrDefault(r =>
                    string.Equals(r.Terefdas, grup.Key.Terefdas, StringComparison.OrdinalIgnoreCase) &&
                    r.Il == grup.Key.Year &&
                    r.Ay == grup.Key.Month);

                if (movcud is null)
                {
                    siyahi.Add(new PartnerMonthlyRow
                    {
                        Terefdas = grup.Key.Terefdas,
                        Il = grup.Key.Year,
                        Ay = grup.Key.Month,
                        BolguSayi = 0,
                        Qazanilmis = 0m,
                        Odenilmis = grup.Sum(p => p.Mebleg)
                    });
                }
                else
                {
                    movcud.Odenilmis = grup.Sum(p => p.Mebleg);
                }
            }

            return siyahi
                .OrderByDescending(r => r.Il)
                .ThenByDescending(r => r.Ay)
                .ThenBy(r => r.Terefdas)
                .ToList();
        }

        /// <inheritdoc />
        public async Task<PartnerPortfolioTotals> BuildTotalsAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            var jurnal = await BuildLedgerAsync(from, to, cancellationToken);
            var odenisler = await _payments.GetInRangeAsync(from, to, cancellationToken);

            var terefdasSayi = jurnal.Select(r => r.Terefdas)
                .Concat(odenisler.Select(p => p.Terefdas))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            return new PartnerPortfolioTotals(
                jurnal.Sum(r => r.Mebleg),
                odenisler.Sum(p => p.Mebleg),
                jurnal.Count,
                terefdasSayi);
        }
    }
}
