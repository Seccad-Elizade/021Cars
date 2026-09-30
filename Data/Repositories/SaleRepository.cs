using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    public class SaleRepository : Repository<Sale>, ISaleRepository
    {
        public SaleRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<Sale>> GetWithCarAsync(CancellationToken cancellationToken = default)
            => await Context.Sales
                            .AsNoTracking()
                            .Include(s => s.Car)
                            // ⏳ Nisyə (möhlətli) satışın ödəniş planı ✓✓✓
                            .Include(s => s.Mohletler)
                            .OrderByDescending(s => s.SatisTarixi)
                            .ThenByDescending(s => s.Id)
                            .ToListAsync(cancellationToken);
    }
}
