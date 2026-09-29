using System.ComponentModel.DataAnnotations.Schema;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// Avtomobil parkındakı bir avtomobili təmsil edən əsas biznes obyekti.
    /// </summary>
    public class CarItem
    {
        public int Id { get; set; }

        /// <summary>Marka / model adı.</summary>
        public string Marka { get; set; } = string.Empty;

        /// <summary>Dövlət qeydiyyat nömrəsi (məs. 77HY717).</summary>
        public string QeydiyyatNisani { get; set; } = string.Empty;

        /// <summary>VIN kod (17 simvol, boş ola bilər).</summary>
        public string Vin { get; set; } = string.Empty;

        /// <summary>Buraxılış ili.</summary>
        public int Il { get; set; }

        /// <summary>Yürüş (km).</summary>
        public int Yurus { get; set; }

        /// <summary>Yanacaq növü (Benzin, Dizel, ...).</summary>
        public string Yanacaq { get; set; } = string.Empty;

        /// <summary>Alış tarixi.</summary>
        public DateTime? AlisTarixi { get; set; }

        /// <summary>Alış saatı (sərbəst mətn, məs. 12:15).</summary>
        public string AlisSaati { get; set; } = string.Empty;

        /// <summary>
        /// Alış ödəniş üsulu — <c>"Nağd"</c> və ya <c>"Barter"</c>.
        /// Barter seçildikdə <see cref="BarterTesviri"/> sahəsi doldurulur.
        /// </summary>
        public string AlisUsulu { get; set; } = "Nağd";

        /// <summary>
        /// Barter zamanı avtomobil əvəzinə verilən əvəz
        /// (sərbəst mətn — məs. «10 000 ₼ + Nexia»).
        /// </summary>
        public string BarterTesviri { get; set; } = string.Empty;

        /// <summary>
        /// Barter zamanı AVTO PARKDAN verilən avtomobilin Id-si.
        /// Məs. yeni maşını almaq üçün köhnə maşın verilibsə.
        /// </summary>
        public int? BarterCarId { get; set; }

        /// <summary>Barter üçün verilən avtomobil (əllə doldurulur — EF izləmir).</summary>
        [NotMapped]
        public CarItem? BarterCar { get; set; }


        /// <summary>
        /// Barter zamanı verilən avtomobilin dəyəri (AZN — snapshot).
        /// Alış anındaki maya dəyəri saxlanılır ki, sonrakı dəyişikliklər
        /// tarixi məlumatı pozmasın.
        /// </summary>
        public decimal BarterDeyeri { get; set; }

        /// <summary>
        /// Bu avtomobil barter SATIŞI nəticəsində parka gəlibsə,
        /// həmin satışın Id-si. Beləliklə alınan maşının maya dəyəri
        /// (barter dəyəri) və hansı satışdan gəldiyi izlənə bilir.
        /// </summary>
        public int? BarterSaleId { get; set; }

        /// <summary>Alış qiyməti (AZN).</summary>
        public decimal AlisQiymeti { get; set; }

        /// <summary>Nəzərdə tutulan satış qiyməti (AZN) — redaktə oluna bilər.</summary>
        public decimal SatisQiymeti { get; set; }

        /// <summary>Cari status (Stokda, Satışda, Kreditdə, ...).</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Kredit sıra nömrəsi (məs. KR-0001). Avtomobil "Kreditdə" statusuna
        /// keçdikdə avtomatik ardıcıl təyin olunur və istifadəçi tərəfindən dəyişdirilə bilər.
        /// </summary>
        public string KreditNomresi { get; set; } = string.Empty;

        /// <summary>Yaradılma zamanı (audit).</summary>
        public DateTime YaradilmaTarixi { get; set; } = DateTime.Now;

        /// <summary>Avtomobilə bağlı bütün xərclər.</summary>
        public ICollection<ExpenseItem> Expenses { get; set; } = new List<ExpenseItem>();

        /// <summary>Avtomobilə bağlı kreditlər.</summary>
        public ICollection<Credit> Credits { get; set; } = new List<Credit>();

        /// <summary>
        /// Avtomobilin sıra nömrəsi. Boş (0) buraxılarsa avtomatik ardıcıl verilir;
        /// istifadəçi tərəfindən də qoyula və ya dəyişdirilə bilər.
        /// </summary>
        public int SiraNomresi { get; set; }

        /// <summary>Avtomobilə bağlı sənəd / media fayllarının sayı (yalnız görünüş üçün).</summary>
        [NotMapped]
        public int SenedSayi { get; set; }

        /// <summary>
        /// Cədvəldə göstərilən ALIŞ TARİXİ: «21.09.2026».
        /// <para>Alış tarixi yoxdursa «—» göstərilir.</para>
        /// </summary>
        [NotMapped]
        public string AlisTarixiMetni => AlisTarixi?.ToString("dd.MM.yyyy") ?? "—";

        /// <summary>
        /// ALIŞ TARİXİ + SAATI bir yerdə: «21.09.2026 · 12:15».
        /// <para>Saat boşdursa yalnız tarix, tarix boşdursa «—» göstərilir.</para>
        /// </summary>
        [NotMapped]
        public string AlisVaxtiMetni
        {
            get
            {
                var tarix = AlisTarixi?.ToString("dd.MM.yyyy");
                var saat = string.IsNullOrWhiteSpace(AlisSaati) ? string.Empty : AlisSaati.Trim();

                if (tarix is null)
                {
                    return saat.Length == 0 ? "—" : saat;
                }

                return saat.Length == 0 ? tarix : $"{tarix} · {saat}";
            }
        }

        /// <summary>
        /// Alış tarixi doldurulmayıbsa geriyə qeydin YARADILMA tarixini qaytarır.
        /// <para>
        /// Köhnə qeydlərdə «Alış Tarixi» boş ola bilir; hesablamalarda
        /// (dövr filtri, qrafik) boş tarix yaratmasın deyə bu sahə işlədilir.
        /// </para>
        /// </summary>
        [NotMapped]
        public DateTime AlisTarixiTam => AlisTarixi ?? YaradilmaTarixi;

        /// <summary>«Alış Tarixi» tamamlanmış mətni: «21.09.2026» (həmişə dolu).</summary>
        [NotMapped]
        public string AlisTarixiTamMetni => AlisTarixiTam.ToString("dd.MM.yyyy");

        /// <summary>Cədvəldə göstərilən sənəd göstəricisi: "📎 3" və ya "—".</summary>
        [NotMapped]
        public string SenedMetni => SenedSayi > 0 ? $"📎 {SenedSayi}" : "—";

        /// <summary>Grid və ComboBox-larda göstərilən birləşdirilmiş ad.</summary>
        [NotMapped]
        public string DisplayName =>
            string.IsNullOrWhiteSpace(QeydiyyatNisani) ? Marka : $"{Marka} ({QeydiyyatNisani})";

        /// <summary>ComboBox kimi yerlərdə sinif adı yerinə düzgün mətn göstərilsin.</summary>
        public override string ToString() => DisplayName;

        /// <summary>
        /// 🚀 <b>SQL-DƏ HESABLANMIŞ XƏRC CƏMİ</b> ✓✓✓ (yalnız «Alış» xaric ✓)
        /// <para>
        /// ⚠ Avtomobil siyahısı yüklənərkən <b>1 000 000 xərc sətri yaddaşa
        /// yüklənmir</b> ✗ → SQL <c>GROUP BY</c> ilə cəm alınır ✓✓✓
        /// </para>
        /// </summary>
        [NotMapped]
        public decimal XerclerCemi { get; set; }

        /// <summary>🚀 SQL-də hesablanmış BÜTÜN xərclərin cəmi ✓ (ixrac üçün ✓).</summary>
        [NotMapped]
        public decimal ButunXerclerCemi { get; set; }

        /// <summary>
        /// Avtomobilə çəkilmiş əlavə xərclər (avtomatik "Alış" qeydi xaric).
        /// <para>
        /// ✅ Xərc siyahısı yüklənməyibsə SQL-də hesablanmış
        /// <see cref="XerclerCemi"/> istifadə olunur ✓✓✓ (yaddaş qənaəti ✓)
        /// </para>
        /// </summary>
        [NotMapped]
        public decimal Xercler => Expenses is { Count: > 0 }
            ? Expenses.Where(e => e.Kategoriya != "Alış").Sum(e => e.Mebleg)
            : XerclerCemi;

        /// <summary>🚀 Bütün xərclərin cəmi ✓ («Alış» daxil ✓ — ixrac üçün ✓).</summary>
        [NotMapped]
        public decimal ButunXercler => Expenses is { Count: > 0 }
            ? Expenses.Sum(e => e.Mebleg)
            : ButunXerclerCemi;

        /// <summary>Alış qiyməti + əlavə xərclər = ümumi maya dəyəri.</summary>
        [NotMapped]
        public decimal MayaDeyeri => AlisQiymeti + Xercler;

        /// <summary>Alış barter ilə edilibsə <c>true</c>.</summary>
        [NotMapped]
        public bool IsBarter =>
            string.Equals(AlisUsulu, "Barter", StringComparison.OrdinalIgnoreCase);

        /// <summary>Cədvəllərdə göstərilən alış üsulu nişanı.</summary>
        [NotMapped]
        public string AlisUsuluMetni => IsBarter ? "🔄 Barter" : "💵 Nağd";

        /// <summary>
        /// Barter satışından gələn maşındırmı?
        /// (parka müştərinin verdiyi avtomobil kimi daxil olub)
        /// </summary>
        [NotMapped]
        public bool IsFromBarterSale => BarterSaleId.HasValue;

        /// <summary>
        /// Barter üçün verilən avtomobilin adı (varsa), yoxsa sərbəst təsvir.
        /// </summary>
        [NotMapped]
        public string BarterCarInfo =>
            BarterCar?.DisplayName is { Length: > 0 } name
                ? name
                : BarterTesviri;

        /// <summary>
        /// Barter zamanı əlavə ödənilən pul:
        /// alınan maşının qiyməti − verilən maşının dəyəri.
        /// Müsbət = ustaya əlavə pul verilib, mənfi = pul qaytarılıb.
        /// </summary>
        [NotMapped]
        public decimal BarterFerqi => IsBarter ? AlisQiymeti - BarterDeyeri : 0m;

        /// <summary>Barter fərqinin oxunaqlı mətni.</summary>
        [NotMapped]
        public string BarterFerqiMetni
        {
            get
            {
                if (!IsBarter || BarterDeyeri <= 0m)
                {
                    return "—";
                }

                if (BarterFerqi > 0m)
                {
                    return $"əlavə {BarterFerqi:N2} ₼ verilib".Replace(",", " ");
                }

                if (BarterFerqi < 0m)
                {
                    return $"{Math.Abs(BarterFerqi):N2} ₼ qaytarılıb".Replace(",", " ");
                }

                return "bərabər dəyişmə";
            }
        }
    }
}
