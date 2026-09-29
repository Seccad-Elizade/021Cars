using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// Silinmiş qeydin TAM surəti (snapshot).
    /// <para>
    /// Hər silmə əməliyyatından ƏVVƏL bu surət diskə JSON kimi yazılır.
    /// Beləliklə istənilən silinmiş məlumat <b>geri qaytarıla bilir</b>
    /// («Ctrl+Z» və ya «🗑 Silinənlər» bölməsi).
    /// </para>
    /// </summary>
    public sealed class TrashSnapshot
    {
        /// <summary>Nə silinib: «Avtomobil», «Xərc», «Satış», «Kredit».</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// Əməliyyat növü:
        /// <list type="bullet">
        ///   <item><c>"Silinmə"</c> — qeyd silinib, bərpada YENİDƏN yaradılır.</item>
        ///   <item><c>"Dəyişiklik"</c> — qeyd redaktə edilib, bərpada ƏVVƏLKİ vəziyyətinə qaytarılır.</item>
        /// </list>
        /// </summary>
        public string Action { get; set; } = "Silinmə";

        /// <summary>Dəyişiklikdirsə <c>true</c>.</summary>
        [NotMapped]
        [JsonIgnore]
        public bool IsEdit => Action == "Dəyişiklik";

        /// <summary>Cədvəldə göstərilən qısa ad (məs. «BMW 528 (10-VL-330)»).</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Əsas məbləğ (məlumat üçün).</summary>
        public decimal Amount { get; set; }

        /// <summary>Silinmə zamanı.</summary>
        public DateTime DeletedAt { get; set; } = DateTime.Now;

        /// <summary>Əlavə izah (neçə qeyd silindiyi və s.).</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>Diskdəki fayl adı — yalnız bərpa üçün (JSON-a yazılmır).</summary>
        [JsonIgnore]
        public string FileName { get; set; } = string.Empty;

        // ---- Surəti saxlanılan qeydlər ----

        public CarItem? Car { get; set; }
        public List<ExpenseItem> Expenses { get; set; } = new();
        public List<Sale> Sales { get; set; } = new();
        public List<Credit> Credits { get; set; } = new();
        public List<CreditTransaction> Transactions { get; set; } = new();
        public List<MediaAttachment> Attachments { get; set; } = new();

        /// <summary>Surətdə saxlanılan qeydlərin ümumi sayı.</summary>
        [NotMapped]
        public int TotalRecords =>
            (Car is null ? 0 : 1) + Expenses.Count + Sales.Count
            + Credits.Count + Transactions.Count + Attachments.Count;

        /// <summary>Cədvəldə göstərilən izah mətni.</summary>
        [NotMapped]
        public string Summary
        {
            get
            {
                var parts = new List<string>();

                if (Car is not null) parts.Add("1 avtomobil");
                if (Expenses.Count > 0) parts.Add($"{Expenses.Count} xərc");
                if (Sales.Count > 0) parts.Add($"{Sales.Count} satış");
                if (Credits.Count > 0) parts.Add($"{Credits.Count} kredit");
                if (Transactions.Count > 0) parts.Add($"{Transactions.Count} əməliyyat");
                if (Attachments.Count > 0) parts.Add($"{Attachments.Count} sənəd");

                return parts.Count == 0 ? "—" : string.Join(" · ", parts);
            }
        }
    }
}
