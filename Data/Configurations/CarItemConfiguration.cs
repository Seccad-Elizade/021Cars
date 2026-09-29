using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    public class CarItemConfiguration : IEntityTypeConfiguration<CarItem>
    {
        public void Configure(EntityTypeBuilder<CarItem> builder)
        {
            builder.ToTable("Avtomobiller");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Marka).IsRequired().HasMaxLength(150);
            builder.Property(c => c.QeydiyyatNisani).HasMaxLength(20);
            builder.Property(c => c.Vin).HasMaxLength(20);
            builder.Property(c => c.Yanacaq).HasMaxLength(40);
            builder.Property(c => c.Status).HasMaxLength(40);
            builder.Property(c => c.KreditNomresi).HasMaxLength(20);
            builder.Property(c => c.AlisSaati).HasMaxLength(10);
            builder.Property(c => c.AlisUsulu).HasMaxLength(20);
            builder.Property(c => c.BarterTesviri).HasMaxLength(300);
            builder.Property(c => c.AlisQiymeti).HasPrecision(18, 2);
            builder.Property(c => c.SatisQiymeti).HasPrecision(18, 2);
            builder.Property(c => c.BarterDeyeri).HasPrecision(18, 2);

            builder.HasIndex(c => c.QeydiyyatNisani);
        }
    }
}
