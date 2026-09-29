using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Collections;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>"Avto Park &amp; Arxiv" tabının ViewModel-i.</summary>
    public sealed partial class CarParkViewModel : ObservableObject
    {
        private readonly ICarService _carService;
        private readonly IMediaService _media;
        private readonly IExportService _exportService;
        private readonly ISaleService _saleService;
        private readonly ICreditService _creditService;
        private readonly ITrashService _trash;
        private readonly IDialogService _dialogs;
        private readonly ILogger<CarParkViewModel> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly List<CarItem> _all = new();

        /// <summary>
        /// 🚀 Cədvəldəki avtomobillər ✓ — <see cref="BulkObservableCollection{T}"/>
        /// sayəsində minlərlə sətir <b>TƏK</b> bildirişlə yazılır ✓✓✓
        /// (əvvəl hər sətir ayrıca `Add` → cədvəl min dəfə yenidən qurulurdu ✗)
        /// </summary>
        public BulkObservableCollection<CarItem> Cars { get; } = new();

        public CarEditorViewModel Editor { get; }

        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private CarItem? selectedCar;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private int totalCars;
        [ObservableProperty] private int availableCars;
        [ObservableProperty] private int creditCars;

        /// <summary>📤 TRANSFER edilmiş avtomobillərin sayı ✓ (Avto Park göstəricisi ✓).</summary>
        [ObservableProperty] private int transferCars;
        [ObservableProperty] private decimal totalCost;

        /// <summary>Avtomobil məlumatları dəyişdikdə baş verir (xərcləri yeniləmək üçün).</summary>
        public event EventHandler? CarsChanged;

        /// <summary>Arxiv pəncərəsinin açılması tələb olunduqda baş verir.</summary>
        public event EventHandler<CarItem>? ArchiveRequested;

        public CarParkViewModel(
            ICarService carService,
            IMediaService media,
            IExportService exportService,
            ISaleService saleService,
            ICreditService creditService,
            ITrashService trash,
            IDialogService dialogs,
            CarEditorViewModel editor,
            ILogger<CarParkViewModel> logger)
        {
            _carService = carService;
            _media = media;
            _exportService = exportService;
            _saleService = saleService;
            _creditService = creditService;
            _trash = trash;
            _dialogs = dialogs;
            Editor = editor;
            _logger = logger;
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                // ⚡🚀 BÜTÜN OXUMALAR ARXA FONDA ✓✓✓ + YÜNGÜL SİYAHI ✓✓✓
                //  ⚠ ƏVVƏL: hər avtomobilin BÜTÜN xərcləri yaddaşa yüklənirdi ✗
                //     (1 000 000 xərc = ~1 GB RAM → proqram DONURDU ✗✓✓)
                //  ✅ İNDİ: xərc CƏMLƏRİ SQL-də hesablanır ✓ — yalnız avtomobil
                //     siyahısı yaddaşda qalır ✓✓✓ (ani açılır ✓)
                var (cars, candidates, allCars) = await Task.Run(async () =>
                {
                    var c = await _carService.GetCarsLightAsync();
                    var ad = await _carService.GetBarterCandidatesLightAsync();
                    var a = await _carService.GetAllCarsLightAsync();
                    return (c, ad, a);
                });

                _all.Clear();
                _all.AddRange(cars);

                // Hər avtomobil üçün sənəd / media faylının sayı göstərilir.
                var counts = await _media.GetCountsAsync(MediaRefTypes.Car);
                foreach (var car in _all)
                {
                    car.SenedSayi = counts.TryGetValue(car.Id, out var count) ? count : 0;
                }

                ApplyFilter();

                // Barter üçün seçilə bilən maşınlar (əvəzə veriləcək avtomobillər).
                Editor.BarterCandidates.Clear();
                foreach (var candidate in candidates)
                {
                    Editor.BarterCandidates.Add(candidate);
                }

                // Statistika üçün bütün avtomobillər (kreditdə və satılanlar daxil) götürülür.
                UpdateStats(allCars);
                await RefreshTrashCountAsync();
                _logger.LogInformation("{Count} avtomobil yükləndi.", _all.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Avtomobillər yüklənərkən xəta baş verdi.");
                _dialogs.ShowError("Avtomobil siyahısı yüklənə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        [RelayCommand]
        private async Task AddCarAsync()
        {
            if (!Editor.ValidateForm())
            {
                _dialogs.ShowWarning("Zəhmət olmasa formada göstərilən səhvləri düzəldin.");
                return;
            }

            var added = false;
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                if (await _carService.RegistrationExistsAsync(Editor.QeydiyyatNisani))
                {
                    _dialogs.ShowWarning($"\"{Editor.QeydiyyatNisani}\" nömrəli avtomobil artıq mövcuddur.");
                    return;
                }

                var car = Editor.ToModel();
                await _carService.AddCarAsync(car);
                Editor.Reset();
                added = true;
                _logger.LogInformation("Yeni avtomobil əlavə edildi: {Car}", car.DisplayName);
                _dialogs.ShowInfo($"\"{car.DisplayName}\" uğurla əlavə edildi.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Avtomobil əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Avtomobil əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            if (added)
            {
                await LoadAsync();
                CarsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        private void ClearForm() => Editor.Reset();

        /// <summary>
        /// DataGrid daxilində redaktə olunan avtomobili yadda saxlayır
        /// (sətirə iki dəfə kliklədikdən sonra çağırılır).
        /// </summary>
        [RelayCommand]
        private async Task SaveCarAsync(CarItem? car)
        {
            if (car is null)
            {
                return;
            }

            // Əlavə formasındaki eyni validasiya qaydaları tətbiq olunur.
            var regResult = CarEditorViewModel.ValidateRegistration(car.QeydiyyatNisani, new ValidationContext(car));
            var vinResult = CarEditorViewModel.ValidateVin(car.Vin, new ValidationContext(car));
            var error = regResult != ValidationResult.Success
                ? regResult?.ErrorMessage
                : vinResult != ValidationResult.Success ? vinResult?.ErrorMessage : null;

            if (error is not null)
            {
                _dialogs.ShowWarning(error + "\nDəyişiklik yadda saxlanmadı.");
                await LoadAsync(); // yanlış dəyəri geri qaytar
                return;
            }

            var saved = false;
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                if (await _carService.RegistrationExistsAsync(car.QeydiyyatNisani, car.Id))
                {
                    _dialogs.ShowWarning($"\"{car.QeydiyyatNisani}\" nömrəli avtomobil artıq mövcuddur.\nDəyişiklik yadda saxlanmadı.");
                }
                else
                {
                    await _carService.UpdateCarAsync(car);
                    saved = true;
                    _logger.LogInformation("Avtomobil redaktə edildi: {Id} - {Car}", car.Id, car.DisplayName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Avtomobil redaktə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Dəyişikliklər yadda saxlanıla bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            await LoadAsync();
            if (saved)
            {
                CarsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        private async Task DeleteCarAsync()
        {
            if (SelectedCar is null)
            {
                _dialogs.ShowWarning("Silmək üçün siyahıdan avtomobil seçin.");
                return;
            }

            var car = SelectedCar;

            var confirmText =
                $"\"{car.DisplayName}\" avtomobilini silmək istəyirsiniz?\n\n" +
                "Bunlarla birlikdə silinəcək:\n" +
                "  • bütün xərc qeydləri\n" +
                "  • avtomobilə aid satış qeydləri\n" +
                "  • kredit müqaviləsi və bütün ödəniş əməliyyatları\n" +
                "  • sənəd / media faylları\n" +
                "  • digər maşınlarda bu maşına olan barter əlaqəsi kəsiləcək\n\n" +
                "Nəticədə «Ümumi xərc», «Dövr xərci» və «Dövr mənfəəti» dəqiq qalacaq.\n" +
                "⚠ Bu əməliyyat geri qaytarıla bilməz.";

            if (!_dialogs.Confirm(confirmText))
            {
                return;
            }

            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                await _carService.DeleteCarAsync(car.Id);
                SelectedCar = null;
                _logger.LogInformation("Avtomobil silindi: {Id} - {Car}", car.Id, car.DisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Avtomobil silinərkən xəta baş verdi.");
                _dialogs.ShowError("Avtomobil silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            await LoadAsync();
            CarsChanged?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task ExportCarsAsync()
        {
            if (_all.Count == 0)
            {
                _dialogs.ShowWarning("İxrac üçün məlumat yoxdur.");
                return;
            }

            var path = _dialogs.ShowSaveFileDialog(
                "Excel (CSV) faylı|*.csv",
                $"Avtomobil_Parki_{DateTime.Now:yyyyMMdd_HHmm}.csv");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                await _exportService.ExportCarsAsync(_all, path);
                _dialogs.ShowInfo("Məlumatlar uğurla ixrac edildi:\n" + path);
                _logger.LogInformation("Avtomobil məlumatları ixrac edildi: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İxrac zamanı xəta baş verdi.");
                _dialogs.ShowError("İxrac alınmadı: " + ex.Message);
            }
        }

        /// <summary>
        /// «ℹ» düyməsi — avtomobil haqqında TAM məlumatı göstərir:
        /// alış, barter əvəzi, xərclər, maya dəyəri, satış və kredit.
        /// </summary>
        [RelayCommand]
        private async Task ShowInfoAsync(CarItem? car)
        {
            if (car is null)
            {
                _dialogs.ShowWarning("Məlumat üçün cədvəldən avtomobil seçin.");
                return;
            }

            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"🚗  {car.Marka}   ({QazText(car.QeydiyyatNisani)})");
            sb.AppendLine(new string('─', 50));
            sb.AppendLine($"VIN kod        : {QazText(car.Vin)}");
            sb.AppendLine($"Buraxılış ili  : {car.Il}");
            sb.AppendLine($"Yürüş          : {car.Yurus:N0} km");
            sb.AppendLine($"Yanacaq        : {QazText(car.Yanacaq)}");
            sb.AppendLine($"Status         : {StatusText(car.Status)}");
            sb.AppendLine($"Sıra nömrəsi   : {car.SiraNomresi}");
            sb.AppendLine($"Kredit №       : {QazText(car.KreditNomresi)}");

            sb.AppendLine();
            sb.AppendLine("🛒  ALIŞ");
            sb.AppendLine($"Tarix          : {(car.AlisTarixi?.ToString("dd.MM.yyyy") ?? "—")}");
            sb.AppendLine($"Saat           : {QazText(car.AlisSaati)}");
            sb.AppendLine($"Üsul           : {(car.IsBarter ? "🔄 Barter (əvəzləmə)" : "💵 Nağd")}");
            sb.AppendLine($"Alış qiyməti   : {car.AlisQiymeti:N2} ₼");

            if (car.IsBarter)
            {
                sb.AppendLine();
                sb.AppendLine("🔄  BARTER ƏVƏZİ  (hansı maşınla dəyişdirilib)");
                sb.AppendLine($"Əvəzə verilən  : {QazText(car.BarterCarInfo)}");
                sb.AppendLine($"Verilən dəyəri : {car.BarterDeyeri:N2} ₼");
                sb.AppendLine($"Alınan qiymət  : {car.AlisQiymeti:N2} ₼");
                sb.AppendLine($"Fərq           : {car.BarterFerqiMetni}");
                sb.AppendLine($"Qeyd           : {QazText(car.BarterTesviri)}");
            }

            sb.AppendLine();
            sb.AppendLine("🧮  MAYA DƏYƏRİ");
            sb.AppendLine($"Alış qiyməti   : {car.AlisQiymeti:N2} ₼");
            sb.AppendLine($"Əlavə xərclər  : {car.Xercler:N2} ₼");
            sb.AppendLine($"Ümumi maya     : {car.MayaDeyeri:N2} ₼");
            sb.AppendLine($"Satış qiyməti  : {car.SatisQiymeti:N2} ₼");

            try
            {
                var sale = (await _saleService.GetSalesAsync()).FirstOrDefault(s => s.CarId == car.Id);
                if (sale is not null)
                {
                    sb.AppendLine();
                    sb.AppendLine("💰  SATIŞ");
                    sb.AppendLine($"Müqavilə №    : {QazText(sale.MuqavileNomresi)}");
                    sb.AppendLine($"Tarix          : {sale.SatisTarixi:dd.MM.yyyy}");
                    sb.AppendLine($"Müştəri        : {QazText(sale.Mustəri)}");
                    sb.AppendLine($"Satış qiyməti  : {sale.SatisQiymeti:N2} ₼");
                    sb.AppendLine($"Maya (anlıq)   : {sale.MayaDeyeri:N2} ₼");
                    sb.AppendLine($"Mənfəət        : {sale.Menfeet:N2} ₼");
                    sb.AppendLine($"Ödəniş üsulu   : {QazText(sale.OdenisUsulu)}");

                    if (sale.IsBarter)
                    {
                        sb.AppendLine($"  🔄 Barter     : {sale.BarterMebleg:N2} ₼  (alıcının maşını)");
                        sb.AppendLine($"  💵 Nağd       : {sale.NagdMebleg:N2} ₼");
                        sb.AppendLine($"  Maşın         : {QazText(sale.BarterTesviri)}");
                    }
                }

                var credit = (await _creditService.GetCreditsAsync()).FirstOrDefault(c => c.Car?.Id == car.Id);
                if (credit is not null)
                {
                    sb.AppendLine();
                    sb.AppendLine("🏦  KREDİT");
                    sb.AppendLine($"Müqavilə №    : {QazText(credit.MuqavileNomresi)}");
                    sb.AppendLine($"Müştəri        : {QazText(credit.Mustəri)}");
                    sb.AppendLine($"Kreditləşdirilen: {credit.Kreditlesdirilen:N2} ₼");
                    sb.AppendLine($"Faiz           : {credit.FaizDerecesi:N2} %");
                    sb.AppendLine($"Müddət         : {credit.MuddetAy} ay");
                    sb.AppendLine($"Aylıq ödəniş   : {credit.AylıqOdenis:N2} ₼");
                    sb.AppendLine($"Başlama        : {credit.BaslamaTarixi:dd.MM.yyyy}");
                    sb.AppendLine($"Status         : {QazText(credit.Status)}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Məlumat pəncərəsi üçün satış/kredit oxuna bilmədi.");
            }

            sb.AppendLine();
            sb.AppendLine(new string('─', 50));
            sb.AppendLine($"📎 Sənəd & media : {car.SenedSayi} fayl");

            _dialogs.ShowInfo(sb.ToString());
        }

        /// <summary>Boş mətnin yerinə tire qoyur.</summary>
        private static string QazText(string? value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        /// <summary>Status mətnini oxunaqlı göstərir.</summary>
        private static string StatusText(string? status) => status switch
        {
            "Stokda" => "📦 Stokda",
            "Satışda" => "🏷️ Satışda",
            "Kreditdə" => "🏦 Kreditdə",
            "Satıldı" => "✅ Satıldı",
            "Barter edildi" => "🔄 Barter edildi (parkdan çıxıb)",
            "Təmirdə" => "🔧 Təmirdə",
            null or "" => "—",
            _ => status
        };

        /// <summary>Silinmiş qeydlərin sayı (düymədə göstərilir).</summary>
        [ObservableProperty] private int trashCount;

        /// <summary>
        /// «Ctrl+Z» — ən son silinmiş qeydi geri qaytarır.
        /// <para>
        /// Dəfələrlə basmaqla <b>ardıcıl olaraq</b> əvvəlki silinmələr də
        /// qaytarılır (əks-səbət prinsipi) — 20-dən çox əməliyyat daxil.
        /// </para>
        /// </summary>
        [RelayCommand]
        private async Task RestoreLastAsync()
        {
            try
            {
                var restored = await _trash.RestoreLastAsync();

                if (restored is null)
                {
                    _dialogs.ShowWarning("Geri qaytarılacaq silinmiş qeyd yoxdur.");
                    return;
                }

                _logger.LogInformation("Geri qaytarıldı: {Kind} «{Title}»", restored.Kind, restored.Title);

                _dialogs.ShowInfo(
                    "✅ GERİ QAYTARILDI\n\n" +
                    $"Növ    : {restored.Kind}\n" +
                    $"Ad     : {restored.Title}\n" +
                    $"Tərkib : {restored.Summary}\n" +
                    $"Məbləğ : {restored.Amount:N2} ₼\n\n" +
                    "Daha əvvəlki silinməni qaytarmaq üçün yenidən «Ctrl+Z» basın.");

                await LoadAsync();
                CarsChanged?.Invoke(this, EventArgs.Empty);
                await RefreshTrashCountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Geri qaytarma zamanı xəta baş verdi.");
                _dialogs.ShowError("Geri qaytarıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>
        /// «🗑 Silinənlər» <b>pəncərəsini</b> açır — sadə pop-up deyil:
        /// istənilən qeydi seçib geri qaytarmaq və ya birdəfəlik silmək olar.
        /// </summary>
        [RelayCommand]
        private async Task ShowTrashAsync()
        {
            try
            {
                var owner = System.Windows.Application.Current?.MainWindow;

                var window = new EnterpriseAeroStudio.Views.TrashWindow(_trash, _dialogs);

                if (owner is not null && !ReferenceEquals(owner, window))
                {
                    window.Owner = owner;
                }

                window.ShowDialog();

                // Bərpa olunubsa bütün siyahılar yenilənir.
                await LoadAsync();
                CarsChanged?.Invoke(this, EventArgs.Empty);
                await RefreshTrashCountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Silinənlər pəncərəsi açıla bilmədi.");
                _dialogs.ShowError("Silinənlər açıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>Silinmiş qeydlərin sayını yeniləyir.</summary>
        private async Task RefreshTrashCountAsync()
        {
            try
            {
                var entries = await _trash.GetEntriesAsync();
                TrashCount = entries.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Silinənlərin sayı oxuna bilmədi.");
            }
        }

        [RelayCommand]
        private void OpenArchive()
        {
            if (SelectedCar is null)
            {
                _dialogs.ShowWarning("Arxivi açmaq üçün avtomobil seçin.");
                return;
            }

            ArchiveRequested?.Invoke(this, SelectedCar);
        }

        private void ApplyFilter()
        {
            var search = SearchText?.Trim() ?? string.Empty;

            var filtered = string.IsNullOrEmpty(search)
                ? _all
                : _all.Where(c =>
                    c.Marka.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.QeydiyyatNisani.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    c.Vin.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            // ⚡ TƏK bildirişlə yazılır ✓✓✓ (minlərlə avtomobildə cədvəl
            //    min dəfə yenidən qurulmur ✗ → donma yoxdur ✓)
            Cars.ReplaceAll(filtered);
        }

        private void UpdateStats(IEnumerable<CarItem> cars)
        {
            var stats = _carService.BuildStats(cars);
            TotalCars = stats.TotalCars;
            AvailableCars = stats.AvailableCars;
            CreditCars = stats.CreditCars;

            // 📤 TRANSFER edilmiş avtomobillər ✓✓✓ (parkdan çıxıb ✓)
            TransferCars = cars.Count(c => string.Equals(c.Status, Catalog.TransferStatus, StringComparison.Ordinal));
            TotalCost = stats.TotalCost;
        }
    }
}
