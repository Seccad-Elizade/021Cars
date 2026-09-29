namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// «SÜRƏTLİ KATEQORİYA AXTARIŞI» üçün təklif sətri.
    /// <para>
    /// İstifadəçi kateqoriya / xərc adını yazmağa başlayanda BÜTÜN qrupların
    /// kateqoriyaları arasından uyğun olanlar göstərilir. Birini seçəndə
    /// <b>həm qrup</b>, <b>həm kateqoriya</b> avtomatik dolur — qrupu ayrıca
    /// axtarmağa ehtiyac qalmır.
    /// </para>
    /// </summary>
    public sealed class CategorySuggestion
    {
        /// <summary>Təyinat («Avtomobil Xərci» / «Ofis / İnzibati Xərc»).</summary>
        public string Teyinat { get; set; } = string.Empty;

        /// <summary>Qrup adı.</summary>
        public string Qrup { get; set; } = string.Empty;

        /// <summary>Kateqoriya / xərc adı.</summary>
        public string Kategoriya { get; set; } = string.Empty;

        /// <summary>Siyahıda göstərilən mətn: «Dəmirçi — 🛠️ Kuzov, Dəmirçi &amp; Malyar».</summary>
        public string Display => $"{Kategoriya}  —  {Qrup}";

        /// <summary>Axtarış açarı (kateqoriya + qrup + təyinat bir yerdə).</summary>
        public string SearchKey => $"{Kategoriya} {Qrup} {Teyinat}";

        public override string ToString() => Display;
    }
}
