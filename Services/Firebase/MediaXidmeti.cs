// ============================================================================
//  📎 021Cars — MEDIA/SƏNƏD XİDMƏTİ (09)  ★ TƏLƏB №2 ★
// ----------------------------------------------------------------------------
//  ✅ AĞIR FAYLLAR (PDF · ŞƏKİL) YALNIZ FLƏŞKARTDA ✓✓✓
//  ✅ BULUDDA YALNIZ NİSBİ YOL (storedPath) ✓ → RAM/disk dolmur ✗
//  ✅ FLƏŞKART YOXDURSA: yalnız sənəd əməliyyatı məhdud ✓ (proqram İŞLƏYİR ✓)
//  ✅ Fləşkart hərfi dəyişsə (E: → F:) HEÇ NƏ POZULMUR ✗✓✓ — yol nisbidir ✓
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cas0201.Firebase.Modeller;

namespace Cas0201.Firebase
{
    /// <summary>📎 <b>MEDIA/SƏNƏD XİDMƏTİ</b> ✓✓✓</summary>
    public sealed class MediaXidmeti
    {
        private readonly UsbDriveDetector _usb;
        private readonly FirebaseRepository<MediaFayl> _repo;
        private readonly FirebaseOptions _o;

        public MediaXidmeti(
            UsbDriveDetector usb,
            FirebaseRepository<MediaFayl> repo,
            FirebaseOptions options)
        {
            _usb = usb ?? throw new ArgumentNullException(nameof(usb));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _o = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>✅ Sənəd əməliyyatları mümkündür? ✓ (fləşkart taxılıb? ✓)</summary>
        public bool Hazirdir => _usb.Hazir;

        /// <summary>💬 UI üçün vəziyyət mətni ✓</summary>
        public string VeziyyetMetni =>
            _usb.Hazir
                ? $"💾 Fləşkart hazır ✓ ({_usb.KokYol})"
                : "🔌 Fləşkart yoxdur ✗ — şəkil/PDF yükləmək mümkün deyil ✗";

        // --------------------------------------------------------------------
        //  📤 SƏNƏD ƏLAVƏ ET ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>
        /// 📤 Sənədi əlavə edir ✓✓✓ — <b>İKİ ADDIM</b>:
        /// <list type="number">
        ///   <item>① Fayl → <b>fləşkart</b> (<c>{Disk}:\021Cars\Media\…</c> ✓)</item>
        ///   <item>② Metadata → <b>bulud</b> (<c>021Cars/media/{id}</c> ✓ — yalnız NİSBİ yol ✓)</item>
        /// </list>
        /// <returns>
        /// ✅ Metadata ✓ · <c>null</c> — fləşkart yoxdur ✗ <b>(proqram ÇÖKMÜR ✗ — çağıran UI xəbərdarlıq göstərir ✓)</b>
        /// </returns>
        /// </summary>
        public async Task<MediaFayl?> ElaveEtAsync(
            string refType,
            string refId,
            string fileName,
            byte[] məzmun,
            string kateqoriya = "Senedler",
            int siraNomresi = 0,
            string marka = "",
            string qeydiyyatNisani = "",
            string? qeyd = null,
            CancellationToken ct = default)
        {
            try
            {
                if (məzmun is null || məzmun.Length == 0) return null;

                // 🔌 ① FLƏŞKART YOXDUR → zərif məhdudiyyət ✓✓✓
                if (!_usb.Hazir)
                {
                    AppLogger.Xeberdarliq(
                        $"🔌 Fləşkart yoxdur ✗ — «{fileName}» saxlanılmadı ✓ " +
                        "(istifadəçi fləşkartı taxmalıdır ✓)");
                    return null;
                }

                // 📂 ② Hədəf qovluq ✓ (məs. Senedler/Avtomobil/001 - Hyundai Sonata - 99QE103 ✓)
                var nisbiQovluq = siraNomresi > 0
                    ? _usb.NisbiQovluq(kateqoriya, siraNomresi, marka, qeydiyyatNisani)
                    : kateqoriya;

                // 💾 ③ FAYL → FLƏŞKART ✓ (nisbi yol qaytarır ✓)
                var nisbiYol = _usb.FayliSaxla(nisbiQovluq, fileName, məzmun);

                if (string.IsNullOrWhiteSpace(nisbiYol)) return null;

                // ☁️ ④ METADATA → BULUD ✓ (yalnız NİSBİ yol ✓✓✓)
                var media = new MediaFayl
                {
                    RefType = refType,
                    RefId = refId,
                    FileName = System.IO.Path.GetFileName(nisbiYol),
                    StoredPath = nisbiYol,
                    SizeBytes = məzmun.LongLength,
                    Tarix = FirebaseOptions.UtcIndi(),
                    UsbToken = _usb.UsbToken ?? "",
                    Qeyd = qeyd ?? ""
                };

                var uğur = await _repo.YazAsync(media, ct).ConfigureAwait(false);

                AppLogger.Melumat(
                    $"📤 Sənəd əlavə edildi ✓ — {media.FileName} " +
                    $"({məzmun.Length / 1024} KB ✓) · nisbi yol: {nisbiYol} ✓ · bulud: {(uğur ? "✓" : "növbədə ⏳")}");

                return media;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "sənəd əlavə: " + fileName);
                return null;
            }
        }

