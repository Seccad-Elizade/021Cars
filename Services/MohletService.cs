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

        // ====================================================================
        //  ♻️ v6.2.83 — QİSMƏN ÖDƏNİŞİN GERİ ALINMASI ✓✓✓
        // ====================================================================

        /// <inheritdoc />
        public async Task<decimal?> QismenOdenisiGeriQaytarAsync(
            int qismenMohletId,
            CancellationToken cancellationToken = default)
        {
            var hamısı = await _mohletler.GetAllAsync(cancellationToken);

            var qismen = hamısı.FirstOrDefault(m => m.Id == qismenMohletId);

            if (qismen is null)
            {
                _logger.LogWarning("♻️ Qismən ödəniş tapılmadı: #{Id}", qismenMohletId);
                return null;
            }

            var əsas = ƏsasMohletiTap(hamısı, qismen);

            if (əsas is null)
            {
                // 🛡️ ƏSAS TAPILMADI — pul İTMƏSİN ✗ → sətir «gözlənilən möhlət» olur ✓
                qismen.Odenilib = false;
                qismen.OdenilmeTarixi = null;

                if (QismenIlkinTarix(qismen) is DateTime ilkin)
                {
                    qismen.Tarix = ilkin;
                }

                await UpdateAsync(qismen, cancellationToken);

                _logger.LogWarning(
                    "↩ Qismən ödəniş geri alındı — ƏSAS möhlət tapılmadı ✗ → sətir möhlət kimi saxlanıldı: #{Id} · {Mebleg:N2} ₼",
                    qismen.Id, qismen.Mebleg);

                return null;
            }

            // ① məbləğ TAM bərpa ✓ (4 000 + 1 000 = 5 000 ✓)
            əsas.Mebleg = Math.Round(əsas.Mebleg + qismen.Mebleg, 2);

            // ② İLKİN tarix bərpa ✓ (marker varsa — köhnə qeydlərdə tarix TOXUNULMUR ✓)
            if (QismenIlkinTarix(qismen) is DateTime ilkinTarix)
            {
                əsas.Tarix = ilkinTarix;
            }

            əsas.Odenilib = false;
            əsas.OdenilmeTarixi = null;

            await UpdateAsync(əsas, cancellationToken);

            // ③ qismən ödəniş sətri SİLİNİR ✓ (əlavə möhlət yaranmasın ✗)
            await DeleteAsync(qismen.Id, cancellationToken);

            _logger.LogInformation(
                "♻️ Qismən ödəniş geri qaytarıldı: əsas möhlət #{Id} → {Mebleg:N2} ₼ ✓ · ödəniş sətri #{Odenis} silindi ✓",
                əsas.Id, əsas.Mebleg, qismen.Id);

            return əsas.Mebleg;
        }

        /// <inheritdoc />
        public async Task<int> KohnəQismenQaliqlariDuzeltAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var hamısı = await _mohletler.GetAllAsync(cancellationToken);

                // 🔍 «ödənilməmiş» + təsvirdə «Qismən ödəniş» → köhnə xətanın qalığı ✗
                var qalıqlar = hamısı
                    .Where(m => !m.Odenilib
                                && (m.Qeyd ?? string.Empty)
                                    .Contains("Qismən ödəniş", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (qalıqlar.Count == 0)
                {
                    return 0;
                }

                var düzəldilən = 0;

                foreach (var qalıq in qalıqlar)
                {
                    var əsas = ƏsasMohletiTap(hamısı, qalıq);

                    if (əsas is null)
                    {
                        continue;      // ⚠ əsas yoxdur → toxunmuruq ✓ (pul itməsin ✗)
                    }

                    əsas.Mebleg = Math.Round(əsas.Mebleg + qalıq.Mebleg, 2);

                    if (QismenIlkinTarix(qalıq) is DateTime ilkinTarix)
                    {
                        əsas.Tarix = ilkinTarix;
                    }

                    await UpdateAsync(əsas, cancellationToken);
                    await DeleteAsync(qalıq.Id, cancellationToken);

                    düzəldilən++;

                    _logger.LogInformation(
                        "🩹 Köhnə «qismən ödəniş qalığı» düzəldildi: sətir #{Qaliq} ({QMebleg:N2} ₼) → əsas möhlət #{Esas} = {EMebleg:N2} ₼ ✓",
                        qalıq.Id, qalıq.Mebleg, əsas.Id, əsas.Mebleg);
                }

                if (düzəldilən > 0)
                {
                    _logger.LogInformation("🩹 Cəmi {Say} köhnə möhlət qalığı təmizləndi ✓", düzəldilən);
                }

                return düzəldilən;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Köhnə möhlət qalıqları düzəldilmədi ✗ (proqram işləyir ✓)");
                return 0;
            }
        }

        /// <summary>🔍 Qismən ödənişin ƏSAS (qalıq) möhlət sətrini tapır ✓</summary>
        private static OdenisMohlet? ƏsasMohletiTap(
            IReadOnlyList<OdenisMohlet> hamısı,
            OdenisMohlet qismen)
        {
            // ① MARKER ilə — ən dəqiq yol ✓
            if (QismenEsasId(qismen) is int esasId)
            {
                var markerlə = hamısı.FirstOrDefault(m => m.Id == esasId && !m.Odenilib);

                if (markerlə is not null)
                {
                    return markerlə;
                }
            }

            // ② Eyni mənbə + sıra üzrə ✓ (köhnə qeydlər ✓)
            bool Uyğundur(OdenisMohlet m) =>
                m.Id != qismen.Id
                && !m.Odenilib
                && m.CreditId == qismen.CreditId
                && m.SaleId == qismen.SaleId
                && m.Sira == qismen.Sira
                && string.Equals(m.Menbe, qismen.Menbe, StringComparison.Ordinal);

            var namizədlər = hamısı.Where(Uyğundur).ToList();

            if (namizədlər.Count == 0)
            {
                return null;
            }

            // ③ «ilkin məbləğ» məlumdursa — cəmi uyğun gələn seçilir ✓
            if (QismenIlkinMebleg(qismen) is decimal cəm)
            {
                var uyğun = namizədlər.FirstOrDefault(
                    m => Math.Abs(m.Mebleg + qismen.Mebleg - cəm) < 0.01m);

                if (uyğun is not null)
                {
                    return uyğun;
                }
            }

            return namizədlər[0];
        }

        /// <summary>🔗 «əsas #123» markerindən ƏSAS möhlətin Id-sini oxuyur ✓</summary>
        private static int? QismenEsasId(OdenisMohlet qismen)
        {
            try
            {
                var qeyd = qismen.Qeyd ?? string.Empty;
                var baş = qeyd.IndexOf("əsas #", StringComparison.OrdinalIgnoreCase);

                if (baş < 0)
                {
                    return null;
                }

                baş += "əsas #".Length;

                var rəqəm = new System.Text.StringBuilder();

                while (baş < qeyd.Length && char.IsDigit(qeyd[baş]))
                {
                    rəqəm.Append(qeyd[baş]);
                    baş++;
                }

                return rəqəm.Length > 0 && int.TryParse(rəqəm.ToString(), out var id) ? id : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>📅 «ilkin tarix: 07.09.2026» markerindən İLKİN tarixi oxuyur ✓</summary>
        private static DateTime? QismenIlkinTarix(OdenisMohlet qismen)
        {
            try
            {
                var qeyd = qismen.Qeyd ?? string.Empty;
                var baş = qeyd.IndexOf("ilkin tarix:", StringComparison.OrdinalIgnoreCase);

                if (baş < 0)
                {
                    return null;
                }

                baş += "ilkin tarix:".Length;

                // ⚠ v6.2.83 — « 07.09.2026» → yalnız RƏQƏM və NÖQTƏLƏR götürülür ✓
                //   (sabit 10 simvol götürsək boşluq səbəbindən tarix kəsilirdi ✗)
                var qalan = qeyd.Substring(baş).TrimStart();
                var mətn = new string(qalan
                    .TakeWhile(c => char.IsDigit(c) || c == '.')
                    .ToArray());

                return DateTime.TryParseExact(
                    mətn, "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var tarix)
                    ? tarix
                    : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>💰 «möhlət 5 000,00 ₼ idi» mətnindən İLKİN məbləği oxuyur ✓</summary>
        private static decimal? QismenIlkinMebleg(OdenisMohlet qismen)
        {
            try
            {
                var qeyd = qismen.Qeyd ?? string.Empty;
                var baş = qeyd.IndexOf("möhlət ", StringComparison.OrdinalIgnoreCase);

                if (baş < 0)
                {
                    return null;
                }

                baş += "möhlət ".Length;
                var son = qeyd.IndexOf('₼', baş);

                if (son <= baş)
                {
                    return null;
                }

                var mətn = qeyd.Substring(baş, son - baş)
                    .Replace(" ", string.Empty)
                    .Replace("\u00A0", string.Empty)
                    .Replace(',', '.')
                    .Trim();

                return decimal.TryParse(
                    mətn, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var mebleg)
                    ? mebleg
                    : null;
            }
            catch
            {
                return null;
            }
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
