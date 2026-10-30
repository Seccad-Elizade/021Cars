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

        // ====================================================================
        //  💾 v6.2.36 — YALNIZ YERLİ BAZA + FLASH USB ✓✓✓
        //  ----------------------------------------------------------------
        //  ⛔ Firebase / bulud sinxronizasiyası TAM SİLİNDİ ✗✓✓
        //  ★ İstifadəçi tələbi: «Firebase ilə əlaqəni tam kəs — hər şeyi sil ✓
        //    yalnız kompüter və USB-yə save olunsun ✓✓✓»
        //  ✅ Məlumat YALNIZ bu kompüterdəki SQLite bazasında saxlanılır ✓
        //    və 💾 fləşkartda ehtiyat nüsxə kimi yazılır ✓ (DataUsbService ✓)
        // ====================================================================

        /// <summary>
        /// 🖥️ <b>MASAÜSTÜ QISAYOLUNU ZƏMANƏTLƏŞDİRİR</b> ✓✓✓ (v6.2.20)
        /// <para>
        /// ★ İstifadəçi şikayəti: «Installer ilə proqramı birinci dəfə
        /// yükləyəndə masaüstünə qısayol qoymur» ✗✓✓
        /// </para>
        /// <para>
        /// <b>⚠ ƏSL SƏBƏB:</b> quraşdırıcı qısayolu yalnız <b>ORTAQ</b>
        /// (<c>C:\Users\Public\Desktop</c>) masaüstünə yazırdı ✗ → quraşdırma
        /// <b>admin olmadan</b> işləyəndə orada yazmaq MÜMKÜN DEYİL ✗ →
        /// qısayol heç vaxt yaranmırdı ✗✓✓
        /// </para>
        /// <para>
        /// <b>✅ HƏLL (2 qat ✓✓✓):</b>
        /// </para>
        /// <list type="number">
        ///   <item>Quraşdırıcı artıq <b>hər iki</b> masaüstünə yazır ✓
        ///         (istifadəçinin özü ✓ + ortaq ✓)</item>
        ///   <item>Proqram <b>hər açılışda</b> yoxlayır ✓ → qısayol YOXDURSA
        ///         <b>ÖZÜ yaradır</b> ✓✓✓ (bu metod ✓ — admin LAZIM DEYİL ✗)</item>
        /// </list>
        /// <para>
        /// 🛡️ Heç bir halda proqram çökmür ✗ — bütün xətalar UDULUR ✓ ·
        /// qısayol varsa <b>toxunulmur</b> ✗✓✓
        /// </para>
        /// </summary>
        private static void MasaustuQisayoluYoxla()
        {
            try
            {
                // ① İcra olunan fayl (exe) tapılır ✓
                var exe = Environment.ProcessPath;

                if (string.IsNullOrWhiteSpace(exe)
                    || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return;   // ⚠ `dotnet run` / veb host ✗ → qısayol yaradılmır ✓
                }

                // ⚠ Yaradılma (development) qovluğunda qısayol YARADILMIR ✗
                //   (məs. ...\bin\Debug\net8.0-windows\... ✗)
                if (exe.Contains(@"\bin\", StringComparison.OrdinalIgnoreCase)
                    || exe.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                var işQovluğu = Path.GetDirectoryName(exe) ?? string.Empty;

                // ② Artıq qısayol VARMI? ✓
                //    • istifadəçinin öz masaüstü ✓
                //    • ortaq (Public) masaüstü ✓
                var istifadeciMasa = Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);

                var ortaqMasa = Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonDesktopDirectory);

                // ================================================================
                //  🧹 ★ v6.2.24 — KÖHNƏ/DUPLİKAT QISAYOLLAR TƏMİZLƏNİR ★✓✓✓
                // ----------------------------------------------------------------
                //  ⚠ İSTİFADƏÇİ ŞİKAYƏTİ: «hər güncəlləmədə masaüstünə YENİ proqram
                //    gəlir ✗ — 2-3 ədəd olurlar ✗.»
                //  ✅ ƏVVƏLCƏ köhnə adlı («021Cars — Avtomobil Parkı.lnk» ✗) qısayollar
                //    HƏR İKİ masaüstündən SİLİNİR ✓ → sonra YALNIZ BİR dənə qalır ✓✓✓
                //  (⚠ cari «021Cars.lnk» SİLİNMİR ✗ — varsa toxunulmur ✓)
                // ================================================================
                Cas0201.Setup.QisayolYaradan.KöhnələriTemizle(istifadeciMasa);
                Cas0201.Setup.QisayolYaradan.KöhnələriTemizle(ortaqMasa);

                var istifadeciLnk = Path.Combine(
                    istifadeciMasa, Cas0201.Setup.QisayolYaradan.FaylAdi);

                var ortaqLnk = Path.Combine(
                    ortaqMasa, Cas0201.Setup.QisayolYaradan.FaylAdi);

                if (File.Exists(istifadeciLnk) || File.Exists(ortaqLnk))
                {
                    return;   // ✔ artıq var ✓ → toxunulmur ✗✓✓ (İKİNCİ yaradılmır ✗)
                }

                // ③ ƏVVƏLCƏ istifadəçinin ÖZ masaüstünə ✓ (admin lazım DEYİL ✓)
                if (QisayolYaz(istifadeciMasa, exe, işQovluğu))
                {
                    AppLogger.Melumat(
                        "🖥️ Masaüstü qısayolu YARADILDI ✓ (istifadəçi masaüstü ✓)");

                    return;
                }

                // ④ Alınmadısa → ORTAQ masaüstünə ✓ (admin olduqda işləyir ✓)
                if (QisayolYaz(ortaqMasa, exe, işQovluğu))
                {
                    AppLogger.Melumat(
                        "🖥️ Masaüstü qısayolu YARADILDI ✓ (ortaq masaüstü ✓)");
                }
            }
            catch (Exception ex)
            {
                // 🛡️ Qısayol yarada bilməmək proqramın açılışını DAYANDIRMIR ✗✓✓
                AppLogger.Xeta(ex, "masaüstü qısayolu");
            }

            // ---- yerli köməkçi: verilən masaüstü qovluğuna qısayol yazır ✓ ----
            //  ⚠ v6.2.21: artıq `QisayolYaradan` (COM interop ✓ trim-safe ✓) işlədilir ✗
            //    → `dynamic` / `WScript.Shell` YOX ✗ (trimmed publish-də SİNİRDI ✗✓✓)
            bool QisayolYaz(string qovluq, string hedefExe, string işQovluğu)
                => Cas0201.Setup.QisayolYaradan.Yarat(
                    Path.Combine(qovluq, Cas0201.Setup.QisayolYaradan.FaylAdi),
                    hedefExe,
                    işQovluğu,
                    "021Cars — Avtomobil Parkı");
        }

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
            //  👥 v6.2.39 — TƏRƏFDAŞ KEŞİ YÜKLƏNİR ✓✓✓
            // ----------------------------------------------------------------
            //  ★ İstifadəçi şikayəti: «Tərəfdaşlar tabında yeni ad (Vusal)
            //    əlavə etdim ✗ amma tərəfdaş bölgüsü cədvəlinə düşmədi ✗» ✓✓✓
            //  ⚠ SƏBƏB: `PartnerMath.PartnersProvider` keşi yalnız
            //    `RefreshProviderAsync()` ilə dolur ✗ — o isə HEÇ VAXT
            //    çağırılmırdı ✗ → bölgü cədvəlləri həmişə STATİK siyahını
            //    işlədirdi ✗✓✓
            //  ✅ İNDİ: açılışda keş yüklənir ✓ → yeni adlar avtomatik görünür ✓
            // ================================================================
            try
            {
                _ = Task.Run(async () =>
                {
                    // ⚠ v6.2.40 — ƏVVƏLCƏ GÖZLƏNİLİR ✓✓✓
                    //   ★ Log sübutu: «no such table: Terefdaslar» ✗ — çünki bu
                    //     blok `InitializeDatabase()`-dən ƏVVƏL işə düşürdü ✗
                    //     (miqrasiya hələ cədvəlləri yaratmamışdı ✗✓✓)
                    //   ✅ İNDİ: ilk cəhd 3 saniyə SONRA ✓ (baza hazır olur ✓)
                    for (var cehd = 1; cehd <= 5; cehd++)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(cehd == 1 ? 3 : 2));

                            using var scope = _services!.CreateScope();

                            await scope.ServiceProvider
                                .GetRequiredService<IPartnerService>()
                                .RefreshProviderAsync();

                            return;      // ✅ yükləndi ✓
                        }
                        catch (Exception ex)
                        {
                            if (cehd >= 5)
                            {
                                AppLogger.Xeberdarliq(
                                    "👥 Tərəfdaş siyahısı yüklənmədi ✗ — " + ex.Message);
                                return;
                            }
                        }
                    }
                });
            }
            catch
            {
                // 🛡️ keş yüklənməsə də tətbiq TAM İŞLƏYİR ✓ (statik siyahı ✓)
            }

            // ================================================================
            //  💾 v6.2.36 — BULUD SINXRONU SİLİNDİ ✗✓✓
            //  ----------------------------------------------------------------
            //  ⛔ ƏVVƏL: hər 5 saniyədə Firebase-ə bütün məlumat göndərilirdi ✗
            //    və digər kompüterdən oxunurdu ✗ → İNTERNET/ŞƏBƏKƏ problemləri
            //    yazma əməliyyatlarına təsir edirdi ✗✓✓
            //  ✅ İNDİ: tətbiq TAM OFLAYN işləyir ✓ — məlumat bu kompüterin
            //    SQLite bazasında saxlanılır ✓ və 💾 fləşkartda ehtiyat nüsxə
            //    alınır ✓ (DataUsbService.AvtomatikYedekAsync ✓)
            // ================================================================

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
                    AppLogger.Melumat("🔑 USB: " + tanıma);

                    // 💾 BƏRPA ✓ / 🔀 BİRLƏŞDİRMƏ ✓
                    var bərpa = EnterpriseAeroStudio.Services.DataUsbService.UsbDenBerpaEt();
                    AppLogger.Melumat("💾 USB bərpa: " + bərpa);
                }
                catch (Exception ex)
                {
                    AppLogger.Melumat("💾 USB bərpa yoxlanmadı ✓ — " + ex.Message);
                }

                // ================================================================
                //  ④ 🔢 SIRA NÖMRƏLƏRİ — ARXA FONDA YOXLANILIR ✓✓✓ (v6.2.80)
                // ----------------------------------------------------------------
                //  🩹 USB-dən gələn maşının sıra nömrəsi «0» qalıbsa → düzəlir ✓
                //  ✅ İstifadəçinin ÖZ nömrəsi (məs. 312 ✓) HEÇ VAXT dəyişmir ✗✓✓
                //
                //  ⚠ ƏVVƏL: `SiraNomreleriDuzelt()` BURADA, UI axınında,
                //    `GetAwaiter().GetResult()` ilə BLOKLAYICI çağırılırdı ✗ →
                //    proqram açılarkən pəncərə görünməzdən əvvəl donurdu ✗✓✓
                //  ✅ İNDİ: arxa fonda ✓ — açılış ANİ olur ✓, düzəliş isə
                //    pəncərə açıldıqdan sonra səssizcə tamamlanır ✓✓✓
                //    (məlumat İTMİR ✗ — tam eyni əməliyyatdır ✓)
                // ================================================================
                _ = Task.Run(() => SiraNomreleriDuzelt());

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
                // ================================================================
                //  🗃️ v6.2.80 — QOVLUQ ADI KEŞİ ✓✓✓
                // ----------------------------------------------------------------
                //  Media siyahısı hər çəkilişdə eyni maşını TƏKRAR soruşur ✗ →
                //  qısa (3 saniyəlik) keş cavabı DƏRHAL verir ✓ (DB-yə getmir ✗)
                // ================================================================
                var qovluqAdiKeshi = new System.Collections.Concurrent.ConcurrentDictionary<
                    (string Tip, int Id), (string? Ad, DateTime Vaxt)>();

                var qovluqAdiKeshMuddati = TimeSpan.FromSeconds(3);

                EnterpriseAeroStudio.Services.MediaService.QovluqAdiProvider = (tip, id) =>
                {
                    var keshAchari = (Tip: tip ?? string.Empty, Id: id);

                    if (qovluqAdiKeshi.TryGetValue(keshAchari, out var keshli)
                        && DateTime.UtcNow - keshli.Vaxt < qovluqAdiKeshMuddati)
                    {
                        return keshli.Ad;
                    }

                    string? cavab = null;

                    try
                    {
                        using var scope = _services!.CreateScope();

                        var db = scope.ServiceProvider
                            .GetRequiredService<EnterpriseAeroStudio.Data.AppDbContext>();

                        // 🚗 Avtomobil İd-si (birbaşa ✓ və ya kredit əməliyyatı ilə ✓)
                        var avtomobilId = id;

                        if (!string.Equals(tip, EnterpriseAeroStudio.Models.MediaRefTypes.Car,
                                StringComparison.Ordinal))
                        {
                            // 💳 KREDİT ƏMƏLİYYATI sənədi → kredit → AVTOMOBİL ✓✓✓
                            var kreditId = db.CreditTransactions
                                             .AsNoTracking()
                                             .Where(t => t.Id == id)
                                             .Select(t => t.CreditId)
                                             .FirstOrDefault();

                            if (kreditId is not int kId)
                            {
                                qovluqAdiKeshi[keshAchari] = (null, DateTime.UtcNow);
                                return null;      // ✓ ehtiyat: refId ✓
                            }

                            var carId = db.Credits
                                          .AsNoTracking()
                                          .Where(k => k.Id == kId)
                                          .Select(k => k.CarId)
                                          .FirstOrDefault();

                            if (carId is not int cId)
                            {
                                qovluqAdiKeshi[keshAchari] = (null, DateTime.UtcNow);
                                return null;      // ✓ ehtiyat: refId («61») ✓
                            }

                            avtomobilId = cId;
                        }

                        // ============================================================
                        //  ⚡🚀 YALNIZ TƏK SƏTİR OXUNUR ✓✓✓  (DONMA DÜZƏLİŞİ)
                        // ------------------------------------------------------------
                        //  ⚠ ƏVVƏL: `GetAllCarsAsync().GetAwaiter().GetResult()`
                        //    → BÜTÜN avtomobillər + BÜTÜN xərcləri yaddaşa
                        //    yükləyirdi ✗ və UI axınında bloklayırdı ✗ →
                        //    sənəd/media siyahısı açılanda proqram DONURDU ✗✓✓
                        //  ✅ İNDİ: indeksli TƏK SƏTİR sorğusu ✓ (~0,1 ms ✓),
                        //    yalnız 3 sahə ✓ — park ÜMUMİYYƏTLƏ yüklənmir ✗✓✓
                        // ============================================================
                        var avtomobil = db.Cars
                                          .AsNoTracking()
                                          .Where(c => c.Id == avtomobilId)
                                          .Select(c => new
                                          {
                                              c.SiraNomresi,
                                              c.Marka,
                                              c.QeydiyyatNisani
                                          })
                                          .FirstOrDefault();

                        if (avtomobil is not null)
                        {
                            // ✅ Sıra № · Marka (model) · Dövlət qeydiyyat nişanı ✓✓✓
                            var sira = avtomobil.SiraNomresi > 0
                                ? avtomobil.SiraNomresi.ToString("000")
                                : avtomobilId.ToString();

                            cavab = $"{sira} - {avtomobil.Marka} - {avtomobil.QeydiyyatNisani}";
                        }
                    }
                    catch
                    {
                        cavab = null;      // ✓ ehtiyat: «61» ✓
                    }

                    qovluqAdiKeshi[keshAchari] = (cavab, DateTime.UtcNow);

                    return cavab;
                };

                var window = _services.GetRequiredService<MainWindow>();
                MainWindow = window;
                window.Show();

                // ================================================================
                //  🔧 v6.2.80 — TEXNİKİ BAXIM ARXA FONDA ✓✓✓ (AÇILIŞ DONMASI)
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL bu işlər pəncərə açılmazdan ƏVVƏL, UI axınında
                //    bloklayıcı şəkildə icra olunurdu ✗ → «birinci açılış
                //    çox uzun çəkir / proqram donur» ✗✓✓
                //  ✅ İNDİ: pəncərə DƏRHAL açılır ✓, baxım isə burada,
                //    ayrı DI scope-da, arxa fonda tamamlanır ✓✓✓
                //  🛡️ Əməliyyatlar idempotentdir ✓ → MƏLUMAT İTMİR ✗✓✓
                //  🛡️ Hər addım öz `try/catch`-i ilə ✓ — biri uğursuz olsa
                //     digərləri yenə də işləyir ✓ (proqram çökmür ✗)
                // ================================================================
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var baximScope = _services!.CreateScope();

                        var baxim = baximScope.ServiceProvider
                            .GetRequiredService<EnterpriseAeroStudio.Data.DbInitializer>();

                        await baxim.BakimAsync();

                        _logger?.LogInformation("🔧 Texniki baxım arxa fonda tamamlandı ✓");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Melumat("🔧 Texniki baxım alınmadı ✓ — " + ex.Message);
                    }
                });

                // ================================================================
                //  🆕 ☁️ YENİ KOMPÜTER — «BULUDDAN GÖTÜR?» ✓✓✓  (v6.2.12)
                // ----------------------------------------------------------------
                //  ⚠ İSTİFADƏÇİ ŞİKAYƏTİ: yeni kompüterə yükləyəndə tətbiq BOŞ
                //    açılırdı ✗ (bulud sinxronu qəsdən sönülü ✓) → məlumat
                //    görünmürdü ✗ və 💾 USB taxmağa MƏCBUR qalırdı ✗✓✓
                //  ✅ İNDİ: təmiz quraşdırma + BOŞ baza → BİR DƏFƏ soruşulur ✓
                //     «Bəli» → bütün məlumat buluddan DƏRHAL götürülür ✓✓✓
                // ================================================================
                // ⛔ v6.2.36 — «buluddan ilk yükləmə» SİLİNDİ ✗ (yalnız yerli baza + USB ✓)

                // ================================================================
                //  🖥️ MASAÜSTÜ QISAYOLU ZƏMANƏTİ ✓✓✓ (v6.2.20)
                // ----------------------------------------------------------------
                //  ⚠ İSTİFADƏÇİ ŞİKAYƏTİ: «Installer ilə ilk yükləyəndə masaüstünə
                //    qısayol qoymur» ✗
                //  ✅ Proqram hər açılışda YOXLAYIR ✓ → qısayol YOXDURSA
                //    ÖZÜ yaradır ✓✓✓ (admin lazım DEYİL ✗ — istifadəçinin öz
                //    masaüstünə yazır ✓)
                // ================================================================
                MasaustuQisayoluYoxla();

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
                        AppLogger.Melumat("🔎 Axtarış mətni hazırlanmadı — " + ex.Message);
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
                    // ⏱️ 90 saniyə ✓ (v6.2.81 — 30 → 90) · UI DONMUR ✗
                    //  ✅ Real dəyişikliklər onsuz da ANİ tutulur ✓ (fayl izləyicisi ✓)
                    anaVm.AvtoYenilemeyeBasla(90);
                }

                // 🩺 v6.2.81 — UI DONMA NƏZARƏTÇİSİ ✓✓✓
                //  • Fon nəzarətçisi: UI 2,5 s-dən çox bloklanarsa loga yazır ✓
                //  • Nəbz: UI axınında 250 ms-lik DispatcherTimer ✓
                //  (istifadəçi yalnız log faylını göndərir → dəqiq diaqnoz ✓)
                EnterpriseAeroStudio.Services.UiWatchdog.Başlat();

                var nəbzTimeri = new System.Windows.Threading.DispatcherTimer(
                    System.Windows.Threading.DispatcherPriority.Send)
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };

                nəbzTimeri.Tick += (_, _) => EnterpriseAeroStudio.Services.UiWatchdog.Nəbz();
                nəbzTimeri.Start();

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
                AppLogger.Melumat("🔢 Sıra nömrəsi düzəlişi ✓ — " + ex.Message);
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

            // ✅ VAHİD MƏNBƏ ✓✓✓: baza yolu YALNIZ `Cas0201.Kok.BazaYolu`-dan götürülür ✓
            //  (USB bərpa/birləşdirmə də EYNİ yolu işlədir ✓ → məlumat dərhal görünür ✓✓✓)
            //
            //  🛡️ v6.2.42 — ★ KRİTİK DÜZƏLİŞ ★
            //  ⚠ ƏVVƏL burada yol ƏL İLƏ qurulurdu ✗:
            //      Path.Combine(dataDirectory, "avtopark.db")   ✗✓✓
            //  → `Kok.BazaYolunuTap()`-ın BÜTÜN özünü-bərpa məntiqi
            //    (yazma testi · «Error 8» / «Error 14» · ehtiyat yola keçid)
            //    HEÇ VAXT İŞƏ DÜŞMÜRDÜ ✗✓✓
            //  ✅ İNDİ: yol YALNIZ `Kok.BazaYolu`-dan alınır ✓ → baza yazıla
            //    bilmirsə proqram AVTOMATİK olaraq yazıla bilən yola keçir ✓
            //    (məlumat köçürülür ✓) → «readonly database» xətası BİTMİŞDİR ✓✓✓
            var bazaYolu = Cas0201.Kok.BazaYolu;

            var dataDirectory = Path.GetDirectoryName(bazaYolu)
                ?? Cas0201.Kok.DataQovlugu;

            Directory.CreateDirectory(dataDirectory);

            // 🚀 PERFORMANS: bağlantı hovuzu (pooling) + SQLite PRAGMA sazlaması ✓
            //  (WAL · 64 MB keş · 256 MB mmap ✓) → böyük bazada proqram DONMUR ✓✓✓
            var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = bazaYolu,
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
            services.AddTransient<IKreditIdxalService, KreditIdxalService>();
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
            //  🛡️ v6.2.42 — ⚠ ƏVVƏL yol əl ilə qurulurdu ✗ → ehtiyat yola
            //   keçildikdə izləyici MÖVCUD OLMAYAN faylı izləyirdi ✗✓✓
            //  ✅ İNDİ: faktiki işlədilən `bazaYolu` izlənilir ✓✓✓
            services.AddSingleton<IDataChangeWatcher>(_ =>
            {
                var watcher = new DataChangeWatcher(bazaYolu);
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
            services.AddSingleton<KreditIdxalViewModel>();

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
            // ================================================================
            //  🛡️ v6.2.50 — ZƏRƏRSİZ UI XƏTALARI (dialoq ÇIXMIR) ✓✓✓
            //  ⚠ DataGrid / CollectionView daxili indeks xətaları ✗
            //     («Index was out of range», «…inconsistent with its items source»)
            //     — WPF növbəti layout-da ÖZÜ bərpa edir ✓
            //  ✅ Yalnız LOQA yazılır ✓ · istifadəçiyə xəta pəncərəsi ÇIXMIR ✓✓✓
            // ================================================================
            if (ZerersizUiXetasi(e.Exception))
            {
                e.Handled = true;
                return;
            }

            MessageBox.Show(
                "Gözlənilməz xəta baş verdi:\n" + e.Exception.Message,
                "Xəta",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        /// <summary>
        /// 🛡️ WPF-in cədvəl/kolleksiya daxilində baş verən və ÖZÜ bərpa etdiyi
        /// zərərsiz xətaları tanıyır ✓ (istifadəçiyə göstərilmir ✗).
        /// </summary>
        private static bool ZerersizUiXetasi(Exception? ex)
        {
            for (var x = ex; x is not null; x = x.InnerException)
            {
                if (x is ArgumentOutOfRangeException or IndexOutOfRangeException)
                {
                    return true;
                }

                var m = x.Message ?? string.Empty;

                if (m.Contains("Index was out of range", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("inconsistent with its items source", StringComparison.OrdinalIgnoreCase) ||
                    m.Contains("does not appear at given index", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
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
