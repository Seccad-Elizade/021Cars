namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// <b>TƏRƏFDAŞ KARTI</b> — bir tərəfdaşın seçilmiş dövr üzrə tam maliyyə
    /// mənzərəsi («umumi dövriyyəsi»).
    /// <para>
    /// <code>
    /// Qazanılmış (bölgüdən)  : 12 500,00 ₼   ← satış + kredit + əlavə gəlir
    /// Verilmiş  (xaric edilən):  9 000,00 ₼
    /// QALIQ                   :  3 500,00 ₼
    /// Bölgü sayı              : 34
    /// Aktiv aylar             : 7  (İyul 2026 → Sentyabr 2026)
    /// </code>
    /// Əlavə olaraq mənbələr üzrə bölgü də saxlanılır ki, pulun HARADAN
    /// gəldiyi aydın olsun.
    /// </para>
    /// </summary>
    public sealed class PartnerCardRow
    {
        /// <summary>Tərəfdaşın adı.</summary>
        public string Terefdas { get; set; } = string.Empty;

        /// <summary>Standart faiz dərəcəsi (%).</summary>
        public decimal Faiz { get; set; }

        /// <summary>Qalıq payçısıdırmı?</summary>
        public bool QaligPayi { get; set; }

        /// <summary>Bazada aktivdirmi? (silinibsə/gizlədilibsə «Xeyr»)</summary>
        public bool Aktiv { get; set; } = true;

        /// <summary>Dövr ərzində SATIŞ bölgülərindən qazanılan (₼).</summary>
        public decimal SatisQazanc { get; set; }

        /// <summary>Dövr ərzində KREDİT bölgülərindən qazanılan (₼).</summary>
        public decimal KreditQazanc { get; set; }

        /// <summary>Dövr ərzində KREDİT ƏLAVƏ GƏLİR bölgülərindən qazanılan (₼).</summary>
        public decimal GelirQazanc { get; set; }

        /// <summary>Ümumi qazanılmış pay: üç mənbənin cəmi (₼).</summary>
        public decimal Qazanilmis => SatisQazanc + KreditQazanc + GelirQazanc;

        /// <summary>Tərəfdaşa faktiki VERİLƏN pul (₼).</summary>
        public decimal Verilmis { get; set; }

        /// <summary>Qalıq: qazanılmış − verilmiş (₼). Mənfi ola bilər (avans verilibsə).</summary>
        public decimal Qaliq => Qazanilmis - Verilmis;

        /// <summary>Dövr ərzində iştirak etdiyi bölgü sayı.</summary>
        public int BolguSayi { get; set; }

        // ====================================================================
        //  📅 SEÇİLMİŞ DÖVR rəqəmləri  (ümumi tarixçə ilə YANAŞI)
        // --------------------------------------------------------------------
        //  Kart cədvəli HƏMİŞƏ bütün vaxtın mənzərəsini saxlayır ✓ (tarixçə
        //  itmir ✗), lakin seçilmiş dövrdə nə qazandığı da ayrıca görünür ✓
        // ====================================================================

        /// <summary>SEÇİLMİŞ DÖVR ərzində qazanılan (₼).</summary>
        public decimal DonemQazanilmis { get; set; }

        /// <summary>SEÇİLMİŞ DÖVR ərzində verilən pul (₼).</summary>
        public decimal DonemVerilmis { get; set; }

        /// <summary>Seçilmiş dövrün qalığı (₼).</summary>
        public decimal DonemQaliq => DonemQazanilmis - DonemVerilmis;

        /// <summary>Dövr ərzində bölgü sayı.</summary>
        public int DonemBolguSayi { get; set; }

        /// <summary>Seçilmiş dövr üzrə qazanılmış mətni.</summary>
        public string DonemQazanilmisMetni => $"{DonemQazanilmis:N2} ₼";

        /// <summary>Seçilmiş dövr üzrə verilmiş mətni.</summary>
        public string DonemVerilmisMetni => $"{DonemVerilmis:N2} ₼";

        /// <summary>Dövrdə heç nə olmayıbsa nişan: «—».</summary>
        public string DonemQazanilmisYoxsa => DonemQazanilmis <= 0m ? "—" : DonemQazanilmisMetni;

        /// <summary>Dövrdə heç nə olmayıbsa nişan: «—».</summary>
        public string DonemVerilmisYoxsa => DonemVerilmis <= 0m ? "—" : DonemVerilmisMetni;

        /// <summary>
        /// Ümumi balansın rəngi: verilməli qalıb yaşıl, avans verilibsə qırmızı.
        /// </summary>
        public string UmumiBalansReng => Qaliq > 0m ? "#34D399" : Qaliq < 0m ? "#F43F5E" : "#94A3B8";

        // ====================================================================
        //  📊 PEŞƏKAR GÖRÜNÜŞ (sıra nömrəsi + irəliləyiş zolağı)
        // ====================================================================

        /// <summary>Cədvəldə sıra nömrəsi / medal yeri (VM tərəfindən təyin olunur).</summary>
        public int SiraNomresi { get; set; }

        /// <summary>Sıra nişanı: 1 → «🥇», 2 → «🥈», 3 → «🥉», sonra rəqəm.</summary>
        public string SiraNisani => SiraNomresi switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => SiraNomresi.ToString()
        };

        /// <summary>Ödəniş faizi (0–1) — irəliləyiş zolağı üçün.</summary>
        public double OdenisFaizi => Qazanilmis <= 0m
            ? 0d
            : (double)Math.Clamp(Verilmis / Qazanilmis, 0m, 1m);

        /// <summary>«64,7% ödənilib» mətni.</summary>
        public string OdenisFaiziMetni => Qazanilmis <= 0m
            ? "bölgü yoxdur"
            : $"{Verilmis / Qazanilmis * 100m:0.0}% ödənilib";

        /// <summary>Balans nişanı mətni: «ödənilməli» / «avans verilib» / «hesablaşma tamam».</summary>
        public string BalansNisani => Qaliq > 0.005m
            ? "ödənilməli"
            : Qaliq < -0.005m ? "avans verilib" : "hesablaşma tamam";

        /// <summary>Balans sıfırdırsa ✅ nişanı göstərilir.</summary>
        public bool BalansSifir => Math.Abs(Qaliq) <= 0.005m;

        /// <summary>Bölgüyə düşən İLK ay («İyul 2026»).</summary>
        public string IlkAy { get; set; } = "—";

        /// <summary>Bölgüyə düşən SON ay («Sentyabr 2026»).</summary>
        public string SonAy { get; set; } = "—";

        /// <summary>Dövr ərzində aktiv olduğu AY sayı («dövriyyə uzunluğu»).</summary>
        public int AktivAySayi { get; set; }

        /// <summary>Faiz sütunu mətni: «%6» və ya «qalıq».</summary>
        public string FaizMetni => QaligPayi || Faiz <= 0m ? "qalıq" : $"%{Faiz:0.##}";

        /// <summary>Qazanılmış sütunu mətni.</summary>
        public string QazanilmisMetni => $"{Qazanilmis:N2} ₼";

        /// <summary>Verilmiş sütunu mətni.</summary>
        public string VerilmisMetni => $"{Verilmis:N2} ₼";

        /// <summary>Qalıq sütunu mətni.</summary>
        public string QaliqMetni => $"{Qaliq:N2} ₼";

        /// <summary>
        /// <b>ÜMUMİ DÖVRİYYƏ</b> mətni: «İyul 2026 → Sentyabr 2026 · 3 ay · 47 bölgü».
        /// <para>
        /// Tərəfdaşın nə vaxtdan nə vaxtadək bölgüdə iştirak etdiyini,
        /// neçə ay aktiv olduğunu və neçə bölgü aldığını bir sətirdə göstərir ✓
        /// </para>
        /// </summary>
        public string DonemMetni => BolguSayi == 0
            ? "bölgü yoxdur"
            : $"{IlkAy} → {SonAy}  ·  {AktivAySayi} ay  ·  {BolguSayi} bölgü";

        /// <summary>Dövriyyə sütununun alət ipucu.</summary>
        public string DonemIpucu => BolguSayi == 0
            ? "Bu dövrdə heç bir bölgü yoxdur"
            : $"İlk bölgü: {IlkAy}{Environment.NewLine}Son bölgü: {SonAy}" +
              $"{Environment.NewLine}Aktiv aylar: {AktivAySayi}" +
              $"{Environment.NewLine}Bölgü sayı: {BolguSayi}";

        /// <summary>Qalıq rəngi: borc qalıbsa yaşıl, avans varsa qırmızı.</summary>
        public string QaliqReng => Qaliq > 0m ? "#34D399" : Qaliq < 0m ? "#F43F5E" : "#94A3B8";

        public override string ToString() => $"{Terefdas} — {QazanilmisMetni}";
    }
}
