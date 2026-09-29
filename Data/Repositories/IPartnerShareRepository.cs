using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>Tərəfdaş payları üçün xüsusi repository.</summary>
    public interface IPartnerShareRepository : IRepository<PartnerShare>
    {
        /// <summary>Bir kredit əməliyyatına aid bütün paylar (sıra ilə).</summary>
        Task<IReadOnlyList<PartnerShare>> GetByTransactionAsync(
            int transactionId,
            CancellationToken cancellationToken = default);

        /// <summary>Bütün paylar (əməliyyat tarixçəsinə bağlamaq üçün).</summary>
        Task<IReadOnlyList<PartnerShare>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// <b>BÜTÜN</b> tərəfdaş payları — satış, kredit və kredit əlavə gəlir
        /// mənbələri ilə birlikdə (avtomobil, müqavilə, müştəri adları daxil).
        /// <para>
        /// «👥 Tərəfdaşlar» tabının bölgü jurnalı bu metoddan qidalanır.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<PartnerShare>> GetAllDetailedAsync(CancellationToken cancellationToken = default);
    }
}
