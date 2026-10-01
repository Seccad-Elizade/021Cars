using System.ComponentModel.DataAnnotations.Schema;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// 🔔 <b>BİLDİRİŞ SƏTRİ</b> — «nə vaxt, kim, nə qədər ödəməlidir» ✓✓✓
    /// <para>Bütün mənbələrdən toplanır ✓:</para>
    /// <list type="bullet">
    ///   <item>🏦 <b>Kredit taksiti</b> — hər ayın ödənişi ✓</item>
    ///   <item>⏳ <b>İlkin ödəniş möhləti</b> — «3 000 dərhal + 2 000 → 10 günə» ✓</item>
    ///   <item>🛒 <b>Satış (nisyə) möhləti</b> — «7 000 dərhal + 10 000 + 5 000» ✓</item>
    ///   <item>⚠️ <b>Gecikmə cəriməsi</b> — ödənilməmiş gecikmə ✓</item>
    /// </list>
    /// </summary>
    public sealed class OdenisBildirisi
    {
        /// <summary>Ödənişin tarixi (nə vaxtdır ✓).</summary>
        public DateTime Tarix { get; init; }

        /// <summary>Mənbə qrupu: «Kredit» · «Möhlət» · «Cərimə».</summary>
        public string Menbe { get; init; } = string.Empty;

        /// <summary>Növ mətni: «🏦 Kredit taksiti» · «⏳ İlkin ödəniş möhləti» …</summary>
        public string Nov { get; init; } = string.Empty;

        /// <summary>Müqavilə / sənəd nömrəsi.</summary>
        public string Muqavile { get; init; } = string.Empty;

        /// <summary>Müştərinin adı.</summary>
        public string Mustar { get; init; } = string.Empty;

        /// <summary>Avtomobilin adı (marka · model · nömrə ✓).</summary>
        public string Avtomobil { get; init; } = string.Empty;

        /// <summary>Taksit nömrəsi (kredit taksitlərində ✓ · digərlərində 0 ✓).</summary>
        public int TaksitNo { get; init; }

        /// <summary>Ödəniləcək məbləğ (₼).</summary>
        public decimal Mebleg { get; init; }

        /// <summary>Artıq ödənilibmi?</summary>
        public bool Odenilib { get; init; }

        /// <summary>Əlavə izahat («10 günə» · «gecikmə cəriməsi» ✓).</summary>
        public string Qeyd { get; init; } = string.Empty;

        // ====================================================================
        //  ⏱️ «NƏ VAXTDIR?» — bugünə görə hesablanan göstəricilər ✓✓✓
        // ====================================================================

        /// <summary>Bugündən fərq (GÜN ✓) — mənfi = vaxtı keçib ⚠.</summary>
        [NotMapped]
        public int GunFerqi => (int)(Tarix.Date - DateTime.Today).TotalDays;

        /// <summary>⚠ Vaxtı keçibmi? (ödənilməyibsə ✓)</summary>
        [NotMapped]
        public bool Gecikmis => !Odenilib && GunFerqi < 0;

        /// <summary>📅 BUGÜN ödənilməlidir? ✓</summary>
        [NotMapped]
        public bool Bugun => !Odenilib && GunFerqi == 0;

        /// <summary>🗓 Yaxın 7 gün içindədir? ✓</summary>
        [NotMapped]
        public bool YaxinHefte => !Odenilib && GunFerqi > 0 && GunFerqi <= 7;

        /// <summary>📆 Yaxın 30 gün içindədir? ✓</summary>
        [NotMapped]
        public bool YaxinAy => !Odenilib && GunFerqi > 0 && GunFerqi <= 30;

        /// <summary>🔴 TƏCİLİ? (gecikmiş və ya bugün ✓)</summary>
        [NotMapped]
        public bool Tecili => Gecikmis || Bugun;

        // ====================================================================
        //  📄 CƏDVƏL MƏTN VƏ RƏNGLƏRİ ✓
        // ====================================================================

        [NotMapped]
        public string TarixMetni => Tarix.ToString("dd.MM.yyyy");

        /// <summary>Məbləğ mətni: «1 378,00 ₼».</summary>
        [NotMapped]
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        /// <summary>⏱️ «NƏ VAXTDIR» mətni: «⚠ 5 gün GECİKİB» · «📅 BUGÜN» · «🗓 3 günə».</summary>
        [NotMapped]
        public string VaxtMetni
        {
            get
            {
                if (Odenilib)
                {
                    return "✅ Ödənilib";
                }

                return GunFerqi switch
                {
                    0 => "📅 BUGÜN ödənilməlidir",
                    < 0 => $"⚠ {Math.Abs(GunFerqi)} gün GECİKİB",
                    <= 7 => $"🗓 {GunFerqi} günə",
                    <= 30 => $"📆 {GunFerqi} günə",
                    _ => $"🕓 {GunFerqi} günə"
                };
            }
        }

        /// <summary>Vəziyyət rəngi: gecikmiş qırmızı ⚠ · bugün narıncı · yaxın sarı · ödənilmiş yaşıl ✓.</summary>
        [NotMapped]
        public string VeziyyetRengi => Odenilib
            ? "#34D399"
            : GunFerqi switch
            {
                < 0 => "#FB7185",
                0 => "#F59E0B",
                <= 7 => "#FBBF24",
                _ => "#38BDF8"
            };

        /// <summary>Mənbə rəngi (nişan üçün ✓).</summary>
        [NotMapped]
        public string MenbeRengi => Menbe switch
        {
            "Kredit" => "#60A5FA",
            "Möhlət" => "#C084FC",
            "Cərimə" => "#FB7185",
            _ => "#94A3B8"
        };

        public override string ToString() =>
            $"{TarixMetni} · {Nov} · {Mustar} · {MeblegMetni} · {VaxtMetni}";
    }

    /// <summary>
    /// 🗓️ <b>BİR AYIN ÖDƏNİŞ XÜLASƏSİ</b> ✓✓✓ — «bu ay hansı maşınlar pul verəcək» ✓
    /// </summary>
    public sealed class AyOdenisCemi
    {
        /// <summary>Ayın ilk günü (qruplaşdırma açarı ✓).</summary>
        public DateTime Ay { get; init; }

        /// <summary>«Sentyabr 2026» ✓.</summary>
        public string AyAdi { get; init; } = string.Empty;

        /// <summary>Həmin ayın bütün ödənişləri (₼).</summary>
        public decimal Cemi { get; init; }

        /// <summary>Ödənilmiş hissə (₼) ✓.</summary>
        public decimal Odenilmis { get; init; }

        /// <summary>Ödənilməmiş hissə (₼) ⚠.</summary>
        public decimal Qaliq { get; init; }

        /// <summary>Həmin aydaki bildiriş sətirləri ✓.</summary>
        public IReadOnlyList<OdenisBildirisi> Setirler { get; init; } = Array.Empty<OdenisBildirisi>();

        /// <summary>Sətir (ödəniş) sayı.</summary>
        [NotMapped]
        public int SetirSayi => Setirler.Count;

        /// <summary>Fərqli müştəri sayı ✓.</summary>
        [NotMapped]
        public int MusteriSayi => Setirler
            .Select(s => s.Mustar)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        /// <summary>Fərqli avtomobil sayı ✓.</summary>
        [NotMapped]
        public int AvtomobilSayi => Setirler
            .Select(s => s.Avtomobil)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        /// <summary>Bu ay GECİKMİŞ məbləğ var? ⚠</summary>
        [NotMapped]
        public bool GecikmeVar => Setirler.Any(s => s.Gecikmis);

        [NotMapped]
        public string CemiMetni => $"{Cemi:N2} ₼";

        [NotMapped]
        public string OdenilmisMetni => $"{Odenilmis:N2} ₼";

        [NotMapped]
        public string QaliqMetni => $"{Qaliq:N2} ₼";

        /// <summary>Keçmiş aydırmı? (bitib ✓)</summary>
        [NotMapped]
        public bool KecmisAy => Ay.Year < DateTime.Today.Year
            || (Ay.Year == DateTime.Today.Year && Ay.Month < DateTime.Today.Month);

        /// <summary>CARİ aydırmı? ✓</summary>
        [NotMapped]
        public bool CariAydir => Ay.Year == DateTime.Today.Year && Ay.Month == DateTime.Today.Month;

        /// <summary>Gələcək aydırmı? ✓</summary>
        [NotMapped]
        public bool GelecekAy => !KecmisAy && !CariAydir;

        /// <summary>Ayın rəngi: keçmiş boz · cari yaşıl · gələcək mavi ✓.</summary>
        [NotMapped]
        public string AyRengi => GecikmeVar ? "#FB7185" : CariAydir ? "#34D399" : KecmisAy ? "#94A3B8" : "#38BDF8";

        public override string ToString() => $"{AyAdi} — {CemiMetni} ({SetirSayi} ödəniş)";
    }
}
