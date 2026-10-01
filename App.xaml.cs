using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using EnterpriseAeroStudio.Data;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Hosting;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;
using EnterpriseAeroStudio.ViewModels;
using EnterpriseAeroStudio.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace EnterpriseAeroStudio
{
    /// <summary>
    /// Tətbiqin giriş nöqtəsi. Serilog, Dependency Injection və
    /// qlobal xəta idarəetməsi burada qurulur.
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider? _services;
        private ILogger<App>? _logger;

        /// <summary>
        /// 🚫 <b>TƏK NÜSXƏ KİLİDİ</b> ✓✓✓ — ★ VACİB ★
        /// <para>
        /// ⚠️ İki nüsxə eyni anda işləsə ✗ → ikisi də EYNİ
        /// <c>D:\021Cars\Yedekler\Json\*.json</c> fayllarına yazır ✗ →
        /// <c>The process cannot access the file … being used by another process</c> ✗ +
        /// yedək İKİQAT yazılır ✗ + eyni loqa iki sətir düşür ✗ → disk yorulur ✗ → DONMA ✗✓✓
        /// </para>
        /// <para>✅ İndi ikinci nüsxə heç AÇILMIR ✗ — yalnız birinci işləyir ✓</para>
        /// </summary>
        private static Mutex? _tekNusxeKilidi;

        /// <summary>Tətbiqin DI konteyneri — pəncərələrin xidmətləri tapması üçün.</summary>
        public static IServiceProvider? Services { get; private set; }

        /// <summary>
        /// ☁️ <b>FIREBASE BULUD KÖRPÜSÜ</b> ✓✓✓ — tətbiq açılanda işə düşür ✓
        /// <para>HƏR 5 SANİYƏDƏN BİR bütün məlumat (avtomobil · xərc · satış · kredit ·
        /// əməliyyat · tərəfdaş · pay · ödəniş) buluda göndərilir ✓</para>
        /// <para>📎 PDF / ŞƏKİL / MEDIA GÖNDƏRİLMİR ✗ — onlar YALNIZ fləşkartda qalır ✓</para>
        /// </summary>
        public static Cas0201.Firebase.BuludKopru? Kopru { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ================================================================
            //  🚫 TƏK NÜSXƏ (SINGLE INSTANCE) ✓✓✓ — ★ DONMANIN QARŞISI ★
            // ----------------------------------------------------------------
            //  ⚠️ İki nüsxə birlikdə: USB JSON yazısı toqquşur ✗, yedək ikiqat ✗,
            //     eyni loqa iki sətir ✗ → disk yorulur ✗ → proqram DONUR ✗✓✓
            //  ✅ İndi ikinci nüsxə açılmır ✗
            // ================================================================
            if (!TekNusxeTut())
            {
                MessageBox.Show(
                    "021Cars artıq açıqdır ✓\n\n" +
                    "İkinci nüsxə AÇILMIR ✗ — çünki iki nüsxə birlikdə işləsə\n" +
                    "fləşkart yedəyi toqquşur və proqram donur ✗✓✓",
                    "021Cars — artıq işləyir",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Shutdown();
                return;
            }

            ConfigureCulture();
            ConfigureSerilog();

            // ================================================================
            //  🖱️ CTRL + SİÇAN ÇARXI = SAĞA/SOLA SÜRÜŞMƏ ✓✓✓
            //  (proqram SİÇAN + KLAVİATURA ilə idarə olunur ✓ — cədvəllər ✓,
            //   siyahılar ✓, dialoqlar ✓ — HAMISI ✓✓✓)
            // ================================================================
            try { Views.UiSürüşmə.QlobalQos(); } catch { }

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            _services = BuildServices();
            Services = _services;
            _logger = _services.GetRequiredService<ILogger<App>>();

            // ================================================================
            //  ☁️ FIREBASE BULUD KÖRPÜSÜ — HƏR 5 SANİYƏDƏN BİR BÜTÜN MƏLUMAT ✓
            //  📎 PDF / ŞƏKİL / MEDIA GÖNDƏRİLMİR ✗ (yalnız fləşkartda qalır ✓)
            // ================================================================
            try
            {
                var fb = new Cas0201.Firebase.FirebaseOptions();
                fb.TokenYukle();                         // 🔐 CAS_FIREBASE_TOKEN (varsa ✓)
                fb.ClientiQur();

                Kopru = new Cas0201.Firebase.BuludKopru(
                    _services!, new Cas0201.Firebase.FirebaseRestClient(fb), fb);

                // ⚙️ TƏNZİMLƏMƏLƏR — yalnız 👑 Seccad + 🛡️ Asif dəyişə bilər ✓
                Cas0201.Firebase.BuludAyarlari.Yukle();
                Cas0201.Firebase.BuludAyarlari.TətbiqEt(Kopru);

                Kopru.Basla();                           // ⏱️ ayarlardaki fasilə ilə ✓

                // ☁️ Digər kompüter ayarı dəyişibsə → hər 60 saniyədə götür və CANLI tətbiq et ✓
                var ayarKlient = new Cas0201.Firebase.FirebaseRestClient(fb);

                _ = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(60));

                            await Cas0201.Firebase.BuludAyarlari.YukleBuluddanAsync(ayarKlient);
                            Cas0201.Firebase.BuludAyarlari.TətbiqEt(Kopru);
                        }
                        catch (Exception ex)
                        {
                            Cas0201.Firebase.AppLogger.Xeta(ex, "ayar dövrü");
                        }
                    }
                });

                _logger.LogInformation(
                    "☁️ Bulud körpüsü başladı — hər 5 saniyədə bir bütün məlumat buluda ✓");
            }
            catch (Exception ex)
            {
                // 🛡️ Bulud olmasa da tətbiq TAM İŞLƏYİR ✓✓✓ (çökmə YOX ✗)
                Cas0201.Firebase.AppLogger.Xeta(ex, "App: bulud körpüsü");
            }

            // Hesabatın PDF çeviricisi — gizli WebView2 (Chromium) ilə.
            // Beləliklə Azərbaycan hərfləri PDF-də düzgün görünür.
            // ⚠ Lambda istifadə olunur: `RenderAsync`-in əlavə (optional)
            //   parametrləri var → metod qrupu delegata uyğun gəlmir ✗✓✓
            ReportService.PdfRenderer = (html, pdfYolu) => Hosting.HtmlPdfWriter.RenderAsync(html, pdfYolu);

            _logger.LogInformation("Tətbiq işə düşür: Avtomobil Parkı v6.0");

            try
            {
                // ================================================================
                //  ① 🏗️ SXEM ƏVVƏLCƏ HAZIRLANIR ✓✓✓ (VACİB ✗✓✓)
                // ----------------------------------------------------------------
                //  ⚠️ SƏBƏB ✗: USB birləşdirməsi ƏVVƏL işləyirdi ✗ → yerli bazada
                //     yeni sütunlar (məs. `SiraNomresi` ✓) hələ yaradılmamışdı ✗ →
                //     həmin sütunların dəyəri KÖÇÜRÜLMÜRDÜ ✗ → «312 → 0» ✗✓✓
                //  ✅ İNDİ: əvvəlcə miqrasiya/sxem TAM hazır olur ✓, sonra birləşdirmə ✓
                // ================================================================
                InitializeDatabase();

                // 🔢 ② Sıra nömrələri (yerli ✓) — yalnız «0» olanlara nömrə verilir ✓
                SiraNomreleriDuzelt();

                // ================================================================
                //  📥 ③ USB-DƏN BƏRPA / BİRLƏŞDİRMƏ ✓✓✓
                // ----------------------------------------------------------------
                //  🆕 Yerli baza BOŞDURSA ✓ → USB-dəki məlumat gəlir ✓
                //  🔀 Yerli baza DOLUDURSA ✓ → BİRLƏŞDİRİLİR ✓ (heç nə İTMİR ✗✓✓)
                //  🛡️ ƏSLA üzərinə yazılmır ✗✓✓
                // ================================================================
                try
                {
                    // 🔑 Qeydiyyatsız DATA USB-ni AVTOMATİK TANIT ✓✓✓
                    var tanıma = EnterpriseAeroStudio.Services.DataUsbService.UsbAvtomatikTanit();
                    Cas0201.Firebase.AppLogger.Melumat("🔑 USB: " + tanıma);

                    // 💾 BƏRPA ✓ / 🔀 BİRLƏŞDİRMƏ ✓
                    var bərpa = EnterpriseAeroStudio.Services.DataUsbService.UsbDenBerpaEt();
                    Cas0201.Firebase.AppLogger.Melumat("💾 USB bərpa: " + bərpa);
                }
                catch (Exception ex)
                {
                    Cas0201.Firebase.AppLogger.Melumat("💾 USB bərpa yoxlanmadı ✓ — " + ex.Message);
                }

                // ================================================================
                //  ④ 🔢 BİRLƏŞDİRMƏDƏN SONRA SIRA NÖMRƏLƏRİ YENİDƏN YOXLANILIR ✓✓✓
                // ----------------------------------------------------------------
                //  🩹 USB-dən gələn maşının sıra nömrəsi «0» qalıbsa → burada düzəlir ✓
                //  ✅ İstifadəçinin ÖZ nömrəsi (məs. 312 ✓) HEÇ VAXT dəyişmir ✗✓✓
                // ================================================================
                SiraNomreleriDuzelt();

                // ================================================================
                //  🔐 GİRİŞ PƏNCƏRƏSİ — PROQRAM AÇILANDA İLK BU GƏLİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ VACİB: `ShutdownMode="OnExplicitShutdown"` (App.xaml ✓)
                //    Əks halda login pəncərəsi bağlananda pəncərə sayı SIFIR
                //    olur ✗ → WPF proqramı AVTOMATİK söndürür ✗ → əsas pəncərə
                //    heç vaxt açılmır ✗✓✓ (istifadəçinin şikayəti ✓)
                //  ✅ Uğurlu giriş → əsas pəncərə açılır ✓ və ROL tətbiq olunur ✓
                //  ✗ Uğursuz / ləğv edilərsə → proqram BAĞLANIR ✗✓✓
                // ================================================================
                var girisPenceresi = new Views.LoginWindow();

                if (girisPenceresi.ShowDialog() != true || girisPenceresi.Istifadeci is null)
                {
                    Shutdown();
                    return;
                }

                // ================================================================
                //  🏷️ MEDIA QOVLUQLARI MAŞININ ADI İLƏ ADLANIR ✓✓✓
                // ----------------------------------------------------------------
                //  ✅ «D:\021Cars\Media\Avtomobil\001 - Hyundai Sonata - 99QE103\»
                //     (əvvəl YALNIZ «61» kimi rəqəm idi ✗✓✓)
                //  ⚠ Maşın tapılmazsa → refId (61) ✓ — proqram çökmür ✗
                // ================================================================
                EnterpriseAeroStudio.Services.MediaService.QovluqAdiProvider = (tip, id) =>
                {
                    try
                    {
                        // 🚗 Əvvəlcə AVTOMOBİL İD-sini tapırıq ✓
                        //    (birbaşa ✓ və ya kredit əməliyyatı vasitəsilə ✓)
                        var avtomobilId = id;

                        if (!string.Equals(tip, EnterpriseAeroStudio.Models.MediaRefTypes.Car,
                                StringComparison.Ordinal))
                        {
                            // 💳 KREDİT ƏMƏLİYYATI sənədi → kredit → AVTOMOBİL ✓✓✓
                            try
                            {
                                using var scope = _services.CreateScope();

                                var db = scope.ServiceProvider
                                    .GetRequiredService<EnterpriseAeroStudio.Data.AppDbContext>();

                                var əməliyyat = db.CreditTransactions.FirstOrDefault(t => t.Id == id);

                                var kredit = əməliyyat?.CreditId is int kId
                                    ? db.Credits.FirstOrDefault(k => k.Id == kId)
                                    : null;

                                if (kredit?.CarId is not int cId) return null;   // ✓ ehtiyat: refId ✓

                                avtomobilId = cId;
                            }
                            catch
                            {
                                return null;      // ✓ ehtiyat: refId («61») ✓
                            }
                        }

                        var avtomobilXidmeti = _services.GetRequiredService<ICarService>();

                        var avtomobil = avtomobilXidmeti
                            .GetAllCarsAsync()
                            .GetAwaiter()
                            .GetResult()
                            .FirstOrDefault(c => c.Id == avtomobilId);

                        if (avtomobil is null)
                        {
                            return null;
                        }

                        // ✅ Sıra № · Marka (model) · Dövlət qeydiyyat nişanı ✓✓✓
                        var sira = avtomobil.SiraNomresi > 0
                            ? avtomobil.SiraNomresi.ToString("000")
                            : avtomobil.Id.ToString();

                        return $"{sira} - {avtomobil.Marka} - {avtomobil.QeydiyyatNisani}";
                    }
                    catch
                    {
                        return null;      // ✓ ehtiyat: «61» ✓
                    }
                };

                var window = _services.GetRequiredService<MainWindow>();
                MainWindow = window;
                window.Show();

                // ================================================================
                //  🆕 ☁️ YENİ KOMPÜTER — «BULUDDAN GÖTÜR?» ✓✓✓  (v6.2.12)
                // ----------------------------------------------------------------
                //  ⚠ İSTİFADƏÇİ ŞİKAYƏTİ: yeni kompüterə yükləyəndə tətbiq BOŞ
                //    açılırdı ✗ (bulud sinxronu qəsdən sönülü ✓) → məlumat
                //    görünmürdü ✗ və 💾 USB taxmağa MƏCBUR qalırdı ✗✓✓
                //  ✅ İNDİ: təmiz quraşdırma + BOŞ baza → BİR DƏFƏ soruşulur ✓
                //     «Bəli» → bütün məlumat buluddan DƏRHAL götürülür ✓✓✓
                // ================================================================
                IlkQurasdirmaYoxla(window);

                // ================================================================
                //  🔎 FON REJİMİNDƏ: KÖHNƏ XƏRC QEYDLƏRİNİN AXTARIŞ MƏTNİ ✓✓✓
                // ----------------------------------------------------------------
                //  • Bir dəfəlik işdir ✓ (sonrakı açılışlarda heç nə etmir ✗)
                //  • 5 000-lik dəstələrlə ✓ → RAM şişmir ✗ · UI DONMUR ✗✓✓
                //  • Bitəndə yalnız LOQ yazılır ✓ (istifadəçi heç nə hiss etmir ✓)
                // ================================================================
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _services!.CreateScope();
                        var xərcXidməti = scope.ServiceProvider
                            .GetRequiredService<EnterpriseAeroStudio.Services.IExpenseService>();

                        var düzəldilən = await xərcXidməti.BackfillAxtarisAsync();

                        if (düzəldilən > 0)
                        {
                            _logger?.LogInformation(
                                "🔎 {Say} xərc qeydinin axtarış mətni hazırlandı ✓ (sürətli axtarış üçün ✓).",
                                düzəldilən);
                        }
                    }
                    catch (Exception ex)
                    {
                        Cas0201.Firebase.AppLogger.Melumat("🔎 Axtarış mətni hazırlanmadı — " + ex.Message);
                    }
                });

                // ================================================================
                //  ⏱️ HƏR 5 SANİYƏ — BÜTÜN MƏLUMATLAR AVTOMATİK YENİLƏNİR ✓✓✓
                // ----------------------------------------------------------------
                //  • Bütün tablar: Avto Park · Xərclər · Satış · Arxiv ·
                //    Kreditlər · Maliyyə · Tərəfdaşlar ✓
                //  • UI DONMUR ✗ (DispatcherTimer ✓ + async ✓ + təkrar kilid ✓)
                //  • Hər hansı xəta SƏSSİZCƏ udulur ✓ (proqram işləyir ✓)
                // ================================================================
                if (window.DataContext is ViewModels.MainViewModel anaVm)
                {
                    // ⏱️ 30 saniyə ✓ (ƏVVƏL 5 ✗) — böyük bazada hər 5 saniyəlik
                    // tam yeniləmə «işləyərkən donma» yaradırdı ✗✓✓
                    // ✅ Real dəyişikliklər onsuz da ANİ tutulur ✓ (fayl izləyicisi ✓)
                    anaVm.AvtoYenilemeyeBasla(30);
                }

                // ✅ Əsas pəncərə açıldı → NORMAL bağlanma rejimi bərpa olunur ✓✓✓
                //  (əsas pəncərə bağlananda proqram düzgün bağlanır ✓)
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Tətbiq başladıla bilmədi.");
                MessageBox.Show(
                    "Tətbiq başladıla bilmədi:\n" + ex.Message,
                    "Kritik Xəta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        /// <summary>
        /// 🆕 ☁️ <b>YENİ KOMPÜTER AŞKARLANMASI</b> ✓✓✓  (v6.2.12)
        /// <para>
        /// Təmiz quraşdırma (ayar faylı YOXDUR ✓) + baza BOŞDURSA → istifadəçidən
        /// «buluddaki məlumat götürülsün?» soruşulur ✓✓✓
        /// </para>
        /// <para>
        /// ✅ «Bəli» → bulud sinxronizasiyası AKTİV olur ✓ + DƏRHAL tam dövr işlədilir ✓
        /// → 🚗 maşınlar · 💳 kreditlər · ⏳ möhlətlər · 💰 satışlar · 💸 xərclər ·
        /// 👥 tərəfdaşlar yerli bazaya yazılır ✓✓✓
        /// </para>
        /// <para>✗ «Xeyr» → heç nə edilmir ✓ (sonra ⚙️ Tənzimləmələr → «⬇️ BULUDDAN GÖTÜR» ✓)</para>
        /// <para>
        /// ⚠ <b>YALNIZ BİR DƏFƏ</b> ✓ — ayar faylı yaranan kimi <c>TemizQurasdirma</c>
        /// <c>false</c> olur ✓ → növbəti açılışlarda soruşulmur ✗
        /// </para>
        /// <para>
        /// 🛡️ Baza BOŞ deyilsə HEÇ SORUŞULMUR ✗ (mövcud məlumatı olan kompüterə bulud
        /// «yad» məlumat gətirə bilməz ✗✓✓)
        /// </para>
        /// </summary>
        private void IlkQurasdirmaYoxla(Window window)
        {
            try
            {
                // ① 🆕 Təmiz quraşdırmadır? (ayar faylı yoxdur ✓)
                if (!Cas0201.Firebase.BuludAyarlari.TemizQurasdirma)
                {
                    return;
                }

                // ② ☁️ Bulud körpüsü hazırdır?
                if (Kopru is null || _services is null)
                {
                    return;
                }

                // ③ 🗄️ Baza BOŞDURMU? (SİNXRON EF API ✓ — deadlock TƏHLÜKƏSİ YOXDUR ✗✓✓)
                bool bos;

                using (var bosScope = _services.CreateScope())
                {
                    var bosDb = bosScope.ServiceProvider
                        .GetRequiredService<EnterpriseAeroStudio.Data.AppDbContext>();

                    bos = !bosDb.Cars.Any()
                          && !bosDb.Credits.Any()
                          && !bosDb.Sales.Any()
                          && !bosDb.Expenses.Any()
                          && !bosDb.Partners.Any();
                }

                if (!bos)
                {
                    // ⚠ Məlumat var → bulud «ilk yükləmə» QADAĞANDIR ✗ (təhlükəsizlik ✓)
                    Cas0201.Firebase.AppLogger.Melumat(
                        "🆕 Təmiz quraşdırma ✓ lakin baza BOŞ DEYİL ✗ — ilk yükləmə soruşulmadı ✓");

                    return;
                }

                // ================================================================
                //  ⬇️ AVTOMATİK — İSTİFADƏÇİDƏN SORUŞULMUR ✓✓✓  (v6.2.13)
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL: modal sual verilirdi ✗ → istifadəçi «əllə basmaq
                //  lazım idi» deyə şikayət etdi ✗✓✓
                //  ✅ İNDİ: boş baza + təmiz quraşdırma → məlumat AVTOMATİK
                //  buluddan götürülür ✓ (yalnız BOŞ bazada ✓ — təhlükəsiz ✓✓✓)
                // ================================================================
                Cas0201.Firebase.AppLogger.Melumat(
                    "🆕 Boş baza aşkarlandı ✓ — bulud məlumatı AVTOMATİK götürülür ✓ (sual verilmir ✓)");

                // ⑤⑥ ⬇️ Faktiki yükləmə + nəticə ✓
                BuluddanIlkYukle(window);
            }
            catch (Exception ex)
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
                _logger?.LogError(ex, "İlk quraşdırma bulud yoxlaması alınmadı.");
            }
        }

        /// <summary>
        /// ⬇️ <b>BULUDDAN İLK YÜKLƏMƏ</b> ✓✓✓ — məlumatı götürür + nəticəni göstərir ✓
        /// (v6.2.12 — həm açılışda avtomatik ✓ həm ⚙️ Tənzimləmələr düyməsindən ✓)
        /// </summary>
        internal static int BuluddanIlkYukle(Window? window)
        {
            Cas0201.Firebase.AppLogger.Melumat("⬇️ İlk yükləmə başladı ✓ — buluddan məlumat götürülür…");

            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

            int çekilen;

            try
            {
                // 🛡️ `Task.Run` → fon ipi ✓ (SynchronizationContext YOXDUR ✗ →
                //    EF davamı UI ipini gözləmir ✗ → DEADLOCK TƏHLÜKƏSİ YOXDUR ✓✓✓)
                çekilen = Task.Run(() => App.Kopru!.IlkYuklemeAsync()).GetAwaiter().GetResult();
            }
            finally
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }

            // 🔄 UI DƏRHAL yenilənir ✓ (bütün tablar ✓)
            if (window?.DataContext is MainViewModel bVm)
            {
                bVm.HamisiniIndiYenileCommand.Execute(null);
            }

            MessageBox.Show(
                window ?? Current.MainWindow,
                "✅ BULUDDAN MƏLUMAT GÖTÜRÜLDÜ ✓\n\n" +
                $"📥 {çekilen} qeyd yerli bazaya yazıldı ✓\n\n" +
                "☁️ Avtomatik bulud sinxronizasiyası İŞLƏYİR ✓\n" +
                "   (yeni dəyişikliklər hər 10 saniyədə avtomatik gedir ✓)\n\n" +
                "⚠ Məlumat görünmürsə → «🔄 Bütün məlumatları yenilə» düyməsini basın ✓",
                "Uğurlu ✓",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return çekilen;
        }

        /// <summary>
        /// 🔢 <b>SIRA NÖMRƏLƏRİNİ DÜZƏLDİRİR</b> ✓✓✓ —
        /// yalnız <c>0</c> nömrəli avtomobillərə nömrə verir ✓
        /// <para>
        /// ✅ İstifadəçinin ÖZ yazdığı nömrə (məs. <c>312</c> ✓) <b>HEÇ VAXT dəyişmir</b> ✗✓✓
        /// </para>
        /// </summary>
        private void SiraNomreleriDuzelt()
        {
            try
            {
                using var siraScope = _services!.CreateScope();

                var carXidmeti = siraScope.ServiceProvider.GetRequiredService<ICarService>();
                var düzəldilən = carXidmeti.SiraNomreleriniDuzeltAsync().GetAwaiter().GetResult();

                if (düzəldilən > 0)
                {
                    _logger?.LogInformation("🔢 {Say} avtomobilin sıra nömrəsi düzəldildi.", düzəldilən);
                }
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Melumat("🔢 Sıra nömrəsi düzəlişi ✓ — " + ex.Message);
            }
        }

        private void InitializeDatabase()
        {
            using var scope = _services!.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
            initializer.InitializeAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Bütün tarix sahələri gün.ay.il (gg.aa.iiii) formatında göstərilsin və daxil edilsin.
        /// Rəqəm formatı dəyişmir — onluq ayırıcı kimi '.' işləməyə davam edir.
        /// </summary>
        private static void ConfigureCulture()
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
            culture.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
            culture.DateTimeFormat.DateSeparator = ".";

            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        private static void ConfigureSerilog()
        {
            var logDirectory = Path.Combine(
                Cas0201.Kok.Qovluq,
                "EnterpriseAeroStudio",
                "Logs");
            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(
                    Path.Combine(logDirectory, "avtopark-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        private static ServiceProvider BuildServices()
        {
            var services = new ServiceCollection();

            services.AddLogging(builder => builder.AddSerilog(Log.Logger, dispose: false));

            // ✅ VAHİD MƏNBƏ ✓✓✓: baza yolu YALNIZ `Cas0201.Kok`-dan götürülür ✓
            //  (USB bərpa/birləşdirmə də EYNİ yolu işlədir ✓ → məlumat dərhal görünür ✓✓✓)
            var dataDirectory = Cas0201.Kok.DataQovlugu;
            Directory.CreateDirectory(dataDirectory);

            // 🚀 PERFORMANS: bağlantı hovuzu (pooling) + SQLite PRAGMA sazlaması ✓
            //  (WAL · 64 MB keş · 256 MB mmap ✓) → böyük bazada proqram DONMUR ✓✓✓
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(dataDirectory, "avtopark.db"),
                Pooling = true,
                DefaultTimeout = 30
            }.ToString();

            services.AddDbContext<AppDbContext>(
                options => options
                    .UseSqlite(connectionString)
                    .AddInterceptors(new SqlitePragmaInterceptor()),
                ServiceLifetime.Transient);

            // ---- Repositories ----
            services.AddTransient(typeof(IRepository<>), typeof(Repository<>));
            services.AddTransient<ICarRepository, CarRepository>();
            services.AddTransient<IExpenseRepository, ExpenseRepository>();
            services.AddTransient<ICreditRepository, CreditRepository>();
            services.AddTransient<ISaleRepository, SaleRepository>();
            services.AddTransient<IPartnerShareRepository, PartnerShareRepository>();
            services.AddTransient<IPartnerRepository, PartnerRepository>();
            services.AddTransient<IPartnerPaymentRepository, PartnerPaymentRepository>();
        services.AddTransient<IOdenisMohletRepository, OdenisMohletRepository>();
        services.AddTransient<IKassaHereketRepository, KassaHereketRepository>();

            // ---- Services ----
            services.AddTransient<ICarService, CarService>();
            services.AddTransient<IExpenseService, ExpenseService>();
            services.AddTransient<IExpenseCatalogService, ExpenseCatalogService>();

            // 📜 Skript (JSON) idxalı — avtomobil · xərc · qeyd ✓✓✓
            services.AddTransient<IScriptImportService, ScriptImportService>();
            services.AddTransient<IMediaService, MediaService>();
            services.AddTransient<ITrashService, TrashService>();
            services.AddTransient<ICreditService, CreditService>();
            services.AddTransient<ISaleService, SaleService>();
            services.AddTransient<IPartnerService, PartnerService>();
        services.AddTransient<IMohletService, MohletService>();
        services.AddSingleton<IKassaService, KassaService>();

            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<IReportService, ReportService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddTransient<DbInitializer>();

            // ---- Dəyişiklik izləyicisi (masaüstü ↔ veb CANLI sinxronizasiya) ----
            //  Veb server EYNİ avtopark.db faylını işlədir. Bu izləyici faylı
            //  nəzarətdə saxlayır və hər dəyişiklikdə MainViewModel-i xəbərdar
            //  edir ki, bütün tablar dərhal yenilənsin.
            services.AddSingleton<IDataChangeWatcher>(_ =>
            {
                var watcher = new DataChangeWatcher(Path.Combine(dataDirectory, "avtopark.db"));
                watcher.Start();
                return watcher;
            });

            // ---- Veb server (masaüstü ↔ veb inteqrasiyası) ----
            //  Veb server ayrı proses kimi işə salınır və tətbiq bağlananda dayandırılır.
            services.AddSingleton<IWebServerService, WebServerService>();
            services.AddSingleton<WebViewModel>();

            // ---- ViewModels ----
            services.AddTransient<CarEditorViewModel>();
            services.AddTransient<ExpenseEditorViewModel>();
            services.AddTransient<ArchiveViewModel>();
            services.AddSingleton<CarParkViewModel>();
            services.AddSingleton<ExpensesViewModel>();
            services.AddSingleton<LoanCalculatorViewModel>();
            services.AddSingleton<SalesViewModel>();
            services.AddSingleton<SalesArchiveViewModel>();
            services.AddSingleton<CreditsViewModel>();
            services.AddSingleton<CreditTransactionsViewModel>();
            services.AddSingleton<FinanceViewModel>();
        services.AddSingleton<KassaViewModel>();
            services.AddSingleton<BildirisViewModel>();
            services.AddSingleton<TevimViewModel>();
            services.AddSingleton<PartnersViewModel>();

            // 📜 «Skript İdxalı» tabı ✓✓✓
            services.AddSingleton<ScriptImportViewModel>();

            services.AddSingleton<MainViewModel>();

            // ---- Views ----
            services.AddSingleton<MainWindow>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 🚫 <b>İkinci nüsxənin açılmasının qarşısını alır</b> ✓✓✓
        /// <para>
        /// <c>Local\</c> → eyni istifadəçi sessiyası üçün ✓ (server/çox-istifadəçi ssenarisi ✗)
        /// </para>
        /// </summary>
        private static bool TekNusxeTut()
        {
            try
            {
                _tekNusxeKilidi = new Mutex(true, @"Local\021Cars_TekNusxe_v6", out var sahib);

                return sahib;   // ✅ ilk nüsxə ✓ / ✗ ikinci nüsxə ✗
            }
            catch
            {
                return true;    // 🛡️ kilid qurulmasa ✗ → proqram yenə açılır ✓ (çökmə YOX ✗)
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // ☁️ Körpünü TƏMİZ dayandır ✓✓✓ (yarımçıq yedək yazısı qalmasın ✗)
            try { Kopru?.Dayandir(); } catch { }

            _logger?.LogInformation("Tətbiq bağlanır.");
            _services?.Dispose();
            Log.CloseAndFlush();

            // 🚫 Tək nüsxə kilidini burax ✓
            try { _tekNusxeKilidi?.ReleaseMutex(); } catch { }
            try { _tekNusxeKilidi?.Dispose(); } catch { }

            base.OnExit(e);
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "UI axınında tutulmamış xəta.");

            // 🚨 ÇÖKMƏ FAYLI ✓✓✓
            try
            {
                var q = System.IO.Path.Combine(Cas0201.Kok.Qovluq, "Logs");
                System.IO.Directory.CreateDirectory(q);
                System.IO.File.AppendAllText(System.IO.Path.Combine(q, "CRASH_UI.txt"),
                    "═══════════════════════════════════════" + Environment.NewLine +
                    $"🚨 UI ÇÖKMƏ : {DateTime.Now:yyyy-MM-dd HH:mm:ss}" + Environment.NewLine +
                    $"💥 TİP     : {e.Exception.GetType().FullName}" + Environment.NewLine +
                    $"📝 MESAJ   : {e.Exception.Message}" + Environment.NewLine +
                    $"🧵 İZ      : {e.Exception.StackTrace}" + Environment.NewLine + Environment.NewLine);
            }
            catch { }
            MessageBox.Show(
                "Gözlənilməz xəta baş verdi:\n" + e.Exception.Message,
                "Xəta",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            _logger?.LogCritical(exception, "Tətbiq səviyyəsində tutulmamış kritik xəta.");

            // ================================================================
            //  🚨 ÇÖKMƏ FAYLI ✓✓✓ — proqram bağlansa belə xəta QALIR ✓
            //  (bu fayl olmadan çökmənin səbəbini tapmaq mümkün olmur ✗)
            // ================================================================
            try
            {
                var qovluq = Path.Combine(Cas0201.Kok.Qovluq, "Logs");
                Directory.CreateDirectory(qovluq);

                File.AppendAllText(
                    Path.Combine(qovluq, "CRASH.txt"),
                    "═══════════════════════════════════════════════════════" + Environment.NewLine +
                    $"🚨 ÇÖKMƏ     : {DateTime.Now:yyyy-MM-dd HH:mm:ss}" + Environment.NewLine +
                    $"💥 TİP       : {exception?.GetType().FullName}" + Environment.NewLine +
                    $"📝 MESAJ     : {exception?.Message}" + Environment.NewLine +
                    $"🧵 İZ        : {exception?.StackTrace}" + Environment.NewLine +
                    $"🔗 DAXİLİ    : {exception?.InnerException?.Message}" + Environment.NewLine +
                    Environment.NewLine);
            }
            catch { }

            try
            {
                if (System.Windows.Application.Current is not null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            MessageBox.Show(
                                "🚨 Gözlənilməz xəta ✗\n\n" +
                                (exception?.Message ?? "naməlum") + "\n\n" +
                                "📂 Ətraflı: " + Cas0201.Kok.Qovluq + "\\Logs\\CRASH.txt",
                                "021Cars — Xəta",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        catch { }
                    });
                }
            }
            catch { }

            Log.CloseAndFlush();
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "Gözlənilməmiş asinxron tapşırıq xətası.");
            e.SetObserved();
        }
    }
}
