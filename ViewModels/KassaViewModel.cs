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
        private readonly IDialogService _dialogs;
        private readonly ILogger<KassaViewModel> _logger;

        /// <summary>Eyni anda iki yükləmənin işləməsinin qarşısını alır ✓.</summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>Filtrləmə zamanı geri-əlaqə (re-entrancy) dövrünün qarşısını alır ✓.</summary>
        private bool _filtrlenir;

        /// <summary>Son hesabatın XAM sətirləri (filtr tətbiq olunmamış ✓✓✓).</summary>
        private List<KassaSetiri> _butunSetirler = new();

        public KassaViewModel(
            IKassaService kassa,
            IMohletService mohletler,
            IDialogService dialogs,
            ILogger<KassaViewModel> logger)
        {
            _kassa = kassa;
            _mohletler = mohletler;
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
        public string XalisRengi => Xalis >= 0m ? "#34D399" : "#FB7185";

        /// <summary>Kassa qalığının rəngi (mənfi = qırmızı ⚠).</summary>
        public string QaliqRengi => Qaliq >= 0m ? "#38BDF8" : "#FB7185";

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
        public ObservableCollection<KassaHereket> ManuelHereketler { get; } = new();

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
        public ObservableCollection<OdenisMohlet> GozlenilenMohletler { get; } = new();

        /// <summary>✅ Son ödənilmiş möhlətlər (ən yenisi əvvəldə ✓).</summary>
        public ObservableCollection<OdenisMohlet> OdenilmisMohletler { get; } = new();

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

        /// <summary>✅ Möhləti ÖDƏNİLDİ işarələyir → pul DƏRHAL kassaya daxil olur ✓✓✓</summary>
        [RelayCommand]
        private async Task MohletOdenildiAsync(OdenisMohlet? mohlet)
        {
            if (mohlet is null || mohlet.Id <= 0)
            {
                return;
            }

            var tarix = DateTime.Today;

            try
            {
                await _mohletler.SetOdenildiAsync(mohlet.Id, true, tarix);

                _logger.LogInformation(
                    "✅ Möhlət ödənildi: {Menbe} · {Mebleg:N2} ₼ · {Tarix:dd.MM.yyyy}",
                    mohlet.MenbeMetni, mohlet.Mebleg, tarix);

                await LoadAsync();

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
                await _mohletler.SetOdenildiAsync(mohlet.Id, false, null);
                await LoadAsync();

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
                await _mohletler.DeleteAsync(mohlet.Id);
                await LoadAsync();

                _logger.LogInformation("🗑 Möhlət silindi: {Id}", mohlet.Id);
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

                ManuelHereketler.Clear();

                foreach (var hereket in elIle)
                {
                    ManuelHereketler.Add(hereket);
                }

                // ================================================================
                //  ③ ⏳ MÖHLƏTLƏR  («nə vaxt, nə qədər pul gələcək» ✓✓✓)
                // ================================================================
                var butunMohletler = await _mohletler.GetAllAsync(cancellationToken);

                var gozlenilen = butunMohletler
                    .Where(m => !m.Odenilib)
                    .OrderBy(m => m.Tarix)
                    .ToList();

                var odenilmis = butunMohletler
                    .Where(m => m.Odenilib)
                    .OrderByDescending(m => m.OdenilmeTarixi ?? m.Tarix)
                    .Take(100)
                    .ToList();

                GozlenilenMohletler.Clear();
                foreach (var mohlet in gozlenilen)
                {
                    GozlenilenMohletler.Add(mohlet);
                }

                OdenilmisMohletler.Clear();
                foreach (var mohlet in odenilmis)
                {
                    OdenilmisMohletler.Add(mohlet);
                }

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
