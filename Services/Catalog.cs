namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Tətbiqdəki bütün statik Azərbaycandilli seçim siyahılarının (ComboBox məzmunu)
    /// vahid mərkəzi. UI mətnləri yalnız burada saxlanılır.
    /// </summary>
    public static class Catalog
    {
        public static IReadOnlyList<string> FuelTypes { get; } = new[]
        {
            "Benzin", "Dizel", "Qaz (LPG)", "Qaz (CNG / Metan)",
            "Hibrid", "Plug-in Hibrid (PHEV)", "Mild Hibrid (MHEV)", "Mikro Hibrid",
            "Elektrik", "Hidrogen", "Digər"
        };

        public static IReadOnlyList<string> CarStatuses { get; } = new[]
        {
            "Stokda", "Satışda", "Kreditdə", "Satıldı", "Barter edildi", "Təmirdə"
        };

        // ====================================================================
        //  KREDİT «QRAFİK BÖLGÜSÜ» ƏMSALLARI  (müddətə görə)
        // --------------------------------------------------------------------
        //  Maşını satan şəxs deyir: «12 ay, aylıq 1 985 ₼, ilkin 5 000 ₼».
        //  Bu əmsal vasitəsilə maşının qiyməti hesablanır:
        //
        //      Maşının qiyməti = (Aylıq × Ay) ÷ Əmsal + İlkin ödəniş
        //
        //  Nümunə:  (12 × 1 985) ÷ 1.4 + 5 000 = 17 014,29 + 5 000 = 22 014 ₼
        // --------------------------------------------------------------------
        //  Əmsallar xanada AVTOMATİK yazılır (müddətə görə), lakin istifadəçi
        //  onları ƏL İLƏ də dəyişə bilər — çünki bunlar razılaşılan faizlərdir.
        // ====================================================================

        /// <summary>
        /// Kredit müddətinə (ay) uyğun standart əmsal — «qrafik bölgüsü» ✓✓✓
        /// <para>
        /// <b>FORMULA:</b> <c>Əmsal = 1.0 + (Ay ÷ 6) × 0.2</c> ✓ — hər 6 ayda
        /// <b>+0.2</b> ✓ (İSTƏNİLƏN müddətə işləyir ✓):
        /// </para>
        /// <list type="bullet">
        ///   <item>6 → 1.2 · 12 → 1.4 · 15 → 1.5 · 18 → 1.6 ✓</item>
        ///   <item>24 → 1.8 · 30 → 2.0 · 36 → 2.2 ✓</item>
        ///   <item><b>42 → 2.4 · 48 → 2.6 · 54 → 2.8 · 60 → 3.0 ✓✓✓</b></item>
        ///   <item>66 → 3.2 · 72 → 3.4 · … ✓ (müddət uzandıqca davam edir ✓)</item>
        /// </list>
        /// <para>
        /// ⚠ ƏVVƏLKİ XƏTA: cədvəl 36 ayda BİTİRDİ ✗ → 42 · 48 · 60 ayın
        /// <b>hamısı 2.2</b> alırdı ✗ → qrafik bölgüsü (aylıq ödəniş / maşının
        /// qiyməti) SƏHV hesablanırdı ✗✓✓
        /// </para>
        /// </summary>
        public static decimal CreditFactorFor(int ay)
        {
            if (ay <= 0)
            {
                return 1.2m;
            }

            // ✓ FORMULA: 1.0 + hər 6 ay üçün +0.2 ✓✓✓
            return Math.Round(1.0m + ay / 6m * 0.2m, 2);
        }

        /// <summary>
        /// Əmsal cədvəli — UI-də göstərmək üçün (ay → əmsal) ✓
        /// (60 aya qədər uzadıldı ✓✓✓).
        /// </summary>
        public static IReadOnlyList<(int Ay, decimal Emsal)> CreditFactors { get; } = new[]
        {
            (6, 1.2m), (12, 1.4m), (15, 1.5m), (18, 1.6m),
            (24, 1.8m), (30, 2.0m), (36, 2.2m),
            (42, 2.4m), (48, 2.6m), (54, 2.8m), (60, 3.0m)
        };

        public static IReadOnlyList<string> ExpenseDestinations { get; } = new[]
        {
            "Avtomobil Xərci", "Ofis / İnzibati Xərc"
        };

        public static IReadOnlyList<string> PaymentMethods { get; } = new[]
        {
            "Nağd", "Kart / Köçürmə"
        };

        /// <summary>Avtomobil SATIŞINDA ödəniş üsulları (barter və möhlət daxil).</summary>
        public static IReadOnlyList<string> SalePaymentMethods { get; } = new[]
        {
            "Nağd", "Kart / Köçürmə", BarterSale, MohletliSatis
        };

        /// <summary>Satış ödəniş üsulu: barter.</summary>
        public const string BarterSale = "Barter";

        /// <summary>
        /// Satış ödəniş üsulu: <b>⏳ MÖHLƏTLİ (nisyə) satış</b> ✓✓✓
        /// <para>
        /// Maşın nisyə verilir — pul <b>hissə-hissə, gələcək tarixlərdə</b> gəlir ✓.
        /// Hər gələcək ödəniş <see cref="Models.OdenisMohlet"/> sətri kimi yazılır:
        /// «nə vaxt → nə qədər» ✓ (birdən çox ola bilər ✓✓✓)
        /// </para>
        /// </summary>
        public const string MohletliSatis = "Möhlət (nisyə)";

        /// <summary>Möhlətli satışdırmı?</summary>
        public static bool IsMohletliSatis(string? usul)
            => string.Equals(usul, MohletliSatis, StringComparison.Ordinal);


        /// <summary>Avtomobil ALIŞINDA ödəniş üsulları (əlavə gəlir/xərc üçün deyil).</summary>
        public static IReadOnlyList<string> PurchaseMethods { get; } = new[]
        {
            "Nağd", "Barter"
        };

        /// <summary>Alışlar səhifəsindəki filtr seçimləri.</summary>
        public static IReadOnlyList<string> PurchaseFilterTypes { get; } = new[]
        {
            "Bütün Alışlar", "Yalnız Nağd", "Yalnız Barter"
        };

        public static IReadOnlyList<string> ExpenseFilterTypes { get; } = new[]
        {
            "Bütün Xərclər", "Yalnız Avtomobil Xərcləri", "Yalnız Ofis Xərcləri"
        };

        public const string OfficeDestination = "Ofis / İnzibati Xərc";
        public const string CarDestination = "Avtomobil Xərci";

        /// <summary>Alış üsulu: nağd ödəniş.</summary>
        public const string CashPurchase = "Nağd";

        /// <summary>Alış üsulu: barter (əvəzləmə).</summary>
        public const string BarterPurchase = "Barter";
        /// <summary>
        /// 🧩 <b>«İlkin bölgü»</b> — idxalda yazılan <b>İLKİN MƏNFƏƏT BÖLGÜSÜ</b> əməliyyatı ✓✓✓
        /// <para>
        /// ★ İstifadəçi tələbi: qrafikdəki «İlkin mənfəət bölgüsü» sətri <b>real aylıq
        /// ödəniş SAYILMAMALIDIR</b> ✗ — lakin onun <b>tərəfdaş payları SAXLANILMALIDIR</b> ✓
        /// (əks halda tərəfdaş kartlarında <b>0 ₼</b> qalır ✗✓✓).
        /// </para>
        /// <para>
        /// ⚠ Bu növ <b>«Gəlir» DEYİL</b> ✗ → kredit balansına, kassa daxilolmasına və
        /// ödəniş sayına <b>TƏSİR ETMİR</b> ✗ (məbləği 0,00 ₼ ✓) — yalnız tərəfdaş
        /// paylarını daşıyır ✓✓✓
        /// </para>
        /// </summary>
        public const string IlkinBolguNovu = "İlkin bölgü";

        public const string CreditStatus = "Kreditdə";
        public const string SoldStatus = "Satıldı";

        /// <summary>
        /// Avtomobil barter əvəzi kimi verilib — parkdan çıxıb.
        /// «Satıldı»dan fərqli olaraq pul gəlməyib, əvəzində başqa maşın alınıb.
        /// </summary>
        public const string BarterGivenStatus = "Barter edildi";

        public const string StockStatus = "Stokda";

        /// <summary>
        /// 📤 Avtomobil bir ŞƏXSƏ <b>TRANSFER</b> edilib ✓ — parkdan çıxır ✓ və
        /// «🗄️ Satılan &amp; Krediti Bitmiş» bölməsində görünür ✓.
        /// </summary>
        public const string TransferStatus = "Transfer edildi";

        /// <summary>
        /// Status avtomobilin artıq parkda olmadığını göstərirmi?
        /// Satılan, barterə verilən və kreditə salınan maşınlar aktiv parkda görünmür.
        /// </summary>
        public static bool IsOutOfPark(string? status) =>
            string.Equals(status, SoldStatus, StringComparison.Ordinal) ||
            string.Equals(status, BarterGivenStatus, StringComparison.Ordinal) ||
            string.Equals(status, TransferStatus, StringComparison.Ordinal) ||
            string.Equals(status, CreditStatus, StringComparison.Ordinal);

        // ====================================================================
        //  TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ  (kredit əlavə gəlir/xərc)
        // --------------------------------------------------------------------
        //  Maşından gələn mənfəət («xeyir») tərəfdaşlar arasında bölünür.
        //  Faiz dərəcələri UI-də HƏMİŞƏ manual dəyişdirilə bilər —
        //  buradakı dəyərlər yalnız STANDART (default) qiymətlərdir.
        //
        //  Nümunə (mənfəət = 1 590 ₼):
        //      Zaur    %6      →    95,40 ₼
        //      Eşqin   %5      →    79,50 ₼
        //      Asiman  %5      →    79,50 ₼
        //      ─────────────────────────────
        //      qalıq   → 1 335,60 ₼ → Asif 667,80 ₼ + Musa 667,80 ₼
        // ====================================================================

        /// <summary>Bir tərəfdaşın standart payı: ad, faiz (%), qalıq payçısıdırmı?</summary>
        public sealed record PartnerDefault(string Ad, decimal Faiz, bool QaligPayi);

        /// <summary>
        /// Tərəfdaşların STANDART payları.
        /// <para>
        /// <c>QaligPayi = true</c> olanlar (Asif, Musa) faiz almır — onlar faiz
        /// payları çıxıldıqdan sonra QALAN məbləği öz aralarında yarı-yarıya bölürlər.
        /// </para>
        /// </summary>
        public static IReadOnlyList<PartnerDefault> DefaultPartners { get; } = new[]
        {
            new PartnerDefault("Zaur",   6m, false),
            new PartnerDefault("Eşqin",  5m, false),
            new PartnerDefault("Asiman", 5m, false),
            new PartnerDefault("Asif",   0m, true),
            new PartnerDefault("Musa",   0m, true)
        };

        /// <summary>Avtomobil xərcləri üçün qruplar.</summary>
        public static IReadOnlyList<string> CarExpenseGroups { get; } = new[]
        {
            "💰 Alış & Maya Xərcləri",
            "🛠️ Kuzov, Dəmirçi & Malyar",
            "⚙️ Mühərrik, Slesar & Ehtiyat Hissələri",
            "📄 Sənədləşmə & DYP Xərcləri",
            "⛽ Yanacaq, Yuma & Nəqliyyat",
            "👤 Usta & İşçilik Haqqı"
        };

        /// <summary>Ofis / inzibati xərclər üçün qruplar.</summary>
        public static IReadOnlyList<string> OfficeExpenseGroups { get; } = new[]
        {
            "🏢 İcarə & Kommunal Xərclər",
            "💼 Əməkhaqqı & Personal (Maaş/Avans)",
            "🖨️ Ofis Ləvazimatları & Avadanlıq",
            "🌐 İnternet & İT Xidmətləri",
            "📣 Reklam & Marketinq",
            "☕ Qida, Çay & Təmizlik",
            "⚖️ Mühasibat & Bank Komissiyası",
            "📌 Digər İnzibati Xərclər"
        };

        private static readonly Dictionary<string, string[]> Categories = new()
        {
            // ----- Avtomobil qrupları -----
            ["💰 Alış & Maya Xərcləri"] = new[]
            {
                "Alış", "Maşına qoyulan dəyər", "Mayası qaldırıldı", "Maşının bağlanma dəyəri",
                "Maşın sahibinə verildi", "Qalıq ödəniş", "Calağ", "Beh", "Çexol Beh"
            },
            ["🛠️ Kuzov, Dəmirçi & Malyar"] = new[]
            {
                "Dəmirçi", "Malyar", "Malyar baqaj", "Malyar avans", "Fara təmiri", "Fara polirov",
                "Polirovka", "Rol üzləmə", "Rol rəng", "Dam üzləmə", "Qapı rezini 2 ədəd",
                "Patpres", "Patkrilnik", "Patkrilnik bağlama"
            },
            ["⚙️ Mühərrik, Slesar & Ehtiyat Hissələri"] = new[]
            {
                "Slesar", "Mühərrik təmiri", "Yağ dəyişmə", "Yağ filter", "Diaqnostika", "Razval",
                "Yoxlanış", "Rulavoy", "Naklatka", "Benzin bakı", "Lampa"
            },
            ["📄 Sənədləşmə & DYP Xərcləri"] = new[]
            {
                "Etibarnamə", "Qeydiyyat", "Qeydiyyat (Samir)", "Texniki baxış", "Cərimə", "Sığorta"
            },
            ["⛽ Yanacaq, Yuma & Nəqliyyat"] = new[]
            {
                "Benzin", "Dizel", "Moyka", "Par moyka", "Taksi", "Bazar xərci"
            },
            ["👤 Usta & İşçilik Haqqı"] = new[]
            {
                "Elşən", "Zaur", "Usta haqqı (Digər)"
            },

            // ----- Ofis / inzibati qruplar -----
            ["🏢 İcarə & Kommunal Xərclər"] = new[]
            {
                "Ofis İcarə Haqqı", "Elektrik Enerjisi", "Su & Kommunal", "Qaz / İsitmə"
            },
            ["💼 Əməkhaqqı & Personal (Maaş/Avans)"] = new[]
            {
                "İşçi Maaşı", "Maaş Avansı", "Bonus / Mükafat", "Şəxsi Hesablaşma"
            },
            ["🖨️ Ofis Ləvazimatları & Avadanlıq"] = new[]
            {
                "Kantselyariya (Qələm, Dəftər)", "A4 Kağız & Kartric", "Ofis Avadanlığı / Kompüter",
                "Mebel & Aksesuar", "Təmir & Abadlaşdırma"
            },
            ["🌐 İnternet & İT Xidmətləri"] = new[]
            {
                "İnternet Haqqı", "Mobil Əlaqə / Telefon", "Proqram Təminatı / Lisenziya",
                "Domain & Hosting / Bulud"
            },
            ["📣 Reklam & Marketinq"] = new[]
            {
                "Sosial Şəbəkə Reklamı (Targeting)", "Baner / Flayer / Poliqrafiya", "SMM & Marketinq Xidməti"
            },
            ["☕ Qida, Çay & Təmizlik"] = new[]
            {
                "Çay, Qəhvə, Şəkər", "İçməli Su (Dispenser)", "Yemək / Qida Xərci", "Təmizlik Vasitələri"
            },
            ["⚖️ Mühasibat & Bank Komissiyası"] = new[]
            {
                "Mühasibat Xidməti", "Bank Komissiyası", "Vergi Ödənişləri", "Konsaltinq Xidməti"
            },
            ["📌 Digər İnzibati Xərclər"] = new[]
            {
                "Poçt & Kuryer", "Ofis Nəqliyyat / Taksi", "Gözlənilməz İnzibati Xərc"
            }
        };

        /// <summary>Verilmiş qrupa uyğun kateqoriya siyahısını qaytarır.</summary>
        public static IReadOnlyList<string> GetCategories(string? group)
        {
            if (string.IsNullOrWhiteSpace(group)) return Array.Empty<string>();
            return Categories.TryGetValue(group, out var items) ? items : Array.Empty<string>();
        }

        /// <summary>Verilmiş təyinat ofis xərcidirmi?</summary>
        public static bool IsOffice(string? destination)
            => string.Equals(destination, OfficeDestination, StringComparison.Ordinal);

        // ====================================================================
        //  📜 SKRİPT İDXALI QRUPU  (skript idxalı üçün avtomatik qrup ✓✓✓)
        // --------------------------------------------------------------------
        //  Skript idxalı zamanı MÖVCUD OLMAYAN kateqoriyalar avtomatik
        //  yaradılır ✗ → əvvəl onlar standart qrupların (Mühərrik · Usta …)
        //  içinə düşürdü ✗ və oranı qarışdırırdı ✗✓✓
        //  İNDİ isə proqram ÖZÜ «📜 Skript İdxalı» qrupunu yaradır ✓ və
        //  idxalda yaranan BÜTÜN kateqoriyalar orada toplanır ✓✓✓
        // ====================================================================

        /// <summary>
        /// 📜 Skript idxalı ilə yaranan kateqoriyaların <b>AVTOMATİK qrupu</b> ✓✓✓
        /// (proqram bu qrupu özü yaradır ✓ — istifadəçi ad seçmir ✓)
        /// </summary>
        public const string ImportedExpenseGroup = "📜 Skript İdxalı";

        /// <summary>
        /// Kateqoriya proqramın <b>STANDART</b> (daxili) siyahısındadırmı? ✓
        /// <para>
        /// <c>false</c> → kateqoriya <b>sonradan</b> yaranıb ✓ (skript idxalı ✓)
        /// → «📜 Skript İdxalı» qrupuna köçürülür ✓✓✓
        /// </para>
        /// <para>
        /// Müqayisə «ə/ş/ç/ğ/ı» fərqini nəzərə almır ✓ (məs. «malyar» = «Malyar» ✓)
        /// </para>
        /// </summary>
        public static bool IsStandardCategory(string? group, string? category)
        {
            if (string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(category))
            {
                return true;   // qrup sətri (kateqoriyasız) standart sayılır ✓
            }

            if (!Categories.TryGetValue(group, out var items))
            {
                return false;
            }

            var norm = MetinUygunlasdirici.Normallasdir(category);

            return items.Any(item => MetinUygunlasdirici.Normallasdir(item) == norm);
        }
    }
}
