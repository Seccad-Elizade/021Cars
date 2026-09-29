// ============================================================================
//  🧩 021Cars — FIREBASE ƏSAS OBYEKTİ (2/6)
//  HƏR QEYD MÜTLƏQ BU 5 SAHƏNİ DAŞIYIR ✓✓✓
// ============================================================================

using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Cas0201.Firebase
{
    /// <summary>
    /// 📦 <b>BÜTÜN OBYEKTLƏRİN ƏSASI</b> ✓✓✓
    /// <list type="bullet">
    ///   <item><b>Id</b> — GUID açar ✓ (merge + soft delete üçün ✓)</item>
    ///   <item><b>UpdatedAt</b> — son dəyişiklik ✓ (UTC ✓ LWW konflikt həlli ✓)</item>
    ///   <item><b>UpdatedBy</b> — cihaz identifikatoru ✓ (deterministik tiebreak ✓)</item>
    ///   <item><b>IsDeleted</b> — 🗑️ SOFT DELETE ✓ (fiziki silinmə YOX ✗)</item>
    ///   <item><b>DeletedAt</b> — silinmə vaxtı ✓</item>
    /// </list>
    /// ⚠ JSON-da sxem yoxdur ✗ → bu əsas sinif sxemin YERİNƏ keçir ✓
    /// </summary>
    public abstract class FirebaseEntity
    {
        /// <summary>🆔 GUID açar ✓ (Firebase node açarı ilə eynidir ✓)</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = FirebaseOptions.YeniId();

        /// <summary>🕒 Son dəyişiklik — UTC ISO-8601 ✓ (LWW açarı ✓)</summary>
        [JsonPropertyName("updatedAt")]
        public string UpdatedAt { get; set; } = FirebaseOptions.UtcIndi();

        /// <summary>🖥️ Hansı cihaz yazdı ✓ (məs. «PC-ASIF-01» ✓)</summary>
        [JsonPropertyName("updatedBy")]
        public string UpdatedBy { get; set; } = "";

        /// <summary>🗑️ Soft-delete bayrağı ✓ — fiziki silinmə HEÇ VAXT ✗</summary>
        [JsonPropertyName("isDeleted")]
        public bool IsDeleted { get; set; }

        /// <summary>🗓️ Silinmə vaxtı (UTC ✓ — yalnız silinəndə ✓)</summary>
        [JsonPropertyName("deletedAt")]
        public string? DeletedAt { get; set; }

        // --------------------------------------------------------------------
        //  ✅ KÖMƏKÇİ METODLAR — HƏR YAZMADAN ƏVVƏL ÇAĞIRILIR ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>✍️ Dəyişikliyi qeyd edir ✓ (UpdatedAt = indi ✓)</summary>
        public void Toxun(string cihaz, bool silindi = false)
        {
            UpdatedAt = FirebaseOptions.UtcIndi();
            UpdatedBy = string.IsNullOrWhiteSpace(cihaz) ? "naməlum" : cihaz;

            IsDeleted = silindi;
            DeletedAt = silindi ? UpdatedAt : null;
        }

        /// <summary>🗑️ <b>SOFT DELETE</b> ✓ — sətir buluddan SİLİNMİR ✗</summary>
        public void SoftDelete(string cihaz) => Toxun(cihaz, silindi: true);

        /// <summary>♻️ Silinməni geri qaytarır ✓ (zibil qutusundan bərpa ✓)</summary>
        public void Berpa(string cihaz) => Toxun(cihaz, silindi: false);

        /// <summary>🕒 UpdatedAt → DateTime (UTC ✓) — müqayisə üçün ✓</summary>
        [JsonIgnore]
        public DateTime UpdatedAtUtc =>
            DateTime.TryParse(UpdatedAt, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var p)
                ? p
                : DateTime.MinValue;

        /// <summary>📅 Tarix mətnini DateTime-a çevirir ✓ (sıralama üçün ✓)</summary>
        public static DateTime TarixAl(string? iso, DateTime defeult = default) =>
            DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var p)
                ? p
                : defeult;

        /// <summary>
        /// ⚖️ <b>LWW — «Last Write Wins» ✓✓✓</b>
        /// <para>
        /// ① <see cref="UpdatedAt"/> böyükdür → o tərəf üstün ✓<br/>
        /// ② bərabərdirsə → <see cref="UpdatedBy"/> ordinal müqayisə ✓
        /// (HƏR İKİ CİHAZDA EYNİ NƏTİCƏ → sonsuz dövrə olmur ✗✓✓)
        /// </para>
        /// </summary>
        public static T? UstunTut<T>(T? a, T? b) where T : FirebaseEntity
        {
            if (a is null) return b;
            if (b is null) return a;

            if (a.UpdatedAtUtc > b.UpdatedAtUtc) return a;
            if (b.UpdatedAtUtc > a.UpdatedAtUtc) return b;

            return string.CompareOrdinal(a.UpdatedBy, b.UpdatedBy) >= 0 ? a : b;
        }

        /// <summary>🔁 Dərin kopya ✓ (göndərməzdən əvvəl snapshot ✓)</summary>
        public T Kopyala<T>() where T : FirebaseEntity =>
            System.Text.Json.JsonSerializer.Deserialize<T>(
                System.Text.Json.JsonSerializer.Serialize((T)this, FirebaseOptions.Json),
                FirebaseOptions.Json)!;
    }
}
