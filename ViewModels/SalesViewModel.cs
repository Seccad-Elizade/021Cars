using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>"Satış" tabının ViewModel-i.</summary>
    public sealed partial class SalesViewModel : ObservableValidator
    {
        private readonly ISaleService _saleService;
        private readonly ICarService _carService;
        private readonly IDialogService _dialogs;
        private readonly ILogger<SalesViewModel> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Filtrləmə zamanı geri-əlaqə (re-entrancy) dövrünün qarşısını alır.</summary>
        private bool _applyingFilter;

        public ObservableCollection<Sale> Sales { get; } = new();
        public ObservableCollection<CarItem> AvailableCars { get; } = new();

        /// <summary>Axtarış mətninə uyğun avtomobillər (canlı filtrasiya).</summary>
        public ObservableCollection<CarItem> FilteredCars { get; } = new();

        [ObservableProperty] private string carSearchText = string.Empty;

        public IReadOnlyList<string> PaymentMethods { get; } = Catalog.SalePaymentMethods;

        [ObservableProperty] private Sale? selectedSale;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private int totalSales;
        [ObservableProperty] private decimal totalRevenue;
        [ObservableProperty] private decimal totalProfit;
        [ObservableProperty] private decimal monthRevenue;
        [ObservableProperty] private int barterSales;
        [ObservableProperty] private decimal barterRevenue;

        // ---- Forma sahələri ----
        [ObservableProperty] private CarItem? selectedCar;

        [ObservableProperty]
        [Required(ErrorMessage = "Alıcının adı tələb olunur.")]
        [MaxLength(150, ErrorMessage = "Alıcı adı 150 simvoldan çox ola bilməz.")]
        private string musteri = string.Empty;

        [ObservableProperty]
        [MaxLength(60, ErrorMessage = "Müqavilə nömrəsi 60 simvoldan çox ola bilməz.")]
        private string muqavileNomresi = string.Empty;

        [ObservableProperty]
        [Range(typeof(decimal), "1", "999999999", ErrorMessage = "Satış qiyməti 0-dan böyük olmalıdır.")]
        [NotifyPropertyChangedFor(nameof(BarterCashPart))]
        [NotifyPropertyChangedFor(nameof(BarterSummary))]
        private decimal satisQiymeti;

        [ObservableProperty]
        [Required(ErrorMessage = "Satış tarixi seçilməlidir.")]
        private DateTime? satisTarixi = DateTime.Today;

        [ObservableProperty] private string odenisUsulu = Catalog.SalePaymentMethods[0];

        /// <summary>Barter zamanı alıcının verdiyi maşının dəyəri (AZN).</summary>
        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Barter hissəsi mənfi ola bilməz.")]
        [NotifyPropertyChangedFor(nameof(BarterCashPart))]
        [NotifyPropertyChangedFor(nameof(BarterSummary))]
        private decimal barterMebleg;

        /// <summary>Barter zamanı alıcının verdiyi maşının təsviri.</summary>
        [ObservableProperty]
        [MaxLength(500, ErrorMessage = "Barter təsviri 500 simvoldan çox ola bilməz.")]
        private string barterTesviri = string.Empty;

        // ---- Alıcının verdiyi maşının TAM məlumatı (parka əlavə olunur) ----

        /// <summary>Alıcının verdiyi maşının markası. Boşdursa parka əlavə edilmir.</summary>
        [ObservableProperty]
        [MaxLength(150, ErrorMessage = "Marka 150 simvoldan çox ola bilməz.")]
        private string receivedMarka = string.Empty;

        /// <summary>Alıcının verdiyi maşının dövlət nömrəsi.</summary>
        [ObservableProperty]
        [MaxLength(20, ErrorMessage = "Nömrə 20 simvoldan çox ola bilməz.")]
        private string receivedNisan = string.Empty;

        /// <summary>Alıcının verdiyi maşının buraxılış ili.</summary>
        [ObservableProperty]
        [Range(1900, 2100, ErrorMessage = "İl 1900-2100 aralığında olmalıdır.")]
        private int receivedIl = DateTime.Today.Year;

        /// <summary>Alıcının verdiyi maşının yürüşü (km).</summary>
        [ObservableProperty]
        [Range(0, 9_999_999, ErrorMessage = "Yürüş mənfi ola bilməz.")]
        private int receivedYurus;

        /// <summary>Alıcının verdiyi maşının yanacaq növü.</summary>
        [ObservableProperty]
        private string receivedYanacaq = Catalog.FuelTypes[0];

        /// <summary>Alıcının verdiyi maşın üçün nəzərdə tutulan satış qiyməti (₼).</summary>
        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Satış qiyməti mənfi ola bilməz.")]
        private decimal receivedSatisQiymeti;

        /// <summary>Yanacaq növləri (ComboBox məzmunu).</summary>
        public IReadOnlyList<string> FuelTypes { get; } = Catalog.FuelTypes;

        /// <summary>Satış barter ilə edilirmi? — XAML-də əlavə sahələri göstərmək üçün.</summary>
        public bool IsBarter => string.Equals(OdenisUsulu, Catalog.BarterSale, StringComparison.Ordinal);

        /// <summary>Barter satışında nağd / köçürmə ilə ödənilən hissə.</summary>
        public decimal BarterCashPart => Math.Max(0m, SatisQiymeti - BarterMebleg);

        /// <summary>Barter hesablaşmasının canlı izahı (formada göstərilir).</summary>
        public string BarterSummary =>
            IsBarter && BarterMebleg > 0m
                ? $"🔄 {BarterMebleg:N2} ₼ maşınla  +  💵 {BarterCashPart:N2} ₼ nağd"
                : string.Empty;

        /// <summary>Ödəniş üsulu dəyişdikdə barter sahələri təmizlənir.</summary>
        partial void OnOdenisUsuluChanged(string value)
        {
            if (!string.Equals(value, Catalog.BarterSale, StringComparison.Ordinal))
            {
                BarterMebleg = 0m;
                BarterTesviri = string.Empty;
            }

            OnPropertyChanged(nameof(IsBarter));
            OnPropertyChanged(nameof(BarterCashPart));
            OnPropertyChanged(nameof(BarterSummary));
        }

        [ObservableProperty]
        [MaxLength(300, ErrorMessage = "Qeyd 300 simvoldan çox ola bilməz.")]
        private string qeyd = string.Empty;

        public SalesViewModel(
            ISaleService saleService,
            ICarService carService,
            IDialogService dialogs,
            ILogger<SalesViewModel> logger)
        {
            _saleService = saleService;
            _carService = carService;
            _dialogs = dialogs;
            _logger = logger;
        }

        /// <summary>Avtomobil seçildikdə əvvəlcə avtomobilin satış qiyməti, yoxdursa maya dəyəri təklif olunur.</summary>
        partial void OnSelectedCarChanged(CarItem? value)
        {
            if (value is null)
            {
                return;
            }

            var suggested = value.SatisQiymeti > 0m ? value.SatisQiymeti : value.MayaDeyeri;
            if (SatisQiymeti <= 0m)
            {
                SatisQiymeti = suggested;
            }

            CarSearchText = value.DisplayName;

            // Maya dəyəri dəyişdi → bölgü bazası yenilənir.
            OnPropertyChanged(nameof(MayaDeyeri));
            OnPropertyChanged(nameof(SatisXeyirMetni));
            if (SatisBolguTetbiq)
            {
                SatisBolguHesabla();
            }
        }

        partial void OnCarSearchTextChanged(string value) => ApplyCarFilter();

        /// <summary>Yazıldıqca avtomobil siyahısını filtrləyir (marka, dövlət nömrəsi, VIN).</summary>
        private void ApplyCarFilter()
        {
            if (_applyingFilter)
            {
                return;
            }

            _applyingFilter = true;
            try
            {
                var search = CarSearchText?.Trim() ?? string.Empty;

                if (SelectedCar is not null &&
                    string.Equals(search, SelectedCar.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    search = string.Empty;
                }

                var matches = (string.IsNullOrEmpty(search)
                    ? AvailableCars
                    : AvailableCars.Where(c =>
                        c.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.Marka.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.QeydiyyatNisani.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.Vin.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();

                // Nəticə əvvəlki ilə eynidirsə siyahını yenidən qurmuruq.
                if (matches.Count == FilteredCars.Count && matches.SequenceEqual(FilteredCars))
                {
                    return;
                }

                FilteredCars.Clear();
                foreach (var car in matches)
                {
                    FilteredCars.Add(car);
                }
            }
            finally
            {
                _applyingFilter = false;
            }
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                var sales = await _saleService.GetSalesAsync();
                Sales.Clear();
                foreach (var sale in sales)
                {
                    Sales.Add(sale);
                }
                UpdateStats();

                var cars = await _carService.GetCarsAsync();
                AvailableCars.Clear();
                foreach (var car in cars.Where(c => c.Status != Catalog.SoldStatus))
                {
                    AvailableCars.Add(car);
                }
                ApplyCarFilter();

                _logger.LogInformation("{Count} satış qeydi yükləndi.", Sales.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Satışlar yüklənərkən xəta baş verdi.");
                _dialogs.ShowError("Satış siyahısı yüklənə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        /// <summary>Satışlar dəyişdikdə baş verir (maliyyə panelini yeniləmək üçün).</summary>
        public event EventHandler? SalesChanged;

        [RelayCommand]
        private async Task AddSaleAsync()
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                _dialogs.ShowWarning("Zəhmət olmasa formada göstərilən səhvləri düzəldin.");
                return;
            }

            if (SelectedCar is null)
            {
                _dialogs.ShowWarning("Satış üçün avtomobil seçilməlidir.");
                return;
            }

            var added = false;
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                var sale = new Sale
                {
                    MuqavileNomresi = MuqavileNomresi.Trim(),
                    Mustəri = Musteri.Trim(),
                    CarId = SelectedCar.Id,
                    SatisQiymeti = SatisQiymeti,
                    SatisTarixi = SatisTarixi ?? DateTime.Today,
                    OdenisUsulu = OdenisUsulu,
                    BarterMebleg = IsBarter ? BarterMebleg : 0m,
                    BarterTesviri = IsBarter ? BarterTesviri.Trim() : string.Empty,
                    ReceivedCar = IsBarter ? BuildReceivedCar() : null,
                    Qeyd = Qeyd.Trim()
                };

                await _saleService.AddSaleAsync(sale);
                added = true;

                // 👥 Tərəfdaş bölgüsü tətbiq olunubsa — satışın mənfəət payları yazılır
                //    (XEYİR = Satış qiyməti − Maya dəyəri).
                if (SatisBolguTetbiq && sale.Id > 0)
                {
                    SatisBolguHesabla();
                    var paylar = SatisTerefdaslari.Select((r, i) => r.ToModel(i)).ToList();
                    await _saleService.SaveSaleSharesAsync(sale.Id, true, paylar);

                    _logger.LogInformation(
                        "👥 Satış bölgüsü saxlanıldı: satış #{Id}, xeyir {Baza:N2} ₼, cəm {Cem:N2} ₼",
                        sale.Id, SatisBolguBazasi, SatisBolguCemi);
                }

                _logger.LogInformation("Satış qeydə alındı: {Car} -> {Buyer} ({Price} AZN)",
                    SelectedCar.DisplayName, sale.Mustəri, sale.SatisQiymeti);
                _dialogs.ShowInfo(BuildSaleResultMessage(sale));
                ClearForm();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Satış əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Satış qeydə alına bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            if (added)
            {
                await LoadAsync();
                SalesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        private async Task DeleteSaleAsync()
        {
            if (SelectedSale is null)
            {
                _dialogs.ShowWarning("Silmək üçün siyahıdan satış seçin.");
                return;
            }

            var sale = SelectedSale;
            if (!_dialogs.Confirm($"\"{sale.CarInfo}\" satışını silmək istəyirsiniz?"))
            {
                return;
            }

            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                await _saleService.DeleteSaleAsync(sale.Id);
                SelectedSale = null;
                _logger.LogInformation("Satış silindi: {Id}", sale.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Satış silinərkən xəta baş verdi.");
                _dialogs.ShowError("Satış silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            await LoadAsync();
            SalesChanged?.Invoke(this, EventArgs.Empty);
        }

        // ====================================================================
        //  👥 SATIŞIN TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ
        // --------------------------------------------------------------------
        //  Maşın SATILANDA mənfəəti («xeyir») tərəfdaşlar arasında bölünür:
        //
        //      XEYİR = Satış qiyməti − Maya dəyəri
        //
        //  Nümunə: 22 014 − 20 420 = 1 594 ₼  → bu bölünür
        //
        //  ☑ işarəsi qoyulan tərəfdaşların faizi hesablanır; işarəsizlər
        //  İŞTİRAK ETMİR. Qalan məbləği Asif & Musa yarı-yarıya bölür.
        // ====================================================================

        /// <summary>Satışın tərəfdaş payları (adları ilə).</summary>
        public ObservableCollection<PartnerPayRow> SatisTerefdaslari { get; } = new();

        /// <summary>Seçilmiş avtomobilin MAYA DƏYƏRİ (bölgü bazası üçün).</summary>
        public decimal MayaDeyeri => SelectedCar?.MayaDeyeri ?? 0m;

        /// <summary>Bölgü tətbiq olunur? (checkbox)</summary>
        [ObservableProperty] private bool satisBolguTetbiq;

        /// <summary>Bölgü bazası («xeyir») — Satış qiyməti − Maya dəyəri.</summary>
        [ObservableProperty] private decimal satisBolguBazasi;

        /// <summary>Payların cəmi (₼).</summary>
        public decimal SatisBolguCemi => SatisTerefdaslari.Sum(r => r.Mebleg);

        /// <summary>Cəm ilə baza arasındaki fərq.</summary>
        public decimal SatisBolguFergi => SatisBolguCemi - SatisBolguBazasi;

        /// <summary>Xeyir mətni: «Xeyir = Satış − Maya = 22 014 − 20 420 = 1 594 ₼».</summary>
        public string SatisXeyirMetni
        {
            get
            {
                var satis = SatisQiymeti;
                var maya = MayaDeyeri;
                return $"Xeyir = Satış qiyməti − Maya = {satis:N2} − {maya:N2} = {SatisBolguBazasi:N2} ₼";
            }
        }

        /// <summary>Vəziyyət mətni.</summary>
        public string SatisBolguVeziyyet => SatisBolguTetbiq ? "✅ Tətbiq olunub" : "⬜ Tətbiq olunmayıb";

        /// <summary>Xülasə: «Zaur 95,40 ₼ · Eşqin 79,50 ₼ · …».</summary>
        public string SatisBolguXulase => SatisTerefdaslari.Count == 0
            ? "Bölgü hazır deyil"
            : string.Join(" · ", SatisTerefdaslari.Select(r => $"{r.Terefdas} {r.Mebleg:N2} ₼"));

        /// <summary>Fərq mətni.</summary>
        public string SatisBolguFergMetni => SatisBolguFergi == 0m
            ? "✓ Dəqiq bölünüb — fərq yoxdur"
            : $"⚠ Fərq: {SatisBolguFergi:N2} ₼";

        /// <summary>Fərq rəngi.</summary>
        public string SatisBolguFergReng => SatisBolguFergi == 0m ? "#34D399" : "#F43F5E";

        /// <summary>
        /// SATIŞIN BÖLGÜ BAZASI = <b>Satış qiyməti − Maya dəyəri</b> («xeyir»).
        /// </summary>
        [RelayCommand]
        private void SatisBolguHesabla()
        {
            // Sətirlər hazır deyilsə — standart faizlərlə doldurulur.
            if (SatisTerefdaslari.Count == 0)
            {
                foreach (var share in PartnerMath.CreateDefaultRows())
                {
                    SatisTerefdaslari.Add(PartnerPayRow.FromModel(share));
                }
            }

            var baza = Math.Round(SatisQiymeti - MayaDeyeri, 2);
            SatisBolguBazasi = baza > 0m ? baza : 0m;

            var modeller = new List<PartnerShare>();
            var sira = 0;
            foreach (var row in SatisTerefdaslari)
            {
                modeller.Add(row.ToModel(sira++));
            }

            PartnerMath.Distribute(SatisBolguBazasi, modeller);

            for (var i = 0; i < modeller.Count && i < SatisTerefdaslari.Count; i++)
            {
                SatisTerefdaslari[i].Mebleg = modeller[i].Mebleg;
            }

            SatisBolguYenile();
        }

        /// <summary>Faiz dərəcələrini standart qiymətlərə qaytarır.</summary>
        [RelayCommand]
        private void SatisBolguDefault()
        {
            var standart = PartnerMath.CreateDefaultRows();

            foreach (var row in SatisTerefdaslari)
            {
                var uygun = standart.FirstOrDefault(d => d.Terefdas == row.Terefdas);
                if (uygun is not null)
                {
                    row.Faiz = uygun.Faiz;
                    row.QaligPayi = uygun.QaligPayi;
                    row.Aktiv = true;
                }
            }

            SatisBolguHesabla();
        }

        /// <summary>Sətirləri standart hala gətirir (yeni satış üçün).</summary>
        private void SatisBolguSifirla()
        {
            SatisTerefdaslari.Clear();
            foreach (var share in PartnerMath.CreateDefaultRows())
            {
                SatisTerefdaslari.Add(PartnerPayRow.FromModel(share));
            }

            SatisBolguTetbiq = false;
            SatisBolguBazasi = 0m;
            SatisBolguYenile();
        }

        private void SatisBolguYenile()
        {
            OnPropertyChanged(nameof(SatisBolguCemi));
            OnPropertyChanged(nameof(SatisBolguFergi));
            OnPropertyChanged(nameof(SatisBolguXulase));
            OnPropertyChanged(nameof(SatisXeyirMetni));
            OnPropertyChanged(nameof(SatisBolguVeziyyet));
            OnPropertyChanged(nameof(SatisBolguFergMetni));
            OnPropertyChanged(nameof(SatisBolguFergReng));
        }

        partial void OnSatisBolguTetbiqChanged(bool value)
        {
            OnPropertyChanged(nameof(SatisBolguVeziyyet));

            // İlk dəfə açılırsa — baza avtomatik hesablanır.
            if (value && SatisBolguBazasi <= 0m && SatisQiymeti > 0m)
            {
                SatisBolguHesabla();
            }
        }

        [RelayCommand]
        private void ClearForm()
        {
            SelectedCar = null;
            Musteri = string.Empty;
            MuqavileNomresi = string.Empty;
            SatisQiymeti = 0m;
            SatisTarixi = DateTime.Today;
            OdenisUsulu = Catalog.SalePaymentMethods[0];
            BarterMebleg = 0m;
            BarterTesviri = string.Empty;
            ReceivedMarka = string.Empty;
            ReceivedNisan = string.Empty;
            ReceivedIl = DateTime.Today.Year;
            ReceivedYurus = 0;
            ReceivedYanacaq = Catalog.FuelTypes[0];
            ReceivedSatisQiymeti = 0m;
            Qeyd = string.Empty;
            SatisBolguSifirla();
            ClearErrors();
        }

        /// <summary>
        /// Satış nəticəsini izah edən mətn. Barter satışında parka əlavə olunan
        /// maşının <b>maya dəyəri</b> də göstərilir.
        /// </summary>
        private string BuildSaleResultMessage(Sale sale)
        {
            var message = $"\"{SelectedCar?.DisplayName}\" avtomobili \"{sale.Mustəri}\" adına satıldı.\n" +
                          $"Satış qiyməti: {sale.SatisQiymeti:N2} ₼";

            if (!sale.IsBarter)
            {
                return message;
            }

            message += $"\n\n🔄 BARTER ÖDƏNİŞİ" +
                       $"\n   Maşınla    : {sale.BarterMebleg:N2} ₼" +
                       $"\n   Nağd       : {sale.NagdMebleg:N2} ₼";

            if (sale.ReceivedCar is { } received)
            {
                message += $"\n\n📥 AVTO PARKA ƏLAVƏ EDİLDİ" +
                           $"\n   Avtomobil  : {received.DisplayName}" +
                           $"\n   Maya dəyəri: {received.AlisQiymeti:N2} ₼  (barter payı)" +
                           $"\n\n«Alış» xərci avtomatik yazıldı, sıra nömrəsi verildi.";
            }

            return message;
        }

        /// <summary>
        /// Barter satışında alıcının verdiyi maşını <see cref="CarItem"/>-a çevirir.
        /// <para>
        /// <b>MAYA DƏYƏRİ = BARTER HİSSƏSİ</b>: maşın üçün faktiki olaraq bu qədər
        /// "ödənilib", çünki satışın həmin hissəsi maşınla qarşılanıb.
        /// </para>
        /// </summary>
        /// <returns>Marka boşdursa <c>null</c> — maşın parka əlavə edilmir.</returns>
        private CarItem? BuildReceivedCar()
        {
            var marka = ReceivedMarka?.Trim();

            if (string.IsNullOrWhiteSpace(marka))
            {
                return null;
            }

            return new CarItem
            {
                Marka = marka,
                QeydiyyatNisani = ReceivedNisan?.Trim().ToUpperInvariant() ?? string.Empty,
                Il = ReceivedIl,
                Yurus = ReceivedYurus,
                Yanacaq = ReceivedYanacaq,
                AlisQiymeti = BarterMebleg,               // ← MAYА = barter dəyəri
                SatisQiymeti = ReceivedSatisQiymeti,
                BarterTesviri = BarterTesviri.Trim()
            };
        }

        private void UpdateStats()
        {
            var stats = _saleService.BuildStats(Sales);
            TotalSales = stats.Count;
            TotalRevenue = stats.TotalRevenue;
            TotalProfit = stats.TotalProfit;
            MonthRevenue = stats.MonthRevenue;
            BarterSales = stats.BarterCount;
            BarterRevenue = stats.BarterTotal;
        }
    }
}
