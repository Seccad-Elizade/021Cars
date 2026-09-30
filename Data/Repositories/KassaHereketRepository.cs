using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <inheritdoc />
    public class KassaHereketRepository : Repository<KassaHereket>, IKassaHereketRepository
    {
        public KassaHereketRepository(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<KassaHereket>> GetAllOrderedAsync(
            CancellationToken cancellationToken = default)
            => await Context.KassaHereketleri
                            .AsNoTracking()
                            .OrderByDescending(k => k.Tarix)
                            .ThenByDescending(k => k.Id)
                            .ToListAsync(cancellationToken);
    }
}
