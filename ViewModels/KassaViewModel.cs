using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Collections;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// 💵 <b>«KASSA» TABININ VIEWMODEL-i</b> — kassaya <b>DAXİL OLAN</b> ✓ və
    /// kassadan <b>ÇIXAN</b> ✗ bütün pulun VAHİD jurnalı ✓✓✓
    /// <para>
    /// <b>İSTİFADƏÇİNİN TƏLƏBİ:</b> «kassa ayrı bölmə olsun — həm kreditə möhlət
    /// yazmaq, həm də ümumi nisyə satış; hər şey bir yerdə görünsün» ✓
    /// </para>
    /// <para>
    /// <b>⚙️ DÜSTUR TƏKRARLANMIR</b> ✗ — bütün rəqəmlər <see cref="KassaHesabi"/>-dən
    /// gəlir ✓ → <b>Kassa tabı</b> ✓ <b>Maliyyə Paneli</b> ✓ <b>Veb Dashboard</b> ✓
    /// HƏMİŞƏ ÜST-ÜSTƏ DÜŞÜR ✓✓✓
    /// </para>
    /// <list type="number">
    ///   <item>📒 <b>Jurnal</b> — dövrdəki bütün avtomatik + əl ilə hərəkətlər ✓</item>
    ///   <item>⏳ <b>Möhlətlər</b> — «gözlənilən daxilolma» ✓ və «⚠ gecikmiş» ✓
    ///         + <b>ÖDƏNİLDİ</b> düyməsi (basıldıqda pul kassaya DAXİL OLUR ✓✓✓)</item>
    /// </list>
    /// </summary>
    public sealed partial class KassaViewModel : ObservableObject
    {
        private readonly IKassaService _kassa;
        private readonly IMohletService _mohletler;

        /// <summary>📕 Kredit xidməti — möhlət ödənişini KREDİTƏ də yazmaq üçün ✓✓✓</summary>
        private readonly ICreditService _credits;

        /// <summary>🧾 Satış xidməti — nisyə satış möhlətinin MAYA-sı üçün ✓ (v6.2.78)</summary>
        private readonly ISaleService _satislar;
        private readonly IDialogService _dialogs;
        private readonly ILogger<KassaViewModel> _logger;

        /// <summary>
        /// 📦 v6.2.78 — Kredit/satış KEŞİ ✓ (möhlətin maya və mənfəətini
        /// <b>sinxron</b> hesablamaq üçün — hər klikdə bazaya sorğu getmir ✗✓✓)
        /// </summary>
        private List<Credit> _kreditKeshi = new();
        private List<Sale> _satisKeshi = new();

        /// <summary>Eyni anda iki yükləmənin işləməsinin qarşısını alır ✓.</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>
        /// 📢 <b>v6.2.79 — MƏLUMAT DƏYİŞDİ HADİSƏSİ</b> ✓✓✓
        /// <para>
        /// ⚠ <b>SƏBƏB:</b> Kassa tabında möhlət ödənildikdə tərəfdaş payları
        /// bazaya yazılırdı ✓, amma <b>«👥 Tərəfdaşlar» tabı YENİLƏNMİRDİ</b> ✗ →
        /// istifadəçi payları GÖRMÜRDÜ ✗✓✓ (şikayət: «insanların üzərinə gəlmədi»)
        /// </para>
        /// <para>
        /// ✅ <c>MainViewModel</c> bu hadisəyə abunədir ✓ → bütün tablar
        /// «çirkli» işarələnir ✓ və aktiv tab dərhal yenilənir ✓✓✓
        /// </para>
        /// </summary>
        public event EventHandler? DataChanged;

        /// <summary>Filtrləmə zamanı geri-əlaqə (re-entrancy) dövrünün qarşısını alır ✓.</summary>
        private bool _filtrlenir;

        /// <summary>Son hesabatın XAM sətirləri (filtr tətbiq olunmamış ✓✓✓).</summary>
        private List<KassaSetiri> _butunSetirler = new();

        public KassaViewModel(
            IKassaService kassa,
            IMohletService mohletler,
            ICreditService credits,
            ISaleService satislar,
            IDialogService dialogs,
            ILogger<KassaViewModel> logger)
        {
            _kassa = kassa;
            _mohletler = mohletler;
            _credits = credits;
            _satislar = satislar;
            _dialogs = dialogs;
            _logger = logger;

            // ================================================================
            //  🔁 «➕ Daxilolma / ➖ Xərc» dəyişdikdə KATEQORİYA siyahısı dəyişir ✓
            //     (daxilolma kateqoriyaları ✗ xərc kateqoriyaları ilə qarışmasın ✓✓✓)
            // ================================================================
            YeniHereket.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(KassaHereket.Nov))
                {
                    return;
                }

                OnPropertyChanged(nameof(Kateqoriyalar));

                // ⚙️ Köhnə seçim yeni siyahıda yoxdursa → ilk kateqoriya seçilir ✓
                if (!Kateqoriyalar.Contains(YeniHereket.Kateqoriya))
                {
                    YeniHereket.Kateqoriya = Kateqoriyalar[0];
                }
            };

            // ================================================================
            //  👥 v6.2.77 — MÖHLƏTİN TƏRƏFDAŞ BÖLGÜSÜ sətirləri DƏRHAL hazır ✓
            //  (digər tablarda olduğu kimi — cədvəl boş açılmır ✗✓✓)
            // ================================================================
            MohletBolguSifirla();
        }

        // ====================================================================
        //  🗓️ DÖVR FİLTRİ  (Maliyyə Paneli ilə EYNİ seçimlər ✓✓✓)
        // ====================================================================
        public IReadOnlyList<string> RangeOptions { get; } = new[]
        {
            "Bu gün", "Bu həftə", "Son 7 gün", "Bu ay", "Son 30 gün",
            "Keçən ay", "Bu il", "Son 12 ay", "Bütün vaxt", "Fərdi aralıq"
        };

        [ObservableProperty] private string selectedRange = "Bu ay";
        [ObservableProperty] private DateTime? customFrom = DateTime.Today.AddDays(-29);
        [ObservableProperty] private DateTime? customTo = DateTime.Today;

        /// <summary>Fərdi aralıq seçilibmi? (yalnız onda tarixlər aktivdir ✓)</summary>
        public bool IsCustomRange => SelectedRange == "Fərdi aralıq";

        partial void OnSelectedRangeChanged(string value)
        {
            OnPropertyChanged(nameof(IsCustomRange));
            _ = LoadAsync();
        }

        partial void OnCustomFromChanged(DateTime? value)
        {
            if (IsCustomRange) _ = LoadAsync();
        }

        partial void OnCustomToChanged(DateTime? value)
        {
            if (IsCustomRange) _ = LoadAsync();
        }

        /// <summary>«01.09.2026 → 29.09.2026» — hansı dövr göstərilir ✓.</summary>
        [ObservableProperty] private string dovrMetni = "—";

        // ====================================================================
        //  📊 XÜLASƏ RƏQƏMLƏRİ  (hamısı KassaHesabi-dən ✓ — təkrar düstur YOX ✗)
        // ====================================================================
        [ObservableProperty] private decimal acilisQaligi;
        [ObservableProperty] private decimal daxilolma;
        [ObservableProperty] private decimal xerc;
        [ObservableProperty] private decimal xalis;
        [ObservableProperty] private decimal qaliq;
        [ObservableProperty] private decimal proqnozQaliq;
        [ObservableProperty] private int setirSayi;

        // ---------- DAXİLOLMALARIN BÖLMƏLƏRİ ----------
        [ObservableProperty] private decimal satisDaxilolma;
        [ObservableProperty] private decimal ilkinOdenisDaxilolma;
        [ObservableProperty] private decimal kreditDaxilolma;
        [ObservableProperty] private decimal mohletDaxilolma;
        [ObservableProperty] private decimal elIleDaxilolma;

        // ---------- XƏRCLƏRİN BÖLMƏLƏRİ ----------
        [ObservableProperty] private decimal xercCemi;
        [ObservableProperty] private decimal kreditXerci;

        /// <summary>
        /// 👥 Tərəfdaşlara <b>FAKTİKİ VERİLƏN</b> pul (dövr üzrə ✓) — yalnız
        /// «Tərəfdaşlar» tabındaki «pul ver» əməliyyatları ✗✓✓
        /// </summary>
        [ObservableProperty] private decimal terefdasOdenilen;

        [ObservableProperty] private decimal elIleXerci;

        // ---------- 👥 TƏRƏFDAŞ PAYLARI (kassada QALAN pul ✓) ----------

        /// <summary>
        /// 👥 Dövrdə <b>hesablanan</b> tərəfdaş payı (₼) — pul KASSADA QALIR ✓✓✓
        /// (xərcə düşmür ✗ — bax: «ÖDƏNİLMƏLİ QALIQ» ✓)
        /// </summary>
        [ObservableProperty] private decimal terefdasPayi;

        /// <summary>
        /// 👥 Hələ tərəfdaşlara <b>verilməli</b> olan məbləğ (bütün vaxt üzrə, ₼) —
        /// bu pul HƏLƏ KASSADADIR ✓ («pul ver» ediləndə kassadan çıxır ✗)
        /// </summary>
        [ObservableProperty] private decimal terefdasVerilmeli;

        /// <summary>⚖️ ÖZ qalıq (₼) = kassa qalığı − tərəfdaşlara verilməli ✓.</summary>
        [ObservableProperty] private decimal ozQaliq;

        /// <summary>🔮 ÖZ proqnoz qalıq (₼) = proqnoz − tərəfdaşlara verilməli ✓.</summary>
        [ObservableProperty] private decimal ozProqnozQaliq;

        // ---------- ⏳ MÖHLƏT PROQNOZU ----------
        [ObservableProperty] private decimal gozlenilenMohlet;
        [ObservableProperty] private decimal gecikmisMohlet;

        /// <summary>Dövr üzrə xalis axın rəngi (mənfi = qırmızı ⚠).</summary>
        public string XalisRengi => Xalis >= 0m ? "#41D39A" : "#FA8A9E";

        /// <summary>Kassa qalığının rəngi (mənfi = qırmızı ⚠).</summary>
        public string QaliqRengi => Qaliq >= 0m ? "#4EC3F5" : "#FA8A9E";

        partial void OnXalisChanged(decimal value) => OnPropertyChanged(nameof(XalisRengi));

        partial void OnQaliqChanged(decimal value) => OnPropertyChanged(nameof(QaliqRengi));

        // ====================================================================
        //  📒 KASSA JURNALI  (dövrdəki BÜTÜN hərəkətlər ✓✓✓)
        // ====================================================================
        /// <summary>Cədvəldə göstərilən (filtrlənmiş) jurnal sətirləri ✓.</summary>
        public BulkObservableCollection<KassaSetiri> Setirler { get; } = new();

        /// <summary>Jurnal filtrinin başlığı: «📒 143 sətir · daxil 12 500 ₼ / xərc 9 300 ₼».</summary>
        [ObservableProperty] private string setirlerInfo = "—";

        // ---------- 🔍 FİLTRLƏR ----------
        public IReadOnlyList<string> NovFiltreleri { get; } = new[]
        {
            "Hamısı", "➕ Yalnız daxilolmalar", "➖ Yalnız xərclər"
        };

        [ObservableProperty] private string novFiltri = "Hamısı";

        /// <summary>Qrup filtri: «Hamısı» · «Satış» · «İlkin ödəniş» · «Kredit» · «Möhlət» · «Xərc» · «Tərəfdaş» · «Əl ilə».</summary>
        public ObservableCollection<string> QrupFiltreleri { get; } = new() { "Hamısı" };

        [ObservableProperty] private string qrupFiltri = "Hamısı";

        [ObservableProperty] private string axtaris = string.Empty;

        partial void OnNovFiltriChanged(string value) => Filtrle();

        partial void OnQrupFiltriChanged(string value) => Filtrle();

        partial void OnAxtarisChanged(string value) => Filtrle();

        /// <summary>🏷️ Bölmələr üzrə cəm (hər qrup üçün ayrı Kart ✓✓✓).</summary>
        public ObservableCollection<KassaQrupCem> QrupCemleri { get; } = new();

        // ====================================================================
        //  ✍ ƏL İLƏ YAZILAN KASSA HƏRƏKƏTLƏRİ  («kassaya qoyuldu / götürüldü»)
        // --------------------------------------------------------------------
        //  ⚠ Jurnalın BÖYÜK HİSSƏSİ AVTOMATİKDİR ✓ — bu bölmə YALNIZ
        //    istifadəçinin özünün yazdığı hərəkətlər üçündür ✓✓✓
        // ====================================================================
        /// <summary>Əl ilə yazılmış hərəkətlər (ən yenidən köhnəyə ✓).</summary>
        public BulkObservableCollection<KassaHereket> ManuelHereketler { get; } = new();

        /// <summary>Əl ilə heç bir hərəkət yoxdurmu? (boş panel yazısı üçün ✓)</summary>
        public bool HereketYoxdur => ManuelHereketler.Count == 0;

        /// <summary>Yeni hərəkətin QARALAMA forması ✓ (yazıldıqdan sonra təmizlənir ✓).</summary>
        public KassaHereket YeniHereket { get; } = new();

        /// <summary>Növ seçimləri: «➕ Daxilolma» və «➖ Xərc».</summary>
        public IReadOnlyList<string> Novlar { get; } = new[]
        {
            KassaHereket.NovDaxilolma, KassaHereket.NovXerc
        };

        /// <summary>Ödəniş üsulları (Nağd · Kart / Köçürmə ✓).</summary>
        public IReadOnlyList<string> PaymentMethods => Catalog.PaymentMethods;

        /// <summary>
        /// Seçilmiş NÖVƏ uyğun kateqoriyalar ✓✓✓
        /// (daxilolma = «Kapital qoyuluşu» … ✗ xərc = «Sahibə verildi» …)
        /// </summary>
        public IReadOnlyList<string> Kateqoriyalar => YeniHereket.IsDaxilolma
            ? KassaKategoriyalari.Daxilolmalar
            : KassaKategoriyalari.Xercler;

        /// <summary>➕ Kassaya qoyulan pulu / kassadan götürülən pulu YAZIR ✓✓✓</summary>
        [RelayCommand]
        private async Task HereketElaveEtAsync()
        {
            if (YeniHereket.Mebleg <= 0m)
            {
                _dialogs.ShowWarning("Məbləğ 0-dan böyük olmalıdır ✓");
                return;
            }

            try
            {
                var hereket = new KassaHereket
                {
                    Tarix = YeniHereket.Tarix.Date,
                    Nov = YeniHereket.Nov,
                    Kateqoriya = YeniHereket.Kateqoriya,
                    Mebleg = YeniHereket.Mebleg,
                    OdenisUsulu = YeniHereket.OdenisUsulu,
                    Qeyd = YeniHereket.Qeyd
                };

                await _kassa.AddHereketAsync(hereket);

                _logger.LogInformation("💵 Əl ilə kassa hərəkəti yazıldı: {Hereket}", hereket);

                // ♻️ Siyahıya ANİ əlavə olunur ✓ (yenidən tam yükləmə lazım deyil ✓)
                ManuelHereketler.Insert(0, hereket);

                // 📊 CƏMLƏR DƏRHAL yenilənir ✓ (kassa qalığı dəyişdi ✓✓✓)
                await LoadAsync();

                // 📢 v6.2.79 — digər tablar da yenilənsin ✓
                MelumatDegisdi();

                // 🧹 Forma təmizlənir ✓ (növ · kateqoriya · ödəniş üsulu saxlanılır ✓)
                YeniHereket.Mebleg = 0m;
                YeniHereket.Qeyd = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kassa hərəkəti yazıla bilmədi");
                _dialogs.ShowError("Kassa hərəkəti yazıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>🧹 Formanı təmizləyir (məbləğ · qeyd ✓).</summary>
        [RelayCommand]
        private void HereketTemizle()
        {
            YeniHereket.Mebleg = 0m;
            YeniHereket.Qeyd = string.Empty;
            YeniHereket.Tarix = DateTime.Today;
        }

        /// <summary>✖ Əl ilə yazılmış hərəkəti silir (təsdiqlə ✓✓✓).</summary>
        [RelayCommand]
        private async Task HereketSilAsync(KassaHereket? hereket)
        {
            if (hereket is null || hereket.Id <= 0)
            {
                return;
            }

            var cavab = _dialogs.Confirm(
                $"{hereket.TarixMetni} · {hereket.NovMetni} · {hereket.Kateqoriya} · {hereket.MeblegMetni}\n\n" +
                "Bu əl ilə yazılmış kassa hərəkəti SİLİNSİN?",
                "🗑 Silmə təsdiqi");

            if (!cavab)
            {
                return;
            }

            try
            {
                await _kassa.DeleteHereketAsync(hereket.Id);

                ManuelHereketler.Remove(hereket);
                await LoadAsync();

                MelumatDegisdi();   // 📢 v6.2.79 — digər tablar yenilənsin ✓

                _logger.LogInformation("🗑 Əl ilə kassa hərəkəti silindi: {Hereket}", hereket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kassa hərəkəti silinə bilmədi");
                _dialogs.ShowError("Kassa hərəkəti silinə bilmədi: " + ex.Message);
            }
        }

        // ====================================================================
        //  ⏳ MÖHLƏTLƏR  («nə vaxt, nə qədər pul gələcək» ✓✓✓)
        // --------------------------------------------------------------------
        //  ① 🕓 GÖZLƏNİLƏN — pul hələ GƏLMƏYİB ✗ (kassaya daxil olmayıb ✗)
        //  ② ⚠ GECİKMİŞ  — tarix keçib, pul gəlməyib ✗
        //  ③ ✅ ÖDƏNİLMİŞ — pul GƏLDİ ✓ → kassaya DAXİL olundu ✓✓✓
        // ====================================================================
        /// <summary>🕓 Ödənilməmiş möhlətlər (tarixə görə ✓ — ən yaxını əvvəldə ✓).</summary>
        public BulkObservableCollection<OdenisMohlet> GozlenilenMohletler { get; } = new();

        /// <summary>✅ Son ödənilmiş möhlətlər (ən yenisi əvvəldə ✓).</summary>
        public BulkObservableCollection<OdenisMohlet> OdenilmisMohletler { get; } = new();

        /// <summary>✅ Ödənilmiş (kassaya daxil olmuş) möhlətlərin CƏMİ (₼).</summary>
        [ObservableProperty] private decimal odenilmisCem;

        /// <summary>📅 Yaxın 7 günə gözlənilən möhlətlər (₼) — tez gələcək pul ✓.</summary>
        [ObservableProperty] private decimal yaxinHefteMohlet;

        /// <summary>Ödənilməmiş möhlət sayı.</summary>
        public int GozlenilenSayi => GozlenilenMohletler.Count;

        /// <summary>⚠ Vaxtı keçmiş möhlət sayı.</summary>
        public int GecikmisSayi => GozlenilenMohletler.Count(m => m.Gecikmis);

        /// <summary>Gözlənilən möhlət varmı? (panel boş olanda «yoxdur» yazısı ✓)</summary>
        public bool HasGozlenilen => GozlenilenMohletler.Count > 0;

        /// <summary>Ödənilmiş möhlət varmı?</summary>
        public bool HasOdenilmis => OdenilmisMohletler.Count > 0;

        /// <summary>Heç gözlənilən möhlət yoxdurmu? (boş panel yazısı üçün ✓)</summary>
        public bool MohletYoxdur => GozlenilenMohletler.Count == 0;

        // ====================================================================
        //  👥 v6.2.77 — GÖZLƏNİLƏN MÖHLƏTİN TƏRƏFDAŞ BÖLGÜSÜ ✓✓✓
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi: «Kassa tabında gözlənilən möhlətlərin yanında
        //    tərəfdaş bölgüsü əlavə et — eynilə digər tablardakıların.
        //    Bəzən bu ilkin ödənişə möhlətlər də tərəfdaşlar arasında bölünür.»
        // --------------------------------------------------------------------
        //  ✅ Dizayn və davranış «Kreditlər» tabı ilə EYNİDİR ✓✓✓
        //    • 🏷️ MAYA (₼) — əl ilə yazılır ✓
        //    • ✍️ Manual bölgü rejimi ✓ (açıqda paylar əl ilə — «Hesabla»
        //      məbləğlərin ÜZƏRİNƏ YAZMIR ✗)
        //    • ☑ TƏTBİQ OLUNUR — işarəsiz tərəfdaşın faizi hesablanmır ✗
        //    • «qalıq» payçısında FAİZ xanası BAĞLIDIR ✓
        //    • PAYLARIN CƏMİ + «✓ Dəqiq bölünüb / ⚠ Fərq» ✓
        //  📌 Baza = MÖHLƏTİN MƏBLƏĞİ − MAYA ✓ (yalnız mənfəət bölünür ✓)
        //  📌 Cədvəldə MÖHLƏT seçilməyibsə → BÜTÜN gözlənilən möhlətlərin
        //     cəmi üzrə bölgü göstərilir ✓✓✓
        //  ⚠ Bu panel HESABLAMADIR ✗ — pul «Tərəfdaşlar» tabında faktiki
        //     ödəniş kimi yazılır ✓ (kassa balansı dəyişmir ✗✓✓)
        // ====================================================================

        /// <summary>👥 Möhlətin tərəfdaş payları (canlı ✓).</summary>
        public ObservableCollection<PartnerPayRow> MohletTerefdaslari { get; } = new();

        /// <summary>🕓 Bölgüsü göstərilən möhlət (seçilməyibsə — hamısı ✓).</summary>
        [ObservableProperty] private OdenisMohlet? selectedGozlenilenMohlet;

        /// <summary>☑ Bölgü tətbiq olunur? (işarəsiz → hesablanmır ✗)</summary>
        [ObservableProperty] private bool mohletBolguTetbiq = true;

        /// <summary>✍️ Manual rejim — paylar əl ilə yazılır ✓</summary>
        [ObservableProperty] private bool mohletBolguManual;

        /// <summary>🏷️ MAYA (₼) — bölgü bazasından çıxılır ✓</summary>
        [ObservableProperty] private decimal mohletBolguMaya;

        /// <summary>📊 Bölgü bazası («xeyir») = möhlətin məbləği − MAYA ✓</summary>
        [ObservableProperty] private decimal mohletBolguBazasi;

        /// <summary>Rejim mətni — «✍️ MANUAL» / «⚙️ AVTOMATİK» ✓</summary>
        public string MohletBolguRejimMetni => MohletBolguManual ? "✍️ MANUAL" : "⚙️ AVTOMATİK";

        /// <summary>☑ Vəziyyət mətni ✓</summary>
        public string MohletBolguVeziyyet => MohletBolguTetbiq ? "✅ Tətbiq olunub" : "⬜ Tətbiq olunmayıb";

        /// <summary>✍️ Manual rejim izahı — digər tablarla EYNİ mətn ✓</summary>
        public string MohletBolguManualIzahMetni => MohletBolguManual
            ? "① MAYA-nı və hər tərəfdaşın MƏBLƏĞİNİ əl ilə yazın ✓  ② «🔄 Hesabla» basın ✓ — paylar TOXUNULMUR ✗"
            : string.Empty;

        /// <summary>🕓 Bölgünün hansı məbləğ üzərindən qurulduğu (izah ✓)</summary>
        public string MohletBolguHedefMetni => SelectedGozlenilenMohlet is OdenisMohlet m
            ? $"🕓 {m.MenbeMetni} · {m.TarixMetni} · {m.Mebleg:N2} ₼"
            : "🕓 Ödəniş üçün cədvəldən bir möhlət SEÇİN ✗";

        /// <summary>💰 Bölgü bazası üçün MƏNBƏ məbləğ = ÖDƏNİLƏCƏK məbləğ ✓ (v6.2.78)</summary>
        public decimal MohletBolguMenbe => MohletOdenisMebleg;

        /// <summary>«Xeyir = Möhlət məbləği − Maya = … ₼» ✓</summary>
        public string MohletXeyirMetni =>
            $"Xeyir = Möhlət məbləği − Maya = {MohletBolguMenbe:N2} − {MohletBolguMaya:N2} = {MohletBolguBazasi:N2} ₼";

        /// <summary>Payların cəmi (yalnız ☑ tərəfdaşlar ✓).</summary>
        public decimal MohletBolguCemi => MohletTerefdaslari.Where(r => r.Aktiv).Sum(r => r.Mebleg);

        /// <summary>Cəm ilə baza arasındaki fərq ✓.</summary>
        public decimal MohletBolguFergi => MohletBolguCemi - MohletBolguBazasi;

        /// <summary>Xülasə: «Zaur 95,40 ₼ · Eşqin 79,50 ₼ · …» ✓</summary>
        public string MohletBolguXulase => MohletTerefdaslari.Count == 0
            ? "Bölgü hazır deyil"
            : string.Join(" · ", MohletTerefdaslari
                .Where(r => r.Aktiv)
                .Select(r => $"{r.Terefdas} {r.Mebleg:N2} ₼"));

        /// <summary>Fərq mətni ✓.</summary>
        public string MohletBolguFergMetni => MohletBolguFergi == 0m
            ? "✓ Dəqiq bölünüb — fərq yoxdur"
            : $"⚠ Fərq: {MohletBolguFergi:N2} ₼";

        /// <summary>Fərq rəngi ✓.</summary>
        public string MohletBolguFergReng => MohletBolguFergi == 0m ? "#41D39A" : "#F2677F";

        /// <summary>Bölgü paneli görünsün? (ən azı bir gözlənilən möhlət varsa ✓)</summary>
        public bool MohletBolguGorunur => GozlenilenMohletler.Count > 0;

        // ====================================================================
        //  💳 v6.2.78 — MÖHLƏT ÖDƏNİŞİNİN PROFESIONAL PANELİ ✓✓✓
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi: «möhlətin üstünə basıb seçirik ✓ — paneldə
        //    TAM ÖDƏNİŞ / TAM ÖDƏNİLMƏDİ seçimi olmalıdır ✓
        //    • TAM ÖDƏNİŞ      → yalnız «nə vaxt ödəndi» soruşulur ✓
        //    • TAM ÖDƏNİLMƏDİ  → «ödəniş tarixi» + «qalan nə vaxt ödəniləcək» ✓
        //    • hər iki halda MƏBLƏĞ soruşulur ✓ (default = möhlətin məbləği ✓)
        //    • mayaya (kökə) və tərəfdaş mənfəətinə nə qədər getdiyi YAZILIR ✓
        //    • bütün hesablamalar maşının mayasından DÜZGÜN götürülür ✓»
        // --------------------------------------------------------------------
        //  🔢 MAYA (kök payı) AVTOMATİK:
        //     • Kreditə bağlı möhlət → `Credit.OdenisKokPayi(ödəniş)` ✓
        //     • Nisyə satışa bağlı möhlət → `MayaDeyeri × (ödəniş ÷ SatışQiyməti)` ✓
        //     ⚠ İstifadəçi MAYA-nı əl ilə də DƏYİŞƏ BİLƏR ✓
        //  💰 BÖLGÜ BAZASI = ÖDƏNİŞ − MAYA (kök) ✓ → yalnız MƏNFƏƏT bölünür ✓
        // ====================================================================

        /// <summary>✅ <b>TAM ÖDƏNİŞ</b> rejimi (möhlətin hamısı ödənilir) ✓</summary>
        [ObservableProperty] private bool mohletTamOdenis = true;

        /// <summary>⚠ <b>TAM ÖDƏNİLMƏDİ</b> rejimi (qismən ödəniş + yeni tarix) ✓</summary>
        [ObservableProperty] private bool mohletQismenOdenis;

        /// <summary>💵 ÖDƏNİLƏN MƏBLƏĞ (₼) — default: möhlətin tam məbləği ✓</summary>
        [ObservableProperty] private decimal mohletOdenisMebleg;

        /// <summary>📅 ÖDƏNİŞ TARİXİ — default: bu gün ✓</summary>
        [ObservableProperty] private DateTime? mohletOdenisTarixi = DateTime.Today;

        /// <summary>⏳ Qalan məbləğ NƏ VAXT ödəniləcək (yalnız qismən ödənişdə ✓)</summary>
        [ObservableProperty] private DateTime? mohletSonrakiTarix;

        /// <summary>⏳ Ödənişdən sonra QALAN məbləğ (₼) ✓</summary>
        [ObservableProperty] private decimal mohletQaliqMebleg;

        /// <summary>🏷️ MAYA (kök payı) — bu ödənişin əsas borc hissəsi (₼) ✓</summary>
        [ObservableProperty] private decimal mohletMayaPayi;

        /// <summary>✅ «Möhlət seçilib?» — panelin aktivliyi üçün ✓</summary>
        public bool MohletSecilib => SelectedGozlenilenMohlet is OdenisMohlet;

        /// <summary>📊 «MAYA (kök): X ₼ · Bölünən mənfəət: Y ₼» izahı ✓</summary>
        public string MohletKokMetni =>
            $"🏷️ Maya (kök) payı: {MohletMayaPayi:N2} ₼   ·   💰 Bölünən mənfəət: {MohletBolguBazasi:N2} ₼" +
            (MohletOdenisMebleg > 0m
                ? $"   ·   💵 Ödəniş: {MohletOdenisMebleg:N2} ₼"
                : string.Empty);

        /// <summary>⏳ Qalan məbləğ mətni (qismən ödənişdə ✓)</summary>
        public string MohletQaliqMetni => MohletQaliqMebleg <= 0m
            ? "✓ Tam ödəniş — qalıq yoxdur"
            : $"⏳ Qalan: {MohletQaliqMebleg:N2} ₼ — nə vaxt ödəniləcək?";

        /// <summary>🔘 Təsdiq düyməsinin mətni ✓</summary>
        public string MohletOdenisDugmeMetni => MohletQismenOdenis
            ? "⚠  QİSMƏN ÖDƏNİŞİ TƏSDİQ ET"
            : "✅  ÖDƏNİŞİ TƏSDİQ ET";

        /// <summary>💬 Panelin status/izah mətni ✓</summary>
        /// <summary>💬 Panelin status/izah mətni ✓</summary>
        [ObservableProperty] private string mohletOdenisStatusMetni =
            "① Cədvəldən möhlət seçin  ② TAM ÖDƏNİŞ / TAM ÖDƏNİLMƏDİ seçin  ③ Məbləğ və tarixləri yoxlayın  ④ «ÖDƏNİŞİ TƏSDİQ ET»";

        /// <summary>✅ Ödəniş təsdiqlənə BİLƏRMİ? (bütün yoxlamalar ✓)</summary>
        public bool MohletOdenisOlunur
        {
            get
            {
                if (SelectedGozlenilenMohlet is not OdenisMohlet m || m.Id <= 0)
                {
                    return false;
                }

                if (MohletOdenisMebleg <= 0m || MohletOdenisMebleg > m.Mebleg + 0.005m)
                {
                    return false;
                }

                if (MohletOdenisTarixi is not DateTime)
                {
                    return false;
                }

                // ⏳ Qismən ödənişdə «qalan nə vaxt» MÜTLƏQ lazımdır ✓
                if (MohletQaliqMebleg > 0.005m && MohletSonrakiTarix is not DateTime)
                {
                    return false;
                }

                return true;
            }
        }

        /// <summary>🔄 «Qismən» seçildikdə «tam» söndürülür ✓ (radio davranışı)</summary>
        partial void OnMohletQismenOdenisChanged(bool value)
        {
            if (value && MohletTamOdenis)
            {
                MohletTamOdenis = false;
            }

            MohletOdenisiniYenile();
        }

        /// <summary>🔄 «Tam» seçildikdə «qismən» söndürülür ✓ (radio davranışı)</summary>
        partial void OnMohletTamOdenisChanged(bool value)
        {
            if (value && MohletQismenOdenis)
            {
                MohletQismenOdenis = false;
            }

            MohletOdenisiniYenile();
        }

        /// <summary>💵 Məbləğ dəyişdi → qalıq, MAYA və bölgü yenilənir ✓</summary>
        partial void OnMohletOdenisMeblegChanged(decimal value)
        {
            MohletQaliqMebleg = SelectedGozlenilenMohlet is OdenisMohlet m
                ? Math.Max(0m, Math.Round(m.Mebleg - value, 2))
                : 0m;

            // 🏷️ MAYA (kök payı) avtomatik yenidən hesablanır ✓
            MohletMayaPayi = MohletKokPayiHesabla(value);

            MohletBolguMaya = MohletMayaPayi;   // → baza yenilənir ✓

            MohletOdenisiniYenile();
        }

        partial void OnMohletOdenisTarixiChanged(DateTime? value) => MohletOdenisiniYenile();

        partial void OnMohletSonrakiTarixChanged(DateTime? value) => MohletOdenisiniYenile();

        /// <summary>🔢 <b>Bu ödənişin MAYA (kök) payı</b> ✓✓✓ (v6.2.78)</summary>
        private decimal MohletKokPayiHesabla(decimal mebleg)
        {
            if (mebleg <= 0m || SelectedGozlenilenMohlet is not OdenisMohlet mohlet)
            {
                return 0m;
            }

            // ── 💳 KREDİTƏ BAĞLI MÖHLƏT ────────────────────────────────
            if (mohlet.CreditId is int creditId)
            {
                var kredit = _kreditKeshi.FirstOrDefault(k => k.Id == creditId);

                return kredit is null ? 0m : Math.Round(kredit.OdenisKokPayi(mebleg), 2);
            }

            // ── 🧾 NİSYƏ SATIŞA BAĞLI MÖHLƏT ──────────────────────────
            if (mohlet.SaleId is int saleId)
            {
                var satis = _satisKeshi.FirstOrDefault(s => s.Id == saleId);

                if (satis is null || satis.SatisQiymeti <= 0m)
                {
                    return 0m;
                }

                var nisbet = Math.Clamp(mebleg / satis.SatisQiymeti, 0m, 1m);

                return Math.Round(satis.MayaDeyeri * nisbet, 2);
            }

            return 0m;
        }

        /// <summary>Bütün ödəniş göstəricilərini UI-a bildirir ✓.</summary>
        private void MohletOdenisiniYenile()
        {
            OnPropertyChanged(nameof(MohletSecilib));
            OnPropertyChanged(nameof(MohletOdenisOlunur));
            OnPropertyChanged(nameof(MohletOdenisDugmeMetni));
            OnPropertyChanged(nameof(MohletQaliqMetni));
            OnPropertyChanged(nameof(MohletKokMetni));
            OnPropertyChanged(nameof(MohletBolguHedefMetni));
            OnPropertyChanged(nameof(MohletBolguMenbe));
            OnPropertyChanged(nameof(MohletXeyirMetni));
            OnPropertyChanged(nameof(MohletBolguCemi));
            OnPropertyChanged(nameof(MohletBolguFergi));
            OnPropertyChanged(nameof(MohletBolguXulase));
            OnPropertyChanged(nameof(MohletBolguFergMetni));
            OnPropertyChanged(nameof(MohletBolguFergReng));
        }

        /// <summary>
        /// 🕓 <b>Seçilmiş möhlət üçün ödəniş panelini HAZIRLAYIR</b> ✓✓✓
        /// <para>Məbləğ = möhlətin tam məbləği ✓ · Tarix = bu gün ✓ ·
        /// MAYA = avtomatik kök payı ✓ · bölgü dərhal hesablanır ✓</para>
        /// </summary>
        private void MohletOdenisiniHazirla()
        {
            if (SelectedGozlenilenMohlet is not OdenisMohlet mohlet)
            {
                MohletOdenisMebleg = 0m;
                MohletQaliqMebleg = 0m;
                MohletMayaPayi = 0m;
                MohletBolguMaya = 0m;
                MohletOdenisTarixi = DateTime.Today;
                MohletSonrakiTarix = null;
                MohletOdenisStatusMetni =
                    "🕓 Ödəniş üçün cədvəldən möhlət SEÇİN ✗ — məbləğ, tarixlər və tərəfdaş bölgüsü avtomatik hazırlanacaq ✓";

                MohletBolguBazasiniYenile();
                MohletOdenisiniYenile();
                return;
            }

            MohletTamOdenis = true;
            MohletQismenOdenis = false;

            MohletOdenisMebleg = mohlet.Mebleg;
            MohletQaliqMebleg = 0m;
            MohletOdenisTarixi = DateTime.Today;

            MohletSonrakiTarix = mohlet.Tarix > DateTime.Today
                ? mohlet.Tarix
                : DateTime.Today.AddDays(7);

            MohletMayaPayi = MohletKokPayiHesabla(mohlet.Mebleg);
            MohletBolguMaya = MohletMayaPayi;

            MohletOdenisStatusMetni =
                $"✅ «{mohlet.MenbeMetni}» seçildi — ödəniləcək {mohlet.Mebleg:N2} ₼ hazırdır ✓";

            MohletBolguBazasiniYenile();
            MohletBolguHesabla();
            MohletOdenisiniYenile();
        }

        /// <summary>
        /// ✅ <b>MÖHLƏT ÖDƏNİŞİNİ TƏSDİQLƏYİR</b> ✓✓✓ (v6.2.78)
        /// <list type="number">
        ///   <item><b>TAM ÖDƏNİŞ</b> → möhlət «ödənildi» olur ✓ (yalnız tarix yazılır ✓)</item>
        ///   <item><b>TAM ÖDƏNİLMƏDİ</b> → ödənilən hissə ayrıca «ödənildi» sətri olur ✓
        ///         və qalan hissə YENİ TARİXLƏ möhlətlərdə qalır ✓</item>
        ///   <item>💳 Kreditə bağlı möhlətdə <b>tərəfdaş payları yazılır</b> ✓ →
        ///         «👥 Tərəfdaşlar» tabında «Möhlət ödənişi» kimi görünür ✓✓✓</item>
        /// </list>
        /// <para>⚠ Kassa balansı DƏYİŞMİR ✗ — pul `OdenisMohlet` vasitəsilə artıq daxil olur ✓</para>
        /// </summary>
        [RelayCommand]
        private async Task MohletOdenisiniTesdiqleAsync()
        {
            if (SelectedGozlenilenMohlet is not OdenisMohlet mohlet || mohlet.Id <= 0)
            {
                _dialogs.ShowWarning("Əvvəlcə cədvəldən möhləti SEÇİN ✗");
                return;
            }

            if (!MohletOdenisOlunur)
            {
                _dialogs.ShowWarning(
                    "⚠ Ödəniş məlumatları tam deyil ✗\n\n" +
                    $"• Məbləğ 0-dan böyük və {mohlet.Mebleg:N2} ₼-dən çox olmamalıdır ✓\n" +
                    "• Ödəniş tarixi seçilməlidir ✓\n" +
                    "• Qismən ödənişdə «qalan nə vaxt ödəniləcək» tarixi MÜTLƏQ lazımdır ✓");
                return;
            }

            var odenis = Math.Round(Math.Min(MohletOdenisMebleg, mohlet.Mebleg), 2);
            var tarix = MohletOdenisTarixi!.Value.Date;
            var tam = Math.Abs(mohlet.Mebleg - odenis) < 0.005m;
            var qaliq = Math.Round(mohlet.Mebleg - odenis, 2);
            var payCemi = MohletBolguTetbiq ? MohletBolguCemi : 0m;

            if (!_dialogs.Confirm(
                    $"📌 {mohlet.MenbeMetni}\n" +
                    $"Möhlətin məbləği : {mohlet.Mebleg:N2} ₼\n\n" +
                    (tam ? "✅ TAM ÖDƏNİŞ\n" : "⚠ TAM ÖDƏNİLMƏDİ (qismən)\n") +
                    $"💵 Ödənilən       : {odenis:N2} ₼\n" +
                    $"📅 Ödəniş tarixi  : {tarix:dd.MM.yyyy}\n" +
                    (tam ? string.Empty
                         : $"⏳ Qalan          : {qaliq:N2} ₼ → {MohletSonrakiTarix:dd.MM.yyyy}\n") +
                    $"\n🏷️ Maya (kök)     : {MohletMayaPayi:N2} ₼\n" +
                    $"💰 Bölünən mənfəət: {MohletBolguBazasi:N2} ₼\n" +
                    (MohletBolguTetbiq && payCemi > 0m
                        ? $"👥 Tərəfdaş payları: {MohletBolguXulase}\n"
                        : "👥 Tərəfdaş bölgüsü: tətbiq olunmur\n") +
                    "\nÖdəniş yazılsın? ✓",
                    "💳 Möhlət ödənişi"))
            {
                return;
            }

            try
            {
                MohletOdenisStatusMetni = "⏳ Ödəniş yazılır…";

                // 🔗 Payların markeri üçün ÖDƏNİLMİŞ sətrin İD-si ✓
                var odenilmisId = mohlet.Id;

                if (tam)
                {
                    mohlet.Odenilib = true;
                    mohlet.OdenilmeTarixi = tarix;
                    await _mohletler.UpdateAsync(mohlet);
                }
                else
                {
                    // ① ÖDƏNİLƏN HİSSƏ — ayrıca «ödənildi» sətri ✓
                    var odenilmis = await _mohletler.AddAsync(new OdenisMohlet
                    {
                        Menbe = mohlet.Menbe,
                        CreditId = mohlet.CreditId,
                        SaleId = mohlet.SaleId,
                        Tarix = tarix,
                        Mebleg = odenis,
                        Sira = mohlet.Sira,
                        Qeyd = $"⏳ Qismən ödəniş ✓ (möhlət {mohlet.Mebleg:N2} ₼ idi)"
                    });

                    odenilmis.Odenilib = true;
                    odenilmis.OdenilmeTarixi = tarix;
                    await _mohletler.UpdateAsync(odenilmis);

                    odenilmisId = odenilmis.Id;   // 🔗 paylar BU sətrə bağlanır ✓

                    // ② QALAN HİSSƏ — YENİ TARİXLƏ möhlətdə qalır ✓
                    mohlet.Mebleg = qaliq;
                    mohlet.Tarix = MohletSonrakiTarix!.Value.Date;
                    await _mohletler.UpdateAsync(mohlet);
                }

                // ③ 👥 TƏRƏFDAŞ PAYLARI — kredit VƏ nisyə satış möhlətlərində ✓
                var paySayi = await MohletBolguPaylariniYazAsync(
                    odenilmisId, mohlet.CreditId, odenis, tarix);

                await LoadAsync();

                // 📢 v6.2.79 — «👥 Tərəfdaşlar» / «Maliyyə» tabları DƏRHAL yenilənsin ✓✓✓
                MelumatDegisdi();

                MohletOdenisStatusMetni =
                    $"✅ ÖDƏNİŞ YAZILDI — {odenis:N2} ₼ · {tarix:dd.MM.yyyy}" +
                    (tam ? " (tam)" : $" · qalıq {qaliq:N2} ₼") +
                    (paySayi > 0 ? $" · {paySayi} tərəfdaş payı ✓" : string.Empty);

                _dialogs.ShowInfo(
                    "✅ MÖHLƏT ÖDƏNİŞİ YAZILDI ✓✓✓\n\n" +
                    $"📌 {mohlet.MenbeMetni}\n" +
                    $"💵 Ödənildi : {odenis:N2} ₼\n" +
                    $"📅 Tarix    : {tarix:dd.MM.yyyy}\n" +
                    (tam ? string.Empty
                         : $"⏳ Qalıq    : {qaliq:N2} ₼ → {MohletSonrakiTarix:dd.MM.yyyy}\n") +
                    $"\n🏷️ Maya (kök)      : {MohletMayaPayi:N2} ₼\n" +
                    $"💰 Bölünən mənfəət : {MohletBolguBazasi:N2} ₼\n" +
                    (paySayi > 0
                        ? $"👥 Tərəfdaş payları: {MohletBolguXulase}\n\n" +
                          "ℹ️ «👥 Tərəfdaşlar» tabında «Möhlət ödənişi» kimi görünür ✓"
                        : "\nℹ️ Tərəfdaş bölgüsü yazılmadı (bölgü söndürülüb və ya möhlət kreditə bağlı deyil)"),
                    "💳 Möhlət ödənişi — nəticə");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Möhlət ödənişi yazıla bilmədi");
                MohletOdenisStatusMetni = "❌ Xəta: " + ex.Message;
                _dialogs.ShowError("Möhlət ödənişi yazıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>📢 Məlumat dəyişdi → bütün tablar yenilənsin ✓ (v6.2.79)</summary>
        private void MelumatDegisdi()
        {
            try
            {
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kassa: DataChanged hadisəsi ötürülmədi ✗");
            }
        }

        /// <summary>
        /// 👥 <b>Möhlət ödənişinin tərəfdaş paylarını BAZAYA yazır</b> ✓✓✓ (v6.2.79)
        /// <para>
        /// 💳 <b>Həm kreditə, həm nisyə satışa</b> bağlı möhlətlərdə işləyir ✓ —
        /// <c>CreditTransaction</c> yaradılır (növ: «Möhlət ödənişi» ✗ <b>«Gəlir» deyil</b>)
        /// → <b>kassada ikiqat olmaz</b> ✗✓✓, yalnız paylar yazılır ✓
        /// </para>
        /// <para>
        /// 🔗 <b>ƏLAQƏ MARKERİ:</b> təsvirə <c>möhlət #Id</c> yazılır ✓ →
        /// «geri al» / «sil» zamanı paylar DƏQİQ tapılıb silinir ✓✓✓
        /// </para>
        /// </summary>
        /// <param name="mohletId">Ödənilmiş möhlət sətrinin İD-si (marker üçün ✓)</param>
        private async Task<int> MohletBolguPaylariniYazAsync(int mohletId, int? creditId, decimal odenis, DateTime tarix)
        {
            if (!MohletBolguTetbiq || odenis <= 0m)
            {
                return 0;
            }

            var paylar = new List<PartnerShare>();
            var sira = 0;

            foreach (var row in MohletTerefdaslari)
            {
                var model = row.ToModel(sira++);

                if (model.Mebleg > 0m)
                {
                    paylar.Add(model);
                }
            }

            if (paylar.Count == 0)
            {
                _logger.LogInformation("⏳ Möhlət ödənişi: tərəfdaş payı 0 ₼ — bölgü yazılmadı ✗");
                return 0;
            }

            // ✍️ MANUAL rejimdə baza 0 olsa da paylar YAZILIR ✓ (istifadəçi əl ilə yazıb ✓)
            var baza = MohletBolguBazasi > 0m
                ? MohletBolguBazasi
                : Math.Round(paylar.Sum(p => p.Mebleg), 2);

            await _credits.AddTransactionAsync(new CreditTransaction
            {
                CreditId = creditId,
                Nov = Catalog.MohletOdenisNovu,
                Mebleg = odenis,
                Tarix = tarix,
                Odenilib = true,
                Tesvir = $"⏳ Möhlət ödənişi · möhlət #{mohletId} · maya {MohletMayaPayi:N2} ₼ · mənfəət {baza:N2} ₼",
                TerefdasBolguTetbiqOlunub = true,
                BolguBazasi = baza,
                TerefdasPaylari = paylar
            });

            _logger.LogInformation(
                "⏳ Möhlət ödənişi + tərəfdaş bölgüsü ✓: möhlət #{MohletId} · kredit {CreditId} · {Mebleg:N2} ₼ · {Pay} pay · {Tarix:dd.MM.yyyy}",
                mohletId, creditId?.ToString() ?? "—", odenis, paylar.Count, tarix);

            return paylar.Count;
        }

        /// <summary>
        /// 🗑️ <b>Möhlətə bağlı TƏRƏFDAŞ PAYLARINI SİLİR</b> ✓✓✓ (v6.2.79)
        /// <para>
        /// ★ İstifadəçi tələbi: «geri alım edəndə ödənilmiş möhlət tərəfdaşlar
        /// bölgüsündən də silinməlidir» ✓ → marker (<c>möhlət #Id</c>) ilə
        /// DƏQİQ sətirlər tapılır ✓ və payları ilə birlikdə silinir ✓✓✓
        /// </para>
        /// </summary>
        private async Task<int> MohletBolguPaylariniSilAsync(int mohletId)
        {
            try
            {
                var marker = $"möhlət #{mohletId}";
                var hamisi = await _credits.GetTransactionsAsync();

                var silinecek = hamisi
                    .Where(t => string.Equals(t.Nov, Catalog.MohletOdenisNovu, StringComparison.Ordinal)
                                && (t.Tesvir ?? string.Empty).Contains(marker, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var tx in silinecek)
                {
                    await _credits.DeleteTransactionAsync(tx.Id);
                }

                if (silinecek.Count > 0)
                {
                    _logger.LogInformation(
                        "🗑️ Möhlət #{MohletId} — {Say} tərəfdaş bölgüsü sətri SİLİNDİ ✓",
                        mohletId, silinecek.Count);
                }

                return silinecek.Count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Möhlətin tərəfdaş bölgüsü silinmədi ✗ (möhlət #{Id})", mohletId);
                return 0;
            }
        }

        /// <summary>🔄 Bölgü bazasını möhlət məbləğindən yeniləyir ✓.</summary>
        private void MohletBolguBazasiniYenile()
        {
            var baza = Math.Round(MohletBolguMenbe - MohletBolguMaya, 2);

            MohletBolguBazasi = baza > 0m ? baza : 0m;

            MohletBolguYenile();
        }

        /// <summary>Bölgü ilə bağlı hesablanmış göstəriciləri UI-a bildirir ✓.</summary>
        private void MohletBolguYenile()
        {
            OnPropertyChanged(nameof(MohletBolguMenbe));
            OnPropertyChanged(nameof(MohletBolguHedefMetni));
            OnPropertyChanged(nameof(MohletXeyirMetni));
            OnPropertyChanged(nameof(MohletBolguCemi));
            OnPropertyChanged(nameof(MohletBolguFergi));
            OnPropertyChanged(nameof(MohletBolguXulase));
            OnPropertyChanged(nameof(MohletBolguFergMetni));
            OnPropertyChanged(nameof(MohletBolguFergReng));
            OnPropertyChanged(nameof(MohletBolguVeziyyet));
            OnPropertyChanged(nameof(MohletBolguRejimMetni));
            OnPropertyChanged(nameof(MohletBolguManualIzahMetni));
            OnPropertyChanged(nameof(MohletBolguGorunur));
        }

        /// <summary>🏷️ MAYA dəyişdi → baza yenidən hesablanır ✓.</summary>
        partial void OnMohletBolguMayaChanged(decimal value)
        {
            MohletBolguBazasiniYenile();

            if (!MohletBolguManual)
            {
                MohletBolguHesabla();
            }
        }

        /// <summary>🕓 Seçilmiş möhlət dəyişdi → ÖDƏNİŞ PANELİ hazırlanır ✓ (v6.2.78)</summary>
        partial void OnSelectedGozlenilenMohletChanged(OdenisMohlet? value)
        {
            MohletOdenisiniHazirla();
        }

        /// <summary>☑ Bölgü açıldı/bağlandı ✓.</summary>
        partial void OnMohletBolguTetbiqChanged(bool value)
        {
            OnPropertyChanged(nameof(MohletBolguVeziyyet));

            if (value)
            {
                MohletBolguBazasiniYenile();
                MohletBolguHesabla();
            }
        }

        /// <summary>✍️ Manual rejim dəyişdi → faizlər sıfırlanır ✓ (digər tablar kimi).</summary>
        partial void OnMohletBolguManualChanged(bool value)
        {
            OnPropertyChanged(nameof(MohletBolguRejimMetni));
            OnPropertyChanged(nameof(MohletBolguManualIzahMetni));

            try
            {
                if (value)
                {
                    foreach (var r in MohletTerefdaslari)
                    {
                        r.Faiz = 0m;
                    }
                }
            }
            catch
            {
                // 🛡️ səssizcə keçir ✓
            }
        }

        /// <summary>👥 Bölgü sətirlərini standart tərəfdaşlarla doldurur ✓.</summary>
        private void MohletBolguSifirla()
        {
            MohletTerefdaslari.Clear();

            foreach (var share in PartnerMath.CreateDefaultRows())
            {
                MohletTerefdaslari.Add(PartnerPayRow.FromModel(share));
            }

            MohletBolguMaya = 0m;
            MohletBolguBazasi = 0m;
            MohletBolguYenile();
        }

        /// <summary>➕ Çatışmayan tərəfdaşları cədvələ əlavə edir ✓ (DB-dən ✓).</summary>
        private void MohletTerefdaslariniTamamla()
        {
            try
            {
                foreach (var pay in PartnerMath.CreateDefaultRows())
                {
                    var ad = (pay.Terefdas ?? string.Empty).Trim();

                    if (ad.Length == 0)
                    {
                        continue;
                    }

                    if (!MohletTerefdaslari.Any(r =>
                            string.Equals(r.Terefdas?.Trim(), ad, StringComparison.OrdinalIgnoreCase)))
                    {
                        MohletTerefdaslari.Add(PartnerPayRow.FromModel(pay));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Möhlət bölgüsü: yeni tərəfdaşlar əlavə olunmadı ✗");
            }
        }

        /// <summary>
        /// 🔄 <b>Möhlətin tərəfdaş bölgüsünü hesablayır</b> ✓✓✓
        /// <para>Qayda digər tablarla EYNİDİR: faiz payçıları faizi qədər alır,
        /// qalan məbləği qalıq payçıları (Asif &amp; Musa) yarı-yarıya bölür ✓</para>
        /// <para>⚠ Manual rejimdə məbləğlərə TOXUNULMUR ✗ (yalnız cəm/fərq ✓)</para>
        /// </summary>
        [RelayCommand]
        private void MohletBolguHesabla()
        {
            MohletTerefdaslariniTamamla();
            MohletBolguBazasiniYenile();

            // ✍️ Manual rejim → məbləğlər ƏL İLƏDİR ✓ (üzərinə yazılmır ✗)
            if (MohletBolguManual)
            {
                MohletBolguYenile();
                return;
            }

            var modeller = new List<PartnerShare>();
            var sira = 0;

            foreach (var row in MohletTerefdaslari)
            {
                modeller.Add(row.ToModel(sira++));
            }

            PartnerMath.Distribute(MohletBolguBazasi, modeller);

            for (var i = 0; i < modeller.Count && i < MohletTerefdaslari.Count; i++)
            {
                MohletTerefdaslari[i].Mebleg = modeller[i].Mebleg;
            }

            MohletBolguYenile();
        }

        /// <summary>♻️ Faizləri standart qiymətlərə qaytarır ✓ (digər tablar kimi).</summary>
        [RelayCommand]
        private void MohletBolguDefault()
        {
            MohletTerefdaslariniTamamla();

            var standart = PartnerMath.CreateDefaultRows();

            foreach (var row in MohletTerefdaslari)
            {
                var uygun = standart.FirstOrDefault(d => d.Terefdas == row.Terefdas);

                if (uygun is null)
                {
                    continue;
                }

                row.Faiz = uygun.Faiz;
                row.QaligPayi = uygun.QaligPayi;
                row.Aktiv = true;
            }

            MohletBolguHesabla();
        }

        /// <summary>
        /// 🗓️ Tarixi istifadəçidən soruşur ✓
        /// <para>⛔ «Ləğv» / boş cavab → <b>null</b> qaytarır ✓ → çağıran əməliyyat DAYANIR ✓✓✓</para>
        /// <para>⚠ Səhv format → xəbərdarlıq + YENİDƏN soruşulur ✓ (səssizcə bu gün yazılmır ✗)</para>
        /// </summary>
        private DateTime? TarixSor(string basliq, string mesaj, DateTime? defaultTarix = null)
        {
            var esas = defaultTarix ?? DateTime.Today;

            var formatlar = new[] { "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };

            while (true)
            {
                var cavab = _dialogs.ShowInputDialog(mesaj, basliq, esas.ToString("dd.MM.yyyy"));

                // ⛔ v6.2.52 — «✖ Ləğv» basıldı → ƏMƏLİYYAT LƏĞV EDİLİR ✓✓✓
                // ⚠ ƏVVƏL: burada `esas` (bu gün) qaytarılırdı ✗ → ödəniş yenə yazılırdı ✗✓✓
                if (string.IsNullOrWhiteSpace(cavab))
                {
                    return null;
                }

                if (DateTime.TryParseExact(cavab.Trim(), formatlar,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var t))
                {
                    return t.Date;
                }

                if (DateTime.TryParse(cavab.Trim(), out t))
                {
                    return t.Date;
                }

                _dialogs.ShowWarning(
                    $"⚠ «{cavab}» düzgün tarix deyil ✗\n\n" +
                    $"Format: gg.aa.yyyy  (məsələn: {esas:dd.MM.yyyy}) ✓\n" +
                    "Yenidən yazın ✓");
            }
        }

        /// <summary>
        /// ⏳ <b>TAM ÖDƏNİLMƏDİ</b> — qismən ödəniş (v6.2.51) ✓✓✓
        /// <para>① Nə qədər ödənildi ② Ödəniş tarixi ③ Qalan nə vaxt ödəniləcək ✓</para>
        /// <para>📌 Ödənilən hissə dərhal KASSAYA daxil olur ✓ · qalan hissə yeni tarixlə qalır ✓</para>
        /// </summary>
        [RelayCommand]
        private async Task MohletQismenOdeAsync(OdenisMohlet? mohlet)
        {
            if (mohlet is null || mohlet.Id <= 0 || mohlet.Mebleg <= 0m)
            {
                return;
            }

            var meblegMetni = _dialogs.ShowInputDialog(
                $"📌 {mohlet.MenbeMetni}\nÜmumi möhlət: {mohlet.Mebleg:N2} ₼\n\n① ÖDƏNİLƏN MƏBLƏĞ (₼):",
                "⏳ Tam ödənilmədi",
                mohlet.Mebleg.ToString("N2"));

            if (string.IsNullOrWhiteSpace(meblegMetni))
            {
                return;
            }

            var temiz = meblegMetni.Trim().Replace(" ", string.Empty).Replace(',', '.');

            if (!decimal.TryParse(temiz, System.Globalization.NumberStyles.Any,
                                  System.Globalization.CultureInfo.InvariantCulture, out var odenilen))
            {
                _dialogs.ShowWarning("⚠ Məbləğ düzgün deyil ✗ — yalnız rəqəm yazın ✓");
                return;
            }

            if (odenilen <= 0m || odenilen >= mohlet.Mebleg)
            {
                _dialogs.ShowWarning(
                    $"⚠ Məbləğ 0-dan BÖYÜK və {mohlet.Mebleg:N2} ₼-dən KİÇİK olmalıdır ✓\n\n" +
                    "Hamısı ödənildisə «✅ TAM ÖDƏNİLDİ» düyməsini basın ✓");
                return;
            }

            // ⛔ «Ləğv» basılıbsa → heç nə yazılmır ✓✓✓
            var odenisTarixiSor = TarixSor("⏳ Qismən ödəniş",
                $"Ödənilən: {odenilen:N2} ₼\n\n② ÖDƏNİŞ TARİXİ (gg.aa.yyyy):");

            if (odenisTarixiSor is null)
            {
                return;
            }

            var odenisTarixi = odenisTarixiSor.Value;

            var qaliq = Math.Round(mohlet.Mebleg - odenilen, 2);

            var qalanTarixSor = TarixSor("⏳ Qalan məbləğ",
                $"Qalan: {qaliq:N2} ₼\n\n③ QALAN NƏ VAXT ÖDƏNİLƏCƏK? (gg.aa.yyyy):",
                mohlet.Tarix > DateTime.Today ? mohlet.Tarix : DateTime.Today.AddDays(7));

            if (qalanTarixSor is null)
            {
                return;
            }

            var qalanTarix = qalanTarixSor.Value;

            try
            {
                var odenilmis = await _mohletler.AddAsync(new OdenisMohlet
                {
                    Menbe = mohlet.Menbe,
                    CreditId = mohlet.CreditId,
                    SaleId = mohlet.SaleId,
                    Tarix = odenisTarixi,
                    Mebleg = odenilen,
                    Sira = mohlet.Sira,
                    Qeyd = "⏳ Qismən ödəniş ✓"
                });

                // ✅ v6.2.53 — BİRBAŞA yeniləmə ✓ (SetOdenildiAsync səssiz uğursuz olurdu ✗)
                odenilmis.Odenilib = true;
                odenilmis.OdenilmeTarixi = odenisTarixi.Date;
                await _mohletler.UpdateAsync(odenilmis);

                // ⛔ v6.2.53 — qismən ödəniş də AYRICA kreditə yazılmır ✗
                //    (möhlət sətri artıq kassaya düşür ✓ → ikiqat olmaz ✓)

                mohlet.Mebleg = qaliq;
                mohlet.Tarix = qalanTarix;
                await _mohletler.UpdateAsync(mohlet);

                await LoadAsync();

                MelumatDegisdi();   // 📢 v6.2.79 — digər tablar yenilənsin ✓

                _dialogs.ShowInfo(
                    "⏳ QİSMƏN ÖDƏNİŞ YAZILDI ✓✓✓\n\n" +
                    $"📌 {mohlet.MenbeMetni}\n\n" +
                    $"💵 Ödənildi : {odenilen:N2} ₼   ({odenisTarixi:dd.MM.yyyy})\n" +
                    $"⏳ Qalıq    : {qaliq:N2} ₼   ({qalanTarix:dd.MM.yyyy})\n\n" +
                    "✅ Ödənilən hissə KASSAYA daxil edildi ✓\n" +
                    "✅ Qalan hissə yeni tarixlə möhlətlərdə qaldı ✓");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Qismən ödəniş yazıla bilmədi");
                _dialogs.ShowError("Qismən ödəniş yazıla bilmədi: " + ex.Message);
            }
        }

        /// <summary>✅ Möhləti ÖDƏNİLDİ işarələyir → pul DƏRHAL kassaya daxil olur ✓✓✓</summary>
        [RelayCommand]
        private async Task MohletOdenildiAsync(OdenisMohlet? mohlet)
        {
            if (mohlet is null || mohlet.Id <= 0)
            {
                return;
            }

            // 🗓️ v6.2.51 — TARİX MÜTLƏQ SORUŞULUR ✓✓✓ (istifadəçi tələbi ✓)
            var tarixSor = TarixSor("✅ Tam ödəniş",
                $"📌 {mohlet.MenbeMetni}\nMəbləğ: {mohlet.Mebleg:N2} ₼\n\nÖDƏNİŞ TARİXİ (gg.aa.yyyy):");

            // ⛔ v6.2.52 — «✖ Ləğv» → ÖDƏNİŞ YAZILMIR ✓✓✓ (əvvəl bu gün yazılırdı ✗)
            if (tarixSor is null)
            {
                return;
            }

            var tarix = tarixSor.Value;

            try
            {
                // ✅ v6.2.53 — BİRBAŞA yeniləmə ✓ (SetOdenildiAsync səssiz uğursuz olurdu ✗)
                mohlet.Odenilib = true;
                mohlet.OdenilmeTarixi = tarix.Date;
                await _mohletler.UpdateAsync(mohlet);

                // ================================================================
                //  ⛔ v6.2.53 — KREDİTƏ AYRICA «Gəlir» YAZILMIR ✗✓✓
                //  ⚠ SƏBƏB: möhlət ödənişi KASSAYA ARTIQ avtomatik düşür ✓
                //     (KassaHesabi → «⏳ İlkin ödəniş möhləti» ✓)
                //     + CreditTransaction əlavə etsək → KASSADA İKİQAT yazı ✗
                //     (200 ₼ → 400 ₼ görünürdü ✗✓✓ — istifadəçi şikayəti ✓)
                //  ✅ İNDİ: yalnız möhlət yazılır ✓ — kassa DƏQİQ ✓
                // ================================================================

                _logger.LogInformation(
                    "✅ Möhlət ödənildi: {Menbe} · {Mebleg:N2} ₼ · {Tarix:dd.MM.yyyy}",
                    mohlet.MenbeMetni, mohlet.Mebleg, tarix);

                await LoadAsync();

                MelumatDegisdi();   // 📢 v6.2.79 — digər tablar yenilənsin ✓

                _dialogs.ShowInfo(
                    "✅ MÖHLƏT ÖDƏNİLDİ ✓\n\n" +
                    $"{mohlet.MenbeMetni}\n" +
                    $"Məbləğ : {mohlet.Mebleg:N2} ₼\n" +
                    $"Tarix  : {tarix:dd.MM.yyyy}\n\n" +
                    "💰 Bu məbləğ artıq KASSAYA DAXİL OLDU ✓ (jurnalda görünür ✓)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Möhlət ödənildi kimi işarələnə bilmədi");
                _dialogs.ShowError("Möhlət işarələnə bilmədi: " + ex.Message);
            }
        }

        /// <summary>↩ Səhvən ödənildi işarələnibsə → geri qaytarır (pul kassadan çıxır ✗).</summary>
        [RelayCommand]
        private async Task MohletGeriAlAsync(OdenisMohlet? mohlet)
        {
            if (mohlet is null || mohlet.Id <= 0)
            {
                return;
            }

            var cavab = _dialogs.Confirm(
                $"{mohlet.MenbeMetni}\n{mohlet.Mebleg:N2} ₼\n\n" +
                "«Ödənildi» işarəsi GERİ ALINSIN? (məbləğ kassadan çıxacaq ✗)",
                "↩ Geri alma");

            if (!cavab)
            {
                return;
            }

            try
            {
                // ================================================================
                //  ✅ v6.2.51 — LƏĞV: BİRBAŞA UpdateAsync ilə yazılır ✓✓✓
                //  ⚠ ƏVVƏL: SetOdenildiAsync(...) nəticəsi YOXLANILMIRDI ✗ →
                //     uğursuz olsa belə proqram «ləğv olundu» deyirdi ✗ (səssiz ✗)
                //  ✅ İNDİ: entity-nin özü yenilənir ✓ + nəticə yoxlanılır ✓
                // ================================================================
                mohlet.Odenilib = false;
                mohlet.OdenilmeTarixi = null;

                // ================================================================
                //  💰 v6.2.52 — «A»: QİSMƏN ÖDƏNİŞ GERİ ALINIRSA
                //  → orijinal möhlətin MƏBLƏĞİ və TARİXİ tam BƏRPA olunur ✓✓✓
                //  ⚠ ƏVVƏL: yalnız bu sətir «ödənilmədi» olurdu ✗ →
                //     orijinalın AZALDILMIŞ məbləği qalırdı ✗ → pul «yox olurdu» ✗✓✓
                // ================================================================
                if (mohlet.Qeyd?.Contains("Qismən", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var orijinal = GozlenilenMohletler.FirstOrDefault(m =>
                        m.Id != mohlet.Id
                        && !m.Odenilib
                        && m.CreditId == mohlet.CreditId
                        && m.SaleId == mohlet.SaleId
                        && m.Sira == mohlet.Sira
                        && string.Equals(m.Menbe, mohlet.Menbe, StringComparison.Ordinal));

                    if (orijinal is not null)
                    {
                        // ① məbləğ geri qaytarılır ✓ (5 000 → 2 000 ödənildi → 5 000 ✓)
                        orijinal.Mebleg = Math.Round(orijinal.Mebleg + mohlet.Mebleg, 2);

                        // ② orijinal tarix bərpa olunur ✓
                        orijinal.Tarix = mohlet.Tarix;

                        await _mohletler.UpdateAsync(orijinal);

                        _logger.LogInformation(
                            "↩ Qismən ödəniş geri alındı → orijinal bərpa: #{Id} · {Mebleg:N2} ₼ ({Tarix:dd.MM.yyyy})",
                            orijinal.Id, orijinal.Mebleg, orijinal.Tarix);
                    }
                }

                await _mohletler.UpdateAsync(mohlet);

                // ================================================================
                //  🗑️ v6.2.79 — TƏRƏFDAŞ PAYLARI DA SİLİNİR ✓✓✓
                // ----------------------------------------------------------------
                //  ★ İstifadəçi tələbi: «geri alım edəndə ödənilmiş möhlət
                //    tərəfdaşlar bölgüsündən də silinməlidir» ✓✓✓
                //  🔗 Marker (`möhlət #Id`) ilə DƏQİQ sətir tapılır ✓ →
                //     payları ilə birlikdə silinir ✓
                // ================================================================
                var silinenPaylar = await MohletBolguPaylariniSilAsync(mohlet.Id);

                OdenilmisMohletler.Remove(mohlet);

                OnPropertyChanged(nameof(HasOdenilmis));
                OnPropertyChanged(nameof(OdenilmisCem));

                await LoadAsync();

                // 📢 Bütün tablar yenilənsin ✓ (Tərəfdaşlar · Maliyyə ✓)
                MelumatDegisdi();

                _dialogs.ShowInfo(
                    "↩️ GERİ ALINDI ✓✓✓\n\n" +
                    $"📌 {mohlet.MenbeMetni}\n" +
                    $"Məbləğ: {mohlet.Mebleg:N2} ₼\n\n" +
                    "✅ Möhlət yenidən «GÖZLƏNİLƏN MÖHLƏTLƏR» siyahısındadır ✓\n" +
                    "⚠ Pul kassadan ÇIXARILDI ✓ (jurnal yeniləndi ✓)" +
                    (silinenPaylar > 0
                        ? $"\n🗑️ Tərəfdaş bölgüsü də SİLİNDİ ✓ ({silinenPaylar} sətir)"
                        : string.Empty));

                _logger.LogInformation("↩ Möhlətin ödəniş işarəsi geri alındı: {Id}", mohlet.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Möhlətin işarəsi geri alına bilmədi");
                _dialogs.ShowError("Geri alına bilmədi: " + ex.Message);
            }
        }

        /// <summary>💾 Tarix / məbləğ düzəlişini bazaya yazır ✓ (cədvəldə düzəlt → bas ✓).</summary>
        [RelayCommand]
        private async Task MohletYaddaAsync(OdenisMohlet? mohlet)
        {
            if (mohlet is null || mohlet.Id <= 0)
            {
                return;
            }

            if (mohlet.Mebleg <= 0m)
            {
                _dialogs.ShowWarning("Möhlətin məbləği 0-dan böyük olmalıdır ✓");
                return;
            }

            try
            {
                await _mohletler.UpdateAsync(mohlet);
                await LoadAsync();

                _dialogs.ShowInfo($"💾 Yadda saxlanıldı ✓\n\n{mohlet.MenbeMetni}\n{mohlet.Xulase}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Möhlət yenilənə bilmədi");
                _dialogs.ShowError("Möhlət yenilənə bilmədi: " + ex.Message);
            }
        }

        /// <summary>✖ Möhləti tamamilə silir (təsdiqlə ✓✓✓).</summary>
        [RelayCommand]
        private async Task MohletSilAsync(OdenisMohlet? mohlet)
        {
            if (mohlet is null || mohlet.Id <= 0)
            {
                return;
            }

            var cavab = _dialogs.Confirm(
                $"{mohlet.MenbeMetni}\n{mohlet.Xulase}\n\n" +
                "Bu möhlət qeydi SİLİNSİN?",
                "🗑 Silmə təsdiqi");

            if (!cavab)
            {
                return;
            }

            try
            {
                // 🗑️ v6.2.79 — möhlətə bağlı TƏRƏFDAŞ BÖLGÜSÜ də silinir ✓✓✓
                var silinenPaylar = await MohletBolguPaylariniSilAsync(mohlet.Id);

                await _mohletler.DeleteAsync(mohlet.Id);
                await LoadAsync();

                MelumatDegisdi();

                _logger.LogInformation(
                    "🗑 Möhlət silindi: {Id} · tərəfdaş bölgüsü: {Pay} sətir",
                    mohlet.Id, silinenPaylar);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Möhlət silinə bilmədi");
                _dialogs.ShowError("Möhlət silinə bilmədi: " + ex.Message);
            }
        }

        // ====================================================================
        //  🔄 YÜKLƏMƏ  (tab açıldıqda ✓ · dövrdə dəyişiklik olanda ✓ · yazıldıqdan sonra ✓)
        // --------------------------------------------------------------------
        //  ⚠ Adı <b>LoadAsync</b> olmalıdır ✓ — MainViewModel onu reflection ilə tapır ✓✓✓
        // ====================================================================
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);

            try
            {
                var (from, to) = GetRange();
                DovrMetni = $"{from:dd.MM.yyyy} → {to:dd.MM.yyyy}";

                // ================================================================
                //  ① 📊 HESABAT — VAHİD DÜSTUR ✓✓✓  (KassaHesabi — TƏKRARLANMIR ✗)
                // ================================================================
                var hesabat = await _kassa.BuildAsync(from, to, cancellationToken);

                _butunSetirler = hesabat.Setirler.ToList();

                AcilisQaligi = hesabat.AcilisQaligi;
                Daxilolma = hesabat.Daxilolma;
                Xerc = hesabat.Xerc;
                Xalis = hesabat.Xalis;
                Qaliq = hesabat.Qaliq;
                ProqnozQaliq = hesabat.ProqnozQaliq;
                SetirSayi = hesabat.SetirSayi;

                SatisDaxilolma = hesabat.SatisDaxilolma;
                IlkinOdenisDaxilolma = hesabat.IlkinOdenisDaxilolma;
                KreditDaxilolma = hesabat.KreditDaxilolma;
                MohletDaxilolma = hesabat.MohletDaxilolma;
                ElIleDaxilolma = hesabat.ElIleDaxilolma;

                XercCemi = hesabat.XercCemi;
                KreditXerci = hesabat.KreditXerci;
                TerefdasOdenilen = hesabat.TerefdasOdenilen;
                ElIleXerci = hesabat.ElIleXerci;

                TerefdasPayi = hesabat.TerefdasPayi;
                TerefdasVerilmeli = hesabat.TerefdasVerilmeli;
                OzQaliq = hesabat.OzQaliq;
                OzProqnozQaliq = hesabat.OzProqnozQaliq;

                GozlenilenMohlet = hesabat.GozlenilenMohlet;
                GecikmisMohlet = hesabat.GecikmisMohlet;

                // ================================================================
                //  ② ✍ ƏL İLƏ YAZILAN HƏRƏKƏTLƏR  (ən yenidən köhnəyə ✓)
                // ================================================================
                var elIle = await _kassa.GetHereketlerAsync(cancellationToken);

                // 🚀 v6.2.81 — TƏK Reset ✓
                ManuelHereketler.ReplaceAll(elIle);

                // ================================================================
                //  ③ ⏳ MÖHLƏTLƏR  («nə vaxt, nə qədər pul gələcək» ✓✓✓)
                // ================================================================
                var butunMohletler = await _mohletler.GetAllAsync(cancellationToken);

                // ================================================================
                //  📦 v6.2.78 — KREDİT / SATIŞ KEŞİ ✓✓✓
                // ----------------------------------------------------------------
                //  Möhlətin MAYA (kök) payını SINXRON hesablamaq üçün ✓ —
                //  hər klikdə bazaya sorğu getmir ✗✓✓ (performans ✓)
                // ================================================================
                try
                {
                    _kreditKeshi = (await _credits.GetCreditsAsync(cancellationToken)).ToList();
                    _satisKeshi = (await _satislar.GetSalesAsync(cancellationToken)).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Kassa: kredit/satış keşi yüklənmədi ✗ (MAYA 0 qalacaq)");
                }

                var gozlenilen = butunMohletler
                    .Where(m => !m.Odenilib)
                    .OrderBy(m => m.Tarix)
                    .ToList();

                // ================================================================
                //  📅 v6.2.51 — ÖDƏNİLMİŞ MÖHLƏTLƏR **DÖVR ÜZRƏ** ✓✓✓
                //  ★ İstifadəçi tələbi: «hansı ayı seçsək O AYIN ödənilmiş
                //    möhlətləri görünməlidir» ✓
                //  → yalnız seçilmiş dövrün ödəniş tarixi olanlar ✓
                // ================================================================
                var odenilmis = butunMohletler
                    .Where(m => m.Odenilib
                                && m.OdenilmeTarixi.HasValue
                                && m.OdenilmeTarixi.Value >= from.Date
                                && m.OdenilmeTarixi.Value <= to.Date)
                    .OrderByDescending(m => m.OdenilmeTarixi ?? m.Tarix)
                    .Take(300)
                    .ToList();

                GozlenilenMohletler.ReplaceAll(gozlenilen);

                OdenilmisMohletler.ReplaceAll(odenilmis);

                OdenilmisCem = butunMohletler.Where(m => m.Odenilib).Sum(m => m.Mebleg);

                YaxinHefteMohlet = gozlenilen
                    .Where(m => m.QalanGun >= 0 && m.QalanGun <= 7)
                    .Sum(m => m.Mebleg);

                OnPropertyChanged(nameof(GozlenilenSayi));
                OnPropertyChanged(nameof(GecikmisSayi));
                OnPropertyChanged(nameof(HasGozlenilen));
                OnPropertyChanged(nameof(HasOdenilmis));
                OnPropertyChanged(nameof(MohletYoxdur));
                OnPropertyChanged(nameof(HereketYoxdur));

                // ================================================================
                //  👥 v6.2.77 — MÖHLƏT BÖLGÜSÜ YENİLƏNİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ Seçilmiş möhlət artıq siyahıda yoxdursa (ödənildi / silindi)
                //    → seçim sıfırlanır ✓ və ödəniş paneli BOŞALIR ✓✓✓
                // ================================================================
                if (SelectedGozlenilenMohlet is OdenisMohlet secili
                    && !GozlenilenMohletler.Any(m => m.Id == secili.Id))
                {
                    SelectedGozlenilenMohlet = null;
                }

                MohletOdenisiniHazirla();

                // ================================================================
                //  ④ 🏷️ BÖLMƏLƏR ÜZRƏ CƏM + 📒 JURNAL (filtrlərlə ✓)
                // ================================================================
                QrupCemleriniQur();
                Filtrle();

                _logger.LogInformation(
                    "💵 Kassa tabı yükləndi: {From:dd.MM.yyyy} → {To:dd.MM.yyyy} · daxil {Daxil:N2} ₼ · xərc {Xerc:N2} ₼",
                    from, to, Daxilolma, Xerc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kassa tabı yüklənə bilmədi");
                SetirlerInfo = "⚠ Kassa hesabatı yüklənə bilmədi: " + ex.Message;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>🖱 «↻ Yenilə» düyməsi.</summary>
        [RelayCommand]
        private Task Yenile() => LoadAsync();

        /// <summary>
        /// 🔍 <b>JURNALI FİLTRLƏYİR</b> ✓✓✓ — növ (➕/➖) · qrup (Satış · Xərc …) ✓
        /// və axtarış mətni (müqavilə № · müştəri · maşın ✓) üzrə ✓
        /// <para>⚠ CƏMLƏR (yuxarıdaki kartlar) <b>DƏYİŞMİR</b> ✗ — onlar DÖVR üzrədir ✓✓✓</para>
        /// </summary>
        private void Filtrle()
        {
            if (_filtrlenir)
            {
                return;
            }

            _filtrlenir = true;

            try
            {
                var setirler = _butunSetirler.AsEnumerable();

                // ➕ / ➖
                if (NovFiltri.StartsWith("➕", StringComparison.Ordinal))
                {
                    setirler = setirler.Where(s => s.IsDaxilolma);
                }
                else if (NovFiltri.StartsWith("➖", StringComparison.Ordinal))
                {
                    setirler = setirler.Where(s => !s.IsDaxilolma);
                }

                // 🏷️ Qrup
                if (!string.IsNullOrWhiteSpace(QrupFiltri) && QrupFiltri != "Hamısı")
                {
                    setirler = setirler.Where(s => s.Qrup == QrupFiltri);
                }

                // 🔎 Axtarış («rebbil» · «M-0007» · «Malibu» ✓)
                var axtaris = (Axtaris ?? string.Empty).Trim();

                if (axtaris.Length > 0)
                {
                    var axtarisKicik = axtaris.ToLowerInvariant();
                    setirler = setirler.Where(s => s.AxtarisMetni.Contains(axtarisKicik));
                }

                var siyahi = setirler.ToList();

                Setirler.ReplaceAll(siyahi);

                var daxil = siyahi.Where(s => s.IsDaxilolma).Sum(s => s.Mebleg);
                var xerc = siyahi.Where(s => !s.IsDaxilolma).Sum(s => s.Mebleg);

                SetirlerInfo =
                    $"📒 {siyahi.Count} sətir  ·  ➕ daxil {daxil:N2} ₼  ·  ➖ xərc {xerc:N2} ₼  ·  " +
                    $"xalis {daxil - xerc:N2} ₼";

                // 🏷️ Qrup filtri siyahısına yeni qruplar əlavə olunur ✓
                QrupFiltreleriniYenile();
            }
            finally
            {
                _filtrlenir = false;
            }
        }

        /// <summary>🏷️ Qrup filtrindəki variantları jurnal sətirlərindən yeniləyir ✓.</summary>
        private void QrupFiltreleriniYenile()
        {
            foreach (var qrup in _butunSetirler
                         .Select(s => s.Qrup)
                         .Where(q => !string.IsNullOrWhiteSpace(q))
                         .Distinct()
                         .OrderBy(q => q))
            {
                if (!QrupFiltreleri.Contains(qrup))
                {
                    QrupFiltreleri.Add(qrup);
                }
            }
        }

        /// <summary>
        /// 🏷️ <b>BÖLMƏLƏR ÜZRƏ CƏM</b> ✓✓✓ — «Satış» nə qədər gətirdi ✓,
        /// «Xərc» nə qədər apardı ✗ (jurnal sətirlərindən hesablanır ✓)
        /// </summary>
        private void QrupCemleriniQur()
        {
            QrupCemleri.Clear();

            var qruplar = _butunSetirler
                .GroupBy(s => s.Qrup)
                .OrderByDescending(g => g.Where(s => s.IsDaxilolma).Sum(s => s.Mebleg))
                .ThenBy(g => g.Key);

            foreach (var qrup in qruplar)
            {
                QrupCemleri.Add(new KassaQrupCem
                {
                    Qrup = qrup.Key,
                    Gelir = qrup.Where(s => s.IsDaxilolma).Sum(s => s.Mebleg),
                    Xerc = qrup.Where(s => !s.IsDaxilolma).Sum(s => s.Mebleg),
                    SetirSayi = qrup.Count()
                });
            }
        }

        // ====================================================================
        //  🗓️ DÖVRÜN HESABLANMASI  (Maliyyə Paneli ilə EYNİ məntiq ✓✓✓)
        // ====================================================================
        private (DateTime From, DateTime To) GetRange()
        {
            var today = DateTime.Today;
            var ayBasi = new DateTime(today.Year, today.Month, 1);

            return SelectedRange switch
            {
                "Bu gün" => (today, today),
                "Bu həftə" => (today.AddDays(-(int)today.DayOfWeek), today),
                "Son 7 gün" => (today.AddDays(-6), today),
                "Bu ay" => (ayBasi, today),
                "Son 30 gün" => (today.AddDays(-29), today),
                "Keçən ay" => (ayBasi.AddMonths(-1), ayBasi.AddDays(-1)),
                "Bu il" => (new DateTime(today.Year, 1, 1), today),
                "Son 12 ay" => (ayBasi.AddMonths(-11), today),
                "Fərdi aralıq" => (CustomFrom?.Date ?? ayBasi, CustomTo?.Date ?? today),
                _ => (new DateTime(2000, 1, 1), today)   // 📜 Bütün vaxt ✓
            };
        }
    }
}
