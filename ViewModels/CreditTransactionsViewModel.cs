using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Collections;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>"Kredit Əlavə Gəlir/Xərc" tabının ViewModel-i.</summary>
    public sealed partial class CreditTransactionsViewModel : ObservableValidator
    {
        private readonly ICreditService _creditService;
        private readonly IMediaService _media;
        private readonly IDialogService _dialogs;
        private readonly ILogger<CreditTransactionsViewModel> _logger;

        /// <summary>Eyni anda iki yükləmənin işləməsinin qarşısını alır (siyahıların ikiqat olmasını önləyir).</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Filtrləmə zamanı geri-əlaqə (re-entrancy) dövrünün qarşısını alır.</summary>
        private bool _applyingFilter;

        public BulkObservableCollection<CreditTransaction> Transactions { get; } = new();
        public BulkObservableCollection<Credit> Credits { get; } = new();

        /// <summary>
        /// Cədvəldə göstərilən əməliyyatlar.
        /// Müqaviləyə avtomobil bağlanıbsa — yalnız HƏMİN AVTOMOBİLİN tarixçəsi,
        /// bağlanmayıbsa — BÜTÜN avtomobillərin kredit ödəniş/xərc tarixçəsi.
        /// </summary>
        public BulkObservableCollection<CreditTransaction> FilteredTransactions { get; } = new();

        /// <summary>Cədvəlin başlığında göstərilən izah mətni.</summary>
        [ObservableProperty] private string transactionsInfo = string.Empty;

        /// <summary>Seçilmiş əməliyyata bağlı sənəd / media faylları (çek, qaimə və s.).</summary>
        public ObservableCollection<MediaAttachment> Attachments { get; } = new();

        [ObservableProperty] private MediaAttachment? selectedAttachment;

        /// <summary>Fayl panelinin başlığı.</summary>
        [ObservableProperty] private string attachmentTitle = "📎 Fayl əlavə etmək üçün cədvəldən əməliyyat seçin";

        /// <summary>Seçilmiş fayl haqqında məlumat.</summary>
        [ObservableProperty] private string attachmentInfo = "Fayl seçilməyib";

        /// <summary>Axtarış mətninə uyğun (filtrlənmiş) kreditlər.</summary>
        public ObservableCollection<Credit> FilteredCredits { get; } = new();

        [ObservableProperty] private string creditSearchText = string.Empty;

        /// <summary>
        /// «NÖV» siyahısı — «Kredit Əlavə Gəlir/Xərc» tab-ının tip seçimləri ✓
        /// <list type="bullet">
        ///   <item>💰 <b>Gəlir</b> — kredit ödənişi ✓</item>
        ///   <item>🔴 <b>Xərc</b> — əlavə xərc ✓</item>
        ///   <item>🟡 <b>Möhlət</b> — ödəniş gecikdirməsi ✓</item>
        ///   <item>⚠️ <b>Gecikmə</b> — cərimə (50/50 Asif &amp; Musa) ✓</item>
        ///   <item>📤 <b>Transfer olunmaq</b> — maşın şəxsə verilir, <b>kredit
        ///         qiymətindən ÇIXILIR</b> ✓✓✓</item>
        ///   <item>🤝 <b>Barter</b> — maşın barterlə alınır, <b>nisyədən ÇIXILIR</b> ✓✓✓</item>
        ///   <item>🤝 <b>Barter köhnə</b> — keçmiş barter, <b>nisyədən ÇIXILIR</b> ✓✓✓</item>
        /// </list>
        /// </summary>
        public IReadOnlyList<string> Types { get; } = new[]
        {
            "Gəlir",
            "Xərc",
            "Möhlət",
            "Gecikmə",
            "Vaxtından tez bağlama",
            "Barter",
            "Barter köhnə"

            // ⚠ «Transfer olunmaq» BU SİYAHIDAN ÇIXARILDI ✓✓✓
            //   Səbəb: transfer «💳 Kreditlər» → «📤 Transfer» bölməsində
            //   idarə olunur ✓ (şəxs seçimi + 💰 PUL = kreditləşdirilən ✓).
            //   ⚠ KÖHNƏ «Transfer olunmaq» QEYDLƏRİ silinmir ✗ ✓ — cədvəldə
            //   normal görünür ✓ və nisyədən çıxılır ✓ (uyğunluq pozulmur ✓).
        };

        [ObservableProperty] private CreditTransaction? selectedTransaction;
        [ObservableProperty] private bool isBusy;

        [ObservableProperty] private Credit? selectedCredit;
        [ObservableProperty] private string nov = "Gəlir";

        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "Məbləğ mənfi ola bilməz.")]
        private decimal mebleg;

        /// <summary>Möhlətin verildiyi son tarix (yalnız "Möhlət" növü üçün).</summary>
        [ObservableProperty] private DateTime? mohletTarixi = DateTime.Today.AddMonths(1);

        /// <summary>Gecikmənin bağlandığı tarix (yalnız "Gecikmə" növü üçün).</summary>
        [ObservableProperty] private DateTime? gecikmeTarixi = DateTime.Today.AddDays(5);

        /// <summary>Seçilmiş növ "Möhlət"-dirmi?</summary>
        public bool IsMohlet => Nov == "Möhlət";

        /// <summary>Seçilmiş növ "Gecikmə"-dirmi?</summary>
        public bool IsGecikme => Nov == "Gecikmə";

        /// <summary>
        /// 📕 Növ «Vaxtından tez bağlama»-dırmı? ✓✓✓
        /// <para>
        /// Müştəri krediti <b>vaxtından əvvəl</b> bağlayır ✓:
        /// <list type="bullet">
        ///   <item><b>MƏBLƏĞ</b> = müştərinin <b>VERSƏCƏYİ</b> pul ✓</item>
        ///   <item><b>GÜZƏŞT</b> = kreditin <b>QALIĞINDAN ÇIXILAN</b> pul ✓
        ///         (salonun müştəriyə etdiyi endirim ✓)</item>
        ///   <item>Bağlanan qalıq = <c>MƏBLƏĞ + GÜZƏŞT</c> ✓</item>
        /// </list>
        /// </para>
        /// </summary>
        public bool IsErkenBaglama => Nov == "Vaxtından tez bağlama";

        /// <summary>
        /// 👥 TƏRƏFDAŞ BÖLGÜSÜ PANELİ görünməlidirmi? ✓✓✓
        /// («Gəlir» və «Vaxtından tez bağlama» növləri üçün ✓)
        /// </summary>
        public bool IsBolguPaneli => IsGelir || IsErkenBaglama;

        /// <summary>
        /// 💰 <b>GÜZƏŞT (₼)</b> ✓✓✓ — kreditin QALIĞINDAN çıxılan pul ✓
        /// (yalnız «Vaxtından tez bağlama» növü üçün ✓).
        /// </summary>
        [ObservableProperty] private decimal guzest;

        // ✅ Güzəşt yazıldıqca MƏBLƏĞ AVTOMATİK hesablanır ✓✓✓
        partial void OnGuzestChanged(decimal value) => ErkenBaglamaHesabla();

        /// <summary>
        /// 📕 <b>«VAXTINDAN TEZ BAĞLAMA» — AVTOMATİK HESABLAMA</b> ✓✓✓
        /// <para>
        /// <c>QALIQ  = Kreditin qiyməti − ödənilmiş (bütün «Gəlir» qeydləri)</c> ✓
        /// <c>MƏBLƏĞ = QALIQ − GÜZƏŞT</c> ✓ ← müştərinin VERƏCƏYİ pul ✓
        /// </para>
        /// <para>
        /// Nümunə: qalıq 45 000 ₼ · güzəşt 5 000 ₼ → MƏBLƏĞ = 40 000 ₼ ✓✓✓
        /// </para>
        /// </summary>
        private void ErkenBaglamaHesabla()
        {
            if (!IsErkenBaglama || SelectedCredit is null)
            {
                return;
            }

            var odenilmis = Transactions
                .Where(t => t.CreditId == SelectedCredit.Id && t.Nov == "Gəlir")
                .Sum(t => t.Mebleg);

            var qaliq = Math.Max(0m, SelectedCredit.KreditQiymeti - odenilmis);

            // ✅ MƏBLƏĞ = QALIQ − GÜZƏŞT ✓✓✓
            Mebleg = Math.Max(0m, qaliq - Guzest);

            // ================================================================
            //  👥 TƏRƏFDAŞ BÖLGÜSÜ — AVTOMATİK HAZIRLANIR ✓✓✓
            // ----------------------------------------------------------------
            //  ① «Tətbiq olunmaq» checkbox AVTOMATİK CHECK olunur ✓
            //  ② BAZA = GÜZƏŞT ✓ (salonun endirimi tərəfdaşlar arasında bölünür ✓)
            //  ③ YALNIZ «QALIQ» NÖV TƏRƏFDAŞLAR (Asif & Musa) pay alır ✓ —
            //     faiz payı olanlara (Zaur · Eşqin · Asiman) 0 yazılır ✓
            // ================================================================
            BolguTetbiqOlunub = true;

            foreach (var setir in TerefdasPaylari)
            {
                if (setir.QaligPayi)
                {
                    continue;      // ✓ qalığı YARI-YARIYA bölürlər ✓
                }

                setir.Faiz = 0m;   // ✗ faiz payı olanlara 0%
                setir.Mebleg = 0m;
            }

            BolguHesabla();

            // ================================================================
            //  ⚠ VACİB: BAZA «BolguHesabla()»-DAN SONRA təyin olunur ✓✓✓
            // ----------------------------------------------------------------
            //  `BolguHesabla()` bazanı ÖZÜ hesablayır ✗ → əvvəl yazsaydıq
            //  Güzəşt (2 000 ₼) ÜZƏRİNƏ YAZILIRDI ✗ → bölgü 52 682 ₼ kimi
            //  səhv rəqəm çıxırdı ✗✓✓
            //
            //  ✅ «VAXTINDAN TEZ BAĞLAMA» → BAZA = GÜZƏŞT ✓✓✓
            //     (salonun müştəriyə verdiyi endirim tərəfdaşlar arasında
            //      bölünür ✓ — məs. 2 000 ₼ → Asif 1 000 · Musa 1 000 ✓)
            // ================================================================
            // ================================================================
            //  ✅ «VAXTINDAN TEZ BAĞLAMA» → BAZA = MƏBLƏĞ ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL baza Məbləğdən AZ çıxırdı ✗ (49 682,08 vs 50 230,00)
            //  → **547,92 ₼ İTİRDİ** ✗✓✓
            //
            //  ✅ İNDİ: müştərinin verdiyi MƏBLƏĞ TAM bölünür ✓
            //     → HEÇ NƏ İTMİR ✗✓✓
            //     Məs.: 50 230,00 ₼ → Asif 25 115,00 · Musa 25 115,00 ✓
            // ================================================================
            BolguBazasi = Mebleg;
        }

        // ====================================================================
        //  TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ
        // --------------------------------------------------------------------
        //  Maşından gələn mənfəət («xeyir») tərəfdaşlar arasında bölünür:
        //      Zaur %6 · Eşqin %5 · Asiman %5   → FAİZ PAYLARI
        //      Asif & Musa                      → qalığı YARI-YARIYA bölürlər
        //
        //  Nümunə (mənfəət = 1 590 ₼):
        //      Zaur   →   95,40 ₼      Asif → 667,80 ₼
        //      Eşqin  →   79,50 ₼      Musa → 667,80 ₼
        //      Asiman →   79,50 ₼      CƏMİ → 1 590,00 ₼
        //
        //  Faiz dərəcələri də, məbləğlər də HƏMİŞƏ manual dəyişdirilə bilər.
        // ====================================================================

        /// <summary>Tərəfdaş bölgüsü TƏTBİQ OLUNUB? (formadakı checkbox)</summary>
        [ObservableProperty] private bool bolguTetbiqOlunub;

        /// <summary>Bölgü bazası (₼) — bölünən mənfəət («xeyir»). Manual dəyişdirilə bilər.</summary>
        [ObservableProperty] private decimal bolguBazasi;

        /// <summary>Tərəfdaş sətirləri (Zaur, Eşqin, Asiman, Asif, Musa).</summary>
        public ObservableCollection<PartnerPayRow> TerefdasPaylari { get; } = new();

        /// <summary>Bölgü yalnız «Gəlir» əməliyyatlarında tətbiq olunur.</summary>
        public bool IsGelir => Nov == "Gəlir";

        // ====================================================================
        //  📤 TRANSFER OLUNMAQ  ·  🤝 BARTER  ·  🤝 BARTER KÖHNƏ
        // --------------------------------------------------------------------
        //  Bu üç növ kreditin NİSYƏ QİYMƏTİNDƏN ÇIXILIR ✓✓✓
        //  (bax: CreditsViewModel.BuildScheduleCore → balance)
        // ====================================================================

        /// <summary>Növ «Transfer olunmaq»-dırmı.</summary>
        public bool IsTransferOlunmaq => Nov == "Transfer olunmaq";

        /// <summary>Növ «Barter» və ya «Barter köhnə»-dirmi.</summary>
        public bool IsBarter => Nov == "Barter" || Nov == "Barter köhnə";

        /// <summary>📤 Transfer edilən ŞƏXSİN adı (məs. «Tural»).</summary>
        [ObservableProperty] private string transferSexsAdi = string.Empty;

        /// <summary>🤝 Barter edilən maşının MARKASI (Avto Park formatı ✓).</summary>
        [ObservableProperty] private string barterMarka = string.Empty;

        /// <summary>🤝 Barter edilən maşının MODELİ (Avto Park formatı ✓).</summary>
        [ObservableProperty] private string barterModel = string.Empty;

        /// <summary>📄 Barter SƏNƏDİNİN adı (məs. «M188»).</summary>
        [ObservableProperty] private string barterSenedAdi = string.Empty;

        // ====================================================================
        //  📅 «BAŞQA TAKSİTİN ÖDƏNİŞİDİR» (ƏVVƏLCƏDƏN ÖDƏNİŞ) ✓✓✓ (v6.2.19)
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi:
        //    «Kredit ödənişi gəlir/xərc-də GƏLİR əlavə edəndə — məsələn
        //     30.03.2024-də ödəyib, amma 03.04.2024-ün kreditini ƏVVƏLCƏDƏN
        //     ödəyib. Orada bir CHECK BOX qoy: “başqa tarix” — yəni hansı
        //     taksitin ödənişidir. Kredit CƏDVƏLİNDƏ də AVTOMATİK hansı ayın
        //     ödənişidirsə O AYIN sırasına yazılsın.» ✓✓✓
        // --------------------------------------------------------------------
        //  ⚠ ƏVVƏL: taksit nömrəsi YALNIZ ödəniş TARİXİNDƏN hesablanırdı ✗ →
        //    əvvəlcədən ödəniş KEÇMİŞ ayın sırasına yazılırdı ✗ →
        //    həmin ay «artıq ödəniş» ✗, gələn ay isə «ödənilməyib» görünürdü ✗✓✓
        //  ✅ İNDİ: istifadəçi istəsə TAKSİTİ ÖZÜ SEÇİR ✓ → cədvəldə DÜZGÜN
        //    ayın sırasına düşür ✓✓✓
        // ====================================================================

        /// <summary>📅 «Başqa taksitin ödənişidir» — əvvəlcədən ödəniş rejimi ✓✓✓</summary>
        [ObservableProperty] private bool basqaTaksit;

        /// <summary>📅 Seçilmiş taksitin nömrəsi (1 … Müddət) ✓✓✓</summary>
        [ObservableProperty] private int secilmisTaksitNo;

        /// <summary>📅 Taksit seçim siyahısı ✓ (hansı ay / ödənilibmi ✓)</summary>
        public ObservableCollection<TaksitSecimi> TaksitSecimleri { get; } = new();

        /// <summary>📅 Checkbox yalnız «Gəlir» növündə və kredit seçilibsə görünür ✓</summary>
        public bool IsBasqaTaksitGorunur => IsGelir && SelectedCredit is not null;

        /// <summary>📅 Seçim siyahısı yalnız checkbox işarələnəndə açılır ✓</summary>
        public bool TaksitSecimiGorunur => BasqaTaksit && IsBasqaTaksitGorunur;

        /// <summary>
        /// 📅 <b>ƏVVƏLCƏDƏN ÖDƏNİŞ İZAHI</b> ✓✓✓:
        /// «📅 11-ci taksitin (03.04.2024) ödənişidir — ödəniş tarixi 30.03.2024 ✓»
        /// </summary>
        public string BasqaTaksitIzahi
        {
            get
            {
                var credit = SelectedCredit;

                if (credit is null || !BasqaTaksit || SecilmisTaksitNo <= 0)
                {
                    return string.Empty;
                }

                var planTarixi = credit.BaslamaTarixi.AddMonths(SecilmisTaksitNo - 1);
                var odenisTarixi = Tarix ?? DateTime.Today;

                return $"📅 {SecilmisTaksitNo}-ci taksitin ödənişidir " +
                       $"(plan: {planTarixi:dd.MM.yyyy}) · ödəniş tarixi: {odenisTarixi:dd.MM.yyyy} ✓";
            }
        }

        /// <summary>
        /// 📅 Taksit seçim sətri ✓✓✓ — cədvəldə görünən № · plan tarixi · vəziyyət ✓
        /// </summary>
        public sealed class TaksitSecimi
        {
            /// <summary>Taksit nömrəsi (1 … Müddət) ✓</summary>
            public int No { get; init; }

            /// <summary>Plan (cədvəl) tarixi ✓</summary>
            public DateTime PlanTarixi { get; init; }

            /// <summary>Artıq ödənilibmi? ✓</summary>
            public bool Odenilib { get; init; }

            /// <summary>ComboBox-da göstərilən mətn ✓</summary>
            public string Metn { get; init; } = string.Empty;

            /// <inheritdoc />
            public override string ToString() => Metn;
        }

        /// <summary>Barter/Transfer məbləği = MEBLEG sahəsi (₼) — nisyədən çıxılır ✓.</summary>
        public string BarterTransferIzahi =>
            "Məbləğ sahəsi: Barter MAYA DƏYƏRİ / Transfer BEH məbləğidir — kreditin nisyə qiymətindən ÇIXILIR ✓";

        /// <summary>📤 Transfer edilə bilən ŞƏXSLƏR (axtarışlı seçim ✓).</summary>
        public ObservableCollection<string> TransferSexsleri { get; } = new();

        /// <summary>🤝 Barter MARKALARI (Avto Park-dan + əvvəlki barterlər ✓).</summary>
        public ObservableCollection<string> BarterMarkalari { get; } = new();

        /// <summary>Şəxs siyahısını bütün transfer qeydlərindən yığır ✓.</summary>
        private void TransferSexsleriniYenile()
        {
            TransferSexsleri.Clear();

            var adlar = Transactions
                .Where(t => (t.Nov == "Transfer"
                             || t.Nov == "Transfer olunmaq"
                             // ⚠️ VACİB: «Transfer» tab-ında ➕ İNSAN ƏLAVƏ ET ilə
                             //  yazılan şəxslər `TransferŞəxs` növü ilə saxlanılır ✓
                             || t.Nov == "TransferŞəxs")
                            && !string.IsNullOrWhiteSpace(t.Tesvir))
                // «Tural · Toyota Camry» → «Tural» ✓ (şaхıs adı birinci hissədir)
                .Select(t => t.Tesvir.Split('·')[0].Trim())
                .Where(a => a.Length > 0)
                .ToList();

            // ================================================================
            //  💾 DAİMİ JSON FAYLINDAN ADLAR ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏLKİ XƏTA: yalnız BAZADAKI qeydlər oxunurdu ✗ → «📤 Transfer»
            //  tab-ında ➕ İNSAN ƏLAVƏ ET ilə yazılan şəxslər (JSON faylında
            //  saxlanılır) burada GÖRÜNMÜRDÜ ✗ → üçbucağa (▼) basanda
            //  HEÇ NƏ GƏLMİRDİ ✗✓✓
            // ================================================================
            adlar.AddRange(TransferSexsStore.Oxu());

            // Əl ilə yazılan ad da siyahıda görünsün ✓
            if (!string.IsNullOrWhiteSpace(TransferSexsAdi))
            {
                adlar.Add(TransferSexsAdi.Trim());
            }

            foreach (var ad in adlar
                         .Where(a => !string.IsNullOrWhiteSpace(a))
                         .Select(a => a.Trim())
                         .Distinct(StringComparer.CurrentCultureIgnoreCase)
                         .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase))
            {
                TransferSexsleri.Add(ad);
            }
        }

        /// <summary>Barter markalarını Avto Park-dan + keçmiş barterlərdən yığır ✓.</summary>
        private void BarterSexsleriniYenile()
        {
            BarterMarkalari.Clear();

            foreach (var marka in _carMarkalari
                         .Concat(Transactions
                             .Where(t => t.Nov == "Barter" || t.Nov == "Barter köhnə")
                             .SelectMany(t => (t.Tesvir ?? string.Empty)
                                 .Split('·', StringSplitOptions.RemoveEmptyEntries)))
                         .Select(s => s.Trim())
                         .Where(s => s.Length is > 1 and < 40)
                         .Distinct()
                         .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            {
                BarterMarkalari.Add(marka);
            }

            if (!string.IsNullOrWhiteSpace(BarterMarka)
                && !BarterMarkalari.Contains(BarterMarka.Trim()))
            {
                BarterMarkalari.Add(BarterMarka.Trim());
            }
        }

        /// <summary>Avto Park-daki maşın markaları (search üçün) ✓.</summary>
        private static readonly string[] _carMarkalari =
        {
            "Toyota", "Hyundai", "Kia", "Nissan", "Chevrolet", "Mercedes", "BMW",
            "Audi", "Volkswagen", "VAZ", "Lada", "Opel", "Renault", "Peugeot",
            "Ford", "Mazda", "Honda", "Mitsubishi", "Skoda", "Lexus", "Chery",
            "Geely", "Daewoo", "GAZ", "UAZ", "ZAZ", "Moskvich", "İsuzu", "Isuzu"
        };

        /// <summary>Barter maşınının tam adı: «VAZ 2107» ✓.</summary>
        public string BarterMasinAdi =>
            $"{BarterMarka?.Trim()} {BarterModel?.Trim()}".Trim();

        /// <summary>Formaya uyğun «Tesvir» mətni qurur ✓.</summary>
        private string TesvirQur(CreditTransaction? _ = null)
        {
            if (IsTransferOlunmaq)
            {
                var masin = SelectedCredit?.Car?.DisplayName;
                return string.IsNullOrWhiteSpace(TransferSexsAdi)
                    ? $"Transfer olunmaq{(string.IsNullOrWhiteSpace(masin) ? string.Empty : $" — {masin}")}"
                    : $"{TransferSexsAdi.Trim()}{(string.IsNullOrWhiteSpace(masin) ? string.Empty : $" · {masin}")}";
            }

            if (IsBarter)
            {
                var hisse = new[]
                {
                    string.IsNullOrWhiteSpace(BarterSenedAdi) ? null : $"Sənəd: {BarterSenedAdi.Trim()}",
                    string.IsNullOrWhiteSpace(BarterMasinAdi) ? null : $"Maşın: {BarterMasinAdi}",
                    $"({Nov})"
                }.Where(s => !string.IsNullOrWhiteSpace(s));

                return string.Join(" · ", hisse);
            }

            return Tesvir.Trim();
        }

        /// <summary>Tərəfdaş paylarının cəmi (₼).</summary>
        public decimal BolguCemi => TerefdasPaylari.Sum(r => r.Mebleg);

        /// <summary>Cəm ilə bölgü bazası arasındaki fərq (0,00 olmalıdır).</summary>
        public decimal BolguFergi => BolguCemi - BolguBazasi;

        /// <summary>Formadakı qısa xülasə: «Zaur 95,40 ₼ · Eşqin 79,50 ₼ · …».</summary>
        public string BolguXulase => TerefdasPaylari.Count == 0
            ? "Bölgü hazır deyil"
            : string.Join(" · ", TerefdasPaylari.Select(r => $"{r.Terefdas} {r.Mebleg:N2} ₼"));

        /// <summary>Başlıqdaki checkbox-ın yanında göstərilən vəziyyət mətni.</summary>
        public string BolguVeziyyetMetni => BolguTetbiqOlunub ? "✅ Tətbiq olunub" : "⬜ Tətbiq olunmayıb";

        /// <summary>Yekun sətrindəki fərq mətni — bölgünün dəqiq olub olmadığını bildirir.</summary>
        public string BolguFergiMetni => BolguFergi == 0m
            ? "✓ Dəqiq bölünüb — fərq yoxdur"
            : $"⚠ Fərq: {BolguFergi:N2} ₼ — payların cəmi baza ilə üst-üstə düşmür";

        /// <summary>Fərq mətninin rəngi (dəqiqdirsə yaşıl, fərq varsa qırmızı).</summary>
        public string BolguFergiRengi => BolguFergi == 0m ? "#34D399" : "#F43F5E";

        // ====================================================================
        //  🔢 KÖK (ƏSAS BORC) BÖLGÜ İZAHI ✓✓✓ (v6.2.17)
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi: «Ödənişdən KÖK QİYMƏTİ ÇIXARILMALIDIR,
        //    SONRA BÖLÜNMƏLİDİR» ✓ — istifadəçi bunu GÖRMƏLİDİR ✓✓✓
        // ====================================================================

        /// <summary>🔢 <b>Bu ödənişdən çıxılan KÖK payı</b> (₼) ✓✓✓</summary>
        public decimal BolguKokPayi
        {
            get
            {
                var credit = SelectedCredit;

                return credit is null || Mebleg <= 0m
                    ? 0m
                    : credit.OdenisKokPayi(Mebleg, OdenilmisKokHesabla());
            }
        }

        /// <summary>✅ Seçilmiş kredit üzrə artıq ödənilmiş KÖK (₼) ✓</summary>
        public decimal BolguOdenilmisKok => OdenilmisKokHesabla();

        /// <summary>
        /// 🔢 <b>İZAH MƏTNİ</b> ✓✓✓:
        /// «Ödəniş 1 984,97 ₼ − KÖK payı 1 417,83 ₼ = BÖLÜNƏN 567,14 ₼» —
        /// yanında qalan əsas borc da göstərilir ✓
        /// </summary>
        public string BolguKokMetni
        {
            get
            {
                var credit = SelectedCredit;

                if (credit is null || Mebleg <= 0m)
                {
                    return "🔢 Ödəniş yazın → KÖK payı avtomatik çıxılacaq ✓";
                }

                var ödenilmis = OdenilmisKokHesabla();
                var qalan = Math.Max(0m, credit.Kok - ödenilmis);

                if (qalan <= 0m && credit.Kok > 0m)
                {
                    return $"✅ KÖK (əsas borc) TAM ÖDƏNİLİB ✓ — bu ödənişin HAMISI mənfəətdir ✓ ({Mebleg:N2} ₼ bölünür ✓)";
                }

                var kokPayi = credit.OdenisKokPayi(Mebleg, ödenilmis);

                return $"🔢 {Mebleg:N2} ₼ − KÖK payı {kokPayi:N2} ₼ = " +
                       $"BÖLÜNƏN {BolguBazasi:N2} ₼  ·  qalıq kök {qalan:N2} ₼";
            }
        }

        [ObservableProperty] private DateTime? tarix = DateTime.Today;

        [ObservableProperty]
        [MaxLength(300, ErrorMessage = "Təsvir 300 simvoldan çox ola bilməz.")]
        private string tesvir = string.Empty;

        [ObservableProperty] private decimal totalIncome;
        [ObservableProperty] private decimal totalExpense;
        [ObservableProperty] private decimal net;

        /// <summary>Əməliyyat dəyişdikdə baş verir (kredit ödəniş qrafikini dərhal yeniləmək üçün).</summary>
        public event EventHandler? DataChanged;

        public CreditTransactionsViewModel(
            ICreditService creditService,
            IMediaService media,
            IDialogService dialogs,
            ILogger<CreditTransactionsViewModel> logger)
        {
            _creditService = creditService;
            _media = media;
            _dialogs = dialogs;
            _logger = logger;

            // Tərəfdaş bölgüsü sətirləri standart faizlərlə hazırlanır
            // (Zaur %6 · Eşqin %5 · Asiman %5 · Asif & Musa qalıq).
            BolguSifirla();
        }

        /// <summary>
        /// 📢 Əməliyyat əlavə/silinəndə baş verir ✓✓✓
        /// (<c>MainViewModel</c> abunə olur → «💳 Kreditlər» və arxiv DƏRHAL
        /// yenilənir ✓ — məs. 📕 vaxtından tez bağlama krediti siyahıdan çıxır ✓)
        /// </summary>
        public event EventHandler? TransactionsChanged;

        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                // ⚡🚀 AĞIR OXUMA ARXA FONDA ✓✓✓ (milyon sətirdə belə UI donmur ✗)
                var credits = await Task.Run(() => _creditService.GetCreditsAsync());
                var transactions = await Task.Run(() => _creditService.GetTransactionsAsync());

                // ================================================================
                //  📤 TRANSFER OLUNMUŞ MAŞINLAR — GƏLİR / QEYDLƏR GÖSTƏRİLMİR ✗✓✓
                // ----------------------------------------------------------------
                //  Transfer edilmiş kredit artıq BİZİM DEYİL ✗ → onun
                //  «Gəlir» (ödəniş) və digər qeydləri bu tabda GÖRÜNMƏMƏLİDİR ✗✓✓
                //  (nə cədvəldə, nə kredit seçimində ✓)
                // ================================================================
                var transferliKreditIdler = transactions
                    .Where(t => t.CreditId.HasValue
                                && (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq"))
                    .Select(t => t.CreditId!.Value)
                    .ToHashSet();

                Credits.ReplaceAll(credits.Where(c => !transferliKreditIdler.Contains(c.Id)));

                var creditById = Credits.ToDictionary(c => c.Id);

                // ⚡ Sətirlər SİYAHIYA yığılır ✓ — sonra TƏK bildirişlə yazılır ✓✓✓
                //   (əvvəl minlərlə `Add` hadisəsi ilə cədvəl min dəfə yenilənirdi ✗)
                var sətirlər = new List<CreditTransaction>(transactions.Count);

                foreach (var transaction in transactions.Where(t =>
                             !t.CreditId.HasValue
                             || !transferliKreditIdler.Contains(t.CreditId.Value)))
                {
                    if (transaction.CreditId is int creditId && creditById.TryGetValue(creditId, out var credit))
                    {
                        transaction.Credit = credit;
                    }

                    sətirlər.Add(transaction);
                }

                Transactions.ReplaceAll(sətirlər);

                UpdateStats();
                ApplyCreditFilter();
                ApplyTransactionFilter();

                _logger.LogInformation("{Count} kredit əməliyyatı yükləndi.", Transactions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit əməliyyatları yüklənərkən xəta baş verdi.");
                _dialogs.ShowError("Kredit əməliyyatları yüklənə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        [RelayCommand]
        private async Task AddTransactionAsync()
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                _dialogs.ShowWarning("Zəhmət olmasa formada göstərilən səhvləri düzəldin.");
                return;
            }

            // ⚠️ «XÜSUSİ» NÖVLƏR — tarix/müddət məcburi deyil, taksit hesablanmır ✓
            //  Möhlət · Gecikmə · Transfer olunmaq · Barter · Barter köhnə ✓
            var isException = IsMohlet || IsGecikme || IsTransferOlunmaq || IsBarter;
            var isPayment = Nov == "Gəlir" || isException;

            if (!isException && Mebleg <= 0m)
            {
                _dialogs.ShowWarning("Məbləğ 0-dan böyük olmalıdır.");
                return;
            }

            if (isException && SelectedCredit is null)
            {
                _dialogs.ShowWarning(Nov + " kreditə bağlanır — zəhmət olmasa kredit seçin.");
                return;
            }

            if (IsMohlet && MohletTarixi is null)
            {
                _dialogs.ShowWarning("Möhlət üçün son tarix seçilməlidir.");
                return;
            }

            if (IsGecikme && GecikmeTarixi is null)
            {
                _dialogs.ShowWarning("Gecikmə üçün tarix seçilməlidir.");
                return;
            }

            int? installmentNo = null;

            try
            {
                IsBusy = true;
                if (isPayment && SelectedCredit is not null)
                {
                    // ================================================================
                    //  📅 v6.2.19 — ƏVVƏLCƏDƏN ÖDƏNİŞ ✓✓✓
                    // ----------------------------------------------------------------
                    //  ⚠ ƏVVƏL taksit YALNIZ tarixdən hesablanırdı ✗ → 30.03.2024-də
                    //    edilən ödəniş 03.2024-ün sırasına yazılırdı ✗ (halbuki
                    //    müştəri 03.04.2024-ün ödənişini ƏVVƏLCƏDƏN ödəyirdi ✗✓✓)
                    //  ✅ İNDİ: «📅 Başqa taksitin ödənişidir» işarələnibsə
                    //    istifadəçinin SEÇDİYİ taksit yazılır ✓ → kredit
                    //    cədvəlində DÜZGÜN ayın sırasına düşür ✓✓✓
                    // ================================================================
                    installmentNo = BasqaTaksit && SecilmisTaksitNo > 0
                        ? SecilmisTaksitNo
                        : ComputeInstallmentNo(SelectedCredit, Tarix ?? DateTime.Today);
                }

                if (isException && installmentNo is null)
                {
                    _dialogs.ShowWarning(Nov + " tarixi kredit müddətindən kənardır. Tarixi düzəldin.");
                    return;
                }

                await _creditService.AddTransactionAsync(new CreditTransaction
                {
                    CreditId = SelectedCredit?.Id,
                    InstallmentNo = installmentNo,
                    Nov = Nov,
                    Mebleg = Mebleg,
                    Tarix = Tarix ?? DateTime.Today,
                    MohletTarixi = IsMohlet ? MohletTarixi : null,

                    // (💰 GÜZƏŞT aşağıda «BolguBazasi» sahəsində saxlanılır ✓✓✓
                    //  «Vaxtından tez bağlama» növü üçün özəl sahədir ✓)
                    GecikmeTarixi = IsGecikme ? GecikmeTarixi : null,
                    Tesvir = TesvirQur(),

                    // ---- TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ ----
                    //  Checkbox işarələnməyibsə pay siyahısı BOŞ göndərilir → bazadakı
                    //  əvvəlki bölgü də silinir.
                    //  ⚠️ GECİKMƏ istisnadır ✓ — cərimə bölgüsü AVTOMATİK tətbiq
                    //  olunur (yarı-yarı Asif & Musa) və bölgü jurnalına yazılır ✓
                    // ================================================================
                    //  ✅ YENİ GECİKMƏ → HƏMİŞƏ «ÖDƏNİLMƏMİŞ» YARADILIR ✓✓✓
                    // ----------------------------------------------------------------
                    //  Pul hələ GƏLMƏYİB ✗ → tərəfdaşlar arasında BÖLÜNMÜR ✓
                    //  (ödəniş ediləndə «ödənilib» checkbox-ı ilə vəziyyət
                    //   dəyişir və paylar O ZAMAN yazılır ✓✓✓)
                    // ================================================================
                    TerefdasBolguTetbiqOlunub =
                        Nov == "Gecikmə"
                            ? false                        // ✅ ödənilməmiş → bölgü yox ✓
                            : BolguTetbiqOlunub && Nov == "Gəlir",
                    // 💰 «Vaxtından tez bağlama» → BolguBazasi = MƏBLƏĞ ✓✓✓
                    //  ⚠ ƏVVƏL burada GÜZƏŞT yazılırdı ✗ → yadda saxlanan baza
                    //    ekrandakından fərqli olurdu ✗ («eksik» fərq yaranırdı ✓)
                    //  ✅ İNDİ ekranda göstərilən baza ilə BİR-BİRDİR ✓✓✓
                    BolguBazasi = IsErkenBaglama
                        ? Mebleg
                        : Nov == "Gecikmə"
                            ? Mebleg
                            : BolguTetbiqOlunub && Nov == "Gəlir" ? BolguBazasi : null,
                    // ================================================================
                    //  ✅ GECİKMƏ YARADILARKEN PAYLAR BOŞDUR ✓✓✓
                    // ----------------------------------------------------------------
                    //  «ödənilib» ☑ → pul GƏLƏNDƏ paylar yazılır ✓ (checkbox toggle ✓)
                    //  «ödənilib» ☐ → pul GƏLMƏYİB ✗ → bölünəcək heç nə yoxdur ✓✓✓
                    // ================================================================
                    TerefdasPaylari = Nov == "Gecikmə"
                        ? new List<PartnerShare>()
                        : BolguModelleri()
                });

                // ================================================================
                //  📤 ŞƏXS ADI DAİMİ JSON FAYLINA YAZILIR ✓✓✓
                // ----------------------------------------------------------------
                //  → «💳 Kreditlər» tab-ı və bu tab EYNİ faylı oxuyur ✓
                //  → şəxs HEÇ VAXT silinmir ✓ və hər iki tabın üçbucağında (▼)
                //    həmişə görünür ✓✓✓
                // ================================================================
                if (Nov == "Transfer olunmaq" && !string.IsNullOrWhiteSpace(TransferSexsAdi))
                {
                    TransferSexsStore.Elave(TransferSexsAdi);
                }

                // ================================================================
                //  📕 VAXTINDAN TEZ BAĞLAMA → KREDİT AVTOMATİK BAĞLANIR ✓✓✓
                // ----------------------------------------------------------------
                //  ① «💳 Kreditlər» tabından ÇIXIR ✓ (bağlı kreditlər süzülür ✓)
                //  ② «🗄️ Satılan & Krediti Bitmiş» → «Krediti Bitmiş» bölməsində
                //     görünür ✓ (status «Bağlı» ✓)
                // ================================================================
                if (Nov == "Vaxtından tez bağlama" && SelectedCredit is not null)
                {
                    var qeyd =
                        $"📕 Vaxtından tez bağlandı · {Tarix:dd.MM.yyyy} · " +
                        $"güzəşt {Guzest:N2} ₼ · müştəri verdi {Mebleg:N2} ₼";

                    // ✅ KREDİT + AVTOMOBİL BİRGƏ BAĞLANIR ✓✓✓
                    //  (servis metodu ClearTracker ilə işləyir ✓ →
                    //   kredit «💳 Kreditlər»-dən, avtomobil 🚘 parkdan
                    //   və «🟠 Kreditdə» sayından ÇIXIR ✓✓✓)
                    var baglandi = await _creditService.CloseCreditEarlyAsync(
                        SelectedCredit.Id, qeyd);

                    _logger.LogInformation(
                        "📕 Vaxtından tez bağlama {Netice}: {Muqavile} · güzəşt {Guzest:N2} ₼ · ödəniş {Mebleg:N2} ₼",
                        baglandi ? "✓" : "✗ (kredit tapılmadı)",
                        SelectedCredit.MuqavileNomresi, Guzest, Mebleg);
                }

                Mebleg = 0m;
                Tesvir = string.Empty;
                BolguSifirla();

                // 📅 v6.2.19 — «Əvvəlcədən ödəniş» rejimi SÖNDÜRÜLÜR ✓✓✓
                //    (hər yeni qeyd üçün təkrar seçim tələb olunsun ✗ → təhlükəsizdir ✓)
                SecilmisTaksitNo = 0;
                BasqaTaksit = false;
                TaksitSecimleri.Clear();

                ClearErrors();
                _logger.LogInformation("Kredit əməliyyatı əlavə edildi ({Type}).", Nov);

                // 📢 «💳 Kreditlər» + arxiv DƏRHAL yenilənir ✓✓✓
                //  (📕 vaxtından tez bağlama → kredit siyahıdan ÇIXIR ✓)
                TransactionsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit əməliyyatı əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Əməliyyat əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task DeleteTransactionAsync()
        {
            if (SelectedTransaction is null)
            {
                _dialogs.ShowWarning("Silmək üçün siyahıdan əməliyyat seçin.");
                return;
            }

            var transaction = SelectedTransaction;
            if (!_dialogs.Confirm($"\"{transaction.Nov} - {transaction.Mebleg:N2} AZN\" əməliyyatını silmək istəyirsiniz?"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _creditService.DeleteTransactionAsync(transaction.Id);

                // Əməliyyata bağlı çek / sənəd faylları da silinir.
                var files = await _media.GetAsync(MediaRefTypes.CreditTransaction, transaction.Id);
                foreach (var file in files)
                {
                    await _media.DeleteAsync(file.Id);
                }

                SelectedTransaction = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kredit əməliyyatı silinərkən xəta baş verdi.");
                _dialogs.ShowError("Əməliyyat silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            await LoadAsync();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gecikmə qeydinin "ödənilib / gözləyir" vəziyyətini dəyişib bazada saxlayır.
        /// Dəyişiklikdən sonra ödəniş qrafiki yenidən qurulur (rəng dərhal yenilənir).
        /// </summary>
        [RelayCommand]
        private async Task ToggleTransactionPaidAsync(CreditTransaction? transaction)
        {
            if (transaction is null)
            {
                return;
            }

            try
            {
                IsBusy = true;

                // ================================================================
                //  ✅ «ÖDƏNİLMƏYİB» EDİLƏNDƏ TƏRƏFDAŞ PAYLARI GERİ ÇƏKİLİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏLKİ SƏHV: paylar bazada QALIRDI ✗ → «ödənilməyib»
                //  edildikdən sonra da kartlarda/dövriyyədə/jurnalda 50 ₼
                //  görünürdü ✗✓✓ (qalıq sahiblərindən geri çəkilmirdi ✗)
                //
                //  ✅ İNDİ: vəziyyət «ödənilməyib» olanda bölgü SÖNDÜRÜLÜR ✓ və
                //     paylar BOŞ göndərilir ✓ → bazadan SİLİNİR ✓✓✓
                //     (yenidən «ödənilib» ediləndə VM payları yenidən yazır ✓)
                // ================================================================
                if (!transaction.Odenilib
                    && (transaction.Nov == "Gecikmə" || transaction.Nov == "Gəlir"))
                {
                    transaction.TerefdasBolguTetbiqOlunub = false;
                    transaction.TerefdasPaylari = new List<PartnerShare>();
                }

                // ================================================================
                //  ✅ «ÖDƏNİLİB» EDİLDİKDƏ GECİKMƏ PULU BÖLÜNÜR ✓✓✓
                // ----------------------------------------------------------------
                //  ☑ «ödənilib» → pul GƏLDİ ✓ → qalıq tərəfdaşlar arasında
                //     YARI-YARIYA bölünür ✓ (Asif & Musa ✓)
                //  ☐ «ödənilməyib» → pul gəlməyib ✗ → bölgü SİLİNİR ✓ (yuxarıda ✓)
                // ================================================================
                else if (transaction.Nov == "Gecikmə"
                         && transaction.TerefdasPaylari.Count == 0)
                {
                    var qaliglar = Catalog.DefaultPartners
                        .Where(p => p.QaligPayi)
                        .ToList();

                    if (qaliglar.Count > 0)
                    {
                        var pay = Math.Round(transaction.Mebleg / qaliglar.Count, 2);
                        var yeniPaylar = new List<PartnerShare>();

                        for (var i = 0; i < qaliglar.Count; i++)
                        {
                            yeniPaylar.Add(new PartnerShare
                            {
                                Terefdas = qaliglar[i].Ad,
                                Faiz = 0m,
                                QaligPayi = true,
                                Aktiv = true,
                                Mebleg = i == qaliglar.Count - 1
                                    ? Math.Round(transaction.Mebleg - pay * i, 2)
                                    : pay,
                                Sira = i + 1
                            });
                        }

                        transaction.TerefdasBolguTetbiqOlunub = true;
                        transaction.TerefdasPaylari = yeniPaylar;

                        _logger.LogInformation(
                            "⏱ Gecikmə ödənildi ✓ → paylar BÖLÜNDÜ: {Mebleg:N2} ₼ → {Pay:N2} ₼ × {Say} qalıq tərəfdaş",
                            transaction.Mebleg, pay, qaliglar.Count);
                    }
                }

                await _creditService.UpdateTransactionAsync(transaction);
                _logger.LogInformation(
                    "Əməliyyat vəziyyəti dəyişdirildi: {Id} → {State}",
                    transaction.Id, transaction.Odenilib ? "ödənilib" : "gözləyir");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Əməliyyat vəziyyəti yenilənə bilmədi.");
                _dialogs.ShowError("Vəziyyət yadda saxlanıla bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }

            ApplyTransactionFilter();
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Ödəniş tarixinə uyğun <b>TAKSİT NÖMRƏSİNİ</b> hesablayır.
        /// <para>
        /// ⚠ <b>1-ci taksit = başlama tarixinin ÖZÜ</b> ✓ → nəticəyə <c>+1</c>
        /// əlavə olunur.
        /// </para>
        /// <para>
        /// <b>Əvvəlki xəta:</b> <c>+1</c> olmadığı üçün başlama tarixindən sonraki
        /// ilk ay <b>1-ci taksit</b> sayılırdı ✗. Məsələn kredit <b>14.05.2025</b>-də
        /// başlayır, ödəniş <b>13.06.2025</b>-də edilir → səhvən 1-ci taksitə
        /// (plan 14.05.2025) yazılırdı və <b>30 gün gecikmə</b> görünürdü ✗✓
        /// </para>
        /// </summary>
        private static int? ComputeInstallmentNo(Credit credit, DateTime date)
        {
            var months = ((date.Year - credit.BaslamaTarixi.Year) * 12)
                         + (date.Month - credit.BaslamaTarixi.Month)
                         + 1;   // ← 1-ci taksit = başlama tarixinin özü ✓

            return months >= 1 && months <= credit.MuddetAy ? months : null;
        }

        // ====================================================================
        //  📅 ƏVVƏLCƏDƏN ÖDƏNİŞ — TAKSİT SEÇİMİ ✓✓✓ (v6.2.19)
        // ====================================================================

        /// <summary>
        /// 📅 Taksit seçim siyahısını doldurur ✓✓✓ — hər taksit üçün
        /// <b>№ · plan tarixi · ödənilibmi</b> göstərilir ✓
        /// <para>
        /// ⚠ Ödənilmişlər <c>✅</c>, qalanlar <c>⬜</c> ilə işarələnir ✓ →
        /// istifadəçi <b>hansı ayın</b> ödənişi olduğunu dərhal görür ✓✓✓
        /// </para>
        /// </summary>
        private void TaksitSecimleriniYukle()
        {
            TaksitSecimleri.Clear();

            var credit = SelectedCredit;

            if (credit is null || credit.MuddetAy <= 0)
            {
                return;
            }

            // ---- Hansı taksitlər artıq ÖDƏNİLİB? ✓ ----
            var ödənilmiş = new HashSet<int>();

            foreach (var t in Transactions)
            {
                if (t.CreditId != credit.Id)
                {
                    continue;
                }

                if (!string.Equals(t.Nov, "Gəlir", StringComparison.Ordinal))
                {
                    continue;
                }

                var no = t.InstallmentNo ?? ComputeInstallmentNo(credit, t.Tarix) ?? 0;

                if (no > 0)
                {
                    ödənilmiş.Add(no);
                }
            }

            // ---- Bütün taksitlər siyahıya salınır ✓ ----
            for (var no = 1; no <= credit.MuddetAy; no++)
            {
                var planTarixi = credit.BaslamaTarixi.AddMonths(no - 1);
                var ödənilib = ödənilmiş.Contains(no);

                TaksitSecimleri.Add(new TaksitSecimi
                {
                    No = no,
                    PlanTarixi = planTarixi,
                    Odenilib = ödənilib,
                    Metn = $"{(ödənilib ? "✅" : "⬜")} {no}-ci taksit · {planTarixi:dd.MM.yyyy}" +
                           (ödənilib ? "  (ödənilib)" : "")
                });
            }

            // ---- AĞILLI DEFAULT ✓✓✓ ----
            //  ① Ödəniş tarixinə uyğun taksit götürülür ✓
            //  ② o ÖDƏNİLİBSƏ (əvvəlcədən ödəniş halı ✓) → ilk ÖDƏNİLMƏMİŞ
            //     taksit seçilir ✓✓✓ (istifadəçi tələbi: «gələn ayınkini ödədi» ✓)
            SecilmisTaksitNo = UygunTaksitNoTap();
        }

        /// <summary>
        /// 📅 Ödəniş üçün ən UYĞUN taksiti tapır ✓✓✓:
        /// tarixə uyğun taksit ödənilməyibsə o ✓, ödənilibsə → <b>ilk
        /// ödənilməmiş</b> taksit ✓ (əvvəlcədən ödəniş ✓)
        /// </summary>
        private int UygunTaksitNoTap()
        {
            var credit = SelectedCredit;

            if (credit is null || TaksitSecimleri.Count == 0)
            {
                return 0;
            }

            var tarixəGörə = ComputeInstallmentNo(credit, Tarix ?? DateTime.Today) ?? 0;

            var uygun = TaksitSecimleri.FirstOrDefault(x => x.No == tarixəGörə);

            if (uygun is not null && !uygun.Odenilib)
            {
                return uygun.No;
            }

            // ⚠ Tarixə uyğun taksit ÖDƏNİLİB ✗ (və ya tarix aralıqdan kənardır ✗)
            //   → ilk ÖDƏNİLMƏMİŞ taksit ✓✓✓ (əvvəlcədən ödəniş ✓)
            var ilkBos = TaksitSecimleri.FirstOrDefault(x => !x.Odenilib);

            return ilkBos?.No ?? uygun?.No ?? 0;
        }

        /// <summary>📅 Checkbox dəyişdikdə: siyahı hazırlanır + default taksit seçilir ✓✓✓</summary>
        partial void OnBasqaTaksitChanged(bool value)
        {
            if (value)
            {
                TaksitSecimleriniYukle();

                // ⚠ Siyahı boşdursa (kredit seçilməyib ✗) → checkbox geri söndürülür ✓
                if (TaksitSecimleri.Count == 0)
                {
                    BasqaTaksit = false;
                    return;
                }
            }

            OnPropertyChanged(nameof(TaksitSecimiGorunur));
            OnPropertyChanged(nameof(BasqaTaksitIzahi));
            OnPropertyChanged(nameof(TaksitInfo));
        }

        /// <summary>📅 Seçilmiş taksit dəyişdikdə izah yenilənir ✓</summary>
        partial void OnSecilmisTaksitNoChanged(int value)
        {
            OnPropertyChanged(nameof(BasqaTaksitIzahi));
            OnPropertyChanged(nameof(TaksitInfo));
        }

        /// <summary>Seçilmiş kredit və tarixə görə hansı taksitə düşdüyünü göstərir.</summary>
        public string TaksitInfo
        {
            get
            {
                if (SelectedCredit is null)
                {
                    return string.Empty;
                }

                // ================================================================
                //  📅 v6.2.19 — ƏVVƏLCƏDƏN ÖDƏNİŞ ✓✓✓
                //  İstifadəçi taksiti ÖZÜ seçibsə → o göstərilir ✓✓✓
                //  (məs. «📅 11-ci taksitin ödənişi (plan: 03.04.2024) — ƏVVƏLCƏDƏN ✓»)
                // ================================================================
                if (Nov == "Gəlir" && BasqaTaksit && SecilmisTaksitNo > 0)
                {
                    var plan = SelectedCredit.BaslamaTarixi.AddMonths(SecilmisTaksitNo - 1);

                    return $"📅 {SecilmisTaksitNo}-ci taksitin ödənişi " +
                           $"(plan: {plan:dd.MM.yyyy}) — ƏVVƏLCƏDƏN ✓";
                }

                if (Nov == "Möhlət")
                {
                    var mohletNo = ComputeInstallmentNo(SelectedCredit, Tarix ?? DateTime.Today);
                    var limit = MohletTarixi is DateTime d ? d.ToString("dd.MM.yyyy") : "seçilməyib";
                    return mohletNo is int m
                        ? $"⏸ {m}-ci taksit möhlətə verilir  →  son tarix: {limit}"
                        : "→ Bu tarix kredit müddətindən kənardır.";
                }

                if (Nov == "Gecikmə")
                {
                    var gecikmeNo = ComputeInstallmentNo(SelectedCredit, Tarix ?? DateTime.Today);
                    var limit = GecikmeTarixi is DateTime g ? g.ToString("dd.MM.yyyy") : "seçilməyib";
                    return gecikmeNo is int gm
                        ? $"⚠ {gm}-ci taksit gecikmişdir  →  bağlanma tarixi: {limit}"
                        : "→ Bu tarix kredit müddətindən kənardır.";
                }

                if (Nov != "Gəlir")
                {
                    return "Xərc qeydi (taksitə bağlanmır).";
                }

                var no = ComputeInstallmentNo(SelectedCredit, Tarix ?? DateTime.Today);
                return no is int value
                    ? $"→ {value}-ci taksit  (plan tarixi: {SelectedCredit.BaslamaTarixi.AddMonths(value - 1):dd.MM.yyyy})"
                    : "→ Bu tarix kredit müddətindən kənardır.";
            }
        }

        partial void OnSelectedCreditChanged(Credit? value)
        {
            OnPropertyChanged(nameof(TaksitInfo));
            ApplyTransactionFilter();

            // ================================================================
            //  🔢 ★ v6.2.22 — KÖK PAYI YENİDƏN HESABLANIR ★✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ İSTİFADƏÇİ ŞİKAYƏTİ:
            //    «Birinci GƏLİR məbləğini yazanda (məs. 654 ₼) tərəfdaş
            //     bölgüsündə BUTÖV bölünür ✗ (kök çıxılmır ✗).
            //     Sonra maşını seçəndə də köhnə qalır ✗. Amma ƏVVƏL maşını
            //     seçib SONRA məbləği yazanda DÜZGÜN işləyir ✓.»
            // ----------------------------------------------------------------
            //  ⚠ ƏSL SƏBƏB: məbləğ YAZILANDA `SelectedCredit` hələ NULL idi ✗
            //    → `BolguBazasiHesabla` məbləği OLDUĞU KİMİ qaytarırdı ✗
            //    (kök çıxılmırdı ✗) → bütün 654 ₼ bölünürdü ✗✓✓
            //    Sonra kredit seçiləndə isə YENİDƏN HESABLANMIRDI ✗✓✓
            //  ✅ İNDİ: kredit seçiləndə/kəmi dəyişəndə baza AVTOMATİK
            //    yenidən hesablanır ✓ → kök çıxılır ✓ → qalan hissə bölünür ✓✓✓
            // ================================================================
            if (value is not null && BolguTetbiqOlunub && Mebleg > 0m && Nov == "Gəlir")
            {
                BolguBazasi = BolguBazasiHesabla(Mebleg);
                BolguHesabla();
                BolguYenile();
            }

            // ================================================================
            //  📅 v6.2.19 — Kredit dəyişdikdə «əvvəlcədən ödəniş» SIFIRLANIR ✓✓✓
            //  (köhnə kreditin taksit siyahısı qalmasın ✗)
            // ================================================================
            OnPropertyChanged(nameof(IsBasqaTaksitGorunur));
            OnPropertyChanged(nameof(TaksitSecimiGorunur));

            if (value is null)
            {
                SecilmisTaksitNo = 0;
                BasqaTaksit = false;
                TaksitSecimleri.Clear();
            }
            else if (BasqaTaksit)
            {
                // ☑ rejim açıqdırsa → yeni kreditin taksitləri yüklənir ✓
                TaksitSecimleriniYukle();
            }

            if (value is not null)
            {
                // Seçimdən sonra tam ad yazılır; filtr bütün siyahını göstərir.
                CreditSearchText = value.DisplayText;

                // ============================================================
                //  📤 «TRANSFER OLUNMAQ» → MƏBLƏĞ AVTOMATİK = NİSYƏ QİYMƏTİ ✓
                //  ⚠️ Növ ƏVVƏL seçilibsə də işləyir ✓ (əvvəl yalnız `Nov`
                //     dəyişəndə dolurdu ✗ → indi KREDİT seçiləndə də dolur ✓✓✓)
                // ============================================================
                if (IsTransferOlunmaq)
                {
                    if (Mebleg <= 0m)
                    {
                        Mebleg = value.Kreditlesdirilen;
                    }

                    TransferSexsleriniYenile();
                }

                if (IsBarter)
                {
                    BarterSexsleriniYenile();
                }
            }
        }

        partial void OnSelectedTransactionChanged(CreditTransaction? value)
        {
            _ = LoadAttachmentsAsync();

            AttachmentTitle = value is null
                ? "📎 Fayl əlavə etmək üçün cədvəldən əməliyyat seçin"
                : $"📎 Sənəd / çek — {value.Nov} {value.Mebleg:N2} ₼ ({value.Tarix:dd.MM.yyyy})";
        }

        partial void OnSelectedAttachmentChanged(MediaAttachment? value)
        {
            AttachmentInfo = value is null
                ? "Fayl seçilməyib"
                : $"{value.FileName}  ·  {value.Extension}  ·  {value.SizeText}"
                  + (value.Exists ? string.Empty : "  ·  ⚠ fayl tapılmadı");
        }

        /// <summary>Seçilmiş əməliyyata bağlı faylları yükləyir.</summary>
        public async Task LoadAttachmentsAsync()
        {
            Attachments.Clear();
            SelectedAttachment = null;

            if (SelectedTransaction is null || SelectedTransaction.Id <= 0)
            {
                return;
            }

            try
            {
                var items = await _media.GetAsync(MediaRefTypes.CreditTransaction, SelectedTransaction.Id);
                foreach (var item in items)
                {
                    Attachments.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Əməliyyat faylları yüklənə bilmədi.");
            }
        }

        /// <summary>Seçilmiş əməliyyata fayl(lar) — çek, qaimə, sənəd — əlavə edir.</summary>
        [RelayCommand]
        private async Task AddFilesAsync()
        {
            if (SelectedTransaction is null || SelectedTransaction.Id <= 0)
            {
                _dialogs.ShowWarning("Fayl əlavə etmək üçün cədvəldən əməliyyat seçin.");
                return;
            }

            var files = _dialogs.ShowOpenFilesDialog(_media.FileFilter);
            if (files is null || files.Length == 0)
            {
                return;
            }

            try
            {
                IsBusy = true;
                var count = await _media.AddAsync(MediaRefTypes.CreditTransaction, SelectedTransaction.Id, files);
                await LoadAttachmentsAsync();

                if (count > 0)
                {
                    _dialogs.ShowInfo($"{count} fayl əlavə edildi.", "Əlavə edildi");
                }
                else
                {
                    _dialogs.ShowWarning("Fayl əlavə edilə bilmədi.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fayl əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Fayl əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Əməliyyatın fayllarının saxlandığı qovluğu birbaşa açır.</summary>
        [RelayCommand]
        private void OpenFolder()
        {
            if (SelectedTransaction is null || SelectedTransaction.Id <= 0)
            {
                _dialogs.ShowWarning("Qovluğu açmaq üçün cədvəldən əməliyyat seçin.");
                return;
            }

            if (!_media.OpenFolder(MediaRefTypes.CreditTransaction, SelectedTransaction.Id))
            {
                _dialogs.ShowWarning("Qovluq açıla bilmədi.");
            }
        }

        /// <summary>Seçilmiş faylı əməliyyat sistemi ilə açır.</summary>
        [RelayCommand]
        private void OpenAttachment()
        {
            if (SelectedAttachment is null)
            {
                _dialogs.ShowWarning("Açmaq üçün fayl seçin.");
                return;
            }

            if (!_media.OpenWithShell(SelectedAttachment))
            {
                _dialogs.ShowWarning("Fayl açıla bilmədi — diskdə mövcud olmaya bilər.");
            }
        }

        /// <summary>Seçilmiş faylı həm diskdən, həm bazadan silir.</summary>
        [RelayCommand]
        private async Task DeleteAttachmentAsync()
        {
            if (SelectedAttachment is null)
            {
                _dialogs.ShowWarning("Silmək üçün fayl seçin.");
                return;
            }

            var attachment = SelectedAttachment;
            if (!_dialogs.Confirm($"\"{attachment.FileName}\" faylını silmək istəyirsiniz?", "Faylı sil"))
            {
                return;
            }

            try
            {
                IsBusy = true;
                await _media.DeleteAsync(attachment.Id);
                await LoadAttachmentsAsync();
                _dialogs.ShowInfo("Fayl silindi.", "Silindi");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fayl silinərkən xəta baş verdi.");
                _dialogs.ShowError("Fayl silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Cədvəlin məzmununu təyin edir.
        /// Müqaviləyə avtomobil bağlanıbsa — yalnız HƏMİN AVTOMOBİLİN tarixçəsi,
        /// bağlanmayıbsa — BÜTÜN avtomobillərin kredit ödəniş/xərc tarixçəsi.
        /// </summary>
        private void ApplyTransactionFilter()
        {
            var carId = SelectedCredit?.CarId;

            // ⚡ Siyahı yığılır ✓ — sonra TƏK bildirişlə yazılır ✓✓✓
            //   (minlərlə əməliyyatda cədvəl min dəfə yenidən qurulmur ✗)
            var seçilmişlər = carId.HasValue
                ? Transactions.Where(t => t.CreditId == SelectedCredit!.Id).ToList()
                : Transactions.ToList();

            FilteredTransactions.ReplaceAll(seçilmişlər);

            TransactionsInfo = carId.HasValue
                ? $"🚗 {SelectedCredit?.Car?.DisplayName ?? "Avtomobil"} — bu avtomobilin kredit tarixçəsi ({FilteredTransactions.Count} qeyd)"
                : $"📋 Bütün avtomobillərin kredit əməliyyatları — {FilteredTransactions.Count} qeyd (müqaviləyə avtomobil bağlanmayıb)";
        }

        partial void OnCreditSearchTextChanged(string value) => ApplyCreditFilter();

        /// <summary>
        /// Yazıldıqca kredit siyahısını filtrləyir (müqavilə №, müştəri, maşın nömrəsi, marka-model).
        /// </summary>
        private void ApplyCreditFilter()
        {
            if (_applyingFilter)
            {
                return;
            }

            _applyingFilter = true;
            try
            {
                var search = CreditSearchText?.Trim() ?? string.Empty;

                // Yazılan mətn seçilmiş elementi tam təsvir edirsə, bütün siyahı göstərilir.
                if (SelectedCredit is not null &&
                    string.Equals(search, SelectedCredit.DisplayText, StringComparison.OrdinalIgnoreCase))
                {
                    search = string.Empty;
                }

                var matches = (string.IsNullOrEmpty(search)
                    ? Credits
                    : Credits.Where(c => c.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                // Nəticə əvvəlki ilə eynidirsə siyahını yenidən qurmuruq.
                if (matches.Count == FilteredCredits.Count && matches.SequenceEqual(FilteredCredits))
                {
                    return;
                }

                FilteredCredits.Clear();
                foreach (var credit in matches)
                {
                    FilteredCredits.Add(credit);
                }
            }
            finally
            {
                _applyingFilter = false;
            }
        }

        // ====================================================================
        //  TƏRƏFDAŞ BÖLGÜSÜ  -  əmrlər və hesablama
        // ====================================================================

        /// <summary>
        /// Bölgünü yenidən hesablayır: faiz payları hesablanır, qalan məbləğ
        /// qalıq payçıları (Asif &amp; Musa) arasında YARI-YARIYA bölünür.
        /// </summary>
        [RelayCommand]
        private void BolguHesabla()
        {
            var modeller = new List<PartnerShare>();
            var sira = 0;

            foreach (var row in TerefdasPaylari)
            {
                modeller.Add(row.ToModel(sira++));
            }

            PartnerMath.Distribute(BolguBazasi, modeller);

            for (var i = 0; i < modeller.Count && i < TerefdasPaylari.Count; i++)
            {
                TerefdasPaylari[i].Mebleg = modeller[i].Mebleg;
            }

            BolguYenile();
        }

        /// <summary>
        /// ✅ <b>BAZA DƏYİŞDİKDƏ BÖLGÜ AVTOMATİK YENİLƏNİR</b> ✓✓✓
        /// <para>
        /// ⚠ ƏVVƏLKİ SƏHV: baza (məs. 55 230 ₼) dəyişirdi ✗, paylar isə köhnə
        /// bazadan (55 225 ₼) qalırdı ✗ → <b>5,00 ₼ «eksik»</b> görünürdü ✗✓✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: baza hər dəyişəndə <see cref="BolguHesabla"/> işə düşür ✓ →
        /// payların cəmi HƏMİŞƏ bazaya TAM bərabərdir ✓✓✓
        /// </para>
        /// </summary>
        partial void OnBolguBazasiChanged(decimal value)
        {
            if (!IsBolguPaneli)
            {
                return;
            }

            BolguHesabla();      // ✅ paylar dərhal yenidən bölünür ✓✓✓
        }

        /// <summary>
        /// BÖLGÜ BAZASI = <b>yazılan məbləğ − KÖK (ƏSAS BORC) PAYI</b> ✓✓✓ (v6.2.17)
        /// <para>
        /// «Kredit əlavə gəlir» qeydində məbləğ yazılanda həmin məbləğin içindən
        /// <b>KÖK payı çıxılır</b> ✓ — yalnız qalan hissə (yəni <b>FAİZ
        /// mənfəəti</b>) tərəfdaşlar arasında bölünür ✓✓✓
        /// </para>
        /// <para>
        /// <b>★ İstifadəçi tələbi:</b> «Nisyəni böləndə, necə ki aylıq kredit
        /// veririksə, oradan aylıqlardakı KÖK QİYMƏTİ tapırıq. Ödənişdən KÖK
        /// QİYMƏTİ ÇIXARILMALIDIR, SONRA BÖLÜNMƏLİDİR.» ✓✓✓
        /// </para>
        /// <example>
        /// KÖK (kreditləşdirilən) 17 014 ₼ · Qiymət 23 819,60 ₼ · 12 ay
        /// <code>
        /// Ödəniş yazıldı        : 1 984,97 ₼
        /// KÖK payı çıxıldı      : −1 417,83 ₼   (aylıq kök ✓)
        /// ─────────────────────────────────────
        /// BÖLGÜ BAZASI (faiz)   :    567,14 ₼   ← YALNIZ BU BÖLÜNÜR ✓✓✓
        /// </code>
        /// <para>
        /// ⚠ <b>QALAN əsas borc nəzərə alınır</b> ✓✓✓ — KÖK tam ödənilibsə
        /// (qalıq 0 ₼) → ödənişin <b>HAMISI</b> mənfəət sayılır ✓ → tam bölünür ✓
        /// (əvvəl bu halda da «aylıq kök» çıxılırdı ✗ → az bölünürdü ✗✓✓)
        /// </para>
        /// </example>
        /// </summary>
        private decimal BolguBazasiHesabla(decimal mebleg)
        {
            var credit = SelectedCredit;

            if (credit is null || mebleg <= 0m)
            {
                return mebleg;
            }

            // ================================================================
            //  ✅ ★ v6.2.24 — MƏNFƏƏT PLAN (AYLIQ) İLƏ MƏHDUDLAŞDIRILIR ★
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏLKİ SƏHV ✗✓✓: baza = mebleg − (mebleg × KÖK/qiymət) ✗ →
            //    müştəri aylıqdan ARTIQ ödəyəndə (654 yerinə 815 ₼ ✗) mənfəət
            //    AVTOMATİK ARTIRDI ✗ → tərəfdaşlar daha çox pay alırdılar ✗
            //  ✅ İNDİ: mənfəət YALNIZ plan (aylıq taksit) hissəsindən hesablanır ✓
            //    → plandan artıq ödəniş TAMAMİLƏ kökə (mayaya) gedir ✓✓✓
            //    (654 ₼ → mənfəət 327 ₼ · 815 ₼ → mənfəət YENƏ 327 ₼ ✓✓✓)
            // ================================================================
            return credit.OdenisMenfeetBazasi(mebleg, OdenilmisKokHesabla());
        }

        /// <summary>
        /// ✅ <b>ÖDƏNİLMİŞ KÖK</b> — seçilmiş kredit üzrə artıq qaytarılmış əsas
        /// borc (₼) ✓✓✓ (v6.2.17)
        /// <para>
        /// <c>«Gəlir»</c> tipli bütün ödənişlər TARİX SIRASI ilə götürülür ✓ →
        /// hər ödənişin KÖK payı cəmlənir ✓ → cəm əsas borcu <b>AŞA BİLMƏZ</b> ✗✓✓
        /// </para>
        /// </summary>
        private decimal OdenilmisKokHesabla()
        {
            var credit = SelectedCredit;

            if (credit is null)
            {
                return 0m;
            }

            var cem = 0m;

            var ödənişlər = Transactions
                .Where(t => t.CreditId == credit.Id
                            && string.Equals(t.Nov, "Gəlir", StringComparison.Ordinal)
                            && t.Odenilib)
                .OrderBy(t => t.Tarix)
                .ThenBy(t => t.Id);

            foreach (var t in ödənişlər)
            {
                cem += credit.OdenisKokPayi(t.Mebleg, cem);
            }

            return Math.Round(cem, 2);
        }

        /// <summary>
        /// Bölgü bazasını hesablayır: yazılan məbləğdən ƏSAS BORC çıxılır
        /// («📐 Məbləğdən götür» düyməsi).
        /// </summary>
        [RelayCommand]
        private void BolguBazaniMeblegdenGot()
        {
            BolguBazasi = BolguBazasiHesabla(Mebleg);
            BolguHesabla();
        }

        /// <summary>
        /// Faiz dərəcələrini STANDART qiymətlərə qaytarır:
        /// Zaur %6 · Eşqin %5 · Asiman %5 · Asif &amp; Musa qalıq payı.
        /// </summary>
        [RelayCommand]
        private void BolguDefaultFaizler()
        {
            var standart = PartnerMath.CreateDefaultRows();

            foreach (var row in TerefdasPaylari)
            {
                var uygun = standart.FirstOrDefault(d => d.Terefdas == row.Terefdas);
                if (uygun is not null)
                {
                    row.Faiz = uygun.Faiz;
                    row.QaligPayi = uygun.QaligPayi;
                }
            }

            BolguHesabla();
        }

        /// <summary>
        /// Tərəfdaş sətirlərini standart hala gətirir (yeni əməliyyat üçün).
        /// <para>
        /// ✅ <b>Avtomatik vəziyyət:</b>
        /// <list type="bullet">
        ///   <item>Tərəfdaş mənfəət bölgüsü <b>AVTOMATİK İŞARƏLƏNİR</b> (aktiv) ✓</item>
        ///   <item>Yalnız <b>Musa &amp; Asif</b> (qalıq payçıları) işarəli ✓</item>
        ///   <item>Zaur / Eşqin / Asiman — <b>işarəsiz</b> ✗</item>
        /// </list>
        /// </para>
        /// <para>
        /// Əvvəl bölgü <c>false</c> (söndürülmüş) başlayırdı ✗ və istifadəçi hər dəfə
        /// əl ilə işarələməli olurdu ✗.
        /// </para>
        /// </summary>
        private void BolguSifirla()
        {
            TerefdasPaylari.Clear();

            // ✅ Yalnız QALIQ PAYÇILARI (Musa & Asif) aktiv ✓, faiz payçıları işarəsiz ✗
            foreach (var share in PartnerMath.CreateDefaultRows(yalnizQaligPaycilari: true))
            {
                TerefdasPaylari.Add(PartnerPayRow.FromModel(share));
            }

            // ⚠ Əvvəl söndürülür ki, aşağıdaki «true» DƏYİŞİKLİK saysın və
            //   `OnBolguTetbiqOlunubChanged` avtomatik hesablamanı işə salsın ✓
            BolguTetbiqOlunub = false;
            BolguBazasi = 0m;
            BolguSifirlaYardimci();
        }

        /// <summary>
        /// Bölgünü <b>AVTOMATİK AKTİV</b> edir ✓ — məbləğ artıq yazılıbsa bölgü
        /// dərhal hesablanır ✓ (əvvəl istifadəçi checkbox-ı əl ilə işarələməli idi ✗).
        /// </summary>
        private void BolguSifirlaYardimci()
        {
            // «true» yazmaq `OnBolguTetbiqOlunubChanged`-i işə salır ✓ →
            // o da `Mebleg > 0` olduqda bazanı hesablayıb BÖLGÜNÜ AVTOMATİK qurur ✓
            BolguTetbiqOlunub = true;

            BolguYenile();
        }

        /// <summary>Bölgü ilə bağlı hesablanmış göstəriciləri UI-a bildirir.</summary>
        private void BolguYenile()
        {
            OnPropertyChanged(nameof(BolguCemi));
            OnPropertyChanged(nameof(BolguFergi));
            OnPropertyChanged(nameof(BolguXulase));
            OnPropertyChanged(nameof(BolguFergiMetni));
            OnPropertyChanged(nameof(BolguFergiRengi));

            // 🔢 KÖK izahı da yenilənir ✓✓✓ (v6.2.17)
            OnPropertyChanged(nameof(BolguKokPayi));
            OnPropertyChanged(nameof(BolguOdenilmisKok));
            OnPropertyChanged(nameof(BolguKokMetni));
        }

        /// <summary>Checkbox dəyişdikdə vəziyyət mətni də yenilənir.</summary>
        partial void OnBolguTetbiqOlunubChanged(bool value)
        {
            OnPropertyChanged(nameof(BolguVeziyyetMetni));

            // Bölgü ilk dəfə açılırsa və baza boşdursa — avtomatik hesablanır:
            // yazılan məbləğdən ƏSAS BORC çıxılır (yalnız faiz bölünür).
            if (value && BolguBazasi <= 0m && Mebleg > 0m)
            {
                BolguBazasi = BolguBazasiHesabla(Mebleg);
                BolguHesabla();
            }
        }

        /// <summary>
        /// Bölgü tətbiq olunmursa <b>boş</b> siyahı qaytarır — beləliklə checkbox
        /// söndürüləndə əvvəlki paylar tamamilə silinir.
        /// </summary>
        private List<PartnerShare> BolguModelleri()
        {
            if (!BolguTetbiqOlunub || Nov != "Gəlir")
            {
                // ================================================================
                //  ⚠️ GECİKMƏ (cərimə) → TƏRƏFDAŞLARA BÖLÜNMÜR ✗✓✓ (v6.2.28)
                // ----------------------------------------------------------------
                //  Cərimə pulu YALNIZ «💵 Kassa»ya (gəlirə) yazılır ✓
                //  (bax: <see cref="KassaHesabi"/> — «Gecikmə + Ödənilib» ✓)
                //  → Tərəfdaş payı YARADILMIR ✗ · köhnə paylar da TƏMİZLƏNİR ✓
                // ================================================================
                return new List<PartnerShare>();
            }

            var nəticə = new List<PartnerShare>();
            var sira = 0;

            foreach (var row in TerefdasPaylari)
            {
                nəticə.Add(row.ToModel(sira++));
            }

            return nəticə;
        }

        /// <summary>
        /// MƏBLƏĞ dəyişdi — bölgü tətbiq olunursa BAZA <b>HƏR DƏFƏ</b> avtomatik
        /// yenilənir: yazılan məbləğdən ƏSAS BORC çıxılır (yalnız faiz bölünür).
        /// <para>
        /// Əvvəllər baza yalnız BİR DƏFƏ (checkbox işarələnəndə) yazılırdı —
        /// sonra məbləği dəyişdikdə yenilənmirdi. İndi məbləğ hər yazıldıqda
        /// (Enter / fokus dəyişməsi) düstur tətbiq olunur.
        /// </para>
        /// </summary>
        partial void OnMeblegChanged(decimal value)
        {
            if (Nov != "Gəlir")
            {
                return;
            }

            // Bölgü aparılmayıbsa heç nə etmirik.
            if (!BolguTetbiqOlunub)
            {
                return;
            }

            if (value <= 0m)
            {
                BolguBazasi = 0m;
                BolguYenile();      // 🔢 KÖK izahı da yenilənsin ✓ (v6.2.17)
                return;
            }

            BolguBazasi = BolguBazasiHesabla(value);
            BolguHesabla();
            BolguYenile();          // 🔢 KÖK izahı da yenilənsin ✓✓✓ (v6.2.17)
        }

        /// <summary>
        /// NÖV dəyişdi — «Gəlir» seçilibsə və məbləğ varsa, bölgü bazası
        /// avtomatik hesablanır (əvvəlki düsturla).
        /// </summary>
        partial void OnNovChanged(string value)
        {
            OnPropertyChanged(nameof(TaksitInfo));
            OnPropertyChanged(nameof(IsMohlet));
            OnPropertyChanged(nameof(IsGecikme));
            OnPropertyChanged(nameof(IsGelir));
            OnPropertyChanged(nameof(IsErkenBaglama));   // 📕 vaxtından tez bağlama ✓
            OnPropertyChanged(nameof(IsBolguPaneli));    // 👥 bölgü paneli görünüşü ✓
            // 📤🤝 YENİ NÖVLƏR — «Transfer olunmaq» · «Barter» · «Barter köhnə» ✓
            OnPropertyChanged(nameof(IsTransferOlunmaq));
            OnPropertyChanged(nameof(IsBarter));
            OnPropertyChanged(nameof(BarterTransferIzahi));

            // ================================================================
            //  📅 v6.2.19 — «ƏVVƏLCƏDƏN ÖDƏNİŞ» YALNIZ «GƏLİR» ÜÇÜNDÜR ✓✓✓
            //  Növ dəyişəndə checkbox gizlədilir ✓ və söndürülür ✓
            // ================================================================
            OnPropertyChanged(nameof(IsBasqaTaksitGorunur));
            OnPropertyChanged(nameof(TaksitSecimiGorunur));

            if (value != "Gəlir" && BasqaTaksit)
            {
                BasqaTaksit = false;      // → OnBasqaTaksitChanged də siyahını təmizləyir ✓
            }

            // ================================================================
            //  🔢 ★ v6.2.22 — «GƏLİR» SEÇİLƏNDƏ KÖK PAYI YENİDƏN HESABLANIR ★✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ Əgər istifadəçi ƏVVƏL məbləği ✗ (və ya krediti ✗) yazıb,
            //    SONRA «Gəlir» növünü seçibsə → baza köhnə (kök çıxılmamış ✗)
            //    qalırdı ✗✓✓ → BÜTÜN məbləğ bölünürdü ✗ (istifadəçi şikayəti ✓)
            //  ✅ İNDİ: növ də seçiləndə baza avtomatik düzəldilir ✓ →
            //    ödənişdən KÖK çıxılır ✓ → yalnız FAİZ bölünür ✓✓✓
            // ================================================================
            if (value == "Gəlir" && BolguTetbiqOlunub && Mebleg > 0m && SelectedCredit is not null)
            {
                BolguBazasi = BolguBazasiHesabla(Mebleg);
                BolguHesabla();
                BolguYenile();
            }

            // ================================================================
            //  📤 «TRANSFER OLUNMAQ» → MƏBLƏĞ AVTOMATİK = MAŞININ NİSYƏ QİYMƏTİ ✓
            // ----------------------------------------------------------------
            //  Seçilən kreditin «Kreditləşdirilən» məbləği avtomatik yazılır ✓ —
            //  istifadəçi İSTƏSƏ əl ilə DƏYİŞƏ BİLƏR ✓✓✓
            // ================================================================
            if (value == "Transfer olunmaq" && SelectedCredit is not null)
            {
                if (Mebleg <= 0m)
                {
                    Mebleg = SelectedCredit.Kreditlesdirilen;
                }

                TransferSexsleriniYenile();
            }

            if (value == "Barter" || value == "Barter köhnə")
            {
                BarterSexsleriniYenile();
            }

            // Növ «Gəlir» olub bölgü açıqdırsa — baza dərhal hesablanır.
            if (value == "Gəlir" && BolguTetbiqOlunub && Mebleg > 0m)
            {
                BolguBazasi = BolguBazasiHesabla(Mebleg);
                BolguHesabla();
            }
        }

        partial void OnMohletTarixiChanged(DateTime? value) => OnPropertyChanged(nameof(TaksitInfo));

        partial void OnGecikmeTarixiChanged(DateTime? value) => OnPropertyChanged(nameof(TaksitInfo));

        partial void OnTarixChanged(DateTime? value) => OnPropertyChanged(nameof(TaksitInfo));

        private void UpdateStats()
        {
            TotalIncome = Transactions.Where(t => t.Nov == "Gəlir").Sum(t => t.Mebleg);
            TotalExpense = Transactions.Where(t => t.Nov == "Xərc").Sum(t => t.Mebleg);
            Net = TotalIncome - TotalExpense;
        }
    }
}
