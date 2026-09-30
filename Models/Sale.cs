using System.ComponentModel.DataAnnotations.Schema;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>Avtomobil satışını təmsil edən biznes obyekti.</summary>
    public class Sale
    {
        public int Id { get; set; }

        /// <summary>Satış müqaviləsinin nömrəsi.</summary>
        public string MuqavileNomresi { get; set; } = string.Empty;

        /// <summary>Alıcının adı soyadı.</summary>
        public string Mustəri { get; set; } = string.Empty;

        /// <summary>Satılan avtomobil.</summary>
        public int? CarId { get; set; }

        public CarItem? Car { get; set; }

        /// <summary>Satış qiyməti (AZN).</summary>
        public decimal SatisQiymeti { get; set; }

        /// <summary>Satış anındaki maya dəyəri (hesablamadan snapshot).</summary>
        public decimal MayaDeyeri { get; set; }

        /// <summary>Satış tarixi.</summary>
        public DateTime SatisTarixi { get; set; } = DateTime.Today;

        /// <summary>Ödəniş üsulu (Nağd, Kart / Köçürmə, Barter).</summary>
        public string OdenisUsulu { get; set; } = string.Empty;

        /// <summary>
        /// Barter zamanı alıcının verdiyi avtomobilin dəyəri (AZN).
        /// Yalnız <see cref="IsBarter"/> <c>true</c> olduqda mənalıdır.
        /// </summary>
        public decimal BarterMebleg { get; set; }

        /// <summary>
        /// Barter zamanı alıcının verdiyi avtomobilin təsviri
        /// (məs. «2013 Kia Rio 90AB123»).
        /// </summary>
        public string BarterTesviri { get; set; } = string.Empty;

        /// <summary>
        /// Barter satışında alıcının verdiyi avtomobilin TAM məlumatı.
        /// <para>
        /// YALNIZ FORMA ÜÇÜNDÜR (bazada saxlanılmır): satış qeydə alınarkən
        /// <see cref="Services.ISaleService.AddSaleAsync"/> bu maşını avtomatik
        /// olaraq AVTO PARKA əlavə edir və <b>maya dəyəri = <see cref="BarterMebleg"/></b>
        /// təyin edir.
        /// </para>
        /// </summary>
        [NotMapped]
        public CarItem? ReceivedCar { get; set; }

        /// <summary>Barter nəticəsində parka əlavə edilmiş avtomobilin Id-si (yalnız forma üçün).</summary>
        [NotMapped]
        public int? ReceivedCarId { get; set; }

        /// <summary>Əlavə qeyd.</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Satışın barter ilə edilib-edilmədiyi.</summary>
        [NotMapped]
        public bool IsBarter =>
            string.Equals(OdenisUsulu, "Barter", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Barter zamanı nağd (və ya köçürmə ilə) ödənilən hissə:
        /// satış qiyməti − barter məbləği.
        /// </summary>
        [NotMapped]
        public decimal NagdMebleg =>
            IsBarter ? Math.Max(0m, SatisQiymeti - BarterMebleg) : SatisQiymeti;

        // ====================================================================
        //  ⏳ MÖHLƏTLİ (NİSYƏ) SATIŞ — «nə vaxt, nə qədər ödəniləcək» ✓✓✓
        // --------------------------------------------------------------------
        //  Satış formasında ödəniş üsulu «Möhlət (nisyə)» seçiləndə müştərinin
        //  söz verdiyi ödənişlər sətir-sətir yazılır ✓ (BİRDƏN ÇOX ✓):
        //
        //      Satış qiyməti : 22 000,00 ₼
        //      Dərhal        :  7 000,00 ₼   (avtomatik = qiymət − möhlətlər ✓)
        //      Möhlətlər     : 10 000,00 ₼ → 21.10.2026
        //                       5 000,00 ₼ → 21.11.2026
        // ====================================================================

        /// <summary>Bu satışa yazılmış möhlət (nisyə) ödənişləri ✓.</summary>
        [NotMapped]
        public List<OdenisMohlet> Mohletler { get; set; } = new();

        /// <summary>Möhlətə salınmış məbləğlərin cəmi (₼).</summary>
        [NotMapped]
        public decimal MohletCemi => Mohletler.Sum(m => m.Mebleg);

        /// <summary>Möhlətli satışdırmı? (ödəniş üsulu «Möhlət (nisyə)» ✓)</summary>
        [NotMapped]
        public bool IsMohletli => Catalog.IsMohletliSatis(OdenisUsulu);

        /// <summary>Möhlət yazılıbmı?</summary>
        [NotMapped]
        public bool MohletVar => Mohletler.Count > 0;

        /// <summary>Hələ ödənilməmiş möhlətlərin cəmi (₼) — gözlənilən pul ✓.</summary>
        [NotMapped]
        public decimal MohletGozlenilen => Mohletler.Where(m => !m.Odenilib).Sum(m => m.Mebleg);

        /// <summary>
        /// 💰 SATIŞDA DƏRHAL ödənilən pul (₼) = nağd hissə − möhlətlər ✓✓✓
        /// (barter hissəsi onsuz da pul deyil ✗)
        /// </summary>
        [NotMapped]
        public decimal DerhalOdenilen => Math.Max(0m, NagdMebleg - MohletCemi);

        /// <summary>Möhlət xülasəsi: «⏳ 10 000,00 ₼ → 21.10.2026 · 5 000,00 ₼ → 21.11.2026».</summary>
        [NotMapped]
        public string MohletMetni
        {
            get
            {
                if (Mohletler.Count == 0)
                {
                    return string.Empty;
                }

                var setirler = Mohletler
                    .OrderBy(m => m.Tarix)
                    .ThenBy(m => m.Id)
                    .Select(m => $"{m.Mebleg:N2} ₼ → {m.TarixMetni}{(m.Odenilib ? " ✅" : string.Empty)}");

                return $"⏳ Möhlət {MohletCemi:N2} ₼  ({string.Join(" · ", setirler)})";
            }
        }


        /// <summary>Cədvəldə göstərilən ödəniş üsulu nişanı.</summary>
        [NotMapped]
        public string OdenisUsuluMetni =>
            IsBarter ? "🔄 Barter"
                     : IsMohletli ? "⏳ Möhlət (nisyə)"
                                  : $"💵 {OdenisUsulu}".TrimEnd();

        /// <summary>
        /// Cədvəllərdə göstərilən ödəniş mətni: barter satışlarda
        /// «🔄 8 000 ₼ + 💵 2 000 ₼», möhlətli satışlarda
        /// «⏳ nisyə — dərhal 7 000 ₼ + möhlət 15 000 ₼», digərlərində üsulun adı.
        /// </summary>
        [NotMapped]
        public string OdenisMetni
        {
            get
            {
                if (IsBarter && BarterMebleg > 0m)
                {
                    return OdenisBolqusuMetni;
                }

                if (IsMohletli || MohletVar)
                {
                    return $"{DerhalOdenilen:N2} ₼ dərhal + {MohletCemi:N2} ₼ möhlət";
                }

                return OdenisUsulu;
            }
        }

        /// <summary>
        /// Ödəniş bölgüsünün qısa təsviri
        /// (məs. «🔄 8 000 ₼ + 💵 2 000 ₼»).
        /// </summary>
        [NotMapped]
        public string OdenisBolqusuMetni
        {
            get
            {
                if (!IsBarter)
                {
                    return $"{SatisQiymeti:N2} ₼".Replace(",", " ");
                }

                if (BarterMebleg <= 0m)
                {
                    return $"🔄 barter ({SatisQiymeti:N2} ₼)".Replace(",", " ");
                }

                if (NagdMebleg <= 0m)
                {
                    return $"🔄 barter {BarterMebleg:N2} ₼".Replace(",", " ");
                }

                return $"🔄 {BarterMebleg:N2} ₼ + 💵 {NagdMebleg:N2} ₼".Replace(",", " ");
            }
        }

        /// <summary>Satışdan əldə olunan mənfəət (satış qiyməti − maya dəyəri).</summary>
        [NotMapped]
        public decimal Menfeet => SatisQiymeti - MayaDeyeri;

        /// <summary>Grid-də göstərilən avtomobil məlumatı.</summary>
        [NotMapped]
        public string CarInfo => Car?.DisplayName ?? string.Empty;
    }
}
