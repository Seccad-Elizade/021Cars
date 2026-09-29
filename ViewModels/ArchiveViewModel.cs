using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>"Sənəd və Media Arxivi" pəncərəsinin ViewModel-i.</summary>
    public sealed partial class ArchiveViewModel : ObservableObject
    {
        private readonly IMediaService? _media;
        private readonly IDialogService? _dialogs;
        private readonly ILogger<ArchiveViewModel>? _logger;

        [ObservableProperty] private CarItem? car;
        [ObservableProperty] private string header = "Sənəd və Media Arxivi";
        [ObservableProperty] private MediaAttachment? selectedAttachment;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string attachmentInfo = "Fayl seçilməyib";

        /// <summary>Avtomobilin xərc tarixçəsi.</summary>
        public ObservableCollection<ExpenseItem> History { get; } = new();

        /// <summary>Avtomobilə bağlı sənəd / media faylları.</summary>
        public ObservableCollection<MediaAttachment> Attachments { get; } = new();

        /// <summary>Parametrsiz konstruktor (XAML dizayneri üçün).</summary>
        public ArchiveViewModel()
        {
        }

        public ArchiveViewModel(IMediaService media, IDialogService dialogs, ILogger<ArchiveViewModel> logger)
        {
            _media = media;
            _dialogs = dialogs;
            _logger = logger;
        }

        public ArchiveViewModel(CarItem car) : this()
        {
            SetCar(car);
        }

        public void SetCar(CarItem? value)
        {
            Car = value;
            Header = value is null
                ? "Sənəd və Media Arxivi"
                : $"Sənəd və Media Arxivi — {value.DisplayName}";

            History.Clear();
            if (value?.Expenses is not null)
            {
                foreach (var expense in value.Expenses.OrderByDescending(e => e.Tarix))
                {
                    History.Add(expense);
                }
            }

            _ = LoadAttachmentsAsync();
        }

        partial void OnSelectedAttachmentChanged(MediaAttachment? value)
        {
            AttachmentInfo = value is null
                ? "Fayl seçilməyib"
                : $"{value.FileName}  ·  {value.Extension}  ·  {value.SizeText}"
                  + (value.Exists ? string.Empty : "  ·  ⚠ fayl tapılmadı");
        }

        /// <summary>Avtomobilə bağlı sənəd / media fayllarını yükləyir.</summary>
        public async Task LoadAttachmentsAsync()
        {
            Attachments.Clear();
            SelectedAttachment = null;

            if (_media is null || Car is null || Car.Id <= 0)
            {
                return;
            }

            try
            {
                var items = await _media.GetAsync(MediaRefTypes.Car, Car.Id);
                foreach (var item in items)
                {
                    Attachments.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Sənəd faylları yüklənə bilmədi.");
            }
        }

        /// <summary>Fayl(lar) seçib avtomobilin arxivinə əlavə edir.</summary>
        [RelayCommand]
        private async Task AddFilesAsync()
        {
            if (_media is null || _dialogs is null || Car is null || Car.Id <= 0)
            {
                _dialogs?.ShowWarning("Fayl əlavə etmək üçün avtomobil seçilməlidir.");
                return;
            }

            var files = _dialogs.ShowOpenFilesDialog(_media.FileFilter);
            if (files is null || files.Length == 0)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var count = await _media.AddAsync(MediaRefTypes.Car, Car.Id, files);
                await LoadAttachmentsAsync();

                if (count > 0)
                {
                    _dialogs.ShowInfo($"{count} fayl arxivə əlavə edildi.", "Əlavə edildi");
                }
                else
                {
                    _dialogs.ShowWarning("Fayl əlavə edilə bilmədi.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fayl əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Fayl əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Avtomobilin fayllarının saxlandığı qovluğu birbaşa açır.</summary>
        [RelayCommand]
        private void OpenFolder()
        {
            if (_media is null || _dialogs is null || Car is null || Car.Id <= 0)
            {
                _dialogs?.ShowWarning("Qovluğu açmaq üçün avtomobil seçilməlidir.");
                return;
            }

            if (!_media.OpenFolder(MediaRefTypes.Car, Car.Id))
            {
                _dialogs.ShowWarning("Qovluq açıla bilmədi.");
            }
        }

        /// <summary>Seçilmiş faylı əməliyyat sistemi ilə açır.</summary>
        [RelayCommand]
        private void OpenAttachment()
        {
            if (_media is null || SelectedAttachment is null)
            {
                _dialogs?.ShowWarning("Açmaq üçün siyahıdan fayl seçin.");
                return;
            }

            if (!_media.OpenWithShell(SelectedAttachment))
            {
                _dialogs?.ShowWarning("Fayl açıla bilmədi — diskdə mövcud olmaya bilər.");
            }
        }

        /// <summary>Seçilmiş faylı həm diskdən, həm bazadan silir.</summary>
        [RelayCommand]
        private async Task DeleteAttachmentAsync()
        {
            if (_media is null || _dialogs is null || SelectedAttachment is null)
            {
                _dialogs?.ShowWarning("Silmək üçün siyahıdan fayl seçin.");
                return;
            }

            var attachment = SelectedAttachment;
            if (!_dialogs.Confirm($"\"{attachment.FileName}\" faylını silmək istəyirsiniz?", "Faylı sil"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _media.DeleteAsync(attachment.Id);
                await LoadAttachmentsAsync();
                _dialogs.ShowInfo("Fayl silindi.", "Silindi");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Fayl silinərkən xəta baş verdi.");
                _dialogs.ShowError("Fayl silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
