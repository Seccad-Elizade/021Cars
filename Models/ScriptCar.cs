using System.Text.Json.Serialization;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// 📜 <b>SKRİPT AVTOMOBİLİ</b> — istifadəçinin yapışdırdığı JSON-da bir avtomobil.
    /// <para>
    /// Nümunə:
    /// <code>
    /// {
    ///   "SiraNomresi": null,
    ///   "MarkaModel": "Mercedes E240",
    ///   "DovletNomresi": "90-DV-174",
    ///   "Il": 1998,
    ///   "AlisTarixi": "09.07.2023",
    ///   "AlisQiymeti": 10820.00,
    ///   "Xercler": [ { "Təsvir": "Elsen", "Tarix": "09.07.2023", "Məbləğ": 40.00 } ],
    ///   "Qeydler": "Texniki baxışı 30.07.23-də bitir…"
    /// }
    /// </code>
    /// </para>
    /// <para>
    /// ⚠ <c>SiraNomresi</c> boş (null ✓) buraxılırsa proqram <b>avtomatik</b> ardıcıl
    /// nömrə verir ✓✓✓
    /// </para>
    /// </summary>
    public sealed class ScriptCar
    {
        /// <summary>Sıra nömrəsi (boş ola bilər → avtomatik verilir ✓).</summary>
        [JsonPropertyName("SiraNomresi")]
        public int? SiraNomresi { get; set; }

        /// <summary>Marka / model (məs. «Mercedes E240» ✓).</summary>
        [JsonPropertyName("MarkaModel")]
        public string? MarkaModel { get; set; }

        /// <summary>Marka / model sahəsinin alternativ adı ✓.</summary>
        [JsonPropertyName("Marka")]
        public string? Marka { get; set; }

        /// <summary>Dövlət qeydiyyat nömrəsi (məs. «90-DV-174» ✓).</summary>
        [JsonPropertyName("DovletNomresi")]
        public string? DovletNomresi { get; set; }

        /// <summary>Dövlət nömrəsinin alternativ adı ✓.</summary>
        [JsonPropertyName("QeydiyyatNisani")]
        public string? QeydiyyatNisani { get; set; }

        /// <summary>Buraxılış ili.</summary>
        [JsonPropertyName("Il")]
        public int? Il { get; set; }

        /// <summary>Alış tarixi — «09.07.2023» mətni ✓.</summary>
        [JsonPropertyName("AlisTarixi")]
        public string? AlisTarixi { get; set; }

        /// <summary>Alış qiyməti (AZN ✓).</summary>
        [JsonPropertyName("AlisQiymeti")]
        public decimal? AlisQiymeti { get; set; }

        /// <summary>Avtomobilin bütün xərcləri ✓.</summary>
        [JsonPropertyName("Xercler")]
        public List<ScriptXerc>? Xercler { get; set; }

        /// <summary>
        /// 📝 Avtomobil üzrə qeyd — <b>ƏN AXIRINCI XƏRCİN</b> «Qeyd» xanasına yazılır ✓✓✓
        /// </summary>
        [JsonPropertyName("Qeydler")]
        public string? Qeydler { get; set; }

        /// <summary>Qeydin alternativ adı ✓.</summary>
        [JsonPropertyName("Qeyd")]
        public string? Qeyd { get; set; }

        /// <summary>Ən yaxşı uyğun marka/model mətni ✓.</summary>
        [JsonIgnore]
        public string MarkaMetni =>
            new[] { MarkaModel, Marka }
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))?.Trim() ?? string.Empty;

        /// <summary>Ən yaxşı uyğun dövlət nömrəsi ✓.</summary>
        [JsonIgnore]
        public string NomreMetni =>
            new[] { DovletNomresi, QeydiyyatNisani }
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))?.Trim() ?? string.Empty;

        /// <summary>Ən yaxşı uyğun qeyd mətni ✓.</summary>
        [JsonIgnore]
        public string QeydMetni =>
            new[] { Qeydler, Qeyd }
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))?.Trim() ?? string.Empty;
    }
}
