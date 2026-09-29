using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using EnterpriseAeroStudio.Models;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// Yeni avtomobil əlavə etmə formasının vəziyyəti və validasiyası.
    /// <see cref="ObservableValidator"/> vasitəsilə INotifyDataErrorInfo tətbiq edir.
    /// </summary>
    public sealed partial class CarEditorViewModel : ObservableValidator
    {
        public IReadOnlyList<string> FuelTypes { get; } = Catalog.FuelTypes;

        public IReadOnlyList<string> CarStatuses { get; } = Catalog.CarStatuses;

        [ObservableProperty]
        [Required(ErrorMessage = "Marka / model tələb olunur.")]
        [MaxLength(150, ErrorMessage = "Marka 150 simvoldan çox ola bilməz.")]
        private string marka = string.Empty;

        [ObservableProperty]
        [Required(ErrorMessage = "Dövlət nömrəsi tələb olunur.")]
        [CustomValidation(typeof(CarEditorViewModel), nameof(ValidateRegistration))]
        private string qeydiyyatNisani = string.Empty;

        [ObservableProperty]
        [CustomValidation(typeof(CarEditorViewModel), nameof(ValidateVin))]
        private string vin = string.Empty;

        [ObservableProperty]
        [Range(1900, 2100, ErrorMessage = "İl 1900-2100 aralığında olmalıdır.")]
        private int il = DateTime.Today.Year;

        [ObservableProperty]
        [Range(0, int.MaxValue, ErrorMessage = "Yürüş mənfi ola bilməz.")]
        private int yurus;

        [ObservableProperty]
        private string yanacaq = Catalog.FuelTypes[0];

        [ObservableProperty]
        private DateTime? alisTarixi = DateTime.Today;

        [ObservableProperty]
        [RegularExpression(@"^$|^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Saat formatı SS:DD olmalıdır (məs. 12:15).")]
        private string alisSaati = string.Empty;

        /// <summary>Alış üsulları: Nağd / Barter (ComboBox məzmunu).</summary>
        public IReadOnlyList<string> PurchaseMethods { get; } = Catalog.PurchaseMethods;

        /// <summary>Alış ödəniş üsulu — "Nağd" və ya "Barter".</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBarter))]
        private string alisUsulu = Catalog.CashPurchase;

        /// <summary>Barter zamanı verilən əvəzin təsviri.</summary>
        [ObservableProperty]
        [MaxLength(300, ErrorMessage = "Barter təsviri 300 simvoldan çox ola bilməz.")]
        private string barterTesviri = string.Empty;

        /// <summary>
        /// Barter üçün avto parkdan verilən avtomobil (ComboBox seçimi).
        /// Seçildikdə dəyəri avtomatik götürülür.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BarterSummary))]
        private CarItem? barterCar;

        /// <summary>Seçilmiş əvəz avtomobilin Id-si (modelə yazılır).</summary>
        public int? BarterCarId { get; private set; }

        /// <summary>Barter zamanı verilən maşının dəyəri (AZN).</summary>
        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Barter dəyəri mənfi ola bilməz.")]
        [NotifyPropertyChangedFor(nameof(BarterSummary))]
        private decimal barterDeyeri;

        /// <summary>Barter üçün seçilə bilən avto park maşınları (ComboBox məzmunu).</summary>
        public ObservableCollection<CarItem> BarterCandidates { get; } = new();

        /// <summary>
        /// Barter hesablaşmasının canlı izahı (formada göstərilir).
        /// <para>
        /// Verilən maşının <b>maya dəyəri</b> (alış + xərclər) əsas götürülür —
        /// satış qiyməti deyil.
        /// </para>
        /// </summary>
        public string BarterSummary
        {
            get
            {
                if (!IsBarter)
                {
                    return string.Empty;
                }

                if (BarterCar is null)
                {
                    return BarterDeyeri <= 0m
                        ? "Əvəz maşını seçin — maya dəyəri avtomatik gələcək."
                        : $"Verilən maşının dəyəri: {BarterDeyeri:N2} ₼";
                }

                var lines = new System.Text.StringBuilder();

                lines.AppendLine($"🧮 {BarterCar.DisplayName} maya dəyəri:");
                lines.AppendLine($"     Alış qiyməti      {BarterCar.AlisQiymeti:N2} ₼");
                lines.AppendLine($"   + Əlavə xərclər     {BarterCar.Xercler:N2} ₼");
                lines.AppendLine($"   = MAYA DƏYƏRİ       {BarterCar.MayaDeyeri:N2} ₼");

                var difference = AlisQiymeti - BarterDeyeri;

                lines.AppendLine();
                lines.AppendLine("⚖️ Hesablaşma:");
                lines.AppendLine($"     Alınan maşın      {AlisQiymeti:N2} ₼");
                lines.AppendLine($"   − Verilən (maya)    {BarterDeyeri:N2} ₼");

                if (difference > 0m)
                {
                    lines.Append($"   = Əlavə ödənilib    {difference:N2} ₼");
                }
                else if (difference < 0m)
                {
                    lines.Append($"   = Geri alınıb       {Math.Abs(difference):N2} ₼");
                }
                else
                {
                    lines.Append("   = Bərabər dəyişmə");
                }

                return lines.ToString();
            }
        }

        /// <summary>Əvəz maşın seçildikdə Id-ni saxlayır və dəyəri avtomatik doldurur.</summary>
        partial void OnBarterCarChanged(CarItem? value)
        {
            BarterCarId = value?.Id;

            if (value is null)
            {
                return;
            }

            // MAYA DƏYƏRİ istifadə olunur (satış qiyməti DEYİL): maşın əldən
            // çıxır və sizə başa gəldiyi dəyər hesaba alınır.
            BarterDeyeri = value.MayaDeyeri;
        }

        /// <summary>Barter seçilibmi? — XAML-də əvəz sahəsini göstərmək üçün.</summary>
        public bool IsBarter => string.Equals(AlisUsulu, Catalog.BarterPurchase, StringComparison.Ordinal);

        /// <summary>Üsul dəyişdikdə Nağd seçilibsə barter sahələri təmizlənir.</summary>
        partial void OnAlisUsuluChanged(string value)
        {
            if (string.Equals(value, Catalog.BarterPurchase, StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(BarterSummary));
                return;
            }

            BarterTesviri = string.Empty;
            BarterCar = null;
            BarterCarId = null;
            BarterDeyeri = 0m;
            OnPropertyChanged(nameof(BarterSummary));
        }

        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Alış qiyməti mənfi ola bilməz.")]
        [NotifyPropertyChangedFor(nameof(BarterSummary))]
        private decimal alisQiymeti;

        [ObservableProperty]
        [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "Satış qiyməti mənfi ola bilməz.")]
        private decimal satisQiymeti;

        [ObservableProperty]
        private string status = Catalog.CarStatuses[0];

        [ObservableProperty]
        [MaxLength(20, ErrorMessage = "Kredit nömrəsi 20 simvoldan çox ola bilməz.")]
        private string kreditNomresi = string.Empty;

        /// <summary>Sıra nömrəsi. Boş (0) buraxılarsa avtomatik ardıcıl verilir.</summary>
        [ObservableProperty]
        [Range(0, 999999, ErrorMessage = "Sıra nömrəsi 0-999999 aralığında olmalıdır.")]
        private int siraNomresi;

        /// <summary>Bütün xassələri yoxlayır; forma etibarlıdırsa true qaytarır.</summary>
        public bool ValidateForm()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        /// <summary>Formanı başlanğıc vəziyyətinə qaytarır.</summary>
        public void Reset()
        {
            Marka = string.Empty;
            QeydiyyatNisani = string.Empty;
            Vin = string.Empty;
            Il = DateTime.Today.Year;
            Yurus = 0;
            Yanacaq = Catalog.FuelTypes[0];
            AlisTarixi = DateTime.Today;
            AlisSaati = string.Empty;
            AlisUsulu = Catalog.CashPurchase;
            BarterTesviri = string.Empty;
            BarterCar = null;
            BarterDeyeri = 0m;
            AlisQiymeti = 0m;
            SatisQiymeti = 0m;
            Status = Catalog.CarStatuses[0];
            KreditNomresi = string.Empty;
            SiraNomresi = 0;
            ClearErrors();
        }

        /// <summary>Formadan yeni <see cref="CarItem"/> yaradır.</summary>
        public CarItem ToModel() => new()
        {
            Marka = Marka.Trim(),
            QeydiyyatNisani = QeydiyyatNisani.Trim().ToUpperInvariant(),
            Vin = Vin.Trim().ToUpperInvariant(),
            Il = Il,
            Yurus = Yurus,
            Yanacaq = Yanacaq,
            AlisTarixi = AlisTarixi,
            AlisSaati = AlisSaati.Trim(),
            AlisUsulu = string.IsNullOrWhiteSpace(AlisUsulu) ? Catalog.CashPurchase : AlisUsulu.Trim(),
            BarterTesviri = BarterTesviri.Trim(),
            BarterCarId = IsBarter ? BarterCarId : null,
            BarterDeyeri = IsBarter ? BarterDeyeri : 0m,
            AlisQiymeti = AlisQiymeti,
            SatisQiymeti = SatisQiymeti,
            Status = Status,
            KreditNomresi = KreditNomresi.Trim(),
            SiraNomresi = SiraNomresi
        };

        public static ValidationResult? ValidateRegistration(string? value, ValidationContext context)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ValidationResult("Dövlət nömrəsi tələb olunur.");
            }

            if (!Regex.IsMatch(text.ToUpperInvariant(), @"^\d{2}\s?-?[A-Z]{2}\s?-?\d{3}$"))
            {
                return new ValidationResult("Format yanlışdır. Nümunə: 77HY717 və ya 90-XX-999.");
            }

            return ValidationResult.Success;
        }

        public static ValidationResult? ValidateVin(string? value, ValidationContext context)
        {
            var text = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(text))
            {
                return ValidationResult.Success; // VIN məcburi deyil.
            }

            if (text.Length != 17)
            {
                return new ValidationResult("VIN kod tam olaraq 17 simvoldan ibarət olmalıdır.");
            }

            if (!Regex.IsMatch(text, "^[A-HJ-NPR-Z0-9]{17}$"))
            {
                return new ValidationResult("VIN kod yalnız hərf və rəqəmlərdən ibarət olmalıdır (I, O, Q istisna).");
            }

            return ValidationResult.Success;
        }
    }
}
