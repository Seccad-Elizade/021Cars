using System.Globalization;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// 🔑 <b>BULUD SİNXRON AÇARI</b> — hər qeydə BİR DƏFƏ verilən QLOBAL UNİKAL
    /// açar ✓✓✓ (v6.2.16)
    /// <para>
    /// <b>⚠ PROBLEM (istifadəçi şikayəti ✓):</b> hər kompüter ÖZ rəqəm ID-sini
    /// ayrıca verirdi ✗ → iki kompüterdə <b>eyni rəqəm</b> yaranırdı ✗
    /// (məs. hər ikisində «232» ✗) →
    /// </para>
    /// <list type="bullet">
    ///   <item>bir kompüterdə əlavə olunan maşın digərində <b>GÖRÜNMÜRDÜ</b> ✗</item>
    ///   <item>və ya digər kompüterin <b>ÖZ qeydini SİLİRDİ / üzərinə yazırdı</b> ✗</item>
    /// </list>
    /// <para>
    /// <b>✅ HƏLL:</b> yeni qeydlərə <c>Guid</c> açarı verilir ✓ → iki kompüterdə
    /// <b>HEÇ VAXT toqquşmur</b> ✗✓✓
    /// </para>
    /// <para>
    /// ⚠ Köhnə (v6.2.16-dan əvvəlki) qeydlərdə <see cref="BuludId"/> <b>BOŞdur</b> ✗
    /// → onlar üçün köhnə rəqəm ID işlədilir ✓ (köhnə məlumat DƏYİŞMİR ✗ ·
    /// təkrar qeyd YARANMIR ✗ · hər iki kompüterdə eyni qalır ✓✓✓)
    /// </para>
    /// </summary>
    public interface IBuludIdli
    {
        /// <summary>Yerli (SQLite) identifikator ✓ — əlaqələr (carId · creditId ✓) bununla işləyir ✓</summary>
        int Id { get; set; }

        /// <summary>
        /// 🔑 Qlobal unikal bulud açarı ✓ (<c>Guid</c> ✓ v6.2.16).
        /// <para>⚠ Köhnə qeydlərdə <c>null</c> ✗ → <see cref="SinxronAcar"/> rəqəm ID qaytarır ✓</para>
        /// </summary>
        string? BuludId { get; set; }

        /// <summary>
        /// 🔑 <b>SİNXRON AÇARI</b> ✓✓✓ — Firebase-də istifadə olunan açar ✓
        /// <para>
        /// • <see cref="BuludId"/> varsa → <b>GUID</b> ✓ (yeni qeydlər ✓ — toqquşma YOXDUR ✗✓✓)
        /// </para>
        /// <para>
        /// • yoxdursa → köhnə rəqəm ID ✓ (v6.2.16-dan əvvəlki qeydlər ✓ — <b>dəyişmir</b> ✗✓✓)
        /// </para>
        /// </summary>
        string SinxronAcar => string.IsNullOrWhiteSpace(BuludId)
            ? Id.ToString(CultureInfo.InvariantCulture)
            : BuludId!;
    }
}
