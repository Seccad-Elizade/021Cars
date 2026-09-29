using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Hosting;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// «🌐 Veb Sayt» tabının ViewModel-i.
    /// <para>
    /// Veb serveri idarə edir və vəziyyətini canlı göstərir.
    /// Masaüstü tətbiq açıldıqda server <b>avtomatik</b> işə salınır.
    /// </para>
    /// </summary>
    public sealed partial class WebViewModel : ObservableObject, IDisposable
    {
        private readonly IWebServerService _server;
        private readonly System.Windows.Threading.DispatcherTimer _timer;

        private bool _disposed;

        [ObservableProperty] private bool isRunning;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string statusText = "Yoxlanılır…";
        [ObservableProperty] private string hintText = string.Empty;
        [ObservableProperty] private string logText = string.Empty;
        [ObservableProperty] private string? errorText;

        /// <summary>
        /// Telefonla skan üçün QR kod.
        /// <para>
        /// Router-də DNS ayarı olmasa belə telefon birbaşa şəbəkə ünvanına
        /// qoşula bilir — kameranı bu koda tutmaq kifayətdir.
        /// </para>
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasQr))]
        private System.Windows.Media.Imaging.BitmapImage? qrImage;

        /// <summary>QR kodun kodlaşdırdığı ünvan (istifadəçiyə göstərilir).</summary>
        [ObservableProperty] private string qrTargetUrl = "—";

        /// <summary>QR kod hazırdırmı?</summary>
        public bool HasQr => QrImage is not null;

        /// <summary>QR kodun hazırlandığı ünvan — təkrar yaratmamaq üçün.</summary>
        private string? _qrSource;

        public WebViewModel(IWebServerService server)
        {
            _server = server;
            _server.StateChanged += OnServerStateChanged;

            // Hər 3 saniyədə vəziyyət yenilənir (server gözlənilmədən bağlana bilər).
            // DispatcherTimer — UI axınında işləyir, əlavə marshalling lazım deyil.
            _timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            _timer.Tick += async (_, _) => await RefreshAsync();
            _timer.Start();

            Refresh();
        }

        // ----------------------------------------------------------- ÜNVANLAR ---

        /// <summary>Bu kompüterdə giriş ünvanı.</summary>
        public string LocalUrl => _server.LocalUrl;

        /// <summary>Domen ünvanı (021cars.az).</summary>
        public string DomainUrl => _server.DomainUrl;

        /// <summary>Şəbəkədəki telefonlar üçün ünvan.</summary>
        public string LanUrl => string.IsNullOrWhiteSpace(_server.LanUrl)
            ? "— şəbəkə tapılmadı —"
            : _server.LanUrl;

        /// <summary>Daxili brauzerin açacağı ünvan.</summary>
        public string BrowserUrl => _server.LocalUrl;

        /// <summary>Veb tətbiqinin fiziki yolu (diaqnostika üçün).</summary>
        public string ApplicationPath => _server.ApplicationPath ?? "— tapılmadı —";

        /// <summary>Daxili brauzerin ünvanı dəyişməlidirmi?</summary>
        public event EventHandler<string>? NavigateRequested;

        // ---------------------------------------------------------------- ƏMRƏLƏR ---

        /// <summary>Proqram açılışında avtomatik çağırılır.</summary>
        public async Task AutoStartAsync()
        {
            if (_server.IsRunning)
            {
                Refresh();
                return;
            }

            await StartAsync();
        }

        [RelayCommand]
        private async Task StartAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            StatusText = "Başladılır…";
            ErrorText = null;

            try
            {
                var ok = await _server.StartAsync();

                ErrorText = ok ? null : _server.LastError ?? "Server başladıla bilmədi.";

                if (ok)
                {
                    NavigateRequested?.Invoke(this, BrowserUrl);
                }
            }
            finally
            {
                IsBusy = false;
                Refresh();
            }
        }

        [RelayCommand]
        private void Stop()
        {
            if (IsBusy)
            {
                return;
            }

            StatusText = "Dayandırılır…";
            _server.Stop();
            Refresh();
        }

        [RelayCommand]
        private async Task RestartAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            StatusText = "Yenidən başladılır…";
            ErrorText = null;

            try
            {
                var ok = await _server.RestartAsync();

                ErrorText = ok ? null : _server.LastError ?? "Yenidən başlatmaq alınmadı.";

                if (ok)
                {
                    NavigateRequested?.Invoke(this, BrowserUrl);
                }
            }
            finally
            {
                IsBusy = false;
                Refresh();
            }
        }

        [RelayCommand]
        private void Reload()
        {
            Refresh();
            NavigateRequested?.Invoke(this, BrowserUrl);
        }

        [RelayCommand]
        private void OpenLocal() => Open(LocalUrl);

        [RelayCommand]
        private void OpenDomain() => Open(DomainUrl);

        [RelayCommand]
        private void OpenLan() => Open(_server.LanUrl);

        /// <summary>Ünvanı standart brauzerdə açır.</summary>
        private void Open(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("—"))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ErrorText = "Brauzer açıla bilmədi: " + ex.Message;
            }
        }

        // -------------------------------------------------------------- VƏZİYYƏT ---

        /// <summary>Vəziyyəti serverdən yenidən oxuyur (proses əsaslı).</summary>
        public void Refresh() => Apply(responding: false);

        /// <summary>
        /// Vəziyyəti yoxlayır — server <b>cavab verirsə</b> də «işləyir» sayılır.
        /// Beləliklə server başqa yolla (məs. <c>AVTOPARK.bat</c>) işə salınsa da
        /// tab düzgün vəziyyət göstərir.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (_disposed)
            {
                return;
            }

            var responding = await _server.PingAsync();
            Apply(responding);
        }

        private void Apply(bool responding)
        {
            if (_disposed)
            {
                return;
            }

            var ownProcess = _server.IsRunning;

            IsRunning = ownProcess || responding;

            StatusText = IsRunning ? "İşləyir" : "Dayandırılıb";

            HintText = IsRunning
                ? ownProcess
                    ? "Veb sayt aktivdir — telefon və kompüterlərdən giriş mümkündür."
                    : "Veb sayt aktivdir (server başqa proseslə başladılıb)."
                : "Veb sayt dayandırılıb. «▶ Başlat» düyməsini basın.";

            var lines = _server.LogLines;

            LogText = lines.Count == 0
                ? "Log hələ boşdur."
                : string.Join(Environment.NewLine, lines);

            // Şəbəkə ünvanı DHCP ilə dəyişə bilər — hər dəfə yenidən oxunur.
            OnPropertyChanged(nameof(LanUrl));
            OnPropertyChanged(nameof(LocalUrl));
            OnPropertyChanged(nameof(DomainUrl));
            BuildQrCode();
        }

        /// <summary>
        /// Şəbəkə ünvanı üçün QR kod yaradır.
        /// <para>
        /// Ünvan dəyişməyibsə kod təkrar yaradılmır (resurs qənaəti).
        /// QR yaradıla bilməsə də veb sayt işləməyə davam edir.
        /// </para>
        /// </summary>
        private void BuildQrCode()
        {
            if (_disposed)
            {
                return;
            }

            var url = _server.LanUrl;
            QrTargetUrl = string.IsNullOrWhiteSpace(url) ? "—" : url;

            if (string.IsNullOrWhiteSpace(url))
            {
                QrImage = null;
                _qrSource = null;
                return;
            }

            if (string.Equals(url, _qrSource, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                using var generator = new QRCoder.QRCodeGenerator();
                using var data = generator.CreateQrCode(url, QRCoder.QRCodeGenerator.ECCLevel.Q);

                // PngByteQRCode — System.Drawing asılılığı yoxdur, WPF üçün idealdır.
                var png = new QRCoder.PngByteQRCode(data).GetGraphic(8);

                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.StreamSource = new MemoryStream(png);
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();

                QrImage = image;
                _qrSource = url;
            }
            catch (Exception ex)
            {
                QrImage = null;
                _qrSource = null;
                System.Diagnostics.Debug.WriteLine("QR kod yaradıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>Server hadisəsini UI axınına keçirir.</summary>
        private void OnServerStateChanged(object? sender, EventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher is null)
            {
                return;
            }

            if (dispatcher.CheckAccess())
            {
                Refresh();
            }
            else
            {
                dispatcher.InvokeAsync(Refresh);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _server.StateChanged -= OnServerStateChanged;
            _timer.Stop();
        }
    }
}
