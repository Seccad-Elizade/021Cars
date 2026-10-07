using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// «👥 Tərəfdaşlar» tabının ViewModel-i — TƏRƏFDAŞ BÖLGÜSÜ mərkəzi.
    /// <para>
    /// Bölmələr:
    /// <list type="number">
    ///   <item><b>Tərəfdaş idarəsi</b> — şəxs <b>əlavə et / sil</b>, <b>faiz təyin et</b>.</item>
    ///   <item><b>Ödənişlər</b> — tərəfdaşlara <b>xaric edilən pullar</b>.</item>
    ///   <item><b>Tərəfdaş kartı</b> — qazanılmış / verilmiş / qalıq / dövriyyə.</item>
    ///   <item><b>Bölgü jurnalı</b> — BÜTÜN mənbələrdən: hansı maşından, hansı ayda, kimə nə qədər.</item>
    ///   <item><b>Aylıq dövriyyə</b> — ay-ay gəlir və ödəniş mənzərəsi.</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed partial class PartnersViewModel : ObservableObject
    {
        private readonly IPartnerService _partnerService;
        private readonly IExportService _export;
        private readonly IDialogService _dialogs;
        private readonly ILogger<PartnersViewModel> _logger;

        /// <summary>Eyni anda iki yükləmənin qarşısını alır.</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        // Dövr üçün xam məlumat (filtrləmə bunların üzərində aparılır).
        private List<PartnerLedgerRow> _ledgerAll = new();

        /// <summary>
        /// KART CƏDVƏLİ — <b>BÜTÜN VAXT</b> (tarixçə).
        /// <para>
        /// ⚠ Dövr filtrinə BAXMAYARAQ həmişə tam mənzərəni saxlayır ✓
        /// Beləliklə keçən ay qazanc olmasa da ümumi balans itmir ✗.
        /// </para>
        /// </summary>
        private List<PartnerCardRow> _cardsAllTime = new();

        /// <summary>Seçilmiş DÖVR üzrə kart rəqəmləri (ümumi ilə birləşdirilir).</summary>
        private List<PartnerCardRow> _cardsPeriod = new();

        private List<PartnerMonthlyRow> _monthlyAll = new();
        private List<PartnerPayment> _paymentsAll = new();

        // ====================================================================
        //  KOLLEKSİYALAR
        // ====================================================================

        /// <summary>Tərəfdaşlar — burada əlavə/silmə/faiz dəyişikliyi edilir.</summary>
        public ObservableCollection<Partner> Partners { get; } = new();

        /// <summary>Tərəfdaş kartları: qazanılmış / verilmiş / qalıq / dövriyyə.</summary>
        public ObservableCollection<PartnerCardRow> Cards { get; } = new();

        /// <summary>Bölgü jurnalı: hansı maşından, hansı ayda, kimə nə qədər.</summary>
        public ObservableCollection<PartnerLedgerRow> Ledger { get; } = new();

        /// <summary>Aylıq dövriyyə.</summary>
        public ObservableCollection<PartnerMonthlyRow> Monthly { get; } = new();

        /// <summary>Tərəfdaşlara xaric edilən pullar.</summary>
        public ObservableCollection<PartnerPayment> Payments { get; } = new();

        /// <summary>Filtr üçün tərəfdaş adları («Hamısı» + adlar).</summary>
        public ObservableCollection<string> TerefdasAdlari { get; } = new();

        /// <summary>Filtr üçün mənbə seçimləri.</summary>
        public ObservableCollection<string> MenbeSecimleri { get; } = new();

        /// <summary>Ödəniş formasi üçün ödəniş üsulları.</summary>
        public IReadOnlyList<string> OdenisUsullari { get; } = Catalog.PaymentMethods;

        // ====================================================================
        //  DÖVR FİLTRİ
        // ====================================================================

        /// <summary>Dövr seçimləri.</summary>
        public IReadOnlyList<string> RangeOptions { get; } = new[]
        {
            "Bu ay", "Keçən ay", "Bu il", "Son 12 ay", "Bütün vaxt", "Fərdi aralıq"
        };

        /// <summary>
        /// Seçilmiş dövr — <b>default «Bu ay»</b> ✓
        /// <para>
        /// Beləliklə tab açılanda cari ayın dövriyyəsi görünür; istifadəçi
        /// istəsə «Bütün vaxt» seçib ümumi dövriyyəyə baxa bilər ✓
        /// </para>
        /// </summary>
        [ObservableProperty] private string selectedRange = "Bu ay";
        [ObservableProperty] private DateTime? customFrom;
        [ObservableProperty] private DateTime? customTo;

        /// <summary>«Fərdi aralıq» seçilibsə tarix xanaları görünür.</summary>
        public bool IsCustomRange => SelectedRange == "Fərdi aralıq";

        /// <summary>Dövrün oxunaqlı təsviri: «01.01.2026 — 21.09.2026».</summary>
        [ObservableProperty] private string donemMetni = string.Empty;

        // ====================================================================
        //  YEKUN GÖSTƏRİCİLƏR  (dövr üzrə)
        // ====================================================================

        /// <summary>Dövr ərzində tərəfdaşlara düşən ÜMUMİ pay (₼).</summary>
        [ObservableProperty] private decimal totalQazanilmis;

        /// <summary>Dövr ərzində tərəfdaşlara VERİLƏN pul (₼).</summary>
        [ObservableProperty] private decimal totalVerilmis;

        /// <summary>Ödənilməli qalıq (₼).</summary>
        [ObservableProperty] private decimal totalQaliq;

        /// <summary>Dövr ərzindəki bölgü sayı.</summary>
        [ObservableProperty] private int totalBolguSayi;

        /// <summary>Dövrdə payı olan tərəfdaş sayı.</summary>
        [ObservableProperty] private int totalTerefdasSayi;

        /// <summary>Xərc artıqlaması (avans) varsa qırmızı nişan.</summary>
        public string QaliqMetni => $"{TotalQaliq:N2} ₼";

        /// <summary>Qalıq rəngi.</summary>
        public string QaliqReng => TotalQaliq > 0m ? "#34D399" : TotalQaliq < 0m ? "#F43F5E" : "#94A3B8";

        /// <summary>Bölgü jurnalı boşdursa xəbərdarlıq göstərilir.</summary>
        public bool HasLedger => Ledger.Count > 0;

        /// <summary>Jurnal boşdursa məlumat mətni.</summary>
        public bool HasNoLedger => Ledger.Count == 0;

        /// <summary>Kart cədvəli boşdursa.</summary>
        public bool HasCards => Cards.Count > 0;

        // ====================================================================
        //  FİLTRLƏR
        // ====================================================================

        [ObservableProperty] private string selectedTerefdas = "Hamısı";
        [ObservableProperty] private string selectedMenbe = "Bütün mənbələr";

        /// <summary>Jurnalda axtarış (maşın, müqavilə, müştəri).</summary>
        [ObservableProperty] private string axtaris = string.Empty;

        // ====================================================================
        //  YENİ TƏRƏFDAŞ FORMASI
        // ====================================================================

        [ObservableProperty] private string newPartnerAd = string.Empty;
        [ObservableProperty] private decimal newPartnerFaiz = 5m;
        [ObservableProperty] private bool newPartnerQalig;
        [ObservableProperty] private string newPartnerQeyd = string.Empty;

        // ====================================================================
        //  YENİ ÖDƏNİŞ FORMASI (tərəfdaşa pul verilməsi)
        // ====================================================================

        [ObservableProperty] private string paymentTerefdas = string.Empty;
        [ObservableProperty] private decimal paymentMebleg;
        [ObservableProperty] private DateTime? paymentTarix = DateTime.Today;
        [ObservableProperty] private string paymentUsulu = Catalog.PaymentMethods[0];
        [ObservableProperty] private string paymentQeyd = string.Empty;

        // ====================================================================
        //  SEÇİMLƏR
        // ====================================================================

        [ObservableProperty] private Partner? selectedPartner;
        [ObservableProperty] private PartnerCardRow? selectedCard;
        [ObservableProperty] private PartnerLedgerRow? selectedLedgerRow;
        [ObservableProperty] private PartnerPayment? selectedPayment;

        /// <summary>Əməliyyat gedir (düymələr bloklanır).</summary>
        [ObservableProperty] private bool isBusy;

        /// <summary>Status sətri — istifadəçiyə nə baş verdiyini bildirir.</summary>
        [ObservableProperty] private string statusMessage = "Hazır";

        public PartnersViewModel(
            IPartnerService partnerService,
            IExportService export,
            IDialogService dialogs,
            ILogger<PartnersViewModel> logger)
        {
            _partnerService = partnerService;
            _export = export;
            _dialogs = dialogs;
            _logger = logger;

            MenbeSecimleri.Add("Bütün mənbələr");
            MenbeSecimleri.Add("💰 Satış");
            MenbeSecimleri.Add("💳 Kredit");
            MenbeSecimleri.Add("🏷️ Kredit Gəliri");
            // ⚠️ GECİKMƏ CƏRİMƏSİ SİYAHIDAN ÇIXARILDI ✗✓✓ (v6.2.28)
            //  Cərimə YALNIZ «💵 Kassa»ya (gəlirə) yazılır ✓ — tərəfdaşlara
            //  BÖLÜNMÜR ✗ → bölgü jurnalında heç vaxt görünmür ✓✓✓
        }

        // ====================================================================
        //  DÖVR HESABI
        // ====================================================================

        /// <summary>Seçilmiş dövrün başlanğıc və son tarixi.</summary>
        private (DateTime From, DateTime To) GetRange()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);

            return SelectedRange switch
            {
                "Bu ay" => (monthStart, today),
                "Keçən ay" => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
                "Bu il" => (new DateTime(today.Year, 1, 1), today),
                "Son 12 ay" => (monthStart.AddMonths(-11), today),
                "Bütün vaxt" => (new DateTime(2000, 1, 1), today),
                "Fərdi aralıq" => (CustomFrom ?? monthStart.AddMonths(-1), CustomTo ?? today),
                _ => (new DateTime(2000, 1, 1), today)
            };
        }

        partial void OnSelectedRangeChanged(string value)
        {
            OnPropertyChanged(nameof(IsCustomRange));
            _ = LoadAsync();
        }

        partial void OnCustomFromChanged(DateTime? value)
        {
            if (IsCustomRange)
            {
                _ = LoadAsync();
            }
        }

        partial void OnCustomToChanged(DateTime? value)
        {
            if (IsCustomRange)
            {
                _ = LoadAsync();
            }
        }

        partial void OnSelectedTerefdasChanged(string value) => ApplyFilters();

        partial void OnSelectedMenbeChanged(string value) => ApplyFilters();

        partial void OnAxtarisChanged(string value) => ApplyFilters();

        private static bool Contains(string? value, string search)
            => !string.IsNullOrEmpty(value) &&
               value.Contains(search, StringComparison.OrdinalIgnoreCase);

        // ====================================================================
        //  YÜKLƏMƏ
        // ====================================================================

        /// <summary>Bütün tərəfdaş məlumatını bazadan yükləyir və hesabatları qurur.</summary>
        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                var (from, to) = GetRange();
                DonemMetni = $"{from:dd.MM.yyyy} — {to:dd.MM.yyyy}";

                // 1) Tərəfdaşlar (əlavə / silmə / faiz üçün).
                var partners = await _partnerService.GetPartnersAsync();

                Partners.Clear();
                foreach (var partner in partners)
                {
                    Partners.Add(partner);
                }

                // 2) Analitika — BÜTÜN mənbələrdən.
                _ledgerAll = (await _partnerService.BuildLedgerAsync(from, to)).ToList();
                _monthlyAll = (await _partnerService.BuildMonthlyAsync(from, to)).ToList();
                _paymentsAll = (await _partnerService.GetPaymentsAsync()).ToList();

                // 👥 KARTLAR: ÜMUMİ (bütün vaxt) + seçilmiş DÖVR rəqəmləri
                //  ⚠ Kartlar həmişə BÜTÜN VAXTI göstərir — dövr filtrinə baxmır ✗.
                //    Beləliklə keçən ay qazanc olmasa da tarixçə və balans qalır ✓
                var butunVaxBaslangic = new DateTime(2000, 1, 1);

                _cardsAllTime = (await _partnerService
                    .BuildCardsAsync(butunVaxBaslangic, DateTime.Today)).ToList();

                _cardsPeriod = (await _partnerService
                    .BuildCardsAsync(from, to)).ToList();

                BirlestirDonemRagmeleri();

                // 3) Filtr siyahıları + cədvəllər.
                RebuildNameFilters();
                ApplyFilters();

                // 4) Ödəniş tarixçəsi.
                Payments.Clear();
                foreach (var payment in _paymentsAll)
                {
                    Payments.Add(payment);
                }

                // 5) Yekun göstəricilər.
                var totals = await _partnerService.BuildTotalsAsync(from, to);
                TotalQazanilmis = totals.Qazanilmis;
                TotalVerilmis = totals.Verilmis;
                TotalQaliq = totals.Qaliq;
                TotalBolguSayi = totals.BolguSayi;
                TotalTerefdasSayi = totals.TerefdasSayi;

                StatusMessage =
                    $"Hazır — {Partners.Count} tərəfdaş · {_ledgerAll.Count} bölgü sətri · {Cards.Count} kart";

                _logger.LogInformation(
                    "👥 Tərəfdaş tabı: {Terefdas} tərəfdaş, {Bolgu} bölgü, qazanılmış {Qaz:N2} ₼, verilmiş {Ver:N2} ₼",
                    Partners.Count, _ledgerAll.Count, TotalQazanilmis, TotalVerilmis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş məlumatları yüklənərkən xəta baş verdi.");
                _dialogs.ShowError("Tərəfdaş məlumatları yüklənə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        /// <summary>
        /// Seçilmiş DÖVR rəqəmlərini <b>ÜMUMİ (bütün vaxt)</b> kartlarına birləşdirir.
        /// <para>
        /// Nəticədə hər kart həm <b>ümumi balansı</b> (tarixçə), həm də
        /// <b>cari dövrün</b> qazanc/ödənişlərini göstərir ✓
        /// </para>
        /// </summary>
        private void BirlestirDonemRagmeleri()
        {
            var donemMap = _cardsPeriod
                .GroupBy(c => c.Terefdas, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var card in _cardsAllTime)
            {
                if (donemMap.TryGetValue(card.Terefdas, out var donem))
                {
                    card.DonemQazanilmis = donem.Qazanilmis;
                    card.DonemVerilmis = donem.Verilmis;
                    card.DonemBolguSayi = donem.BolguSayi;
                }
            }
        }

        /// <summary>Filtr siyahılarını (tərəfdaş adları) yenidən qurur.</summary>
        private void RebuildNameFilters()
        {
            var kohne = SelectedTerefdas;

            TerefdasAdlari.Clear();
            TerefdasAdlari.Add("Hamısı");

            foreach (var ad in Partners.Select(p => p.Ad).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                TerefdasAdlari.Add(ad);
            }

            SelectedTerefdas = TerefdasAdlari.Contains(kohne) ? kohne : "Hamısı";
        }

        /// <summary>Filtrləri tətbiq edib cədvəlləri yenidən doldurur.</summary>
        private void ApplyFilters()
        {
            var jurnal = _ledgerAll.AsEnumerable();

            if (!string.Equals(SelectedTerefdas, "Hamısı", StringComparison.Ordinal))
            {
                jurnal = jurnal.Where(r =>
                    string.Equals(r.Terefdas, SelectedTerefdas, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(SelectedMenbe, "Bütün mənbələr", StringComparison.Ordinal))
            {
                jurnal = jurnal.Where(r => r.MenbeNisani == SelectedMenbe);
            }

            var axtaris = (Axtaris ?? string.Empty).Trim();
            if (axtaris.Length > 0)
            {
                jurnal = jurnal.Where(r =>
                    Contains(r.Avtomobil, axtaris) ||
                    Contains(r.SenedMetni, axtaris) ||
                    Contains(r.Terefdas, axtaris) ||
                    Contains(r.Menbe, axtaris));
            }

            Ledger.Clear();
            foreach (var row in jurnal)
            {
                Ledger.Add(row);
            }

            // Kartlar — BÜTÜN VAXT (tarixçə) + tərəfdaş filtrinə uyğun ✓
            //  ⚠ Dövr filtrinə BAXMIR — ümumi balans həmişə görünür ✓
            var kartlar = _cardsAllTime.AsEnumerable();
            if (!string.Equals(SelectedTerefdas, "Hamısı", StringComparison.Ordinal))
            {
                kartlar = kartlar.Where(c =>
                    string.Equals(c.Terefdas, SelectedTerefdas, StringComparison.OrdinalIgnoreCase));
            }

            Cards.Clear();
            var sira = 0;
            foreach (var card in kartlar.OrderByDescending(c => c.Qazanilmis).ThenBy(c => c.Terefdas))
            {
                card.SiraNomresi = ++sira;   // 🥇🥈🥉 nişanı üçün
                Cards.Add(card);
            }

            // Aylıq dövriyyə — tərəfdaş filtrinə uyğun.
            var aylar = _monthlyAll.AsEnumerable();
            if (!string.Equals(SelectedTerefdas, "Hamısı", StringComparison.Ordinal))
            {
                aylar = aylar.Where(m =>
                    string.Equals(m.Terefdas, SelectedTerefdas, StringComparison.OrdinalIgnoreCase));
            }

            Monthly.Clear();
            foreach (var ay in aylar)
            {
                Monthly.Add(ay);
            }

            OnPropertyChanged(nameof(HasLedger));
            OnPropertyChanged(nameof(HasNoLedger));
            OnPropertyChanged(nameof(HasCards));
        }

        partial void OnTotalQaliqChanged(decimal value)
        {
            OnPropertyChanged(nameof(QaliqMetni));
            OnPropertyChanged(nameof(QaliqReng));
        }

        /// <summary>Tərəfdaş seçildikdə ödəniş forması onun adı ilə doldurulur.</summary>
        partial void OnSelectedPartnerChanged(Partner? value)
        {
            if (value is not null)
            {
                PaymentTerefdas = value.Ad;
            }
        }

        // ====================================================================
        //  TƏRƏFDAŞ ƏLAVƏ ET / SİL / FAİZ YADDA SAXLA
        // ====================================================================

        /// <summary>Yeni tərəfdaş əlavə edir (ad + faiz + qalıq payı).</summary>
        [RelayCommand]
        private async Task AddPartnerAsync()
        {
            var ad = (NewPartnerAd ?? string.Empty).Trim();

            if (ad.Length == 0)
            {
                _dialogs.ShowWarning("Tərəfdaşın adını yazın.");
                return;
            }

            if (Partners.Any(p => string.Equals(p.Ad, ad, StringComparison.OrdinalIgnoreCase)))
            {
                _dialogs.ShowWarning($"«{ad}» adlı tərəfdaş artıq siyahıdadır.");
                return;
            }

            try
            {
                var partner = await _partnerService.AddPartnerAsync(
                    ad, NewPartnerFaiz, NewPartnerQalig, NewPartnerQeyd);

                NewPartnerAd = string.Empty;
                NewPartnerFaiz = 5m;
                NewPartnerQalig = false;
                NewPartnerQeyd = string.Empty;

                _dialogs.ShowInfo(
                    $"✅ «{partner.Ad}» tərəfdaşlar siyahısına əlavə olundu.\n" +
                    $"Pay: {partner.FaizMetni}\n\n" +
                    "Yeni şəxs bütün bölgü panellərində (Satış, Kredit, Əlavə Gəlir) dərhal görünəcək.");

                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş əlavə edilərkən xəta.");
                _dialogs.ShowError("Tərəfdaş əlavə edilə bilmədi: " + ex.Message);
            }
        }

        /// <summary>Seçilmiş tərəfdaşı siyahıdan silir.</summary>
        [RelayCommand]
        private async Task DeletePartnerAsync()
        {
            var partner = SelectedPartner;

            if (partner is null)
            {
                _dialogs.ShowWarning("Silmək üçün cədvəldən tərəfdaş seçin.");
                return;
            }

            if (!_dialogs.Confirm(
                $"«{partner.Ad}» tərəfdaşını silmək istəyirsiniz?\n\n" +
                "• Keçmiş bölgülər və ödənişlər TARİXÇƏDƏ QALIR.\n" +
                "• Yalnız gələcək bölgülərdə iştirak etməyəcək."))
            {
                return;
            }

            try
            {
                await _partnerService.DeletePartnerAsync(partner.Id);
                SelectedPartner = null;
                _logger.LogInformation("👥 Tərəfdaş silindi: {Ad}", partner.Ad);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş silinərkən xəta.");
                _dialogs.ShowError("Tərəfdaş silinə bilmədi: " + ex.Message);
            }
        }

        /// <summary>Cədvəldə dəyişdirilmiş faizləri / adları bazaya yazır.</summary>
        [RelayCommand]
        private async Task SavePartnersAsync()
        {
            try
            {
                await _partnerService.SavePartnersAsync(Partners.ToList());

                var xulase = string.Join(" · ", Partners
                    .OrderBy(p => p.Sira)
                    .Select(p => $"{p.Ad} {p.FaizMetni}"));

                _dialogs.ShowInfo(
                    "💾 Tərəfdaş siyahısı yadda saxlanıldı.\n\n" +
                    "Yeni standart paylar:\n" + xulase + "\n\n" +
                    "«♻️ Default faizlər» düymələri artıq bu dəyərləri istifadə edəcək.");

                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş siyahısı saxlanılarkən xəta.");
                _dialogs.ShowError("Yadda saxlana bilmədi: " + ex.Message);
            }
        }

        /// <summary>Faizləri əvvəlki (bazadakı) qiymətlərə qaytarır.</summary>
        [RelayCommand]
        private async Task RevertPartnersAsync()
        {
            if (!_dialogs.Confirm("Cədvəldəki dəyişiklikləri ləğv edib bazadakı dəyərləri bərpa edək?"))
            {
                return;
            }

            await LoadAsync();
        }

        // ====================================================================
        //  ÖDƏNİŞLƏR  (tərəfdaşa pul verilməsi)
        // ====================================================================

        /// <summary>Tərəfdaşa verilən pulu qeydə alır.</summary>
        [RelayCommand]
        private async Task AddPaymentAsync()
        {
            var ad = (PaymentTerefdas ?? string.Empty).Trim();

            if (ad.Length == 0)
            {
                _dialogs.ShowWarning("Ödəniş üçün tərəfdaş seçin.");
                return;
            }

            if (PaymentMebleg <= 0m)
            {
                _dialogs.ShowWarning("Ödəniş məbləği sıfırdan böyük olmalıdır.");
                return;
            }

            try
            {
                await _partnerService.AddPaymentAsync(new PartnerPayment
                {
                    Terefdas = ad,
                    Mebleg = PaymentMebleg,
                    Tarix = PaymentTarix ?? DateTime.Today,
                    OdenisUsulu = PaymentUsulu,
                    Qeyd = PaymentQeyd
                });

                PaymentMebleg = 0m;
                PaymentQeyd = string.Empty;

                await LoadAsync();
                StatusMessage = $"💸 «{ad}» tərəfdaşına ödəniş qeydə alındı.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödəniş qeydə alınarkən xəta.");
                _dialogs.ShowError("Ödəniş qeydə alına bilmədi: " + ex.Message);
            }
        }

        /// <summary>Seçilmiş ödənişi silir.</summary>
        [RelayCommand]
        private async Task DeletePaymentAsync()
        {
            var payment = SelectedPayment;

            if (payment is null)
            {
                _dialogs.ShowWarning("Silmək üçün cədvəldən ödəniş seçin.");
                return;
            }

            if (!_dialogs.Confirm(
                $"{payment.TarixMetni} — «{payment.Terefdas}» — {payment.MeblegMetni}\n\n" +
                "Bu ödəniş qeydini silmək istəyirsiniz?"))
            {
                return;
            }

            try
            {
                await _partnerService.DeletePaymentAsync(payment.Id);
                SelectedPayment = null;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ödəniş silinərkən xəta.");
                _dialogs.ShowError("Ödəniş silinə bilmədi: " + ex.Message);
            }
        }

        /// <summary>Ödəniş formasını təmizləyir.</summary>
        [RelayCommand]
        private void ClearPaymentForm()
        {
            PaymentTerefdas = string.Empty;
            PaymentMebleg = 0m;
            PaymentTarix = DateTime.Today;
            PaymentUsulu = Catalog.PaymentMethods[0];
            PaymentQeyd = string.Empty;
        }

        /// <summary>Filtrləri sıfırlayır.</summary>
        [RelayCommand]
        private void ResetFilters()
        {
            SelectedTerefdas = "Hamısı";
            SelectedMenbe = "Bütün mənbələr";
            Axtaris = string.Empty;
        }

        // ====================================================================
        //  İXRAC  (Excel / PDF / HTML)
        // ====================================================================

        private string ReportFileName(string uzanti)
            => $"Terefdas-Bolgusu-{DateTime.Now:yyyy-MM-dd_HHmm}.{uzanti}";

        /// <summary>Tərəfdaş hesabatını Excel (CSV) faylına yazır.</summary>
        [RelayCommand]
        private async Task ExportExcelAsync()
        {
            var path = _dialogs.ShowSaveFileDialog("Excel (CSV)|*.csv", ReportFileName("csv"));

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                await _export.ExportPartnersAsync(Cards, Ledger, Payments, path);
                _dialogs.ShowInfo("✅ Excel hesabatı hazırlandı:\n" + path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş Excel ixracı alınmadı.");
                _dialogs.ShowError("Excel hesabatı yazıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>Hesabatı PDF kimi yazır (WebView2; mümkün olmasa HTML + brauzer).</summary>
        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            var path = _dialogs.ShowSaveFileDialog("PDF hesabatı|*.pdf", ReportFileName("pdf"));

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var netice = await ReportService.WritePdfAsync(BuildHtmlReport(), path);
                _dialogs.ShowInfo("✅ Hesabat hazırlandı:\n" + netice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş PDF ixracı alınmadı.");
                _dialogs.ShowError("PDF hesabatı yazıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>Hesabatı HTML faylı kimi yazır.</summary>
        [RelayCommand]
        private async Task ExportHtmlAsync()
        {
            var path = _dialogs.ShowSaveFileDialog("HTML hesabatı|*.html", ReportFileName("html"));

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                await File.WriteAllTextAsync(path, BuildHtmlReport(), new System.Text.UTF8Encoding(false));
                _logger.LogInformation("👥 Tərəfdaş HTML hesabatı yazıldı: {Path}", path);
                _dialogs.ShowInfo("✅ HTML hesabatı hazırlandı:\n" + path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tərəfdaş HTML ixracı alınmadı.");
                _dialogs.ShowError("HTML hesabatı yazıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>Çap / PDF üçün peşəkar HTML hesabat qurur.</summary>
        private string BuildHtmlReport()
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("<!DOCTYPE html><html lang=\"az\"><head><meta charset=\"utf-8\">");
            sb.Append("<title>Tərəfdaş Bölgüsü Hesabatı</title><style>");
            sb.Append("@page{size:A4;margin:13mm 11mm;}");
            sb.Append("body{font-family:'Segoe UI',Arial,sans-serif;color:#1e293b;font-size:11px;margin:0;}");
            sb.Append("h1{font-size:19px;margin:0;}");
            sb.Append("h2{font-size:13px;margin:19px 0 7px;padding:7px 11px;color:#fff;background:#1e293b;border-radius:6px;}");
            sb.Append(".head{border-bottom:3px solid #1e293b;padding-bottom:10px;margin-bottom:5px;}");
            sb.Append(".sub{color:#64748b;font-size:11px;margin-top:3px;}");
            sb.Append(".right{text-align:right;color:#64748b;font-size:10px;}");
            sb.Append("table{width:100%;border-collapse:collapse;margin-top:5px;}");
            sb.Append("th{background:#f1f5f9;text-align:left;padding:6px 8px;font-size:10px;");
            sb.Append("text-transform:uppercase;color:#475569;border-bottom:2px solid #cbd5e1;}");
            sb.Append("td{padding:5px 8px;border-bottom:1px solid #e2e8f0;}");
            sb.Append(".num{text-align:right;font-variant-numeric:tabular-nums;}");
            sb.Append(".pos{color:#047857;font-weight:600;}.neg{color:#b91c1c;font-weight:600;}");
            sb.Append(".tot td{font-weight:700;background:#f8fafc;border-top:2px solid #94a3b8;}");
            sb.Append(".foot{margin-top:20px;padding-top:8px;border-top:2px solid #1e293b;");
            sb.Append("font-size:10px;color:#64748b;display:flex;justify-content:space-between;}");
            sb.Append("</style></head><body>");

            // ---------- BAŞLIQ ----------
            sb.Append("<div class=\"head\"><div>");
            sb.Append("<h1>👥 TƏRƏFDAŞ BÖLGÜSÜ HESABATI</h1>");
            sb.Append($"<div class=\"sub\">Dövr: <b>{DonemMetni}</b> · ");
            sb.Append($"Tərəfdaş: <b>{TotalTerefdasSayi}</b> · Bölgü: <b>{TotalBolguSayi}</b></div>");
            sb.Append("</div><div class=\"right\">");
            sb.Append($"Hazırlandı: {DateTime.Now:dd.MM.yyyy HH:mm}<br>Avtomobil Parkı v6.0</div></div>");

            // ---------- 1. KARTLAR ----------
            sb.Append("<h2>1 · TƏRƏFDAŞ KARTLARI — qazanılmış / verilmiş / qalıq</h2><table>");
            sb.Append("<tr><th>Tərəfdaş</th><th>Faiz</th><th class=\"num\">Satışdan</th>");
            sb.Append("<th class=\"num\">Kreditdən</th><th class=\"num\">Əlavə Gəlirdən</th>");
            sb.Append("<th class=\"num\">Qazanılmış</th><th class=\"num\">Verilmiş</th>");
            sb.Append("<th class=\"num\">Qalıq</th><th class=\"num\">Bölgü</th><th>Dövr</th></tr>");

            foreach (var card in Cards)
            {
                var cls = card.Qaliq >= 0m ? "pos" : "neg";
                sb.Append("<tr>");
                sb.Append($"<td><b>{ReportBuilder.Esc(card.Terefdas)}</b></td><td>{card.FaizMetni}</td>");
                sb.Append($"<td class=\"num\">{ReportBuilder.Money(card.SatisQazanc)}</td>");
                sb.Append($"<td class=\"num\">{ReportBuilder.Money(card.KreditQazanc)}</td>");
                sb.Append($"<td class=\"num\">{ReportBuilder.Money(card.GelirQazanc)}</td>");
                sb.Append($"<td class=\"num\"><b>{ReportBuilder.Money(card.Qazanilmis)}</b></td>");
                sb.Append($"<td class=\"num\">{ReportBuilder.Money(card.Verilmis)}</td>");
                sb.Append($"<td class=\"num {cls}\"><b>{ReportBuilder.Money(card.Qaliq)}</b></td>");
                sb.Append($"<td class=\"num\">{card.BolguSayi}</td>");
                sb.Append($"<td>{ReportBuilder.Esc(card.DonemMetni)}</td></tr>");
            }

            sb.Append("<tr class=\"tot\"><td colspan=\"5\">CƏMİ</td>");
            sb.Append($"<td class=\"num\">{ReportBuilder.Money(TotalQazanilmis)}</td>");
            sb.Append($"<td class=\"num\">{ReportBuilder.Money(TotalVerilmis)}</td>");
            sb.Append($"<td class=\"num\">{ReportBuilder.Money(TotalQaliq)}</td>");
            sb.Append($"<td class=\"num\">{TotalBolguSayi}</td><td></td></tr></table>");

            sb.Append(BuildHtmlLedger());
            sb.Append(BuildHtmlPayments());

            sb.Append("<div class=\"foot\"><div>Avtomobil Parkı · Tərəfdaş Bölgüsü Modulu</div>");
            sb.Append($"<div>{DateTime.Now:dd.MM.yyyy HH:mm}</div></div>");
            sb.Append("</body></html>");

            return sb.ToString();
        }

        /// <summary>Hesabatın 2-ci bölməsi: bölgü jurnalı.</summary>
        private string BuildHtmlLedger()
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("<h2>2 · BÖLGÜ JURNALI — hansı maşından, hansı ayda, kimə nə qədər</h2><table>");
            sb.Append("<tr><th>Tarix</th><th>Dövr</th><th>Mənbə</th><th>Avtomobil</th>");
            sb.Append("<th>Müqavilə / Müştəri</th><th>Tərəfdaş</th><th>Faiz</th>");
            sb.Append("<th class=\"num\">Xeyir (baza)</th><th class=\"num\">Pay</th></tr>");

            foreach (var row in Ledger)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{row.TarixMetni}</td><td>{ReportBuilder.Esc(row.AyMetni)}</td>");
                sb.Append($"<td>{row.MenbeNisani}</td><td>{ReportBuilder.Esc(row.Avtomobil)}</td>");
                sb.Append($"<td>{ReportBuilder.Esc(row.SenedMetni)}</td>");
                sb.Append($"<td><b>{ReportBuilder.Esc(row.Terefdas)}</b></td><td>{row.FaizMetni}</td>");
                sb.Append($"<td class=\"num\">{ReportBuilder.Money(row.Baza)}</td>");
                sb.Append($"<td class=\"num\"><b>{ReportBuilder.Money(row.Mebleg)}</b></td></tr>");
            }

            if (Ledger.Count == 0)
            {
                sb.Append("<tr><td colspan=\"9\" style=\"color:#94a3b8;font-style:italic;\">");
                sb.Append("Bu dövrdə bölgü qeydi yoxdur.</td></tr>");
            }

            sb.Append("</table>");
            return sb.ToString();
        }

        /// <summary>Hesabatın 3-cü bölməsi: tərəfdaşlara verilən pullar.</summary>
        private string BuildHtmlPayments()
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("<h2>3 · TƏRƏFDAŞLARA VERİLƏN PULLAR</h2><table>");
            sb.Append("<tr><th>Tarix</th><th>Dövr</th><th>Tərəfdaş</th>");
            sb.Append("<th class=\"num\">Məbləğ</th><th>Ödəniş Üsulu</th><th>Qeyd</th></tr>");

            foreach (var payment in Payments)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{payment.TarixMetni}</td><td>{ReportBuilder.Esc(payment.AyMetni)}</td>");
                sb.Append($"<td><b>{ReportBuilder.Esc(payment.Terefdas)}</b></td>");
                sb.Append($"<td class=\"num\">{ReportBuilder.Money(payment.Mebleg)}</td>");
                sb.Append($"<td>{ReportBuilder.Esc(payment.OdenisUsulu)}</td>");
                sb.Append($"<td>{ReportBuilder.Esc(payment.Qeyd)}</td></tr>");
            }

            if (Payments.Count == 0)
            {
                sb.Append("<tr><td colspan=\"6\" style=\"color:#94a3b8;font-style:italic;\">");
                sb.Append("Hələ tərəfdaşa pul verilməyib.</td></tr>");
            }

            sb.Append("<tr class=\"tot\"><td colspan=\"3\">CƏMİ VERİLİB</td>");
            sb.Append($"<td class=\"num\">{ReportBuilder.Money(Payments.Sum(p => p.Mebleg))}</td>");
            sb.Append("<td colspan=\"2\"></td></tr></table>");
            return sb.ToString();
        }
    }
}
