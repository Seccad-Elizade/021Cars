using System.Globalization;
using System.Text;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🔍 <b>«SKRİPT İDXALI» MƏTNİNİN OXYUCUSU</b> ✓✓✓ (v6.2.72)
    /// <para>
    /// Mətn <b>5 BLOKDAN</b> ibarətdir — bloklar <b>BOŞ SƏTİR</b> ilə ayrılır ✓:
    /// </para>
    /// <code>
    /// ① Tarix        ② Məbləğ      ③ Maya       ④ Musa       ⑤ Asif
    /// 22,10,2024     1470,00      816,00      327,00      327,00
    /// 22,11,2024     1200,00      546,00      327,00      327,00
    /// </code>
    /// <para><b>Yoxlanış:</b> hər sətirdə <c>Maya + Musa + Asif = Məbləğ</c> ✓</para>
    /// <para>
    /// ⚠ <b>Bu sinif bazaya HEÇ NƏ yazmır</b> ✗ — yalnız oxuyur və yoxlayır ✓
    /// (yazma: <see cref="IKreditIdxalService.SkriptiKrediteYazAsync"/> ✓).
    /// </para>
    /// </summary>
    public static class KreditSkriptOxuyucu
    {
        /// <summary>Gözlənilən blok sayı: ① Tarix ② Məbləğ ③ Maya ④ Musa ⑤ Asif ✓</summary>
        public const int BlokSayi = 5;

        /// <summary>Dəstəklənən tarix formatları — «22,10,2024» BİRİNCİDİR ✓.</summary>
        private static readonly string[] TarixFormatlari =
        {
            "dd,MM,yyyy", "d,M,yyyy", "dd,MM,yy", "d,M,yy",
            "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy",
            "yyyy-MM-dd", "yyyy/MM/dd"
        };

        /// <summary>
        /// 🔍 Mətni yoxlayır — <b>bazaya heç nə yazmır</b> ✗✓✓
        /// </summary>
        /// <param name="metn">5 bloklu yapışdırma mətni ✓</param>
        public static KreditSkriptYoxlama Yoxla(string? metn)
        {
            var netice = new KreditSkriptYoxlama();

            if (string.IsNullOrWhiteSpace(metn))
            {
                netice.Xeta = "Mətn boşdur ✗ — 5 bloku yapışdırın: ① Tarix ② Məbləğ ③ Maya ④ Musa ⑤ Asif";
                netice.Xetalar.Add(netice.Xeta);
                return netice;
            }

            var bloklar = BloklariAyir(metn);

            if (bloklar.Count != BlokSayi)
            {
                netice.Xeta = bloklar.Count == 0
                    ? "Heç bir məlumat sətri tapılmadı ✗ — 5 bloku (boş sətirlə ayrılmış) yapışdırın."
                    : $"5 BLOK olmalıdır ✗ — {bloklar.Count} blok tapıldı. " +
                      "Sıra: ① Tarix ② Məbləğ ③ Maya ④ Musa ⑤ Asif ✓";

                netice.Xetalar.Add(netice.Xeta);
                return netice;
            }

            var say = bloklar[0].Count;

            if (say == 0 || bloklar.Any(b => b.Count == 0))
            {
                netice.Xeta = "Bloklardan biri BOŞDUR ✗ — hər 5 blokda ən azı 1 sətir olmalıdır ✓";
                netice.Xetalar.Add(netice.Xeta);
                return netice;
            }

            // ---- ② HƏR BLOKDA EYNİ SAYDA SƏTİR OLMALIDIR ✓ ----
            for (var b = 1; b < bloklar.Count; b++)
            {
                if (bloklar[b].Count != say)
                {
                    netice.Xetalar.Add(
                        $"Sətir sayı eyni deyil ✗ — {b + 1}-ci blokda {bloklar[b].Count} sətir, " +
                        $"1-ci blokda {say} sətir var.");
                }
            }

            var maxSay = bloklar.Max(b => b.Count);

            for (var i = 0; i < maxSay; i++)
            {
                var setir = SetiriYoxla(
                    i + 1,
                    i < bloklar[0].Count ? bloklar[0][i] : null,
                    i < bloklar[1].Count ? bloklar[1][i] : null,
                    i < bloklar[2].Count ? bloklar[2][i] : null,
                    i < bloklar[3].Count ? bloklar[3][i] : null,
                    i < bloklar[4].Count ? bloklar[4][i] : null);

                netice.Setirler.Add(setir);

                if (!setir.Duzgundur && setir.Xeta.Length > 0)
                {
                    netice.Xetalar.Add(setir.Xeta);
                }
            }

            TekrarlariIsaretle(netice);

            netice.Ugurlu = netice.Xetalar.Count == 0 && netice.Setirler.Count > 0;
            netice.Xeta = netice.Xetalar.FirstOrDefault();

            return netice;
        }

        /// <summary>Bir sətri yoxlayır — 5 blokdan <b>eyni indeksli</b> dəyərlər ✓.</summary>
        private static KreditSkriptSetir SetiriYoxla(
            int no,
            string? tarixMetni,
            string? meblegMetni,
            string? mayaMetni,
            string? musaMetni,
            string? asifMetni)
        {
            var xetalar = new List<string>();

            // ---- ① TARİX ----
            var tarix = TarixOku(tarixMetni);

            if (tarix is null)
            {
                xetalar.Add($"{no}. sətir: TARİX formatı səhvdir ✗ — «{Metn(tarixMetni)}» " +
                            "(gözlənilən: 22,10,2024)");
            }

            // ---- ② MƏBLƏĞ · ③ MAYA · ④ MUSA · ⑤ ASIF ----
            var mebleg = PulOku(meblegMetni);
            var maya = PulOku(mayaMetni);
            var musa = PulOku(musaMetni);
            var asif = PulOku(asifMetni);

            if (mebleg is null)
            {
                xetalar.Add($"{no}. sətir: MƏBLƏĞ rəqəm deyil ✗ — «{Metn(meblegMetni)}» (gözlənilən: 1470,00)");
            }

            if (maya is null)
            {
                xetalar.Add($"{no}. sətir: MAYA rəqəm deyil ✗ — «{Metn(mayaMetni)}» (gözlənilən: 816,00)");
            }

            if (musa is null)
            {
                xetalar.Add($"{no}. sətir: MUSA rəqəm deyil ✗ — «{Metn(musaMetni)}» (gözlənilən: 327,00)");
            }

            if (asif is null)
            {
                xetalar.Add($"{no}. sətir: ASIF rəqəm deyil ✗ — «{Metn(asifMetni)}» (gözlənilən: 327,00)");
            }

            // ---- ⛔ MƏNFİ DƏYƏR QADAĞANDIR ✓ ----
            if (mebleg is < 0m)
            {
                xetalar.Add($"{no}. sətir: MƏBLƏĞ mənfi ola bilməz ✗ ({KreditSkriptYoxlama.Pul(mebleg.Value)})");
            }

            if (maya is < 0m)
            {
                xetalar.Add($"{no}. sətir: MAYA mənfi ola bilməz ✗ ({KreditSkriptYoxlama.Pul(maya.Value)})");
            }

            if (musa is < 0m)
            {
                xetalar.Add($"{no}. sətir: MUSA payı mənfi ola bilməz ✗ ({KreditSkriptYoxlama.Pul(musa.Value)})");
            }

            if (asif is < 0m)
            {
                xetalar.Add($"{no}. sətir: ASIF payı mənfi ola bilməz ✗ ({KreditSkriptYoxlama.Pul(asif.Value)})");
            }

            // ---- ✅ ƏSAS ŞƏRT: Maya + Musa + Asif = Məbləğ ----
            if (mebleg is not null && maya is not null && musa is not null && asif is not null)
            {
                var cem = Math.Round(maya.Value + musa.Value + asif.Value, 2);
                var hedef = Math.Round(mebleg.Value, 2);

                if (cem != hedef)
                {
                    xetalar.Add($"{no}. sətir: Maya + Musa + Asif = Məbləğ DEYİL ✗ — " +
                                $"{KreditSkriptYoxlama.Pul(cem)} ≠ {KreditSkriptYoxlama.Pul(hedef)} " +
                                $"(fərq: {KreditSkriptYoxlama.Pul(hedef - cem)})");
                }
            }

            return new KreditSkriptSetir
            {
                No = no,
                Tarix = tarix,
                Mebleg = mebleg ?? 0m,
                Maya = maya ?? 0m,
                Musa = musa ?? 0m,
                Asif = asif ?? 0m,
                Duzgundur = xetalar.Count == 0,
                Xeta = string.Join(" · ", xetalar)
            };
        }

        /// <summary>⚠ Təkrar olunan tarixləri nişanlayır (XƏBƏRDARLIQ ✓ — idxalı bağlamır ✗).</summary>
        private static void TekrarlariIsaretle(KreditSkriptYoxlama netice)
        {
            var tekrarlar = netice.Setirler
                .Where(s => s.Duzgundur && s.Tarix is not null)
                .GroupBy(s => s.Tarix!.Value.Date)
                .Where(g => g.Count() > 1)
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var grup in tekrarlar)
            {
                var setirler = grup.ToList();

                netice.Xeberdarliqlar.Add(
                    $"⚠ {grup.Key:dd.MM.yyyy} tarixi {setirler.Count} dəfə təkrarlanır " +
                    $"(sətirlər: {string.Join(", ", setirler.Select(s => s.No))}) — " +
                    "hər sətir AYRICA yazılacaq ✓");

                foreach (var setir in setirler)
                {
                    setir.TekrarTarix = true;
                }
            }
        }

        /// <summary>Xəta mesajı üçün mətni qısaldır («(boş)» ✓).</summary>
        private static string Metn(string? metn)
            => string.IsNullOrWhiteSpace(metn) ? "(boş)" : metn.Trim();

        /// <summary>
        /// Mətni <b>bloklara</b> ayırır — hər BOŞ SƏTİR yeni blok başladır ✓✓✓
        /// <para>«#» və «//» ilə başlayan sətirlər şərh sayılır və atılır ✓.</para>
        /// </summary>
        private static List<List<string>> BloklariAyir(string metn)
        {
            var bloklar = new List<List<string>>();
            var cari = new List<string>();

            foreach (var xam in metn.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var setir = Temizle(xam);

                if (setir.Length == 0)
                {
                    if (cari.Count > 0)
                    {
                        bloklar.Add(cari);
                        cari = new List<string>();
                    }

                    continue;
                }

                if (setir.StartsWith("#", StringComparison.Ordinal)
                    || setir.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;   // şərh sətri ✓
                }

                cari.Add(setir);
            }

            if (cari.Count > 0)
            {
                bloklar.Add(cari);
            }

            return bloklar;
        }

        /// <summary>
        /// Sətri təmizləyir: boşluqlar · BOM · Excel-in qoyduğu sonda vergül ✓
        /// <para>«22,10,2024,» → «22,10,2024» ✓ · «1470,00,» → «1470,00» ✓</para>
        /// </summary>
        private static string Temizle(string xam)
        {
            var setir = xam.Replace('\u00A0', ' ')
                           .Replace("\uFEFF", string.Empty)
                           .Trim();

            while (setir.EndsWith(",", StringComparison.Ordinal))
            {
                setir = setir[..^1].TrimEnd();
            }

            return setir;
        }

        /// <summary>«22,10,2024» · «22.10.2024» · «22/10/2024» kimi tarixi oxuyur ✓.</summary>
        public static DateTime? TarixOku(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return null;
            }

            var temiz = metn.Trim().Trim('"', '\'', ' ');

            if (DateTime.TryParseExact(temiz, TarixFormatlari, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var tam))
            {
                return tam.Date;
            }

            return DateTime.TryParse(temiz, CultureInfo.InvariantCulture, DateTimeStyles.None, out var serbest)
                ? serbest.Date
                : null;
        }

        /// <summary>
        /// Pul mətnini rəqəmə çevirir ✓✓✓
        /// <para>
        /// «1470,00» · «1 470,00» · «1.470,00» · «1470.00» · «1,470.00» ·
        /// «327» · «-500,00» — hamısı oxunur ✓
        /// </para>
        /// </summary>
        public static decimal? PulOku(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return null;
            }

            var t = metn
                .Replace("₼", string.Empty)
                .Replace("AZN", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("\u00A0", string.Empty)
                .Replace("\u202F", string.Empty)
                .Replace(" ", string.Empty)
                .Trim();

            // Yalnız rəqəm · işarə · ayırıcılar saxlanılır ✓
            var sb = new StringBuilder();

            foreach (var ch in t)
            {
                if (char.IsDigit(ch) || ch is ',' or '.' or '-' or '+')
                {
                    sb.Append(ch);
                }
            }

            t = sb.ToString();

            if (t.Length == 0)
            {
                return null;
            }

            var menfi = t.StartsWith("-", StringComparison.Ordinal);
            t = t.TrimStart('+', '-');

            if (t.Length == 0)
            {
                return null;
            }

            var sonVergul = t.LastIndexOf(',');
            var sonNokte = t.LastIndexOf('.');

            string tam;

            if (sonVergul >= 0 && sonNokte >= 0)
            {
                // Hər ikisi var → SONUNCU onluq ayırıcıdır ✓
                tam = sonVergul > sonNokte
                    ? t.Replace(".", string.Empty).Replace(',', '.')   // «1.470,00» ✓
                    : t.Replace(",", string.Empty);                    // «1,470.00» ✓
            }
            else if (sonVergul >= 0)
            {
                tam = AyiriciYoxla(t, ',');
            }
            else if (sonNokte >= 0)
            {
                tam = AyiriciYoxla(t, '.');
            }
            else
            {
                tam = t;
            }

            return decimal.TryParse(tam, NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out var deyer)
                ? (menfi ? -deyer : deyer)
                : null;
        }

        /// <summary>
        /// Tək ayırıcını yoxlayır: «1,470» → 1470 (minlik ✓) · «1470,00» → 1470.00 (onluq ✓)
        /// </summary>
        private static string AyiriciYoxla(string t, char ayirici)
        {
            var eks = ayirici == ',' ? '.' : ',';

            // Əks ayırıcı da varsa → bu MİNLİK ayırıcısıdır ✓
            if (t.IndexOf(eks) >= 0)
            {
                return t.Replace(ayirici.ToString(), string.Empty);
            }

            var parcalar = t.Split(ayirici);

            if (parcalar.Length > 2)
            {
                return t.Replace(ayirici.ToString(), string.Empty);         // «1,470,000» ✓
            }

            if (parcalar.Length == 2
                && parcalar[1].Length == 3
                && parcalar[0].Length is >= 1 and <= 3
                && parcalar[0] != "0")
            {
                return t.Replace(ayirici.ToString(), string.Empty);         // «1,470» → 1470 ✓
            }

            return t.Replace(ayirici, '.');                                // «1470,00» → 1470.00 ✓
        }
    }
}
