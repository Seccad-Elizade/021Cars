using CommunityToolkit.Mvvm.ComponentModel;
using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.ViewModels
{
    /// <summary>
    /// WPF «Tərəfdaş mənfəət bölgüsü» cədvəlindəki TƏK sətir
    /// (Zaur / Eşqin / Asiman / Asif / Musa).
    /// <para>
    /// Dəyərlər <see cref="ObservableObject"/> sayəsində dəyişdikcə UI dərhal
    /// yenilənir — faiz yazıldıqca məbləğ yenidən hesablana bilir.
    /// </para>
    /// </summary>
    public sealed partial class PartnerPayRow : ObservableObject
    {
        /// <summary>Tərəfdaşın adı.</summary>
        [ObservableProperty] private string terefdas = string.Empty;

        /// <summary>Faiz dərəcəsi (%). Yalnız <see cref="QaligPayi"/> <c>false</c> olduqda istifadə olunur.</summary>
        [ObservableProperty] private decimal faiz;

        /// <summary>Payın məbləği (₼) — hesablanır, sonra manual düzəldilə bilər.</summary>
        [ObservableProperty] private decimal mebleg;

        /// <summary>Qalan məbləği digərləri ilə bərabər bölür? (Asif &amp; Musa)</summary>
        [ObservableProperty] private bool qaligPayi;

        /// <summary>
        /// <b>TƏTBİQ OLUNUR?</b> — adın qabağındaki checkbox.
        /// <para>
        /// İşarə qoyulmayıbsa bu tərəfdaşın <b>faizi hesablanmır</b> və pay
        /// ona verilmir (qalıq bölgüsündə də iştirak etmir).
        /// </para>
        /// </summary>
        [ObservableProperty] private bool aktiv = true;

        /// <summary>Faiz xanası düzəliş edilə bilərmi? (qalıq payçısı üçün bağlıdır)</summary>
        public bool FaizDuzelis => !QaligPayi;

        /// <summary>«Qalıq» nişanı — sətir qalıq payçısıdırsa göstərilir.</summary>
        public string QaligMetni => QaligPayi ? "qalıq" : string.Empty;

        partial void OnQaligPayiChanged(bool value)
        {
            OnPropertyChanged(nameof(FaizDuzelis));
            OnPropertyChanged(nameof(QaligMetni));
        }

        /// <summary>Modeldən (bazadan gələn <see cref="PartnerShare"/>) sətir yaradır.</summary>
        public static PartnerPayRow FromModel(PartnerShare share) => new()
        {
            Terefdas = share.Terefdas,
            Faiz = share.Faiz,
            Mebleg = share.Mebleg,
            QaligPayi = share.QaligPayi,
            Aktiv = share.Aktiv
        };

        /// <summary>Sətri saxlanıla bilən modelə çevirir.</summary>
        public PartnerShare ToModel(int sira) => new()
        {
            Terefdas = Terefdas,
            Faiz = QaligPayi ? 0m : Faiz,
            Mebleg = Aktiv ? Mebleg : 0m,
            QaligPayi = QaligPayi,
            Aktiv = Aktiv,
            Sira = sira
        };

        public override string ToString() => $"{Terefdas} — {Mebleg:N2} ₼";
    }
}
