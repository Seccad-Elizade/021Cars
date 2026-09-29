namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// Maliyyə qrafikində bir sütun (gün və ya ay) üzrə göstəricilər.
    /// Hündürlüklər piksel ilə əvvəlcədən hesablanır ki, XAML sadə qalsın.
    /// </summary>
    public sealed record FinanceChartPoint(
        string Label,
        decimal Income,
        decimal Expense,
        decimal Profit,
        double IncomeHeight,
        double ExpenseHeight,
        double ProfitHeight);
}
