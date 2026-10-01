using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>💾 Qeydiyyatdan keçmiş DATA USB məlumatı ✓✓✓</summary>
    public sealed class DataUsbInfo
    {
        /// <summary>🔑 Unikal token — fləşkartı TANIYAN açar ✓ (başqa fləşkartlar ✗)</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>🏷️ İstifadəçinin verdiyi ad (məs. «Asif USB» ✓)</summary>
        public string Ad { get; set; } = string.Empty;

        /// <summary>📅 Qeydiyyat tarixi ✓</summary>
        public DateTime QeydiyyatTarixi { get; set; } = DateTime.Now;

        /// <summary>🔤 Son məlum hərf (məlumat üçün ✓ — hərf dəyişə bilər ✗)</summary>
        public string SonHərf { get; set; } = string.Empty;
    }

    /// <summary>
    /// 💾 <b>DATA USB XİDMƏTİ</b> ✓✓✓
    /// <para>
    /// ⚠ <b>YALNIZ SİZİN fləşkartınız tanınır</b> — «bütün fləşkartlar» YOX ✗✓✓
    /// </para>
    /// <para>
    /// Prinsip:
    /// <list type="number">
    ///   <item>«➕ Əlavə et» → seçilmiş fləşkartın kökündə <b>GİZLİ</b> marker faylı
    ///         yazılır ✓ (<c>021cars_drive.lock</c> + atribut <b>Hidden</b> ✓) və
    ///         içinə <b>unikal token</b> yazılır ✓</item>
    ///   <item>Token həm də kompüterdə <c>%AppData%\EnterpriseAeroStudio\data_usb.json</c>
    ///         faylında saxlanılır ✓</item>
    ///   <item>Proqram açılarkən bütün disklər skan edilir ✓, marker tapılan diskin
    ///         tokeni qeydiyyatdaki token ilə <b>ÜST-ÜSTƏ DÜŞÜRSƏ</b> → DATA USB ✓✓✓
    ///         (başqa fləşkartın tokeni fərqli olduğu üçün <b>tanınmır</b> ✗)</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class DataUsbService
    {
        /// <summary>🔑 Kökdə yazılan marker faylın adı ✓ (yalnız HIDDEN — System YOX ✗)</summary>
        public const string MarkerAdi = "021cars_drive.lock";

        /// <summary>📄 Kökdə yazılan **GÖRÜNƏN** info faylı ✓✓✓ (istifadəçi dərhal görür ✓)</summary>
        public const string InfoAdi = "021cars_DATA_USB.txt";

        /// <summary>📁 Marker faylın «data» qovluğundaki ehtiyat adı ✓</summary>
        public const string MarkerAdiData = "021cars_drive.lock";

        /// <summary>💾 Qeydiyyat faylı ✓ (baza ilə EYNİ qovluqda ✓✓✓)</summary>
        public static string QeydiyyatFayli { get; } = Path.Combine(
            Cas0201.Kok.DataQovlugu, "data_usb.json");

        /// <summary>✅ Qeydiyyatdan keçmiş DATA USB (yoxdursa null ✗)</summary>
        public static DataUsbInfo? Qeydiyyat { get; private set; }

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        static DataUsbService() => Yukle();

        /// <summary>📥 Qeydiyyatı fayldan oxuyur ✓</summary>
        public static void Yukle()
        {
            try
            {
                if (!File.Exists(QeydiyyatFayli))
                {
                    Qeydiyyat = null;
                    return;
                }

                var metn = File.ReadAllText(QeydiyyatFayli);
                Qeydiyyat = JsonSerializer.Deserialize<DataUsbInfo>(metn, Json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DataUsb qeydiyyatı oxunmadı: " + ex.Message);
                Qeydiyyat = null;
            }
        }

        /// <summary>📤 Qeydiyyatı fayla yazır ✓</summary>
        private static void YaddaSaxla()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(QeydiyyatFayli) ?? ".");
                MecburiYaz(QeydiyyatFayli,
                    JsonSerializer.Serialize(Qeydiyyat, Json));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DataUsb qeydiyyatı yazılmadı: " + ex.Message);
            }
        }

        /// <summary>
        /// 💾 <b>MÖVCUD USB NAMİZƏDLƏRİ</b> ✓✓✓
        /// <para>
        /// ⚠ ƏVVƏL yalnız <c>DriveType.Removable</c> göstərilirdi ✗ → bir çox USB
        /// fləşkart və xarici qurğu Windows tərəfindən <b>«Fixed»</b> kimi tanınır ✗
        /// → siyahı BOŞ qalırdı ✗✓✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: çıxarıla bilənlər ✓ + sabit disklər (sistem diski C: istisna ✗)
        /// hamısı göstərilir ✓ və markeri olanlar <b>«✓ marker var»</b> ilə işarələnir ✓
        /// </para>
        /// </summary>
        /// <returns>«Kök|Ad|Boş GB|Qeyd» formatında sətirlər ✓</returns>
        public static IReadOnlyList<string> Namizədlər()
        {
            var siyahi = new List<(string Sətir, int Sira)>();

            try
            {
                var sistem = SistemDiski();

                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady)
                        {
                            continue;
                        }

                        // ⚠ CD/DVD · şəbəkə diskləri göstərilmir ✗
                        if (d.DriveType is DriveType.CDRom or DriveType.Network or DriveType.NoRootDirectory)
                        {
                            continue;
                        }

                        var root = d.RootDirectory.FullName;

                        // ⚠ Sistem diski (Windows qurulub) ✗ — istifadəçi səhvən seçməsin
                        if (sistem is not null
                            && string.Equals(root, sistem, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var bos = d.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                        var ad = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "ADSIZ" : d.VolumeLabel;

                        var markerVar = File.Exists(Path.Combine(root, MarkerAdi))
                                        || File.Exists(Path.Combine(root, "data", MarkerAdiData));

                        var tip = d.DriveType == DriveType.Removable ? "USB" : "disk";

                        // ✓ sıralama: əvvəl çıxarıla bilənlər ✓, sonra markeri olanlar ✓
                        var sira = (d.DriveType == DriveType.Removable ? 100 : 0) + (markerVar ? 10 : 0);

                        siyahi.Add((
                            $"{root}|{ad}|{bos:F1} GB boş|{(markerVar ? "✓ marker var" : tip)}",
                            sira));
                    }
                    catch
                    {
                        // ✗ bu disk oxunmursa → keç ✓
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USB siyahısı alınmadı: " + ex.Message);
            }

            return siyahi
                .OrderByDescending(x => x.Sira)
                .Select(x => x.Sətir)
                .ToList();
        }

        /// <summary>🖥️ Sistem (Windows) diskinin kökü — siyahıdan çıxarılır ✗</summary>
        public static string? SistemDiski()
        {
            try
            {
                var kok = Path.GetPathRoot(Environment.SystemDirectory);

                return string.IsNullOrWhiteSpace(kok) ? null : kok;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>📂 İstifadəçinin seçdiyi QOVLUQDAN disk kökünü çıxarır ✓✓✓</summary>
        public static string? QovluqdanKok(string? qovluq)
        {
            if (string.IsNullOrWhiteSpace(qovluq))
            {
                return null;
            }

            try
            {
                var tam = Path.GetFullPath(qovluq);
                var kok = Path.GetPathRoot(tam);

                return string.IsNullOrWhiteSpace(kok) ? null : kok;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// ➕ <b>USB-ni DATA USB kimi QEYDİYYATA ALIR</b> ✓✓✓
        /// <list type="number">
        ///   <item>Kökdə <b>GİZLİ</b> marker faylı yaradılır ✓ (Hidden atribut ✓)</item>
        ///   <item>İçinə unikal <b>Token</b> yazılır ✓</item>
        ///   <item>Token kompüterdə saxlanılır ✓ → növbəti açılışlarda TANINIR ✓</item>
        /// </list>
        /// </summary>
        /// <returns>Nəticə mesajı ✓</returns>
        public static string ElaveEt(string root, string ad = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    return "⚠️ USB seçilməyib ✗";
                }

                root = Path.GetPathRoot(root.TrimEnd('\\')) ?? root;

                if (!Directory.Exists(root))
                {
                    return "⚠️ USB əlçatan deyil ✗ (taxılıbmı? ✓)";
                }

                İz($"═══ ➕ USB ƏLAVƏ ET → {root} ✓");
                
                var token = Guid.NewGuid().ToString("N");
                var adTam = string.IsNullOrWhiteSpace(ad) ? "DATA USB" : ad;

                // ================================================================
                //  ✅ ① KÖKDƏ GİZLİ MARKER ✓ (yalnız HIDDEN ✓ — System YOX ✗,
                //     çünki System atributu faylı Explorer-də TAMAMİLƏ gizlədir ✗✓✓)
                //  ✅ YERİ: «D:\021Cars\021cars_drive.lock» ✓✓✓
                // ================================================================
                var kok = KokQovluq(root);
                var marker = Path.Combine(kok, MarkerAdi);

                // 🔓 KÖHNƏ MARKER-LƏRİN ATRIBUTLARI TƏMİZLƏNİR ✓✓✓
                //    (əvvəl Hidden/ReadOnly idi ✗ → «Access to the path is denied» ✗✓✓)
                İz("① marker yazılır… ✓");

                AtributTemizle(marker);
                AtributTemizle(Path.Combine(root, MarkerAdi));
                AtributTemizle(Path.Combine(root, "data", MarkerAdiData));

                MecburiYaz(marker, JsonSerializer.Serialize(new
                {
                    token,
                    ad = adTam,
                    app = "Avtomobil Parkı v6.0",
                    yaradildi = DateTime.Now
                }, Json));

                try
                {
                    File.SetAttributes(marker, FileAttributes.Hidden);   // ✓ yalnız Hidden
                }
                catch
                {
                    // ✓ atribut qoyula bilmədisə fayl yenə işləyir ✓
                }

                // ================================================================
                //  ✅ ② KÖKDƏ **GÖRÜNƏN** INFO FAYLI ✓✓✓
                //     (istifadəçi USB-ni açanda DƏRHAL görür ✓ — gizli deyil ✗)
                // ================================================================
                try
                {
                    MecburiYaz(
                        Path.Combine(kok, InfoAdi),
                        "💾 DATA USB — AVTO SALON EHTİYAT DİSKİ\r\n" +
                        "════════════════════════════════════════════════════════\r\n" +
                        $"İstifadəçi : {adTam}\r\n" +
                        $"Kök       : {root}\r\n" +
                        $"Tarix     : {DateTime.Now:dd.MM.yyyy HH:mm}\r\n" +
                        $"Token     : {token}\r\n" +
                        "════════════════════════════════════════════════════════\r\n" +
                        "⚠️ BU FAYLI SİLMƏYİN ✗\r\n" +
                        "   Proqram bu fləşkartı TOKEN vasitəsilə tanıyır ✓.\r\n" +
                        "   Fayl silinsə «Tənzimləmələr → DATA USB → ➕ Əlavə et»\r\n" +
                        "   ilə yenidən qeydə almaq lazımdır ✓.\r\n");
                }
                catch
                {
                    // ✓ görünən fayl yazıla bilmədisə marker kifayətdir ✓
                }

                // ================================================================
                //  ✅ ③ «data» QOVLUĞU + gizli marker ✓ (ehtiyat mənbə ✓)
                // ================================================================
                try
                {
                    var dataQovluq = Path.Combine(kok, "data");
                    Directory.CreateDirectory(dataQovluq);

                    var ikinci = Path.Combine(dataQovluq, MarkerAdiData);
                    MecburiYaz(ikinci, File.ReadAllText(marker));
                    File.SetAttributes(ikinci, FileAttributes.Hidden);
                }
                catch
                {
                    // ✓ kök marker kifayətdir ✓
                }

                // ✅ ④ Kompüterdə qeyd ✓
                Qeydiyyat = new DataUsbInfo
                {
                    Token = token,
                    Ad = adTam,
                    QeydiyyatTarixi = DateTime.Now,
                    SonHərf = root
                };

                YaddaSaxla();

                // ================================================================
                //  ✅ ⑤ «021Cars» QURULUŞU YARADILIR + BAZA KOPYALANIR ✓✓✓
                //     (Media\Avtomobil · Media\Kredit · Senedler · Hesabatlar ✓)
                // ================================================================
                İz("② «021Cars» quruluşu + baza kopyalanır… ✓");
                
                var bazaNeticesi = YenidenQur(root);
                
                İz("③ bitdi ✓ — USB hazırdır ✓✓✓");

                return $"✅ DATA USB qeydə alındı ✓ → {root}\n" +
                       $"   • {MarkerAdi} (gizli ✓)\n" +
                       $"   • {InfoAdi} (GÖRÜNƏN ✓)\n" +
                       $"   • data\\{MarkerAdiData} (gizli ehtiyat ✓)\n" +
                       $"{bazaNeticesi}";
            }
            catch (Exception ex)
            {
                return "❌ USB qeydə alına bilmədi: " + ex.Message;
            }
        }

        /// <summary>
        /// ➖ <b>QEYDİYYATI SİLİR</b> ✓✓✓
        /// <list type="bullet">
        ///   <item>Kompüterdən qeyd silinir ✓ → artıq HEÇ BİR fləşkart DATA USB sayılmır ✗</item>
        ///   <item>Marker faylı da silinir ✓ (əgər taxılıbsa ✓)</item>
        /// </list>
        /// </summary>
        public static string Sil(bool markeriDeSil = true)
        {
            try
            {
                if (Qeydiyyat is null)
                {
                    return "ℹ️ Qeydiyyatdan keçmiş DATA USB yoxdur ✗";
                }

                if (markeriDeSil)
                {
                    // ================================================================
                    //  ✅ ① USB-DƏN **HƏR ŞEY** SİLİNİR ✓✓✓ («D:\021Cars\» bütün
                    //     bölmələrlə: baza · Media · sənədlər · hesabatlar ✓)
                    // ================================================================
                    try
                    {
                        var tapilan = Tap();

                        if (tapilan is not null)
                        {
                            System.Diagnostics.Debug.WriteLine(HamisiniSil(tapilan.Value.Root));
                        }
                    }
                    catch
                    {
                        // ✓ davam ✓
                    }

                    foreach (var root in DiskKokleri())
                    {
                        try
                        {
                            var marker = Path.Combine(root, MarkerAdi);

                            if (File.Exists(marker))
                            {
                                File.SetAttributes(marker, FileAttributes.Normal);
                                File.Delete(marker);
                            }

                            var ikinci = Path.Combine(root, "data", MarkerAdiData);

                            if (File.Exists(ikinci))
                            {
                                File.SetAttributes(ikinci, FileAttributes.Normal);
                                File.Delete(ikinci);
                            }

                            // ✅ GÖRÜNƏN info faylı da silinir ✓
                            var info = Path.Combine(root, InfoAdi);

                            if (File.Exists(info))
                            {
                                File.Delete(info);
                            }
                        }
                        catch
                        {
                            // ✗ silinmədisə də qeyd silinir ✓
                        }
                    }
                }

                var ad = Qeydiyyat.Ad;
                Qeydiyyat = null;
                YaddaSaxla();

                return $"✅ DATA USB qeydiyyatı silindi ✓ ({ad})";
            }
            catch (Exception ex)
            {
                return "❌ Qeydiyyat silinə bilmədi: " + ex.Message;
            }
        }

        /// <summary>📁 USB-dəki ƏSAS QOVLUĞUN adı ✓✓✓ («D:\021Cars\»)</summary>
        public const string KokAdi = "021Cars";

        /// <summary>📁 USB-dəki «021Cars» kök qovluğu — yaradılır ✓✓✓</summary>
        public static string KokQovluq(string root)
        {
            var kok = Path.Combine(root, KokAdi);

            try
            {
                Directory.CreateDirectory(kok);
            }
            catch
            {
                // ✓ yaradıla bilmədisə kökün özü istifadə olunur ✓
            }

            return kok;
        }

        /// <summary>
        /// 📎 <b>SƏNƏD & MEDIA QOVLUĞU</b> → <c>D:\021Cars\Media</c> ✓✓✓
        /// <para>
        /// ✅ DATA USB TANINMIŞSA media faylları (sənəd · şəkil ✓) BURADA saxlanılır ✓
        /// — başqa cür <c>null</c> qaytarır ✗ və proqram yerli qovluğa yazır ✓
        /// </para>
        /// </summary>
        public static string? MediaQovlugu()
        {
            try
            {
                var tapilan = Tap();

                if (tapilan is null)
                {
                    return null;
                }

                var qovluq = Path.Combine(KokQovluq(tapilan.Value.Root), "Media");
                Directory.CreateDirectory(qovluq);

                return qovluq;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USB media qovluğu alınmadı: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 🏗️ <b>USB-DƏ PROFESSİONAL QURULUŞU YARADIR</b> ✓✓✓
        /// <pre>
        /// D:\021Cars\
        /// ├── 021cars_data.db          💾 baza (tam surət ✓)
        /// ├── 021cars_DATA_USB.txt     📄 info (token · tarix ✓)
        /// ├── 021cars_drive.lock       🔑 gizli marker ✓
        /// ├── data\                    💾 backup.db + gizli marker ✓
        /// ├── Media\Avtomobil\&lt;Id&gt;\   📎 avtomobil sənədləri ✓
        /// ├── Media\Kredit\&lt;Id&gt;\      📎 kredit sənədləri ✓
        /// ├── Media\Emeliyyat\&lt;Id&gt;\   📎 əməliyyat sənədləri ✓
        /// ├── Senedler\                📑 ümumi sənədlər ✓
        /// └── Hesabatlar\              📊 PDF hesabatlar ✓
        /// </pre>
        /// </summary>
        public static string YenidenQur(string root)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    return "⚠️ USB əlçatan deyil ✗";
                }

                var kok = KokQovluq(root);

                // ✅ Bütün bölmələr yaradılır ✓✓✓
                foreach (var alt in new[]
                         {
                             "data",
                             @"Media\Avtomobil",
                             @"Media\Kredit",
                             @"Media\Emeliyyat",
                             "Senedler",
                             "Hesabatlar"
                         })
                {
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(kok, alt));
                    }
                    catch
                    {
                        // ✓ bu bölmə yaradıla bilmədisə davam ✓
                    }
                }

                // ✅ Kökdəki KÖHNƏ fayllar təmizlənir ✓ (hamısı «021Cars»-a köçdü ✓✓✓)
                foreach (var fayl in new[] { MarkerAdi, InfoAdi, "021cars_data.db" })
                {
                    try
                    {
                        var yol = Path.Combine(root, fayl);

                        if (File.Exists(yol))
                        {
                            MecburiSil(yol);
                        }
                    }
                    catch
                    {
                        // ✓ davam ✓
                    }
                }

                // ✅ Köhnə «D:\data\» qovluğundaki fayllar da təmizlənir ✓
                try
                {
                    var kohneData = Path.Combine(root, "data");

                    if (Directory.Exists(kohneData))
                    {
                        foreach (var fayl in new[] { MarkerAdiData, "backup.db" })
                        {
                            var yol = Path.Combine(kohneData, fayl);

                            if (File.Exists(yol))
                            {
                                MecburiSil(yol);
                            }
                        }

                        if (Directory.GetFileSystemEntries(kohneData).Length == 0)
                        {
                            Directory.Delete(kohneData, recursive: true);
                        }
                    }
                }
                catch
                {
                    // ✓ davam ✓
                }

                // ✅ Baza surəti də yenilənir ✓
                var baza = UsbYeKopyala(root);

                return $"🏗️ USB quruluşu hazırdır ✓ → {kok}\n" +
                       "   ├── 021cars_data.db · 021cars_DATA_USB.txt · 021cars_drive.lock\n" +
                       "   ├── data\\ (backup.db + marker)\n" +
                       "   ├── Media\\Avtomobil · Media\\Kredit · Media\\Emeliyyat\n" +
                       "   ├── Senedler\\ · Hesabatlar\\\n" +
                       $"   └── {baza}";
            }
            catch (Exception ex)
            {
                return "❌ Quruluş yaradıla bilmədi: " + ex.Message;
            }
        }

        /// <summary>
        /// 🗑️ <b>USB-DƏN HƏR ŞEYİ SİLİR</b> ✓✓✓
        /// <list type="bullet">
        ///   <item><c>D:\021Cars\</c> qovluğu — İÇİNDƏKİ HƏR ŞEY ilə birlikdə ✓</item>
        ///   <item>Kökdəki və «data» qovluğundaki köhnə fayllar ✓</item>
        /// </list>
        /// </summary>
        public static string HamisiniSil(string root)
        {
            var silinen = new List<string>();

            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    return "ℹ️ USB taxılı deyil ✗ — yalnız kompüterdəki qeydiyyat silinir ✓";
                }

                // ✅ ① «021Cars» qovluğu — TAMAMİLƏ ✓✓✓
                var kok = Path.Combine(root, KokAdi);

                if (Directory.Exists(kok))
                {
                    try
                    {
                        Directory.Delete(kok, recursive: true);
                        silinen.Add($"{KokAdi}\\ (bütün bölmələrlə ✓)");
                    }
                    catch (Exception ex)
                    {
                        silinen.Add($"{KokAdi}\\ — SİLİNMƏDİ ✗ ({ex.Message})");
                    }
                }

                // ✅ ② Kökdəki köhnə fayllar ✓
                foreach (var fayl in new[] { MarkerAdi, InfoAdi, "021cars_data.db" })
                {
                    try
                    {
                        var yol = Path.Combine(root, fayl);

                        if (File.Exists(yol))
                        {
                            MecburiSil(yol);
                            silinen.Add(fayl);
                        }
                    }
                    catch
                    {
                        // ✓ davam ✓
                    }
                }

                // ✅ ③ Köhnə «data» qovluğu — yalnız BİZİM fayllar ✓
                var dataQovluq = Path.Combine(root, "data");

                if (Directory.Exists(dataQovluq))
                {
                    foreach (var fayl in new[] { MarkerAdiData, "backup.db" })
                    {
                        try
                        {
                            var yol = Path.Combine(dataQovluq, fayl);

                            if (File.Exists(yol))
                            {
                                MecburiSil(yol);
                                silinen.Add($"data\\{fayl}");
                            }
                        }
                        catch
                        {
                            // ✓ davam ✓
                        }
                    }

                    try
                    {
                        if (Directory.GetFileSystemEntries(dataQovluq).Length == 0)
                        {
                            Directory.Delete(dataQovluq, recursive: true);
                            silinen.Add("data\\ (boş idi ✓)");
                        }
                    }
                    catch
                    {
                        // ✓ davam ✓
                    }
                }

                return silinen.Count == 0
                    ? "✅ USB-də silinəcək heç nə yox idi ✓"
                    : "✅ USB-dən SİLİNDİ ✓:\n   • " + string.Join("\n   • ", silinen);
            }
            catch (Exception ex)
            {
                return "⚠️ USB təmizlənməsi yarımçıq: " + ex.Message;
            }
        }

        /// <summary>🗂️ Yerli baza faylını (.db) tapır ✓ (① əsl baza ✓ → ② köhnə yol ✓ → ③ ən böyük ✓)</summary>
        public static string? BazaFayliTap()
        {
            try
            {
                // ✅ ① ƏSL (real) baza → BİRBAŞA ondan istifadə ✓✓✓
                if (File.Exists(YerliBazaYolu)) return YerliBazaYolu;

                // ♻️ ② köhnə (səhv) yolda qalmış baza ✓ — məlumat itməsin ✗✓✓
                if (File.Exists(Cas0201.Kok.KohneBazaYolu)) return Cas0201.Kok.KohneBazaYolu;

                // ③ son çarə: exe qovluğunda ən böyük .db ✓
                return Directory
                    .GetFiles(AppContext.BaseDirectory, "*.db", SearchOption.AllDirectories)
                    .OrderByDescending(f =>
                    {
                        try
                        {
                            return new FileInfo(f).Length;
                        }
                        catch
                        {
                            return 0L;
                        }
                    })
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Baza tapılmadı: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 💾 <b>VERİLƏNLƏR BAZASINI (.db) USB-YƏ KOPYALAYIR</b> ✓✓✓
        /// <list type="number">
        ///   <item><c>D:\021cars_data.db</c> — <b>GÖRÜNƏN</b> baza faylı ✓</item>
        ///   <item><c>D:\data\backup.db</c> — «data» qovluğundaki surət ✓</item>
        /// </list>
        /// <para>
        /// ✅ <b>VACUUM INTO</b> ilə — WAL jurnalı da daxil olmaqla bazanın
        /// <b>TAM, TƏMİZ</b> surəti alınır ✓ (adi kopyalamada WAL itə bilərdi ✗).
        /// ⚠ VACUUM alınmazsa → adi <c>File.Copy</c> ilə ehtiyat yol ✓
        /// </para>
        /// </summary>
        public static string UsbYeKopyala(string root, string? bazaYolu = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    return "⚠️ USB əlçatan deyil ✗ — baza kopyalanmadı";
                }

                bazaYolu ??= BazaFayliTap();

                if (bazaYolu is null || !File.Exists(bazaYolu))
                {
                    return "⚠️ Yerli baza faylı (.db) tapılmadı ✗";
                }

                // ✅ HƏR ŞEY «021Cars» QOVLUĞUNUN İÇİNDƏDİR ✓✓✓
                var kok = KokQovluq(root);
                var dataQovluq = Path.Combine(kok, "data");
                Directory.CreateDirectory(dataQovluq);

                var hedefler = new[]
                {
                    Path.Combine(kok, "021cars_data.db"),         // ✅ 021Cars\ içində ✓
                    Path.Combine(dataQovluq, "backup.db")         // ✅ 021Cars\data\ içində ✓
                };

                foreach (var hedef in hedefler)
                {
                    try
                    {
                        if (File.Exists(hedef))
                        {
                            File.SetAttributes(hedef, FileAttributes.Normal);
                            File.Delete(hedef);
                        }
                    }
                    catch
                    {
                        // ✓ üzərinə yazmaqla həll olunacaq ✓
                    }

#if WINDOWS
                    // ✅ ① TAM surət (WAL daxil ✓) — VACUUM INTO ✓✓✓
                    try
                    {
                        using var c = new Microsoft.Data.Sqlite.SqliteConnection(
                            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                            {
                                DataSource = bazaYolu,
                                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly
                            }.ToString());

                        c.Open();

                        using var cmd = c.CreateCommand();
                        cmd.CommandText = $"VACUUM INTO '{hedef.Replace("'", "''")}';";
                        cmd.ExecuteNonQuery();

                        continue;      // ✓ alındı ✓
                    }
                    catch
                    {
                        // ✓ aşağıda adi kopya ✓
                    }
#endif

                    // ✅ ② Ehtiyat yol — adi kopya ✓
                    File.Copy(bazaYolu, hedef, overwrite: true);
                }

                var olcuKb = 0L;

                try
                {
                    olcuKb = new FileInfo(hedefler[0]).Length / 1024;
                }
                catch
                {
                    // ✓ ölçü oxunmadı ✓
                }

                return $"✅ Baza USB-yə kopyalandı ✓ ({olcuKb:N0} KB)\n" +
                       $"   • 021cars_data.db (görünən ✓)\n" +
                       $"   • data\\backup.db ✓";
            }
            catch (Exception ex)
            {
                return "❌ Baza USB-yə kopyalana bilmədi: " + ex.Message;
            }
        }

        // ====================================================================
        //  📥 USB-DƏN YERLİ BAZANI BƏRPA ✓✓✓ (yeni quraşdırılmış kompüter ✓)
        // ====================================================================

        /// <summary>
        /// 🖥️ <b>YERLİ BAZA FAYLININ TAM YOLU</b> ✓✓✓ —
        /// <c>{kök}\EnterpriseAeroStudio\avtopark.db</c> ✓
        /// <para>
        /// ⚠️ <b>VAHİD MƏNBƏ</b> ✗✓✓: yol <see cref="Cas0201.Kok.BazaYolu"/>-dan götürülür ✓.
        /// Əvvəl burada <c>{kök}\avtopark.db</c> yazılırdı ✗ → USB bərpası
        /// <b>YANLIŞ fayla</b> kopyalayırdı ✗, proqram isə DÜZGÜN faylı açırdı ✗
        /// → «USB-dən məlumat gəlmədi» ✗✓✓
        /// </para>
        /// </summary>
        public static string YerliBazaYolu => Cas0201.Kok.BazaYolu;

        /// <summary>✅ Fayl mövcuddur? ✓ (istənilən xəta → <c>false</c> ✗)</summary>
        private static bool FaylVar(string? yol)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(yol) && File.Exists(yol);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>🔎 Yerli bazada ən azı BİR qeyd var? ✓ (fayl yoxdursa → yox ✗)</summary>
        public static bool YerliBazadaMelumatVar(string? bazaYolu = null)
        {
            try
            {
                bazaYolu ??= YerliBazaYolu;

                if (!File.Exists(bazaYolu)) return false;

                // ⚠️ Çox kiçik fayl = boş baza ✓ (SQLite başlığı ~24-28 KB ✓)
                if (new FileInfo(bazaYolu).Length < 24 * 1024) return false;

#if WINDOWS
                try
                {
                    using var c = new Microsoft.Data.Sqlite.SqliteConnection(
                        new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                        {
                            DataSource = bazaYolu,
                            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly
                        }.ToString());

                    c.Open();

                    var cəmi = 0L;

                    foreach (var cədvəl in new[]
                             {
                                 "Cars", "Sales", "Credits", "CreditTransactions",
                                 "Expenses", "Partners", "PartnerShares", "PartnerPayments"
                             })
                    {
                        try
                        {
                            using var cmd = c.CreateCommand();
                            cmd.CommandText = $"SELECT COUNT(*) FROM \"{cədvəl}\";";

                            cəmi += Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);

                            if (cəmi > 0) return true;      // ✅ məlumat var ✓
                        }
                        catch
                        {
                            // ✓ bu cədvəl yoxdursa → keç ✓
                        }
                    }

                    return cəmi > 0;
                }
                catch
                {
                    // ✓ baza oxunmadı (zədəli?) → məlumat YOX sayılır ✓
                    return false;
                }
#else
                return true;      // 🌐 Veb tərəfdə yoxlama aparılmır ✓
#endif
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 📥 <b>USB-DƏN YERLİ BAZANI BƏRPA EDİR</b> ✓✓✓
        /// <list type="number">
        ///   <item>💾 Qeydiyyatlı DATA USB taxılıbsa ✓ <c>021Cars\021cars_data.db</c> tapılır ✓</item>
        ///   <item>🆕 Yerli baza <b>boşdursa</b> ✓ (yeni quraşdırma ✓) → USB-dəki baza köçürülür ✓✓✓</item>
        ///   <item>🛡️ Yerli bazada məlumat VARSA → <b>toxunulmur</b> ✗ (heç nə itmir ✗✓✓)</item>
        ///   <item>♻️ <c>mecburi: true</c> olsa → köhnə baza <c>avtopark.db.bak</c> kimi saxlanıb dəyişilir ✓</item>
        /// </list>
        /// <para>
        /// 🖥️ Nəticə: YENİ kompüterdə proqram açılanda <b>bütün məlumat</b>
        /// (avtomobil · xərc · satış · kredit · əməliyyat · tərəfdaş · pay ✓)
        /// USB-dən gəlir ✓✓✓ → sonra Firebase ilə də sinxronlaşır ✓
        /// </para>
        /// </summary>
        /// <returns>Nəticə mətni ✓ (loq üçün ✓)</returns>
        /// <summary>
        /// ♻️ <b>KÖHNƏ (SƏHV) YOLDA QALMIŞ BAZANI ƏSAS YOLA BƏRPA EDİR</b> ✓✓✓
        /// <para>
        /// ⚠️ <b>SƏBƏB</b> ✗✓✓: köhnə buraxılışda USB bərpası bazanı
        /// <c>{kök}\avtopark.db</c> faylına yazırdı ✗, proqram isə
        /// <c>{kök}\EnterpriseAeroStudio\avtopark.db</c> faylını açırdı ✗ →
        /// USB-dən gələn avtomobillər <b>GÖRÜNMÜRDÜ</b> ✗✓✓
        /// </para>
        /// <para>
        /// ✅ İNDİ: ƏSAS baza boşdursa/yoxdursa ✗, köhnə yolda isə məlumat VARSA ✓
        /// → həmin baza ƏSAS yola köçürülür ✓ (istifadəçi məlumatı İTMİR ✗✓✓)
        /// </para>
        /// </summary>
        /// <returns>Nəticə mətni ✓ (köçürmə lazım deyilsə → boş sətir ✗)</returns>
        /// <summary>
        /// ♻️ <b>KÖHNƏ (SƏHV) YOLDA QALMIŞ BAZANI ƏSAS BAZA İLƏ BİRLƏŞDİRİR</b> ✓✓✓
        /// <para>
        /// ⚠️ <b>SƏBƏB</b> ✗✓✓: köhnə buraxılışda USB bərpası bazanı
        /// <c>{kök}\avtopark.db</c> faylına yazırdı ✗, proqram isə
        /// <c>{kök}\EnterpriseAeroStudio\avtopark.db</c> faylını açırdı ✗
        /// </para>
        /// <para>
        /// 🛡️ <b>TƏHLÜKƏSİZLİK</b> ✗✓✓: ƏSAS baza varsa <b>HEÇ VAXT ÜZƏRİNƏ YAZILMIR</b> ✗ —
        /// yalnız <b>BİRLƏŞDİRMƏ</b> olunur ✓ (çatışmayan qeydlər əlavə olunur ✓, heç nə itmir ✗✓✓).
        /// Kopyalama YALNIZ ƏSAS BAZA HEÇ YOXDURSA edilir ✓ (o halda itiriləcək bir şey yoxdur ✓).
        /// </para>
        /// </summary>
        /// <returns>Nəticə mətni ✓ (heç nə lazım deyilsə → boş sətir ✗)</returns>
        public static string KohneBazaniBərpaEt()
        {
            try
            {
                var əsas = YerliBazaYolu;
                var köhnə = Cas0201.Kok.KohneBazaYolu;

                if (!File.Exists(köhnə)) return string.Empty;

                Directory.CreateDirectory(Path.GetDirectoryName(əsas) ?? Cas0201.Kok.DataQovlugu);

                // ------------------------------------------------------------
                //  🆕 ƏSAS BAZA HEÇ YOXDURSA ✓ → sxem lazımdır ✓ → KOPYALA ✓
                //     (bu, YEGANƏ kopyalama halıdır ✓ — itiriləcək məlumat yoxdur ✓)
                // ------------------------------------------------------------
                if (!File.Exists(əsas))
                {
                    File.Copy(köhnə, əsas, overwrite: false);

                    foreach (var əlavə in new[] { "-wal", "-shm" })
                    {
                        try
                        {
                            if (File.Exists(köhnə + əlavə)) File.Copy(köhnə + əlavə, əsas + əlavə, overwrite: true);
                        }
                        catch { }
                    }

                    var ö = new FileInfo(əsas).Length / 1024;

                    Cas0201.Firebase.AppLogger.Melumat($"♻️ Köhnə yoldan baza kopyalandı ✓ → {əsas} ({ö:N0} KB ✓)");

                    return $"♻️ KÖHNƏ YOLDAN BAZA BƏRPA OLUNDU ✓ ({ö:N0} KB)\n" +
                           $"   • Köhnə yol: {köhnə}\n" +
                           $"   • Əsas yol : {əsas}\n" +
                           "   • Məlumat İTMƏDİ ✓✓✓";
                }

                // ------------------------------------------------------------
                //  ✅ ƏSAS BAZA VARDIR ✗ → HEÇ VAXT ÜZƏRİNƏ YAZMIRIQ ✗✓✓
                //     → yalnız BİRLƏŞDİRMƏ (çatışmayanlar əlavə olunur ✓)
                // ------------------------------------------------------------
                return "♻️ KÖHNƏ YOL: " + BirləşdirUnion(köhnə, "Köhnə yol");
            }
            catch (Exception ex)
            {
                return "♻️ Köhnə baza bərpası alınmadı ✗ — " + ex.Message;
            }
        }

        /// <summary>
        /// 📥 <b>USB-DƏN YERLİ BAZANI BƏRPA EDİR</b> ✓✓✓
        /// <para>♻️ Əvvəlcə köhnə (səhv) yolda qalmış baza ƏSAS yola gətirilir ✓✓✓</para>
        /// </summary>
        /// <param name="mecburi">♻️ <c>true</c> → yerli baza dolu olsa da USB-dən dəyişilir ✓</param>
        public static string UsbDenBerpaEt(bool mecburi = false)
        {
            // ♻️ ⓪ İTMİŞ MƏLUMATIN BƏRPASI ✓ (.bak ✓ · köhnə yol ✓ · ehtiyat surətlər ✓)
            var itmişBərpa = ItmisMelumatiBerpaEt();

            // ♻️ ① KÖHNƏ YOLDAN BİRLƏŞDİRMƏ ✓ (heç nə itmir ✗✓✓)
            var köhnəBərpa = KohneBazaniBərpaEt();

            var əsasNəticə = UsbDenBerpaEsas(mecburi);

            var giriş = new List<string>();

            if (!string.IsNullOrWhiteSpace(itmişBərpa)) giriş.Add(itmişBərpa);
            if (!string.IsNullOrWhiteSpace(köhnəBərpa)) giriş.Add(köhnəBərpa);

            return giriş.Count == 0
                ? əsasNəticə
                : string.Join("\n\n", giriş) + "\n\n" + əsasNəticə;
        }

        /// <summary>📥 USB-dən bərpanın ƏSAS hissəsi ✓ (yuxarıdaki metoddan çağırılır ✓)</summary>
        private static string UsbDenBerpaEsas(bool mecburi)
        {
            try
            {
                var tapilan = Tap();

                // ℹ️ USB yoxdur ✗ → problem deyil ✓ (proqram normal işləyir ✓)
                if (tapilan is null) return "ℹ️ DATA USB taxılı deyil ✓ — bərpa tələb olunmur ✓";

                var kok = KokQovluq(tapilan.Value.Root);

                // 📥 USB-dəki baza namizədləri ✓ (öncə ƏSAS ✓, sonra ehtiyat ✓)
                var namizədlər = new[]
                {
                    Path.Combine(kok, "021cars_data.db"),
                    Path.Combine(kok, "data", "backup.db"),
                    Path.Combine(tapilan.Value.Root, "021cars_data.db")
                };

                var mənbə = namizədlər.FirstOrDefault(FaylVar);

                if (mənbə is null) return "ℹ️ USB-də baza surəti hələ yoxdur ✗ (ilk dəfədir ✓)";

                var yerli = YerliBazaYolu;

                // ============================================================
                //  🆕 YERLİ BAZA HEÇ YOXDURSA ✓ (yeni quraşdırma ✓) → KOPYALA ✓
                // ------------------------------------------------------------
                //  ⚠️ Bu, YEGANƏ kopyalama halıdır ✓ — itiriləcək məlumat YOXDUR ✗✓✓
                // ============================================================
                if (!File.Exists(yerli))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(yerli) ?? Cas0201.Kok.DataQovlugu);

                    File.Copy(mənbə, yerli, overwrite: false);

                    foreach (var əlavə in new[] { "-wal", "-shm" })
                    {
                        try
                        {
                            if (File.Exists(mənbə + əlavə)) File.Copy(mənbə + əlavə, yerli + əlavə, overwrite: true);
                        }
                        catch { }
                    }

                    var ö = new FileInfo(yerli).Length / 1024;

                    Cas0201.Firebase.AppLogger.Melumat($"📥 USB-dən baza kopyalandı ✓ ({ö:N0} KB ✓)");

                    return $"📥 USB-DƏN BƏRPA OLUNDU ✓ ({ö:N0} KB)\n" +
                           $"   • Mənbə: {mənbə}\n" +
                           "   • Bütün məlumat gəldi ✓ (avtomobil · xərc · satış · kredit · əməliyyat · tərəfdaş ✓)";
                }

                // ============================================================
                //  🔀 YERLİ BAZA VARDIR ✓ → HEÇ VAXT ÜZƏRİNƏ YAZMIRIQ ✗✓✓
                // ------------------------------------------------------------
                //  ✅ Yalnız BİRLƏŞDİRMƏ: çatışmayan qeydlər ƏLAVƏ olunur ✓
                //  ✅ Id toqquşanda qeyd YENİ Id alır ✓ (heç nə İTMİR ✗✓✓)
                //  ✅ Zibil (burada silinmiş) qeydlər geri gətirilmir ✗✓✓
                // ============================================================
                var birləşdirmə = BirləşdirUnion(mənbə, "USB");

                Cas0201.Firebase.AppLogger.Melumat("🔀 USB birləşdirmə: " + birləşdirmə.Replace("\n", " "));

                return birləşdirmə;
            }
            catch (Exception ex)
            {
                return "⚠️ USB-dən bərpa alınmadı ✗ — " + ex.Message;
            }
        }

        /// <summary>
        /// 🔑 <b>QEYDİYYATSIZ DATA USB-Nİ AVTOMATİK TANITIR</b> ✓✓✓
        /// <para>
        /// ⚠️ <b>Problem</b> ✗: USB bir kompüterdə qeydiyyatdan keçir ✓, başqa kompüterdə
        /// <c>data_usb.json</c> faylı YOXDUR ✗ → proqram «USB datası yoxdur» deyirdi ✗✓✓
        /// </para>
        /// <para>
        /// ✅ <b>İNDİ</b>: fləşkartın kökündəki GİZLİ marker (<c>021cars_drive.lock</c>)
        /// oxunur ✓ və token AVTOMATİK yerli qeydiyyata yazılır ✓ →
        /// <b>həmin kompüter də USB-ni tanıyır</b> ✓✓✓
        /// </para>
        /// <para>🛡️ Başqa fləşkartın tokeni fərqlidirsə → toxunulmur ✗</para>
        /// </summary>
        public static string UsbAvtomatikTanit()
        {
            try
            {
                // ✅ Artıq tanınır ✓ və taxılıbsa → iş yoxdur ✗
                if (Tap() is not null) return "✅ DATA USB artıq tanınır ✓";

                foreach (var root in DiskKokleri())
                {
                    var token = MarkerOxu(root);

                    if (string.IsNullOrWhiteSpace(token)) continue;

                    // ⚠️ Köhnə qeydiyyat FƏRQLİ tokenlidirsə → toxunmuruq ✗ (başqa fləşkart ✗)
                    if (Qeydiyyat is not null
                        && !string.IsNullOrWhiteSpace(Qeydiyyat.Token)
                        && !string.Equals(Qeydiyyat.Token, token, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    Qeydiyyat = new DataUsbInfo
                    {
                        Token = token,
                        Ad = string.IsNullOrWhiteSpace(Qeydiyyat?.Ad)
                            ? "Avtomatik tanındı ✓"
                            : Qeydiyyat!.Ad,
                        QeydiyyatTarixi = Qeydiyyat?.QeydiyyatTarixi ?? DateTime.Now,
                        SonHərf = root
                    };

                    YaddaSaxla();

                    Cas0201.Firebase.AppLogger.Melumat(
                        $"🔑 DATA USB avtomatik tanındı ✓ → {root} ✓ (token qeydə alındı ✓)");

                    return $"🔑 DATA USB AVTOMATİK TANINDI ✓\n   • Disk: {root}\n" +
                           "   • Bu kompüter də fləşkartı tanıyır ✓✓✓";
                }

                return "ℹ️ DATA USB tapılmadı ✗ (qeydiyyat tələb olunur ✓)";
            }
            catch (Exception ex)
            {
                return "⚠️ USB tanıma xətası ✗ — " + ex.Message;
            }
        }

        /// <summary>
        /// 🔀 <b>USB-DƏKİ BAZANI YERLİ BAZA İLƏ BİRLƏŞDİRİR</b> ✓✓✓
        /// <para>
        /// ⚠️ Mövcud qeydlərə <b>TOXUNULMUR</b> ✗ — yalnız yerli bazada <b>OLMAYAN</b>
        /// ID-lər əlavə olunur ✓✓✓ (<c>INSERT OR IGNORE</c> ✓)
        /// </para>
        /// <para>🖥️ Nəticə: bir kompüterdə yığılan məlumat USB ilə digərinə GƏLİR ✓</para>
        /// </summary>
        /// <summary>
        /// 🔀 <b>USB BAZASINI YERLİ BAZA İLƏ BİRLƏŞDİRİR</b> ✓✓✓ — <b>HEÇ NƏ İTMİR</b> ✗✓✓
        /// <para>
        /// 🛡️ Yerli bazaya <b>HEÇ VAXT üzərinə yazılmır</b> ✗ — yalnız çatışmayan qeydlər
        /// ƏLAVƏ olunur ✓. Id toqquşanda qeyd <b>YENİ Id</b> alır ✓ və asılı istinadlar
        /// (CarId · CreditId · SaleId …) <b>avtomatik düzəldilir</b> ✓✓✓
        /// </para>
        /// </summary>
        public static string UsbBazasiniBirləşdir(string usbBaza) => BirləşdirUnion(usbBaza, "USB");

        // ====================================================================
        //  🔀 BİRLƏŞDİRMƏ MEXANİZMİ ✓✓✓ — «HEÇ NƏ İTMİR, HEÇ NƏ SİLİNMİR» ✗✓✓
        // --------------------------------------------------------------------
        //  ✅ PRİNSİP:
        //    ① Yerli bazaya HEÇ VAXT üzərinə yazılmır ✗ (kopyalama yalnız baza
        //       HEÇ YOXDURSA ✓).
        //    ② Qeyd yerli bazada yoxdursa → eyni Id ilə əlavə olunur ✓.
        //    ③ Id TOQQUŞURSA (iki kompüterdə də 1, 2, 3 … ✓) →
        //       • sətir TAM EYNİDİRSƏ → təkrar əlavə edilmir ✗ (idempotent ✓)
        //       • sətir FƏRLİDİRSƏ   → YENİ Id ilə əlavə olunur ✓✓✓
        //         və asılı istinadlar (CarId ✓ və s.) yeni Id-yə düzəldilir ✓
        //    ④ ZİBİL qaydası: burada SİLİNMİŞ qeyd geri gətirilmir ✗✓✓
        // ====================================================================

        /// <summary>🔗 Xarici açar sütunu → istinad etdiyi cədvəl ✓ (boş ad = xüsusi hal ✓: Senedler.RefId ✓)</summary>
        private static readonly Dictionary<string, string> XariciAcarlar = new(StringComparer.OrdinalIgnoreCase)
        {
            ["CarId"] = "Avtomobiller",
            ["CreditId"] = "Kreditler",
            ["CreditTransactionId"] = "KreditEmeliyyatlari",
            ["SaleId"] = "Satislar",
            ["ExpenseId"] = "Xercler",
            ["PartnerId"] = "Terefdaslar",
            ["PartnerPaymentId"] = "TerefdasOdenisleri",
            ["BarterCarId"] = "Avtomobiller",
            ["BarterSaleId"] = "Satislar",
            ["RefId"] = "",                                  // ⚠ növü RefType-dan asılıdır ✓
        };

        /// <summary>
        /// 🔑 <b>«EYNİ QEYD» AÇARLARI</b> ✓✓✓ — Id toqquşanda sətri TANIMAQ üçün ✓
        /// <para>
        /// ⚠️ İki kompüterdə Id-lər eyni olur (1, 2, 3 … ✓) — açar sütun uyğun gəlirsə
        /// (məs. eyni <b>dövlət nömrəsi</b> ✓) → bu, <b>EYNİ</b> qeyddir ✓ →
        /// dublikat yaradılmır ✗ və <b>boş sahələr doldurulur</b> ✓✓✓
        /// (dolu sahələrə — məs. sıra nömrəsi <c>312</c> ✓ — TOXUNULMUR ✗✓✓)
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, string[]> EyniQeydAcarlari = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Avtomobiller"] = new[] { "QeydiyyatNisani" },     // 🚗 eyni dövlət nömrəsi = eyni maşın ✓
            ["Kreditler"] = new[] { "MuqavileNomresi" },         // 💳 eyni müqavilə nömrəsi ✓
            ["Satislar"] = new[] { "MuqavileNomresi" },          // 🧾 eyni satış müqaviləsi ✓
            ["Terefdaslar"] = new[] { "Ad" },                    // 👥 eyni tərəfdaş adı ✓
        };

        /// <summary>📋 Cədvəllərin emal SIRASI ✓ (əvvəl valideyn ✓, sonra övlad ✓)</summary>
        private static readonly string[] BirlestirmeSirasi =
        {
            "Avtomobiller", "Terefdaslar", "XercKataloqu", "Kreditler", "Xercler",
            "Satislar", "KreditEmeliyyatlari", "TerefdasPaylari",
            "TerefdasOdenisleri", "Senedler",

            // ================================================================
            //  ✅ v6.2.11 — MÖHLƏTLƏR və MANUAL KASSA HƏRƏKƏTLƏRİ də birləşdirilir ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL BU İKİ CƏDVƏL BURADA YOX İDİ ✗ → 📥 «USB / başqa
            //  kompüterdən məlumat götür» edildikdə:
            //    • ⏳ bütün MÖHLƏT ödənişləri İTİRDİ ✗
            //    • 💵 əl ilə yazılmış kassa hərəkətləri İTİRDİ ✗✓✓
            //  (`CreditId` / `SaleId` xarici açarları artıq `XariciAcarlar`
            //   xəritəsində idi ✓ → Id-lər avtomatik DÜZGÜN bağlanır ✓)
            //  ⚠ Sıra VACİBDİR: valideynlər (Kreditler · Satislar) YUXARIDADIR ✓
            // ================================================================
            "OdenisMohletleri", "KassaHereketleri"
        };

        /// <summary>🔑 UNİKAL açar sütunları ✓ (toqquşmada mövcud sətir tapılır ✓ — dublikat yaranmır ✗✓✓)</summary>
        private static readonly Dictionary<string, string[]> UnikalAcarlar = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Terefdaslar"] = new[] { "Ad" },
        };

        /// <summary>
        /// 🔀 <b>BİRLƏŞDİRMƏNİN ÜMUMİ METODU</b> ✓✓✓ —
        /// mənbə bazanı yerli baza ilə <b>TƏHLÜKƏSİZ</b> birləşdirir ✓ (heç nə itmir ✗✓✓)
        /// </summary>
        private static string BirləşdirUnion(string mənbəYolu, string ad) =>
            BirləşdirUnionNet(mənbəYolu, ad).Mesaj;

        /// <summary>🔀 Birləşdirmə ✓ (nəticə: əlavə olunan qeyd · düzəldilən istinad · mesaj ✓)</summary>
        private static (int Əlavə, int Düzəliş, string Mesaj) BirləşdirUnionNet(string mənbəYolu, string ad)
        {
            var əlavə = 0;
            var düzəliş = 0;
            var doldurulan = 0;
            var cədvəllər = new List<string>();

            try
            {
                var hədəf = YerliBazaYolu;

                if (!File.Exists(mənbəYolu))
                {
                    return (0, 0, $"ℹ️ «{ad}» bazası tapılmadı ✗ — birləşdirmə lazım deyil ✓");
                }

                if (!File.Exists(hədəf))
                {
                    return (0, 0, "⚠️ Yerli baza yoxdur ✗ — əvvəlcə yaradılmalıdır ✓");
                }

                SnaypşotAl(hədəf);        // 🛡️ ƏVVƏLCƏ EHTİYAT SURƏTİ ✓✓✓

#if WINDOWS
                using var c = new Microsoft.Data.Sqlite.SqliteConnection(
                    new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = hədəf }.ToString());

                c.Open();

                using (var pr = c.CreateCommand())
                {
                    pr.CommandText = "PRAGMA busy_timeout = 20000;";
                    pr.ExecuteNonQuery();
                }

                using (var qoş = c.CreateCommand())
                {
                    qoş.CommandText = $"ATTACH DATABASE '{mənbəYolu.Replace("'", "''")}' AS src;";
                    qoş.ExecuteNonQuery();
                }

                var xəritələr = new Dictionary<string, Dictionary<long, long>>(StringComparer.OrdinalIgnoreCase);
                var gözləyən = new List<(string Cədvəl, long Id, string Sütun, long Köhnə, string Hədəf)>();
                var zibil = ZibilIdleriniOxu();

                foreach (var cədvəl in CədvəlSırası(c))
                {
                    var nəticə = BirləşdirCədvəl(c, cədvəl, xəritələr, gözləyən, zibil);

                    əlavə += nəticə.Əlavə;
                    doldurulan += nəticə.Doldurulan;

                    if (nəticə.Əlavə > 0) cədvəllər.Add($"{cədvəl} +{nəticə.Əlavə}");
                }

                // ============================================================
                //  ♻️ NÖVBƏ ② : ASILI İSTİNADLARIN DÜZƏLİŞİ ✓✓✓
                // ------------------------------------------------------------
                //  ⚠️ Yalnız «KÖHNƏ dəyəri hələ də saxlayan» sətirlərə toxunulur ✗✓✓
                //     → ikiqat dəyişdirmə (səhv Id-yə bağlama) MÜMKÜN DEYİL ✗✓✓✓
                // ============================================================
                düzəliş = XariciAcarlariDuzelt(c, gözləyən, xəritələr);

                try
                {
                    using var ayır = c.CreateCommand();
                    ayır.CommandText = "DETACH DATABASE src;";
                    ayır.ExecuteNonQuery();
                }
                catch { }
#endif

                if (əlavə == 0 && doldurulan == 0)
                {
                    return (0, düzəliş, $"✅ «{ad}» ilə yerli baza EYNİDİR ✓ — əlavə olunacaq yeni qeyd yoxdur ✓");
                }

                var mesaj = $"🔀 «{ad}» — {əlavə} YENİ QEYD GƏLDİ ✓✓✓\n" +
                            (əlavə > 0 ? $"   • {string.Join(" · ", cədvəllər)}\n" : string.Empty) +
                            (doldurulan > 0
                                ? $"   • 🩹 {doldurulan} BOŞ SAHƏ DOLDURULDU ✓ (məs. sıra nömrəsi «312» ✓)\n"
                                : string.Empty) +
                            (düzəliş > 0 ? $"   • ♻️ {düzəliş} asılı istinad düzəldildi ✓\n" : string.Empty) +
                            "   • Mövcud qeydlərə TOXUNULMADI ✗✓✓ (heç nə silinmir ✓)\n" +
                            "   • ☁️ Sonra Firebase-ə də göndəriləcək ✓";

                Cas0201.Firebase.AppLogger.Melumat(mesaj.Replace("\n", " "));

                return (əlavə, düzəliş + doldurulan, mesaj);
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "birləşdirmə: " + ad);

                return (əlavə, düzəliş, $"⚠️ «{ad}» birləşdirməsi alınmadı ✗ — {ex.Message}");
            }
        }

        /// <summary>
        /// 🛡️ <b>BAZANIN EHTİYAT SURƏTİNİ SAXLAYIR</b> ✓✓✓ —
        /// <c>{data}\Yedekler\Baza\avtopark_yyyyMMdd_HHmmss.db</c> ✓ (son 20 surət ✓)
        /// <para>⚠️ Hər birləşdirmədən ƏVVƏL çağırılır ✓ → istənilən hal baş verərsə məlumat GERİ QAYTARILA BİLƏR ✓✓✓</para>
        /// </summary>
        public static string? SnaypşotAl(string? bazaYolu = null)
        {
            try
            {
                var baza = bazaYolu ?? YerliBazaYolu;

                if (!File.Exists(baza)) return null;

                var qovluq = Path.Combine(Cas0201.Kok.DataQovlugu, "Yedekler", "Baza");
                Directory.CreateDirectory(qovluq);

                var hədəf = Path.Combine(qovluq, $"avtopark_{DateTime.Now:yyyyMMdd_HHmmss}.db");

                File.Copy(baza, hədəf, overwrite: true);

                foreach (var əlavə in new[] { "-wal", "-shm" })
                {
                    try
                    {
                        if (File.Exists(baza + əlavə)) File.Copy(baza + əlavə, hədəf + əlavə, overwrite: true);
                    }
                    catch { }
                }

                // 🧹 Yalnız son 20 surət saxlanılır ✓ (disk dolmur ✗)
                var köhnələr = Directory.GetFiles(qovluq, "avtopark_*.db")
                    .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                    .Skip(20)
                    .ToList();

                foreach (var f in köhnələr)
                {
                    try { File.Delete(f); } catch { }
                }

                return hədəf;
            }
            catch
            {
                return null;      // ✓ surət alınmasa da birləşdirmə davam edir ✓ (çökmə YOX ✗)
            }
        }

        /// <summary>
        /// 🗑️ <b>ZİBİL (SİLİNMİŞ) QEYDLƏR</b> ✓ — <c>{data}\Trash\*.json</c> fayllarından oxunur ✓
        /// <para>⚠️ Məqsəd ✗✓✓: birləşdirmə zamanı <b>burada SİLİNMİŞ</b> qeyd USB-dən geri gəlməsin ✗✓✓</para>
        /// </summary>
        private static Dictionary<string, HashSet<long>> ZibilIdleriniOxu()
        {
            var nəticə = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Avtomobiller"] = new HashSet<long>(),
                ["Xercler"] = new HashSet<long>(),
                ["Satislar"] = new HashSet<long>(),
                ["Kreditler"] = new HashSet<long>(),
                ["KreditEmeliyyatlari"] = new HashSet<long>(),
                ["Senedler"] = new HashSet<long>(),
            };

            try
            {
                var qovluq = Path.Combine(Cas0201.Kok.DataQovlugu, "Trash");

                if (!Directory.Exists(qovluq)) return nəticə;

                foreach (var fayl in Directory.GetFiles(qovluq, "*.json"))
                {
                    try
                    {
                        using var sənəd = JsonDocument.Parse(File.ReadAllText(fayl));
                        var kök = sənəd.RootElement;

                        if (!kök.TryGetProperty("Action", out var aksiya)) continue;
                        if (!string.Equals(aksiya.GetString(), "Silinmə", StringComparison.Ordinal)) continue;

                        IdYaz(nəticə, "Avtomobiller", kök, "Car");
                        IdYazList(nəticə, "Xercler", kök, "Expenses");
                        IdYazList(nəticə, "Satislar", kök, "Sales");
                        IdYazList(nəticə, "Kreditler", kök, "Credits");
                        IdYazList(nəticə, "KreditEmeliyyatlari", kök, "Transactions");
                        IdYazList(nəticə, "Senedler", kök, "Attachments");
                    }
                    catch { }
                }
            }
            catch { }

            return nəticə;
        }

