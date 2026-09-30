using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <inheritdoc />
    public class OdenisMohletRepository : Repository<OdenisMohlet>, IOdenisMohletRepository
    {
        public OdenisMohletRepository(AppDbContext context) : base(context)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<OdenisMohlet>> GetAllDetailedAsync(
            CancellationToken cancellationToken = default)
            => await Context.OdenisMohletler
                            .AsNoTracking()
                            .Include(m => m.Credit)!.ThenInclude(c => c!.Car)
                            .Include(m => m.Sale)!.ThenInclude(s => s!.Car)
                            .OrderBy(m => m.Tarix)
                            .ThenBy(m => m.Id)
                            .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<OdenisMohlet>> GetByCreditAsync(
            int creditId,
            CancellationToken cancellationToken = default)
            => await Context.OdenisMohletler
                            .AsNoTracking()
                            .Where(m => m.CreditId == creditId)
                            .OrderBy(m => m.Sira)
                            .ThenBy(m => m.Tarix)
                            .ThenBy(m => m.Id)
                            .ToListAsync(cancellationToken);

        /// <inheritdoc />
        public async Task<IReadOnlyList<OdenisMohlet>> GetBySaleAsync(
            int saleId,
            CancellationToken cancellationToken = default)
            => await Context.OdenisMohletler
                            .AsNoTracking()
                            .Where(m => m.SaleId == saleId)
                            .OrderBy(m => m.Sira)
                            .ThenBy(m => m.Tarix)
                            .ThenBy(m => m.Id)
                            .ToListAsync(cancellationToken);
    }
}
