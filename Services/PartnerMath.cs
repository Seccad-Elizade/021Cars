using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Tərəfdaş mənfəət bölgüsünün hesablanması — <b>saf məntiq</b>,
    /// UI-dən və bazadan asılı deyil (asanlıqla yoxlanıla bilər).
    /// <para>
    /// Qayda:
    /// <list type="number">
    ///   <item>Faiz payçıları (Zaur %6, Eşqin %5, Asiman %5) — bazanın faizi qədər alır.</item>
    ///   <item>Qalıq payçıları (Asif, Musa) — faizlər çıxıldıqdan sonra qalan məbləği
    ///         öz aralarında <b>bərabər</b> (yarı-yarıya) bölür.</item>
    ///   <item>Yuvarlaqlaşdırma fərqi <b>sonuncu</b> qalıq payçısına əlavə olunur ki,
    ///         cəm dəqiq bazaya bərabər olsun.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class PartnerMath
    {
        /// <summary>
        /// Bazadan gələn tərəfdaşların təchizatçısı.
        /// <para>
        /// <see cref="Services.PartnerService"/> tərəfindən təyin olunur. Beləliklə
        /// istifadəçinin «👥 Tərəfdaşlar» tabında <b>əlavə etdiyi / sildiyi</b>
        /// şəxslər və dəyişdirdiyi <b>standart faizlər</b> BÜTÜN bölgü panellərinə
        /// (Satış, Kredit, Kredit Əlavə Gəlir) dərhal tətbiq olunur.
        /// </para>
        /// <para>
        /// <c>null</c> olduqda (hələ yüklənməyibsə) <see cref="Catalog.DefaultPartners"/>
        /// siyahısına düşür.
        /// </para>
        /// </summary>
        public static Func<IReadOnlyList<Partner>>? PartnersProvider { get; set; }

        /// <summary>Faiz dərəcələri standart siyahıya qaytarılır («♻️ Default» düyməsi).</summary>
        /// <param name="yalnizQaligPaycilari">
        /// <c>true</c> olduqda <b>YALNIZ QALIQ PAYÇILARI</b> (Musa &amp; Asif) işarələnmiş
        /// gəlir ✓ — faiz payçıları (Zaur/Eşqin/Asiman) <b>işarəsiz</b> qalır ✗.
        /// <para>
        /// «Kredit Əlavə Gəlir» formasında standart vəziyyət budur ✓ — istifadəçi
        /// istəsə faiz payçılarını da əl ilə işarələyə bilər ✓.
        /// </para>
        /// </param>
        public static List<PartnerShare> CreateDefaultRows(bool yalnizQaligPaycilari = false)
        {
            var rows = new List<PartnerShare>();
            var sira = 0;

            // 1) Əvvəlcə BAZADAKI tərəfdaşlar (əlavə/silinmiş şəxslər nəzərə alınır).
            var bazadan = PartnersProvider?.Invoke() ?? Array.Empty<Partner>();

            if (bazadan.Count > 0)
            {
                foreach (var partner in bazadan.OrderBy(p => p.Sira).ThenBy(p => p.Id))
                {
                    rows.Add(new PartnerShare
                    {
                        Terefdas = partner.Ad,
                        Faiz = partner.Faiz,
                        QaligPayi = partner.QaligPayi,
                        // 🔑 Yalnız qalıq payçıları (Asif & Musa) / hamısı
                        Aktiv = yalnizQaligPaycilari ? partner.QaligPayi : partner.Aktiv,
                        Sira = sira++
                    });
                }

                return rows;
            }

            // 2) Baza boşdursa — kodda olan standart siyahı.
            foreach (var partner in Catalog.DefaultPartners)
            {
                rows.Add(new PartnerShare
                {
                    Terefdas = partner.Ad,
                    Faiz = partner.Faiz,
                    QaligPayi = partner.QaligPayi,
                    // 🔑 Yalnız qalıq payçıları (Asif & Musa) / hamısı aktiv
                    Aktiv = !yalnizQaligPaycilari || partner.QaligPayi,
                    Sira = sira++
                });
            }

            return rows;
        }

        /// <summary>Tərəfdaş paylarının cəmi (₼).</summary>
        public static decimal Total(IEnumerable<PartnerShare> rows) => rows.Sum(r => r.Mebleg);

        /// <summary>Faiz payı çıxıldıqdan sonra QALAN məbləğ (₼).</summary>
        public static decimal Remainder(
            decimal baza,
            IEnumerable<PartnerShare> rows)
            => baza - rows.Where(r => !r.QaligPayi).Sum(r => r.Mebleg);

        /// <summary>
        /// Bölgünü hesablayır və <paramref name="rows"/> sətirlərinin
        /// <see cref="PartnerShare.Mebleg"/> qiymətlərini DOLDURUR.
        /// </summary>
        /// <returns>Bölünməmiş qalıq (₼). Qalıq payçısı varsa 0,00 olur.</returns>
        public static decimal Distribute(decimal baza, IList<PartnerShare> rows)
        {
            if (rows.Count == 0)
            {
                return 0m;
            }

            // ⚠ YALNIZ «TƏTBİQ OLUNUR» işarəsi qoyulmuş tərəfdaşlar hesablanır.
            //  İşarəsiz tərəfdaşın faizi TƏTBİQ OLUNMUR və pay ona verilmir.
            var aktivFaiz = rows.Where(r => r.Aktiv && !r.QaligPayi).ToList();
            var aktivQalig = rows.Where(r => r.Aktiv && r.QaligPayi).ToList();

            // İştirak etməyənlər sıfırlanır.
            foreach (var row in rows.Where(r => !r.Aktiv))
            {
                row.Mebleg = 0m;
            }

            // ---- 1) Faiz payçıları (yalnız işarələnənlər) ----
            var faizCemi = 0m;
            foreach (var row in aktivFaiz)
            {
                row.Mebleg = Money(baza * row.Faiz / 100m);
                faizCemi += row.Mebleg;
            }

            // ---- 2) Qalıq ----
            var qalig = baza - faizCemi;

            if (aktivQalig.Count == 0)
            {
                // Qalıq payçısı yoxdur/ işarələnməyib → bölünməmiş qalıq qaytarılır.
                return qalig;
            }

            if (qalig <= 0m)
            {
                foreach (var row in aktivQalig)
                {
                    row.Mebleg = 0m;
                }

                return qalig;
            }

            // ---- 3) Qalıq bərabər bölünür (yuvarlaqlaşdırma fərqi sonuncuya) ----
            var pay = Money(qalig / aktivQalig.Count);
            var verilen = 0m;

            for (var i = 0; i < aktivQalig.Count; i++)
            {
                var sonuncu = i == aktivQalig.Count - 1;

                // Sonuncu payçı qalan TAM məbləği alır → cəm heç vaxt itmir.
                aktivQalig[i].Mebleg = sonuncu ? qalig - verilen : pay;
                verilen += aktivQalig[i].Mebleg;
            }

            return qalig - verilen;
        }

        /// <summary>Məbləği qəpiklə dəqiq yuvarlaqlaşdırır (2 onluq).</summary>
        public static decimal Money(decimal value)
            => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
