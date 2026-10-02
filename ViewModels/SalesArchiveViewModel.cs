using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// "Satılan &amp; Krediti Bitmiş" bölməsinin ViewModel-i.
    /// Satılan avtomobillər və müddəti bitmiş/bağlanmış kreditlər buraya avtomatik düşür.
    /// </summary>
    public sealed partial class SalesArchiveViewModel : ObservableObject
    {
        private readonly ICarService _carService;
        private readonly ICreditService _creditService;
        private readonly IMediaService _media;
        private readonly ISaleService _saleService;
        private readonly IDialogService _dialogs;
        private readonly ILogger<SalesArchiveViewModel> _logger;

        /// <summary>Eyni anda iki yükləmənin işləməsinin qarşısını alır (siyahıların ikiqat olmasını önləyir).</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Filtrasiya zamanı geri-əlaqə (re-entrancy) dövrünün qarşısını alır.</summary>
        private bool _applyingFilter;

        public ObservableCollection<CarItem> SoldCars { get; } = new();
        public ObservableCollection<Credit> CompletedCredits { get; } = new();

        /// <summary>Axtarışa uyğun satılan avtomobillər.</summary>
        public ObservableCollection<CarItem> FilteredSoldCars { get; } = new();

        /// <summary>Axtarışa uyğun krediti bitmiş müqavilələr.</summary>
        public ObservableCollection<Credit> FilteredCompletedCredits { get; } = new();

        [ObservableProperty] private string soldCarSearchText = string.Empty;
        [ObservableProperty] private string creditSearchText = string.Empty;

        [ObservableProperty] private CarItem? selectedSoldCar;

        /// <summary>
        /// 📕 «Krediti Bitmiş / Bağlı» cədvəlində SEÇİLMİŞ kredit ✓✓✓
        /// (geri qaytarma düyməsi bu kreditin maşınını götürür ✓).
        /// </summary>
        [ObservableProperty] private Credit? selectedCompletedCredit;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private int soldCarsCount;
        [ObservableProperty] private int completedCreditsCount;
        [ObservableProperty] private decimal totalSoldRevenue;
        [ObservableProperty] private decimal totalSoldProfit;

        /// <summary>Arxivdə dəyişiklik olduqda (məs. avtomobil geri qaytarıldı) baş verir.</summary>
        public event EventHandler? DataChanged;

        public SalesArchiveViewModel(
            ICarService carService,
            ICreditService creditService,
            IMediaService media,
            ISaleService saleService,
            IDialogService dialogs,
            ILogger<SalesArchiveViewModel> logger)
        {
            _carService = carService;
            _creditService = creditService;
            _media = media;
            _saleService = saleService;
            _dialogs = dialogs;
            _logger = logger;
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                // ================================================================
                //  ✅ KREDİTİ OLAN AVTOMOBİL «SATILAN» SİYAHISINA DÜŞMÜR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL «📕 Vaxtından tez bağlanmış» / krediti bitmiş maşın
                //    HƏM «🗂️ Satılan Avtomobillər»-də ✗ HƏM «✅ Krediti Bitmiş»-də
                //    görünürdü ✗ → İKİ YERDƏ TƏKRAR ✗ (istifadəçi şikayəti ✓)
                //  ✅ İNDİ: kredit müqaviləsi olan maşın YALNIZ «✅ Krediti
                //    Bitmiş» bölməsindədir ✓✓✓ («Satılan»-da YOX ✗)
                // ================================================================
                var credits = await _creditService.GetCreditsAsync();

                var kreditliCarIds = credits
                    .Where(c => c.CarId.HasValue)
                    .Select(c => c.CarId!.Value)
                    .ToHashSet();

                var sold = (await _carService.GetSoldCarsAsync())
                    .Where(car => !kreditliCarIds.Contains(car.Id))    // ✅ kreditli ✗
                    .ToList();

                SoldCars.Clear();
                foreach (var car in sold)
                {
                    SoldCars.Add(car);
                }
                SoldCarsCount = SoldCars.Count;

                // Hər satılan avtomobil üçün sənəd sayı göstərilir.
                var counts = await _media.GetCountsAsync(MediaRefTypes.Car);
                foreach (var car in SoldCars)
                {
                    car.SenedSayi = counts.TryGetValue(car.Id, out var count) ? count : 0;
                }

                ApplySoldCarFilter();

                CompletedCredits.Clear();
                foreach (var credit in credits.Where(IsCompleted))
                {
                    CompletedCredits.Add(credit);
                }
                CompletedCreditsCount = CompletedCredits.Count;
                ApplyCompletedCreditFilter();

                var sales = await _saleService.GetSalesAsync();
                var stats = _saleService.BuildStats(sales);
                TotalSoldRevenue = stats.TotalRevenue;
                TotalSoldProfit = stats.TotalProfit;

                _logger.LogInformation(
                    "Arxiv yükləndi: {Sold} satılan avtomobil, {Credits} bitmiş kredit.",
                    SoldCarsCount, CompletedCreditsCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Arxiv yüklənərkən xəta baş verdi.");
                _dialogs.ShowError("Arxiv yüklənə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        /// <summary>Kredit bitmiş sayılır: status "Bağlı" və ya müddəti tamamlanıb.</summary>
        private static bool IsCompleted(Credit credit)
            => credit.Status == "Bağlı"
               || credit.BaslamaTarixi.AddMonths(credit.MuddetAy) <= DateTime.Today;

        // ================================================================
        //  🖱️ İKİ DƏFƏ KLİK → TAM DETAL PƏNCƏRƏSİ ✓✓✓
        // ----------------------------------------------------------------
        //  Sətrə iki dəfə basanda avtomobilin / kreditin BÜTÜN məlumatı
        //  (🚘 avtomobil · 🧾 xərclər · 💳 kredit · 📋 hərəkətlər ·
        //   👥 tərəfdaş payları) görünür ✓ və oradan
        //  «📄 PDF İXRAC ET» ilə PEŞƏKAR sənəd alınır ✓
        //  (💾 hara yadda saxlanacağını istifadəçi ÖZÜ seçir ✓✓✓)
        // ================================================================

        /// <summary>🚘 Seçilmiş SATILAN avtomobilin tam detalını açır ✓✓✓</summary>
        [RelayCommand]
        private async Task AvtomobilDetayAcAsync()
        {
            if (SelectedSoldCar is not CarItem car)
            {
                return;
            }

            var credits = await _creditService.GetCreditsAsync();
            var credit = credits.FirstOrDefault(c => c.CarId == car.Id);

            await DetayAcAsync(car, credit);
        }

        /// <summary>✅ Seçilmiş KREDİTİ BİTMİŞ müqavilənin tam detalını açır ✓✓✓</summary>
        [RelayCommand]
        private async Task KreditDetayAcAsync()
        {
            if (SelectedCompletedCredit is not Credit credit)
            {
                return;
            }

            var car = credit.Car;

            if (car is null && credit.CarId is int carId)
            {
                var hamisi = await _carService.GetAllCarsAsync();
                car = hamisi.FirstOrDefault(c => c.Id == carId);
            }

            if (car is null)
            {
                _dialogs.ShowWarning("Müqaviləyə bağlı avtomobil tapılmadı.");
                return;
            }

            await DetayAcAsync(car, credit);
        }

        /// <summary>🗄️ Detal pəncərəsini BÜTÜN məlumatla açır ✓✓✓</summary>
        private async Task DetayAcAsync(CarItem car, Credit? credit)
        {
            IReadOnlyList<CreditTransaction> hereketler = Array.Empty<CreditTransaction>();
            IReadOnlyList<PartnerShare> paylar = Array.Empty<PartnerShare>();
            IReadOnlyList<Sale> satislar = Array.Empty<Sale>();

            // ================================================================
            //  ✅ SATIŞ QEYDLƏRİ — «NECƏYƏ SATILIB» ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL satış qeydləri PDF-ə ÖTÜRÜLMÜRDÜ ✗ → sənəddə
            //    satış qiyməti / müştəri / ödəniş üsulu / barter görünmürdü ✗✓✓
            //  ✅ İNDİ avtomobilin BÜTÜN satış qeydləri PDF-ə verilir ✓
            // ================================================================
            try
            {
                var butunSatislar = await _saleService.GetSalesAsync();

                satislar = butunSatislar
                    .Where(s => s.CarId == car.Id)
                    .OrderBy(s => s.SatisTarixi)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Satış qeydləri sənəd üçün yüklənə bilmədi.");
            }

            // ================================================================
            //  📎 SƏNƏDLƏR (MEDIA ARXİVİ) ✓✓✓
            // ----------------------------------------------------------------
            //  Avtomobilə bağlı NEÇƏ SƏNƏD VARSA HAMISI göstərilir ✓
            //  (qaimə · çek · müqavilə · şəkil · sığorta ✓)
            //  → detallar pəncərəsində iki dəfə klikləyərək AÇMAQ olar ✓✓✓
            // ================================================================
            IReadOnlyList<MediaAttachment> senedler = Array.Empty<MediaAttachment>();

            try
            {
                senedler = await _media.GetAsync(MediaRefTypes.Car, car.Id);

                _logger.LogInformation(
                    "📎 «{Car}» üçün {Say} sənəd tapıldı ✓", car.DisplayName, senedler.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Sənədlər detallar üçün yüklənə bilmədi.");
            }

            if (credit is not null)
            {
                var hamisi = await _creditService.GetTransactionsAsync();

                hereketler = hamisi
                    .Where(t => t.CreditId == credit.Id)
                    .OrderBy(t => t.Tarix)
                    .ToList();

                var xerite = await _creditService.GetCreditSharesMapAsync();
                if (xerite.TryGetValue(credit.Id, out var kreditPaylari))
                {
                    paylar = kreditPaylari;
                }
            }

            var detalPenceresi = new Views.ArchiveDetayWindow(_dialogs)
            {
                DataContext = new Views.ArchiveDetayVeri
                {
                    Car = car,
                    Credit = credit,
                    Hereketler = hereketler,
                    Paylar = paylar,
                    Satislar = satislar,          // 💰 necəyə satılıb ✓✓✓
                    Senedler = senedler,          // 📎 bütün sənədlər ✓✓✓
                    SenedQovlugu = _media.GetFolderPath(MediaRefTypes.Car, car.Id)
                },
                Owner = System.Windows.Application.Current?.MainWindow
            };

            detalPenceresi.ShowDialog();
        }

        partial void OnSoldCarSearchTextChanged(string value) => ApplySoldCarFilter();

        partial void OnCreditSearchTextChanged(string value) => ApplyCompletedCreditFilter();

        /// <summary>Satılan avtomobilləri yazıldıqca filtrləyir (marka, dövlət nömrəsi, VIN).</summary>
        private void ApplySoldCarFilter()
        {
            if (_applyingFilter)
            {
                return;
            }

            _applyingFilter = true;
            try
            {
                var search = SoldCarSearchText?.Trim() ?? string.Empty;

                var matches = (string.IsNullOrEmpty(search)
                    ? SoldCars
                    : SoldCars.Where(c =>
                        (c.Marka ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (c.QeydiyyatNisani ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (c.Vin ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (c.DisplayName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();

                if (matches.Count == FilteredSoldCars.Count && matches.SequenceEqual(FilteredSoldCars))
                {
                    OnPropertyChanged(nameof(SoldCarCountText));
                    return;
                }

                FilteredSoldCars.Clear();
                foreach (var car in matches)
                {
                    FilteredSoldCars.Add(car);
                }

                OnPropertyChanged(nameof(SoldCarCountText));
            }
            finally
            {
                _applyingFilter = false;
            }
        }

        /// <summary>
        /// Krediti bitmiş müqavilələri yazıldıqca filtrləyir
        /// (müqavilə №, müştəri, avtomobil markası / nömrəsi, status).
        /// </summary>
        private void ApplyCompletedCreditFilter()
        {
            if (_applyingFilter)
            {
                return;
            }

            _applyingFilter = true;
            try
            {
                var search = CreditSearchText?.Trim() ?? string.Empty;

                var matches = (string.IsNullOrEmpty(search)
                    ? CompletedCredits
                    : CompletedCredits.Where(c =>
                        (c.MuqavileNomresi ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (c.Mustəri ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (c.Status ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (c.Car is not null && (
                            (c.Car.DisplayName ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            (c.Car.Marka ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            (c.Car.QeydiyyatNisani ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            (c.Car.Vin ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase))))).ToList();

                if (matches.Count == FilteredCompletedCredits.Count && matches.SequenceEqual(FilteredCompletedCredits))
                {
                    OnPropertyChanged(nameof(CompletedCreditCountText));
                    return;
                }

                FilteredCompletedCredits.Clear();
                foreach (var credit in matches)
                {
                    FilteredCompletedCredits.Add(credit);
                }

                OnPropertyChanged(nameof(CompletedCreditCountText));
            }
            finally
            {
                _applyingFilter = false;
            }
        }

        /// <summary>Satılan avtomobil cədvəlinin başlığındaki say.</summary>
        public string SoldCarCountText => string.IsNullOrWhiteSpace(SoldCarSearchText)
            ? $"{SoldCars.Count} avtomobil"
            : $"{FilteredSoldCars.Count} / {SoldCars.Count} avtomobil";

        /// <summary>Krediti bitmiş cədvəlinin başlığındaki say.</summary>
        public string CompletedCreditCountText => string.IsNullOrWhiteSpace(CreditSearchText)
            ? $"{CompletedCredits.Count} kredit"
            : $"{FilteredCompletedCredits.Count} / {CompletedCredits.Count} kredit";

        /// <summary>Satılan avtomobil axtarışını təmizləyir.</summary>
        [RelayCommand]
        private void ClearSoldCarSearch()
        {
            SoldCarSearchText = string.Empty;
            ApplySoldCarFilter();
        }

        /// <summary>Krediti bitmiş axtarışını təmizləyir.</summary>
        [RelayCommand]
        private void ClearCreditSearch()
        {
            CreditSearchText = string.Empty;
            ApplyCompletedCreditFilter();
        }

        /// <summary>Satılmış avtomobili yenidən aktiv parka qaytarır (satış qeydi silinir).</summary>
        [RelayCommand]
        private async Task RestoreCarAsync()
        {
            // ================================================================
            //  ✅ ƏVVƏLCƏ satılan maşın ✓, YOXDURSA «Krediti Bitmiş» seçilmiş
            //  KREDİT-in maşını ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏLKİ SƏHV: yalnız `SelectedSoldCar` yoxlanılırdı ✗ →
            //  «Krediti Bitmiş» cədvəlindən geri qaytarmaq istəyəndə
            //  «siyahıdan avtomobil seçin» deyirdi ✗ (maşın seçilmiş olsa da ✗✓✓)
            // ================================================================
            var car = SelectedSoldCar ?? SelectedCompletedCredit?.Car;

            if (car is null)
            {
                _dialogs.ShowWarning("Geri qaytarmaq üçün siyahıdan avtomobil seçin.");
                return;
            }

            if (!_dialogs.Confirm(
                $"\"{car.DisplayName}\" avtomobilini yenidən aktiv parka qaytarmaq istəyirsiniz?\n(Satış qeydi silinəcək.)"))
            {
                return;
            }

            try
            {
                IsBusy = true;

                // 1️⃣ Satış qeydləri silinir (birbaşa bazadan — tracker-safe ✓)
                await _saleService.DeleteSalesByCarAsync(car.Id);

                // ================================================================
                //  3️⃣ 🔓 KREDİT «Bağlı» idisə YENİDƏN «Aktiv» EDİLİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ SIRA VACİBDİR ✗: əvvəl kredit açılır ✓ → sonra maşının
                //    statusu ona uyğun təyin olunur ✓
                // ================================================================
                var kreditAcildi = await _creditService.ReopenCreditByCarAsync(car.Id);

                // ================================================================
                //  2️⃣ 🔑 AVTOMOBİLİN STATUSU — KREDİT VARSa «KREDİTDƏ» ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL həmişə `Catalog.StockStatus` («Stokda») yazılırdı ✗ →
                //    krediti açılmış maşın
                //      ① «🚘 Avto Park» cədvəlinə DÜŞÜRDÜ ✗  (satış bölməsi ✗)
                //      ② «💳 Kreditlər» bölməsinə DƏ düşürdü ✓
                //    → İKİ YERDƏ görünürdü ✗✓✓ (istifadəçi şikayəti ✓)
                //  ✅ İNDİ:
                //      • kredit açıldısa → status **«Kreditdə»** ✓ →
                //        maşın YALNIZ «💳 Kreditlər» bölməsindədir ✓✓✓
                //        (park cədvəlinə DÜŞMÜR ✗ — «Kreditdə» parkdankənardır ✓)
                //      • kredit yoxdursa → «Stokda» ✓ → parka qayıdır ✓
                // ================================================================
                var hedefStatus = kreditAcildi ? Catalog.CreditStatus : Catalog.StockStatus;

                var deyisdi = await _carService.SetStatusAsync(car.Id, hedefStatus);

                if (!deyisdi)
                {
                    _dialogs.ShowWarning(
                        "Avtomobil bazada tapılmadı — status dəyişdirilə bilmədi.\n" +
                        "Səhifəni yeniləyib təkrar yoxlayın.");
                    return;
                }

                SelectedSoldCar = null;

                _logger.LogInformation(
                    "✅ Avtomobil arxivdən geri qaytarıldı: {Car} (status → {Status}, kredit yenidən açıldı: {Kredit})",
                    car.DisplayName, hedefStatus, kreditAcildi);

                _dialogs.ShowInfo(
                    $"✅ \"{car.DisplayName}\" geri qaytarıldı.\n\n" +
                    (kreditAcildi
                        ? $"• Status: {hedefStatus}  →  YALNIZ «💳 Kreditlər» bölməsindədir ✓\n" +
                          "  (avto park cədvəlinə düşmür ✓ — krediti aktivdir ✓)\n"
                        : $"• Status: {hedefStatus}  →  AVTO PARKA əlavə edildi ✓\n") +
                    "• Satış qeydi silindi: ✓\n" +
                    (kreditAcildi ? "• Kredit yenidən AÇILDI: ✓" : "• Kredit: dəyişmədi"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Avtomobil geri qaytarılarkən xəta baş verdi.");
                _dialogs.ShowError("Əməliyyat alınmadı: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 🗑️ <b>SEÇİLMİŞ AVTOMOBİLİ TAM SİL</b> ✓✓✓ (v6.2.15)
        /// <para>
        /// İstifadəçi tələbi: «Satılan və krediti bitmiş maşın tabından
        /// silinəndə maşın SATIŞ GƏLİRİ də avtomatik silinməlidir» ✓
        /// </para>
        /// <para>
        /// <see cref="ICarService.DeleteCarAsync"/> BÜTÜN zənciri silir ✓:
        /// satış qeydləri ✓ · satışın kassa gəliri ✓ · kreditlər ✓ ·
        /// kredit ödənişləri ✓ · kassa gəlirləri ✓ · xərclər ✓ ·
        /// möhlətlər ✓ · sənəd faylları ✓ · avtomobilin özü ✓✓✓
        /// (həm də «🗑 Silinənlər» bölməsindən geri qaytarıla bilər ✓)
        /// </para>
        /// </summary>
        [RelayCommand]
        private async Task DeleteCarAsync()
        {
            var car = SelectedSoldCar ?? SelectedCompletedCredit?.Car;

            if (car is null)
            {
                _dialogs.ShowWarning("Silmək üçün siyahıdan avtomobil seçin.");
                return;
            }

            if (!_dialogs.Confirm(
                $"⚠️ «{car.DisplayName}» avtomobilini TAM SİLMƏK istəyirsiniz?\n\n" +
                "Bunlarla BİRLİKDƏ silinəcək:\n" +
                "• 💰 maşının satış gəliri (kassa daxilolması)\n" +
                "• ⏳ ödəniş möhlətləri\n" +
                "• 💳 kredit müqaviləsi və bütün ödənişləri\n" +
                "• 🧾 avtomobilə aid bütün xərclər\n" +
                "• 📄 sənəd faylları\n\n" +
                "ℹ️ Səhv olarsa «🗑 Silinənlər» bölməsindən geri qaytara bilərsiniz."))
            {
                return;
            }

            try
            {
                IsBusy = true;

                var ad = car.DisplayName;
                await _carService.DeleteCarAsync(car.Id);

                SelectedSoldCar = null;
                SelectedCompletedCredit = null;

                _logger.LogInformation("🗑️ Arxivdən avtomobil tam silindi: {Car}", ad);

                _dialogs.ShowInfo(
                    $"🗑️ «{ad}» TAM SİLİNDİ ✓\n\n" +
                    "• Satış gəliri · kassa daxilolması: silindi ✓\n" +
                    "• Kredit və ödənişləri: silindi ✓\n" +
                    "• Xərclər · möhlətlər · sənədlər: silindi ✓\n\n" +
                    "ℹ️ «🗑 Silinənlər» bölməsindən geri qaytara bilərsiniz.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Arxivdən avtomobil silinərkən xəta baş verdi.");
                _dialogs.ShowError("Silmə alınmadı: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
