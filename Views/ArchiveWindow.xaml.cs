using System.Windows;
using System.Windows.Input;
using EnterpriseAeroStudio.ViewModels;

namespace EnterpriseAeroStudio.Views
{
    /// <summary>"Sənəd və Media Arxivi" pəncərəsi (ViewModel tərəfindən idarə olunur).</summary>
    public partial class ArchiveWindow : Window
    {
        public ArchiveWindow()
        {
            InitializeComponent();
        }

        /// <summary>Fayl sətrinə iki dəfə klikləyəndə faylı əməliyyat sistemi ilə açır.</summary>
        private void AttachmentGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ArchiveViewModel viewModel &&
                viewModel.OpenAttachmentCommand.CanExecute(null))
            {
                viewModel.OpenAttachmentCommand.Execute(null);
            }
        }
    }
}
