namespace EnterpriseAeroStudio.Models
{
    using System.ComponentModel.DataAnnotations.Schema;

    /// <summary>
    /// Şirkətin <b>TƏRƏFDAŞI</b> (bazada saxlanılır — əlavə/silmə mümkündür).
    /// <para>
    /// Əvvəllər tərəfdaşlar kodda sabit idi (<see cref="Services.Catalog.DefaultPartners"/>).
    /// Artıq istifadəçi «👥 Tərəfdaşlar» tabından <b>yeni şəxs əlavə edə</b>,
    /// <b>şəxsi silə</b> və hər birinin <b>standart faiz dərəcəsini</b> təyin edə bilər.
    /// </para>
    /// <para>
    /// <c>QaligPayi = true</c> olanlar (Asif, Musa) faiz almır — onlar faiz
    /// payları çıxıldıqdan sonra QALAN məbləği öz aralarında yarı-yarıya bölürlər.
    /// </para>
    /// </summary>
    public class Partner : IBuludIdli
    {
        public int Id { get; set; }

        /// <summary>🔑 Qlobal unikal bulud açarı (GUID ✓ v6.2.16). Köhnə qeydlərdə boş ✗ → rəqəm ID işlədilir ✓</summary>
        public string? BuludId { get; set; }

        /// <summary>Tərəfdaşın adı (Zaur, Eşqin, Asiman, Asif, Musa...).</summary>
        public string Ad { get; set; } = string.Empty;

        /// <summary>Standart faiz dərəcəsi (%). Qalıq payçıları üçün 0.</summary>
        public decimal Faiz { get; set; }

        /// <summary>Qalan məbləği digər qalıq payçıları ilə bərabər bölürmü?</summary>
        public bool QaligPayi { get; set; }

        /// <summary>Bölgülərdə iştirak edirmi? İşarə qoyulmayıbsa faizi hesablanmır.</summary>
        public bool Aktiv { get; set; } = true;

        /// <summary>Cədvəldə göstərilmə sırası.</summary>
        public int Sira { get; set; }

        /// <summary>Əlavə qeyd (telefon, vəzifə və s.).</summary>
        public string Qeyd { get; set; } = string.Empty;

        /// <summary>Cədvəl üçün faiz mətni: «%6» və ya «qalıq».</summary>
        [NotMapped]
        public string FaizMetni => QaligPayi ? "qalıq" : $"{Faiz:0.##}%";

        /// <summary>Cədvəl üçün tam təsvir: «Zaur — %6».</summary>
        [NotMapped]
        public string Display => $"{Ad} — {FaizMetni}";

        /// <summary>ComboBox kimi yerlərdə sinif adı yerinə düzgün mətn göstərilsin.</summary>
        public override string ToString() => Display;
    }
}
