using System.IO;
using EnterpriseAeroStudio.Models;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAeroStudio.Data
{
    /// <summary>
    /// Verilənlər bazası <b>ilk dəfə</b> yaradılarkən nümunə (seed) məlumatlarını yazır.
    /// </summary>
    public static class DatabaseSeeder
    {
        /// <summary>
        /// Nümunə məlumatların artıq yazıldığını bildirən BAYRAQ FAYLI.
        /// <para>
        /// ⚠ KRİTİK: Əvvəllər yoxlama <c>Cars.AnyAsync()</c> ilə edilirdi.
        /// İstifadəçi BÜTÜN maşınları sildikdə cədvəl boş qalırdı və proqram hər
        /// açılışda nümunə maşınları (Mercedes C300 + BMW 328) və onların 20 000 ₼-lıq
        /// «Alış» xərcini <b>YENİDƏN YARADIRDI</b> — silinmiş maşınlar «geri qayıdırdı».
        /// </para>
        /// <para>
        /// Artıq bayraq faylı yoxlanılır: bir dəfə yazıldısa, bir daha yazılmır.
        /// </para>
        /// </summary>
        private static string MarkerPath => Path.Combine(
            Cas0201.Kok.Qovluq,
            "EnterpriseAeroStudio",
            ".numune-melumat-yazildi");

        /// <summary>
        /// 🆕 <b>NÜMUNƏ (DEMO) MƏLUMAT YAZILSIN?</b> — <c>false</c> ✓✓✓
        /// <para>
        /// ⚠ SƏBƏB: quraşdırıcıdan çıxan proqram <b>SIFIR</b> olmalıdır ✓ —
        /// içində heç bir avtomobil · kredit · xərc olmamalıdır ✗
        /// (müştəri tələbi ✓✓✓).
        /// </para>
        /// <para>
        /// Beləliklə ilk açılışda yalnız <b>BOŞ</b> baza yaranır ✓:
        /// avtomobil ✗ · kredit ✗ · xərc ✗ — yalnız standart xərc qrupları /
        /// kateqoriyaları ✓ və tərəfdaş siyahısı ✓ (konfiqurasiya ✓).
        /// </para>
        /// <para>
        /// 📜 Demo əvəzinə məlumat «📜 Skript İdxalı» tabından və ya əl ilə
        /// əlavə olunur ✓✓✓
        /// </para>
        /// </summary>
        private static readonly bool NumuneMelumatYazilsin = false;

        public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
        {
            // 🆕 SIFIR QURAŞDIRMA: nümunə (demo) məlumat YAZILMIR ✓✓✓
            if (!NumuneMelumatYazilsin)
            {
                return;
            }

            // ---- 1) Nümunə məlumatlar YALNIZ BİR DƏFƏ yazılır ----
            if (File.Exists(MarkerPath))
            {
                return;
            }

            // Bayraq faylı ƏVVƏLCƏ yaradılır ki, xəta olsa belə təkrar yazılmasın.
            try
            {
                var dir = Path.GetDirectoryName(MarkerPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(MarkerPath, DateTime.Now.ToString("O"), cancellationToken);
            }
            catch
            {
                // Fayl yaradıla bilməsə də davam edirik.
            }

            // ---- 2) Bazada artıq məlumat varsa, heç nə yazmırıq ----
            if (await context.Cars.AnyAsync(cancellationToken))
            {
                return;
            }

            var mercedes = new CarItem
            {
                Marka = "Mercedes C300",
                QeydiyyatNisani = "77HY717",
                Vin = string.Empty,
                Il = 2014,
                Yurus = 0,
                Yanacaq = "Benzin",
                AlisTarixi = DateTime.Today,
                AlisSaati = "12:15",
                AlisQiymeti = 0m,
                Status = "Stokda"
            };

            var bmw = new CarItem
            {
                Marka = "BMW 328",
                QeydiyyatNisani = "77HH717",
                Vin = string.Empty,
                Il = 2012,
                Yurus = 0,
                Yanacaq = "Benzin",
                AlisTarixi = DateTime.Today,
                AlisSaati = "12:15",
                AlisQiymeti = 20000m,
                Status = "Stokda"
            };

            context.Cars.AddRange(mercedes, bmw);
            await context.SaveChangesAsync(cancellationToken);

            context.Expenses.Add(new ExpenseItem
            {
                Tarix = DateTime.Today,
                Teyinat = "Avtomobil Xərci",
                Qrup = "💰 Alış & Maya Xərcləri",
                Kategoriya = "Alış",
                CarId = bmw.Id,
                Mebleg = 20000m,
                OdenisUsulu = "Nağd",
                Qeyd = "Avtomobilin alış qiyməti (Avtomatik sinxronlaşdırıldı)"
            });

            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
