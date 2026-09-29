using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    /// <summary>Tərəfdaş ödənişləri (xaric edilən pullar) cədvəlinin konfiqurasiyası.</summary>
    public class PartnerPaymentConfiguration : IEntityTypeConfiguration<PartnerPayment>
    {
        public void Configure(EntityTypeBuilder<PartnerPayment> builder)
        {
            builder.ToTable("TerefdasOdenisleri");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Terefdas).IsRequired().HasMaxLength(60);
            builder.Property(p => p.Mebleg).HasPrecision(18, 2);
            builder.Property(p => p.OdenisUsulu).HasMaxLength(40);
            builder.Property(p => p.Qeyd).HasMaxLength(300);

            builder.HasIndex(p => p.Tarix);
            builder.HasIndex(p => p.Terefdas);
        }
    }
}
