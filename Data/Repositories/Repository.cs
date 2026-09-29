using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data.Repositories
{
    /// <summary>
    /// EF Core üzərində qurulmuş ümumi repository tətbiqi.
    /// </summary>
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        protected readonly AppDbContext Context;
        protected readonly DbSet<TEntity> Set;

        public Repository(AppDbContext context)
        {
            Context = context;
            Set = context.Set<TEntity>();
        }

        public virtual async Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => await Set.FindAsync(new object?[] { id }, cancellationToken);

        public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().ToListAsync(cancellationToken);

        public virtual async Task<IReadOnlyList<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
            => await Set.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);

        public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
            => await Set.AddAsync(entity, cancellationToken);

        public virtual void Update(TEntity entity) => Set.Update(entity);

        public virtual void Remove(TEntity entity) => Set.Remove(entity);

        public virtual async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => await Context.SaveChangesAsync(cancellationToken);

        /// <inheritdoc />
        public virtual async Task<int> DeleteWhereAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
            => await Set.Where(predicate).ExecuteDeleteAsync(cancellationToken);

        /// <inheritdoc />
        public virtual void ClearTracker() => Context.ChangeTracker.Clear();
    }
}