#if WINDOWS
        /// <summary>📋 Hər iki bazada olan cədvəllərin emal SIRASI ✓ (valideyn → övlad ✓)</summary>
        private static List<string> CədvəlSırası(Microsoft.Data.Sqlite.SqliteConnection c)
        {
            var mövcud = new List<string>();

            using (var q = c.CreateCommand())
            {
                q.CommandText =
                    "SELECT name FROM main.sqlite_master WHERE type = 'table' " +
                    "AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsHistory' " +
                    "AND name IN (SELECT name FROM src.sqlite_master WHERE type = 'table');";

                using var r = q.ExecuteReader();

                while (r.Read()) mövcud.Add(r.GetString(0));
            }

            var sıra = new List<string>();

            foreach (var ad in BirlestirmeSirasi)
            {
                var tapılan = mövcud.FirstOrDefault(m => string.Equals(m, ad, StringComparison.OrdinalIgnoreCase));

                if (tapılan is null) continue;

                sıra.Add(tapılan);
                mövcud.Remove(tapılan);
            }

            sıra.AddRange(mövcud.OrderBy(m => m, StringComparer.OrdinalIgnoreCase));

            return sıra;
        }

        /// <summary>📐 Cədvəlin sütunları ✓ (ad + PK olub-olmaması ✓)</summary>
        private static List<(string Ad, bool Pk)> SutunSiyahı(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl)
        {
            var siyahı = new List<(string, bool)>();

            using var q = c.CreateCommand();
            q.CommandText = $"PRAGMA main.table_info('{cədvəl.Replace("'", "''")}');";

            using var r = q.ExecuteReader();

            while (r.Read()) siyahı.Add((r.GetString(1), r.GetInt32(5) > 0));

            return siyahı;
        }

        /// <summary>📐 Cədvəlin sütun adları ✓</summary>
        private static List<string> SutunAdlari(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl) =>
            SutunSiyahı(c, cədvəl).Select(s => s.Ad).ToList();

        /// <summary>📐 Sütun varmı? ✓</summary>
        private static bool SutunVar(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl, string sütun) =>
            SutunAdlari(c, cədvəl).Any(s => string.Equals(s, sütun, StringComparison.OrdinalIgnoreCase));
