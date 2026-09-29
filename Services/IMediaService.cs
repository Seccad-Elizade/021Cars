using System.Diagnostics;
using System.IO;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>Sənəd / media (şəkil, çek, qaimə) fayllarının saxlanması və idarəsi.</summary>
    public interface IMediaService
    {
        /// <summary>Verilmiş obyektə bağlı fayllar (ən yenisi əvvəldə).</summary>
        Task<IReadOnlyList<MediaAttachment>> GetAsync(string refType, int refId, CancellationToken cancellationToken = default);

        /// <summary>Seçilmiş faylları tətbiqin Media qovluğuna kopyalayır və bazaya yazır.</summary>
        Task<int> AddAsync(string refType, int refId, IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken = default);

        /// <summary>Faylı həm diskdən, həm də bazadan silir.</summary>
        Task DeleteAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Faylı əməliyyat sistemi vasitəsilə açır (uğurlu olarsa true).</summary>
        bool OpenWithShell(MediaAttachment attachment);

        /// <summary>Verilmiş obyektin fayllarının saxlandığı qovluğu açır (uğurlu olarsa true).</summary>
        bool OpenFolder(string refType, int refId);

        /// <summary>Verilmiş obyektin fayl qovluğunun tam yolu.</summary>
        string GetFolderPath(string refType, int refId);

        /// <summary>Hər obyekt üzrə fayl sayı (RefId → say).</summary>
        Task<Dictionary<int, int>> GetCountsAsync(string refType, CancellationToken cancellationToken = default);

        /// <summary>Fayl seçmə dialoqu üçün hazır filtr.</summary>
        string FileFilter { get; }
    }
}
