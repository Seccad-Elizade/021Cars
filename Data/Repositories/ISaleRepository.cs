using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    public interface ISaleRepository : IRepository<Sale>
    {
        Task<IReadOnlyList<Sale>> GetWithCarAsync(CancellationToken cancellationToken = default);
    }
}
