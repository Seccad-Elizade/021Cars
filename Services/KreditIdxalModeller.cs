using System.Globalization;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🧾 <b>«KREDİT VƏ SKRİPT İDXAL PANELİ» MODELLƏRİ</b> ✓✓✓
    /// <para>
    /// İstifadəçi maşın + kredit məlumatlarını <b>toplu mətn</b> şəklində yapışdırır
    /// (bax: <see cref="IdxalMetni.Numune"/>) → panel onları <b>təhlil edir</b> ✓,
    /// <b>aylıq ödəniş cədvəlini</b> (maya · mənfəət · tərəfdaş payları) önizləyir ✓ və
    /// təsdiqdən sonra bazaya yazır ✓✓✓
    /// </para>
    /// </summary>
    public static class IdxalMetni
    {
        /// <summary>📋 Nümunə mətn — «📋 Nümunə» düyməsi bunu input sahəsinə yazır ✓.</summary>
        public const string Numune = """
        # ============================================================
        #  021Cars — KREDİT İDXALI NÜMUNƏSİ (TAM VƏ ƏHATƏLİ) ✓
        #  BÖLMƏLƏR: [Müqavilə Məlumatları] · [Tərəfdaşlar və Pay
        #  Bölgüsü] · [Gecikmələr və Cərimələr Tarixçəsi] · [Aylıq
        #  Ödənişlər və Paylar Qrafiki] ✓
        #  AÇARLAR: Maşın · Nömrə · İl · Müştəri · Müqavilə No · Alış
        #  Qiyməti · Əlavə Xərclər · Ümumi Maya · Satış Qiyməti ·
        #  İlkin Ödəniş (Beh) · İlkin Ödəniş Möhləti · Müddət (Ay) ·
        #  Standart Aylıq Ödəniş · Ödəniş Gün Aralığı · Başlama Tarixi ·
        #  Faiz · Kök · Maya · Qeyd ✓
        #  QRAFİK: «# Tarix | Ödəniş | Maya | Kar<Ad> | … | Qeyd» ✓
        #    • «İlkin mənfəət bölgüsü» sətri (0,00 ₼) → ÖDƏNİŞ SAYILMIR ✗
        #    • «…möhlətinin ödənilən pulu» → kredit taksiti DEYİL ✗
        #  ÇOXLU KREDİT: blokları «---» sətri ilə ayırın ✓
        # ============================================================

        [Müqavilə Məlumatları]
        Maşın: Kia Megantis
        Nömrə: 90-MX-712
        İl: 2019
        Müştəri: Kamran Həsənov
        Müqavilə No: M-0712
        Alış Qiyməti: 11907.00
        Əlavə Xərclər: 763.00
        Ümumi Maya: 12670.00
        Satış Qiyməti: 15630.00
        İlkin Ödəniş (Beh): 11000.00
        Müddət (Ay): 36
        Standart Aylıq Ödəniş: 300.00
        Ödəniş Gün Aralığı: Hər ayın 1-i ilə 7-si arası
        Başlama Tarixi: 29.04.2024

        [Tərəfdaşlar və Pay Bölgüsü]
        Zaur | 6% | KarZaur
        Eşqin | 5% | KarEshgin
        Asiman | 4% | KarAsiman
        Asif | qalıq | KarAsif
        Musa | qalıq | KarMusa

        [Gecikmələr və Cərimələr Tarixçəsi (Kassa)]
        # Tarix | Məbləğ (AZN) | Qeyd
        10.02.2025 | 20.00 | Gecikmə cəriməsi (çatmayan pul) -> Kassaya yazılır
        11.07.2025 | 20.00 | Gecikmə cəriməsi (obyekt) -> Kassaya yazılır
        13.11.2025 | 20.00 | Gecikmə cəriməsi (obyekt) -> Kassaya yazılır
        08.12.2025 | 20.00 | Gecikmə cəriməsi (obyekt) -> Kassaya yazılır

        [Aylıq Ödənişlər və Paylar Qrafiki]
        # Tarix | Ödəniş | Maya | KarMusa | KarAsif | KarZaur | KarAsiman | Qeyd
        29.04.2024 | 0.00 | 0.00 | 1332.00 | 1332.00 | 177.00 | 119.00 | İlkin mənfəət bölgüsü
        04.06.2024 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        05.07.2024 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        03.08.2024 | 298.00 | 144.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        30.08.2024 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        04.10.2024 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        05.11.2024 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        06.12.2024 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        06.01.2025 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        06.02.2025 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        05.03.2025 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        04.04.2025 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        02.05.2025 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay
        05.06.2025 | 300.00 | 146.00 | 77.00 | 77.00 | 0.00 | 0.00 | Standart ay

        ---
        [Müqavilə Məlumatları]
        Maşın: Kia Ceed 1.6
        Nömrə: 90-GA-455
        Müştəri: Rebbil Əliyev
        Müqavilə No: M-0021
        Alış Qiyməti: 13800.00
        Əlavə Xərclər: 1200.00
        Ümumi Maya: 15000.00
        Satış Qiyməti: 16200.00
        İlkin Ödəniş (Beh): 9000.00
        İlkin Ödəniş Möhləti: 25.04.2024 tarixədək əlavə 4000 AZN ödəniləcək
        Müddət (Ay): 12
        Standart Aylıq Ödəniş: 1000.00
        Ödəniş Gün Aralığı: Hər ayın 5-i ilə 10-u arası
        Başlama Tarixi: 05.03.2024
        Faiz: 66.67
        Kök: 7200.00
        Maya: 15000.00

        [Tərəfdaşlar və Pay Bölgüsü]
        Zaur | 6% | KarZaur
        Eşqin | 5% | KarEshgin
        Asiman | 4% | KarAsiman
        Asif | qalıq | KarAsif
        Musa | qalıq | KarMusa

        [Aylıq Ödənişlər və Paylar Qrafiki]
        # Tarix | Ödəniş | Maya | KarZaur | KarEshgin | KarAsiman | KarAsif | KarMusa | Qeyd
        05.03.2024 | 0.00 | 0.00 | 30.00 | 25.00 | 20.00 | 212.50 | 212.50 | İlkin mənfəət bölgüsü
        25.04.2024 | 4000.00 | 4000.00 | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 | İlkin ödəniş möhlətinin ödənilən pulu
        05.04.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.05.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.06.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.07.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.08.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.09.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.10.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.11.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.12.2024 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.01.2025 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.02.2025 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Standart ay
        05.03.2025 | 1000.00 | 600.00 | 24.00 | 20.00 | 16.00 | 170.00 | 170.00 | Sonuncu ay

        ---
        [Müqavilə Məlumatları]
        Maşın: Hyundai i30
        Nömrə: 77-KL-936
        Müştəri: Nurlan Quliyev
        Müqavilə No: M-0936
        Alış Qiyməti: 12900.00
        Əlavə Xərclər: 735.00
        Ümumi Maya: 13635.00
        Satış Qiyməti: 13818.00
        İlkin Ödəniş (Beh): 4000.00
        Müddət (Ay): 36
        Standart Aylıq Ödəniş: 600.00
        Başlama Tarixi: 31.05.2024

        [Gecikmələr və Cərimələr Tarixçəsi (Kassa)]
        31.03.2025 | 500.00 | Gecikmə cəriməsi (Obyektdən)
        31.08.2025 | 100.00 | Gecikmə cəriməsi (obyekt)
        31.08.2026 | 250.00 | Gecikmə cəriməsi (obyekt)

        [Aylıq Ödənişlər və Paylar Qrafiki]
        # Tarix | Ödəniş | Maya | Kar(M) | Kar(A) | Kar(Z) | Kar(A2) | Qeyd
        31.05.2024 | 0.00 | 0.00 | 82.00 | 82.00 | 12.00 | 7.00 | İlkin mənfəət bölgüsü
        30.06.2024 | 600.00 | 272.00 | 164.00 | 164.00 | 0.00 | 0.00 | Standart ay
        29.07.2024 | 600.00 | 272.00 | 164.00 | 164.00 | 0.00 | 0.00 | Standart ay
        30.08.2024 | 600.00 | 272.00 | 164.00 | 164.00 | 0.00 | 0.00 | Standart ay
        30.09.2024 | 600.00 | 272.00 | 164.00 | 164.00 | 0.00 | 0.00 | Standart ay
        29.10.2024 | 600.00 | 272.00 | 164.00 | 164.00 | 0.00 | 0.00 | Standart ay
        """;
    }

    /// <summary>Bir kredit blokunun təhlil edilmiş xam məlumatı (bazaya yazılmazdan əvvəl ✓).</summary>
    public sealed class KreditIdxalBloku
    {
        public string Masin { get; set; } = string.Empty;
        public string Nomre { get; set; } = string.Empty;
        public int Il { get; set; }
        public string Musteri { get; set; } = string.Empty;
        public string Muqavile { get; set; } = string.Empty;

        /// <summary>KÖK — kreditləşdirilən (faktiki borc) məbləğ (₼).</summary>
        public decimal Kok { get; set; }

        /// <summary>İlkin ödəniş / beh (₼).</summary>
        public decimal Ilkin { get; set; }

        /// <summary>Ümumi ödəniləcək məbləğ (₼) — faizi buradan tapırıq ✓.</summary>
        public decimal Umumi { get; set; }

        /// <summary>Faiz dərəcəsi (%).</summary>
        public decimal Faiz { get; set; }

        /// <summary>Müddət (ay).</summary>
        public int Muddet { get; set; }

        /// <summary>Aylıq ödəniş (₼).</summary>
        public decimal Aylik { get; set; }

        /// <summary>Avtomobilin maya dəyəri (₼) — bazaya yazılır ✓.</summary>
        public decimal Maya { get; set; }

        /// <summary>Avtomobilin satış qiyməti (₼).</summary>
        public decimal SatisQiymeti { get; set; }

        public DateTime? Baslama { get; set; }
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Tərəfdaş bölgüsü (boşdursa standart qalıq payçıları işlədilir ✓).</summary>
        public List<KreditIdxalTerefdas> Terefdaslar { get; set; } = new();

        /// <summary>İstifadəçinin yazdığı faktiki ödənişlər (tarix + məbləğ ✓).</summary>
        public List<KreditIdxalOdenis> Odenisler { get; set; } = new();

        /// <summary>⏳ İLKİN ÖDƏNİŞƏ MÖHLƏTLƏR («25.04.2024-ə 2000 ₼» ✓).</summary>
        public List<KreditIdxalMohlet> Mohletler { get; set; } = new();

        /// <summary>⚖️ GECİKMƏ CƏRİMƏLƏRİ tarixçəsi ✓.</summary>
        public List<KreditIdxalGecikme> Gecikmeler { get; set; } = new();

        /// <summary>Ödəniş gün aralığı mətni («hər ayın 5-i ilə 10-u arası» ✓).</summary>
        public string OdenisGunAraligi { get; set; } = string.Empty;

        /// <summary>📅 İstifadəçinin verdiyi DƏQİQ aylıq qrafik (maya + paylar ✓✓✓).</summary>
        public List<KreditIdxalQrafikSetir> Qrafik { get; set; } = new();

        /// <summary>🚘 Avtomobilin alış qiyməti (₼).</summary>
        public decimal AlisQiymeti { get; set; }

        /// <summary>➕ Əlavə xərclər (₼).</summary>
        public decimal ElaveXercler { get; set; }

        /// <summary>🛠️ Ümumi maya dəyəri (₼) — KÖK bazası ✓.</summary>
        public decimal UmumiMaya { get; set; }

        /// <summary>Blokun nömrəsi (hesabat üçün ✓).</summary>
        public int Sira { get; set; }

        /// <summary>«M-0021 | Rebbil | Kia Ceed 90-GA-455» kimi qısa başlıq ✓.</summary>
        public string Basliq
        {
            get
            {
                var hisseler = new List<string>();
                if (!string.IsNullOrWhiteSpace(Muqavile)) hisseler.Add(Muqavile.Trim());
                if (!string.IsNullOrWhiteSpace(Musteri)) hisseler.Add(Musteri.Trim());
                var masin = Masin.Trim();
                if (!string.IsNullOrWhiteSpace(Nomre)) masin += " " + Nomre.Trim();
                if (!string.IsNullOrWhiteSpace(masin)) hisseler.Add(masin.Trim());
                return hisseler.Count == 0 ? $"Blok #{Sira}" : string.Join(" | ", hisseler);
            }
        }
    }

    /// <summary>İdxal mətnindəki bir tərəfdaş göstərişi ✓.</summary>
    public sealed class KreditIdxalTerefdas
    {
        public string Ad { get; set; } = string.Empty;

        /// <summary>Faiz dərəcəsi (%) — qalıq payçılarında 0.</summary>
        public decimal Faiz { get; set; }

        /// <summary>Qalan məbləği digər qalıq payçıları ilə bərabər bölürmü?</summary>
        public bool QaligPayi { get; set; }

        /// <summary>Rol / vəzifə mətni («KarM» · «KarA» ✓).</summary>
        public string Rol { get; set; } = string.Empty;
    }

    /// <summary>İdxal mətnində bir faktiki ödəniş ✓.</summary>
    public sealed class KreditIdxalOdenis
    {
        public DateTime Tarix { get; set; }
        public decimal Mebleg { get; set; }
    }

    /// <summary>⏳ İLKİN ÖDƏNİŞƏ MÖHLƏT («10 günə 2000 ₼» ✓).</summary>
    public sealed class KreditIdxalMohlet
    {
        /// <summary>Pulun gələcəyi tarix.</summary>
        public DateTime Tarix { get; set; }

        /// <summary>Möhlətə salınmış məbləğ (₼).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>Əlavə qeyd.</summary>
        public string Qeyd { get; set; } = string.Empty;
    }

    /// <summary>⚖️ GECİKMƏ CƏRİMƏSİ ✓.</summary>
    public sealed class KreditIdxalGecikme
    {
        /// <summary>Cərimənin tarixi.</summary>
        public DateTime Tarix { get; set; }

        /// <summary>Cərimə məbləği (₼).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>Qeyd («Gecikmə cəriməsi» ✓).</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Ödənilibmi? (tarixçədə adətən «bəli» ✓)</summary>
        public bool Odenilib { get; set; } = true;
    }

    /// <summary>👥 Bir tərəfdaşın payı (ad + məbləğ ✓).</summary>
    public sealed class KreditIdxalPay
    {
        public string Ad { get; set; } = string.Empty;
        public decimal Mebleg { get; set; }
    }

    /// <summary>
    /// 📅 <b>İSTİFADƏÇİNİN VERDİYİ DƏQİQ AYLIQ QRAFİK SƏTRİ</b> ✓✓✓
    /// <para>
    /// Maya və hər tərəfdaşın payı <b>sətir-sətir verilib</b> → app onları
    /// <b>olduğu kimi</b> yazır (hesablamır ✓).
    /// </para>
    /// </summary>
    public sealed class KreditIdxalQrafikSetir
    {
        /// <summary>Ödəniş tarixi.</summary>
        public DateTime Tarix { get; set; }

        /// <summary>Ödəniş məbləği (₼).</summary>
        public decimal Odenis { get; set; }

        /// <summary>MAYA — bu sətrin kök (əsas borc) payı (₼).</summary>
        public decimal Maya { get; set; }

        /// <summary>Qeyd («Standart ay» · «Vaxtından tez bağlama» ✓).</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>
        /// ⏳ Bu sətir <b>İLKİN ÖDƏNİŞ MÖHLƏTİNİN ödənişi</b>dir? (kredit taksiti DEYİL ✗)
        /// <para>
        /// Belə sətirlər <b>kreditin ödəniş cəminə DAXİL EDİLMİR</b> ✗✓✓ — əks halda
        /// kredit vaxtından əvvəl «bitmiş» sayılır və avtomobil səhvən arxivə düşür ✗.
        /// Onlar ayrıca <b>OdenisMohlet</b> kimi «ödənilib» işarələnir ✓.
        /// </para>
        /// </summary>
        public bool MohletOdenisi { get; set; }

        /// <summary>
        /// 🧩 Bu sətir <b>«İLKİN MƏNFƏƏT BÖLGÜSÜ»</b>dür? ✓✓✓ (v6.2.29)
        /// <para>
        /// ★ İstifadəçi tələbi: «İlkin mənfəət bölgüsü» sətri (ödəniş = <b>0,00</b>,
        /// maya = <b>0,00</b>) <b>aylıq kredit ödənişi SAYILMAMALIDIR</b> ✗ —
        /// yalnız tərəfdaşların <b>ilkin pay bölgüsünü</b> təyin edir ✓.
        /// </para>
        /// <para>
        /// ⚠ Əks halda bu sətir <b>0 ₼-lıq saxta «Gəlir» əməliyyatı</b> yaradır ✗ və
        /// <b>bütün taksitləri 1 yer SÜRÜŞDÜRÜR</b> ✗✗✗ (məs. 04.06.2024 ödənişi
        /// «1-ci ay» yerinə «2-ci ay» olur ✗) → kredit cədvəli və balans səhv
        /// görünür ✗.
        /// </para>
        /// </summary>
        public bool IlkinBolgu { get; set; }

        /// <summary>Tərəfdaş payları (verildiyi kimi ✓).</summary>
        public List<KreditIdxalPay> Paylar { get; set; } = new();

        /// <summary>Bölünən mənfəət = tərəfdaş paylarının cəmi (₼).</summary>
        public decimal Menfeet => Paylar.Sum(p => p.Mebleg);
    }

    /// <summary>
    /// 📊 Önizləmə cədvəlinin BİR SƏTRİ — bir aylıq taksit ✓✓✓
    /// (tarix · maya/kök · mənfəət · tərəfdaş payları ✓)
    /// </summary>
    public sealed class KreditIdxalSetir
    {
        /// <summary>Kreditin başlığı (çoxlu blok olduqda ayırd etmək üçün ✓).</summary>
        public string Kredit { get; init; } = string.Empty;

        /// <summary>Taksit nömrəsi (1-dən ✓; cərimə/möhlət sətirlərində 0 ✓).</summary>
        public int Ay { get; init; }

        /// <summary>Sətir növü: «Taksit» · «⚖️ Cərimə» · «⏳ Möhlət» ✓.</summary>
        public string Nov { get; init; } = "Taksit";

        /// <summary>Ödəniş tarixi.</summary>
        public DateTime Tarix { get; init; }

        /// <summary>Ödəniş məbləği (₼).</summary>
        public decimal Odenis { get; init; }

        /// <summary>MAYA — bu ödənişin KÖK (əsas borc) payı (₼).</summary>
        public decimal Maya { get; init; }

        /// <summary>Bölünən mənfəət (faiz) (₼) = ödəniş − maya.</summary>
        public decimal Menfeet { get; init; }

        /// <summary>1-ci tərəfdaşın payı (₼).</summary>
        public decimal Kar1 { get; init; }

        /// <summary>2-ci tərəfdaşın payı (₼).</summary>
        public decimal Kar2 { get; init; }

        /// <summary>1-ci tərəfdaşın adı.</summary>
        public string Kar1Ad { get; init; } = string.Empty;

        /// <summary>2-ci tərəfdaşın adı.</summary>
        public string Kar2Ad { get; init; } = string.Empty;

        /// <summary>3-cü tərəfdaşın payı (₼).</summary>
        public decimal Kar3 { get; init; }

        /// <summary>4-cü tərəfdaşın payı (₼).</summary>
        public decimal Kar4 { get; init; }

        /// <summary>3-cü tərəfdaşın adı.</summary>
        public string Kar3Ad { get; init; } = string.Empty;

        /// <summary>4-cü tərəfdaşın adı.</summary>
        public string Kar4Ad { get; init; } = string.Empty;

        /// <summary>Bütün tərəfdaş payları mətni: «Asif 164,00 ₼ · Musa 164,00 ₼».</summary>
        public string Bolgu { get; init; } = string.Empty;

        /// <summary>Bu ödənişdən SONRA qalan kök (₼).</summary>
        public decimal QaliqKok { get; init; }

        /// <summary>Ödəniş artıq edilibmi (istifadəçi mətnində verilibsə ✓)?</summary>
        public bool Odenilib { get; init; }

        // ---- Cədvəl üçün mətnlər ----

        public string TarixMetni => Tarix.ToString("dd.MM.yyyy");
        public string AyMetni => Nov == "Taksit" && Ay > 0 ? Ay.ToString() : "—";
        public string OdenisMetni => Pul(Odenis);
        public string MayaMetni => Nov == "Taksit" ? Pul(Maya) : "—";
        public string MenfeetMetni => Nov == "Taksit" ? Pul(Menfeet) : "—";
        public string Kar1Metni => Nov.StartsWith("⏳", StringComparison.Ordinal) || Kar1Ad.Length == 0 ? "—" : Pul(Kar1);
        public string Kar2Metni => Nov.StartsWith("⏳", StringComparison.Ordinal) || Kar2Ad.Length == 0 ? "—" : Pul(Kar2);
        public string Kar3Metni => Nov.StartsWith("⏳", StringComparison.Ordinal) || Kar3Ad.Length == 0 ? "—" : Pul(Kar3);
        public string Kar4Metni => Nov.StartsWith("⏳", StringComparison.Ordinal) || Kar4Ad.Length == 0 ? "—" : Pul(Kar4);
        public string QaliqKokMetni => Nov == "Taksit" ? Pul(QaliqKok) : "—";

        /// <summary>Vəziyyət mətni növə görə ✓.</summary>
        public string Veziyyet => Nov switch
        {
            "⚖️ Cərimə" => Odenilib ? "⚖️ Cərimə (öd.)" : "⚖️ Cərimə (gözl.)",
            "⏳ Möhlət" => Odenilib ? "⏳ Möhlət (öd. ✓)" : "⏳ Möhlət",
            "🧩 İlkin bölgü" => "🧩 Bölgü (ödəniş deyil ✗)",
            _ => Odenilib ? "✅ Ödənilib" : "🕓 Plan"
        };

        public string VeziyyetRengi => Nov switch
        {
            "⚖️ Cərimə" => "#FB7185",
            "⏳ Möhlət" => "#F59E0B",
            "🧩 İlkin bölgü" => "#C084FC",
            _ => Odenilib ? "#34D399" : "#FBBF24"
        };

        private static string Pul(decimal mebleg)
            => mebleg.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ") + " ₼";
    }

    /// <summary>Bir kreditin idxal nəticəsi + önizləmə cədvəli ✓.</summary>
    public sealed class KreditIdxalKart
    {
        public string Basliq { get; init; } = string.Empty;
        public decimal Kok { get; init; }
        public decimal Ilkin { get; init; }
        public decimal Umumi { get; init; }
        public decimal Faiz { get; init; }
        public decimal Aylik { get; init; }
        public int Muddet { get; init; }
        public decimal Maya { get; init; }
        public decimal SatisQiymeti { get; init; }

        /// <summary>İstifadəçinin verdiyi ödənişlərin cəmi (₼).</summary>
        public decimal Odenilmis { get; init; }

        /// <summary>⚖️ Bütün gecikmə cərimələrinin cəmi (₼).</summary>
        public decimal CerimeCemi { get; init; }

        /// <summary>⏳ İlkin ödənişə möhlətlərin cəmi (₼).</summary>
        public decimal MohletCemi { get; init; }

        /// <summary>Ödəniş gün aralığı mətni ✓.</summary>
        public string OdenisGunAraligi { get; init; } = string.Empty;

        /// <summary>QALIQ NİSYƏ = ümumi qiymət + cərimələr − ödənişlər (₼).</summary>
        public decimal QaliqNisye { get; init; }

        /// <summary>📅 İstifadəçinin verdiyi DƏQİQ qrafikdən götürülübmü?</summary>
        public bool Qrafikden { get; init; }

        /// <summary>Ödəniş cədvəli (ay-ay ✓).</summary>
        public List<KreditIdxalSetir> Setirler { get; init; } = new();

        /// <summary>1-ci tərəfdaşın adı (cədvəl başlığı üçün ✓).</summary>
        public string Kar1Ad { get; init; } = string.Empty;

        /// <summary>2-ci tərəfdaşın adı.</summary>
        public string Kar2Ad { get; init; } = string.Empty;

        /// <summary>3-cü tərəfdaşın adı.</summary>
        public string Kar3Ad { get; init; } = string.Empty;

        /// <summary>4-cü tərəfdaşın adı.</summary>
        public string Kar4Ad { get; init; } = string.Empty;

        /// <summary>Bölgü xülasəsi: «Asif, Musa → hər ay 164,00 ₼ / 164,00 ₼».</summary>
        public string BolguXulase { get; init; } = string.Empty;

        public string KokMetni => Pul(Kok);
        public string UmumiMetni => Pul(Umumi);
        public string AylikMetni => Pul(Aylik);
        public string IlkinMetni => Pul(Ilkin);
        public string MayaMetni => Pul(Maya);
        public string OdenilmisMetni => Pul(Odenilmis);
        public string QaliqNisyeMetni => Pul(QaliqNisye);
        public string CerimeMetni => Pul(CerimeCemi);
        public string MohletMetni => Pul(MohletCemi);
        public string FaizMetni => $"{Faiz:0.##}%";
        public string MuddetMetni => $"{Muddet} ay";

        private static string Pul(decimal mebleg)
            => mebleg.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ") + " ₼";
    }

    /// <summary>İdxal/yoxlama əməliyyatının YEKUN nəticəsi ✓.</summary>
    public sealed class KreditIdxalNeticesi
    {
        /// <summary>Uğurlu? (heç bir təhlil xətası yoxdursa ✓)</summary>
        public bool Ugurlu { get; set; }

        /// <summary>Ümumi xəta mesajı (təhlil mümkün olmadıqda ✓).</summary>
        public string? Xeta { get; set; }

        /// <summary>Hesabat sətirləri (istifadəçiyə göstərilir ✓).</summary>
        public List<string> Setirler { get; set; } = new();

        /// <summary>Hər kredit üzrə nəticə + cədvəl ✓.</summary>
        public List<KreditIdxalKart> Kartlar { get; set; } = new();

        // ---- Sayğaclar ----

        public int KreditSayi { get; set; }
        public int MasinSayi { get; set; }
        public int OdenisSayi { get; set; }
        public int PaySayi { get; set; }

        /// <summary>⚖️ Yazılan gecikmə cəriməsi sayı.</summary>
        public int CerimeSayi { get; set; }

        /// <summary>⏳ Yazılan ilkin ödəniş möhləti sayı.</summary>
        public int MohletSayi { get; set; }

        /// <summary>⏳ Ödənildiyi işarələnən ilkin möhlət sayı.</summary>
        public int MohletOdenisSayi { get; set; }

        /// <summary>⏭️ Mövcud olduğu üçün KEÇİLƏN kredit sayı (təkrar idxal yoxdur ✗).</summary>
        public int KecirilenKredit { get; set; }

        /// <summary>Ümumi kreditləşdirilən məbləğ (₼).</summary>
        public decimal UmumiKok { get; set; }

        /// <summary>Ümumi maya dəyəri (₼).</summary>
        public decimal UmumiMaya { get; set; }
    }
}
