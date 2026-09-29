using System.IO;
using System.Text.Json;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 📤 <b>TRANSFER EDİLƏN ŞƏXSLƏRİN DAİMİ SAXLANMASI</b> ✓✓✓
    /// <para>
    /// Şəxs adları <c>%APPDATA%\Autocode\transfer-sexler.json</c> faylında
    /// saxlanılır ✓ — kredit əməliyyatları silinsə də adlar <b>HEÇ VAXT
    /// silinmir</b> ✓ (əvvəl yalnız bazada `TransferŞəxs` qeydi kimi
    /// saxlanılırdı ✗ və silinirdi ✗✓✓).
    /// </para>
    /// <para>
    /// Həm «💳 Kreditlər» tab-ı, həm «🏷️ Kredit Əlavə Gəlir/Xərc» tab-ı
    /// EYNİ faylı oxuyur ✓ → şəxs siyahısı (▼ üçbucaq) həmişə doludur ✓✓✓
    /// </para>
    /// </summary>
    public static class TransferSexsStore
    {
        /// <summary>Fayl yolu: %APPDATA%\Autocode\transfer-sexler.json ✓</summary>
        public static string Fayl => Path.Combine(
            Cas0201.Kok.Qovluq,
            "Autocode",
            "transfer-sexler.json");

        /// <summary>Fayldan şəxs siyahısını oxuyur ✓ (xəta olsa boş siyahı ✓).</summary>
        public static List<string> Oxu()
        {
            try
            {
                if (!File.Exists(Fayl))
                {
                    return new List<string>();
                }

                var json = File.ReadAllText(Fayl);
                var adlar = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

                return adlar
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a.Trim())
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>Siyahını fayla yazır ✓ (qovluq yoxdursa yaradılır ✓).</summary>
        public static void Yaz(IEnumerable<string> adlar)
        {
            try
            {
                var temiz = adlar
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Select(a => a.Trim())
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .OrderBy(a => a, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                var qovluq = Path.GetDirectoryName(Fayl);

                if (!string.IsNullOrWhiteSpace(qovluq))
                {
                    Directory.CreateDirectory(qovluq);
                }

                File.WriteAllText(Fayl, JsonSerializer.Serialize(temiz));
            }
            catch
            {
                // Fayl yazıla bilmədi — tətbiq işləməyə davam edir ✓
            }
        }

        /// <summary>➕ Yeni şəxs əlavə edir ✓.</summary>
        public static void Elave(string ad)
        {
            if (string.IsNullOrWhiteSpace(ad))
            {
                return;
            }

            var adlar = Oxu();

            if (!adlar.Contains(ad.Trim(), StringComparer.CurrentCultureIgnoreCase))
            {
                adlar.Add(ad.Trim());
                Yaz(adlar);
            }
        }

        /// <summary>🗑️ Şəxsi silir ✓.</summary>
        public static void Sil(string ad)
        {
            if (string.IsNullOrWhiteSpace(ad))
            {
                return;
            }

            var adlar = Oxu().Where(a => !string.Equals(a, ad.Trim(), StringComparison.CurrentCultureIgnoreCase));
            Yaz(adlar);
        }
    }
}
