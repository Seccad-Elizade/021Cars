namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Kredit hesablamaları üçün <b>VAHİD DÜSTURLAR</b>.
    /// <para>
    /// <b>AYLIK ÖDƏNİŞ = Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət(ay)</b>
    /// </para>
    /// <para>
    /// Yəni faiz dərəcəsi əsas borcun <b>ÜSTÜNƏ BİR DƏFƏ</b> əlavə olunur, sonra
    /// bütün müddətə bərabər bölünür — hər ay eyni məbləğ ödənilir.
    /// (Əvvəllər «annuitet» düsturu işlədilirdi və aylıq ödəniş SƏHV çıxırdı.)
    /// </para>
    /// <example>
    /// Kreditləşdirilən 17 014 ₼ · Faiz 40% · Müddət 12 ay:
    /// <code>
    /// Faiz məbləği : 17 014 × 40%   =  6 805,60 ₼
    /// Faizli cəm   : 17 014 + 6 805,60 = 23 819,60 ₼
    /// Aylıq ödəniş : 23 819,60 ÷ 12 =  1 984,97 ₼   ✔
    /// </code>
    /// </example>
    /// </summary>
    public static class CreditMath
    {
        /// <summary>Faizin əsas borca nisbəti (məs. 40 → 0,40).</summary>
        public static decimal RateFactor(decimal faizDerecesi)
            => Math.Max(0m, faizDerecesi) / 100m;

        // ====================================================================
        //  «QRAFİK BÖLGÜSÜ» ƏMSALI  (maşının qiymətini hesablamaq üçün)
        // --------------------------------------------------------------------
        //  Satıcı deyir: «12 ay, aylıq 1 985 ₼, ilkin 5 000 ₼».
        //  Buradan maşının qiyməti tapılır:
        //
        //      Maşının qiyməti = (Aylıq × Ay) ÷ Əmsal + İlkin ödəniş
        //
        //  Nümunə: (12 × 1 985) ÷ 1.4 + 5 000 = 17 014,29 + 5 000 = 22 014 ₼
        // ====================================================================

        /// <summary>ƏMSAL = (Aylıq × Ay) ÷ Kreditləşdirilən.</summary>
        public static decimal FactorFrom(decimal aylıqOdenis, int ay, decimal kreditlesdirilen)
            => kreditlesdirilen <= 0m || ay <= 0
                ? 0m
                : Math.Round(aylıqOdenis * ay / kreditlesdirilen, 4);

        /// <summary>KREDİTLƏŞDİRİLƏN = (Aylıq × Ay) ÷ Əmsal.</summary>
        public static decimal PrincipalFromMonthly(decimal aylıqOdenis, int ay, decimal emsal)
            => emsal <= 0m || ay <= 0
                ? 0m
                : Sinirla(aylıqOdenis * ay / emsal);

        /// <summary>
        /// <b>MAŞININ QİYMƏTİ</b> = <c>(Aylıq × Ay) ÷ Əmsal + İlkin ödəniş</c>.
        /// </summary>
        public static decimal CarPriceFromMonthly(decimal aylıqOdenis, int ay, decimal emsal, decimal ilkinOdenis)
            => Sinirla(PrincipalFromMonthly(aylıqOdenis, ay, emsal) + Math.Max(0m, ilkinOdenis));

        /// <summary>AYLIQ ÖDƏNİŞ (əmsalla) = Kreditləşdirilən × Əmsal ÷ Ay.</summary>
        public static decimal MonthlyFromFactor(decimal kreditlesdirilen, decimal emsal, int ay)
            => ay <= 0
                ? Sinirla(Math.Max(0m, kreditlesdirilen))
                : Math.Round(Math.Max(0m, kreditlesdirilen) * Math.Max(0m, emsal) / ay, 2);

        /// <summary>Əmsalı faiz dərəcəsinə çevirir (1,4 → 40%).</summary>
        public static decimal FactorToPercent(decimal emsal)
            => Math.Round(Math.Max(0m, emsal - 1m) * 100m, 2);

        /// <summary>Faiz dərəcəsini əmsala çevirir (40 → 1,4).</summary>
        public static decimal PercentToFactor(decimal faizDerecesi)
            => Math.Round(1m + Math.Max(0m, faizDerecesi) / 100m, 4);

        /// <summary>
        /// Kredit üzrə ÜMUMİ (faizli) ödəniləcək məbləğ:
        /// <c>əsas borc × (1 + faiz% ÷ 100)</c>.
        /// </summary>
        public static decimal TotalPayable(decimal principal, decimal faizDerecesi)
            => Sinirla(Math.Max(0m, principal) * (1m + RateFactor(faizDerecesi)));

        /// <summary>
        /// <b>AYLIK ÖDƏNİŞ</b> = <c>Kreditləşdirilən × (1 + Faiz% ÷ 100) ÷ Müddət</c>.
        /// </summary>
        public static decimal MonthlyPayment(decimal principal, decimal faizDerecesi, int muddetAy)
        {
            if (muddetAy <= 0)
            {
                return Sinirla(Math.Max(0m, principal));
            }

            return Math.Round(TotalPayable(principal, faizDerecesi) / muddetAy, 2);
        }

        /// <summary>Ümumi FAİZ məbləği = ümumi ödəniş − əsas borc.</summary>
        public static decimal TotalInterest(decimal principal, decimal faizDerecesi)
            => Sinirla(TotalPayable(principal, faizDerecesi) - Math.Max(0m, principal));

        /// <summary>
        /// Ümumi faizin bütün ödənişlər içindəki payı (0–1).
        /// Faizsiz kreditlərdə 0 qaytarır.
        /// </summary>
        public static decimal InterestShare(decimal principal, decimal faizDerecesi)
        {
            if (principal <= 0m || faizDerecesi <= 0m)
            {
                return 0m;
            }

            var total = TotalPayable(principal, faizDerecesi);
            return total <= principal ? 0m : (total - principal) / total;
        }

        /// <summary>Nəticəni decimal sərhədləri içində saxlayır (çökmənin qarşısını alır).</summary>
        public static decimal Sinirla(decimal value)
        {
            const decimal limit = 1000000000000m;   // 1 trilyon

            if (value > limit)
            {
                return limit;
            }

            return value < -limit ? -limit : Math.Round(value, 2);
        }
    }
}
