using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class ExpenseService : IExpenseService
    {
        private readonly IExpenseRepository _expenses;
        private readonly ITrashService _trash;

        public ExpenseService(IExpenseRepository expenses, ITrashService trash)
        {
            _expenses = expenses;
            _trash = trash;
        }

        public Task<IReadOnlyList<ExpenseItem>> GetExpensesAsync(CancellationToken cancellationToken = default)
            => _expenses.GetWithCarAsync(cancellationToken);

        // ====================================================================
        //  🚀 SƏHİFƏLƏMƏ VƏ SQL YEKUNLARI ✓✓✓ (donmanın qarşısı ✓)
        // ====================================================================
        public Task<ExpensePage> GetPageAsync(
            int skip,
            int take,
            ExpenseFilter filter,
            ExpenseSort sort = ExpenseSort.Tarix,
            bool descending = true,
            CancellationToken cancellationToken = default)
            => _expenses.GetPageAsync(skip, take, filter, sort, descending, cancellationToken);

        public Task<ExpenseStats> GetStatsAsync(CancellationToken cancellationToken = default)
            => _expenses.GetStatsAsync(cancellationToken);

        public Task<int> BackfillAxtarisAsync(
            int batchSize = 5000,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default)
            => _expenses.BackfillAxtarisAsync(batchSize, progress, cancellationToken);

        public async Task<ExpenseItem> AddExpenseAsync(ExpenseItem expense, CancellationToken cancellationToken = default)
        {
            await _expenses.AddAsync(expense, cancellationToken);
            await _expenses.SaveChangesAsync(cancellationToken);
            return expense;
        }

        public async Task UpdateExpenseAsync(ExpenseItem expense, CancellationToken cancellationToken = default)
        {
            var existing = await _expenses.GetByIdAsync(expense.Id, cancellationToken);
            if (existing is null)
            {
                return;
            }

            // Redaktədən ƏVVƏLKİ vəziyyət surətə götürülür — kateqoriya / qrup /
            // məbləğ səhvən dəyişdirilərsə «Ctrl+Z» ilə geri qaytarıla bilər.
            await _trash.BackupExpenseEditAsync(expense.Id, cancellationToken);

            existing.Tarix = expense.Tarix;
            existing.Teyinat = expense.Teyinat;
            existing.Qrup = expense.Qrup;
            existing.Kategoriya = expense.Kategoriya;
            existing.CarId = expense.CarId;
            existing.Mebleg = expense.Mebleg;
            existing.OdenisUsulu = expense.OdenisUsulu;
            existing.Qeyd = expense.Qeyd;

            await _expenses.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteExpenseAsync(int id, CancellationToken cancellationToken = default)
        {
            var expense = await _expenses.GetByIdAsync(id, cancellationToken);
            if (expense is null)
            {
                return;
            }

            // Silmədən əvvəl bərpa surəti («🗑 Silinənlər» / Ctrl+Z).
            await _trash.BackupExpenseAsync(id, cancellationToken);

            _expenses.Remove(expense);
            await _expenses.SaveChangesAsync(cancellationToken);
        }

        public ExpenseStats BuildStats(IEnumerable<ExpenseItem> expenses)
        {
            var list = expenses.ToList();
            var total = list.Sum(e => e.Mebleg);
            var carTotal = list.Where(e => !Catalog.IsOffice(e.Teyinat)).Sum(e => e.Mebleg);
            var officeTotal = list.Where(e => Catalog.IsOffice(e.Teyinat)).Sum(e => e.Mebleg);
            var monthTotal = list
                .Where(e => e.Tarix.Year == DateTime.Today.Year && e.Tarix.Month == DateTime.Today.Month)
                .Sum(e => e.Mebleg);

            return new ExpenseStats(total, carTotal, officeTotal, monthTotal);
        }
    }
}
