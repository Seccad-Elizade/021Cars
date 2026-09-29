using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace EnterpriseAeroStudio.Hosting
{
    /// <summary>
    /// HTML sənədini <b>gizli WebView2</b> vasitəsilə həqiqi PDF faylına çevirir.
    /// <para>
    /// Chromium mühərriki işlədildiyi üçün Azərbaycan hərfləri (ə, ı, ş, ğ, ç, ö, ü),
    /// emojilər və ₼ simvolu PDF-də <b>düzgün</b> görünür. Bu, xarici PDF
    /// kitabxanası olmadan ən keyfiyyətli yoldur.
    /// </para>
    /// </summary>
    public static class HtmlPdfWriter
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);

        /// <summary>
        /// HTML mətnini PDF faylına yazır.
        /// <para>
        /// <paramref name="landscape"/> = <c>true</c> → <b>A4 LANDSHAFT</b> ✓
        /// (geniş cədvəllər — məs. 9 sütunlu ödəniş cədvəli — <b>TAM sığır</b> ✓✓✓).
        /// </para>
        /// </summary>
        public static async Task RenderAsync(
            string html,
            string pdfPath,
            bool landscape = false,
            double scale = 1.0)
        {
            await Gate.WaitAsync();
            Window? window = null;
            var tempPath = Path.Combine(
                Path.GetTempPath(),
                "avtopark-hesabat-" + Guid.NewGuid().ToString("N") + ".html");

            try
            {
                await File.WriteAllTextAsync(tempPath, html, new UTF8Encoding(false));

                // Pəncərə ekrandan kənarda açılır — istifadəçi görmür.
                var web = new WebView2();
                window = new Window
                {
                    Width = 1180,
                    Height = 900,
                    Left = -4000,
                    Top = -4000,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize,
                    Content = web
                };

                window.Show();

                await web.EnsureCoreWebView2Async();

                var hazir = new TaskCompletionSource<bool>();
                web.NavigationCompleted += (_, _) => hazir.TrySetResult(true);

                web.CoreWebView2.Navigate(new Uri(tempPath).AbsoluteUri);
                await hazir.Task;

                // Səhifənin tam yüklənməsi və şəbəkə resurslarının bitməsi üçün.
                await Task.Delay(500);

                // 🖨️ LANDSHAFT + miqyas → 9 sütunlu CƏDVƏL TAM SIĞIR ✓✓✓
                var settings = web.CoreWebView2.Environment.CreatePrintSettings();
                settings.Orientation = landscape
                    ? CoreWebView2PrintOrientation.Landscape
                    : CoreWebView2PrintOrientation.Portrait;
                settings.ScaleFactor = scale;
                settings.ShouldPrintBackgrounds = true;
                settings.MarginTop = 0.4;
                settings.MarginBottom = 0.4;
                settings.MarginLeft = 0.4;
                settings.MarginRight = 0.4;

                await web.CoreWebView2.PrintToPdfAsync(pdfPath, settings);
            }
            finally
            {
                try
                {
                    window?.Close();
                }
                catch
                {
                    // Pəncərə artıq bağlıdır.
                }

                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Müvəqqəti fayl silinməsə də zərəri yoxdur.
                }

                Gate.Release();
            }
        }
    }
}
