namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🔒 <b>«BİZ TƏRƏFDƏN BAĞLAMA» NƏTİCƏSİ</b> ✓✓✓
    /// <para>
    /// Müştəri kredit ödənişlərini davam etdirə bilmədikdə pulu BİZ veririk →
    /// maşın parka qayıdır ✓ (yeni sıra nömrəsi · maya = ödədiyimiz məbləğ ✓)
    /// </para>
    /// </summary>
    public sealed class BizTerefdenBaglamaNeticesi
    {
        /// <summary>Əməliyyat uğurludurmu?</summary>
        public bool Ugurlu { get; init; }

        /// <summary>İstifadəçiyə göstəriləcək mesaj ✓.</summary>
        public string Mesaj { get; init; } = string.Empty;

        /// <summary>Bizim ödədiyimiz məbləğ (₼) — yeni maşının MAYASI ✓.</summary>
        public decimal OdenilenMebleg { get; init; }

        /// <summary>Parka qaytarılan YENİ avtomobilin Id-si ✓.</summary>
        public int YeniCarId { get; init; }

        /// <summary>Yeni avtomobilin SIRA NÖMRƏSİ ✓.</summary>
        public int YeniSiraNomresi { get; init; }

        /// <summary>Yeni avtomobilin adı (Marka + nömrə ✓).</summary>
        public string YeniCarAdi { get; init; } = string.Empty;

        public static BizTerefdenBaglamaNeticesi Xeta(string mesaj)
            => new() { Ugurlu = false, Mesaj = mesaj };
    }
}
