using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Xərc qrupları və kateqoriyaları üzrə əməliyyatlar.
    /// Standart siyahı bazaya köçürülür, istifadəçi isə yenisini əlavə edə bilər.
    /// </summary>
    public interface IExpenseCatalogService
    {
        /// <summary>Verilmiş təyinat üzrə qruplar.</summary>
        Task<IReadOnlyList<string>> GetGroupsAsync(string teyinat, CancellationToken cancellationToken = default);

        /// <summary>Verilmiş qrup üzrə kateqoriyalar.</summary>
        Task<IReadOnlyList<string>> GetCategoriesAsync(string qrup, CancellationToken cancellationToken = default);

        /// <summary>Bütün qruplar (cədvəl daxilində redaktə üçün).</summary>
        Task<IReadOnlyList<string>> GetAllGroupsAsync(CancellationToken cancellationToken = default);

        /// <summary>Bütün kateqoriyalar (cədvəl daxilində redaktə üçün).</summary>
        Task<IReadOnlyList<string>> GetAllCategoriesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Bütün kateqoriyalar — <b>TƏYİNAT və QRUP məlumatı ilə birlikdə</b>.
        /// <para>
        /// «Sürətli axtarış» üçün: istifadəçi xərc adını yazır, tətbiq həm
        /// qrupu, həm kateqoriyanı avtomatik seçir.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<ExpenseCatalogEntry>> GetAllEntriesAsync(CancellationToken cancellationToken = default);

        /// <summary>Yeni qrup əlavə edir (artıq varsa heç nə etmir).</summary>
        Task AddGroupAsync(string teyinat, string qrup, CancellationToken cancellationToken = default);

        /// <summary>Yeni kateqoriya əlavə edir (artıq varsa heç nə etmir).</summary>
        Task AddCategoryAsync(string teyinat, string qrup, string kategoriya, CancellationToken cancellationToken = default);

        /// <summary>Qrupu və ona aid bütün kateqoriyaları silir.</summary>
        Task DeleteGroupAsync(string teyinat, string qrup, CancellationToken cancellationToken = default);

        /// <summary>Verilmiş qrupdan kateqoriyanı silir.</summary>
        Task DeleteCategoryAsync(string qrup, string kategoriya, CancellationToken cancellationToken = default);

        // ====================================================================
        //  📜 SKRİPT İDXALI QRUPU
        // ====================================================================

        /// <summary>
        /// 📜 <b>«Skript İdxalı» qrupunu təmin edir</b> ✓ və skript idxalı ilə
        /// <b>yaranmış BÜTÜN kateqoriyaları ora köçürür</b> ✓✓✓
        /// <para>
        /// Proqramın standart siyahısında (<see cref="Catalog"/>) OLMAYAN
        /// avtomobil kateqoriyaları sonradan yaranıb ✓ → idxal onları
        /// standart qrupların içinə atırdı ✗✓✓
        /// </para>
        /// <para>
        /// ⚠ <b>İDEMPOTENTDİR</b> ✓ — istənilən qədər çağırıla bilər ✓
        /// (artıq köçürülmüşlərə toxunmur ✗).
        /// </para>
        /// </summary>
        Task<KocurmeNeticesi> MoveImportedCategoriesToGroupAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 📜 Skript idxalı kateqoriyalarının köçürülmə nəticəsi ✓
    /// <param name="KateqoriyaSayi">Qrupa köçürülən kateqoriya sayı ✓</param>
    /// <param name="XercSayi">Qrupu yenilənən xərc sətri sayı ✓</param>
    /// </summary>
    public sealed record KocurmeNeticesi(int KateqoriyaSayi, int XercSayi);
}
