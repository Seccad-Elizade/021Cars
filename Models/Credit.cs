namespace EnterpriseAeroStudio.Models
{
    using System.ComponentModel.DataAnnotations.Schema;
    using EnterpriseAeroStudio.Services;

    /// <summary>
    /// Avtomobil satışı üzrə kredit müqaviləsini təmsil edir.
    /// </summary>
    public class Credit : IBuludIdli
    {
        public int Id { get; set; }

        /// <summary>🔑 Qlobal unikal bulud açarı (GUID ✓ v6.2.16). Köhnə qeydlərdə boş ✗ → rəqəm ID işlədilir ✓</summary>
        public string? BuludId { get; set; }

        /// <summary>Cədvəldə göstərilən ardıcıl sıra nömrəsi (yalnız görünüş üçün).</summary>
        [NotMapped]
        public int SiraNomresi { get; set; }

        /// <summary>ComboBox kimi yerlərdə sinif adı yerinə düzgün mətn göstərilsin.</summary>
        public override string ToString() => DisplayText;

        /// <summary>Müqavilə nömrəsi.</summary>
        public string MuqavileNomresi { get; set; } = string.Empty;

        /// <summary>Müştəri adı soyadı.</summary>
        public string Mustəri { get; set; } = string.Empty;

        /// <summary>Kreditə bağlı avtomobil.</summary>
        public int? CarId { get; set; }

        public CarItem? Car { get; set; }

        /// <summary>Kreditin əsas məbləği (AZN).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>İlkin ödəniş (avans) məbləği (AZN).</summary>
        public decimal IlkinOdenis { get; set; }

        // ====================================================================
        //  ⏳ İLKİN ÖDƏNİŞƏ MÖHLƏT  (Kreditlər tabı ✓✓✓)
        // --------------------------------------------------------------------
        //  Müştəri BİR HİSSƏ avansı dərhal verir, qalanını söz verir:
        //  «3 min nağd ilkin ödəniş, 10 günə 2 min nağd» ✓
        //  → İlkinOdenis = 5 000 ✓  (möhlətlər BUNUN İÇİNDƏDİR ✓)
        //  → Dərhal ödənilən = 5 000 − 2 000 = 3 000 ✓ (avtomatik hesablanır ✓)
        //  BİRDƏN ÇOX möhlət ola bilər ✓ (bax: OdenisMohlet) ✓✓✓
        // ====================================================================

        /// <summary>Bu kreditin <b>ilkin ödənişinə</b> yazılmış möhlətlər ✓.</summary>
        [NotMapped]
        public List<OdenisMohlet> IlkinMohletleri { get; set; } = new();

        /// <summary>Möhlətə salınmış (hələ ödənilməmiş də ola bilər) avans hissəsinin cəmi (₼).</summary>
        [NotMapped]
        public decimal IlkinMohletCemi => IlkinMohletleri.Sum(m => m.Mebleg);

        /// <summary>
        /// ⏳ HƏLƏ ÖDƏNİLMƏMİŞ möhlətlərin cəmi (₼) — «gözlənilən pul» ✓✓✓
        /// <para>Kassa axınında gəlirə YAZILMIR ✗ — yalnız pul gələndə yazılır ✓</para>
        /// </summary>
        [NotMapped]
        public decimal IlkinMohletGozlenilen =>
            IlkinMohletleri.Where(m => !m.Odenilib).Sum(m => m.Mebleg);

        /// <summary>
        /// 💰 İlkin ödənişin <b>DƏRHAL</b> ödənilən hissəsi (₼) = avans − möhlətlər ✓✓✓
        /// </summary>
        [NotMapped]
        public decimal IlkinDerhalOdenilen => Math.Max(0m, IlkinOdenis - IlkinMohletCemi);

        /// <summary>İlkin ödənişə möhlət yazılıbmı?</summary>
        [NotMapped]
        public bool IlkinMohletVar => IlkinMohletleri.Count > 0;

        /// <summary>
        /// İlkin ödənişin yanında göstərilən möhlət xülasəsi:
        /// «⏳ Möhlət: 2 000,00 ₼ (10 günə) · dərhal 3 000,00 ₼».
        /// </summary>
        [NotMapped]
        public string IlkinMohletMetni
        {
            get
            {
                if (IlkinMohletleri.Count == 0)
                {
                    return string.Empty;
                }

                var setirler = IlkinMohletleri
                    .OrderBy(m => m.Tarix)
                    .ThenBy(m => m.Id)
                    .Select(m => $"{m.Mebleg:N2} ₼ → {m.TarixMetni}{(m.Odenilib ? " ✅" : string.Empty)}");

                return $"⏳ Möhlət: {IlkinMohletCemi:N2} ₼  ·  dərhal {IlkinDerhalOdenilen:N2} ₼"
                       + $"  ({string.Join(" · ", setirler)})";
            }
        }


        /// <summary>
        /// Kreditləşdirilən (faktiki borc) məbləğ = Mebleg − İlkin ödəniş.
        /// Ödəniş qrafiki bunun üzərindən hesablanır.
        /// </summary>
        [NotMapped]
        public decimal Kreditlesdirilen => Mebleg - IlkinOdenis;

        /// <summary>
        /// Kredit üzrə ÜMUMİ FAİZ məbləği (₼):
        /// <c>Kreditləşdirilən × Faiz% ÷ 100</c>.
        /// <para>Nümunə: 17 014 ₼ · 40% → <b>6 805,60 ₼</b> faiz.</para>
        /// </summary>
        [NotMapped]
        public decimal FaizMeblegi => CreditMath.TotalInterest(Kreditlesdirilen, FaizDerecesi);

        /// <summary>
        /// KREDİTİN QİYMƏTİ — müştərinin cəmi ödəyəcəyi məbləğ (₼):
        /// <c>Kreditləşdirilən + Faiz</c> = <c>Aylıq × Müddət</c>.
        /// </summary>
        [NotMapped]
        public decimal KreditQiymeti => CreditMath.TotalPayable(Kreditlesdirilen, FaizDerecesi);

        /// <summary>
        /// «Kreditləşdirilən» xanasının altında göstərilən FAİZ + QİYMƏT mətni:
        /// «Faiz 6 805,60 ₼ · Qiyməti 23 819,60 ₼».
        /// </summary>
        [NotMapped]
        public string FaizVeQiymetMetni =>
            $"Faiz {FaizMeblegi:N2} ₼  ·  Qiyməti {KreditQiymeti:N2} ₼";

        /// <summary>«Aylıq × Müddət» yoxlaması: «1 984,97 ₼ × 12 ay».</summary>
        [NotMapped]
        public string AylıqVeMuddetMetni =>
            MuddetAy <= 0 ? "—" : $"{AylıqOdenis:N2} ₼ × {MuddetAy} ay";

        // ====================================================================
        //  🔢 KÖK (ƏSAS BORC) MƏNTİQİ ✓✓✓ (v6.2.17)
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi:
        //    «Nisyəni böləndə, necə ki aylıq kredit veririksə, oradan
        //     aylıqlardakı KÖK QİYMƏTİ tapırıq. Kredit əlavə gəlir/xərc
        //     tabında GƏLİR-ə basanda və tərəfdaş bölgüsünü seçəndə
        //     ÖDƏNİŞDƏN KÖK QİYMƏTİ ÇIXARILMALIDIR, SONRA BÖLÜNMƏLİDİR.
        //     Kreditlər tabında müqaviləni seçəndə KÖK-də görsənməlidir
        //     və ÖDƏNİLMİŞ KÖK-də görsənməlidir.» ✓✓✓
        // --------------------------------------------------------------------
        //  📌 KÖK = KREDİTLƏŞDİRİLƏN (müştərinin faktiki borcu) ✓
        //  📌 Hər ödənişin içindən KÖK payı çıxılır ✓ → yalnız FAİZ
        //     (mənfəət) tərəfdaşlar arasında bölünür ✓✓✓
        // ====================================================================

        /// <summary>
        /// 🔢 <b>KÖK (ƏSAS BORC)</b> ✓✓✓ — müştərinin qaytarmalı olduğu əsas
        /// məbləğ = <c>Mebleg − İlkin ödəniş</c> ✓
        /// <para>⚠ Bu pul <b>bölünmür</b> ✗ — yalnız FAİZ bölünür ✓✓✓</para>
        /// </summary>
        [NotMapped]
        public decimal Kok => Kreditlesdirilen;

        /// <summary>
        /// 🔢 <b>AYLIQ KÖK</b> ✓✓✓ — hər ay əsas borcdan düşən pay =
        /// <c>KÖK ÷ Müddət</c> ✓
        /// <example>KÖK 17 014 ₼ · 12 ay → <b>1 417,83 ₼/ay</b> ✓</example>
        /// </summary>
        [NotMapped]
        public decimal AylıqKok =>
            MuddetAy > 0 ? Math.Round(Kreditlesdirilen / MuddetAy, 2) : 0m;

        /// <summary>
        /// 📊 KÖK-ün kreditin qiymətindəki payı (0–1) ✓✓✓ —
        /// ödənişdən çıxılan <b>nisbət</b> ✓
        /// <para>Bax <see cref="KokPayi"/> — bütün hesablama bunun üzərindədir ✓</para>
        /// </summary>
        [NotMapped]
        public decimal KokNisbeti =>
            KreditQiymeti > 0m ? Kreditlesdirilen / KreditQiymeti : 0m;

        /// <summary>
        /// 🔢 <b>ÖDƏNİŞDƏKİ KÖK PAYI</b> ✓✓✓ (★ ƏSAS HESABLAMA ★)
        /// <para>
        /// <c>Kök payı = Ödəniş × (KÖK ÷ Kreditin qiyməti)</c> ✓ —
        /// <b>qalan</b> əsas borcla MƏHDUDLAŞDIRILIR ✓✓✓
        /// </para>
        /// <example>
        /// KÖK 17 014 ₼ · Qiymət 23 819,60 ₼ · Aylıq 1 984,97 ₼
        /// <code>
        /// Tam aylıq ödəniş  : 1 984,97 × (17 014 ÷ 23 819,60) = 1 417,83 ₼  ← AYLIQ KÖK ✓
        /// Yarım ödəniş (992) :   992,49 × 0,7143              =   708,92 ₼  ✓ (ədalətli ✓)
        /// KÖK tam ödənilibsə :                                 0,00 ₼  ✓ (hamısı mənfəət ✓✓✓)
        /// </code>
        /// </example>
        /// <param name="odenis">Ödəniş məbləği (₼) ✓</param>
        /// <param name="odenilmisKok">
        /// Bu kredit üzrə <b>artıq ödənilmiş</b> kök (₼) ✓ — qalan borcun
        /// hesablanması üçün ✓ (0 = heç nə ödənilməyib ✓)
        /// </param>
        /// </summary>
        public decimal KokPayi(decimal odenis, decimal odenilmisKok = 0m)
        {
            if (odenis <= 0m || KreditQiymeti <= 0m)
            {
                return 0m;
            }

            var pay = Math.Round(odenis * KokNisbeti, 2);
            var qalan = Math.Max(0m, Kreditlesdirilen - Math.Max(0m, odenilmisKok));

            return Math.Min(pay, Math.Round(qalan, 2));
        }

        /// <summary>
        /// 🔢 <b>NƏZƏRDƏ TUTULAN (PLAN) AYLIQ ÖDƏNİŞ</b> ✓✓✓ (v6.2.24)
        /// <para>
        /// <see cref="AylıqOdenis"/> varsa o ✓ · yoxsa <c>KreditQiymeti ÷ Müddət</c> ✓
        /// · o da yoxdursa <paramref name="ehtiyat"/> qaytarılır ✓
        /// </para>
        /// </summary>
        public decimal PlanAylıqOdenis(decimal ehtiyat = 0m)
        {
            if (AylıqOdenis > 0m) return AylıqOdenis;

            return MuddetAy > 0 && KreditQiymeti > 0m
                ? Math.Round(KreditQiymeti / MuddetAy, 2)
                : ehtiyat;
        }

        /// <summary>
        /// 💰 <b>ÖDƏNİŞƏ DÜŞƏN KÖK PAYI</b> ✓✓✓ (★ v6.2.24 ★)
        /// <para>
        /// ⚠ ƏVVƏLKİ DAVRANIŞ (səhv ✗✓✓): kök payı = <c>ödəniş × (KÖK ÷ qiymət)</c> ✗ →
        /// müştəri aylıqdan <b>ARTIQ</b> ödəyəndə (məs. 654 yerinə 815 ₼ ✗) kök payı
        /// və deməli <b>mənfəət də AVTOMATİK ARTIRDI</b> ✗ → tərəfdaşlar daha çox
        /// pay alırdılar ✗✓✓ (istifadəçi şikayəti ✓)
        /// </para>
        /// <para>
        /// ✅ YENİ MƏNTİQ:
        /// <list type="bullet">
        ///   <item>Yalnız <b>PLAN (aylıq taksit)</b> hissəsindən kök payı çıxılır ✓</item>
        ///   <item><b>PLANDAN ARTIQ</b> ödəniş TAMAMİLƏ kökə (mayaya) gedir ✓✓✓</item>
        /// </list>
        /// </para>
        /// <example>
        /// Aylıq 654 ₼ · KÖK/qiymət nisbəti 0,5 (kök 9 810 ₼ · qiymət 19 620 ₼)
        /// <code>
        /// Ödəniş 654 ₼ → kök 327 ₼ · mənfəət 327 ₼  ✓ (bölünən 327 ₼ ✓)
        /// Ödəniş 815 ₼ → kök 327+161=488 ₼ · mənfəət 327 ₼ ✓✓✓
        ///                (161 ₼ ARTIQ — TAMAMILƏ mayaya ✓ — mənfəət ARTMIN kəsilir ✗)
        /// </code>
        /// </example>
        /// <param name="odenis">Faktiki ödəniş (₼) ✓</param>
        /// <param name="odenilmisKok">Artıq ödənilmiş kök (₼) ✓ — qalan borcla məhdudiyyət üçün ✓</param>
        /// </summary>
        public decimal OdenisKokPayi(decimal odenis, decimal odenilmisKok = 0m)
        {
            if (odenis <= 0m || KreditQiymeti <= 0m)
            {
                return 0m;
            }

            var plan = PlanAylıqOdenis(ehtiyat: odenis);

            // 🔢 Plan hissəsinin kök payı (qalan borcla MƏHDUD ✓)
            var planKök = KokPayi(Math.Min(odenis, plan), odenilmisKok);

            // 💰 Plandan ARTIQ hissə — TAMAMİLƏ kökə (mayaya) ✓✓✓
            var artıq = Math.Max(0m, odenis - plan);

            var cəm = Math.Round(planKök + artıq, 2);

            // Qalan əsas borcdan ARTQ kök yazıla bilməz ✗
            var qalan = Math.Max(0m, Kreditlesdirilen - Math.Max(0m, odenilmisKok));

            return Math.Min(cəm, Math.Round(qalan, 2));
        }

        /// <summary>
        /// 💰 <b>BÖLGÜ BAZASI (MƏNFƏƏT)</b> ✓✓✓ (★ v6.2.24 ★) =
        /// <c>ödəniş − kök payı</c> ✓
        /// <para>
        /// ⚠ Plandan artıq ödəniş mənfəətə ƏLAVƏ olunmur ✗ — çünki tamamilə
        /// kökə getdiyi üçün <see cref="OdenisKokPayi"/> onu çıxır ✓✓✓.
        /// Nəticədə 654 ₼-lik taksitin mənfəəti 815 ₼ ödənildikdə də
        /// <b>EYNİ</b> qalır ✓ (327 ₼ → 163,5 ₼ və 163,5 ₼ ✓✓✓)
        /// </para>
        /// </summary>
        public decimal OdenisMenfeetBazasi(decimal odenis, decimal odenilmisKok = 0m)
        {
            if (odenis <= 0m)
            {
                return 0m;
            }

            var kök = OdenisKokPayi(odenis, odenilmisKok);
            var baza = Math.Round(odenis - kök, 2);

            return baza > 0m ? baza : 0m;
        }

        /// <summary>
        /// ✅ <b>ÖDƏNİLMİŞ KÖK</b> ✓✓✓ — artıq qaytarılmış əsas borc (₼)
        /// <para>
        /// ⚠ Bazada SAXLANILMIR ✗ — UI (ViewModel) hesablayıb doldurur ✓
        /// (bütün ödənişlərin kök paylarının cəmi ✓)
        /// </para>
        /// </summary>
        [NotMapped]
        public decimal OdenilmisKok { get; set; }

        /// <summary>⏳ <b>QALIQ KÖK</b> = KÖK − ödənilmiş kök (₼) ✓</summary>
        [NotMapped]
        public decimal QaliqKok => Math.Max(0m, Kok - OdenilmisKok);

        /// <summary>📊 KÖK-ün ödənilmə faizi (0–1) — irəliləyiş zolağı üçün ✓</summary>
        [NotMapped]
        public double KokOdenisFaizi =>
            Kok <= 0m ? 0d : (double)Math.Clamp(OdenilmisKok / Kok, 0m, 1m);

        /// <summary>🔢 «Kök: 17 014,00 ₼ · aylıq kök: 1 417,83 ₼» ✓</summary>
        [NotMapped]
        public string KokMetni =>
            $"Kök {Kok:N2} ₼  ·  aylıq kök {AylıqKok:N2} ₼";

        /// <summary>✅ «Ödənilmiş 2 835,66 ₼ · qalıq kök 14 178,34 ₼» ✓</summary>
        [NotMapped]
        public string OdenilmisKokMetni =>
            $"Ödənilmiş {OdenilmisKok:N2} ₼  ·  qalıq {QaliqKok:N2} ₼";

        /// <summary>
        /// KREDİT BİTİBMİ? — bütün məbləğ ödənilibsə <c>true</c>.
        /// <para>
        /// <paramref name="odenilmis"/> — bu kreditə aid toplanmış ödənişlərin cəmi.
        /// Kiçik yuvarlaqlaşdırma fərqi (0,50 ₼) nəzərə alınır.
        /// </para>
        /// </summary>
        public bool Bitibmi(decimal odenilmis)
            => KreditQiymeti > 0m && odenilmis >= KreditQiymeti - 0.50m;

        /// <summary>Kredit bitmiş sayılır? (status «Bağlı» və ya müddət tamamlanıb)</summary>
        [NotMapped]
        public bool BitmisKredit =>
            string.Equals(Status, "Bağlı", StringComparison.OrdinalIgnoreCase)
            || (MuddetAy > 0 && BaslamaTarixi.AddMonths(MuddetAy) <= DateTime.Today);

        /// <summary>Kreditin neçə faizi ödənilib (0–100).</summary>
        public decimal OdenisFaizi(decimal odenilmis)
            => KreditQiymeti <= 0m ? 0m : Math.Min(100m, Math.Round(odenilmis / KreditQiymeti * 100m, 1));

        /// <summary>İllik faiz dərəcəsi (%).</summary>
        public decimal FaizDerecesi { get; set; }

        /// <summary>Müddət (ay).</summary>
        public int MuddetAy { get; set; }

        /// <summary>Aylıq ödəniş (AZN).</summary>
        public decimal AylıqOdenis { get; set; }

        /// <summary>Kreditin başlama tarixi.</summary>
        public DateTime BaslamaTarixi { get; set; } = DateTime.Today;

        /// <summary>Status (Aktiv, Bağlı, Gecikmiş).</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Əlavə qeyd.</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>
        /// ComboBox-larda göstərilən tam məlumat:
        /// "M-0001 | Rebbil | 77HH717 | Mercedes C300".
        /// </summary>
        [NotMapped]
        public string DisplayText
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(MuqavileNomresi))
                {
                    parts.Add(MuqavileNomresi);
                }
                if (!string.IsNullOrWhiteSpace(Mustəri))
                {
                    parts.Add(Mustəri);
                }
                if (Car is not null && !string.IsNullOrWhiteSpace(Car.QeydiyyatNisani))
                {
                    parts.Add(Car.QeydiyyatNisani);
                }
                if (Car is not null && !string.IsNullOrWhiteSpace(Car.Marka))
                {
                    parts.Add(Car.Marka);
                }
                return string.Join(" | ", parts);
            }
        }
    }
}
