namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// Kreditin <b>BİR AYI</b> üzrə tərəfdaş bölgüsü — «hansı ayın ödənişində
    /// kimə nə qədər düşdü».
    /// <para>
    /// Kredit ödənişi zamanı «Kredit əlavə gəlir» qeydi yaranır və onun
    /// bölgüsü (məs. <b>Zaur 95,40 ₼ · Asif 667,80 ₼</b>) bu sətirdə göstərilir.
    /// </para>
    /// </summary>
    public sealed class CreditMonthlyShareRow
    {
        /// <summary>Taksit nömrəsi (neçənci ay).</summary>
        public int Ay { get; set; }

        /// <summary>Ödəniş tarixi.</summary>
        public DateTime Tarix { get; set; } = DateTime.Today;

        /// <summary>Həmin ay üzrə kredit ödənişinin məbləği (₼).</summary>
        public decimal OdenisMeblegi { get; set; }

        /// <summary>Bölünən məbləğ — «xeyir» bazası (₼).</summary>
        public decimal Baza { get; set; }

        /// <summary>Həmin ayın tərəfdaş payları (adlar ilə, sıra ilə).</summary>
        public IReadOnlyList<PartnerShare> Paylar { get; set; } = Array.Empty<PartnerShare>();

        /// <summary>Ayın adı: «1-ci ay · 21.09.2026».</summary>
        public string AyMetni => $"{Ay}-ci ay · {Tarix:dd.MM.yyyy}";

        /// <summary>Kredit ödənişinin məbləği mətni.</summary>
        public string OdenisMetni => $"{OdenisMeblegi:N2} ₼";

        /// <summary>Bölünən məbləğ mətni.</summary>
        public string BazaMetni => $"{Baza:N2} ₼";

        /// <summary>Bölgü sətirləri — hər tərəfdaş AYRI SƏTİRDƏ (cədvəl üçün).</summary>
        public string PaylarMetni => Paylar.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, Paylar.Select(p => $"{p.Terefdas} — {p.Mebleg:N2} ₼"));

        /// <summary>Bir sətirdə qısa bölgü mətni: «Zaur 95,40 ₼ · Asif 667,80 ₼».</summary>
        public string PaylarQisa => Paylar.Count == 0
            ? "—"
            : string.Join(" · ", Paylar.Select(p => $"{p.Terefdas} {p.Mebleg:N2} ₼"));

        /// <summary>Payların cəmi (₼).</summary>
        public decimal PaylarCemi => Paylar.Sum(p => p.Mebleg);

        /// <summary>Payların cəmi mətni.</summary>
        public string PaylarCemiMetni => $"{PaylarCemi:N2} ₼";

        public override string ToString() => $"{AyMetni} — {PaylarQisa}";
    }
}
