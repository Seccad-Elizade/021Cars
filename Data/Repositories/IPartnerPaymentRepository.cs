using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>Tərəfdaş ödənişləri (xaric edilən pullar) üçün repository.</summary>
    public interface IPartnerPaymentRepository : IRepository<PartnerPayment>
    {
        /// <summary>Bütün ödənişlər — ən yenidən köhnəyə.</summary>
        Task<IReadOnlyList<PartnerPayment>> GetOrderedAsync(CancellationToken cancellationToken = default);

        /// <summary>Verilmiş tarix aralığındaki ödənişlər.</summary>
        Task<IReadOnlyList<PartnerPayment>> GetInRangeAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default);
    }
}
