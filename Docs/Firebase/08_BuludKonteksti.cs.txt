// ============================================================================
//  ☁️ 021Cars — BULUD KONTEKSTİ (08)  ★ TƏTBİQİN GİRİŞ NÖQTƏSİ ★
// ----------------------------------------------------------------------------
//  ✅ BÜTÜN servislər BİR yerdə qurulur ✓ (SQL YOX ✗ — yalnız Firebase ✓)
//  ✅ İnternet qopub → işləməyə DAVAM EDİR ✓ (offline növbə ✓)
//  ✅ Fləşkart taxılmasa → ÇÖKMÜR ✗ (yalnız sənəd əməliyyatları məhdud ✓)
//  ✅ Real-time izləmə hər kolleksiya üçün arxa fonda ✓
//  ✅ Tətbiq: App.xaml.cs-də BİR sətir:  Bulud = new BuludKonteksti(); ✓
// ============================================================================

using System;
using System.Threading;
using System.Threading.Tasks;
using Cas0201.Firebase.Modeller;

namespace Cas0201.Firebase
{
    /// <summary>☁️ <b>021Cars BULUD KONTEKSTİ</b> ✓✓✓</summary>
    public sealed class BuludKonteksti : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        /// <summary>⚙️ Konfiqurasiya ✓</summary>
        public FirebaseOptions Options { get; }

        /// <summary>🌐 REST klienti ✓</summary>
        public FirebaseRestClient Klient { get; }

        /// <summary>💾 Fləşkart aşkarlayıcı ✓ (<c>021cars_drive.lock</c> ✓)</summary>
        public UsbDriveDetector Usb { get; }

        // --------------------------------------------------------------------
        //  🗃️ KOLLEKSİYALAR — SQL CƏDVƏLLƏRİNİN ƏVƏZİ ✓✓✓
        // --------------------------------------------------------------------
        public FirebaseRepository<Avtomobil> Avtomobiller { get; }
        public FirebaseRepository<Musteri> Musteriler { get; }
        public FirebaseRepository<Xerc> Xercler { get; }
        public FirebaseRepository<Satis> Satislar { get; }
        public FirebaseRepository<Kredit> Kreditler { get; }
        public FirebaseRepository<KreditEmeliyyati> KreditEmeliyyatlari { get; }
        public FirebaseRepository<Terefdas> Terefdaslar { get; }
        public FirebaseRepository<TerefdasPayi> TerefdasPaylari { get; }
        public FirebaseRepository<MediaFayl> MediaFayllar { get; }
        public FirebaseRepository<Istifadeci> Istifadeciler { get; }
        public FirebaseRepository<Ayar> Ayarlar { get; }
        public FirebaseRepository<ZibilQeydi> Zibil { get; }

        /// <summary>📎 Media/sənəd xidməti ✓ (fayl → USB ✓ · metadata → bulud ✓)</summary>
        public MediaXidmeti Media { get; }

        /// <summary>📊 Vəziyyət ✓ (UI üçün ✓)</summary>
        public bool Onlayn => Klient.Onlayn;

        /// <summary>📋 Offline növbəsindəki əməliyyat sayı ✓</summary>
        public int Növbədə =>
            Avtomobiller.NövbəSayı + Musteriler.NövbəSayı + Xercler.NövbəSayı +
            Satislar.NövbəSayı + Kreditler.NövbəSayı + KreditEmeliyyatlari.NövbəSayı +
            Terefdaslar.NövbəSayı + TerefdasPaylari.NövbəSayı + MediaFayllar.NövbəSayı;

