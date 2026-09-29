using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Məlumatların Excel-uyğun fayla (CSV) ixracını təmin edən xidmət.
    /// </summary>
    public interface IExportService
    {
        Task ExportCarsAsync(IEnumerable<CarItem> cars, string filePath, CancellationToken cancellationToken = default);

        Task ExportExpensesAsync(IEnumerable<ExpenseItem> expenses, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Avtomobil ALIŞLARINI CSV faylına yazır — nağd/barter üsulu və
        /// barter təsviri daxil olmaqla.
        /// </summary>
        Task ExportPurchasesAsync(IEnumerable<CarItem> cars, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// 👥 TƏRƏFDAŞ BÖLGÜSÜ hesabatını (kartlar + bölgü jurnalı + ödənişlər)
        /// Excel-uyğun CSV faylına yazır.
        /// </summary>
        Task ExportPartnersAsync(
            IEnumerable<PartnerCardRow> cards,
            IEnumerable<PartnerLedgerRow> ledger,
            IEnumerable<PartnerPayment> payments,
            string filePath,
            CancellationToken cancellationToken = default);
    }
}
