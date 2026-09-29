using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>Avtomobillər üzrə biznes əməliyyatları.</summary>
    public interface ICarService
    {
        Task<IReadOnlyList<CarItem>> GetCarsAsync(CancellationToken cancellationToken = default);

        /// <summary>Bütün avtomobillər (statistika üçün — satılan və kreditdəkilər daxil).</summary>
        Task<IReadOnlyList<CarItem>> GetAllCarsAsync(CancellationToken cancellationToken = default);

        // ====================================================================
        //  🚀 YÜNGÜL SİYAHILAR ✓✓✓  (MİLYON XƏRC SƏTRİ ÜÇÜN)
        // --------------------------------------------------------------------
        //  ⚠ Adi siyahı metodları hər avtomobilin BÜTÜN xərclərini yaddaşa
        //     yükləyir ✗ (1 000 000 xərc ≈ 1 GB RAM ✗ → proqram donur ✗✓✓)
        //  ✅ Yüngül variantlarda xərc CƏMLƏRİ SQL `GROUP BY` ilə alınır ✓
        //     («Avto Park» tabı və statistika bunları işlədir ✓✓✓)
        // ====================================================================

        /// <summary>🚀 Aktiv park avtomobilləri — xərc cəmləri SQL-də ✓ (yaddaş qənaəti ✓).</summary>
        Task<IReadOnlyList<CarItem>> GetCarsLightAsync(CancellationToken cancellationToken = default);

        /// <summary>🚀 Bütün avtomobillər (statistika üçün) — xərc cəmləri SQL-də ✓.</summary>
        Task<IReadOnlyList<CarItem>> GetAllCarsLightAsync(CancellationToken cancellationToken = default);

        /// <summary>🚀 Barter namizədləri — xərc cəmləri SQL-də ✓.</summary>
        Task<IReadOnlyList<CarItem>> GetBarterCandidatesLightAsync(
            int? excludeCarId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Arxivdəki avtomobillər — satılmış VƏ barter əvəzi verilmiş maşınlar.
        /// </summary>
        Task<IReadOnlyList<CarItem>> GetSoldCarsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Barter alışında əvəzə verilə bilən avtomobillər:
        /// aktiv parkdakı maşınlar (satılan/kreditdə olanlar və artıq
        /// barter üçün verilmişlər xaric).
        /// </summary>
        /// <param name="excludeCarId">Hazırda redaktə olunan avtomobilin Id-si.</param>
        Task<IReadOnlyList<CarItem>> GetBarterCandidatesAsync(int? excludeCarId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sahibi silinmiş «yetim» qeydləri təmizləyir:
        /// avtomobil xərcləri, satışlar, kreditlər, kredit əməliyyatları və
        /// media faylları. Hesabatların (ümumi xərc, dövr mənfəəti və s.)
        /// <b>dəqiq</b> qalmasını təmin edir.
        /// </summary>
        /// <returns>Silinmiş qeydlərin ümumi sayı.</returns>
        Task<int> CleanupOrphansAsync(CancellationToken cancellationToken = default);

        Task<bool> RegistrationExistsAsync(string registration, int? excludeId = null, CancellationToken cancellationToken = default);

        Task<CarItem> AddCarAsync(CarItem car, CancellationToken cancellationToken = default);

    /// <summary>
    /// 🔢 <b>SIRA NÖMRƏSİ «0» OLAN AVTOMOBİLLƏRƏ ARDICIL NÖMRƏ VERİR</b> ✓✓✓
    /// <para>1, 2, 3 … ✓ — təkrarsız ✓ (boş qalan nömrələr yenidən istifadə olunur ✓)</para>
    /// </summary>
    Task<int> SiraNomreleriniDuzeltAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// <b>YALNIZ STATUSU</b> dəyişir — change tracker-dən asılı olmayan,
        /// <b>DƏQİQ</b> yeniləmə.
        /// <para>
        /// «Arxivdən geri qaytar» kimi əməliyyatlar üçün: uzunömürlü kontekstdəki
        /// köhnə (stale) nüsxə dəyişikliyi <b>uda bilər</b> ✗ — bu metod əvvəlcə
        /// izləyicini təmizləyir, sonra sətri TƏZƏ oxuyub yeniləyir ✓
        /// </para>
        /// <para>
        /// Sıra nömrəsi boş (0) olarsa avtomatik ardıcıl nömrə verilir ✓
        /// </para>
        /// </summary>
        /// <returns><c>true</c> — yeniləndi; <c>false</c> — avtomobil bazada tapılmadı.</returns>
        Task<bool> SetStatusAsync(int carId, string status, CancellationToken cancellationToken = default);

        Task UpdateCarAsync(CarItem car, CancellationToken cancellationToken = default);

        Task DeleteCarAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Alış qiyməti + bütün xərclər = maya dəyəri.</summary>
        decimal CalculateCost(CarItem car);

        CarStats BuildStats(IEnumerable<CarItem> cars);

        /// <summary>
        /// Alışlar üzrə statistika — nağd və barter bölgüsü ilə.
        /// «Alışlar» bölməsi və idarə paneli istifadə edir.
        /// </summary>
        PurchaseStats BuildPurchaseStats(IEnumerable<CarItem> cars);
    }
}
