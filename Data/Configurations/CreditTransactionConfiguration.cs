using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    public class CreditTransactionConfiguration : IEntityTypeConfiguration<CreditTransaction>
    {
        public void Configure(EntityTypeBuilder<CreditTransaction> builder)
        {
            builder.ToTable("KreditEmeliyyatlari");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Nov).IsRequired().HasMaxLength(20);
            builder.Property(t => t.Tesvir).HasMaxLength(300);
            builder.Property(t => t.Mebleg).HasPrecision(18, 2);

            builder.HasOne(t => t.Credit)
                   .WithMany()
                   .HasForeignKey(t => t.CreditId)
                   .OnDelete(DeleteBehavior.Cascade);

            // ================================================================
            //  🚀 PERFORMANS İNDEKSLƏRİ ✓✓✓
            // ----------------------------------------------------------------
            //  «Kredit Əməliyyatları» tabında hər süzgəc (kreditə görə ·
            //  tarixə görə ✓) indekslə işləyir → milyonlarla əməliyyatda
            //  da proqram DONMUR ✓✓✓
            // ================================================================
            builder.HasIndex(t => t.CreditId);
            builder.HasIndex(t => t.Tarix);
            builder.HasIndex(t => new { t.CreditId, t.Tarix });
            builder.HasIndex(t => t.Nov);
        }
    }
}
