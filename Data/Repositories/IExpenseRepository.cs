using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>
    /// 🚀 <b>XƏRC CƏDVƏLİ ÜÇÜN SÜZGƏC</b> ✓✓✓
    /// <para>
    /// ⚠ Bütün şərtlər <b>SQL tərəfində</b> tətbiq olunur ✓ →
    /// 1 000 000 sətir olsa da yalnız lazım olan 200 sətir oxunur ✓✓✓
    /// </para>
    /// </summary>
    /// <param name="Axtaris">Axtarış mətni (istifadəçinin yazdığı ✓).</param>
    /// <param name="Teyinat">«Avtomobil Xərci» / «Ofis / İnzibati Xərc» (boş = hamısı ✓).</param>
    /// <param name="TeyinatDeyil">Bu təyinatdan BAŞQA hamısı ✓ (məs. ofisdən başqa ✓).</param>
    /// <param name="CarId">Yalnız bir avtomobilin xərcləri (boş = hamısı ✓).</param>
    /// <param name="Kategoriya">Yalnız bir kateqoriya (məs. «Alış» ✓ — skript idxalı üçün ✓).</param>
    public sealed record ExpenseFilter(
        string? Axtaris = null,
        string? Teyinat = null,
        string? TeyinatDeyil = null,
        int? CarId = null,
        string? Kategoriya = null);

    /// <summary>🚀 Xərc cədvəlinin bir səhifəsi ✓ (yalnız yüklənən hissə ✓).</summary>
    /// <param name="Setirler">Bu səhifədəki sətirlər ✓.</param>
    /// <param name="UmumiSay">Süzgəcə uyğun ÜMUMİ sətir sayı ✓ (SQL <c>COUNT</c> ✓).</param>
    public sealed record ExpensePage(IReadOnlyList<ExpenseItem> Setirler, int UmumiSay);

    /// <summary>
    /// 🚀 Cədvəldə sıralama sahələri ✓ (SQL <c>ORDER BY</c> ilə ✓)
    /// <para>
    /// ⚠ <b>«Məbləğ» YOXDUR ✗✓✓:</b> SQLite bazasında <c>decimal</c> sütunu
    /// <b>MƏTN</b> kimi saxlanılır ✗ → mətn sıralaması YANLIŞ nəticə verir ✗
    /// («100» &lt; «20» ✗). Buna görə məbləğ sütunu sıralanmır ✗ (cədvəldə
    /// <c>CanUserSort=False</c> ✓).
    /// </para>
    /// </summary>
    public enum ExpenseSort
    {
        Tarix,
        Teyinat,
        Qrup,
        Kategoriya,
        Qeyd
    }

    public interface IExpenseRepository : IRepository<ExpenseItem>
    {
        Task<IReadOnlyList<ExpenseItem>> GetWithCarAsync(CancellationToken cancellationToken = default);

        IQueryable<ExpenseItem> Query();

        /// <summary>
        /// 🚀 <b>YALNIZ BİR SƏHİFƏNİ OXUYUR</b> ✓✓✓ (<c>LIMIT/OFFSET</c> ✓)
        /// <para>
        /// ⚠ ƏVVƏL bütün cədvəl yaddaşa yüklənirdi ✗ → 1 000 000 qeyddə
        /// proqram donurdu ✗✓✓ İNDİ yalnız görünən səhifə oxunur ✓
        /// </para>
        /// </summary>
        Task<ExpensePage> GetPageAsync(
            int skip,
            int take,
            ExpenseFilter filter,
            ExpenseSort sort = ExpenseSort.Tarix,
            bool descending = true,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 🚀 Yekun məbləğlər <b>SQL <c>SUM</c></b> ilə hesablanır ✓✓✓ —
        /// milyonlarla sətir yaddaşa yüklənmir ✗
        /// </summary>
        Task<Services.ExpenseStats> GetStatsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 🚀 «Axtaris» sahəsi boş olan köhnə qeydləri doldurur ✓
        /// (tətbiq açılışında bir dəfə — arxa fonda ✓)
        /// </summary>
        Task<int> BackfillAxtarisAsync(
            int batchSize = 5000,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
