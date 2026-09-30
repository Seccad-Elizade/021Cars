using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 💵 <b>KASSA HESABATININ VAHİD DÜSTURU</b> — saf məntiq ✓✓✓
    /// (UI-dən və bazadan asılı deyil → asanlıqla yoxlanıla bilər ✓)
    /// <para>
    /// <b>Nə üçün ortaq sinif?</b> — əvvəllər Maliyyə Paneli kartları bir düsturla,
    /// qrafik isə başqa düsturla hesablanırdı ✗ (istifadəçi: «dövr gəliri/xərci
    /// işləmir»). İndi <b>Kassa tabı</b> ✓ <b>Maliyyə Paneli</b> ✓ <b>Veb Dashboard</b> ✓
    /// HAMISI bu sinifdən istifadə edir → rəqəmlər HƏMİŞƏ üst-üstə düşür ✓✓✓
    /// </para>
    /// <para><b>DÜSTUR:</b></para>
    /// <code>
    /// GƏLİR (kassaya DAXİL olan):
    ///   💰 satışların nağd/köçürmə hissəsi      (barter əvəzi pul DEYİL ✗)
    ///   🏦 kredit ilkin ödənişləri (avanslar)
    ///   💳 kredit ödənişləri (erkən bağlama · ödənilmiş gecikmə)
    ///   ⏳ ÖDƏNİLMİŞ möhlətlər                  (yalnız pul GƏLƏNDƏ ✓)
    ///   ✍ əl ilə yazılan kassa daxilolmaları
    ///
    /// XƏRC (kassadan ÇIXAN):
    ///   💸 BÜTÜN xərc qeydləri (avtomobil ✓ ofis ✓)
    ///   🏷️ kredit əlavə xərcləri
    ///   👥 tərəfdaşlara FAKTİKİ VERİLƏN pul («pul ver» ✓✓✓)
    ///   ✍ əl ilə yazılan kassa xərcləri
    ///
    /// QALIQ = GƏLİR − XƏRC   (+ dövrə qədərki açılış qalığı ✓)
    /// </code>
    /// <para>
    /// 👥 <b>TƏRƏFDAŞ BÖLGÜSÜ QAYDASI</b> ✓✓✓ — satışda/kreditdə hesablanan tərəfdaş
    /// payı kassadan <b>DƏRHAL ÇIXMIR</b> ✗!! Pul tərəfdaşa <b>faktiki verilənə qədər
    /// kassada qalır</b> ✓ (yalnız «Tərəfdaşa verilməli» kimi göstərilir ✓).
    /// Ödəniş «👥 Tərəfdaşlar» tabında edilir ✓ →
    /// <see cref="Models.PartnerPayment"/> sətri kassa XƏRCinə çevrilir ✓.
    /// </para>
    /// <para>
    /// Buna görə kassa qalığı həmişə <b>real pulu</b> göstərir ✓;
    /// «<see cref="Models.KassaHesabati.OzQaliq"/>» isə tərəfdaş payları
    /// çıxıldıqdan sonra qalan məbləği göstərir ✓.
    /// </para>
    /// </summary>
    public static class KassaHesabi
    {
        // ====================================================================
        //  ① KÖMƏKÇİ HESABLAMALAR (Maliyyə Paneli ilə ORTAQ ✓✓✓)
        // ====================================================================

        /// <summary>Tarix verilmiş dövrün İÇİNDƏDİRMİ? (gün dəqiqliyi ✓)</summary>
        public static bool InRange(DateTime tarix, DateTime from, DateTime to)
            => tarix.Date >= from.Date && tarix.Date <= to.Date;

        /// <summary>Möhlət siyahısının cəmi (₼).</summary>
        public static decimal MohletCemi(IEnumerable<OdenisMohlet>? rows)
            => rows is null ? 0m : rows.Sum(m => m.Mebleg);

        /// <summary>
        /// 💰 SATIŞDA DƏRHAL ödənilən pul (₼) = nağd hissə − möhlətlər ✓✓✓
        /// (barter əvəzi nağd sayılmır ✗)
        /// </summary>
        public static decimal SatisDerhalOdenilen(Sale sale)
            => Math.Max(0m, sale.NagdMebleg - MohletCemi(sale.Mohletler));

        /// <summary>
        /// 👥 <b>KREDİT BÖLGÜSÜ PAYLARI</b> ✓✓✓ — kreditin <b>ÖZ</b> xeyirindən tutulan paylar
        /// <para>✗ Əməliyyat payları SAYILMIR ✗ — onlar aylıq taksitin bölgüsüdür ✓</para>
        /// </summary>
        public static List<PartnerShare> KreditPaylariniSec(IEnumerable<PartnerShare>? paylar)
            => paylar is null
                ? new List<PartnerShare>()
                : paylar.Where(p => p.CreditId.HasValue && !p.CreditTransactionId.HasValue).ToList();

        /// <summary>
        /// 👥 <b>SATIŞ BÖLGÜSÜ PAYLARI</b> ✓✓✓ — satılan maşının xeyirindən tutulan paylar
        /// </summary>
        public static List<PartnerShare> SatisPaylariniSec(IEnumerable<PartnerShare>? paylar)
            => paylar is null
                ? new List<PartnerShare>()
                : paylar.Where(p => p.SaleId.HasValue).ToList();

        /// <summary>
        /// 🏦 KREDİTİN ilkin ödənişində DƏRHAL ödənilən hissə (₼) = avans − möhlətlər ✓
        /// </summary>
        public static decimal IlkinDerhalOdenilen(Credit credit, IEnumerable<OdenisMohlet>? mohletler)
            => Math.Max(0m, credit.IlkinOdenis - MohletCemi(mohletler));

        /// <summary>
        /// ⏳ Dövrdə <b>ÖDƏNİLMİŞ</b> möhlətlərin cəmi (₼) — kassaya DAXİL OLAN pul ✓✓✓
        /// (ödənilməmiş möhlət gəlirə yazılmır ✗)
        /// </summary>
        public static decimal OdenilmisMohletCemi(
            IEnumerable<OdenisMohlet>? rows,
            DateTime from,
            DateTime to)
        {
            if (rows is null)
            {
                return 0m;
            }

            return rows
                .Where(m => m.Odenilib
                            && m.OdenilmeTarixi is DateTime t
                            && InRange(t, from, to))
                .Sum(m => m.Mebleg);
        }

        /// <summary>
        /// 💰 Pul <b>KASSAYA GƏLDİ?</b> ✓✓✓ — kredit ödənişi ✓ vaxtından tez bağlama ✓
        /// ödənilmiş gecikmə cəriməsi ✓
        /// <para>✗ «Transfer» ✗ «Barter» ✗ «Möhlət» ✗ ödənilməmiş gecikmə — pul gətirmir ✗</para>
        /// </summary>
        public static bool KreditDaxilolmasidir(CreditTransaction hereket)
            => hereket.Nov == "Gəlir"
               || hereket.Nov == "Vaxtından tez bağlama"
               || (hereket.Nov == "Gecikmə" && hereket.Odenilib);

        // ====================================================================
        //  ② ƏSAS HESABAT — hərəkətləri yığıb dövrə görə hesablayır ✓✓✓
        // ====================================================================

        /// <summary>
        /// Bütün mənbələrdən kassa jurnalını qurur və dövr üzrə yekunları çıxarır ✓✓✓
        /// </summary>
        /// <param name="from">Dövrün başlanğıcı (daxil ✓).</param>
        /// <param name="to">Dövrün sonu (daxil ✓).</param>
        /// <param name="terefdasOdenisleri">
        /// 👥 «Tərəfdaşlar» tabında edilən <b>FAKTİKİ ödənişlər</b> ✓✓✓
        /// (yalnız bunlar kassadan çıxır ✗ — hesablanmış paylar YOX ✗)
        /// </param>
        public static KassaHesabati Qur(
            IEnumerable<Sale> satislar,
            IEnumerable<Credit> kreditler,
            IEnumerable<CreditTransaction> kreditEmeliyyatlari,
            IEnumerable<ExpenseItem> xercler,
            IEnumerable<PartnerShare> kreditPaylari,
            IEnumerable<PartnerShare> satisPaylari,
            IEnumerable<PartnerPayment> terefdasOdenisleri,
            IEnumerable<KassaHereket> elIleHereketleri,
            DateTime from,
            DateTime to)
        {
            if (to < from)
            {
                (from, to) = (to, from);
            }

            var satisList = satislar.ToList();
            var kreditList = kreditler.ToList();
            var emeliyyatList = kreditEmeliyyatlari.ToList();
            var kreditPayList = kreditPaylari.ToList();
            var satisPayList = satisPaylari.ToList();
            var odenisList = terefdasOdenisleri.ToList();

            var setirler = new List<KassaSetiri>();

            // 🚫 TRANSFER OLUNMUŞ kreditlərin avansı ayrıca qeyd olunur —
            //    Maliyyə Paneli ilə EYNİ məntiq ✓ (aşağıda izah var ✓)
            var transferliIdler = emeliyyatList
                .Where(t => (t.Nov == "Transfer" || t.Nov == "Transfer olunmaq")
                            && t.CreditId.HasValue)
                .Select(t => t.CreditId!.Value)
                .ToHashSet();

            SatislariYaz(setirler, satisList);
            KreditleriYaz(setirler, kreditList, emeliyyatList, transferliIdler);
            XercleriYaz(setirler, xercler);

            // 👥 YALNIZ faktiki verilən pullar kassadan çıxır ✓✓✓
            TerefdasOdenisleriniYaz(setirler, odenisList);

            ElIleYaz(setirler, elIleHereketleri);

            // ---- ⏳ GÖZLƏNİLƏN MÖHLƏTLƏR (hələ pul gəlməyib ✗) ----------------
            var butunMohletler = satisList.SelectMany(s => s.Mohletler)
                .Concat(kreditList.SelectMany(c => c.IlkinMohletleri))
                .ToList();

            var gozlenilen = butunMohletler.Where(m => !m.Odenilib).Sum(m => m.Mebleg);
            var gecikmis = butunMohletler
                .Where(m => !m.Odenilib && m.Tarix.Date < DateTime.Today)
                .Sum(m => m.Mebleg);

            // ---- Dövrə görə ayırma + açılış qalığı ✓✓✓ ----------------------
            var acilis = setirler
                .Where(s => s.Tarix.Date < from.Date)
                .Sum(s => s.IsareliMebleg);

            var dovruSetirleri = setirler
                .Where(s => InRange(s.Tarix, from, to))
                .OrderBy(s => s.Tarix)
                .ThenBy(s => s.Qrup)
                .ToList();

            // ---- 👥 TƏRƏFDAŞ PAYLARI (kassada QALAN pul ✓ — xərc DEYİL ✗) ----
            //  ⚠ Hesablanan pay XƏRC yazılmır ✗!! Pul tərəfdaşa faktiki
            //    verilənə qədər kassada qalır ✓ (yalnız məlumat üçün ✓)
            var butunPaylar = TerefdasPaylari(kreditPayList, satisPayList, kreditList, satisList);

            var terefdasPayi = butunPaylar
                .Where(p => InRange(p.Tarix, from, to))
                .Sum(p => p.Mebleg);

            // Bütün vaxt üzrə hələ verilməli olan məbləğ (kassadaki «tərəfdaş pulu» ✓)
            var terefdasVerilmeli = butunPaylar.Sum(p => p.Mebleg) - odenisList.Sum(p => p.Mebleg);

            return new KassaHesabati
            {
                Setirler = dovruSetirleri,
                AcilisQaligi = acilis,
                SatisDaxilolma = Cem(dovruSetirleri, "Satış"),
                IlkinOdenisDaxilolma = Cem(dovruSetirleri, "İlkin ödəniş"),
                KreditDaxilolma = Cem(dovruSetirleri, "Kredit"),
                MohletDaxilolma = Cem(dovruSetirleri, "Möhlət"),
                ElIleDaxilolma = Cem(dovruSetirleri, "Əl ilə"),
                XercCemi = Cem(dovruSetirleri, "Xərc"),
                KreditXerci = Cem(dovruSetirleri, "Kredit xərci"),
                TerefdasOdenilen = Cem(dovruSetirleri, "Tərəfdaş"),
                ElIleXerci = Cem(dovruSetirleri, "Əl ilə xərc"),
                TerefdasPayi = terefdasPayi,
                TerefdasVerilmeli = terefdasVerilmeli,
                GozlenilenMohlet = gozlenilen,
                GecikmisMohlet = gecikmis
            };
        }

        // ====================================================================
        //  ③ SƏTİR QURUCULARI (hər mənbə üçün bir metod ✓✓✓)
        // ====================================================================

        /// <summary>
        /// 💰 <b>SATIŞLAR</b> — dərhal ödənilən nağd/köçürmə hissəsi + sonradan
        /// ödənilmiş möhlətlər ✓✓✓
        /// </summary>
        private static void SatislariYaz(ICollection<KassaSetiri> setirler, IReadOnlyList<Sale> satislar)
        {
            foreach (var sale in satislar)
            {
                var aciqlama = SatisAciqlamasi(sale);

                if (sale.MohletCemi > 0m)
                {
                    aciqlama += $"   ·   ⏳ möhlət {sale.MohletCemi:N2} ₼";
                }

                Elave(
                    setirler,
                    sale.SatisTarixi,
                    KassaHereket.NovDaxilolma,
                    "Satış",
                    sale.IsBarter ? "💵 Barter fərqi (nağd)" : $"💵 {sale.OdenisUsulu} satış",
                    SatisDerhalOdenilen(sale),
                    sale.IsBarter || sale.IsMohletli ? Catalog.PaymentMethods[0] : sale.OdenisUsulu,
                    aciqlama);

                // ⏳ Sonradan ödənilən möhlətlər → pulun GƏLDİYİ tarixdə yazılır ✓
                foreach (var mohlet in sale.Mohletler.Where(m => m.Odenilib && m.OdenilmeTarixi.HasValue))
                {
                    Elave(
                        setirler,
                        mohlet.OdenilmeTarixi!.Value,
                        KassaHereket.NovDaxilolma,
                        "Möhlət",
                        "⏳ Nisyə satış möhləti",
                        mohlet.Mebleg,
                        mohlet.OdenisUsulu,
                        $"{aciqlama}   ·   söz verilən tarix {mohlet.TarixMetni}");
                }
            }
        }

        /// <summary>
        /// 🏦 <b>KREDİTLƏR</b> — ilkin ödəniş (avans ✓ onun möhlətləri ✓),
        /// kredit ödənişləri ✓ və kredit əlavə xərcləri ✓✓✓
        /// </summary>
        private static void KreditleriYaz(
            ICollection<KassaSetiri> setirler,
            IReadOnlyList<Credit> kreditler,
            IReadOnlyList<CreditTransaction> emeliyyatlar,
            HashSet<int> transferliIdler)
        {
            foreach (var credit in kreditler)
            {
                // 🚫 TRANSFER OLUNMUŞ kreditin avansı kassaya yazılmır ✓
                //    (Maliyyə Paneli də belə hesablayır ✓ → rəqəmlər üst-üstə düşür ✓)
                if (transferliIdler.Contains(credit.Id))
                {
                    continue;
                }

                var aciqlama = credit.DisplayText;

                if (credit.IlkinMohletCemi > 0m)
                {
                    aciqlama += $"   ·   ⏳ möhlət {credit.IlkinMohletCemi:N2} ₼";
                }

                Elave(
                    setirler,
                    credit.BaslamaTarixi,
                    KassaHereket.NovDaxilolma,
                    "İlkin ödəniş",
                    "🏦 İlkin ödəniş (avans)",
                    IlkinDerhalOdenilen(credit, credit.IlkinMohletleri),
                    Catalog.PaymentMethods[0],
                    aciqlama);

                foreach (var mohlet in credit.IlkinMohletleri.Where(m => m.Odenilib && m.OdenilmeTarixi.HasValue))
                {
                    Elave(
                        setirler,
                        mohlet.OdenilmeTarixi!.Value,
                        KassaHereket.NovDaxilolma,
                        "Möhlət",
                        "⏳ İlkin ödəniş möhləti",
                        mohlet.Mebleg,
                        mohlet.OdenisUsulu,
                        $"{aciqlama}   ·   söz verilən tarix {mohlet.TarixMetni}");
                }
            }

            // 🔎 Kredit axtarışı indeksli olsun → milyon əməliyyatda donma yoxdur ✓
            var kreditXeritesi = kreditler
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var hereket in emeliyyatlar)
            {
                var aciqlama = hereket.CreditId is int cid && kreditXeritesi.TryGetValue(cid, out var credit)
                    ? credit.DisplayText
                    : $"Kredit #{hereket.CreditId}";

                if (hereket.InstallmentNo is int no)
                {
                    aciqlama += $"   ·   {no}-cu taksit";
                }

                if (KreditDaxilolmasidir(hereket))
                {
                    var kateqoriya = hereket.Nov switch
                    {
                        "Vaxtından tez bağlama" => "⚡ Vaxtından tez bağlama",
                        "Gecikmə" => "⚠ Gecikmə ödənişi",
                        _ => "💳 Kredit ödənişi"
                    };

                    Elave(setirler, hereket.Tarix, KassaHereket.NovDaxilolma, "Kredit",
                        kateqoriya, hereket.Mebleg, Catalog.PaymentMethods[0], aciqlama);
                }
                else if (hereket.Nov == "Xərc")
                {
                    var izah = string.IsNullOrWhiteSpace(hereket.Tesvir)
                        ? aciqlama
                        : $"{aciqlama}   ·   {hereket.Tesvir}";

                    Elave(setirler, hereket.Tarix, KassaHereket.NovXerc, "Kredit xərci",
                        "🏷️ Kredit əlavə xərci", hereket.Mebleg, Catalog.PaymentMethods[0], izah);
                }
            }
        }

        /// <summary>
        /// 💸 <b>BÜTÜN XƏRC QEYDLƏRİ</b> (avtomobil ✓ ofis ✓) — «Xərclər» tabı ilə
        /// EYNİ mənbə ✓✓✓ (heç bir xərc gizli qalmır ✓)
        /// </summary>
        private static void XercleriYaz(ICollection<KassaSetiri> setirler, IEnumerable<ExpenseItem> xercler)
        {
            foreach (var xerc in xercler)
            {
                var kateqoriya = string.IsNullOrWhiteSpace(xerc.Kategoriya)
                    ? $"💸 {xerc.Teyinat}".TrimEnd()
                    : $"💸 {xerc.Teyinat} · {xerc.Kategoriya}";

                var hisseler = new List<string>();
                if (xerc.Car is not null && !string.IsNullOrWhiteSpace(xerc.Car.DisplayName))
                {
                    hisseler.Add(xerc.Car.DisplayName);
                }
                if (!string.IsNullOrWhiteSpace(xerc.Qeyd))
                {
                    hisseler.Add(xerc.Qeyd);
                }

                Elave(
                    setirler,
                    xerc.Tarix,
                    KassaHereket.NovXerc,
                    "Xərc",
                    kateqoriya,
                    xerc.Mebleg,
                    string.IsNullOrWhiteSpace(xerc.OdenisUsulu) ? Catalog.PaymentMethods[0] : xerc.OdenisUsulu,
                    string.Join(" · ", hisseler));
            }
        }

        /// <summary>
        /// 👥 <b>TƏRƏFDAŞLARA VERİLƏN PULLAR</b> ✓✓✓ — «Tərəfdaşlar» tabındaki
        /// <b>«pul ver»</b> əməliyyatları kassadan <b>ÇIXIŞ</b> kimi yazılır ✗
        /// <para>
        /// 🐞 ƏVVƏL burada <b>hesablanmış payların ÖZÜ</b> xərc kimi yazılırdı ✗ →
        /// pul tərəfdaşa hələ verilməmiş də kassadan «yoxa çıxırdı» ✗✗✗
        /// (istifadəçi: «tərəfdaş bölgüsü hesablandı amma pul verilməyibse,
        /// hələ də kassada qalmalıdır» ✓)
        /// </para>
        /// <para>
        /// ✅ İNDİ: yalnız <b>FAKTİKİ ödəniş</b> kassadan çıxır ✓ — hesablanan pay
        /// isə <see cref="KassaHesabati.TerefdasPayi"/> / <c>TerefdasVerilmeli</c>
        /// kimi <b>məlumat</b> kimi göstərilir ✓ (pul kassada qalır ✓)
        /// </para>
        /// </summary>
        private static void TerefdasOdenisleriniYaz(
            ICollection<KassaSetiri> setirler,
            IReadOnlyList<PartnerPayment> odenisler)
        {
            foreach (var odenis in odenisler.Where(o => o.Mebleg > 0m))
            {
                var aciqlama = string.IsNullOrWhiteSpace(odenis.Qeyd)
                    ? odenis.Terefdas
                    : $"{odenis.Terefdas} — {odenis.Qeyd}";

                Elave(
                    setirler,
                    odenis.Tarix,
                    KassaHereket.NovXerc,
                    "Tərəfdaş",
                    "👥 Tərəfdaşa verildi",
                    odenis.Mebleg,
                    string.IsNullOrWhiteSpace(odenis.OdenisUsulu)
                        ? Catalog.PaymentMethods[0]
                        : odenis.OdenisUsulu,
                    aciqlama);
            }
        }

        /// <summary>
        /// 👥 <b>HESABLANMIŞ tərəfdaş payları</b> — (tarix, məbləğ) cütləri ✓✓✓
        /// <para>
        /// ⚠ Bu paylar <b>KASSADAN ÇIXMIR</b> ✗ — yalnız «nə qədər pul tərəfdaşlara
        /// aiddir?» sualına cavab verir ✓ (kassa XƏRCinə <b>düşmür</b> ✗)
        /// </para>
        /// <para>
        /// 📅 Tarix mənbəyə görə seçilir: kredit payı → <c>Credit.BaslamaTarixi</c> ✓,
        /// satış payı → <c>Sale.SatisTarixi</c> ✓ (jurnal sətirləri ilə eyni ✓)
        /// </para>
        /// </summary>
        private static List<(DateTime Tarix, decimal Mebleg)> TerefdasPaylari(
            IReadOnlyList<PartnerShare> kreditPaylari,
            IReadOnlyList<PartnerShare> satisPaylari,
            IReadOnlyList<Credit> kreditler,
            IReadOnlyList<Sale> satislar)
        {
            var netice = new List<(DateTime Tarix, decimal Mebleg)>();

            var kreditXeritesi = kreditler.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
            var satisXeritesi = satislar.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());

            foreach (var pay in kreditPaylari.Where(p => p.Mebleg > 0m && p.CreditId.HasValue))
            {
                if (kreditXeritesi.TryGetValue(pay.CreditId!.Value, out var credit))
                {
                    netice.Add((credit.BaslamaTarixi.Date, pay.Mebleg));
                }
            }

            foreach (var pay in satisPaylari.Where(p => p.Mebleg > 0m && p.SaleId.HasValue))
            {
                if (satisXeritesi.TryGetValue(pay.SaleId!.Value, out var sale))
                {
                    netice.Add((sale.SatisTarixi.Date, pay.Mebleg));
                }
            }

            return netice;
        }

        /// <summary>✍ <b>ƏL İLƏ</b> yazılan kassa hərəkətləri ✓</summary>
        private static void ElIleYaz(ICollection<KassaSetiri> setirler, IEnumerable<KassaHereket> hereketler)
        {
            foreach (var hereket in hereketler)
            {
                Elave(
                    setirler,
                    hereket.Tarix,
                    hereket.IsDaxilolma ? KassaHereket.NovDaxilolma : KassaHereket.NovXerc,
                    hereket.IsDaxilolma ? "Əl ilə" : "Əl ilə xərc",
                    $"✍ {hereket.Kateqoriya}",
                    hereket.Mebleg,
                    hereket.OdenisUsulu,
                    hereket.Qeyd);
            }
        }

        /// <summary>Satışın izahat mətni: «Rebbil · Mercedes C300 · müq. S-0004».</summary>
        private static string SatisAciqlamasi(Sale sale)
        {
            var hisseler = new List<string>();

            if (!string.IsNullOrWhiteSpace(sale.Mustəri))
            {
                hisseler.Add(sale.Mustəri);
            }

            if (sale.Car is not null && !string.IsNullOrWhiteSpace(sale.Car.DisplayName))
            {
                hisseler.Add(sale.Car.DisplayName);
            }

            if (!string.IsNullOrWhiteSpace(sale.MuqavileNomresi))
            {
                hisseler.Add($"müq. {sale.MuqavileNomresi}");
            }

            return hisseler.Count == 0 ? "Satış" : string.Join(" · ", hisseler);
        }

        /// <summary>
        /// Müəyyən qrupun məbləğ cəmi (₼) ✓✓✓
        /// <para>
        /// ⚠ Qruplar İSTİQAMƏTƏ görə HOMOGENDİR ✓ — daxilolma qrupları
        /// («Satış» · «İlkin ödəniş» · «Kredit» · «Möhlət» · «Əl ilə» ✓) və
        /// xərc qrupları («Xərc» · «Kredit xərci» · «Tərəfdaş» · «Əl ilə xərc» ✗)
        /// HEÇ VAXT qarışmır ✓✓✓
        /// </para>
        /// <para>
        /// 🐞 <b>ƏVVƏL <c>&amp;&amp; s.IsDaxilolma</c> şərti var idi</b> ✗ →
        /// XƏRC qruplarının cəmi <b>HƏMİŞƏ 0</b> çıxırdı ✗✗✗
        /// («xərclər görünmür ✗» xətasının kökü MƏHZ bu idi ✓)
        /// </para>
        /// </summary>
        private static decimal Cem(IEnumerable<KassaSetiri> setirler, string qrup)
            => setirler.Where(s => s.Qrup == qrup).Sum(s => s.Mebleg);

        /// <summary>Sətir əlavə etmək üçün qısa köməkçi ✓.</summary>
        private static void Elave(
            ICollection<KassaSetiri> setirler,
            DateTime tarix,
            string nov,
            string qrup,
            string kateqoriya,
            decimal mebleg,
            string odenisUsulu,
            string aciqlama)
        {
            if (mebleg <= 0m)
            {
                return;   // ✗ 0 məbləğli sətir göstərilmir ✓
            }

            setirler.Add(new KassaSetiri
            {
                Tarix = tarix.Date,
                Nov = nov,
                Qrup = qrup,
                Kateqoriya = kateqoriya,
                Mebleg = mebleg,
                OdenisUsulu = odenisUsulu,
                Aciqlama = aciqlama
            });
        }
    }
}

