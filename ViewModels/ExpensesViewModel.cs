using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Collections;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// 🚀 <b>«Ümumi Xərclər» tabı — MİLYON SƏTİR ÜÇÜN YENİDƏN QURULDU</b> ✓✓✓
    /// <para>
    /// <b>ƏVVƏL (DONURDU ✗):</b>
    /// <list type="number">
    ///   <item>BÜTÜN xərc cədvəli yaddaşa yüklənirdi ✗ (<c>SELECT *</c> ✓ 1 000 000 sətir ✗)</item>
    ///   <item>Hər hərf yazıldıqda 1 000 000 sətir RAM-da süzülürdü ✗</item>
    ///   <item>Yekunlar RAM-da <c>Sum()</c> ilə hesablanırdı ✗</item>
    ///   <item>Sətirlər bir-bir <c>Add()</c> olunurdu ✗ (minlərlə hadisə ✗)</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>İNDİ (DONMUR ✓):</b>
    /// <list type="number">
    ///   <item>Yalnız <b>200 sətirlik səhifə</b> oxunur ✓ (<c>LIMIT/OFFSET</c> ✓)</item>
    ///   <item>Axtarış <b>SQL-də</b> gedir ✓ + 350 ms «debounce» ✓ (hər hərfdə sorğu yox ✗)</item>
    ///   <item>Yekunlar <b>SQL <c>SUM</c></b> ilə ✓ (yaddaşa yükləmədən ✓)</item>
    ///   <item>Sətirlər <b>TƏHLÜKƏSİZ</b> yazılır ✓ — <c>ReplaceAll</c> → tək <c>Reset</c> ✓ ·
    ///         <c>AddRange</c> → tək-tək <c>Add</c> ✓✓✓ (⚠ WPF çox-elementli «range»
    ///         bildirişini DƏSTƏKLƏMİR ✗ → «items source is inconsistent» xətası ✗✓✓)</item>
    ///   <item>Bütün ağır iş <b>arxa fonda</b> ✓ → pəncərə heç vaxt donmur ✓✓✓</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed partial class ExpensesViewModel : ObservableObject
    {
        /// <summary>Avtomatik yüklənən sətir sayı ✓ (scroll edildikcə artır ✓).</summary>
        public const int SehifeOlcusu = 200;

        /// <summary>⏱ Axtarış yazılarkən gözləmə (ms) — hər hərfdə DB sorğusu getməsin ✗.</summary>
        private const int AxtarisGecikmesiMs = 350;

        private readonly IExpenseService _expenseService;
        private readonly IExpenseCatalogService _catalogService;
        private readonly ICarService _carService;
        private readonly IExportService _exportService;
        private readonly IDialogService _dialogs;
        private readonly ILogger<ExpensesViewModel> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>⏱ Axtarış «debounce» taymeri ✓.</summary>
        private readonly System.Windows.Threading.DispatcherTimer? _axtarisTaymeri;

        public BulkObservableCollection<ExpenseItem> Expenses { get; } = new();

        /// <summary>Cədvəl daxilində redaktə üçün bütün qruplar (istifadəçi əlavə etdikləri daxil).</summary>
        public ObservableCollection<string> AllGroups { get; } = new();

        /// <summary>Cədvəl daxilində redaktə üçün bütün kateqoriyalar (istifadəçi əlavə etdikləri daxil).</summary>
        public ObservableCollection<string> AllCategories { get; } = new();

        /// <summary>Xərc cədvəlinin redaktəsi üçün avtomobil siyahısı.</summary>
        public ObservableCollection<CarItem> AvailableCars { get; } = new();

        public ExpenseEditorViewModel Editor { get; }

        public IReadOnlyList<string> FilterTypes { get; } = Catalog.ExpenseFilterTypes;

        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private string selectedFilterType = Catalog.ExpenseFilterTypes[0];
        [ObservableProperty] private ExpenseItem? selectedExpense;
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private decimal totalExpense;
        [ObservableProperty] private decimal carExpense;
        [ObservableProperty] private decimal officeExpense;
        [ObservableProperty] private decimal monthExpense;

        /// <summary>📄 Süzgəcə uyğun ÜMUMİ sətir sayı ✓ (bazada 1 000 000 olsa da ✓).</summary>
        [ObservableProperty] private int umumiSay;

        /// <summary>⬇️ Hələ yüklənməmiş sətir var? ✓</summary>
        [ObservableProperty] private bool dahaVar;

        /// <summary>📊 «200 / 1 234 567 sətir göstərilir» ✓</summary>
        [ObservableProperty] private string veziyyetMetni = string.Empty;

        /// <summary>🔽 Sıralama sahəsi ✓ (SQL <c>ORDER BY</c> ✓).</summary>
        [ObservableProperty] private ExpenseSort siralamaNovu = ExpenseSort.Tarix;

        /// <summary>🔽 Sıralama azalan? ✓</summary>
        [ObservableProperty] private bool siralamaAzalan = true;

        /// <summary>Xərclər dəyişdikdə baş verir (maliyyə panelini yeniləmək üçün).</summary>
        public event EventHandler? ExpensesChanged;

        public ExpensesViewModel(
            IExpenseService expenseService,
            IExpenseCatalogService catalogService,
            ICarService carService,
            IExportService exportService,
            IDialogService dialogs,
            ExpenseEditorViewModel editor,
            ILogger<ExpensesViewModel> logger)
        {
            _expenseService = expenseService;
            _catalogService = catalogService;
            _carService = carService;
            _exportService = exportService;
            _dialogs = dialogs;
            Editor = editor;
            _logger = logger;

            // Qrup/kateqoriya əlavə edildikdə cədvəlin siyahıları da yenilənir.
            Editor.CatalogChanged += async (_, _) => await LoadCatalogListsAsync();

            // ⏱ Axtarış taymeri ✓ — istifadəçi yazmağı dayandırdıqdan SONRA sorğu gedir ✓
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            {
                _axtarisTaymeri = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromMilliseconds(AxtarisGecikmesiMs),
                    System.Windows.Threading.DispatcherPriority.Background,
                    async (_, _) => await AxtarisTetbiqEtAsync(),
                    dispatcher);
                _axtarisTaymeri.Stop();
            }
        }

        // ====================================================================
        //  🔎 AXTARIŞ VƏ SÜZGƏC  (SQL tərəfində ✓ · «debounce» ilə ✓)
        // ====================================================================
        /// <summary>Qrup və kateqoriya siyahılarını (cədvəlin redaktəsi üçün) yeniləyir.</summary>
        public async Task LoadCatalogListsAsync()
        {
            var groups = await _catalogService.GetAllGroupsAsync();
            var categories = await _catalogService.GetAllCategoriesAsync();

            AllGroups.Clear();
            foreach (var group in groups)
            {
                AllGroups.Add(group);
            }

            AllCategories.Clear();
            foreach (var category in categories)
            {
                AllCategories.Add(category);
            }
        }

        partial void OnSearchTextChanged(string value) => AxtarisPlanla();

        partial void OnSelectedFilterTypeChanged(string value) => _ = SehifeniYenileAsync();

        /// <summary>⏱ Yazmağı dayandırdıqdan 350 ms sonra axtarış gedir ✓.</summary>
        private void AxtarisPlanla()
        {
            if (_axtarisTaymeri is null)
            {
                _ = SehifeniYenileAsync();
                return;
            }

            _axtarisTaymeri.Stop();
            _axtarisTaymeri.Start();
        }

        private async Task AxtarisTetbiqEtAsync()
        {
            _axtarisTaymeri?.Stop();
            await SehifeniYenileAsync();
        }

        /// <summary>Cari axtarış + süzgəci SQL sorğusuna çevirir ✓.</summary>
        private ExpenseFilter CariSüzgəc()
        {
            var axtaris = SearchText?.Trim();

            // ⚠ «Yalnız Avtomobil Xərcləri» = OFISDƏN BAŞQA HAMISI ✓
            //   (köhnə davranış EYNİ qalır ✓ — təyinatı boş olan qeydlər də görünür ✓)
            return SelectedFilterType switch
            {
                "Yalnız Avtomobil Xərcləri" => new ExpenseFilter(axtaris, TeyinatDeyil: Catalog.OfficeDestination),
                "Yalnız Ofis Xərcləri" => new ExpenseFilter(axtaris, Teyinat: Catalog.OfficeDestination),
                _ => new ExpenseFilter(axtaris)
            };
        }

        // ====================================================================
        //  💾 YÜKLƏMƏ  (yalnız 200 sətir + SQL yekunları ✓ → DONMA YOXDUR ✓)
        // ====================================================================
        [RelayCommand]
        public async Task LoadAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;

                // 💾 ① FORMA VƏZİYYƏTİNİ YADDA SAXLA ✓✓✓
                //     ⚠ Bunsuz: SetCars() + LoadCatalogAsync() Qrup/Kateqoriya/Avtomobil
                //       seçimlərini və yazılanları SİLİRDİ ✗ → indi SİLİNMİR ✓✓✓
                var forma = Editor.VəziyyətiAl();
                var formaDolu = Editor.FormaDoludur;

                // ⚡ ② CƏDVƏL: yalnız BİRİNCİ SƏHİFƏ ✓ (SQL `LIMIT 200` ✓)
                await SəhifəYazAsync(0, temizle: true);

                // ⚡ ③ YEKUMLAR: SQL `SUM` ✓ (1 000 000 sətir yaddaşa yüklənmir ✗)
                await YekunlariYenileAsync();

                // ④ Avtomobil siyahısı (kiçikdir ✓) + qrup/kateqoriya siyahıları
                var cars = await _carService.GetCarsAsync();
                Editor.SetCars(cars);

                AvailableCars.Clear();
                foreach (var car in cars)
                {
                    AvailableCars.Add(car);
                }

                await Editor.LoadCatalogAsync(selectDefault: false);
                await LoadCatalogListsAsync();

                // ♻️ ⑤ FORMA VƏZİYYƏTİNİ BƏRPA ET ✓✓✓
                if (formaDolu)
                {
                    Editor.VəziyyətiQoy(forma);
                }

                _logger.LogInformation(
                    "Xərclər yükləndi — bazada {Total} qeyd, ekranda {Shown} sətir.",
                    UmumiSay, Expenses.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Xərclər yüklənərkən xəta baş verdi.");
                _dialogs.ShowError("Xərc siyahısı yüklənə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        /// <summary>
        /// 🚀 <b>BİR SƏHİFƏNİ YAZIR</b> ✓✓✓ — əməliyyat <b>arxa fonda</b> gedir ✓,
        /// kolleksiya <b>tək bildirişlə</b> yenilənir ✓ (cədvəl donmur ✓)
        /// </summary>
        /// <param name="skip">Neçə sətir atlanır ✓ (<c>OFFSET</c> ✓).</param>
        /// <param name="temizle"><c>true</c> → əvvəlkilər silinir ✓, <c>false</c> → sona əlavə ✓.</param>
        private async Task SəhifəYazAsync(int skip, bool temizle)
        {
            var süzgəc = CariSüzgəc();
            var sıra = SiralamaNovu;
            var azalan = SiralamaAzalan;

            // ⚡ AĞIR İŞ ARXA FONDA ✓✓✓ (UI thread BOŞ qalır ✓ → donma YOXDUR ✓)
            var səhifə = await Task.Run(() =>
                _expenseService.GetPageAsync(skip, SehifeOlcusu, süzgəc, sıra, azalan));

            try
            {
                if (temizle)
                {
                    Expenses.ReplaceAll(səhifə.Setirler);   // ✅ TƏK `Reset` hadisəsi ✓
                }
                else
                {
                    Expenses.AddRange(səhifə.Setirler);     // ✅ TƏHLÜKƏSİZ `Add` bildirişləri ✓ (scroll pozulmur ✓)
                }
            }
            catch (Exception ex)
            {
                // 🛟 QALXAN ✓ — WPF cədvəli ilə uyğunsuzluq
                //   («An ItemsControl is inconsistent with its items source») yaranarsa
                //   proqram ÇÖKMÜR ✗ → siyahı TAM yenidən yazılır (`Reset` ✓) ✓✓✓
                _logger.LogWarning(ex, "Cədvəl yenilənərkən WPF uyğunsuzluğu — tam yeniləmə ilə düzəldilir.");
                SəhifəniTamYenile(səhifə.Setirler);
            }

            UmumiSay = səhifə.UmumiSay;
            DahaVar = Expenses.Count < UmumiSay;
            VeziyyetMetni = $"{Expenses.Count:N0} / {UmumiSay:N0} sətir göstərilir";
        }

        /// <summary>
        /// 🛟 <b>QALXAN (EHTİYAT) YENİLƏMƏ</b> ✓✓✓
        /// <para>
        /// WPF cədvəli ilə siyahı arasında uyğunsuzluq yaranarsa (nadir hal ✗) —
        /// siyahı <b>layout-dan kənarda</b>, <c>Reset</c> bildirişi ilə TAM yenidən
        /// yazılır ✓ → cədvəl özünü bərpa edir ✓ (proqram bağlanmır ✗✓✓)
        /// </para>
        /// </summary>
        private void SəhifəniTamYenile(IEnumerable<ExpenseItem> setirler)
        {
            var siyahı = setirler.ToList();
            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher is null)
            {
                return;
            }

            dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                try
                {
                    Expenses.ReplaceAll(siyahı);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cədvəl tam yenilənə bilmədi.");
                }
            }));
        }

        /// <summary>🚀 Yekun məbləğlər — SQL-də hesablanır ✓ (arxa fonda ✓).</summary>
        private async Task YekunlariYenileAsync()
        {
            var stats = await Task.Run(() => _expenseService.GetStatsAsync());

            TotalExpense = stats.Total;
            CarExpense = stats.CarTotal;
            OfficeExpense = stats.OfficeTotal;
            MonthExpense = stats.MonthTotal;
        }

        /// <summary>🔎 Süzgəc / axtarış / sıralama dəyişdi → 1-ci səhifə yenidən ✓.</summary>
        private async Task SehifeniYenileAsync()
        {
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                await SəhifəYazAsync(0, temizle: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Xərc süzgəci tətbiq olunarkən xəta baş verdi.");
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        /// <summary>⬇️ Növbəti 200 sətri yükləyir ✓ (cədvəl aşağı sürüşdükdə AVTOMATİK ✓).</summary>
        [RelayCommand]
        private async Task DahaCoxYukleAsync()
        {
            if (IsBusy || !DahaVar)
            {
                return;
            }

            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                await SəhifəYazAsync(Expenses.Count, temizle: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Əlavə sətirlər yüklənərkən xəta baş verdi.");
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }
        }

        /// <summary>🔽 Cədvəl başlığına basıldı → sıralama SQL-də edilir ✓ (bütün baza üzrə ✓).</summary>
        public async Task SıralaAsync(ExpenseSort nov, bool azalan)
        {
            SiralamaNovu = nov;
            SiralamaAzalan = azalan;
            await SehifeniYenileAsync();
        }

        // ====================================================================
        //  ➕ YENİ XƏRC · ✏️ REDAKTƏ · 🗑 SİLMƏ  (yazma → yalnız səhifə yenilənir ✓)
        // ====================================================================
        [RelayCommand]
        private async Task AddExpenseAsync()
        {
            if (!Editor.ValidateForm())
            {
                _dialogs.ShowWarning("Zəhmət olmasa formada göstərilən səhvləri düzəldin.");
                return;
            }

            if (Editor.IsCarExpense && Editor.SelectedCar is null)
            {
                _dialogs.ShowWarning("Avtomobil xərci üçün avtomobil seçilməlidir.");
                return;
            }

            var added = false;
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                var expense = Editor.ToModel();
                await _expenseService.AddExpenseAsync(expense);
                Editor.Reset();
                added = true;
                _logger.LogInformation("Yeni xərc əlavə edildi: {Category} - {Amount}", expense.Kategoriya, expense.Mebleg);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Xərc əlavə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Xərc əlavə edilə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            if (added)
            {
                await LoadAsync();
                ExpensesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        private void ResetExpense() => Editor.Reset();

        /// <summary>
        /// Xərc cədvəlində sətir redaktəsi təsdiqləndikdən sonra dəyişiklikləri yadda saxlayır.
        /// </summary>
        [RelayCommand]
        private async Task SaveExpenseAsync(ExpenseItem? expense)
        {
            if (expense is null)
            {
                return;
            }

            if (expense.Mebleg <= 0m)
            {
                _dialogs.ShowWarning("Məbləğ 0-dan böyük olmalıdır.\nDəyişiklik yadda saxlanmadı.");
                await LoadAsync();
                return;
            }

            var saved = false;
            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                await _expenseService.UpdateExpenseAsync(expense);
                saved = true;
                _logger.LogInformation("Xərc redaktə edildi: {Id} - {Category}", expense.Id, expense.Kategoriya);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Xərc redaktə edilərkən xəta baş verdi.");
                _dialogs.ShowError("Dəyişikliklər yadda saxlanıla bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            await LoadAsync();

            if (saved)
            {
                ExpensesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        private async Task DeleteExpenseAsync()
        {
            if (SelectedExpense is null)
            {
                _dialogs.ShowWarning("Silmək üçün siyahıdan xərc seçin.");
                return;
            }

            var expense = SelectedExpense;

            if (!_dialogs.Confirm($"\"{expense.Kategoriya} - {expense.Mebleg:N2} AZN\" xərcini silmək istəyirsiniz?"))
            {
                return;
            }

            await _gate.WaitAsync();
            try
            {
                IsBusy = true;
                await _expenseService.DeleteExpenseAsync(expense.Id);
                SelectedExpense = null;
                _logger.LogInformation("Xərc silindi: {Id}", expense.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Xərc silinərkən xəta baş verdi.");
                _dialogs.ShowError("Xərc silinə bilmədi: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
                _gate.Release();
            }

            await LoadAsync();
            ExpensesChanged?.Invoke(this, EventArgs.Empty);
        }

        // ====================================================================
        //  📤 EXCEL (CSV) İXRACI  ✓✓✓
        // --------------------------------------------------------------------
        //  ⚠ ƏVVƏL: cədvəldəki (yüklənmiş) sətirlər ixrac olunurdu ✗
        //  ✅ İNDİ: süzgəcə uyğun BÜTÜN sətirlər — səhifə-səhifə oxunur ✓
        //     (5000-lik dəstələrlə ✓ → yaddaş ŞİŞMİR ✗ · UI DONMUR ✗✓✓)
        // ====================================================================
        [RelayCommand]
        private async Task ExportExpensesAsync()
        {
            if (UmumiSay == 0)
            {
                _dialogs.ShowWarning("İxrac üçün məlumat yoxdur.");
                return;
            }

            var path = _dialogs.ShowSaveFileDialog(
                "Excel (CSV) faylı|*.csv",
                $"Xercler_{DateTime.Now:yyyyMMdd_HHmm}.csv");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                IsBusy = true;

                // ♻️ `ExportService` özü `Task.Run` içində işləyir ✓ →
                //    səhifələr ARXA FONDA oxunur ✓ (ekran donmur ✓)
                await _exportService.ExportExpensesAsync(ButunSetirleriGetir(), path);

                _dialogs.ShowInfo("Məlumatlar uğurla ixrac edildi:\n" + path);
                _logger.LogInformation("Xərc məlumatları ixrac edildi: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "İxrac zamanı xəta baş verdi.");
                _dialogs.ShowError("İxrac alınmadı: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 🚀 İxrac üçün BÜTÜN sətirləri <b>səhifə-səhifə</b> qaytarır ✓✓✓
        /// <para>
        /// ⚠ Bu iterator <c>ExportService.WriteAsync</c>-in <c>Task.Run</c>-i
        /// içində (arxa fonda ✓) işlənir → UI thread bloklanmır ✗✓✓
        /// </para>
        /// </summary>
        private IEnumerable<ExpenseItem> ButunSetirleriGetir()
        {
            const int dəstə = 5000;

            var skip = 0;

            while (skip < UmumiSay)
            {
                var səhifə = _expenseService
                    .GetPageAsync(skip, dəstə, CariSüzgəc(), SiralamaNovu, SiralamaAzalan)
                    .GetAwaiter()
                    .GetResult();

                if (səhifə.Setirler.Count == 0)
                {
                    yield break;
                }

                foreach (var sətir in səhifə.Setirler)
                {
                    yield return sətir;
                }

                skip += səhifə.Setirler.Count;
            }
        }
    }
}
