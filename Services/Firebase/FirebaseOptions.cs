// ============================================================================
//  🔥 021Cars — FIREBASE REALTIME DATABASE ARXİTEKTURASI
//  ⚠ 1/6 — KONFİQURASİYA + ƏSAS MODEL
// ----------------------------------------------------------------------------
//  ✅ SQLİTE / SQL — TAMAMİLƏ LƏĞV EDİLDİ ✗
//  ✅ BÜTÜN MƏLUMATLAR BİRBAŞA BULUDDA:
//     https://cas-database-96e6e-default-rtdb.firebaseio.com/
//  ✅ Hər obyektdə MÜTLƏQ:  id · updatedAt · updatedBy · isDeleted · deletedAt
//  ✅ Ağır fayllar (PDF/şəkil) YALNIZ fləşkartda ✓ — buludda yalnız NİSBİ YOL ✓
//  ✅ Xətalar səssizcə tutulur ✓ → app_errors.log ✓ (proqram ÇÖKMÜR ✗)
// ----------------------------------------------------------------------------
//  Paket tələbi: YOXDUR ✗ — yalnız .NET standart kitabxanaları ★
//     System.Net.Http  ·  System.Text.Json  ·  System.IO
// ============================================================================

using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cas0201.Firebase
{
    /// <summary>
    /// ⚙️ <b>FIREBASE KONFİQURASİYASI</b> ✓✓✓
    /// </summary>
    public sealed class FirebaseOptions
    {
        /// <summary>🔥 Realtime Database ünvanı ✓</summary>
        public string BaseUrl { get; set; } =
            "https://cas-database-96e6e-default-rtdb.firebaseio.com";

        /// <summary>🗂️ Tətbiqin kök qovluğu (ağacın başı ✓)</summary>
        public string RootPath { get; set; } = "021Cars";

        /// <summary>
        /// 🔐 Database secret / ID token ✓
        /// <para>
        /// ⚠ KODA YAZILMIR ✗ → mühit dəyişəni ilə verilir ✓
        /// (<c>CAS_FIREBASE_TOKEN</c> ✓) və <c>?auth=…</c> kimi əlavə olunur ✓
        /// </para>
        /// </summary>
        public string? AuthToken { get; set; }

        /// <summary>⏱️ HTTP timeout (saniyə) ✓</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>🔄 Uğursuz cəhd üçün maksimum təkrar ✓</summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>⏲️ Təkrar cəhdlər arası fasilə (millisaniyə ✓)</summary>
        public int RetryDelayMs { get; set; } = 700;

        /// <summary>
        /// 📜 Loq faylı ✓ — <c>%LOCALAPPDATA%\EnterpriseAeroStudio\Logs\app_errors.log</c> ✓
        /// <para>⚠ <see cref="AppLogger.LogQovlugu"/> ilə EYNİ qovluqdadır ✓ (tək mənbə ✓)</para>
        /// </summary>
        public string LogPath { get; set; } = AppLogger.FaylYolu;

        /// <summary>📋 Offline növbə faylı ✓ (JSON ✓ — SQL DEYİL ✗)</summary>
        public string QueuePath { get; set; } =
            System.IO.Path.Combine(AppLogger.LogQovlugu, "offline_queue.json");

        /// <summary>🖥️ Cihaz identifikatoru ✓ (LWW tiebreak ✓)</summary>
        public string DeviceId { get; set; } = Environment.MachineName;

        /// <summary>🖥️ Cihaz adı qısaltması ✓ (audit sütunları üçün ✓)</summary>
        public string CihazAdi => string.IsNullOrWhiteSpace(DeviceId) ? "namelum" : DeviceId;

        /// <summary>🌐 Paylaşılan HttpClient ✓ (socket tükənməsin ✗)</summary>
        public HttpClient Client { get; } = new();

        /// <summary>⚙️ HttpClient-i konfiqurasiya edir ✓ (engine ctor-dan çağırılır ✓)</summary>
        public void ClientiQur()
        {
            try
            {
                Client.Timeout = TimeSpan.FromSeconds(Math.Max(10, TimeoutSeconds));
            }
            catch { }
        }

        /// <summary>🧾 JSON parametrləri ✓ (camelCase ✓)</summary>
        public static JsonSerializerOptions Json { get; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        /// <summary>✅ Konfiqurasiya hazırdır?</summary>
        public bool Hazirdir => !string.IsNullOrWhiteSpace(BaseUrl);

        /// <summary>🔐 Token təyin olunub? ✓ (boşdursa açıq qaydalar tələb olunur ✓)</summary>
        public bool TokenVar => !string.IsNullOrWhiteSpace(AuthToken);

        /// <summary>
        /// 🔗 Tam URL ✓ — <c>{baseUrl}/{rootPath}/{path}.json?auth=…</c> ✓✓✓
        /// <para>
        /// 🔐 <b>ƏSAS DÜZƏLİŞ:</b> token MÜTLƏQ URL-ə əlavə olunur ✓<br/>
        /// (Firebase RTDB üçün <c>?auth=</c> forması BÜTÜN əməliyyatlarda işləyir ✓
        /// — həm GET/PUT/PATCH ✓, həm də SSE canlı axın ✓)
        /// </para>
        /// </summary>
        public string Url(string path = "")
        {
            var tam = $"{BaseUrl.TrimEnd('/')}/{RootPath}";

            if (!string.IsNullOrWhiteSpace(path))
            {
                tam += "/" + path.Trim('/');
            }

            tam += ".json";

            // 🔐 TOKEN — ƏLAVƏ OLUNMASSA 401/403 ALINIR ✗✓✓
            if (TokenVar)
            {
                tam += (tam.Contains('?') ? "&" : "?") +
                       "auth=" + Uri.EscapeDataString(AuthToken!);
            }

            return tam;
        }

        /// <summary>🔐 Tokeni mühit dəyişənindən oxuyur ✓ (CAS_FIREBASE_TOKEN ✓)</summary>
        public bool TokenYukle()
        {
            try
            {
                AuthToken = Environment.GetEnvironmentVariable("CAS_FIREBASE_TOKEN")
                         ?? Environment.GetEnvironmentVariable("FIREBASE_TOKEN")
                         ?? AuthToken;

                return TokenVar;
            }
            catch { return false; }
        }

        /// <summary>🆔 Yeni açar (GUID ✓ — Firebase key üçün təhlükəsiz ✓)</summary>
        public static string YeniId() => Guid.NewGuid().ToString("N");

        /// <summary>🕒 UTC vaxt nişanı (ISO-8601 ✓)</summary>
        public static string UtcIndi() => DateTime.UtcNow.ToString("o");
    }

    // ==== DAVAMI ====
}
