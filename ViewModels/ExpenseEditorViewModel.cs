using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// Xərc əlavə etmə formasının vəziyyəti və validasiyası (INotifyDataErrorInfo).
    /// </summary>
    public sealed partial class ExpenseEditorViewModel : ObservableValidator
    {
        /// <summary>Siyahıların ən sonunda göstərilən "əlavə et" seçimləri.</summary>
        public const string AddGroupOption = "➕ Yeni qrup əlavə et…";
        public const string AddCategoryOption = "➕ Yeni kateqoriya əlavə et…";

        /// <summary>Siyahıların ən sonunda göstərilən "sil" seçimləri.</summary>
        public const string DeleteGroupOption = "🗑 Seçilmiş qrupu sil…";
        public const string DeleteCategoryOption = "🗑 Seçilmiş kateqoriyanı sil…";

        /// <summary>Verilmiş dəyər qrup siyahısının xüsusi (əlavə/sil) seçimidirmi?</summary>
        public static bool IsGroupOption(string? value)
            => value == AddGroupOption || value == DeleteGroupOption;

        /// <summary>Verilmiş dəyər kateqoriya siyahısının xüsusi (əlavə/sil) seçimidirmi?</summary>
        public static bool IsCategoryOption(string? value)
            => value == AddCategoryOption || value == DeleteCategoryOption;

        private readonly IExpenseCatalogService _catalog;
        private readonly IDialogService _dialogs;
        private readonly List<CarItem> _allCars = new();

        /// <summary>Silmə əməliyyatı üçün sonuncu real seçimlər.</summary>
        private string _lastGroup = string.Empty;
        private string _lastCategory = string.Empty;

        /// <summary>Filtrləmə zamanı geri-əlaqə (re-entrancy) dövrünün qarşısını alır.</summary>
        private bool _applyingFilter;

        /// <summary>Kataloq yenilənərkən hadisələrin təkrar işə düşməsinin qarşısını alır.</summary>
        private bool _suppressCatalogEvents;

        private bool _loadingCatalog;

        public IReadOnlyList<string> Destinations { get; } = Catalog.ExpenseDestinations;
        public IReadOnlyList<string> PaymentMethods { get; } = Catalog.PaymentMethods;

        public ObservableCollection<CarItem> FilteredCars { get; } = new();

        /// <summary>Qrup/kateqoriya əlavə edildikdə baş verir (cədvəlin siyahılarını yeniləmək üçün).</summary>
        public event EventHandler? CatalogChanged;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsCarExpense))]
        private string teyinat = Catalog.CarDestination;

        [ObservableProperty]
        [Required(ErrorMessage = "Xərc qrupu seçilməlidir.")]
        private string qrup = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Kateqoriya / xərc adı tələb olunur.")]
        [MaxLength(150, ErrorMessage = "Kateqoriya 150 simvoldan çox ola bilməz.")]
        private string kategoriya = string.Empty;

        [ObservableProperty]
        private CarItem? selectedCar;

        [ObservableProperty]
        private string carSearchText = string.Empty;

        // ====================================================================
        //  SÜRƏTLİ KATEQORİYA AXTARIŞI  (qrup + kateqoriya birlikdə)
        // --------------------------------------------------------------------
        //  İstifadəçi xərc adını yazır — BÜTÜN QRUPLLARIN kateqoriyaları
        //  arasından uyğun olanlar göstərilir. Birini seçəndə həm QRUP,
        //  həm KATEQORİYA avtomatik dolur (qrupu ayrıca axtarmaq lazım deyil).
        // ====================================================================

        /// <summary>Bütün qrupların bütün kateqoriyaları (sürətli axtarış mənbəyi).</summary>
        private readonly List<CategorySuggestion> _allSuggestions = new();

        /// <summary>Sürətli axtarış nəticələri (yazıldıqca süzülür).</summary>
        public ObservableCollection<CategorySuggestion> Suggestions { get; } = new();

        /// <summary>Sürətli axtarış mətninin başlığı.</summary>
        [ObservableProperty] private string categorySearchText = string.Empty;

        /// <summary>Sürətli axtarışdan seçilmiş kateqoriya təklifi.</summary>
        [ObservableProperty] private CategorySuggestion? selectedSuggestion;

        /// <summary>Sürətli axtarışda göstərilən maksimum təklif sayı.</summary>
        private const int MaxSuggestions = 200;

        [ObservableProperty]
        [Range(typeof(decimal), "0.01", "999999999999", ErrorMessage = "Məbləğ 0-dan böyük olmalıdır.")]
        private decimal mebleg;

        [ObservableProperty]
        private string odenisUsulu = Catalog.PaymentMethods[0];

        [ObservableProperty]
        [Required(ErrorMessage = "Tarix seçilməlidir.")]
        private DateTime? tarix = DateTime.Today;

        [ObservableProperty]
        [MaxLength(300, ErrorMessage = "Qeyd 300 simvoldan çox ola bilməz.")]
        private string qeyd = string.Empty;

        /// <summary>Qrup siyahısı — ən sonunda "➕ Yeni qrup əlavə et…" seçimi var.</summary>
        public ObservableCollection<string> Groups { get; } = new();

        /// <summary>Kateqoriya siyahısı — ən sonunda "➕ Yeni kateqoriya əlavə et…" seçimi var.</summary>
        public ObservableCollection<string> Categories { get; } = new();

        public bool IsCarExpense => !Catalog.IsOffice(Teyinat);

        // ====================================================================
        //  💾 FORMA VƏZİYYƏTİNİN QORUNMASI ✓✓✓
        //  ⚠ SƏBƏB: `LoadAsync()` → `SetCars()` + `LoadCatalogAsync()` çağırır ✗
        //    → Qrup ✓ Kateqoriya ✓ Avtomobil ✓ seçimləri və yazılanlar SIFIRLANIRDI ✗
        //  ✅ HƏLL: yeniləmədən ƏVVƏL vəziyyət yadda saxlanılır ✓, SONRA bərpa olunur ✓✓✓
        // ====================================================================

        /// <summary>💾 Formanın cari vəziyyəti ✓ (seçilmişlər + yazılanlar ✓)</summary>
        public sealed record FormaVəziyyəti(
            string Teyinat,
            string Qrup,
            string Kategoriya,
            int? CarId,
            decimal Mebleg,
            string OdenisUsulu,
            DateTime? Tarix,
            string Qeyd,
            string CategorySearchText,
            string CarSearchText);

        /// <summary>💾 Formanın DOLU olub-olmadığı ✓ (boşdursa bərpaya ehtiyac yoxdur ✗)</summary>
        public bool FormaDoludur =>
            !string.IsNullOrWhiteSpace(Qrup)
            || !string.IsNullOrWhiteSpace(Kategoriya)
            || !string.IsNullOrWhiteSpace(Qeyd)
            || Mebleg > 0m
            || SelectedCar is not null
            || !string.IsNullOrWhiteSpace(CategorySearchText)
            || !string.IsNullOrWhiteSpace(CarSearchText);

        /// <summary>💾 <b>FORMA VƏZİYYƏTİNİ ALIR</b> ✓✓✓ (yeniləmədən əvvəl ✓)</summary>
        public FormaVəziyyəti VəziyyətiAl() => new(
            Teyinat,
            Qrup,
            Kategoriya,
            SelectedCar?.Id,
            Mebleg,
            OdenisUsulu,
            Tarix,
            Qeyd,
            CategorySearchText,
            CarSearchText);

        /// <summary>
        /// ♻️ <b>FORMA VƏZİYYƏTİNİ BƏRPA EDİR</b> ✓✓✓ — sinxron formu SİLMİR ✗
        /// <para>⚠ Sıra vacibdir ✓: əvvəl «Təyinat» ✓ (o, avtomobil seçimini təsir edir ✓), sonra qalanlar ✓, ən sonda avtomobil ✓</para>
        /// </summary>
        public void VəziyyətiQoy(FormaVəziyyəti v)
        {
            try
            {
                // ① TƏYİNAT (ofis xərcidirsə avtomobili null edir ✗ — ona görə ƏVVƏL ✓)
                if (!string.IsNullOrWhiteSpace(v.Teyinat))
                {
                    Teyinat = v.Teyinat;
                }

                // ② Qrup · Kateqoriya · Məbləğ · Üsul · Tarix · Qeyd ✓
                Qrup = v.Qrup;
                Kategoriya = v.Kategoriya;
                Mebleg = v.Mebleg;
                OdenisUsulu = v.OdenisUsulu;
                Tarix = v.Tarix;
                Qeyd = v.Qeyd;

                // ③ Axtarış mətnləri ✓
                CategorySearchText = v.CategorySearchText;
                CarSearchText = v.CarSearchText;

                // ④ 🚗 AVTOMOBİL — İd üzrə TAPIB qoy ✓✓✓
                if (v.CarId is int id && id > 0)
                {
                    foreach (var c in FilteredCars)
                    {
                        if (c.Id == id)
                        {
                            SelectedCar = c;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // 🛡️ bərpa alınmadı ✗ → xəta udulur ✓ (çökmə YOX ✗)
            }
        }

        public ExpenseEditorViewModel(IExpenseCatalogService catalog, IDialogService dialogs)
        {
            _catalog = catalog;
            _dialogs = dialogs;
            _ = LoadCatalogAsync();
            _ = LoadSuggestionsAsync();
        }

        partial void OnTeyinatChanged(string value)
        {
            if (Catalog.IsOffice(value))
            {
                SelectedCar = null;
            }

            if (!_suppressCatalogEvents)
            {
                _ = LoadCatalogAsync();
            }
        }

        partial void OnQrupChanged(string value)
        {
            if (_suppressCatalogEvents)
            {
                return;
            }

            if (value == AddGroupOption)
            {
                _ = AddGroupFlowAsync();
                return;
            }

            if (value == DeleteGroupOption)
            {
                _ = DeleteGroupFlowAsync();
                return;
            }

            _lastGroup = value;
            _ = LoadCategoriesAsync(selectDefault: true);
        }

        partial void OnKategoriyaChanged(string value)
        {
            if (_suppressCatalogEvents)
            {
                return;
            }

            if (value == AddCategoryOption)
            {
                _ = AddCategoryFlowAsync();
                return;
            }

            if (value == DeleteCategoryOption)
            {
                _ = DeleteCategoryFlowAsync();
                return;
            }

            _lastCategory = value;
        }

        partial void OnCarSearchTextChanged(string value) => ApplyCarFilter();

        // ====================================================================
        //  SÜRƏTLİ KATEQORİYA AXTARIŞI  -  hadisələr
        // ====================================================================

        partial void OnCategorySearchTextChanged(string value)
        {
            // Yazılan mətn seçilmiş təklifin TAM adıdırsa, filtri sıfırlamırıq
            // (əks halda seçim dərhal itərdi).
            if (SelectedSuggestion is not null &&
                string.Equals(value, SelectedSuggestion.Display, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ApplySuggestionFilter();
        }

        /// <summary>
        /// Təklif seçildi — TƏYİNAT, QRUP və KATEQORİYA avtomatik doldurulur,
        /// siyahılar isə yeni təyinata uyğun yenilənir.
        /// </summary>
        partial void OnSelectedSuggestionChanged(CategorySuggestion? value)
        {
            if (value is null)
            {
                return;
            }

            _ = ApplySuggestionAsync(value);
        }

        private async Task ApplySuggestionAsync(CategorySuggestion value)
        {
            try
            {
                // ---- 1) Təyinat + qrup səssizcə yazılır (hadisə işə düşməsin) ----
                _suppressCatalogEvents = true;
                try
                {
                    Teyinat = value.Teyinat;
                    SetQrupSilently(value.Qrup);
                }
                finally
                {
                    _suppressCatalogEvents = false;
                }

                // ---- 2) Siyahılar yeni təyinata/qrupa uyğunlaşdırılır ----
                var groups = await _catalog.GetGroupsAsync(value.Teyinat);
                FillOptions(Groups, groups, AddGroupOption, DeleteGroupOption);

                var categories = await _catalog.GetCategoriesAsync(value.Qrup);
                FillOptions(Categories, categories, AddCategoryOption, DeleteCategoryOption);

                // ---- 3) Seçim yerinə oturdulur ----
                _suppressCatalogEvents = true;
                try
                {
                    SetQrupSilently(value.Qrup);
                    SetKategoriyaSilently(value.Kategoriya);
                }
                finally
                {
                    _suppressCatalogEvents = false;
                }

                _lastGroup = value.Qrup;
                _lastCategory = value.Kategoriya;

                ApplySuggestionFilter();
            }
            catch
            {
                // Sürətli seçim alınmasa, istifadəçi siyahılardan əl ilə seçə bilər.
            }
        }

        // ====================================================================
        //  SÜRƏTLİ KATEQORİYA AXTARIŞI  -  yükləmə və süzgəc
        // ====================================================================

        /// <summary>
        /// BÜTÜN qrupların kateqoriyalarını sürətli axtarış üçün yükləyir.
        /// Kataloq dəyişdikdə (yeni qrup/kateqoriya) yenidən çağırılır.
        /// </summary>
        public async Task LoadSuggestionsAsync()
        {
            try
            {
                var entries = await _catalog.GetAllEntriesAsync();

                _allSuggestions.Clear();
                foreach (var entry in entries)
                {
                    _allSuggestions.Add(new CategorySuggestion
                    {
                        Teyinat = entry.Teyinat,
                        Qrup = entry.Qrup,
                        Kategoriya = entry.Kategoriya
                    });
                }

                ApplySuggestionFilter();
            }
            catch
            {
                // Təklif siyahısı alınmasa forma yenə işləyir.
            }
        }

        /// <summary>
        /// Yazılan mətnə görə təklifləri süzür. Mətn boşdursa BÜTÜN kateqoriyalar
        /// göstərilir (açılan siyahı kimi) — istifadəçi dərhal seçim edə bilir.
        /// </summary>
        private void ApplySuggestionFilter()
        {
            var term = CategorySearchText?.Trim() ?? string.Empty;

            // Seçilmiş elementin tam adı yazılıbsa, siyahını dəyişmirik.
            if (SelectedSuggestion is not null &&
                string.Equals(term, SelectedSuggestion.Display, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var matches = string.IsNullOrEmpty(term)
                ? _allSuggestions
                : _allSuggestions
                    .Where(s => s.SearchKey.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            Suggestions.Clear();
            foreach (var item in matches.Take(MaxSuggestions))
            {
                Suggestions.Add(item);
            }
        }

        public void SetCars(IEnumerable<CarItem> cars)
        {
            _allCars.Clear();
            _allCars.AddRange(cars);
            ApplyCarFilter();
        }

        /// <summary>Qrupları və kateqoriyaları bazadan yükləyir (ilk açılışda və yenilikdə).</summary>
        public async Task LoadCatalogAsync(bool selectDefault = true)
        {
            if (_loadingCatalog)
            {
                return;
            }

            _loadingCatalog = true;
            try
            {
                var groups = await _catalog.GetGroupsAsync(Teyinat);
                FillOptions(Groups, groups, AddGroupOption, DeleteGroupOption);

                var target = selectDefault || string.IsNullOrWhiteSpace(Qrup) || !groups.Contains(Qrup)
                    ? groups.FirstOrDefault() ?? string.Empty
                    : Qrup;

                SetQrupSilently(target);
                await LoadCategoriesAsync(selectDefault: true);

                // Sürətli axtarış təklifləri də təzələnir (yeni qrup/kateqoriya daxil).
                await LoadSuggestionsAsync();
            }
            finally
            {
                _loadingCatalog = false;
            }
        }

        /// <summary>Seçilmiş qrup üzrə kateqoriyaları yükləyir.</summary>
        private async Task LoadCategoriesAsync(bool selectDefault)
        {
            var categories = string.IsNullOrWhiteSpace(Qrup)
                ? Array.Empty<string>()
                : await _catalog.GetCategoriesAsync(Qrup);

            FillOptions(Categories, categories, AddCategoryOption, DeleteCategoryOption);

            var target = selectDefault || string.IsNullOrWhiteSpace(Kategoriya) || !categories.Contains(Kategoriya)
                ? categories.FirstOrDefault() ?? string.Empty
                : Kategoriya;

            SetKategoriyaSilently(target);
        }

        /// <summary>Qrupu proqram yolu ilə (hadisə işə salmadan) təyin edir.</summary>
        private void SetQrupSilently(string value)
        {
            _suppressCatalogEvents = true;
            Qrup = value;
            _suppressCatalogEvents = false;

            if (!IsGroupOption(value))
            {
                _lastGroup = value;
            }
        }

        /// <summary>Kateqoriyanı proqram yolu ilə (hadisə işə salmadan) təyin edir.</summary>
        private void SetKategoriyaSilently(string value)
        {
            _suppressCatalogEvents = true;
            Kategoriya = value;
            _suppressCatalogEvents = false;

            if (!IsCategoryOption(value))
            {
                _lastCategory = value;
            }
        }

        /// <summary>Siyahını doldurur və ən AŞAĞIYA "əlavə et" / "sil" seçimlərini yazır.</summary>
        private static void FillOptions(
            ObservableCollection<string> target,
            IReadOnlyList<string> values,
            string addOption,
            string deleteOption)
        {
            target.Clear();
            foreach (var value in values)
            {
                target.Add(value);
            }

            target.Add(addOption);
            target.Add(deleteOption);
        }

        /// <summary>Dialoq açıb yeni QRUPa əlavə edir; ləğv edilərsə əvvəlki qrup qalır.</summary>
        private async Task AddGroupFlowAsync()
        {
            var previous = string.IsNullOrWhiteSpace(_lastGroup)
                ? Groups.FirstOrDefault(g => !IsGroupOption(g)) ?? string.Empty
                : _lastGroup;

            var name = _dialogs.ShowInputDialog("Yeni xərc qrupu adını yazın:", "➕ Yeni Qrup");

            if (string.IsNullOrWhiteSpace(name))
            {
                SetQrupSilently(previous);
                return;
            }

            await _catalog.AddGroupAsync(Teyinat, name.Trim());

            await LoadCatalogAsync(selectDefault: false);
            SetQrupSilently(name.Trim());
            await LoadCategoriesAsync(selectDefault: true);

            _dialogs.ShowInfo($"\"{name.Trim()}\" qrupu əlavə edildi.", "Əlavə edildi");
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Dialoq açıb seçilmiş qrupa yeni KATEQORİYA əlavə edir.</summary>
        private async Task AddCategoryFlowAsync()
        {
            var previous = string.IsNullOrWhiteSpace(_lastCategory)
                ? Categories.FirstOrDefault(c => !IsCategoryOption(c)) ?? string.Empty
                : _lastCategory;

            var name = _dialogs.ShowInputDialog(
                $"\"{Qrup}\" qrupu üçün yeni kateqoriya adını yazın:", "➕ Yeni Kateqoriya");

            if (string.IsNullOrWhiteSpace(name))
            {
                SetKategoriyaSilently(previous);
                return;
            }

            await _catalog.AddCategoryAsync(Teyinat, Qrup, name.Trim());

            await LoadCategoriesAsync(selectDefault: false);
            SetKategoriyaSilently(name.Trim());

            _dialogs.ShowInfo($"\"{name.Trim()}\" kateqoriyası əlavə edildi.", "Əlavə edildi");
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Seçilmiş QURUPU (və ona aid bütün kateqoriyaları) silir.
        /// Mövcud xərc qeydləri dəyişməz qalır — onlar qrup adını mətn kimi saxlayır.
        /// </summary>
        private async Task DeleteGroupFlowAsync()
        {
            var target = string.IsNullOrWhiteSpace(_lastGroup)
                ? Groups.FirstOrDefault(g => !IsGroupOption(g)) ?? string.Empty
                : _lastGroup;

            if (string.IsNullOrWhiteSpace(target))
            {
                _dialogs.ShowWarning("Silinəcək qrup yoxdur.");
                SetQrupSilently(string.Empty);
                return;
            }

            var confirmed = _dialogs.Confirm(
                $"\"{target}\" qrupunu silmək istəyirsiniz?\n\n" +
                "• Bu qrupa aid bütün kateqoriyalar da silinəcək.\n" +
                "• Mövcud xərc qeydləri dəyişməz qalacaq.",
                "🗑 Qrupu sil");

            if (!confirmed)
            {
                SetQrupSilently(target);
                return;
            }

            await _catalog.DeleteGroupAsync(Teyinat, target);

            _lastGroup = string.Empty;
            await LoadCatalogAsync(selectDefault: true);

            _dialogs.ShowInfo($"\"{target}\" qrupu silindi.", "Silindi");
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Seçilmiş KATEQORİYANI cari qrupdan silir.
        /// Mövcud xərc qeydləri dəyişməz qalır.
        /// </summary>
        private async Task DeleteCategoryFlowAsync()
        {
            var target = string.IsNullOrWhiteSpace(_lastCategory)
                ? Categories.FirstOrDefault(c => !IsCategoryOption(c)) ?? string.Empty
                : _lastCategory;

            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(Qrup))
            {
                _dialogs.ShowWarning("Silinəcək kateqoriya yoxdur.");
                SetKategoriyaSilently(string.Empty);
                return;
            }

            var confirmed = _dialogs.Confirm(
                $"\"{Qrup}\" qrupundan \"{target}\" kateqoriyasını silmək istəyirsiniz?\n\n" +
                "• Mövcud xərc qeydləri dəyişməz qalacaq.",
                "🗑 Kateqoriyanı sil");

            if (!confirmed)
            {
                SetKategoriyaSilently(target);
                return;
            }

            await _catalog.DeleteCategoryAsync(Qrup, target);

            _lastCategory = string.Empty;
            await LoadCategoriesAsync(selectDefault: true);

            _dialogs.ShowInfo($"\"{target}\" kateqoriyası silindi.", "Silindi");
            CatalogChanged?.Invoke(this, EventArgs.Empty);
        }

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
                    ? _allCars
                    : _allCars.Where(c =>
                        c.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.Marka.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.QeydiyyatNisani.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        c.Vin.Contains(search, StringComparison.OrdinalIgnoreCase))).ToList();

                // Nəticə əvvəlki ilə eynidirsə siyahını yenidən qurmuruq.
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

        partial void OnSelectedCarChanged(CarItem? value)
        {
            if (value is not null)
            {
                CarSearchText = value.DisplayName;
            }
        }

        /// <summary>Bütün xassələri yoxlayır; forma etibarlıdırsa true qaytarır.</summary>
        public bool ValidateForm()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        public void Reset()
        {
            _suppressCatalogEvents = true;
            Teyinat = Catalog.CarDestination;
            _suppressCatalogEvents = false;

            SelectedCar = null;
            CarSearchText = string.Empty;
            Mebleg = 0m;
            OdenisUsulu = Catalog.PaymentMethods[0];
            Tarix = DateTime.Today;
            Qeyd = string.Empty;
            ClearErrors();

            _ = LoadCatalogAsync(selectDefault: true);
        }

        public ExpenseItem ToModel() => new()
        {
            Tarix = Tarix ?? DateTime.Today,
            Teyinat = Teyinat,
            Qrup = Qrup,
            Kategoriya = Kategoriya.Trim(),
            CarId = IsCarExpense ? SelectedCar?.Id : null,
            Mebleg = Mebleg,
            OdenisUsulu = OdenisUsulu,
            Qeyd = Qeyd.Trim()
        };
    }
}
