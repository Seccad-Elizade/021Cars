using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>🚗 İlkin ödənişin tərkibi ✓ (nağd + barter maşın ✓)</summary>
    public sealed class IlkinBarterKaydi
    {
        public decimal Nagd { get; set; }
        public decimal Barter { get; set; }
        public string Qeyd { get; set; } = string.Empty;
        public string Tarix { get; set; } = string.Empty;
    }

    /// <summary>
    /// 🚗 <b>İLKİN ÖDƏNİŞ — BARTER (MAŞINLA) QEYDİ</b> ✓✓✓ (v6.2.92)
    /// <para>
    /// ★ İSTİFADƏÇİ TƏLƏBİ: «Kreditlər tabında İlkin ödənişin yanına <b>barter
    /// checkbox</b> qoy — məsələn adam 3 000 ₼ nağd ilkin ödəniş verir ✓ və
    /// 10 000 ₼ dəyərində maşın verir ✓ — yanında da qeyd yazmağa yer olsun ✓»
    /// </para>
    /// <para>
    /// 📌 Baza sxemi DƏYİŞMİR ✗ — məlumat ayrıca JSON faylında saxlanılır ✓:
    /// <c>%LOCALAPPDATA%\021Cars\ilkin_barter.json</c>
    /// </para>
    /// <para>
    /// 🔢 <c>IlkinOdenis</c> (bazada) = <b>nağd + barter</b> ✓ → bütün mövcud
    /// hesablamalar (kök ✓ qrafik ✓ bölgü ✓) olduğu kimi işləyir ✓✓✓
    /// </para>
    /// </summary>
    public static class IlkinBarterStore
    {
        private static readonly object _kilid = new();

        private static string Fayl => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "021Cars", "ilkin_barter.json");

        private static Dictionary<string, IlkinBarterKaydi> Hamisi()
        {
            try
            {
                if (!File.Exists(Fayl))
                {
                    return new Dictionary<string, IlkinBarterKaydi>(StringComparer.OrdinalIgnoreCase);
                }

                var yüklənən = JsonSerializer.Deserialize<Dictionary<string, IlkinBarterKaydi>>(
                    File.ReadAllText(Fayl));

                return yüklənən is null
                    ? new Dictionary<string, IlkinBarterKaydi>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, IlkinBarterKaydi>(yüklənən, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, IlkinBarterKaydi>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>📖 Kredit üzrə barter qeydini oxuyur ✓ (yoxdursa <c>null</c> ✗)</summary>
        public static IlkinBarterKaydi? Oxu(int creditId)
        {
            lock (_kilid)
            {
                return creditId <= 0
                    ? null
                    : Hamisi().TryGetValue(creditId.ToString(), out var k) ? k : null;
            }
        }

        /// <summary>💾 Barter qeydini yazır ✓ (barter = 0 → QEYD SİLİNİR ✗)</summary>
        public static void Yaz(int creditId, decimal nagd, decimal barter, string? qeyd)
        {
            if (creditId <= 0)
            {
                return;
            }

            lock (_kilid)
            {
                try
                {
                    var hamısı = Hamisi();
                    var açar = creditId.ToString();

                    if (barter <= 0m && string.IsNullOrWhiteSpace(qeyd))
                    {
                        hamısı.Remove(açar);
                    }
                    else
                    {
                        hamısı[açar] = new IlkinBarterKaydi
                        {
                            Nagd = Math.Round(nagd, 2),
                            Barter = Math.Round(barter, 2),
                            Qeyd = qeyd ?? string.Empty,
                            Tarix = DateTime.Now.ToString("yyyy-MM-dd")
                        };
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(Fayl)!);
                    File.WriteAllText(
                        Fayl,
                        JsonSerializer.Serialize(hamısı, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch
                {
                    // 🛡️
                }
            }
        }

        /// <summary>📋 Bütün barter qeydləri (hesabat / USB köçürmə üçün ✓)</summary>
        public static IReadOnlyList<IlkinBarterKaydi> HamisiSiyahi()
            => Hamisi().Values.ToList();
    }
}
