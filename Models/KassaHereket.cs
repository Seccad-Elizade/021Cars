using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// <b>💵 KASSA HƏRƏKƏTİ (əl ilə yazılan)</b> — kassaya qoyulan / kassadan
    /// götürülən pul ✓✓✓
    /// <para>
    /// Kassa bölməsindəki hərəkətlərin <b>BÖYÜK HİSSƏSİ AVTOMATİK</b> hesablanır ✓
    /// (satışlar ✓ kredit ödənişləri ✓ ilkin ödənişlər ✓ bütün xərclər ✓
    /// tərəfdaş bölgüsü ✓) — bax <see cref="Services.KassaHesabi"/>.
    /// </para>
    /// <para>
    /// Bu cədvəl isə <b>yalnız istifadəçinin ƏL İLƏ yazdığı</b> hərəkətlər üçündür:
    /// «kassaya pul qoyuldu» ✓ · «kassadan pul götürüldü» ✓ · «bank köçürməsi» ✓.
    /// </para>
    /// </summary>
    public sealed partial class KassaHereket : ObservableObject, IBuludIdli
    {
        /// <summary>Növ: kassaya <b>daxil olan</b> pul.</summary>
        public const string NovDaxilolma = "Daxilolma";

        /// <summary>Növ: kassadan <b>çıxan</b> pul.</summary>
        public const string NovXerc = "Xərc";

        /// <summary>
        /// 🔑 <b>SİNXRON AÇARI</b> ✓✓✓ (v6.2.16) — bax <see cref="IBuludIdli"/>.
        /// Köhnə qeydlərdə boş ✗ → rəqəm ID işlədilir ✓
        /// </summary>
        public string? BuludId { get; set; }

        public int Id { get; set; }

        /// <summary>Əməliyyatın tarixi.</summary>
        [ObservableProperty] private DateTime tarix = DateTime.Today;

        /// <summary>Növ: <see cref="NovDaxilolma"/> və ya <see cref="NovXerc"/>.</summary>
        [ObservableProperty] private string nov = NovDaxilolma;

        /// <summary>
        /// Kateqoriya — «Kapital qoyuluşu» ✓ «Sahibə verildi» ✓ «Banka köçürüldü» ✓
        /// «Digər» ✓ (sərbəst mətn — istifadəçi yaza bilər).
        /// </summary>
        [ObservableProperty] private string kateqoriya = KassaKategoriyalari.DigerDaxilolma;

        /// <summary>Məbləğ (₼).</summary>
        [ObservableProperty] private decimal mebleg;

        /// <summary>Ödəniş üsulu (Nağd · Kart / Köçürmə).</summary>
        [ObservableProperty] private string odenisUsulu = Catalog.PaymentMethods[0];

        /// <summary>Əlavə qeyd.</summary>
        [ObservableProperty] private string qeyd = string.Empty;

        /// <summary>Daxilolmaya bağlı gəlir əməliyyatı (varsa).</summary>
        public int? CreditId { get; set; }

        public Credit? Credit { get; set; }

        /// <summary>Daxilolmaya bağlı satış (varsa).</summary>
        public int? SaleId { get; set; }

        public Sale? Sale { get; set; }

        /// <summary>Daxilolma növüdürmü? (<c>false</c> = xərc)</summary>
        [NotMapped]
        public bool IsDaxilolma => Nov == NovDaxilolma;

        /// <summary>Tarix mətni: «21.09.2026».</summary>
        [NotMapped]
        public string TarixMetni => Tarix.ToString("dd.MM.yyyy");

        /// <summary>Məbləğ mətni: «1 500,00 ₼».</summary>
        [NotMapped]
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        /// <summary>Növ mətni: «➕ Daxilolma» · «➖ Xərc».</summary>
        [NotMapped]
        public string NovMetni => IsDaxilolma ? "➕ Daxilolma" : "➖ Xərc";

        /// <summary>Növ rəngi (hex).</summary>
        [NotMapped]
        public string NovRengi => IsDaxilolma ? "#34D399" : "#FB7185";

        /// <summary>İşarəli məbləğ: daxilolma <c>+</c>, xərc <c>−</c>.</summary>
        [NotMapped]
        public decimal IsareliMebleg => IsDaxilolma ? Mebleg : -Mebleg;

        public override string ToString() => $"{TarixMetni} · {NovMetni} · {Kateqoriya} · {MeblegMetni}";

        partial void OnNovChanged(string value)
        {
            OnPropertyChanged(nameof(IsDaxilolma));
            OnPropertyChanged(nameof(NovMetni));
            OnPropertyChanged(nameof(NovRengi));
            OnPropertyChanged(nameof(IsareliMebleg));
        }

        partial void OnMeblegChanged(decimal value)
        {
            OnPropertyChanged(nameof(MeblegMetni));
            OnPropertyChanged(nameof(IsareliMebleg));
        }

        partial void OnTarixChanged(DateTime value) => OnPropertyChanged(nameof(TarixMetni));

        partial void OnKateqoriyaChanged(string value)
        {
            // Kateqoriya seçiminə uyğun növ avtomatik təyin olunur ✓✓✓
            var tapilan = KassaKategoriyalari.NovOf(value);
            if (tapilan is not null && tapilan != Nov)
            {
                Nov = tapilan;
            }
        }
    }

    /// <summary>
    /// 💵 Kassa hərəkətlərinin <b>kateqoriya siyahısı</b> — növə görə ✓✓✓
    /// (UI-də ComboBox məzmunu; sərbəst mətn də yazıla bilər ✓)
    /// </summary>
    public static class KassaKategoriyalari
    {
        public const string Kapital = "Kapital qoyuluşu";
        public const string Sahibe = "Sahibə verildi";
        public const string Banka = "Banka köçürüldü";
        public const string DigerDaxilolma = "Digər daxilolma";
        public const string DigerXerc = "Digər xərc";

        /// <summary>Daxilolma kateqoriyaları (➕).</summary>
        public static IReadOnlyList<string> Daxilolmalar { get; } = new[]
        {
            Kapital, "Kredit / Borc alındı", "Banka hesabından", DigerDaxilolma
        };

        /// <summary>Xərc kateqoriyaları (➖).</summary>
        public static IReadOnlyList<string> Xercler { get; } = new[]
        {
            Sahibe, Banka, "Borc qaytarıldı", DigerXerc
        };

        /// <summary>Verilmiş kateqoriyanın növü (tapılmazsa <c>null</c>).</summary>
        public static string? NovOf(string? kateqoriya)
        {
            if (string.IsNullOrWhiteSpace(kateqoriya))
            {
                return null;
            }

            if (Daxilolmalar.Contains(kateqoriya))
            {
                return KassaHereket.NovDaxilolma;
            }

            return Xercler.Contains(kateqoriya) ? KassaHereket.NovXerc : null;
        }
    }
}
