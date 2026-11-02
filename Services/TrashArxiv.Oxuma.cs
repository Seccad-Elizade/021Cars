using System;
using System.Collections.Generic;
using System.Linq;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🗄️ <b>ARXİV — OXUMA / SİLMƏ / TƏMİZLƏMƏ</b> ✓✓✓ (v6.2.86)
    /// <para>Hamısı SQL ilə ✓ — fayl IO YOX ✗ (110 000 fayl problemi həll edildi ✓)</para>
    /// </summary>
    public static partial class TrashArxiv
    {
        /// <summary>📋 Son qeydlər (JSON YÜKLƏNMİR ✗ — yalnız başlıq sətirləri ✓)</summary>
        public static IReadOnlyList<ArxivQeydi> Oxu(int limit = 300)
        {
            var netice = new List<ArxivQeydi>();

            try
            {
                Temel();

                using var c = Aç();
                using var emr = c.CreateCommand();

                emr.CommandText = @"
                    SELECT Id, Ad, Tarix, Nov, Tip, Basliq, Mebleg, Xulase
                    FROM Qeyd ORDER BY Tarix DESC, Id DESC LIMIT $limit;";

                emr.Parameters.AddWithValue("$limit", Math.Max(1, limit));

                using var oxucu = emr.ExecuteReader();

                while (oxucu.Read())
                {
                    DateTime.TryParse(oxucu.GetString(2), out var tarix);

                    netice.Add(new ArxivQeydi
                    {
                        Id = oxucu.GetInt64(0),
                        Ad = oxucu.GetString(1),
                        Tarix = tarix,
                        Nov = oxucu.GetString(3),
                        Tip = oxucu.GetString(4),
                        Basliq = oxucu.GetString(5),
                        Mebleg = (decimal)oxucu.GetDouble(6),
                        Xulase = oxucu.IsDBNull(7) ? string.Empty : oxucu.GetString(7)
                    });
                }
            }
            catch
            {
                // 🛡️ oxunmasa — boş siyahı ✓
            }

            return netice;
        }

        /// <summary>📄 Bir qeydin TAM JSON-u ✓</summary>
        public static string? JsonAl(string ad)
        {
            try
            {
                Temel();

                using var c = Aç();
                using var emr = c.CreateCommand();

                emr.CommandText = "SELECT Json FROM Qeyd WHERE Ad = $ad LIMIT 1;";
                emr.Parameters.AddWithValue("$ad", ad);

                return emr.ExecuteScalar() as string;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>🗑️ Bir qeydi arxivdən silir ✓</summary>
        public static bool Sil(string ad)
        {
            try
            {
                Temel();

                using var c = Aç();
                using var emr = c.CreateCommand();

                emr.CommandText = "DELETE FROM Qeyd WHERE Ad = $ad;";
                emr.Parameters.AddWithValue("$ad", ad);

                return emr.ExecuteNonQuery() > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>🧹 Ən son <paramref name="keep"/> qeydi saxlayır ✓</summary>
        public static int Temizle(int keep = 200)
        {
            try
            {
                Temel();

                using var c = Aç();
                using var emr = c.CreateCommand();

                emr.CommandText = @"
                    DELETE FROM Qeyd
                    WHERE Id NOT IN (SELECT Id FROM Qeyd ORDER BY Tarix DESC, Id DESC LIMIT $keep);";

                emr.Parameters.AddWithValue("$keep", Math.Max(0, keep));

                return emr.ExecuteNonQuery();
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 🧾 <b>SİLİNMİŞ ID-lər</b> ✓✓✓ — USB birləşdirməsində «silinmiş qeyd
        /// geri gəlməsin» ✗. ⚡ SQL ilə ✓ (110 000 JSON parse YOX ✗✓✓)
        /// </summary>
        public static Dictionary<string, HashSet<long>> SilinenIdler()
        {
            var netice = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                Temel();

                using var c = Aç();
                using var emr = c.CreateCommand();
                emr.CommandText = "SELECT Cedvel, Id FROM Silinen;";

                using var oxucu = emr.ExecuteReader();

                while (oxucu.Read())
                {
                    var cedvel = oxucu.GetString(0);

                    if (!netice.TryGetValue(cedvel, out var set))
                    {
                        set = new HashSet<long>();
                        netice[cedvel] = set;
                    }

                    set.Add(oxucu.GetInt64(1));
                }
            }
            catch
            {
                // 🛡️ oxunmasa — boş ✓
            }

            return netice;
        }
    }
}