        /// <summary>
        /// 🏗️ Kontekst qurur ✓
        /// <param name="token">
        /// 🔐 Firebase database secret ✓ — verilməzsə <c>CAS_FIREBASE_TOKEN</c> oxunur ✓
        /// </param>
        /// </summary>
        public BuludKonteksti(string? token = null)
        {
            // ⚙️ ① KONFİQURASİYA ✓
            // 📜 Loq faylı → %LOCALAPPDATA%\EnterpriseAeroStudio\Logs\app_errors.log ✓
            //    (yazmaq mümkün olmayan qovluq ✗ → avtomatik fallback ✓)
            AppLogger.QovluqTeminEt();

            Options = new FirebaseOptions
            {
                AuthToken = token
                    ?? Environment.GetEnvironmentVariable("CAS_FIREBASE_TOKEN")
                    ?? Environment.GetEnvironmentVariable("FIREBASE_TOKEN"),
                DeviceId = Environment.MachineName
            };

            Options.ClientiQur();
            Klient = new FirebaseRestClient(Options);

            // 💾 ② FLƏŞKART ✓ (yoxdursa çökmür ✗ — vəziyyət «Taxilmayib» olur ✓)
            Usb = new UsbDriveDetector();
            Usb.SkanEt();

            // 🗃️ ③ KOLLEKSİYALAR ✓
            Avtomobiller = new FirebaseRepository<Avtomobil>(Klient, Kolleksiyalar.Cars);
            Musteriler = new FirebaseRepository<Musteri>(Klient, Kolleksiyalar.Customers);
            Xercler = new FirebaseRepository<Xerc>(Klient, Kolleksiyalar.Expenses);
            Satislar = new FirebaseRepository<Satis>(Klient, Kolleksiyalar.Sales);
            Kreditler = new FirebaseRepository<Kredit>(Klient, Kolleksiyalar.Credits);
            KreditEmeliyyatlari = new FirebaseRepository<KreditEmeliyyati>(Klient, Kolleksiyalar.CreditTransactions);
            Terefdaslar = new FirebaseRepository<Terefdas>(Klient, Kolleksiyalar.Partners);
            TerefdasPaylari = new FirebaseRepository<TerefdasPayi>(Klient, Kolleksiyalar.PartnerShares);
            MediaFayllar = new FirebaseRepository<MediaFayl>(Klient, Kolleksiyalar.Media);
            Istifadeciler = new FirebaseRepository<Istifadeci>(Klient, Kolleksiyalar.Users);
            Ayarlar = new FirebaseRepository<Ayar>(Klient, Kolleksiyalar.Settings);
            Zibil = new FirebaseRepository<ZibilQeydi>(Klient, Kolleksiyalar.Trash);

            // 📎 ④ MEDIA XİDMƏTİ ✓
            Media = new MediaXidmeti(Usb, MediaFayllar, Options);

            AppLogger.Melumat(
                $"☁️ Bulud konteksti hazırdır ✓ — {Options.BaseUrl}/{Options.RootPath} ✓ " +
                $"· 💾 {(Usb.Hazir ? "fləşkart HAZIR ✓ (" + Usb.KokYol + ")" : "fləşkart YOXDUR ✗")}");

            // 📜 Loq yolunu da loqa yaz ✓ (istifadəçi asan tapsın ✓)
            AppLogger.Melumat($"📜 Loq faylı: {AppLogger.FaylYolu} ✓");
        }

        // --------------------------------------------------------------------
        //  🚀 BAŞLATMA — İLK YÜKLƏMƏ + REAL-TIME + FLƏŞKART İZLƏMƏSİ ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>
        /// 🚀 <b>SİSTEMİ İŞƏ SALIR</b> ✓✓✓
        /// <para>
        /// ① 📥 Bütün kolleksiyalar buluddan oxunur ✓ (paralel ✓ — sürətli ✓)<br/>
        /// ② 📡 Real-time izləmə arxa fonda başlayır ✓ (hər kolleksiya üçün ✓)<br/>
        /// ③ 💾 Fləşkart izləməsi ✓ (5 saniyə ✓ — taxılanda avtomatik tapılır ✓)
        /// </para>
        /// ⚠ İnternet yoxdursa: <b>XƏTA VERMİR</b> ✗ — boş siyahılarla işləyir ✓✓✓
        /// </summary>
        public async Task BaslatAsync()
        {
            try
            {
                AppLogger.Melumat("🚀 021Cars bulud sinxronizasiyası başladı ✓");

                // ① 📥 İLK YÜKLƏMƏ (paralel ✓)
                await IlkYuklemeAsync().ConfigureAwait(false);

                // ② 📡 REAL-TIME + 💾 FLƏŞKART (arxa fon ✓ — UI donmur ✓)
                _ = Task.Run(() => CanliİzlemeDovruAsync(_cts.Token));
                _ = Task.Run(() => Usb.İzleAsync(5, _cts.Token));
            }
            catch (Exception ex)
            {
                // 🛡️ Başlatma xətası proqramı DAYANDIRMIR ✗✓✓
                AppLogger.Xeta(ex, "BaslatAsync");
            }
        }

