namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// Tərəfdaşın <b>AYLIQ DÖVRİYYƏSİ</b> — «hansı ayda nə qədər gəlib».
    /// <para>
    /// Bir sətir = bir tərəfdaş + bir ay. «👥 Tərəfdaşlar» tabında
    /// aylıq cədvəldə göstərilir ki, hansı ayda kimin nə qədər pay aldığı
    /// dərhal görünsün.
    /// </para>
    /// </summary>
    public sealed class PartnerMonthlyRow
    {
        /// <summary>Azərbaycandilli ay adları (1 → «Yanvar»).</summary>
        public static readonly string[] AyAdlari =
        {
            "Yanvar", "Fevral", "Mart", "Aprel", "May", "İyun",
            "İyul", "Avqust", "Sentyabr", "Oktyabr", "Noyabr", "Dekabr"
        };

        /// <summary>Qısa ay adları (1 → «Yan»).</summary>
        public static readonly string[] AyQisaAdlari =
        {
            "Yan", "Fev", "Mar", "Apr", "May", "İyn",
            "İyl", "Avq", "Sen", "Okt", "Noy", "Dek"
        };

        /// <summary>Ay nömrəsi + il → «Sentyabr 2026».</summary>
        public static string AyAdi(DateTime tarix)
            => $"{AyAdlari[tarix.Month - 1]} {tarix.Year}";

        /// <summary>Tərəfdaşın adı.</summary>
        public string Terefdas { get; set; } = string.Empty;

        /// <summary>İl.</summary>
        public int Il { get; set; }

        /// <summary>Ay nömrəsi (1–12).</summary>
        public int Ay { get; set; }

        /// <summary>«Sentyabr 2026».</summary>
        public string AyMetni => $"{AyAdlari[Ay - 1]} {Il}";

        /// <summary>«09.2026» — qısa dövr nişanı.</summary>
        public string DonemMetni => $"{Ay:00}.{Il}";

        /// <summary>Həmin ay tərəfdaşın pay aldığı bölgü sayı.</summary>
        public int BolguSayi { get; set; }

        /// <summary>Həmin ayda tərəfdaşa düşən ÜMUMİ pay (₼).</summary>
        public decimal Qazanilmis { get; set; }

        /// <summary>Həmin ayda tərəfdaşa VERİLƏN pul (₼).</summary>
        public decimal Odenilmis { get; set; }

        /// <summary>Həmin ayın qalığı: qazanılmış − verilmiş.</summary>
        public decimal Qaliq => Qazanilmis - Odenilmis;

        /// <summary>Cədvəl üçün məbləğ mətni.</summary>
        public string QazanilmisMetni => $"{Qazanilmis:N2} ₼";

        /// <summary>Cədvəl üçün ödəniş mətni.</summary>
        public string OdenilmisMetni => $"{Odenilmis:N2} ₼";

        public override string ToString() => $"{Terefdas} · {AyMetni} — {QazanilmisMetni}";
    }
}
