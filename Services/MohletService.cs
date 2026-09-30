using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class MohletService : IMohletService
    {
        private readonly IOdenisMohletRepository _mohletler;
        private readonly ILogger<MohletService> _logger;

        public MohletService(IOdenisMohletRepository mohletler, ILogger<MohletService> logger)
        {
            _mohletler = mohletler;
            _logger = logger;
        }

        public Task<IReadOnlyList<OdenisMohlet>> GetAllAsync(CancellationToken cancellationToken = default)
            => _mohletler.GetAllDetailedAsync(cancellationToken);

        public Task<IReadOnlyList<OdenisMohlet>> GetForCreditAsync(
            int creditId,
            CancellationToken cancellationToken = default)
            => _mohletler.GetByCreditAsync(creditId, cancellationToken);

        public Task<IReadOnlyList<OdenisMohlet>> GetForSaleAsync(
            int saleId,
            CancellationToken cancellationToken = default)
            => _mohletler.GetBySaleAsync(saleId, cancellationToken);

        public async Task<int> SaveForCreditAsync(
            int creditId,
            IReadOnlyList<OdenisMohlet> rows,
            CancellationToken cancellationToken = default)
        {
            var say = await YazAsync(
                rows.Where(r => r.Mebleg > 0m).ToList(),
                m => m.CreditId = creditId,
                p => p.CreditId == creditId,
                OdenisMohlet.MenbeIlkinOdenis,
                cancellationToken);

            _logger.LogInformation("⏳ Kredit #{Id} üçün {Say} ilkin ödəniş möhləti saxlanıldı.", creditId, say);
            return say;
        }

        public async Task<int> SaveForSaleAsync(
            int saleId,
            IReadOnlyList<OdenisMohlet> rows,
            CancellationToken cancellationToken = default)
        {
            var say = await YazAsync(
                rows.Where(r => r.Mebleg > 0m).ToList(),
                m => m.SaleId = saleId,
                p => p.SaleId == saleId,
                OdenisMohlet.MenbeSatis,
                cancellationToken);

            _logger.LogInformation("⏳ Satış #{Id} üçün {Say} möhlət ödənişi saxlanıldı.", saleId, say);
            return say;
        }

        /// <summary>
        /// «Bütöv siyahı» yazma məntiqi ✓✓✓ — əvvəlkilər silinir ✓ sonra yeniləri
        /// yazılır ✓ (köhnə izlər konflikti yaratmasın deyə BAZADAN silinir ✓)
        /// </summary>
        private async Task<int> YazAsync(
            IReadOnlyList<OdenisMohlet> rows,
            Action<OdenisMohlet> menbeyiYaz,
            System.Linq.Expressions.Expression<Func<OdenisMohlet, bool>> silmeSherti,
            string menbe,
            CancellationToken cancellationToken)
        {
            _mohletler.ClearTracker();
            await _mohletler.DeleteWhereAsync(silmeSherti, cancellationToken);

            var sira = 0;

            foreach (var row in rows.OrderBy(r => r.Tarix).ThenBy(r => r.Id))
            {
                var yeni = new OdenisMohlet
                {
                    Menbe = menbe,
                    Tarix = row.Tarix.Date,
                    Mebleg = row.Mebleg,
                    OdenisUsulu = string.IsNullOrWhiteSpace(row.OdenisUsulu)
                        ? Catalog.PaymentMethods[0]
                        : row.OdenisUsulu,
                    Odenilib = row.Odenilib,
                    OdenilmeTarixi = row.Odenilib ? row.OdenilmeTarixi ?? row.Tarix.Date : null,
                    Qeyd = row.Qeyd?.Trim() ?? string.Empty,
                    Sira = sira++
                };

                menbeyiYaz(yeni);
                await _mohletler.AddAsync(yeni, cancellationToken);
            }

            if (rows.Count > 0)
            {
                await _mohletler.SaveChangesAsync(cancellationToken);
            }

            return rows.Count;
        }

        public async Task<bool> SetOdenildiAsync(
            int id,
            bool odenilib,
            DateTime? odenilmeTarixi = null,
            CancellationToken cancellationToken = default)
        {
            var mohlet = await _mohletler.GetByIdAsync(id, cancellationToken);

            if (mohlet is null)
            {
                return false;
            }

            mohlet.Odenilib = odenilib;
            mohlet.OdenilmeTarixi = odenilib ? (odenilmeTarixi ?? DateTime.Today).Date : null;

            _mohletler.Update(mohlet);
            await _mohletler.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "⏳ Möhlət #{Id} → {Veziyyet} ({Mebleg:N2} ₼)",
                id,
                odenilib ? "ödənildi ✓ (kassaya daxil oldu)" : "ödənilmədi",
                mohlet.Mebleg);

            return true;
        }

        public async Task<OdenisMohlet> AddAsync(
            OdenisMohlet mohlet,
            CancellationToken cancellationToken = default)
        {
            mohlet.Tarix = mohlet.Tarix.Date;
            mohlet.Qeyd = mohlet.Qeyd?.Trim() ?? string.Empty;

            await _mohletler.AddAsync(mohlet, cancellationToken);
            await _mohletler.SaveChangesAsync(cancellationToken);

            return mohlet;
        }

        /// <summary>
        /// 💾 Möhlətin TARİX / MƏBLƏĞ / ÜSUL düzəlişini bazaya yazır ✓✓✓
        /// <para>
        /// ⚠⚠ <b>NƏ ÜÇÜN <c>GetByIdAsync</c> İLƏ? (DÜZƏLİŞ ✓✓✓)</b>
        /// Siyahılar <c>AsNoTracking()</c> ilə oxunur ✗ → UI-dən gələn sətir
        /// <b>izlənməyən (untracked)</b> nüsxədir ✗. Həmin nüsxəni birbaşa
        /// <c>Update()</c> ilə yazmaq istəsək, kontekst eyni <c>Id</c>-li BAŞQA
        /// nüsxəni artıq izləyirsə ✗ (məs. <see cref="SetOdenildiAsync"/>-dən sonra ✓)
        /// EF <c>«another instance with the same key value is already being tracked»</c>
        /// xətası verirdi ✗✓✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: əvvəlcə <b>İZLƏNƏN</b> sətir tapılır ✓ sahələr kopyalanır ✓
        /// sonra yazılır ✓ — xəta YOXDUR ✗✓✓
        /// </para>
        /// </summary>
        public async Task UpdateAsync(OdenisMohlet mohlet, CancellationToken cancellationToken = default)
        {
            var movcud = await _mohletler.GetByIdAsync(mohlet.Id, cancellationToken)
                ?? throw new InvalidOperationException($"Möhlət #{mohlet.Id} tapılmadı ✗");

            movcud.Menbe = mohlet.Menbe;
            movcud.CreditId = mohlet.CreditId;
            movcud.SaleId = mohlet.SaleId;
            movcud.Sira = mohlet.Sira;
            movcud.Tarix = mohlet.Tarix.Date;
            movcud.Mebleg = mohlet.Mebleg;
            movcud.OdenisUsulu = mohlet.OdenisUsulu;
            movcud.Odenilib = mohlet.Odenilib;
            movcud.OdenilmeTarixi = mohlet.OdenilmeTarixi?.Date;
            movcud.Qeyd = mohlet.Qeyd?.Trim() ?? string.Empty;

            _mohletler.Update(movcud);
            await _mohletler.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "💾 Möhlət #{Id} yeniləndi: {Tarix:dd.MM.yyyy} · {Mebleg:N2} ₼ · {Veziyyet}",
                movcud.Id, movcud.Tarix, movcud.Mebleg, movcud.Odenilib ? "ödənilib ✓" : "gözlənilir");
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            _mohletler.ClearTracker();
            var say = await _mohletler.DeleteWhereAsync(m => m.Id == id, cancellationToken);

            _logger.LogInformation("🗑️ Möhlət #{Id} silindi ({Say} sətir).", id, say);
        }

        public async Task<int> DeleteForCreditAsync(int creditId, CancellationToken cancellationToken = default)
        {
            _mohletler.ClearTracker();
            return await _mohletler.DeleteWhereAsync(m => m.CreditId == creditId, cancellationToken);
        }

        public async Task<int> DeleteForSaleAsync(int saleId, CancellationToken cancellationToken = default)
        {
            _mohletler.ClearTracker();
            return await _mohletler.DeleteWhereAsync(m => m.SaleId == saleId, cancellationToken);
        }
    }
}
