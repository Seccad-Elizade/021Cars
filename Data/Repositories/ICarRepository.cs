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

        Task<CarItem?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default);

        Task<bool> RegistrationExistsAsync(string registration, int? excludeId = null, CancellationToken cancellationToken = default);
    }
}
