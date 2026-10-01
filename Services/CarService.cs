using System.IO;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class CarService : ICarService
    {
        private readonly ICarRepository _cars;
        private readonly IExpenseRepository _expenses;
        private readonly ICreditRepository _credits;
        private readonly IRepository<CreditTransaction> _creditTransactions;
        private readonly ISaleRepository _sales;
        private readonly IRepository<MediaAttachment> _attachments;

        /// <summary>
        /// ⏳ Möhlətlər — avtomobil (və onun kredit/satışları) silinəndə
        /// möhlətlər də silinməlidir ✓✓✓ (v6.2.11).
        /// </summary>
        private readonly IOdenisMohletRepository _mohletler;

        private readonly ITrashService _trash;
        private readonly ILogger<CarService> _logger;

        public CarService(
            ICarRepository cars,
            IExpenseRepository expenses,
            ICreditRepository credits,
            IRepository<CreditTransaction> creditTransactions,
            ISaleRepository sales,
            IRepository<MediaAttachment> attachments,
            IOdenisMohletRepository mohletler,
            ITrashService trash,
            ILogger<CarService> logger)
        {
            _cars = cars;
            _expenses = expenses;
            _credits = credits;
            _creditTransactions = creditTransactions;
            _sales = sales;
            _attachments = attachments;
            _mohletler = mohletler;
            _trash = trash;
            _logger = logger;
        }

        public async Task<IReadOnlyList<CarItem>> GetCarsAsync(CancellationToken cancellationToken = default)
        {
            // Satılan, barterə verilən və kreditə salınmış avtomobillər aktiv parkda göstərilmir.
            var cars = await _cars.GetWithExpensesAsync(cancellationToken);
            var list = cars.Where(c => !Catalog.IsOutOfPark(c.Status)).ToList();
            ResolveBarterCars(list, cars);
            return list;
        }

        public async Task<IReadOnlyList<CarItem>> GetAllCarsAsync(CancellationToken cancellationToken = default)
        {
            var cars = await _cars.GetWithExpensesAsync(cancellationToken);
            ResolveBarterCars(cars, cars);
            return cars;
        }

        // ====================================================================
        //  🚀 YÜNGÜL (LIGHT) SİYAHILAR ✓✓✓  (PERFORMANS — MİLYON SƏTİR)
        // --------------------------------------------------------------------
        //  ⚠ `GetCarsAsync` / `GetAllCarsAsync` hər avtomobilin BÜTÜN
        //     xərclərini yaddaşa yükləyir ✗ → 1 000 000 xərc = ~1 GB RAM ✗✓✓
        //  ✅ Bu variantlarda xərc CƏMLƏRİ SQL `GROUP BY` ilə alınır ✓ →
        //     avtomobil siyahısı ANİ açılır ✓✓✓ («Avto Park» tabı ✓)
        // ====================================================================

        /// <inheritdoc />
        public async Task<IReadOnlyList<CarItem>> GetCarsLightAsync(CancellationToken cancellationToken = default)
        {
            var cars = await _cars.GetWithExpenseTotalsAsync(cancellationToken);
            var list = cars.Where(c => !Catalog.IsOutOfPark(c.Status)).ToList();
            ResolveBarterCars(list, cars);
            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<CarItem>> GetAllCarsLightAsync(CancellationToken cancellationToken = default)
        {
            var cars = await _cars.GetWithExpenseTotalsAsync(cancellationToken);
            ResolveBarterCars(cars, cars);
            return cars;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<CarItem>> GetBarterCandidatesLightAsync(
            int? excludeCarId = null,
            CancellationToken cancellationToken = default)
        {
            var cars = await _cars.GetWithExpenseTotalsAsync(cancellationToken);

            return BarterCandidates(cars, excludeCarId);
        }

        public async Task<IReadOnlyList<CarItem>> GetSoldCarsAsync(CancellationToken cancellationToken = default)
        {
            // Arxiv: satılmış VƏ barter əvəzi verilmiş avtomobillər.
            var cars = await _cars.GetWithExpensesAsync(cancellationToken);
            var list = cars
                .Where(c => string.Equals(c.Status, Catalog.SoldStatus, StringComparison.Ordinal)
                         || string.Equals(c.Status, Catalog.BarterGivenStatus, StringComparison.Ordinal)
                         || string.Equals(c.Status, Catalog.TransferStatus, StringComparison.Ordinal))
                .ToList();

            ResolveBarterCars(list, cars);
            return list;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<CarItem>> GetBarterCandidatesAsync(
            int? excludeCarId = null,
            CancellationToken cancellationToken = default)
        {
            var cars = await _cars.GetWithExpensesAsync(cancellationToken);

            return BarterCandidates(cars, excludeCarId);
        }

        /// <summary>
        /// Barter üçün seçilə bilən avtomobilləri süzür ✓
        /// (artıq verilmişlər · parkdan çıxanlar · istisna olunan ✓)
        /// </summary>
        private static List<CarItem> BarterCandidates(IReadOnlyList<CarItem> cars, int? excludeCarId)
        {
            // Artıq başqa alışda barter əvəzi kimi verilmiş maşınlar təkrar təklif edilmir.
            var usedDonors = cars
                .Where(c => c.BarterCarId.HasValue)
                .Select(c => c.BarterCarId!.Value)
                .ToHashSet();

            return cars
                .Where(c => !Catalog.IsOutOfPark(c.Status))
                .Where(c => !excludeCarId.HasValue || c.Id != excludeCarId.Value)
                .Where(c => !usedDonors.Contains(c.Id))
                .OrderBy(c => c.SiraNomresi)
                .ThenBy(c => c.Id)
                .ToList();
        }

        /// <summary>
        /// Hər avtomobilin <see cref="CarItem.BarterCar"/> xassəsini
        /// <see cref="CarItem.BarterCarId"/> əsasında doldurur
        /// (EF bu əlaqəni izləmir — xassə <c>[NotMapped]</c>-dir).
        /// </summary>
        private static void ResolveBarterCars(IReadOnlyList<CarItem> target, IReadOnlyList<CarItem> pool)
        {
            var byId = pool.Where(c => c.Id > 0).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());

            foreach (var car in target)
            {
                car.BarterCar = car.BarterCarId.HasValue && byId.TryGetValue(car.BarterCarId.Value, out var donor)
                    ? donor
                    : null;
            }
        }

        public Task<bool> RegistrationExistsAsync(string registration, int? excludeId = null, CancellationToken cancellationToken = default)
            => _cars.RegistrationExistsAsync(registration, excludeId, cancellationToken);

        public async Task<CarItem> AddCarAsync(CarItem car, CancellationToken cancellationToken = default)
        {
            // Avtomobil bilavasitə kreditə salınırsa, ardıcıl kredit nömrəsi verilir.
            if (car.Status == Catalog.CreditStatus && string.IsNullOrWhiteSpace(car.KreditNomresi))
            {
                car.KreditNomresi = await GenerateCreditNumberAsync(cancellationToken);
            }

            // Sıra nömrəsi boş (0) buraxılıbsa avtomatik ardıcıl verilir.
            if (car.SiraNomresi <= 0)
            {
                car.SiraNomresi = await GenerateSiraNumberAsync(cancellationToken);
            }

            await _cars.AddAsync(car, cancellationToken);
            await _cars.SaveChangesAsync(cancellationToken);

            // Barter ilə alınıbsa, əvəzə verilən maşın parkdan çıxarılır (Satıldı).
            await MarkBarterDonorAsync(car, cancellationToken);

            // Alış qiyməti varsa, avtomatik "Alış" xərci yaradılır (maya hesablanması üçün).
            if (car.AlisQiymeti > 0)
            {
                await _expenses.AddAsync(new ExpenseItem
                {
                    Tarix = car.AlisTarixi ?? DateTime.Today,
                    Teyinat = Catalog.CarDestination,
                    Qrup = "💰 Alış & Maya Xərcləri",
                    Kategoriya = "Alış",
                    CarId = car.Id,
                    Mebleg = car.AlisQiymeti,
                    OdenisUsulu = "Nağd",
                    Qeyd = car.IsBarter
                        ? BuildBarterNote(car)
                        : "Avtomobilin alış qiyməti (Avtomatik sinxronlaşdırıldı)"
                }, cancellationToken);

                await _expenses.SaveChangesAsync(cancellationToken);
            }

            return car;
        }

        /// <inheritdoc />
        public async Task<bool> SetStatusAsync(int carId, string status, CancellationToken cancellationToken = default)
        {
            // ⚠ Uzunömürlü kontekstdəki köhnə (stale) nüsxələr buraxılır.
            //   Əks halda `GetByIdAsync` izləyicidəki köhnə obyekti qaytarır və
            //   dəyişiklik DƏQİQ tətbiq olunmur ✗ (məhz «geri qaytar» xətası).
            _cars.ClearTracker();

            var car = await _cars.GetByIdAsync(carId, cancellationToken);

            if (car is null)
            {
                return false;
            }

            var kohne = car.Status;

            car.Status = status;

            // Sıra nömrəsi boşdursa ardıcıl nömrə verilir (parkda yer tutsun).
            if (car.SiraNomresi <= 0)
            {
                car.SiraNomresi = await GenerateSiraNumberAsync(cancellationToken);
            }

            _cars.Update(car);
            await _cars.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "🚗 Avtomobil statusu dəyişdirildi: «{Car}» {Kohne} → {Yeni} (sıra №{Sira})",
                car.DisplayName, kohne, status, car.SiraNomresi);

            return true;
        }

        public async Task UpdateCarAsync(CarItem car, CancellationToken cancellationToken = default)
        {
            // İzlənilən (tracked) nüsxəni yükləyib yalnız skalyar sahələri kopyalayırıq.
            // Beləliklə naviqasiya kolleksiyaları (xərclər) səhvən yenilənmir.
            var existing = await _cars.GetByIdAsync(car.Id, cancellationToken);
            if (existing is null)
            {
                return;
            }

            // Redaktədən ƏVVƏLKİ vəziyyət surətə götürülür (Ctrl+Z ilə geri almaq üçün).
            await _trash.BackupCarEditAsync(car.Id, cancellationToken);

            // ================================================================
            //  ✅ MAŞININ ADI HEÇ VAXT DƏYİŞMİR / İTMİR ✗✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL bu 3 sahə ŞƏRTSİZ kopyalanırdı ✗ → başqa axından
            //    (kredit · sənəd əlavə etmə · media arxivi ✓) QİSMƏN
            //    yüklənmiş maşın obyekti ötürüləndə Marka / Dövlət nömrəsi /
            //    VIN BOŞ yazılırdı ✗ → «maşınların adları dəyişir» ✗✓✓
            //  ✅ İNDİ: YALNIZ DOLU dəyər gələrsə yenilənir ✓
            //     (boş gələrsə KÖHNƏ dəyər QORUNUR ✓✓✓)
            // ================================================================
            if (!string.IsNullOrWhiteSpace(car.Marka))
            {
                existing.Marka = car.Marka.Trim();
            }

            if (!string.IsNullOrWhiteSpace(car.QeydiyyatNisani))
            {
                existing.QeydiyyatNisani = car.QeydiyyatNisani.Trim();
            }

            if (!string.IsNullOrWhiteSpace(car.Vin))
            {
                existing.Vin = car.Vin.Trim();
            }

            existing.Il = car.Il;
            existing.Yurus = car.Yurus;
            existing.Yanacaq = car.Yanacaq;
            existing.AlisTarixi = car.AlisTarixi;
            existing.AlisSaati = car.AlisSaati;
            existing.AlisUsulu = car.AlisUsulu;
            existing.BarterTesviri = car.BarterTesviri;
            existing.BarterCarId = car.BarterCarId;
            existing.BarterDeyeri = car.BarterDeyeri;
            existing.BarterSaleId = car.BarterSaleId;
            existing.AlisQiymeti = car.AlisQiymeti;
            existing.SatisQiymeti = car.SatisQiymeti;
            existing.Status = car.Status;
            existing.KreditNomresi = car.KreditNomresi;
            // ================================================================
            //  🔢 SIRA NÖMRƏSİ ✓✓✓ — YALNIZ DOLU gələrsə köçürülür ✓
            // ----------------------------------------------------------------
            //  ⚠️ ƏVVƏL ✗: buluddan/USB-dən gələn «0» dəyər istifadəçinin ÖZ
            //     nömrəsini (məs. 312 ✓) SİLİRDİ ✗ → «312 → 0» ✗✓✓
            //  ✅ İNDİ: 0 heç vaxt mövcud nömrəni əvəz etmir ✗✓✓
            // ================================================================
            if (car.SiraNomresi > 0)
            {
                existing.SiraNomresi = car.SiraNomresi;
            }

            // Status "Kreditdə" olduqda və nömrə boşdursa, avtomatik verilir.
            if (existing.Status == Catalog.CreditStatus && string.IsNullOrWhiteSpace(existing.KreditNomresi))
            {
                existing.KreditNomresi = await GenerateCreditNumberAsync(cancellationToken);
            }

            // Sıra nömrəsi boş (0) buraxılıbsa avtomatik verilir.
            if (existing.SiraNomresi <= 0)
            {
                existing.SiraNomresi = await GenerateSiraNumberAsync(cancellationToken);
            }

            await _cars.SaveChangesAsync(cancellationToken);

            // Barter əvəzi seçilibsə, həmin maşın parkdan çıxarılır (Satıldı).
            await MarkBarterDonorAsync(existing, cancellationToken);
        }

        /// <summary>
        /// Boş qalan ƏN KİÇİK sıra nömrəsini qaytarır (1, 2, 3...).
        /// Satılan avtomobillərin nömrələri boş sayılır — yeni avtomobil onların yerini tutur.
        /// </summary>
        private async Task<int> GenerateSiraNumberAsync(CancellationToken cancellationToken)
        {
            var all = await _cars.FindAsync(_ => true, cancellationToken);

            var used = all
                .Where(c => c.Status != Catalog.SoldStatus)
                .Select(c => c.SiraNomresi)
                .Where(n => n > 0)
                .ToHashSet();

            var next = 1;
            while (used.Contains(next))
            {
                next++;
            }

            return next;
        }

        /// <summary>
        /// 🔢 <b>SIRA NÖMRƏLƏRİNİ DÜZƏLDİR</b> ✓✓✓ — <c>SiraNomresi = 0</c> olanlara nömrə verir ✓
        /// <para>
        /// ⚠️ Əvvəl bəzi avtomobillər (xüsusən 💳 «Kreditlər» bölməsindən əlavə olunanlar ✗)
        /// <c>0</c> nömrəsi ilə qalırdı ✗ → həm proqramda ✗, həm Firebase-də ✗ səhv görünürdü ✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: <c>1, 2, 3 …</c> ardıcıl nömrələr verilir ✓ — satılanların boş qalan
        /// nömrələri YENİDƏN istifadə olunur ✓ (təkrarsız ✓✓✓)
        /// </para>
        /// <para>🖥️ Tətbiq açılışında BİR DƏFƏ çağırılır ✓ → nəticə Firebase-ə də gedir ✓✓✓</para>
        /// </summary>
        /// <returns>Düzəldilən avtomobil sayı ✓</returns>
        public async Task<int> SiraNomreleriniDuzeltAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var hamısı = await _cars.FindAsync(_ => true, cancellationToken);

                var sıfırlar = hamısı
                    .Where(c => c.SiraNomresi <= 0)
                    .OrderBy(c => c.Id)
                    .ToList();

                if (sıfırlar.Count == 0) return 0;

                // ✅ Artıq istifadə olunan nömrələr ✓ (satılanlarınkı BOŞ sayılır ✓)
                var istifadə = hamısı
                    .Where(c => c.Status != Catalog.SoldStatus && c.SiraNomresi > 0)
                    .Select(c => c.SiraNomresi)
                    .ToHashSet();

                var sonrakı = 1;

                foreach (var car in sıfırlar)
                {
                    while (istifadə.Contains(sonrakı)) sonrakı++;

                    car.SiraNomresi = sonrakı;
                    istifadə.Add(sonrakı);

                    _cars.Update(car);
                }

                await _cars.SaveChangesAsync(cancellationToken);

                Cas0201.Firebase.AppLogger.Melumat(
                    $"🔢 Sıra nömrələri düzəldildi ✓ — {sıfırlar.Count} avtomobil nömrələndi ✓");

                return sıfırlar.Count;
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Melumat("🔢 Sıra nömrələri düzəldilə bilmədi ✗ — " + ex.Message);
                return 0;
            }
        }

        /// <summary>Növbəti ardıcıl kredit nömrəsini yaradır (məs. KR-0001).</summary>
        private async Task<string> GenerateCreditNumberAsync(CancellationToken cancellationToken)
        {
            var existing = await _cars.FindAsync(c => c.KreditNomresi != string.Empty, cancellationToken);

            var max = 0;
            foreach (var item in existing)
            {
                var digits = new string(item.KreditNomresi.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var value) && value > max)
                {
                    max = value;
                }
            }

            return "KR-" + (max + 1).ToString("D4");
        }

        /// <summary>
        /// Avtomobili və ona aid <b>BÜTÜN maliyyə qeydlərini</b> silir.
        /// <para>
        /// Silinənlər: xərclər, satışlar, kreditlər + onların bütün əməliyyatları,
        /// sənəd / media faylları (disk daxil). Həmçinin digər avtomobillərdə bu
        /// maşına olan barter əlaqəsi kəsilir.
        /// </para>
        /// <para>
        /// Nəticə: «Ümumi xərc», «Dövr xərci», «Dövr mənfəəti» və digər
        /// göstəricilər <b>dəqiq</b> qalır — heç bir qeyd yuxarıda qalmır.
        /// </para>
        /// </summary>
        public async Task DeleteCarAsync(int id, CancellationToken cancellationToken = default)
        {
            // ⚠ KRİTİK: avtomobil `GetByIdAsync` ilə alınır — `GetWithDetailsAsync`
            //   İSTİFADƏ OLUNMUR. Səbəb: GetWithDetailsAsync xərcləri də `_cars`
            //   kontekstinə ilişdirir. Xərclər sonra `_expenses` konteksti ilə
            //   silindikdə `_cars` konteksti köhnə izlənilən nüsxələri
            //   «OnDelete(SetNull)» qaydası ilə CarId = NULL etməyə çalışır →
            //   0 sətir → DbUpdateConcurrencyException verirdi və avtomobil
            //   SİLİNMİRDİ. İndi xərclər yalnız `_expenses` vasitəsilə idarə olunur.
            var car = await _cars.GetByIdAsync(id, cancellationToken);
            if (car is null)
            {
                return;
            }

            // ---- 0) GERİ QAYTARMA SURƏTİ ----
            //  Silmədən ƏVVƏL hər şey diskə yazılır ki, səhv silmə geri alına bilsin
            //  («Ctrl+Z» və ya «🗑 Silinənlər» bölməsi).
            var backup = await _trash.BackupCarAsync(id, cancellationToken);

            _logger.LogInformation(
                "Avtomobil silinir: «{Car}» — bərpa surəti: {Backup}",
                car.DisplayName, backup is null ? "YARADILA BİLMƏDİ" : backup.FileName);

            // ---- 1) Digər avtomobillərdə bu maşına olan barter əlaqəsini kəs ----
            //  FindAsync NO-TRACKING işləyir; sadəcə Update() çağırsaq, EF ayrılmış
            //  qrafiı (xərclər daxil) yenidən ilişdirməyə çalışıb
            //  «another instance with the same key value is already being tracked»
            //  xətası verir. Ona görə izlənilən nüsxə ayrıca götürülür.
            var referrers = await _cars.FindAsync(c => c.BarterCarId == id, cancellationToken);
            foreach (var referrer in referrers)
            {
                var tracked = await _cars.GetByIdAsync(referrer.Id, cancellationToken);
                if (tracked is not null)
                {
                    tracked.BarterCarId = null;
                }
            }

            // ---- 2) Kreditlər + onların BÜTÜN əməliyyatları ----
            var credits = await _credits.FindAsync(c => c.CarId == id, cancellationToken);
            foreach (var credit in credits)
            {
                var transactions = await _creditTransactions
                    .FindAsync(t => t.CreditId == credit.Id, cancellationToken);

                foreach (var transaction in transactions)
                {
                    var trackedTransaction = await _creditTransactions
                        .GetByIdAsync(transaction.Id, cancellationToken);

                    if (trackedTransaction is not null)
                    {
                        _creditTransactions.Remove(trackedTransaction);
                    }
                }

                var trackedCredit = await _credits.GetByIdAsync(credit.Id, cancellationToken);
                if (trackedCredit is not null)
                {
                    _credits.Remove(trackedCredit);
                }

                // ⏳ Kreditin İLKİN ÖDƏNİŞ möhlətləri DƏ silinir ✓✓✓ (v6.2.11)
                _mohletler.ClearTracker();
                await _mohletler.DeleteWhereAsync(m => m.CreditId == credit.Id, cancellationToken);
            }

            // Hər repo öz kontekstində saxlayır (AppDbContext transient-dir).
            await _creditTransactions.SaveChangesAsync(cancellationToken);
            await _credits.SaveChangesAsync(cancellationToken);

            // ---- 3) Satış qeydləri ----
            var sales = await _sales.FindAsync(s => s.CarId == id, cancellationToken);
            foreach (var sale in sales)
            {
                var tracked = await _sales.GetByIdAsync(sale.Id, cancellationToken);
                if (tracked is not null)
                {
                    _sales.Remove(tracked);
                }

                // ⏳ Satışın (nisyə) MÖHLƏTLƏRİ DƏ silinir ✓✓✓ (v6.2.11)
                _mohletler.ClearTracker();
                await _mohletler.DeleteWhereAsync(m => m.SaleId == sale.Id, cancellationToken);
            }

            await _sales.SaveChangesAsync(cancellationToken);

            // ---- 4) Avtomobilə bağlı bütün xərclər ----
            //  ⚠ Xərclər `_cars` kontekstinə ilişməməli olduğu üçün (yuxarıya bax)
            //  siyahı BİRBAŞA `_expenses` repositoriyasından oxunur və həmin
            //  kontekstdə silinir. `SaveChangesAsync` MÜTLƏQ çağırılmalıdır,
            //  əks halda xərclər bazada qalır («yetim qeyd» → «Ümumi Xərc» şişir).
            var carExpenses = await _expenses.FindAsync(e => e.CarId == id, cancellationToken);
            var expenseCount = carExpenses.Count;

            foreach (var expense in carExpenses)
            {
                var trackedExpense = await _expenses.GetByIdAsync(expense.Id, cancellationToken);
                if (trackedExpense is not null)
                {
                    _expenses.Remove(trackedExpense);
                }
            }

            await _expenses.SaveChangesAsync(cancellationToken);

            // ---- 5) Sənəd / media faylları (həm disk, həm baza) ----
            await DeleteMediaAsync(id, cancellationToken);
            await _attachments.SaveChangesAsync(cancellationToken);

            // ---- 6) Avtomobilin özü ----
            //  (jurnal üçün saylar silinməzdən ƏVVƏL götürülür)
            var saleCount = sales.Count;
            var creditCount = credits.Count;

            _cars.Remove(car);
            await _cars.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Avtomobil silindi: #{Id} — {ExpenseCount} xərc, {SaleCount} satış, {CreditCount} kredit də silindi.",
                id, expenseCount, saleCount, creditCount);
        }

        /// <inheritdoc />
        public async Task<int> CleanupOrphansAsync(CancellationToken cancellationToken = default)
        {
            // DİQQƏT: AppDbContext TRANSIENT-dir — hər repository AYRI kontekst alır.
            // Ona görə hər repo-ya aid dəyişiklik MÜTLƏQ həmin repo-nun
            // SaveChangesAsync() ilə saxlanılmalıdır, əks halda itir.
            var removed = 0;
            var expensesRemoved = 0;
            var salesRemoved = 0;
            var creditsRemoved = 0;
            var transactionsRemoved = 0;
            var attachmentsRemoved = 0;

            // Mövcud Id-lər (yüngül, yalnız Id oxunur).
            var carIds = (await _cars.GetAllAsync(cancellationToken)).Select(c => c.Id).ToHashSet();
            var creditIds = (await _credits.GetAllAsync(cancellationToken)).Select(c => c.Id).ToHashSet();

            var allExpenses = await _expenses.GetAllAsync(cancellationToken);
            var allSales = await _sales.GetAllAsync(cancellationToken);
            var allAttachments = await _attachments.GetAllAsync(cancellationToken);
            var allCars = await _cars.GetAllAsync(cancellationToken);

            // ---- AUDIT: bazada nə var, nə qədər? ----
            _logger.LogInformation(
                "BAZA AUDİTİ → Avtomobil: {Cars} ({Active} aktiv, {Out} parkdan çıxmış) · " +
                "Xərc: {Expenses} ({ExpTotal} AZN) · Satış: {Sales} ({SalesTotal} AZN) · " +
                "Kredit: {Credits} · Əməliyyat: {Transactions} · Media: {Media}",
                allCars.Count,
                allCars.Count(c => !Catalog.IsOutOfPark(c.Status)),
                allCars.Count(c => Catalog.IsOutOfPark(c.Status)),
                allExpenses.Count,
                allExpenses.Sum(e => e.Mebleg),
                allSales.Count,
                allSales.Sum(s => s.SatisQiymeti),
                creditIds.Count,
                (await _creditTransactions.GetAllAsync(cancellationToken)).Count,
                allAttachments.Count);

            // Silinmiş avtomobillərə aid xərclər (yetimlər) — MƏLUMAT üçün.
            var orphanCarExpenses = allExpenses
                .Where(e => e.Teyinat == Catalog.CarDestination
                            && (!e.CarId.HasValue || !carIds.Contains(e.CarId.Value)))
                .ToList();

            if (orphanCarExpenses.Count > 0)
            {
                _logger.LogWarning(
                    "YETİM AVTOMOBİL XƏRCLƏRİ: {Count} qeyd, cəmi {Total} AZN — silinir " +
                    "(sahibi olan avtomobil artıq bazada yoxdur).",
                    orphanCarExpenses.Count, orphanCarExpenses.Sum(e => e.Mebleg));
            }

            // ---- 1) Sahibi silinmiş AVTOMOBİL xərcləri ----
            //  Ofis / inzibati xərclər TOXUNULMUR (onların avtomobili olmur).
            foreach (var expense in orphanCarExpenses)
            {
                var tracked = await _expenses.GetByIdAsync(expense.Id, cancellationToken);
                if (tracked is not null)
                {
                    _expenses.Remove(tracked);
                    expensesRemoved++;
                }
            }

            if (expensesRemoved > 0)
            {
                await _expenses.SaveChangesAsync(cancellationToken);
            }

            // ---- 2) Avtomobili silinmiş satışlar ----
            foreach (var sale in await _sales.FindAsync(s => s.CarId == null, cancellationToken))
            {
                var tracked = await _sales.GetByIdAsync(sale.Id, cancellationToken);
                if (tracked is not null)
                {
                    _sales.Remove(tracked);
                    salesRemoved++;
                }
            }

            if (salesRemoved > 0)
            {
                await _sales.SaveChangesAsync(cancellationToken);
            }

            // ---- 3) Avtomobili silinmiş kreditlər ----
            foreach (var credit in await _credits.FindAsync(c => c.CarId == null, cancellationToken))
            {
                var tracked = await _credits.GetByIdAsync(credit.Id, cancellationToken);
                if (tracked is not null)
                {
                    _credits.Remove(tracked);
                    creditsRemoved++;
                }
            }

            if (creditsRemoved > 0)
            {
                await _credits.SaveChangesAsync(cancellationToken);
            }

            // ---- 4) Krediti silinmiş ödəniş əməliyyatları ----
            var transactions = await _creditTransactions.GetAllAsync(cancellationToken);
            foreach (var transaction in transactions)
            {
                if (transaction.CreditId.HasValue && creditIds.Contains(transaction.CreditId.Value))
                {
                    continue;
                }

                var tracked = await _creditTransactions.GetByIdAsync(transaction.Id, cancellationToken);
                if (tracked is not null)
                {
                    _creditTransactions.Remove(tracked);
                    transactionsRemoved++;
                }
            }

            if (transactionsRemoved > 0)
            {
                await _creditTransactions.SaveChangesAsync(cancellationToken);
            }

            // ---- 5) Sahibi silinmiş media faylları (həm disk, həm baza) ----
            var attachments = await _attachments.FindAsync(
                a => a.RefType == MediaRefTypes.Car, cancellationToken);

            foreach (var attachment in attachments)
            {
                if (carIds.Contains(attachment.RefId))
                {
                    continue;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(attachment.StoredPath)
                        && File.Exists(attachment.StoredPath))
                    {
                        File.Delete(attachment.StoredPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Yetim media faylı diskdən silinə bilmədi: {Path}", attachment.StoredPath);
                }

                var tracked = await _attachments.GetByIdAsync(attachment.Id, cancellationToken);
                if (tracked is not null)
                {
                    _attachments.Remove(tracked);
                    attachmentsRemoved++;
                }
            }

            if (attachmentsRemoved > 0)
            {
                await _attachments.SaveChangesAsync(cancellationToken);
            }

            // ================================================================
            //  ---- 6) Sahibi silinmiş MÖHLƏTLƏR ✓✓✓  (v6.2.11)
            // ----------------------------------------------------------------
            //  İSTİFADƏÇİ TƏLƏBİ: kredit/satış silinəndə möhlətlər də silinməlidir ✓
            //  (ödənilmiş ✓ və ya gözlənilən ✓ — FƏRQ ETMİR ✓)
            //  ⚠ Köhnə bazalarda (xarici alətlə yazılmış və ya FK nəzarəti
            //  SÖNÜK halda yaranmış) SAHİBSİZ sətirlər qala bilər ✗ →
            //  açılışda avtomatik təmizlənir ✓✓✓
            // ================================================================
            var saleIds = allSales.Select(s => s.Id).ToHashSet();
            var mohletlerRemoved = 0;

            foreach (var mohlet in await _mohletler.GetAllAsync(cancellationToken))
            {
                var sahibsiz =
                    (mohlet.CreditId is int cid && !creditIds.Contains(cid)) ||
                    (mohlet.SaleId is int sid && !saleIds.Contains(sid)) ||
                    (mohlet.CreditId is null && mohlet.SaleId is null);

                if (!sahibsiz)
                {
                    continue;
                }

                _mohletler.ClearTracker();
                mohletlerRemoved += await _mohletler.DeleteWhereAsync(
                    m => m.Id == mohlet.Id, cancellationToken);
            }

            removed = expensesRemoved + salesRemoved + creditsRemoved
                      + transactionsRemoved + attachmentsRemoved + mohletlerRemoved;

            if (removed > 0)
            {
                _logger.LogWarning(
                    "Yetim qeydlər təmizləndi: {Count} ədəd " +
                    "({Exp} xərc, {Sale} satış, {Credit} kredit, {Tx} əməliyyat, " +
                    "{Media} media, {Mohlet} möhlət) — hesabatlar dəqiqləşdirildi.",
                    removed, expensesRemoved, salesRemoved, creditsRemoved,
                    transactionsRemoved, attachmentsRemoved, mohletlerRemoved);
            }
            else
            {
                _logger.LogInformation("Yetim qeyd YOXDUR — baza təmizdir.");
            }

            return removed;
        }

        /// <summary>
        /// Avtomobilə bağlı bütün sənəd / media fayllarını silir:
        /// həm fiziki fayl, həm də baza qeydi.
        /// </summary>
        private async Task DeleteMediaAsync(int carId, CancellationToken cancellationToken)
        {
            var attachments = await _attachments.FindAsync(
                a => a.RefType == MediaRefTypes.Car && a.RefId == carId,
                cancellationToken);

            foreach (var attachment in attachments)
            {
                try
                {
                    var path = attachment.StoredPath;

                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    // Fayl kilidlənibsə silinmir — baza qeydi yenə də təmizlənir.
                    _logger.LogWarning(ex, "Media faylı diskdən silinə bilmədi: {Path}", attachment.StoredPath);
                }

                var tracked = await _attachments.GetByIdAsync(attachment.Id, cancellationToken);
                if (tracked is not null)
                {
                    _attachments.Remove(tracked);
                }
            }
        }

        // Maya dəyəri = alış qiyməti + əlavə xərclər (avtomatik "Alış" qeydi ikiqat sayılmır).
        public decimal CalculateCost(CarItem car) => car.MayaDeyeri;

        public CarStats BuildStats(IEnumerable<CarItem> cars)
        {
            var all = cars.ToList();

            // Barter üçün VERİLMİŞ avtomobillər parkdan çıxıb — ümumi sayda və
            // maya dəyərində sayılmır. Əks halda 1 maşın verib yenisini alanda
            // park 2 avtomobil göstərirdi (1 qalmalı idi).
            var donated = all
                .Where(c => c.BarterCarId.HasValue)
                .Select(c => c.BarterCarId!.Value)
                .ToHashSet();

            var list = all
                .Where(c => !donated.Contains(c.Id))
                // ================================================================
                //  ✅ YALNIZ SATILAN / TRANSFER / BARTER VERİLƏN çıxarılır ✓✓✓
                // ----------------------------------------------------------------
                //  «🟠 Kreditdə» maşın BİZİM MAŞINIMIZDIR ✓ → ümumi sayda
                //  və maya dəyərində SAYILIR ✓✓✓
                //  (kredit bağlananda → «Satıldı» olur ✓ → o zaman çıxır ✓)
                //
                //  ⚠ ƏVVƏL `Catalog.IsOutOfPark` istifadə olunurdu ✗ → bu,
                //    «Kreditdə» statusunu da parkdan kənar sayırdı ✗ →
                //    ümumi avto sayı 0 görünürdü ✗✓✓
                // ================================================================
                .Where(c => !string.Equals(c.Status, Catalog.SoldStatus, StringComparison.Ordinal)
                            && !string.Equals(c.Status, Catalog.BarterGivenStatus, StringComparison.Ordinal)
                            && !string.Equals(c.Status, Catalog.TransferStatus, StringComparison.Ordinal))
                .ToList();

            var available = list.Count(c => c.Status is "Stokda" or "Satışda");
            var credit = list.Count(c => c.Status == "Kreditdə");
            var totalCost = list.Sum(CalculateCost);
            return new CarStats(list.Count, available, credit, totalCost);
        }

        /// <inheritdoc />
        public PurchaseStats BuildPurchaseStats(IEnumerable<CarItem> cars)
        {
            var list = cars
                .Where(c => c.AlisQiymeti > 0 || c.IsBarter)
                .ToList();

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            var cash = list.Where(c => !c.IsBarter).ToList();
            var barter = list.Where(c => c.IsBarter).ToList();

            var cashTotal = cash.Sum(c => c.AlisQiymeti);
            var barterTotal = barter.Sum(c => c.AlisQiymeti);

            var monthTotal = list
                .Where(c => (c.AlisTarixi ?? c.YaradilmaTarixi) >= monthStart)
                .Sum(c => c.AlisQiymeti);

            return new PurchaseStats(
                list.Count,
                cash.Count,
                barter.Count,
                cashTotal,
                barterTotal,
                cashTotal + barterTotal,
                monthTotal);
        }

        /// <summary>
        /// Barter ilə alınan maşın üçün əvəzə verilən avtomobili tapır və
        /// onu avto parkdan çıxarır («Satıldı» statusu verilir).
        /// </summary>
        private async Task MarkBarterDonorAsync(CarItem car, CancellationToken cancellationToken)
        {
            if (!car.IsBarter || car.BarterCarId is not { } donorId || donorId == car.Id)
            {
                return;
            }

            var donor = await _cars.GetByIdAsync(donorId, cancellationToken);
            if (donor is null)
            {
                // Seçilmiş maşın artıq bazada yoxdur — əlaqəni təmizləyirik.
                car.BarterCarId = null;
                car.BarterCar = null;
                _cars.Update(car);
                await _cars.SaveChangesAsync(cancellationToken);
                return;
            }

            // Xərc qeydi və UI üçün əvəzə verilən maşını yadda saxlayırıq.
            car.BarterCar = donor;

            // Əvəz maşının məlumatı MƏTN kimi snapshot edilir: maşın parkdan
            // çıxdıqdan sonra da tarixçə itmir və «hansı maşınla barter edilib»
            // sorusuna cavab avtomobilin kartında qalır.
            if (string.IsNullOrWhiteSpace(car.BarterTesviri))
            {
                car.BarterTesviri = $"{donor.DisplayName} · {donor.Il} il";
                _cars.Update(car);
                await _cars.SaveChangesAsync(cancellationToken);
            }

            if (string.Equals(donor.Status, Catalog.BarterGivenStatus, StringComparison.Ordinal))
            {
                return;
            }

            // «Satıldı» deyil, «Barter edildi»: bu maşın üçün pul GƏLMƏYİB —
            // əvəzində başqa avtomobil alınıb. Arxivdə belə göstərilir.
            donor.Status = Catalog.BarterGivenStatus;
            _cars.Update(donor);
            await _cars.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Barter alışı üçün avtomatik «Alış» xərc qeydini hazırlayır.</summary>
        private static string BuildBarterNote(CarItem car)
        {
            var donor = car.BarterCar is { } donorCar && !string.IsNullOrWhiteSpace(donorCar.DisplayName)
                ? donorCar.DisplayName
                : AppFormatBarter(car.BarterTesviri);

            var note = $"Barter alış — əvəzə verilən: {donor}";

            if (car.BarterDeyeri <= 0m)
            {
                return note;
            }

            note += $"; dəyəri {car.BarterDeyeri:N2} ₼";

            return car.BarterFerqi switch
            {
                > 0m => note + $"; əlavə {car.BarterFerqi:N2} ₼ ödənilib",
                < 0m => note + $"; {Math.Abs(car.BarterFerqi):N2} ₼ qaytarılıb",
                _ => note + "; bərabər dəyişmə"
            };
        }

        /// <summary>Barter təsvirini oxunaqlı formaya salır (boşdursa «göstərilməyib»).</summary>
        private static string AppFormatBarter(string? value)
            => string.IsNullOrWhiteSpace(value) ? "göstərilməyib" : value.Trim();
    }
}