        // --------------------------------------------------------------------
        //  📖 SƏNƏDİ AÇ ✓ — fləşkartdan oxunur ✓
        // --------------------------------------------------------------------

        /// <summary>📖 Sənədin baytlarını qaytarır ✓ (fləşkart yoxdursa <c>null</c> ✓ — ÇÖKMƏ YOX ✗)</summary>
        public byte[]? FayliAc(MediaFayl media)
        {
            try
            {
                if (media is null || string.IsNullOrWhiteSpace(media.StoredPath)) return null;

                if (!_usb.Hazir)
                {
                    AppLogger.Xeberdarliq(
                        $"🔌 Fləşkart yoxdur ✗ — «{media.FileName}» açıla bilməz ✓ " +
                        $"(nisbi yol: {media.StoredPath} ✓ — fləşkartı taxın ✓)");
                    return null;
                }

                return _usb.FayliOxu(media.StoredPath);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "sənəd aç: " + media?.FileName);
                return null;
            }
        }

        /// <summary>🛣️ Sənədin <b>TAM yolunu</b> qaytarır ✓ (PDF çapı · WebView2 · OpenFile ✓)</summary>
        public string? TamYol(MediaFayl media) => _usb.TamYol(media?.StoredPath);

        /// <summary>✅ Fayl fiziki olaraq fləşkartdadır? ✓</summary>
        public bool Mövcuddur(MediaFayl media) => _usb.FaylVar(media?.StoredPath);

        // --------------------------------------------------------------------
        //  📋 SİYAHI ✓
        // --------------------------------------------------------------------

