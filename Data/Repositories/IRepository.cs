using System.Linq.Expressions;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>
    /// Bütün müəssisələr üçün ümumi (generic) repository müqaviləsi.
    /// </summary>
    public interface IRepository<TEntity> where TEntity : class
    {
        Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

        void Update(TEntity entity);

        void Remove(TEntity entity);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Şərtə uyğun sətirləri <b>BİRBAŞA BAZADAN</b> silir — change tracker
        /// tamamilə kənarda qalır.
        /// <para>
        /// <b>Nə üçün lazımdır:</b> <c>AppDbContext</c> transient, ViewModel-lər isə
        /// singleton olduğu üçün kontekst <b>tətbiqin ömrü boyu</b> yaşayır və
        /// içində köhnə (stale) nüsxələr qalır. Belə nüsxə artıq bazada olmadıqda
        /// EF yenə <c>DELETE</c> göndərir və
        /// <c>DbUpdateConcurrencyException: expected to affect 1 row(s), but actually affected 0</c>
        /// xətası yaranır. Bu metod həmin problemi tamamilə aradan qaldırır.
        /// </para>
        /// </summary>
        /// <returns>Faktiki silinən sətir sayı.</returns>
        Task<int> DeleteWhereAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Kontekstin izlədiyi BÜTÜN entity-ləri buraxır (detach).
        /// Ağır silmə/yenidən-yazma əməliyyatlarından əvvəl çağırılır.
        /// </summary>
        void ClearTracker();
    }
}
