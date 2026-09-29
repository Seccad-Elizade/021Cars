using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    /// <remarks>
    /// İki qatdan ibarətdir:
    /// <list type="number">
    ///   <item>
    ///     <b>FileSystemWatcher</b> — dəyişikliyə ANİ reaksiya verir.
    ///   </item>
    ///   <item>
    ///     <b>Nəzarət taymeri</b> — hər 2 saniyədə bazanın vaxtını və ölçüsünü
    ///     yoxlayır. FileSystemWatcher bəzi hallarda (şəbəkə diski, OneDrive,
    ///     SQLite WAL rejimi) hadisəni buraxa bilir — bu qat onu tutur.
    ///   </item>
    /// </list>
    /// Hər iki mənbə 400 ms "debounce" ilə birləşdirilir ki, SQLite-ın bir
    /// əməliyyatda yazdığı bir neçə fayl (db, -wal, -shm) tək hadisə olsun.
    /// </remarks>
    public sealed class DataChangeWatcher : IDataChangeWatcher
    {
        /// <summary>Ardıcıl dəyişikliklərin tək hadisəyə birləşdirilmə müddəti.</summary>
        private const int DebounceMilliseconds = 400;

        /// <summary>Nəzarət taymerinin yoxlama aralığı (ms).</summary>
        private const int WatchdogIntervalMilliseconds = 2000;

        private readonly string _databasePath;
        private readonly ILogger? _logger;
        private readonly object _sync = new();

        private FileSystemWatcher? _watcher;
        private Timer? _debounce;
        private Timer? _watchdog;
        private string _signature = string.Empty;
        private bool _disposed;

        public DataChangeWatcher(string databasePath, ILogger? logger = null)
        {
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
            _logger = logger;
        }

        /// <inheritdoc />
        public event EventHandler? Changed;

        /// <inheritdoc />
        public bool IsRunning
        {
            get
            {
                lock (_sync)
                {
                    return _watcher is not null;
                }
            }
        }

        /// <inheritdoc />
        public int ChangeCount { get; private set; }

        /// <inheritdoc />
        public DateTime? LastChangeUtc { get; private set; }

        /// <inheritdoc />
        public void Start()
        {
            lock (_sync)
            {
                if (_disposed || _watcher is not null)
                {
                    return;
                }

                var directory = Path.GetDirectoryName(_databasePath);

                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    _logger?.LogWarning(
                        "Baza qovluğu tapılmadı, dəyişiklik izləməsi işə düşmədi: {Directory}", directory);
                    return;
                }

                try
                {
                    _signature = ComputeSignature();

                    _watcher = new FileSystemWatcher(directory, Path.GetFileName(_databasePath) + "*")
                    {
                        NotifyFilter = NotifyFilters.LastWrite
                                       | NotifyFilters.Size
                                       | NotifyFilters.FileName
                                       | NotifyFilters.CreationTime,
                        IncludeSubdirectories = false
                    };

                    _watcher.Changed += OnFileSystemEvent;
                    _watcher.Created += OnFileSystemEvent;
                    _watcher.Deleted += OnFileSystemEvent;
                    _watcher.Renamed += OnFileSystemEvent;
                    _watcher.Error += OnFileSystemError;
                    _watcher.EnableRaisingEvents = true;

                    // İkinci qat: dövri yoxlama.
                    _watchdog = new Timer(
                        _ => Fire(),
                        null,
                        WatchdogIntervalMilliseconds,
                        WatchdogIntervalMilliseconds);

                    _logger?.LogInformation("Dəyişiklik izləməsi aktivdir: {Path}", _databasePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Dəyişiklik izləməsi işə düşə bilmədi.");
                    DisposeWatcher();
                }
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            lock (_sync)
            {
                DisposeWatcher();
            }
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
                DisposeWatcher();
            }
        }

        /// <summary>Bütün izləyiciləri sıradan çıxarır (kilid daxilində çağırılmalıdır).</summary>
        private void DisposeWatcher()
        {
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileSystemEvent;
                _watcher.Created -= OnFileSystemEvent;
                _watcher.Deleted -= OnFileSystemEvent;
                _watcher.Renamed -= OnFileSystemEvent;
                _watcher.Error -= OnFileSystemError;
                _watcher.Dispose();
                _watcher = null;
            }

            _debounce?.Dispose();
            _debounce = null;

            _watchdog?.Dispose();
            _watchdog = null;
        }

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e) => ScheduleNotify();

        private void OnFileSystemError(object sender, ErrorEventArgs e)
            => _logger?.LogWarning(e.GetException(), "Fayl izləyicisi xətası.");

        /// <summary>Ardıcıl hadisələri gecikdirmə ilə tək hadisəyə yığır.</summary>
        private void ScheduleNotify()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _debounce ??= new Timer(_ => Fire(), null, Timeout.Infinite, Timeout.Infinite);
                _debounce.Change(DebounceMilliseconds, Timeout.Infinite);
            }
        }

        /// <summary>Baza həqiqətən dəyişibsə <see cref="Changed"/> hadisəsini işə salır.</summary>
        private void Fire()
        {
            string signature;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                signature = ComputeSignature();

                if (signature == _signature)
                {
                    return;   // real dəyişiklik yoxdur — təkrar hadisə göndərilmir
                }

                _signature = signature;
            }

            ChangeCount++;
            LastChangeUtc = DateTime.UtcNow;

            _logger?.LogInformation("Baza dəyişikliyi aşkarlandı (#{Count}) — sinxronizasiya işə salınır.",
                ChangeCount);

            try
            {
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Dəyişiklik hadisəsi işə salınarkən xəta baş verdi.");
            }
        }

        /// <summary>Faylların vaxtını və ölçüsünü bir mətn imzasına yığır.</summary>
        private string ComputeSignature()
        {
            var builder = new StringBuilder(96);

            // SQLite üç fayl işlədir: avtopark.db, -wal, -shm
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            {
                try
                {
                    var info = new FileInfo(_databasePath + suffix);
                    builder.Append(info.Exists ? info.LastWriteTimeUtc.Ticks : 0)
                           .Append(':')
                           .Append(info.Exists ? info.Length : 0);
                }
                catch
                {
                    builder.Append('?');
                }

                builder.Append('|');
            }

            return builder.ToString();
        }
    }
}
