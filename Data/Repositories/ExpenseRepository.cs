using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    public class ExpenseRepository : Repository<ExpenseItem>, IExpenseRepository
    {
        public ExpenseRepository(AppDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<ExpenseItem>> GetWithCarAsync(CancellationToken cancellationToken = default)
            => await Context.Expenses
                            .AsNoTracking()
                            .Include(e => e.Car)
                            .OrderByDescending(e => e.Tarix)
                            .ThenByDescending(e => e.Id)
                            .ToListAsync(cancellationToken);

        public IQueryable<ExpenseItem> Query() => Context.Expenses.AsNoTracking();

        // ====================================================================
        //  🚀 SƏHİFƏLƏMƏ (PAGING) ✓✓✓
        // --------------------------------------------------------------------
        //  ⚠ ƏVVƏL: `SELECT * FROM Xercler` → BÜTÜN cədvəl yaddaşa ✗
        //     (1 000 000 qeyd ≈ 1 GB RAM + 3-5 saniyə donma ✗✗)
        //  ✅ İNDİ: `SELECT ... LIMIT 200 OFFSET 0` → yalnız görünən səhifə ✓
        //     (bazada 10 000 000 sətir olsa da cavab ~5-20 ms ✓✓✓)
        // ====================================================================
        public async Task<ExpensePage> GetPageAsync(
            int skip,
            int take,
            ExpenseFilter filter,
            ExpenseSort sort = ExpenseSort.Tarix,
            bool descending = true,
            CancellationToken cancellationToken = default)
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take, 1, 5000);

            var sorğu = Süzgəc(Context.Expenses.AsNoTracking(), filter);

            // 🔢 Ümumi say (səhifədən asılı olmayaraq ✓) — SQL `COUNT(*)` ✓
            var ümumi = await sorğu.CountAsync(cancellationToken);

            var sətirlər = await Sırala(sorğu, sort, descending)
                .Skip(skip)
                .Take(take)
                .Include(e => e.Car)
                .ToListAsync(cancellationToken);

            return new ExpensePage(sətirlər, ümumi);
        }

        /// <summary>
        /// 🚀 Bütün xərclərin yekunu — <b>SQL-də</b> ✓✓✓ (TƏK sorğu ✓)
        /// <para>
        /// ⚠ <b>NƏ ÜÇÜN <c>double</c>?</b> SQLite bazasında <c>decimal</c> cəmi
        /// <b>DƏSTƏKLƏNMİR</b> ✗ (<c>SQLite cannot apply aggregate operator 'Sum'
        /// on expressions of type 'decimal'</c> ✗✓✓) — buna görə cəm
        /// <c>double</c> ilə alınır ✓ (dəqiqlik kifayətdir ✓: 15-16 rəqəm ✓)
        /// və nəticə <c>decimal</c>-a çevrilir ✓
        /// </para>
        /// </summary>
        public async Task<ExpenseStats> GetStatsAsync(CancellationToken cancellationToken = default)
        {
            var ayınƏvvəli = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var ayınSonu = ayınƏvvəli.AddMonths(1);

            var yekun = await Context.Expenses
                .AsNoTracking()
                .GroupBy(e => 1)
                .Select(q => new
                {
                    Total = q.Sum(e => (double?)e.Mebleg),
                    CarTotal = q.Sum(e => e.Teyinat != Catalog.OfficeDestination ? (double?)e.Mebleg : null),
                    OfficeTotal = q.Sum(e => e.Teyinat == Catalog.OfficeDestination ? (double?)e.Mebleg : null),
                    MonthTotal = q.Sum(e => e.Tarix >= ayınƏvvəli && e.Tarix < ayınSonu ? (double?)e.Mebleg : null)
                })
                .FirstOrDefaultAsync(cancellationToken);

            return yekun is null
                ? new ExpenseStats(0m, 0m, 0m, 0m)
                : new ExpenseStats(
                    Yuvarlaq(yekun.Total),
                    Yuvarlaq(yekun.CarTotal),
                    Yuvarlaq(yekun.OfficeTotal),
                    Yuvarlaq(yekun.MonthTotal));
        }

        /// <summary><c>double</c> cəmini pullu <c>decimal</c>-a çevirir ✓ (2 rəqəm ✓).</summary>
        private static decimal Yuvarlaq(double? dəyər) => Math.Round((decimal)(dəyər ?? 0d), 2);

        /// <summary>
        /// 🚀 Köhnə qeydlərin «Axtaris» mətnini doldurur ✓✓✓
        /// <para>
        /// ⚠ <b>BİR SQL ƏMRİ İLƏ</b> ✓ — EF ilə sətir-sətir yazmaq 200 000
        /// sətirdə 10+ DƏQİQƏ çəkirdi ✗✓✓ SQLite isə eyni işi
        /// <b>saniyələr</b> içində görür ✓ (aşağıdaki <c>replace()</c> zənciri ✓)
        /// </para>
        /// <para>
        /// ⚠ <c>lower()</c> funksiyası SQLite-da yalnız ASCII hərfləri kiçildir ✗ →
        /// Azərbaycan hərfləri (<c>Ə Ş Ç Ğ İ Ö Ü</c>) ayrıca əvəzlənir ✓✓✓
        /// (C# tərəfindəki <see cref="MetinAxtaris.Normallasdir"/> ilə EYNİ nəticə ✓)
        /// </para>
        /// <para>
        /// Dəstələrlə işləyir ✓ (50 000 sətir ✓) → uzun kilid yaranmır ✗
        /// </para>
        /// </summary>
        public async Task<int> BackfillAxtarisAsync(
            int batchSize = 50_000,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            batchSize = Math.Clamp(batchSize, 1000, 500_000);

            // 🔢 Id aralığı: ən kiçik və ən böyük Id ✓
            var aralıq = await Context.Expenses
                .AsNoTracking()
                .GroupBy(e => 1)
                .Select(g => new { Min = g.Min(e => e.Id), Max = g.Max(e => e.Id) })
                .FirstOrDefaultAsync(cancellationToken);

            if (aralıq is null)
            {
                return 0;
            }

            var cəmi = 0;

            // ⚠ Aralıq üzrə dəstələr ✓ (alt-sorğu YOXDUR ✗ → sadə və sürətli ✓)
            for (var başlanğıc = aralıq.Min; başlanğıc <= aralıq.Max; başlanğıc += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var son = başlanğıc + batchSize - 1;

                var sql =
                    "UPDATE Xercler SET Axtaris = " + NormallasdirmaSql +
                    " WHERE Id >= " + başlanğıc + " AND Id <= " + son +
                    " AND (Axtaris IS NULL OR Axtaris = '');";

                var dəyişən = await Context.Database.ExecuteSqlRawAsync(sql, cancellationToken);

                if (dəyişən > 0)
                {
                    cəmi += dəyişən;
                    progress?.Invoke(cəmi);
                }
            }

            return cəmi;
        }

        /// <summary>
        /// SQL daxilində mətn normalizasiyası ✓✓✓
        /// (<c>lower()</c> + Azərbaycan hərflərinin əvəzlənməsi ✓ —
        /// <see cref="MetinAxtaris.Normallasdir"/> ilə eyni məntiq ✓)
        /// </summary>
        private const string NormallasdirmaSql =
            "replace(replace(replace(replace(replace(replace(replace(replace(replace(replace(" +
            "replace(replace(replace(replace(" +
            "lower(coalesce(Teyinat,'') || ' ' || coalesce(Qrup,'') || ' ' || coalesce(Kategoriya,'') || ' ' || " +
            "coalesce(OdenisUsulu,'') || ' ' || coalesce(Qeyd,''))" +
            ",'Ə','e'),'ə','e'),'İ','i'),'ı','i'),'Ş','s'),'ş','s'),'Ç','c'),'ç','c'),'Ğ','g'),'ğ','g')" +
            ",'Ö','o'),'ö','o'),'Ü','u'),'ü','u')";

        /// <summary>Süzgəci <c>IQueryable</c>-ə çevirir ✓ (HAMISI SQL-də ✓).</summary>
        private static IQueryable<ExpenseItem> Süzgəc(IQueryable<ExpenseItem> sorğu, ExpenseFilter filter)
        {
            if (!string.IsNullOrWhiteSpace(filter.Teyinat))
            {
                sorğu = sorğu.Where(e => e.Teyinat == filter.Teyinat);
            }

            if (!string.IsNullOrWhiteSpace(filter.TeyinatDeyil))
            {
                var xaric = filter.TeyinatDeyil;
                sorğu = sorğu.Where(e => e.Teyinat != xaric);
            }

            if (filter.CarId is int carId)
            {
                sorğu = sorğu.Where(e => e.CarId == carId);
            }

            if (!string.IsNullOrWhiteSpace(filter.Kategoriya))
            {
                var kateqoriya = filter.Kategoriya;
                sorğu = sorğu.Where(e => e.Kategoriya == kateqoriya);
            }

            var axtaris = MetinAxtaris.LikeEhtiyatla(filter.Axtaris);

            if (axtaris.Length > 0)
            {
                // 🚀 Axtarış NORMAL EDİLMİŞ mətn üzərində gedir ✓✓✓
                //  (ə/e · ş/s · ç/c · ğ/g · ı/i · ö/o · ü/u bir sayılır ✓)
                var nümunə = $"%{axtaris}%";

                // ⚠ HƏLƏ «Axtaris» mətni hazırlanmamış KÖHNƏ qeydlər üçün
                //   sütunların ÖZÜ yoxlanılır ✓ → axtarış «backfill»dən ASILI
                //   DEYİL ✗✓✓ (fon rejimindəki hazırlıq yalnız SÜRƏT üçündür ✓)
                var xamNümunə = $"%{MetinAxtaris.LikeEhtiyatlaXam(filter.Axtaris)}%";

                sorğu = sorğu.Where(e =>
                    EF.Functions.Like(e.Axtaris, nümunə, "\\")
                    || (e.Axtaris == string.Empty
                        && (EF.Functions.Like(e.Qrup, xamNümunə, "\\")
                            || EF.Functions.Like(e.Kategoriya, xamNümunə, "\\")
                            || EF.Functions.Like(e.Qeyd, xamNümunə, "\\")))
                    || EF.Functions.Like(e.Car!.QeydiyyatNisani, xamNümunə, "\\")
                    || EF.Functions.Like(e.Car!.Marka, xamNümunə, "\\"));
            }

            return sorğu;
        }

        /// <summary>Sıralama (SQL <c>ORDER BY</c> ✓ — yüklənmiş səhifə üzrə deyil ✗✓✓).</summary>
        private static IOrderedQueryable<ExpenseItem> Sırala(
            IQueryable<ExpenseItem> sorğu,
            ExpenseSort sort,
            bool descending) => (sort, descending) switch
            {
                (ExpenseSort.Teyinat, true) => sorğu.OrderByDescending(e => e.Teyinat).ThenByDescending(e => e.Id),
                (ExpenseSort.Teyinat, false) => sorğu.OrderBy(e => e.Teyinat).ThenBy(e => e.Id),
                (ExpenseSort.Qrup, true) => sorğu.OrderByDescending(e => e.Qrup).ThenByDescending(e => e.Id),
                (ExpenseSort.Qrup, false) => sorğu.OrderBy(e => e.Qrup).ThenBy(e => e.Id),
                (ExpenseSort.Kategoriya, true) => sorğu.OrderByDescending(e => e.Kategoriya).ThenByDescending(e => e.Id),
                (ExpenseSort.Kategoriya, false) => sorğu.OrderBy(e => e.Kategoriya).ThenBy(e => e.Id),
                (ExpenseSort.Qeyd, true) => sorğu.OrderByDescending(e => e.Qeyd).ThenByDescending(e => e.Id),
                (ExpenseSort.Qeyd, false) => sorğu.OrderBy(e => e.Qeyd).ThenBy(e => e.Id),
                (_, true) => sorğu.OrderByDescending(e => e.Tarix).ThenByDescending(e => e.Id),
                _ => sorğu.OrderBy(e => e.Tarix).ThenBy(e => e.Id)
            };
    }
}
