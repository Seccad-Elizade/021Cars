using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// 🧾 <b>«KREDİT İDXALI» TABININ VIEWMODEL-İ</b> ✓✓✓
    /// <para>
    /// İstifadəçi maşın/kredit məlumatlarını <b>toplu mətn</b> şəklində yapışdırır →
    /// <b>🔎 Yoxla</b> ilə aylıq cədvəli (maya · mənfəət · tərəfdaş payları) görür →
    /// <b>📥 İdxal et</b> ilə bütün məlumat proqrama yazılır ✓
    /// </para>
    /// <para>
    /// ⚠ Yoxlama zamanı bazaya heç nə yazılmır ✗✓✓ (yalnız önizləmə ✓).
    /// </para>
    /// </summary>
    public sealed partial class KreditIdxalViewModel : ObservableObject
    {
        private readonly IKreditIdxalService _xidmet;
        private readonly IDialogService _dialoglar;
        private readonly ILogger<KreditIdxalViewModel> _logger;
        private readonly SemaphoreSlim _qapi = new(1, 1);

        /// <summary>Yapışdırılan kredit mətni ✓.</summary>
        [ObservableProperty] private string skriptMetni = string.Empty;

        /// <summary>Nəticə hesabatı (sətir-sətir ✓).</summary>
        [ObservableProperty] private string hesabatMetni = string.Empty;

        /// <summary>Status mətni ✓.</summary>
        [ObservableProperty] private string statusMetni =
            "🧾 Kredit məlumatını yapışdırın → «🔎 Yoxla» → «📥 İdxal et» ✓  (nümunə üçün «📋 Nümunə»)";

        /// <summary>İşləyir? (düymələr bloklanır ✓)</summary>
        [ObservableProperty] private bool isBusy;

        /// <summary>Nəticə görünsün? (xülasə + cədvəl ✓)</summary>
        [ObservableProperty] private bool neticeVar;

        /// <summary>🚘 Maşının mənfəətini (xeyir) də tərəfdaşlara bölüşdür?</summary>
        [ObservableProperty] private bool maasinXeyiriniBol;

        // ---- Cədvəl başlıqları üçün tərəfdaş adları ✓ ----
        [ObservableProperty] private string kar1Basliq = "KAR (1)";
        [ObservableProperty] private string kar2Basliq = "KAR (2)";
        [ObservableProperty] private string kar3Basliq = "KAR (3)";
        [ObservableProperty] private string kar4Basliq = "KAR (4)";

        // ---- Önizləmə cədvəli ✓ ----
        /// <summary>Bütün kreditlərin aylıq sətirləri (vahid cədvəl ✓).</summary>
        public ObservableCollection<KreditIdxalSetir> Setirler { get; } = new();

        /// <summary>Hər kreditin xülasə kartı ✓.</summary>
        public ObservableCollection<KreditIdxalKart> Kartlar { get; } = new();

        // ---- Xülasə göstəriciləri ✓ ----
        [ObservableProperty] private int kreditSayi;
        [ObservableProperty] private int odenisSayi;
        [ObservableProperty] private decimal umumiKok;
        [ObservableProperty] private decimal umumiMaya;
        [ObservableProperty] private decimal umumiOdenilmis;
        [ObservableProperty] private decimal umumiQaliqNisye;
        [ObservableProperty] private decimal umumiMayaKar;
        [ObservableProperty] private decimal umumiCerime;
        [ObservableProperty] private decimal umumiMohlet;

        /// <summary>📢 İdxal bitdikdə baş verir → bütün tablar yenilənir ✓✓✓</summary>
        public event EventHandler? IdxalBitdi;

        public KreditIdxalViewModel(
            IKreditIdxalService xidmet,
            IDialogService dialoglar,
            ILogger<KreditIdxalViewModel> logger)
        {
            _xidmet = xidmet;
            _dialoglar = dialoglar;
            _logger = logger;
        }

        /// <summary>📋 Nümunə mətni input sahəsinə yazır ✓.</summary>
        [RelayCommand]
        private void NumuneYukle()
        {
            SkriptMetni = _xidmet.Numune;
            StatusMetni = "📋 Nümunə yükləndi — «🔎 Yoxla» basın ✓";
        }

        /// <summary>🧹 Hər şeyi təmizləyir ✓.</summary>
        [RelayCommand]
        private void Temizle()
        {
            SkriptMetni = string.Empty;
            HesabatMetni = string.Empty;
            NeticeVar = false;
            Setirler.Clear();
            Kartlar.Clear();
            KreditSayi = 0;
            OdenisSayi = 0;
            UmumiKok = 0m;
            UmumiMaya = 0m;
            UmumiOdenilmis = 0m;
            UmumiQaliqNisye = 0m;
            UmumiMayaKar = 0m;
            UmumiCerime = 0m;
            UmumiMohlet = 0m;
            Kar1Basliq = "KAR (1)";
            Kar2Basliq = "KAR (2)";
            Kar3Basliq = "KAR (3)";
            Kar4Basliq = "KAR (4)";
            StatusMetni = "🧾 Kredit məlumatını yapışdırın → «🔎 Yoxla» → «📥 İdxal et» ✓";
        }

        /// <summary>🔎 Yoxlama — bazaya HEÇ NƏ yazılmır ✗✓✓.</summary>
        [RelayCommand]
        private async Task YoxlaAsync() => await IsletAsync(yaz: false);

        /// <summary>📥 İdxal — bütün məlumat bazaya YAZILIR ✓✓✓.</summary>
        [RelayCommand]
        private async Task IdxalEtAsync()
        {
            if (!Setirler.Any(s => s.Odenilib))
            {
                var davam = _dialoglar.Confirm(
                    "Heç bir faktiki ödəniş yazılmayıb — yalnız kredit və ödəniş PLANI yaradılacaq.\n\n" +
                    "Davam edilsin?",
                    "🧾 Kredit idxalı — təsdiq");

                if (!davam)
                {
                    return;
                }
            }

            await IsletAsync(yaz: true);
        }

        /// <summary>Yoxlama və idxal üçün ÜMUMİ məntiq ✓.</summary>
        private async Task IsletAsync(bool yaz)
        {
            if (string.IsNullOrWhiteSpace(SkriptMetni))
            {
                _dialoglar.ShowWarning("Mətn boşdur — kredit məlumatını yapışdırın.", "🧾 Kredit idxalı");
                return;
            }

            if (yaz)
            {
                var tesdiq = _dialoglar.Confirm(
                    "Aşağıdakı məlumatlar proqrama YAZILACAQ:\n\n" +
                    "  • 🚘 avtomobillər (nömrəyə görə — mövcudsa təkrar yaradılmır ✓)\n" +
                    "  • 📄 kredit müqavilələri\n" +
                    "  • 💰 faktiki ödənişlər + tərəfdaş bölgüsü\n\n" +
                    "Davam edilsin?",
                    "🧾 Kredit idxalı — təsdiq");

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
                    ? await _xidmet.IcraEtAsync(SkriptMetni, MaasinXeyiriniBol)
                    : _xidmet.Yoxla(SkriptMetni);

                HesabatMetni = string.Join(Environment.NewLine, netice.Setirler);
                Kartlar.Clear();
                Setirler.Clear();

                foreach (var kart in netice.Kartlar)
                {
                    Kartlar.Add(kart);

                    foreach (var setir in kart.Setirler)
                    {
                        Setirler.Add(setir);
                    }
                }

                var ilk = netice.Kartlar.FirstOrDefault();

                Kar1Basliq = string.IsNullOrWhiteSpace(ilk?.Kar1Ad) ? "KAR (1)" : "KAR · " + ilk!.Kar1Ad;
                Kar2Basliq = string.IsNullOrWhiteSpace(ilk?.Kar2Ad) ? "KAR (2)" : "KAR · " + ilk!.Kar2Ad;
                Kar3Basliq = string.IsNullOrWhiteSpace(ilk?.Kar3Ad) ? "KAR (3)" : "KAR · " + ilk!.Kar3Ad;
                Kar4Basliq = string.IsNullOrWhiteSpace(ilk?.Kar4Ad) ? "KAR (4)" : "KAR · " + ilk!.Kar4Ad;

                KreditSayi = netice.Kartlar.Count;
                OdenisSayi = Setirler.Count(s => s.Odenilib);
                UmumiKok = netice.Kartlar.Sum(k => k.Kok);
                UmumiMaya = netice.Kartlar.Sum(k => k.Maya);
                UmumiOdenilmis = netice.Kartlar.Sum(k => k.Odenilmis);
                UmumiQaliqNisye = netice.Kartlar.Sum(k => k.QaliqNisye);
                UmumiMayaKar = Setirler.Where(s => s.Odenilib).Sum(s => s.Menfeet);
                UmumiCerime = netice.Kartlar.Sum(k => k.CerimeCemi);
                UmumiMohlet = netice.Kartlar.Sum(k => k.MohletCemi);

                NeticeVar = netice.Kartlar.Count > 0;

                if (!netice.Ugurlu)
                {
                    StatusMetni = "❌ " + (netice.Xeta ?? "Mətn işlənmədi");
                    _dialoglar.ShowError(netice.Xeta ?? "Mətn işlənmədi.", "🧾 Kredit idxalı — xəta");
                    return;
                }

                if (yaz)
                {
                    StatusMetni =
                        $"✅ İDXAL TAMAMLANDI ✓ — {netice.KreditSayi} kredit · {netice.MasinSayi} yeni avtomobil · " +
                        $"{netice.OdenisSayi} ödəniş · {netice.CerimeSayi} cərimə · {netice.MohletSayi} möhlət" +
                        (netice.KecirilenKredit > 0 ? $" · {netice.KecirilenKredit} keçildi ⏭️" : string.Empty);

                    _logger.LogInformation(
                        "🧾 Kredit idxalı bitti: {Kredit} kredit · {Odenis} ödəniş · {Pay} pay ✓",
                        netice.KreditSayi, netice.OdenisSayi, netice.PaySayi);

                    IdxalBitdi?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    StatusMetni =
                        $"🔎 YOXLAMA BİTDİ ✓ — {netice.KreditSayi} kredit · {OdenisSayi} ödəniş · " +
                        $"qalıq nisyə {UmumiQaliqNisye:N2} ₼ · «📥 İdxal et» ilə yazın";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit idxalı zamanı gözlənilməz xəta.");
                StatusMetni = "❌ Xəta: " + ex.Message;
                _dialoglar.ShowError("Kredit idxalı alınmadı: " + ex.Message, "🧾 Kredit idxalı — xəta");
            }
            finally
            {
                IsBusy = false;
                _qapi.Release();
            }
        }
    }
}
