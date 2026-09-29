using CommunityToolkit.Mvvm.ComponentModel;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// Kredit ödəniş qrafikinin bir sətri. "Ödənilib" işarəsi dəyişdikdə
    /// kredit əlavə gəlir qeydi avtomatik yaradılır və ya silinir.
    /// </summary>
    public sealed partial class PaymentRow : ObservableObject
    {
        public int No { get; init; }
        public DateTime Tarix { get; init; }
        public decimal Odenis { get; init; }
        public decimal EsasBorclu { get; init; }
        public decimal Faiz { get; init; }
        public decimal Qaliq { get; init; }

        /// <summary>
        /// Ödəniş kredit müddətindən KƏNARDADIR (başlama tarixindən əvvəl və ya
        /// sonuncu aydan sonra). Belə sətirlər qrafikdə NARINCI rəngdə göstərilir.
        /// </summary>
        public bool XaricindeHedd { get; init; }

        /// <summary>Sətrin növü: "Müddətdən əvvəl" və ya "Müddətdən sonra" (plan sətirlərində boş).</summary>
        public string Novu { get; init; } = string.Empty;

        /// <summary>Cədvəldə göstərilən sıra nömrəsi mətni (müddətdən kənar sətirlərdə "—").</summary>
        public string NoText => XaricindeHedd ? "—" : No.ToString();

        // ---------- Möhlət (ödəniş gecikdirməsi) ----------

        /// <summary>Bu ay üçün möhlət verilibmi?</summary>
        public bool MohletVar { get; init; }

        /// <summary>Möhlətin verildiyi son tarix (bu tarixə qədər ödəniş gecikdirilir).</summary>
        public DateTime? MohletTarixi { get; init; }

        /// <summary>Möhlət edilmiş (gecikdirilmiş) məbləğ.</summary>
        public decimal MohletMeblegi { get; init; }

        /// <summary>Möhlət sətirləri — hər möhlət qeydi ayrı sətirdə.</summary>
        public IReadOnlyList<string> MohletSetirleri { get; init; } = Array.Empty<string>();

        /// <summary>Möhlət mətni: hər möhlət ayrı sətirdə "400,00 ₼ → 10.11.2026".</summary>
        public string MohletMetni => MohletSetirleri.Count > 0
            ? string.Join(Environment.NewLine, MohletSetirleri)
            : MohletVar ? $"{MohletMeblegi:N2} ₼" : string.Empty;

        /// <summary>Möhlət hələ qüvvədədir (son tarix keçməyib).</summary>
        public bool MohletAktiv => MohletVar && (MohletTarixi is null || MohletTarixi.Value.Date >= DateTime.Today);

        // ---------- Gecikmə (gecikmiş ödəniş) ----------

        /// <summary>Bu ay üçün gecikmə qeydə alınıbmı?</summary>
        public bool GecikmeVar { get; init; }

        /// <summary>Ödənilməmiş (aktiv) gecikmə var — sətir qırmızı göstərilir.</summary>
        public bool GecikmeAktiv { get; init; }

        /// <summary>Ödənişin gecikdiyi / bağlandığı son tarix.</summary>
        public DateTime? GecikmeTarixi { get; init; }

        /// <summary>Gecikmə cərimələrinin cəmi (AZN).</summary>
        public decimal GecikmeMeblegi { get; init; }

        /// <summary>Gecikmə sətirləri — hər gecikmə qeydi ayrı sətirdə.</summary>
        public IReadOnlyList<string> GecikmeSetirleri { get; init; } = Array.Empty<string>();

        /// <summary>Gecikmə mətni: hər gecikmə ayrı sətirdə (gün · tarix · cərimə · vəziyyət).</summary>
        public string GecikmeMetni => GecikmeSetirleri.Count > 0
            ? string.Join(Environment.NewLine, GecikmeSetirleri)
            : GecikmeVar ? "Gecikmə" : string.Empty;

        /// <summary>Kreditin başlama tarixindən əvvəl edilmiş ödəniş.</summary>
        public bool ErkenOdenis => XaricindeHedd && Novu == "Müddətdən əvvəl";

        /// <summary>Bu ayın ödənişi edilibmi?</summary>
        [ObservableProperty] private bool odenilib;

        /// <summary>Bu ay üçün ödənilmiş məbləğ (AZN) — hissə-hissə ödənişlərin CƏMİ.</summary>
        [ObservableProperty] private decimal odenilenMebleg;

        /// <summary>Sonuncu ödənişin tarixi.</summary>
        [ObservableProperty] private DateTime? odenilmeTarixi;

        /// <summary>Bu ay üzrə ödənişlərin sayı (hissə-hissə ödənişlər daxil).</summary>
        [ObservableProperty] private int odenisSayi;

        /// <summary>Bu ay üzrə bütün ödənişlərin tarixləri (vergüllə): "11.09.2026, 12.09.2026, 15.09.2026".</summary>
        [ObservableProperty] private string odenisTarixleri = string.Empty;

        /// <summary>Ödənişlərin ətraflı siyahısı (alət ipucu üçün).</summary>
        [ObservableProperty] private string odenisDetali = string.Empty;

        /// <summary>Bu aya aid "Kredit əlavə gəlir" qeydlərinin Id-ləri.</summary>
        public List<int> TransactionIds { get; set; } = new();

        // ====================================================================
        //  TƏRƏFDAŞ MƏNFƏƏT BÖLGÜSÜ  (ay üzrə)
        // --------------------------------------------------------------------
        //  Həmin ayın «Kredit əlavə gəlir» qeydlərində kimə nə qədər pay
        //  verildiyi (məs. Asiman 79,50 ₼ · Zaur 95,40 ₼) qrafikdə göstərilir.
        // ====================================================================

        /// <summary>Bu ayın əməliyyatlarına bağlı TƏRƏFDAŞ PAYLARI.</summary>
        public IReadOnlyList<PartnerShare> TerefdasPaylari { get; set; } = Array.Empty<PartnerShare>();

        /// <summary>Bu ay üçün tərəfdaş bölgüsü varmı?</summary>
        public bool BolguVar => TerefdasPaylari.Count > 0;

        /// <summary>
        /// Qrafik üçün bölgü mətni — hər tərəfdaş AYRI SƏTİRDƏ:
        /// «Zaur 95,40 ₼», «Eşqin 79,50 ₼», «Asiman 79,50 ₼», «Asif 667,80 ₼»…
        /// </summary>
        public string TerefdasMetni => TerefdasPaylari.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, TerefdasPaylari
                .OrderBy(p => p.Sira)
                .Select(p => $"{p.Terefdas} {p.Mebleg:N2} ₼"));

        /// <summary>Bir sətirdə qısa bölgü mətni (alət ipucu / veb cədvəli üçün).</summary>
        public string TerefdasQisa => TerefdasPaylari.Count == 0
            ? string.Empty
            : string.Join(" · ", TerefdasPaylari
                .OrderBy(p => p.Sira)
                .Select(p => $"{p.Terefdas} {p.Mebleg:N2} ₼"));

        /// <summary>Tərəfdaş paylarının cəmi (₼).</summary>
        public decimal TerefdasCemi => TerefdasPaylari.Sum(p => p.Mebleg);

        /// <summary>Gecikmə: ödənilməyib və plan tarixi keçib, ya da plan tarixindən sonra ödənilib.</summary>
        public bool Gecikmis
        {
            get
            {
                // Möhlət verilibsə ödəniş gecikmə sayılmır.
                if (MohletAktiv)
                {
                    return false;
                }

                // Açıq (ödənilməmiş) "Gecikmə" qeydi varsa sətir gecikmiş sayılır.
                if (GecikmeAktiv)
                {
                    return true;
                }

                if (Odenilib)
                {
                    return OdenilmeTarixi is DateTime paid && paid.Date > Tarix.Date;
                }

                return Tarix.Date < DateTime.Today;
            }
        }

        /// <summary>Tam ödənilməyib (NET plandan az) — möhlət verilibsə qırmızı sayılmır.</summary>
        public bool AzOdenilib => Odenilib && !MohletAktiv && OdenilenMebleg > 0m && OdenilenMebleg < NetOdenis;

        /// <summary>Artıq ödənilib (NET plandan çox).</summary>
        public bool CoxOdenilib => Odenilib && OdenilenMebleg > NetOdenis;

        // ====================================================================
        //  AZ / ARTIQ ÖDƏNİŞ  (rənglə bildirmə + sonuncu aylara keçirmə)
        // --------------------------------------------------------------------
        //  • Az ödənilibsə  → sətir QIRMIZI olur və çatışmayan məbləğ yazılır.
        //  • Artıq ödənilibsə → yaşıl nişan qoyulur və artıq məbləğ SONUNCU
        //    aylardan çıxılır (bu sətirdə «ArtiqKecen» kimi göstərilir).
        // ====================================================================

        /// <summary>Plan məbləğindən AZ ödənilən hissə (₼).</summary>
        public decimal AzOdenisMeblegi => AzOdenilib ? Math.Round(NetOdenis - OdenilenMebleg, 2) : 0m;

        /// <summary>Plan məbləğindən ÇOX ödənilən hissə (₼) — bu ay edilən artıq ödəniş.</summary>
        public decimal ArtiqOdenisMeblegi => CoxOdenilib ? Math.Round(OdenilenMebleg - NetOdenis, 2) : 0m;

        /// <summary>
        /// Bu aydan SONRAKI aylardan çıxılan artıq ödəniş (₼).
        /// <para>
        /// Məsələn aylıq 1 800 ₼ ödənilməlidir, amma 2 000 ₼ ödənilib →
        /// <b>200 ₼</b> sonuncu aylardan çıxılır. Bu dəyər qrafikdə göstərilir.
        /// </para>
        /// <para>
        /// ⚠ YALNIZ o aylarda doldurulur ki, həmin aydan <b>FAKTİKİ OLARAQ</b>
        /// məbləğ çıxılır — yəni <b>SONUNCU</b> aylarda. Digər aylarda 0 olur.
        /// </para>
        /// </summary>
        [ObservableProperty] private decimal artiqKecen;

        /// <summary>Bu aydan artıq ödəniş çıxılıbmı?</summary>
        public bool ArtiqKecenVar => ArtiqKecen != 0m;

        /// <summary>
        /// Bu ayın <b>NET</b> plan məbləği: <c>Odenis − ArtiqKecen</c>.
        /// <para>
        /// Müştərinin bu ay faktiki ödəməli olduğu məbləğ — son aylarda
        /// artıq ödəniş hesabına azalır.
        /// </para>
        /// </summary>
        public decimal NetOdenis => Math.Max(0m, Math.Round(Odenis - ArtiqKecen, 2));

        /// <summary>Az ödəniş nişanı mətni: «⚠ 200,00 ₼ az ödənilib».</summary>
        public string AzOdenisMetni => AzOdenilib
            ? $"⚠ {AzOdenisMeblegi:N2} ₼ az ödənilib"
            : string.Empty;

        /// <summary>Artıq ödəniş nişanı mətni: «➕ 200,00 ₼ artıq — son aylardan çıxılır».</summary>
        public string ArtiqOdenisMetni => ArtiqOdenisMeblegi > 0m
            ? $"➕ {ArtiqOdenisMeblegi:N2} ₼ artıq ödəniş"
            : string.Empty;

        /// <summary>
        /// Sonuncu aylardan <b>çıxılan</b> (artıq ödəniş) və ya son aylara
        /// <b>əlavə olunan</b> (az ödəniş) məbləğin mətni.
        /// <para>
        /// ⬇ müsbət = «bu aydan çıxıldı» ✗ · ⬆ mənfi = «bu aya əlavə olundu» ✓
        /// </para>
        /// </summary>
        public string ArtiqKecenMetni => ArtiqKecen > 0m
            ? $"⬇ {ArtiqKecen:N2} ₼ çıxıldı → {NetOdenis:N2} ₼"
            : ArtiqKecen < 0m
                ? $"⬆ {Math.Abs(ArtiqKecen):N2} ₼ əlavə olundu → {NetOdenis:N2} ₼"
                : string.Empty;

        /// <summary>
        /// Artıq ödəniş bu aydan çıxıldıqda <b>bütün</b> asılı göstəriciləri
        /// yeniləyir (rəng, mətn, az/artıq nişanları).
        /// </summary>
        partial void OnArtiqKecenChanged(decimal value)
        {
            OnPropertyChanged(nameof(ArtiqKecenVar));
            OnPropertyChanged(nameof(NetOdenis));
            OnPropertyChanged(nameof(ArtiqKecenMetni));
            OnPropertyChanged(nameof(AzOdenilib));
            OnPropertyChanged(nameof(CoxOdenilib));
            OnPropertyChanged(nameof(AzOdenisMeblegi));
            OnPropertyChanged(nameof(ArtiqOdenisMeblegi));
            OnPropertyChanged(nameof(AzOdenisMetni));
            OnPropertyChanged(nameof(ArtiqOdenisMetni));
        }

        partial void OnOdenilibChanged(bool value) => RaiseStatus();

        partial void OnOdenilenMeblegChanged(decimal value) => RaiseStatus();

        partial void OnOdenilmeTarixiChanged(DateTime? value) => RaiseStatus();

        // ====================================================================
        //  📅 PLAN TARİXİ vs FAKTİKİ ÖDƏNİŞ TARİXİ
        // --------------------------------------------------------------------
        //  «Ödəniş Tarixi» sütunu PLAN tarixidir (kreditin başlama tarixi + ay) ✓
        //  Faktiki ödəniş tarixi isə «Ödənilən / Ödəniş edildiyi tarixlər»-dədir ✓
        //  Aşağıdaki göstəricilər fərqi AYDINLAŞDIRIR (erkən / gecikmiş) ✓
        // ====================================================================

        /// <summary>Ödəniş PLAN tarixindən fərqlidir? (erkən və ya gecikmiş)</summary>
        public bool TarixFergli => OdenilmeTarixi is DateTime t && t.Date != Tarix.Date;

        /// <summary>Fərq mətni: «5 gün gecikmə» / «3 gün erkən» / «plan üzrə».</summary>
        public string TarixFerqMetni
        {
            get
            {
                if (OdenilmeTarixi is not DateTime t)
                {
                    return string.Empty;
                }

                var gun = (int)(t.Date - Tarix.Date).TotalDays;

                if (gun == 0)
                {
                    return "plan üzrə";
                }

                return gun > 0 ? $"⚠ {gun} gün gecikmə" : $"⚡ {Math.Abs(gun)} gün erkən";
            }
        }

        /// <summary>Fərq rəngi: gecikmə qırmızı, erkən yaşıl, plan üzrə boz.</summary>
        public string TarixFerqReng
        {
            get
            {
                if (OdenilmeTarixi is not DateTime t)
                {
                    return "#6B7280";
                }

                var gun = (int)(t.Date - Tarix.Date).TotalDays;

                return gun > 0 ? "#FB7185" : gun < 0 ? "#4ADE80" : "#6B7280";
            }
        }

        private void RaiseStatus()
        {
            OnPropertyChanged(nameof(Gecikmis));
            OnPropertyChanged(nameof(AzOdenilib));
            OnPropertyChanged(nameof(CoxOdenilib));
            OnPropertyChanged(nameof(TarixFergli));
            OnPropertyChanged(nameof(TarixFerqMetni));
            OnPropertyChanged(nameof(TarixFerqReng));
        }
    }
}
