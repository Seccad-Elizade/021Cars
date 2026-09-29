using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    public interface ICreditRepository : IRepository<Credit>
    {
        Task<IReadOnlyList<Credit>> GetWithCarAsync(CancellationToken cancellationToken = default);
    }
}
