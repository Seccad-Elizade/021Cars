using System.ComponentModel.DataAnnotations.Schema;

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

        /// <summary>Cədvəldə göstərilən ödəniş üsulu nişanı.</summary>
        [NotMapped]
        public string OdenisUsuluMetni =>
            IsBarter ? "🔄 Barter" : $"💵 {OdenisUsulu}".TrimEnd();

        /// <summary>
        /// Cədvəllərdə göstərilən ödəniş mətni: barter satışlarda
        /// «🔄 8 000 ₼ + 💵 2 000 ₼», digərlərində üsulun adı.
        /// </summary>
        [NotMapped]
        public string OdenisMetni =>
            IsBarter && BarterMebleg > 0m ? OdenisBolqusuMetni : OdenisUsulu;

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
