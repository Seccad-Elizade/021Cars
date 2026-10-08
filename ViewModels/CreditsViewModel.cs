using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Hosting;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>"Kreditlər &amp; Ödəniş Qrafiki" tabının ViewModel-i.</summary>
    public sealed partial class CreditsViewModel : ObservableValidator
    {
        private readonly ICreditService _creditService;
        private readonly ICarService _carService;
        private readonly IMohletService _mohletService;
        private readonly IDialogService _dialogs;
        private readonly ILogger<CreditsViewModel> _logger;

        /// <summary>Eyni anda iki yükləmənin işləməsinin qarşısını alır (siyahıların ikiqat olmasını önləyir).</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Filtrləmə zamanı geri-əlaqə (re-entrancy) dövrünün qarşısını alır.</summary>
        private bool _applyingFilter;

        /// <summary>Müqavilə filtrinin geri-əlaqə (re-entrancy) dövrünün qarşısını alır.</summary>
        private bool _applyingContractFilter;

        /// <summary>Ödəniş qrafiki kredit seçiminin filtr qoruyucusu.</summary>
        private bool _applyingScheduleFilter;

        private List<CreditTransaction> _creditTransactions = new();

        /// <summary>
        /// BÜTÜN kreditlər — bağlı/transfer olunanlar DA daxil ✓
        /// (📄 PDF ixracı üçün lazımdır: `Credits` siyahısı bağlıları süzür ✗✓✓)
        /// </summary>
        private List<Credit> _butunKreditler = new();

        /// <summary>
        /// 💰 «PUL» sahəsinin AVTOMATİK doldurulduğu son kreditin Id-si ✓
        /// (manual düzəliş yükləmələrdə İTMƏSİN ✗✓✓).
        /// </summary>
        private int _sonTransferKreditId;
        private bool _suppressPaymentEvents;
        private decimal _lastAutoMebleg;

        /// <summary>
        /// Kredit Id-si → həmin kreditin <b>XEYİR</b> (maşın mənfəəti) bölgüsü.
        /// Yükləmə zamanı bir dəfə doldurulur (əlavə sorğu olmasın).
        /// </summary>
        private Dictionary<int, IReadOnlyList<PartnerShare>> _creditSharesMap = new();

        public ObservableCollection<Credit> Credits { get; } = new();

        /// <summary>Axtarış mətninə uyğun kredit müqavilələri (cədvəldə canlı filtrasiya).</summary>
        public ObservableCollection<Credit> FilteredContracts { get; } = new();

        [ObservableProperty] private string contractSearchText = string.Empty;

        /// <summary>Ödəniş qrafiki tabındaki kredit seçimi üçün filtrlənmiş siyahı.</summary>
        public ObservableCollection<Credit> FilteredScheduleCredits { get; } = new();

        [ObservableProperty] private string scheduleSearchText = string.Empty;

        public ObservableCollection<PaymentRow> Schedule { get; } = new();

        // ====================================================================
        //  👥 SEÇİLMİŞ KREDİT ÜZRƏ TƏRƏFDAŞ BÖLGÜSÜ
        // --------------------------------------------------------------------
        //  1) XEYİR BÖLGÜSÜ  — maşının mənfəəti (satış qiyməti − maya) kredit
        //     ilk dəfə veriləndə tərəfdaşlar arasında bölünür.
        //  2) AYLIK BÖLGÜ     — hər ayın kredit ödənişində bölünən pullar
        //     (məs. Zaur 95,40 ₼ · Asif 667,80 ₼).
        // ====================================================================

        /// <summary>Seçilmiş kreditin «XEYİR» (maşın mənfəəti) tərəfdaş bölgüsü.</summary>
        public ObservableCollection<PartnerShare> SelectedCreditShares { get; } = new();

        /// <summary>Seçilmiş kreditin AYLIK tərəfdaş bölgüləri (ay-ay).</summary>
        public ObservableCollection<CreditMonthlyShareRow> SelectedMonthlyShares { get; } = new();

        /// <summary>Xeyir bölgüsü varmı? (panel göstərilsin?)</summary>
        public bool HasCreditShares => SelectedCreditShares.Count > 0;

        /// <summary>Aylıq bölgü varmı?</summary>
        public bool HasMonthlyShares => SelectedMonthlyShares.Count > 0;

        /// <summary>Xeyir bölgüsünün cəmi (₼) — bazaya bərabər olmalıdır.</summary>
        public decimal SelectedCreditSharesTotal => SelectedCreditShares.Sum(s => s.Mebleg);

        /// <summary>Xeyir bölgüsünün cəmi mətni.</summary>
        public string SelectedCreditSharesTotalMetni => $"{SelectedCreditSharesTotal:N2} ₼";

        /// <summary>Xeyir bölgüsünün bir sətirdə xülasəsi.</summary>
        public string SelectedCreditSharesXulase => SelectedCreditShares.Count == 0
            ? "Bu kredit üzrə maşın mənfəəti bölünməmişdir."
            : string.Join(" · ", SelectedCreditShares
                .OrderBy(s => s.Sira)
                .Select(s => $"{s.Terefdas} {s.Mebleg:N2} ₼"));

        /// <summary>Xeyir bölgüsünün başlıq mətni: «XEYİR = Satış − Maya = 1 594,00 ₼».</summary>
        public string SelectedCreditXeyirMetni
        {
            get
            {
                if (SelectedCredit is null)
                {
                    return string.Empty;
                }

                if (SelectedCreditSharesTotal <= 0m)
                {
                    return "Bölgü qeydə alınmayıb";
                }

                return $"XEYİR (bölünən məbləğ) = {SelectedCreditSharesTotal:N2} ₼";
            }
        }

        /// <summary>Ümumi bölünmüş məbləğ — bütün aylar üzrə (₼).</summary>
        public decimal SelectedMonthlySharesTotal => SelectedMonthlyShares.Sum(r => r.PaylarCemi);

        /// <summary>Ümumi bölünmüş məbləğ mətni.</summary>
        public string SelectedMonthlySharesTotalMetni => $"{SelectedMonthlySharesTotal:N2} ₼";

        /// <summary>Neçə ay bölgü olub.</summary>
        public string SelectedMonthlySharesXulase => SelectedMonthlyShares.Count == 0
            ? "Hələ heç bir ayın ödənişi bölünməyib."
            : $"{SelectedMonthlyShares.Count} ay üzrə bölgü · cəmi {SelectedMonthlySharesTotal:N2} ₼";

        // ====================================================================
        //  ⏳ İLKİN ÖDƏNİŞƏ MÖHLƏT  (kredit forması + seçilmiş kredit ✓✓✓)
        // --------------------------------------------------------------------
        //  İstifadəçinin tələbi:
        //    «3 min nağd ilkin ödəniş verib, deyirsə ki 10 günə 2 min nağd
        //     verəcəm → ilkin ödənişin YANINDA möhlət checkbox-u olsun ✓
        //     nə qədər gələcək və hansı tarixdə yazılsın ✓ ➕ ilə BİRDƏN ÇOX
        //     möhlət əlavə edilə bilsin ✓✓✓»
        // --------------------------------------------------------------------
        //  Hesablama (avtomatik ✓):
        //     İlkin ödəniş = 5 000 ₼ ✓ (istifadəçi yazır ✓)
        //     Möhlətlər    = 2 000 ₼  →  10 gün sonra ✓
        //     DƏRHAL       = 3 000 ₼  ← kassaya dərhal daxil olan pul ✓
        // ====================================================================

        /// <summary>Formada «İlkin ödənişə möhlət» işarələnibmi?</summary>
        [ObservableProperty] private bool ilkinMohletVar;

        /// <summary>Formada yazılan möhlətlər (yeni kredit üçün ✓ birdən çox ✓).</summary>
        public ObservableCollection<OdenisMohlet> IlkinMohletleri { get; } = new();

        /// <summary>Seçilmiş kreditin möhlətləri (detallar paneli ✓).</summary>
        public ObservableCollection<OdenisMohlet> SelectedIlkinMohletleri { get; } = new();

        /// <summary>Seçilmiş kreditin möhləti varmı?</summary>
        public bool HasSelectedIlkinMohlet => SelectedIlkinMohletleri.Count > 0;

        /// <summary>Forma üzrə möhlətlərin cəmi (₼).</summary>
        public decimal IlkinMohletCemi => IlkinMohletleri.Sum(m => m.Mebleg);

        /// <summary>Forma üzrə DƏRHAL ödənilən avans hissəsi (₼) = avans − möhlətlər ✓.</summary>
        public decimal IlkinDerhalOdenilen => Math.Max(0m, IlkinOdenis - IlkinMohletCemi);

        /// <summary>Forma üzrə möhlət xülasəsi (canlı ✓).</summary>
        public string IlkinMohletXulase
        {
            get
            {
                if (!IlkinMohletVar)
                {
                    return "İlkin ödəniş tam olaraq dərhal ödənilir (möhlət yoxdur).";
                }

                if (IlkinMohletleri.Count == 0)
                {
                    return "➕ düyməsi ilə möhləti əlavə edin: «nə vaxt → nə qədər».";
                }

                var setirler = string.Join(" · ", IlkinMohletleri
                    .OrderBy(m => m.Tarix)
                    .Select(m => $"{m.Mebleg:N2} ₼ → {m.TarixMetni}"));

                return $"⏳ Möhlət {IlkinMohletCemi:N2} ₼  ·  dərhal ödənilən {IlkinDerhalOdenilen:N2} ₼  ({setirler})";
            }
        }

        /// <summary>Forma üzrə möhlət xülasəsinin rəngi.</summary>
        public string IlkinMohletRengi =>
            IlkinMohletCemi > IlkinOdenis + 0.01m ? "#FB7185" : "#FBBF24";

        /// <summary>Möhlət panelinin görünmə vəziyyəti (checkbox ✓).</summary>
        public bool IlkinMohletPanelGorunur => IlkinMohletVar;

        partial void OnIlkinMohletVarChanged(bool value)
        {
            OnPropertyChanged(nameof(IlkinMohletPanelGorunur));
            OnPropertyChanged(nameof(IlkinMohletXulase));
        }

        /// <summary>Seçilmiş kreditin möhlət xülasəsi (detallar üçün ✓).</summary>
        public string SelectedIlkinMohletXulase => SelectedIlkinMohletleri.Count == 0
            ? "İlkin ödənişin hamısı dərhal ödənilib (möhlət yoxdur)."
            : string.Join(" · ", SelectedIlkinMohletleri
                .OrderBy(m => m.Tarix)
                .Select(m => $"{m.Mebleg:N2} ₼ → {m.TarixMetni} {m.Veziyyet}"));

        public ObservableCollection<CarItem> AvailableCars { get; } = new();

        /// <summary>Axtarış mətninə uyğun avtomobillər (canlı filtrasiya).</summary>
        public ObservableCollection<CarItem> FilteredCars { get; } = new();

        [ObservableProperty] private string carSearchText = string.Empty;

        public IReadOnlyList<string> Statuses { get; } = new[] { "Aktiv", "Bağlı", "Gecikmiş" };

        /// <summary>⏳ Möhlət sətirlərində ödəniş üsulu seçimi (Nağd · Kart / Köçürmə) ✓</summary>
        public IReadOnlyList<string> PaymentMethods { get; } = Catalog.PaymentMethods;

        /// <summary>Kreditlər dəyişdikdə baş verir (arxiv və maliyyə panelini yeniləmək üçün).</summary>
        public event EventHandler? CreditsChanged;

        [ObservableProperty] private Credit? selectedCredit;
        [ObservableProperty] private bool isBusy;

        // ---- Forma sahələri ----
        [ObservableProperty]
        [MaxLength(60, ErrorMessage = "Müqavilə nömrəsi 60 simvoldan çox ola bilməz.")]
        private string muqavileNomresi = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Müştəri adı tələb olunur.")]
        private string musteri = string.Empty;

        [ObservableProperty] private CarItem? selectedCar;

        [ObservableProperty]
        [Range(typeof(decimal), "1", "999999999", ErrorMessage = "Kredit məbləği 0-dan böyük olmalıdır.")]
        private decimal mebleg;

        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "İlkin ödəniş mənfi ola bilməz.")]
        private decimal ilkinOdenis;

        [ObservableProperty]
        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Faiz dərəcəsi mənfi ola bilməz.")]
        private decimal faizDerecesi = 12m;

        [ObservableProperty]
        [Range(1, 120, ErrorMessage = "Müddət 1-120 ay aralığında olmalıdır.")]
        private int muddetAy = 12;

        [ObservableProperty] private DateTime? baslamaTarixi = DateTime.Today;
        [ObservableProperty] private string status = "Aktiv";
        [ObservableProperty] private string qeyd = string.Empty;

        public CreditsViewModel(
            ICreditService creditService,
            ICarService carService,
            IMohletService mohletService,
            IDialogService dialogs,
            ILogger<CreditsViewModel> logger)
        {
            _creditService = creditService;
            _carService = carService;
            _mohletService = mohletService;
            _dialogs = dialogs;
            _logger = logger;

            // ⏳ Möhlət sətirləri dəyişdikcə xülasə CANLI yenilənir ✓✓✓
            IlkinMohletleri.CollectionChanged += (_, ə) =>
            {
                if (ə.NewItems is not null)
                {
                    foreach (OdenisMohlet yeni in ə.NewItems)
                    {
                        yeni.PropertyChanged += (_, __) => MohletXulaseYenile();
                    }
                }

                MohletXulaseYenile();
            };
        }

        partial void OnSelectedCreditChanged(Credit? value)
        {
            BuildSchedule(value);
            UpdateSelectedSummary();
            BuildSelectedShares(value);
            BarterYenile(value);          // 🤝 BARTER paneli ✓
            TransferYenile(value);        // 📤 TRANSFER paneli + «Transfer olunan» ✓
            IlkinMohletYukle(value);      // ⏳ İLKİN ÖDƏNİŞƏ MÖHLƏT paneli ✓✓✓
            OnPropertyChanged(nameof(ScheduleInfo));
        }

        /// <summary>
        /// ⏳ Seçilmiş kreditin <b>ilkin ödəniş möhlətlərini</b> panelə yükləyir ✓✓✓
        /// </summary>
        private void IlkinMohletYukle(Credit? credit)
        {
            SelectedIlkinMohletleri.Clear();

            if (credit is not null)
            {
                foreach (var mohlet in credit.IlkinMohletleri
                    .OrderBy(m => m.Tarix)
                    .ThenBy(m => m.Id))
                {
                    SelectedIlkinMohletleri.Add(mohlet);
                }
            }

            OnPropertyChanged(nameof(HasSelectedIlkinMohlet));
            OnPropertyChanged(nameof(SelectedIlkinMohletXulase));
        }

        /// <summary>
        /// ➕ <b>Formaya YENİ möhlət sətri əlavə edir</b> ✓✓✓
        /// (birdən çox möhlət ola bilər: «2 000 ₼ → 10 günə» + «1 000 ₼ → 25 günə» ✓)
        /// </summary>
        [RelayCommand]
        private void IlkinMohletElaveEt()
        {
            IlkinMohletVar = true;

            var qaliq = Math.Max(0m, IlkinOdenis - IlkinMohletCemi);

            var mohlet = new OdenisMohlet
            {
                Menbe = OdenisMohlet.MenbeIlkinOdenis,
                Tarix = (BaslamaTarixi ?? DateTime.Today).AddDays(10),
                Mebleg = qaliq,
                OdenisUsulu = Catalog.PaymentMethods[0],
                Sira = IlkinMohletleri.Count
            };

            IlkinMohletleri.Add(mohlet);
            MohletXulaseYenile();

            _logger.LogInformation("⏳ İlkin ödənişə möhlət sətri əlavə edildi: {Xulase}", mohlet.Xulase);
        }

        /// <summary>✖ Formadan möhlət sətrini silir ✓.</summary>
        [RelayCommand]
        private void IlkinMohletSil(OdenisMohlet? mohlet)
        {
            if (mohlet is null)
            {
                return;
            }

            IlkinMohletleri.Remove(mohlet);
            MohletXulaseYenile();
        }

        /// <summary>Formadakı möhlət xülasəsini yeniləyir (canlı hesablama ✓).</summary>
        private void MohletXulaseYenile()
        {
            OnPropertyChanged(nameof(IlkinMohletCemi));
            OnPropertyChanged(nameof(IlkinDerhalOdenilen));
            OnPropertyChanged(nameof(IlkinMohletXulase));
            OnPropertyChanged(nameof(IlkinMohletRengi));
        }

        /// <summary>
        /// Seçilmiş kredit üzrə <b>TƏRƏFDAŞ BÖLGÜLƏRİNİ</b> qurur:
        /// <list type="number">
        ///   <item><b>XEYİR bölgüsü</b> — maşının mənfəəti kredit veriləndə
        ///         tərəfdaşlar arasında necə bölündü.</item>
        ///   <item><b>Aylıq bölgü</b> — hər ayın kredit ödənişində bölünən pullar
        ///         (məs. 1-ci ay: Zaur 95,40 ₼ · Asif 667,80 ₼).</item>
        /// </list>
        /// </summary>
        private void BuildSelectedShares(Credit? credit)
        {
            SelectedCreditShares.Clear();
            SelectedMonthlyShares.Clear();

            if (credit is not null)
            {
                // ---- 1) XEYİR (maşın mənfəəti) bölgüsü ----------------------
                if (_creditSharesMap.TryGetValue(credit.Id, out var xeyirPaylari))
                {
                    // ✅ Yalnız real paylar (`Mebleg > 0`) — köhnə bazadaki
                    //    `0,00 ₼` sətirləri də təzələnir ✓
                    foreach (var share in xeyirPaylari.Where(s => s.Mebleg > 0m))
                    {
                        SelectedCreditShares.Add(share);
                    }
                }

                // ---- 2) AYLIK bölgülər (kredit əlavə gəlir qeydlərindən) -----
                var ayliq = _creditTransactions
                    .Where(t => t.CreditId == credit.Id && t.TerefdasPaylari.Count > 0)
                    .OrderBy(t => t.InstallmentNo ?? 0)
                    .ThenBy(t => t.Tarix)
                    .ThenBy(t => t.Id);

                foreach (var tx in ayliq)
                {
                    // ✅ YALNIZ REAL PAYLAR (`Mebleg > 0`) ✓✓✓
                    //  Əvvəl işarəsiz tərəfdaşlar `0,00 ₼` ilə sıralanırdı ✗ —
                    //  istifadəçinin şikayəti: «qrafikdə onların qabağında 0.00
                    //  yazılır, adları ümumiyyətlə yazılmamalıdır» ✗✓
                    var paylar = tx.TerefdasPaylari
                        .Where(p => p.Mebleg > 0m)          // ← 0,00 paylar GÖSTƏRİLMİR ✓
                        .OrderBy(p => p.Sira)
                        .ThenBy(p => p.Id)
                        .ToList();

                    // Heç bir real pay yoxdursa — həmin ay üçün SƏTİR YARADILMIR ✓
                    if (paylar.Count == 0)
                    {
                        continue;
                    }

                    SelectedMonthlyShares.Add(new CreditMonthlyShareRow
                    {
                        Ay = tx.InstallmentNo ?? 0,
                        // ⚠ HƏMİN AYIN plan tarixi = kreditin başlama tarixi + taksit №
                        //   (bazadakı «bu gün» dəyəri YOX ✗ → köhnə qeydlər də düzəlir ✓)
                        Tarix = tx.GosterilenTarix,
                        OdenisMeblegi = tx.Mebleg,
                        Baza = tx.BolguBazasi ?? paylar.Sum(p => p.Mebleg),
                        Paylar = paylar
                    });
                }
            }

            OnPropertyChanged(nameof(HasCreditShares));
            OnPropertyChanged(nameof(HasMonthlyShares));
            OnPropertyChanged(nameof(SelectedCreditSharesTotal));
            OnPropertyChanged(nameof(SelectedCreditSharesTotalMetni));
            OnPropertyChanged(nameof(SelectedCreditSharesXulase));
            OnPropertyChanged(nameof(SelectedCreditXeyirMetni));
            OnPropertyChanged(nameof(SelectedMonthlySharesTotal));
            OnPropertyChanged(nameof(SelectedMonthlySharesTotalMetni));
            OnPropertyChanged(nameof(SelectedMonthlySharesXulase));
        }

        /// <summary>
        /// Avtomobil seçildikdə satış qiyməti avtomatik yazılır (istifadəçi sonra dəyişə bilər).
        /// </summary>
        partial void OnSelectedCarChanged(CarItem? value)
        {
            if (value is null)
            {
                return;
            }

            CarSearchText = value.DisplayName;

            // ================================================================
            //  ✅ PROFESSIONAL HƏLL — MAŞIN SEÇİMİ SATIŞ QİYMƏTİNİ POZMUR ✗✓✓
            // ----------------------------------------------------------------
            //  QAYDA:
            //   ① «Satış qiyməti» ARTIQ YAZILMIŞSA (əl ilə və ya aylıq/əmsal
            //      ilə hesablanmışsa) → maşın seçimi ona TOXUNMUR ✓✓✓
            //      (əvvəlki axın: hər şeyi yazıb SONRA maşını seçmək ✓ DÜZGÜN ✓)
            //   ② «Satış qiyməti» BOŞDIRSA → maşının qiyməti (Satış/Maya)
            //      TƏKLİF kimi yazılır ✓ (sonra özünüz dəyişə bilərsiniz ✓)
            //
            //  ⚠ ƏVVƏLKİ SƏHVLƏR:
            //     ✗ maşının qiyməti hesablanmış qiyməti ÜZƏRİNƏ YAZIRDI ✗
            //     ✗ maşın seçiləndə hesablama YENİDƏN işə düşürdü ✗ →
            //       satış qiyməti HEÇ DÜZGÜN gəlmirdi ✗✓✓
            // ================================================================
            if (Mebleg > 0m)
            {
                return;   // ✓ qiymət qorunur — heç bir dəyişiklik yoxdur ✓
            }

            var suggested = value.SatisQiymeti > 0m ? value.SatisQiymeti : value.MayaDeyeri;

            if (suggested > 0m)
            {
                Mebleg = suggested;
                _lastAutoMebleg = suggested;
            }
        }

        partial void OnCarSearchTextChanged(string value) => ApplyCarFilter();

        /// <summary>
        /// Yazıldıqca avtomobil siyahısını filtrləyir (marka/model, dövlət nömrəsi, VIN).
        /// </summary>
        private void ApplyCarFilter()
        {
            if (_applyingFilter)
            {
                return;
            }

            _applyingFilter = true;
            try
            {
                var search = CarSearchText?.Trim() ?? string.Empty;

                // Yazılan mətn seçilmiş avtomobilin tam adıdırsa, bütün siyahı göstərilir.
                if (SelectedCar is not null &&
                    string.Equals(search, SelectedCar.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    search = string.Empty;
                }

                var matches = (string.IsNullOrEmpty(search)
                    ? AvailableCars
                    : AvailableCars.Where(c =>
                        c.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.Marka.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.QeydiyyatNisani.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.Vin.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();

                // Nəticə əvvəlki ilə eynidirsə siyahını yenidən qurmuruq
                // (ComboBox ilə sonsuz geri-əlaqə dövrünün qarşısını alır).
                if (matches.Count == FilteredCars.Count && matches.SequenceEqual(FilteredCars))
                {
                    return;
                }

                FilteredCars.Clear();
                foreach (var car in matches)
                {
                    FilteredCars.Add(car);
                }
            }
            finally
            {
                _applyingFilter = false;
            }
        }

        partial void OnContractSearchTextChanged(string value) => ApplyContractFilter();

        /// <summary>
        /// Kredit müqavilələri cədvəlini yazıldıqca filtrləyir
        /// (müqavilə №, müştəri, status, avtomobil markası / nömrəsi / VIN).
        /// </summary>
        private void ApplyContractFilter()
        {
            if (_applyingContractFilter)
            {
                return;
            }

            _applyingContractFilter = true;
            try
            {
                var search = ContractSearchText?.Trim() ?? string.Empty;

                var matches = (string.IsNullOrEmpty(search)
                    ? Credits
                    : Credits.Where(c => CreditMatches(c, search)))
                    // 🚫 «Bağlı» kreditlər cədvəldə HEÇ VAXT göstərilmir ✓✓✓
                    //  (bitmiş ✓ / 📕 VAXTINDAN TEZ BAĞLANMIŞ ✓ / 📤 transfer ✓)
                    // ----------------------------------------------------------------
                    //  ⚠ ƏVVƏL `|| c.Id == SelectedCredit?.Id` İSTİSNASI var idi ✗
                    //    → «Vaxtından tez bağlama»dan sonra kredit SEÇİLİ olduğu
                    //    üçün siyahıda QALIRDI ✗ (istifadəçi şikayəti ✓✓✓)
                    //  ✅ İNDİ İSTİSNASIZ süzülür ✓ → bağlanan kredit siyahıdan
                    //    DƏRHAL çıxır ✓ və «🗄️ Satılan & Krediti Bitmiş» tabının
                    //    «✅ Krediti Bitmiş» bölməsində görünür ✓✓✓
                    .Where(c => !string.Equals(c.Status, "Bağlı", StringComparison.Ordinal))
                    .ToList();

                // ✅ Seçilmiş kredit «Bağlı» oldusa → seçim SIFIRLANIR ✓✓✓
                //  (detallar / ödəniş cədvəli paneli köhnə BAĞLI krediti
                //   göstərməyə davam etməsin ✗✓✓)
                if (SelectedCredit is not null
                    && string.Equals(SelectedCredit.Status, "Bağlı", StringComparison.Ordinal))
                {
                    SelectedCredit = null;
                }

                if (matches.Count == FilteredContracts.Count && matches.SequenceEqual(FilteredContracts))
                {
                    OnPropertyChanged(nameof(ContractCountText));
                    return;
                }

                FilteredContracts.Clear();
                foreach (var credit in matches)
                {
                    FilteredContracts.Add(credit);
                }

                // Axtarış nəticəsində seçilmiş müqavilə görünmürsə, ilk uyğun müqavilə seçilir
                // (məlumat kartı və ödəniş qrafiki boş qalmasın).
                if (!string.IsNullOrEmpty(search) &&
                    (SelectedCredit is null || !FilteredContracts.Contains(SelectedCredit)))
                {
                    SelectedCredit = FilteredContracts.FirstOrDefault();
                }

                OnPropertyChanged(nameof(ContractCountText));
            }
            finally
            {
                _applyingContractFilter = false;
            }
        }

        /// <summary>Cədvəl başlığında göstərilən say: "5 müqavilə" və ya "2 / 5 müqavilə".</summary>
        public string ContractCountText => string.IsNullOrWhiteSpace(ContractSearchText)
            ? $"{Credits.Count} müqavilə"
            : $"{FilteredContracts.Count} / {Credits.Count} müqavilə";

        /// <summary>Axtarış mətnini təmizləyir və bütün müqavilələri göstərir.</summary>
        [RelayCommand]
        private void ClearContractFilter()
        {
            ContractSearchText = string.Empty;
            ApplyContractFilter();
        }

        /// <summary>Kredit axtarış mətnə uyğundursa true (müqavilə №, müştəri, status, avtomobil).</summary>
        private static bool CreditMatches(Credit credit, string search)
            => (credit.MuqavileNomresi ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
               || (credit.Mustəri ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
               || (credit.Status ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
               || (credit.Car is not null
                   && (credit.Car.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase)
                       || (credit.Car.Marka ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
                       || (credit.Car.QeydiyyatNisani ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)
                       || (credit.Car.Vin ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase)));

        partial void OnScheduleSearchTextChanged(string value) => ApplyScheduleFilter();

        /// <summary>
        /// "Kredit Ödəniş Qrafiki" tabındaki kredit seçimini yazıldıqca filtrləyir
        /// (müqavilə №, müştəri, status, avtomobil markası / nömrəsi).
        /// </summary>
        private void ApplyScheduleFilter()
        {
            if (_applyingScheduleFilter)
            {
                return;
            }

            _applyingScheduleFilter = true;
            try
            {
                var search = ScheduleSearchText?.Trim() ?? string.Empty;

                // Yazılan mətn seçilmiş krediti tam təsvir edirsə, bütün siyahı göstərilir.
                if (SelectedCredit is not null &&
                    string.Equals(search, SelectedCredit.DisplayText, StringComparison.OrdinalIgnoreCase))
                {
                    search = string.Empty;
                }

                var matches = (string.IsNullOrEmpty(search)
                    ? Credits
                    : Credits.Where(c => CreditMatches(c, search))).ToList();

                if (matches.Count == FilteredScheduleCredits.Count && matches.SequenceEqual(FilteredScheduleCredits))
                {
                    return;
                }

                FilteredScheduleCredits.Clear();
                foreach (var credit in matches)
                {
                    FilteredScheduleCredits.Add(credit);
                }
            }
            finally
            {
                _applyingScheduleFilter = false;
            }
        }

        /// <summary>Ödəniş qrafikinin başlığında göstərilən xülasə.</summary>
        public string ScheduleInfo => SelectedCredit is null
            ? string.Empty
            : $"Satış qiyməti: {SelectedCredit.Mebleg:N2} AZN   •   İlkin ödəniş: {SelectedCredit.IlkinOdenis:N2} AZN   " +
              $"•   Kreditləşdirilən: {SelectedCredit.Kreditlesdirilen:N2} AZN   " +
              $"•   Faiz: {SelectedCredit.FaizMeblegi:N2} AZN   " +
              $"•   Kreditin qiyməti: {SelectedCredit.KreditQiymeti:N2} AZN   " +
              $"•   Aylıq: {SelectedCredit.AylıqOdenis:N2} AZN × {SelectedCredit.MuddetAy} ay";

        // ---- Seçilmiş kredit üzrə yekun göstəricilər (məlumat kartı üçün) ----

        /// <summary>Faktiki ödənilmiş məbləğ (AZN).</summary>
        public decimal SelectedOdenilen { get; private set; }

        /// <summary>
        /// <b>QALIQ (kredit + faiz)</b> — müştərinin hələ ödəməli olduğu
        /// <b>TAM</b> məbləğ: <c>Kreditin qiyməti − Ödənilmiş</c>.
        /// <para>
        /// ⚠ <b>Kreditləşdirilən DEYİL!</b> Kreditin qiyməti =
        /// <c>Kreditləşdirilən + Faiz</c> (məs. 17 014 ₼ + 6 805,60 ₼ = 23 819,60 ₼).
        /// Beləliklə qalıq faizi də əhatə edir ✓
        /// </para>
        /// </summary>
        public decimal SelectedQaliq { get; private set; }

        /// <summary>Ödənişdən əvvəlki ümumi borc (kreditləşdirilən + faiz).</summary>
        public decimal SelectedKreditQiymeti => SelectedCredit?.KreditQiymeti ?? 0m;

        /// <summary>Kredit üzrə ümumi FAİZ məbləği (₼).</summary>
        public decimal SelectedFaizMeblegi => SelectedCredit?.FaizMeblegi ?? 0m;

        /// <summary>
        /// «Kreditləşdirilən» kartının altındakı izah:
        /// «Faiz 6 805,60 ₼ · Qiyməti 23 819,60 ₼».
        /// </summary>
        public string SelectedFaizVeQiymet => SelectedCredit?.FaizVeQiymetMetni ?? string.Empty;

        /// <summary>Qalıq kartının altındakı izah: «Qiymət 23 819,60 ₼ − ödənilmiş 5 000,00 ₼».</summary>
        public string SelectedQaliqIzahi
        {
            get
            {
                if (SelectedCredit is null)
                {
                    return string.Empty;
                }

                // ================================================================
                //  📊 ŞƏFFAF VƏ DƏQİQ DÜSTUR ✓✓✓ (v6.2.30)
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL: «Qiymət X − ödənilmiş Y» ✗ — cərimələr YOX ✗ →
                //    rəqəmlər uyğun GƏLMİRDİ ✗ (istifadəçi şikayəti ✓✓✓)
                //
                //  ✅ İNDİ:  Qiymət + (ÖDƏNİLMƏMİŞ cərimə) − taksit ödənişləri
                //    • ödənilməmiş gecikmə → QALIĞA ƏLAVƏ olunur ✓
                //    • ödənilmiş gecikmə   → əlavə OLUNMUR ✗ (bir-birini ləğv edir ✓)
                //    → düstur DƏQİQ balanslaşır ✓✓✓
                // ================================================================
                var esas = $"Qiymət {SelectedKreditQiymeti:N2} ₼";

                if (SelectedGecikmeQaliq > 0m)
                {
                    esas += $" + cərimə {SelectedGecikmeQaliq:N2} ₼";
                }

                return $"{esas} − taksit ödənişləri {SelectedTaksitOdenisCemi:N2} ₼";
            }
        }

        /// <summary>
        /// Taksit (kredit) ödənişlərinin cəmi — <b>cərimələr DAXİL DEYİL</b> ✗ (v6.2.30).
        /// </summary>
        public decimal SelectedTaksitOdenisCemi => Math.Max(0m, SelectedOdenilen - SelectedGecikmeOdenilmis);

        /// <summary>Edilmiş ödənişlərin sayı.</summary>
        public int SelectedOdenisSayi { get; private set; }

        // ====================================================================
        //  🔢 KÖK (ƏSAS BORC) GÖSTƏRİCİLƏRİ ✓✓✓ (v6.2.17)
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi: «Kreditlər tabında müqaviləni seçəndə orda
        //    KÖK-də görsənməlidir və ÖDƏNİLMİŞ KÖK-də görsənməlidir» ✓✓✓
        // ====================================================================

        /// <summary>🔢 <b>KÖK</b> — kreditin əsas borcu (₼) = Kreditləşdirilən ✓</summary>
        public decimal SelectedKok { get; private set; }

        /// <summary>🔢 <b>AYLIQ KÖK</b> — hər ay əsas borcdan düşən pay = KÖK ÷ Müddət ✓</summary>
        public decimal SelectedAylıqKok { get; private set; }

        /// <summary>
        /// ✅ <b>ÖDƏNİLMİŞ KÖK</b> — artıq qaytarılmış əsas borc (₼) ✓✓✓
        /// <para>Bütün ödənişlərin KÖK paylarının cəmi ✓ (qalan borcla məhdud ✓)</para>
        /// </summary>
        public decimal SelectedOdenilmisKok { get; private set; }

        /// <summary>⏳ <b>QALIQ KÖK</b> = KÖK − ödənilmiş kök (₼) ✓</summary>
        public decimal SelectedQaliqKok { get; private set; }

        /// <summary>📊 KÖK-ün ödənilmə faizi (0–1) — irəliləyiş zolağı üçün ✓</summary>
        public double SelectedKokFaizi { get; private set; }

        /// <summary>«Kök 17 014,00 ₼ · aylıq kök 1 417,83 ₼» ✓</summary>
        public string SelectedKokIzahi { get; private set; } = string.Empty;

        /// <summary>«Ödənilmiş 2 835,66 ₼ · qalıq 14 178,34 ₼» ✓</summary>
        public string SelectedOdenilmisKokIzahi { get; private set; } = string.Empty;

        /// <summary>
        /// ✅ <b>ÖDƏNİLMİŞ KÖK-Ü HESABLAYIR</b> ✓✓✓ (v6.2.17)
        /// <para>
        /// Ödənişlər <b>TARİX SIRASI</b> ilə götürülür ✓ → hər ödənişin
        /// KÖK payı cəmlənir ✓ → nəticə əsas borcu <b>AŞA BİLMƏZ</b> ✗✓✓
        /// (borc bitəndən sonrakı ödənişlər tam MALİYYƏT/mənfəət sayılır ✓✓✓)
        /// </para>
        /// </summary>
        private static decimal HesablaOdenilmisKok(Credit credit, IEnumerable<CreditTransaction> payments)
        {
            var cem = 0m;

            foreach (var t in payments.OrderBy(t => t.Tarix).ThenBy(t => t.Id))
            {
                cem += credit.OdenisKokPayi(t.Mebleg, cem);
            }

            return Math.Round(cem, 2);
        }

        /// <summary>Son ödənişin tarixi (mətn).</summary>
        public string SelectedSonOdenis { get; private set; } = "—";

        /// <summary>Möhlətə verilmiş ümumi məbləğ (AZN).</summary>
        public decimal SelectedMohlet { get; private set; }

        /// <summary>Möhlət verilmiş taksitlərin sayı.</summary>
        public int SelectedMohletSayi { get; private set; }

        /// <summary>Gecikmə qeydə alınmış taksitlərin sayı.</summary>
        public int SelectedGecikmeSayi { get; private set; }

        /// <summary>Gecikmə cərimələrinin cəmi (AZN).</summary>
        public decimal SelectedGecikmeCerimesi { get; private set; }

        /// <summary>⚠️ ÖDƏNİLMİŞ gecikmə cərimələrinin cəmi (AZN) ✓ (v6.2.29).</summary>
        public decimal SelectedGecikmeOdenilmis { get; private set; }

        /// <summary>⚠️ Hələ ÖDƏNİLMƏMİŞ (gözləyən) gecikmə cərimələrinin cəmi (AZN) ✓ (v6.2.29).</summary>
        public decimal SelectedGecikmeQaliq { get; private set; }

        /// <summary>Möhlət xülasəsi: "2 möhlət · 800,00 ₼" / "Möhlət yoxdur".</summary>
        public string SelectedMohletYekunu => SelectedMohletSayi == 0
            ? "Möhlət yoxdur"
            : $"{SelectedMohletSayi} möhlət · {SelectedMohlet:N2} ₼";

        /// <summary>
        /// ⚠️ Gecikmə xülasəsi ✓✓✓ (v6.2.29)
        /// <para>★ İstifadəçi tələbi: «gecikmə 100 AZN cərimə var — 50 manatı
        /// ödənilib» kimi <b>ödənilmiş / qalıq bölgüsü</b> görünməlidir ✓✓✓</para>
        /// <list type="bullet">
        ///   <item><c>Gecikmə yoxdur</c></item>
        ///   <item><c>⚠️ 1 gecikmə · 100,00 ₼ cərimə</c> — heç biri ödənilməyib ✓</item>
        ///   <item><c>⚠️ 1 gecikmə · 100,00 ₼ cərimə (ödənilib ✓)</c> — hamısı ödənilib ✓</item>
        ///   <item><c>⚠️ 2 gecikmə · 100,00 ₼ cərimə (ödənilib 50,00 ₼ · qalıq 50,00 ₼)</c></item>
        /// </list>
        /// </summary>
        public string SelectedGecikmeYekunu
        {
            get
            {
                if (SelectedGecikmeSayi == 0)
                {
                    return "Gecikmə yoxdur";
                }

                var esas = $"⚠️ {SelectedGecikmeSayi} gecikmə · {SelectedGecikmeCerimesi:N2} ₼ cərimə";

                if (SelectedGecikmeOdenilmis <= 0m)
                {
                    return esas;
                }

                return SelectedGecikmeQaliq <= 0m
                    ? $"{esas} (ödənilib ✓)"
                    : $"{esas} (ödənilib {SelectedGecikmeOdenilmis:N2} ₼ · qalıq {SelectedGecikmeQaliq:N2} ₼)";
            }
        }

        /// <summary>⚠️ Seçilmiş kreditdə gecikmə cəriməsi VAR? (panel görünsün ✓) — v6.2.30.</summary>
        public bool GecikmeVar => SelectedGecikmeSayi > 0 || SelectedGecikmeCerimesi > 0m;

        /// <summary>
        /// ⚠️ <b>GECİKMƏ CƏRİMƏSİ XÜLASƏSİ</b> ✓✓✓ (v6.2.30)
        /// <para>
        /// ★ İstifadəçi tələbi: «kredit məlumatında yazılmalıdır — <b>ne qədər
        /// gecikmə olub, ne qədər pul, ne qədər ödənilib</b>» ✓✓✓
        /// </para>
        /// <example>
        /// <code>
        /// ⚠️ GECİKMƏ CƏRİMƏSİ — 4 qeyd · cəmi 80,00 ₼  ·  ödənilib 20,00 ₼  ·  qalıq 60,00 ₼
        /// ↳ ödənilməmiş hissə (60,00 ₼) QALIĞA əlavə olunur ✓
        /// </code>
        /// </example>
        /// </summary>
        public string SelectedGecikmeDetal
        {
            get
            {
                if (!GecikmeVar)
                {
                    return string.Empty;
                }

                var metn =
                    $"⚠️ GECİKMƏ CƏRİMƏSİ — {SelectedGecikmeSayi} qeyd" +
                    $"  ·  cəmi {SelectedGecikmeCerimesi:N2} ₼" +
                    $"  ·  ödənilib {SelectedGecikmeOdenilmis:N2} ₼" +
                    $"  ·  qalıq {SelectedGecikmeQaliq:N2} ₼";

                return SelectedGecikmeQaliq > 0m
                    ? metn + $"\n↳ ödənilməmiş hissə ({SelectedGecikmeQaliq:N2} ₼) QALIĞA əlavə olunur ✓"
                    : metn + "\n↳ hamısı ödənilib ✓ — qalığa əlavə olunmur ✗";
            }
        }

        /// <summary>Ödənişin tamamlanma faizi (0-1) — irəliləyiş zolağı üçün.</summary>
        public double SelectedOdenisFaizi =>
            SelectedCredit is null || SelectedCredit.KreditQiymeti <= 0m
                ? 0d
                : (double)Math.Clamp(SelectedOdenilen / SelectedCredit.KreditQiymeti, 0m, 1m);

        /// <summary>İllik faiz dərəcəsi və müddət: "12,00%   •   12 ay".</summary>
        public string SelectedFaizVeMuddet => SelectedCredit is null
            ? "—"
            : $"{SelectedCredit.FaizDerecesi:N2}%   •   {SelectedCredit.MuddetAy} ay";

        /// <summary>Ödənişlərin yekun mətni.</summary>
        public string SelectedOdenisYekunu =>
            $"Ödənilib: {SelectedOdenilen:N2} AZN     Qalıq: {SelectedQaliq:N2} AZN     Ödəniş sayı: {SelectedOdenisSayi}     Son ödəniş: {SelectedSonOdenis}";

        /// <summary>Seçilmiş kredit üzrə ödəniş göstəricilərini yeniləyir.</summary>
        private void UpdateSelectedSummary()
        {
            if (SelectedCredit is null)
            {
                SelectedOdenilen = 0m;
                SelectedQaliq = 0m;
                SelectedOdenisSayi = 0;
                SelectedSonOdenis = "—";

                // 🔢 KÖK göstəriciləri sıfırlanır ✓ (v6.2.17)
                SelectedKok = 0m;
                SelectedAylıqKok = 0m;
                SelectedOdenilmisKok = 0m;
                SelectedQaliqKok = 0m;
                SelectedKokFaizi = 0d;
                SelectedKokIzahi = string.Empty;
                SelectedOdenilmisKokIzahi = string.Empty;
            }
            else
            {
                var payments = CollectPayments(SelectedCredit).Values.SelectMany(v => v).ToList();
                SelectedOdenilen = payments.Sum(t => t.Mebleg);

                // ================================================================
                //  🔢 KÖK / ÖDƏNİLMİŞ KÖK ✓✓✓ (v6.2.17)
                // ----------------------------------------------------------------
                //  ★ İstifadəçi tələbi: «Kreditlər tabında müqaviləni seçəndə
                //    KÖK-də görsənməlidir və ÖDƏNİLMİŞ KÖK-də görsənməlidir» ✓✓✓
                //  📌 Ödənilmiş kök = bütün ödənişlərin KÖK paylarının cəmi ✓
                //     (hər ödənişdən kök payı çıxılır ✓ → ucu-bucağı qalan
                //      əsas borcla MƏHDUDLAŞDIRILIR ✓ → borc bitəndən sonra
                //      ödənişin HAMISI mənfəət sayılır ✓✓✓)
                // ================================================================
                SelectedOdenilmisKok = HesablaOdenilmisKok(SelectedCredit, payments);
                SelectedKok = SelectedCredit.Kok;
                SelectedAylıqKok = SelectedCredit.AylıqKok;
                SelectedQaliqKok = Math.Max(0m, SelectedKok - SelectedOdenilmisKok);
                SelectedKokFaizi = SelectedKok <= 0m
                    ? 0d
                    : (double)Math.Clamp(SelectedOdenilmisKok / SelectedKok, 0m, 1m);

                SelectedKokIzahi = SelectedCredit.KokMetni;
                SelectedOdenilmisKokIzahi = SelectedCredit.OdenilmisKokMetni;

                // 🔗 Kartlarda da görünsün ✓ (modelin öz xassələri ✓)
                SelectedCredit.OdenilmisKok = SelectedOdenilmisKok;

                // ================================================================
                //  ✅ ÖDƏNİLMİŞ GECİKMƏLƏR «ÖDƏNİLİB»Ə ƏLAVƏ OLUNUR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ SƏHV: «Ödənilib» yalnız taksit ödənişlərindən
                //  hesablanırdı ✗ → 100 ₼ gecikmə ödənilsə də «0,00 ₼»
                //  görünürdü ✗✓✓
                //  İNDİ: ödənilmiş gecikmə cərimələri də DAXİL EDİLİR ✓
                // ================================================================
                SelectedOdenilen += _creditTransactions
                    .Where(t => t.CreditId == SelectedCredit.Id
                                && t.Nov == "Gecikmə"
                                && t.Odenilib)
                    .Sum(t => t.Mebleg);

                // ⚠ QALIQ = KREDİTİN QİYMƏTİ (kreditləşdirilən + FAİZ) − ödənilmiş.
                SelectedQaliq = Math.Max(
                0m,
                SelectedCredit.KreditQiymeti
                    + _creditTransactions
                        // ============================================================
                        //  ✅ BÜTÜN GECİKMƏLƏR BORCA ƏLAVƏ OLUNUR ✓✓✓
                        // ------------------------------------------------------------
                        //  ⚠ ƏVVƏLKİ SƏHV: yalnız ÖDƏNİLMƏMİŞ gecikmələr borca
                        //  əlavə olunurdu ✗ amma ÖDƏNİLMİŞ olan `SelectedOdenilen`
                        //  içində ÇIXILIRDI ✗ → 100 ₼ İKİ DƏFƏ çıxılırdı ✗✓✓
                        //
                        //  ✅ DÜZGÜN MƏNTİQ:
                        //     borc  = qiymət + BÜTÜN gecikmələr ✓
                        //     ödəniş = taksitlər + ÖDƏNİLMİŞ gecikmələr ✓
                        //     → ödənilmiş gecikmə bir-birini LƏĞV EDİR ✓
                        //     → ödənilməmiş gecikmə isə borcda QALIR ✓✓✓
                        //
                        //  Nümunə: 15 407,99 + 100 (gecikmə) − 100 (ödənilib)
                        //          = **15 407,99 ₼** ✓✓✓
                        // ============================================================
                        .Where(t => t.CreditId == SelectedCredit.Id
                                    && t.Nov == "Gecikmə")
                        .Sum(t => t.Mebleg)
                    - SelectedOdenilen);

                SelectedOdenisSayi = payments.Count;
                SelectedSonOdenis = payments.Count == 0
                    ? "—"
                    : payments.Max(t => t.Tarix).ToString("dd.MM.yyyy");
            }

            OnPropertyChanged(nameof(SelectedOdenilen));
            OnPropertyChanged(nameof(SelectedQaliq));
            OnPropertyChanged(nameof(SelectedKreditQiymeti));
            OnPropertyChanged(nameof(SelectedFaizMeblegi));
            OnPropertyChanged(nameof(SelectedFaizVeQiymet));
            OnPropertyChanged(nameof(SelectedQaliqIzahi));
            OnPropertyChanged(nameof(SelectedOdenisSayi));
            OnPropertyChanged(nameof(SelectedSonOdenis));
            OnPropertyChanged(nameof(SelectedOdenisFaizi));
            OnPropertyChanged(nameof(SelectedOdenisYekunu));
            OnPropertyChanged(nameof(SelectedFaizVeMuddet));

            // ================================================================
            //  🔢 KÖK / ÖDƏNİLMİŞ KÖK ✓✓✓ (v6.2.17)
            //  ★ İstifadəçi tələbi: «Kreditlər tabında müqaviləni seçəndə
            //    KÖK-də görsənməlidir və ÖDƏNİLMİŞ KÖK-də görsənməlidir» ✓✓✓
            // ================================================================
            OnPropertyChanged(nameof(SelectedKok));
            OnPropertyChanged(nameof(SelectedAylıqKok));
            OnPropertyChanged(nameof(SelectedOdenilmisKok));
            OnPropertyChanged(nameof(SelectedQaliqKok));
            OnPropertyChanged(nameof(SelectedKokIzahi));
            OnPropertyChanged(nameof(SelectedOdenilmisKokIzahi));
            OnPropertyChanged(nameof(SelectedKokFaizi));

            // Möhlət göstəriciləri qrafikdən oxunur (BuildSchedule-dan sonra çağırılır).
            SelectedMohletSayi = Schedule.Count(r => r.MohletVar);
            SelectedMohlet = Schedule.Where(r => r.MohletVar).Sum(r => r.MohletMeblegi);
            OnPropertyChanged(nameof(SelectedMohletSayi));
            OnPropertyChanged(nameof(SelectedMohlet));
            OnPropertyChanged(nameof(SelectedMohletYekunu));

            // Gecikmə göstəriciləri.
            SelectedGecikmeSayi = Schedule.Count(r => r.GecikmeVar);

            // ================================================================
            //  ⚠️ CƏRİMƏNİN «ÖDƏNİLMİŞ / QALIQ» BÖLGÜSÜ ✓✓✓ (v6.2.30)
            // ----------------------------------------------------------------
            //  ★ İstifadəçi tələbi:
            //    • «cərimə 100 AZN var — 50 manatı ödənilib» GÖRÜNSÜN ✓
            //    • «ödənilməmiş gecikmə QALIĞIN ÜSTÜNƏ gəlsin ✓,
            //       ödənilmiş gecikmə gəlməsin ✗» ✓✓✓
            // ================================================================
            var cerimeHereketleri = SelectedCredit is null
                ? new List<CreditTransaction>()
                : _creditTransactions
                    .Where(t => t.CreditId == SelectedCredit.Id && t.Nov == "Gecikmə")
                    .ToList();

            // Cəmi BİRBAŞA əməliyyatlardan ✓ → `SelectedQaliq` düsturu ilə
            // BİR-BİRDİR ✓ (dəqiq balans ✓)
            SelectedGecikmeCerimesi = cerimeHereketleri.Sum(t => t.Mebleg);

            SelectedGecikmeOdenilmis = cerimeHereketleri
                .Where(t => t.Odenilib)
                .Sum(t => t.Mebleg);

            SelectedGecikmeQaliq = cerimeHereketleri
                .Where(t => !t.Odenilib)
                .Sum(t => t.Mebleg);

            OnPropertyChanged(nameof(SelectedGecikmeSayi));
            OnPropertyChanged(nameof(SelectedGecikmeCerimesi));
            OnPropertyChanged(nameof(SelectedGecikmeOdenilmis));
            OnPropertyChanged(nameof(SelectedGecikmeQaliq));
            OnPropertyChanged(nameof(SelectedGecikmeYekunu));
            OnPropertyChanged(nameof(SelectedGecikmeDetal));
            OnPropertyChanged(nameof(GecikmeVar));
            OnPropertyChanged(nameof(SelectedTaksitOdenisCemi));
            OnPropertyChanged(nameof(SelectedQaliqIzahi));
        }

        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                var credits = await _creditService.GetCreditsAsync();

                // 📄 PDF ixracı üçün BÜTÜN kreditlər saxlanılır ✓ (bağlılar da ✓)
                _butunKreditler = credits.ToList();

                // Müqavilə nömrəsi boş olan kreditlərə avtomatik ardıcıl nömrə verilir.
                var nextContract = ComputeNextContractNumber(credits);
                var anyFilled = false;
                foreach (var credit in credits.Where(c => string.IsNullOrWhiteSpace(c.MuqavileNomresi)))
                {
                    credit.MuqavileNomresi = "M-" + nextContract.ToString("D4");
                    nextContract++;
                    await _creditService.UpdateCreditAsync(credit);
                    anyFilled = true;
                }

                if (anyFilled)
                {
                    credits = await _creditService.GetCreditsAsync();
                    _logger.LogInformation("Boş müqavilə nömrələri avtomatik təyin edildi.");
                }

                var keepId = SelectedCredit?.Id;
                Credits.Clear();
                var sira = 1;

                // 🔢 «0» nömrəli kreditlər → ardıcıl nömrə alıb YADDA SAXLANILIR ✓✓✓
                //   (əvvəl yalnız EKRANDA nömrələnirdi ✗ → Firebase-ə «0» gedirdi ✗)
                var siraDüzəldiləcək = new List<Credit>();

                foreach (var credit in credits)
                {
                    // ============================================================
                    //  📤🚫 «BAĞLI» (bitmiş / TRANSFER edilmiş) kreditlər «💳 Kreditlər»
                    //  siyahısından ÇIXARILIR ✓✓✓
                    // ------------------------------------------------------------
                    //  ⚠ ƏVVƏLKİ XƏTA: bütün kreditlər əlavə olunurdu ✗ → transfer
                    //  etdikdən sonra maşın «💳 Kreditlər» bölməsindən GÖRÜNMƏYƏ
                    //  davam edirdi ✗✓✓
                    //  İNDİ: bağlı/transfer edilmiş kreditlər buradan ÇIXIR ✓ və
                    //  «🗄️ Satılan & Krediti Bitmiş» bölməsində görünür ✓
                    //  (seçilmiş kredit istisnadır — qrafik/PDF üçün qalır ✓)
                    // ============================================================
                    if (string.Equals(credit.Status, "Bağlı", StringComparison.Ordinal)
                        && credit.Id != keepId)
                    {
                        continue;
                    }

                    // 🔢 Nömrə «0»-dırsa → ardıcıl nömrə VERİLİR və YADDA SAXLANILIR ✓✓✓
                    if (credit.SiraNomresi <= 0)
                    {
                        credit.SiraNomresi = sira;
                        siraDüzəldiləcək.Add(credit);
                    }

                    sira++;
                    Credits.Add(credit);
                }

                // 💾 Düzəldilən nömrələr bazaya yazılır ✓ → ☁️ Firebase-ə də düzgün gedir ✓✓✓
                foreach (var düzəldilən in siraDüzəldiləcək)
                {
                    try { await _creditService.UpdateCreditAsync(düzəldilən); } catch { }
                }

                ApplyContractFilter();      // cədvəlin axtarış filtri tətbiq olunur
                ApplyScheduleFilter();      // ödəniş qrafiki tabının kredit seçimi yenilənir

                var cars = await _carService.GetCarsAsync();
                AvailableCars.Clear();
                foreach (var car in cars)
                {
                    AvailableCars.Add(car);
                }
                ApplyCarFilter();

                _creditTransactions = (await _creditService.GetTransactionsAsync()).ToList();

                // ================================================================
                //  🔄 ŞƏXS / BARTER SİYAHILARINI DƏRHAL YENİLƏ ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ XƏTA: yeni şəxs əlavə edəndə `LoadAsync` çağırılırdı ✗,
                //  amma `TransferYenile` YALNIZ kredit dəyişəndə işə düşürdü ✗ →
                //  «Tural» siyahıya düşmürdü ✗✓✓
                //  İNDİ: hər yükləmədən sonra siyahılar yenilənir ✓
                // ================================================================
                TransferYenile(SelectedCredit);
                BarterYenile(SelectedCredit);

                // ================================================================
                //  🩺 CAR SELF-HEAL — TRANSFER OLUNMUŞ MAŞIN PARKDAN ÇIXARILIR ✓
                // ----------------------------------------------------------------
                //  Avtomobil statusu «Satıldı» olmalıdır ✓ → Avto Park-dan çıxır ✓,
                //  ümumi sayı azalır ✓ və «🗄️ Satılan & Krediti Bitmiş»-də görünür ✓
                //  (əvvəlki transferlər də RETROAKTİV düzəlir ✓✓✓)
                // ================================================================
                //  ⚠ `Credits` siyahısı bağlı/transfer kreditlərini SÜZÜR ✗ → ona görə
                //  BÜTÜN kreditlərin `credits` siyahısından (filtrdən ƏVVƏL) CarId
                //  götürülür ✓ — əks halda transfer edilmiş kredit tapılmır və maşın
                //  parkdan ÇIXMIR ✗✓✓
                var carIdByCredit = credits
                    .Where(c => c.CarId.HasValue)
                    .ToDictionary(c => c.Id, c => c.CarId!.Value);

                // ✅ YALNIZ hələ də PARKDA görünən maşınlar düzəldilir ✓✓✓
                //  ⚠ ƏVVƏLKİ XƏTA: hər yükləmədə HƏR transfer üçün bazaya
                //  YAZILIRDI ✗ → «⏳ Məlumatlar yüklənir...» tez-tez görünürdü ✗
                //  və hər seçimdə gecikmə yaranırdı ✗✓✓
                //  İNDİ: maşın parkdan çıxdıqdan sonra bu blok HEÇ NƏ ETMİR ✓
                var aktivCarIds = cars.Select(c => c.Id).ToHashSet();

                foreach (var tx in _creditTransactions
                             .Where(t => t.Nov == "Transfer" || t.Nov == "Transfer olunmaq")
                             .ToList())
                {
                    if (tx.CreditId is not int cid
                        || !carIdByCredit.TryGetValue(cid, out var kId)
                        || !aktivCarIds.Contains(kId))     // ✓ artıq parkda deyilsə YAZMA ✓
                    {
                        continue;
                    }

                    await _carService.SetStatusAsync(kId, Catalog.TransferStatus);
                }

            // 🔑 Hər əməliyyata KREDİT MÜQAVİLƏSİNİ bağlayırıq.
            //    Bu, «həmin ayın plan tarixini» hesablamaq üçün VACİBDİR ✓
            //    (ödəniş tarixi bazada səhv olsa da düzgün tarix göstərilir ✓)
            var creditById = Credits.ToDictionary(c => c.Id);
            foreach (var tx in _creditTransactions)
            {
                if (tx.CreditId is int cid && creditById.TryGetValue(cid, out var cr))
                {
                    tx.Credit = cr;
                }
            }


            // 👥 Kredit səviyyəli XEYİR bölgüləri — bir sorğu ilə xəritə.
            _creditSharesMap = (await _creditService.GetCreditSharesMapAsync())
                .ToDictionary(kv => kv.Key, kv => kv.Value);

                // Əvvəlki seçimi saxlamaq (filtrdən asılı olmayaraq), yoxdursa ilkini seçmək.
                var selected = keepId.HasValue
                    ? Credits.FirstOrDefault(c => c.Id == keepId.Value)
                    : null;
                selected ??= FilteredContracts.FirstOrDefault() ?? Credits.FirstOrDefault();

                if (!ReferenceEquals(SelectedCredit, selected))
                {
                    SelectedCredit = selected;
                }
                else
                {
                    BuildSchedule(SelectedCredit);
                    UpdateSelectedSummary();
                    BuildSelectedShares(SelectedCredit);
                }
                _logger.LogInformation("{Count} kredit yükləndi.", Credits.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kreditlər yüklənərkən xəta baş verdi.");
                _dialogs.ShowError("Kredit siyahısı yüklənə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        [RelayCommand]
        private async Task AddCreditAsync()
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                _dialogs.ShowWarning("Zəhmət olmasa formada göstərilən səhvləri düzəldin.");
                return;
            }

            if (IlkinOdenis < 0m || IlkinOdenis >= Mebleg)
            {
                _dialogs.ShowWarning("İlkin ödəniş 0-dan kiçik və ya kredit məbləğindən böyük ola bilməz.");
                return;
            }

            // ⏳ Möhlətlərin cəmi ilkin ödənişdən ÇOX ola bilməz ✓✓✓
            if (IlkinMohletCemi > IlkinOdenis + 0.01m)
            {
                _dialogs.ShowWarning(
                    $"İlkin ödənişə yazılan möhlətlər ({IlkinMohletCemi:N2} ₼) ilkin ödənişdən " +
                    $"({IlkinOdenis:N2} ₼) çox ola bilməz. Zəhmət olmasa məbləğləri düzəldin.");
                return;
            }

            try
            {
                IsBusy = true;
                var contractNo = MuqavileNomresi.Trim();
                if (string.IsNullOrWhiteSpace(contractNo))
                {
                    // Boş buraxılıbsa ardıcıl növbəti müqavilə nömrəsi verilir.
                    contractNo = await GenerateContractNumberAsync();
                }

                var kreditlesdirilen = Mebleg - IlkinOdenis;

                var credit = new Credit
                {
                    MuqavileNomresi = contractNo,
                    Mustəri = Musteri.Trim(),
                    CarId = SelectedCar?.Id,
                    Mebleg = Mebleg,
                    IlkinOdenis = IlkinOdenis,
                    FaizDerecesi = FaizDerecesi,
                    MuddetAy = MuddetAy,
                    // Aylıq ödəniş yalnız KREDİTLƏŞDİRİLƏN məbləğ üzərindən hesablanır.
                    AylıqOdenis = CalculateMonthly(kreditlesdirilen, FaizDerecesi, MuddetAy),
                    BaslamaTarixi = BaslamaTarixi ?? DateTime.Today,
                    Status = Status,
                    Qeyd = Qeyd.Trim()
                };

                await _creditService.AddCreditAsync(credit);

                // ⏳ İLKİN ÖDƏNİŞƏ MÖHLƏT — «3 min nağd indi ✓ 2 min 10 günə ✓» ✓✓✓
                if (credit.Id > 0 && IlkinMohletVar && IlkinMohletleri.Count > 0)
                {
                    var say = await _mohletService.SaveForCreditAsync(
                        credit.Id,
                        IlkinMohletleri.ToList());

                    _logger.LogInformation(
                        "⏳ Kredit #{Id}: {Say} ilkin ödəniş möhləti saxlanıldı (dərhal {Derhal:N2} ₼ · möhlət {Mohlet:N2} ₼)",
                        credit.Id, say, IlkinDerhalOdenilen, IlkinMohletCemi);
                }

                // 👥 Tərəfdaş bölgüsü tətbiq olunubsa — kreditin mənfəət payları yazılır
                //    (XEYİR = Satış qiyməti − Maya dəyəri).
                if (KreditBolguTetbiq && credit.Id > 0)
                {
                    KreditBolguHesabla();
                    var paylar = KreditTerefdaslari.Select((r, i) => r.ToModel(i)).ToList();
                    await _creditService.SaveCreditSharesAsync(credit.Id, true, paylar);

                    _logger.LogInformation(
                        "👥 Kredit bölgüsü saxlanıldı: kredit #{Id}, xeyir {Baza:N2} ₼, cəm {Cem:N2} ₼",
                        credit.Id, KreditBolguBazasi, KreditBolguCemi);
                }

                // Kreditə verilmiş avtomobil avtomatik olaraq aktiv parkdan və arxivdən çıxarılır.
                var creditCar = SelectedCar;
                if (creditCar is not null)
                {
                    creditCar.Status = Catalog.CreditStatus;
                    await _carService.UpdateCarAsync(creditCar);
                    _logger.LogInformation("Avtomobil kreditə salındı: {Car}", creditCar.DisplayName);
                }

                _logger.LogInformation("Yeni kredit əlavə edildi: {Customer}", credit.Mustəri);

                // ⏳ Möhlət varsa — təsdiq mesajında ƏTRAFLI göstərilir ✓✓✓
                var mesaj = "Kredit uğurla əlavə edildi. Avtomobil avtomatik olaraq aktiv parkdan çıxarıldı.";

                if (IlkinMohletVar && IlkinMohletleri.Count > 0)
                {
                    var setirler = string.Join(
                        "\n",
                        IlkinMohletleri
                            .OrderBy(m => m.Tarix)
                            .Select(m => $"   • {m.Mebleg:N2} ₼  →  {m.TarixMetni}  ({m.OdenisUsulu})"));

                    mesaj += "\n\n⏳ İLKİN ÖDƏNİŞƏ MÖHLƏT\n" +
                             $"   İlkin ödəniş    : {IlkinOdenis:N2} ₼\n" +
                             $"   Dərhal ödənilən : {IlkinDerhalOdenilen:N2} ₼\n" +
                             $"   Möhlətə yazılan : {IlkinMohletCemi:N2} ₼\n{setirler}\n\n" +
                             "ℹ️ Möhlət ÖDƏNİLDİKDƏ kassaya daxil olacaq (💵 Kassa tabı) ✓";
                }

                ClearForm();
                _dialogs.ShowInfo(mesaj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Kredit əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
            CreditsChanged?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task DeleteCreditAsync()
        {
            if (SelectedCredit is null)
            {
                _dialogs.ShowWarning("Silmək üçün siyahıdan kredit seçin.");
                return;
            }

            var credit = SelectedCredit;
            if (!_dialogs.Confirm($"\"{credit.Mustəri}\" adlı müştərinin kreditini silmək istəyirsiniz?"))
            {
                return;
            }

            try
            {
                IsBusy = true;

                var creditCar = credit.Car;

                await _creditService.DeleteCreditAsync(credit.Id);
                SelectedCredit = null;

                // Kredit silindikdə avtomobil yenidən aktiv parka qaytarılır.
                if (creditCar is not null)
                {
                    creditCar.Status = Catalog.StockStatus;
                    await _carService.UpdateCarAsync(creditCar);
                    _logger.LogInformation("Avtomobil aktiv parka qaytarıldı: {Car}", creditCar.DisplayName);
                }

                _logger.LogInformation("Kredit silindi: {Id}", credit.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit silinərkən xəta baş verdi.");
                _dialogs.ShowError("Kredit silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
            CreditsChanged?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void ClearForm()
        {
            MuqavileNomresi = string.Empty;
            Musteri = string.Empty;
            SelectedCar = null;
            Mebleg = 0m;
            IlkinOdenis = 0m;
            _lastAutoMebleg = 0m;
            CarSearchText = string.Empty;
            FaizDerecesi = 12m;
            MuddetAy = 12;
            BaslamaTarixi = DateTime.Today;
            Status = "Aktiv";
            Qeyd = string.Empty;

            // ⏳ İlkin ödəniş möhləti də sıfırlanır ✓✓✓
            IlkinMohletleri.Clear();
            IlkinMohletVar = false;

            ClearErrors();
        }

        /// <summary>
        /// Ödəniş qrafikini qurur. Faktiki ödənişlər nəzərə alınır:
        /// artıq ödəniş balansı azaldır və qalan ayların aylıq ödənişi yenidən hesablanır.
        /// Az ödənişdə isə qalıq artır və sonrakı ödənişlər artır.
        /// </summary>
        /// <summary>
        /// Ödəniş qrafikini qurur və sətirlərə TƏRƏFDAŞ PAYLARINI bağlayır.
        /// </summary>
        private void BuildSchedule(Credit? credit)
        {
            BuildScheduleCore(credit);
            AttachPartnerShares();

            // ================================================================
            //  🩺 GECİKMƏ CƏRİMƏLƏRİ → SON AYA ƏLAVƏ OLUNUR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ VACİB: bu, cədvəl TAM qurulduqdan SONRA işləyir ✓ —
            //  əks halda sonrakı hesablamalar `ArtiqKecen`-i sıfırlayırdı ✗✓✓
            //
            //  Məs.: 12 ay · aylıq 1 284,00 ₼ · gecikmə 100,00 ₼
            //        → SON AY = 1 284,00 + 100,00 = 1 384,00 ₼ ✓✓✓
            // ================================================================
            GecikmeSonAyaElave(credit);
        }

        /// <summary>
        /// ⏱ <b>GECİKMƏ CƏRİMƏLƏRİNİ SON AYA ƏLAVƏ EDİR</b> ✓✓✓
        /// <para>
        /// Kredit üzrə bütün «Gecikmə» qeydlərinin məbləği cəmlənir ✓ və
        /// <b>sonuncu plan ayının</b> ödənişinə əlavə olunur ✓
        /// (<c>ArtiqKecen</c> mənfi → <c>NetOdenis</c> artır ✓).
        /// </para>
        /// <para>
        /// Eyni zamanda <b>QALIQ</b> da gecikmə qədər artır ✓ — kredit
        /// tabındaki «Qalıq» göstəricisi düzgün olur ✓✓✓
        /// </para>
        /// </summary>
        private void GecikmeSonAyaElave(Credit? credit)
        {
            var hedef = credit ?? SelectedCredit;

            if (hedef is null)
            {
                return;
            }

            var gecikmeCemi = _creditTransactions
                .Where(t => t.CreditId == hedef.Id
                            && t.Nov == "Gecikmə"
                            && !t.Odenilib)      // ✅ yalnız ÖDƏNİLMƏMİŞ ✓✓✓
                .Sum(t => t.Mebleg);

            if (gecikmeCemi <= 0m)
            {
                return;
            }

            var sonAyi = Schedule.LastOrDefault(r => !r.XaricindeHedd);

            if (sonAyi is null)
            {
                return;
            }

            // ✅ GECİKMƏ SON AYIN ÖDƏNİŞİNƏ ƏLAVƏ OLUNUR ✓✓✓
            sonAyi.ArtiqKecen -= gecikmeCemi;

            _logger.LogInformation(
                "⏱ GECİKMƏ SON AYA ƏLAVƏ OLUNDU ✓: {Mebleg:N2} ₼ → {Ay}-ci ay " +
                "(plan {Odenis:N2} ₼ → {Net:N2} ₼ ✓)",
                gecikmeCemi, sonAyi.No, sonAyi.Odenis, sonAyi.NetOdenis);
        }

        /// <summary>
        /// Qrafik sətirlərinə həmin ayın <b>TƏRƏFDAŞ PAYLARINI</b> bağlayır.
        /// <para>
        /// «Kredit əlavə gəlir» qeydində kimə nə qədər verildiyi (məs. Asiman,
        /// Zaur, Eşqin) <b>ödəniş qrafikində də</b> görünür — hər tərəfdaş
        /// ayrı sətirdə.
        /// </para>
        /// </summary>
        private void AttachPartnerShares()
        {
            foreach (var row in Schedule)
            {
                if (row.TransactionIds.Count == 0)
                {
                    continue;
                }

                var shares = new List<PartnerShare>();

                foreach (var id in row.TransactionIds)
                {
                    var transaction = _creditTransactions.FirstOrDefault(t => t.Id == id);

                    if (transaction is { TerefdasPaylari.Count: > 0 })
                    {
                        shares.AddRange(transaction.TerefdasPaylari);
                    }
                }

                row.TerefdasPaylari = shares;
            }

            OnPropertyChanged(nameof(AnyBolgu));
        }

        /// <summary>
        /// Qrafikdə hər hansı ayda tərəfdaş bölgüsü varmı?
        /// (Cədvəldə «👥 Tərəfdaş bölgüsü» sütununun görünüşünü idarə edir.)
        /// </summary>
        public bool AnyBolgu => Schedule.Any(r => r.BolguVar);

        private void BuildScheduleCore(Credit? credit)
        {
            foreach (var existing in Schedule)
            {
                existing.PropertyChanged -= OnPaymentRowChanged;
            }
            Schedule.Clear();

            if (credit is null || credit.MuddetAy <= 0)
            {
                return;
            }

            // ================================================================
            //  SADƏ FAİZ: ümumi faiz bütün müddətə BƏRABƏR bölünür.
            //  Aylıq faiz hissəsi = Kreditləşdirilən × Faiz% ÷ 100 ÷ Müddət
            //  (Əvvəllər qalıq balansa görə «mürəkkəb» hesablanırdı — səhv idi.)
            // ================================================================
            var interestPerMonth = credit.MuddetAy > 0
                ? Sinirla(credit.Kreditlesdirilen * credit.FaizDerecesi / 100m / credit.MuddetAy)
                : 0m;
            var payments = CollectPayments(credit);

            // ================================================================
            //  🤝 BARTER — NİSYƏ OLUNAN QİYMƏTDƏN ÇIXILIR ✓✓✓
            // ----------------------------------------------------------------
            //  Müştəri nisyəyə girən maşının bir hissəsini BAŞQA MAŞINLA
            //  (məs. sənəd adı «M188») ödəyibsə — həmin maşının MAYA DƏYƏRİ
            //  kredit məbləğindən ÇIXILIR ✓.
            //
            //  Nümunə: Kreditləşdirilən 4 000 ₼ · Barter maya 1 200 ₼ (M188)
            //          → qrafik 2 800 ₼ üzərindən qurulur ✓✓✓
            //
            //  Barter «CreditTransaction.Nov = "Barter"» kimi saxlanılır ✓
            //  (yeni cədvəl/miqrasiya LAZIM DEYİL ✗✓)
            // ================================================================
            // ================================================================
            //  📤🤝 TRANSFER + BARTER — HAMISI NİSYƏ QİYMƏTİNDƏN ÇIXILIR ✓✓✓
            // ----------------------------------------------------------------
            //  «Transfer olunmaq» · «Barter» · «Barter köhnə» növləri ilə yazılan
            //  məbləğlər kreditin qiymətindən çıxılır ✓ → qrafik həmin azalmış
            //  məbləğ üzərindən qurulur ✓
            // ================================================================
            var barterCemi = BarterCemiHesabla(credit);
            var transferCemiYerli = TransferCemiHesabla(credit);
            var cixilan = barterCemi + transferCemiYerli;

            // Qrafik yalnız KREDİTLƏŞDİRİLƏN məbləğ (Məbləğ − İlkin ödəniş) üzərindən
            // qurulur — BARTER və TRANSFER dəyərləri çıxıldıqdan sonra ✓
            var balance = Math.Max(0m, credit.Kreditlesdirilen - cixilan);
            if (balance <= 0m)
            {
                return;
            }

            var total = credit.MuddetAy;
            var deferrals = CollectDeferrals(credit);
            var delays = CollectGecikmeler(credit);

            // 1) Kredit başlamazdan ƏVVƏL edilmiş ödənişlər → ayrı NARINCI sətir.
            foreach (var group in payments.Where(p => p.Key <= 0).OrderBy(p => p.Key))
            {
                var paid = group.Value.Sum(t => t.Mebleg);
                balance = Math.Max(0m, balance - paid);
                Schedule.Add(CreateOutOfRangeRow(group.Key, group.Value, paid, balance, "Müddətdən əvvəl"));
            }

            // Artıq ödəniş yığımı — SONUNCU aylardan çıxılır.
            var artiqToplam = 0m;

            // AZ ödəniş yığımı — SONUNCU aylara ƏLAVƏ olunur ✓✓✓
            var azToplam = 0m;

            for (var i = 1; i <= total; i++)
            {
                var interest = interestPerMonth;

                // ============================================================
                //  ✅ PLAN (AYLIQ) MƏBLƏĞ — **SABİT** ✓✓✓
                // ------------------------------------------------------------
                //  Aylıq məbləğ = Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət
                //
                //  ⚠ ƏVVƏLKİ XƏTA: `CalculateMonthly(balance, …)` — hər ay
                //  QALAN balansa görə yenidən hesablanırdı ✗ → aylıq məbləğ
                //  ay-ay sürüşürdü: 310,00 → 310,48 → 310,50 → 310,53 … ✗✓
                //  Nəticədə gecikmə/erkən ödəniş və ya hər hansı ödəniş
                //  QİYMƏTİ AVTOMATİK DƏYİŞİRDİ ✗ — bu artıq BAŞ VERMİR ✓.
                //
                //  Qrafik cəmi dəqiq bağlanır: total × planned = K + faiz ✓
                // ============================================================
                var planned = CalculateMonthly(credit.Kreditlesdirilen, credit.FaizDerecesi, total);

                payments.TryGetValue(i, out var list);
                var paid = list?.Sum(t => t.Mebleg) ?? 0m;

                // ============================================================
                //  ARTIQ ÖDƏNİŞ → SONUNCU AYLARDAN ÇIXILIR
                // ------------------------------------------------------------
                //  Aylıq 1 800 ₼ ödənilməlidir, amma 2 000 ₼ ödənilib →
                //  200 ₼ artıq ödəniş yığılır və balansdan çıxılır. Beləliklə
                //  SONUNCU ayların plan məbləği azalır (lazım gələrsə 0 olur).
                // ============================================================
                if (list is not null && paid > planned)
                {
                    artiqToplam += Math.Round(paid - planned, 2);
                }

                // Möhlət verilibsə: bu ayın ödənişi PLAN səviyyəsində sayılır →
                // qalıq plana uyğun azalır və SONRAKI AYLARIN ÖDƏNİŞİ ARTMIYIR.
                deferrals.TryGetValue(i, out var mohletler);

                // ============================================================
                //  ⬆ AZ ÖDƏNİŞ → SON AYNLARA ƏLAVƏ OLUNUR ✓✓✓
                // ------------------------------------------------------------
                //  Müştəri bir ayda PLAN-dan az ödəyibsə, çatışmayan məbləğ
                //  yığılır və SONUNCU aylara ƏLAVƏ edilir (əks istiqamət).
                //  Möhlət verilmiş aylar hesaba alınmır ✗ (onlar plan sayılır ✓).
                // ============================================================
                if (list is not null && mohletler is null && paid < planned)
                {
                    azToplam += Math.Round(planned - paid, 2);
                }
                // 📅 PLAN TARİXİ: 1-ci ay = BAŞLAMA TARİXİ özü ✓
                //    (əvvəl `AddMonths(i)` idi → 1-ci ay başlamadan 1 ay SONRA
                //     görünürdü ✗ — sizin gördüyünüz «14.06.2025» xətası)
                var planTarix = credit.BaslamaTarixi.AddMonths(i - 1);
                var shortfall = Math.Max(0m, planned - paid);

                var mohletSetirleri = new List<string>();
                var mohletMeblegi = 0m;
                DateTime? sonMohletTarixi = null;

                foreach (var m in mohletler ?? new List<CreditTransaction>())
                {
                    var mebleg = m.Mebleg > 0m ? m.Mebleg : shortfall;
                    mohletMeblegi += mebleg;
                    if (m.MohletTarixi is DateTime md && (sonMohletTarixi is null || md > sonMohletTarixi))
                    {
                        sonMohletTarixi = md;
                    }

                    mohletSetirleri.Add(m.MohletTarixi is DateTime limit
                        ? $"{mebleg:N2} ₼ → {limit:dd.MM.yyyy}"
                        : $"{mebleg:N2} ₼");
                }

                // Gecikmə qeydləri — bir ayda BİRDƏN ÇOX ola bilər, hər biri ayrı sətirdə.
                delays.TryGetValue(i, out var gecikmeler);
                var gecikmeSetirleri = new List<string>();
                var gecikmeMeblegi = 0m;
                var gecikmeAktiv = false;
                DateTime? sonGecikmeTarixi = null;

                foreach (var g in gecikmeler ?? new List<CreditTransaction>())
                {
                    gecikmeMeblegi += g.Mebleg;
                    if (!g.Odenilib)
                    {
                        gecikmeAktiv = true;
                    }

                    if (g.GecikmeTarixi is DateTime gd && (sonGecikmeTarixi is null || gd > sonGecikmeTarixi))
                    {
                        sonGecikmeTarixi = gd;
                    }

                    var details = new List<string>();
                    if (g.GecikmeTarixi is DateTime gecikmeTarix)
                    {
                        var gun = Math.Max(0, (int)(gecikmeTarix.Date - planTarix.Date).TotalDays);
                        details.Add(gun > 0 ? $"{gun} gün → {gecikmeTarix:dd.MM.yyyy}" : $"{gecikmeTarix:dd.MM.yyyy}");
                    }

                    if (g.Mebleg > 0m)
                    {
                        details.Add($"{g.Mebleg:N2} ₼ cərimə");
                    }

                    // ✅ «⏳ gözləyir» YAZILMIR ✗✓✓ — yalnız ÖDƏNİLDİKDƏ göstərilir ✓
                if (g.Odenilib)
                {
                    details.Add("✔ ödənilib");
                }
                    gecikmeSetirleri.Add(string.Join(" · ", details));
                }

                // Balans faktiki ödənişə görə azalır (ödənilməyibsə plana görə).
                // ⚠ ARTIQ hissə balansı SÜRƏTLƏNDİRMİR — o, ayrıca yığılır və
                //   SONUNCU aylara kredit kimi yazılır (aşağıdaki 3-cü addım).
                //   Əks halda artıq ödəniş bütün qalan aylara səpələnirdi ✗.
                var effective = list is not null ? Math.Min(paid, planned) : planned;
                if (mohletler is not null)
                {
                    effective = Math.Max(effective, planned);
                }

                var principal = Math.Round(effective - interest, 2);
                balance = Math.Round(balance - principal, 2);
                if (balance < 0m)
                {
                    balance = 0m;
                }

                var row = new PaymentRow
                {
                    No = i,
                    Tarix = planTarix,
                        Odenis = planned,          // planlaşdırılan məbləğ (yaşıl/qırmızı müqayisəsi üçün)
                    EsasBorclu = principal,
                    Faiz = interest,
                    // Artıq ödəniş artıq ödənilib → real borc daha azdır.
                    Qaliq = Math.Max(0m, Math.Round(balance - artiqToplam, 2)),
                    MohletVar = mohletler is not null,
                    MohletTarixi = sonMohletTarixi,
                    MohletMeblegi = mohletMeblegi,
                    MohletSetirleri = mohletSetirleri,
                    GecikmeVar = gecikmeler is not null,
                    GecikmeAktiv = gecikmeAktiv,
                    GecikmeTarixi = sonGecikmeTarixi,
                    GecikmeMeblegi = gecikmeMeblegi,
                    GecikmeSetirleri = gecikmeSetirleri
                };

                if (list is not null)
                {
                    ApplyPaid(row, list);    // faktiki ödənişlərin cəmi və detalları
                }

                row.PropertyChanged += OnPaymentRowChanged;
                Schedule.Add(row);
            }

            // ================================================================
            //  3) ⬇ ARTIQ ÖDƏNİŞ SONUNCU AYLARDAN ÇIXILIR  (SONDAN GERİYƏ)
            // ----------------------------------------------------------------
            //  Nümunə: 12 ay × 1 984,97 ₼. 1-ci ayda 2 000 ₼ ödənilib →
            //  15,03 ₼ artıq yığılır və bu məbləğ SONUNCU aydan (12-ci)
            //  çıxılır → həmin ayın NET planı 1 969,94 ₼ olur.
            //
            //  Əvvəl bu dəyər BÜTÜN aylara yazılırdı ✗ — indi yalnız faktiki
            //  məbləğ çıxılan SON aylara yazılır ✓
            // ================================================================
            PaylaArtiqOdenisi(artiqToplam);

            // ⬆ AZ ÖDƏNİŞ SONUNCU AYLARA ƏLAVƏ OLUNUR (əks istiqamət) ✓
            PaylaAzOdenisi(azToplam);

            // 3) Kredit müddəti BİTDİKDƏN SONRA edilmiş ödənişlər → ayrı NARINCI sətir.
            foreach (var group in payments.Where(p => p.Key > total).OrderBy(p => p.Key))
            {
                var paid = group.Value.Sum(t => t.Mebleg);
                balance = Math.Max(0m, balance - paid);
                Schedule.Add(CreateOutOfRangeRow(group.Key, group.Value, paid, balance, "Müddətdən sonra"));
            }
        }

        /// <summary>
        /// <b>ARTIQ ÖDƏNİŞİ SONUNCU AYLARA PAYLAYIR (sondan geriyə).</b>
        /// <para>
        /// Nümunə: 12 ay × 1 984,97 ₼ kredit, 1-ci ayda 2 000 ₼ ödənilib →
        /// artıq <b>15,03 ₼</b> yığılır. Bu məbləğ <b>12-ci</b> aydan çıxılır →
        /// həmin ayın planı <b>1 969,94 ₼</b> olur.
        /// </para>
        /// <para>
        /// Məbləğ bir aydan çoxdursa, qonşu SON aylara da keçir (məs. 2 500 ₼
        /// artıq varsa — son 2-3 ay sıfıra qədər azalır).
        /// </para>
        /// <para>
        /// ⚠ Müddətdən kənar (narıncı) sətirlərə <b>toxunulmur</b> ✗ — onların
        /// öz planı yoxdur, yalnız faktiki ödəniş göstərilir.
        /// </para>
        /// </summary>
        private void PaylaArtiqOdenisi(decimal artiqToplam)
        {
            if (artiqToplam <= 0m || Schedule.Count == 0)
            {
                return;
            }

            var qalan = Math.Round(artiqToplam, 2);

            // SONDAN GERİYƏ: sonuncu aydan başlayaraq məbləği çıxırıq.
            for (var idx = Schedule.Count - 1; idx >= 0 && qalan > 0m; idx--)
            {
                var row = Schedule[idx];

                // Müddətdən kənar sətirlər (əvvəl / sonra) — plan yoxdur, atla.
                if (row.XaricindeHedd)
                {
                    continue;
                }

                // Bu aydan çıxıla bilən maksimum: plan məbləği (sıfırdan aşağı düşməsin).
                var cixilan = Math.Min(row.Odenis, qalan);
                cixilan = Math.Round(cixilan, 2);

                if (cixilan <= 0m)
                {
                    continue;
                }

                // ⚠ `ArtiqKecen` yalnız BURA yazılır — yəni yalnız faktiki
                //   məbləğ çıxılan SON aylara ✓
                row.ArtiqKecen = cixilan;
                qalan = Math.Round(qalan - cixilan, 2);
            }

            _logger.LogInformation(
                "⬇ Artıq ödəniş son aylara paylaşıldı: {Mebleg:N2} ₼ · azalan aylar: {Aylar}",
                artiqToplam,
                string.Join(", ", Schedule
                    .Where(r => r.ArtiqKecen > 0m)
                    .Select(r => $"{r.No} ({r.ArtiqKecen:N2} ₼)")));
        }

        /// <summary>
        /// <b>AZ ÖDƏNİŞİ SONUNCU AYLARA ƏLAVƏ EDİR (sondan geriyə).</b> ✓✓✓
        /// <para>
        /// <see cref="PaylaArtiqOdenisi"/> funksiyasının <b>əks</b> istiqamətidir:
        /// çox ödəniş son aylardan <b>çıxılır</b> ✗, az ödəniş isə son aylara
        /// <b>əlavə olunur</b> ✓ — yəni borc geriyə yox, <b>İRƏLİYƏ</b> yığılır ✓.
        /// </para>
        /// <para>
        /// Nümunə: 12 ay × 1 000,00 ₼ kredit, 3-cü ayda cəmi <b>400,00 ₼</b>
        /// ödənilib → çatışmayan <b>600,00 ₼</b> yığılır və SONUNCU aydan
        /// başlayaraq geriyə doğru aylara <b>əlavə</b> olunur ✓.
        /// </para>
        /// <para>
        /// ⚠ Məbləğ <see cref="PaymentRow.ArtiqKecen"/> sahəsində <b>MƏNFİ</b>
        /// kimi saxlanılır ✓ — mənfi = «bu aya əlavə olundu» ✓.
        /// </para>
        /// </summary>
        private void PaylaAzOdenisi(decimal azToplam)
        {
            if (azToplam <= 0m || Schedule.Count == 0)
            {
                return;
            }

            var qalan = Math.Round(azToplam, 2);

            // SONDAN GERİYƏ: sonuncu aydan başlayaraq məbləği ƏLAVƏ edirik.
            for (var idx = Schedule.Count - 1; idx >= 0 && qalan > 0m; idx--)
            {
                var row = Schedule[idx];

                // Müddətdən kənar sətirlər (əvvəl / sonra) — plan yoxdur, atla.
                if (row.XaricindeHedd)
                {
                    continue;
                }

                // Bir aya əlavə oluna bilən maksimum = həmin ayın plan məbləği ✓
                var elave = Math.Min(row.Odenis, qalan);
                elave = Math.Round(elave, 2);

                if (elave <= 0m)
                {
                    continue;
                }

                // MƏNFİ → «bu aya əlavə olundu» ✓
                row.ArtiqKecen -= elave;
                qalan = Math.Round(qalan - elave, 2);
            }

            _logger.LogInformation(
                "⬆ Az ödəniş son aylara əlavə olundu: {Mebleg:N2} ₼ · artan aylar: {Aylar}",
                azToplam,
                string.Join(", ", Schedule
                    .Where(r => r.ArtiqKecen < 0m)
                    .Select(r => $"{r.No} ({Math.Abs(r.ArtiqKecen):N2} ₼)")));

            // ================================================================
            //  🩺 GECİKMƏ CƏRİMƏLƏRİ → SON AYA ƏLAVƏ OLUNUR ✓✓✓
            // ----------------------------------------------------------------
            //  Məsələn: 12 ay · aylıq 1 284,00 ₼ · gecikmə 100,00 ₼
            //      → SON AY = 1 284,00 + 100,00 = 1 384,00 ₼ ✓✓✓
            //
            //  ⚠ ƏVVƏL gecikmə YALNIZ məlumat kimi göstərilirdi ✗ —
            //  məbləğ cədvələ ƏLAVƏ OLUNMURDU ✗✓✓
            //
            //  `ArtiqKecen` MƏNFİ olarsa `NetOdenis` ARTIR ✓ (az ödəniş
            //  məntiqi ilə eyni ✓) → son ayın ödənişi böyüyür ✓
            // ================================================================
            // 🩺 GECİKMƏ SON AYA → `BuildSchedule`-də, cədvəl BİTDİKDƏN SONRA ✓
        }

        /// <summary>
        /// Kredit müddətindən kənar ödəniş üçün narıncı sətir yaradır.
        /// Bu sətirdə plan məbləği yoxdur — yalnız faktiki ödəniş göstərilir.
        /// </summary>
        private PaymentRow CreateOutOfRangeRow(
            int installmentNo,
            List<CreditTransaction> payments,
            decimal paid,
            decimal balance,
            string novu)
        {
            var ordered = payments.OrderBy(t => t.Tarix).ThenBy(t => t.Id).ToList();

            var row = new PaymentRow
            {
                No = installmentNo,
                Tarix = ordered[^1].Tarix,   // faktiki ödəniş tarixi (plan tarixi yoxdur)
                Odenis = paid,
                EsasBorclu = paid,
                Faiz = 0m,
                Qaliq = balance,
                Novu = novu,
                XaricindeHedd = true
            };

            ApplyPaid(row, ordered);
            row.PropertyChanged += OnPaymentRowChanged;
            return row;
        }

        /// <summary>
        /// Kreditə aid "Gəlir" qeydlərini ay sırasına görə qruplaşdırır.
        /// Müddətdən KƏNAR ödənişlər də saxlanılır:
        /// açar ≤ 0 → başlama tarixindən əvvəl, açar &gt; Müddət → sonuncu aydan sonra.
        /// </summary>
        private Dictionary<int, List<CreditTransaction>> CollectPayments(Credit credit)
        {
            var result = new Dictionary<int, List<CreditTransaction>>();

            foreach (var transaction in _creditTransactions.Where(t => t.CreditId == credit.Id && t.Nov == "Gəlir"))
            {
                var no = transaction.InstallmentNo ?? MonthOffset(transaction.Tarix, credit.BaslamaTarixi);
                if (!result.TryGetValue(no, out var list))
                {
                    list = new List<CreditTransaction>();
                    result[no] = list;
                }

                list.Add(transaction);
            }

            return result;
        }

        // ====================================================================
        //  🤝 BARTER  —  nisyə olunan qiymətdən çıxılan maşın
        // --------------------------------------------------------------------
        //  «Sənəd adı ilə M188 ilə barter olunub, maya dəyəri 1 200 ₼» → bu
        //  məbləğ kreditin NİSYƏ məbləğindən ÇIXILIR ✓ və qrafik yenidən
        //  qurulur ✓. Saxlanma: `CreditTransaction.Nov = "Barter"` ✓
        //  (yeni cədvəl/miqrasiya LAZIM DEYİL ✗✓)
        // ====================================================================

        /// <summary>Seçilmiş kreditin barter qeydləri (cədvəl üçün).</summary>
        public ObservableCollection<CreditTransaction> Barterler { get; } = new();

        /// <summary>Barter edilən maşının sənəd adı (məs. «M188»).</summary>
        [ObservableProperty] private string barterSenedAdi = string.Empty;

        /// <summary>Barter edilən maşının marka/modeli (məs. «VAZ 2107»).</summary>
        [ObservableProperty] private string barterMasin = string.Empty;

        /// <summary>Barter edilən maşının MAYA DƏYƏRİ (₼) — nisyədən çıxılır ✓.</summary>
        [ObservableProperty] private decimal barterMayaDeyeri;

        /// <summary>Barter tarixi.</summary>
        [ObservableProperty] private DateTime? barterTarix = DateTime.Today;

        /// <summary>Barter qeydi (əlavə izah).</summary>
        [ObservableProperty] private string barterQeyd = string.Empty;

        /// <summary>Barter edilən maşının MARKASI (Avto Park-daki kimi ✓).</summary>
        [ObservableProperty] private string barterMarka = string.Empty;

        /// <summary>Barter edilən maşının MODELİ (Avto Park-daki kimi ✓).</summary>
        [ObservableProperty] private string barterModel = string.Empty;

        /// <summary>Barter maşınının tam adı: «VAZ 2107» (Avto Park formatı ✓).</summary>
        public string BarterMasinAdi =>
            $"{BarterMarka?.Trim()} {BarterModel?.Trim()}".Trim();

        /// <summary>🤝 BARTER KEÇMİŞİ — bütün kreditlər üzrə (maşın adı + qiymət) ✓.</summary>
        public ObservableCollection<string> BarterKecmisi { get; } = new();

        /// <summary>Barter keçmişinin ümumi xülasəsi ✓.</summary>
        public string BarterKecmisXulase
        {
            get
            {
                var say = BarterKecmisi.Count;
                var cem = BarterCemiHesabla(SelectedCredit);

                var hamisi = _creditTransactions
                    .Where(t => t.Nov == "Barter")
                    .Sum(t => t.Mebleg);

                return say == 0
                    ? "Hələ barter qeydə alınmayıb"
                    : $"{say} barter · bu kreditdə {cem:N2} ₼ · bütün kreditlərdə {hamisi:N2} ₼";
            }
        }

        /// <summary>Seçilmiş kreditin barter cəmi (₼) — nisyədən çıxılan.</summary>
        [ObservableProperty] private decimal barterCemi;

        /// <summary>Barter varmı (panel göstəricisi).</summary>
        public bool HasBarter => Barterler.Count > 0;

        /// <summary>«1 200,00 ₼ nisyədən çıxıldı → qrafik 3 400,00 ₼ üzərindən».</summary>
        public string BarterXulase
        {
            get
            {
                var kreditlesdirilen = SelectedCredit?.Kreditlesdirilen ?? 0m;
                var qalan = Math.Max(0m, kreditlesdirilen - BarterCemi);

                return BarterCemi <= 0m
                    ? "Barter yoxdur — nisyə tam məbləğ üzərindən hesablanır"
                    : $"{BarterCemi:N2} ₼ nisyədən çıxıldı  →  qrafik {qalan:N2} ₼ üzərindən";
            }
        }

        /// <summary>
        /// Barter cəmindən nisyədən çıxılan məbləğ ✓
        /// <para>«Barter» və «Barter köhnə» növlərinin cəmi ✓</para>
        /// </summary>
        private decimal BarterCemiHesabla(Credit? credit)
            => credit is null
                ? 0m
                : _creditTransactions
                    .Where(t => t.CreditId == credit.Id
                                && (t.Nov == "Barter" || t.Nov == "Barter köhnə"))
                    .Sum(t => t.Mebleg);

        /// <summary>
        /// Transfer cəmi — nisyədən çıxılan ✓
        /// <para>«Transfer» və «Transfer olunmaq» növlərinin cəmi ✓</para>
        /// </summary>
        private decimal TransferCemiHesabla(Credit? credit)
            => credit is null
                ? 0m
                : _creditTransactions
                    .Where(t => t.CreditId == credit.Id
                                && (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq"))
                    .Sum(t => t.Mebleg);

        /// <summary>Barter panelini seçilmiş kreditə görə yeniləyir ✓.</summary>
        private void BarterYenile(Credit? credit)
        {
            Barterler.Clear();

            if (credit is not null)
            {
                foreach (var barter in _creditTransactions
                             .Where(t => t.CreditId == credit.Id && t.Nov == "Barter")
                             .OrderByDescending(t => t.Tarix)
                             .ThenByDescending(t => t.Id))
                {
                    Barterler.Add(barter);
                }
            }

            BarterCemi = BarterCemiHesabla(credit);

            // ================================================================
            //  🤝 BARTER KEÇMİŞİ — BÜTÜN KREDİTLƏR ÜZRƏ ✓
            //  «Yalnız maşın adı və qiyməti» ✓ — kredit qiymətindən çıxılan ✓
            // ================================================================
            BarterKecmisi.Clear();

            foreach (var b in _creditTransactions
                         .Where(t => t.Nov == "Barter")
                         .OrderByDescending(t => t.Tarix)
                         .ThenByDescending(t => t.Id))
            {
                var kredit = Credits.FirstOrDefault(c => c.Id == b.CreditId);

                BarterKecmisi.Add(
                    $"🚗 {b.Tesvir}   ·   💰 {b.Mebleg:N2} ₼   ·   📅 {b.Tarix:dd.MM.yyyy}\n" +
                    $"      💳 Kredit: {kredit?.MuqavileNomresi ?? "—"} · " +
                    $"{kredit?.Mustəri ?? "—"} · " +
                    $"Nisyədən çıxıldı: {b.Mebleg:N2} ₼");
            }

            OnPropertyChanged(nameof(HasBarter));
            OnPropertyChanged(nameof(BarterXulase));
            OnPropertyChanged(nameof(BarterKecmisXulase));
        }

        /// <summary>
        /// <b>🤝 BARTER ƏLAVƏ EDİR</b> ✓ — barter edilən maşının maya dəyəri
        /// nisyə məbləğindən çıxılır ✓ və qrafik avtomatik yenidən qurulur ✓.
        /// </summary>
        [RelayCommand]
        private async Task BarterElaveEtAsync()
        {
            if (SelectedCredit is null)
            {
                _dialogs.ShowWarning("Barter əlavə etmək üçün əvvəlcə kredit seçin.");
                return;
            }

            if (string.IsNullOrWhiteSpace(BarterSenedAdi))
            {
                _dialogs.ShowWarning("Barter edilən maşının SƏNƏD ADINI yazın (məs. «M188»).");
                return;
            }

            if (BarterMayaDeyeri <= 0m)
            {
                _dialogs.ShowWarning("Barter edilən maşının MAYA DƏYƏRİNİ yazın.");
                return;
            }

            if (BarterCemi + BarterMayaDeyeri > SelectedCredit.Kreditlesdirilen)
            {
                _dialogs.ShowWarning(
                    "Barter cəmi nisyə məbləğindən böyük ola bilməz.\n" +
                    $"Nisyə: {SelectedCredit.Kreditlesdirilen:N2} ₼ · " +
                    $"Barter cəmi: {BarterCemi + BarterMayaDeyeri:N2} ₼");
                return;
            }

            try
            {
                IsBusy = true;

                var tesvir = string.Join(" · ", new[]
                {
                    $"Sənəd: {BarterSenedAdi.Trim()}",
                    string.IsNullOrWhiteSpace(BarterMasinAdi) ? null : $"Maşın: {BarterMasinAdi}",
                    string.IsNullOrWhiteSpace(BarterQeyd) ? null : BarterQeyd.Trim()
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

                await _creditService.AddTransactionAsync(new CreditTransaction
                {
                    CreditId = SelectedCredit.Id,
                    Nov = "Barter",
                    Mebleg = BarterMayaDeyeri,
                    Tarix = BarterTarix ?? DateTime.Today,
                    Tesvir = tesvir
                });

                _logger.LogInformation(
                    "🤝 Barter əlavə edildi: kredit {Id} · sənəd {Sened} · maya {Maya:N2} ₼",
                    SelectedCredit.Id, BarterSenedAdi, BarterMayaDeyeri);

                BarterSenedAdi = string.Empty;
                BarterMarka = string.Empty;
                BarterModel = string.Empty;
                BarterMayaDeyeri = 0m;
                BarterQeyd = string.Empty;

                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Barter əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Barter əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>🤝 Barter qeydini SİLİR ✓ — nisyə yenidən tam məbləğə qayıdır ✓.</summary>
        [RelayCommand]
        private async Task BarterSilAsync(CreditTransaction? barter)
        {
            if (barter is null)
            {
                _dialogs.ShowWarning("Silmək üçün cədvəldən barter sətri seçin.");
                return;
            }

            if (!_dialogs.Confirm(
                    $"Barter qeydi silinsin?\n\n{barter.Tesvir}\n\n" +
                    $"Maya dəyəri {barter.Mebleg:N2} ₼ yenidən nisyəyə ƏLAVƏ olunacaq.",
                    "Barter silinməsi"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _creditService.DeleteTransactionAsync(barter.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Barter silinərkən xəta baş verdi.");
                _dialogs.ShowError("Barter silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ====================================================================
        //  📤 KREDİT TRANSFER  —  maşının bir ŞƏXSƏ verilməsi (beh ilə)
        // --------------------------------------------------------------------
        //  «Kredit Əlavə Gəlir/Xərc»-də şəxs (məs. Tural) seçilir → maşın
        //  həmin şəxsə TRANSFER edilir ✓. Şəxs siyahısını İSTİFADƏÇİ idarə edir
        //  (➕ əlavə et · 🗑️ sil) ✓.
        //  Saxlanma: `Nov = "Transfer"` (transfer) · `Nov = "TransferŞəxs"`
        //  (şəxs markeri) → yeni cədvəl/miqrasiya LAZIM DEYİL ✗✓
        // ====================================================================

        /// <summary>🔎 Transfer AXTARIŞI ✓ (maşın adı · dövlət nömrəsi · şəxs adı ✓).</summary>
        [ObservableProperty] private string transferAxtaris = string.Empty;

        /// <summary>📅 Transfer DÖVRÜ ✓ (Hamısı · Bu ay · Keçən ay · Bu il ✓).</summary>
        [ObservableProperty] private string transferDovr = "Hamısı";

        /// <summary>Dövr seçimləri ✓.</summary>
        public IReadOnlyList<string> TransferDovrleri { get; } = new[]
        {
            "Hamısı", "Bu ay", "Keçən ay", "Bu il"
        };

        // ✓ Axtarış/dövr dəyişdikcə siyahı DƏRHAL yenilənir ✓
        partial void OnTransferAxtarisChanged(string value) => TransferYenile(SelectedCredit);

        partial void OnTransferDovrChanged(string value) => TransferYenile(SelectedCredit);

        /// <summary>
        /// 📅 Seçilmiş TRANSFER DÖVRÜNÜN tarix aralığı ✓✓✓
        /// (məs. «Bu ay» → bu ayın 1-dən sonuna qədər ✓ — yalnız HƏMİN AY
        /// transfer olunan maşınlar göstərilir ✓).
        /// </summary>
        private (DateTime? Bas, DateTime? Son) TransferDovrAraligi()
        {
            var bugun = DateTime.Today;
            var ayBasi = new DateTime(bugun.Year, bugun.Month, 1);

            return TransferDovr switch
            {
                "Bu ay" => (ayBasi, ayBasi.AddMonths(1).AddDays(-1)),
                "Keçən ay" => (ayBasi.AddMonths(-1), ayBasi.AddDays(-1)),
                "Bu il" => (new DateTime(bugun.Year, 1, 1), new DateTime(bugun.Year, 12, 31)),
                _ => (null, null)
            };
        }

        /// <summary>
        /// 📤 Transfer edilə bilən ŞƏXSLƏR (istifadəçi idarə edir).
        /// </summary>
        public ObservableCollection<string> TransferSexsleri { get; } = new();

        // ====================================================================
        //  💾 ŞƏXS SİYAHISININ DAİMİ SAXLANMASI (JSON fayl) ✓✓✓
        // --------------------------------------------------------------------
        //  ⚠ ƏVVƏLKİ XƏTA: şəxslər yalnız `CreditTransaction.Nov = "TransferŞəxs"`
        //  kimi bazada saxlanılırdı ✗ → kredit əməliyyatları silinəndə şəxslər
        //  də YOX OLURDU ✗✓✓
        //  İNDİ: şəxslər `%APPDATA%\Autocode\transfer-sexler.json` faylında
        //  saxlanılır ✓ — HEÇ VAXT silinmir ✓✓✓
        // ====================================================================

        /// <summary>Şəxs siyahısının fayl yolu: %APPDATA%\Autocode\transfer-sexler.json ✓</summary>
        private static string TransferSexsFayli => Path.Combine(
            Cas0201.Kok.Qovluq,
            "Autocode",
            "transfer-sexler.json");

        /// <summary>Fayldan şəxs siyahısını oxuyur ✓.</summary>
        private static List<string> TransferSexsOxu()
        {
            try
            {
                if (!File.Exists(TransferSexsFayli))
                {
                    return new List<string>();
                }

                var json = File.ReadAllText(TransferSexsFayli);
                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>Şəxs siyahısını fayla yazır ✓ (heç vaxt silinmir ✗).</summary>
        private static void TransferSexsYaz(IEnumerable<string> adlar)
        {
            try
            {
                var qovluq = Path.GetDirectoryName(TransferSexsFayli);
                if (!string.IsNullOrWhiteSpace(qovluq))
                {
                    Directory.CreateDirectory(qovluq);
                }

                var temiz = adlar
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a.Trim())
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                File.WriteAllText(
                    TransferSexsFayli,
                    JsonSerializer.Serialize(temiz, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch
            {
                // Fayl yazıla bilməsə də tətbiq işləməyə davam edir ✓
            }
        }

        /// <summary>Yeni şəxsi DAİMİ siyahıya əlavə edir ✓.</summary>
        private static void TransferSexsElave(string ad)
        {
            var adlar = TransferSexsOxu();

            if (!adlar.Contains(ad, StringComparer.CurrentCultureIgnoreCase))
            {
                adlar.Add(ad);
                TransferSexsYaz(adlar);
            }
        }

        /// <summary>Şəxsi DAİMİ siyahıdan silir ✓.</summary>
        private static void TransferSexsSil(string ad)
        {
            var adlar = TransferSexsOxu();
            adlar.RemoveAll(a => string.Equals(a, ad, StringComparison.CurrentCultureIgnoreCase));
            TransferSexsYaz(adlar);
        }

        /// <summary>Seçilmiş kreditin transfer qeydləri.</summary>
        public ObservableCollection<CreditTransaction> Transferler { get; } = new();

        /// <summary>«Transfer olunan» səhifəsi — şəxs üzrə yekun sətirlər ✓.</summary>
        public ObservableCollection<string> TransferOzetleri { get; } = new();

        /// <summary>Formada seçilmiş şəxs (məs. «Tural»).</summary>
        [ObservableProperty] private string? selectedTransferSexs;

        /// <summary>Yeni şəxs adı (➕ üçün).</summary>
        [ObservableProperty] private string yeniTransferSexs = string.Empty;

        /// <summary>Transfer behi (₼).</summary>
        [ObservableProperty] private decimal transferBeh;

        /// <summary>
        /// 📅 <b>TRANSFER TARİXİ</b> ✓✓✓ — transfer sənədinin tarixi ✓
        /// (uğur PDF-də «sənəd tarixi» kimi qeyd olunur ✓ — istifadəçi
        /// istənilən tarixi seçə bilər ✓, standart = bugün ✓).
        /// </summary>
        [ObservableProperty] private DateTime? transferTarixi = DateTime.Today;

        /// <summary>Seçilmiş kredit üzrə transfer cəmi (₼).</summary>
        [ObservableProperty] private decimal transferCemi;

        /// <summary>Bütün transferlərin ümumi behi (₼) — «Transfer olunan» səhifəsi.</summary>
        [ObservableProperty] private decimal transferUmumiBeh;

        /// <summary>Transfer varmı.</summary>
        public bool HasTransfer => Transferler.Count > 0;

        /// <summary>«2 transfer · 5 000,00 ₼ beh».</summary>
        public string TransferXulase => Transferler.Count == 0
            ? "Transfer yoxdur"
            : $"{Transferler.Count} transfer · {TransferCemi:N2} ₼ beh";

        /// <summary>Şəxs siyahısı boşdursa göstərilən izah.</summary>
        public bool TransferSexsYoxdur => TransferSexsleri.Count == 0;

        /// <summary>«Transfer olunan» səhifəsinin ümumi xülasəsi ✓.</summary>
        public string TransferUmumiXulase => TransferOzetleri.Count == 0
            ? "Hələ heç bir maşın transfer edilməyib"
            : $"{TransferOzetleri.Count} şəxs · {TransferUmumiBeh:N2} ₼ beh toplanıb";

        /// <summary>Transfer panelini + «Transfer olunan» yekunlarını yeniləyir ✓.</summary>
        private void TransferYenile(Credit? credit)
        {
            // ================================================================
            //  💰 «PUL» SAHƏSİ — AVTOMATİK FORMULA ✓✓✓
            // ----------------------------------------------------------------
            //  «📤 Transfer» bölməsindəki PUL sahəsi = <b>KREDİTLƏŞDİRİLƏN
            //  MƏBLƏĞ</b> (Məbləğ − İlkin ödəniş) ✓ — avtomatik yazılır ✓ və
            //  istifadəçi onu <b>ƏL İLƏ DƏYİŞƏ BİLƏR</b> ✓✓✓
            //
            //  ⚠ Yalnız SEÇİLƏN KREDİT dəyişdikdə yenilənir ✓ → manual
            //    düzəliş yükləmələr zamanı İTMİR ✗✓✓
            // ================================================================
            if (credit is not null && credit.Id != _sonTransferKreditId)
            {
                _sonTransferKreditId = credit.Id;
                TransferBeh = credit.Kreditlesdirilen;
            }

            Transferler.Clear();
            TransferOzetleri.Clear();

            // ================================================================
            //  👤 ŞƏXS SİYAHISI — **BÜTÜN KREDİTLƏR ÜZRƏ QLOBAL** ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏLKİ XƏTA: şəxslər yalnız SEÇİLMİŞ kreditin əməliyyatlarından
            //  yığılırdı ✗ (`t.CreditId == credit.Id`) → başqa kreditdə yaradılan
            //  «Tural» görünmürdü ✗✓✓
            //  İNDİ: bütün kreditlərin bütün transfer qeydləri nəzərə alınır ✓
            // ================================================================
            var adlar = _creditTransactions
                .Where(t => t.Nov == "TransferŞəxs"      // ➕ İNSAN ƏLAVƏ ET markeri ✓
                            || t.Nov == "Transfer"        // ✓ transfer qeydi
                            || t.Nov == "Transfer olunmaq") // ✓ kredit əlavə gəlir növü
                .Where(t => !string.IsNullOrWhiteSpace(t.Tesvir))
                // «Tural · Toyota Camry» → «Tural» ✓
                .Select(t => t.Tesvir.Split('·')[0].Trim())
                .Where(a => a.Length > 0)
                .Distinct()
                .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            // 💾 DAİMİ fayldan gələn şəxslər (heç vaxt silinmir ✓) + baza qeydləri ✓
            foreach (var ad in TransferSexsOxu())
            {
                if (!string.IsNullOrWhiteSpace(ad)
                    && !adlar.Contains(ad, StringComparer.CurrentCultureIgnoreCase))
                {
                    adlar.Add(ad);
                }
            }

            // Seçilmiş kreditin transfer qeydləri (cədvəl üçün) ✓
            if (credit is not null)
            {
                foreach (var tx in _creditTransactions
                             .Where(t => t.CreditId == credit.Id
                                         // «Transfer» (Transfer tab-ı) + «Transfer olunmaq»
                                         // (Kredit Əlavə Gəlir/Xərc tab-ı) ✓✓✓
                                         && (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq"))
                             .OrderByDescending(t => t.Tarix)
                             .ThenByDescending(t => t.Id))
                {
                    Transferler.Add(tx);
                }
            }

            // Şəxs siyahısı: əl ilə əlavə edilənlər + transferlərdən gələnlər ✓
            TransferSexsleri.Clear();
            foreach (var ad in adlar.OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase))
            {
                TransferSexsleri.Add(ad);
            }

            TransferCemi = Transferler.Sum(t => t.Mebleg);

            // ================================================================
            //  📤 «TRANSFER OLUNAN» SƏHİFƏSİ — BÜTÜN KREDİTLƏR ÜZRƏ ✓
            //  «Nə maşınlar verilib · bizə nə qədər verib» ✓
            // ================================================================
            // ================================================================
            //  🔎 AXTARIŞ + 📅 DÖVR FİLTRİ ✓✓✓
            // ----------------------------------------------------------------
            //  • AXTARIŞ: maşının adı / dövlət nömrəsi (məs. «99QE103») /
            //    şəxs adı yazıldıqca DİGƏRLƏR ELİMİNƏ OLUNUR ✓ — yalnız
            //    uyğun nəticələr qalır ✓✓✓
            //  • DÖVR: «Bu ay» seçilsə yalnız BU AY transfer olunan
            //    maşınlar göstərilir ✓
            // ================================================================
            var axtaris = TransferAxtaris?.Trim() ?? string.Empty;
            var (dovrBas, dovrSon) = TransferDovrAraligi();

            var butunTransferler = _creditTransactions
                .Where(t => (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq")
                            && !string.IsNullOrWhiteSpace(t.Tesvir))
                .Where(t => dovrBas is null
                            || (t.Tarix.Date >= dovrBas.Value.Date
                                && t.Tarix.Date <= dovrSon!.Value.Date))
                .Where(t =>
                {
                    if (axtaris.Length == 0)
                    {
                        return true;
                    }

                    // 🚗 maşının tam adı + nömrəsi ✓ (məs. «Hyundai Sonata (99QE103)»)
                    var masin = _butunKreditler
                        .FirstOrDefault(c => c.Id == t.CreditId)?.Car?.DisplayName ?? string.Empty;

                    return (t.Tesvir ?? string.Empty).Contains(axtaris, StringComparison.OrdinalIgnoreCase)
                           || masin.Contains(axtaris, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            TransferUmumiBeh = butunTransferler.Sum(t => t.Mebleg);

            // ================================================================
            //  ✅ HƏR TRANSFER AYRI SƏTİRDƏ ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL: eyni şəxsin bütün transferləri BİR kartda yığılırdı ✗
            //  (məs. Turala 3 maşın → 1 kart ✗). İndi hər maşın AYRI sətirdir ✓:
            //
            //    #12 👤 Tural — 🚗 Hyundai Sonata (99QE103) · 20.09.2026 · PUL 23 012,50 ₼
            //    #13 👤 Tural — 🚗 Kia Rio (10BC456)        · 21.09.2026 · PUL 15 000,00 ₼
            //    #14 👤 Tural — 🚗 Nissan (77XY999)         · 22.09.2026 · PUL  8 000,00 ₼
            //
            //  ✨ Sətir başındaki «#Id» → 📄 PDF və 🗑️ SİL düymələri MƏHZ
            //     həmin transferi hədəfləyir ✓✓✓ (digərlərinə toxunmur ✓)
            // ================================================================
            foreach (var t in butunTransferler
                         .OrderByDescending(t => t.Tarix)
                         .ThenByDescending(t => t.Id))
            {
                var kredit = _butunKreditler.FirstOrDefault(c => c.Id == t.CreditId);
                var masin = kredit?.Car?.DisplayName ?? "—";
                var ad = (t.Tesvir ?? string.Empty).Split('·')[0].Trim();

                var qaliq = Math.Max(0m, (kredit?.KreditQiymeti ?? 0m) - t.Mebleg);

                TransferOzetleri.Add(
                    $"#{t.Id} 👤 {ad}  —  🚗 {masin}  ·  📅 {t.Tarix:dd.MM.yyyy}  ·  " +
                    $"💰 PUL {t.Mebleg:N2} ₼  ·  qalıq {qaliq:N2} ₼");
            }

            // ================================================================
            //  🩺 SELF-HEAL — TRANSFER OLUNMUŞ KREDİT «💳 KREDİTLƏR»-DƏN ÇIXARILIR ✓
            // ----------------------------------------------------------------
            //  Baza statusu nə olursa olsun: transfer qeydi varsa kredit
            //  «Bağlı» sayılır ✓ və siyahıdan çıxarılır ✓✓✓
            // ================================================================
            foreach (var tx in _creditTransactions.Where(t => t.Nov == "Transfer"
                                                              || t.Nov == "Transfer olunmaq"))
            {
                if (tx.CreditId is not int cid)
                {
                    continue;
                }

                var hedef = Credits.FirstOrDefault(c => c.Id == cid);
                if (hedef is null)
                {
                    continue;
                }

                hedef.Status = "Bağlı";

                if (hedef.Id != SelectedCredit?.Id)
                {
                    // ✅ BİRBAŞA çıxarılır — cədvəl YENİDƏN QURULMUR ✓✓✓
                    //  ⚠ ƏVVƏLKİ XƏTA: `ApplyContractFilter()` BÜTÜN cədvəli
                    //  TƏMİZLƏYİRDİ ✗ → istifadəçinin SEÇİMİ İTİRDİ ✗.
                    //  Üstəlik bu metod `OnSelectedCreditChanged`-dən çağırıldığı
                    //  üçün «seçim → təmizləmə → seçim itir» DÖVRÜ yaranırdı ✗✓✓
                    Credits.Remove(hedef);
                    FilteredContracts.Remove(hedef);
                }
            }

            OnPropertyChanged(nameof(HasTransfer));
            OnPropertyChanged(nameof(TransferXulase));
            OnPropertyChanged(nameof(TransferSexsYoxdur));
            OnPropertyChanged(nameof(TransferUmumiXulase));
        }

        /// <summary>➕ Yeni şəxs əlavə edir ✓ (Tural, …).</summary>
        [RelayCommand]
        private async Task TransferSexsElaveEtAsync()
        {
            var ad = YeniTransferSexs?.Trim();

            if (string.IsNullOrWhiteSpace(ad))
            {
                _dialogs.ShowWarning("Əlavə etmək üçün şəxsin ADINI yazın (məs. «Tural»).");
                return;
            }

            if (SelectedCredit is null)
            {
                _dialogs.ShowWarning("Şəxs əlavə etmək üçün əvvəlcə kredit seçin.");
                return;
            }

            if (TransferSexsleri.Contains(ad))
            {
                SelectedTransferSexs = ad;
                _dialogs.ShowWarning($"«{ad}» artıq siyahıdadır.");
                return;
            }

            try
            {
                IsBusy = true;

                await _creditService.AddTransactionAsync(new CreditTransaction
                {
                    CreditId = SelectedCredit.Id,
                    Nov = "TransferŞəxs",
                    Mebleg = 0m,
                    Tarix = DateTime.Today,
                    Tesvir = ad
                });

                // 💾 DAİMİ fayla yazılır — HEÇ VAXT SİLİNMİR ✓✓✓
                TransferSexsElave(ad);

                YeniTransferSexs = string.Empty;
                await LoadAsync();
                SelectedTransferSexs = ad;

                _logger.LogInformation("📤 Transfer şəxsi əlavə edildi: {Ad}", ad);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer şəxsi əlavə edilərkən xəta.");
                _dialogs.ShowError("Şəxs əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>🗑️ Şəxsi və onun bütün transferlərini silir ✓.</summary>
        [RelayCommand]
        private async Task TransferSexsSilAsync(string? ad)
        {
            ad = ad?.Trim();

            if (string.IsNullOrWhiteSpace(ad))
            {
                _dialogs.ShowWarning("Silmək üçün şəxs seçin.");
                return;
            }

            if (!_dialogs.Confirm(
                    $"«{ad}» şəxsi silinsin?\n\nOnun BÜTÜN transfer qeydləri də silinəcək.",
                    "Şəxsin silinməsi"))
            {
                return;
            }

            try
            {
                IsBusy = true;

                foreach (var tx in _creditTransactions
                             .Where(t => (t.Nov == "Transfer" || t.Nov == "TransferŞəxs")
                                         && t.Tesvir == ad)
                             .ToList())
                {
                    await _creditService.DeleteTransactionAsync(tx.Id);
                }

                await LoadAsync();

                // 💾 DAİMİ fayldan da silinir ✓
                TransferSexsSil(ad);

                _logger.LogInformation("🗑️ Transfer şəxsi silindi: {Ad}", ad);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer şəxsi silinərkən xəta.");
                _dialogs.ShowError("Şəxs silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// <b>📤 TRANSFER ET</b> ✓ — seçilmiş maşın seçilmiş ŞƏXSƏ (məs. Tural)
        /// verilir ✓, beh məbləği ilə ✓.
        /// </summary>
        [RelayCommand]
        private async Task TransferEtAsync()
        {
            if (SelectedCredit is null)
            {
                _dialogs.ShowWarning("Transfer etmək üçün əvvəlcə kredit seçin.");
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedTransferSexs))
            {
                _dialogs.ShowWarning("Transfer etmək üçün ŞƏXS seçin (məs. «Tural»).");
                return;
            }

            try
            {
                IsBusy = true;

                var ad = SelectedTransferSexs.Trim();
                var masin = SelectedCredit.Car?.DisplayName;

                await _creditService.AddTransactionAsync(new CreditTransaction
                {
                    CreditId = SelectedCredit.Id,
                    Nov = "Transfer",
                    Mebleg = TransferBeh,

                    // ✅ İSTİFADƏÇİNİN SEÇDİYİ TARİX ✓✓✓ (sənəd tarixi PDF-də ✓)
                    Tarix = TransferTarixi ?? DateTime.Today,
                    Tesvir = ad,
                    Odenilib = TransferBeh > 0m
                });

                _logger.LogInformation(
                    "📤 Transfer edildi: kredit {Id} · {Masin} → {Ad} · beh {Beh:N2} ₼",
                    SelectedCredit.Id, masin, ad, TransferBeh);

                TransferBeh = 0m;
                await LoadAsync();

                // ================================================================
                //  📢 BÜTÜN BÖLMƏLƏR DƏRHAL YENİLƏNİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ XƏTA: Avto Park yalnız tab dəyişəndə yenilənirdi ✗
                //  → transfer olunmuş maşın parkda QALIRDI ✗✓✓
                //  İNDİ: `CreditsChanged` hadisəsi işə düşür ✓ →
                //  `MainViewModel` `CarPark.LoadAsync()` çağırır ✓ →
                //  maşın parkdan <b>DƏRHAL</b> çıxır ✓ və sayı azalır ✓✓✓
                // ================================================================
                CreditsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer edilərkən xəta.");
                _dialogs.ShowError("Transfer edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>🗑️ Transfer qeydini silir ✓.</summary>
        [RelayCommand]
        private async Task TransferSilAsync(CreditTransaction? transfer)
        {
            if (transfer is null)
            {
                _dialogs.ShowWarning("Silmək üçün transfer sətri seçin.");
                return;
            }

            if (!_dialogs.Confirm($"Transfer silinsin?\n\n{transfer.Tesvir}", "Transfer silinməsi"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _creditService.DeleteTransactionAsync(transfer.Id);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer silinərkən xəta.");
                _dialogs.ShowError("Transfer silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// «👤 Tural · Toyota Camry  —  2 transfer …» → <b>«Tural»</b> ✓
        /// <para>
        /// ⚠ VACİB: «Transfer Olunan» kartlarının başlığı TAM TƏSVİRİ göstərir
        /// («Tural · Toyota Camry») ✗, axtarış isə ŞƏXS ADI ilə aparılmalıdır ✓.
        /// Əvvəl tam mətnlə müqayisə edilirdi ✗ → heç bir kredit tapılmırdı ✗✓✓
        /// (nəticədə 🗑️ silmə «heç nə etmirdi» ✗ və 📄 PDF «kredit tapılmadı» ✗).
        /// </para>
        /// </summary>
        private static string TransferSexsAdiAl(string? metn) =>
            (metn ?? string.Empty)
                .Replace("👤", string.Empty)
                .Split('·')[0]
                .Split('—')[0]
                .Trim();

        /// <summary>
        /// 📄 <b>«Transfer Olunan» kartından PDF İXRAC</b> ✓✓✓
        /// <para>
        /// Şəxsə transfer edilmiş <b>HƏR maşın</b> üçün TAM kredit sənədi
        /// hazırlanır ✓: müqavilə · müştəri · avtomobil · maliyyə göstəriciləri ·
        /// <b>ödəniş cədvəli</b> · barter/transfer qeydləri · tərəfdaş bölgüsü ✓.
        /// </para>
        /// <para>
        /// Brauzerdə açılır → <b>Ctrl+P → «Microsoft Print to PDF»</b> ✓
        /// (bütün Azərbaycan hərfləri ilə ✓).
        /// </para>
        /// </summary>
        [RelayCommand]
        private async Task TransferOzetPdfAsync(string? ozet)
        {
            if (string.IsNullOrWhiteSpace(ozet))
            {
                return;
            }

            // «#12 👤 Tural — 🚗 Hyundai Sonata …» → 12 ✓
            var transferId = TransferIdAl(ozet);

            // ✅ ƏVVƏLCƏ MƏHZ HƏMİN TRANSFERİN krediti ✓✓✓
            var kreditler = transferId > 0
                ? _butunKreditler
                    .Where(c => c.Id == _creditTransactions
                        .FirstOrDefault(t => t.Id == transferId)?.CreditId)
                    .ToList()
                : new List<Credit>();

            if (kreditler.Count > 0)
            {
                // ✓ tapıldı — aşağıdaki ehtiyat axtarışlara ehtiyac yoxdur ✓
            }

            // «👤 Tural · Toyota Camry  —  2 transfer · beh …» → «Tural» ✓
            var ad = TransferSexsAdiAl(ozet);

            if (string.IsNullOrWhiteSpace(ad) && kreditler.Count == 0)
            {
                return;
            }

            try
            {
                IsBusy = true;

                var idler = _creditTransactions
                    .Where(t => (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq")
                                && t.CreditId.HasValue
                                && string.Equals(
                                    TransferSexsAdiAl(t.Tesvir),
                                    ad,
                                    StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.CreditId!.Value)
                    .Distinct()
                    .ToList();

                if (kreditler.Count == 0)
                {
                    kreditler = _butunKreditler.Where(c => idler.Contains(c.Id)).ToList();
                }

                // ================================================================
                //  🩺 EHTİYAT AXATARIŞ ✓ — transfer qeydi tapılmasa,
                //  kreditin QEYDİNƏ görə axtarılır ✓✓✓
                //  («📤 Transfer olunmuş → Tural · 25.09.2026» ✓)
                // ================================================================
                if (kreditler.Count == 0)
                {
                    kreditler = _butunKreditler
                        .Where(c => (c.Qeyd ?? string.Empty)
                            .Contains(ad, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                if (kreditler.Count == 0)
                {
                    _dialogs.ShowWarning(
                        $"«{ad}» üçün transfer edilmiş kredit tapılmadı.\n\n" +
                        "«📤 Transfer» tab-ından yeni transfer edin ✓");
                    return;
                }

                // ================================================================
                //  💾 HARA YAZILACAQ — İSTİFADƏÇİ ÖZÜ SEÇİR ✓✓✓
                //  (native «Save As» pəncərəsi = File Explorer ✓)
                // ================================================================
                var teklif = kreditler.Count == 1
                    ? CreditPdfBuilder.FaylAdi(kreditler[0].Mustəri, kreditler[0].MuqavileNomresi)
                    : $"Transfer_{ad.Replace(' ', '_')}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";

                var secilen = _dialogs.ShowSaveFileDialog("PDF sənədi|*.pdf", teklif);

                if (string.IsNullOrWhiteSpace(secilen))
                {
                    return;   // ✓ istifadəçi ləğv etdi
                }

                // 👥 Kredit üzrə tərəfdaş payları ✓
                var paylar = await _creditService.GetCreditSharesMapAsync();

                var fayllar = new List<string>();

                for (var i = 0; i < kreditler.Count; i++)
                {
                    var kredit = kreditler[i];

                    // 📅 ÖDƏNİŞ CƏDVƏLİ — `SelectedCredit`-ə TOXUNMADAN ✓✓✓
                    //  (əvvəl seçim dəyişdirilirdi ✗ → cədvəl BOŞ qalırdı ✗✓✓)
                    var cedvel = ScheduleRowsFor(kredit);

                    var oKreditTransferleri = _creditTransactions
                        .Where(t => t.CreditId == kredit.Id
                                    && (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq"))
                        .ToList();

                    var oKreditBarterleri = _creditTransactions
                        .Where(t => t.CreditId == kredit.Id
                                    && (t.Nov == "Barter" || t.Nov == "Barter köhnə"))
                        .ToList();

                    IReadOnlyList<PartnerShare> kreditPaylari =
                        paylar.TryGetValue(kredit.Id, out var p) ? p : new List<PartnerShare>();

                    var html = CreditPdfBuilder.Build(
                        kredit,
                        cedvel,
                        oKreditBarterleri,
                        oKreditTransferleri,
                        kreditPaylari,
                        BarterCemiHesabla(kredit),
                        TransferCemiHesabla(kredit),
                        // ✅ ŞƏXSƏ VERİLƏN SƏNƏD ✓ — salonun daxili
                        //    göstəriciləri (mənfəət bölgüsü · transferlər ·
                        //    kreditin tam qiyməti) GİZLƏDİLİR ✗✓✓
                        musteriRejimi: true,

                        // 📅 SƏNƏD TARİXİ = TRANSFER TARİXİ ✓✓✓
                        //   (əvvəl həmişə BUGÜN yazılırdı ✗✓✓)
                        senedTarixi: oKreditTransferleri.Count > 0
                            ? oKreditTransferleri.Max(t => t.Tarix)
                            : (DateTime?)null);

                    // 📄 ƏSL PDF ✓ — çox maşın varsa adlara «_2», «_3» əlavə olunur ✓
                    var yol = kreditler.Count == 1
                        ? secilen
                        : Path.Combine(
                            Path.GetDirectoryName(secilen) ?? string.Empty,
                            $"{Path.GetFileNameWithoutExtension(secilen)}_{i + 1}.pdf");

                    // 🖨️ LANDSHAFT → ödəniş cədvəli TAM görünür ✓✓✓
                    await HtmlPdfWriter.RenderAsync(html, yol, landscape: true, scale: 0.8);
                    fayllar.Add(yol);
                }

                KlasoruAc(fayllar[0]);

                _dialogs.ShowInfo(
                    $"✅ PDF hazırdır — {fayllar.Count} sənəd:\n\n" + string.Join("\n", fayllar),
                    "📄 PDF");

                _logger.LogInformation(
                    "📄 Transfer PDF ixracı: {Ad} · {Say} sənəd ✓", ad, fayllar.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer PDF ixracı zamanı xəta.");
                _dialogs.ShowError("PDF hazırlana bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// «#12 👤 Tural — 🚗 …» → <b>12</b> ✓✓✓
        /// (kartın başındaki transfer <b>Id</b>-si — 🗑️ sil və 📄 PDF MƏHZ həmin
        /// transferi hədəfləyir ✓, digərlərinə toxunmur ✓).
        /// </summary>
        private static int TransferIdAl(string? ozet)
        {
            var metn = (ozet ?? string.Empty).TrimStart();

            if (metn.Length < 2 || metn[0] != '#')
            {
                return 0;
            }

            var son = metn.IndexOf(' ');
            var reqem = son > 1 ? metn[1..son] : metn[1..];

            return int.TryParse(reqem, out var id) ? id : 0;
        }

        /// <summary>
        /// 🗑️ <b>«Transfer» kartından SİLMƏ</b> ✓ — MƏHZ həmin transferi silir ✓,
        /// kredit + maşın GERİ qaytarılır ✓✓✓
        /// </summary>
        [RelayCommand]
        private async Task TransferOzetSilAsync(string? ozet)
        {
            var id = TransferIdAl(ozet);

            if (id <= 0)
            {
                _dialogs.ShowWarning("Silmək üçün transfer sətri seçin.");
                return;
            }

            var transfer = _creditTransactions.FirstOrDefault(t => t.Id == id);

            if (transfer is null)
            {
                _dialogs.ShowWarning("Transfer qeydi tapılmadı.");
                return;
            }

            var ad = TransferSexsAdiAl(transfer.Tesvir);

            if (!_dialogs.Confirm(
                    $"Bu transfer silinsin?\n\n👤 {ad}\n🚗 {ozet}",
                    "Transfer silinməsi"))
            {
                return;
            }

            try
            {
                IsBusy = true;

                await _creditService.DeleteTransactionAsync(transfer.Id);
                await LoadAsync();
                CreditsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transfer silinərkən xəta.");
                _dialogs.ShowError("Silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// <b>📄 PDF — REAL PDF SƏNƏDİ</b> ✓✓✓
        /// <list type="number">
        ///   <item>💾 <b>«Save As» pəncərəsi</b> açılır → hara yazılacağını
        ///         İSTİFADƏÇİ ÖZÜ seçir ✓ (File Explorer ✓)</item>
        ///   <item>📄 <b>ƏSL .pdf faylı</b> yaradılır ✓ (HTML deyil ✗✓✓)</item>
        ///   <item>📂 Faylın olduğu qovluq Explorer-də açılır ✓</item>
        /// </list>
        /// </summary>
        [RelayCommand]
        private async Task PdfCixartAsync()
        {
            if (SelectedCredit is null)
            {
                _dialogs.ShowWarning("PDF üçün əvvəlcə kredit seçin.");
                return;
            }

            try
            {
                IsBusy = true;

                // 💾 HARA YAZILACAQ — İSTİFADƏÇİ ÖZÜ SEÇİR ✓✓✓
                var teklif = CreditPdfBuilder.FaylAdi(
                    SelectedCredit.Mustəri, SelectedCredit.MuqavileNomresi);

                var yol = _dialogs.ShowSaveFileDialog("PDF sənədi|*.pdf", teklif);

                if (string.IsNullOrWhiteSpace(yol))
                {
                    return;   // ✓ istifadəçi ləğv etdi
                }

                var html = CreditPdfBuilder.Build(
                    SelectedCredit,
                    Schedule.ToList(),
                    Barterler.ToList(),
                    Transferler.ToList(),
                    SelectedCreditShares.ToList(),
                    BarterCemi,
                    TransferCemi);

                // 📄 ƏSL PDF FAYLI (HTML deyil ✗) ✓✓✓
                //  🖨️ LANDSHAFT → ödəniş cədvəli TAM görünür ✓✓✓
                await HtmlPdfWriter.RenderAsync(html, yol, landscape: true, scale: 0.8);

                KlasoruAc(yol);

                _dialogs.ShowInfo($"✅ PDF hazırdır!\n\nFayl: {yol}", "📄 PDF");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF yaradılarkən xəta.");
                _dialogs.ShowError("PDF yaradıla bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 📂 Faylın olduğu qovluğu <b>File Explorer</b>-də açır ✓
        /// (fayl seçilmiş vəziyyətdə ✓).
        /// </summary>
        private static void KlasoruAc(string yol)
        {
            try
            {
                if (File.Exists(yol))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{yol}\"")
                    {
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
                // Explorer açıla bilmədi — kritik deyil ✓
            }
        }

        /// <summary>
        /// Verilmiş kredit üçün <b>ÖDƏNİŞ CƏDVƏLİNİ</b> qurur ✓✓✓
        /// <para>
        /// ⚠ VACİB: <c>SelectedCredit</c>-ə <b>TOXUNULMUR</b> ✓ — əks halda UI
        /// seçimi sıfırlanır ✗ və cədvəl BOŞ qalır ✗✓✓ (PDF-də cədvəl
        /// görünməməsinin səbəbi məhz bu idi ✗).
        /// </para>
        /// </summary>
        private List<PaymentRow> ScheduleRowsFor(Credit credit)
        {
            var kohneSecim = SelectedCredit;

            BuildSchedule(credit);                     // ✓ Schedule = bu kreditin cədvəli
            var netice = Schedule.ToList();

            BuildSchedule(kohneSecim);                 // ✓ əvvəlki vəziyyət bərpa olunur

            return netice;
        }

        /// <summary>
        /// İki tarix arasındakı TAKSİT NÖMRƏSİ.
        /// <para>
        /// ⚠ 1-ci taksit = <b>başlama tarixinin özü</b> ✓ → nəticəyə <c>+1</c>
        /// əlavə olunur (əvvəl 0 qaytarırdı ✗ və taksit nömrəsi sürüşürdü ✗)
        /// </para>
        /// </summary>
        private static int MonthOffset(DateTime date, DateTime start)
            => (((date.Year - start.Year) * 12) + (date.Month - start.Month)) + 1;

        /// <summary>
        /// Kreditə aid "Möhlət" (ödəniş gecikdirməsi) qeydlərini taksit nömrəsinə görə yığır.
        /// Bir taksit üçün BÜTÜN möhlət qeydləri saxlanılır.
        /// </summary>
        private Dictionary<int, List<CreditTransaction>> CollectDeferrals(Credit credit)
        {
            var result = new Dictionary<int, List<CreditTransaction>>();

            foreach (var transaction in _creditTransactions
                         .Where(t => t.CreditId == credit.Id && t.Nov == "Möhlət")
                         .OrderBy(t => t.Tarix)
                         .ThenBy(t => t.Id))
            {
                // ✅ Möhlət qeydi müddətdən KƏNARDA olsa da ATILMIR ✗ — ən
                //    yaxın taksitə BAĞLANIR ✓ (əvvəl `continue` ilə səssizcə
                //    itirdi ✗ və heç bir yerdə görünmürdü ✗✓)
                var hampNo = transaction.InstallmentNo ?? MonthOffset(transaction.Tarix, credit.BaslamaTarixi);
                var no = Math.Clamp(hampNo, 1, credit.MuddetAy);

                if (!result.TryGetValue(no, out var list))
                {
                    list = new List<CreditTransaction>();
                    result[no] = list;
                }

                list.Add(transaction);
            }

            return result;
        }

        /// <summary>
        /// Kreditə aid "Gecikmə" qeydlərini taksit nömrəsinə görə yığır.
        /// Bir taksit üçün BÜTÜN gecikmə qeydləri saxlanılır.
        /// </summary>
        private Dictionary<int, List<CreditTransaction>> CollectGecikmeler(Credit credit)
        {
            var result = new Dictionary<int, List<CreditTransaction>>();

            foreach (var transaction in _creditTransactions
                         .Where(t => t.CreditId == credit.Id && t.Nov == "Gecikmə")
                         .OrderBy(t => t.Tarix)
                         .ThenBy(t => t.Id))
            {
                // ============================================================
                //  ⚠ ƏVVƏLKİ XƏTA ✗ → ✅ HƏLL
                // ------------------------------------------------------------
                //  Gecikmə qeydi müddətdən KƏNARDA idisə (`no < 1 || no > MuddetAy`)
                //  `continue` ilə SƏSSİZCƏ ATILIRDI ✗ → «Kredit Əlavə Gəlir/Xərc»
                //  bölməsində gecikmə yazsanız da (məs. 14.09.2026) ÖDƏNİŞ
                //  QRAFİKİNDƏ HEÇ VAXT GÖRÜNMÜRDÜ ✗✓✓
                //
                //  İNDİ: qeyd **itmir** ✓ — ən yaxın (son) taksitə BAĞLANIR ✓
                //  və qrafikdə ⚠ GECİKMƏ sütununda tam detal ilə görünür ✓.
                // ============================================================
                var hamNo = transaction.InstallmentNo ?? MonthOffset(transaction.Tarix, credit.BaslamaTarixi);
                var no = Math.Clamp(hamNo, 1, credit.MuddetAy);

                if (!result.TryGetValue(no, out var list))
                {
                    list = new List<CreditTransaction>();
                    result[no] = list;
                }

                list.Add(transaction);
            }

            return result;
        }

        /// <summary>Sətrə faktiki ödənişlərin CƏMİNİ, sayını və detallarını yazır.</summary>
        private void ApplyPaid(PaymentRow row, List<CreditTransaction> payments)
        {
            var ordered = payments.OrderBy(t => t.Tarix).ThenBy(t => t.Id).ToList();

            _suppressPaymentEvents = true;
            row.Odenilib = true;
            row.TransactionIds = ordered.Select(t => t.Id).ToList();
            row.OdenilenMebleg = ordered.Sum(t => t.Mebleg);
            row.OdenilmeTarixi = ordered[^1].Tarix;
            row.OdenisSayi = ordered.Count;
            row.OdenisTarixleri = string.Join(", ", ordered.Select(t => t.Tarix.ToString("dd.MM.yyyy")));
            row.OdenisDetali = string.Join(
                Environment.NewLine,
                ordered.Select(t => $"{t.Tarix:dd.MM.yyyy} — {t.Mebleg:N2} AZN"));
            _suppressPaymentEvents = false;
        }

        /// <summary>
        /// Ödəniş işarəsi dəyişdikdə kredit əlavə gəlir qeydini yaradır və ya silir.
        /// Beləliklə həmin gəlir "Kredit Əlavə Gəlir/Xərc" cədvəlində və Maliyyə Paneli-də görünür.
        /// </summary>
        private async void OnPaymentRowChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressPaymentEvents || e.PropertyName != nameof(PaymentRow.Odenilib))
            {
                return;
            }

            if (sender is not PaymentRow row || SelectedCredit is null)
            {
                return;
            }

            var credit = SelectedCredit;
            try
            {
                // ================================================================
                //  🛑 ★★ v6.2.23 — AVTOMATİK ÖDƏNİŞ YARATMA/SİLMƏ SÖNDÜRÜLDÜ ★★
                // ----------------------------------------------------------------
                //  ⚠ İSTİFADƏÇİ ŞİKAYƏTİ (ciddi bug ✗✓✓):
                //    «Mən maşının BÜTÜN kredit ödənişlərini silirəm ✗ → avtomatik
                //     BİRİNCİ AYA (03.10.2026) yenidən kredit ödənişi əlavə olunur ✗
                //     və hər vaxt keçdikcə əlavə olunurlar ✗. Belə buglara
                //     QƏTİYYƏN yol vermək olmaz ✗ — SİL bunları ✗.»
                // ----------------------------------------------------------------
                //  ⚠ ƏSL SƏBƏB: cədvəldəki «✅ ÖDƏNİLİB?» işarəsi dəyişəndə
                //    proqram ÖZÜ bazaya «Gəlir» qeydi YARADIRDI ✗ (və ya SİLİRDİ ✗)
                //    → istifadəçi ödənişləri sildikcə onlar GİZLİCƏ geri gəlirdi ✗✓✓
                //  ✅ İNDİ: cədvəl YALNIZ GÖSTƏRİR ✗ — heç bir gizli yazma/silmə YOX ✗✓✓
                //    📌 Ödəniş YALNIZ «Kredit əlavə gəlir/xərc» formasından
                //       əlavə olunur ✓ (istifadəçi ÖZÜ yazır ✓✓✓)
                // ================================================================
                if (e.PropertyName == nameof(PaymentRow.Odenilib))
                {
                    _logger.LogInformation(
                        "ℹ️ Cədvəldə ödəniş işarəsi dəyişdi — AVTOMATİK yazma/silmə YOXDUR ✗ " +
                        "(ödəniş «Kredit əlavə gəlir/xərc» formasından edilir ✓)");
                }

                // Cədvəl yenidən hesablanır: artıq/az ödəniş sonrakı ayların ödənişini dəyişir.
                BuildSchedule(credit);
                UpdateSelectedSummary();
                CreditsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit ödənişi qeyd edilərkən xəta baş verdi.");
                _dialogs.ShowError("Ödəniş qeydə alına bilmədi: " + ex.Message);
                BuildSchedule(credit);
                UpdateSelectedSummary();
            }
        }

        /// <summary>Növbəti ardıcıl müqavilə nömrəsini yaradır (məs. M-0001).</summary>
        private async Task<string> GenerateContractNumberAsync()
        {
            var existing = await _creditService.GetCreditsAsync();
            return "M-" + ComputeNextContractNumber(existing).ToString("D4");
        }

        /// <summary>Mövcud kreditlərdəki ən böyük nömrədən sonrakını qaytarır.</summary>
        private static int ComputeNextContractNumber(IEnumerable<Credit> credits)
        {
            var max = 0;
            foreach (var credit in credits)
            {
                var digits = new string((credit.MuqavileNomresi ?? string.Empty).Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var value) && value > max)
                {
                    max = value;
                }
            }

            return max + 1;
        }

        private static decimal CalculateMonthly(decimal principal, decimal annualRate, int months)
        {
            // ================================================================
            //  AYLIK ÖDƏNİŞ = Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət
            // ----------------------------------------------------------------
            //  Faiz əsas borcun ÜSTÜNƏ BİR DƏFƏ əlavə olunur, sonra bütün
            //  müddətə bərabər bölünür. Vahid düstur: Services/CreditMath.cs
            //
            //  17 014 ₼ · 40% · 12 ay  →  23 819,60 ÷ 12  =  1 984,97 ₼
            // ================================================================
            return CreditMath.MonthlyPayment(principal, annualRate, months);
        }

        // ====================================================================
        //  🔄 SATICI MƏLUMATI  →  MAŞININ QİYMƏTİ AVTOMATİK
        // --------------------------------------------------------------------
        //  Satıcı deyir: «12 ay, aylıq 1 985 ₼, ilkin 5 000 ₼».
        //  Bu üç rəqəm yazıldıqda SATIŞ QİYMƏTİ avtomatik hesablanır:
        //
        //      Satış qiyməti = (Aylıq × Ay) ÷ Əmsal + İlkin ödəniş
        //
        //  Nümunə: (12 × 1 985) ÷ 1.4 + 5 000 = 17 014,29 + 5 000 = 22 014 ₼
        //
        //  ƏMSAL müddətə görə AVTOMATİK gəlir, lakin əl ilə dəyişilə bilər.
        // ====================================================================

        /// <summary>Satıcının dediyi AYLIQ ÖDƏNİŞ (₼) — bu xananı doldurun.</summary>
        [ObservableProperty] private decimal satisAylıq;

        /// <summary>«Qrafik bölgüsü» ƏMSALI — müddətə görə avtomatik, əl ilə dəyişilir.</summary>
        [ObservableProperty] private decimal satisEmsal = 1.4m;

        /// <summary>Əmsala uyğun faiz: 1,4 → 40%.</summary>
        public string SatisEmsalFaiz => $"≈ {CreditMath.FactorToPercent(SatisEmsal):0.##}% faiz";

        /// <summary>Əmsal cədvəlinin qısa mətni (izah üçün).</summary>
        public string SatisEmsalCedveli => string.Join(" · ",
            Catalog.CreditFactors.Select(f => $"{f.Ay}→{f.Emsal:0.0}"));

        /// <summary>
        /// SATIŞ QİYMƏTİNİ avtomatik hesablayır və formaya yazır:
        /// <c>(Aylıq × Ay) ÷ Əmsal + İlkin ödəniş</c>.
        /// </summary>
        [RelayCommand]
        private void SatisQiymetiHesabla()
        {
            if (MuddetAy <= 0 || SatisAylıq <= 0m)
            {
                return;
            }

            if (SatisEmsal <= 0m)
            {
                SatisEmsal = Catalog.CreditFactorFor(MuddetAy);
            }

            // MAŞININ QİYMƏTİ → forma avtomatik doldurulur
            Mebleg = CreditMath.CarPriceFromMonthly(SatisAylıq, MuddetAy, SatisEmsal, IlkinOdenis);

            // Faiz əmsaldan çıxarılır → aylıq ödəniş satıcının dediyi kimi olur.
            FaizDerecesi = CreditMath.FactorToPercent(SatisEmsal);
        }

        partial void OnSatisAylıqChanged(decimal value) => SatisQiymetiHesabla();

        partial void OnSatisEmsalChanged(decimal value)
        {
            OnPropertyChanged(nameof(SatisEmsalFaiz));
            SatisQiymetiHesabla();
        }

        partial void OnMuddetAyChanged(int value)
        {
            // ƏMSAL avtomatik: 6→1.2 · 12→1.4 · 24→1.8 · 36→2.2
            SatisEmsal = Catalog.CreditFactorFor(value);
            SatisQiymetiHesabla();
        }

        partial void OnIlkinOdenisChanged(decimal value)
        {
            SatisQiymetiHesabla();

            // ⏳ Avans SONRADAN yazıldısa və möhlət sətri BOŞ qalıbsa (0 ₼) →
            //    avtomatik doldurulur ✓ (istifadəçi sonra düzəldə bilər ✓)
            if (IlkinMohletleri.Count == 1 && IlkinMohletleri[0].Mebleg <= 0m)
            {
                IlkinMohletleri[0].Mebleg = Math.Max(0m, value);
            }

            MohletXulaseYenile();
        }

        // ====================================================================
        //  👥 KREDİTİN TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ
        // --------------------------------------------------------------------
        //  Maşın KREDİTƏ VERİLƏNDƏ onun mənfəəti («xeyir») tərəfdaşlar
        //  arasında bölünür:
        //
        //      XEYİR = Satış Qiyməti − Maşının MAYA DƏYƏRİ
        //
        //  Nümunə: 22 014 − 20 420 = 1 594 ₼  → bu bölünür
        //
        //  Bölgü «Kredit Əlavə Gəlir/Xərc» tabındaki kimidir — ☑ işarəsi
        //  qoyulan tərəfdaşların faizi hesablanır, qalanı Asif & Musa bölür.
        // ====================================================================

        /// <summary>Kreditin tərəfdaş payları (adları ilə).</summary>
        public ObservableCollection<PartnerPayRow> KreditTerefdaslari { get; } = new();

        /// <summary>Bölgü tətbiq olunur? (checkbox)</summary>
        [ObservableProperty] private bool kreditBolguTetbiq;

        /// <summary>Bölgü bazası («xeyir») — Satış Qiyməti − Maya Dəyəri.</summary>
        [ObservableProperty] private decimal kreditBolguBazasi;

        /// <summary>Seçilmiş avtomobilin MAYA DƏYƏRİ (₼).</summary>
        [ObservableProperty] private decimal kreditMaya;

        /// <summary>Payların cəmi (₼).</summary>
        public decimal KreditBolguCemi => KreditTerefdaslari.Sum(r => r.Mebleg);

        /// <summary>Cəm ilə baza arasındaki fərq.</summary>
        public decimal KreditBolguFergi => KreditBolguCemi - KreditBolguBazasi;

        /// <summary>Xeyir mətni: «Xeyir: 1 594,00 ₼ (22 014 − 20 420)».</summary>
        public string KreditXeyirMetni =>
            $"Xeyir = Satış qiyməti − Maya = {Mebleg:N2} − {KreditMaya:N2} = {KreditBolguBazasi:N2} ₼";

        /// <summary>Vəziyyət mətni.</summary>
        public string KreditBolguVeziyyet => KreditBolguTetbiq ? "✅ Tətbiq olunub" : "⬜ Tətbiq olunmayıb";

        /// <summary>Xülasə: «Zaur 95,40 ₼ · Eşqin 79,50 ₼ · …».</summary>
        public string KreditBolguXulase => KreditTerefdaslari.Count == 0
            ? "Bölgü hazır deyil"
            : string.Join(" · ", KreditTerefdaslari.Select(r => $"{r.Terefdas} {r.Mebleg:N2} ₼"));

        /// <summary>Fərq mətni.</summary>
        public string KreditBolguFergMetni => KreditBolguFergi == 0m
            ? "✓ Dəqiq bölünüb — fərq yoxdur"
            : $"⚠ Fərq: {KreditBolguFergi:N2} ₼";

        /// <summary>Fərq rəngi.</summary>
        public string KreditBolguFergReng => KreditBolguFergi == 0m ? "#34D399" : "#F43F5E";

        /// <summary>
        /// KREDİTİN BÖLGÜ BAZASI = <b>Satış Qiyməti − Maya Dəyəri</b> («xeyir»).
        /// </summary>
        [RelayCommand]
        private void KreditBolguHesabla()
        {
            // Sətirlər hazır deyilsə — standart faizlərlə doldurulur.
            if (KreditTerefdaslari.Count == 0)
            {
                foreach (var share in PartnerMath.CreateDefaultRows())
                {
                    KreditTerefdaslari.Add(PartnerPayRow.FromModel(share));
                }
            }

            // Maya: seçilmiş avtomobilin maya dəyəri (yoxdursa 0).
            KreditMaya = SelectedCar?.MayaDeyeri ?? KreditMaya;

            var baza = Mebleg - KreditMaya;
            KreditBolguBazasi = baza > 0m ? Math.Round(baza, 2) : 0m;

            var modeller = new List<PartnerShare>();
            var sira = 0;
            foreach (var row in KreditTerefdaslari)
            {
                modeller.Add(row.ToModel(sira++));
            }

            PartnerMath.Distribute(KreditBolguBazasi, modeller);

            for (var i = 0; i < modeller.Count && i < KreditTerefdaslari.Count; i++)
            {
                KreditTerefdaslari[i].Mebleg = modeller[i].Mebleg;
            }

            KreditBolguYenile();
        }

        /// <summary>Faiz dərəcələrini standart qiymətlərə qaytarır.</summary>
        [RelayCommand]
        private void KreditBolguDefault()
        {
            var standart = PartnerMath.CreateDefaultRows();

            foreach (var row in KreditTerefdaslari)
            {
                var uygun = standart.FirstOrDefault(d => d.Terefdas == row.Terefdas);
                if (uygun is not null)
                {
                    row.Faiz = uygun.Faiz;
                    row.QaligPayi = uygun.QaligPayi;
                    row.Aktiv = true;
                }
            }

            KreditBolguHesabla();
        }

        /// <summary>Sətirləri standart hala gətirir.</summary>
        private void KreditBolguSifirla()
        {
            KreditTerefdaslari.Clear();
            foreach (var share in PartnerMath.CreateDefaultRows())
            {
                KreditTerefdaslari.Add(PartnerPayRow.FromModel(share));
            }

            KreditBolguTetbiq = false;
            KreditBolguBazasi = 0m;
            KreditBolguYenile();
        }

        private void KreditBolguYenile()
        {
            OnPropertyChanged(nameof(KreditBolguCemi));
            OnPropertyChanged(nameof(KreditBolguFergi));
            OnPropertyChanged(nameof(KreditBolguXulase));
            OnPropertyChanged(nameof(KreditXeyirMetni));
            OnPropertyChanged(nameof(KreditBolguVeziyyet));
            OnPropertyChanged(nameof(KreditBolguFergMetni));
            OnPropertyChanged(nameof(KreditBolguFergReng));
        }

        partial void OnKreditBolguTetbiqChanged(bool value)
        {
            OnPropertyChanged(nameof(KreditBolguVeziyyet));

            // ================================================================
            //  ⚠ ƏVVƏLKİ SƏHV: hesablama YALNIZ ilk dəfə (baza 0 ikən)
            //  işə düşürdü ✗ → satış qiyməti/maya sonra dəyişəndə bölgü
            //  YENİLƏNMİRDİ ✗ → «bölgü işləmir» ✗✓✓
            //  ✅ İNDİ: hər dəfə «TƏTBİQ ET» işarələnəndə YENİDƏN hesablanır ✓
            // ================================================================
            if (value && Mebleg > 0m)
            {
                KreditBolguHesabla();
            }
        }

        /// <summary>Nəticəni decimal sərhədləri içində saxlayır (çökmənin qarşısını alır).</summary>
        private static decimal Sinirla(decimal value)
        {
            const decimal limit = 1000000000000m;   // 1 trilyon
            if (value > limit)
            {
                return limit;
            }

            return value < -limit ? -limit : Math.Round(value, 2);
        }
    }
}
