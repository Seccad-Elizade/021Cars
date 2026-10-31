using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// ⏳ <b>MÖHLƏT XİDMƏTİ</b> — «nə vaxt, nə qədər pul gələcək» qeydlərinin
    /// saxlanması ✓✓✓
    /// <para>
    /// İki mənbə dəstəklənir:
    /// </para>
    /// <list type="number">
    ///   <item><b>🏦 Kreditin İLKİN ÖDƏNİŞİ</b> (Kreditlər tabı) ✓</item>
    ///   <item><b>💰 NİSYƏ SATIŞ</b> (Satış tabı) ✓</item>
    /// </list>
    /// </summary>
    public interface IMohletService
    {
        /// <summary>Bütün möhlətlər (kredit · satış məlumatı ilə birlikdə ✓).</summary>
        Task<IReadOnlyList<OdenisMohlet>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Bir kreditin ilkin ödəniş möhlətləri.</summary>
        Task<IReadOnlyList<OdenisMohlet>> GetForCreditAsync(
            int creditId,
            CancellationToken cancellationToken = default);

        /// <summary>Bir satışın möhlətləri.</summary>
        Task<IReadOnlyList<OdenisMohlet>> GetForSaleAsync(
            int saleId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Kreditin ilkin ödəniş möhlətlərini <b>BÜTÖV</b> yazır ✓✓✓
        /// (köhnə sətirlər silinir ✓ yeniləri yazılır ✓ — «bütün siyahı» məntiqi ✓)
        /// </summary>
        Task<int> SaveForCreditAsync(
            int creditId,
            IReadOnlyList<OdenisMohlet> rows,
            CancellationToken cancellationToken = default);

        /// <summary>Satışın (nisyə) möhlətlərini BÜTÖV yazır ✓✓✓</summary>
        Task<int> SaveForSaleAsync(
            int saleId,
            IReadOnlyList<OdenisMohlet> rows,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Möhləti <b>ÖDƏNİLDİ</b> / ödənilmədi kimi işarələyir ✓
        /// (ödənildikdə pul kassaya DAXİL OLUR ✓✓✓)
        /// </summary>
        Task<bool> SetOdenildiAsync(
            int id,
            bool odenilib,
            DateTime? odenilmeTarixi = null,
            CancellationToken cancellationToken = default);

        /// <summary>Yeni möhlət əlavə edir (Kassa tabından sürətli əlavə ✓).</summary>
        Task<OdenisMohlet> AddAsync(OdenisMohlet mohlet, CancellationToken cancellationToken = default);

        /// <summary>Möhləti yeniləyir (tarix · məbləğ dəyişikliyi ✓).</summary>
        Task UpdateAsync(OdenisMohlet mohlet, CancellationToken cancellationToken = default);

        /// <summary>Tək möhləti silir.</summary>
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);

        // ====================================================================
        //  ♻️ v6.2.83 — QİSMƏN ÖDƏNİŞİN GERİ ALINMASI ✓✓✓
        // --------------------------------------------------------------------
        //  ★ İstifadəçi şikayəti: 5 000 ₼ möhlətdən 1 000 ₼ ödənildi
        //    (qalıq 4 000 ✓). Geri alanda 4 000 → 5 000 oldu ✓ AMMA
        //    yanında ƏLAVƏ 1 000 ₼-lıq möhlət yarandı ✗ («ortadan pul çıxdı»).
        //  ✅ Bu metod qismən ödəniş sətrini ƏSAS möhlətlə birləşdirir:
        //     əsas möhlətin məbləği TAM bərpa olunur ✓ və ödəniş sətri
        //     SİLİNİR ✓ → əlavə möhlət YARANMIR ✗✓✓
        // ====================================================================
        /// <summary>
        /// ♻️ Qismən ödənişi geri qaytarır — əsas möhlət bərpa olunur ✓,
        /// ödəniş sətri silinir ✓.
        /// </summary>
        /// <param name="qismenMohletId">Qismən ödəniş sətrinin Id-si.</param>
        /// <returns>Bərpa olunmuş məbləğ (₼) ✓ · əsas tapılmasa <c>null</c> ✗</returns>
        Task<decimal?> QismenOdenisiGeriQaytarAsync(
            int qismenMohletId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 🩹 Köhnə versiyalarda yaranmış «qismən ödəniş qalığı» sətirlərini
        /// təmizləyir ✓ (bir dəfəlik ✓).
        /// </summary>
        /// <returns>Düzəldilən sətir sayı ✓</returns>
        Task<int> KohnəQismenQaliqlariDuzeltAsync(CancellationToken cancellationToken = default);

        /// <summary>Bir kreditin BÜTÜN möhlətlərini silir.</summary>
        Task<int> DeleteForCreditAsync(int creditId, CancellationToken cancellationToken = default);

        /// <summary>Bir satışın BÜTÜN möhlətlərini silir.</summary>
        Task<int> DeleteForSaleAsync(int saleId, CancellationToken cancellationToken = default);
    }
}
