using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class ExpenseCatalogService : IExpenseCatalogService
    {
        private readonly IRepository<ExpenseCatalogEntry> _catalog;
        private readonly IRepository<ExpenseItem> _xercler;

        public ExpenseCatalogService(
            IRepository<ExpenseCatalogEntry> catalog,
            IRepository<ExpenseItem> xercler)
        {
            _catalog = catalog;
            _xercler = xercler;
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

        // ====================================================================
        //  📜 SKRİPT İDXALI QRUPU  (avtomatik qrup ✓✓✓)
        // --------------------------------------------------------------------
        //  ① «📜 Skript İdxalı» qrupu YOXDURSA yaradılır ✓
        //  ② Proqramın standart siyahısında olmayan (yəni idxalda yaranan)
        //     avtomobil kateqoriyaları ora köçürülür ✓
        //  ③ Həmin kateqoriyaları daşıyan KÖHNƏ XƏRC sətirlərinin qrupu da
        //     yenilənir ✓ → kataloq ilə xərclər HƏMİŞƏ üst-üstə düşür ✓✓✓
        // ====================================================================

        /// <inheritdoc />
        public async Task<KocurmeNeticesi> MoveImportedCategoriesToGroupAsync(
            CancellationToken cancellationToken = default)
        {
            var teyinat = Catalog.CarDestination;
            var yeniQrup = Catalog.ImportedExpenseGroup;

            // ① Qrup mütləq mövcud olmalıdır ✓ (artıq varsa heç nə etmir ✓)
            await AddGroupAsync(teyinat, yeniQrup, cancellationToken);

            var entries = await _catalog.FindAsync(e => e.Teyinat == teyinat, cancellationToken);

            var kocurulecek = entries
                .Where(e => e.Kategoriya != string.Empty)
                .Where(e => e.Qrup != yeniQrup)
                .Where(e => !Catalog.IsStandardCategory(e.Qrup, e.Kategoriya))
                .ToList();

            if (kocurulecek.Count == 0)
            {
                return new KocurmeNeticesi(0, 0);
            }

            foreach (var entry in kocurulecek)
            {
                entry.Qrup = yeniQrup;

                if (entry.Sira <= 0)
                {
                    entry.Sira = await NextSiraAsync(cancellationToken);
                }

                _catalog.Update(entry);
            }

            await _catalog.SaveChangesAsync(cancellationToken);

            // ③ Köhnə xərc sətirlərinin qrupu ✓
            var xercSayi = await XercQruplariniYenileAsync(kocurulecek, teyinat, yeniQrup, cancellationToken);

            return new KocurmeNeticesi(kocurulecek.Count, xercSayi);
        }

        /// <summary>
        /// Köçürülən kateqoriyaları daşıyan xərc sətirlərinin <c>Qrup</c> sahəsini
        /// yeniləyir ✓ (kataloq ilə xərclər uyğun qalsın ✓).
        /// </summary>
        private async Task<int> XercQruplariniYenileAsync(
            IReadOnlyList<ExpenseCatalogEntry> kocurulecek,
            string teyinat,
            string yeniQrup,
            CancellationToken cancellationToken)
        {
            var xercler = await _xercler.FindAsync(e => e.Teyinat == teyinat, cancellationToken);

            if (xercler.Count == 0)
            {
                return 0;
            }

            // Axtarış açarı: «ə/ş/ç/ğ/ı» fərqinə həssas olmayan kateqoriya adı ✓
            var hedefler = new HashSet<string>(
                kocurulecek.Select(e => MetinUygunlasdirici.Normallasdir(e.Kategoriya)),
                StringComparer.Ordinal);

            var say = 0;

            foreach (var xerc in xercler)
            {
                if (xerc.Qrup == yeniQrup)
                {
                    continue;
                }

                if (!hedefler.Contains(MetinUygunlasdirici.Normallasdir(xerc.Kategoriya)))
                {
                    continue;
                }

                xerc.Qrup = yeniQrup;
                _xercler.Update(xerc);
                say++;
            }

            if (say > 0)
            {
                await _xercler.SaveChangesAsync(cancellationToken);
            }

            return say;
        }

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
