using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// 📜 <b>«SKRİPT İDXALI» TABININ VIEWMODEL-İ</b> ✓✓✓
    /// <para>
    /// İstifadəçi JSON skripti yapışdırır → <b>🔎 Yoxla</b> ilə nə olacağını görür →
    /// <b>📥 İdxal et</b> ilə avtomobillər · xərclər · tarixlər · qeydlər proqrama düşür ✓
    /// </para>
    /// </summary>
    public sealed partial class ScriptImportViewModel : ObservableObject
    {
        private readonly IScriptImportService _xidmet;
        private readonly IDialogService _dialoglar;
        private readonly ILogger<ScriptImportViewModel> _logger;
        private readonly SemaphoreSlim _qapi = new(1, 1);

        /// <summary>Yapışdırılan JSON mətni ✓.</summary>
        [ObservableProperty] private string skriptMetni = string.Empty;

        /// <summary>Nəticə hesabatı (sətir-sətir ✓).</summary>
        [ObservableProperty] private string hesabatMetni = string.Empty;

        /// <summary>Status mətni ✓.</summary>
        [ObservableProperty] private string statusMetni = "📜 JSON skripti yapışdırın → «🔎 Yoxla» → «📥 İdxal et» ✓";

        /// <summary>İşləyir? (düymələr bloklanır ✓)</summary>
        [ObservableProperty] private bool isBusy;

        /// <summary>Nəticə kartları görünsün? ✓</summary>
        [ObservableProperty] private bool neticeVar;

        [ObservableProperty] private int elaveOlunanMasin;
        [ObservableProperty] private int elaveOlunanXerc;
        [ObservableProperty] private int yeniKateqoriya;
        [ObservableProperty] private int kecirilenMasin;

        /// <summary>Ümumi maya dəyəri (alış + xərclər ✓).</summary>
        [ObservableProperty] private decimal umumiMaya;

        /// <summary>📢 İdxal uğurla bitdikdə baş verir → bütün tab-lar yenilənir ✓✓✓</summary>
        public event EventHandler? IdxalBitdi;

        public ScriptImportViewModel(
            IScriptImportService xidmet,
            IDialogService dialoglar,
            ILogger<ScriptImportViewModel> logger)
        {
            _xidmet = xidmet;
            _dialoglar = dialoglar;
            _logger = logger;
        }

        /// <summary>🔎 Yoxlama — bazaya HEÇ NƏ yazılmır ✗.</summary>
        [RelayCommand]
        private async Task YoxlaAsync() => await IsletAsync(yaz: false);

        /// <summary>📥 İdxal — avtomobillər və xərclər bazaya YAZILIR ✓.</summary>
        [RelayCommand]
        private async Task IdxalEtAsync() => await IsletAsync(yaz: true);

        /// <summary>🧹 Hər şeyi təmizləyir ✓.</summary>
        [RelayCommand]
        private void Temizle()
        {
            SkriptMetni = string.Empty;
            HesabatMetni = string.Empty;
            NeticeVar = false;
            ElaveOlunanMasin = 0;
            ElaveOlunanXerc = 0;
            YeniKateqoriya = 0;
            KecirilenMasin = 0;
            UmumiMaya = 0m;
            StatusMetni = "📜 JSON skripti yapışdırın → «🔎 Yoxla» → «📥 İdxal et» ✓";
        }

        /// <summary>📋 Nümunə skripti yapışdırır ✓.</summary>
        [RelayCommand]
        private void NumuneYukle()
        {
            SkriptMetni = NumuneJson;
            StatusMetni = "📋 Nümunə yükləndi — «🔎 Yoxla» basın ✓";
        }

        /// <summary>Yoxlama və idxal üçün ÜMUMİ məntiq ✓.</summary>
        private async Task IsletAsync(bool yaz)
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(SkriptMetni))
            {
                _dialoglar.ShowWarning("Skript boşdur ✓ — JSON mətnini yapışdırın.");
                return;
            }

            if (yaz)
            {
                var tesdiq = _dialoglar.Confirm(
                    "📥 Skriptdəki BÜTÜN avtomobillər, xərclər və qeydlər proqrama yazılacaq.\n\n" +
                    "Artıq mövcud olan nömrələr KEÇİLİR ✗ (köhnə məlumat dəyişmir ✓).\n\n" +
                    "Davam edilsin?",
                    "📜 Skript idxalı — təsdiq");

                if (!tesdiq)
                {
                    return;
                }
            }

            await _qapi.WaitAsync();

            try
            {
                IsBusy = true;
                StatusMetni = yaz ? "⏳ İdxal edilir — gözləyin…" : "⏳ Yoxlanılır — gözləyin…";

                var netice = yaz
                    ? await _xidmet.IcraEtAsync(SkriptMetni)
                    : await _xidmet.YoxlaAsync(SkriptMetni);

                HesabatMetni = string.Join(Environment.NewLine, netice.Setirler);

                ElaveOlunanMasin = netice.ElaveOlunanMasin;
                ElaveOlunanXerc = netice.ElaveOlunanXerc;
                YeniKateqoriya = netice.YeniKateqoriya;
                KecirilenMasin = netice.KecirilenMasin;
                UmumiMaya = netice.UmumiMaya;
                NeticeVar = true;

                if (!netice.Ugurlu)
                {
                    StatusMetni = "❌ " + (netice.Xeta ?? "Skript işlənmədi");
                    _dialoglar.ShowError(netice.Xeta ?? "Skript işlənmədi.", "📜 Skript xətası");
                    return;
                }

                if (yaz)
                {
                    StatusMetni = $"✅ İDXAL TAMAMLANDI ✓ — {netice.ElaveOlunanMasin} avtomobil · " +
                                  $"{netice.ElaveOlunanXerc} xərc · ümumi maya {netice.UmumiMaya:N2} ₼";

                    _logger.LogInformation(
                        "📜 Skript idxalı: {Masin} avtomobil · {Xerc} xərc · {Kateqoriya} yeni kateqoriya ✓",
                        netice.ElaveOlunanMasin, netice.ElaveOlunanXerc, netice.YeniKateqoriya);

                    // Bütün tab-lar yenilənir ✓✓✓
                    IdxalBitdi?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    StatusMetni = $"🔎 YOXLAMA BİTDİ ✓ — {netice.ElaveOlunanMasin} avtomobil yazılacaq · " +
                                  $"{netice.ElaveOlunanXerc} xərc · ümumi maya {netice.UmumiMaya:N2} ₼";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Skript idxalı zamanı gözlənilməz xəta.");
                StatusMetni = "❌ Xəta: " + ex.Message;
                _dialoglar.ShowError("Skript idxalı alınmadı: " + ex.Message, "📜 Skript xətası");
            }
            finally
            {
                IsBusy = false;
                _qapi.Release();
            }
        }

        /// <summary>Nümunə JSON (formatı göstərir ✓).</summary>
        private const string NumuneJson = """
        [
          {
            "SiraNomresi": null,
            "MarkaModel": "Mercedes E240",
            "DovletNomresi": "90-DV-174",
            "Il": 1998,
            "AlisTarixi": "09.07.2023",
            "AlisQiymeti": 10820.00,
            "Xercler": [
              { "Təsvir": "Elsen", "Tarix": "09.07.2023", "Məbləğ": 40.00 },
              { "Təsvir": "Çərimə ödənişi", "Tarix": "09.07.2023", "Məbləğ": 80.00 },
              { "Təsvir": "Qeydiyyat", "Tarix": "10.07.2023", "Məbləğ": 260.00 }
            ],
            "Qeydler": "Texniki baxışı 30.07.23-də bitir. 100 azn onun üçün tutulub"
          }
        ]
        """;
    }
}
