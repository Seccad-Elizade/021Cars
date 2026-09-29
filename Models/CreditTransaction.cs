namespace EnterpriseAeroStudio.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// Kreditə bağlı əlavə gəlir/xərc qeydi.
    /// </summary>
    public class CreditTransaction
    {
        public int Id { get; set; }

        public int? CreditId { get; set; }

        public Credit? Credit { get; set; }

        /// <summary>Növ: "Gəlir", "Xərc", "Möhlət" və ya "Gecikmə".</summary>
        public string Nov { get; set; } = string.Empty;

        /// <summary>
        /// "Möhlət" qeydləri üçün: ödənişin gecikdirildiyi SON tarix
        /// (məsələn 10.11.2026 — həmin tarixə qədər ödəniş gecikdirilib).
        /// </summary>
        public DateTime? MohletTarixi { get; set; }

        /// <summary>
        /// "Gecikmə" qeydləri üçün: ödənişin gecikdiyi / bağlandığı tarix.
        /// </summary>
        public DateTime? GecikmeTarixi { get; set; }

        /// <summary>Ödənişin aid olduğu ayın sıra nömrəsi (1-dən başlayır; kredit ödənişləri üçün).</summary>
        public int? InstallmentNo { get; set; }

        /// <summary>Məbləğ (AZN).</summary>
        public decimal Mebleg { get; set; }

        public DateTime Tarix { get; set; } = DateTime.Today;

        /// <summary>
        /// <b>GÖSTƏRİLƏN TARİX</b> — kredit ödənişlərində <b>HƏMİN AYIN plan
        /// tarixi</b> (<c>Kreditin başlama tarixi + taksit nömrəsi</c>).
        /// <para>
        /// <b>Nə üçün lazımdır:</b> əvvəlki versiyalarda ödəniş qeydi yaradılarkən
        /// <see cref="Tarix"/> «bu gün» yazılırdı ✗ — buna görə keçən / gələn ayın
        /// ödənişi tərəfdaş bölgüsündə SƏHV tarixlə görünürdü ✗.
        /// Bu xassə tarixi <b>hesablayır</b> → bütün köhnə qeydlər də düzəlir ✓
        /// </para>
        /// </summary>
        [NotMapped]
        public DateTime GosterilenTarix =>
            InstallmentNo is > 0 && Credit is not null
                ? Credit.BaslamaTarixi.AddMonths(InstallmentNo.Value - 1)   // 1-ci ay = başlama tarixi ✓
                : Tarix;

        /// <summary>Göstərilən tarixin mətni: «21.10.2026».</summary>
        [NotMapped]
        public string GosterilenTarixMetni => GosterilenTarix.ToString("dd.MM.yyyy");

        /// <summary>Göstərilən ayın adı: «Oktyabr 2026».</summary>
        [NotMapped]
        public string GosterilenAyMetni => PartnerMonthlyRow.AyAdi(GosterilenTarix);

        /// <summary>
        /// Qeydin tarixi <b>plan tarixindən fərqlidir</b>? (köhnə «bu gün» qeydi)
        /// <para>Belə qeydlərdə cədvəldə ⚠ nişanı göstərilir.</para>
        /// </summary>
        [NotMapped]
        public bool TarixUyğunsuz =>
            InstallmentNo is > 0 && Credit is not null && Tarix.Date != GosterilenTarix.Date;

        /// <summary>Qısa təsvir.</summary>
        public string Tesvir { get; set; } = string.Empty;

        /// <summary>
        /// "Gecikmə" qeydləri üçün: gecikmiş ödəniş bağlanıb (ödənilib)?
        /// Digər növlərdə istifadə olunmur.
        /// </summary>
        public bool Odenilib { get; set; }

        // ====================================================================
        //  TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ  (yalnız «Gəlir» əməliyyatları üçün)
        // ====================================================================

        /// <summary>
        /// Tərəfdaş mənfəət bölgüsü TƏTBİQ OLUNUB?
        /// <para>
        /// Formadakı <b>checkbox</b> — işarələnməyibsə bölgü hesablanmır və
        /// heç bir tərəfdaş payı saxlanılmır.
        /// </para>
        /// </summary>
        public bool TerefdasBolguTetbiqOlunub { get; set; }

        /// <summary>
        /// Bölgünün bazası (₼) — yəni bölünən mənfəət («xeyir»).
        /// Bölgü tətbiq olunmayıbsa <c>null</c>.
        /// </summary>
        public decimal? BolguBazasi { get; set; }

        /// <summary>Bu əməliyyat üzrə tərəfdaş payları (Zaur, Eşqin, Asiman, Asif, Musa).</summary>
        public List<PartnerShare> TerefdasPaylari { get; set; } = new();

        /// <summary>
        /// Cədvəldə göstərilən qısa bölgü mətni:
        /// «Zaur 95,40 ₼ · Eşqin 79,50 ₼ · Asif 667,80 ₼ · Musa 667,80 ₼».
        /// </summary>
        [NotMapped]
        public string BolguMetni => TerefdasBolguTetbiqOlunub && TerefdasPaylari.Count > 0
            ? string.Join(" · ", TerefdasPaylari
                .OrderBy(p => p.Sira)
                .Select(p => $"{p.Terefdas} {p.Mebleg:N2} ₼"))
            : "—";

        /// <summary>Tərəfdaş bölgüsü tətbiq olunubmu? (UI-də nişan üçün)</summary>
        [NotMapped]
        public bool HasBolgu => TerefdasBolguTetbiqOlunub && TerefdasPaylari.Count > 0;

        /// <summary>Bu qeyd "Gecikmə" növüdürmü? (UI-də ödənilib işarəsini göstərmək üçün)</summary>
        [NotMapped]
        public bool IsGecikme => Nov == "Gecikmə";

        /// <summary>Cədvəldə göstərilən kredit məlumatı: müqavilə | müştəri | avtomobil.</summary>
        [NotMapped]
        public string CreditInfo
        {
            get
            {
                if (Credit is null)
                {
                    return string.Empty;
                }

                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Credit.MuqavileNomresi))
                {
                    parts.Add(Credit.MuqavileNomresi);
                }
                if (!string.IsNullOrWhiteSpace(Credit.Mustəri))
                {
                    parts.Add(Credit.Mustəri);
                }
                if (Credit.Car is not null && !string.IsNullOrWhiteSpace(Credit.Car.DisplayName))
                {
                    parts.Add(Credit.Car.DisplayName);
                }
                return string.Join(" | ", parts);
            }
        }
    }
}
