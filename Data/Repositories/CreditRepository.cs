using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    public class CreditRepository : Repository<Credit>, ICreditRepository
    {
        public CreditRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<Credit>> GetWithCarAsync(CancellationToken cancellationToken = default)
            => await Context.Credits
                            .AsNoTracking()
                            .Include(c => c.Car)
                            // ⏳ İlkin ödənişə verilən möhlətlər (bax: OdenisMohlet ✓✓✓)
                            .Include(c => c.IlkinMohletleri)
                            .OrderByDescending(c => c.Id)
                            .ToListAsync(cancellationToken);
    }
}
