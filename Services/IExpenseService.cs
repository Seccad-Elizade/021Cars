using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>Xərclər üzrə biznes əməliyyatları.</summary>
    public interface IExpenseService
    {
        Task<IReadOnlyList<ExpenseItem>> GetExpensesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 🚀 <b>YALNIZ BİR SƏHİFƏNİ</b> qaytarır ✓✓✓ (milyon sətir üçün ✓)
        /// <para><see cref="ExpenseFilter"/> şərtləri SQL-də tətbiq olunur ✓</para>
        /// </summary>
        Task<ExpensePage> GetPageAsync(
            int skip,
            int take,
            ExpenseFilter filter,
            ExpenseSort sort = ExpenseSort.Tarix,
            bool descending = true,
            CancellationToken cancellationToken = default);

        /// <summary>🚀 Yekun məbləğlər — SQL <c>SUM</c> ilə ✓✓✓ (yaddaşa yükləmədən ✓)</summary>
        Task<ExpenseStats> GetStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>🚀 Köhnə qeydlərin axtarış mətnini doldurur ✓ (bir dəfəlik, arxa fonda ✓)</summary>
        Task<int> BackfillAxtarisAsync(
            int batchSize = 5000,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default);

        Task<ExpenseItem> AddExpenseAsync(ExpenseItem expense, CancellationToken cancellationToken = default);

        Task UpdateExpenseAsync(ExpenseItem expense, CancellationToken cancellationToken = default);

        Task DeleteExpenseAsync(int id, CancellationToken cancellationToken = default);

        ExpenseStats BuildStats(IEnumerable<ExpenseItem> expenses);
    }
}
