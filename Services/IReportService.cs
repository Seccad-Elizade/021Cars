using System.IO;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Peşəkar MALİYYƏ HESABATI («Açot») ixracı — PDF, Excel və ya HTML.
    /// </summary>
    public interface IReportService
    {
        /// <summary>Hesabat HTML-i qurur (brauzerdə açmaq / PDF üçün).</summary>
        string BuildHtml(ReportBuilder.ReportInput input);

        /// <summary>Excel (CSV) hesabatını fayla yazır.</summary>
        Task ExportExcelAsync(ReportBuilder.ReportInput input, string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// PDF hesabatını fayla yazır. PDF çeviricisi («WebView2») təyin
        /// olunmayıbsa HTML faylı yazılır və brauzer açılır — istifadəçi
        /// <b>Ctrl+P → PDF kimi saxla</b> edə bilir.
        /// </summary>
        Task<string> ExportPdfAsync(ReportBuilder.ReportInput input, string filePath, CancellationToken cancellationToken = default);

        /// <summary>HTML hesabatı fayla yazır və standart brauzerdə açır.</summary>
        Task ExportHtmlAsync(ReportBuilder.ReportInput input, string filePath, CancellationToken cancellationToken = default);
    }

    /// <inheritdoc />
    public sealed class ReportService : IReportService
    {
        /// <summary>
        /// HTML → PDF çeviricisi (WPF tətbiqi tərəfindən WebView2 ilə təyin olunur).
        /// <c>null</c> olduqda HTML-ə düşülür.
        /// </summary>
        public static Func<string, string, Task>? PdfRenderer { get; set; }

        public string BuildHtml(ReportBuilder.ReportInput input) => ReportBuilder.BuildHtml(input);

        /// <summary>
        /// Hazır HTML mətnini PDF kimi yazır. PDF çeviricisi («WebView2») yoxdursa
        /// və ya xəta verərsə — HTML fayl kimi yazılır və yolu qaytarılır.
        /// </summary>
        public static async Task<string> WritePdfAsync(string html, string filePath)
        {
            if (PdfRenderer is not null)
            {
                try
                {
                    await PdfRenderer(html, filePath);
                    return filePath;
                }
                catch
                {
                    // PDF alınmadı → HTML-ə düşürük.
                }
            }

            var htmlPath = Path.ChangeExtension(filePath, ".html");
            await File.WriteAllTextAsync(htmlPath, html, new System.Text.UTF8Encoding(false));
            OpenInBrowser(htmlPath);
            return htmlPath;
        }

        public async Task ExportExcelAsync(
            ReportBuilder.ReportInput input,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            // BOM — Excel Azərbaycan hərflərini düzgün göstərsin.
            var csv = ReportBuilder.BuildCsv(input);
            await File.WriteAllTextAsync(filePath, csv, new System.Text.UTF8Encoding(true), cancellationToken);
        }

        public async Task<string> ExportPdfAsync(
            ReportBuilder.ReportInput input,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var html = ReportBuilder.BuildHtml(input);

            if (PdfRenderer is not null)
            {
                try
                {
                    await PdfRenderer(html, filePath);
                    return filePath;
                }
                catch
                {
                    // PDF alınmasa HTML-ə düşürük (istifadəçi çap edə bilər).
                }
            }

            var htmlPath = Path.ChangeExtension(filePath, ".html");
            await File.WriteAllTextAsync(htmlPath, html, new System.Text.UTF8Encoding(false), cancellationToken);
            OpenInBrowser(htmlPath);
            return htmlPath;
        }

        public async Task ExportHtmlAsync(
            ReportBuilder.ReportInput input,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            var html = ReportBuilder.BuildHtml(input);
            await File.WriteAllTextAsync(filePath, html, new System.Text.UTF8Encoding(false), cancellationToken);
            OpenInBrowser(filePath);
        }

        private static void OpenInBrowser(string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Brauzer açıla bilməsə fayl yenə hazırdır.
            }
        }
    }
}
