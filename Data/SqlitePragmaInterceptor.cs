using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EnterpriseAeroStudio.Data
{
    /// <summary>
    /// 🚀 <b>SQLITE-I BÖYÜK BAZA ÜÇÜN SAZLAYIR</b> ✓✓✓
    /// <para>
    /// Hər bağlantı açıldıqda <c>PRAGMA</c> əmrləri tətbiq olunur ✓ —
    /// beləliklə milyonlarla sətir olan bazada da oxuma/yazma sürətli olur ✓
    /// </para>
    /// <list type="bullet">
    ///   <item><c>journal_mode=WAL</c> — oxuma və yazma EYNİ ANDA mümkün ✓</item>
    ///   <item><c>synchronous=NORMAL</c> — WAL ilə təhlükəsiz, amma 3-10× sürətli ✓</item>
    ///   <item><c>cache_size=-65536</c> — 64 MB səhifə keşi ✓ (təkrarlanan sorğular RAM-dan ✓)</item>
    ///   <item><c>temp_store=MEMORY</c> — müvəqqəti cədvəllər (GROUP BY / ORDER BY ✓) RAM-da ✓</item>
    ///   <item><c>mmap_size</c> — 256 MB yaddaş xəritələmə ✓ (böyük cədvəldə I/O azalır ✓)</item>
    ///   <item><c>busy_timeout</c> — kilid gözləməsi ✓ («database is locked» xətası olmur ✗)</item>
    /// </list>
    /// </summary>
    public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        /// <summary>Bağlantı açılan kimi icra olunan əmrlər ✓.</summary>
        private static readonly string[] Emirler =
        {
            "PRAGMA journal_mode = WAL;",
            "PRAGMA synchronous = NORMAL;",
            "PRAGMA cache_size = -65536;",
            "PRAGMA temp_store = MEMORY;",
            "PRAGMA mmap_size = 268435456;",
            "PRAGMA busy_timeout = 10000;",
            "PRAGMA optimize;"
        };

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        {
            TetbiqEt(connection);
            base.ConnectionOpened(connection, eventData);
        }

        public override Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TetbiqEt(connection);
            return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }

        private static void TetbiqEt(DbConnection connection)
        {
            try
            {
                using var əmr = connection.CreateCommand();

                foreach (var emir in Emirler)
                {
                    əmr.CommandText = emir;
                    əmr.ExecuteNonQuery();
                }
            }
            catch
            {
                // ⚠ PRAGMA tətbiq olunmasa da tətbiq İŞLƏMƏLİDİR ✓
                //  (köhnə SQLite sürətlərində bəzi PRAGMA-lar olmaya bilər ✓)
            }
        }
    }
}
