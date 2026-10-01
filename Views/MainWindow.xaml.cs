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

            // 🟢🔴🟠 STATUS DAİRƏLƏRİ ✓ — hər 2 saniyədə yenilənir ✓
            _daireTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _daireTimer.Tick += (_, _) => DaireleriYenile();
            _daireTimer.Start();

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
                try { _daireTimer?.Stop(); } catch { }
                try { _guncellemeTimer?.Stop(); } catch { }
            };
        }

        // ====================================================================
        //  🚀 AVTOMATİK GÜNCƏLLƏMƏ ✓✓✓ — yoxlama + popup ✓
        // ====================================================================

        /// <summary>⏱️ Güncəlləmə yoxlama taymeri ✓ (hər 10 dəqiqə ✓)</summary>
        private DispatcherTimer? _guncellemeTimer;

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
                Cas0201.Firebase.AppLogger.Melumat("🚀 Güncəlləmə yoxlaması ✓ — " + ex.Message);
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
                Cas0201.Firebase.AppLogger.Melumat("🚀 Güncəlləmə pəncərəsi açılmadı ✓ — " + ex.Message);
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
                Cas0201.Firebase.AppLogger.Melumat("🚀 Güncəlləmə düyməsi ✓ — " + ex.Message);
            }
            finally
            {
                _guncellemeAciq = false;
            }
        }

        // ====================================================================
        //  🟢🔴🟠 STATUS DAİRƏLƏRİ + 💾 SEÇİM QORUNMASI ✓✓✓
        // ====================================================================

        /// <summary>⏱️ Status dairələrini yeniləyən taymer ✓</summary>
        private DispatcherTimer? _daireTimer;

        /// <summary>💾 Son görülən fləşkart vəziyyəti ✓ (dəyişəndə panel avtomatik yenilənir ✓✓✓)</summary>
        private bool _sonUsbVeziyyeti;

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
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeberdarliq("⚠️ Seçim yadda saxlanmadı ✗ — " + ex.Message);
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
                }
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeberdarliq("⚠️ Seçim bərpa olunmadı ✗ — " + ex.Message);
            }
            finally
            {
                _secimler.Clear();
            }
        }

        // ====================================================================
        //  ☁️ BULUD SİNXRON TƏNZİMLƏMƏLƏRİ ✓✓✓
        //  ⚙️ «Tənzimləmələr» tabı → 🛠️ Sistem Alətləri → ☁️ panel
        //  🔐 YALNIZ 👑 Seccad (Admin) və 🛡️ Asif dəyişə bilər ✓ · 🚗 Sahil ✗
        // ====================================================================

        /// <summary>
        /// 🟢🔴🟠 <b>STATUS DAİRƏLƏRİNİ YENİLƏYİR</b> ✓✓✓
        /// <para>
        /// 🖥️ Kompüter (sol) · 💾 USB (orta) · 🔥 Firebase (sağ) ✓<br/>
        /// 🟢 yaşıl = sinxronlaşıb ✓ · 🔴 qırmızı = əlaqə yoxdur ✗ ·
        /// 🟠 narıncı = dəyişiklik var, hələ sinxron olmayıb ✓
        /// </para>
        /// </summary>
        private void DaireleriYenile()
        {
            try
            {
                var kopru = App.Kopru;
                var ayar = Cas0201.Firebase.BuludAyarlari.Cari;

                var indi = DateTime.Now;

                // 🖥️ ① KOMPÜTER (yerli baza) — 🔌 İNTERNETSİZ DƏ TAM İŞLƏYİR ✓✓✓
                var yerli = kopru is null || !kopru.Isleyir
                    ? "#EF4444"                                        // 🔴 işləmir ✗
                    : "#22C55E";                                       // 🟢 işləyir ✓ (offline olsa da ✓)

                // 💾 ② FLASH USB ✓
                var usb = kopru is null || !kopru.UsbHazir
                    ? "#EF4444"                                        // 🔴 taxılmayıb ✗
                    : kopru.SonYedek is null ||
                      (indi - kopru.SonYedek.Value).TotalSeconds >
                      Math.Max(15, ayar.UsbSaniye * 2)
                        ? "#F59E0B"                                    // 🟠 yazılır ✓
                        : "#22C55E";                                   // 🟢 yazılıb ✓

                // 🔥 ③ FIREBASE (bulud) ✓
                var bulud = kopru is null || !kopru.Onlayn
                    ? "#EF4444"                                        // 🔴 əlaqə yoxdur ✗
                    : !string.IsNullOrWhiteSpace(kopru.SonXeta)
                        ? "#F59E0B"                                    // 🟠 gözləyir ✓
                        : "#22C55E";                                   // 🟢 sinxron ✓

                DaireYerli.Fill = Rəng(yerli);
                DaireUsb.Fill = Rəng(usb);
                DaireBulud.Fill = Rəng(bulud);

                // 📊 NAZİK LOADING BAR — yalnız NORMAL sinxron zamanı görünür ✓
                //    ⚠ İnternet yoxdursa dövr uzun çəkir ✗ → bar GİZLƏDİLİR ✗✓✓
                //      (vəziyyəti 🔴 qırmızı FIREBASE dairəsi bildirir ✓)
                var dövrUzun = kopru?.DovruBaslama is null ||
                    (indi - kopru.DovruBaslama.Value).TotalSeconds >
                    Math.Max(8, ayar.FirebaseSaniye * 4);

                SinxronZolaq.Visibility = kopru is not null && kopru.SinxronGedir && !dövrUzun
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;

                // ============================================================
                //  💾 DATA USB PANELİNİ AVTOMATİK YENİLƏ ✓✓✓ (REAL-TIME ✓)
                //  Fləşkart taxılanda / çıxarılanda panel DƏRHAL yenilənir ✓
                //  (əvvəl «🔄 Yenilə» düyməsini əl ilə basmaq lazım idi ✗)
                // ============================================================
                var usbVar = kopru is not null && kopru.UsbHazir;

                if (usbVar != _sonUsbVeziyyeti)
                {
                    _sonUsbVeziyyeti = usbVar;

                    try
                    {
                        _viewModel.UsbYenileCommand.Execute(null);
                    }
                    catch { }
                }

                DaireMetni.Text = kopru is null
                    ? "sinxron başlamayıb ✗"
                    : kopru.Onlayn ? "sinxron ✓" : "oflayn ✗";
            }
            catch
            {
                // 🛡️ status dairəsi heç vaxt proqramı çökdürmür ✗
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

        /// <summary>🌐 Ayar üçün paylaşılan klient ✓ (bir dəfə yaradılır ✓)</summary>
        private static Cas0201.Firebase.FirebaseRestClient? _buludAyarKlienti;

        /// <summary>📂 Panel yüklənəndə cari ayarları göstərir ✓ (Sahil üçün gizlədir ✗)</summary>
        private void BuludAyarPaneli_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🚗 SAHİL → icazə yoxdur ✗ → panel tamamilə gizlədilir ✗✓✓
                if (!Cas0201.Firebase.BuludAyarlari.IcazeVar)
                {
                    if (sender is FrameworkElement element)
                    {
                        element.Visibility = Visibility.Collapsed;
                    }

                    return;
                }

                var cari = Cas0201.Firebase.BuludAyarlari.Cari;

                BuludFbSaniyeQutusu.Text = cari.FirebaseSaniye.ToString();
                BuludUsbSaniyeQutusu.Text = cari.UsbSaniye.ToString();
                BuludYerliSaniyeQutusu.Text = cari.YerliSaniye.ToString();

                // ☁️ AÇAR VƏZİYYƏTİ ✓ (təmiz quraşdırmada BAĞLI gəlir ✓ — tətbiq BOŞ açılır ✓)
                BuludAvtomatikQutusu.IsChecked = cari.Avtomatik;

                BuludAyarStatusuText.Text = Cas0201.Firebase.BuludAyarlari.StatusMetni;
                BuludAyarNeticeText.Text = Cas0201.Firebase.BuludAyarlari.IcazeMetni;

                if (Cas0201.Firebase.BuludAyarlari.TemizQurasdirma)
                {
                    BuludAyarNeticeText.Text =
                        "🆕 Təmiz quraşdırma ✓ — bulud sinxronizasiyası BAĞLIDIR ✗ (tətbiq boş açılır ✓). " +
                        "Buluddaki köhnə məlumatı götürmək üçün ☁️ işarəsini qoyun və «💾 YADDA SAXLA» basın ✓";
                }
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "bulud ayar paneli");
            }
        }

        /// <summary>
        /// ⬇️ <b>BULUDDAN MƏLUMATI GÖTÜR</b> ✓✓✓  (v6.2.12 — YENİ KOMPÜTER / İLK YÜKLƏMƏ)
        /// <para>
        /// 🆕 İstifadəçi şikayəti: yeni kompüterdə məlumat gəlmirdi ✗ və 💾 USB taxmağa
        /// məcbur qalırdı ✗✓✓ → bu düymə BİR KLİKLƏ hər şeyi həll edir:
        /// bulud sinxronizasiyası AKTİV olur ✓ + bütün məlumat DƏRHAL aşağı çəkilir ✓
        /// </para>
        /// <para>⚠ İcazə: yalnız 👑 Admin və 🛡️ Asif ✓ (Sahil üçün panel gizlədilir ✗)</para>
        /// </summary>
        private void BuluddanGotur_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🚗 İcazə yoxsa (Sahil) → düymə işləmir ✗
                if (!Cas0201.Firebase.BuludAyarlari.IcazeVar)
                {
                    BuludAyarNeticeText.Text = Cas0201.Firebase.BuludAyarlari.IcazeMetni;
                    return;
                }

                if (App.Kopru is null)
                {
                    BuludAyarNeticeText.Text =
                        "⚠️ Bulud körpüsü hazır deyil ✗ — proqramı yenidən açın ✓";
                    return;
                }

                var təsdiq = MessageBox.Show(
                    this,
                    "⬇️ BULUDDAN MƏLUMAT GÖTÜRÜLSÜN?\n\n" +
                    "☁️ Buluddaki BÜTÜN məlumat yerli bazaya götürülür ✓:\n" +
                    "   🚗 maşınlar · 💳 kreditlər · ⏳ möhlətlər · 💰 satışlar\n" +
                    "   💸 xərclər · 👥 tərəfdaşlar · 💵 kassa hərəkətləri\n\n" +
                    "🛡️ Yerli məlumat SİLİNMİR ✗ — yalnız buludda DAHA TƏZƏ olanlar\n" +
                    "   tətbiq olunur ✓ (Last-Write-Wins ✓)\n\n" +
                    "☁️ Avtomatik bulud sinxronizasiyası da İŞƏ DÜŞÜR ✓",
                    "⬇️ Buluddan məlumat götür",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (təsdiq != MessageBoxResult.Yes)
                {
                    return;
                }

                // ⬇️ Yükləmə + nəticə mesajı + UI yenilənməsi ✓
                var say = App.BuluddanIlkYukle(this);

                BuludAvtomatikQutusu.IsChecked = true;
                BuludAyarStatusuText.Text = Cas0201.Firebase.BuludAyarlari.StatusMetni;
                BuludAyarNeticeText.Text =
                    $"✅ {say} qeyd buluddan götürüldü ✓ — ☁️ avtomatik sinxron AKTİVDİR ✓";
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "buluddan gotur");
                BuludAyarNeticeText.Text = "⚠️ Xəta ✗ — " + ex.Message;
            }
        }

        /// <summary>💾 YADDA SAXLA — yerli fayl ✓ + Firebase ✓ + dərhal tətbiq ✓</summary>
        private async void BuludAyarSaxla_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _buludAyarKlienti ??=
                    new Cas0201.Firebase.FirebaseRestClient(new Cas0201.Firebase.FirebaseOptions());

                if (!int.TryParse(BuludFbSaniyeQutusu.Text, out var saniye))
                {
                    saniye = Cas0201.Firebase.BuludAyarlari.Cari.FirebaseSaniye;
                }

                if (!int.TryParse(BuludUsbSaniyeQutusu.Text, out var usbSaniye))
                {
                    usbSaniye = Cas0201.Firebase.BuludAyarlari.Cari.UsbSaniye;
                }

                if (!int.TryParse(BuludYerliSaniyeQutusu.Text, out var yerliSaniye))
                {
                    yerliSaniye = Cas0201.Firebase.BuludAyarlari.Cari.YerliSaniye;
                }

                var avtomatik = BuludAvtomatikQutusu.IsChecked == true;

                var netice = await Cas0201.Firebase.BuludAyarlari
                    .YaddaSaxlaAsync(_buludAyarKlienti, saniye, usbSaniye, yerliSaniye, avtomatik);

                BuludAyarNeticeText.Text = netice;
                BuludAyarStatusuText.Text = Cas0201.Firebase.BuludAyarlari.StatusMetni;

                // ⚡ DƏRHAL tətbiq olunur ✓ — proqramı yenidən başlatmaq LAZIM DEYİL ✗✓✓
                Cas0201.Firebase.BuludAyarlari.TətbiqEt(App.Kopru);

                // ☁️ AÇAR: yandırıldı → körpü BAŞLAYIR ✓ · söndürüldü → DAYANIR ✗✓✓
                if (avtomatik)
                {
                    App.Kopru?.Basla();

                    BuludAyarNeticeText.Text += "\n☁️ Bulud sinxronizasiyası YANDIRILDI ✓";
                }
                else
                {
                    App.Kopru?.Dayandir();

                    BuludAyarNeticeText.Text +=
                        "\n✗ Bulud sinxronizasiyası SÖNDÜRÜLDÜ ✓ — buluddan heç nə oxunmur / yazılmır ✗";
                }
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "bulud ayar saxlama");
                BuludAyarNeticeText.Text = "⚠️ Xəta ✗ — " + ex.Message;
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
