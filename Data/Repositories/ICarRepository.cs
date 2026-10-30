using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    public interface ICarRepository : IRepository<CarItem>
    {
        Task<IReadOnlyList<CarItem>> GetWithExpensesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 🚀 <b>YÜNGÜL SİYAHI</b> ✓✓✓ — xərc sətirləri YÜKLƏNMİR ✗,
        /// cəmlər SQL <c>GROUP BY</c> ilə gəlir ✓ (milyon sətir üçün ✓✓✓)
        /// </summary>
        Task<IReadOnlyList<CarItem>> GetWithExpenseTotalsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 🚀 <b>TƏK AVTOMOBİL + XƏRC CƏMİ</b> ✓✓✓ (v6.2.80)
        /// <para>
        /// ⚠ Əvvəl «tam maya» üçün <see cref="GetWithExpensesAsync"/> çağırılırdı ✗
        /// → BÜTÜN park + BÜTÜN xərclər yaddaşa yüklənirdi ✗ → donma ✗✓✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: yalnız <b>bir</b> avtomobil ✓, xərc cəmi isə SQL
        /// <c>SUM</c> ilə ✓ (`XerclerCemi` doldurulur ✓ → `MayaDeyeri` dəqiqdir ✓)
        /// </para>
        /// </summary>
        Task<CarItem?> GetWithExpenseTotalAsync(int id, CancellationToken cancellationToken = default);

        Task<CarItem?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);

        Task<bool> RegistrationExistsAsync(string registration, int? excludeId = null, CancellationToken cancellationToken = default);
    }
}
