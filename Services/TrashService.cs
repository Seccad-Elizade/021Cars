using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class TrashService : ITrashService
    {
        private readonly ICarRepository _cars;
        private readonly IRepository<ExpenseItem> _expenses;
        private readonly IRepository<Sale> _sales;
        private readonly IRepository<Credit> _credits;
        private readonly IRepository<CreditTransaction> _transactions;
        private readonly IRepository<MediaAttachment> _attachments;

        /// <summary>
        /// ⏳ Möhlətlər — silinən kredit/satışın möhlətləri DƏ surətə düşməlidir ✓✓✓
        /// (v6.2.11 — bax: <see cref="RestoreAsync"/>).
        /// </summary>
        private readonly IOdenisMohletRepository _mohletler;

        private readonly ILogger<TrashService> _logger;

        /// <summary>Dövrə istinadların JSON-da sonsuz döngə yaratmaması üçün.</summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public TrashService(
            ICarRepository cars,
            IRepository<ExpenseItem> expenses,
            IRepository<Sale> sales,
            IRepository<Credit> credits,
            IRepository<CreditTransaction> transactions,
            IRepository<MediaAttachment> attachments,
            IOdenisMohletRepository mohletler,
            ILogger<TrashService> logger)
        {
            _cars = cars;
            _expenses = expenses;
            _sales = sales;
            _credits = credits;
            _transactions = transactions;
            _attachments = attachments;
            _mohletler = mohletler;
            _logger = logger;

            try
            {
                Directory.CreateDirectory(Folder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Silinənlər qovluğu yaradıla bilmədi: {Folder}", Folder);
            }
        }

        /// <inheritdoc />
        public string Folder => Path.Combine(
            Cas0201.Kok.Qovluq,
            "EnterpriseAeroStudio",
            "Trash");

        // ------------------------------------------------------------- YAZMA ---

        /// <inheritdoc />
        public async Task<TrashSnapshot?> BackupCarAsync(int carId, CancellationToken cancellationToken = default)
        {
            var car = await _cars.GetWithDetailsAsync(carId, cancellationToken);
            if (car is null)
            {
                return null;
            }

            var snapshot = new TrashSnapshot
            {
                Kind = "Avtomobil",
                Title = car.DisplayName,
                Amount = car.MayaDeyeri,
                Car = StripCar(car),
                Expenses = (await _expenses.FindAsync(e => e.CarId == carId, cancellationToken)).ToList(),
                Sales = (await _sales.FindAsync(s => s.CarId == carId, cancellationToken)).ToList(),
                Credits = (await _credits.FindAsync(c => c.CarId == carId, cancellationToken)).ToList(),
                Attachments = (await _attachments.FindAsync(
                    a => a.RefType == MediaRefTypes.Car && a.RefId == carId, cancellationToken)).ToList()
            };

            // Kreditlərin əməliyyatları da surətə düşür.
            foreach (var credit in snapshot.Credits)
            {
                var transactions = await _transactions.FindAsync(t => t.CreditId == credit.Id, cancellationToken);
                foreach (var transaction in transactions)
                {
                    snapshot.Transactions.Add(transaction);
                }

                // ⏳ Kreditin İLKİN ÖDƏNİŞ möhlətləri də surətə düşür ✓✓✓ (v6.2.11)
                foreach (var mohlet in await _mohletler.FindAsync(
                             m => m.CreditId == credit.Id, cancellationToken))
                {
                    snapshot.Mohletler.Add(mohlet);
                }
            }

            // ⏳ Satışların (nisyə) möhlətləri də surətə düşür ✓✓✓ (v6.2.11)
            foreach (var sale in snapshot.Sales)
            {
                foreach (var mohlet in await _mohletler.FindAsync(
                             m => m.SaleId == sale.Id, cancellationToken))
                {
                    snapshot.Mohletler.Add(mohlet);
                }
            }

            foreach (var expense in snapshot.Expenses)
            {
                expense.Car = null;
            }

            foreach (var sale in snapshot.Sales)
            {
                sale.Car = null;
            }

            foreach (var credit in snapshot.Credits)
            {
                credit.Car = null;
            }

            snapshot.Note =
                $"{snapshot.Summary} · maya {car.MayaDeyeri:N2} ₼ · status {car.Status}";

            return await WriteAsync(snapshot, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TrashSnapshot?> BackupSaleAsync(int saleId, CancellationToken cancellationToken = default)
        {
            var sale = await _sales.GetByIdAsync(saleId, cancellationToken);
            if (sale is null)
            {
                return null;
            }

            var snapshot = new TrashSnapshot
            {
                Kind = "Satış",
                Title = string.IsNullOrWhiteSpace(sale.MuqavileNomresi)
                    ? $"{sale.Mustəri} — {sale.SatisQiymeti:N2} ₼"
                    : $"{sale.MuqavileNomresi} — {sale.Mustəri}",
                Amount = sale.SatisQiymeti,
                Sales = { sale }
            };

            // ⏳ Satışın MÖHLƏTLƏRİ də surətə düşür ✓✓✓ (v6.2.11)
            //  (əks halda Ctrl+Z bərpasından sonra möhlətlər İTİRİRDİ ✗)
            foreach (var mohlet in await _mohletler.FindAsync(
                         m => m.SaleId == saleId, cancellationToken))
            {
                snapshot.Mohletler.Add(mohlet);
            }

            snapshot.Note = $"{sale.SatisTarixi:dd.MM.yyyy} · {sale.Mustəri} · {sale.SatisQiymeti:N2} ₼" +
                            (snapshot.Mohletler.Count > 0 ? $" · {snapshot.Mohletler.Count} möhlət" : string.Empty);

            return await WriteAsync(snapshot, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TrashSnapshot?> BackupExpenseAsync(int expenseId, CancellationToken cancellationToken = default)
        {
            var expense = await _expenses.GetByIdAsync(expenseId, cancellationToken);
            if (expense is null)
            {
                return null;
            }

            var snapshot = new TrashSnapshot
            {
                Kind = "Xərc",
                Title = string.IsNullOrWhiteSpace(expense.Kategoriya)
                    ? expense.Qrup
                    : $"{expense.Kategoriya} — {expense.Mebleg:N2} ₼",
                Amount = expense.Mebleg,
                Expenses = { expense }
            };

            snapshot.Note =
                $"{expense.Tarix:dd.MM.yyyy} · {expense.Teyinat} · {expense.Mebleg:N2} ₼";

            return await WriteAsync(snapshot, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TrashSnapshot?> BackupCreditAsync(int creditId, CancellationToken cancellationToken = default)
        {
            var credit = await _credits.GetByIdAsync(creditId, cancellationToken);
            if (credit is null)
            {
                return null;
            }

            var snapshot = new TrashSnapshot
            {
                Kind = "Kredit",
                Title = string.IsNullOrWhiteSpace(credit.MuqavileNomresi)
                    ? $"{credit.Mustəri} — {credit.Mebleg:N2} ₼"
                    : $"{credit.MuqavileNomresi} — {credit.Mustəri}",
                Amount = credit.Mebleg,
                Credits = { credit }
            };

            foreach (var transaction in await _transactions.FindAsync(
                         t => t.CreditId == creditId, cancellationToken))
            {
                snapshot.Transactions.Add(transaction);
            }

            // ⏳ Kreditin MÖHLƏTLƏRİ də surətə düşür ✓✓✓ (v6.2.11)
            //  (əks halda Ctrl+Z bərpasından sonra möhlətlər İTİRİRDİ ✗)
            foreach (var mohlet in await _mohletler.FindAsync(
                         m => m.CreditId == creditId, cancellationToken))
            {
                snapshot.Mohletler.Add(mohlet);
            }

            snapshot.Note =
                $"{credit.BaslamaTarixi:dd.MM.yyyy} · {credit.Mebleg:N2} ₼ · " +
                $"{snapshot.Transactions.Count} əməliyyat" +
                (snapshot.Mohletler.Count > 0 ? $" · {snapshot.Mohletler.Count} möhlət" : string.Empty);

            return await WriteAsync(snapshot, cancellationToken);
        }

        // --------------------------------------------- REDAKTƏ SURƏTLƏRİ ---

        /// <inheritdoc />
        public async Task<TrashSnapshot?> BackupCarEditAsync(int carId, CancellationToken cancellationToken = default)
        {
            var car = await _cars.GetWithDetailsAsync(carId, cancellationToken);
            if (car is null)
            {
                return null;
            }

            var snapshot = new TrashSnapshot
            {
                Kind = "Avtomobil",
                Action = "Dəyişiklik",
                Title = car.DisplayName,
                Amount = car.MayaDeyeri,
                Car = StripCar(car)
            };

            snapshot.Note =
                $"Redaktədən ƏVVƏLKİ vəziyyət · status {car.Status} · sıra № {car.SiraNomresi}";

            return await WriteAsync(snapshot, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TrashSnapshot?> BackupExpenseEditAsync(int expenseId, CancellationToken cancellationToken = default)
        {
            var expense = await _expenses.GetByIdAsync(expenseId, cancellationToken);
            if (expense is null)
            {
                return null;
            }

            var snapshot = new TrashSnapshot
            {
                Kind = "Xərc",
                Action = "Dəyişiklik",
                Title = string.IsNullOrWhiteSpace(expense.Kategoriya)
                    ? expense.Qrup
                    : expense.Kategoriya,
                Amount = expense.Mebleg,
                Expenses = { expense }
            };

            snapshot.Note =
                $"ƏVVƏLKİ: {expense.Teyinat} › {expense.Qrup} › {expense.Kategoriya} · {expense.Mebleg:N2} ₼";

            return await WriteAsync(snapshot, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TrashSnapshot?> BackupCreditEditAsync(int creditId, CancellationToken cancellationToken = default)
        {
            var credit = await _credits.GetByIdAsync(creditId, cancellationToken);
            if (credit is null)
            {
                return null;
            }

            var snapshot = new TrashSnapshot
            {
                Kind = "Kredit",
                Action = "Dəyişiklik",
                Title = string.IsNullOrWhiteSpace(credit.MuqavileNomresi)
                    ? credit.Mustəri
                    : credit.MuqavileNomresi,
                Amount = credit.Mebleg,
                Credits = { credit }
            };

            // ⏳ Redaktədən ƏVVƏLKİ möhlətlər də surətə düşür ✓✓✓ (v6.2.11)
            //  (əks halda kredit redaktəsi Ctrl+Z ilə geri alınsa möhlətlər
            //   YENİ vəziyyətdə qalırdı ✗ → «gözlənilən möhlət» səhv olurdu ✗)
            foreach (var mohlet in await _mohletler.FindAsync(
                         m => m.CreditId == creditId, cancellationToken))
            {
                snapshot.Mohletler.Add(mohlet);
            }

            snapshot.Note = $"ƏVVƏLKİ: {credit.Mustəri} · {credit.Mebleg:N2} ₼ · {credit.Status}" +
                            (snapshot.Mohletler.Count > 0 ? $" · {snapshot.Mohletler.Count} möhlət" : string.Empty);

            return await WriteAsync(snapshot, cancellationToken);
        }

        // -------------------------------------------------------- KÖMƏKÇİLƏR ---

        /// <summary>
        /// Avtomobilin <b>kopyasını</b> yaradır.
        /// Orijinal EF tərəfindən izlənir — ona toxunmaq OLMAZ, ona görə surət çıxarılır.
        /// </summary>
        private static CarItem StripCar(CarItem car) => new()
        {
            Id = car.Id,
            Marka = car.Marka,
            QeydiyyatNisani = car.QeydiyyatNisani,
            Vin = car.Vin,
            Il = car.Il,
            Yurus = car.Yurus,
            Yanacaq = car.Yanacaq,
            AlisTarixi = car.AlisTarixi,
            AlisSaati = car.AlisSaati,
            AlisUsulu = car.AlisUsulu,
            BarterTesviri = car.BarterTesviri,
            BarterCarId = car.BarterCarId,
            BarterDeyeri = car.BarterDeyeri,
            BarterSaleId = car.BarterSaleId,
            AlisQiymeti = car.AlisQiymeti,
            SatisQiymeti = car.SatisQiymeti,
            Status = car.Status,
            KreditNomresi = car.KreditNomresi,
            YaradilmaTarixi = car.YaradilmaTarixi,
            SiraNomresi = car.SiraNomresi
        };

        /// <summary>Surəti diskə JSON kimi yazır və geri qaytarır.</summary>
        private async Task<TrashSnapshot?> WriteAsync(TrashSnapshot snapshot, CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(Folder);

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}"[..31] + ".json";
                var path = Path.Combine(Folder, fileName);

                // Sənəd faylları da surətə köçürülür ki, bərpada yerində olsun.
                await CopyMediaAsync(snapshot, fileName, cancellationToken);

                snapshot.FileName = fileName;

                var json = JsonSerializer.Serialize(snapshot, JsonOptions);
                await File.WriteAllTextAsync(path, json, cancellationToken);

                _logger.LogInformation(
                    "SİLİNMƏ SURƏTİ saxlanıldı: {Kind} «{Title}» → {File}",
                    snapshot.Kind, snapshot.Title, fileName);

                return snapshot;
            }
            catch (Exception ex)
            {
                // Surət yazıla bilməsə belə silmə davam edir (əsas əməliyyat dayanmamalıdır).
                _logger.LogError(ex, "Silinmə surəti yazıla bilmədi.");
                return null;
            }
        }

        /// <summary>
        /// Sənəd / media fayllarını surət qovluğuna köçürür ki, bərpa
        /// zamanı həmin fayllar yerinə qaytarılsın.
        /// </summary>
        private async Task CopyMediaAsync(
            TrashSnapshot snapshot,
            string fileName,
            CancellationToken cancellationToken)
        {
            if (snapshot.Attachments.Count == 0)
            {
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var target = Path.Combine(Folder, "media", baseName);
            Directory.CreateDirectory(target);

            foreach (var attachment in snapshot.Attachments)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(attachment.StoredPath)
                        || !File.Exists(attachment.StoredPath))
                    {
                        continue;
                    }

                    var copy = Path.Combine(target, Path.GetFileName(attachment.StoredPath));
                    File.Copy(attachment.StoredPath, copy, overwrite: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sənəd faylı surətə köçürülə bilmədi: {Path}", attachment.StoredPath);
                }
            }

            await Task.CompletedTask;
        }

        // ------------------------------------------------------------ OXUMA ---

        /// <inheritdoc />
        public async Task<IReadOnlyList<TrashSnapshot>> GetEntriesAsync(CancellationToken cancellationToken = default)
        {
            var result = new List<TrashSnapshot>();

            if (!Directory.Exists(Folder))
            {
                return result;
            }

            foreach (var file in Directory.GetFiles(Folder, "*.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken);
                    var snapshot = JsonSerializer.Deserialize<TrashSnapshot>(json, JsonOptions);

                    if (snapshot is not null)
                    {
                        snapshot.FileName = Path.GetFileName(file);
                        result.Add(snapshot);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Silinmə surəti oxuna bilmədi: {File}", file);
                }
            }

            return result.OrderByDescending(s => s.DeletedAt).ToList();
        }

        // ----------------------------------------------------------- BƏRPA ---

        /// <inheritdoc />
        public async Task<TrashSnapshot?> RestoreLastAsync(CancellationToken cancellationToken = default)
        {
            var entries = await GetEntriesAsync(cancellationToken);
            var last = entries.FirstOrDefault();

            return last is null ? null : await RestoreAsync(last.FileName, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TrashSnapshot?> RestoreAsync(string fileName, CancellationToken cancellationToken = default)
        {
            var path = Path.Combine(Folder, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            TrashSnapshot? snapshot;
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                snapshot = JsonSerializer.Deserialize<TrashSnapshot>(json, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Surət oxuna bilmədi: {File}", fileName);
                return null;
            }

            if (snapshot is null)
            {
                return null;
            }

            // Əvvəlcə: DƏYİŞİKLİK surətidirsə mövcud qeyd ƏVVƏLKİ vəziyyətinə qaytarılır.
            if (snapshot.IsEdit)
            {
                return await RevertEditAsync(snapshot, path, cancellationToken);
            }

            // Mövcud avtomobillər — Id xəritəsi və sıra nömrəsi yoxlaması üçün.
            var existingCars = await _cars.GetAllAsync(cancellationToken);
            var existingCarIds = existingCars.Select(c => c.Id).ToHashSet();

            // Aktiv parkdaki sıra nömrələri (əvvəlki nömrəni bərpa etmək üçün).
            var existingSira = existingCars
                .Where(c => c.SiraNomresi > 0 && !Catalog.IsOutOfPark(c.Status))
                .Select(c => c.SiraNomresi)
                .ToHashSet();

            var carIdMap = new Dictionary<int, int>();

            int? MapCar(int? id)
            {
                if (!id.HasValue)
                {
                    return null;
                }

                if (carIdMap.TryGetValue(id.Value, out var newId))
                {
                    return newId;
                }

                // Xəritədə yoxdursa: maşın hələ də varsa saxlanılır, yoxsa əlaqə kəsilir.
                return existingCarIds.Contains(id.Value) ? id : null;
            }

            // ---- 1) Avtomobil ----
            if (snapshot.Car is { } car)
            {
                var oldCarId = car.Id;

                // SIRA NÖMRƏSİ əvvəlki kimi bərpa olunur; yer tutulubsa yeni verilir.
                var wanted = car.SiraNomresi;

                car.Id = 0;

                if (wanted > 0 && !existingSira.Contains(wanted))
                {
                    car.SiraNomresi = wanted;
                }
                else
                {
                    car.SiraNomresi = NextFreeSira(existingSira);
                }

                existingSira.Add(car.SiraNomresi);

                await _cars.AddAsync(car, cancellationToken);
                await _cars.SaveChangesAsync(cancellationToken);
                carIdMap[oldCarId] = car.Id;
            }

            // ---- 2) Xərclər ----
            foreach (var expense in snapshot.Expenses)
            {
                expense.Id = 0;
                expense.CarId = MapCar(expense.CarId);
                await _expenses.AddAsync(expense, cancellationToken);
            }

            // ---- 3) Satışlar ----
            var salePairs = new List<(int OldId, Sale Entity)>();
            foreach (var sale in snapshot.Sales)
            {
                var oldSaleId = sale.Id;
                sale.Id = 0;
                sale.CarId = MapCar(sale.CarId);
                await _sales.AddAsync(sale, cancellationToken);
                salePairs.Add((oldSaleId, sale));
            }

            // ---- 4) Kreditlər ----
            var creditPairs = new List<(int OldId, Credit Entity)>();
            foreach (var credit in snapshot.Credits)
            {
                var oldCreditId = credit.Id;
                credit.Id = 0;
                credit.CarId = MapCar(credit.CarId);
                await _credits.AddAsync(credit, cancellationToken);
                creditPairs.Add((oldCreditId, credit));
            }

            await _cars.SaveChangesAsync(cancellationToken);

            // Hər repo öz kontekstində saxlayır (AppDbContext transient-dir).
            await _expenses.SaveChangesAsync(cancellationToken);
            await _sales.SaveChangesAsync(cancellationToken);
            await _credits.SaveChangesAsync(cancellationToken);

            // ---- 5) Kredit əməliyyatları (yeni kredit Id-lərinə bağlanır) ----
            var creditIdMap = creditPairs.ToDictionary(p => p.OldId, p => p.Entity.Id);

            foreach (var transaction in snapshot.Transactions)
            {
                transaction.Id = 0;

                if (transaction.CreditId.HasValue
                    && creditIdMap.TryGetValue(transaction.CreditId.Value, out var newCreditId))
                {
                    transaction.CreditId = newCreditId;
                }

                await _transactions.AddAsync(transaction, cancellationToken);
            }

            await _transactions.SaveChangesAsync(cancellationToken);

            // ================================================================
            //  ---- 5.5) ⏳ MÖHLƏTLƏR (yeni kredit / satış Id-lərinə bağlanır) ✓✓✓
            // ----------------------------------------------------------------
            //  İSTİFADƏÇİ TƏLƏBİ: kredit/satış silinəndə möhlətlər də silinir ✓
            //  və BƏRPA (Ctrl+Z) edildikdə GERİ QAYTARILIR ✓✓✓  (v6.2.11)
            //  ⚠ ƏVVƏL möhlətlər heç surətə salınmırdı ✗ → bərpadan sonra
            //  İTİRİRDİ ✗ (kassa proqnozu + ödəniş qrafiki səhv olurdu ✗)
            // ================================================================
            var saleIdMap = salePairs.ToDictionary(p => p.OldId, p => p.Entity.Id);

            foreach (var mohlet in snapshot.Mohletler)
            {
                var kohneKreditId = mohlet.CreditId;
                var kohneSatisId = mohlet.SaleId;

                mohlet.Id = 0;
                mohlet.Credit = null;
                mohlet.Sale = null;

                mohlet.CreditId =
                    kohneKreditId.HasValue
                    && creditIdMap.TryGetValue(kohneKreditId.Value, out var yeniKreditId)
                        ? yeniKreditId
                        : null;

                mohlet.SaleId =
                    kohneSatisId.HasValue
                    && saleIdMap.TryGetValue(kohneSatisId.Value, out var yeniSatisId)
                        ? yeniSatisId
                        : null;

                // ⚠ Sahibi bərpa olunmadısa möhlət YAZILMIR ✓ (sahibsiz sətir yaratmırıq ✗)
                if (mohlet.CreditId is null && mohlet.SaleId is null)
                {
                    continue;
                }

                await _mohletler.AddAsync(mohlet, cancellationToken);
            }

            if (snapshot.Mohletler.Count > 0)
            {
                await _mohletler.SaveChangesAsync(cancellationToken);
            }

            // ---- 6) Sənəd / media faylları ----
            await RestoreMediaAsync(snapshot, fileName, carIdMap, cancellationToken);

            // Surət artıq lazım deyil.
            DeleteFile(path);

            _logger.LogWarning(
                "SİLİNMİŞ QEYD GERİ QAYTARILDI: {Kind} «{Title}» ({Summary})",
                snapshot.Kind, snapshot.Title, snapshot.Summary);

            return snapshot;
        }

        /// <summary>Surətdən sənəd / media fayllarını yerinə qaytarır.</summary>
        private async Task RestoreMediaAsync(
            TrashSnapshot snapshot,
            string fileName,
            Dictionary<int, int> carIdMap,
            CancellationToken cancellationToken)
        {
            if (snapshot.Attachments.Count == 0)
            {
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var source = Path.Combine(Folder, "media", baseName);

            foreach (var attachment in snapshot.Attachments)
            {
                var newRefId = attachment.RefId;

                if (attachment.RefType == MediaRefTypes.Car
                    && carIdMap.TryGetValue(attachment.RefId, out var newCarId))
                {
                    newRefId = newCarId;
                }

                var target = attachment.StoredPath;
                var original = Path.Combine(source, Path.GetFileName(attachment.StoredPath ?? string.Empty));

                try
                {
                    if (File.Exists(original) && !string.IsNullOrWhiteSpace(target))
                    {
                        var dir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        File.Copy(original, target, overwrite: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Sənəd faylı geri qaytarıla bilmədi: {Path}", target);
                }

                attachment.Id = 0;
                attachment.RefId = newRefId;
                await _attachments.AddAsync(attachment, cancellationToken);
            }

            await _attachments.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Boş qalan ən kiçik sıra nömrəsini tapır (1, 2, 3...).</summary>
        private static int NextFreeSira(HashSet<int> used)
        {
            var next = 1;

            while (used.Contains(next))
            {
                next++;
            }

            return next;
        }

        /// <summary>
        /// DƏYİŞİKLİK surətini geri alır: mövcud qeyd yenidən yaradılmır —
        /// sadəcə <b>əvvəlki dəyərlərinə qaytarılır</b>.
        /// <para>
        /// Xərcin kateqoriyası/qrupu səhvən dəyişdirildikdə, kredit məlumatları
        /// və ya avtomobilin sahələri korlandıqda istifadə olunur.
        /// </para>
        /// </summary>
        private async Task<TrashSnapshot?> RevertEditAsync(
            TrashSnapshot snapshot,
            string path,
            CancellationToken cancellationToken)
        {
            var reverted = 0;

            // ---- XƏRC (kateqoriya / qrup / məbləğ və s.) ----
            foreach (var expense in snapshot.Expenses)
            {
                var existing = await _expenses.GetByIdAsync(expense.Id, cancellationToken);
                if (existing is null)
                {
                    continue;
                }

                existing.Tarix = expense.Tarix;
                existing.Teyinat = expense.Teyinat;
                existing.Qrup = expense.Qrup;
                existing.Kategoriya = expense.Kategoriya;
                existing.CarId = expense.CarId;
                existing.Mebleg = expense.Mebleg;
                existing.OdenisUsulu = expense.OdenisUsulu;
                existing.Qeyd = expense.Qeyd;
                reverted++;
            }

            if (reverted > 0)
            {
                await _expenses.SaveChangesAsync(cancellationToken);
            }

            var expenseReverted = reverted;
            reverted = 0;

            // ---- KREDİT ----
            foreach (var credit in snapshot.Credits)
            {
                var existing = await _credits.GetByIdAsync(credit.Id, cancellationToken);
                if (existing is null)
                {
                    continue;
                }

                existing.MuqavileNomresi = credit.MuqavileNomresi;
                existing.Mustəri = credit.Mustəri;
                existing.CarId = credit.CarId;
                existing.Mebleg = credit.Mebleg;
                existing.IlkinOdenis = credit.IlkinOdenis;
                existing.FaizDerecesi = credit.FaizDerecesi;
                existing.MuddetAy = credit.MuddetAy;
                existing.AylıqOdenis = credit.AylıqOdenis;
                existing.BaslamaTarixi = credit.BaslamaTarixi;
                existing.Status = credit.Status;
                existing.Qeyd = credit.Qeyd;
                reverted++;
            }

            if (reverted > 0)
            {
                await _credits.SaveChangesAsync(cancellationToken);
            }

            var creditReverted = reverted;
            reverted = 0;

            // ================================================================
            //  ---- ⏳ MÖHLƏTLƏR (kredit redaktəsi geri alınır) ✓✓✓  (v6.2.11)
            // ----------------------------------------------------------------
            //  Kredit redaktə olunanda möhlətlər də dəyişə bilər ✓ →
            //  Ctrl+Z edildikdə ƏVVƏLKİ möhlət siyahısı BÜTÖV bərpa olunur ✓
            //  (əvvəlki sətirlər silinir ✓ surətdəkilər yenidən yazılır ✓)
            // ================================================================
            var mohletReverted = 0;

            if (snapshot.Mohletler.Count > 0)
            {
                var kreditIdleri = snapshot.Mohletler
                    .Where(m => m.CreditId.HasValue)
                    .Select(m => m.CreditId!.Value)
                    .Distinct()
                    .ToList();

                foreach (var kreditId in kreditIdleri)
                {
                    _mohletler.ClearTracker();
                    await _mohletler.DeleteWhereAsync(
                        m => m.CreditId == kreditId, cancellationToken);

                    foreach (var mohlet in snapshot.Mohletler.Where(x => x.CreditId == kreditId))
                    {
                        mohlet.Id = 0;
                        mohlet.Credit = null;
                        mohlet.Sale = null;

                        await _mohletler.AddAsync(mohlet, cancellationToken);
                        mohletReverted++;
                    }
                }

                await _mohletler.SaveChangesAsync(cancellationToken);
            }

            // ---- AVTOMOBİL ----
            if (snapshot.Car is { } car)
            {
                var existing = await _cars.GetByIdAsync(car.Id, cancellationToken);
                if (existing is not null)
                {
                    existing.Marka = car.Marka;
                    existing.QeydiyyatNisani = car.QeydiyyatNisani;
                    existing.Vin = car.Vin;
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
                    existing.SiraNomresi = car.SiraNomresi;
                    reverted++;
                }

                await _cars.SaveChangesAsync(cancellationToken);
            }

            DeleteFile(path);

            _logger.LogWarning(
                "DƏYİŞİKLİK GERİ ALINDI: {Kind} «{Title}» — {Exp} xərc, {Credit} kredit, " +
                "{Car} avtomobil, {Mohlet} möhlət",
                snapshot.Kind, snapshot.Title, expenseReverted, creditReverted, reverted, mohletReverted);

            return snapshot;
        }

        // -------------------------------------------------------- TƏMİZLİK ---

        /// <inheritdoc />
        public async Task<bool> DeletePermanentlyAsync(string fileName, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            var path = Path.Combine(Folder, fileName);
            if (!File.Exists(path))
            {
                return false;
            }

            DeleteFile(path);

            // Media surətini də silirik.
            try
            {
                var media = Path.Combine(Folder, "media", Path.GetFileNameWithoutExtension(fileName));
                if (Directory.Exists(media))
                {
                    Directory.Delete(media, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Media surəti silinə bilmədi: {Name}", fileName);
            }

            _logger.LogWarning("Silinmə surəti BİRDƏFƏLİK silindi: {File}", fileName);
            return true;
        }

        /// <inheritdoc />
        public async Task<int> CleanupAsync(int keep = 200, CancellationToken cancellationToken = default)
        {
            var entries = await GetEntriesAsync(cancellationToken);

            if (entries.Count <= keep)
            {
                return 0;
            }

            var removed = 0;
            foreach (var entry in entries.Skip(keep))
            {
                if (await DeletePermanentlyAsync(entry.FileName, cancellationToken))
                {
                    removed++;
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation("Köhnə silinmə surətləri təmizləndi: {Count} ədəd.", removed);
            }

            return removed;
        }

        /// <summary>Faylı səssizcə silir.</summary>
        private void DeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fayl silinə bilmədi: {Path}", path);
            }
        }
    }
}
