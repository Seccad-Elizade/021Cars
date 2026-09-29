using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    /// <summary>Tərəfdaşlar cədvəlinin konfiqurasiyası.</summary>
    public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
    {
        public void Configure(EntityTypeBuilder<Partner> builder)
        {
            builder.ToTable("Terefdaslar");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Ad).IsRequired().HasMaxLength(60);
            builder.Property(p => p.Faiz).HasPrecision(9, 4);
            builder.Property(p => p.Qeyd).HasMaxLength(300);
            builder.Property(p => p.Aktiv).HasDefaultValue(true);

            // Eyni adlı tərəfdaş iki dəfə olmasın.
            builder.HasIndex(p => p.Ad).IsUnique();
        }
    }
}
