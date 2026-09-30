using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    /// <summary>
    /// 💵 <b>Əl ilə yazılan kassa hərəkətləri</b> cədvəlinin konfiqurasiyası ✓
    /// <para>
    /// Kassa jurnalının qalan hissəsi avtomatik hesablanır — bax
    /// <see cref="Services.KassaHesabi"/> ✓✓✓
    /// </para>
    /// </summary>
    public class KassaHereketConfiguration : IEntityTypeConfiguration<KassaHereket>
    {
        public void Configure(EntityTypeBuilder<KassaHereket> builder)
        {
            builder.ToTable("KassaHereketleri");
            builder.HasKey(k => k.Id);

            builder.Property(k => k.Nov).IsRequired().HasMaxLength(20);
            builder.Property(k => k.Kateqoriya).IsRequired().HasMaxLength(60);
            builder.Property(k => k.Mebleg).HasPrecision(18, 2);
            builder.Property(k => k.OdenisUsulu).HasMaxLength(40);
            builder.Property(k => k.Qeyd).HasMaxLength(300);

            // 🚀 Performans indeksləri ✓
            builder.HasIndex(k => k.Tarix);
            builder.HasIndex(k => k.Nov);
        }
    }
}
