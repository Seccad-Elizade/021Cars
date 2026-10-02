namespace EnterpriseAeroStudio.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// Tərəfdaşa <b>XARİC EDİLƏN / VERİLƏN</b> pul (bölgü payının ödənişi).
    /// <para>
    /// Tərəfdaşların bölgüdən qazandığı məbləğ ayrı hesablanır; onlara
    /// <b>faktiki nə qədər pul verildiyi</b> bu qeydlərlə izlənilir:
    /// </para>
    /// <code>
    /// Qazanılmış  : 12 500,00 ₼
    /// Verilmiş    :  9 000,00 ₼
    /// Qalıq       :  3 500,00 ₼   ← tərəfdaşa hələ verilməli olan məbləğ
    /// </code>
    /// </summary>
    public class PartnerPayment : IBuludIdli
    {
        public int Id { get; set; }

        /// <summary>🔑 Qlobal unikal bulud açarı (GUID ✓ v6.2.16). Köhnə qeydlərdə boş ✗ → rəqəm ID işlədilir ✓</summary>
        public string? BuludId { get; set; }

        /// <summary>Pul verilən tərəfdaşın adı.</summary>
        public string Terefdas { get; set; } = string.Empty;

        /// <summary>Verilən məbləğ (₼).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>Ödəniş tarixi.</summary>
        public DateTime Tarix { get; set; } = DateTime.Today;

        /// <summary>Ödəniş üsulu (Nağd, Kart / Köçürmə).</summary>
        public string OdenisUsulu { get; set; } = string.Empty;

        /// <summary>Əlavə qeyd (məs. «sentyabr payı», «avans»).</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Cədvəl üçün məbləğ mətni: «1 500,00 ₼».</summary>
        [NotMapped]
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        /// <summary>Cədvəl üçün tarix mətni: «21.09.2026».</summary>
        [NotMapped]
        public string TarixMetni => Tarix.ToString("dd.MM.yyyy");

        /// <summary>Cədvəl üçün dövr mətni: «Sentyabr 2026».</summary>
        [NotMapped]
        public string AyMetni => PartnerMonthlyRow.AyAdi(Tarix);

        /// <summary>ComboBox kimi yerlərdə sinif adı yerinə düzgün mətn göstərilsin.</summary>
        public override string ToString() => $"{TarixMetni} — {Terefdas} — {MeblegMetni}";
    }
}
