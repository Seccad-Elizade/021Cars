using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    public class ExpenseItemConfiguration : IEntityTypeConfiguration<ExpenseItem>
    {
        public void Configure(EntityTypeBuilder<ExpenseItem> builder)
        {
            builder.ToTable("Xercler");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Teyinat).IsRequired().HasMaxLength(80);
            builder.Property(e => e.Qrup).HasMaxLength(120);
            builder.Property(e => e.Kategoriya).IsRequired().HasMaxLength(150);
            builder.Property(e => e.OdenisUsulu).HasMaxLength(40);
            builder.Property(e => e.Qeyd).HasMaxLength(500);
            builder.Property(e => e.Mebleg).HasPrecision(18, 2);

            // 🚀 SQL-tərəfli axtarış mətni ✓ (AppDbContext avtomatik doldurur ✓)
            builder.Property(e => e.Axtaris).HasMaxLength(900);

            builder.HasOne(e => e.Car)
                   .WithMany(c => c.Expenses)
                   .HasForeignKey(e => e.CarId)
                   .OnDelete(DeleteBehavior.SetNull);

            // ================================================================
            //  🚀 İNDEKSLƏR (MİLYON SƏTİR) ✓✓✓
            // ----------------------------------------------------------------
            //  Cədvəl 10 000 000 sətrə çatsa da səhifələmə (paging) və
            //  süzgəclər indekslə işləyir → proqram DONMUR ✓✓✓
            // ================================================================
            builder.HasIndex(e => e.Tarix);

            // 🔑 Səhifələmə sırası: `ORDER BY Tarix DESC, Id DESC` → indekslə ✓
            builder.HasIndex(e => new { e.Tarix, e.Id });

            builder.HasIndex(e => e.Teyinat);
            builder.HasIndex(e => e.Qrup);
            builder.HasIndex(e => e.Kategoriya);
            builder.HasIndex(e => e.CarId);
            builder.HasIndex(e => e.Axtaris);
        }
    }
}