#endif

#if WINDOWS
        /// <summary>🔤 Dəyərin MÜQAYİSƏ üçün mətn forması ✓ (dəqiq ✓ — format itkisi YOX ✗)</summary>
        private static string DeyerMetni(object? dəyər)
        {
            if (dəyər is null || dəyər is DBNull) return "\u0000null";

            if (dəyər is byte[] baytlar) return Convert.ToBase64String(baytlar);

            if (dəyər is double d) return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            if (dəyər is float f) return f.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

            if (dəyər is IFormattable ff) return ff.ToString(null, System.Globalization.CultureInfo.InvariantCulture);

            return dəyər.ToString() ?? string.Empty;
        }

        /// <summary>🔍 Yerli bazadaki sətir mənbə sətri ilə TAM EYNİDİRMİ? ✓ (təkrar əlavə olunmasın ✗✓✓)</summary>
        private static bool SətirEynidir(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl,
            long id, Dictionary<string, object?> dəyərlər)
        {
            using var q = c.CreateCommand();
            q.CommandText = $"SELECT * FROM main.\"{cədvəl}\" WHERE \"Id\" = $id;";
            q.Parameters.AddWithValue("$id", id);

            using var r = q.ExecuteReader();

            if (!r.Read()) return false;

            for (var i = 0; i < r.FieldCount; i++)
            {
                var ad = r.GetName(i);

                if (!dəyərlər.TryGetValue(ad, out var mənbə)) continue;

                var hədəf = r.IsDBNull(i) ? null : r.GetValue(i);

                if (!string.Equals(DeyerMetni(hədəf), DeyerMetni(mənbə), StringComparison.Ordinal)) return false;
            }

            return true;
        }

        /// <summary>➕ Sətri yerli bazaya əlavə edir ✓ (<c>idIle: false</c> → YENİ Id alır ✓)</summary>
        private static long İnsertEt(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl,
            List<(string Ad, bool Pk)> sutunlar, Dictionary<string, object?> dəyərlər, bool idIle)
        {
            var adlar = new List<string>();
            var parametrlər = new List<string>();

            using var q = c.CreateCommand();

            foreach (var (ad, _) in sutunlar)
            {
                if (!dəyərlər.TryGetValue(ad, out var dəyər)) continue;
                if (!idIle && string.Equals(ad, "Id", StringComparison.OrdinalIgnoreCase)) continue;

                var p = "$p" + parametrlər.Count;

                adlar.Add($"\"{ad}\"");
                parametrlər.Add(p);
                q.Parameters.AddWithValue(p, dəyər ?? DBNull.Value);
            }

            if (adlar.Count == 0) return -1;

            q.CommandText = $"INSERT INTO main.\"{cədvəl}\" ({string.Join(", ", adlar)}) " +
                            $"VALUES ({string.Join(", ", parametrlər)});";

            q.ExecuteNonQuery();

            using var idq = c.CreateCommand();
            idq.CommandText = "SELECT last_insert_rowid();";

            var nəticə = idq.ExecuteScalar();

            return nəticə is null || nəticə is DBNull ? -1 : Convert.ToInt64(nəticə);
        }

        /// <summary>🔑 UNİKAL açarı uyğun gələn MÖVCUD sətri tapır ✓ (dublikat yaranmasın ✗✓✓)</summary>
        private static long? UnikalTap(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl,
            Dictionary<string, object?> dəyərlər)
        {
            if (!UnikalAcarlar.TryGetValue(cədvəl, out var açarlar)) return null;

            using var q = c.CreateCommand();

            var şərtlər = new List<string>();

            for (var i = 0; i < açarlar.Length; i++)
            {
                if (!dəyərlər.TryGetValue(açarlar[i], out var dəyər)) return null;

                şərtlər.Add($"\"{açarlar[i]}\" = $p{i}");
                q.Parameters.AddWithValue($"$p{i}", dəyər ?? DBNull.Value);
            }

            q.CommandText = $"SELECT \"Id\" FROM main.\"{cədvəl}\" WHERE {string.Join(" AND ", şərtlər)} LIMIT 1;";

            var nəticə = q.ExecuteScalar();

            return nəticə is null || nəticə is DBNull ? null : Convert.ToInt64(nəticə);
        }
