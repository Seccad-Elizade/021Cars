using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>Satışlar üzrə biznes əməliyyatları.</summary>
    public interface ISaleService
    {
        Task<IReadOnlyList<Sale>> GetSalesAsync(CancellationToken cancellationToken = default);

        Task<Sale> AddSaleAsync(Sale sale, CancellationToken cancellationToken = default);

        /// <summary>
        /// SATIŞA bağlı tərəfdaş mənfəət paylarını qaytarır
        /// (<c>Satış qiyməti − Maya dəyəri</c> bölgüsü).
        /// </summary>
        Task<IReadOnlyList<PartnerShare>> GetSaleSharesAsync(
            int saleId,
            CancellationToken cancellationToken = default);

        /// <summary>Satışın tərəfdaş paylarını yenidən yazır.</summary>
        Task SaveSaleSharesAsync(
            int saleId,
            bool tetbiqOlunub,
            IReadOnlyList<PartnerShare> rows,
            CancellationToken cancellationToken = default);

        Task DeleteSaleAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Müəyyən avtomobilə aid bütün satış qeydlərini silir.</summary>
        Task DeleteSalesByCarAsync(int carId, CancellationToken cancellationToken = default);

        SaleStats BuildStats(IEnumerable<Sale> sales);
    }
}
