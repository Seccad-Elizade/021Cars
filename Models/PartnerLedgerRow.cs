namespace EnterpriseAeroStudio.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// <b>TƏRƏFDAŞ BÖLGÜ JURNALI</b> sətri — BÜTÜN bölmələrdən avtomatik toplanan
    /// tək pay qeydi.
    /// <para>
    /// Mənbələr:
    /// <list type="bullet">
    ///   <item><b>💰 Satış</b> — maşın satıldı, mənfəət bölündü.</item>
    ///   <item><b>💳 Kredit</b> — maşın kreditə verildi, mənfəət bölündü.</item>
    ///   <item><b>🏷️ Kredit Əlavə Gəlir</b> — kredit üzrə əlavə gəlir bölündü.</item>
    /// </list>
    /// Beləliklə «hansı maşından, hansı ayda, kimə nə qədər gəldi» sualı
    /// tək cədvəldə cavablanır.
    /// </para>
    /// </summary>
    public sealed class PartnerLedgerRow
    {
        /// <summary>Payın mənbəyi: «Satış», «Kredit», «Kredit Əlavə Gəlir».</summary>
        public string Menbe { get; set; } = string.Empty;

        /// <summary>Mənbə nişanı (cədvəldə göstərilən ikonlu mətn).</summary>
        public string MenbeNisani => Menbe switch
        {
            "Satış" => "💰 Satış",
            "Kredit" => "💳 Kredit",
            "Kredit Əlavə Gəlir" => "🏷️ Kredit Gəliri",
            // ⚠️ GECİKMƏ CƏRİMƏSİ — ayrı mənbə ✓✓✓ (50/50 Asif & Musa)
            "Gecikmə" => "⚠️ Gecikmə cəriməsi",
            _ => Menbe
        };

        /// <summary>Tərəfdaşın adı.</summary>
        public string Terefdas { get; set; } = string.Empty;

        /// <summary>Bölgünün aid olduğu avtomobil: marka + dövlət nömrəsi.</summary>
        public string Avtomobil { get; set; } = string.Empty;

        /// <summary>Müqavilə / sənəd nömrəsi.</summary>
        public string MuqavileNomresi { get; set; } = string.Empty;

        /// <summary>Müştəri (alıcı) adı.</summary>
        public string Musteri { get; set; } = string.Empty;

        /// <summary>Bölgünün tarixi (satış / kredit / əməliyyat tarixi).</summary>
        public DateTime Tarix { get; set; } = DateTime.Today;

        /// <summary>Bölgünün bazası («xeyir») — bölünən məbləğ (₼).</summary>
        public decimal Baza { get; set; }

        /// <summary>Tərəfdaşın faiz dərəcəsi (%).</summary>
        public decimal Faiz { get; set; }

        /// <summary>Qalıq payçısıdırmı?</summary>
        public bool QaligPayi { get; set; }

        /// <summary>Tərəfdaşa düşən pay (₼).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>Faiz sütunu mətni: «%6» və ya «qalıq».</summary>
        public string FaizMetni => QaligPayi || Faiz <= 0m ? "qalıq" : $"%{Faiz:0.##}";

        /// <summary>Məbləğ sütunu mətni: «95,66 ₼».</summary>
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        /// <summary>Baza sütunu mətni: «1 594,00 ₼».</summary>
        public string BazaMetni => $"{Baza:N2} ₼";

        /// <summary>Tarix sütunu mətni: «21.09.2026».</summary>
        public string TarixMetni => Tarix.ToString("dd.MM.yyyy");

        /// <summary>Dövr sütunu mətni: «Sentyabr 2026».</summary>
        public string AyMetni => PartnerMonthlyRow.AyAdi(Tarix);

        /// <summary>İl (dövr filtri üçün).</summary>
        [NotMapped]
        public int Il => Tarix.Year;

        /// <summary>Ay nömrəsi 1–12 (dövr filtri üçün).</summary>
        [NotMapped]
        public int Ay => Tarix.Month;

        /// <summary>Sənəd sütunu mətni: müqavilə yoxdursa müştəri, o da yoxdursa «—».</summary>
        public string SenedMetni
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(MuqavileNomresi))
                {
                    return MuqavileNomresi;
                }
                return string.IsNullOrWhiteSpace(Musteri) ? "—" : Musteri;
            }
        }

        public override string ToString()
            => $"{TarixMetni} · {MenbeNisani} · {Avtomobil} · {Terefdas} — {MeblegMetni}";
    }
}
