// ============================================================================
//  🗃️ 021Cars — FIREBASE REPOSITORY (6/6)  ★ SQL-in ƏVƏZİ ★
// ----------------------------------------------------------------------------
//  ✅ Hər domain tipi üçün BİR kolleksiya ✓ (cars ✓ · customers ✓ · sales ✓ …)
//  ✅ Bütün oxumalar SOFT-DELETE süzgəcindən keçir ✓ (isDeleted = false ✓)
//  ✅ Yazmada UpdatedAt/UpdatedBy AVTOMATİK qeyd olunur ✓✓✓
//  ✅ LWW merge ✓ — konfliktlərdə ən TƏZƏ nüsxə üstün ✓
//  ✅ Offline → daxili növbəyə düşür ✓ (JSON ✓ — SQL DEYİL ✗)
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Cas0201.Firebase
{
    /// <summary>
    /// 🗃️ <b>ÜMUMİ BULUD REPOSITORY</b> ✓✓✓
    /// <para>
    /// Misal: <c>new FirebaseRepository&lt;Avtomobil&gt;(klient, "cars")</c> ✓
    /// → bütün CRUD əməliyyatları birbaşa Firebase RTDB-də ✓
    /// </para>
    /// </summary>
    /// <typeparam name="T">Əsası <see cref="FirebaseEntity"/> olan tip ✓</typeparam>
    public class FirebaseRepository<T> where T : FirebaseEntity
    {
        private readonly FirebaseRestClient _klient;
        private readonly FirebaseOptions _o;
        private readonly object _qapi = new();

        /// <summary>💾 Yerli keş ✓ (UI ani cavab ✓ · real-time yeniləyir ✓)</summary>
        private readonly Dictionary<string, T> _kes = new();

        /// <summary>📋 OFFLINE NÖVBƏSİ ✓ (internet yox → burada saxlanılır ✓ sonra göndərilir ✓)</summary>
        private readonly List<(string Yol, string Cism)> _növbə = new();

        /// <summary>🗂️ Kolleksiya adı ✓ (məs. <c>"cars"</c> ✓)</summary>
        public string Kolleksiya { get; }

        /// <summary>📣 Real-time dəyişiklik ✓ (UI abunə olur ✓)</summary>
        public event Action<T>? Deyisdi;

        /// <summary>🗑️ Silinmə hadisəsi ✓</summary>
        public event Action<string>? Silindi;

        public FirebaseRepository(FirebaseRestClient klient, string kolleksiya)
        {
            _klient = klient ?? throw new ArgumentNullException(nameof(klient));
            _o = klient.Options ?? throw new ArgumentNullException(nameof(klient.Options));

            Kolleksiya = kolleksiya?.Trim('/')
                ?? throw new ArgumentNullException(nameof(kolleksiya));
        }

        /// <summary>🛣️ Kolleksiya yolu ✓ (<c>cars</c> ✓)</summary>
        public string Kok => Kolleksiya;

        /// <summary>🛣️ Tək obyekt yolu ✓ (<c>cars/{id}</c> ✓)</summary>
        public string Yol(string id) => $"{Kolleksiya}/{id}";

        // --------------------------------------------------------------------
        //  📥 OXUMA ✓
        // --------------------------------------------------------------------

        /// <summary>
        /// 📥 <b>BÜTÜN AKTİV QEYDLƏRİ</b> oxuyur ✓✓✓
        /// <para>🗑️ Soft-delete olunmuşlar <b>QAYTARILMIR</b> ✗ (süzgəc ✓)</para>
        /// </summary>
        public async Task<List<T>> HamisiniAlAsync(
            bool zibilDaxil = false, CancellationToken ct = default)
        {
            var neticə = new List<T>();

            try
            {
                var xam = await _klient.OxuAsync(Kok, ct).ConfigureAwait(false);

                if (xam is null) return neticə; // 🔌 offline → boş siyahı ✓ (çökmə YOX ✗)

                foreach (var cüt in xam)
                {
                    try
                    {
                        if (cüt.Value is not JsonObject o) continue;

                        // 🆔 Açar yoxdursa doldur ✓
                        if (o["id"] is null || string.IsNullOrWhiteSpace(o["id"]?.ToString()))
                        {
                            o["id"] = cüt.Key;
                        }

                        var element = System.Text.Json.JsonSerializer
                            .Deserialize<T>(o, FirebaseOptions.Json);

                        if (element is null) continue;

                        // 🔑 Firebase açarı həmişə üstün ✓
                        if (element.Id != cüt.Key) element.Id = cüt.Key;

                        // 🗑️ SOFT-DELETE SÜZGƏCİ ✓✓✓
                        if (!zibilDaxil && element.IsDeleted) continue;

                        neticə.Add(element);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Xeta(ex, $"{Kolleksiya} element oxu: {cüt.Key}");
                    }
                }

                // 💾 Keşi yenilə ✓
                lock (_qapi)
                {
                    _kes.Clear();

                    foreach (var e in neticə.Where(x => !x.IsDeleted))
                    {
                        _kes[e.Id] = e;
                    }
                }

                AppLogger.Melumat($"📥 {Kolleksiya}: {neticə.Count} qeyd oxundu ✓");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, $"{Kolleksiya} oxu");
            }

            return neticə;
        }

        /// <summary>📥 Tək qeydi oxuyur ✓ (soft-delete olunmuşsa <c>null</c> ✓)</summary>
        public async Task<T?> AlAsync(string id, CancellationToken ct = default)
        {
            try
            {
                var e = await _klient.OxuAsync<T>(Yol(id), ct).ConfigureAwait(false);

                if (e is null) return null;

                e.Id = id;
                return e.IsDeleted ? null : e; // 🗑️ süzgəc ✓
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, $"{Kolleksiya} tək oxu: {id}");
                return null;
            }
        }

        /// <summary>💾 Keşdən ani oxu ✓ (UI şəbəkə gözləmir ✓)</summary>
        public List<T> KesdenAl()
        {
            lock (_qapi)
            {
                return _kes.Values.Where(x => !x.IsDeleted).ToList();
            }
        }

        // --------------------------------------------------------------------
        //  ✍️ YAZMA — hər yazmada UpdatedAt AVTOMATİK ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>
        /// 💾 Qeydi buluda yazır ✓ (yeni ✓ / mövcud ✓ — artıq fərq etmir ✓)
        /// <para>
        /// ① <see cref="FirebaseEntity.Toxun"/> çağırılır ✓ (UpdatedAt ✓ UpdatedBy ✓)<br/>
        /// ② Yazılır → keş yenilənir ✓ → <see cref="Deyisdi"/> hadisəsi atılır ✓<br/>
        /// ③ 🔌 Offline-dırsa → <b>növbəyə</b> düşür ✓ (bağlantı gələndə göndərilir ✓)
        /// </para>
        /// </summary>
        public async Task<bool> YazAsync(T element, CancellationToken ct = default)
        {
            try
            {
                if (element is null) return false;

                if (string.IsNullOrWhiteSpace(element.Id))
                {
                    element.Id = FirebaseOptions.YeniId();
                }

                // ⏱️ AUDİT — MÜTLƏQ ✓✓✓
                element.Toxun(_o.CihazAdi, element.IsDeleted);

                var cism = System.Text.Json.JsonSerializer
                    .Serialize(element, FirebaseOptions.Json);

                var uğur = await _klient.YazAsync(Yol(element.Id), element, ct)
                    .ConfigureAwait(false);

                if (!uğur)
                {
                    NövbəyəƏlavəEt(Yol(element.Id), cism);
                    return false;
                }

                KesəYaz(element);
                Deyisdi?.Invoke(element);

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, $"{Kolleksiya} yazma: {element?.Id}");
                return false;
            }
        }

        /// <summary>
        /// 🗑️ <b>SOFT DELETE</b> ✓✓✓ — qeyd bazadan <b>SİLİNMİR</b> ✗
        /// <para>
        /// Yalnız <c>isDeleted = true</c> + <c>deletedAt</c> yazılır ✓
        /// (≈ SQL-dəki <c>UPDATE … SET is_deleted = 1</c> ✓ eynidir ✓)
        /// </para>
        /// </summary>
        public async Task<bool> SoftSilAsync(string id, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return false;

                var indi = FirebaseOptions.UtcIndi();

                var uğur = await _klient.YenileAsync(Yol(id), new Dictionary<string, object?>
                {
                    ["isDeleted"] = true,
                    ["deletedAt"] = indi,
                    ["updatedAt"] = indi,
                    ["updatedBy"] = _o.CihazAdi
                }, ct).ConfigureAwait(false);

                if (!uğur)
                {
                    NövbəyəƏlavəEt(Yol(id), System.Text.Json.JsonSerializer.Serialize(
                        new Dictionary<string, object?>
                        {
                            ["isDeleted"] = true,
                            ["deletedAt"] = indi,
                            ["updatedAt"] = indi,
                            ["updatedBy"] = _o.CihazAdi
                        }, FirebaseOptions.Json));

                    return false;
                }

                lock (_qapi) { _kes.Remove(id); }

                Silindi?.Invoke(id);
                AppLogger.Melumat($"🗑️ {Kolleksiya}/{id} SOFT-DELETE edildi ✓ (bərpa mümkündür ✓)");

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, $"{Kolleksiya} soft-sil: {id}");
                return false;
            }
        }

        /// <summary>♻️ Zibil qutusundan bərpa ✓ (isDeleted = false ✓)</summary>
        public async Task<bool> BerpaEtAsync(T element, CancellationToken ct = default)
        {
            try
            {
                if (element is null) return false;

                element.Berpa(_o.CihazAdi); // ♻️ isDeleted=false + deletedAt=null ✓
                return await YazAsync(element, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, $"{Kolleksiya} bərpa: {element?.Id}");
                return false;
            }
        }

        // --------------------------------------------------------------------
        //  ⚖️ LWW MERGE — REAL-TIME KONFLİKT HƏLLİ ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>
        /// ⚖️ Gələn siyahını keşlə <b>LWW</b> ilə birləşdirir ✓✓✓
        /// <para>
        /// Yalnız <b>həqiqətən dəyişmişləri</b> yazır ✓ (dövrə olmur ✗✓✓)
        /// · nəticə: ən təzə nüsxə hər iki cihazda EYNİ olur ✓
        /// </para>
        /// </summary>
        public async Task<int> MergeEtAsync(
            IEnumerable<T> gələnlər, CancellationToken ct = default)
        {
            var sayğac = 0;

            try
            {
                foreach (var gələn in gələnlər ?? Enumerable.Empty<T>())
                {
                    if (ct.IsCancellationRequested) break;
                    if (gələn is null) continue;

                    T? yerli;

                    lock (_qapi)
                    {
                        _kes.TryGetValue(gələn.Id, out yerli);
                    }

                    // ⚖️ Hansı təzədir? ✓
                    var üstün = FirebaseEntity.UstunTut(gələn, yerli);

                    // 🔍 Dəyişiklik yoxdursa yazmırıq ✓ (sonsuz dövrə YOX ✗✓✓)
                    if (üstün is null ||
                        (yerli is not null &&
                         yerli.UpdatedAtUtc >= gələn.UpdatedAtUtc))
                    {
                        continue;
                    }

                    var uğur = await _klient.YazAsync(Yol(gələn.Id), gələn, ct)
                        .ConfigureAwait(false);

                    if (uğur)
                    {
                        KesəYaz(gələn);

                        if (!gələn.IsDeleted) Deyisdi?.Invoke(gələn);

                        sayğac++;
                    }
                }

                if (sayğac > 0)
                {
                    AppLogger.Melumat($"⚖️ {Kolleksiya}: {sayğac} konflikt həll edildi ✓");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, $"{Kolleksiya} merge");
            }

            return sayğac;
        }

        /// <summary>🗑️ Zibil qutusu ✓ (soft-delete olunmuşlar ✓ — bərpa üçün ✓)</summary>
        public async Task<List<T>> ZibilAsync(CancellationToken ct = default)
        {
            var hamısı = await HamisiniAlAsync(zibilDaxil: true, ct).ConfigureAwait(false);
            return hamısı.Where(x => x.IsDeleted).OrderByDescending(x => x.DeletedAt).ToList();
        }

        /// <summary>💾 Keşi yeniləyir ✓ (daxili ✓)</summary>
        private void KesəYaz(T element)
        {
            try
            {
                lock (_qapi)
                {
                    if (element.IsDeleted) _kes.Remove(element.Id);
                    else _kes[element.Id] = element;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "keş yazma");
            }
        }

        // --------------------------------------------------------------------
        //  📋 OFFLINE NÖVBƏSİ — İNTERNET QOPANDA İTİRMƏMƏK ÜÇÜN ✓✓✓
        // --------------------------------------------------------------------

        /// <summary>📋 Növbədəki əməliyyat sayı ✓ (UI indikatoru ✓)</summary>
        public int NövbəSayı
        {
            get { lock (_qapi) { return _növbə.Count; } }
        }

        /// <summary>
        /// 📋 Növbəyə əlavə edir ✓
        /// <para>
        /// ⚠ Eyni yol varsa <b>əvvəlkini əvəz edir</b> ✓ — yalnız SON vəziyyət göndərilir ✓
        /// (növbə şişmir ✗✓✓)
        /// </para>
        /// </summary>
        private void NövbəyəƏlavəEt(string yol, string cism)
        {
            try
            {
                lock (_qapi)
                {
                    _növbə.RemoveAll(x => x.Yol == yol);
                    _növbə.Add((yol, cism));

                    // 💾 Diskə də yaz ✓ (proqram bağlansa itməsin ✓)
                    NövbəniYaddaSaxla();
                }

                AppLogger.Xeberdarliq(
                    $"📋 OFFLINE növbəyə düşdü ✓ — {yol} (cəmi: {NövbəSayı})");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "növbəyə əlavə");
            }
        }

        /// <summary>📤 Növbəni buluda göndərir ✓ (bağlantı bərpa olunanda ✓)</summary>
        public async Task<int> NövbəniBoşaltAsync(CancellationToken ct = default)
        {
            var göndərilən = 0;

            List<(string Yol, string Cism)> kopya;

            lock (_qapi)
            {
                kopya = _növbə.ToList();
            }

            foreach (var (yol, cism) in kopya)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    // 📤 Xam JSON göndərilir ✓ (yenidən serializasiya YOX ✗)
                    var uğur = await _klient.XamYazAsync(yol, cism, ct).ConfigureAwait(false);

                    if (!uğur) break; // 🔌 hələ də offline ✗ → dayan ✓ (növbə qalır ✓)

                    lock (_qapi)
                    {
                        _növbə.RemoveAll(x => x.Yol == yol);
                        NövbəniYaddaSaxla();
                    }

                    göndərilən++;
                }
                catch (Exception ex)
                {
                    AppLogger.Xeta(ex, "növbə göndərmə: " + yol);
                    break;
                }
            }

            if (göndərilən > 0)
            {
                AppLogger.Melumat($"📤 OFFLINE növbə göndərildi ✓ ({göndərilən} əməliyyat ✓)");
            }

            return göndərilən;
        }

        /// <summary>💾 Növbəni JSON faylına yazır ✓ (SQL DEYİL ✗✓✓)</summary>
        private void NövbəniYaddaSaxla()
        {
            try
            {
                var fayl = Path.Combine(
                    Path.GetDirectoryName(_o.QueuePath) ?? ".", $"queue_{Kolleksiya}.json");

                var qovluq = Path.GetDirectoryName(fayl)!;
                if (!Directory.Exists(qovluq)) Directory.CreateDirectory(qovluq);

                File.WriteAllText(fayl, System.Text.Json.JsonSerializer.Serialize(
                    _növbə.Select(x => new Dictionary<string, string>
                    {
                        ["yol"] = x.Yol,
                        ["cism"] = x.Cism
                    }), FirebaseOptions.Json));
            }
            catch
            {
                // 🛡️ disk problemi ✗ → növbə yaddaşda qalır ✓
            }
        }

        // --------------------------------------------------------------------
        //  📡 CANLI REAL-TIME İZLƏMƏ — BULUD → TƏTBİQ (saniyələr içində ✓)
        // --------------------------------------------------------------------

        /// <summary>
        /// 📡 <b>Kolleksiyanı REAL-TIME izləyir</b> ✓✓✓
        /// <para>
        /// ⚙️ Başqa cihaz dəyişiklik edəndə <b>dərhal</b> <see cref="Deyisdi"/> hadisəsi atılır ✓<br/>
        /// 🔌 Bağlantı qopanda <b>səssizcə</b> gözləyir ✓ → 2·4·6… san sonra təkrar qoşulur ✓ (max 30 ✓)<br/>
        /// 📤 Hər təkrar qoşulmada offline növbəsi də boşaldılır ✓✓✓
        /// </para>
        /// </summary>
        public async Task CanliBaslaAsync(CancellationToken ct)
        {
            var təkrar = 0;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await foreach (var (hadisə, yol, melumat) in
                        _klient.CanliİzleAsync(Kok, ct).ConfigureAwait(false))
                    {
                        // 🔌 axın bağlandı ✓ → təkrar qoşulma blokuna düşür ✓
                        if (hadisə is "disconnect" or "cancel") break;

                        if (hadisə == "keep-alive")
                        {
                            təkrar = 0; // ✅ bağlantı həqiqətən canlıdır ✓
                            continue;
                        }

                        if (melumat is null) continue;

                        // 🆔 id = yolun son seqmenti ✓ (məs. "/abc123" → "abc123" ✓)
                        var id = (yol ?? "").Trim('/');

                        if (id.Contains('/'))
                        {
                            id = id[(id.LastIndexOf('/') + 1)..];
                        }

                        if (id.Length == 0)
                        {
                            id = melumat["id"]?.ToString() ?? "";
                        }

                        if (id.Length == 0) continue;

                        if (melumat["id"] is null) melumat["id"] = id;

                        var element = System.Text.Json.JsonSerializer
                            .Deserialize<T>(melumat, FirebaseOptions.Json);

                        if (element is null) continue;

                        element.Id = id;
                        KesəYaz(element);

                        if (element.IsDeleted)
                        {
                            Silindi?.Invoke(id); // 🗑️ başqa cihaz sildi ✓
                        }
                        else
                        {
                            Deyisdi?.Invoke(element); // ✨ dəyişiklik gəldi ✓
                        }
                    }

                    təkrar = 0;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    AppLogger.Sebeke(ex, $"canlı izləmə: {Kolleksiya}");
                }

                if (ct.IsCancellationRequested) break;

                // ⏲️ ARTAN FASİLƏ İLƏ TƏKRAR QOŞULMA ✓ (2 → 4 → 6 … 30 san ✓)
                təkrar++;
                var fasilə = Math.Min(30, 2 * təkrar);
                AppLogger.Xeberdarliq($"🔄 {Kolleksiya}: {fasilə} san sonra təkrar qoşulma ✓");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(fasilə), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                // 📤 Offline növbəsini də boşalt ✓
                await NövbəniBoşaltAsync(ct).ConfigureAwait(false);
            }
        }



    }
}
