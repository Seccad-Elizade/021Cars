using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <inheritdoc />
    public class PartnerRepository : Repository<Partner>, IPartnerRepository
    {
        public PartnerRepository(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Partner>> GetOrderedAsync(
            CancellationToken cancellationToken = default)
            => await Context.Partners
                            .AsNoTracking()
                            .OrderBy(p => p.Sira)
                            .ThenBy(p => p.Id)
                            .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<Partner?> GetByNameAsync(
            string ad,
            CancellationToken cancellationToken = default)
        {
            var name = (ad ?? string.Empty).Trim();
            return await Context.Partners
                                .FirstOrDefaultAsync(p => p.Ad == name, cancellationToken);
        }
    }
}
