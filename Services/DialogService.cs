using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>WPF MessageBox və fayl dialoqları üzərində qurulmuş tətbiq.</summary>
    public sealed class DialogService : IDialogService
    {
        public void ShowInfo(string message, string title = "Məlumat")
            => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        public void ShowWarning(string message, string title = "Xəbərdarlıq")
            => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public void ShowError(string message, string title = "Xəta")
            => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

        public bool Confirm(string message, string title = "Təsdiq")
            => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        public string? ShowSaveFileDialog(string filter, string defaultFileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = defaultFileName,
                AddExtension = true,
                OverwritePrompt = true
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? ShowOpenFileDialog(string filter)
        {
            var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string[]? ShowOpenFilesDialog(string filter)
        {
            var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true, Multiselect = true };
            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }

        /// <summary>Tətbiqin tünd temasına uyğun sadə mətn daxiletmə pəncərəsi.</summary>
        public string? ShowInputDialog(string message, string title = "Yeni qeyd", string defaultValue = "")
        {
            var window = new Window
            {
                Title = title,
                Width = 430,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Background = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
                Foreground = Brushes.White
            };

            if (System.Windows.Application.Current?.MainWindow is Window owner && owner.IsLoaded)
            {
                window.Owner = owner;
            }

            var panel = new StackPanel { Margin = new Thickness(18) };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var input = new TextBox
            {
                Text = defaultValue ?? string.Empty,
                Height = 30,
                Padding = new Thickness(6, 0, 6, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(input);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var ok = new Button { Content = "✔ Təsdiq", Width = 110, Height = 30, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "✖ Ləğv", Width = 95, Height = 30, IsCancel = true };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);

            window.Content = panel;

            string? result = null;
            ok.Click += (_, _) =>
            {
                result = input.Text?.Trim();
                window.Close();
            };
            cancel.Click += (_, _) =>
            {
                result = null;
                window.Close();
            };

            window.Loaded += (_, _) =>
            {
                input.Focus();
                input.SelectAll();
            };

            window.ShowDialog();
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}