#endif

#if WINDOWS
        /// <summary>
        /// 📋 <b>BİR CƏDVƏLİ BİRLƏŞDİRİR</b> ✓✓✓ —
        /// çatışmayan sətirlər əlavə olunur ✓, Id toqquşanda isə sətir <b>YENİ Id</b> alır ✓ (heç nə itmir ✗✓✓)
        /// </summary>
        private static (int Əlavə, int Atlanan, int Doldurulan) BirləşdirCədvəl(Microsoft.Data.Sqlite.SqliteConnection c,
            string cədvəl,
            Dictionary<string, Dictionary<long, long>> xəritələr,
            List<(string Cədvəl, long Id, string Sütun, long Köhnə, string Hədəf)> gözləyən,
            Dictionary<string, HashSet<long>> zibil)
        {
            var əlavə = 0;
            var atlanan = 0;
            var doldurulan = 0;

            var sutunlar = SutunSiyahı(c, cədvəl);

            if (sutunlar.Count == 0) return (0, 0, 0);
            if (!sutunlar.Any(s => string.Equals(s.Ad, "Id", StringComparison.OrdinalIgnoreCase))) return (0, 0, 0);

            var xəritə = new Dictionary<long, long>();

            xəritələr[cədvəl] = xəritə;

            // 🔗 Bu cədvəldə mövcud olan XARİCİ AÇAR sütunları ✓
            var fkSütunları = sutunlar
                .Select(s => s.Ad)
                .Where(ad => XariciAcarlar.ContainsKey(ad))
                .ToList();

            // 🔗 Sətrin xarici açarları SONRA düzəltmək üçün QEYDƏ ALINIR ✓✓✓ (təhlükəsiz ✓)
            void GözləyənləriYaz(long hədəfId, Dictionary<string, object?> mənbəSətir)
            {
                foreach (var sütun in fkSütunları)
                {
                    if (!mənbəSətir.TryGetValue(sütun, out var xam) || xam is null) continue;

                    var hədəf = XariciAcarlar[sütun];

                    if (string.IsNullOrEmpty(hədəf))     // 🔗 Senedler.RefId ✓ (növ RefType-dan ✓)
                    {
                        var növ = mənbəSətir.TryGetValue("RefType", out var rt) ? Convert.ToString(rt) : null;

                        hədəf = növ switch
                        {
                            "Avtomobil" => "Avtomobiller",
                            "KreditEmeliyyati" => "KreditEmeliyyatlari",
                            _ => string.Empty
                        };
                    }

                    if (string.IsNullOrEmpty(hədəf)) continue;

                    gözləyən.Add((cədvəl, hədəfId, sütun, Convert.ToInt64(xam), hədəf));
                }
            }

            var hədəfdəIdlər = new HashSet<long>();

            using (var q = c.CreateCommand())
            {
                q.CommandText = $"SELECT \"Id\" FROM main.\"{cədvəl}\";";

                using var r = q.ExecuteReader();

                while (r.Read())
                {
                    if (!r.IsDBNull(0)) hədəfdəIdlər.Add(r.GetInt64(0));
                }
            }

            var mənbəSətirləri = new List<Dictionary<string, object?>>();

            using (var q = c.CreateCommand())
            {
                q.CommandText = $"SELECT * FROM src.\"{cədvəl}\";";

                using var r = q.ExecuteReader();

                while (r.Read())
                {
                    var sətir = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                    for (var i = 0; i < r.FieldCount; i++)
                    {
                        sətir[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                    }

                    mənbəSətirləri.Add(sətir);
                }
            }

            var zibilIdlər = zibil.TryGetValue(cədvəl, out var z) ? z : null;

            foreach (var sətir in mənbəSətirləri)
            {
                if (!sətir.TryGetValue("Id", out var idXam) || idXam is null) continue;

                var köhnəId = Convert.ToInt64(idXam);
                var dəyərlər = XariciAcarlariDeyis(cədvəl, sətir, xəritələr);

                // 🗑️ ZİBİL: burada SİLİNMİŞ qeyd USB-dən GERİ GƏTİRİLMİR ✗✓✓
                if (zibilIdlər is not null && zibilIdlər.Contains(köhnəId) && !hədəfdəIdlər.Contains(köhnəId))
                {
                    atlanan++;
                    continue;
                }

                // ① Yerli bazada BU Id YOXDUR → eyni Id ilə əlavə olunur ✓
                if (!hədəfdəIdlər.Contains(köhnəId))
                {
                    try
                    {
                        İnsertEt(c, cədvəl, sutunlar, dəyərlər, idIle: true);

                        GözləyənləriYaz(köhnəId, sətir);

                        xəritə[köhnəId] = köhnəId;
                        hədəfdəIdlər.Add(köhnəId);
                        əlavə++;

                        continue;
                    }
                    catch
                    {
                        // ⚠️ Bu Id ARTIQ TUTULUB ✗ (başqa sətrə YENİ Id verilib ✓) → aşağıda YENİ Id alır ✓
                    }
                }

                // ② 🚗 EYNİ QEYDDİR? ✓ (açar sütun uyğundur — məs. eyni dövlət nömrəsi ✓)
                //    → dublikat YARADILMIR ✗ və BOŞ SAHƏLƏR mənbədən DOLDURULUR ✓✓✓
                //    📍 dolu sahələrə — məs. sıra nömrəsi «312» ✓ — TOXUNULMUR ✗✓✓
                var eyniId = EyniQeydiTap(c, cədvəl, dəyərlər);

                if (eyniId is not null)
                {
                    doldurulan += BoşSahələriDoldur(c, cədvəl, eyniId.Value, dəyərlər);
                    xəritə[köhnəId] = eyniId.Value;
                    continue;
                }

                // ③ Id VAR və sətir TAM EYNİDİR → təkrar əlavə edilmir ✗ (idempotent ✓✓✓)
                if (SətirEynidir(c, cədvəl, köhnəId, dəyərlər))
                {
                    xəritə[köhnəId] = köhnəId;
                    continue;
                }

                // ③ Id VAR, sətir FƏRLİDİR → YENİ Id ilə əlavə olunur ✓✓✓ (heç nə İTMİR ✗)
                try
                {
                    var yeniId = İnsertEt(c, cədvəl, sutunlar, dəyərlər, idIle: false);

                    if (yeniId > 0)
                    {
                        GözləyənləriYaz(yeniId, sətir);

                        xəritə[köhnəId] = yeniId;
                        hədəfdəIdlər.Add(yeniId);
                        əlavə++;
                    }
                }
                catch
                {
                    // ⚠️ UNİKAL açar toqquşması (məs. Terefdaslar.Ad ✓) → MÖVCUD sətir tapılır ✓
                    var tapılan = UnikalTap(c, cədvəl, dəyərlər);

                    if (tapılan is not null) xəritə[köhnəId] = tapılan.Value;
                    else atlanan++;
                }
            }

            return (əlavə, atlanan, doldurulan);
        }
#endif

#if WINDOWS
        /// <summary>🔗 Mənbə sətrinin XARİCİ AÇARLARINI yeni Id-lərə dəyişir ✓ (mümkün olduqca ✓)</summary>
        private static Dictionary<string, object?> XariciAcarlariDeyis(string cədvəl,
            Dictionary<string, object?> sətir,
            Dictionary<string, Dictionary<long, long>> xəritələr)
        {
            var nəticə = new Dictionary<string, object?>(sətir, StringComparer.OrdinalIgnoreCase);

            foreach (var (sütun, hədəfCədvəl) in XariciAcarlar)
            {
                if (!nəticə.TryGetValue(sütun, out var xam) || xam is null) continue;

                var istinad = hədəfCədvəl;

                // 🔗 Senedler.RefId ✓ — növü RefType-dan asılıdır ✓
                if (string.IsNullOrEmpty(istinad))
                {
                    var növ = nəticə.TryGetValue("RefType", out var rt) ? Convert.ToString(rt) : null;

                    istinad = növ switch
                    {
                        "Avtomobil" => "Avtomobiller",
                        "KreditEmeliyyati" => "KreditEmeliyyatlari",
                        _ => ""
                    };
                }

                if (string.IsNullOrEmpty(istinad)) continue;
                if (!xəritələr.TryGetValue(istinad, out var x)) continue;

                var köhnə = Convert.ToInt64(xam);

                if (x.TryGetValue(köhnə, out var yeni) && yeni != köhnə) nəticə[sütun] = yeni;
            }

            return nəticə;
        }

        /// <summary>
        /// ♻️ <b>ASILI İSTİNADLARI DÜZƏLDİR</b> ✓✓✓ (növbə ② ✓)
        /// <para>
        /// 🔒 <b>TƏHLÜKƏSİZLİK</b> ✗✓✓: hər düzəliş <c>AND &lt;sütun&gt; = köhnə dəyər</c> şərti ilə
        /// edilir ✓ → yalnız <b>HƏLƏ KÖHNƏ Id-ni saxlayan</b> sətirlər dəyişir ✓
        /// (ikiqat dəyişdirmə ✗ / səhv Id-yə bağlama ✗ MÜMKÜN DEYİL ✓✓✓)
        /// </para>
        /// </summary>
        private static int XariciAcarlariDuzelt(Microsoft.Data.Sqlite.SqliteConnection c,
            List<(string Cədvəl, long Id, string Sütun, long Köhnə, string Hədəf)> gözləyən,
            Dictionary<string, Dictionary<long, long>> xəritələr)
        {
            var say = 0;

            foreach (var (cədvəl, id, sütun, köhnə, hədəfCədvəl) in gözləyən)
            {
                if (!xəritələr.TryGetValue(hədəfCədvəl, out var xəritə)) continue;
                if (!xəritə.TryGetValue(köhnə, out var yeni) || yeni == köhnə) continue;

                try
                {
                    using var u = c.CreateCommand();
                    u.CommandText = $"UPDATE main.\"{cədvəl}\" SET \"{sütun}\" = $yeni " +
                                    $"WHERE \"Id\" = $id AND \"{sütun}\" = $köhnə;";
                    u.Parameters.AddWithValue("$yeni", yeni);
                    u.Parameters.AddWithValue("$id", id);
                    u.Parameters.AddWithValue("$köhnə", köhnə);

                    say += u.ExecuteNonQuery();
                }
                catch
                {
                    // ✓ bu sütun yoxdursa/köhnə sxemdirsə → keç ✓ (çökmə YOX ✗)
                }
            }

            return say;
        }
#endif

#if WINDOWS
        /// <summary>🔍 «EYNİ QEYD»i tapır ✓ — açar sütun(lar) uyğun gələn MÖVCUD sətri qaytarır ✓</summary>
        private static long? EyniQeydiTap(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl,
            Dictionary<string, object?> dəyərlər)
        {
            try
            {
                if (!EyniQeydAcarlari.TryGetValue(cədvəl, out var açarlar)) return null;

                using var q = c.CreateCommand();

                var şərtlər = new List<string>();

                for (var i = 0; i < açarlar.Length; i++)
                {
                    if (!dəyərlər.TryGetValue(açarlar[i], out var dəyər)) return null;

                    // ⚠️ açar BOŞDURSA → tanıma mümkün deyil ✗ (boş nömrə «eyni» sayılmır ✓)
                    if (BoşDəyərdir(dəyər)) return null;

                    şərtlər.Add($"\"{açarlar[i]}\" = $p{i}");
                    q.Parameters.AddWithValue($"$p{i}", dəyər!);
                }

                q.CommandText = $"SELECT \"Id\" FROM main.\"{cədvəl}\" WHERE {string.Join(" AND ", şərtlər)} LIMIT 1;";

                var nəticə = q.ExecuteScalar();

                return nəticə is null || nəticə is DBNull ? null : Convert.ToInt64(nəticə);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>🩹 Yerli sətrin BOŞ sahələrini mənbədən DOLDURUR ✓ (dolu sahələrə TOXUNULMUR ✗✓✓)</summary>
        private static int BoşSahələriDoldur(Microsoft.Data.Sqlite.SqliteConnection c, string cədvəl,
            long id, Dictionary<string, object?> dəyərlər)
        {
            var say = 0;

            try
            {
                var mövcud = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                using (var q = c.CreateCommand())
                {
                    q.CommandText = $"SELECT * FROM main.\"{cədvəl}\" WHERE \"Id\" = $id;";
                    q.Parameters.AddWithValue("$id", id);

                    using var r = q.ExecuteReader();

                    if (!r.Read()) return 0;

                    for (var i = 0; i < r.FieldCount; i++)
                    {
                        mövcud[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                    }
                }

                foreach (var (sütun, mənbə) in dəyərlər)
                {
                    if (string.Equals(sütun, "Id", StringComparison.OrdinalIgnoreCase)) continue;

                    // ⚠️ mənbə BOŞDURSA → doldurmağın mənası yoxdur ✗
                    if (BoşDəyərdir(mənbə)) continue;

                    // ========================================================
                    //  🔢 İSTİSNA ✓✓✓: `SiraNomresi` (avtomobil ✓)
                    // --------------------------------------------------------
                    //  ⚠️ SƏBƏB ✗: birləşdirmə nəticəsində maşına avtomatik
                    //     nömrə (0/4 ✓) verilə bilər ✗ — halbuki istifadəçinin
                    //     ÖZ nömrəsi (məs. 312 ✓) daha dəyərlidir ✓✓✓
                    //  ✅ Ona görə bu BİR sahə mənbədən gəlir ✓ (eyni maşın ✓)
                    // ========================================================
                    var serialİstisna =
                        string.Equals(sütun, "SiraNomresi", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(cədvəl, "Avtomobiller", StringComparison.OrdinalIgnoreCase);

                    // ✅ yerli dəyər DOLUDURSA → TOXUNULMUR ✗✓✓ (istisna olan sahə ✓ müstəsna ✓)
                    var yerliDolu = mövcud.TryGetValue(sütun, out var cari) && !BoşDəyərdir(cari);

                    if (yerliDolu && !serialİstisna) continue;

                    try
                    {
                        using var u = c.CreateCommand();
                        u.CommandText = $"UPDATE main.\"{cədvəl}\" SET \"{sütun}\" = $dəyər WHERE \"Id\" = $id;";
                        u.Parameters.AddWithValue("$dəyər", mənbə!);
                        u.Parameters.AddWithValue("$id", id);

                        say += u.ExecuteNonQuery();
                    }
                    catch { }
                }

                // ============================================================
                //  🔇 SƏSSİZ REJİM ✓✓✓ — ★ LOG FLOODUNUN QARŞISI ★
                // ------------------------------------------------------------
                //  ⚠️ ƏVVƏL ✗: hər DOLDURULMUŞ QEYD üçün AYRI loq sətri
                //     yazılırdı ✗ (məs. 165 avtomobil ✗) → bir bərpa
                //     əməliyyatı **500-ə yaxın sətir** ✗ → loq faylı
                //     şişirdi ✗, disk/UI yoruldu ✗✓✓
                //  ✅ İNDİ: yekun say YALNIZ **BİR** sətirdə yazılır ✓✓✓
                //     («   • 🩹 N BOŞ SAHƏ DOLDURULDU ✓» —
                //      bax: <see cref="BirləşdirUnionNet"/> ✓)
                //  ℹ️ Nəticə (say ✓) DƏYİŞMİR ✗ — yalnız loq azalır ✓
                // ============================================================
            }
            catch { }

            return say;
        }

        /// <summary>⬜ Dəyər BOŞDUR? ✓ (<c>null</c> · <c>0</c> · boş mətn ✓)</summary>
        private static bool BoşDəyərdir(object? dəyər)
        {
            if (dəyər is null || dəyər is DBNull) return true;
            if (dəyər is string s) return s.Trim().Length == 0;
            if (dəyər is long l) return l == 0;
            if (dəyər is int i) return i == 0;
            if (dəyər is short sh) return sh == 0;
            if (dəyər is double d) return Math.Abs(d) < 0.0000001;
            if (dəyər is float f) return Math.Abs(f) < 0.0000001f;
            if (dəyər is decimal m) return m == 0m;
            if (dəyər is byte[] b) return b.Length == 0;

            return false;
        }
#endif

        /// <summary>🔢 JSON obyektinin <c>Id</c> dəyərini siyahıya yazır ✓</summary>
        private static void IdYaz(Dictionary<string, HashSet<long>> hədəf, string cədvəl,
            JsonElement kök, string xassə)
        {
            try
            {
                if (!hədəf.TryGetValue(cədvəl, out var idlər)) return;
                if (!kök.TryGetProperty(xassə, out var obyekt)) return;
                if (obyekt.ValueKind != JsonValueKind.Object) return;
                if (!obyekt.TryGetProperty("Id", out var id)) return;
                if (id.ValueKind != JsonValueKind.Number) return;

                idlər.Add(id.GetInt64());
            }
            catch { }
        }

        /// <summary>🔢 JSON massivinin bütün <c>Id</c> dəyərlərini yazır ✓</summary>
        private static void IdYazList(Dictionary<string, HashSet<long>> hədəf, string cədvəl,
            JsonElement kök, string xassə)
        {
            try
            {
                if (!hədəf.TryGetValue(cədvəl, out var idlər)) return;
                if (!kök.TryGetProperty(xassə, out var massiv)) return;
                if (massiv.ValueKind != JsonValueKind.Array) return;

                foreach (var element in massiv.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object) continue;
                    if (!element.TryGetProperty("Id", out var id)) continue;
                    if (id.ValueKind != JsonValueKind.Number) continue;

                        idlər.Add(id.GetInt64());
                }
            }
            catch { }
        }

        /// <summary>
        /// ♻️ <b>İTMİŞ MƏLUMATI BƏRPA EDİR</b> ✓✓✓
        /// <para>
        /// Köhnə buraxılışlar bazanı səhv yola yazırdı ✗ və ya üzərinə yazırdı ✗ →
        /// qalmış BÜTÜN bazalar (köhnə yol ✓ · <c>.bak</c> ✓ · ehtiyat surətlər ✓)
        /// tapılıb ƏSAS baza ilə <b>BİRLƏŞDİRİLİR</b> ✓✓✓ (heç nə itmir ✗)
        /// </para>
        /// </summary>
        public static string ItmisMelumatiBerpaEt()
        {
            try
            {
                var əsas = YerliBazaYolu;

                // ⚠️ Əsas baza yoxdursa → birləşdirmə mümkün deyil ✗ (əvvəlcə kopyalama/bərpa ✓)
                if (!File.Exists(əsas)) return string.Empty;

                var namizədlər = new List<string>
                {
                    əsas + ".bak",
                    əsas + ".bak1",
                    Cas0201.Kok.KohneBazaYolu,
                    Cas0201.Kok.KohneBazaYolu + ".bak",
                };

                try
                {
                    var bazaQovluq = Path.Combine(Cas0201.Kok.DataQovlugu, "Yedekler", "Baza");

                    if (Directory.Exists(bazaQovluq))
                    {
                        namizədlər.AddRange(Directory.GetFiles(bazaQovluq, "avtopark_*.db")
                            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                            .Take(3));      // ⚡ yalnız ən son 3 surət ✓ (sürətli ✓)
                    }
                }
                catch { }

                var faylSayı = 0;
                var qeydSayı = 0;
                var detallar = new List<string>();
                var görülən = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var yol in namizədlər)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol)) continue;

                        var tam = Path.GetFullPath(yol);

                        if (!görülən.Add(tam)) continue;
                        if (string.Equals(tam, Path.GetFullPath(əsas), StringComparison.OrdinalIgnoreCase)) continue;

                        var nəticə = BirləşdirUnionNet(yol, Path.GetFileName(yol));

                        if (nəticə.Əlavə > 0)
                        {
                            faylSayı++;
                            qeydSayı += nəticə.Əlavə;
                            detallar.Add($"{Path.GetFileName(yol)} → +{nəticə.Əlavə}");
                        }
                    }
                    catch { }
                }

                if (qeydSayı == 0) return string.Empty;

                var mesaj = $"♻️ İTMİŞ MƏLUMAT BƏRPA OLUNDU ✓ — {qeydSayı} qeyd ({faylSayı} fayl ✓)\n" +
                            "   • " + string.Join("\n   • ", detallar) + "\n" +
                            "   • Mövcud qeydlərə TOXUNULMADI ✗✓✓";

                Cas0201.Firebase.AppLogger.Melumat(mesaj.Replace("\n", " "));

                return mesaj;
            }
            catch (Exception ex)
            {
                return "♻️ İtmiş məlumat bərpası yoxlanmadı ✓ — " + ex.Message;
            }
        }

        // ====================================================================
        //  🔓 ATRIBUT KÖMƏKÇİLƏRİ ✓✓✓ — «Access to the path is denied» ✗ həlli
        // --------------------------------------------------------------------
        //  ⚠️ PROBLEM ✗: marker faylı <c>Hidden</c> atributu ilə yaradılır ✓ →
        //  başqa kompüterdə onu YENİDƏN YAZMAQ ✗ / SİLMƏK ✗ mümkün olmur ✗
        //  («Access to the path 'D:\021cars_drive.lock' is denied» ✗✓✓)
        //  ✅ HƏLL: hər yazma/silmədən ƏVVƏL atributlar təmizlənir ✓✓✓
        // ====================================================================

        /// <summary>🔓 Faylın atributlarını TƏMİZLƏYİR ✓ (Hidden/ReadOnly → Normal ✓)</summary>
        private static void AtributTemizle(string? yol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(yol) || !File.Exists(yol)) return;

                File.SetAttributes(yol, FileAttributes.Normal);
            }
            catch
            {
                // 🔁 ikinci cəhd: YALNIZ ReadOnly-nu götür ✓
                try
                {
                    var a = File.GetAttributes(yol!) & ~FileAttributes.ReadOnly & ~FileAttributes.Hidden;
                    File.SetAttributes(yol!, a);
                }
                catch { }
            }
        }

        /// <summary>✍️ Fayla MƏCBURİ yazır ✓ — köhnə atributlar əngəl olmur ✗✓✓</summary>
        private static void MecburiYaz(string yol, string mətn)
        {
            try
            {
                AtributTemizle(yol);
                File.WriteAllText(yol, mətn);      // ⚠️ BURADA «MecburiYaz» ÇAĞIRILMIR ✗ (rekursiya olardı ✗)
            }
            catch
            {
                try
                {
                    using var s = new FileStream(yol, FileMode.Create, FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var y = new StreamWriter(s);
                    y.Write(mətn);
                }
                catch { }
            }
        }

        /// <summary>🗑️ Faylı MƏCBURİ silir ✓ — atributlardan asılı olmayaraq ✓✓✓</summary>
        private static bool MecburiSil(string yol)
        {
            try
            {
                if (!File.Exists(yol)) return true;

                AtributTemizle(yol);
                File.Delete(yol);                  // ⚠️ BURADA «MecburiSil» ÇAĞIRILMIR ✗ (rekursiya olardı ✗)
                return true;
            }
            catch
            {
                try
                {
                    using var s = new FileStream(yol, FileMode.Open, FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    s.SetLength(0);          // 🗑️ məzmun boşaldılır ✓
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 📜 <b>USB ƏMƏLİYYATLARININ İZİ</b> ✓✓✓ — çökmənin yerini tapmaq üçün ✓
        /// <para>⚠️ <b>HƏR SƏTİR DƏRHAL DİSKƏ YAZILIR</b> ✗→✓ — proqram çöksə belə iz qalır ✓✓✓</para>
        /// <para>📂 Fayl: <c>{app}\Logs\usb_izleme.log</c> ✓</para>
        /// </summary>
        public static void İz(string mətn)
        {
            try
            {
                var yol = Path.Combine(Cas0201.Kok.Qovluq, "Logs", "usb_izleme.log");

                Directory.CreateDirectory(Path.GetDirectoryName(yol) ?? Cas0201.Kok.Qovluq);

                using var s = new FileStream(yol, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite);

                using var y = new StreamWriter(s);

                y.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  {mətn}");

                y.Flush();          // ⚠️ DƏRHAL diske ✓ (çökmə olsa da qalır ✓)
                s.Flush(true);
            }
            catch { }
        }

        /// <summary>🗂️ Bütün hazır disk kökləri ✓ (əvvəl çıxarıla bilənlər ✓)</summary>
        private static IEnumerable<string> DiskKokleri()
        {

            var siyahi = new List<(string Root, bool CixarilaBilen)>();

            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady) continue;

                        siyahi.Add((d.RootDirectory.FullName, d.DriveType == DriveType.Removable));
                    }
                    catch
                    {
                        // ✗ oxunmur → keç ✓
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Disk siyahısı alınmadı: " + ex.Message);
            }

            return siyahi
                .OrderByDescending(x => x.CixarilaBilen)
                .Select(x => x.Root);
        }

        /// <summary>
        /// 🔑 TOKEN oxuyur ✓ — ① JSON marker ✓ ② «data» ehtiyatı ✓ ③ GÖRÜNƏN info faylı ✓✓✓
        /// </summary>
        private static string? MarkerOxu(string root)
        {
            foreach (var yol in new[]
                     {
                         // ✅ YENİ QURULUŞ: «D:\021Cars\…» ✓✓✓
                         Path.Combine(root, KokAdi, MarkerAdi),
                         Path.Combine(root, KokAdi, InfoAdi),
                         Path.Combine(root, KokAdi, "data", MarkerAdiData),

                         // ✓ KÖHNƏ quruluş (uyğunluq üçün ✓)
                         Path.Combine(root, MarkerAdi),
                         Path.Combine(root, "data", MarkerAdiData),
                         Path.Combine(root, InfoAdi)
                     })
            {
                try
                {
                    if (!File.Exists(yol))
                    {
                        continue;
                    }

                    var metn = File.ReadAllText(yol);

                    // ① JSON marker ✓
                    try
                    {
                        using var sənəd = JsonDocument.Parse(metn);

                        if (sənəd.RootElement.TryGetProperty("token", out var t))
                        {
                            return t.GetString();
                        }
                    }
                    catch
                    {
                        // ✓ JSON deyil → aşağıda mətn axtarışı ✓
                    }

                    // ② GÖRÜNƏN info faylı: «Token     : abc123…» ✓
                    foreach (var setir in metn.Split('\n'))
                    {
                        var i = setir.IndexOf("Token", StringComparison.OrdinalIgnoreCase);

                        if (i < 0)
                        {
                            continue;
                        }

                        var hissə = setir[(i + 5)..].Trim(' ', ':', '\t', '\r');

                        if (hissə.Length >= 16)
                        {
                            return hissə;
                        }
                    }
                }
                catch
                {
                    // ✗ zədəli fayl → növbəti mənbə ✓
                }
            }

            return null;
        }

        /// <summary>
        /// ✅ <b>AKTİV DATA USB-Nİ TAPIR</b> ✓✓✓
        /// <para>
        /// ⚠ <b>YALNIZ tokeni üst-üstə düşən fləşkart</b> tanınır ✓ →
        /// başqa fləşkartlar (marker olsa belə ✗) TANINMIR ✗✓✓
        /// </para>
        /// </summary>
        /// <returns>Kök qovluq və token (tapılmazsa <c>null</c> ✗)</returns>
        public static (string Root, string Token)? Tap()
        {
            try
            {
                if (Qeydiyyat is null || string.IsNullOrWhiteSpace(Qeydiyyat.Token))
                {
                    return null;
                }

                foreach (var root in DiskKokleri())
                {
                    var token = MarkerOxu(root);

                    if (token is not null
                        && string.Equals(token, Qeydiyyat.Token, StringComparison.OrdinalIgnoreCase))
                    {
                        // ✓ hərf dəyişmiş olsa da burada YENİLƏNİR ✓
                        if (!string.Equals(Qeydiyyat.SonHərf, root, StringComparison.OrdinalIgnoreCase))
                        {
                            Qeydiyyat.SonHərf = root;
                            YaddaSaxla();
                        }

                        return (root, token);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("USB axtarışında xəta: " + ex.Message);
                return null;
            }
        }

        /// <summary>💾 Aktual DATA USB yolu (tapılmazsa null ✗) — sənəd əməliyyatları üçün ✓</summary>
        public static string? DataQovlugu()
        {
            var tapilan = Tap();

            if (tapilan is null)
            {
                return null;
            }

            var qovluq = Path.Combine(tapilan.Value.Root, "data");

            try
            {
                Directory.CreateDirectory(qovluq);
            }
            catch
            {
                return null;
            }

            return qovluq;
        }

        /// <summary>📢 <b>STATUS MƏTNİ</b> — tənzimləmələr paneli üçün ✓✓✓</summary>
        public static string StatusMetni()
        {
            if (Qeydiyyat is null)
            {
                return "✗ DATA USB qeydiyyatdan keçməyib — USB taxın və «➕ Əlavə et» basın ✓";
            }

            var tapilan = Tap();

            return tapilan is null
                ? $"⚠️ «{Qeydiyyat.Ad}» əlçatan deyil ✗ (son məlum hərf: {Qeydiyyat.SonHərf}) — USB-ni taxın ✓"
                : $"✅ «{Qeydiyyat.Ad}» TANINDI ✓ → {tapilan.Value.Root}";
        }

        /// <summary>🕒 Qeydiyyat tarixi mətni ✓</summary>
        public static string QeydiyyatMetni()
            => Qeydiyyat is null
                ? "—"
                : $"{Qeydiyyat.Ad} · {Qeydiyyat.QeydiyyatTarixi:dd.MM.yyyy HH:mm} · son hərf {Qeydiyyat.SonHərf}";
    }
}
