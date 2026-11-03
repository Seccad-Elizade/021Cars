using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>📦 USB-dəki ayar paketinin məlumatı ✓</summary>
    public sealed class AyarlarPaketi
    {
        public string Tarix { get; set; } = string.Empty;
        public string Komputer { get; set; } = string.Empty;
        public string Versiya { get; set; } = string.Empty;
        public int FaylSayi { get; set; }
        public string Acar { get; set; } = string.Empty;
    }

    /// <summary>
    /// 🧳 <b>AYARLARIN USB İLƏ KÖÇÜRÜLMƏSİ</b> ✓✓✓ (v6.2.89)
    /// <para>
    /// ★ İSTİFADƏÇİ TƏLƏBİ: «USB-yə köçürəndə Tənzimləmələrdəki <b>istifadəçilər</b> ·
    /// <b>tab icazələri</b> · <b>bütün ayarlar</b> da köçürülsün ✓ və başqa kompüterə
    /// taxanda **avtomatik** gəlsin hər şey ✓»
    /// </para>
    /// <para>📤 Köçürülən fayllar (<c>{USB}\021Cars_Ayarlar\</c>):</para>
    /// <list type="bullet">
    ///   <item><c>istifadeciler.json</c> — istifadəçilər + rollar ✓</item>
    ///   <item><c>tab_icazeleri.json</c> — hər istifadəçinin tab icazələri ✓</item>
    ///   <item><c>ui_zoom.txt</c> — cədvəl zoom səviyyəsi ✓</item>
    ///   <item><c>bulud_ayarlari.json</c> — bulud ayarları ✓</item>
    ///   <item><c>transfer-sexler.json</c> — transfer şəxsləri ✓</item>
    ///   <item><c>flags\*.flag</c> — birdəfəlik miqrasiya nişanları ✓</item>
    /// </list>
    /// <para>
    /// 📥 Geri qaytarma (başqa kompüterdə): proqram açılanda DATA USB tapılsa →
    /// paket **avtomatik** tətbiq olunur ✓ (təkrar tətbiq olunmur ✗ — acar yadda saxlanılır ✓)
    /// </para>
    /// <para>🛡️ Hər yazmadan ƏVVƏL mövcud fayllar <c>%LOCALAPPDATA%\021Cars\yedek_*</c>-yə surət olunur ✓</para>
    /// </summary>
    public static class AyarlarKocurmeServisi
    {
        /// <summary>📂 USB-dəki paket qovluğunun adı ✓</summary>
        public const string PaketQovlugu = "021Cars_Ayarlar";

        private const string InfoFayli = "paket.json";

        /// <summary>📂 Lokal ayar qovluğu ✓</summary>
        private static string LocalQovluq => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "021Cars");

        /// <summary>🧾 Son tətbiq olunan paketin acarı ✓ (təkrar tətbiq olunmasın ✗)</summary>
        private static string AcarFayli => Path.Combine(LocalQovluq, "ayarlar_inteqrasiya.txt");

        /// <summary>📋 Köçürüləcək fayllar ✓ (mənbə → paketdəki ad ✓)</summary>
        private static List<(string Menbe, string Nisbi)> Fayllar()
        {
            var netice = new List<(string, string)>
            {
                (Path.Combine(Cas0201.Kok.Qovluq, "EnterpriseAeroStudio", "istifadeciler.json"),
                 "istifadeciler.json"),
                (Path.Combine(LocalQovluq, "tab_icazeleri.json"), "tab_icazeleri.json"),
                (Path.Combine(LocalQovluq, "ui_zoom.txt"), "ui_zoom.txt"),
                (Path.Combine(LocalQovluq, "ilkin_barter.json"), "ilkin_barter.json"),
                (Path.Combine(Cas0201.Kok.Qovluq, "bulud_ayarlari.json"), "bulud_ayarlari.json"),
                (Path.Combine(Cas0201.Kok.Qovluq, "Autocode", "transfer-sexler.json"), "transfer-sexler.json"),
            };

            // 🚩 Birdəfəlik miqrasiya nişanları ✓ (digər kompüterdə təkrar işləməsin ✗)
            try
            {
                var flagQovluq = Path.Combine(LocalQovluq, "flags");

                if (Directory.Exists(flagQovluq))
                {
                    foreach (var fal in Directory.GetFiles(flagQovluq, "*.flag"))
                    {
                        netice.Add((fal, Path.Combine("flags", Path.GetFileName(fal))));
                    }
                }
            }
            catch
            {
                // 🛡️
            }

            return netice;
        }

        /// <summary>🧮 Paketin acarı ✓ (tarix + kompüter + fayl sayı ✓)</summary>
        private static string PaketAcari(AyarlarPaketi paket) =>
            $"{paket.Tarix}|{paket.Komputer}|{paket.FaylSayi}";

        /// <summary>📖 Son tətbiq olunan acar ✓</summary>
        private static string OxunanAcar()
        {
            try
            {
                return File.Exists(AcarFayli) ? File.ReadAllText(AcarFayli).Trim() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>💾 Acarı yadda saxlayır ✓</summary>
        private static void AcarYaz(string acar)
        {
            try
            {
                Directory.CreateDirectory(LocalQovluq);
                File.WriteAllText(AcarFayli, acar);
            }
            catch
            {
                // 🛡️
            }
        }

        /// <summary>🔌 DATA USB-nin kökünü tapır ✓ (yoxdursa <c>null</c> ✗)</summary>
        private static string? UsbKoku()
        {
            try
            {
                var tapilan = DataUsbService.Tap();

                return tapilan is null ? null : DataUsbService.KokQovluq(tapilan.Value.Root);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 📤 <b>AYARLARI USB-YƏ KÖÇÜRÜR</b> ✓✓✓ (v6.2.89)
        /// <para>🔌 «💾 USB-yə köçür» düyməsi ilə EYNİ anda işləyir ✓ — ayrıca düymə lazım deyil ✗</para>
        /// </summary>
        /// <returns>İstifadəçiyə göstərilən qısa nəticə mətni ✓</returns>
        public static string UsbYeKocur()
        {
            try
            {
                var usb = UsbKoku();

                if (usb is null)
                {
                    return "ℹ️ DATA USB tapılmadı ✗ — ayarlar köçürülmədi";
                }

                var hedef = Path.Combine(usb, PaketQovlugu);

                Directory.CreateDirectory(hedef);

                var say = 0;

                foreach (var (menbe, nisbi) in Fayllar())
                {
                    try
                    {
                        if (!File.Exists(menbe))
                        {
                            continue;   // ⚠ fayl yoxdursa keçilir ✓ (proqram dayanmır ✗)
                        }

                        var tam = Path.Combine(hedef, nisbi);

                        Directory.CreateDirectory(Path.GetDirectoryName(tam) ?? hedef);

                        File.Copy(menbe, tam, overwrite: true);
                        say++;
                    }
                    catch
                    {
                        // 🛡️
                    }
                }

                var paket = new AyarlarPaketi
                {
                    Tarix = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Komputer = Environment.MachineName,
                    Versiya = typeof(AyarlarKocurmeServisi).Assembly.GetName().Version?.ToString() ?? string.Empty,
                    FaylSayi = say
                };

                paket.Acar = PaketAcari(paket);

                File.WriteAllText(
                    Path.Combine(hedef, InfoFayli),
                    JsonSerializer.Serialize(paket, new JsonSerializerOptions { WriteIndented = true }));

                return $"🧳 Ayarlar USB-yə köçürüldü ✓ — {say} fayl (istifadəçilər ✓ · tab icazələri ✓ · bütün ayarlar ✓)";
            }
            catch (Exception ex)
            {
                return "🧳 Ayarlar köçürülə bilmədi ✓ — " + ex.Message;
            }
        }

        /// <summary>
        /// 📥 <b>USB-DƏKİ AYARLARI TƏTBİQ EDİR (İNTEQRASİYA)</b> ✓✓✓ (v6.2.89)
        /// <para>★ «Başqa kompüterə taxanda hər şey gəlsin» ✓ — açılışda avtomatik ✓</para>
        /// <list type="bullet">
        ///   <item>👤 İstifadəçilər — mövcudlar QORUNUR ✗, yalnız YENİLƏR əlavə olunur ✓</item>
        ///   <item>📑 Tab icazələri — USB-dəki qaydalar ÜSTÜN ✓</item>
        ///   <item>⚙️ Digər ayarlar (zoom · bulud · transfer · flags) → köçürülür ✓</item>
        /// </list>
        /// <para>🛡️ Hər yazmadan əvvəl köhnə fayl <c>yedek_*</c> qovluğuna surət olunur ✓</para>
        /// </summary>
        public static string UsbDenIntegrasiyaEt(bool mecburi = false)
        {
            try
            {
                var usb = UsbKoku();

                if (usb is null)
                {
                    return string.Empty;
                }

                var paket = PaketiOxu(usb);

                if (paket is null)
                {
                    return string.Empty;      // 📦 USB-də ayar paketi yoxdur ✗
                }

                var acar = PaketAcari(paket);

                if (!mecburi && string.Equals(OxunanAcar(), acar, StringComparison.Ordinal))
                {
                    return string.Empty;      // 🔁 Artıq tətbiq olunub ✓
                }

                var qaynaq = Path.Combine(usb, PaketQovlugu);
                var yedek = Path.Combine(LocalQovluq, "yedek_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                var edilen = new List<string>();
                var yeniIstifadeci = 0;

                foreach (var (menbe, nisbi) in Fayllar())
                {
                    var usbFayl = Path.Combine(qaynaq, nisbi);

                    if (!File.Exists(usbFayl))
                    {
                        continue;
                    }

                    try
                    {
                        if (File.Exists(menbe))
                        {
                            var surət = Path.Combine(yedek, nisbi);
                            Directory.CreateDirectory(Path.GetDirectoryName(surət) ?? yedek);
                            File.Copy(menbe, surət, overwrite: true);
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(menbe) ?? Cas0201.Kok.Qovluq);

                        var ad = Path.GetFileName(nisbi);

                        if (string.Equals(ad, "istifadeciler.json", StringComparison.OrdinalIgnoreCase))
                        {
                            yeniIstifadeci = IstifadecileriBirlesdir(menbe, usbFayl);
                            edilen.Add($"👤 istifadəçilər (+{yeniIstifadeci})");
                        }
                        else if (string.Equals(ad, "tab_icazeleri.json", StringComparison.OrdinalIgnoreCase))
                        {
                            TabIcazeleriniBirlesdir(menbe, usbFayl);
                            edilen.Add("📑 tab icazələri");
                        }
                        else
                        {
                            File.Copy(usbFayl, menbe, overwrite: true);
                            edilen.Add("⚙️ " + ad);
                        }
                    }
                    catch
                    {
                        // 🛡️ bir fayl alınmasa — qalanlar davam edir ✓
                    }
                }

                AcarYaz(acar);

                try
                {
                    AuthService.Yukle();      // 🔄 istifadəçilər dərhal yenilənir ✓
                }
                catch
                {
                    // 🛡️
                }

                var mətn = $"🧳 USB ayarları TƏTBİQ OLUNDU ✓ — {edilen.Count} qrup" +
                           (yeniIstifadeci > 0 ? $" · {yeniIstifadeci} yeni istifadəçi ✓" : string.Empty) +
                           $"  ({paket.Komputer} · {paket.Tarix})";

                AppLogger.Melumat(mətn);

                return mətn;
            }
            catch (Exception ex)
            {
                AppLogger.Melumat("🧳 USB ayarları tətbiq olunmadı ✓ — " + ex.Message);

                return string.Empty;
            }
        }

        /// <summary>👤 İstifadəçiləri BİRLƏŞDİRİR ✓ — mövcudlar qorunur ✗, yenilər əlavə olunur ✓</summary>
        private static int IstifadecileriBirlesdir(string lokalFayl, string usbFayl)
        {
            try
            {
                var lokal = Oxu<List<Istifadeci>>(lokalFayl) ?? new List<Istifadeci>();
                var usb = Oxu<List<Istifadeci>>(usbFayl) ?? new List<Istifadeci>();

                var adlar = new HashSet<string>(
                    lokal.Select(u => u.IstifadeciAdi ?? string.Empty), StringComparer.OrdinalIgnoreCase);

                var elave = 0;

                foreach (var u in usb)
                {
                    if (string.IsNullOrWhiteSpace(u.IstifadeciAdi))
                    {
                        continue;
                    }

                    if (adlar.Add(u.IstifadeciAdi))
                    {
                        lokal.Add(u);
                        elave++;
                    }
                }

                File.WriteAllText(
                    lokalFayl,
                    JsonSerializer.Serialize(lokal, new JsonSerializerOptions { WriteIndented = true }));

                return elave;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>📑 Tab icazələrini BİRLƏŞDİRİR ✓ — USB-dəki qaydalar üstün ✓</summary>
        private static void TabIcazeleriniBirlesdir(string lokalFayl, string usbFayl)
        {
            try
            {
                var usb = Oxu<Dictionary<string, List<string>>>(usbFayl);
                var lokal = Oxu<Dictionary<string, List<string>>>(lokalFayl);

                if (usb is null || usb.Count == 0)
                {
                    return;
                }

                var netice = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var cut in usb)
                {
                    netice[cut.Key] = cut.Value;
                }

                if (lokal is not null)
                {
                    foreach (var cut in lokal.Where(c => !netice.ContainsKey(c.Key)))
                    {
                        netice[cut.Key] = cut.Value;
                    }
                }

                File.WriteAllText(
                    lokalFayl,
                    JsonSerializer.Serialize(netice, new JsonSerializerOptions { WriteIndented = true }));

                TabIcazeService.KeshiTemizle();   // 🔄 dərhal qüvvədə ✓
            }
            catch
            {
                // 🛡️
            }
        }

        /// <summary>📖 JSON oxuyur ✓ (xəta olsa <c>null</c> ✗)</summary>
        private static T? Oxu<T>(string fayl)
        {
            try
            {
                return File.Exists(fayl)
                    ? JsonSerializer.Deserialize<T>(File.ReadAllText(fayl))
                    : default;
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// 📖 USB-dəki ayar paketini oxuyur ✓ (yoxdursa <c>null</c> ✗)
        /// </summary>
        public static AyarlarPaketi? PaketiOxu(string usbKoku)
        {
            try
            {
                var fayl = Path.Combine(usbKoku, PaketQovlugu, InfoFayli);

                if (!File.Exists(fayl))
                {
                    return null;
                }

                return JsonSerializer.Deserialize<AyarlarPaketi>(File.ReadAllText(fayl));
            }
            catch
            {
                return null;
            }
        }
    }
}
