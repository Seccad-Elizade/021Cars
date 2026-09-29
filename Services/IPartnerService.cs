using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Dövr üzrə tərəfdaş portfelinin YEKUN göstəriciləri.
    /// </summary>
    public sealed record PartnerPortfolioTotals(
        decimal Qazanilmis,
        decimal Verilmis,
        int BolguSayi,
        int TerefdasSayi)
    {
        /// <summary>Ödənilməli qalıq: qazanılmış − verilmiş (₼).</summary>
        public decimal Qaliq => Qazanilmis - Verilmis;

        /// <summary>Qazanılmış göstəricisi üçün mətn.</summary>
        public string QazanilmisMetni => $"{Qazanilmis:N2} ₼";

        /// <summary>Verilmiş göstəricisi üçün mətn.</summary>
        public string VerilmisMetni => $"{Verilmis:N2} ₼";

        /// <summary>Qalıq göstəricisi üçün mətn.</summary>
        public string QaliqMetni => $"{Qaliq:N2} ₼";
    }

    /// <summary>
    /// TƏRƏFDAŞ BÖLGÜSÜ sisteminin mərkəzi xidməti.
    /// <para>
    /// Vəzifələri:
    /// <list type="number">
    ///   <item>Tərəfdaşları idarə etmək — <b>əlavə et / sil / faiz təyin et</b>.</item>
    ///   <item>Tərəfdaşlara <b>xaric edilən pulları</b> (ödənişləri) izləmək.</item>
    ///   <item>BÜTÜN bölmələrdən (satış · kredit · kredit əlavə gəlir) bölgüləri
    ///         toplayıb <b>vahid jurnal</b> qurmaq.</item>
    ///   <item>Tərəfdaş kartı («umumi dövriyyə») və aylıq dövriyyə hesabatları.</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface IPartnerService
    {
        // ---------------- TƏRƏFDAŞ İDARƏSİ ----------------

        /// <summary>Bütün tərəfdaşlar (cədvəl sırası ilə).</summary>
        Task<IReadOnlyList<Partner>> GetPartnersAsync(CancellationToken cancellationToken = default);

        /// <summary>Yeni tərəfdaş əlavə edir. Eyni ad varsa xəta atır.</summary>
        Task<Partner> AddPartnerAsync(
            string ad,
            decimal faiz,
            bool qaligPayi,
            string qeyd = "",
            CancellationToken cancellationToken = default);

        /// <summary>Mövcud tərəfdaşı yeniləyir (ad, faiz, qalıq, aktiv).</summary>
        Task UpdatePartnerAsync(Partner partner, CancellationToken cancellationToken = default);

        /// <summary>Tərəfdaşı silir. Ona aid paylar/ödənişlər tarixçədə qalır.</summary>
        Task DeletePartnerAsync(int partnerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cədvəldə faizlər dəyişdirildikdən sonra HAMISINI bir dəfəyə yazır
        /// («💾 Faizləri yadda saxla» düyməsi).
        /// </summary>
        Task SavePartnersAsync(
            IEnumerable<Partner> partners,
            CancellationToken cancellationToken = default);

        // ---------------- ÖDƏNİŞLƏR (XARİC EDİLƏN PULLAR) ----------------

        /// <summary>Bütün tərəfdaş ödənişləri (ən yenidən köhnəyə).</summary>
        Task<IReadOnlyList<PartnerPayment>> GetPaymentsAsync(CancellationToken cancellationToken = default);

        /// <summary>Yeni ödəniş qeydə alır.</summary>
        Task AddPaymentAsync(PartnerPayment payment, CancellationToken cancellationToken = default);

        /// <summary>Ödəniş qeydini silir.</summary>
        Task DeletePaymentAsync(int paymentId, CancellationToken cancellationToken = default);

        // ---------------- ANALİTİKA ----------------

        /// <summary>
        /// BÜTÜN mənbələrdən bölgü jurnalı: <i>hansı maşından, hansı tarixdə,
        /// hansı tərəfdaşa nə qədər</i>.
        /// </summary>
        Task<IReadOnlyList<PartnerLedgerRow>> BuildLedgerAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);

        /// <summary>Tərəfdaş kartları: qazanılmış / verilmiş / qalıq / dövriyyə.</summary>
        Task<IReadOnlyList<PartnerCardRow>> BuildCardsAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);

        /// <summary>Aylıq dövriyyə: «hansı ayda kimə nə qədər gəldi».</summary>
        Task<IReadOnlyList<PartnerMonthlyRow>> BuildMonthlyAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);

        /// <summary>Dövr üzrə yekun göstəricilər.</summary>
        Task<PartnerPortfolioTotals> BuildTotalsAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);
    }
}
