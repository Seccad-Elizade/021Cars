using System.ComponentModel.DataAnnotations.Schema;
using CommunityToolkit.Mvvm.ComponentModel;
using EnterpriseAeroStudio.Services;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// <b>⏳ MÖHLƏTƏ VERİLMİŞ ÖDƏNİŞ</b> — «nə vaxt, nə qədər pul gələcək» ✓✓✓
    /// <para>
    /// İstifadəçinin tələbi (bax: README «Kassa &amp; Möhlət»):
    /// </para>
    /// <code>
    /// «3 min nağd ilkin ödəniş verib, deyirsə ki 10 günə 2 min nağd verəcəm»
    ///   → İlkin ödəniş  : 5 000,00 ₼
    ///   → Dərhal ödəndi : 3 000,00 ₼   (avtomatik: 5 000 − 2 000 ✓)
    ///   → Möhlət        : 2 000,00 ₼  →  10 gün sonrakı tarix ✓
    /// </code>
    /// <para>
    /// Bir mənbədə <b>BİRDƏN ÇOX</b> möhlət ola bilər ✓ (➕ düyməsi ilə əlavə edilir ✓):
    /// məs. «2 000 ₼ → 10 günə» + «1 000 ₼ → 25 günə» ✓✓✓
    /// </para>
    /// <para>
    /// ⚠ Möhlət <b>kassa axınına yalnız ÖDƏNİLDİKDƏ</b> daxil olur ✓ —
    /// ödənilməmiş möhlət «gözlənilən daxilolma»dır ✗✓✓
    /// </para>
    /// </summary>
    public sealed partial class OdenisMohlet : ObservableObject, IBuludIdli
    {
        /// <summary>Mənbə: <b>kreditin İLKİN ÖDƏNİŞİ</b> ✓ (Kreditlər tabı).</summary>
        public const string MenbeIlkinOdenis = "İlkin ödəniş";

        /// <summary>Mənbə: <b>SATIŞ</b> (nisyə / möhlətli satış) ✓ (Satış tabı).</summary>
        public const string MenbeSatis = "Satış (nisyə)";

        /// <summary>
        /// 🔑 <b>SİNXRON AÇARI</b> ✓✓✓ (v6.2.16) — bax <see cref="IBuludIdli"/>.
        /// Köhnə qeydlərdə boş ✗ → rəqəm ID işlədilir ✓
        /// </summary>
        public string? BuludId { get; set; }

        /// <summary>Bazaya yazılan unikal identifikator.</summary>
        public int Id { get; set; }

        /// <summary>Mənbə növü: <see cref="MenbeIlkinOdenis"/> və ya <see cref="MenbeSatis"/>.</summary>
        public string Menbe { get; set; } = MenbeIlkinOdenis;

        /// <summary>Aid olduğu kredit (ilkin ödəniş möhləti).</summary>
        public int? CreditId { get; set; }

        public Credit? Credit { get; set; }

        /// <summary>Aid olduğu satış (nisyə satış möhləti).</summary>
        public int? SaleId { get; set; }

        public Sale? Sale { get; set; }

        /// <summary>Pulun <b>GƏLƏCƏYİ</b> tarix (müştərinin söz verdiyi tarix).</summary>
        [ObservableProperty] private DateTime tarix = DateTime.Today;

        /// <summary>Gələcək məbləğ (₼).</summary>
        [ObservableProperty] private decimal mebleg;

        /// <summary>Gözlənilən ödəniş üsulu (Nağd · Kart / Köçürmə).</summary>
        [ObservableProperty] private string odenisUsulu = Catalog.PaymentMethods[0];

        /// <summary>Pul faktiki gəldimi? Möhlət ödənilibmi?</summary>
        [ObservableProperty] private bool odenilib;

        /// <summary>Pulun FAKTİKİ gəldiyi tarix (ödənildikdə yazılır).</summary>
        [ObservableProperty] private DateTime? odenilmeTarixi;

        /// <summary>Əlavə qeyd («telefonla razılaşdıq» və s.).</summary>
        [ObservableProperty] private string qeyd = string.Empty;

        /// <summary>Cədvəldə göstərilmə sırası.</summary>
        public int Sira { get; set; }

        // ====================================================================
        //  📊 HESABLANAN GÖSTƏRİCİLƏR (UI üçün)
        // ====================================================================

        /// <summary>Tarix mətni: «21.10.2026».</summary>
        [NotMapped]
        public string TarixMetni => Tarix.ToString("dd.MM.yyyy");

        /// <summary>Məbləğ mətni: «2 000,00 ₼».</summary>
        [NotMapped]
        public string MeblegMetni => $"{Mebleg:N2} ₼";

        /// <summary>Ödənilmə tarixi mətni (varsa).</summary>
        [NotMapped]
        public string OdenilmeTarixiMetni =>
            OdenilmeTarixi is DateTime d ? d.ToString("dd.MM.yyyy") : "—";

        /// <summary>Möhlətin vaxtı keçib, amma pul gəlməyib? (⚠ qırmızı sətir)</summary>
        [NotMapped]
        public bool Gecikmis => !Odenilib && Tarix.Date < DateTime.Today;

        /// <summary>Qalan gün sayı (mənfi = gecikib).</summary>
        [NotMapped]
        public int QalanGun => (int)(Tarix.Date - DateTime.Today).TotalDays;

        /// <summary>Qalan gün mətni: «3 günə» · «bu gün» · «5 gün gecikib».</summary>
        [NotMapped]
        public string QalanGunMetni
        {
            get
            {
                if (Odenilib)
                {
                    return $"ödənilib {OdenilmeTarixiMetni}";
                }

                var gun = QalanGun;

                return gun switch
                {
                    0 => "bu gün",
                    > 0 => $"{gun} günə",
                    _ => $"⚠ {-gun} gün gecikib"
                };
            }
        }

        /// <summary>Vəziyyət: «✅ Ödənilib» · «⚠ Gecikmiş» · «🕓 Gözlənilir».</summary>
        [NotMapped]
        public string Veziyyet => Odenilib
            ? "✅ Ödənilib"
            : Gecikmis ? "⚠ Gecikmiş" : "🕓 Gözlənilir";

        /// <summary>Vəziyyət rəngi (hex).</summary>
        [NotMapped]
        public string VeziyyetRengi => Odenilib
            ? "#34D399"
            : Gecikmis ? "#FB7185" : "#FBBF24";

        /// <summary>Mənbə mətni: «🏦 İlkin ödəniş — M-0007 | Rebbil | 77HH717».</summary>
        [NotMapped]
        public string MenbeMetni
        {
            get
            {
                if (Credit is not null)
                {
                    return $"🏦 İlkin ödəniş — {Credit.DisplayText}";
                }

                if (Sale is not null)
                {
                    var car = Sale.Car?.DisplayName;
                    return string.IsNullOrWhiteSpace(car)
                        ? $"💰 Nisyə satış — {Sale.Mustəri}"
                        : $"💰 Nisyə satış — {Sale.Mustəri} · {car}";
                }

                return Menbe == MenbeSatis ? "💰 Nisyə satış" : "🏦 İlkin ödəniş";
            }
        }

        /// <summary>Cədvəl üçün qısa təsvir: «21.10.2026 · 2 000,00 ₼ · Nağd — 10 günə».</summary>
        [NotMapped]
        public string Xulase =>
            $"{TarixMetni} · {MeblegMetni} · {OdenisUsulu} — {QalanGunMetni}";

        public override string ToString() => Xulase;

        // ---- Dəyişiklik → hesablanan göstəricilər də yenilənir ✓ ----
        partial void OnTarixChanged(DateTime value) => RaiseHesab();

        partial void OnMeblegChanged(decimal value) => RaiseHesab();

        partial void OnOdenilibChanged(bool value) => RaiseHesab();

        partial void OnOdenilmeTarixiChanged(DateTime? value) => RaiseHesab();

        private void RaiseHesab()
        {
            OnPropertyChanged(nameof(TarixMetni));
            OnPropertyChanged(nameof(MeblegMetni));
            OnPropertyChanged(nameof(OdenilmeTarixiMetni));
            OnPropertyChanged(nameof(Gecikmis));
            OnPropertyChanged(nameof(QalanGun));
            OnPropertyChanged(nameof(QalanGunMetni));
            OnPropertyChanged(nameof(Veziyyet));
            OnPropertyChanged(nameof(VeziyyetRengi));
            OnPropertyChanged(nameof(Xulase));
        }
    }
}
