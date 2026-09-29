using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class ExpenseCatalogService : IExpenseCatalogService
    {
        private readonly IRepository<ExpenseCatalogEntry> _catalog;

        public ExpenseCatalogService(IRepository<ExpenseCatalogEntry> catalog)
        {
            _catalog = catalog;
        }

        public async Task<IReadOnlyList<string>> GetGroupsAsync(string teyinat, CancellationToken cancellationToken = default)
        {
            var entries = await _catalog.FindAsync(e => e.Teyinat == teyinat, cancellationToken);
            return Ordered(entries).Select(e => e.Qrup).Distinct().ToList();
        }

        public async Task<IReadOnlyList<string>> GetCategoriesAsync(string qrup, CancellationToken cancellationToken = default)
        {
            var entries = await _catalog.FindAsync(e => e.Qrup == qrup && e.Kategoriya != string.Empty, cancellationToken);
            return Ordered(entries).Select(e => e.Kategoriya).Distinct().ToList();
        }

        public async Task<IReadOnlyList<string>> GetAllGroupsAsync(CancellationToken cancellationToken = default)
        {
            var entries = await _catalog.FindAsync(_ => true, cancellationToken);
            return Ordered(entries).Select(e => e.Qrup).Distinct().ToList();
        }

        public async Task<IReadOnlyList<string>> GetAllCategoriesAsync(CancellationToken cancellationToken = default)
        {
            var entries = await _catalog.FindAsync(e => e.Kategoriya != string.Empty, cancellationToken);
            return Ordered(entries).Select(e => e.Kategoriya).Distinct().ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ExpenseCatalogEntry>> GetAllEntriesAsync(CancellationToken cancellationToken = default)
        {
            var entries = await _catalog.FindAsync(e => e.Kategoriya != string.Empty, cancellationToken);
            return Ordered(entries).ToList();
        }

        public async Task AddGroupAsync(string teyinat, string qrup, CancellationToken cancellationToken = default)
        {
            var name = (qrup ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return;
            }

            var existing = await _catalog.FindAsync(e => e.Teyinat == teyinat && e.Qrup == name, cancellationToken);
            if (existing.Count > 0)
            {
                return;
            }

            await _catalog.AddAsync(new ExpenseCatalogEntry
            {
                Teyinat = teyinat,
                Qrup = name,
                Kategoriya = string.Empty,
                Sira = await NextSiraAsync(cancellationToken)
            }, cancellationToken);
            await _catalog.SaveChangesAsync(cancellationToken);
        }

        public async Task AddCategoryAsync(string teyinat, string qrup, string kategoriya, CancellationToken cancellationToken = default)
        {
            var name = (kategoriya ?? string.Empty).Trim();
            if (name.Length == 0 || string.IsNullOrWhiteSpace(qrup))
            {
                return;
            }

            var existing = await _catalog.FindAsync(e => e.Qrup == qrup && e.Kategoriya == name, cancellationToken);
            if (existing.Count > 0)
            {
                return;
            }

            await _catalog.AddAsync(new ExpenseCatalogEntry
            {
                Teyinat = teyinat,
                Qrup = qrup,
                Kategoriya = name,
                Sira = await NextSiraAsync(cancellationToken)
            }, cancellationToken);
            await _catalog.SaveChangesAsync(cancellationToken);
        }

        private static IEnumerable<ExpenseCatalogEntry> Ordered(IEnumerable<ExpenseCatalogEntry> entries)
            => entries.OrderBy(e => e.Sira).ThenBy(e => e.Id);

        public async Task DeleteGroupAsync(string teyinat, string qrup, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(qrup))
            {
                return;
            }

            var entries = await _catalog.FindAsync(e => e.Teyinat == teyinat && e.Qrup == qrup, cancellationToken);
            if (entries.Count == 0)
            {
                return;
            }

            foreach (var entry in entries)
            {
                _catalog.Remove(entry);
            }

            await _catalog.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteCategoryAsync(string qrup, string kategoriya, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(qrup) || string.IsNullOrWhiteSpace(kategoriya))
            {
                return;
            }

            var entries = await _catalog.FindAsync(e => e.Qrup == qrup && e.Kategoriya == kategoriya, cancellationToken);
            if (entries.Count == 0)
            {
                return;
            }

            foreach (var entry in entries)
            {
                _catalog.Remove(entry);
            }

            await _catalog.SaveChangesAsync(cancellationToken);
        }

        private async Task<int> NextSiraAsync(CancellationToken cancellationToken)
        {
            var all = await _catalog.FindAsync(_ => true, cancellationToken);
            return all.Count == 0 ? 1 : all.Max(e => e.Sira) + 1;
        }
    }
}
