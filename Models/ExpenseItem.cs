using System.ComponentModel.DataAnnotations.Schema;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// Bir xərc qeydini (avtomobil və ya ofis xərci) təmsil edir.
    /// </summary>
    public class ExpenseItem : IBuludIdli
    {
        public int Id { get; set; }

        /// <summary>🔑 Qlobal unikal bulud açarı (GUID ✓ v6.2.16). Köhnə qeydlərdə boş ✗ → rəqəm ID işlədilir ✓</summary>
        public string? BuludId { get; set; }

        /// <summary>Xərcin tarixi.</summary>
        public DateTime Tarix { get; set; } = DateTime.Today;

        /// <summary>Təyinat: "Avtomobil Xərci" və ya "Ofis / İnzibati Xərc".</summary>
        public string Teyinat { get; set; } = string.Empty;

        /// <summary>Xərc qrupu (məs. "💰 Alış & Maya Xərcləri").</summary>
        public string Qrup { get; set; } = string.Empty;

        /// <summary>Kateqoriya / xərc adı.</summary>
        public string Kategoriya { get; set; } = string.Empty;

        /// <summary>Bağlı olduğu avtomobil (ofis xərclərində null).</summary>
        public int? CarId { get; set; }

        /// <summary>Naviqasiya xassəsi.</summary>
        public CarItem? Car { get; set; }

        /// <summary>Məbləğ (AZN).</summary>
        public decimal Mebleg { get; set; }

        /// <summary>Ödəniş üsulu (Nağd, Kart / Köçürmə).</summary>
        public string OdenisUsulu { get; set; } = string.Empty;

        /// <summary>Əlavə qeyd.</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Yaradılma zamanı (audit).</summary>
        public DateTime YaradilmaTarixi { get; set; } = DateTime.Now;

        /// <summary>
        /// 🚀 <b>SQL-TƏRƏFLİ AXTARIŞ MƏTNİ</b> ✓✓✓ (avtomatik doldurulur ✓)
        /// <para>
        /// <see cref="Services.MetinAxtaris.Birlestir"/> ilə hazırlanır:
        /// kiçik hərf + Azərbaycan hərfləri ASCII-yə çevrilmiş ✓
        /// (məs. «Şüşə dəyişmə» → «suse deyisme» ✓)
        /// </para>
        /// <para>
        /// ⚠ Bu sahəni <b>əl ilə yazmaq lazım DEYİL</b> ✗ —
        /// <c>AppDbContext.SaveChanges</c> hər əlavə/düzəlişdə avtomatik yeniləyir ✓
        /// </para>
        /// </summary>
        public string Axtaris { get; set; } = string.Empty;

        /// <summary>Grid-də göstərilən avtomobil məlumatı.</summary>
        [NotMapped]
        public string CarInfo => Car?.DisplayName ?? string.Empty;
    }
}
