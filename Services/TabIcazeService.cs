using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 📑 <b>TAB İCAZƏ SƏTRİ</b> ✓✓✓ (v6.2.86)
    /// <para>Admin «Tənzimləmələr» bölməsində hər istifadəçi üçün tabları işarələyir ✓</para>
    /// </summary>
    public sealed partial class TabIcazeSatiri : ObservableObject
    {
        public TabIcazeSatiri(string key, string ad, bool gorunur)
        {
            Key = key;
            Ad = ad;
            this.gorunur = gorunur;
        }

        /// <summary>🔑 Sabit açar (tab adı ilə eynidir ✓)</summary>
        public string Key { get; }

        /// <summary>🏷️ Ekranda görünən ad ✓</summary>
        public string Ad { get; }

        /// <summary>☑ Bu istifadəçi bu tabı görsün?</summary>
        [ObservableProperty] private bool gorunur;
    }

    /// <summary>
    /// 📑 <b>İSTİFADƏÇİ ÜZRƏ TAB İCAZƏLƏRİ</b> ✓✓✓ (v6.2.86)
    /// <para>
    /// ★ İstifadəçi tələbi: «Admin üçün tənzimləmələr bölməsinə əlavə et —
    /// hansı tabları hansı istifadəçilər görsün / görməsin (checkbox)»
    /// </para>
    /// <para>
    /// 💾 Saxlanılır: <c>%LOCALAPPDATA%\021Cars\tab_icazeleri.json</c> ✓
    /// (istifadəçi adı → icazəli tab açarları ✓)
    /// </para>
    /// <para>
    /// 🛡️ <b>Default:</b> qeyd yoxdursa → <b>BÜTÜN tablar görünür</b> ✓
    /// (köhnə davranış pozulmur ✗✓✓)
    /// </para>
    /// </summary>
    public static class TabIcazeService
    {
        private static readonly object _kilid = new();
        private static Dictionary<string, List<string>>? _kesh;

        /// <summary>📂 JSON faylının yolu ✓</summary>
        public static string Fayl
        {
            get
            {
                var qovluq = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "021Cars");

                try { Directory.CreateDirectory(qovluq); } catch { }

                return Path.Combine(qovluq, "tab_icazeleri.json");
            }
        }

        /// <summary>
        /// 📑 <b>İDARƏ OLUNA BİLƏN TABLAR</b> ✓✓✓
        /// <para>⚠ Sıra <c>MainViewModel</c> tab sırası ilə EYNİDİR ✓</para>
        /// <para>ℹ️ Gizli (Collapsed) tablar siyahıya daxil deyil ✗</para>
        /// </summary>
        public static IReadOnlyList<string> ButunTablar { get; } = new[]
        {
            "🚘 Avto Park & Arxiv",
            "💳 Ümumi Xərclər",
            "🧮 Kredit Kalkulyatoru",
            "💰 Satış",
            "💳 Kreditlər",
            "📅 Kredit Ödəniş Qrafiki",
            "🏷️ Kredit Əlavə Gəlir/Xərc",
            "📊 Maliyyə Paneli",
            "💵 Kassa",
            "⏳ Möhlət",
            "🔔 Bildirişlər",
            "🗓️ Ödəniş Təqvimi",
            "🗄️ Satılan & Krediti Bitmiş",
            "👥 Tərəfdaşlar",
            "🌐 Veb Sayt",
            "⚙️ Tənzimləmələr",
            "📜 Skript İdxalı",
            "🏦 Kredit İdxalı"
        };

        /// <summary>🛡️ HEÇ VAXT gizlədilə bilməyən tablar ✓ (admin özünü kilidləməsin ✗)</summary>
        public static IReadOnlyList<string> MecburiTablar { get; } = new[]
        {
            "⚙️ Tənzimləmələr"
        };

        /// <summary>📂 Bütün icazələri oxuyur ✓ (keşlə ✓)</summary>
        private static Dictionary<string, List<string>> Hamisi()
        {
            lock (_kilid)
            {
                if (_kesh is not null)
                {
                    return _kesh;
                }

                try
                {
                    if (File.Exists(Fayl))
                    {
                        var json = File.ReadAllText(Fayl);

                        var yüklənən = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);

                        _kesh = yüklənən is null
                            ? new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                            : new Dictionary<string, List<string>>(yüklənən, StringComparer.OrdinalIgnoreCase);

                        return _kesh;
                    }
                }
                catch
                {
                    // 🛡️ fayl zədəlidirsə — default (hamısı görünür) ✓
                }

                _kesh = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                return _kesh;
            }
        }

        /// <summary>
        /// 📑 İstifadəçinin GÖRƏ BİLDİYİ tablar ✓
        /// <para>⚠ Qeyd yoxdursa → <c>null</c> qaytarır ✓ (= hamısı görünür ✓)</para>
        /// </summary>
        public static IReadOnlyList<string>? Gorunenler(string? istifadeciAdi)
        {
            if (string.IsNullOrWhiteSpace(istifadeciAdi))
            {
                return null;
            }

            var hamisi = Hamisi();

            return hamisi.TryGetValue(istifadeciAdi.Trim(), out var siyahi) && siyahi.Count > 0
                ? siyahi
                : null;
        }

        /// <summary>✅ İstifadəçi bu tabı görə bilərmi? ✓</summary>
        public static bool Gorunur(string? istifadeciAdi, string tab)
        {
            // 🛡️ məcburi tablar həmişə görünür ✓
            if (MecburiTablar.Contains(tab, StringComparer.Ordinal))
            {
                return true;
            }

            var icaze = Gorunenler(istifadeciAdi);

            return icaze is null || icaze.Contains(tab, StringComparer.Ordinal);
        }

        /// <summary>💾 İstifadəçinin icazələrini yazır ✓</summary>
        public static void Saxla(string istifadeciAdi, IEnumerable<string> tablar)
        {
            if (string.IsNullOrWhiteSpace(istifadeciAdi))
            {
                return;
            }

            var hamisi = Hamisi();

            var siyahi = tablar
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            // 🛡️ məcburi tablar HƏMİŞƏ daxildir ✓
            foreach (var mecburi in MecburiTablar)
            {
                if (!siyahi.Contains(mecburi, StringComparer.Ordinal))
                {
                    siyahi.Add(mecburi);
                }
            }

            hamisi[istifadeciAdi.Trim()] = siyahi;

            YaddaSaxla();
        }

        /// <summary>♻️ İstifadəçinin fərdi icazələrini SİLİR ✓ (= hamısı görünür ✓)</summary>
        public static void Sifirla(string istifadeciAdi)
        {
            if (string.IsNullOrWhiteSpace(istifadeciAdi))
            {
                return;
            }

            Hamisi().Remove(istifadeciAdi.Trim());

            YaddaSaxla();
        }

        /// <summary>💾 Keşi fayla yazır ✓</summary>
        private static void YaddaSaxla()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    Hamisi(),
                    new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(Fayl, json);
            }
            catch
            {
                // 🛡️ yazıla bilmirsə — yaddaşda qalır ✓ (proqram işləyir ✓)
            }
        }

        /// <summary>🔄 Keşi sıfırlayır ✓</summary>
        public static void KeshiTemizle()
        {
            lock (_kilid)
            {
                _kesh = null;
            }
        }
    }
}
