// ========================================================================
//  ⚠ VACİB: Bu fayl YALNIZ MASAÜSTÜ (WPF · net8.0-windows) layihəsində
//  kompilyasiya olunur ✓ — WebView2 paketi WPF-ə xasdır ✗.
//  Web layihəsi (net8.0) bu faylı atlasın deyə `#if WINDOWS` ilə əhatə
//  olunub ✓ (WPF-də WINDOWS işarəsi avtomatik təyin olunur ✓✓✓).
// ========================================================================
#if WINDOWS
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 📄 <b>REAL PDF</b> yazıcısı ✓✓✓
    /// <para>
    /// HTML <b>HTML kimi YOX</b> ✗ — gizli <b>WebView2</b> sənədi render edir ✓ və
    /// <see cref="CoreWebView2.PrintToPdfAsync(string, CoreWebView2PrintSettings?)"/>
    /// ilə <b>ƏSL PDF faylı</b> yaradır ✓ (Chromium-un öz PDF mühərriki ✓ —
    /// xarici kitabxana LAZIM DEYİL ✗ ✓).
    /// </para>
    /// <para>
    /// Səhifə: <b>A4 · landscape</b> · fon rəngləri çap olunur ✓ ·
    /// kənar boşluqlar 8 mm ✓ → peşəkar sənəd ✓✓✓
    /// </para>
    /// </summary>
    public static class PdfYazici
    {
        /// <summary>
        /// HTML-i REAL PDF faylına çevirib <paramref name="yol"/>-a yazır ✓✓✓
        /// </summary>
        /// <param name="view">Pəncərədəki gizli WebView2 ✓ (ekranda görünmür ✗)</param>
        /// <param name="html">Sənədin HTML məzmunu ✓</param>
        /// <param name="yol">İstifadəçinin «Save As» pəncərəsindən seçdiyi PDF yolu ✓</param>
        public static async Task PdfYazAsync(WebView2 view, string html, string yol)
        {
            // ① WebView2 hazırlanır ✓
            await view.EnsureCoreWebView2Async();

            // ② HTML render olunur və bitməsi GÖZLƏNİLİR ✓
            var bitdi = new TaskCompletionSource<bool>();

            void Tamamlandi(object? sender, CoreWebView2NavigationCompletedEventArgs e)
            {
                view.NavigationCompleted -= Tamamlandi;
                bitdi.TrySetResult(e.IsSuccess);
            }

            view.NavigationCompleted += Tamamlandi;
            view.NavigateToString(html);

            await bitdi.Task;

            // ③ Şriftlərin / cədvəllərin tam yerləşməsi üçün qısa fasilə ✓
            await Task.Delay(600);

            // ④ PEŞƏKAR ÇAP PARAMETRLƏRİ ✓✓✓
            var parametrler = view.CoreWebView2.Environment.CreatePrintSettings();

            parametrler.Orientation = CoreWebView2PrintOrientation.Landscape;  // ✓ A4 yan
            parametrler.MarginTop = 0.31;      // ≈ 8 mm ✓
            parametrler.MarginBottom = 0.31;
            parametrler.MarginLeft = 0.31;
            parametrler.MarginRight = 0.31;
            parametrler.ShouldPrintBackgrounds = true;          // ✓ rəngli KPI kartları
            parametrler.ShouldPrintHeaderAndFooter = false;     // ✗ URL/tarix zolağı yox

            // ⑤ ✅ ƏSL PDF FAYLI YAZILIR ✓✓✓
            await view.CoreWebView2.PrintToPdfAsync(yol, parametrler);
        }
    }
}
#endif
