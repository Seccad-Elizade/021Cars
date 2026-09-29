using EnterpriseAeroStudio.Data.Configurations;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace EnterpriseAeroStudio.Data
{
    /// <summary>
    /// Tətbiqin əsas EF Core (SQLite) verilənlər bazası konteksti.
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // ================================================================
            //  ⚠ DİQQƏT: `ChangeTracker.AutoDetectChangesEnabled = false` QOYULMUR ✗✓✓
            // ----------------------------------------------------------------
            //  Sınaqda təsbit olundu ✗: EF Core 8-də bu bayraq söndürüldükdə
            //  `SaveChanges` dəyişiklikləri GÖRMÜR ✗ → redaktə (məs. xərc
            //  məbləğinin dəyişdirilməsi ✓) BAZAYA YAZILMIR ✗✗ (sükutla itir ✗)
            //
            //  ✅ Sürət onsuz da təmin olunub ✓ — bütün oxumalar
            //     `AsNoTracking()` ilədir ✓ (izlənən obyekt sayı azdır ✓)
            // ================================================================
        }

        public DbSet<CarItem> Cars => Set<CarItem>();
        public DbSet<ExpenseItem> Expenses => Set<ExpenseItem>();
        public DbSet<Credit> Credits => Set<Credit>();
        public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<ExpenseCatalogEntry> ExpenseCatalog => Set<ExpenseCatalogEntry>();
        public DbSet<MediaAttachment> Attachments => Set<MediaAttachment>();
        public DbSet<PartnerShare> PartnerShares => Set<PartnerShare>();
        public DbSet<Partner> Partners => Set<Partner>();
        public DbSet<PartnerPayment> PartnerPayments => Set<PartnerPayment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new CarItemConfiguration());
            modelBuilder.ApplyConfiguration(new ExpenseItemConfiguration());
            modelBuilder.ApplyConfiguration(new CreditConfiguration());
            modelBuilder.ApplyConfiguration(new CreditTransactionConfiguration());
            modelBuilder.ApplyConfiguration(new SaleConfiguration());
            modelBuilder.ApplyConfiguration(new ExpenseCatalogEntryConfiguration());
            modelBuilder.ApplyConfiguration(new MediaAttachmentConfiguration());
            modelBuilder.ApplyConfiguration(new PartnerShareConfiguration());
            modelBuilder.ApplyConfiguration(new PartnerConfiguration());
            modelBuilder.ApplyConfiguration(new PartnerPaymentConfiguration());
        }

        // ====================================================================
        //  🚀 PERFORMANS (MİLYON SƏTİR) ✓✓✓
        // --------------------------------------------------------------------
        //  ① Hər yazmadan ƏVVƏL xərclərin «Axtaris» (axtarış mətni) sahəsi
        //     avtomatik yenilənir ✓ → SQL-tərəfli axtarış həmişə DÜZGÜN
        //     işləyir ✓ (əl ilə doldurmağa ehtiyac yoxdur ✗✓✓)
        //
        //  ② `ChangeTracker.AutoDetectChangesEnabled` SÖNDÜRÜLÜR ✗✓✓ —
        //     minlərlə entity izlənərkən EF-in hər `SaveChanges`-də bütün
        //     obyektləri skan etməsi proqramı DONDURURDU ✗
        // ====================================================================
        public override int SaveChanges()
        {
            AxtarisMetnleriniYenile();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AxtarisMetnleriniYenile();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            AxtarisMetnleriniYenile();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(
            bool acceptAllChangesOnSuccess,
            CancellationToken cancellationToken = default)
        {
            AxtarisMetnleriniYenile();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        /// <summary>
        /// Əlavə olunan / düzəldilən hər xərc qeydinin <c>Axtaris</c> sahəsini
        /// yenidən hesablayır ✓ (axtarış SQL-də sürətli getsin ✓✓✓)
        /// </summary>
        private void AxtarisMetnleriniYenile()
        {
            List<EntityEntry<ExpenseItem>>? dəyişənlər = null;

            foreach (var giriş in ChangeTracker.Entries<ExpenseItem>())
            {
                if (giriş.State is not (EntityState.Added or EntityState.Modified))
                {
                    continue;
                }

                (dəyişənlər ??= new List<EntityEntry<ExpenseItem>>()).Add(giriş);
            }

            if (dəyişənlər is null)
            {
                return;
            }

            foreach (var giriş in dəyişənlər)
            {
                giriş.Entity.Axtaris = MetinAxtaris.Birlestir(
                    giriş.Entity.Teyinat,
                    giriş.Entity.Qrup,
                    giriş.Entity.Kategoriya,
                    giriş.Entity.OdenisUsulu,
                    giriş.Entity.Qeyd);
            }
        }
    }
}
