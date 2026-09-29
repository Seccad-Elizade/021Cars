// ============================================================================
//  🖱️ 021Cars — Ctrl + Siçan ÇARXI SÜRÜŞMƏSİ ✓✓✓
// ----------------------------------------------------------------------------
//  ✅ Proqram SİÇAN + KLAVİATURA ilə idarə olunur ✓
//  ✅ <b>Ctrl basılı saxlanılıb çarx fırladılanda</b> → cədvəl/qovluq
//     <b>SAĞA / SOLA</b> sürüşür ✓✓✓  (yt-dlp / brauzer davranışı ✓)
//  ✅ Alt cədvəllər daxil ✓ (DataGrid · ListView · ScrollViewer ✓)
//  ✅ Sıçanın ALTINDAKI elementdən başlayaraq ən yaxın sürüşən elementi tapır ✓
//  ✅ Ctrl basılı deyilsə → HEÇ NƏ dəyişmir ✗ (adi şaquli sürüşmə işləyir ✓)
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EnterpriseAeroStudio.Views
{
    /// <summary>
    /// 🖱️ <b>CTRL + SİÇAN ÇARXI İLƏ SAĞA/SOLA SÜRÜŞMƏ</b> ✓✓✓
    /// <para>
    /// Pəncərəyə <c>UiSürüşmə.Qos(this)</c> çağırmaq kifayətdir ✓ — bütün alt
    /// cədvəllər (DataGrid · ListView · ScrollViewer) avtomatik işləyir ✓
    /// </para>
    /// </summary>
    public static class UiSürüşmə
    {
        /// <summary>📏 Bir fırlatmada sürüşən məsafə (piksel ✓)</summary>
        public const double Addım = 90;

        /// <summary>
        /// 🌍 <b>BÜTÜN PƏNCƏRƏLƏR ÜÇÜN QLOBAL QOŞMA</b> ✓✓✓
        /// <para>
        /// ⚠️ <b>BİR DƏFƏ</b> çağırılır ✓ (<c>App.OnStartup</c> ✓) — bundan sonra
        /// proqramda açılan <b>hər bir pəncərə</b> (cədvəl · dialoq · tab ✓)
        /// Ctrl+Çarx ilə SAĞA/SOLA sürüşür ✓✓✓
        /// </para>
        /// </summary>
        public static void QlobalQos()
        {
            if (_qosulub) return;
            _qosulub = true;

            try
            {
                EventManager.RegisterClassHandler(
                    typeof(Window),
                    UIElement.PreviewMouseWheelEvent,
                    new MouseWheelEventHandler((_, e) => İşlə(e)));
            }
            catch { }
        }

        /// <summary>🔒 Artıq qoşulub? ✓ (iki dəfə qoşulmasın ✗)</summary>
        private static bool _qosulub;

        /// <summary>
        /// 🪄 <b>PƏNCƏRƏYƏ Ctrl+Çarx davranışını QOŞUR</b> ✓
        /// <para>(qlobal qoşma mümkün olmadıqda əl ilə ✓)</para>
        /// </summary>
        public static void Qos(Window? pəncərə)
        {
            if (pəncərə is null) return;

            try
            {
                pəncərə.PreviewMouseWheel += (_, e) => İşlə(e);
            }
            catch { }
        }

        /// <summary>🖱️ Əsas məntiq ✓ — Ctrl basılıbsa YATAY sürüşdürür ✓</summary>
        private static void İşlə(MouseWheelEventArgs? e)
        {
            try
            {
                if (e is null) return;

                // 🔑 Ctrl basılı deyilsə → adi (şaquli) davranış ✓
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

                var sürüşən = Tap(e.OriginalSource as DependencyObject);

                if (sürüşən is null) return;

                // ⬅️➡️ Yalnız YATAY sürüşə bilən elementlər ✓
                if (sürüşən.ScrollableWidth <= 0.5) return;

                var geri = e.Delta > 0;      // 🔼 yuxarı = sola ✓ · 🔽 aşağı = sağa ✓

                var yeni = sürüşən.HorizontalOffset + (geri ? -Addım : Addım);

                sürüşən.ScrollToHorizontalOffset(yeni);

                e.Handled = true;            // ✓ şaquli sürüşmə BAŞ VERMƏSİN ✗
            }
            catch { }
        }

        /// <summary>
        /// 🔎 Siçanın altındaki elementdən YUXARI doğru ən yaxın
        /// <see cref="ScrollViewer"/>-i tapır ✓ (DataGrid-in içindəki daxil ✓)
        /// </summary>
        private static ScrollViewer? Tap(DependencyObject? element)
        {
            try
            {
                var cari = element;

                while (cari is not null)
                {
                    // ✅ ScrollViewer (və ya onun alt sinfi) → dərhal qaytar ✓
                    if (cari is ScrollViewer sv) return sv;

                    // ✅ DataGrid → öz daxili ScrollViewer-i ✓
                    if (cari is DataGrid dg)
                    {
                        var iç = TapŞablon(dg);
                        if (iç is not null) return iç;
                    }

                    cari = GörməElementi(cari);
                }
            }
            catch { }

            return null;
        }

        /// <summary>🧭 Elementin valideynini tapır ✓ (vizual + məntiqi ağac ✓)</summary>
        private static DependencyObject? GörməElementi(DependencyObject element)
        {
            try
            {
                if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
                {
                    var valideyn = VisualTreeHelper.GetParent(element);

                    if (valideyn is not null) return valideyn;
                }
            }
            catch { }

            try
            {
                return LogicalTreeHelper.GetParent(element);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>📦 Şablon (template) içindəki ilk <see cref="ScrollViewer"/> ✓</summary>
        private static ScrollViewer? TapŞablon(Control nəzarət)
        {
            try
            {
                var şablon = nəzarət.Template;

                if (şablon is null) return null;

                return şablon.FindName("DG_ScrollViewer", nəzarət) as ScrollViewer
                       ?? AxtarVizual(nəzarət);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>🔍 Vizual ağacda ilk ScrollViewer-i axtarır ✓ (dərinlik 4 ✓)</summary>
        private static ScrollViewer? AxtarVizual(DependencyObject kök, int dərinlik = 0)
        {
            if (dərinlik > 4) return null;

            try
            {
                var say = VisualTreeHelper.GetChildrenCount(kök);

                for (var i = 0; i < say; i++)
                {
                    var uşaq = VisualTreeHelper.GetChild(kök, i);

                    if (uşaq is ScrollViewer sv) return sv;

                    var tapılan = AxtarVizual(uşaq, dərinlik + 1);

                    if (tapılan is not null) return tapılan;
                }
            }
            catch { }

            return null;
        }
    }
}
