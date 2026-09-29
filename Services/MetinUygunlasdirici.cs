using System.Text;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🇦🇿 <b>AZƏRBAYCAN DİLİNƏ UYĞUN MƏTN UYĞUNLAŞDIRICISI</b>
    /// <para>
    /// Skriptdən gələn mətn ilə proqramdaki kateqoriya adları arasındaki
    /// <b>yazılış fərqlərini</b> tanıyır:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>Elsen</c> ↔ <c>Elşən</c> ✓ (ə → e)</item>
    ///   <item><c>Cerime / Çərimə</c> ✓ (ç → c)</item>
    ///   <item><c>Yag / Yağ · Qeydiyyat / Qeydiyyat</c> ✓ (ğ → g · ı → i)</item>
    ///   <item><c>Usta Xerci / Usta haqqı (Digər)</c> ✓ (söz uyğunluğu + oxşarlıq)</item>
    /// </list>
    /// <para>
    /// ⚠ Yalnız <b>müqayisə</b> üçün istifadə olunur ✗ — baza heç vaxt
    /// sadələşdirilmiş mətnlə YAZILMIR ✓; istifadəçinin gördüyü <b>əsl yazılış</b> qalır ✓✓✓
    /// </para>
    /// </summary>
    public static class MetinUygunlasdirici
    {
        /// <summary>
        /// Azərbaycan hərflərinin ASCII qarşılığı (yalnız müqayisə üçün).
        /// <c>I</c> və <c>İ</c> hər ikisi <c>i</c>-yə çevrilir ✓.
        /// </summary>
        private static readonly Dictionary<char, char> HerfCevirisi = new()
        {
            ['ə'] = 'e', ['Ə'] = 'e',
            ['ı'] = 'i', ['I'] = 'i', ['İ'] = 'i',
            ['ş'] = 's', ['Ş'] = 's',
            ['ç'] = 'c', ['Ç'] = 'c',
            ['ğ'] = 'g', ['Ğ'] = 'g',
            ['ö'] = 'o', ['Ö'] = 'o',
            ['ü'] = 'u', ['Ü'] = 'u',
            ['â'] = 'a', ['Â'] = 'a',
            ['û'] = 'u', ['Û'] = 'u',
            ['î'] = 'i', ['Î'] = 'i'
        };

        /// <summary>Emoji və durğu işarələrini atıb mətni STANDART forma salır.</summary>
        public static string Normallasdir(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return string.Empty;
            }

            var bina = new StringBuilder(metn.Length);

            foreach (var herf in metn)
            {
                var cevrilmis = HerfCevirisi.TryGetValue(herf, out var qarsiliq) ? qarsiliq : herf;

                // Yalnız hərf və rəqəm qalır ✓ (emoji · durğu işarəsi · tire → boşluq)
                bina.Append(char.IsLetterOrDigit(cevrilmis) ? char.ToLowerInvariant(cevrilmis) : ' ');
            }

            var sade = bina.ToString().Trim();

            while (sade.Contains("  ", StringComparison.Ordinal))
            {
                sade = sade.Replace("  ", " ", StringComparison.Ordinal);
            }

            return sade;
        }

        /// <summary>Mətni normallaşdırılmış sözlərə bölür (təkrarsız ✓).</summary>
        public static IReadOnlyList<string> Sozlere(string? metn)
        {
            var norm = Normallasdir(metn);

            if (norm.Length == 0)
            {
                return Array.Empty<string>();
            }

            return norm
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// İki mətnin oxşarlığı: <c>0.0</c> (tamamilə fərqli) … <c>1.0</c> (eynidir ✓).
        /// <para>Hesablama: söz uyğunluğu (65 %) + hərf-səviyyəli məsafə (35 %) ✓</para>
        /// </summary>
        public static double Oxsarlıq(string? birinci, string? ikinci)
        {
            var a = Normallasdir(birinci);
            var b = Normallasdir(ikinci);

            if (a.Length == 0 || b.Length == 0)
            {
                return 0.0;
            }

            if (string.Equals(a, b, StringComparison.Ordinal))
            {
                return 1.0;
            }

            var six1 = a.Replace(" ", string.Empty, StringComparison.Ordinal);
            var six2 = b.Replace(" ", string.Empty, StringComparison.Ordinal);

            // Biri digərinin İÇİNDƏDİRSƏ (məs. «cerime odenisi» ⊃ «cerime») → yüksək uyğunluq ✓
            if (six1.Length >= 4 && six2.Length >= 4 &&
                (six1.Contains(six2, StringComparison.Ordinal) ||
                 six2.Contains(six1, StringComparison.Ordinal)))
            {
                return 0.9;
            }

            var soz1 = Sozlere(a);
            var soz2 = Sozlere(b);
            var ortaq = soz1.Count(soz2.Contains);

            var sozNisbeti = ortaq == 0
                ? 0.0
                : (double)ortaq / Math.Max(soz1.Count, soz2.Count);

            var mesafe = Mesafe(six1, six2);
            var herfNisbeti = 1.0 - (double)mesafe / Math.Max(six1.Length, six2.Length);

            return (0.65 * sozNisbeti) + (0.35 * Math.Max(0.0, herfNisbeti));
        }

        /// <summary>Mətn böyük hərflə başlayırmı? (şəxs adı ehtimalı üçün ✓)</summary>
        public static bool BoyukHerfleBaslayir(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return false;
            }

            foreach (var herf in metn.Trim())
            {
                if (!char.IsLetter(herf))
                {
                    continue;
                }

                return char.IsUpper(herf);
            }

            return false;
        }

        /// <summary>Sadə Levenshtein məsafəsi (qısa mətnlər üçün ✓ — O(n·m)).</summary>
        private static int Mesafe(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;

            var evvelki = new int[b.Length + 1];
            var cari = new int[b.Length + 1];

            for (var j = 0; j <= b.Length; j++)
            {
                evvelki[j] = j;
            }

            for (var i = 1; i <= a.Length; i++)
            {
                cari[0] = i;

                for (var j = 1; j <= b.Length; j++)
                {
                    var qiymet = a[i - 1] == b[j - 1] ? 0 : 1;

                    cari[j] = Math.Min(
                        Math.Min(cari[j - 1] + 1, evvelki[j] + 1),
                        evvelki[j - 1] + qiymet);
                }

                (evvelki, cari) = (cari, evvelki);
            }

            return evvelki[b.Length];
        }
    }
}