        /// <summary>📋 Obyektə bağlı sənədlər ✓ (avtomobil ✓ müştəri ✓ kredit ✓)</summary>
        public async Task<List<MediaFayl>> SiyahAsync(
            string refType, string refId, CancellationToken ct = default)
        {
            try
            {
                var hamısı = await _repo.HamisiniAlAsync(ct: ct).ConfigureAwait(false);

                return hamısı
                    .Where(m => string.Equals(m.RefType, refType, StringComparison.OrdinalIgnoreCase)
                             && m.RefId == refId)
                    .OrderByDescending(m => m.Tarix)
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "sənəd siyahısı");
                return new List<MediaFayl>();
            }
        }

        /// <summary>🗂️ UI cədvəli üçün ✓ — hər sətrə «fləşkartda var? ✓» bayrağı ✓</summary>
        public async Task<List<(MediaFayl Media, bool Movcuddur, string TamYol)>>
            CedvelHazirlaAsync(string refType, string refId, CancellationToken ct = default)
        {
            var neticə = new List<(MediaFayl, bool, string)>();

            try
            {
                foreach (var m in await SiyahAsync(refType, refId, ct).ConfigureAwait(false))
                {
                    var tam = TamYol(m);
                    neticə.Add((m, !string.IsNullOrWhiteSpace(tam) && _usb.FaylVar(m.StoredPath),
                                tam ?? m.StoredPath));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "sənəd cədvəli");
            }

            return neticə;
        }

        // --------------------------------------------------------------------
        //  🗑️ SİL ✓ (SOFT DELETE ✓ — buluddan HEÇ VAXT silinmir ✗)
        // --------------------------------------------------------------------

        /// <summary>
        /// 🗑️ Sənədi silir ✓
        /// <param name="fizikiFaylSil">
        /// <c>true</c> → fləşkartdaki fayl da silinir ✓ (disk yer boşalır ✓)<br/>
        /// <c>false</c> (default) → yalnız buludda SOFT-DELETE ✓✓✓ (bərpa mümkündür ✓)
        /// </param>
        /// </summary>
        public async Task<bool> SilAsync(
            MediaFayl media, bool fizikiFaylSil = false, CancellationToken ct = default)
        {
            try
            {
                if (media is null) return false;

                var uğur = await _repo.SoftSilAsync(media.Id, ct).ConfigureAwait(false);

                if (fizikiFaylSil)
                {
                    _usb.FayliSil(media.StoredPath); // ⚠ geri qaytarıla bilməz ✗
                }

                AppLogger.Melumat($"🗑️ Sənəd silindi ✓ — {media.FileName} (fiziki: {fizikiFaylSil})");
                return uğur;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "sənəd sil");
                return false;
            }
        }

        /// <summary>♻️ Silinmiş sənədi bərpa edir ✓</summary>
        public async Task<bool> BerpaEtAsync(MediaFayl media, CancellationToken ct = default) =>
            await _repo.BerpaEtAsync(media, ct).ConfigureAwait(false);

        // --------------------------------------------------------------------
        //  📊 STATİSTİKA + BAXIM ✓
        // --------------------------------------------------------------------

        /// <summary>📊 Fləşkart + sənəd statistikası ✓ (UI paneli ✓)</summary>
        public (int FaylSayi, long UmumiBayt, long BosYer, long Tutum, bool Hazir) Statistika()
        {
            try
            {
                var (bayt, fayl, bos, tutum) = _usb.Statistika();
                return (fayl, bayt, bos, tutum, _usb.Hazir);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "media statistika");
                return (0, 0, 0, 0, false);
            }
        }

        /// <summary>🧹 Fləşkartda YETİM qalmış faylları (buludda qeydi olmayan) tapır ✓</summary>
        public async Task<List<string>> YetimFayllarAsync(CancellationToken ct = default)
        {
            var neticə = new List<string>();

            try
            {
                if (!_usb.Hazir || _usb.KokYol is null) return neticə;

                var qeydlər = (await _repo.HamisiniAlAsync(ct: ct).ConfigureAwait(false))
                    .Select(m => (m.StoredPath ?? "").Replace('/', '\\').ToLowerInvariant())
                    .ToHashSet();

                foreach (var f in System.IO.Directory.GetFiles(
                    _usb.KokYol, "*", System.IO.SearchOption.AllDirectories))
                {
                    var nisbi = (_usb.NisbiYol(f) ?? "").ToLowerInvariant();

                    if (nisbi.Length > 0 && !qeydlər.Contains(nisbi))
                    {
                        neticə.Add(nisbi);
                    }
                }

                if (neticə.Count > 0)
                {
                    AppLogger.Xeberdarliq($"🧹 {neticə.Count} yetim fayl tapıldı ✓ (buludda qeydi yoxdur ✗)");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "yetim fayllar");
            }

            return neticə;
        }


    }
}
