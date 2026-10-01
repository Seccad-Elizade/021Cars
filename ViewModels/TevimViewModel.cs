using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// 🗓️ <b>ÖDƏNİŞ TƏQVİMİ</b> ✓✓✓ — «hansı ay hansı maşınlar pul verəcək» ✓
    /// <para>
    /// İki cədvəl:
    /// </para>
    /// <list type="number">
    ///   <item>📅 <b>Aylıq xülasə</b> — hər ay üzrə cəmi / ödənilmiş / qalıq ✓
    ///         (cari ay yaşıl ✓ · gecikmə varsa qırmızı ⚠)</item>
    ///   <item>🚘 <b>Seçilmiş ayın maşınları</b> — «BMW 328 · Rebbil · 1 378,00 ₼ · 05.09» ✓✓✓</item>
    ///   <item>📄 <b>Seçilmiş ayın bütün ödəniş sətirləri</b> ✓</item>
    /// </list>
    /// <para>⚙️ Hesablama <see cref="Services.OdenisQrafiki"/>-dədir ✓ (düstur təkrarlanmır ✗)</para>
    /// </summary>
    public sealed partial class TevimViewModel : ObservableObject
    {
        private readonly ICreditService _kreditler;
        private readonly ISaleService _satislar;
        private readonly IMohletService _mohletler;
        private readonly ILogger<TevimViewModel> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        private List<OdenisBildirisi> _butun = new();

        public TevimViewModel(
            ICreditService kreditler,
            ISaleService satislar,
            IMohletService mohletler,
            ILogger<TevimViewModel> logger)
        {
            _kreditler = kreditler;
            _satislar = satislar;
            _mohletler = mohletler;
            _logger = logger;
        }

        // ====================================================================
        //  📋 CƏDVƏLLƏR ✓
        // ====================================================================

        /// <summary>🗓️ Aylar üzrə xülasə ✓ (tarixə görə sıralı ✓).</summary>
        public ObservableCollection<AyOdenisCemi> Aylar { get; } = new();

        /// <summary>🚘 Seçilmiş ayın avtomobilləri ✓ — «hansı maşın nə qədər ödəyəcək» ✓✓✓</summary>
        public ObservableCollection<AvtomobilOdenisi> AyAvtomobilleri { get; } = new();

        /// <summary>📄 Seçilmiş ayın bütün ödəniş sətirləri ✓.</summary>
        public ObservableCollection<OdenisBildirisi> AySetirleri { get; } = new();

        /// <summary>🔍 Axtarış nəticəsində tapılan bütün sətirlər ✓.</summary>
        public ObservableCollection<OdenisBildirisi> AxtarisSetirleri { get; } = new();

        /// <summary>Seçilmiş ay ✓.</summary>
        [ObservableProperty] private AyOdenisCemi? secilmisAy;

        // ---- Seçilmiş ayın rəqəmləri ✓ ----
        [ObservableProperty] private string secilmisAyAdi = "—";
        [ObservableProperty] private decimal secilmisAyCemi;
        [ObservableProperty] private decimal secilmisAyOdenilmis;
        [ObservableProperty] private decimal secilmisAyQaliq;
        [ObservableProperty] private string secilmisAyVeziyyet = "—";

        // ---- Ümumi (bütün aylar) ✓ ----
        [ObservableProperty] private decimal butunCemi;
        [ObservableProperty] private decimal butunOdenilmis;
        [ObservableProperty] private decimal butunQaliq;
        [ObservableProperty] private string butunVeziyyet = "—";

        /// <summary>🔍 Axtarış: müqavilə · müştəri · maşın ✓.</summary>
        [ObservableProperty] private string axtaris = string.Empty;

        /// <summary>Yalnız BUGÜNDƏN sonrakı aylar göstərilsin? ✓</summary>
        [ObservableProperty] private bool yalnizGelecek;

        /// <summary>Cədvəl boşdur? ✓</summary>
        public bool Boshdur => Aylar.Count == 0;

        /// <summary>⚠️ Gecikmiş ay var? ✓</summary>
        public bool GecikmisAyVar => Aylar.Any(a => a.GecikmeVar);

        // ====================================================================
        //  🔄 YÜKLƏMƏ ✓✓✓
        // ====================================================================

        [RelayCommand]
        private Task Yenile() => LoadAsync();

        /// <summary>Bütün ödənişləri oxuyur və AYLAR üzrə cədvəli qurur ✓✓✓</summary>
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

                AylariQur();

                _logger.LogInformation(
                    "🗓️ Ödəniş təqvimi: {Ay} ay ✓ · cəmi {Cemi:N2} ₼ · ödənilmiş {Odenilmis:N2} ₼ · qalıq {Qaliq:N2} ₼ ✓",
                    Aylar.Count, ButunCemi, ButunOdenilmis, ButunQaliq);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🗓️ Ödəniş təqvimi yüklənə bilmədi ✗");
                ButunVeziyyet = "⚠ Yüklənə bilmədi ✗ — " + ex.Message;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>Aylıq xülasə cədvəlini qurur ✓ (köhnə seçim saxlanılır ✓).</summary>
        private void AylariQur()
        {
            var secilmisAyi = SecilmisAy?.Ay;

            var setirler = YalnizGelecek
                ? _butun.Where(b => b.Tarix.Date >= DateTime.Today).ToList()
                : _butun;

            Aylar.Clear();

            foreach (var ay in OdenisQrafiki.Aylar(setirler))
            {
                Aylar.Add(ay);
            }

            var hedef = secilmisAyi is DateTime saxlanan
                ? Aylar.FirstOrDefault(a => a.Ay == saxlanan)
                : null;

            hedef ??= Aylar.FirstOrDefault(a => a.CariAydir);

            // 🔔 Cari ay tapılmazsa → ən yaxın GƏLƏCƏK ay ✓ · yoxsa sonuncu ✓
            hedef ??= Aylar.FirstOrDefault(a => !a.KecmisAy) ?? Aylar.LastOrDefault();

            SecilmisAy = hedef;

            ButunCemi = Aylar.Sum(a => a.Cemi);
            ButunOdenilmis = Aylar.Sum(a => a.Odenilmis);
            ButunQaliq = Aylar.Sum(a => a.Qaliq);

            OnPropertyChanged(nameof(Boshdur));
            OnPropertyChanged(nameof(GecikmisAyVar));

            ButunVeziyyet = Aylar.Count == 0
                ? "🗓️ Ödəniş yoxdur ✓"
                : $"🗓️ {Aylar.Count} ay ✓ · cəmi {ButunCemi:N2} ₼ · " +
                  $"ödənilmiş {ButunOdenilmis:N2} ₼ · qalıq {ButunQaliq:N2} ₼ ✓";
        }

        /// <summary>Seçilmiş ay dəyişdikdə avtomobil + sətir cədvəlləri yenilənir ✓✓✓</summary>
        partial void OnSecilmisAyChanged(AyOdenisCemi? value)
        {
            AyAvtomobilleri.Clear();
            AySetirleri.Clear();

            if (value is null)
            {
                SecilmisAyAdi = "—";
                SecilmisAyCemi = 0m;
                SecilmisAyOdenilmis = 0m;
                SecilmisAyQaliq = 0m;
                SecilmisAyVeziyyet = "🗓️ Ay seçin ✓";
                return;
            }

            foreach (var avtomobil in OdenisQrafiki.Avtomobiller(value))
            {
                AyAvtomobilleri.Add(avtomobil);
            }

            foreach (var setir in value.Setirler)
            {
                AySetirleri.Add(setir);
            }

            SecilmisAyAdi = value.AyAdi;
            SecilmisAyCemi = value.Cemi;
            SecilmisAyOdenilmis = value.Odenilmis;
            SecilmisAyQaliq = value.Qaliq;

            SecilmisAyVeziyyet = value.GecikmeVar
                ? $"⚠ {value.AyAdi} — GECİKMİŞ ödəniş var ✗ · qalıq {value.Qaliq:N2} ₼"
                : $"🗓️ {value.AyAdi} · {value.SetirSayi} ödəniş · {value.MusteriSayi} müştəri · " +
                  $"{value.AvtomobilSayi} avtomobil · qalıq {value.Qaliq:N2} ₼ ✓";
        }

        /// <summary>🔍 Axtarış — bütün aylar üzrə sətirləri süzür ✓</summary>
        [RelayCommand]
        private void Axtar()
        {
            AxtarisSetirleri.Clear();

            if (string.IsNullOrWhiteSpace(Axtaris))
            {
                OnPropertyChanged(nameof(AxtarisVar));
                return;
            }

            var a = Axtaris.Trim().ToLowerInvariant();

            foreach (var s in _butun
                         .Where(b =>
                             (b.Mustar ?? "").ToLowerInvariant().Contains(a)
                             || (b.Muqavile ?? "").ToLowerInvariant().Contains(a)
                             || (b.Avtomobil ?? "").ToLowerInvariant().Contains(a)
                             || (b.Tarix.ToString("dd.MM.yyyy").Contains(a))
                             || b.Tarix.ToString("MMMM", new System.Globalization.CultureInfo("az-Latn-AZ"))
                                 .ToLowerInvariant().Contains(a))
                         .OrderBy(b => b.Tarix))
            {
                AxtarisSetirleri.Add(s);
            }

            OnPropertyChanged(nameof(AxtarisVar));
        }

        /// <summary>Axtarış nəticəsi var? ✓</summary>
        public bool AxtarisVar => AxtarisSetirleri.Count > 0;

        partial void OnAxtarisChanged(string value) => Axtar();

        partial void OnYalnizGelecekChanged(bool value) => AylariQur();
    }
}
