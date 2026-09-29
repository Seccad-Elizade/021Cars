namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// İstifadəçinin əlavə etdiyi xərc qrupu / kateqoriyası.
    /// Boş <see cref="Kategoriya"/> = yalnız qrup (kateqoriyası olmayan qrup).
    /// </summary>
    public class ExpenseCatalogEntry
    {
        public int Id { get; set; }

        /// <summary>Təyinat: "Avtomobil Xərci" və ya "Ofis / İnzibati Xərc".</summary>
        public string Teyinat { get; set; } = string.Empty;

        /// <summary>Xərc qrupu.</summary>
        public string Qrup { get; set; } = string.Empty;

        /// <summary>Kateqoriya / xərc adı (boş ola bilər).</summary>
        public string Kategoriya { get; set; } = string.Empty;

        /// <summary>Göstərilmə sırası.</summary>
        public int Sira { get; set; }
    }
}
