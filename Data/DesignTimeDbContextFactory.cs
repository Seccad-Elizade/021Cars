using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnterpriseAeroStudio.Data
{
    /// <summary>
    /// `dotnet ef` alətləri üçün dizayn zamanı DbContext yaradıcısı.
    /// </summary>
    public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var dataDirectory = Path.Combine(
                Cas0201.Kok.Qovluq,
                "EnterpriseAeroStudio");
            Directory.CreateDirectory(dataDirectory);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={Path.Combine(dataDirectory, "avtopark.db")}")
                .Options;

            return new AppDbContext(options);
        }
    }
}
