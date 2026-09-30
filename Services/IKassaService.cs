using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 💵 <b>KASSA XİDMƏTİ</b> — kassanın tam hesabatını qurur və əl ilə yazılan
    /// hərəkətləri idarə edir ✓✓✓
    /// <para>
    /// Jurnalın böyük hissəsi <b>AVTOMATİK</b> yaranır (satış ✓ kredit ✓ xərc ✓
    /// tərəfdaş bölgüsü ✓) — bax <see cref="KassaHesabi"/> ✓
    /// </para>
    /// </summary>
    public interface IKassaService
    {
        /// <summary>Əl ilə yazılan kassa hərəkətləri (ən yenidən köhnəyə ✓).</summary>
        Task<IReadOnlyList<KassaHereket>> GetHereketlerAsync(CancellationToken cancellationToken = default);

        /// <summary>Yeni əl ilə kassa hərəkəti (kassaya qoyuldu ✓ / kassadan götürüldü ✓).</summary>
        Task<KassaHereket> AddHereketAsync(KassaHereket hereket, CancellationToken cancellationToken = default);

        /// <summary>Mövcud əl ilə kassa hərəkətini yeniləyir.</summary>
        Task UpdateHereketAsync(KassaHereket hereket, CancellationToken cancellationToken = default);

        /// <summary>Əl ilə yazılan hərəkəti silir.</summary>
        Task DeleteHereketAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Verilmiş dövr üçün <b>TAM KASSA HESABATI</b> ✓✓✓
        /// (bütün mənbələr oxunur · açılış qalığı hesablanır · yekunlar çıxarılır ✓)
        /// </summary>
        Task<KassaHesabati> BuildAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);

        /// <summary>Bütün vaxt üzrə kassa hesabatı (açılış qalığı = 0 ✓).</summary>
        Task<KassaHesabati> BuildAllTimeAsync(CancellationToken cancellationToken = default);
    }
}
