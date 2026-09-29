using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    /// <summary>Xərc kataloqu (qrup / kateqoriya) cədvəlinin konfiqurasiyası.</summary>
    public class ExpenseCatalogEntryConfiguration : IEntityTypeConfiguration<ExpenseCatalogEntry>
    {
        public void Configure(EntityTypeBuilder<ExpenseCatalogEntry> builder)
        {
            builder.ToTable("XercKataloqu");
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Teyinat).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Qrup).IsRequired().HasMaxLength(150);
            builder.Property(e => e.Kategoriya).HasMaxLength(150);

            builder.HasIndex(e => new { e.Teyinat, e.Qrup, e.Kategoriya });
        }
    }
}
