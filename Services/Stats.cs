namespace EnterpriseAeroStudio.Services
{
    /// <summary>Avto park üzrə yekun statistika.</summary>
    public sealed record CarStats(int TotalCars, int AvailableCars, int CreditCars, decimal TotalCost);

    /// <summary>Xərclər üzrə yekun statistika.</summary>
    public sealed record ExpenseStats(decimal Total, decimal CarTotal, decimal OfficeTotal, decimal MonthTotal);

    /// <summary>Satışlar üzrə yekun statistika (ödəniş bölgüsü ilə).</summary>
    /// <param name="Count">Satış sayı.</param>
    /// <param name="TotalRevenue">Ümumi satış məbləği (AZN).</param>
    /// <param name="TotalProfit">Ümumi mənfəət (AZN).</param>
    /// <param name="MonthRevenue">Cari ayın satış məbləği (AZN).</param>
    /// <param name="BarterCount">Barter ilə edilən satışların sayı.</param>
    /// <param name="BarterTotal">Barter hissəsinin ümumi dəyəri (AZN).</param>
    /// <param name="NagdTotal">Nağd / köçürmə ilə alınan ümumi məbləğ (AZN).</param>
    public sealed record SaleStats(
        int Count,
        decimal TotalRevenue,
        decimal TotalProfit,
        decimal MonthRevenue,
        int BarterCount = 0,
        decimal BarterTotal = 0m,
        decimal NagdTotal = 0m);

    /// <summary>
    /// Avtomobil ALIŞLARI üzrə yekun statistika (nağd / barter bölgüsü ilə).
    /// </summary>
    /// <param name="Count">Ümumi alış sayı.</param>
    /// <param name="CashCount">Nağd alışların sayı.</param>
    /// <param name="BarterCount">Barter alışlarının sayı.</param>
    /// <param name="CashTotal">Nağd alışların məbləği (AZN).</param>
    /// <param name="BarterTotal">Barter alışlarının dəyəri (AZN).</param>
    /// <param name="Total">Ümumi alış məbləği (AZN).</param>
    /// <param name="MonthTotal">Cari ayın alış məbləği (AZN).</param>
    public sealed record PurchaseStats(
        int Count,
        int CashCount,
        int BarterCount,
        decimal CashTotal,
        decimal BarterTotal,
        decimal Total,
        decimal MonthTotal);
}
