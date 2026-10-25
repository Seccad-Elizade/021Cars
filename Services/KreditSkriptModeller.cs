using System.Globalization;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 📥 <b>«SKRİPT İDXALI» — 5 BLOK × N SƏTİR</b> ✓✓✓ (v6.2.72)
    /// <para>
    /// «💳 Kredit Əlavə Gəlir/Xərc» tabında <b>«Növ» = Skript idxalı</b> seçilir ✓ →
    /// mətn <b>5 blok</b> şəklində yapışdırılır (bloklar BOŞ SƏTİR ilə ayrılır ✓):
    /// </para>
    /// <code>
    /// ① Tarix        ② Məbləğ      ③ Maya       ④ Musa       ⑤ Asif
    /// 22,10,2024     1470,00      816,00      327,00      327,00
    /// 22,11,2024     1200,00      546,00      327,00      327,00
    /// </code>
    /// <para>
    /// <b>Şərt:</b> hər sətirdə <c>Maya + Musa + Asif = Məbləğ</c> ✓
    /// </para>
    /// <para>
    /// ⚠ Bu sinif <b>heç bir bazaya yazmır</b> ✗ — yalnız <b>önizləmə</b> üçündür ✓.
    /// </para>
    /// </summary>
    public sealed class KreditSkriptSetir
    {
        /// <summary>Sətrin sıra nömrəsi (1-dən ✓).</summary>
        public int No { get; init; }

        /// <summary>Ödəniş tarixi (22,10,2024 → 22.10.2024 ✓). Oxunmadıqda <c>null</c> ✗.</summary>
        public DateTime? Tarix { get; init; }

        /// <summary>Məbləğ (₼) — ② blok ✓.</summary>
        public decimal Mebleg { get; init; }

        /// <summary>Maya — kök (əsas borc) payı (₼) — ③ blok ✓.</summary>
        public decimal Maya { get; init; }

        /// <summary>Musa payı (₼) — ④ blok ✓.</summary>
        public decimal Musa { get; init; }

        /// <summary>Asif payı (₼) — ⑤ blok ✓.</summary>
        public decimal Asif { get; init; }

        /// <summary>Sətir tam düzgündürmü? (<c>Maya + Musa + Asif = Məbləğ</c> ✓)</summary>
        public bool Duzgundur { get; init; }

        /// <summary>⚠ Bu tarix mətndə TƏKRAR olunur (xəbərdarlıq ✓ — idxalı dayandırmır ✗).</summary>
        public bool TekrarTarix { get; set; }

        /// <summary>Sətrin xəta mətni (boş = xəta yoxdur ✓).</summary>
        public string Xeta { get; init; } = string.Empty;

        /// <summary>Musa + Asif (tərəfdaş bölgüsünün cəmi ✓).</summary>
        public decimal TerefdasCemi => Musa + Asif;

        /// <summary>Mənfəət (bölgü bazası) = Məbləğ − Maya ✓.</summary>
        public decimal Menfeet => Mebleg - Maya;

        /// <summary>Cədvəldə göstərilən tarix: «22.10.2024».</summary>
        public string TarixMetni => Tarix?.ToString("dd.MM.yyyy") ?? "—";

        /// <summary>Cədvəldə göstərilən məbləğ: «1 470,00 ₼».</summary>
        public string MeblegMetni => KreditSkriptYoxlama.Pul(Mebleg);

        /// <summary>Cədvəldə göstərilən maya: «816,00 ₼».</summary>
        public string MayaMetni => KreditSkriptYoxlama.Pul(Maya);

        /// <summary>Cədvəldə göstərilən Musa payı: «327,00 ₼».</summary>
        public string MusaMetni => KreditSkriptYoxlama.Pul(Musa);

        /// <summary>Cədvəldə göstərilən Asif payı: «327,00 ₼».</summary>
        public string AsifMetni => KreditSkriptYoxlama.Pul(Asif);

        /// <summary>✓ / ✗ / ⚠ nişanı (önizləmə cədvəlinin son sütunu ✓).</summary>
        public string VeziyyetMetni => !Duzgundur ? "✗" : TekrarTarix ? "⚠" : "✓";

        /// <summary>Nişanın rəngi (yaşıl · qırmızı · sarı ✓).</summary>
        public string VeziyyetRengi => !Duzgundur ? "#F2677F" : TekrarTarix ? "#E5A62E" : "#41D39A";

        /// <summary>Sətrin izahı (tooltip ✓) — xəta və ya təkrar tarix xəbərdarlığı.</summary>
        public string Ipucu => Xeta.Length > 0
            ? Xeta
            : TekrarTarix
                ? $"⚠ {TarixMetni} tarixi mətndə təkrarlanır — hər sətir AYRICA yazılacaq ✓"
                : $"{TarixMetni} · Maya {KreditSkriptYoxlama.Pul(Maya)} + Musa {KreditSkriptYoxlama.Pul(Musa)} + " +
                  $"Asif {KreditSkriptYoxlama.Pul(Asif)} = {KreditSkriptYoxlama.Pul(Mebleg)} ✓";
    }

    /// <summary>
    /// 🔍 <b>«SKRİPT İDXALI» YOXLAMASININ NƏTİCƏSİ</b> ✓✓✓ (v6.2.72)
    /// <para>
    /// «🔍 YOXLA» düyməsi bu nəticəni qaytarır ✓ →
    /// <b>xəta varsa</b> «📥 İDXAL ET» düyməsi <b>BAĞLI</b> qalır ✗✓✓
    /// </para>
    /// </summary>
    public sealed class KreditSkriptYoxlama
    {
        /// <summary>Azərbaycan pul formatı: minlik = BOŞLUQ, onluq = VERGÜL («16 680,00 ₼» ✓).</summary>
        private static readonly NumberFormatInfo PulFormati = new()
        {
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ",",
            NumberGroupSizes = new[] { 3 },
            NumberDecimalDigits = 2,
            NegativeSign = "-"
        };

        /// <summary>Hər şey qaydasındadır? (xəta yoxdur ✓ → idxal açılır ✓)</summary>
        public bool Ugurlu { get; set; }

        /// <summary>Ümumi xəta mətni (ilk xəta ✓).</summary>
        public string? Xeta { get; set; }

        /// <summary>Önizləmə cədvəlinin sətirləri ✓.</summary>
        public List<KreditSkriptSetir> Setirler { get; } = new();

        /// <summary>❌ Bloklanan xətalar (idxalı BAĞLAYIR ✗).</summary>
        public List<string> Xetalar { get; } = new();

        /// <summary>⚠ Xəbərdarlıqlar (idxalı BAĞLAMIR ✓ — məs. təkrar tarix ✓).</summary>
        public List<string> Xeberdarliqlar { get; } = new();

        /// <summary>Düzgün sətirlərin sayı ✓.</summary>
        public int DuzgunSayi => Setirler.Count(s => s.Duzgundur);

        /// <summary>Xətalı sətirlərin sayı ✗.</summary>
        public int XetaliSayi => Setirler.Count(s => !s.Duzgundur);

        /// <summary>Düzgün sətirlərin MƏBLƏĞ cəmi (₼) ✓.</summary>
        public decimal CemMebleg => Setirler.Where(s => s.Duzgundur).Sum(s => s.Mebleg);

        /// <summary>Düzgün sətirlərin MAYA cəmi (₼) ✓.</summary>
        public decimal CemMaya => Setirler.Where(s => s.Duzgundur).Sum(s => s.Maya);

        /// <summary>Düzgün sətirlərin MUSA payı cəmi (₼) ✓.</summary>
        public decimal CemMusa => Setirler.Where(s => s.Duzgundur).Sum(s => s.Musa);

        /// <summary>Düzgün sətirlərin ASIF payı cəmi (₼) ✓.</summary>
        public decimal CemAsif => Setirler.Where(s => s.Duzgundur).Sum(s => s.Asif);

        /// <summary>Bölgü bazasının cəmi (Məbləğ − Maya) ✓.</summary>
        public decimal CemMenfeet => Setirler.Where(s => s.Duzgundur).Sum(s => s.Menfeet);

        /// <summary>
        /// 📊 Yekun mətn: «✅ 27 sətir düzgün · cəm 16 680,00 ₼»
        /// <para>⚠ Xəta varsa: «❌ 3 xəta · 24 sətir düzgün» ✗</para>
        /// </summary>
        public string YekunMetni => Ugurlu
            ? $"✅ {DuzgunSayi} sətir düzgün · cəm {Pul(CemMebleg)}"
            : $"❌ {Xetalar.Count} xəta · {DuzgunSayi} sətir düzgün";

        /// <summary>❌ Xəta siyahısı (hər sətir ayrıca ✓).</summary>
        public string XetaMetni => Xetalar.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, Xetalar.Select(x => "❌ " + x));

        /// <summary>⚠ Xəbərdarlıq siyahısı (hər sətir ayrıca ✓).</summary>
        public string XeberdarliqMetni => Xeberdarliqlar.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, Xeberdarliqlar);

        /// <summary>Pulu Azərbaycan formatında göstərir: «1 470,00 ₼».</summary>
        public static string Pul(decimal mebleg) => mebleg.ToString("N2", PulFormati) + " ₼";
    }
}
