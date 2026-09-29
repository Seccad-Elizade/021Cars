namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// MALİYYƏ PANELİ üçün tərəfdaş bölgüsü cəmi.
    /// <para>
    /// Seçilmiş DÖVR ərzində hər tərəfdaşa (Zaur, Eşqin, Asiman, Asif, Musa)
    /// nə qədər pay düşdüyünü göstərir — adları ilə AYRI-AYRI.
    /// </para>
    /// </summary>
    public sealed class PartnerTotal
    {
        /// <summary>Tərəfdaşın adı.</summary>
        public string Terefdas { get; set; } = string.Empty;

        /// <summary>Dövr ərzində tərəfdaşa düşən ÜMUMİ pay (₼).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>Dövr ərzində bu tərəfdaşın pay aldığı bölgü sayı.</summary>
        public int Sayi { get; set; }

        /// <summary>Standart faiz dərəcəsi (%) — qalıq payçılarında 0.</summary>
        public decimal Faiz { get; set; }

        /// <summary>Qalıq payçısıdırmı? (Asif &amp; Musa)</summary>
        public bool QaligPayi { get; set; }

        /// <summary>Cədvəldə göstərilən məbləğ mətni.</summary>
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        /// <summary>Faiz sütunu mətni: «%6», «%5» və ya «qalıq».</summary>
        public string FaizMetni => QaligPayi || Faiz <= 0m ? "qalıq" : $"%{Faiz:0.##}";

        /// <summary>Sətir mətni (çap / ixrac üçün): «Zaur — 95,40 ₼».</summary>
        public string Display => $"{Terefdas} — {MeblegMetni}";

        public override string ToString() => Display;
    }
}
