using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseAeroStudio.Data.Configurations
{
    /// <summary>Sənəd / media faylları cədvəlinin konfiqurasiyası.</summary>
    public class MediaAttachmentConfiguration : IEntityTypeConfiguration<MediaAttachment>
    {
        public void Configure(EntityTypeBuilder<MediaAttachment> builder)
        {
            builder.ToTable("Senedler");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.RefType).IsRequired().HasMaxLength(50);
            builder.Property(a => a.FileName).IsRequired().HasMaxLength(300);
            builder.Property(a => a.StoredPath).IsRequired().HasMaxLength(500);
            builder.Property(a => a.Qeyd).HasMaxLength(300);

            builder.HasIndex(a => new { a.RefType, a.RefId });
        }
    }
}
