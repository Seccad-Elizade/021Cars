using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>Əsas pəncərənin kök ViewModel-i; bütün tab-ların VM-lərini birləşdirir.</summary>
    public sealed partial class MainViewModel : ObservableObject
    {
        public CarParkViewModel CarPark { get; }
        public ExpensesViewModel Expenses { get; }
        public LoanCalculatorViewModel Calculator { get; }
        public SalesViewModel Sales { get; }
        public SalesArchiveViewModel SalesArchive { get; }
        public CreditsViewModel Credits { get; }
        public CreditTransactionsViewModel CreditTransactions { get; }
        public FinanceViewModel Finance { get; }

        /// <summary>
        /// «💵 Kassa» tabının ViewModel-i — kassaya DAXİL OLAN ✓ və kassadan
        /// ÇIXAN ✗ bütün pulun vahid jurnalı ✓ (möhlətlər də buradan idarə olunur ✓✓✓)
        /// </summary>
        public KassaViewModel Kassa { get; }

        /// <summary>
        /// 🔔 «🔔 Bildirişlər» tabının ViewModel-i ✓ — «bu gün hansı maşının ödənişi
        /// var? nə vaxtdır?» ✓✓✓ (kredit taksitləri ✓ möhlətlər ✓ gecikmələr ✓)
        /// </summary>
        public BildirisViewModel Bildirisler { get; }

        /// <summary>
        /// 🗓️ «🗓️ Ödəniş Təqvimi» tabının ViewModel-i ✓ — «hansı ay hansı maşınlar
        /// pul verəcək» ✓✓✓ (ay-ay cədvəl ✓)
        /// </summary>
        public TevimViewModel Tevim { get; }

        /// <summary>«👥 Tərəfdaşlar» tabının ViewModel-i — tərəfdaş bölgüsü mərkəzi.</summary>
        public PartnersViewModel Partners { get; }

        /// <summary>«🌐 Veb Sayt» tabının ViewModel-i — veb serveri idarə edir.</summary>
        public WebViewModel Web { get; }

        /// <summary>
        /// «📜 Skript İdxalı» tabının ViewModel-i — JSON skriptindən
        /// avtomobil · xərc · qeyd idxalı ✓✓✓
        /// </summary>
        public ScriptImportViewModel Skript { get; }

        /// <summary>
        /// 🧾 «🏦 Kredit İdxalı» tabının ViewModel-i — toplu mətndən
        /// kredit · avtomobil · ödəniş · tərəfdaş bölgüsü idxalı ✓✓✓
        /// </summary>
        public KreditIdxalViewModel KreditIdxal { get; }

        // ====================================================================
        //  🔐 İSTİFADƏÇİ / ROL MƏLUMATI  (LOGIN SİSTEMİ) ✓✓✓
        // --------------------------------------------------------------------
        //   • 🔑 Seccad (Admin) → Veb Sayt ✓ · Tənzimləmələr ✓ · TAM GİRİŞ ✓
        //   • ⚙️ Asif            → Veb Sayt ✗ · Tənzimləmələr ✓  ← Sahildən fərqi ✓
        //   • 🚗 Sahil           → Veb Sayt ✗ · Tənzimləmələr ✗
        // ====================================================================

        /// <summary>👤 Hazırda daxil olmuş istifadəçi ✓</summary>
        public Services.Istifadeci? CariIstifadeci => Services.AuthService.Cari;

        /// <summary>🌐 Veb Sayt tabı görünürmü? ✗ (YALNIZ Admin ✓)</summary>
        public bool VebSaytGoruner => Services.AuthService.Cari?.VebSaytGoruner ?? false;

        /// <summary>⚙️ «Tənzimləmələr» tabı ✓ (Asif + Admin ✓ | Sahil ✗)</summary>
        public bool TenzimlemelerGoruner => Services.AuthService.Cari?.TenzimlemelerGoruner ?? false;

        /// <summary>👤 «Sahil  ·  🚗 İstifadəçi» kimi başlıq ✓</summary>
        public string IstifadeciMetni => CariIstifadeci is null
            ? "—"
            : $"{CariIstifadeci.Ad}  ·  {CariIstifadeci.RolMetni}";

        /// <summary>🛡️ Silmə icazəsi ✓ (Admin + Asif ✓ | Sahil ✗)</summary>
        public bool SilmeIcazesi => Services.AuthService.Cari?.SilmeIcazesi ?? false;

        /// <summary>📂 Proqram qovluğu ✓</summary>
        public string ProqramQovlugu => System.AppContext.BaseDirectory;

        /// <summary>📄 Loq qovluğu ✓</summary>
        public string LogQovlugu =>
            System.IO.Path.Combine(System.AppContext.BaseDirectory, "logs");

        /// <summary>📦 Versiya məlumatı ✓</summary>
        public string VersiyaMetni =>
            $"v{System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version}";

        // ====================================================================
        //  ⚙️ TƏNZİMLƏMƏLƏR FUNKSİYALARI ✓✓✓
        // ====================================================================

        /// <summary>📂 Proqram qovluğunu File Explorer-də açır ✓</summary>
        [RelayCommand]
        private void QovlugAc() => AcVeYaCix(ProqramQovlugu);

        /// <summary>📄 Loq qovluğunu açır ✓</summary>
        [RelayCommand]
        private void LoglariAc()
        {
            System.IO.Directory.CreateDirectory(LogQovlugu);
            AcVeYaCix(LogQovlugu);
        }

        /// <summary>💾 Ehtiyat nüsxə — «hara yadda saxlayım?» SORUŞUR ✓✓✓</summary>
        [RelayCommand]
        private void EhtiyatNusxe()
        {
            try
            {
                var baza = System.IO.Directory
                    .GetFiles(System.AppContext.BaseDirectory, "*.db",
                              System.IO.SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (baza is null || !System.IO.File.Exists(baza))
                {
                    StatusMessage = "⚠️ Baza faylı tapılmadı ✗";
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "💾 Ehtiyat nüsxəni hara yadda saxlayaq?",
                    Filter = "Baza faylı (*.db)|*.db",
                    FileName = $"Ehtiyat_{System.DateTime.Now:yyyyMMdd_HHmm}.db"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                System.IO.File.Copy(baza, dialog.FileName, overwrite: true);
                StatusMessage = $"✅ Ehtiyat nüsxə hazır ✓ → {dialog.FileName}";
            }
            catch (System.Exception ex)
            {
                StatusMessage = "❌ Ehtiyat nüsxə alınmadı: " + ex.Message;
            }
        }

        /// <summary>
        /// ♻️ <b>EHTİYAT NÜSXƏDƏN BƏRPA ET</b> ✓✓✓
        /// <para>
        /// İstifadəçi əvvəl saxladığı <b>ehtiyat nüsxə (.db)</b> faylını seçir ✓ →
        /// həmin andaki bütün məlumatlar (maşınlar · kreditlər · satışlar ·
        /// xərclər · maliyyə ✓) PROQRAMA YÜKLƏNİR ✓
        /// </para>
        /// <para>
        /// ⚠ Təhlükəsizlik: əvvəlcə CARİ baza avtomatik surətə götürülür ✓,
        /// sonra proqram yenidən açılır ki, yeni baza yüklənsin ✓✓✓
        /// </para>
        /// </summary>
        [RelayCommand]
        private void EhtiyatdanBerpaEt()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "♻️ Hansı ehtiyat nüsxədən bərpa edək?",
                    Filter = "Baza faylı (*.db)|*.db|Bütün fayllar (*.*)|*.*"
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var baza = System.IO.Directory
                    .GetFiles(System.AppContext.BaseDirectory, "*.db",
                              System.IO.SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (baza is null)
                {
                    StatusMessage = "⚠️ Cari baza faylı tapılmadı ✗";
                    return;
                }

                var cavab = System.Windows.MessageBox.Show(
                    "⚠️ DİQQƏT!\n\n" +
                    "Cari bütün məlumatlar ƏVƏZ OLUNACAQ ✗\n" +
                    "və seçdiyiniz ehtiyat nüsxənin məlumatları YÜKLƏNƏCƏK ✓\n\n" +
                    $"(təhlükəsizlik üçün cari baza avtomatik surətə götürülür ✓)\n\n" +
                    "Davam edəkmi?",
                    "♻️ Ehtiyat nüsxədən bərpa",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (cavab != System.Windows.MessageBoxResult.Yes)
                {
                    return;
                }

                // ✅ ① CARİ bazanın avtomatik surəti (geri qaytarma imkanı ✓)
                var qovluq = System.IO.Path.GetDirectoryName(baza) ?? System.AppContext.BaseDirectory;
                var avtomatik = System.IO.Path.Combine(
                    qovluq, $"Ehtiyat_avtomatik_{System.DateTime.Now:yyyyMMdd_HHmmss}.db");

                System.IO.File.Copy(baza, avtomatik, overwrite: true);

                // ✅ ② Seçilmiş ehtiyat nüsxə bazanın üzərinə yazılır ✓
                System.IO.File.Copy(dialog.FileName, baza, overwrite: true);

                StatusMessage = $"✅ Bərpa olundu ✓ — proqram yenidən açılır... (köhnə surət: {avtomatik})";

                // ✅ ③ Proqram YENİDƏN açılır → yeni baza ilə yüklənir ✓✓✓
                var yol = System.Environment.ProcessPath;

                if (!string.IsNullOrWhiteSpace(yol))
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(yol) { UseShellExecute = true });
                }

                System.Windows.Application.Current?.Shutdown();
            }
            catch (System.Exception ex)
            {
                StatusMessage = "❌ Bərpa alınmadı: " + ex.Message;
            }
        }

        /// <summary>🔄 Bütün məlumatları yenidən yükləyir ✓</summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task MelumatlariYenile()
        {
            try
            {
                StatusMessage = "🔄 Yenilənir...";

                await Finance.LoadAsync();
                await SalesArchive.LoadAsync();

                StatusMessage = "✅ Bütün məlumatlar yeniləndi ✓";
            }
            catch (System.Exception ex)
            {
                StatusMessage = "❌ Yeniləmə alınmadı: " + ex.Message;
            }
        }

        /// <summary>🚪 ÇIXIŞ (SIGN OUT) — proqram yenidən açılır, GİRİŞ pəncərəsi gəlir ✓✓✓</summary>
        [RelayCommand]
        private void CixisEt()
        {
            var cavab = System.Windows.MessageBox.Show(
                "Proqramdan çıxmaq istəyirsinizmi?\n\n" +
                "⚠️ Yenidən daxil olmaq üçün GİRİŞ məlumatları tələb olunacaq ✓",
                "🚪 Çıxış et",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (cavab != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            Services.AuthService.CixisEt();

            var yol = System.Environment.ProcessPath;

            if (!string.IsNullOrWhiteSpace(yol))
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(yol) { UseShellExecute = true });
            }

            System.Windows.Application.Current?.Shutdown();
        }

        /// <summary>📂 Yolu File Explorer-də açır ✓</summary>
        private static void AcVeYaCix(string yol)
        {
            if (string.IsNullOrWhiteSpace(yol) || !System.IO.Directory.Exists(yol))
            {
                return;
            }

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(yol) { UseShellExecute = true });
        }

        // ====================================================================
        //  👥 İSTİFADƏÇİ İDARƏETMƏSİ  (Admin + Asif ✓ — Sahil görünmür ✗)
        // --------------------------------------------------------------------
        //  ✅ Bütün istifadəçilərin ADI · KODU (şifrəsi) · ROLU görünür ✓
        //  ✅ Dəyişmək ✓ · silmək ✓ · YENİ istifadəçi yaratmaq ✓
        //  ✅ Bütün dəyişikliklər JSON faylında saxlanılır ✓ (qalıcıdır ✓)
        // ====================================================================

        private System.Collections.ObjectModel.ObservableCollection<Services.Istifadeci>? _istifadeciSiyahisi;

        /// <summary>👥 Bütün istifadəçilər ✓ (ad · KOD · rol ✓)</summary>
        public System.Collections.ObjectModel.ObservableCollection<Services.Istifadeci> IstifadeciSiyahisi
        {
            get
            {
                if (_istifadeciSiyahisi is null)
                {
                    _istifadeciSiyahisi = new System.Collections.ObjectModel.ObservableCollection<Services.Istifadeci>();
                    IstifadecileriDoldur();
                }

                return _istifadeciSiyahisi;
            }
        }

        /// <summary>👑 İstifadəçini idarə edə bilər? ✓ (Asif + Admin ✓ | Sahil ✗)</summary>
        public bool IstifadeciIdarecisi =>
            Services.AuthService.Cari?.IdareciMi ?? false;

        /// <summary>
        /// 🔢 <b>İstifadəçi sayı mətni</b> ✓✓✓ (v6.2.12) — «👥 İSTİFADƏÇİ İDARƏETMƏSİ»
        /// başlığının sağında göstərilir ✓
        /// </summary>
        public string IstifadeciSayiMetni => IstifadeciSiyahisi.Count switch
        {
            0 => "⚠ HEÇ KİM YOXDUR ✗",
            1 => "1 istifadəçi ✓",
            var say => $"{say} istifadəçi ✓"
        };

        /// <summary>🔑 Kodlar (şifrələr) görünürmü? ✓ (yalnız idarəçilər ✓)</summary>
        public bool KodlarGoruner => IstifadeciIdarecisi;

        /// <summary>📋 Seçilmiş istifadəçi (cədvəldən ✓)</summary>
        [ObservableProperty] private Services.Istifadeci? secilmisIstifadeci;

        /// <summary>
        /// 🛡️ Cədvəldə seçilən istifadəçinin <b>İLK</b> rolu ✓
        /// <para>
        /// «SON ADMIN-in rolu dəyişdirilə bilməz ✗» qoruması üçün saxlanılır ✓
        /// (rol dəyişdirildikdən sonra köhnə dəyəri bilmək lazımdır ✓)
        /// </para>
        /// </summary>
        private Services.IstifadeciRolu? _orijinalRol;

        partial void OnSecilmisIstifadeciChanged(Services.Istifadeci? value)
            => _orijinalRol = value?.Rol;

        [ObservableProperty] private string yeniAd = string.Empty;
        [ObservableProperty] private string yeniIstifadeciAdi = string.Empty;
        [ObservableProperty] private string yeniSifre = string.Empty;
        [ObservableProperty] private Services.IstifadeciRolu yeniRol = Services.IstifadeciRolu.Sahil;

        /// <summary>📨 İstifadəçi əməliyyatlarının nəticəsi ✓</summary>
        [ObservableProperty] private string istifadeciMesaji = string.Empty;

        /// <summary>
        /// 🔽 Rol siyahısı ✓ — 🔒 «BAŞ ADMIN» rolunu YALNIZ ADMIN seçə bilər ✓✓✓
        /// <para>
        /// 🛡️ Asif (Baş İnzibatçı) Admin yarada BİLMİR ✗ — çünki onu görmür ✗✓✓
        /// </para>
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<Services.IstifadeciRolu> Rollar
        {
            get
            {
                if (Services.AuthService.Cari?.Rol == Services.IstifadeciRolu.Admin)
                {
                    return new[]
                    {
                        Services.IstifadeciRolu.Sahil,
                        Services.IstifadeciRolu.Asif,
                        Services.IstifadeciRolu.Admin
                    };
                }

                return new[]
                {
                    Services.IstifadeciRolu.Sahil,
                    Services.IstifadeciRolu.Asif
                };
            }
        }

        /// <summary>💾 İstifadəçi faylının yolu ✓</summary>
        public string IstifadeciFayli => Services.AuthService.Fayl;

        /// <summary>📥 Siyahını yeniləyir ✓ — 🔒 ADMIN-İ YALNIZ ADMIN GÖRÜR ✓✓✓</summary>
        private void IstifadecileriDoldur()
        {
            _istifadeciSiyahisi?.Clear();

            // ================================================================
            //  🛡️ BAŞ İNZİBATÇI (Asif) → BAŞ ADMIN-İ (Seccad) GÖRMÜR ✗✓✓
            //  (əvvəl hamı görürdü ✗ — indi yalnız ADMIN adminləri görür ✓)
            // ================================================================
            var adminleriGoster = Services.AuthService.Cari?.Rol == Services.IstifadeciRolu.Admin;

            foreach (var istifadeci in Services.AuthService.Istifadeciler)
            {
                if (!adminleriGoster && istifadeci.Rol == Services.IstifadeciRolu.Admin)
                {
                    continue;      // ✗ Admin (Seccad) SİYAHIDA GÖRÜNMÜR ✗
                }

                _istifadeciSiyahisi?.Add(istifadeci);
            }

            // 🔢 Başlıqdaki say yenilənir ✓ (v6.2.12)
            OnPropertyChanged(nameof(IstifadeciSayiMetni));
        }

        /// <summary>🔄 Siyahını bazadan yenidən oxuyur ✓</summary>
        [RelayCommand]
        private void IstifadecileriYenile()
        {
            Services.AuthService.Yukle();
            IstifadecileriDoldur();

            IstifadeciMesaji = $"✅ {Services.AuthService.Istifadeciler.Count} istifadəçi yükləndi ✓";
        }

        /// <summary>➕ YENİ istifadəçi yaradır ✓✓✓</summary>
        [RelayCommand]
        private void IstifadeciElaveEt()
        {
            IstifadeciMesaji = Services.AuthService.ElaveEt(
                YeniAd, YeniIstifadeciAdi, YeniSifre, YeniRol);

            IstifadecileriDoldur();

            if (IstifadeciMesaji.StartsWith('✅'))
            {
                YeniAd = string.Empty;
                YeniIstifadeciAdi = string.Empty;
                YeniSifre = string.Empty;
            }
        }

        /// <summary>✏️ Seçilmiş istifadəçinin adını · kodunu · rolunu yadda saxlayır ✓✓✓</summary>
        [RelayCommand]
        private void IstifadeciYaddaSaxla()
        {
            if (SecilmisIstifadeciKontrol() is not { } istifadeci)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(istifadeci.IstifadeciAdi))
            {
                IstifadeciMesaji = "⚠️ «Giriş adı (login)» boş ola bilməz ✗";
                return;
            }

            if (string.IsNullOrWhiteSpace(istifadeci.Sifre))
            {
                IstifadeciMesaji = "⚠️ «🔑 KOD (şifrə)» boş ola bilməz ✗";
                return;
            }

            if (Services.AuthService.AdMovcuddur(istifadeci.IstifadeciAdi, istifadeci))
            {
                IstifadeciMesaji = "⚠️ Bu giriş adı (login) artıq mövcuddur ✗";
                return;
            }

            // ⚠ SON ADMIN-in rolu dəyişdirilə bilməz ✗ (sistem kilidlənər ✗)
            if (_orijinalRol == Services.IstifadeciRolu.Admin
                && istifadeci.Rol != Services.IstifadeciRolu.Admin
                && !Services.AuthService.Istifadeciler.Any(i =>
                       i.Rol == Services.IstifadeciRolu.Admin && !ReferenceEquals(i, istifadeci)))
            {
                istifadeci.Rol = Services.IstifadeciRolu.Admin;   // geri qaytarılır ✓
                IstifadeciMesaji = "⚠️ SON ADMIN-in rolu dəyişdirilə bilməz ✗ (tam giriş itər ✗)";
                return;
            }

            if (string.IsNullOrWhiteSpace(istifadeci.Ad))
            {
                istifadeci.Ad = istifadeci.IstifadeciAdi;   // Ad boşdursa giriş adı ilə eyniləşir ✓
            }

            Services.AuthService.YaddaSaxla();
            _orijinalRol = istifadeci.Rol;
            IstifadecileriDoldur();

            IstifadeciMesaji =
                $"✅ «{istifadeci.Ad}» saxlanıldı ✓ — adı · 🔑 KOD · rol yeniləndi ✓";
        }

        /// <summary>➖ Seçilmiş istifadəçini silir ✓ (hər kəs silə bilər ✓)</summary>
        [RelayCommand]
        private void IstifadeciSil()
        {
            if (SecilmisIstifadeciKontrol() is not { } istifadeci)
            {
                return;
            }

            var ad = string.IsNullOrWhiteSpace(istifadeci.Ad)
                ? istifadeci.IstifadeciAdi
                : istifadeci.Ad;

            var netice = Services.AuthService.Sil(istifadeci);

            IstifadeciMesaji = netice.StartsWith('✅')
                ? $"✅ «{ad}» silindi ✓"
                : netice;

            if (IstifadeciMesaji.StartsWith('✅'))
            {
                SecilmisIstifadeci = null;
            }

            IstifadecileriDoldur();
        }

        /// <summary>♻️ Standart 3 istifadəçini geri gətirir ✓</summary>
        [RelayCommand]
        private void StandartIstifadeciler()
        {
            Services.AuthService.Istifadeciler.Clear();

            foreach (var i in Services.AuthService.Standart())
            {
                Services.AuthService.Istifadeciler.Add(i);
            }

            Services.AuthService.YaddaSaxla();
            IstifadecileriDoldur();

            IstifadeciMesaji = "♻️ Standart istifadəçilər bərpa olundu ✓ (Sahil · Asif · Seccad)";
        }

        /// <summary>🛡️ Seçim yoxlanılır ✓</summary>
        private Services.Istifadeci? SecilmisIstifadeciKontrol()
        {
            if (SecilmisIstifadeci is null)
            {
                IstifadeciMesaji = "⚠️ Əvvəlcə cədvəldən istifadəçi seçin ✗";
                return null;
            }

            return SecilmisIstifadeci;
        }

        // ====================================================================
        //  💾 DATA USB — YALNIZ SİZİN FLƏŞKARTINIZ TANINIR ✓✓✓
        // --------------------------------------------------------------------
        //  • «➕ Əlavə et» → seçilmiş USB-nin kökündə GİZLİ marker faylı
        //    yazılır ✓ və unikal token kompüterdə saxlanılır ✓
        //  • «➖ Sil» → qeydiyyat silinir ✓ (+ marker silinir ✓)
        //  • Başqa fləşkartlar TANINMIR ✗ (token fərqlidir ✓) — bütün
        //    fləşkartlar YOX ✗✓✓
        // ====================================================================

        private System.Collections.ObjectModel.ObservableCollection<string>? _usbSiyahisi;

        /// <summary>
        /// 💾 Mövcud disklər ✓ («F:\|AD|14.9 GB boş|marker» formatı ✓)
        /// <para>
        /// ⚠ Siyahı <b>İLK AÇILIŞDA avtomatik</b> doldurulur ✓ (əvvəl boş qalırdı ✗)
        /// — USB-ni sonra taxsanız «🔄 Yenilə» basın ✓
        /// </para>
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<string> UsbSiyahisi
        {
            get
            {
                if (_usbSiyahisi is null)
                {
                    _usbSiyahisi = new System.Collections.ObjectModel.ObservableCollection<string>();
                    UsbDoldur();
                }

                return _usbSiyahisi;
            }
        }

        [ObservableProperty] private string? secilmisUsb;

        /// <summary>📨 USB əməliyyatlarının nəticəsi ✓</summary>
        [ObservableProperty] private string usbMesaji = string.Empty;

        /// <summary>📢 DATA USB statusu (tanındı / taxılı deyil ✓)</summary>
        public string UsbStatusu => Services.DataUsbService.StatusMetni();

        /// <summary>🕒 Qeydiyyat məlumatı ✓</summary>
        public string UsbQeydiyyati => Services.DataUsbService.QeydiyyatMetni();

        /// <summary>📥 Siyahını disklərlə doldurur ✓</summary>
        private void UsbDoldur()
        {
            _usbSiyahisi?.Clear();

            foreach (var d in Services.DataUsbService.Namizədlər())
            {
                _usbSiyahisi?.Add(d);
            }

            if (!string.IsNullOrWhiteSpace(SecilmisUsb)
                && _usbSiyahisi?.Contains(SecilmisUsb) == true)
            {
                // ✓ seçim qalır ✓
            }
            else if (!string.IsNullOrWhiteSpace(SecilmisUsb))
            {
                SecilmisUsb = null;
            }

            if (SecilmisUsb is null && _usbSiyahisi is { Count: > 0 })
            {
                SecilmisUsb = _usbSiyahisi[0];
            }
        }

        /// <summary>🔄 USB siyahısını yeniləyir ✓ (USB-ni taxdıqdan sonra basın ✓)</summary>
        [RelayCommand]
        private void UsbYenile()
        {
            UsbDoldur();

            OnPropertyChanged(nameof(UsbStatusu));
            OnPropertyChanged(nameof(UsbQeydiyyati));

            UsbMesaji = _usbSiyahisi is null || _usbSiyahisi.Count == 0
                ? "⚠️ Heç bir disk tapılmadı ✗ — «📂 Əl ilə seç» ilə USB-ni göstərin ✓"
                : $"✅ {_usbSiyahisi.Count} disk tapıldı ✓ — USB-ni seçib «➕ Əlavə et» basın ✓";
        }

        /// <summary>
        /// 📂 <b>ƏL İLƏ SEÇ</b> — qovluq seçmə pəncərəsi ✓✓✓
        /// <para>
        /// ⚠ Siyahıda USB görünmürsə (nadir hallarda ✗) bu düymə ilə fləşkartı
        /// birbaşa göstərmək olar ✓ — proqram kökü özü tapır ✓
        /// </para>
        /// </summary>
        [RelayCommand]
        private void UsbQovluqSec()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "💾 DATA USB-ni seçin (fləşkartın kökünü və ya içindəki qovluğu ✓)",
                    Multiselect = false
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var root = Services.DataUsbService.QovluqdanKok(dialog.FolderName);

                if (string.IsNullOrWhiteSpace(root))
                {
                    UsbMesaji = "⚠️ Seçilmiş yoldan disk kökü müəyyən edilə bilmədi ✗";
                    return;
                }

                UsbMesaji = Services.DataUsbService.ElaveEt(root, $"DATA USB {root}");

                UsbDoldur();
                OnPropertyChanged(nameof(UsbStatusu));
                OnPropertyChanged(nameof(UsbQeydiyyati));

                StatusMessage = UsbMesaji;
            }
            catch (Exception ex)
            {
                UsbMesaji = "❌ Qovluq seçmə alınmadı: " + ex.Message;
            }
        }

        /// <summary>➕ <b>SEÇİLMİŞ USB-Nİ DATA USB KİMİ ƏLAVƏ EDİR</b> ✓✓✓</summary>
        [RelayCommand]
        private void UsbElaveEt()
        {
            // 🛡️ TAM QORUMA ✗→✓: heç bir halda proqram ÇÖKMÜR ✗
            try
            {
                if (string.IsNullOrWhiteSpace(SecilmisUsb))
                {
                    UsbMesaji = "⚠️ Əvvəlcə siyahıdan USB seçin ✗ (🔄 Yenilə ✓)";
                    return;
                }

                if (!SecilmisUsb.Contains('|'))
                {
                    UsbMesaji = "⚠️ Seçim düzgün deyil ✗ — «🔄 Yenilə» basıb yenidən seçin ✓";
                    return;
                }

                var root = SecilmisUsb.Split('|')[0];

                UsbMesaji = Services.DataUsbService.ElaveEt(root, $"DATA USB {root}");

                OnPropertyChanged(nameof(UsbStatusu));
                OnPropertyChanged(nameof(UsbQeydiyyati));

                StatusMessage = UsbMesaji;

                UsbYenile();
            }
            catch (Exception ex)
            {
                UsbMesaji = "⚠️ USB əlavə etmə xətası ✗ — " + ex.Message;
                StatusMessage = UsbMesaji;

                Cas0201.Firebase.AppLogger.Xeta(ex, "USB əlavə et");
            }
        }

        /// <summary>
        /// 📥 <b>MÖVCUD USB-Nİ TAX, OXU VƏ İNTEQRASİYA ET</b> ✓✓✓
        /// <list type="number">
        ///   <item>🔑 USB-ni marker-dən <b>avtomatik tanıyır</b> ✓ (qeydiyyat lazım deyil ✗)</item>
        ///   <item>📥 USB-dəki bazanı oxuyub <b>çatışmayan qeydləri</b> proqrama əlavə edir ✓</item>
        ///   <item>💾 Sonra bazanı <b>USB-nin ÖZÜNƏ</b> yazır ✓✓✓</item>
        ///   <item>🔄 Ekran yenilənir ✓ (məlumat dərhal görünür ✓)</item>
        /// </list>
        /// </summary>
        [RelayCommand]
        private void UsbTaxVeOku()
        {
            var nəticə = new System.Collections.Generic.List<string>();

            try
            {
                // ① 🔑 AVTOMATİK TANIMA ✓ (marker-dən token oxunur ✓)
                nəticə.Add(Services.DataUsbService.UsbAvtomatikTanit());

                // ② 📥 USB TAPILIRSA → BƏRPA / BİRLƏŞDİRMƏ + USB-YƏ YAZMA ✓✓✓
                var tapılan = Services.DataUsbService.Tap();

                if (tapılan is null)
                {
                    nəticə.Add("⚠️ Qeydiyyatlı DATA USB tapılmadı ✗");
                    nəticə.Add("💡 Fləşkartı taxın ✓ və ya «➕ Əlavə et» ilə YENİ USB yaradın ✓");
                }
                else
                {
                    nəticə.Add($"🔑 USB tapıldı ✓ → {tapılan.Value.Root}");

                    // 📥 Oxu + inteqrasiya ✓ (yerli baza boşdursa köçürür ✓, dolu olubsa birləşdirir ✓)
                    nəticə.Add(Services.DataUsbService.UsbDenBerpaEt());

                    // 💾 Geri yazma ✓ — bazanın TAM surəti USB-nin özünə ✓
                    nəticə.Add(Services.DataUsbService.UsbYeKopyala(tapılan.Value.Root));
                }

                UsbMesaji = string.Join("\n", nəticə);

                OnPropertyChanged(nameof(UsbStatusu));
                OnPropertyChanged(nameof(UsbQeydiyyati));

                StatusMessage = UsbMesaji;

                UsbYenile();      // 🔄 disk siyahısı + seçim yenilənir ✓
            }
            catch (Exception ex)
            {
                UsbMesaji = "⚠️ USB oxuma xətası ✗ — " + ex.Message;

                Cas0201.Firebase.AppLogger.Xeta(ex, "USB tax və oxu");
            }
        }

        /// <summary>➖ <b>DATA USB QEYDİYYATINI SİLİR</b> ✓✓✓</summary>
        [RelayCommand]
        private void UsbSil()
        {
            if (Services.DataUsbService.Qeydiyyat is null)
            {
                UsbMesaji = "ℹ️ Qeydiyyatdan keçmiş DATA USB yoxdur ✗";
                return;
            }

            var cavab = System.Windows.MessageBox.Show(
                "⚠️ DATA USB qeydiyyatı silinsin?\n\n" +
                "• Kompüter bu fləşkartı artıq TANIMAYACAQ ✗\n" +
                "• Fləşkartdaki GİZLİ marker faylı da silinir ✓\n" +
                "• Sənəd əməliyyatları üçün yenidən «➕ Əlavə et» lazım olacaq ✓",
                "➖ DATA USB silinir",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (cavab != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            UsbMesaji = Services.DataUsbService.Sil();

            OnPropertyChanged(nameof(UsbStatusu));
            OnPropertyChanged(nameof(UsbQeydiyyati));

            StatusMessage = UsbMesaji;
        }

        // ====================================================================
        //  ⏱️ AVTOMATİK YENİLƏMƏ — HƏR 5 SANİYƏ ✓✓✓
        // --------------------------------------------------------------------
        //  • Bütün alt ViewModel-lər (Avto Park · Xərclər · Satış · Arxiv ·
        //    Kreditlər · Maliyyə · Tərəfdaşlar … ✓) yenilənir
        //  • UI HEÇ VAXT donmur ✗ (DispatcherTimer ✓ + async ✓ + təkrar giriş kilidi ✓)
        //  • İstifadəçi redaktə edərkən xəta olarsa → udulur ✗, log yazılır ✓
        // ====================================================================

        private System.Windows.Threading.DispatcherTimer? _avtoYenile;
        private bool _yenilenir;

        /// <summary>🔢 Son görülən məlumat versiyası ✓ — dəyişməyibsə yeniləmə EDİLMİR ✗✓✓✓</summary>
        private long _sonVersiya = -1;

        /// <summary>
        /// ⚡ <b>«ÇİRKİN» TABLAR</b> ✓✓✓ — dəyişiklik olub, lakin həmin tab
        /// GÖRÜNMƏDİYİ üçün hələ yenilənməyib ✗ → istifadəçi taba keçəndə yenilənir ✓
        /// </summary>
        private readonly System.Collections.Generic.HashSet<int> _çirkliTablar = new();

        /// <summary>
        /// 📑 <b>İSTİFADƏÇİ TAB DƏYİŞDİ</b> ✓✓✓ —
        /// əvvəl bütün 9 tab yenilənirdi ✗ (200+ maşın · 1800+ xərc ✓) → proqram donurdu ✗✗
        /// İndi isə YALNIZ o tab yenilənir ✓✓✓
        /// </summary>
        public void TabDeyisdi(int sıra)
        {
            AktivTabSirasi = sıra < 0 ? 0 : sıra;

            // ✗ bu tab yenilənməyə ehtiyac duymursa → iş yoxdur ✓
            if (!_çirkliTablar.Remove(AktivTabSirasi)) return;

            // ================================================================
            //  🚀 YALNIZ SEÇİLƏN TAB YÜKLƏNİR ✓✓✓  (PERFORMANS)
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL `HamisiniYenileAsync()` çağırılırdı ✗ —
            //     o metodun «real dəyişiklik yoxdur» qoruyucusu var ✗ →
            //     «çirkli» tab bəzən HEÇ YÜKLƏNMİRDİ ✗ (köhnə məlumat qalırdı ✗)
            //  ✅ İNDİ birbaşa HƏMİN tab-ın ViewModel-i yüklənir ✓✓✓
            // ================================================================
            try
            {
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () =>
                {
                    var sıraı = 0;

                    foreach (var (ad, vm) in AltViewModeller())
                    {
                        if (sıraı++ == AktivTabSirasi)
                        {
                            await VmYukleAsync(vm, ad);
                            break;
                        }
                    }
                });
            }
            catch { }
        }

        /// <summary>📑 Hazırda GÖRÜNƏN tab-ın sırası ✓ (donmanın qarşısı ✓✓✓)</summary>
        [ObservableProperty] private int aktivTabSirasi;

        /// <summary>
        /// 📢 <b>YENİLƏMƏ BAŞLADI</b> ✓✓✓ — UI (pəncərə) seçimləri YADDA SAXLAMAQ üçün abunə olur ✓
        /// <para>Beləliklə avtomatik yeniləmə istifadəçinin SEÇİMİNİ SİLMİR ✗✓✓</para>
        /// </summary>
        public event System.Action? YenilemeBasladi;

        /// <summary>📢 <b>YENİLƏMƏ BİTDİ</b> ✓✓✓ — seçimlər BƏRPA olunur ✓</summary>
        public event System.Action? YenilemeBitdi;

        /// <summary>⏱️ Avtomatik yeniləmə aktivdir? ✓</summary>
        [ObservableProperty] private bool avtoYenilemeAktiv;

        /// <summary>⏱️ Yeniləmə dövrü (saniyə ✓)</summary>
        [ObservableProperty] private int avtoYenilemeSaniye = 5;

        /// <summary>📢 Avtomatik yeniləmə statusu ✓</summary>
        public string AvtoYenilemeMetni => AvtoYenilemeAktiv
            ? $"⏱️ AVTOMATİK YENİLƏMƏ AKTİVDİR ✓ — hər {AvtoYenilemeSaniye} saniyə ✓"
            : "⏸️ Avtomatik yeniləmə SÖNÜKDÜR ✗ — «⏱️ Başlat» basın ✓";

        /// <summary>
        /// ▶️ <b>AVTOMATİK YENİLƏMƏNİ BAŞLADIR</b> ✓✓✓
        /// <para>Proqram açılanda bir dəfə çağırılır ✓ (App.xaml.cs ✓)</para>
        /// </summary>
        public void AvtoYenilemeyeBasla(int saniye = 5)
        {
            try
            {
                AvtoYenilemeSaniye = saniye <= 0 ? 5 : saniye;

                if (_avtoYenile is not null)
                {
                    _avtoYenile.Interval = TimeSpan.FromSeconds(AvtoYenilemeSaniye);
                    _avtoYenile.Start();
                }
                else
                {
                    _avtoYenile = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(AvtoYenilemeSaniye)
                    };

                    _avtoYenile.Tick += async (_, _) => await HamisiniYenileAsync();
                    _avtoYenile.Start();
                }

                AvtoYenilemeAktiv = true;

                OnPropertyChanged(nameof(AvtoYenilemeMetni));

                StatusMessage = AvtoYenilemeMetni;
            }
            catch (Exception ex)
            {
                StatusMessage = "⚠️ Avto yeniləmə başladıla bilmədi: " + ex.Message;
            }
        }

        /// <summary>⏸️ Avtomatik yeniləməni dayandırır ✓</summary>
        public void AvtoYenilemeniDayandir()
        {
            try
            {
                _avtoYenile?.Stop();
                _avtoYenile = null;
                AvtoYenilemeAktiv = false;

                OnPropertyChanged(nameof(AvtoYenilemeMetni));

                StatusMessage = "⏸️ Avtomatik yeniləmə dayandırıldı ✓";
            }
            catch (Exception ex)
            {
                StatusMessage = "⚠️ Dayandırıla bilmədi: " + ex.Message;
            }
        }

        /// <summary>🔄 Avtomatik yeniləməni aç / bağla ✓</summary>
        [RelayCommand]
        private void AvtoYenilemeToggle()
        {
            if (AvtoYenilemeAktiv)
            {
                AvtoYenilemeniDayandir();
            }
            else
            {
                AvtoYenilemeyeBasla(AvtoYenilemeSaniye);
            }
        }

        /// <summary>
        /// 💾 <b>VERİLƏNLƏR BAZASINI (.db) USB-YƏ KOPYALAYIR</b> ✓✓✓
        /// <para>İstənilən vaxt əl ilə — «D:\021cars_data.db» + «D:\data\backup.db» ✓</para>
        /// </summary>
        [RelayCommand]
        private void UsbBazaKopyala()
        {
            var tapilan = Services.DataUsbService.Tap();

            if (tapilan is null)
            {
                UsbMesaji = "⚠️ DATA USB TANINMADI ✗ — USB-ni taxın və «➕ Əlavə et» basın ✓";
                return;
            }

            // ⚡ AĞIR İŞ ARXA FONDA ✓✓✓ — USB-yə yazma (və media köçürmə ✓) UI-ı DONDURMUR ✗
            var kök = tapilan.Value.Root;

            UsbMesaji = "⏳ USB-yə yazılır… (arxa fonda ✓ — proqram donmur ✗)";
            StatusMessage = UsbMesaji;

            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var nəticə = Services.DataUsbService.UsbYeKopyala(kök);

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        UsbMesaji = nəticə;
                        StatusMessage = nəticə;
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        UsbMesaji = "⚠️ USB yazma xətası ✗ — " + ex.Message;
                        StatusMessage = UsbMesaji;
                    });
                }
            });
        }

        /// <summary>⏱️ İndi (əl ilə) bütün məlumatları yeniləyir ✓</summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task HamisiniIndiYenile() =>
            await HamisiniYenileAsync(hamısı: true);

        /// <summary>
        /// 🔄 <b>BÜTÜN MƏLUMATLARI YENİLƏYİR</b> ✓✓✓
        /// <para>
        /// Hər alt ViewModel-in <c>LoadAsync(...)</c> metodu tapılır və çağırılır ✓
        /// (reflection ilə ✓ — yeni tab əlavə olunanda avtomatik daxil olur ✓)
        /// </para>
        /// <para>
        /// ⚠ UI DONMUR ✗: taymer UI thread-dədir ✓, yükləmə async-dir ✓;
        /// təkrar giriş <c>_yenilenir</c> kilidi ilə bloklanır ✓
        /// </para>
        /// </summary>
        /// <param name="hamısı">
        /// ✅ <c>false</c> (standart ✓): <b>yalnız GÖRÜNƏN tab</b> yenilənir ✓✓✓ (donma YOX ✗)<br/>
        /// ✅ <c>true</c>: bütün tablar ✓ («🔄 İndi yenilə» düyməsi üçün ✓)
        /// </param>
        public async System.Threading.Tasks.Task HamisiniYenileAsync(bool hamısı = false)
        {
            if (_yenilenir)
            {
                return;      // ✓ əvvəlki yeniləmə hələ bitməyib ✓ (üst-üstə düşmür ✗)
            }

            // 🔢 ① REAL DƏYİŞİKLİK YOXDURSA → YENİLƏMƏ HEÇ İŞLƏMİR ✗✓✓✓
            //     ★ BU, ƏSAS HƏLLDİR ★: formalarda seçilmiş variantlar ✓, yazılan mətn ✓,
            //       seçilmiş maşın ✓ — boş dövrlərdə QORUNUR ✓ (5 saniyəlik yeniləmə toxunmur ✗)
            try
            {
                var versiya = App.Kopru?.Versiya ?? -1;

                if (versiya >= 0 && versiya == _sonVersiya)
                {
                    return;      // ✅ HEÇ NƏ DƏYİŞMƏYİB → UI-a TOXUNULMUR ✗✓✓✓
                }

                if (versiya >= 0)
                {
                    _sonVersiya = versiya;
                }
            }
            catch { }

            // ⌨️ ② İSTİFADƏÇİ REDAKTƏ EDİRSƏ → YENİLƏMƏ TƏXİRƏ SALINIR ✗✓✓
            //     Əks halda: «Ümumi Xərclər»də usta adı yazarkən, maşın seçərkən və s.
            //     5 saniyəlik yeniləmə bütün yazdıqlarınızı SİLİRDİ ✗ → indi SİLİNMİR ✓✓✓
            if (İstifadəçiRedakteEdir())
            {
                return;
            }

            _yenilenir = true;

            // 📢 UI-yə xəbər ver: SEÇİMLƏRİ YADDA SAXLA ✓ (silinməsin ✗✓✓)
            try { YenilemeBasladi?.Invoke(); } catch { }

            try
            {
                // ================================================================
                //  🎯 YALNIZ AKTİV (GÖRÜNƏN) TAB YENİLƏNİR ✓✓✓
                // ----------------------------------------------------------------
                //  ⚠️ ƏVVƏL ✗: hər dəyişiklikdə 9 tab-ın HAMISI yenidən qurulurdu ✗
                //     (194 avtomobil · 1800+ xərc ✓) → UI DONURDU ✗✓✓
                //  ✅ İNDİ: yalnız GÖRÜNƏN tab ✓; qalanları «çirkli» işarələnir ✓
                //     və istifadəçi həmin taba keçəndə yenilənir ✓✓✓ (TabDeyisdi ✓)
                // ================================================================
                var sıra = 0;
                var yüklənmiş = new System.Collections.Generic.HashSet<object>();

                foreach (var (ad, vm) in AltViewModeller())
                {
                    var aktiv = sıra == AktivTabSirasi;

                    sıra++;

                    if (!hamısı && !aktiv)
                    {
                        _çirkliTablar.Add(sıra - 1);      // ⏳ sonra yenilənəcək ✓
                        continue;
                    }

                    // ♻️ Eyni ViewModel bir neçə tabda işlənir ✓ → bir dəfə yüklə ✓
                    if (!yüklənmiş.Add(vm))
                    {
                        continue;
                    }

                    await VmYukleAsync(vm, ad);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Avto yeniləmə xətası: " + ex.Message);
            }
            finally
            {
                // ♻️ SEÇİMLƏRİ BƏRPA ET ✓ (istifadəçi seçdiyi maşın/kredit QALIR ✓✓✓)
                try { YenilemeBitdi?.Invoke(); } catch { }

                _yenilenir = false;
            }
        }

        /// <summary>
        /// ⌨️ <b>İSTİFADƏÇİ HAZIRDA REDAKTƏ EDİR?</b> ✓✓✓
        /// <para>
        /// Fokus hər hansı mətn / kod / seçim / tarix sahəsindədirsə → <c>true</c> ✓
        /// </para>
        /// <para>
        /// Nəticə: avtomatik yeniləmə <b>TƏXİRƏ SALINIR</b> ✗ →
        /// yazdığınız «usta adı» ✓, seçdiyiniz maşın ✓, daxil etdiyiniz məbləğ ✓ —
        /// <b>HEÇ NƏ SİLİNMİR</b> ✓✓✓
        /// </para>
        /// <para>
        /// ℹ️ İstifadəçi işini bitirib fokus başqa yerə keçən kimi
        /// ilk 5 saniyəlik dövrdə yenilənmə normal davam edir ✓
        /// </para>
        /// </summary>
        private static bool İstifadəçiRedakteEdir()
        {
            try
            {
                var fokus = System.Windows.Input.Keyboard.FocusedElement;

                return fokus is System.Windows.Controls.Primitives.TextBoxBase   // ✍️ TextBox ✓ RichTextBox ✓
                    or System.Windows.Controls.PasswordBox                        // 🔑 şifrə ✓
                    or System.Windows.Controls.ComboBox                           // 📋 seçim ✓
                    or System.Windows.Controls.DatePicker;                        // 📅 tarix ✓
            }
            catch
            {
                return false;   // 🛡️ yoxlama alınmadı → yeniləmə davam edir ✓ (çökmə YOX ✗)
            }
        }

        /// <summary>
        /// 📋 <b>TAB SIRASINA UYĞUN ViewModel siyahısı</b> ✓✓✓
        /// <para>
        /// ⚠ SIRA VACİBDİR: indeks = <c>TabControl.SelectedIndex</c> ✓
        /// (buna görə «çirkli tab» mexanizmi DÜZGÜN işləyir ✓✓✓)
        /// </para>
        /// <para>
        /// ℹ️ Bir neçə tab EYNİ ViewModel-i işlədir ✓ (məs. «💳 Kreditlər»,
        /// «📅 Ödəniş Qrafiki» və «📤 Transfer» → hamısı <c>Credits</c> ✓)
        /// </para>
        /// </summary>
        private System.Collections.Generic.IEnumerable<(string Ad, object Vm)> AltViewModeller()
        {
            yield return ("🚘 Avto Park & Arxiv", CarPark);                 // 0
            yield return ("💳 Ümumi Xərclər", Expenses);                    // 1
            yield return ("🧮 Kredit Kalkulyatoru", Calculator);            // 2
            yield return ("💰 Satış", Sales);                               // 3
            yield return ("💳 Kreditlər", Credits);                         // 4
            yield return ("📅 Kredit Ödəniş Qrafiki", Credits);             // 5
            yield return ("🏷️ Kredit Əlavə Gəlir/Xərc", CreditTransactions); // 6
            yield return ("📊 Maliyyə Paneli", Finance);                    // 7
            yield return ("💵 Kassa", Kassa);                               // 8
            yield return ("🔔 Bildirişlər", Bildirisler);                    // 9
            yield return ("🗓️ Ödəniş Təqvimi", Tevim);                      // 10
            yield return ("🗄️ Satılan & Krediti Bitmiş", SalesArchive);     // 11
            yield return ("🤝 Barter Keçmişi", Credits);                    // 12
            yield return ("📤 Transfer (gizli)", Credits);                  // 13
            yield return ("📤 Transfer", Credits);                          // 14
            yield return ("👥 Tərəfdaşlar", Partners);                      // 15
            yield return ("🌐 Veb Sayt", Web);                              // 16
            yield return ("⚙️ Tənzimləmələr", this);                        // 17
            yield return ("📜 Skript İdxalı", Skript);                      // 18
            yield return ("🏦 Kredit İdxalı", KreditIdxal);                 // 19
        }

        // ====================================================================
        //  🚀 PERFORMANS: TƏK TABIN YÜKLƏNMƏSİ VƏ «ÇİRKLi» TAB MEXANİZMİ ✓✓✓
        // --------------------------------------------------------------------
        //  ⚠ ƏVVƏL: istifadəçi bir xərc əlavə edəndə 7 ViewModel-in HAMISI
        //     yenidən yüklənirdi ✗ (hər biri öz cədvəlini başdan oxuyurdu ✗)
        //     → «məlumat üzərində işləyəndə donur» problemi MƏHZ bu idi ✗✓✓
        //
        //  ✅ İNDİ: yalnız GÖRÜNƏN tab yenilənir ✓, qalanları «çirkli»
        //     işarələnir ✓ və istifadəçi onlara keçəndə yüklənir ✓✓✓
        // ====================================================================

        /// <summary>
        /// 🚀 YALNIZ GÖRÜNƏN tab-ın ViewModel-ini yükləyir ✓✓✓
        /// (qalan tablar «çirkli» işarələnir ✓ — keçəndə yüklənəcək ✓)
        /// </summary>
        public async System.Threading.Tasks.Task YalnizAktivTabYenileAsync()
        {
            if (_yenilenir)
            {
                return;
            }

            _yenilenir = true;

            try { YenilemeBasladi?.Invoke(); } catch { }

            try
            {
                var sıra = 0;

                foreach (var (ad, vm) in AltViewModeller())
                {
                    var aktiv = sıra == AktivTabSirasi;

                    sıra++;

                    if (aktiv)
                    {
                        await VmYukleAsync(vm, ad);
                    }
                    else
                    {
                        _çirkliTablar.Add(sıra - 1);   // ⏳ sonra yüklənəcək ✓
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Aktiv tab yeniləmə xətası: " + ex.Message);
            }
            finally
            {
                try { YenilemeBitdi?.Invoke(); } catch { }
                _yenilenir = false;
            }
        }

        /// <summary>
        /// ViewModel-in <c>LoadAsync</c> metodunu tapıb çağırır ✓ (reflection ✓)
        /// — yeni tab əlavə olunanda avtomatik daxil olur ✓
        /// </summary>
        private static async System.Threading.Tasks.Task VmYukleAsync(object vm, string ad)
        {
            try
            {
                var metod = vm.GetType().GetMethod(
                    "LoadAsync",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic,
                    null,
                    new[] { typeof(System.Threading.CancellationToken) },
                    null)
                    ?? vm.GetType().GetMethod(
                        "LoadAsync",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null);

                if (metod is null)
                {
                    return;      // ✓ bu VM-də LoadAsync yoxdur → keç ✓
                }

                var parametrler = metod.GetParameters().Length == 1
                    ? new object[] { System.Threading.CancellationToken.None }
                    : null;

                var netice = metod.Invoke(vm, parametrler);

                if (netice is System.Threading.Tasks.Task task)
                {
                    await task;    // ✓ bitməsini gözləyirik ✓
                }
            }
            catch (Exception ex)
            {
                // ✗ bir tab yüklənmədisə digərləri DAVAM EDİR ✓
                System.Diagnostics.Debug.WriteLine($"{ad} yüklənə bilmədi — {ex.Message}");
            }
        }

        [ObservableProperty] private string statusMessage = "Hazır";
        [ObservableProperty] private bool isInitialized;

        /// <summary>Arxiv pəncərəsinin açılması tələb olunduqda (View tərəfindən dinlənir).</summary>
        /// <summary>Xarici dəyişiklik izləyicisi (veb server - masaüstü CANLI sinxronizasiya).</summary>
        private readonly IDataChangeWatcher? _dataChanges;

        /// <summary>
        /// Bu vaxta qədər gələn xarici dəyişiklik hadisələri nəzərə alınmır.
        /// Səbəb: tətbiqin ÖZ yazdığı məlumat da faylı dəyişir və hadisə yaradır —
        /// halbuki o anda cədvəllər artıq yenilənib, ikinci dəfə yeniləmək lazım deyil.
        /// </summary>
        private DateTime _ignoreExternalUntilUtc = DateTime.MinValue;

        /// <summary>Tətbiqin öz dəyişikliyindən sonra gözləmə pəncərəsi.</summary>
        private static readonly TimeSpan SelfChangeWindow = TimeSpan.FromSeconds(2.5);

        public event EventHandler<CarItem>? ArchiveRequested;

        public MainViewModel(
            CarParkViewModel carPark,
            ExpensesViewModel expenses,
            LoanCalculatorViewModel calculator,
            SalesViewModel sales,
            SalesArchiveViewModel salesArchive,
            CreditsViewModel credits,
            CreditTransactionsViewModel creditTransactions,
            FinanceViewModel finance,
            KassaViewModel kassa,
            BildirisViewModel bildirisler,
            TevimViewModel tevim,
            PartnersViewModel partners,
            WebViewModel web,
            ScriptImportViewModel skript,
            KreditIdxalViewModel kreditIdxal,

            IDataChangeWatcher? dataChanges = null)
        {
            _dataChanges = dataChanges;

            // Xarici (veb server) dəyişikliyi -> bütün tablar avtomatik yenilənir.
            if (_dataChanges is not null)
            {
                _dataChanges.Changed += OnExternalDataChanged;
            }

            CarPark = carPark;
            Expenses = expenses;
            Calculator = calculator;
            Sales = sales;
            SalesArchive = salesArchive;
            Credits = credits;
            CreditTransactions = creditTransactions;
            Finance = finance;
            Kassa = kassa;
            Bildirisler = bildirisler;
            Tevim = tevim;
            Partners = partners;
            Web = web;

            // ================================================================
            //  📜 SKRİPT İDXALI BİTDİ → BÜTÜN TAB-LAR DƏRHAL YENİLƏNİR ✓✓✓
            // ----------------------------------------------------------------
            //  Yeni avtomobillər · xərclər · kateqoriyalar dərhal görünür ✓
            // ================================================================
            Skript = skript;
            Skript.IdxalBitdi += async (_, _) => await YalnizAktivTabYenileAsync();

            // ================================================================
            //  🧾 KREDİT İDXALI BİTDİ → BÜTÜN TAB-LAR DƏRHAL YENİLƏNİR ✓✓✓
            // ================================================================
            KreditIdxal = kreditIdxal;
            KreditIdxal.IdxalBitdi += async (_, _) => await YalnizAktivTabYenileAsync();

            CarPark.ArchiveRequested += (_, car) => ArchiveRequested?.Invoke(this, car);

            // ================================================================
            //  🚀 PERFORMANS: HƏR DƏYİŞİKLİKDƏ 7 TAB YENİDƏN YÜKLƏNMİR ✗✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL: bir xərc əlavə edəndə «Avto Park» ✓ «Maliyyə Paneli» ✓
            //     «Tərəfdaşlar» ✓ — HAMISI öz cədvəlini başdan oxuyurdu ✗ →
            //     məlumat çox olanda proqram DONURDU ✗✓✓
            //
            //  ✅ İNDİ: YALNIZ GÖRÜNƏN tab yenilənir ✓ — digərləri «çirkli»
            //     işarələnir ✓ və istifadəçi onlara keçəndə yüklənir ✓✓✓
            // ================================================================
            CarPark.CarsChanged += async (_, _) => await YalnizAktivTabYenileAsync();
            Expenses.ExpensesChanged += async (_, _) => await YalnizAktivTabYenileAsync();
            Sales.SalesChanged += async (_, _) => await YalnizAktivTabYenileAsync();

            CreditTransactions.TransactionsChanged += async (_, _) => await YalnizAktivTabYenileAsync();
            Credits.CreditsChanged += async (_, _) => await YalnizAktivTabYenileAsync();
            CreditTransactions.DataChanged += async (_, _) => await YalnizAktivTabYenileAsync();
            SalesArchive.DataChanged += async (_, _) => await YalnizAktivTabYenileAsync();

            // --- ÖZ dəyişikliklərimizi qeyd edirik ---------------------------------
            //  Tətbiq bazaya yazanda fayl dəyişir və izləyici hadisə yaradır.
            //  Həmin anda cədvəllər artıq yeniləndiyi üçün ikinci dəfə yeniləmək
            //  lazım deyil — bu işarələr həmin hadisəni udur.
            CarPark.CarsChanged += (_, _) => MarkSelfChange();
            Expenses.ExpensesChanged += (_, _) => MarkSelfChange();
            Sales.SalesChanged += (_, _) => MarkSelfChange();
            Credits.CreditsChanged += (_, _) => MarkSelfChange();
            CreditTransactions.DataChanged += (_, _) => MarkSelfChange();
            SalesArchive.DataChanged += (_, _) => MarkSelfChange();
        }

        /// <summary>Növbəti <see cref="SelfChangeWindow"/> müddətində xarici hadisələri udur.</summary>
        private void MarkSelfChange()
            => _ignoreExternalUntilUtc = DateTime.UtcNow + SelfChangeWindow;

        /// <summary>
        /// Veb server (və ya başqa proses) bazanı dəyişdikdə işə düşür.
        /// Hadisə fon axınında gəlir — UI axınına keçib bütün tabları yeniləyirik.
        /// </summary>
        private void OnExternalDataChanged(object? sender, EventArgs e)
        {
            if (DateTime.UtcNow < _ignoreExternalUntilUtc)
            {
                return;   // bu, tətbiqin öz dəyişikliyidir
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;

            if (dispatcher is null)
            {
                return;
            }

            _ = dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await RefreshAllAsync();
                    StatusMessage = $"Veb-dəki dəyişiklik yeniləndi — {DateTime.Now:HH:mm:ss}";
                }
                catch (Exception ex)
                {
                    StatusMessage = "Yeniləmə xətası: " + ex.Message;
                }
            });
        }

        /// <summary>
        /// 🚀 <b>XARİCİ DƏYİŞİKLİK (veb server) → YALNIZ GÖRÜNƏN TAB</b> ✓✓✓
        /// <para>
        /// ⚠ ƏVVƏL 8 ViewModel-in HAMISI yenidən yüklənirdi ✗
        /// (milyon sətirli bazada bu, 30+ saniyəlik DONMA demək idi ✗✓✓)
        /// </para>
        /// <para>
        /// ✅ İNDİ: görünən tab dərhal ✓, qalanlar «çirkli» ✓ →
        /// istifadəçi keçəndə yüklənir ✓
        /// </para>
        /// </summary>
        private async Task RefreshAllAsync()
        {
            if (!IsInitialized)
            {
                return;   // ilk yükləmə hələ bitməyib
            }

            await YalnizAktivTabYenileAsync();
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (IsInitialized)
            {
                return;
            }

            StatusMessage = "Məlumatlar yüklənir...";

            // ================================================================
            //  🚀 SÜRƏTLİ AÇILIŞ ✓✓✓  (ƏVVƏL: 8 tab BİRDƏN yüklənirdi ✗)
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL proqram açılanda 8 ViewModel-in HAMISI öz cədvəlini
            //     başdan oxuyurdu ✗ → məlumat çox olanda pəncərə 10-60 saniyə
            //     DONURDU ✗✓✓ («birinci girəndə donur» şikayətinin səbəbi ✗)
            //
            //  ✅ İNDİ: yalnız AÇILAN tab (Avto Park) + xərc cədvəlinin
            //     1-ci səhifəsi yüklənir ✓ (~50-200 ms ✓)
            //     Qalan tablar «çirkli» işarələnir ✓ — istifadəçi keçəndə
            //     yüklənir ✓✓✓
            // ================================================================
            await CarPark.LoadAsync();
            await Expenses.LoadAsync();

            // ⏳ Qalan tablar: indi yox — istifadəçi keçəndə yüklənəcək ✓
            //   (19 tab var ✓ — 0 və 1 artıq yüklənib ✓ · 🔔 Bildirişlər ✓ 🗓️ Təqvim ✓ daxil)
            for (var i = 2; i < 19; i++)
            {
                _çirkliTablar.Add(i);
            }

            Calculator.HesablaCommand.Execute(null);

            IsInitialized = true;

            // 📊 Status: cədvəldəki ÜMUMİ qeyd sayı göstərilir ✓ (səhifə sayı deyil ✗✓✓)
            StatusMessage = $"Hazır — {CarPark.TotalCars} avtomobil, {Expenses.UmumiSay} xərc qeydi.";

            // Veb server AVTOMATİK işə salınır (brauzerde ve telefonlarda giris ucun).
            await Web.AutoStartAsync();
        }
    }
}
