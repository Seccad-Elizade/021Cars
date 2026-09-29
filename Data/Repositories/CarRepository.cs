using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    public class CarRepository : Repository<CarItem>, ICarRepository
    {
        public CarRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<CarItem>> GetWithExpensesAsync(CancellationToken cancellationToken = default)
            => await Context.Cars
                            .AsNoTracking()
                            .Include(c => c.Expenses)
                            .OrderBy(c => c.SiraNomresi)
                            .ThenByDescending(c => c.Id)
                            .ToListAsync(cancellationToken);

        /// <summary>
        /// 🚀 <b>YÜNGÜL AVTOMOBİL SİYAHISI</b> ✓✓✓ (MİLYON SƏTİR ÜÇÜN)
        /// <para>
        /// ⚠ <see cref="GetWithExpensesAsync"/> hər avtomobilin <b>BÜTÜN</b>
        /// xərclərini yaddaşa yükləyir ✗ → 1 000 000 xərc sətri = ~1 GB RAM ✗✓✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: xərc cəmləri SQL-də <c>GROUP BY</c> ilə hesablanır ✓ →
        /// yalnız avtomobil obyektləri (yüzlərlə ✓) yaddaşda qalır ✓✓✓
        /// </para>
        /// </summary>
        public async Task<IReadOnlyList<CarItem>> GetWithExpenseTotalsAsync(
            CancellationToken cancellationToken = default)
        {
            var cars = await Context.Cars
                .AsNoTracking()
                .OrderBy(c => c.SiraNomresi)
                .ThenByDescending(c => c.Id)
                .ToListAsync(cancellationToken);

            // 📊 CƏMLƏR — TƏK SQL SORĞUSU ✓ (avtomobil üzrə qruplaşdırma ✓)
            // ⚠ SQLite `decimal` cəmini dəstəkləmir ✗ → `double` ilə alınır ✓
            var cəmlər = await Context.Expenses
                .AsNoTracking()
                .Where(e => e.CarId != null)
                .GroupBy(e => e.CarId!.Value)
                .Select(g => new
                {
                    CarId = g.Key,
                    Butun = g.Sum(e => (double?)e.Mebleg),
                    AlisXaric = g.Sum(e => e.Kategoriya != "Alış" ? (double?)e.Mebleg : null)
                })
                .ToListAsync(cancellationToken);

            var lüğət = new Dictionary<int, (decimal Butun, decimal AlisXaric)>(cəmlər.Count);

            foreach (var sətir in cəmlər)
            {
                lüğət[sətir.CarId] = (
                    Math.Round((decimal)(sətir.Butun ?? 0d), 2),
                    Math.Round((decimal)(sətir.AlisXaric ?? 0d), 2));
            }

            foreach (var car in cars)
            {
                if (lüğət.TryGetValue(car.Id, out var cəm))
                {
                    car.ButunXerclerCemi = cəm.Butun;
                    car.XerclerCemi = cəm.AlisXaric;
                }
            }

            return cars;
        }

        public async Task<CarItem?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
            => await Context.Cars
                            .Include(c => c.Expenses)
                            .Include(c => c.Credits)
                            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        public async Task<bool> RegistrationExistsAsync(string registration, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            var normalized = (registration ?? string.Empty).Trim().ToUpperInvariant();
            return await Context.Cars.AnyAsync(
                c => c.QeydiyyatNisani.ToUpper() == normalized &&
                     (!excludeId.HasValue || c.Id != excludeId.Value),
                cancellationToken);
        }
    }
}
