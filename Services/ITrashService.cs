using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Silinmiş məlumatları <b>geri qaytarma</b> xidməti (əks-səbət).
    /// <para>
    /// Hər silmə əməliyyatından ƏVVƏL qeydlərin TAM surəti diskə JSON kimi
    /// yazılır. İstifadəçi səhvən silsə:
    /// <list type="bullet">
    ///   <item>masaüstü tətbiqdə <b>Ctrl+Z</b> — ən son silinəni qaytarır;</item>
    ///   <item>«🗑 Silinənlər» bölməsi — <b>bütün</b> silinmişləri göstərir.</item>
    /// </list>
    /// </para>
    /// </summary>
    public interface ITrashService
    {
        /// <summary>Silinmiş qeydlərin saxlandığı qovluq.</summary>
        string Folder { get; }

        /// <summary>Avtomobili və ona aid BÜTÜN qeydləri surətə götürür (silməzdən əvvəl).</summary>
        Task<TrashSnapshot?> BackupCarAsync(int carId, CancellationToken cancellationToken = default);

        /// <summary>Satış qeydini surətə götürür.</summary>
        Task<TrashSnapshot?> BackupSaleAsync(int saleId, CancellationToken cancellationToken = default);

        /// <summary>Xərc qeydini surətə götürür.</summary>
        Task<TrashSnapshot?> BackupExpenseAsync(int expenseId, CancellationToken cancellationToken = default);

        /// <summary>Krediti və onun bütün əməliyyatlarını surətə götürür.</summary>
        Task<TrashSnapshot?> BackupCreditAsync(int creditId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Avtomobilin REDAKTƏDƏN ƏVVƏLKİ vəziyyətini surətə götürür.
        /// Bərpada qeyd yenidən yaradılmır — <b>əvvəlki dəyərlərə qaytarılır</b>.
        /// </summary>
        Task<TrashSnapshot?> BackupCarEditAsync(int carId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Xərcin REDAKTƏDƏN ƏVVƏLKİ vəziyyətini surətə götürür
        /// (kateqoriya / qrup / məbləğ səhvən dəyişdirildikdə geri qaytarmaq üçün).
        /// </summary>
        Task<TrashSnapshot?> BackupExpenseEditAsync(int expenseId, CancellationToken cancellationToken = default);

        /// <summary>Kreditin REDAKTƏDƏN ƏVVƏLKİ vəziyyətini surətə götürür.</summary>
        Task<TrashSnapshot?> BackupCreditEditAsync(int creditId, CancellationToken cancellationToken = default);

        /// <summary>Silinmiş qeydlərin siyahısı (ən yenisi əvvəldə).</summary>
        Task<IReadOnlyList<TrashSnapshot>> GetEntriesAsync(CancellationToken cancellationToken = default);

        /// <summary>Ən son silinmiş qeydi geri qaytarır (Ctrl+Z).</summary>
        Task<TrashSnapshot?> RestoreLastAsync(CancellationToken cancellationToken = default);

        /// <summary>Verilmiş surəti geri qaytarır.</summary>
        Task<TrashSnapshot?> RestoreAsync(string fileName, CancellationToken cancellationToken = default);

        /// <summary>Surəti birdəfəlik silir (bərpa mümkün olmayacaq).</summary>
        Task<bool> DeletePermanentlyAsync(string fileName, CancellationToken cancellationToken = default);

        /// <summary>Köhnə surətləri təmizləyir — ən son <paramref name="keep"/> ədəd saxlanılır.</summary>
        Task<int> CleanupAsync(int keep = 200, CancellationToken cancellationToken = default);
    }
}
