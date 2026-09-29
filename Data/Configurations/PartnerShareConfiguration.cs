using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    /// <summary>Tərəfdaş payları cədvəlinin konfiqurasiyası.</summary>
    public class PartnerShareConfiguration : IEntityTypeConfiguration<PartnerShare>
    {
        public void Configure(EntityTypeBuilder<PartnerShare> builder)
        {
            builder.ToTable("TerefdasPaylari");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Terefdas).IsRequired().HasMaxLength(60);
            builder.Property(p => p.Faiz).HasPrecision(9, 4);
            builder.Property(p => p.Mebleg).HasPrecision(18, 2);
            builder.Property(p => p.Aktiv).HasDefaultValue(true);

            // Əməliyyat silinəndə onun tərəfdaş payları da silinir.
            builder.HasOne(p => p.CreditTransaction)
                   .WithMany(t => t.TerefdasPaylari)
                   .HasForeignKey(p => p.CreditTransactionId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Kredit müqaviləsi silinəndə ona bağlı paylar da silinir.
            builder.HasOne(p => p.Credit)
                   .WithMany()
                   .HasForeignKey(p => p.CreditId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Satış silinəndə ona bağlı paylar da silinir.
            builder.HasOne(p => p.Sale)
                   .WithMany()
                   .HasForeignKey(p => p.SaleId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(p => p.CreditTransactionId);
            builder.HasIndex(p => p.CreditId);
            builder.HasIndex(p => p.SaleId);
        }
    }
}
