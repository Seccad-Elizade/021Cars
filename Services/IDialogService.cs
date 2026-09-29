namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// UI-dan asılı olmayan dialoq/pəncərə xidməti (MVVM təmizliyini qoruyur).
    /// </summary>
    public interface IDialogService
    {
        void ShowInfo(string message, string title = "Məlumat");

        void ShowWarning(string message, string title = "Xəbərdarlıq");

        void ShowError(string message, string title = "Xəta");

        bool Confirm(string message, string title = "Təsdiq");

        /// <summary>Fayl saxlama dialoqu; ləğv edilərsə null qaytarır.</summary>
        string? ShowSaveFileDialog(string filter, string defaultFileName);

        /// <summary>Mətn açma dialoqu; ləğv edilərsə null qaytarır.</summary>
        string? ShowOpenFileDialog(string filter);

        /// <summary>İstifadəçidən qısa mətn soruşan dialoq; ləğv edilərsə null qaytarır.</summary>
        string? ShowInputDialog(string message, string title = "Yeni qeyd", string defaultValue = "");

        /// <summary>Çoxlu fayl seçmə dialoqu; ləğv edilərsə null qaytarır.</summary>
        string[]? ShowOpenFilesDialog(string filter);
    }
}
