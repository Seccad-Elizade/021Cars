using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    public class CreditConfiguration : IEntityTypeConfiguration<Credit>
    {
        public void Configure(EntityTypeBuilder<Credit> builder)
        {
            builder.ToTable("Kreditler");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.MuqavileNomresi).HasMaxLength(60);
            builder.Property(c => c.Mustəri).IsRequired().HasMaxLength(150);
            builder.Property(c => c.Status).HasMaxLength(40);
            builder.Property(c => c.Qeyd).HasMaxLength(500);
            builder.Property(c => c.Mebleg).HasPrecision(18, 2);
            builder.Property(c => c.IlkinOdenis).HasPrecision(18, 2);
            builder.Property(c => c.FaizDerecesi).HasPrecision(9, 2);
            builder.Property(c => c.AylıqOdenis).HasPrecision(18, 2);

            builder.HasOne(c => c.Car)
                   .WithMany(c => c.Credits)
                   .HasForeignKey(c => c.CarId)
                   .OnDelete(DeleteBehavior.SetNull);

            // 🚀 PERFORMANS indeksləri ✓ (böyük bazada süzgəc/səhifələmə sürətli ✓)
            builder.HasIndex(c => c.CarId);
            builder.HasIndex(c => c.Status);
        }
    }
}
