namespace EnterpriseAeroStudio.Hosting
{
    /// <summary>
    /// AvtoPark veb serverini (<c>Autocode.Web</c>) idarə edən xidmət.
    /// <para>
    /// Masaüstü tətbiq açıldıqda veb serveri <b>avtomatik</b> işə salır,
    /// vəziyyətini izləyir və tətbiq bağlananda dayandırır.
    /// </para>
    /// </summary>
    public interface IWebServerService : IDisposable
    {
        /// <summary>Veb server işləyirmi?</summary>
        bool IsRunning { get; }

        /// <summary>
        /// Veb serverin <b>cavab verdiyini</b> yoxlayır (sağlamlıq sorğusu).
        /// <para>
        /// Server başqa yolla (məs. <c>AVTOPARK.bat</c>) işə salınmış ola bilər —
        /// bu halda proses bizim uşaq prosesimiz deyil, amma sayt işləyir.
        /// </para>
        /// </summary>
        Task<bool> PingAsync();

        /// <summary>Veb tətbiqinin tapıldığı qovluq (tapılmadıqda <c>null</c>).</summary>
        string? ApplicationPath { get; }

        /// <summary>Bu kompüterdə giriş ünvanı — məs. <c>http://localhost:5000</c>.</summary>
        string LocalUrl { get; }

        /// <summary>Domen ünvanı — məs. <c>http://021cars.az</c>.</summary>
        string DomainUrl { get; }

        /// <summary>Şəbəkədəki cihazlar üçün ünvan — məs. <c>http://192.168.1.51</c>.</summary>
        string LanUrl { get; }

        /// <summary>Son xəta mətni (xəta yoxdursa <c>null</c>).</summary>
        string? LastError { get; }

        /// <summary>Serverin son log sətirləri (canlı göstərmək üçün).</summary>
        IReadOnlyList<string> LogLines { get; }

        /// <summary>Veb serverin vəziyyəti dəyişdikdə baş verir.</summary>
        event EventHandler? StateChanged;

        /// <summary>Veb serveri işə salır (artıq işləyirsə heç nə etmir).</summary>
        Task<bool> StartAsync();

        /// <summary>Veb serveri dayandırır.</summary>
        void Stop();

        /// <summary>Serveri yenidən başladır.</summary>
        Task<bool> RestartAsync();
    }
}
