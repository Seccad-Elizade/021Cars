using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class CreditService : ICreditService
    {
        private readonly ICreditRepository _credits;
        private readonly IRepository<CreditTransaction> _transactions;
        private readonly IPartnerShareRepository _shares;
        private readonly ICarRepository _cars;

        /// <summary>📤 Transfer = nağd SATIŞ ✓ → xeyir bizdə qalır ✓.</summary>
        private readonly ISaleRepository _sales;

        /// <summary>
        /// ⏳ Möhlətlər — kredit SİLİNƏNDƏ onun möhlətləri də silinməlidir ✓✓✓
        /// (bax: <see cref="DeleteCreditAsync"/>).
        /// </summary>
        private readonly IOdenisMohletRepository _mohletler;

        private readonly ITrashService _trash;
        private readonly ILogger<CreditService> _logger;

        public CreditService(
            ICreditRepository credits,
            IRepository<CreditTransaction> transactions,
            IPartnerShareRepository shares,
            ICarRepository cars,
            ISaleRepository sales,
            IOdenisMohletRepository mohletler,
            ITrashService trash,
            ILogger<CreditService> logger)
        {
            _credits = credits;
            _transactions = transactions;
            _shares = shares;
            _cars = cars;
            _sales = sales;
            _mohletler = mohletler;
            _trash = trash;
            _logger = logger;
        }

        public Task<IReadOnlyList<Credit>> GetCreditsAsync(CancellationToken cancellationToken = default)
            => _credits.GetWithCarAsync(cancellationToken);

        public async Task<Credit> AddCreditAsync(Credit credit, CancellationToken cancellationToken = default)
        {
            await _credits.AddAsync(credit, cancellationToken);
            await _credits.SaveChangesAsync(cancellationToken);
            return credit;
        }

        public async Task UpdateCreditAsync(Credit credit, CancellationToken cancellationToken = default)
        {
            var existing = await _credits.GetByIdAsync(credit.Id, cancellationToken);
            if (existing is null)
            {
                return;
            }

            // Redaktədən ƏVVƏLKİ vəziyyət surətə götürülür (Ctrl+Z ilə geri almaq üçün).
            await _trash.BackupCreditEditAsync(credit.Id, cancellationToken);

            existing.MuqavileNomresi = credit.MuqavileNomresi;
            existing.Mustəri = credit.Mustəri;
            existing.CarId = credit.CarId;
            existing.Mebleg = credit.Mebleg;
            existing.IlkinOdenis = credit.IlkinOdenis;
            existing.FaizDerecesi = credit.FaizDerecesi;
            existing.MuddetAy = credit.MuddetAy;

            // ================================================================
            //  ✅ STATUS və QEYD də KOPYALANIR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏLKİ SƏHV: bu sahələr kopyalanmırdı ✗ →
            //  📕 «Vaxtından tez bağlama» edildikdə kredit «Bağlı» edilirdi ✗
            //  amma `UpdateCreditAsync` onu bazaya YAZMIRDI ✗ →
            //  kredit «💳 Kreditlər»-dən SİLİNMİRDİ ✗✓✓
            // ================================================================
            existing.Status = credit.Status;
            existing.Qeyd = credit.Qeyd;
            existing.AylıqOdenis = credit.AylıqOdenis;
            existing.BaslamaTarixi = credit.BaslamaTarixi;
            existing.Status = credit.Status;
            existing.Qeyd = credit.Qeyd;

            await _credits.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteCreditAsync(int id, CancellationToken cancellationToken = default)
        {
            var credit = await _credits.GetByIdAsync(id, cancellationToken);
            if (credit is null)
            {
                return;
            }

            // Silmədən əvvəl bərpa surəti (əməliyyatlar daxil).
            await _trash.BackupCreditAsync(id, cancellationToken);

            // Uzunömürlü kontekstlərdəki köhnə (stale) nüsxələr buraxılır —
            // əks halda artıq bazada olmayan sətirlər üçün DELETE göndərilir və
            // «expected to affect 1 row(s), but actually affected 0» xətası yaranır.
            _credits.ClearTracker();
            _transactions.ClearTracker();
            _shares.ClearTracker();
            _mohletler.ClearTracker();

            // ================================================================
            //  ⏳ KREDİTİN MÖHLƏTLƏRİ DƏ SİLİNİR ✓✓✓  (v6.2.11)
            // ----------------------------------------------------------------
            //  İSTİFADƏÇİ TƏLƏBİ: «kredit verilibsə möhlət, ödənilibsə və ya
            //  ödənilməyibsə — kredit SİLİNƏNDƏ möhlətlər DƏ silinməlidir» ✓
            //
            //  ⚠ Möhlətlər əvvəl yalnız BAZADAKI «ON DELETE CASCADE» ilə
            //  silinirdi ✗ → bu, `PRAGMA foreign_keys` açıq olmasından ASILI
            //  idi ✗ (başqa alətlə yazılmış / köhnə bazalarda SÖNÜK ola bilər ✗
            //  → möhlətlər SAHİBSİZ qalır ✗ → 📅 Təqvim və 🔔 Bildirişlər
            //  tablarında silinmiş kreditin möhləti görünürdü ✗✓✓).
            //  ✅ İNDİ: AÇIQ silinir ✓ — heç bir xarici şərtdən asılı deyil ✓
            // ================================================================
            var silinenMohlet = await _mohletler.DeleteWhereAsync(
                m => m.CreditId == id, cancellationToken);

            // Kreditə bağlı hər şey BİRBAŞA BAZADAN silinir:
            //   1) tərəfdaş payları      2) ödəniş əməliyyatları
            //   3) MÖHLƏTLƏR ✓ (yuxarıda) 4) kredit özü
            await _shares.DeleteWhereAsync(p => p.CreditId == id, cancellationToken);
            await _transactions.DeleteWhereAsync(t => t.CreditId == id, cancellationToken);
            await _credits.DeleteWhereAsync(c => c.Id == id, cancellationToken);

            _logger.LogInformation(
                "🗑️ Kredit #{Id} silindi — əməliyyatlar, paylar və {Mohlet} möhlət də silindi ✓",
                id, silinenMohlet);
        }

        public async Task<IReadOnlyList<CreditTransaction>> GetTransactionsAsync(CancellationToken cancellationToken = default)
        {
            var transactions = await _transactions.FindAsync(_ => true, cancellationToken);

            // Tərəfdaş payları əməliyyatlara bağlanır (cədvəldə bölgü göstərilsin).
            var allShares = await _shares.GetAllOrderedAsync(cancellationToken);
            if (allShares.Count > 0)
            {
                var byTransaction = allShares
                    .Where(s => s.CreditTransactionId.HasValue)
                    .GroupBy(s => s.CreditTransactionId!.Value)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var transaction in transactions)
                {
                    if (byTransaction.TryGetValue(transaction.Id, out var shares))
                    {
                        transaction.TerefdasPaylari = shares;
                    }
                }
            }

            return transactions;
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<PartnerShare>> GetCreditSharesAsync(
            int creditId,
            CancellationToken cancellationToken = default)
            => _shares.FindAsync(p => p.CreditId == creditId, cancellationToken);

        /// <inheritdoc />
        public async Task SaveCreditSharesAsync(
            int creditId,
            bool tetbiqOlunub,
            IReadOnlyList<PartnerShare> rows,
            CancellationToken cancellationToken = default)
        {
            // Əvvəlki paylar BİRBAŞA BAZADAN silinir (stale-nüsxə probleminin qarşısı).
            await _shares.DeleteWhereAsync(p => p.CreditId == creditId, cancellationToken);

            if (!tetbiqOlunub || rows.Count == 0)
            {
                return;
            }

            var sira = 0;

            // ✅ YALNIZ REAL PAYLAR yazılır (`Mebleg > 0`) ✓✓✓
            //  Əvvəl işarəsiz (faiz almayan) tərəfdaşlar da `Mebleg = 0` ilə bazaya
            //  yazılırdı ✗ → «Kredit Ödəniş Qrafiki → Tərəfdaş bölgüsü» bölməsində
            //  onların adları görünürdü ✗✓
            foreach (var row in rows.Where(r => r.Mebleg > 0m).OrderBy(p => p.Sira))
            {
                await _shares.AddAsync(new PartnerShare
                {
                    CreditId = creditId,
                    Terefdas = row.Terefdas,
                    Faiz = row.Faiz,
                    Mebleg = row.Mebleg,
                    QaligPayi = row.QaligPayi,
                    Aktiv = true,                        // ← pay varsa deməli iştirak edir ✓
                    Sira = sira++
                }, cancellationToken);
            }

            await _shares.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<PartnerShare>> GetPartnerSharesAsync(
            int transactionId,
            CancellationToken cancellationToken = default)
            => _shares.GetByTransactionAsync(transactionId, cancellationToken);

        public async Task<CreditTransaction> AddTransactionAsync(CreditTransaction transaction, CancellationToken cancellationToken = default)
        {
            await _transactions.AddAsync(transaction, cancellationToken);
            await _transactions.SaveChangesAsync(cancellationToken);

            // ================================================================
            //  🩺 ClearTracker — KÖHNƏ İZLƏNƏN NÜSXƏLƏR TƏMİZLƏNİR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ XƏTA: «The database operation was expected to affect 1 row(s),
            //  but actually affected 0 row(s)» ✗ — tərəfdaş payları yazılarkən
            //  kontekstdə köhnə nüsxələr qalırdı ✗ → UPDATE «0 sətir» verirdi ✗✓✓
            //  (mövcud kod üslubu: DeleteTransactionAsync-də də ClearTracker var ✓)
            // ================================================================
            _shares.ClearTracker();
            _transactions.ClearTracker();

            // Tərəfdaş bölgüsü tətbiq olunubsa paylar da yazılır.
            await SavePartnerSharesAsync(transaction, cancellationToken);

            // 📤 TRANSFER — kredit bağlanır, avtomobil «Satılan & Krediti Bitmiş»-ə keçir ✓
            await RefreshTransferAsync(transaction, cancellationToken);

            // ================================================================
            //  ⛔ AVTOMATİK BAĞLANMA LƏĞV EDİLDİ ✗✓✓ (v6.2.34)
            // ----------------------------------------------------------------
            //  ★ İstifadəçi tələbi: «Skript idxalı edəndə qalıq 0 qalır amma
            //    maşın avtomatik krediti bitmiş tabına gedir ✗ — getməməlidir ✗!
            //    Mən ƏL İLƏ «⏹ KREDİTİ BİTDİ» düyməsinə basmalıyam ✓» ✓✓✓
            //
            //  ⚠ ƏVVƏL: hər ödənişdən sonra `RefreshCreditCompletionAsync`
            //    çağırılırdı ✗ → ödənişlər kreditin qiymətinə çatanda kredit
            //    ÖZÜ «Bağlı» olur, avtomobil ÖZÜ «Satıldı» olub arxivə keçirdi ✗
            //    (istifadəçi bunu İSTƏMİR ✗ — əl ilə idarə etmək istəyir ✓)
            //
            //  ✅ İNDİ: status YALNIZ «⏹ KREDİTİ BİTDİ» düyməsi ilə dəyişir ✓
            //    (📤 Transfer isə əvvəlki kimi AVTOMATİK bağlanır ✓ — bu,
            //     ödəniş yox, maşının verilməsi əməliyyatıdır ✓)
            // ================================================================
            //  ✅ Status dəyişmir ✗ — yalnız əməliyyat qeyd olunur ✓
            // ================================================================

            return transaction;
        }

        /// <summary>
        /// Əməliyyatın tərəfdaş paylarını yenidən yazır — əvvəlki paylar silinir,
        /// sonra formadaki cari vəziyyət (manual düzəlişlər daxil) saxlanılır.
        /// <para>
        /// <c>TerefdasBolguTetbiqOlunub = false</c> olduqda yalnız silinmə aparılır —
        /// beləliklə checkbox söndürüləndə bölgü tamamilə ləğv olunur.
        /// </para>
        /// </summary>
        private async Task SavePartnerSharesAsync(CreditTransaction transaction, CancellationToken cancellationToken)
        {
            // Əvvəlki paylar BİRBAŞA BAZADAN silinir (stale-nüsxə probleminin qarşısı).
            await _shares.DeleteWhereAsync(
                p => p.CreditTransactionId == transaction.Id,
                cancellationToken);

            // ✅ YALNIZ REAL PAYLAR (`Mebleg > 0`) ✓✓✓
            var paylar = transaction.TerefdasPaylari
                .Where(s => s.Mebleg > 0m)          // ← 0,00 paylar YAZILMIR ✓
                .OrderBy(p => p.Sira)
                .ToList();

            // ================================================================
            //  ⚠️ GECİKMƏ CƏRİMƏSİ TƏRƏFDAŞLARA BÖLÜNMÜR ✗✓✓  (v6.2.28)
            // ----------------------------------------------------------------
            //  Cərimə pulu YALNIZ «💵 Kassa»ya (GƏLİRƏ) yazılır ✓✓✓
            //  (bax: <see cref="KassaHesabi"/> — «Gecikmə + Ödənilib» ✓ gəlir sayılır ✓)
            //
            //  • Tərəfdaş PAYI YARADILMIR ✗
            //  • Əvvəl (köhnə versiyalarda) yazılmış 50/50 paylar isə yuxarıda
            //    BİRBAŞA BAZADAN SİLİNİR ✓ → self-healing ✓✓✓
            // ================================================================
            if (string.Equals(transaction.Nov, "Gecikmə", StringComparison.Ordinal))
            {
                return;
            }

            // ================================================================
            //  🩺 SELF-HEALING ✓✓✓ — «bölgü TƏTBİQ OLUNUB ✓ amma məbləğlər 0» ✗
            // ----------------------------------------------------------------
            //  🐞 REAL XƏTA (istifadəçi): «əməliyyatlar görsənir amma məbləğ 0 yazır» ✗
            //  Səbəb: formadaki pay məbləğləri hesablanmadan (0) göndərilirdi ✗ →
            //  `Mebleg > 0` filtri onları ATIRDI ✗ → jurnalda pay ÜMUMİYYƏTLƏ
            //  yaranmırdı ✗ (və ya 0 görünürdü ✗)✓✓
            //
            //  ✅ İNDİ: bölgü işarələnibsə ✓ və baza varsa ✓ → paylar BURADA
            //  yenidən hesablanır ✓ (düstur: <see cref="PartnerMath.Distribute"/> ✓)
            // ================================================================
            if (paylar.Count == 0
                && transaction.TerefdasBolguTetbiqOlunub
                && transaction.BolguBazasi is decimal baza && baza > 0m)
            {
                var duzeldilmis = PartnerMath.CreateDefaultRows();

                PartnerMath.Distribute(baza, duzeldilmis);

                paylar = duzeldilmis
                    .Where(s => s.Mebleg > 0m)
                    .OrderBy(s => s.Sira)
                    .ToList();
            }

            if (!transaction.TerefdasBolguTetbiqOlunub || paylar.Count == 0)
            {
                return;
            }

            var sira = 0;

            foreach (var share in paylar)
            {
                await _shares.AddAsync(new PartnerShare
                {
                    CreditTransactionId = transaction.Id,
                    Terefdas = share.Terefdas,
                    Faiz = share.Faiz,
                    Mebleg = share.Mebleg,
                    QaligPayi = share.QaligPayi,
                    Aktiv = true,                        // ← pay varsa deməli iştirak edir ✓
                    Sira = sira++
                }, cancellationToken);
            }

            await _shares.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Mövcud əməliyyatı yeniləyir (izlənilən nüsxəyə skalyar sahələr köçürülür).</summary>
        public async Task UpdateTransactionAsync(CreditTransaction transaction, CancellationToken cancellationToken = default)
        {
            // ================================================================
            //  🩺 ClearTracker — «0 row(s) affected» XƏTASI DÜZƏLDİLİR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ «Gecikmə» qeydində «ödənilib» checkbox-unu dəyişəndə xəta
            //  verirdi ✗: «The database operation was expected to affect
            //  1 row(s), but actually affected 0 row(s)» ✗ —
            //  səbəb: kontekstdə köhnə izlənən nüsxə qalırdı ✗✓✓
            // ================================================================
            _transactions.ClearTracker();
            _shares.ClearTracker();

            var existing = await _transactions.GetByIdAsync(transaction.Id, cancellationToken);
            if (existing is null)
            {
                return;
            }

            existing.Nov = transaction.Nov;
            existing.Mebleg = transaction.Mebleg;
            existing.Tarix = transaction.Tarix;
            existing.InstallmentNo = transaction.InstallmentNo;
            existing.MohletTarixi = transaction.MohletTarixi;
            existing.GecikmeTarixi = transaction.GecikmeTarixi;
            existing.Odenilib = transaction.Odenilib;
            existing.Tesvir = transaction.Tesvir;

            // ---- Tərəfdaş mənfəət bölgüsü ----
            existing.TerefdasBolguTetbiqOlunub = transaction.TerefdasBolguTetbiqOlunub;
            existing.BolguBazasi = transaction.TerefdasBolguTetbiqOlunub
                ? transaction.BolguBazasi
                : null;

            await _transactions.SaveChangesAsync(cancellationToken);

            // Paylar izlənilən əməliyyata yazılır ki, ID düzgün olsun.
            // ================================================================
            //  ✅ PAYLAR BOŞ GƏLİBSƏ → KÖHNƏ PAYLAR BAZADAN SİLİNİR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏLKİ SƏHV: «ödənilməyib» ediləndə paylar boş göndərilirdi ✗
            //  amma `SavePartnerSharesAsync` YALNIZ yeni sətirləri yazırdı ✗ —
            //  KÖHNƏLƏRİ SİLMİRDİ ✗ → kartlarda 573,04 ₼ QALIRDI ✗✓✓
            // ================================================================
            if (transaction.TerefdasPaylari.Count == 0)
            {
                _shares.ClearTracker();

                await _shares.DeleteWhereAsync(
                    p => p.CreditTransactionId == existing.Id,
                    cancellationToken);
            }

            existing.TerefdasPaylari = transaction.TerefdasPaylari;
            await SavePartnerSharesAsync(existing, cancellationToken);

            // ⛔ v6.2.34 — AVTOMATİK BAĞLANMA LƏĞV EDİLDİ ✗✓✓
            //    Məbləğ dəyişsə də kredit statusu AVTOMATİK dəyişmir ✗ —
            //    yalnız «⏹ KREDİTİ BİTDİ» düyməsi ilə bağlanır ✓
        }

        /// <summary>
        /// 📕 <b>KREDİTİ VAXTINDAN TEZ BAĞLAYIR</b> ✓✓✓
        /// <para>
        /// ① Kredit → «Bağlı» ✓ (qeyd yazılır ✓) →
        ///    «💳 Kreditlər»-dən ÇIXIR ✓ və «🗄️ Krediti Bitmiş»-ə düşür ✓
        /// <br/>
        /// ② Avtomobil → «Satıldı» ✓ → 🚘 parkdan ÇIXIR ✓,
        ///    «🟠 Kreditdə» göstəricisi AZALIR ✓✓✓
        /// </para>
        /// <para>
        /// ⚠ <c>ClearTracker()</c> ilə — köhnə izlənən nüsxə UPDATE-i
        /// bloklamasın ✓ (mövcud kod üslubu ✓).
        /// </para>
        /// </summary>
        public async Task<bool> CloseCreditEarlyAsync(
            int creditId,
            string qeyd,
            CancellationToken cancellationToken = default)
        {
            // ---- ① KREDİT «BAĞLI» OLUR ------------------------------------
            _credits.ClearTracker();

            var credit = await _credits.GetByIdAsync(creditId, cancellationToken);

            if (credit is null)
            {
                return false;
            }

            credit.Status = "Bağlı";
            credit.Qeyd = qeyd;

            await _credits.SaveChangesAsync(cancellationToken);

            // ---- ② AVTOMOBİL «SATILDI» OLUR (parkdan çıxır ✓) ---------------
            if (credit.CarId is int carId)
            {
                _cars.ClearTracker();

                var car = await _cars.GetByIdAsync(carId, cancellationToken);

                if (car is not null
                    && !string.Equals(car.Status, Catalog.SoldStatus, StringComparison.Ordinal))
                {
                    car.Status = Catalog.SoldStatus;
                    await _cars.SaveChangesAsync(cancellationToken);
                }
            }

            _logger.LogInformation(
                "📕 Vaxtından tez bağlama ✓: kredit {Muqavile} BAĞLI · avtomobil «{Status}» · qeyd: {Qeyd}",
                credit.MuqavileNomresi, Catalog.SoldStatus, qeyd);

            return true;
        }

        public async Task DeleteTransactionAsync(int id, CancellationToken cancellationToken = default)
        {
            // Uzunömürlü kontekstdəki köhnə (stale) nüsxələr buraxılır.
            _transactions.ClearTracker();
            _shares.ClearTracker();

            // 📤 TRANSFER silinirsə → kredit + avtomobil + satış GERİ ALINIR ✓
            CreditTransaction? silinen = null;
            try
            {
                silinen = await _transactions.GetByIdAsync(id, cancellationToken);
            }
            catch
            {
                // Oxu xətası kritik deyil — silmə davam edir.
            }

            // 1) ƏVVƏLCƏ tərəfdaş payları BİRBAŞA BAZADAN silinir.
            await _shares.DeleteWhereAsync(p => p.CreditTransactionId == id, cancellationToken);

            // 2) Sonra əməliyyatın özü.
            await _transactions.DeleteWhereAsync(t => t.Id == id, cancellationToken);

            // 3) 📤 Transfer idisə — kredit «Aktiv», maşın «Kreditdə» olur ✓
            if (silinen is not null
                && (silinen.Nov == "Transfer" || silinen.Nov == "Transfer olunmaq")
                && silinen.CreditId is int cid)
            {
                await TransferGeriAlAsync(cid, cancellationToken);
            }

            // 4) ⛔ v6.2.34 — avtomatik bağlanma LƏĞV EDİLDİ ✗✓✓
            //    Ödəniş silinsə də kredit statusu AVTOMATİK dəyişmir ✗ —
            //    yalnız «⏹ KREDİTİ BİTDİ» düyməsi ilə bağlanır ✓
        }

        /// <summary>
        /// 📤 <b>TRANSFER SİLİNDİKDƏ — TAM GERİ ALINMA</b> ✓✓✓
        /// <list type="bullet">
        ///   <item>Kredit yenidən <b>«Aktiv»</b> olur ✓ (qeyd təmizlənir ✓)</item>
        ///   <item>Avtomobil <b>«Kreditdə»</b> statusuna qayıdır ✓ (parka dönür ✓)</item>
        ///   <item>Transfer SATIŞI silinir ✓ (xeyir hesabdan çıxır ✓)</item>
        /// </list>
        /// </summary>
        private async Task TransferGeriAlAsync(int creditId, CancellationToken cancellationToken)
        {
            _credits.ClearTracker();
            var credit = await _credits.GetByIdAsync(creditId, cancellationToken);

            if (credit is not null)
            {
                credit.Status = "Aktiv";
                credit.Qeyd = string.Empty;
                await _credits.SaveChangesAsync(cancellationToken);
            }

            var carId = credit?.CarId;

            if (carId is int kId)
            {
                _cars.ClearTracker();
                var car = await _cars.GetByIdAsync(kId, cancellationToken);

                if (car is not null
                    && !string.Equals(car.Status, Catalog.CreditStatus, StringComparison.Ordinal))
                {
                    car.Status = Catalog.CreditStatus;
                    await _cars.SaveChangesAsync(cancellationToken);
                }

                // 📤 Transfer SATIŞI silinir ✓
                _sales.ClearTracker();

                // ⏳ Transfer satışının MÖHLƏTLƏRİ DƏ silinir ✓✓✓ (v6.2.11)
                //  (əvvəl yalnız bazadakı CASCADE-dən asılı idi ✗ → sahibsiz
                //   möhlətlər 📅 Təqvim / 🔔 Bildirişlər tablarında qalırdı ✗)
                foreach (var transferSatisi in await _sales.FindAsync(
                             s => s.CarId == kId && s.OdenisUsulu == "Transfer", cancellationToken))
                {
                    _mohletler.ClearTracker();
                    await _mohletler.DeleteWhereAsync(
                        m => m.SaleId == transferSatisi.Id, cancellationToken);
                }

                await _sales.DeleteWhereAsync(
                    s => s.CarId == kId && s.OdenisUsulu == "Transfer",
                    cancellationToken);
            }
        }

        // ====================================================================
        //  🎯 KREDİT BİTDİKDƏ AVTOMATİK BAĞLANMA
        // --------------------------------------------------------------------
        //  Kredit üzrə toplanmış ödənişlər KREDİTİN QİYMƏTİNƏ çatdıqda:
        //     1) kreditin statusu  → «Bağlı»
        //     2) avtomobilin statusu → «Satıldı»
        //  Beləliklə maşın avtomatik olaraq
        //  «🗄️ Satılan & Krediti Bitmiş» bölməsinə keçir.
        //
        //  ⚠ Yalnız BİR TƏRƏFLİ işləyir: ödəniş silinsə kredit/car geri
        //    AÇILMIR — belə ki təsadüfən düzəliş edilməsi satılmış maşını
        //    parka qaytarmasın.
        // ====================================================================

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<int, IReadOnlyList<PartnerShare>>> GetCreditSharesMapAsync(
            CancellationToken cancellationToken = default)
        {
            // Bir dəfə bütün paylar oxunur, sonra CreditId-yə görə qruplaşdırılır.
            var shares = await _shares.GetAllOrderedAsync(cancellationToken);

            return shares
                .Where(s => s.CreditId.HasValue)
                .GroupBy(s => s.CreditId!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<PartnerShare>)g
                        .OrderBy(s => s.Sira)
                        .ThenBy(s => s.Id)
                        .ToList());
        }

        /// <inheritdoc />
        public async Task<bool> ReopenCreditByCarAsync(int carId, CancellationToken cancellationToken = default)
        {
            // ⚠ Stale izlər buraxılır — dəyişiklik dəqiq tətbiq olunsun ✓
            _credits.ClearTracker();

            var kreditler = await _credits.FindAsync(c => c.CarId == carId, cancellationToken);

            if (kreditler.Count == 0)
            {
                return false;
            }

            var deyisdi = false;

            foreach (var kohne in kreditler)
            {
                if (string.Equals(kohne.Status, "Aktiv", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var kredit = await _credits.GetByIdAsync(kohne.Id, cancellationToken);

                if (kredit is null)
                {
                    continue;
                }

                kredit.Status = "Aktiv";
                _credits.Update(kredit);
                deyisdi = true;

                _logger.LogInformation(
                    "🔓 Kredit yenidən AÇILDI (geri qaytarma): {Muqavile} — avtomobil parka qaytarıldı",
                    kredit.MuqavileNomresi);
            }

            if (deyisdi)
            {
                await _credits.SaveChangesAsync(cancellationToken);
            }

            // ================================================================
            //  🗑️ SATIŞ QEYDLƏRİ DƏ SİLİNİR — MALİYYƏ PANELİ DÜZƏLİR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL yalnız KREDİT yenidən açılırdı ✗ → satış qeydi BAZADA
            //    QALIRDI ✗ → «📊 Maliyyə Paneli» həmin pulu GƏLİR saymağa
            //    davam edirdi ✗ → maşın YENİDƏN SATILANDA ikinci gəlir də
            //    əlavə olunurdu ✗ → PUL ÜSTÜNƏ GƏLİRDİ ✗✓✓
            //    (nə qaytaranda azalır ✗, nə də satışda düzgün sayılır ✗)
            //  ✅ İNDİ: geri qaytarmada satış qeydi DƏ silinir ✓ →
            //     Maliyyə Panelindən pul DƏRHAL AZALIR ✓✓✓
            //     Yenidən satıldıqda isə TƏK gəlir yazılır ✓ (ikiqat YOX ✗)
            // ================================================================
            _sales.ClearTracker();

            var silinenSatis = await _sales.DeleteWhereAsync(
                s => s.CarId == carId, cancellationToken);

            if (silinenSatis > 0)
            {
                _logger.LogInformation(
                    "🗑️ Geri qaytarma ✓: avtomobilin {Count} satış qeydi silindi (CarId={CarId}) " +
                    "→ Maliyyə Paneli yeniləndi ✓",
                    silinenSatis, carId);
            }

            return deyisdi;
        }

        /// <inheritdoc />
        /// <summary>
        /// 📤 <b>TRANSFER EDİLDİKDƏ</b> ✓✓✓
        /// <list type="number">
        ///   <item>Kredit <b>«Bağlı»</b> olur → «💳 Kreditlər» bölməsindən çıxır ✓,
        ///         qeydə «📤 Transfer olunmuş → Tural» yazılır ✓</item>
        ///   <item>Avtomobil <b>«Satıldı»</b> statusuna keçir → parkdan ÇIXIR ✓ və
        ///         «🗄️ Satılan &amp; Krediti Bitmiş» bölməsində görünür ✓✓✓</item>
        /// </list>
        /// </summary>
        private async Task RefreshTransferAsync(
            CreditTransaction transaction,
            CancellationToken cancellationToken)
        {
            // Yalnız TRANSFER növləri üçün ✓
            if (transaction.Nov != "Transfer" && transaction.Nov != "Transfer olunmaq")
            {
                return;
            }

            if (transaction.CreditId is not int creditId)
            {
                return;
            }

            // Transfer edilən şəxsin adı («Tural · Toyota Camry» → «Tural») ✓
            var sahib = (transaction.Tesvir ?? string.Empty).Split('·')[0].Trim();
            if (string.IsNullOrWhiteSpace(sahib))
            {
                sahib = "—";
            }

            var credits = await _credits.GetWithCarAsync(cancellationToken);
            var credit = credits.FirstOrDefault(c => c.Id == creditId);

            if (credit is null)
            {
                return;
            }

            // ================================================================
            //  1) KREDİT BAĞLANIR ✓ + qeyd yazılır ✓
            // ----------------------------------------------------------------
            //  ⚠ ClearTracker: köhnə izlənən nüsxə UPDATE-in «0 sətir»
            //    xətası verməsinin qarşısını alır ✓ (mövcud kod üslubu ✓).
            // ================================================================
            _credits.ClearTracker();

            var hedef = await _credits.GetByIdAsync(creditId, cancellationToken) ?? credit;

            hedef.Status = "Bağlı";
            hedef.Qeyd = $"📤 Transfer olunmuş → {sahib} · {transaction.Tarix:dd.MM.yyyy}";

            await _credits.SaveChangesAsync(cancellationToken);

            // ================================================================
            //  2) AVTOMOBİL PARKDAN ÇIXIR ✓ — «Transfer edildi» ✓✓✓
            // ================================================================
            var carId = hedef.CarId ?? credit.CarId ?? credit.Car?.Id;
            CarItem? avtomobil = null;

            if (carId is int kId)
            {
                _cars.ClearTracker();

                avtomobil = await _cars.GetByIdAsync(kId, cancellationToken);

                if (avtomobil is not null
                    && !string.Equals(avtomobil.Status, Catalog.TransferStatus, StringComparison.Ordinal))
                {
                    avtomobil.Status = Catalog.TransferStatus;
                    await _cars.SaveChangesAsync(cancellationToken);
                }
            }

            // ================================================================
            //  3) 📤 TRANSFER = NAĞD SATIŞ ✓ — XEYİR BİZDƏ QALIR ✓✓✓
            // ================================================================
            await TransferSatisiYazAsync(hedef, avtomobil, sahib, transaction, cancellationToken);

            _logger.LogInformation(
                "📤 Transfer tamamlandı: kredit {Id} · {Masin} → {Sahib} · parkdan çıxarıldı · satış qeydə alındı ✓",
                creditId, avtomobil?.DisplayName ?? credit.Car?.DisplayName ?? "—", sahib);
        }

        /// <summary>
        /// 🏭 <b>TAM MAYA DƏYƏRİ</b> ✓✓✓ — avtomobil <b>XƏRCLƏRİ YÜKLƏNMİŞ</b>
        /// sorğu ilə oxunur ✓ (maya = Alış qiyməti + BÜTÜN avtomobil xərcləri ✓).
        /// <para>
        /// ⚠ ƏVVƏLKİ XƏTA: sadə `GetByIdAsync` ilə oxunurdu ✗ → avtomobil
        /// xərcləri yüklənmirdi ✗ → maya AZ çıxırdı ✗ →
        /// <b>XEYİR şişirdilmiş</b> görünürdü ✗✓✓ (məs. 31 377 ₼ yerinə 20 000 ₼ ✗).
        /// </para>
        /// </summary>
        private async Task<decimal> TamMayaAsync(CarItem? car, CancellationToken cancellationToken)
        {
            if (car is null)
            {
                return 0m;
            }

            try
            {
                // ✓ Xərclərlə birlikdə TAM məlumat ✓
                var tam = (await _cars.GetWithExpensesAsync(cancellationToken))
                    .FirstOrDefault(c => c.Id == car.Id);

                if (tam is not null)
                {
                    return tam.MayaDeyeri;      // ✓ Alış qiyməti + BÜTÜN xərclər ✓
                }
            }
            catch
            {
                // Xəta olsa mövcud nüsxə ilə davam edilir ✓
            }

            return car.MayaDeyeri;
        }

        /// <summary>
        /// 📤 <b>TRANSFER SATIŞI</b> ✓✓✓
        /// <para>
        /// Transfer edilən maşın <b>nağd satılmış</b> kimi qeydə alınır ✓:
        /// avtomobil bizdən çıxır ✓, alacağımız pul bizə gəlir ✓ və
        /// <b>XEYİR (satış qiyməti − maya dəyəri) BİZDƏ QALIR</b> ✓✓✓
        /// </para>
        /// <para>
        /// Qeyd «💰 Satış», «🗄️ Satılan &amp; Krediti Bitmiş» və
        /// «📊 Maliyyə Paneli» bölmələrində dərhal görünür ✓
        /// (ödəniş üsulu: <c>Transfer</c>).
        /// </para>
        /// </summary>
        private async Task TransferSatisiYazAsync(
            Credit credit,
            CarItem? car,
            string sahib,
            CreditTransaction transaction,
            CancellationToken cancellationToken)
        {
            //  ⚠ TƏKRAR yazının qarşısı: bu maşın/müqavilə üçün transfer
            //    satışı artıq varsa → yenidən yazılmır ✓
            var movcud = await _sales.FindAsync(
                s => s.CarId == credit.CarId && s.MuqavileNomresi == credit.MuqavileNomresi,
                cancellationToken);

            if (movcud.Count > 0)
            {
                return;
            }

            // ================================================================
            //  ✅ DÜZGÜN XEYİR FORMULASI (istifadəçi təsdiqi ✓✓✓)
            // ----------------------------------------------------------------
            //      XEYİR = SATIŞ QİYMƏTİ (Məbləğ) + PUL − MAYA DƏYƏRİ ✓✓✓
            //
            //  SİZİN NÜMUNƏ (dəqiq ✓):
            //      Satış qiyməti = 33 012,50 ₼ ✓
            //      Maya dəyəri   = 31 377,00 ₼ ✓
            //      → XEYİR = 33 012,50 + 0 − 31 377,00 = 1 635,50 ₼ ✓✓✓
            //
            //  NİYƏ TAM SATIŞ QİYMƏTİ?
            //      Satış qiyməti = İLKİN ÖDƏNİŞ (10 000 ₼)
            //                    + NİSYƏ (23 012,50 ₼) ✓
            //      HƏR İKİSİ BİZƏ GƏLİR ✓:
            //        • ilkin ödəniş — kredit açılarkən alınır ✓
            //        • nisyə — transfer edən şəxs TƏRƏFİNDƏN ödənilir ✓
            //      → buna görə əsas = TAM SATIŞ QİYMƏTİ ✓✓✓
            //
            //  ⚠ YALNIZ nisyə götürülürdü ✗ → xeyir olduğundan AZ (mənfi)
            //    çıxırdı ✗ (ilkin ödəniş nəzərə alınmırdı ✗✓✓).
            // ================================================================
            var maya = await TamMayaAsync(car, cancellationToken);   // 🏭 TAM maya (xərclərlə ✓)
            var satisQiymeti = credit.Mebleg;                        // ✓ TAM satış qiyməti (33 012,50)
            var pul = transaction.Mebleg;                            // ℹ️ məlumat üçün (qeyd / siyahı ✓)

            // ================================================================
            //  ⚠⚠ ÇOX VACİB: PUL SATIŞ QİYMƏTİNƏ ƏLAVƏ EDİLMİR ✗✓✓
            // ----------------------------------------------------------------
            //  «PUL» sahəsi AVTOMATİK olaraq NİSYƏ (kreditləşdirilən) ilə dolar
            //  (23 012,50 ₼) ✓ — bu, satış qiymətinin İÇİNDƏ OLAN məbləğdir ✓
            //  (satış 33 012,50 = ilkin 10 000 + nisyə 23 012,50 ✓).
            //
            //  Əlavə etsək:  33 012,50 + 23 012,50 = 56 025,00 ₼ ✗ İKİQAT ✗✓✓
            //  (istifadəçi bunu «Dövr Gəliri»-ndə gördü ✗)
            //
            //  ✅ DÜZGÜN:  XEYİR = SATIŞ QİYMƏTİ − MAYA = 33 012,50 − 31 377,00
            //                                    = 1 635,50 ₼ ✓✓✓
            // ================================================================
            var xeyir = satisQiymeti - maya;                         // ✅ 1 635,50 ₼ ✓✓✓

            var nisye = credit.Kreditlesdirilen;                     // (qeyd üçün ✓)

            var sale = new Sale
            {
                MuqavileNomresi = credit.MuqavileNomresi,
                Mustəri = $"{credit.Mustəri} → {sahib}",
                CarId = credit.CarId,
                SatisQiymeti = satisQiymeti,                         // ✓ 33 012,50 (PUL əlavə edilmir ✗)
                MayaDeyeri = maya,
                SatisTarixi = transaction.Tarix,
                OdenisUsulu = "Transfer",
                Qeyd = ($"📤 Transfer — {credit.Mustəri} → {sahib} · " +
                        $"SATIŞ {satisQiymeti:N2} − MAYA {maya:N2} = " +
                        $"XEYİR {xeyir:N2} ₼  ·  şəxsin verdiyi PUL {pul:N2} ₼ " +
                        $"(nisyə {nisye:N2} ₼ — satış qiymətinin içindədir ✓)").Replace(",", " ")
            };

            await _sales.AddAsync(sale, cancellationToken);
            await _sales.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "📤 Transfer SATIŞI ✓: {Masin} → {Sahib} · SATIŞ {Satis:N2} ₼ − MAYA {Maya:N2} ₼ = XEYİR {Xeyir:N2} ₼ · PUL {Pul:N2} ₼ (nisyə — ikiqat sayılmır ✓)",
                car?.DisplayName ?? credit.Car?.DisplayName ?? $"(avtomobil #{credit.CarId})",
                sahib, satisQiymeti, maya, xeyir, pul);
        }

        public async Task<int> RefreshCreditCompletionAsync(CancellationToken cancellationToken = default)
        {
            var credits = await _credits.GetWithCarAsync(cancellationToken);

            if (credits.Count == 0)
            {
                return 0;
            }

            var transactions = await _transactions.FindAsync(_ => true, cancellationToken);

            // Kredit üzrə toplanmış ödənişlər («Gəlir» növlü qeydlər).
            var odenisler = transactions
                .Where(t => t.CreditId.HasValue && t.Nov == "Gəlir")
                .GroupBy(t => t.CreditId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Mebleg));

            var baglanan = 0;

            foreach (var credit in credits)
            {
                // ================================================================
                //  ✅ YALNIZ HƏQİQƏTƏN «BAĞLI» OLANLAR KEÇİLİR ✓✓✓  (v6.2.26)
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ SƏHV: burada `credit.BitmisKredit` yoxlanılırdı ✗ —
                //  o xassə TARİXƏ görə də true olur:
                //      «BaşlamaTarixi + MüddətAy <= bu gün»  ✗✓✓
                //  Nəticədə müddəti bitmiş AMMA TAM ÖDƏNİLMƏMİŞ (status hələ
                //  «Aktiv») kreditlər BURADA `continue` edilirdi ✗ →
                //  • kredit heç vaxt «Bağlı» edilmirdi ✗
                //  • avtomobil heç vaxt «Satıldı» olub arxivə keçmirdi ✗
                //  → «Kreditlər» tabında ƏBƏDİ qalırdı ✗, eyni zamanda tarixə
                //    görə «Krediti Bitmiş» tabında da görünürdü ✗ (İKİ YERDƏ ✗)
                //
                //  ✅ İNDİ: yalnız STATUSU «Bağlı» olan kredit keçilir ✓ —
                //  qalanları ödənişə görə DÜZGÜN bağlanır ✓✓✓
                //  (tətbiq hər açılışda da işlədir → köhnə qeydlər ÖZÜ DÜZƏLİR ✓)
                // ================================================================
                if (string.Equals(credit.Status?.Trim(), "Bağlı", StringComparison.OrdinalIgnoreCase))
                {
                    continue;   // artıq HƏQİQƏTƏN bağlıdır ✓
                }

                // ⚠ AVTOMOBİL ARTIQ PARKA QAYTARILIB?
                //   «Geri qaytar» edilibsə maşının statusu «Satıldı» DEYİL ✗ —
                //   belə krediti YENİDƏN bağlamırıq (istifadəçi qərarı hörmətlidir ✓)
                if (credit.Car is not null
                    && !string.Equals(credit.Car.Status, Catalog.SoldStatus, StringComparison.Ordinal)
                    && !string.Equals(credit.Car.Status, Catalog.CreditStatus, StringComparison.Ordinal))
                {
                    continue;
                }

                odenisler.TryGetValue(credit.Id, out var odenilmis);

                if (!credit.Bitibmi(odenilmis))
                {
                    continue;
                }

                // ---- 1) Kredit «Bağlı» olur ----------------------------------
                //  ⚠ ClearTracker: uzunömürlü kontekstdəki köhnə nüsxə
                //    UPDATE-in «0 sətir» xətası verməsinin qarşısını alır.
                _credits.ClearTracker();

                var izlenen = await _credits.GetByIdAsync(credit.Id, cancellationToken);
                if (izlenen is null)
                {
                    continue;
                }

                izlenen.Status = "Bağlı";
                await _credits.SaveChangesAsync(cancellationToken);

                // ---- 2) Avtomobil «Satıldı» olur (arxivə keçir) ---------------
                if (credit.CarId is int carId)
                {
                    _cars.ClearTracker();

                    var car = await _cars.GetByIdAsync(carId, cancellationToken);

                    if (car is not null && !string.Equals(car.Status, Catalog.SoldStatus, StringComparison.Ordinal))
                    {
                        car.Status = Catalog.SoldStatus;
                        await _cars.SaveChangesAsync(cancellationToken);
                    }
                }

                baglanan++;

                _logger.LogInformation(
                    "🎯 Kredit tam ödənildi → «Bağlı»: {Muqavile} · ödənilmiş {Odenilmis:N2} ₼ / qiymət {Qiymet:N2} ₼",
                    credit.MuqavileNomresi, odenilmis, credit.KreditQiymeti);
            }

            return baglanan;
        }
    }
}
