// ============================================================================
//  💾 021Cars — FLƏŞKART DİNAMİK AŞKARLAMA (5/6)  ★ TƏLƏB №2 ★
// ----------------------------------------------------------------------------
//  ✅ Bütün disklər skan edilir ✓ (C: → Z: ✓)
//  ✅ Kök qovluqdaki  021cars_drive.lock  marker faylı tapılır ✓
//  ✅ Fləşkartın hərfi DİNAMİK təyin olunur ✓ (E: · F: · G: — fərq etmir ✓)
//  ✅ Fləşkart YOXDURSA proqram ÇÖKMÜR ✗ → zərif MƏHDUDİYYƏT ✓
//  ✅ Buludda saxlanılan yol NİSBİ olur ✓ → hərf dəyişsə sənəd tapılır ✓✓✓
// ============================================================================

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cas0201.Firebase
{
    /// <summary>💾 Fləşkart vəziyyəti ✓</summary>
    public enum DiskVeziyyeti
    {
        /// <summary>✅ Taxılıb və hazırdır ✓</summary>
        Hazir,

        /// <summary>🔌 Taxılmayıb ✗ — sənəd əməliyyatları məhduddur ✓</summary>
        Taxilmayib,

        /// <summary>⏳ Skan olunur ✓</summary>
        Skanda
    }

    /// <summary>
    /// 💾 <b>DİNAMİK FLƏŞKART AŞKARLAYICI</b> ✓✓✓
    /// <para>
    /// <b>Marker:</b> <c>{Disk}:\021cars_drive.lock</c> ✓ —
    /// bu faylın olduğu disk <b>021Cars fləşkartıdır</b> ✓✓✓
    /// </para>
    /// </summary>
    public sealed class UsbDriveDetector
    {
        /// <summary>🔖 Marker fayl adı ✓ (DƏYİŞMƏZ ✗)</summary>
        public const string MarkerAdi = "021cars_drive.lock";

        /// <summary>🗂️ Fləşkartdaki kök qovluq ✓</summary>
        public const string KokQovluqAdi = "021Cars";

        /// <summary>📍 Tapılan kök ✓ (məs. <c>F:\021Cars</c>) — yoxdursa <c>null</c> ✓</summary>
        public string? KokYol { get; private set; }

        /// <summary>💽 Fləşkartın hərfi ✓ (məs. <c>"F:"</c>)</summary>
        public string? Herf =>
            KokYol is null ? null : Path.GetPathRoot(KokYol)?.TrimEnd('\\');

        /// <summary>📊 Cari vəziyyət ✓ (UI indikatoru ✓)</summary>
        public DiskVeziyyeti Veziyyet { get; private set; } = DiskVeziyyeti.Taxilmayib;

        /// <summary>✅ Fləşkart hazırdır? ✓ (sənəd əməliyyatı mümkündür ✓)</summary>
        public bool Hazir => Veziyyet == DiskVeziyyeti.Hazir && KokYol is not null;

        /// <summary>🔖 Marker məzmunu ✓ (token ✓ — sahiblik yoxlaması ✓)</summary>
        public string? UsbToken { get; private set; }

        /// <summary>📣 Vəziyyət dəyişdi ✓ (UI yeniləməsi ✓)</summary>
        public event Action? VeziyyetDeyisdi;

        // --------------------------------------------------------------------
        //  🔍 AŞKARLAMA ✓
        // --------------------------------------------------------------------

        /// <summary>
        /// 🔎 <b>BÜTÜN DİSKLƏRİ SKAN EDİR</b> ✓✓✓ (C: → Z: ✓)
        /// <para>Hər diskin kökündə marker axtarır ✓ · tapılsa hərf DİNAMİK təyin olunur ✓</para>
        /// </summary>
        public bool SkanEt()
        {
            try
            {
                Veziyyet = DiskVeziyyeti.Skanda;
                var tapildi = false;

                foreach (var disk in DiskleriAl())
                {
                    try
                    {
                        // 🔍 MARKER 3 YERDƏ AXTARILIR ✓✓✓
                        //   ① {Disk}\021cars_drive.lock           ✓ (kök ✓)
                        //   ② {Disk}\021Cars\021cars_drive.lock    ✓ ← ★ TƏTBİQİN YAZDIĞI YER ★
                        //   ③ {Disk}\data\021cars_drive.lock       ✓ (ehtiyat ✓)
                        var marker = Path.Combine(disk, MarkerAdi);

                        if (!File.Exists(marker))
                        {
                            var kökİçi = Path.Combine(disk, KokQovluqAdi, MarkerAdi);
                            var dataİçi = Path.Combine(disk, "data", MarkerAdi);

                            if (File.Exists(kökİçi)) marker = kökİçi;
                            else if (File.Exists(dataİçi)) marker = dataİçi;
                            else continue;
                        }

                        // ✅ MARKER TAPILDI ✓✓✓
                        KokYol = Path.Combine(disk, KokQovluqAdi);
                        UsbToken = MarkerOxu(marker);

                        if (!Directory.Exists(KokYol))
                        {
                            Directory.CreateDirectory(KokYol);
                        }

                        Veziyyet = DiskVeziyyeti.Hazir;
                        VeziyyetDeyisdi?.Invoke();

                        AppLogger.Melumat($"💾 FLƏŞKART TAPILDI ✓ → {KokYol}");
                        tapildi = true;
                        break; // 🛑 İlk marker kifayətdir ✓
                    }
                    catch (Exception ex)
                    {
                        // 🛡️ Disk oxunmur (CD-ROM ✓ · şəbəkə diski ✗) → keçirik ✓
                        AppLogger.Xeberdarliq($"💽 Disk atlandı: {disk} → {ex.Message}");
                    }
                }

                if (!tapildi)
                {
                    KokYol = null;
                    UsbToken = null;
                    Veziyyet = DiskVeziyyeti.Taxilmayib;

                    AppLogger.Xeberdarliq(
                        $"🔌 Fləşkart TAXILMAYIB ✗ — marker ({MarkerAdi}) tapılmadı ✓ " +
                        "Sənəd əməliyyatları məhdudlaşdırıldı ✓");
                }

                VeziyyetDeyisdi?.Invoke();
                return tapildi;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "USB skan");
                Veziyyet = DiskVeziyyeti.Taxilmayib;
                return false;
            }
        }

        /// <summary>⏱️ Arxa fonda vaxtaşırı skan ✓ (5 saniyə ✓)</summary>
        public async Task İzleAsync(int fasiləSaniyə = 5, CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(2, fasiləSaniyə)), ct)
                        .ConfigureAwait(false);

                    SkanEt();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { AppLogger.Xeta(ex, "USB izləmə"); }
            }
        }

        // --------------------------------------------------------------------
        //  💽 DİSK SİYAHISI + MARKER ✓
        // --------------------------------------------------------------------

        /// <summary>💽 Bütün hazır diskləri qaytarır ✓ (C: → Z: ✓ · çıxarıla bilənlər əvvəldə ✓)</summary>
        public static string[] DiskleriAl()
        {
            var siyahı = new System.Collections.Generic.List<string>();

            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady) continue; // 💤 Boş kartoxa / CD ✓

                        // ⛔ YALNIZ sabit və çıxarıla bilən disklər ✓
                        if (d.DriveType != DriveType.Fixed &&
                            d.DriveType != DriveType.Removable) continue;

                        if (d.TotalSize <= 0) continue;

                        siyahı.Add(d.Name);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Xeberdarliq($"💽 Disk oxunmadı: {ex.Message}");
                    }
                }

                // 🔢 Çıxarıla bilənlər (E: · F: ✓) ƏVVƏL · C: ən sonda ✓
                return siyahı
                    .OrderBy(s =>
                    {
                        try
                        {
                            if (new DriveInfo(s).DriveType == DriveType.Removable) return 0;
                            return s.StartsWith("C:", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
                        }
                        catch { return 3; }
                    })
                    .ToArray();
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "disk siyahısı");
                return Array.Empty<string>();
            }
        }

        /// <summary>🔖 Marker faylından token oxuyur ✓ (yoxdursa yaradır ✓)</summary>
        private static string? MarkerOxu(string markerYolu)
        {
            try
            {
                var mətn = File.ReadAllText(markerYolu).Trim();

                if (mətn.Contains("TOKEN=", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var sətr in mətn.Split('\n'))
                    {
                        if (sətr.TrimStart().StartsWith("TOKEN=", StringComparison.OrdinalIgnoreCase))
                        {
                            return sətr.Trim()["TOKEN=".Length..].Trim();
                        }
                    }
                }

                return mətn.Length >= 8 ? mətn : null;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "marker oxu: " + markerYolu);
                return null;
            }
        }

        /// <summary>
        /// 🆕 <b>Fləşkartı HAZIRLAYIR</b> ✓✓✓ — marker + qovluq strukturu yaradır ✓
        /// <para>⚠ YALNIZ boş/naməlum fləşkart üçün çağırın ✓ (mövcud data silinir ✗)</para>
        /// </summary>
        public string? Hazirla(string token, string cihaz = "setup")
        {
            try
            {
                string? hedef = null;

                foreach (var d in DiskleriAl())
                {
                    try
                    {
                        if (d.StartsWith("C:", StringComparison.OrdinalIgnoreCase)) continue;
                        if (new DriveInfo(d).DriveType != DriveType.Removable) continue;

                        hedef = d;
                        break;
                    }
                    catch { }
                }

                if (hedef is null)
                {
                    AppLogger.Xeberdarliq("💾 Çıxarıla bilən disk tapılmadı ✗");
                    return null;
                }

                KokYol = Path.Combine(hedef, KokQovluqAdi);
                Directory.CreateDirectory(KokYol);

                foreach (var alt in new[]
                {
                    "Media", "Senedler", "Hesabatlar", "Yedekler"
                })
                {
                    Directory.CreateDirectory(Path.Combine(KokYol, alt));
                }

                File.WriteAllText(
                    Path.Combine(hedef, MarkerAdi),
                    "# 021Cars — FLƏŞKART MARKERİ (bu faylı SİLMƏYİN ✗)" + Environment.NewLine +
                    "# Bu fayl olan disk 021Cars fləşkartı sayılır ✓" + Environment.NewLine +
                    "TOKEN=" + token + Environment.NewLine +
                    "CIHAZ=" + cihaz + Environment.NewLine +
                    "TARIX=" + FirebaseOptions.UtcIndi() + Environment.NewLine);

                UsbToken = token;
                Veziyyet = DiskVeziyyeti.Hazir;

                AppLogger.Melumat($"💾 Fləşkart hazırlandı ✓ → {KokYol}");
                VeziyyetDeyisdi?.Invoke();

                return KokYol;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "fləşkart hazırlama");
                return null;
            }
        }

        // --------------------------------------------------------------------
        //  🛣️ YOLLAR — NİSBİ (relative) ✓✓✓ ƏSAS TƏLƏB ★
        //  Buludda YALNIZ nisbi yol saxlanılır ✓ → fləşkart hərfi dəyişsə də işləyir ✓
        // --------------------------------------------------------------------

        /// <summary>
        /// ➡️ Tam yolu <b>NİSBİ yola</b> çevirir ✓✓✓
        /// <example><c>F:\021Cars\Media\Avtomobil\001 - Sonata\a.jpg</c> → <c>Media\Avtomobil\001 - Sonata\a.jpg</c> ✓</example>
        /// </summary>
        public string? NisbiYol(string? tamYol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tamYol) || KokYol is null) return null;

                var tam = Path.GetFullPath(tamYol);
                var kok = Path.GetFullPath(KokYol).TrimEnd('\\') + "\\";

                return tam.StartsWith(kok, StringComparison.OrdinalIgnoreCase)
                    ? tam[kok.Length..]
                    : tam; // ⚠ Fləşkart xaricindədirsə olduğu kimi ✓
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "nisbi yol: " + tamYol);
                return null;
            }
        }

        /// <summary>⬅️ Nisbi yolu <b>TAM yola</b> çevirir ✓ (<c>null</c> → fləşkart yoxdur ✓)</summary>
        public string? TamYol(string? nisbiYol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nisbiYol)) return null;
                if (KokYol is null) return null; // 🔌 fləşkart yoxdur ✗
                if (Path.IsPathRooted(nisbiYol)) return nisbiYol;

                return Path.Combine(KokYol, nisbiYol);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "tam yol: " + nisbiYol);
                return null;
            }
        }

        /// <summary>📂 Qovluğu yaradır ✓ (fləşkart yoxdursa <c>false</c> ✓ — ÇÖKMƏ YOX ✗)</summary>
        public bool QovluqYarat(string altYol, out string? tamYol)
        {
            tamYol = TamYol(altYol);

            if (tamYol is null) return false; // 🔌 zərif məhdudiyyət ✓

            try
            {
                Directory.CreateDirectory(tamYol);
                return true;
            }
            catch (Exception ex)
            {
                // 🔌 Fləşkart FİZİKİ OLARAQ ÇIXARILIB / əlçatmazdır ✗ →
                //    bu, XƏTA DEYİL ✗ → loq «Could not find a part of the path
                //    'D:\…'» ilə DOLMUR ✗✓✓ (sadəcə məlumat yazılır ✓)
                if (ex is IOException)
                {
                    AppLogger.Melumat("🔌 Fləşkart əlçatmazdır ✗ — qovluq yaradılmadı: " + tamYol);
                }
                else
                {
                    AppLogger.Xeta(ex, "qovluq yarat: " + tamYol);
                }

                tamYol = null;
                return false;
            }
        }

        /// <summary>
        /// 🚗 Avtomobilin media/sənəd qovluğu ✓ (nisbi ✓)
        /// <example><c>Media\Avtomobil\001 - Hyundai Sonata - 99QE103\</c> ✓</example>
        /// </summary>
        public string NisbiQovluq(string kateqoriya, int siraNomresi, string marka, string qeydiyyatNisani)
        {
            var ad = $"{siraNomresi:D3} - {Temizle(marka)} - {Temizle(qeydiyyatNisani)}";
            return Path.Combine(kateqoriya, "Avtomobil", ad);
        }

        /// <summary>🧹 Fayl adı üçün təhlükəsiz mətn ✓ (qadağan simvollar silinir ✗)</summary>
        public static string Temizle(string? ad)
        {
            if (string.IsNullOrWhiteSpace(ad)) return "namelum";

            var təmiz = ad.Trim();

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                təmiz = təmiz.Replace(c, '_');
            }

            return təmiz.Length > 60 ? təmiz[..60].Trim() : təmiz;
        }

        // --------------------------------------------------------------------
        //  💾 FAYL ƏMƏLİYYATLARI — fləşkart yoxdursa ZƏRİF MƏHDUDİYYƏT ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>✅ Fayl fləşkartda mövcuddur? ✓ (fləşkart yoxdursa <c>false</c> ✓)</summary>
        public bool FaylVar(string? nisbiYol)
        {
            try
            {
                var tam = TamYol(nisbiYol);
                return tam is not null && File.Exists(tam);
            }
            catch { return false; }
        }

        /// <summary>
        /// 💾 Faylı <b>YALNIZ fləşkartda</b> saxlayır ✓✓✓ — qaytarır: <b>NİSBİ YOL</b> ✓
        /// <para>
        /// ⚠ Fləşkart yoxdursa <c>null</c> qaytarır ✓ · <b>EXCEPTION ATILMIR</b> ✗✓✓
        /// (buluddaki <c>storedPath</c> sahəsi bu nisbi yolu saxlayır ✓)
        /// </para>
        /// </summary>
        public string? FayliSaxla(string nisbiQovluq, string faylAdi, byte[] məzmun)
        {
            try
            {
                // 🔌 ① FLƏŞKART YOXDUR → məhdudiyyət ✓ (çökmə YOX ✗)
                if (!Hazir || KokYol is null)
                {
                    AppLogger.Xeberdarliq(
                        $"🔌 Fləşkart yoxdur ✗ — «{faylAdi}» saxlanılmadı ✓ (əməliyyat məhdudlaşdırıldı ✓)");
                    return null;
                }

                // 📂 ② Qovluğu yarat ✓
                var qovluqNisbi = Path.Combine(nisbiQovluq);
                if (!QovluqYarat(qovluqNisbi, out var tamQovluq) || tamQovluq is null)
                {
                    return null;
                }

                // 🧹 ③ Fayl adını təhlükəsiz et ✓
                var ad = Temizle(Path.GetFileNameWithoutExtension(faylAdi)) +
                         Path.GetExtension(faylAdi);

                var tamFayl = Path.Combine(tamQovluq, ad);

                // 🔢 ④ Ad toqquşması → «(2)» ✓ (mövcud fayl SİLİNMİR ✗✓✓)
                var sayğac = 1;

                while (File.Exists(tamFayl))
                {
                    sayğac++;
                    tamFayl = Path.Combine(tamQovluq,
                        $"{Path.GetFileNameWithoutExtension(ad)} ({sayğac}){Path.GetExtension(ad)}");
                }

                // 💾 ⑤ Yaz ✓
                File.WriteAllBytes(tamFayl, məzmun);

                var nisbi = NisbiYol(tamFayl);

                AppLogger.Melumat($"💾 Fayl saxlanıldı ✓ → {nisbi} ({məzmun.Length / 1024} KB)");
                return nisbi;
            }
            catch (IOException ex)
            {
                // 🛡️ Disk dolu / çıxarıldı ✗ → zərif xəbərdarlıq ✓
                AppLogger.Xeberdarliq($"💾 Disk xətası ✗ — {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "fayl saxlama: " + faylAdi);
                return null;
            }
        }

        /// <summary>📖 Faylı oxuyur ✓ (yoxdursa/fləşkart yoxdursa <c>null</c> ✓)</summary>
        public byte[]? FayliOxu(string? nisbiYol)
        {
            try
            {
                if (!Hazir) return null; // 🔌 zərif məhdudiyyət ✓

                var tam = TamYol(nisbiYol);

                if (tam is null || !File.Exists(tam)) return null;

                return File.ReadAllBytes(tam);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "fayl oxu: " + nisbiYol);
                return null;
            }
        }

        /// <summary>🗑️ Faylı fləşkartdan silir ✓ (buluddaki qeyd SOFT-DELETE olunur ✓)</summary>
        public bool FayliSil(string? nisbiYol)
        {
            try
            {
                var tam = TamYol(nisbiYol);

                if (tam is null || !File.Exists(tam)) return false;

                File.Delete(tam);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "fayl sil: " + nisbiYol);
                return false;
            }
        }

        /// <summary>📊 Fləşkart statistikası ✓ (UI üçün ✓)</summary>
        public (long Bayt, int Fayl, long BosYer, long Tutum) Statistika()
        {
            try
            {
                if (!Hazir || KokYol is null) return (0, 0, 0, 0);

                var fayllar = Directory.GetFiles(KokYol, "*", SearchOption.AllDirectories);
                long cəm = 0;

                foreach (var f in fayllar)
                {
                    try { cəm += new FileInfo(f).Length; } catch { }
                }

                var disk = new DriveInfo(Path.GetPathRoot(KokYol)!);
                return (cəm, fayllar.Length, disk.AvailableFreeSpace, disk.TotalSize);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "USB statistika");
                return (0, 0, 0, 0);
            }
        }



    }
}
