using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// "Satış &amp; Kredit Kalkulyatoru" tabının ViewModel-i.
    /// <para>
    /// Aylıq ödəniş düsturu: <see cref="CreditMath.MonthlyPayment"/> —
    /// <c>Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət</c>.
    /// </para>
    /// </summary>
    public sealed partial class LoanCalculatorViewModel : ObservableValidator
    {
        [ObservableProperty]
        [Range(typeof(decimal), "1", "999999999", ErrorMessage = "Satış məbləği 0-dan böyük olmalıdır.")]
        private decimal mebleg = 20000m;

        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999", ErrorMessage = "İlkin ödəniş mənfi ola bilməz.")]
        private decimal ilkinOdenis = 4000m;

        [ObservableProperty]
        [Range(typeof(decimal), "0", "1000000000", ErrorMessage = "Faiz dərəcəsi mənfi ola bilməz.")]
        private decimal faizDerecesi = 12m;

        [ObservableProperty]
        [Range(1, 120, ErrorMessage = "Müddət 1-120 ay aralığında olmalıdır.")]
        private int muddetAy = 24;

        [ObservableProperty] private decimal kreditMeblegi;
        [ObservableProperty] private decimal aylıqOdenis;
        [ObservableProperty] private decimal umumiOdenis;
        [ObservableProperty] private decimal umumiFaiz;
        [ObservableProperty] private bool hesablanib;

        // ====================================================================
        //  TƏRS HESABLAMA — satıcı deyir, biz MAŞININ QİYMƏTİNİ tapırıq
        // --------------------------------------------------------------------
        //  Satıcı: «12 ay, aylıq 1 985 ₼, ilkin 5 000 ₼» deyir.
        //
        //      MAŞININ QİYMƏTİ = (Aylıq × Ay) ÷ Əmsal + İlkin ödəniş
        //
        //  Nümunə: (12 × 1 985) ÷ 1.4 + 5 000 = 17 014,29 + 5 000 = 22 014 ₼
        //
        //  Əmsal (qrafik bölgüsü) müddətə görə AVTOMATİK yazılır:
        //      6 ay → 1.2   12 → 1.4   15 → 1.5   18 → 1.6
        //      24 → 1.8     30 → 2.0   36 → 2.2
        //  Lakin istifadəçi onu ƏL İLƏ də dəyişə bilər (razılaşılan faizdir).
        // ====================================================================

        /// <summary>Satıcının dediyi AYLIQ ÖDƏNİŞ (₼).</summary>
        [ObservableProperty] private decimal satisciAylıq = 1985m;

        /// <summary>Satıcının dediyi MÜDDƏT (ay).</summary>
        [ObservableProperty] private int satisciAy = 12;

        /// <summary>Satıcının dediyi İLKİN ÖDƏNİŞ (₼).</summary>
        [ObservableProperty] private decimal satisciIlkin = 5000m;

        /// <summary>
        /// «QRAFİK BÖLGÜSÜ» ƏMSALI — müddətə görə avtomatik yazılır,
        /// lakin ƏL İLƏ dəyişdirilə bilər (razılaşılan faiz dərəcəsidir).
        /// </summary>
        [ObservableProperty] private decimal emsal = 1.4m;

        /// <summary>Tərs hesablamanın nəticəsi — MAŞININ QİYMƏTİ (₼).</summary>
        [ObservableProperty] private decimal masinQiymeti;

        /// <summary>Tərs hesablamanın nəticəsi — KREDİTLƏŞDİRİLƏN (₼).</summary>
        [ObservableProperty] private decimal masinKredit;

        /// <summary>Əmsala uyğun faiz dərəcəsi: 1,4 → 40%.</summary>
        public string EmsalFaiz => $"≈ {CreditMath.FactorToPercent(Emsal):0.##}% faiz";

        /// <summary>Əmsal cədvəlinin mətni (izah üçün).</summary>
        public string EmsalCedveli => string.Join(" · ",
            Catalog.CreditFactors.Select(f => $"{f.Ay} ay → {f.Emsal:0.0}"));

        /// <summary>Müddət dəyişdi — əmsal AVTOMATİK yenilənir.</summary>
        partial void OnSatisciAyChanged(int value)
        {
            Emsal = Catalog.CreditFactorFor(value);
            TersHesabla();
        }

        partial void OnSatisciAylıqChanged(decimal value) => TersHesabla();

        partial void OnSatisciIlkinChanged(decimal value) => TersHesabla();

        partial void OnEmsalChanged(decimal value)
        {
            OnPropertyChanged(nameof(EmsalFaiz));
            TersHesabla();
        }

        /// <summary>
        /// TƏRS HESABLAMA: <c>Maşının qiyməti = (Aylıq × Ay) ÷ Əmsal + İlkin</c>.
        /// </summary>
        [RelayCommand]
        private void TersHesabla()
        {
            MasinKredit = CreditMath.PrincipalFromMonthly(SatisciAylıq, SatisciAy, Emsal);
            MasinQiymeti = CreditMath.CarPriceFromMonthly(SatisciAylıq, SatisciAy, Emsal, SatisciIlkin);
        }

        /// <summary>Tərs hesablamanın nəticəsini əsas kalkulyatora köçürür.</summary>
        [RelayCommand]
        private void TersNeticeKocur()
        {
            TersHesabla();

            if (MasinQiymeti <= 0m)
            {
                return;
            }

            Mebleg = MasinQiymeti;
            IlkinOdenis = SatisciIlkin;
            MuddetAy = SatisciAy;
            FaizDerecesi = CreditMath.FactorToPercent(Emsal);

            Hesabla();
        }

        [RelayCommand]
        private void Hesabla()
        {
            ValidateAllProperties();
            if (HasErrors)
            {
                return;
            }

            var principal = Math.Max(0m, Mebleg - IlkinOdenis);
            KreditMeblegi = principal;

            // ================================================================
            //  AYLIK ÖDƏNİŞ = Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət
            // ----------------------------------------------------------------
            //  Faiz əsas borcun ÜSTÜNƏ BİR DƏFƏ əlavə olunur, sonra bütün
            //  müddətə bərabər bölünür.
            //
            //  Nümunə: 17 014 ₼ · 40% · 12 ay
            //      17 014 × 1,40 = 23 819,60 ₼
            //      23 819,60 ÷ 12 = 1 984,97 ₼   ← aylıq ödəniş
            // ================================================================
            AylıqOdenis = CreditMath.MonthlyPayment(principal, FaizDerecesi, MuddetAy);
            UmumiOdenis = CreditMath.TotalPayable(principal, FaizDerecesi);
            UmumiFaiz = CreditMath.TotalInterest(principal, FaizDerecesi);
            Hesablanib = true;
        }

        [RelayCommand]
        private void Sifirla()
        {
            Mebleg = 20000m;
            IlkinOdenis = 4000m;
            FaizDerecesi = 12m;
            MuddetAy = 24;
            KreditMeblegi = 0m;
            AylıqOdenis = 0m;
            UmumiOdenis = 0m;
            UmumiFaiz = 0m;
            Hesablanib = false;
            ClearErrors();
        }
    }
}
