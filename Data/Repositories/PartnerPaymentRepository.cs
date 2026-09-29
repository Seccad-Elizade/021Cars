using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <inheritdoc />
    public class PartnerPaymentRepository : Repository<PartnerPayment>, IPartnerPaymentRepository
    {
        public PartnerPaymentRepository(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerPayment>> GetOrderedAsync(
            CancellationToken cancellationToken = default)
            => await Context.PartnerPayments
                            .AsNoTracking()
                            .OrderByDescending(p => p.Tarix)
                            .ThenByDescending(p => p.Id)
                            .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<PartnerPayment>> GetInRangeAsync(
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default)
        {
            var start = from.Date;
            var end = to.Date;

            return await Context.PartnerPayments
                                .AsNoTracking()
                                .Where(p => p.Tarix >= start && p.Tarix <= end)
                                .OrderByDescending(p => p.Tarix)
                                .ThenByDescending(p => p.Id)
                                .ToListAsync(cancellationToken);
        }
    }
}
