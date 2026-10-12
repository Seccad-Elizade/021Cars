using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using EnterpriseAeroStudio.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace EnterpriseAeroStudio.Views
{
    /// <summary>
    /// Əsas pəncərə. Bütün məntiq <see cref="MainViewModel"/>-dədir; burada yalnız
    /// görünüşə xas olan nazik bağlantılar saxlanılır.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        private bool _isFullScreen;
        private WindowState _previousState = WindowState.Maximized;
        private WindowStyle _previousStyle = WindowStyle.SingleBorderWindow;
        private ResizeMode _previousResizeMode = ResizeMode.CanResize;

        // ================================================================
        //  🚀 «DAHA ÇOX YÜKLƏ» — TƏHLÜKƏSİZ (layout-dan KƏNAR) ✓✓✓
        // ----------------------------------------------------------------
        //  ⚠ ƏVVƏL yükləmə `LoadingRow`-dan başladılırdı ✗ → cədvəl sətir
        //    yaradarkən kolleksiya dəyişirdi ✗ → WPF:
        //    «An ItemsControl is inconsistent with its items source» ✗✓✓
        //  ✅ İNDİ: scroll dayanır → 600 ms sonra taymer yükləyir ✓
        // ================================================================
        private DispatcherTimer? _xercDahaCoxTaymeri;
        private bool _xercScrollBagli;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            // ================================================================
            //  🏷️ PƏNCƏRƏ BAŞLIĞI — cari versiya ASSEMBLY-dən oxunur ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL XAML-də «Avtomobil Parkı v6.2.2» SABİT yazılmışdı ✗ →
            //    hər yeni buraxılışda KÖHNƏ versiya görünürdü ✗✓✓
            //  ✅ İNDİ: csproj <Version> dəyişən kimi AVTOMATİK görünür ✓
            // ================================================================
            try
            {
                Title = $"Avtomobil Parkı v{GuncellemeXidmeti.CariVersiyaMetni} — Professional Fleet & Office Edition";
            }
            catch { }

            _viewModel.ArchiveRequested += OnArchiveRequested;
            _viewModel.Web.NavigateRequested += OnWebNavigateRequested;

            // 💾 SEÇİM QORUNMASI ✓✓✓ — avtomatik yeniləmə istifadəçinin seçimini SİLMİR ✗
            _viewModel.YenilemeBasladi += SecimleriYaddaSaxla;
            _viewModel.YenilemeBitdi += SecimleriBerpaEt;

            // ================================================================
            //  📑 TAB DƏYİŞDİ → YALNIZ O TAB YENİLƏNİR ✓✓✓
            // ----------------------------------------------------------------
            //  ★ DONMANIN HƏLLİ ★ : əvvəl hər dəyişiklikdə 9 tab-ın hamısı
            //    yenidən qurulurdu ✗ (194 maşın · 1800+ xərc ✓) → donurdu ✗✓✓
            // ================================================================
            MainTabs.SelectionChanged += (_, _) =>
            {
                try { _viewModel.TabDeyisdi(MainTabs.SelectedIndex); } catch { }
            };

            // ================================================================
            //  💾 v6.2.37 — YERLİ BAZA + USB STATUS DAİRƏLƏRİ ✓✓✓
            //  🔴 USB taxılmayıb ✗ · 🟠 taxılıb, hələ sinxron deyil ✓ · 🟢 tamam ✓
            // ================================================================
            _usbDaireTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _usbDaireTimer.Tick += (_, _) => UsbDaireleriniYenile();
            _usbDaireTimer.Start();
            UsbDaireleriniYenile();

            // ================================================================
            //  🚀 AVTOMATİK GÜNCƏLLƏMƏ ✓✓✓ — GitHub Releases ✓
            // ----------------------------------------------------------------
            //  ① Tətbiq AÇILANDA ~15 saniyə sonra yoxlanılır ✓
            //     (⏰ əvvəl «SONRA» basılmışdısa → yenə soruşulur ✓✓✓)
            //  ② Sonra HƏR 10 DƏQİQƏDƏ BİR yoxlanılır ✓
            //  ③ ⏰ «SONRA» basılıbsa → bu sessiyada susur ✗
            //     (növbəti AÇILIŞDA yenidən gəlir ✓✓✓)
            // ================================================================

            _guncellemeTimer = new DispatcherTimer { Interval = GuncellemeXidmeti.Fasile };
            _guncellemeTimer.Tick += async (_, _) => await GuncellemeYoxlaAsync();
            _guncellemeTimer.Start();

            Loaded += async (_, _) =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15));   // ⏳ tətbiq otursun ✓
                    await GuncellemeYoxlaAsync();
                }
                catch { }
            };

            // 🚀 AŞAĞIDAKİ «GÜNCƏLLƏMƏ» DÜYMƏSİ + VERSİYA ✓✓✓
            try
            {
                VersiyaMetni.Text = "Avtomobil Parkı v" + GuncellemeXidmeti.CariVersiyaMetni;

                GuncellemeXidmeti.VeziyyetDeyisdi += GuncellemeDugmesiniYenile;
                GuncellemeDugmesiniYenile();
            }
            catch { }

            Loaded += OnLoaded;
            Closed += (_, _) =>
            {
                try { _usbDaireTimer?.Stop(); } catch { }
                try { _guncellemeTimer?.Stop(); } catch { }
            };
        }

        // ====================================================================
        //  🚀 AVTOMATİK GÜNCƏLLƏMƏ ✓✓✓ — yoxlama + popup ✓
        // ====================================================================

        /// <summary>⏱️ Güncəlləmə yoxlama taymeri ✓ (hər 10 dəqiqə ✓)</summary>
        private DispatcherTimer? _guncellemeTimer;

        // ====================================================================
        //  💾 v6.2.37 — YERLİ BAZA + USB STATUS DAİRƏLƏRİ ✓✓✓
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi: «USB taxılmayıbsa 🔴 QIRMIZI olsun ✓;
        //    taxılıbsa amma hələ sinxron olmayıbsa 🟠 NARINCI ✓;
        //    sonra hər şey 🟢 YAŞIL ✓» ✓✓✓
        // ====================================================================

        /// <summary>⏱️ USB/yerli baza statusunu yeniləyən taymer ✓</summary>
        private DispatcherTimer? _usbDaireTimer;

        /// <summary>
        /// 🟢🟠🔴 <b>YERLİ BAZA + USB STATUSUNU YENİLƏYİR</b> ✓✓✓
        /// <list type="bullet">
        ///   <item>🖥️ Yerli baza — proqram işlədiyi üçün həmişə 🟢 YAŞIL ✓</item>
        ///   <item>🔴 USB <b>taxılmayıb</b> ✗ (və ya qeydiyyatdan keçməyib ✗)</item>
        ///   <item>🟠 USB taxılıb, amma baza nüsxəsi hələ <b>TƏZƏLƏNMƏYİB</b> ✓</item>
        ///   <item>🟢 USB + baza <b>SİNXRONDUR</b> ✓</item>
        /// </list>
        /// </summary>
        private void UsbDaireleriniYenile()
        {
            try
            {
                // 🖥️ YERLİ BAZA — həmişə yaşıl ✓ (proqram açıqdırsa baza işləyir ✓)
                DaireYerli.Fill = Rəng("#22C55E");

                // 💾 DATA USB TANINDIMI? (yalnız SİZİN fləşkartınız ✓)
                var tap = Services.DataUsbService.Tap();

                if (tap is null)
                {
                    // 🔴 USB YOXDUR ✗ — qeydiyyat yoxdursa ayrı mətn ✓
                    DaireUsb.Fill = Rəng("#EF4444");

                    DaireMetni.Text = Services.DataUsbService.Qeydiyyat is null
                        ? "💾 USB: qeydiyyat yoxdur ✗"
                        : "💾 USB: taxılmayıb ✗";

                    return;
                }

                // ✅ USB TAXILIDIR → nüsxə TƏZƏDİRMİ? 🟠 / 🟢
                var yerliBaza = Services.DataUsbService.YerliBazaYolu;
                var usbBaza = Services.DataUsbService.BazaFayliTap();

                var təzə = usbBaza is not null
                           && System.IO.File.Exists(yerliBaza)
                           && System.IO.File.Exists(usbBaza)
                           && System.IO.File.GetLastWriteTime(usbBaza)
                              >= System.IO.File.GetLastWriteTime(yerliBaza).AddSeconds(-3);

                if (təzə)
                {
                    DaireUsb.Fill = Rəng("#22C55E");     // 🟢 SİNXRON ✓
                    DaireMetni.Text = "💾 USB: sinxron ✓";
                }
                else
                {
                    DaireUsb.Fill = Rəng("#F59E0B");     // 🟠 YAZILIR ✓
                    DaireMetni.Text = "💾 USB: ehtiyat alınır… ✓";
                }
            }
            catch
            {
                // 🛡️ status dairəsi HEÇ VAXT proqramı çökdürmür ✗✓✓
            }
        }

        /// <summary>🔒 Popup artıq açıqdır? ✓ (iki dəfə açılmasın ✗)</summary>
        private bool _guncellemeAciq;

        /// <summary>
        /// 🌐 GitHub-dan yoxlayır ✓ — yeni versiya varsa popup göstərir ✓
        /// <para>⏰ «SONRA» basılıbsa → bu sessiyada göstərilmir ✗ (növbəti açılışda gəlir ✓)</para>
        /// </summary>
        private async Task GuncellemeYoxlaAsync()
        {
            try
            {
                if (_guncellemeAciq) return;

                // ⏰ «SONRA» → növbəti AÇILIŞA qədər susur ✗ (§ tələb ✓)
                if (GuncellemeXidmeti.SonrayaAtildi) return;

                var m = await GuncellemeXidmeti.YoxlaAsync();

                if (m is null) return;

                GuncellemeGoster(m);
            }
            catch (Exception ex)
            {
                AppLogger.Melumat("🚀 Güncəlləmə yoxlaması ✓ — " + ex.Message);
            }
        }

        /// <summary>🎉 Güncəlləmə pəncərəsini göstərir ✓</summary>
        private void GuncellemeGoster(Services.GuncellemeMelumati m)
        {
            if (_guncellemeAciq) return;

            _guncellemeAciq = true;

            try
            {
                var pəncərə = new GuncellemeWindow(m);

                if (IsLoaded && IsVisible) pəncərə.Owner = this;

                pəncərə.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Melumat("🚀 Güncəlləmə pəncərəsi açılmadı ✓ — " + ex.Message);
            }
            finally
            {
                _guncellemeAciq = false;
            }
        }

        // ====================================================================
        //  🚀 AŞAĞIDAKİ «GÜNCƏLLƏMƏ» DÜYMƏSİ ✓✓✓
        // ====================================================================

        /// <summary>
        /// 🎨 Düyməni vəziyyətə görə boyayır ✓
        /// <para>⚪ boz = yoxlanılmayıb ✓ · 🟡 sarı = yoxlanılır ✓ · 🟢 yaşıl = ən son ✓ · 🟠 narıncı = yeni var ✓ · 🔴 qırmızı = xəta ✗</para>
        /// </summary>
        private void GuncellemeDugmesiniYenile()
        {
            try
            {
                void İş()
                {
                    try
                    {
                        var vəziyyət = GuncellemeXidmeti.CariVəziyyət;

                        var rəng = vəziyyət switch
                        {
                            GuncellemeXidmeti.Vəziyyət.Yoxlanilir => "#FBBF24",
                            GuncellemeXidmeti.Vəziyyət.EnSon => "#22C55E",
                            GuncellemeXidmeti.Vəziyyət.Yenivar => "#F59E0B",
                            GuncellemeXidmeti.Vəziyyət.Xeta => "#EF4444",
                            _ => "#64748B"
                        };

                        var mətn = vəziyyət switch
                        {
                            GuncellemeXidmeti.Vəziyyət.Yoxlanilir => "🚀 Yoxlanılır…",
                            GuncellemeXidmeti.Vəziyyət.Yenivar => "🎉 Yeni versiya!",
                            _ => "🚀 Güncəlləmə"
                        };

                        DaireGuncelleme.Fill = (System.Windows.Media.Brush)
                            new System.Windows.Media.BrushConverter().ConvertFromString(rəng)!;

                        GuncellemeMetni.Text = mətn;
                    }
                    catch { }
                }

                if (Dispatcher.CheckAccess()) İş();
                else Dispatcher.Invoke(İş);
            }
            catch { }
        }

        /// <summary>🚀 «GÜNCƏLLƏMƏ» düyməsi — 🔍 əl ilə yoxlama pəncərəsini açır ✓✓✓</summary>
        private void BtnGuncelleme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_guncellemeAciq) return;

                _guncellemeAciq = true;

                // 📌 parametrsiz ✓ → əl ilə yoxlama rejimi ✓ (pəncərə özü GitHub-a baxır ✓)
                var pəncərə = new GuncellemeWindow();

                if (IsLoaded && IsVisible) pəncərə.Owner = this;

                pəncərə.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogger.Melumat("🚀 Güncəlləmə düyməsi ✓ — " + ex.Message);
            }
            finally
            {
                _guncellemeAciq = false;
            }
        }

        // ====================================================================
        //  🟢🔴🟠 STATUS DAİRƏLƏRİ + 💾 SEÇİM QORUNMASI ✓✓✓
        // ====================================================================

        /// <summary>💾 Yeniləmədən əvvəl yadda saxlanan seçimlər ✓</summary>
        private readonly System.Collections.Generic.List<(object Sahə, object? Açar)> _secimler = new();

        /// <summary>📢 Yeniləmə BAŞLAYANDA: bütün cədvəllərin seçimini yadda saxlayır ✓✓✓</summary>
        private void SecimleriYaddaSaxla()
        {
            try
            {
                _secimler.Clear();

                foreach (var dg in VizualUşaqlar<System.Windows.Controls.DataGrid>(this))
                {
                    if (dg.SelectedItem is not null)
                    {
                        _secimler.Add((dg, Açar(dg.SelectedItem)));
                    }
                }

                foreach (var lv in VizualUşaqlar<System.Windows.Controls.ListView>(this))
                {
                    if (lv.SelectedItem is not null)
                    {
                        _secimler.Add((lv, Açar(lv.SelectedItem)));
                    }
                }

                // ================================================================
                //  🔽 ★ v6.2.22 — COMBOBOX (BAR) SEÇİMLƏRİ DƏ SAXLANILIR ★✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ İSTİFADƏÇİ ŞİKAYƏTİ: «Sinxron olanda seçdiyimiz xanadan
                //    (bar) seçilmiş maşın/kredit SİLİNİR» ✗✓✓
                //  ⚠ ƏVVƏL yalnız DataGrid + ListView saxlanılırdı ✗ →
                //    ComboBox-lar (kredit seçimi ✗ maşın seçimi ✗) unudulmuşdu ✗✓✓
                // ================================================================
                foreach (var cb in VizualUşaqlar<System.Windows.Controls.ComboBox>(this))
                {
                    if (cb.SelectedItem is not null)
                    {
                        _secimler.Add((cb, Açar(cb.SelectedItem)));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeberdarliq("⚠️ Seçim yadda saxlanmadı ✗ — " + ex.Message);
            }
        }

        /// <summary>♻️ Yeniləmə BİTƏNDƏ: seçimləri Id üzrə TAPIB BƏRPA edir ✓✓✓</summary>
        private void SecimleriBerpaEt()
        {
            try
            {
                foreach (var (sahə, açar) in _secimler)
                {
                    if (açar is null) continue;

                    if (sahə is System.Windows.Controls.DataGrid dg)
                    {
                        var yeni = Tap(dg.ItemsSource, açar);
                        if (yeni is not null) dg.SelectedItem = yeni;
                    }
                    else if (sahə is System.Windows.Controls.ListView lv)
                    {
                        var yeni = Tap(lv.ItemsSource, açar);
                        if (yeni is not null) lv.SelectedItem = yeni;
                    }
                    // 🔽 v6.2.22 — ComboBox (bar) seçimi də bərpa olunur ✓✓✓
                    else if (sahə is System.Windows.Controls.ComboBox cb)
                    {
                        var yeni = Tap(cb.ItemsSource, açar);
                        if (yeni is not null) cb.SelectedItem = yeni;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeberdarliq("⚠️ Seçim bərpa olunmadı ✗ — " + ex.Message);
            }
            finally
            {
                _secimler.Clear();
            }
        }


        // ==== KÖMƏKÇİ ====

        /// <summary>🎨 Rəng mətnini <see cref="System.Windows.Media.Brush"/>-a çevirir ✓</summary>
        private static System.Windows.Media.Brush Rəng(string hex)
        {
            try
            {
                return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter()
                    .ConvertFromString(hex)!;
            }
            catch
            {
                return System.Windows.Media.Brushes.Gray;
            }
        }

        /// <summary>🆔 Elementin açarı ✓ (Id varsa Id ✓, yoxsa özü ✓)</summary>
        private static object? Açar(object? element)
        {
            if (element is null) return null;

            var p = element.GetType().GetProperty("Id");
            return p?.GetValue(element) ?? element;
        }

        /// <summary>🔍 Siyahıda açar üzrə elementi tapır ✓ (Id üzrə ✓✓✓)</summary>
        private static object? Tap(System.Collections.IEnumerable? mənbə, object? açar)
        {
            if (mənbə is null || açar is null) return null;

            foreach (var element in mənbə)
            {
                if (açar.Equals(Açar(element))) return element;
            }

            return null;
        }

        /// <summary>🌳 Vizual ağacda bütün elementləri tapır ✓ (DataGrid · ListView ✓)</summary>
        private static System.Collections.Generic.IEnumerable<T>
            VizualUşaqlar<T>(System.Windows.DependencyObject kök)
            where T : System.Windows.DependencyObject
        {
            var say = System.Windows.Media.VisualTreeHelper.GetChildrenCount(kök);

            for (var i = 0; i < say; i++)
            {
                var uşaq = System.Windows.Media.VisualTreeHelper.GetChild(kök, i);

                if (uşaq is T t) yield return t;

                foreach (var alt in VizualUşaqlar<T>(uşaq)) yield return alt;
            }
        }


        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();

            // Veb server işə düşdükdən sonra daxili brauzer hazırlanır.
            await InitializeBrowserAsync();
        }

        private void OnArchiveRequested(object? sender, CarItem car)
        {
            // ViewModel DI konteynerindən alınır ki, media və dialoq xidmətləri mövcud olsun.
            var viewModel = App.Services?.GetService(typeof(ArchiveViewModel)) as ArchiveViewModel
                            ?? new ArchiveViewModel();
            viewModel.SetCar(car);

            var window = new ArchiveWindow
            {
                DataContext = viewModel,
                Owner = this
            };
            window.ShowDialog();

            // Arxivdə fayl əlavə edilibsə, avtomobil cədvəlindəki "📎 Sənəd" sayı yenilənir.
            if (_viewModel.CarPark.LoadCommand.CanExecute(null))
            {
                _viewModel.CarPark.LoadCommand.Execute(null);
            }
        }

        /// <summary>
        /// Avto park cədvəlində sətir redaktəsi təsdiqləndikdən sonra
        /// dəyişiklikləri ViewModel vasitəsilə yadda saxlayır.
        /// </summary>
        private void CarGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not CarItem car)
            {
                return;
            }

            // Binding-lər tamamlandıqdan sonra komanda çağırılır.
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_viewModel.CarPark.SaveCarCommand.CanExecute(car))
                {
                    _viewModel.CarPark.SaveCarCommand.Execute(car);
                }
            }));
        }

        /// <summary>
        /// Xərc cədvəlində sətir redaktəsi təsdiqləndikdən sonra
        /// dəyişiklikləri ViewModel vasitəsilə yadda saxlayır.
        /// </summary>
        private void ExpenseGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not ExpenseItem expense)
            {
                return;
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (_viewModel.Expenses.SaveExpenseCommand.CanExecute(expense))
                {
                    _viewModel.Expenses.SaveExpenseCommand.Execute(expense);
                }
            }));
        }

        /// <summary>
        /// 🚀 <b>AVTOMATİK «DAHA ÇOX YÜKLƏ» — TƏHLÜKƏSİZ VERSİYA</b> ✓✓✓
        /// <para>
        /// ⚠⚠ <b>ƏVVƏL (XƏTANIN KÖKÜ ✗✓✓):</b> yükləmə <c>LoadingRow</c> hadisəsindən
        /// başladılırdı ✗ — bu hadisə WPF-in <b>sətir yaratma (layout) dövrünün
        /// İÇİNDƏ</b> baş verir ✗ → kolleksiya həmin anda dəyişəndə cədvəlin
        /// <c>ItemContainerGenerator</c>-i
        /// «<i>An ItemsControl is inconsistent with its items source</i>»
        /// xətası verirdi ✗✓✓
        /// </para>
        /// <para>
        /// ✅ <b>İNDİ:</b> scroll hadisəsi yalnız «yükləməyi PLANLA» işarəsi qoyur ✓;
        /// yükləmə <b>scroll DAYANDIQDAN 600 ms SONRA</b>,
        /// <see cref="DispatcherTimer"/> vasitəsilə — yəni layout-dan TAMAMİLƏ
        /// KƏNARDA, sakit vəziyyətdə ✓ — başlayır ✓✓✓
        /// </para>
        /// </summary>
        private void ExpenseGrid_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGrid grid || _xercScrollBagli)
            {
                return;
            }

            _xercScrollBagli = true;

            // 🌊 Cədvəlin DAXİLİ ScrollViewer-inin scroll hadisəsi ✓
            //    (`ScrollChanged` bubble edir ✓ → DataGrid-də tutulur ✓)
            grid.AddHandler(ScrollViewer.ScrollChangedEvent,
                new ScrollChangedEventHandler(ExpenseGrid_ScrollChanged));
        }

        /// <summary>Cədvəlin daxili scroll hadisəsi — YALNIZ PLANLA qoyur ✓ (yükləmir ✗).</summary>
        private void ExpenseGrid_ScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            // ⚠ Yalnız cədvəlin ÖZ scroll-u ✓ (səhifənin scroll-u DEYİL ✗)
            if (e.OriginalSource is not ScrollViewer sv || !ReferenceEquals(sv.TemplatedParent, ExpenseGrid))
            {
                return;
            }

            if (e.VerticalChange == 0 && e.ExtentHeightChange == 0 && e.ViewportHeightChange == 0)
            {
                return;
            }

            // 📏 Sondan 600 pikseldən çox uzaqdadırsa → gözlə ✓ (planı ləğv et ✓)
            if (sv.ScrollableHeight - sv.VerticalOffset > 600)
            {
                _xercDahaCoxTaymeri?.Stop();
                return;
            }

            XercDahaCoxPlanla();
        }

        /// <summary>
        /// ⏱ <b>YÜKLƏMƏNİ PLANLA</b> ✓ — taymer hər scroll-da YENİDƏN başlayır ✓ →
        /// yalnız istifadəçi sürüşdürməyi DAYANDIRANDA (600 ms ✓) işə düşür ✓✓✓
        /// </summary>
        private void XercDahaCoxPlanla()
        {
            if (_xercDahaCoxTaymeri is null)
            {
                _xercDahaCoxTaymeri = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(600)
                };

                _xercDahaCoxTaymeri.Tick += (_, _) =>
                {
                    _xercDahaCoxTaymeri?.Stop();

                    if (_viewModel.Expenses.DahaCoxYukleCommand.CanExecute(null))
                    {
                        _viewModel.Expenses.DahaCoxYukleCommand.Execute(null);
                    }
                };
            }

            _xercDahaCoxTaymeri.Stop();
            _xercDahaCoxTaymeri.Start();
        }

        /// <summary>
        /// 🔽 <b>SÜTUN BAŞLIĞINA KLİKLƏ SIRALAMA — SQL TƏRƏFİNDƏ</b> ✓✓✓
        /// <para>
        /// ⚠ Cədvəldə yalnız 200 sətir var ✗ → adi sıralama YANLIŞ olardı ✗
        /// (yalnız yüklənmiş sətirlər sıralanardı ✗)
        /// </para>
        /// <para>
        /// ✅ İNDİ: sıralama <b>bütün baza üzrə</b> SQL <c>ORDER BY</c> ilə ✓
        /// (bazada milyon sətir olsa da sürətli ✓✓✓)
        /// </para>
        /// </summary>
        private void ExpenseGrid_Sorting(object? sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;   // ✗ lokal sıralama SÖNDÜRÜLÜR ✗

            if (e.Column is null)
            {
                return;
            }

            var azalan = e.Column.SortDirection != ListSortDirection.Descending;

            e.Column.SortDirection = azalan ? ListSortDirection.Descending : ListSortDirection.Ascending;

            var nov = e.Column.Header?.ToString() switch
            {
                "Təyinat" => Data.Repositories.ExpenseSort.Teyinat,
                "Qrup" => Data.Repositories.ExpenseSort.Qrup,
                "Kateqoriya" => Data.Repositories.ExpenseSort.Kategoriya,
                "Qeyd" => Data.Repositories.ExpenseSort.Qeyd,
                _ => Data.Repositories.ExpenseSort.Tarix
            };

            _ = _viewModel.Expenses.SıralaAsync(nov, azalan);
        }

        /// <summary>
        /// Kredit əməliyyatının fayl sətrinə iki dəfə klikləyəndə faylı açır.
        /// </summary>
        private void TxAttachmentGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_viewModel.CreditTransactions.OpenAttachmentCommand.CanExecute(null))
            {
                _viewModel.CreditTransactions.OpenAttachmentCommand.Execute(null);
            }
        }

        /// <summary>
        /// Aşağıdaki "Tam Ekran" düyməsi: birinci basışda tam ekrana keçir,
        /// ikinci basışda əvvəlki vəziyyətə qaytarır.
        /// </summary>
        private void BtnFullScreen_Click(object sender, RoutedEventArgs e)
        {
            if (!_isFullScreen)
            {
                _previousState = WindowState;
                _previousStyle = WindowStyle;
                _previousResizeMode = ResizeMode;

                // WindowStyle dəyişikliyinin qüvvəyə minməsi üçün Normal -> Maximized.
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Normal;
                WindowState = WindowState.Maximized;

                _isFullScreen = true;
                BtnFullScreen.Content = "🗗 Tam Ekrandan Çıx";
            }
            else
            {
                WindowStyle = _previousStyle;
                ResizeMode = _previousResizeMode;
                WindowState = WindowState.Normal;
                WindowState = _previousState;

                _isFullScreen = false;
                BtnFullScreen.Content = "⛶ Tam Ekran";
            }
        }

        // ====================================================================
        //   DAXİLİ BRAUZER (WebView2)  -  «🌐 Veb Sayt» tabı
        // ====================================================================

        /// <summary>Daxili brauzer hazırdır?</summary>
        private bool _browserReady;

        /// <summary>
        /// WebView2 mühərrikini hazırlayır. İstifadəçi məlumatları tətbiqin
        /// öz qovluğunda saxlanılır ki, brauzer sistemi çirkləndirməsin.
        /// </summary>
        private async Task InitializeBrowserAsync()
        {
            if (_browserReady)
            {
                return;
            }

            try
            {
                var userDataFolder = Path.Combine(
                    Cas0201.Kok.Qovluq,
                    "EnterpriseAeroStudio",
                    "WebView2");

                Directory.CreateDirectory(userDataFolder);

                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await WebBrowser.EnsureCoreWebView2Async(environment);

                WebBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                WebBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                WebBrowser.CoreWebView2.Settings.IsZoomControlEnabled = true;

                _browserReady = true;
                WebBrowserHint.Visibility = Visibility.Collapsed;

                NavigateBrowser();
            }
            catch (Exception ex)
            {
                WebBrowserHint.Text =
                    "Daxili brauzer açıla bilmədi:\n" + ex.Message +
                    "\n\nYuxarıdaki ünvan düymələri ilə xarici brauzerdə aça bilərsiniz.";
            }
        }

        /// <summary>ViewModel ünvan dəyişməyi tələb etdikdə çağırılır.</summary>
        private void OnWebNavigateRequested(object? sender, string url) => NavigateBrowser(url);

        /// <summary>Daxili brauzeri verilmiş ünvana yönləndirir.</summary>
        private void NavigateBrowser(string? url = null)
        {
            if (!_browserReady)
            {
                return;
            }

            try
            {
                WebBrowser.CoreWebView2.Navigate(url ?? _viewModel.Web.BrowserUrl);
            }
            catch
            {
                // Naviqasiya xətası tətbiqi dayandırmamalıdır.
            }
        }

        /// <summary>Pəncərə bağlananda brauzer və abunəliklər sərbəst buraxılır.</summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _viewModel.Web.NavigateRequested -= OnWebNavigateRequested;
                WebBrowser.Dispose();
            }
            catch
            {
                // Bağlanma xətası əhəmiyyətli deyil.
            }

            base.OnClosed(e);
        }
    }
}
