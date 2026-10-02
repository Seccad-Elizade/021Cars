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

        /// <summary>
        /// ⚡ <b>VERİLƏNLƏR DƏYİŞDİ HADİSƏSİ</b> ✓✓✓ (v6.2.16)
        /// <para>
        /// Hər <c>SaveChanges</c>-də <b>həqiqi</b> dəyişiklik varsa tətiklənir ✓ →
        /// <c>BuludKopru</c> dərhal sinxron dövrü keçirir ✓ → bir kompüterdə
        /// əlavə olunan maşın digərində <b>DƏRHAL</b> görünür ✓✓✓
        /// (5 saniyə gözləmir ✗)
        /// </para>
        /// <para>🛡️ Abunəçi olmasa heç nə olmur ✗ (tam təhlükəsizdir ✓)</para>
        /// </summary>
        public static event Action? VerilənlərDəyişdi;

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

        /// <summary>⏳ Möhlətə verilmiş ödənişlər (ilkin ödəniş ✓ nisyə satış ✓).</summary>
        public DbSet<OdenisMohlet> OdenisMohletler => Set<OdenisMohlet>();

        /// <summary>💵 Əl ilə yazılan kassa hərəkətləri.</summary>
        public DbSet<KassaHereket> KassaHereketleri => Set<KassaHereket>();

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
            modelBuilder.ApplyConfiguration(new OdenisMohletConfiguration());
            modelBuilder.ApplyConfiguration(new KassaHereketConfiguration());
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
        /// <para>
        /// ⚠ Həm də hər yazmadan ƏVVƏL <b>yeni qeydlərə bulud açarı</b> verilir ✓
        /// (<see cref="BuludIdleriniAta"/> ✓)
        /// </para>
        /// </summary>
        private void AxtarisMetnleriniYenile()
        {
            BuludIdleriniAta();

            // ================================================================
            //  ⚡ v6.2.16 — BULUD KÖRPÜSÜNÜ DƏRHAL OYADIR ✓✓✓
            //  ----------------------------------------------------------------
            //  ⚠ İstifadəçi tələbi: «bir kompüterdə əlavə edəndə o birində
            //    DƏRHAL görünsün» ✓✓✓ — 5 saniyə gözləmədən ✓
            //  ⚠ Yalnız HƏQİQİ dəyişiklik varsa ✓ (boş yazmada tətiyə basılmır ✗)
            //  🛡️ Hadisə UDULUR ✗ — abunəçi olmasa da proqram çökmür ✗✓✓
            // ================================================================
            foreach (var giriş in ChangeTracker.Entries())
            {
                if (giriş.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                {
                    try { VerilənlərDəyişdi?.Invoke(); }
                    catch { /* 🛡️ sinxron xətası yazmanı DAYANDIRMIR ✗✓✓ */ }

                    break;
                }
            }

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

        /// <summary>
        /// 🔑 <b>YENİ QEYDLƏRƏ QLOBAL UNİKAL BULUD AÇARI VERİR</b> ✓✓✓ (v6.2.16)
        /// <para>
        /// <b>⚠ PROBLEM (istifadəçi şikayəti ✓):</b> hər kompüter ÖZ rəqəm ID-sini
        /// ayrıca verirdi ✗ → iki kompüterdə <b>eyni rəqəm</b> yaranırdı ✗
        /// (məs. hər ikisində «232» ✗) → bir kompüterdə əlavə olunan maşın
        /// digərində <b>GÖRÜNMÜRDÜ</b> ✗ / əvvəlki qeydin <b>üzərinə yazılırdı</b> ✗
        /// </para>
        /// <para>
        /// <b>✅ HƏLL:</b> hər YENİ qeydə <see cref="Guid"/> açarı verilir ✓ →
        /// iki kompüterdə <b>heç vaxt toqquşmur</b> ✗✓✓
        /// </para>
        /// <para>
        /// ⚠ <b>KÖHNƏ</b> qeydlərə TOXUNULMUR ✗ — onların açarı köhnə rəqəm ID
        /// olaraq qalır ✓ (köhnə məlumat DƏYİŞMİR ✗ · təkrar qeyd YARANMIR ✗✓✓)
        /// </para>
        /// <para>⚠ Yalnız <c>bulud_izleme</c> kimi kənar cədvəllər istisnadır ✗</para>
        /// </summary>
        private void BuludIdleriniAta()
        {
            foreach (var giriş in ChangeTracker.Entries())
            {
                // Yalnız YENİ əlavə olunanlar ✓ (köhnələrə HEÇ VAXT toxunulmur ✗✓✓)
                if (giriş.State != EntityState.Added)
                {
                    continue;
                }

                if (giriş.Entity is IBuludIdli kok && string.IsNullOrWhiteSpace(kok.BuludId))
                {
                    kok.BuludId = Guid.NewGuid().ToString("N");
                }
            }
        }
    }
}
