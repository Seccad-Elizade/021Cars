using System.Globalization;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🧮 <b>MÜQAVİLƏ NÖMRƏSİ KÖMƏKÇİLƏRİ</b> ✓✓✓ (v6.2.73)
    /// <para>
    /// ⚠ <b>Problem:</b> «Müqavilə №» sütunu <b>mətn</b> kimi sıralanırdı ✗ →
    /// <c>245 · 244 · 242 · 241 · 239 · 234 · 99</c> kimi <b>səhv</b> sıra alınırdı ✗✓✓
    /// (çünki mətn sıralamasında «9» «1»-dən sonra gəlir ✗)
    /// </para>
    /// <para>
    /// ✅ <b>Həll:</b> nömrə <b>rəqəmsal</b> müqayisə olunur ✓ →
    /// <c>245 · 244 · 242 · 241 · 239 · 234 · 99</c> ✓ (<b>azalan</b> ✓)
    /// </para>
    /// <para>
    /// Nömrədə rəqəm yoxdursa (məs. <c>«Proqram»</c>) → əvvəldə <c>-1</c> kimi sıralanır ✓ ·
    /// hərf/defis prefiksi nəzərə alınmır ✗ (<c>«M-245»</c> = <c>«245»</c> ✓)
    /// </para>
    /// </summary>
    public static class MuqavileNomreleri
    {
        /// <summary>Mətndən rəqəmi çıxarır («M-0245/A» → 245 ✓ · rəqəm yoxsa <c>-1</c> ✓).</summary>
        public static int RəqəmNomre(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return -1;
            }

            var rəqəmlər = new string(metn.Where(char.IsDigit).ToArray());

            return int.TryParse(rəqəmlər, NumberStyles.None, CultureInfo.InvariantCulture, out var dəyər)
                ? dəyər
                : -1;
        }
    }
}
