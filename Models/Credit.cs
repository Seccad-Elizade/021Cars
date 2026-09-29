namespace EnterpriseAeroStudio.Models
{
    using System.ComponentModel.DataAnnotations.Schema;
    using EnterpriseAeroStudio.Services;

    /// <summary>
    /// Avtomobil satışı üzrə kredit müqaviləsini təmsil edir.
    /// </summary>
    public class Credit
    {
        public int Id { get; set; }

        /// <summary>Cədvəldə göstərilən ardıcıl sıra nömrəsi (yalnız görünüş üçün).</summary>
        [NotMapped]
        public int SiraNomresi { get; set; }

        /// <summary>ComboBox kimi yerlərdə sinif adı yerinə düzgün mətn göstərilsin.</summary>
        public override string ToString() => DisplayText;

        /// <summary>Müqavilə nömrəsi.</summary>
        public string MuqavileNomresi { get; set; } = string.Empty;

        /// <summary>Müştəri adı soyadı.</summary>
        public string Mustəri { get; set; } = string.Empty;

        /// <summary>Kreditə bağlı avtomobil.</summary>
        public int? CarId { get; set; }

        public CarItem? Car { get; set; }

        /// <summary>Kreditin əsas məbləği (AZN).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>İlkin ödəniş (avans) məbləği (AZN).</summary>
        public decimal IlkinOdenis { get; set; }

        /// <summary>
        /// Kreditləşdirilən (faktiki borc) məbləğ = Mebleg − İlkin ödəniş.
        /// Ödəniş qrafiki bunun üzərindən hesablanır.
        /// </summary>
        [NotMapped]
        public decimal Kreditlesdirilen => Mebleg - IlkinOdenis;

        /// <summary>
        /// Kredit üzrə ÜMUMİ FAİZ məbləği (₼):
        /// <c>Kreditləşdirilən × Faiz% ÷ 100</c>.
        /// <para>Nümunə: 17 014 ₼ · 40% → <b>6 805,60 ₼</b> faiz.</para>
        /// </summary>
        [NotMapped]
        public decimal FaizMeblegi => CreditMath.TotalInterest(Kreditlesdirilen, FaizDerecesi);

        /// <summary>
        /// KREDİTİN QİYMƏTİ — müştərinin cəmi ödəyəcəyi məbləğ (₼):
        /// <c>Kreditləşdirilən + Faiz</c> = <c>Aylıq × Müddət</c>.
        /// </summary>
        [NotMapped]
        public decimal KreditQiymeti => CreditMath.TotalPayable(Kreditlesdirilen, FaizDerecesi);

        /// <summary>
        /// «Kreditləşdirilən» xanasının altında göstərilən FAİZ + QİYMƏT mətni:
        /// «Faiz 6 805,60 ₼ · Qiyməti 23 819,60 ₼».
        /// </summary>
        [NotMapped]
        public string FaizVeQiymetMetni =>
            $"Faiz {FaizMeblegi:N2} ₼  ·  Qiyməti {KreditQiymeti:N2} ₼";

        /// <summary>«Aylıq × Müddət» yoxlaması: «1 984,97 ₼ × 12 ay».</summary>
        [NotMapped]
        public string AylıqVeMuddetMetni =>
            MuddetAy <= 0 ? "—" : $"{AylıqOdenis:N2} ₼ × {MuddetAy} ay";

        /// <summary>
        /// KREDİT BİTİBMİ? — bütün məbləğ ödənilibsə <c>true</c>.
        /// <para>
        /// <paramref name="odenilmis"/> — bu kreditə aid toplanmış ödənişlərin cəmi.
        /// Kiçik yuvarlaqlaşdırma fərqi (0,50 ₼) nəzərə alınır.
        /// </para>
        /// </summary>
        public bool Bitibmi(decimal odenilmis)
            => KreditQiymeti > 0m && odenilmis >= KreditQiymeti - 0.50m;

        /// <summary>Kredit bitmiş sayılır? (status «Bağlı» və ya müddət tamamlanıb)</summary>
        [NotMapped]
        public bool BitmisKredit =>
            string.Equals(Status, "Bağlı", StringComparison.OrdinalIgnoreCase)
            || (MuddetAy > 0 && BaslamaTarixi.AddMonths(MuddetAy) <= DateTime.Today);

        /// <summary>Kreditin neçə faizi ödənilib (0–100).</summary>
        public decimal OdenisFaizi(decimal odenilmis)
            => KreditQiymeti <= 0m ? 0m : Math.Min(100m, Math.Round(odenilmis / KreditQiymeti * 100m, 1));

        /// <summary>İllik faiz dərəcəsi (%).</summary>
        public decimal FaizDerecesi { get; set; }

        /// <summary>Müddət (ay).</summary>
        public int MuddetAy { get; set; }

        /// <summary>Aylıq ödəniş (AZN).</summary>
        public decimal AylıqOdenis { get; set; }

        /// <summary>Kreditin başlama tarixi.</summary>
        public DateTime BaslamaTarixi { get; set; } = DateTime.Today;

        /// <summary>Status (Aktiv, Bağlı, Gecikmiş).</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Əlavə qeyd.</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>
        /// ComboBox-larda göstərilən tam məlumat:
        /// "M-0001 | Rebbil | 77HH717 | Mercedes C300".
        /// </summary>
        [NotMapped]
        public string DisplayText
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(MuqavileNomresi))
                {
                    parts.Add(MuqavileNomresi);
                }
                if (!string.IsNullOrWhiteSpace(Mustəri))
                {
                    parts.Add(Mustəri);
                }
                if (Car is not null && !string.IsNullOrWhiteSpace(Car.QeydiyyatNisani))
                {
                    parts.Add(Car.QeydiyyatNisani);
                }
                if (Car is not null && !string.IsNullOrWhiteSpace(Car.Marka))
                {
                    parts.Add(Car.Marka);
                }
                return string.Join(" | ", parts);
            }
        }
    }
}
