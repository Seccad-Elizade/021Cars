// ============================================================================
//  🌐 021Cars — FIREBASE REST KLIENTİ (4/6)
//  ✅ Xarici paket YOXDUR ✗ — HttpClient + System.Text.Json ★
//  ✅ Avtomatik təkrar (retry + backoff ✓)
//  ✅ Şəbəkə xətası → AppLogger.Sebeke() ✓ — EXCEPTION YUXARI ATILMIR ✗ ✓✓✓
// ============================================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Cas0201.Firebase
{
    /// <summary>
    /// 🌐 <b>Firebase Realtime Database REST klienti</b> ✓✓✓
    /// <para>
    /// RTDB REST API: <c>GET/PUT/PATCH/DELETE {url}/{path}.json</c> ✓
    /// Real-time üçün <b>SSE</b> (Server-Sent Events) ✓
    /// </para>
    /// </summary>
    public sealed class FirebaseRestClient
    {
        private readonly FirebaseOptions _o;

        /// <summary>⚙️ Klientin istifadə etdiyi konfiqurasiya ✓ (repository oxuyur ✓)</summary>
        public FirebaseOptions Options => _o;

        /// <summary>🌐 Şəbəkə vəziyyəti ✓ (public — UI indikatoru üçün ✓)</summary>
        public bool Onlayn { get; private set; }

        public FirebaseRestClient(FirebaseOptions options)
        {
            _o = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>🔗 Sorğu qurur ✓ (auth token əlavə olunur ✓)</summary>
        private HttpRequestMessage Sorğu(HttpMethod metod, string yol, string? cism = null)
        {
            var r = new HttpRequestMessage(metod, _o.Url(yol));

            if (cism is not null)
            {
                r.Content = new StringContent(cism, Encoding.UTF8, "application/json");
            }

            r.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return r;
        }

        // --------------------------------------------------------------------
        //  🩺 DİAQNOSTİKA — «NİYƏ SİNXRONLAŞMIR?» SUALINA CAVAB ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>🩺 Bağlantını <b>REAL</b> yoxlayır ✓ (status kodu + izah ✓)</summary>
        public async Task<string> SinaqEtAsync(CancellationToken ct = default)
        {
            var m = new StringBuilder();

            try
            {
                m.AppendLine("🔗 URL   : " + _o.BaseUrl);
                m.AppendLine("🗂️ KÖK   : /" + _o.RootPath);
                m.AppendLine("🔐 TOKEN : " + (_o.TokenVar
                    ? "VAR ✓ (" + _o.AuthToken!.Length + " simvol ✓)"
                    : "YOXDUR ✗ → 401/403 gözlənilir ✓"));
                m.AppendLine("🖥️ CİHAZ : " + _o.CihazAdi);
                m.AppendLine();

                using var r = await _o.Client
                    .SendAsync(Sorğu(HttpMethod.Get, ""), ct).ConfigureAwait(false);

                var govde = await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                m.AppendLine($"📡 HTTP  : {(int)r.StatusCode} {r.StatusCode}");

                if (r.IsSuccessStatusCode)
                {
                    Onlayn = true;
                    m.AppendLine("✅ NƏTİCƏ: BAĞLANTI İŞLƏYİR ✓");
                    m.AppendLine("📦 MƏZMUN: " + Kısalt(govde, 400));
                }
                else if (r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    Onlayn = false;
                    m.AppendLine("⛔ NƏTİCƏ: İCAZƏ YOXDUR ✗ (qaydalar bağlıdır ✓)");
                    m.AppendLine("👉 ① Firebase → Realtime Database → «Qaydalar»:");
                    m.AppendLine("     { \"rules\": { \"021Cars\": { \".read\": true, \".write\": true } } }");
                    m.AppendLine("👉 ② Yaxud token verin:  $env:CAS_FIREBASE_TOKEN = \"<secret>\"");
                    m.AppendLine("📦 CAVAB : " + Kısalt(govde, 200));
                }
                else
                {
                    m.AppendLine("⚠️ NƏTİCƏ: GÖZLƏNİLMƏYƏN CAVAB ✗");
                    m.AppendLine("📦 CAVAB : " + Kısalt(govde, 300));
                }
            }
            catch (Exception ex)
            {
                m.AppendLine("🔌 NƏTİCƏ: ŞƏBƏKƏ YOXDUR ✗ / DNS problemi ✗");
                m.AppendLine("📝 XƏTA : " + ex.Message);
                AppLogger.Sebeke(ex, "diaqnostika");
            }

            return m.ToString();
        }

        /// <summary>🧪 <b>REAL SİNXRON TESTİ</b> — yazır (PUT ✓) + oxuyur (GET ✓) ✓✓✓</summary>
        public async Task<string> SinxronTestiAsync(CancellationToken ct = default)
        {
            var m = new StringBuilder();

            try
            {
                const string sınaqYolu = "_sinaq";

                var test = new Dictionary<string, object?>
                {
                    ["id"] = sınaqYolu,
                    ["updatedAt"] = FirebaseOptions.UtcIndi(),
                    ["updatedBy"] = _o.CihazAdi,
                    ["isDeleted"] = false,
                    ["deletedAt"] = null,
                    ["mesaj"] = "021Cars sinxron testi ✓"
                };

                m.AppendLine("──────── 🧪 YAZMA (PUT) ────────");

                var yazdı = await YenileAsync(sınaqYolu, test, ct).ConfigureAwait(false);

                m.AppendLine(yazdı
                    ? $"✅ YAZILDI ✓ → /{_o.RootPath}/{sınaqYolu} ✓"
                    : "❌ YAZILMADI ✗ — icazə yoxdur ✗ (qaydaları yoxlayın ✓)");

                m.AppendLine();
                m.AppendLine("──────── 📖 OXUMA (GET) ────────");

                var oxunan = await OxuAsync(sınaqYolu, ct).ConfigureAwait(false);

                m.AppendLine(oxunan is null
                    ? "❌ OXUNMADI ✗ — şəbəkə yoxdur ✗ / icazə yoxdur ✗"
                    : "✅ OXUNDU ✓ → " + Kısalt(oxunan.ToJsonString(), 320));
            }
            catch (Exception ex)
            {
                m.AppendLine("❌ TEST XƏTASI ✗: " + ex.Message);
                AppLogger.Xeta(ex, "sinxron testi");
            }

            return m.ToString();
        }

        /// <summary>✂️ Mətni kəsir ✓</summary>
        private static string Kısalt(string? s, int maks) =>
            string.IsNullOrWhiteSpace(s)
                ? "(boş ✓)"
                : (s.Length <= maks ? s : s[..maks] + "…");

        // --------------------------------------------------------------------
        //  📖 OXUMA ✓
        // --------------------------------------------------------------------

        /// <summary>📥 Ağacı / qovluğu oxuyur ✓ (xəta → boş obyekt ✓)</summary>
        public async Task<JsonObject?> OxuAsync(
            string yol, CancellationToken ct = default)
        {
            return await CəhdAsync(async () =>
            {
                using var r = await _o.Client.SendAsync(Sorğu(HttpMethod.Get, yol), ct)
                    .ConfigureAwait(false);

                if (r.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new FirebaseAuthException(
                        $"🔐 İcazə yoxdur ({(int)r.StatusCode}) — token yanlışdır ✗");
                }

                if (!r.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"HTTP {(int)r.StatusCode}");
                }

                var mətn = await r.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                // ============================================================
                //  📥 DİAQNOSTİKA (v6.2.13) — BOŞ cavab artıq SƏSSİZ DEYİL ✗✓✓
                // ------------------------------------------------------------
                //  ⚠ ƏVVƏL: boş/«null» cavab «boş obyekt» kimi qaytarılırdı ✗
                //  → yuxarıda «heç nə çəkilmədi» ✗ və NİYƏ olduğu BİLİNMİRDİ ✗✓✓
                //  ✅ İNDİ: hər oxuma loqa düşür ✓ (ölçü + status ✓)
                // ============================================================
                if (string.IsNullOrWhiteSpace(mətn))
                {
                    AppLogger.Xeberdarliq(
                        $"⚠️ OXU «{yol}» BOŞ cavab ✗ (HTTP {(int)r.StatusCode}) — buludda bu bölmə yoxdur?");

                    return new JsonObject();
                }

                if (mətn.Trim() == "null")
                {
                    AppLogger.Melumat($"📥 OXU «{yol}» → buludda BOŞDUR ✓ (null ✓)");
                    return new JsonObject();
                }

                AppLogger.Melumat($"📥 OXU «{yol}» → {(int)r.StatusCode} · {mətn.Length / 1024} KB ✓");

                return CevirJsonNode(JsonNode.Parse(mətn));
            }, "Oxu " + yol, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// 🔄 <b>JSON NODE-NU OBYEKTƏ ÇEVİRİR</b> ✓✓✓  (v6.2.13 — ★ KRİTİK DÜZƏLİŞ ★)
        /// <para>
        /// 🔥 <b>FIREBASE RTDB XÜSUSİYYƏTİ:</b> açar(lar) <b>bitişik ədəd</b>dirsə
        /// (<c>1,2,3,4…</c> ✓ — bazadaki Id-lər belədir ✓) Firebase onları
        /// <b>JSON MASSİVİ</b> kimi qaytarır ✗ (<c>[{…},{…}]</c> ✓),
        /// obyekt kimi YOX ✗.
        /// </para>
        /// <para>
        /// ⚠ ƏVVƏL: <c>JsonNode.Parse(mətn) as JsonObject</c> → massiv gələndə
        /// <b>NULL</b> olurdu ✗ → <c>?? new JsonObject()</c> → <b>BOŞ obyekt</b> ✗
        /// → «heç nə çəkilmədi» ✗ və <b>HEÇ BİR XƏTA GÖSTƏRİLMİRDİ</b> ✗✓✓
        /// (istifadəçi şikayəti: «buluddan götür → 0 maşın» ✗✓✓)
        /// </para>
        /// <para>
        /// ✅ İNDİ: massiv də <b>obyektə çevrilir</b> ✓ — açar kimi qeydin ÖZ
        /// <c>id</c> sahəsi götürülür ✓ (yoxdursa massiv indeksi ✓: 0→1 ✓ Firebase qaydası ✓)
        /// </para>
        /// </summary>
        private static JsonObject CevirJsonNode(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obyekt:
                    return obyekt;

                case JsonArray massiv:
                {
                    // 🔥 Bitişik ədəd açarlar → Firebase massiv qaytarır ✗ → düzəldirik ✓
                    var çevrilmiş = new JsonObject();

                    for (var i = 0; i < massiv.Count; i++)
                    {
                        if (massiv[i] is not JsonObject sətr) continue;

                        // 🆔 AÇAR: qeydin ÖZ id-si ✓ (ən etibarlı ✓)
                        var id = sətr["id"]?.ToString();

                        // ⚠ id yoxdursa → Firebase qaydası: massiv indeksi i → açar (i+1) ✓
                        var açar = string.IsNullOrWhiteSpace(id)
                            ? (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                            : id;

                        // ⚠ `DeepClone` — node-un valideyn bağlantısını qoparır ✓ (vacib ✓)
                        çevrilmiş[açar] = sətr.DeepClone();
                    }

                    AppLogger.Melumat(
                        $"🔄 Firebase MASSİVİ obyektə çevrildi ✓ — {çevrilmiş.Count} qeyd ✓");

                    return çevrilmiş;
                }

                default:
                    return new JsonObject();
            }
        }

        /// <summary>📥 Tək obyekti oxuyur ✓ (tapılmadısa <c>null</c> ✓)</summary>
        public async Task<T?> OxuAsync<T>(string yol, CancellationToken ct = default)
            where T : class
        {
            var json = await OxuAsync(yol, ct).ConfigureAwait(false);

            if (json is null || json.Count == 0)
            {
                return null;
            }

            try
            {
                return System.Text.Json.JsonSerializer
                    .Deserialize<T>(json, FirebaseOptions.Json);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "Deserializasiya: " + yol);
                return null;
            }
        }

        // --------------------------------------------------------------------
        //  ✍️ YAZMA ✓
        // --------------------------------------------------------------------

        /// <summary>💾 Obyekti yazır / əvəz edir ✓ (PUT ✓)</summary>
        public Task<bool> YazAsync<T>(
            string yol, T obyekt, CancellationToken ct = default) where T : class
        {
            var cism = JsonSerializer.Serialize(obyekt, FirebaseOptions.Json);
            return GönderAsync(HttpMethod.Put, yol, cism, ct);
        }

        /// <summary>
        /// 📤 <b>XAM JSON</b> göndərir ✓✓✓ — offline növbəsindən göndərmək üçün ✓
        /// <para>⚠ Yenidən serializasiya EDİLMİR ✗ (mətn olduğu kimi gedir ✓)</para>
        /// </summary>
        public Task<bool> XamYazAsync(
            string yol, string xamJson, CancellationToken ct = default) =>
            GönderAsync(HttpMethod.Put, yol, xamJson, ct);

        /// <summary>🩹 Yalnız dəyişən sahələri yeniləyir ✓ (PATCH ✓)</summary>
        public Task<bool> YenileAsync(
            string yol, IDictionary<string, object?> saheler, CancellationToken ct = default)
        {
            var cism = JsonSerializer.Serialize(saheler, FirebaseOptions.Json);
            return GönderAsync(HttpMethod.Patch, yol, cism, ct);
        }

        /// <summary>🗑️ Fiziki silir ⚠️ — YALNIZ administrator təmizləməsi üçün ✓</summary>
        public Task<bool> FizikiSilAsync(string yol, CancellationToken ct = default) =>
            GönderAsync(HttpMethod.Delete, yol, null, ct);

        private async Task<bool> GönderAsync(
            HttpMethod metod, string yol, string? cism, CancellationToken ct)
        {
            // ⚠ Offline-dırsa default(bool)=false qaytarır ✓ (növbəyə düşür ✓)
            return await CəhdAsync(async () =>
            {
                using var r = await _o.Client
                    .SendAsync(Sorğu(metod, yol, cism), ct).ConfigureAwait(false);

                if (!r.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"HTTP {(int)r.StatusCode} ({metod.Method} {yol})");
                }

                return true;
            }, $"{metod.Method} {yol}", ct).ConfigureAwait(false);
        }

        // --------------------------------------------------------------------
        //  🔄 TƏKRAR (RETRY + BACKOFF) — əsas Fail-Safe mexanizmi ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>
        /// 🔁 Əməliyyatı <see cref="FirebaseOptions.MaxRetries"/> dəfə təkrarlayır ✓
        /// <para>
        /// ⚠ <b>EXCEPTION ATILMIR</b> ✗✓✓ — şəbəkə qopubsa <c>default</c> qaytarır ✓
        /// (proqram qətiyyən çökmür ✗ · istifadəçi işini davam etdirir ✓)
        /// </para>
        /// </summary>
        private async Task<T?> CəhdAsync<T>(
            Func<Task<T>> əməliyyat, string kontekst, CancellationToken ct)
        {
            var maks = Math.Max(1, _o.MaxRetries);

            for (var cəhd = 1; cəhd <= maks; cəhd++)
            {
                try
                {
                    var nəticə = await əməliyyat().ConfigureAwait(false);

                    if (!Onlayn)
                    {
                        Onlayn = true;
                        AppLogger.Onlayn = true;
                        AppLogger.Melumat("✅ Bulud bağlantısı BƏRPA OLUNDU ✓");
                    }

                    return nəticə;
                }
                catch (FirebaseAuthException ex)
                {
                    // 🔐 Token problemi ✗ — təkrar ETMİRİK ✗
                    Onlayn = false;
                    AppLogger.Xeta(ex, kontekst);
                    return default;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // 🛑 Proqram bağlanır ✓ — normal haldır ✓ loq yazmırıq ✗
                    return default;
                }
                catch (Exception ex) when (CəhdEdiləBilər(ex))
                {
                    Onlayn = false;
                    AppLogger.Onlayn = false;

                    if (cəhd == maks)
                    {
                        AppLogger.Sebeke(ex, kontekst);
                        return default;
                    }

                    // ⏲️ Artan fasilə ✓ (700ms → 1400ms → 2800ms ✓)
                    var fasilə = _o.RetryDelayMs * (int)Math.Pow(2, cəhd - 1);

                    AppLogger.Xeberdarliq(
                        $"🔄 Təkrar {cəhd}/{maks} — {kontekst} ({fasilə}ms ✓)");

                    try
                    {
                        await Task.Delay(fasilə, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { return default; }
                }
                catch (Exception ex)
                {
                    // ❓ Gözlənilməyən xəta ✗ — loqa yaz ✓ və dayan ✓
                    AppLogger.Xeta(ex, kontekst);
                    return default;
                }
            }

            return default;
        }

        /// <summary>🔎 Xəta müvəqqətidir? (təkrar məntiqlidir ✓)</summary>
        private static bool CəhdEdiləBilər(Exception ex) =>
            ex is HttpRequestException
            or System.Net.Sockets.SocketException
            or TaskCanceledException
            or TimeoutException
            or System.IO.IOException;

        // --------------------------------------------------------------------
        //  📡 REAL-TIME CANLI İZLƏMƏ (SSE ✓✓✓) — Firebase «text/event-stream» ✓
        //  Dəyişiklik DƏRHAL gəlir ✓ (polling YOX ✗ · gecikmə ~100ms ✓)
        // --------------------------------------------------------------------
        /// <summary>
        /// 📡 Buluddaki dəyişiklikləri <b>real vaxtda</b> qaytarır ✓✓✓
        /// <para>
        /// Hadisələr: <c>put</c> ✓ · <c>patch</c> ✓ · <c>keep-alive</c> ✓ ·
        /// <c>cancel</c> ✗ · <c>disconnect</c> 🔌 (engine yenidən qoşulur ✓)
        /// </para>
        /// ⚠ İnternet qopanda <b>səssizcə</b> dayanır ✓ — EXCEPTION ATILMIR ✗✓✓
        /// </summary>
        public async IAsyncEnumerable<(string Hadisə, string Yol, JsonObject? Melumat)>
            CanliİzleAsync(string yol, [EnumeratorCancellation] CancellationToken ct)
        {
            var sorğu = new HttpRequestMessage(HttpMethod.Get, _o.Url(yol));
            sorğu.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            System.Net.Http.HttpResponseMessage? cavab = null;
            System.IO.StreamReader? oxucu = null;

            try
            {
                cavab = await _o.Client
                    .SendAsync(sorğu, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                if (cavab.IsSuccessStatusCode)
                {
                    var axın = await cavab.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    oxucu = new System.IO.StreamReader(axın, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Sebeke(ex, "canlı izləmə: " + yol);
            }

            if (oxucu is null)
            {
                try { cavab?.Dispose(); } catch { }
                yield return ("disconnect", "", null); // 🔌 offline ✓
                yield break;
            }

            Onlayn = true;
            AppLogger.Onlayn = true;
            AppLogger.Melumat($"📡 REAL-TIME qoşuldu ✓ — /{yol}");

            var ad = "";

            while (!ct.IsCancellationRequested)
            {
                string? sətr;

                try
                {
                    sətr = await oxucu.ReadLineAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    AppLogger.Sebeke(ex, "canlı oxuma: " + yol);
                    break;
                }

                if (sətr is null) break; // 🚪 axın bağlandı ✓

                if (sətr.StartsWith("event:", StringComparison.Ordinal))
                {
                    ad = sətr[6..].Trim();
                    continue;
                }

                if (sətr.StartsWith("id:", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!sətr.StartsWith("data:", StringComparison.Ordinal))
                {
                    // ⏱️ Boş sətir → hadisə tamamlandı ✓
                    if (sətr.Length == 0 && ad.Length > 0)
                    {
                        if (ad == "cancel")
                        {
                            AppLogger.Xeberdarliq("🚫 Axın ləğv edildi ✗ — yenidən qoşuluruq ✓");
                            break;
                        }

                        if (ad == "auth_revoked")
                        {
                            AppLogger.Xeta(null, "🔐 Token ləğv edildi ✗");
                            break;
                        }

                        ad = "";
                    }

                    continue;
                }

                var xam = sətr[5..].Trim();

                if (xam.Length == 0 || xam == "null") continue;

                JsonObject? qutu = null;

                try
                {
                    qutu = JsonNode.Parse(xam) as JsonObject;
                }
                catch (Exception ex)
                {
                    AppLogger.Xeta(ex, "SSE JSON: " + yol);
                }

                if (qutu is not null)
                {
                    yield return (
                        ad,
                        qutu["path"]?.GetValue<string>() ?? "",
                        qutu["data"] as JsonObject);
                }
            }

            try { oxucu.Dispose(); cavab?.Dispose(); } catch { }

            Onlayn = false;
            AppLogger.Onlayn = false;
            yield return ("disconnect", "", null); // 🔌 engine təkrar qoşulacaq ✓
        }


    }

    /// <summary>🔐 Token icazə xətası ✓ (TƏKRAR EDİLMİR ✗ — boşuna cəhd etmirik ✗)</summary>
    public sealed class FirebaseAuthException : Exception
    {
        public FirebaseAuthException(string mesaj) : base(mesaj) { }
    }
}
