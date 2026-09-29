using System.ComponentModel.DataAnnotations.Schema;
using System.IO;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>Faylın hansı obyektə bağlandığını göstərən növlər.</summary>
    public static class MediaRefTypes
    {
        /// <summary>Avtomobil — "Sənəd və Media Arxivi".</summary>
        public const string Car = "Avtomobil";

        /// <summary>Kredit əlavə gəlir/xərc qeydi.</summary>
        public const string CreditTransaction = "KreditEmeliyyati";
    }

    /// <summary>
    /// Yüklənmiş sənəd / media faylı (şəkil, çek, qaimə, müqavilə və s.).
    /// Fayl tətbiqin Media qovluğuna kopyalanır, bazada yalnız yolu saxlanılır.
    /// </summary>
    public class MediaAttachment
    {
        public int Id { get; set; }

        /// <summary>Bağlı olduğu obyektin növü (<see cref="MediaRefTypes"/>).</summary>
        public string RefType { get; set; } = string.Empty;

        /// <summary>Bağlı olduğu obyektin Id-si.</summary>
        public int RefId { get; set; }

        /// <summary>Orijinal fayl adı (uzantı ilə).</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Faylın diskdə saxlanılan tam yolu.</summary>
        public string StoredPath { get; set; } = string.Empty;

        /// <summary>Faylın ölçüsü (bayt).</summary>
        public long SizeBytes { get; set; }

        /// <summary>Əlavə edilmə tarixi.</summary>
        public DateTime Tarix { get; set; } = DateTime.Today;

        /// <summary>Əlavə qeyd.</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Fayl diskdə hələ də mövcuddur?</summary>
        [NotMapped]
        public bool Exists => !string.IsNullOrWhiteSpace(StoredPath) && File.Exists(StoredPath);

        /// <summary>Uzantı (nöqtəsiz, böyük hərflərlə): PDF, JPG…</summary>
        [NotMapped]
        public string Extension => Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant();

        /// <summary>Şəkil faylıdırmı?</summary>
        [NotMapped]
        public bool IsImage => Extension is "JPG" or "JPEG" or "PNG" or "BMP" or "GIF" or "WEBP";

        /// <summary>Cədvəldə göstərilən ikon.</summary>
        [NotMapped]
        public string Icon => IsImage
            ? "🖼️"
            : Extension switch
            {
                "PDF" => "📕",
                "DOC" or "DOCX" => "📘",
                "XLS" or "XLSX" or "CSV" => "📗",
                "TXT" or "RTF" => "📃",
                _ => "📄"
            };

        /// <summary>Oxunaqlı ölçü mətni: "1,2 MB".</summary>
        [NotMapped]
        public string SizeText
        {
            get
            {
                if (SizeBytes <= 0)
                {
                    return "—";
                }

                if (SizeBytes < 1024)
                {
                    return $"{SizeBytes} B";
                }

                if (SizeBytes < 1024 * 1024)
                {
                    return $"{SizeBytes / 1024.0:F1} KB";
                }

                return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }

        /// <summary>Cədvəldə göstərilən fayl mətni.</summary>
        [NotMapped]
        public string DisplayText => $"{Icon} {FileName}";
    }
}
