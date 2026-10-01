using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.Views
{
    /// <summary>
    /// 🚀 <b>GÜNCƏLLƏMƏ PƏNCƏRƏSİ</b> ✓✓✓ — <b>İKİ REJİM</b> ✓
    /// <list type="bullet">
    ///   <item>🎉 <b>AVTOMATİK</b> ✓ — yeni versiya tapıldıqda açılır ✓<br/>
    ///         Düymələr: <c>[⏰ SONRA]</c> · <c>[🚀 GÜNCƏLLƏ]</c></item>
    ///   <item>🔍 <b>ƏL İLƏ</b> ✓ — «🚀 Güncəlləmə» düyməsi basanda açılır ✓ və özü GitHub-a baxır ✓<br/>
    ///         Düymələr: <c>[🔄 Yenidən yoxla]</c> · <c>[✕ Bağla]</c> · <c>[🌐 GitHub]</c></item>
    /// </list>
    /// <para>⏰ «SONRA» → bu sessiyada susur ✗ — <b>növbəti AÇILIŞDA yenidən soruşulur</b> ✓✓✓</para>
    /// </summary>
    public partial class GuncellemeWindow : Window
    {
        /// <summary>
        /// 📦 Tapılan güncəlləmə ✓ (əl ilə rejimdə əvvəlcə <c>null</c> ✓ — yoxlama tapanda DOLDURULUR ✓✓✓)
        /// <para>
        /// 🐞 ƏVVƏL bu sahə <c>readonly</c> idi ✗ → əl ilə açılan pəncərədə
        /// <c>null</c> qalırdı ✗ → «🚀 GÜNCƏLLƏ» düyməsi <b>SƏSSİZCƏ HEÇ NƏ ETMİRDİ</b> ✗✓✓
        /// </para>
        /// </summary>
        private GuncellemeMelumati? _m;

        /// <summary>🎉 Avtomatik popup? ✓ (yoxsa 🔍 əl ilə yoxlama ✓)</summary>
        private readonly bool _avtomatik;

        /// <summary>⬇️ Yükləmə gedir? ✓ (pəncərə bağlanmasın ✗)</summary>
        private bool _yuklenir;

        /// <summary>🎨 Pəncərə rejimi ✓</summary>
        private enum Rejim { Yoxlanilir, EnSon, Yenivar, Xeta }

        /// <param name="m">📦 Tapılmış güncəlləmə ✓ — <c>null</c> verilsə <b>ƏL İLƏ YOXLAMA</b> rejimi işə düşür ✓✓✓</param>
        public GuncellemeWindow(GuncellemeMelumati? m = null)
        {
            _m = m;
            _avtomatik = m is not null;

            InitializeComponent();
            Closing += OnClosing;

            if (_avtomatik)
            {
                MelumatiDoldur(m!);
                RejimGoster(Rejim.Yenivar);
            }
            else
            {
                // 🔍 ƏL İLƏ YOXLAMA ✓ — pəncərə açılan kimi GitHub-a baxır ✓
                RejimGoster(Rejim.Yoxlanilir);
                Loaded += async (_, _) => await YoxlaAsync();
            }
        }

        /// <summary>🔒 Yükləmə gedərkən pəncərə bağlanmasın ✗</summary>
        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (_yuklenir) e.Cancel = true;
        }

        /// <summary>⏰ «SONRA» — bu sessiyada susur ✗ (növbəti açılışda yenidən gəlir ✓)</summary>
        private void Sonra_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GuncellemeXidmeti.SonrayaAt();

                Cas0201.Firebase.AppLogger.Melumat(
                    $"⏰ Güncəlləmə SONRAYA atıldı ✓ — {_m?.Versiya} (növbəti açılışda soruşulacaq ✓)");
            }
            catch { }

            Close();
        }

        // ====================================================================
        //  🎨 REJİM İDARƏETMƏSİ ✓
        // ====================================================================

        /// <summary>🎨 Pəncərəni seçilmiş rejimə salır ✓ (başlıq · yazılar · düymələr ✓)</summary>
        private void RejimGoster(Rejim r)
        {
            try
            {
                // 🔄 hamısı gizlədilir ✗ → sonra yalnız lazımlılar göstərilir ✓
                YoxlaButton.Visibility = Visibility.Collapsed;
                SonraButton.Visibility = Visibility.Collapsed;
                BaglaButton.Visibility = Visibility.Collapsed;
                GuncelleButton.Visibility = Visibility.Collapsed;
                Zolaq.Visibility = Visibility.Collapsed;
                Zolaq.IsIndeterminate = false;

                switch (r)
                {
                    case Rejim.Yoxlanilir:
                        BasliqText.Text = "🔍 GÜNCƏLLƏMƏ YOXLANILIR…";
                        Boya("#38BDF8");
                        VersiyaText.Text = $"🏷️ Cari versiya: {GuncellemeXidmeti.CariVersiyaMetni}   ·   🌐 github.com/{GuncellemeXidmeti.Sahib}/{GuncellemeXidmeti.Depo}";
                        AdText.Text = "021Cars — Avtomobil Parkı";
                        TarixText.Text = "";
                        QeydText.Text = "📡 GitHub-dan ən son buraxılış soruşulur… ✓";
                        StatusText.Text = "⏳ Bir neçə saniyə gözləyin… (📶 internet sürətindən asılıdır ✓)";
                        Zolaq.Visibility = Visibility.Visible;
                        Zolaq.IsIndeterminate = true;
                        BaglaButton.Visibility = Visibility.Visible;
                        break;

                    case Rejim.EnSon:
                        BasliqText.Text = "✅ ƏN SON VERSİYADASINIZ";
                        Boya("#4ADE80");
                        VersiyaText.Text = $"🏷️ Cari versiya: {GuncellemeXidmeti.CariVersiyaMetni}   ·   ✨ Ən son versiya: {GuncellemeXidmeti.CariVersiyaMetni}";
                        AdText.Text = "Yeni güncəlləmə yoxdur ✓";
                        TarixText.Text = GuncellemeXidmeti.SonYoxlama is null
                            ? ""
                            : $"🕒 Son yoxlama: {GuncellemeXidmeti.SonYoxlama:dd.MM.yyyy HH:mm:ss}";
                        QeydText.Text =
                            "👍 Proqramınız tam yenidir ✓\n\n" +
                            "💡 Yeni versiya buraxıldıqda proqram sizə özü xəbər verəcək ✓\n" +
                            "⏱️ Avtomatik yoxlama: hər 30 dəqiqədə bir + hər açılışda ✓";
                        StatusText.Text = "✅ Yoxlama tamamlandı ✓ — yeni versiya yoxdur ✓";
                        Zolaq.Visibility = Visibility.Visible;
                        Zolaq.Value = 100;
                        YoxlaButton.Visibility = Visibility.Visible;
                        BaglaButton.Visibility = Visibility.Visible;
                        break;

                    case Rejim.Xeta:
                        BasliqText.Text = "⚠️ YOXLANILA BİLMƏDİ";
                        Boya("#F87171");
                        VersiyaText.Text = $"🏷️ Cari versiya: {GuncellemeXidmeti.CariVersiyaMetni}";
                        AdText.Text = "İnternet / GitHub əlçatmazdır ✗";
                        TarixText.Text = "";
                        QeydText.Text =
                            "📶 İnternet bağlantısını yoxlayın ✓\n\n" +
                            "💡 Bu, proqramın işinə MANE OLMUR ✗ — məlumatlar sinxron olmağa davam edir ✓\n" +
                            "⏱️ Növbəti avtomatik yoxlama: 30 dəqiqə sonra ✓";
                        StatusText.Text = "⚠️ Yoxlama alınmadı ✗ — «🔄 Yenidən yoxla» basın ✓";
                        YoxlaButton.Visibility = Visibility.Visible;
                        BaglaButton.Visibility = Visibility.Visible;
                        break;

                    case Rejim.Yenivar:
                        // 🎉 məlumat artıq doldurulub ✓ — yalnız düymələr göstərilir ✓
                        SonraButton.Visibility = Visibility.Visible;
                        GuncelleButton.Visibility = Visibility.Visible;
                        break;
                }
            }
            catch { }
        }

        /// <summary>🎨 Başlığın rəngini dəyişir ✓</summary>
        private void Boya(string hex)
        {
            try
            {
                BasliqText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            }
            catch { }
        }

        /// <summary>📦 Tapılmış güncəlləmə məlumatını pəncərəyə doldurur ✓</summary>
        private void MelumatiDoldur(GuncellemeMelumati m)
        {
            try
            {
                BasliqText.Text = "🎉 YENİ GÜNCƏLLƏMƏ VAR";
                Boya("#38BDF8");

                VersiyaText.Text =
                    $"🏷️ Cari versiya: {GuncellemeXidmeti.CariVersiyaMetni}   →   ✨ Yeni versiya: {m.Versiya}";

                AdText.Text = string.IsNullOrWhiteSpace(m.Ad) ? "021Cars — Avtomobil Parkı" : m.Ad;

                TarixText.Text = m.Tarix is null
                    ? (m.Olcu > 0 ? $"📊 Ölçü: {m.Olcu / 1024 / 1024} MB" : "")
                    : $"📅 {m.Tarix:dd.MM.yyyy HH:mm}   ·   📊 {m.Olcu / 1024 / 1024} MB";

                QeydText.Text = string.IsNullOrWhiteSpace(m.Qisa)
                    ? "• Bu buraxılış üçün ətraflı qeyd yazılmayıb ✓"
                    : m.Qisa;

                StatusText.Text =
                    "💡 «🚀 GÜNCƏLLƏ» basın — proqram avtomatik bağlanıb yenilənəcək və yenidən açılacaq ✓\n" +
                    "🛡️ Məlumatlarınız (baza · media · yedəklər) QORUNUR ✓✓✓";
            }
            catch { }
        }

        /// <summary>🔍 GitHub-a yoxlama gedir ✓ (həm əl ilə ✓, həm «🔄 Yenidən yoxla» ✓)</summary>
        private async Task YoxlaAsync()
        {
            try
            {
                RejimGoster(Rejim.Yoxlanilir);

                var m = await GuncellemeXidmeti.YoxlaAsync();

                if (m is not null)
                {
                    // ★ VACİB ★ — tapılan məlumat YADDA SAXLANILIR ✓✓✓
                    //    (əks halda «🚀 GÜNCƏLLƏ» düyməsi işləmirdi ✗)
                    _m = m;

                    MelumatiDoldur(m);
                    RejimGoster(Rejim.Yenivar);
                    return;
                }

                _m = null;

                RejimGoster(GuncellemeXidmeti.CariVəziyyət == GuncellemeXidmeti.Vəziyyət.Xeta
                    ? Rejim.Xeta
                    : Rejim.EnSon);
            }
            catch
            {
                RejimGoster(Rejim.Xeta);
            }
        }

        // ====================================================================
        //  🖱️ YENİ DÜYMƏLƏR ✓
        // ====================================================================

        /// <summary>🔄 «YENİDƏN YOXLA» ✓ — GitHub-a yenidən baxır ✓</summary>
        private void Yoxla_Click(object sender, RoutedEventArgs e) => _ = YoxlaAsync();

        /// <summary>✕ «BAĞLA» ✓ — pəncərəni bağlayır ✓ (proqram işləməyə davam edir ✓)</summary>
        private void Bagla_Click(object sender, RoutedEventArgs e) => Close();

        /// <summary>🌐 «GITHUB» ✓ — buraxılışlar səhifəsini brauzerdə açır ✓</summary>
        private void Sayt_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    GuncellemeXidmeti.BuraxilisSehifesi)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    MessageBox.Show("🌐 Səhifə: " + GuncellemeXidmeti.BuraxilisSehifesi,
                        "021Cars", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            }
        }

        /// <summary>🚀 «GÜNCƏLLƏ» — yükləyir ✓ · installer işə salınır ✓ · proqram bağlanır ✗</summary>
        private async void Guncelle_Click(object sender, RoutedEventArgs e)
        {
            if (_yuklenir) return;

            // 🛡️ «HEÇ NƏ OLMUR» QORUYUCUSU ✓✓✓ — ★ VACİB ★
            //    Əl ilə rejimdə məlumat əldə yoxdursa → burada ƏLDƏ EDİLİR ✓
            //    (əvvəl səssizcə `return` edirdi ✗ → istifadəçi «heç nə olmur» deyirdi ✗✓✓)
            var m = _m;

            if (m is null)
            {
                _yuklenir = true;

                GuncelleButton.IsEnabled = false;
                SonraButton.IsEnabled = false;
                Zolaq.Visibility = Visibility.Visible;
                Zolaq.IsIndeterminate = true;
                StatusText.Text = "🔍 GitHub yoxlanılır…";

                try
                {
                    m = await GuncellemeXidmeti.YoxlaAsync();

                    if (m is null)
                    {
                        Zolaq.IsIndeterminate = false;
                        Zolaq.Visibility = Visibility.Collapsed;
                        StatusText.Text = "👍 Yeni versiya yoxdur ✓ — proqramınız ən son versiyadadır ✓";

                        MessageBox.Show(
                            "👍 Yeni versiya yoxdur ✓\n\n" +
                            $"🏷️ Cari versiya: {GuncellemeXidmeti.CariVersiyaMetni} — ən son versiyadadır ✓",
                            "021Cars — Güncəlləmə",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        return;
                    }

                    _m = m;

                    MelumatiDoldur(m);
                    RejimGoster(Rejim.Yenivar);
                }
                catch (Exception ex)
                {
                    Cas0201.Firebase.AppLogger.Xeta(ex, "güncəlləmə düyməsi (yoxlama)");
                    StatusText.Text = "⚠️ Yoxlama alınmadı ✗ — " + ex.Message;
                    return;
                }
                finally
                {
                    _yuklenir = false;
                    GuncelleButton.IsEnabled = true;
                    SonraButton.IsEnabled = true;
                    Zolaq.IsIndeterminate = false;
                }
            }

            if (m is null) return;

            _yuklenir = true;

            // 📜 Loq ✓ — gələcəkdə problem olsa DƏRHAL görünür ✓✓✓
            try
            {
                Cas0201.Firebase.AppLogger.Melumat(
                    $"🚀 «GÜNCƏLLƏ» basıldı ✓ — {m.Versiya} ({m.Olcu / 1024 / 1024} MB) ✓ → yükləmə başlayır ✓");
            }
            catch { }

            GuncelleButton.IsEnabled = false;
            SonraButton.IsEnabled = false;
            Zolaq.Visibility = Visibility.Visible;
            Zolaq.IsIndeterminate = false;   // 📊 faiz göstərilsin ✓ (indeterminate OLMASIN ✗)
            Zolaq.Value = 0;

            try
            {
                StatusText.Text = "⬇️ Güncəlləmə faylı yüklənir… (📶 internet sürətindən asılıdır ✓)";

                var faiz = new Progress<double>(p =>
                {
                    try
                    {
                        Zolaq.Value = p;
                        StatusText.Text = $"⬇️ Yüklənir…  {p:F0}%   ({m.Olcu / 1024 / 1024} MB ✓)";
                    }
                    catch { }
                });

                var fayl = await GuncellemeXidmeti.YukleAsync(m, faiz);

                if (string.IsNullOrWhiteSpace(fayl))
                {
                    StatusText.Text = "⚠️ Yükləmə alınmadı ✗ — internet bağlantısını yoxlayın və yenidən cəhd edin ✓";

                    MessageBox.Show(
                        "⚠️ Güncəlləmə faylı yüklənə bilmədi ✗\n\n" +
                        "📶 İnternet bağlantısını yoxlayın ✓ və yenidən «🚀 GÜNCƏLLƏ» basın ✓",
                        "021Cars — Güncəlləmə",
                        MessageBoxButton.OK, MessageBoxImage.Warning);

                    return;
                }

                Zolaq.Value = 100;
                StatusText.Text = "✅ Yükləndi ✓ — proqram bağlanır ✗ · fayllar yenilənir ✓ · sonra YENİDƏN açılacaq ✓✓✓";

                await Task.Delay(700);

                // 🚪 Proqram bağlanmalıdır ✗ — installer faylları dəyişəcək ✓
                //    (installer sonra proqramı ÖZÜ yenidən açır ✓✓✓)
                // ============================================================
                //  🐞 REAL XƏTA (istifadəçi): «bağlanır yazır amma BAĞLANMIR ✗ →
                //     özüm əl ilə bağlayıram, yoxsa "iki proqram açıq ola bilməz" ✗»
                // ------------------------------------------------------------
                //  Səbəb: `CixisLazim` FON THREAD-dən çağırılır ✗
                //  (`Task.Delay(...).ContinueWith` ✓) → WPF-də `Application.Shutdown()`
                //  YALNIZ UI thread-dən işləyir ✗ → istisna atılırdı ✗ və səssizcə
                //  udulurdu ✗✓✓
                //
                //  ✅ İNDİ: ① UI thread-ə keçilir ✓ ② Shutdown ✓
                //            ③ zəmanət: 3 saniyə sonra hələ də açıqdırsa → məcburi
                //            `Environment.Exit` ✓✓✓ (installer mütləq gözləməsin ✗)
                // ============================================================
                GuncellemeXidmeti.CixisLazim += () =>
                {
                    try
                    {
                        var app = Application.Current;

                        if (app is null)
                        {
                            Environment.Exit(0);
                            return;
                        }

                        app.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { app.Shutdown(); } catch { }

                            // 🛡️ ZƏMANƏT — 3 saniyə sonra proses hələ də varsa məcburi çıxış ✓
                            Task.Delay(3000).ContinueWith(_ =>
                            {
                                try { Environment.Exit(0); } catch { }
                            });
                        }));
                    }
                    catch
                    {
                        try { Environment.Exit(0); } catch { }
                    }
                };

                if (!GuncellemeXidmeti.BaslatGuncelleme(fayl!))
                {
                    StatusText.Text = "⚠️ Güncəlləmə başladıla bilmədi ✗ — «Bəli» (admin) seçimini təsdiqləyin ✓";

                    MessageBox.Show(
                        "⚠️ Güncəlləmə başladıla bilmədi ✗\n\n" +
                        "🔐 Admin icazəsi təsdiqlənmədi ✗ — yenidən cəhd edin və «Bəli» basın ✓",
                        "021Cars — Güncəlləmə",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                // ✅ Uğurlu → proqram bağlanır ✗ (installer özü yenidən açacaq ✓)
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "güncəlləmə pəncərəsi");
                StatusText.Text = "⚠️ Xəta ✗ — " + ex.Message;
            }
            finally
            {
                _yuklenir = false;
                GuncelleButton.IsEnabled = true;
                SonraButton.IsEnabled = true;
                YoxlaButton.Visibility = Visibility.Visible;   // 🔄 uğursuz olsa → yenidən yoxlamaq olar ✓
            }
        }
    }
}
