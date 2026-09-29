using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Data
{
    /// <summary>
    /// Tətbiq işə düşərkən verilənlər bazasını hazırlayır:
    /// mövcud miqrasiyaları tətbiq edir (yoxdursa sxemi yaradır) və seed məlumatlarını yazır.
    /// </summary>
    public sealed class DbInitializer
    {
        private readonly AppDbContext _context;
        private readonly ICarService _carService;
        private readonly ICreditService _creditService;
        private readonly ILogger<DbInitializer> _logger;

        public DbInitializer(
            AppDbContext context,
            ICarService carService,
            ICreditService creditService,
            ILogger<DbInitializer> logger)
        {
            _context = context;
            _carService = carService;
            _creditService = creditService;
            _logger = logger;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var hasMigrations = _context.Database.GetMigrations().Any();

            if (hasMigrations)
            {
                _logger.LogInformation("Tətbiq olunmamış miqrasiyalar bazaya tətbiq edilir.");
                await _context.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Miqrasiya tapılmadı, sxem EnsureCreated ilə yaradılır.");
                await _context.Database.EnsureCreatedAsync(cancellationToken);
            }

            await DatabaseSeeder.SeedAsync(_context, cancellationToken);
            await SeedExpenseCatalogAsync(cancellationToken);
            await SeedPartnersAsync(cancellationToken);
            await BackfillSiraNomresiAsync(cancellationToken);
            await CleanupOrphansAsync(cancellationToken);
            await RecalcCreditMonthlyAsync(cancellationToken);
            await CloseFinishedCreditsAsync(cancellationToken);
            LogPartnerMathSelfCheck();
            LogCreditMathSelfCheck();
            _logger.LogInformation("Verilənlər bazası uğurla hazırlandı.");
        }

        /// <summary>
        /// 🎯 <b>TAM ÖDƏNİLMİŞ KREDİTLƏRİ AVTOMATİK BAĞLAYIR.</b>
        /// <para>
        /// Kredit üzrə toplanmış ödənişlər kreditin qiymətinə
        /// (<c>Kreditləşdirilən + Faiz</c>) çatdıqda kredit <c>«Bağlı»</c>,
        /// avtomobil isə <c>«Satıldı»</c> olur və beləliklə
        /// «🗄️ Satılan &amp; Krediti Bitmiş» bölməsinə keçir.
        /// </para>
        /// <para>
        /// Bu addım tətbiq açılışında bir dəfə işlədilir ki, <b>əvvəlki
        /// versiyalarda</b> tam ödənilmiş, lakin statusu yenilənməmiş kreditlər
        /// də düzəlsə — istifadəçi əl ilə heç nə etməsin.
        /// </para>
        /// </summary>
        private async Task CloseFinishedCreditsAsync(CancellationToken cancellationToken)
        {
            try
            {
                var sayi = await _creditService.RefreshCreditCompletionAsync(cancellationToken);

                if (sayi > 0)
                {
                    _logger.LogInformation(
                        "🎯 {Count} tam ödənilmiş kredit avtomatik «Bağlı» edildi və avtomobilləri arxivə keçirildi.",
                        sayi);
                }
                else
                {
                    _logger.LogInformation("🎯 Tam ödənilmiş, lakin açıq qalan kredit yoxdur.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kreditlərin avtomatik bağlanması alınmadı.");
            }
        }

        /// <summary>
        /// Mövcud kreditlərin <b>AYLIK ÖDƏNİŞİNİ</b> yeni vahid düsturla
        /// yenidən hesablayır.
        /// <para>
        /// Köhnə versiyalarda «annuitet» düsturu işlədilirdi və bazada saxlanılan
        /// <c>AylıqOdenis</c> dəyəri <b>səhv</b> idi. Məsələn
        /// 17 014 ₼ · 40% · 12 ay üçün 1 743,45 ₼ yazılmışdı — düzgün dəyər
        /// <b>1 984,97 ₼</b>-dir. Bu addım köhnə qeydləri avtomatik düzəldir;
        /// istifadəçi əl ilə yenidən yazmağa ehtiyac duymur.
        /// </para>
        /// </summary>
        private async Task<int> RecalcCreditMonthlyAsync(CancellationToken cancellationToken)
        {
            try
            {
                var credits = await _context.Credits.ToListAsync(cancellationToken);
                var fixedCount = 0;

                foreach (var credit in credits)
                {
                    var expected = CreditMath.MonthlyPayment(
                        credit.Kreditlesdirilen,
                        credit.FaizDerecesi,
                        credit.MuddetAy);

                    if (expected <= 0m || credit.AylıqOdenis == expected)
                    {
                        continue;
                    }

                    _logger.LogInformation(
                        "Kredit aylıq ödənişi düzəldildi: «{Muqavile}» — {Kohne:N2} ₼ → {Yeni:N2} ₼",
                        credit.MuqavileNomresi, credit.AylıqOdenis, expected);

                    credit.AylıqOdenis = expected;
                    fixedCount++;
                }

                if (fixedCount > 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning(
                        "{Count} kreditin aylıq ödənişi YENİ DÜSTURLA yenidən hesablandı " +
                        "(Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət).",
                        fixedCount);
                }

                return fixedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kreditlərin aylıq ödənişi yenidən hesablanarkən xəta baş verdi.");
                return 0;
            }
        }

        /// <summary>
        /// KREDİT DÜSTURUNUN özünü yoxlayır və nəticəni loga yazır.
        /// <para>
        /// <b>Aylıq ödəniş = Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət</b>
        /// </para>
        /// <example>
        /// 17 014 ₼ · 40% · 12 ay:
        /// <code>
        /// faiz  : 17 014 × 40%     = 6 805,60 ₼
        /// cəm   : 17 014 + 6 805,60 = 23 819,60 ₼
        /// AYLIK : 23 819,60 ÷ 12   = 1 984,97 ₼   ✔
        /// </code>
        /// </example>
        /// </summary>
        private void LogCreditMathSelfCheck()
        {
            try
            {
                const decimal principal = 17014m;
                const decimal faiz = 40m;
                const int muddet = 12;
                const decimal gozlenilen = 1984.97m;

                var aylıq = CreditMath.MonthlyPayment(principal, faiz, muddet);
                var cem = CreditMath.TotalPayable(principal, faiz);
                var faizMebleg = CreditMath.TotalInterest(principal, faiz);

                if (aylıq == gozlenilen)
                {
                    _logger.LogInformation(
                        "KREDİT DÜSTURU YOXLAMASI ✓ ({Principal:N2} ₼ · {Faiz}% · {Muddet} ay) → " +
                        "faiz {FaizMebleg:N2} ₼ · cəm {Cem:N2} ₼ · AYLIK {Aylıq:N2} ₼",
                        principal, faiz, muddet, faizMebleg, cem, aylıq);
                }
                else
                {
                    _logger.LogWarning(
                        "KREDİT DÜSTURU YOXLAMASI UĞURSUZ ✗ → aylıq {Aylıq:N2} ₼ ({Gozlenilen:N2} olmalıdır)",
                        aylıq, gozlenilen);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit düsturu yoxlaması keçirilə bilmədi.");
            }
        }

        /// <summary>
        /// TƏRƏFDAŞ BÖLGÜSÜ məntiqinin özünü yoxlayır və nəticəni loga yazır.
        /// <para>
        /// Nümunə: mənfəət 1 590 ₼ →
        /// Zaur 95,40 · Eşqin 79,50 · Asiman 79,50 · Asif 667,80 · Musa 667,80
        /// = 1 590,00 ₼ (dəqiq).
        /// </para>
        /// Beləliklə hər açılışda hesablamanın düzgünlüyü birbaşa loqdan görünür.
        /// </summary>
        private void LogPartnerMathSelfCheck()
        {
            try
            {
                const decimal numune = 1590m;

                var rows = PartnerMath.CreateDefaultRows();
                PartnerMath.Distribute(numune, rows);

                var cem = PartnerMath.Total(rows);
                var metn = string.Join(" · ", rows.Select(r => $"{r.Terefdas} {r.Mebleg:N2}"));

                if (cem == numune)
                {
                    _logger.LogInformation(
                        "TƏRƏFDAŞ BÖLGÜSÜ YOXLAMASI ✓ ({Numune:N2} ₼) → {Metn} = {Cem:N2} ₼",
                        numune, metn, cem);
                }
                else
                {
                    _logger.LogWarning(
                        "TƏRƏFDAŞ BÖLGÜSÜ YOXLAMASI UĞURSUZ ✗ → {Metn} = {Cem:N2} ₼ ({Numune:N2} olmalıdır)",
                        metn, cem, numune);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş bölgüsü yoxlaması keçirilə bilmədi.");
            }
        }

        /// <summary>
        /// Sahibi silinmiş «yetim» maliyyə qeydlərini təmizləyir.
        /// <para>
        /// Əvvəlki versiyalarda avtomobil silinəndə ona aid satış / kredit /
        /// xərc qeydləri bazada qalırdı və «Ümumi xərc», «Dövr xərci»,
        /// «Dövr mənfəəti» göstəricilərini şişirdirdi. Bu addım həmin
        /// qeydləri təmizləyir ki, rəqəmlər <b>dəqiq</b> olsun.
        /// </para>
        /// </summary>
        private async Task CleanupOrphansAsync(CancellationToken cancellationToken)
        {
            try
            {
                var removed = await _carService.CleanupOrphansAsync(cancellationToken);

                if (removed > 0)
                {
                    _logger.LogWarning(
                        "Yetim qeydlər təmizləndi: {Count} ədəd — hesabatlar dəqiqləşdirildi.",
                        removed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yetim qeydlərin təmizlənməsi alınmadı.");
            }
        }

        /// <summary>
        /// Xərc kataloqu (qrup / kateqoriya) boşdursa, standart siyahı bazaya köçürülür.
        /// Bundan sonra istifadəçi öz qruplarını və kateqoriyalarını əlavə edə bilər.
        /// </summary>
        private async Task SeedExpenseCatalogAsync(CancellationToken cancellationToken)
        {
            if (await _context.ExpenseCatalog.AnyAsync(cancellationToken))
            {
                return;
            }

            var entries = new List<ExpenseCatalogEntry>();
            var sira = 0;

            void AddGroups(string teyinat, IReadOnlyList<string> groups)
            {
                foreach (var group in groups)
                {
                    var categories = Catalog.GetCategories(group);
                    if (categories.Count == 0)
                    {
                        entries.Add(new ExpenseCatalogEntry
                        {
                            Teyinat = teyinat,
                            Qrup = group,
                            Kategoriya = string.Empty,
                            Sira = sira++
                        });
                        continue;
                    }

                    foreach (var category in categories)
                    {
                        entries.Add(new ExpenseCatalogEntry
                        {
                            Teyinat = teyinat,
                            Qrup = group,
                            Kategoriya = category,
                            Sira = sira++
                        });
                    }
                }
            }

            AddGroups(Catalog.CarDestination, Catalog.CarExpenseGroups);
            AddGroups(Catalog.OfficeDestination, Catalog.OfficeExpenseGroups);

            _context.ExpenseCatalog.AddRange(entries);
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Xərc kataloqu {Count} qeyd ilə dolduruldu.", entries.Count);
        }

        /// <summary>
        /// TƏRƏFDAŞLAR cədvəli boşdursa, kodda olan standart siyahını
        /// (<see cref="Catalog.DefaultPartners"/>) bazaya köçürür:
        /// Zaur %6 · Eşqin %5 · Asiman %5 · Asif &amp; Musa = qalıq.
        /// <para>
        /// Bundan sonra istifadəçi «👥 Tərəfdaşlar» tabından <b>yeni şəxs əlavə
        /// edə</b>, <b>şəxsi silə</b> və <b>faizləri dəyişə</b> bilər.
        /// </para>
        /// </summary>
        private async Task SeedPartnersAsync(CancellationToken cancellationToken)
        {
            if (await _context.Partners.AnyAsync(cancellationToken))
            {
                return;
            }

            var sira = 0;
            var entries = Catalog.DefaultPartners.Select(p => new Partner
            {
                Ad = p.Ad,
                Faiz = p.Faiz,
                QaligPayi = p.QaligPayi,
                Aktiv = true,
                Qeyd = p.QaligPayi ? "Qalıq payçısı" : "Faiz payçısı",
                Sira = sira++
            }).ToList();

            _context.Partners.AddRange(entries);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "👥 Tərəfdaşlar {Count} nəfərlə dolduruldu: {Adlar}",
                entries.Count,
                string.Join(" · ", entries.Select(p => $"{p.Ad} {p.FaizMetni}")));
        }

        /// <summary>
        /// Sıra nömrəsi təyin olunmamış (köhnə) avtomobillərə ardıcıl nömrə verir.
        /// </summary>
        private async Task BackfillSiraNomresiAsync(CancellationToken cancellationToken)

        {
            var missing = await _context.Cars
                .Where(c => c.SiraNomresi == 0)
                .OrderBy(c => c.Id)
                .ToListAsync(cancellationToken);

            if (missing.Count == 0)
            {
                return;
            }

            var max = await _context.Cars
                .Select(c => (int?)c.SiraNomresi)
                .MaxAsync(cancellationToken) ?? 0;

            foreach (var car in missing)
            {
                car.SiraNomresi = ++max;
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("{Count} avtomobilə sıra nömrəsi təyin edildi.", missing.Count);
        }
    }
}
