using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// 🔔 <b>BİLDİRİŞLƏR</b> ✓✓✓ — «bu gün hansı maşının ödənişi var? nə vaxtdır?» ✓
    /// <para>
    /// Bütün mənbələrdən <b>VAHİD</b> siyahı qurur ✓✓✓
    /// (<see cref="Services.OdenisQrafiki"/> — düstur təkrarlanmır ✗):
    /// </para>
    /// <list type="bullet">
    ///   <item>🏦 Kredit taksitləri (aktiv kreditlər ✓)</item>
    ///   <item>⏳ İlkin ödəniş möhlətləri ✓</item>
    ///   <item>🛒 Nisyə satış möhlətləri ✓</item>
    ///   <item>⚠️ Gecikmə cərimələri (ödənilməmiş ✓)</item>
    /// </list>
    /// <para>
    /// ⏱️ Hər sətirdə «nə vaxtdır» göstərilir: <b>BUGÜN</b> ✓ · <b>X günə</b> ✓ ·
    /// <b>⚠ X gün GECİKİB</b> ✓✓✓
    /// </para>
    /// </summary>
    public sealed partial class BildirisViewModel : ObservableObject
    {
        private readonly ICreditService _kreditler;
        private readonly ISaleService _satislar;
        private readonly IMohletService _mohletler;
        private readonly ILogger<BildirisViewModel> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Bütün bildirişlər (filtrsiz ✓).</summary>
        private List<OdenisBildirisi> _butun = new();

        /// <summary>Filtr tətbiq olunmuş siyahı ✓.</summary>
        private List<OdenisBildirisi> _filtrli = new();

        public BildirisViewModel(
            ICreditService kreditler,
            ISaleService satislar,
            IMohletService mohletler,
            ILogger<BildirisViewModel> logger)
        {
            _kreditler = kreditler;
            _satislar = satislar;
            _mohletler = mohletler;
            _logger = logger;
        }

        // ====================================================================
        //  🎛️ FİLTRLƏR ✓
        // ====================================================================

        /// <summary>Dövr seçimləri ✓ (KPI ilə eyni adlar ✓).</summary>
        public IReadOnlyList<string> DovrSecimleri { get; } = new[]
        {
            "Hamısı", "⚠ Gecikmiş", "📅 Bugün", "🗓 Bu həftə", "📆 Bu ay", "✅ Ödənilmiş"
        };

        [ObservableProperty] private string secilmisDovr = "Hamısı";

        /// <summary>Növ seçimləri ✓.</summary>
        public IReadOnlyList<string> NovSecimleri { get; } = new[]
        {
            "Hamısı", "🏦 Kredit taksiti", "⏳ İlkin ödəniş möhləti", "🛒 Satış (nisyə) möhləti", "⚠ Gecikmə cəriməsi"
        };

        [ObservableProperty] private string secilmisNov = "Hamısı";

        /// <summary>🔍 Axtarış: müqavilə № · müştəri · maşın ✓.</summary>
        [ObservableProperty] private string axtaris = string.Empty;

        /// <summary>Ödənilmiş ödənişlər də göstərilsin? ✓</summary>
        [ObservableProperty] private bool odenilmisDeGorsensin;

        public string AxtarisMetni => $"🔍 {_filtrli.Count} bildiriş";

        // ====================================================================
        //  📊 XÜLASƏ RƏQƏMLƏRİ (KPI ✓)
        // ====================================================================

        /// <summary>📅 BUGÜN ödənilməli (₼) ✓.</summary>
        [ObservableProperty] private decimal bugunCemi;

        [ObservableProperty] private int bugunSayi;

        /// <summary>⚠ Vaxtı keçmiş (₼) ✓.</summary>
        [ObservableProperty] private decimal gecikmisCemi;

        [ObservableProperty] private int gecikmisSayi;

        /// <summary>🗓 Yaxın 7 gün (₼) ✓.</summary>
        [ObservableProperty] private decimal hefteCemi;

        [ObservableProperty] private int hefteSayi;

        /// <summary>📆 Yaxın 30 gün (₼) ✓.</summary>
        [ObservableProperty] private decimal ayCemi;

        /// <summary>🕓 BÜTÜN gözlənilən (ödənilməmiş) ödənişlər (₼) ✓.</summary>
        [ObservableProperty] private decimal gozlenilenCemi;

        [ObservableProperty] private int gozlenilenSayi;

        /// <summary>🌅 Bugünə baxış mətni: «6 oktyabr 2026 · bazar ertəsi».</summary>
        [ObservableProperty] private string bugunMetni = string.Empty;

        /// <summary>📄 Vəziyyət mətni (neçə bildiriş göstərilir ✓).</summary>
        [ObservableProperty] private string veziyyetMetni = "—";

        public string GecikmisRengi => GecikmisCemi > 0m ? "#FB7185" : "#34D399";

        partial void OnGecikmisCemiChanged(decimal value) => OnPropertyChanged(nameof(GecikmisRengi));

        // ====================================================================
        //  📋 SİYAHILAR ✓
        // ====================================================================

        /// <summary>🔔 Göstərilən bildirişlər (filtr tətbiq olunmuş ✓).</summary>
        public ObservableCollection<OdenisBildirisi> Bildirisler { get; } = new();

        /// <summary>⚠️ Yalnız TƏCİLİ olanlar (gecikmiş + bugün ✓) — yuxarıda qırmızı blok üçün ✓.</summary>
        public ObservableCollection<OdenisBildirisi> TeciliBildirisler { get; } = new();

        /// <summary>Təcili blok görünsün? ✓</summary>
        public bool TeciliVar => TeciliBildirisler.Count > 0;

        /// <summary>Ümumi siyahı boşdur? ✓</summary>
        public bool Boshdur => Bildirisler.Count == 0;

        // ====================================================================
        //  🔄 YÜKLƏMƏ ✓✓✓
        // ====================================================================

        [RelayCommand]
        private Task Yenile() => LoadAsync();

        /// <summary>Məlumatları oxuyur və bildiriş siyahısını qurur ✓✓✓</summary>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);

            try
            {
                var (kreditler, emeliyyatlar, mohletler, satislar) = await Task.Run(async () =>
                {
                    var k = await _kreditler.GetCreditsAsync(cancellationToken);
                    var t = await _kreditler.GetTransactionsAsync(cancellationToken);
                    var m = await _mohletler.GetAllAsync(cancellationToken);
                    var s = await _satislar.GetSalesAsync(cancellationToken);

                    return (k, t, m, s);
                }, cancellationToken);

                _butun = OdenisQrafiki.Qur(kreditler, emeliyyatlar, mohletler, satislar);

                BugunMetni = DateTime.Today.ToString("dd MMMM yyyy");

                XulaseHesabla();
                Filtrle();

                _logger.LogInformation(
                    "🔔 Bildirişlər: {Butun} qeyd ✓ · gecikmiş {Gec:N2} ₼ · bugün {Bug:N2} ₼ · gözlənilən {Goz:N2} ₼ ✓",
                    _butun.Count, GecikmisCemi, BugunCemi, GozlenilenCemi);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Bildirişlər yüklənə bilmədi ✗");
                VeziyyetMetni = "⚠ Məlumatlar yüklənə bilmədi ✗ — " + ex.Message;
            }
            finally
            {
                _gate.Release();
            }
        }

        // ====================================================================
        //  📊 XÜLASƏ HESABLAMASI ✓ — KPI-lar HƏMİŞƏ tam dövr üzrədir ✓✓✓
        // ====================================================================

        private void XulaseHesabla()
        {
            var gozlenilen = _butun.Where(b => !b.Odenilib).ToList();

            BugunCemi = gozlenilen.Where(b => b.Bugun).Sum(b => b.Mebleg);
            BugunSayi = gozlenilen.Count(b => b.Bugun);

            GecikmisCemi = gozlenilen.Where(b => b.Gecikmis).Sum(b => b.Mebleg);
            GecikmisSayi = gozlenilen.Count(b => b.Gecikmis);

            HefteCemi = gozlenilen.Where(b => b.YaxinHefte).Sum(b => b.Mebleg);
            HefteSayi = gozlenilen.Count(b => b.YaxinHefte);

            AyCemi = gozlenilen.Where(b => b.YaxinAy).Sum(b => b.Mebleg);

            GozlenilenCemi = gozlenilen.Sum(b => b.Mebleg);
            GozlenilenSayi = gozlenilen.Count;
        }

        // ====================================================================
        //  🔎 FİLTRLƏMƏ ✓✓✓ — CƏMLƏR DƏYİŞMİR ✗ (filtr yalnız cədvələ təsir edir ✓)
        // ====================================================================

        private void Filtrle()
        {
            var setirler = _butun.AsEnumerable();

            // 👁 Ödənilmişlər göstərilsin? ✓
            if (!OdenilmisDeGorsensin)
            {
                setirler = setirler.Where(b => !b.Odenilib);
            }

            // 📅 Dövr ✓
            setirler = SecilmisDovr switch
            {
                "⚠ Gecikmiş" => setirler.Where(b => b.Gecikmis),
                "📅 Bugün" => setirler.Where(b => b.Bugun),
                "🗓 Bu həftə" => setirler.Where(b => b.Bugun || b.YaxinHefte),
                "📆 Bu ay" => setirler.Where(b => b.Bugun || b.YaxinAy || b.Gecikmis),
                "✅ Ödənilmiş" => setirler.Where(b => b.Odenilib),
                _ => setirler
            };

            // 🏷️ Növ ✓
            if (SecilmisNov != "Hamısı")
            {
                setirler = setirler.Where(b => b.Nov == SecilmisNov);
            }

            // 🔍 Axtarış ✓
            if (!string.IsNullOrWhiteSpace(Axtaris))
            {
                var a = Axtaris.Trim().ToLowerInvariant();

                setirler = setirler.Where(b =>
                    (b.Mustar ?? "").ToLowerInvariant().Contains(a)
                    || (b.Muqavile ?? "").ToLowerInvariant().Contains(a)
                    || (b.Avtomobil ?? "").ToLowerInvariant().Contains(a)
                    || (b.Nov ?? "").ToLowerInvariant().Contains(a)
                    || (b.Qeyd ?? "").ToLowerInvariant().Contains(a));
            }

            _filtrli = setirler
                .OrderBy(b => b.Odenilib ? 1 : 0)
                .ThenBy(b => b.Tarix)
                .ToList();

            Bildirisler.Clear();

            foreach (var b in _filtrli)
            {
                Bildirisler.Add(b);
            }

            // ⚠️ TƏCİLİ blok — həmişə TAM siyahıdan ✓ (filtrdən asılı deyil ✗)
            TeciliBildirisler.Clear();

            foreach (var b in _butun
                         .Where(b => b.Tecili)
                         .OrderBy(b => b.Tarix)
                         .ThenBy(b => b.Mustar))
            {
                TeciliBildirisler.Add(b);
            }

            OnPropertyChanged(nameof(TeciliVar));
            OnPropertyChanged(nameof(Boshdur));
            OnPropertyChanged(nameof(AxtarisMetni));

            var cemi = _filtrli.Where(b => !b.Odenilib).Sum(b => b.Mebleg);

            VeziyyetMetni = _filtrli.Count == 0
                ? "🔔 Bu filtr üzrə bildiriş yoxdur ✓"
                : $"🔔 {_filtrli.Count} bildiriş göstərilir ✓ · ödənilməmiş: {cemi:N2} ₼";
        }

        partial void OnSecilmisDovrChanged(string value) => Filtrle();

        partial void OnSecilmisNovChanged(string value) => Filtrle();

        partial void OnAxtarisChanged(string value) => Filtrle();

        partial void OnOdenilmisDeGorsensinChanged(bool value) => Filtrle();
    }
}

