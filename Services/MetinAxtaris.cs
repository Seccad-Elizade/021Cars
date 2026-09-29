using System.Globalization;
using System.Text;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🚀 <b>BAZADA (SQL-də) SÜRƏTLİ AXTARIŞ ÜÇÜN MƏTN HAZIRLAYICISI</b> ✓✓✓
    /// <para>
    /// <b>NƏ ÜÇÜN LAZIMDIR?</b>
    /// SQLite-ın <c>LIKE</c> funksiyası yalnız <b>ASCII</b> hərflərdə böyük/kiçik
    /// hərf fərqini görmür ✗ → «şüşə» yazanda «Şüşə» TAPILMIRDI ✗✓✓
    /// </para>
    /// <para>
    /// Buna görə hər xərc qeydi üçün <b>normal edilmiş</b> (kiçik hərfli və
    /// Azərbaycan hərfləri ASCII-yə çevrilmiş ✓) kölgə mətni saxlanılır:
    /// <c>Şüşə → suse</c> · <c>Əyləc → eylec</c> · <c>Çəkmə → cekme</c>
    /// </para>
    /// <para>
    /// Beləliklə axtarış <b>SQL tərəfində</b> gedir ✓ →
    /// <b>1 000 000+ sətirdə belə proqram donmur</b> ✓✓✓
    /// (ə və e · ş və s · ç və c · ğ və g · ı və i · ö və o · ü və u bir sayılır ✓)
    /// </para>
    /// </summary>
    public static class MetinAxtaris
    {
        /// <summary>
        /// Mətni axtarışa hazır formasına çevirir: kiçik hərf + Azərbaycan
        /// hərfləri ASCII-yə (ə→e · ş→s · ç→c · ğ→g · ı→i · ö→o · ü→u) ✓
        /// </summary>
        public static string Normallasdir(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return string.Empty;
            }

            var çıxış = new StringBuilder(metn.Length + 8);

            foreach (var hərf in metn.ToLowerInvariant())
            {
                switch (hərf)
                {
                    case 'ə': çıxış.Append('e'); break;
                    case 'ı': çıxış.Append('i'); break;
                    case 'ş': çıxış.Append('s'); break;
                    case 'ç': çıxış.Append('c'); break;
                    case 'ğ': çıxış.Append('g'); break;
                    case 'ö': çıxış.Append('o'); break;
                    case 'ü': çıxış.Append('u'); break;

                    // 🔤 Kiril variantları (Fayllardan / köhnə skriptlərdən gələ bilər ✓)
                    case 'ё': çıxış.Append('e'); break;
                    case 'ә': çıxış.Append('e'); break;

                    default:
                        // «İ» hərfi kiçildikdə «i + nöqtə» olur ✗ → nöqtə atılır ✓
                        if (char.GetUnicodeCategory(hərf) != UnicodeCategory.NonSpacingMark)
                        {
                            çıxış.Append(hərf);
                        }

                        break;
                }
            }

            return çıxış.ToString();
        }

        /// <summary>
        /// Bir neçə sahəni (təyinat · qrup · kateqoriya · qeyd ✓) tək
        /// axtarış mətninə birləşdirir ✓ (hər hissə ayırıcı ilə ✓ —
        /// «usta» sözü «Ali usta»dan tapılsın ✓, amma «a» + «li» kimi
        /// sərhədsiz uyğunluq yaranmasın ✓)
        /// </summary>
        public static string Birlestir(params string?[] hissələr)
        {
            var çıxış = new StringBuilder(160);

            foreach (var hissə in hissələr)
            {
                var normal = Normallasdir(hissə);

                if (normal.Length == 0)
                {
                    continue;
                }

                if (çıxış.Length > 0)
                {
                    çıxış.Append(' ');
                }

                çıxış.Append(normal);
            }

            return çıxış.ToString();
        }

        /// <summary>
        /// İstifadəçinin yazdığını SQL <c>LIKE</c> nümunəsi üçün təhlükəsiz
        /// hala salır ✓ — <c>%</c> · <c>_</c> · <c>\</c> işarələri
        /// «escape» olunur ✗✓✓ (əks halda «%» yazan istifadəçi bütün
        /// bazanı gətirərdi ✗)
        /// </summary>
        public static string LikeEhtiyatla(string? sorğu)
        {
            var normal = Normallasdir(sorğu);

            return normal.Length == 0 ? string.Empty : Ehtiyatla(normal);
        }

        /// <summary>
        /// Normalizasiya ETMƏDƏN <c>LIKE</c> üçün təhlükəsiz hala salır ✓
        /// (köhnə — «Axtaris» mətni hələ hazırlanmamış — qeydlər üçün ✓)
        /// </summary>
        public static string LikeEhtiyatlaXam(string? sorğu)
        {
            var xam = (sorğu ?? string.Empty).Trim();

            return xam.Length == 0 ? string.Empty : Ehtiyatla(xam);
        }

        /// <summary>Xüsusi <c>LIKE</c> işarələrini «escape» edir ✓.</summary>
        private static string Ehtiyatla(string mətn)
        {
            var çıxış = new StringBuilder(mətn.Length + 8);

            foreach (var hərf in mətn)
            {
                if (hərf is '%' or '_' or '\\' or '[')
                {
                    çıxış.Append('\\');
                }

                çıxış.Append(hərf);
            }

            return çıxış.ToString();
        }
    }
}
