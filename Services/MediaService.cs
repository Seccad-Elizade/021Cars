using System.Diagnostics;
using System.IO;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class MediaService : IMediaService
    {
        private readonly IRepository<MediaAttachment> _attachments;
        private readonly ILogger<MediaService> _logger;

        public MediaService(IRepository<MediaAttachment> attachments, ILogger<MediaService> logger)
        {
            _attachments = attachments;
            _logger = logger;
        }

        /// <summary>
        /// 🏷️ <b>QOVLUQ ADI PROVAYDERİ</b> ✓✓✓
        /// <para>
        /// <c>(refType, refId) → «001 - Hyundai Sonata - 99QE103»</c> ✓
        /// — Sıra № · Marka (model) · Dövlət qeydiyyat nişanı ✓
        /// </para>
        /// <para>
        /// ⚠ Verilməzsə və ya nəticə boşdursa → <c>refId</c> istifadə olunur ✓
        /// (əvvəl qovluq adı YALNIZ «61» kimi rəqəm idi ✗✓✓)
        /// </para>
        /// </summary>
        public static Func<string, int, string?>? QovluqAdiProvider { get; set; }

        /// <summary>
        /// 🗂️ <b>TƏHLÜKƏSİZ QOVLUQ ADI</b> ✓✓✓ —
        /// «001 - Hyundai Sonata - 99QE103» ✓ (qadağan simvollar təmizlənir ✓)
        /// </summary>
        private static string QovluqAdi(string refType, int refId)
        {
            try
            {
                var ad = QovluqAdiProvider?.Invoke(refType, refId);

                if (string.IsNullOrWhiteSpace(ad))
                {
                    return refId.ToString();      // ✓ ehtiyat: «61» ✓
                }

                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    ad = ad.Replace(c, '_');
                }

                ad = ad.Trim().TrimEnd('.');

                return ad.Length == 0 ? refId.ToString() : ad;
            }
            catch
            {
                return refId.ToString();
            }
        }

        /// <summary>
        /// 📁 <b>BÜTÜN FAYLLARIN SAXLANDIĞI KÖK QOVLUQ</b> ✓✓✓
        /// <para>
        /// ✅ DATA USB TANINMIŞSA → <c>D:\021Cars\Media</c> ✓ (fayllar USB-də ✓)
        /// <br/>✗ Əks halda → yerli <c>%LocalAppData%\EnterpriseAeroStudio\Media</c> ✓
        /// (USB çıxarılsa proqram ÇÖKMÜR ✗ — yerli qovluqla işləyir ✓)
        /// </para>
        /// </summary>
        private static string RootDirectory
        {
            get
            {
                try
                {
                    // ============================================================
                    //  ✅ ƏVVƏLCƏ USB ✓ — sənəd · şəkil · PDF hamısı USB-də ✓✓✓
                    //  (əvvəl YALNIZ yerli qovluq idi ✗ → USB-də fayl görünmürdü ✗✓✓)
                    // ============================================================
                    var usb = DataUsbService.MediaQovlugu();

                    if (!string.IsNullOrWhiteSpace(usb))
                    {
                        return usb;
                    }
                }
                catch
                {
                    // ✓ USB yoxlanıla bilmədisə yerli qovluğa düşürük ✓
                }

                return Path.Combine(
                    Cas0201.Kok.Qovluq,
                    "EnterpriseAeroStudio",
                    "Media");
            }
        }

        public string FileFilter =>
            "Sənəd və şəkillər|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.doc;*.docx;*.xls;*.xlsx;*.txt;*.rtf|" +
            "Şəkillər|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|" +
            "Sənədlər|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.txt;*.rtf|" +
            "Bütün fayllar|*.*";

        public async Task<IReadOnlyList<MediaAttachment>> GetAsync(string refType, int refId, CancellationToken cancellationToken = default)
        {
            var items = await _attachments.FindAsync(a => a.RefType == refType && a.RefId == refId, cancellationToken);

            // ================================================================
            //  ✅ KÖHNƏ GUID ADLARI ORİJİNAL ADA QAYTARILIR (avtomatik ✓✓✓)
            // ----------------------------------------------------------------
            //  ⚠ Əvvəl əlavə edilmiş faylların adı «20260926_194917_dfd5…pdf» ✗
            //    idi → sənəd açılanda ORİJİNAL ad bərpa olunur ✓✓✓
            // ================================================================
            var duzeldildi = false;

            foreach (var item in items)
            {
                if (KohneAdDuzelt(item))
                {
                    duzeldildi = true;
                }
            }

            if (duzeldildi)
            {
                await _attachments.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("🔧 Köhnə GUID adlı sənədlər ORİJİNAL adına qaytarıldı ✓");
            }

            return items.OrderByDescending(a => a.Tarix).ThenByDescending(a => a.Id).ToList();
        }

        /// <summary>
        /// 🔧 Köhnə <b>GUID adlı</b> faylı <b>ORİJİNAL adına</b> qaytarır ✓✓✓
        /// <para>Yalnız köhnə format uyğun gələrsə (<c>20260926_194917_&lt;32 hex&gt;</c>) ✓</para>
        /// </summary>
        private static bool KohneAdDuzelt(MediaAttachment item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.StoredPath)
                    || string.IsNullOrWhiteSpace(item.FileName)
                    || !File.Exists(item.StoredPath))
                {
                    return false;
                }

                var kohneAd = Path.GetFileNameWithoutExtension(item.StoredPath);
                var hisseler = kohneAd.Split('_');

                // ✓ köhnə format: tarix _ saat _ 32 simvollu GUID ✓
                if (hisseler.Length != 3 || hisseler[2].Length != 32)
                {
                    return false;
                }

                var folder = Path.GetDirectoryName(item.StoredPath);

                if (string.IsNullOrWhiteSpace(folder))
                {
                    return false;
                }

                var hedef = UniquePath(folder, item.FileName);

                if (string.Equals(hedef, item.StoredPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                File.Move(item.StoredPath, hedef);
                item.StoredPath = hedef;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<int> AddAsync(string refType, int refId, IReadOnlyList<string> sourcePaths, CancellationToken cancellationToken = default)
        {
            if (refId <= 0 || sourcePaths is null || sourcePaths.Count == 0)
            {
                return 0;
            }

            var folder = Path.Combine(RootDirectory, refType, QovluqAdi(refType, refId));
            Directory.CreateDirectory(folder);

            var added = 0;
            foreach (var source in sourcePaths)
            {
                try
                {
                    if (!File.Exists(source))
                    {
                        continue;
                    }

                    // ================================================================
                    //  ✅ ORİJİNAL FAYL ADI QORUNUR ✗✓✓
                    // ----------------------------------------------------------------
                    //  ⚠ ƏVVƏL fayl GUID ilə saxlanılırdı ✗:
                    //    «20260926_194917_dfd56ee4d7114a639db54f3fa149f2e6.pdf» ✗
                    //    → ORİJİNAL ad İTİRDİ ✗ (istifadəçi faylı TANIMIRDI ✗✓✓)
                    //  ✅ İNDİ: orijinal ad olduğu kimi saxlanılır ✓:
                    //    «--98--Opel Astra (77-CT-921) 2008il.pdf» ✓✓✓
                    //    ⚠ Eyni ad artıq varsa → «… (2).pdf» kimi nömrələnir ✓
                    // ================================================================
                    var target = UniquePath(folder, Path.GetFileName(source));

                    File.Copy(source, target, overwrite: false);

                    await _attachments.AddAsync(new MediaAttachment
                    {
                        RefType = refType,
                        RefId = refId,
                        FileName = Path.GetFileName(source),
                        StoredPath = target,
                        SizeBytes = new FileInfo(target).Length,
                        Tarix = DateTime.Today
                    }, cancellationToken);

                    added++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fayl kopyalana bilmədi: {Path}", source);
                }
            }

            if (added > 0)
            {
                await _attachments.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("{Count} fayl əlavə edildi ({RefType} #{RefId}).", added, refType, refId);
            return added;
        }

        /// <summary>
        /// 🗂️ Faylın <b>ORİJİNAL ADINI QORUYUR</b> ✓✓✓
        /// <para>
        /// Eyni adlı fayl artıq varsa «… (2)», «… (3)» kimi nömrələnir ✓ —
        /// beləliklə HƏM ad saxlanılır ✓ HƏM fayllar bir-birini əvəz etmir ✓✓✓
        /// </para>
        /// </summary>
        private static string UniquePath(string folder, string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "sened";
            }

            var ad = Path.GetFileNameWithoutExtension(fileName);
            var uzanti = Path.GetExtension(fileName);

            var target = Path.Combine(folder, fileName);
            var say = 2;

            while (File.Exists(target))
            {
                target = Path.Combine(folder, $"{ad} ({say++}){uzanti}");
            }

            return target;
        }

        public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            var item = await _attachments.GetByIdAsync(id, cancellationToken);
            if (item is null)
            {
                return;
            }

            try
            {
                if (File.Exists(item.StoredPath))
                {
                    File.Delete(item.StoredPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fiziki fayl silinə bilmədi: {Path}", item.StoredPath);
            }

            _attachments.Remove(item);
            await _attachments.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Fayl silindi: {Id} - {Name}", item.Id, item.FileName);
        }

        public bool OpenWithShell(MediaAttachment attachment)
        {
            if (attachment is null || !attachment.Exists)
            {
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(attachment.StoredPath) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fayl açıla bilmədi: {Path}", attachment.StoredPath);
                return false;
            }
        }

        public string GetFolderPath(string refType, int refId)
            => Path.Combine(RootDirectory, refType, QovluqAdi(refType, refId));

        public bool OpenFolder(string refType, int refId)
        {
            if (refId <= 0)
            {
                return false;
            }

            try
            {
                // Qovluq hələ yaradılmayıbsa da açıla bilsin.
                var folder = GetFolderPath(refType, refId);
                Directory.CreateDirectory(folder);

                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
                _logger.LogInformation("Fayl qovluğu açıldı: {Path}", folder);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Qovluq açıla bilmədi: {RefType} #{RefId}", refType, refId);
                return false;
            }
        }

        public async Task<Dictionary<int, int>> GetCountsAsync(string refType, CancellationToken cancellationToken = default)
        {
            var items = await _attachments.FindAsync(a => a.RefType == refType, cancellationToken);

            return items
                .GroupBy(a => a.RefId)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }
}
