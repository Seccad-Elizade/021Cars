using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace EnterpriseAeroStudio.Hosting
{
    /// <inheritdoc />
    /// <remarks>
    /// Veb server <b>ayrı proses</b> kimi işə salınır
    /// (<c>EnterpriseAeroStudio.Web.exe</c>) və çıxışı canlı oxunur.
    /// Tətbiq bağlananda proses də dayandırılır.
    /// </remarks>
    public sealed class WebServerService : IWebServerService
    {
        private const int HealthTimeoutMilliseconds = 30_000;
        private const int MaxLogLines = 300;

        private readonly object _sync = new();
        private readonly List<string> _log = new();

        private Process? _process;
        private bool _disposed;

        public WebServerService()
        {
            ApplicationPath = FindWebApplication();
            ContentRoot = ResolveContentRoot(ApplicationPath);
            DomainUrl = "http://021cars.az";
            LocalUrl = "http://localhost:5000";
        }

        /// <summary>
        /// Veb tətbiqinin <b>işçi qovluğu</b> (ASP.NET <c>ContentRootPath</c>).
        /// <para>
        /// KRİTİK: ASP.NET Core <c>wwwroot</c> qovluğunu işçi qovluqdan oxuyur.
        /// Exe <c>bin\Debug\net10.0</c> içindədirsə, işçi qovluq LAYİHƏ kökü
        /// olmalıdır — əks halda <c>login.html</c>, <c>app.css</c> və digər
        /// statik fayllar tapılmır və giriş səhifəsi açılmır.
        /// </para>
        /// </summary>
        public string? ContentRoot { get; }

        /// <inheritdoc />
        public event EventHandler? StateChanged;

        /// <inheritdoc />
        public string? ApplicationPath { get; }

        /// <inheritdoc />
        public string LocalUrl { get; }

        /// <inheritdoc />
        public string DomainUrl { get; }

        /// <summary>
        /// Şəbəkədəki telefon / planşetlər üçün giriş ünvanı.
        /// <para>
        /// HƏR MÜRACİƏTDƏ yenidən hesablanır: DHCP ilə kompüterin IP-si
        /// dəyişdikdə (məs. <c>192.168.1.50</c> → <c>192.168.1.51</c>) ünvan
        /// avtomatik yenilənir və telefonlara köhnə ünvan göstərilmir.
        /// </para>
        /// </summary>
        public string LanUrl => BuildLanUrl();

        /// <inheritdoc />
        public string? LastError { get; private set; }

        /// <inheritdoc />
        public IReadOnlyList<string> LogLines
        {
            get
            {
                lock (_sync)
                {
                    return _log.ToArray();
                }
            }
        }

        /// <inheritdoc />
        public bool IsRunning
        {
            get
            {
                lock (_sync)
                {
                    try
                    {
                        return _process is { HasExited: false };
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        //   VEB TƏTBİQİNİN TAPILMASI
        // ---------------------------------------------------------------------

        /// <summary>
        /// Veb tətbiqinin <c>.exe</c> faylını tapır.
        /// Masaüstü tətbiqin yanından yuxarı doğru gedir, <c>Autocode.Web</c>
        /// qovluğunu tapır və orada ən yeni build çıxışını axtarır.
        /// </summary>
        private static string? FindWebApplication()
        {
            const string exeName = "EnterpriseAeroStudio.Web.exe";

            try
            {
                var baseDir = AppContext.BaseDirectory;

                // ================================================================
                //  ⓪ 🌐 AYRI «Web» ALT QOVLUĞU ✓✓✓ — ★ VACİB ★
                // ----------------------------------------------------------------
                //  ⚠ Web tətbiqi .NET 10, masaüstü tətbiq .NET 8-dir ✗ →
                //    EYNİ qovluqda saxlanılsa `coreclr.dll` · `System.Runtime.dll`
                //    kimi RUNTIME faylları TOQQUŞUR ✗ → biri MÜTLƏQ çökür ✗✓✓
                //  ✅ İNDİ: web öz «Web» alt qovluğunda saxlanılır ✓ →
                //    hər iki tətbiq öz runtime-ı ilə sakit işləyir ✓✓✓
                // ================================================================
                var webSub = Path.Combine(baseDir, "Web");

                if (File.Exists(Path.Combine(webSub, exeName)))
                {
                    return webSub;
                }

                // 1) Publish zamanı veb tətbiq masaüstü tətbiqin yanında ola bilər.
                if (File.Exists(Path.Combine(baseDir, exeName)))
                {
                    return baseDir;
                }

                // 2) Yuxarı qovluqlarda veb altlayihəsi axtarılır:
                //      "Web"            -> YENİ struktur (Autocode\Web)
                //      "Autocode.Web"   -> köhnə struktur (uyğunluq üçün)
                var current = new DirectoryInfo(baseDir);

                for (var level = 0; level < 7 && current is not null; level++)
                {
                    var parent = current.Parent;

                    if (parent is null)
                    {
                        break;
                    }

                    foreach (var folderName in new[] { "Web", "Autocode.Web" })
                    {
                        var webProject = Path.Combine(parent.FullName, folderName);

                        // Layihə faylı olmalıdır ki, səhv qovluq seçilməsin.
                        if (!Directory.Exists(webProject) ||
                            !File.Exists(Path.Combine(webProject, "Autocode.Web.csproj")))
                        {
                            continue;
                        }

                        var found = SearchBuildOutput(webProject, exeName);

                        if (found is not null)
                        {
                            return found;
                        }
                    }

                    current = parent;
                }
            }
            catch
            {
                // Axtarış xətası tətbiqi dayandırmamalıdır.
            }

            return null;
        }

        /// <summary>Veb layihəsinin qovluğunda ən yeni build çıxışını tapır.</summary>
        private static string? SearchBuildOutput(string webProject, string exeName)
        {
            try
            {
                var roots = new List<string>();

                var publish = Path.Combine(webProject, "publish");
                if (Directory.Exists(publish))
                {
                    roots.Add(publish);
                }

                var bin = Path.Combine(webProject, "bin");
                if (Directory.Exists(bin))
                {
                    roots.AddRange(Directory.GetDirectories(bin, "*", SearchOption.AllDirectories));
                }

                return roots
                    .Select(dir => Path.Combine(dir, exeName))
                    .Where(File.Exists)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Select(Path.GetDirectoryName)
                    .FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Veb tətbiqinin işçi qovluğunu (wwwroot olan qovluğu) tapır.
        /// </summary>
        private static string? ResolveContentRoot(string? applicationPath)
        {
            if (string.IsNullOrWhiteSpace(applicationPath))
            {
                return null;
            }

            // 1) Publish edilmiş çıxış: wwwroot exe-nin yanındadır.
            if (File.Exists(Path.Combine(applicationPath, "wwwroot", "login.html")))
            {
                return applicationPath;
            }

            // 2) Development: exe "bin\Debug\net10.0" içindədir,
            //    layihə kökü (wwwroot olan qovluq) yuxarıdadır.
            var current = new DirectoryInfo(applicationPath);

            for (var level = 0; level < 7 && current is not null; level++)
            {
                if (File.Exists(Path.Combine(current.FullName, "wwwroot", "login.html")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            // Tapılmadı — exe qovluğu ilə davam edirik.
            return applicationPath;
        }

        // ---------------------------------------------------------------------
        //   ŞƏBƏKƏ ÜNVANI
        // ---------------------------------------------------------------------
        private static string BuildLanUrl()
        {
            var address = FindLanAddress();
            return address is null ? string.Empty : $"http://{address}";
        }

        private static string? FindLanAddress()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                                && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.ToString())
                    .Where(ip => !ip.StartsWith("127.") && !ip.StartsWith("169.254."))
                    .Distinct()
                    .ToList();

                return candidates.FirstOrDefault(ip => ip.StartsWith("192.168."))
                       ?? candidates.FirstOrDefault(ip => ip.StartsWith("10."))
                       ?? candidates.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        // ---------------------------------------------------------------------
        //   İŞƏ SALMA / DAYANDIRMA
        // ---------------------------------------------------------------------

        /// <inheritdoc />
        public async Task<bool> StartAsync()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return false;
                }

                if (_process is { HasExited: false })
                {
                    return true;   // artıq işləyir
                }
            }

            // Server BAŞQA yolla işə salınıbsa (məs. AVTOPARK.bat) — təkrar başlatmırıq.
            // Əks halda port konflikti (AddressInUseException) yaranır.
            if (await PingAsync())
            {
                AppendLog("Veb server artıq işləyir — təkrar başladılmadı.");
                RaiseStateChanged();
                return true;
            }

            if (string.IsNullOrWhiteSpace(ApplicationPath))
            {
                LastError = "Veb tətbiqi tapılmadı (EnterpriseAeroStudio.Web.exe).";
                AppendLog("! " + LastError);
                RaiseStateChanged();
                return false;
            }

            LastError = null;
            AppendLog("Veb server başladılır: " + ApplicationPath);

            var started = false;

            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = Path.Combine(ApplicationPath, "EnterpriseAeroStudio.Web.exe"),

                    // KRİTİK: işçi qovluq = wwwroot olan qovluq.
                    // Əks halda ASP.NET login.html, app.css və digər statik
                    // faylları tapa bilmir.
                    WorkingDirectory = ContentRoot ?? ApplicationPath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                var process = new Process { StartInfo = info, EnableRaisingEvents = true };

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        AppendLog(e.Data);
                    }
                };

                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        AppendLog("! " + e.Data);
                    }
                };

                process.Exited += OnProcessExited;

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                lock (_sync)
                {
                    _process = process;
                }

                started = await WaitForServerAsync();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                AppendLog("! Xəta: " + ex.Message);
            }

            RaiseStateChanged();
            return started;
        }

        /// <inheritdoc />
        public async Task<bool> PingAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

            foreach (var url in new[] { "http://localhost:5000/health", "http://localhost/health" })
            {
                try
                {
                    var response = await client.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Server cavab vermir - davam edirik.
                }
            }

            return false;
        }

        /// <summary>Server hazır olana qədər gözləyir (sağlamlıq yoxlaması ilə).</summary>
        private async Task<bool> WaitForServerAsync()
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(HealthTimeoutMilliseconds);

            while (DateTime.UtcNow < deadline)
            {
                if (await PingAsync())
                {
                    return true;
                }

                await Task.Delay(700);
            }

            return false;
        }

        /// <inheritdoc />
        public void Stop()
        {
            Process? process;

            lock (_sync)
            {
                process = _process;
                _process = null;
            }

            if (process is null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // Proses artıq bağlanıb.
            }
            finally
            {
                try
                {
                    process.Exited -= OnProcessExited;
                    process.Dispose();
                }
                catch
                {
                    // Dispose xətası əhəmiyyətli deyil.
                }
            }

            AppendLog("Veb server dayandırıldı.");
            RaiseStateChanged();
        }

        /// <inheritdoc />
        public async Task<bool> RestartAsync()
        {
            AppendLog("Veb server yenidən başladılır...");
            Stop();

            // Portun azad olması üçün qısa fasilə.
            await Task.Delay(1500);

            return await StartAsync();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }

            Stop();
        }

        // ---------------------------------------------------------------------
        //   KÖMƏKÇİLƏR
        // ---------------------------------------------------------------------

        private void OnProcessExited(object? sender, EventArgs e)
        {
            AppendLog("Veb server prosesi bağlandı.");
            RaiseStateChanged();
        }

        /// <summary>Log sətrini əlavə edir (maksimum <see cref="MaxLogLines"/> sətir saxlanılır).</summary>
        private void AppendLog(string line)
        {
            lock (_sync)
            {
                _log.Add($"{DateTime.Now:HH:mm:ss}  {line}");

                while (_log.Count > MaxLogLines)
                {
                    _log.RemoveAt(0);
                }
            }
        }

        private void RaiseStateChanged()
        {
            try
            {
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch
            {
                // Hadisə abunəçisi xəta versə, server işinə təsir etməməlidir.
            }
        }
    }
}
