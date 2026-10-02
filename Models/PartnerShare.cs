namespace EnterpriseAeroStudio.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// Kredit əməliyyatı üzrə <b>TƏRƏFDAŞ PAYI</b>.
    /// <para>
    /// Maşından gələn mənfəət («xeyir») tərəfdaşlar arasında bölünür:
    /// <code>
    /// Mənfəət : 1 590,00 ₼
    /// Zaur    : %6      →    95,40 ₼   (faiz payı)
    /// Eşqin   : %5      →    79,50 ₼   (faiz payı)
    /// Asiman  : %5      →    79,50 ₼   (faiz payı)
    /// Asif    : qalıq÷2 →   667,80 ₼   (qalıq payı)
    /// Musa    : qalıq÷2 →   667,80 ₼   (qalıq payı)
    /// </code>
    /// Faiz dərəcələri <b>manual</b> dəyişdirilə bilər; standart (default)
    /// dəyərlər <see cref="Services.Catalog.DefaultPartners"/> siyahısındadır.
    /// </para>
    /// </summary>
    public class PartnerShare : IBuludIdli
    {
        public int Id { get; set; }

        /// <summary>🔑 Qlobal unikal bulud açarı (GUID ✓ v6.2.16). Köhnə qeydlərdə boş ✗ → rəqəm ID işlədilir ✓</summary>
        public string? BuludId { get; set; }

        /// <summary>Payın aid olduğu kredit əməliyyatı (əlavə gəlir / xərc).</summary>
        public int? CreditTransactionId { get; set; }

        public CreditTransaction? CreditTransaction { get; set; }

        /// <summary>
        /// Payın aid olduğu <b>KREDİT MÜQAVİLƏSİ</b>.
        /// <para>
        /// Maşın KREDİTƏ VERİLƏNDƏ onun mənfəəti
        /// (<c>Satış qiyməti − Maya dəyəri</c>) də tərəfdaşlar arasında bölünür.
        /// Belə paylar bu sahə ilə kreditə bağlanır.
        /// </para>
        /// </summary>
        public int? CreditId { get; set; }

        public Credit? Credit { get; set; }

        /// <summary>
        /// Payın aid olduğu <b>SATIŞ</b>.
        /// <para>
        /// Maşın SATILANDA onun mənfəəti
        /// (<c>Satış qiyməti − Maya dəyəri</c>) də tərəfdaşlar arasında bölünür.
        /// </para>
        /// </summary>
        public int? SaleId { get; set; }

        public Sale? Sale { get; set; }

        /// <summary>Tərəfdaşın adı (Zaur, Eşqin, Asiman, Asif, Musa).</summary>
        public string Terefdas { get; set; } = string.Empty;

        /// <summary>
        /// Faiz dərəcəsi (%). Yalnız <see cref="QaligPayi"/> <c>false</c> olduqda
        /// istifadə olunur — qalıq payçıları üçün 0 saxlanılır.
        /// </summary>
        public decimal Faiz { get; set; }

        /// <summary>Payın hesablanmış (və ya manual düzəldilmiş) məbləği (₼).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>
        /// <c>true</c> — bu tərəfdaş faiz payları çıxıldıqdan sonra QALAN məbləği
        /// digər qalıq payçıları ilə bərabər bölür (Asif &amp; Musa: yarı-yarıya).
        /// </summary>
        public bool QaligPayi { get; set; }

        /// <summary>
        /// <b>TƏTBİQ OLUNUR?</b> — adın qabağındaki checkbox.
        /// <para>
        /// <c>false</c> olduqda bu tərəfdaşın <b>FAİZİ HESABLANMIR</b> və pay
        /// ona verilmir; qalıq payçısıdırsa qalığın bölünməsində də iştirak etmir.
        /// Beləliklə məsələn yalnız Zaur, Asiman və Asif işarələnibsə — yalnız
        /// onların faizi tətbiq olunur, Eşqin və Musa tamamilə kənarda qalır.
        /// </para>
        /// </summary>
        public bool Aktiv { get; set; } = true;

        /// <summary>Cədvəldə göstərilmə sırası.</summary>
        public int Sira { get; set; }

        /// <summary>Cədvəl üçün faiz mətni: «%6» və ya «qalıq».</summary>
        [NotMapped]
        public string FaizMetni => QaligPayi
            ? "qalıq"
            : $"{Faiz:0.##}%";

        /// <summary>Cədvəl üçün məbləğ mətni: «95,40 ₼».</summary>
        [NotMapped]
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        public override string ToString() => $"{Terefdas} — {MeblegMetni}";
    }
}
