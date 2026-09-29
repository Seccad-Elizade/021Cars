using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <inheritdoc />
    public class PartnerShareRepository : Repository<PartnerShare>, IPartnerShareRepository
    {
        public PartnerShareRepository(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerShare>> GetByTransactionAsync(
            int transactionId,
            CancellationToken cancellationToken = default)
            => await Context.PartnerShares
                            .AsNoTracking()
                            .Where(p => p.CreditTransactionId == transactionId)
                            .OrderBy(p => p.Sira)
                            .ThenBy(p => p.Id)
                            .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerShare>> GetAllOrderedAsync(
            CancellationToken cancellationToken = default)
            => await Context.PartnerShares
                            .AsNoTracking()
                            .OrderBy(p => p.CreditTransactionId)
                            .ThenBy(p => p.Sira)
                            .ThenBy(p => p.Id)
                            .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerShare>> GetAllDetailedAsync(
            CancellationToken cancellationToken = default)
            => await Context.PartnerShares
                            .AsNoTracking()
                            .Include(p => p.Sale)
                                .ThenInclude(s => s!.Car)
                            .Include(p => p.Credit)
                                .ThenInclude(c => c!.Car)
                            .Include(p => p.CreditTransaction)
                                .ThenInclude(t => t!.Credit)
                                    .ThenInclude(c => c!.Car)
                            .OrderByDescending(p => p.Id)
                            .ToListAsync(cancellationToken);
    }
}
