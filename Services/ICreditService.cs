using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>Kreditlər üzrə biznes əməliyyatları.</summary>
    public interface ICreditService
    {
        Task<IReadOnlyList<Credit>> GetCreditsAsync(CancellationToken cancellationToken = default);

        Task<Credit> AddCreditAsync(Credit credit, CancellationToken cancellationToken = default);

        Task UpdateCreditAsync(Credit credit, CancellationToken cancellationToken = default);

        Task DeleteCreditAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// KREDİT MÜQAVİLƏSİNƏ bağlı tərəfdaş paylarını qaytarır.
        /// <para>Maşın kreditə veriləndə onun mənfəəti
        /// (<c>Satış qiyməti − Maya dəyəri</c>) tərəfdaşlara bölünür.</para>
        /// </summary>
        Task<IReadOnlyList<PartnerShare>> GetCreditSharesAsync(
            int creditId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Kreditin tərəfdaş paylarını yenidən yazır (əvvəlkilər silinir).
        /// <paramref name="tetbiqOlunub"/> <c>false</c> olduqda yalnız silinmə aparılır.
        /// </summary>
        Task SaveCreditSharesAsync(
            int creditId,
            bool tetbiqOlunub,
            IReadOnlyList<PartnerShare> rows,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CreditTransaction>> GetTransactionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir kredit əməliyyatına aid TƏRƏFDAŞ PAYLARI (Zaur, Eşqin, Asiman, Asif, Musa).
        /// </summary>
        Task<IReadOnlyList<PartnerShare>> GetPartnerSharesAsync(
            int transactionId,
            CancellationToken cancellationToken = default);

        Task<CreditTransaction> AddTransactionAsync(CreditTransaction transaction, CancellationToken cancellationToken = default);

        /// <summary>Mövcud əməliyyatı yeniləyir (məs. gecikmənin ödənilib işarəsi).</summary>
        Task UpdateTransactionAsync(CreditTransaction transaction, CancellationToken cancellationToken = default);

        Task DeleteTransactionAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 📕 <b>KREDİTİ VAXTINDAN TEZ BAĞLAYIR</b> ✓✓✓
        /// <list type="number">
        ///   <item>Kredit → <b>«Bağlı»</b> ✓ (qeyd yazılır ✓)</item>
        ///   <item>Avtomobil → <b>«Satıldı»</b> ✓ (parkdan və «Kreditdə»
        ///         göstəricisindən ÇIXIR ✓, arxivə keçir ✓)</item>
        /// </list>
        /// <para>
        /// ⚠ ClearTracker ilə işləyir ✓ — köhnə izlənən nüsxə UPDATE-i
        /// bloklamasın ✓✓✓
        /// </para>
        /// </summary>
        Task<bool> CloseCreditEarlyAsync(
            int creditId,
            string qeyd,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// <b>BÜTÜN kreditlərin</b> tərəfdaş bölgüləri — <c>CreditId</c>-yə görə
        /// qruplaşdırılmış (bir bazadan oxunuşla).
        /// <para>
        /// «Kreditlər» tabında seçilmiş kreditin <b>XEYİR</b> bölgüsünü
        /// göstərmək üçün istifadə olunur.
        /// </para>
        /// </summary>
        Task<IReadOnlyDictionary<int, IReadOnlyList<PartnerShare>>> GetCreditSharesMapAsync(
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Avtomobilə bağlı <b>«Bağlı» krediti yenidən «Aktiv» edir</b>
        /// (arxivdən geri qaytarma zamanı).
        /// <para>
        /// ⚠ Vacibdir: əks halda maşın parka qayıtsa da krediti «Bağlı» qalır ✗
        /// və növbəti açılışda yenidən arxivə düşə bilər ✗
        /// </para>
        /// </summary>
        /// <returns><c>true</c> — kredit yenidən açıldı.</returns>
        Task<bool> ReopenCreditByCarAsync(int carId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 🎯 <b>TAM ÖDƏNİLMİŞ KREDİTLƏRİ BAĞLAYIR</b> — <b>YALNIZ ƏL İLƏ</b> ✗✓✓
        /// <para>
        /// ⛔ <b>v6.2.34:</b> bu metod artıq <b>AVTOMATİK ÇAĞIRILMIR</b> ✗ —
        /// nə ödənişdən sonra, nə açılışda ✓.
        /// </para>
        /// <para>
        /// ★ İstifadəçi tələbi: «qalıq 0 olsa da kredit avtomatik bağlanmasın ✗ —
        /// mən əl ilə <b>«⏹ KREDİTİ BİTDİ»</b> düyməsinə basmalıyam ✓» ✓✓✓
        /// </para>
        /// <para>
        /// Kredit üzrə toplanmış ödənişlər kreditin qiymətinə
        /// (<c>Kreditləşdirilən + Faiz</c>) çatdıqda kredit <c>«Bağlı»</c>,
        /// avtomobil isə <c>«Satıldı»</c> olur.
        /// </para>
        /// </summary>
        /// <returns>Bu çağırışda bağlanan kreditlərin sayı.</returns>
        Task<int> RefreshCreditCompletionAsync(CancellationToken cancellationToken = default);
    }
}
