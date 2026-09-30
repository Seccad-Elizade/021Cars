using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>💵 Əl ilə yazılan kassa hərəkətləri üçün xüsusi repository.</summary>
    public interface IKassaHereketRepository : IRepository<KassaHereket>
    {
        /// <summary>Bütün kassa hərəkətləri — ən yenidən köhnəyə ✓.</summary>
        Task<IReadOnlyList<KassaHereket>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
    }
}
