using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    public class SaleConfiguration : IEntityTypeConfiguration<Sale>
    {
        public void Configure(EntityTypeBuilder<Sale> builder)
        {
            builder.ToTable("Satislar");
            builder.HasKey(s => s.Id);

            builder.Property(s => s.MuqavileNomresi).HasMaxLength(60);
            builder.Property(s => s.Mustəri).IsRequired().HasMaxLength(150);
            builder.Property(s => s.OdenisUsulu).HasMaxLength(40);
            builder.Property(s => s.BarterTesviri).HasMaxLength(500);
            builder.Property(s => s.Qeyd).HasMaxLength(500);
            builder.Property(s => s.SatisQiymeti).HasPrecision(18, 2);
            builder.Property(s => s.MayaDeyeri).HasPrecision(18, 2);
            builder.Property(s => s.BarterMebleg).HasPrecision(18, 2);

            builder.HasOne(s => s.Car)
                   .WithMany()
                   .HasForeignKey(s => s.CarId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(s => s.SatisTarixi);
        }
    }
}
