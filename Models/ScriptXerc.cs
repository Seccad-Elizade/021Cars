using System.Text.Json.Serialization;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// 📜 <b>SKRİPT XƏRCİ</b> — istifadəçinin yapışdırdığı JSON-da bir xərc sətri.
    /// <para>
    /// Nümunə: <c>{ "Təsvir": "Elsen", "Tarix": "09.07.2023", "Məbləğ": 40.00 }</c>
    /// </para>
    /// <para>
    /// ⚠ Uyğunluq üçün <b>bir neçə yazılış</b> dəstəklənir ✓ (ə ilə və ya a/e ilə ·
    /// «Təsvir» / «Tesvir» / «Ad» · «Məbləğ» / «Mebleg» ✓✓✓)
    /// </para>
    /// </summary>
    public sealed class ScriptXerc
    {
        /// <summary>Xərcin təsviri (kateqoriya adı kimi ✓).</summary>
        [JsonPropertyName("Təsvir")]
        public string? Tesvir { get; set; }

        /// <summary>«Təsvir» sahəsinin sadələşdirilmiş yazılışı ✓.</summary>
        [JsonPropertyName("Tesvir")]
        public string? TesvirSade { get; set; }

        /// <summary>Təsvirin alternativ adı («Ad» / «Xərc» ✓).</summary>
        [JsonPropertyName("Ad")]
        public string? Ad { get; set; }

        /// <summary>Xərcin tarixi — «09.07.2023» mətni ✓.</summary>
        [JsonPropertyName("Tarix")]
        public string? Tarix { get; set; }

        /// <summary>Məbləğ (AZN ✓).</summary>
        [JsonPropertyName("Məbləğ")]
        public decimal? Mebleg { get; set; }

        /// <summary>«Məbləğ» sahəsinin sadələşdirilmiş yazılışı ✓.</summary>
        [JsonPropertyName("Mebleg")]
        public decimal? MeblegSade { get; set; }

        /// <summary>«Məbləğ» sahəsinin ingilis yazılışı ✓.</summary>
        [JsonPropertyName("Amount")]
        public decimal? MeblegIngilis { get; set; }

        /// <summary>Ən yaxşı uyğun təsvir mətni (bütün variantlar yoxlanılır ✓).</summary>
        [JsonIgnore]
        public string TasvirMetni =>
            new[] { Tesvir, TesvirSade, Ad }
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))?.Trim() ?? string.Empty;

        /// <summary>Ən yaxşı uyğun məbləğ (bütün variantlar yoxlanılır ✓).</summary>
        [JsonIgnore]
        public decimal MeblegDeyeri => Mebleg ?? MeblegSade ?? MeblegIngilis ?? 0m;
    }
}
