using System.Collections.ObjectModel;
using System.Windows;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.Views
{
    /// <summary>
    /// «🗑 Silinənlər» pəncərəsi — silinmiş (və ya dəyişdirilmiş) qeydləri
    /// göstərir və <b>seçilmişi</b> geri qaytarmağa imkan verir.
    /// <para>
    /// Sadə pop-up deyil: istənilən qeydi seçib bərpa etmək və ya birdəfəlik
    /// silmək olar.
    /// </para>
    /// </summary>
    public partial class TrashWindow : Window
    {
        private readonly ITrashService _trash;
        private readonly IDialogService _dialogs;

        /// <summary>Cədvəldə göstərilən qeydlər.</summary>
        public ObservableCollection<TrashSnapshot> Entries { get; } = new();

        public TrashWindow(ITrashService trash, IDialogService dialogs)
        {
            _trash = trash;
            _dialogs = dialogs;

            InitializeComponent();
            DataContext = this;

            Loaded += async (_, _) => await RefreshAsync();
        }

        /// <summary>Siyahını bazadan yeniləyir.</summary>
        private async Task RefreshAsync()
        {
            try
            {
                Entries.Clear();

                foreach (var entry in await _trash.GetEntriesAsync())
                {
                    Entries.Add(entry);
                }

                SubText.Text = Entries.Count == 0
                    ? "Silinmiş və ya dəyişdirilmiş qeyd yoxdur."
                    : $"{Entries.Count} qeyd (ən yenisi əvvəldə)  ·  Qovluq: {_trash.Folder}";

                var hasAny = Entries.Count > 0;
                RestoreButton.IsEnabled = hasAny;
                PurgeButton.IsEnabled = hasAny;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Yüklənə bilmədi: " + ex.Message;
            }
        }

        private void Grid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => Restore_Click(sender, e);

        /// <summary>Seçilmiş qeydi geri qaytarır.</summary>
        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItem is not TrashSnapshot entry)
            {
                _dialogs.ShowWarning("Geri qaytarmaq üçün cədvəldən qeyd seçin.");
                return;
            }

            try
            {
                StatusText.Text = "Geri qaytarılır…";

                var restored = await _trash.RestoreAsync(entry.FileName);

                if (restored is null)
                {
                    StatusText.Text = "Geri qaytarıla bilmədi.";
                    _dialogs.ShowError("Geri qaytarıla bilmədi — surət oxunmadı.");
                    return;
                }

                _dialogs.ShowInfo(
                    "✅ GERİ QAYTARILDI\n\n" +
                    $"Əməliyyat : {restored.Action}\n" +
                    $"Növ       : {restored.Kind}\n" +
                    $"Ad        : {restored.Title}\n" +
                    $"Tərkib    : {restored.Summary}\n" +
                    $"Məbləğ    : {restored.Amount:N2} ₼");

                StatusText.Text = $"«{restored.Title}» geri qaytarıldı.";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Xəta: " + ex.Message;
                _dialogs.ShowError("Geri qaytarıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>Seçilmiş surəti birdəfəlik silir.</summary>
        private async void Purge_Click(object sender, RoutedEventArgs e)
        {
            if (Grid.SelectedItem is not TrashSnapshot entry)
            {
                _dialogs.ShowWarning("Silmək üçün cədvəldən qeyd seçin.");
                return;
            }

            var confirmed = _dialogs.Confirm(
                $"«{entry.Title}» surəti BİRDƏFƏLİK silinəcək.\n\n" +
                "Bundan sonra bu məlumatı geri qaytarmaq MÜMKÜN OLMAYACAQ.");

            if (!confirmed)
            {
                return;
            }

            try
            {
                await _trash.DeletePermanentlyAsync(entry.FileName);
                StatusText.Text = $"«{entry.Title}» birdəfəlik silindi.";
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = "Xəta: " + ex.Message;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
