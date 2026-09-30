using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    /// <summary>
    /// ⏳ <b>Möhlətə verilmiş ödənişlər</b> cədvəlinin konfiqurasiyası ✓✓✓
    /// (kreditin ilkin ödənişi ✓ + nisyə satış ✓)
    /// </summary>
    public class OdenisMohletConfiguration : IEntityTypeConfiguration<OdenisMohlet>
    {
        public void Configure(EntityTypeBuilder<OdenisMohlet> builder)
        {
            builder.ToTable("OdenisMohletleri");
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Menbe).IsRequired().HasMaxLength(40);
            builder.Property(m => m.Mebleg).HasPrecision(18, 2);
            builder.Property(m => m.OdenisUsulu).HasMaxLength(40);
            builder.Property(m => m.Qeyd).HasMaxLength(300);

            // Kredit silinəndə ona bağlı möhlətlər də silinir ✓
            builder.HasOne(m => m.Credit)
                   .WithMany(c => c.IlkinMohletleri)
                   .HasForeignKey(m => m.CreditId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Satış silinəndə ona bağlı möhlətlər də silinir ✓
            builder.HasOne(m => m.Sale)
                   .WithMany(s => s.Mohletler)
                   .HasForeignKey(m => m.SaleId)
                   .OnDelete(DeleteBehavior.Cascade);

            // 🚀 Performans indeksləri (milyon sətirdə filtr donmur ✓)
            builder.HasIndex(m => m.CreditId);
            builder.HasIndex(m => m.SaleId);
            builder.HasIndex(m => m.Tarix);
            builder.HasIndex(m => m.Odenilib);
        }
    }
}
