using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>Tərəfdaşlar üçün xüsusi repository.</summary>
    public interface IPartnerRepository : IRepository<Partner>
    {
        /// <summary>Bütün tərəfdaşlar — cədvəldə göstərilmə sırası ilə.</summary>
        Task<IReadOnlyList<Partner>> GetOrderedAsync(CancellationToken cancellationToken = default);

        /// <summary>Verilmiş adlı tərəfdaş (təkrar əlavənin qarşısını almaq üçün).</summary>
        Task<Partner?> GetByNameAsync(string ad, CancellationToken cancellationToken = default);
    }
}
