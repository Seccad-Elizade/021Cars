using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>🗑️ Arxivdəki bir qeydin QISA təsviri ✓ (UI üçün — tam JSON yüklənmir ✗)</summary>
    public sealed class ArxivQeydi
    {
        public long Id { get; init; }
        public string Ad { get; init; } = string.Empty;
        public DateTime Tarix { get; init; }
        public string Nov { get; init; } = string.Empty;
        public string Tip { get; init; } = string.Empty;
        public string Basliq { get; init; } = string.Empty;
        public decimal Mebleg { get; init; }
        public string Xulase { get; init; } = string.Empty;
    }

    /// <summary>
    /// 🗄️ <b>TƏK FAYLLI ARXİV (ZİBİL) SİSTEMİ</b> ✓✓✓ (v6.2.86)
    /// <para>
    /// ★ İstifadəçi şikayəti: «Trash qovluğunda 110 000 fayl var — proqram hər dəfə
    /// bunları bir-bir oxuyur və DONUR ✗»
    /// </para>
    /// <para>
    /// ⚠ ƏVVƏL: hər silinən qeyd üçün <b>ayrıca .json faylı</b> yazılırdı ✗ →
    /// 110 000 fayl ✗ → hər baxışda 110 000 disk oxunuşu ✗ → donma ✗✓✓
    /// </para>
    /// <para>
    /// ✅ İNDİ: hər şey <b>BİR SQLite faylında</b>: <c>NewData\Arxiv.db</c> ✓
    /// (oxuma · silmə · təmizləmə — hamısı SQL ilə ✓ fayl IO YOX ✗)
    /// </para>
    /// </summary>
    public static partial class TrashArxiv
    {
        private static readonly object _kilid = new();
        private static bool _temelHazır;

        /// <summary>📂 Yeni məlumat qovluğu ✓ («NewData» ✓)</summary>
        public static string Qovluq => Path.Combine(
            Cas0201.Kok.Qovluq, "EnterpriseAeroStudio", "NewData");

        /// <summary>🗄️ Arxiv verilənlər bazası (TƏK fayl ✓)</summary>
        public static string Fayl => Path.Combine(Qovluq, "Arxiv.db");

        /// <summary>🗑️ Köhnə JSON fayllarının qovluğu ✓</summary>
        public static string KohneQovluq => Path.Combine(
            Cas0201.Kok.Qovluq, "EnterpriseAeroStudio", "Trash");

        /// <summary>📦 Köçürülmüş köhnə faylların saxlandığı alt qovluq ✓ (silinmir ✗)</summary>
        public static string KohneArxivQovluq => Path.Combine(KohneQovluq, "_kohne");

        /// <summary>🚩 Köçürmənin davam etdiyi yer ✓</summary>
        private static string ProqresFayli => Path.Combine(Qovluq, "kohne_kocurme.txt");

        /// <summary>🔌 Bağlantı açır ✓ (WAL + gözləmə ✓ — paralel oxuma/yazma üçün ✓)</summary>
        private static SqliteConnection Aç()
        {
            Directory.CreateDirectory(Qovluq);

            var c = new SqliteConnection($"Data Source={Fayl};Pooling=True");
            c.Open();

            try
            {
                using var p = c.CreateCommand();

                // ⚡ WAL → arxa fondakı köçürmə ilə UI oxuması toqquşmur ✓
                p.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
                p.ExecuteNonQuery();
            }
            catch
            {
                // 🛡️
            }

            return c;
        }

        /// <summary>🏗️ Cədvəlləri hazırlayır ✓ (bir dəfə ✓)</summary>
        public static void Temel()
        {
            lock (_kilid)
            {
                if (_temelHazır)
                {
                    return;
                }

                try
                {
                    using var c = Aç();
                    using var əmr = c.CreateCommand();

                    əmr.CommandText = @"
                        CREATE TABLE IF NOT EXISTS Qeyd (
                            Id      INTEGER PRIMARY KEY AUTOINCREMENT,
                            Ad      TEXT NOT NULL UNIQUE,
                            Tarix   TEXT NOT NULL,
                            Nov     TEXT NOT NULL,
                            Tip     TEXT NOT NULL,
                            Basliq  TEXT NOT NULL,
                            Mebleg  REAL NOT NULL,
                            Xulase  TEXT NOT NULL DEFAULT '',
                            Json    TEXT NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS ix_qeyd_tarix ON Qeyd(Tarix DESC);
                        CREATE TABLE IF NOT EXISTS Silinen (
                            Cedvel TEXT NOT NULL,
                            Id     INTEGER NOT NULL
                        );
                        CREATE INDEX IF NOT EXISTS ix_silinen ON Silinen(Cedvel);
                        ALTER TABLE Qeyd ADD COLUMN Xulase TEXT NOT NULL DEFAULT '';";

                    try
                    {
                        əmr.ExecuteNonQuery();
                    }
                    catch
                    {
                        // ⚠️ «Xulase» sütunu artıq varsa ALTER xəta verir ✗ → yenidən cəhd ✓
                        əmr.CommandText = əmr.CommandText.Replace(
                            "ALTER TABLE Qeyd ADD COLUMN Xulase TEXT NOT NULL DEFAULT '';", string.Empty);

                        əmr.ExecuteNonQuery();
                    }

                    _temelHazır = true;
                }
                catch
                {
                    // 🛡️ arxiv qurulmasa proqram işləyir ✓
                }
            }
        }

        /// <summary>➕ Bir qeyd əlavə edir ✓ (fayl YOX ✗ — SQL sətri ✓)</summary>
        public static void Yaz(
            string ad,
            DateTime tarix,
            string nov,
            string tip,
            string basliq,
            decimal mebleg,
            string json,
            string xulase = "",
            IEnumerable<(string Cedvel, long Id)>? silinenIdler = null)
        {
            try
            {
                Temel();

                using var c = Aç();
                using var tr = c.BeginTransaction();
                using var əmr = c.CreateCommand();

                əmr.Transaction = tr;
                əmr.CommandText = @"
                    INSERT INTO Qeyd (Ad, Tarix, Nov, Tip, Basliq, Mebleg, Xulase, Json)
                    VALUES ($ad, $tarix, $nov, $tip, $basliq, $mebleg, $xulase, $json)
                    ON CONFLICT(Ad) DO UPDATE SET
                        Tarix = $tarix, Nov = $nov, Tip = $tip, Basliq = $basliq,
                        Mebleg = $mebleg, Xulase = $xulase, Json = $json;";

                əmr.Parameters.AddWithValue("$ad", ad);
                əmr.Parameters.AddWithValue("$tarix", tarix.ToString("s"));
                əmr.Parameters.AddWithValue("$nov", nov);
                əmr.Parameters.AddWithValue("$tip", tip);
                əmr.Parameters.AddWithValue("$basliq", basliq);
                əmr.Parameters.AddWithValue("$mebleg", (double)mebleg);
                əmr.Parameters.AddWithValue("$xulase", xulase);
                əmr.Parameters.AddWithValue("$json", json);

                əmr.ExecuteNonQuery();

                if (silinenIdler is not null)
                {
                    foreach (var (cedvel, id) in silinenIdler)
                    {
                        using var s = c.CreateCommand();
                        s.Transaction = tr;
                        s.CommandText = "INSERT INTO Silinen (Cedvel, Id) VALUES ($c, $i);";
                        s.Parameters.AddWithValue("$c", cedvel);
                        s.Parameters.AddWithValue("$i", id);
                        s.ExecuteNonQuery();
                    }
                }

                tr.Commit();
            }
            catch
            {
                // 🛡️ yazıla bilmirsə — proqram dayanmır ✓
            }
        }

        /// <summary>📊 Arxivdə neçə qeyd var? ✓</summary>
        public static int Say()
        {
            try
            {
                Temel();

                using var c = Aç();
                using var əmr = c.CreateCommand();
                əmr.CommandText = "SELECT COUNT(1) FROM Qeyd;";

                return Convert.ToInt32(əmr.ExecuteScalar() ?? 0);
            }
            catch
            {
                return 0;
            }
        }
    }
}
