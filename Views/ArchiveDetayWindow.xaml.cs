using System.IO;
using System.Windows;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.Views
{
    /// <summary>
    /// 🗄️ <b>ARXİV DETALLARI</b> — «Satılan &amp; Krediti Bitmiş» sətrinə
    /// <b>iki dəfə klikləyəndə</b> açılan pəncərə ✓✓✓
    /// <para>
    /// Burada avtomobilin və kreditin <b>BÜTÜN məlumatı</b> görünür ✓:
    /// 🚘 avtomobil · 🧾 xərclər · 💳 kredit şərtləri ·
    /// 📋 bütün hərəkətlər · 👥 tərəfdaş payları · 📊 yekun göstəricilər ✓
    /// </para>
    /// <para>
    /// 📄 <b>PDF İXRACI</b> — <see cref="IDialogService.ShowSaveFileDialog"/> ilə
    /// <b>«hara yadda saxlayım?»</b> pəncərəsi açılır ✓ (istifadəçi özü yer seçir ✓),
    /// sonra peşəkar sənəd brauzerdə açılır ✓ → <b>Ctrl + P → «Microsoft Print to
    /// PDF»</b> ilə real PDF alınır ✓✓✓
    /// </para>
    /// </summary>
    public partial class ArchiveDetayWindow : Window
    {
        private readonly IDialogService? _dialogs;

        public ArchiveDetayWindow(IDialogService? dialogs = null)
        {
            _dialogs = dialogs;
            InitializeComponent();
        }

        /// <summary>
        /// 📄 <b>REAL PDF İXRACI</b> ✓✓✓
        /// <para>
        /// ⚠ ƏVVƏL HTML faylı yazılırdı ✗ → indi <b>gizli WebView2</b> sənədi
        /// render edir ✓ və <see cref="PdfYazici.PdfYazAsync"/> ilə
        /// <b>ƏSL PDF</b> faylı yaradılır ✓ (Adobe/brauzerdə birbaşa açılır ✓).
        /// </para>
        /// </summary>
        private async void PdfIxrac_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ArchiveDetayVeri veri)
            {
                return;
            }

            try
            {
                // 💾 fayl adı .pdf uzantısı ilə təklif olunur ✓
                var teklif = Path.ChangeExtension(ArchivePdfBuilder.FaylAdi(veri.Car), ".pdf");

                // ================================================================
                //  💾 «HARA YADDA SAXLAYIM?» — İSTİFADƏÇİ ÖZÜ SEÇİR ✓✓✓
                //  (native File Explorer pəncərəsi ✓ · ləğv edə bilər ✓)
                // ================================================================
                var yol = _dialogs?.ShowSaveFileDialog("PDF sənədi|*.pdf", teklif);

                if (string.IsNullOrWhiteSpace(yol))
                {
                    return;   // ✓ istifadəçi ləğv etdi
                }

                var html = ArchivePdfBuilder.Build(
                    veri.Car, veri.Credit, veri.Hereketler, veri.Paylar, veri.Satislar);

                // ================================================================
                //  🖨️ **REAL PDF** — WebView2 «PrintToPdf» ✓✓✓ (HTML YOX ✗)
                // ================================================================
                await PdfYazici.PdfYazAsync(PdfWebView, html, yol);

                _dialogs?.ShowInfo(
                    "✅ REAL PDF hazırdır ✓\n\n" +
                    $"📄 {yol}\n\n" +
                    "Sənədə daxildir: 🚘 avtomobil · 🧾 xərclər · 💳 kredit · " +
                    "📅 ÖDƏNİŞ QRAFİKİ · 📋 hərəkətlər · 👥 tərəfdaş payları ✓",
                    "PDF ixracı");
            }
            catch (Exception ex)
            {
                _dialogs?.ShowError("PDF yaradıla bilmədi: " + ex.Message);
            }
        }

        private void Bagla_Click(object sender, RoutedEventArgs e) => Close();

        // ====================================================================
        //  📎 SƏNƏDLƏR — AÇMA / QOVLUQ ✓✓✓
        // ====================================================================

        /// <summary>📂 Seçilmiş sənədi standart proqramla açır ✓✓✓</summary>
        private void SenedAc_Click(object sender, RoutedEventArgs e)
            => SenedAc(SenedGrid?.SelectedItem as MediaAttachment);

        /// <summary>🖱️ Sənəd sətrinə İKİ DƏFƏ KLİK → fayl açılır ✓✓✓</summary>
        private void SenedGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => SenedAc(SenedGrid?.SelectedItem as MediaAttachment);

        private void SenedAc(MediaAttachment? sened)
        {
            if (sened is null)
            {
                _dialogs?.ShowWarning("Əvvəlcə cədvəldən sənəd seçin ✗");
                return;
            }

            var media = App.Services?.GetService(typeof(IMediaService)) as IMediaService;

            if (media is null || !media.OpenWithShell(sened))
            {
                _dialogs?.ShowError(
                    "Sənəd açıla bilmədi ✗\n\n" +
                    "Fayl silinmiş və ya yerini dəyişmiş ola bilər.\n" +
                    "«🗂️ Sənəd qovluğunu aç» düyməsi ilə qovluğu yoxlayın ✓");
            }
        }

        /// <summary>🗂️ Sənədlər qovluğunu File Explorer-də açır ✓</summary>
        private void SenedQovluguAc_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ArchiveDetayVeri veri)
            {
                return;
            }

            var media = App.Services?.GetService(typeof(IMediaService)) as IMediaService;

            if (media is null || !media.OpenFolder(MediaRefTypes.Car, veri.Car.Id))
            {
                _dialogs?.ShowError("Sənəd qovluğu açıla bilmədi ✗");
            }
        }
    }

    /// <summary>
    /// 🗄️ Arxiv detalı üçün məlumat paketi (WPF binding ✓✓✓)
    /// </summary>
    public sealed class ArchiveDetayVeri
    {
        public CarItem Car { get; init; } = null!;

        public Credit? Credit { get; init; }

        public IReadOnlyList<CreditTransaction> Hereketler { get; init; } = Array.Empty<CreditTransaction>();

        public IReadOnlyList<PartnerShare> Paylar { get; init; } = Array.Empty<PartnerShare>();

        /// <summary>
        /// 💰 Bu avtomobilin <b>SATIŞ QEYDLƏRİ</b> ✓✓✓
        /// (necəyə satılıb · tarix · müştəri · ödəniş üsulu · barter · qeyd ✓)
        /// </summary>
        public IReadOnlyList<Sale> Satislar { get; init; } = Array.Empty<Sale>();

        // ====================================================================
        //  📎 SƏNƏDLƏR (Media arxivi) ✓✓✓
        // ====================================================================

        /// <summary>📎 Avtomobilə bağlı BÜTÜN sənədlər ✓ (qaimə · çek · müqavilə · şəkil ✓)</summary>
        public IReadOnlyList<MediaAttachment> Senedler { get; init; } = Array.Empty<MediaAttachment>();

        /// <summary>📎 Sənəd varmı? ✓</summary>
        public bool SenedVar => Senedler.Count > 0;

        /// <summary>📎 Bölmə başlığı — neçə sənəd olduğu ✓</summary>
        public string SenedBasligi => Senedler.Count == 0
            ? "📎 SƏNƏDLƏR — bu avtomobilə sənəd əlavə edilməyib ✗"
            : $"📎 SƏNƏDLƏR ({Senedler.Count} fayl) — iki dəfə klikləyərək AÇIN ✓";

        /// <summary>🗂️ Sənədlərin saxlandığı qovluq ✓</summary>
        public string SenedQovlugu { get; init; } = string.Empty;

        /// <summary>🏷️ Başlıq ✓</summary>
        public string Basliq => Car.DisplayName;

        /// <summary>🏷️ Alt başlıq — status · müqavilə · müştəri ✓</summary>
        public string AltBasliq =>
            $"Status: {Car.Status}" +
            (Credit is not null ? $"   ·   Müqavilə: {Credit.MuqavileNomresi}" : string.Empty) +
            (Credit is not null ? $"   ·   Müştəri: {Credit.Mustəri}" : string.Empty);

        /// <summary>💳 Kredit bloku görünür? ✓</summary>
        public bool KreditVar => Credit is not null;

        /// <summary>📋 Hərəkətlər bloku görünür? ✓</summary>
        public bool HereketVar => Hereketler.Count > 0;

        /// <summary>👥 Paylar bloku görünür? ✓</summary>
        public bool PayVar => Paylar.Count > 0;

        /// <summary>🧾 Xərclər bloku görünür? ✓</summary>
        public bool XercVar => Xercler.Count > 0;

        /// <summary>
        /// 💰 <b>SATIŞ QİYMƏTİ</b> ✓✓✓
        /// <list type="number">
        ///   <item>Satış qeydi varsa → ondan götürülür ✓ (nağd/kredit satışı ✓)</item>
        ///   <item>Kredit varsa → kredit qiyməti + avans ✓</item>
        ///   <item>Heç biri yoxdursa → avtomobilin sahəsindən ✓</item>
        /// </list>
        /// <para>
        /// ⚠ ƏVVƏL yalnız <c>Car.SatisQiymeti</c> oxunurdu ✗ → satış qeydi
        /// olan maşınlarda <b>0,00 AZN</b> görünürdü ✗✓✓
        /// </para>
        /// </summary>
        public decimal SatisQiymeti => Satislar.Count > 0
            ? Satislar.Sum(s => s.SatisQiymeti)
            : Credit is not null
                ? Credit.KreditQiymeti + Credit.IlkinOdenis
                : Car.SatisQiymeti;

        /// <summary>✅ Satış məlumatı varmı?</summary>
        public bool SatisVar => SatisQiymeti > 0m;

        /// <summary>📋 Satış alt mətni: «26.09.2026 · Rebbil · Nağd» ✓</summary>
        public string SatisAltMetni
        {
            get
            {
                if (Satislar.Count > 0)
                {
                    var s = Satislar[0];
                    return $"{s.SatisTarixi:dd.MM.yyyy} · {s.Mustəri} · {s.OdenisUsuluMetni}";
                }

                return Credit is not null
                    ? $"{Credit.MuqavileNomresi} · {Credit.Mustəri} · kreditlə satış"
                    : "Satış qeydi tapılmadı ✗";
            }
        }

        /// <summary>
        /// 🎯 <b>MƏNFƏƏT</b> = satış qiyməti − maya dəyəri ✓✓✓
        /// (satış qiyməti satış qeydindən götürülür ✓ — 0 çıxmır ✗)
        /// </summary>
        public decimal Menfeet => SatisQiymeti - Car.MayaDeyeri;

        /// <summary>🎨 Mənfəət rəngi ✓</summary>
        public string MenfeetRengi => Menfeet >= 0m ? "#34D399" : "#F43F5E";

        /// <summary>📊 Hərəkətlərin cəmi ✓</summary>
        public decimal HereketCemi => Hereketler.Sum(t => t.Mebleg);

        /// <summary>📊 Payların cəmi ✓</summary>
        public decimal PayCemi => Paylar.Sum(p => p.Mebleg);

        /// <summary>📅 Sənəd tarixi (mətn) ✓</summary>
        public string SenedTarixiMetni => DateTime.Today.ToString("dd.MM.yyyy");

        /// <summary>🧾 Xərclər — kateqoriya üzrə qruplaşdırılmış ✓</summary>
        public IReadOnlyList<XercQrup> Xercler => (Car.Expenses ?? new List<ExpenseItem>())
            .Where(x => !string.Equals(x.Kategoriya, "Alış", StringComparison.Ordinal))
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Kategoriya) ? "Digər" : x.Kategoriya!)
            .Select(g => new XercQrup(g.Key, g.Sum(x => x.Mebleg), g.Count()))
            .OrderByDescending(x => x.Mebleg)
            .ToList();

        /// <summary>🧾 Xərclərin cəmi ✓</summary>
        public decimal XercCemi => Car.Xercler;
    }

    /// <summary>🧾 Bir xərc kateqoriyası ✓</summary>
    public sealed record XercQrup(string Kategoriya, decimal Mebleg, int Say);
}