        /// <summary>📥 Bütün kolleksiyaları <b>paralel</b> oxuyur ✓ (offline → boş ✓)</summary>
        public async Task IlkYuklemeAsync()
        {
            try
            {
                var tapşırıqlar = new Task[]
                {
                    Avtomobiller.HamisiniAlAsync(ct: _cts.Token),
                    Musteriler.HamisiniAlAsync(ct: _cts.Token),
                    Xercler.HamisiniAlAsync(ct: _cts.Token),
                    Satislar.HamisiniAlAsync(ct: _cts.Token),
                    Kreditler.HamisiniAlAsync(ct: _cts.Token),
                    KreditEmeliyyatlari.HamisiniAlAsync(ct: _cts.Token),
                    Terefdaslar.HamisiniAlAsync(ct: _cts.Token),
                    TerefdasPaylari.HamisiniAlAsync(ct: _cts.Token),
                    MediaFayllar.HamisiniAlAsync(ct: _cts.Token),
                    Istifadeciler.HamisiniAlAsync(ct: _cts.Token),
                    Ayarlar.HamisiniAlAsync(ct: _cts.Token)
                };

                await Task.WhenAll(tapşırıqlar).ConfigureAwait(false);

                AppLogger.SonSinxron = DateTime.Now;
                AppLogger.Onlayn = Klient.Onlayn;

                AppLogger.Melumat(
                    $"📥 İlk yükləmə tamamlandı ✓ — 🚗 {Avtomobiller.KesdenAl().Count} avtomobil ✓ " +
                    $"· 👤 {Musteriler.KesdenAl().Count} müştəri ✓ " +
                    $"· 🤝 {Satislar.KesdenAl().Count} satış ✓ " +
                    $"· 🏦 {Kreditler.KesdenAl().Count} kredit ✓");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "ilk yükləmə");
            }
        }

        /// <summary>🔁 Bütün kolleksiyaları əl ilə yeniləyir ✓ (UI «Yenilə» düyməsi ✓)</summary>
        public async Task HamisiniYenileAsync()
        {
            try
            {
                await IlkYuklemeAsync().ConfigureAwait(false);

                // 📤 Növbədə qalanları da göndər ✓
                await Task.WhenAll(
                    Avtomobiller.NövbəniBoşaltAsync(_cts.Token),
                    Musteriler.NövbəniBoşaltAsync(_cts.Token),
                    Xercler.NövbəniBoşaltAsync(_cts.Token),
                    Satislar.NövbəniBoşaltAsync(_cts.Token),
                    Kreditler.NövbəniBoşaltAsync(_cts.Token),
                    KreditEmeliyyatlari.NövbəniBoşaltAsync(_cts.Token)).ConfigureAwait(false);

                AppLogger.SonSinxron = DateTime.Now;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "əl ilə yeniləmə");
            }
        }

        /// <summary>📡 Bütün kolleksiyalar üçün real-time dövrə ✓ (arxa fon ✓)</summary>
        private async Task CanliİzlemeDovruAsync(CancellationToken ct)
        {
            try
            {
                await Task.WhenAll(
                    Avtomobiller.CanliBaslaAsync(ct),
                    Musteriler.CanliBaslaAsync(ct),
                    Xercler.CanliBaslaAsync(ct),
                    Satislar.CanliBaslaAsync(ct),
                    Kreditler.CanliBaslaAsync(ct),
                    KreditEmeliyyatlari.CanliBaslaAsync(ct),
                    Terefdaslar.CanliBaslaAsync(ct),
                    TerefdasPaylari.CanliBaslaAsync(ct),
                    MediaFayllar.CanliBaslaAsync(ct)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "real-time dövrə");
            }
        }

        // --------------------------------------------------------------------
        //  🛑 DAYANDIRMA ✓
        // --------------------------------------------------------------------

        /// <summary>🛑 Sinxronizasiyanı dayandırır ✓ (tətbiq bağlananda ✓)</summary>
        public void Dayandir()
        {
            try
            {
                if (!_cts.IsCancellationRequested) _cts.Cancel();
                AppLogger.Melumat("🛑 Bulud sinxronizasiyası dayandırıldı ✓");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "Dayandir");
            }
        }

        /// <summary>🧹 Resursları azad edir ✓ (exception ATILMIR ✗)</summary>
        public void Dispose()
        {
            try
            {
                Dayandir();
                _cts.Dispose();
                Options.Client.Dispose();
            }
            catch { }
        }

    }
}
