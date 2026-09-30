using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>⏳ Möhlətə verilmiş ödənişlər üçün xüsusi repository.</summary>
    public interface IOdenisMohletRepository : IRepository<OdenisMohlet>
    {
        /// <summary>
        /// BÜTÜN möhlətlər — kredit (avtomobil ✓ müqavilə ✓) və satış
        /// (avtomobil ✓ müştəri ✓) məlumatları ilə birlikdə ✓✓✓
        /// </summary>
        Task<IReadOnlyList<OdenisMohlet>> GetAllDetailedAsync(CancellationToken cancellationToken = default);

        /// <summary>Bir kreditin ilkin ödəniş möhlətləri (tarixə görə).</summary>
        Task<IReadOnlyList<OdenisMohlet>> GetByCreditAsync(
            int creditId,
            CancellationToken cancellationToken = default);

        /// <summary>Bir satışın möhlətləri (tarixə görə).</summary>
        Task<IReadOnlyList<OdenisMohlet>> GetBySaleAsync(
            int saleId,
            CancellationToken cancellationToken = default);
    }
}
