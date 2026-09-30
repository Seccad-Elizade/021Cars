// ============================================================================
//  🚀 021Cars — AVTOMATİK GÜNCƏLLƏMƏ XİDMƏTİ ✓✓✓
// ----------------------------------------------------------------------------
//  ✅ 🌐 GitHub Releases-dən yoxlayır ✓  (hər 10 dəqiqədə ✓ + hər AÇILIŞDA ✓)
//  ✅ 🎉 Yeni versiya varsa → popup:  [🚀 GÜNCƏLLƏ]  [⏰ SONRA] ✓
//  ✅ 🚀 «GÜNCƏLLƏ» → installer yüklənir (⬇️ faizlə ✓) → proqram özü bağlanır ✗
//     → installer faylları əvəz edir ✓ → proqram YENİDƏN açılır ✓✓✓
//  ✅ ⏰ «SONRA» → bu sessiyada bir daha soruşulmur ✗ (növbəti AÇILIŞDA gəlir ✓)
//  🛡️ HEÇ BİR xəta proqramı çökdürmür ✗ (hər şey try-catch ilə ✓)
//  📌 Depo:  github.com/Seccad-Elizade/021Cars  (Public ✓)
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>📦 GitHub-dan tapılan güncəlləmə məlumatı ✓</summary>
    public sealed class GuncellemeMelumati
    {
        /// <summary>🏷️ Versiya (məs. <c>6.1.0</c> ✓ — <c>v</c> işarəsi silinmiş ✓)</summary>
        public string Versiya { get; set; } = "";

        /// <summary>🏷️ GitHub teqi (məs. <c>v6.1</c> ✓)</summary>
        public string Teq { get; set; } = "";

        /// <summary>📛 Buraxılışın adı ✓</summary>
        public string Ad { get; set; } = "";

        /// <summary>📝 Qeydlər (nə dəyişdi ✓)</summary>
        public string Qeyd { get; set; } = "";

        /// <summary>⬇️ Installer faylının yükləmə ünvanı ✓</summary>
        public string YuklemeUrl { get; set; } = "";

        /// <summary>🌐 Buraxılış səhifəsi ✓</summary>
        public string SehifeUrl { get; set; } = "";

        /// <summary>📅 Buraxılış tarixi ✓</summary>
        public DateTime? Tarix { get; set; }

        /// <summary>📊 Faylın ölçüsü (bayt ✓)</summary>
        public long Olcu { get; set; }

        /// <summary>📖 Göstərmək üçün qısa mətn ✓</summary>
        public string Qisa
        {
            get
            {
                var n = (Qeyd ?? "").Replace("\r", "").Trim();
                if (n.Length > 420) n = n[..420] + "…";
                return n;
            }
        }
    }

    /// <summary>
    /// 🚀 <b>AVTOMATİK GÜNCƏLLƏMƏ</b> ✓✓✓ — GitHub Releases ilə işləyir ✓
    /// <para>10 dəqiqədən bir ✓ + hər açılışda ✓ yoxlayır ✓</para>
    /// </summary>
    public static class GuncellemeXidmeti
    {
        // ══════════════════════════════════════════════════════════════════
        //  ⚙️ AYARLAR ✓
        // ══════════════════════════════════════════════════════════════════

        /// <summary>👤 GitHub sahibi ✓</summary>
        public const string Sahib = "Seccad-Elizade";

        /// <summary>📦 Depo adı ✓</summary>
        public const string Depo = "021Cars";

        /// <summary>📛 Installer faylının adı ✓ (Release asset ✓)</summary>
        public const string AssetAdi = "021Cars_Installer.exe";

        /// <summary>⏱️ Yoxlama fasiləsi ✓ — <b>30 DƏQİQƏ</b> ✓✓✓ (avtomatik popup ✓)</summary>
        public static readonly TimeSpan Fasile = TimeSpan.FromMinutes(30);

        /// <summary>🏷️ API ünvanı ✓</summary>
        private static string ApiYolu =>
            $"https://api.github.com/repos/{Sahib}/{Depo}/releases/latest";

        /// <summary>🌐 Buraxılışlar səhifəsi ✓ (əl ilə baxmaq üçün ✓)</summary>
        public static string BuraxilisSehifesi =>
            $"https://github.com/{Sahib}/{Depo}/releases";

        // ══════════════════════════════════════════════════════════════════
        //  📊 GÜNCƏLLƏMƏ VƏZİYYƏTİ ✓ — 🚀 düymənin rəngi üçün ✓
        // ══════════════════════════════════════════════════════════════════

        /// <summary>🎨 Güncəlləmə vəziyyəti ✓</summary>
        public enum Vəziyyət
        {
            /// <summary>❔ Hələ yoxlanılmayıb ✓</summary>
            Yoxlanilmadi,

            /// <summary>🔍 Hazırda yoxlanılır ✓</summary>
            Yoxlanilir,

            /// <summary>🟢 Ən son versiyadadır ✓</summary>
            EnSon,

            /// <summary>🎉 Yeni versiya var ✓</summary>
            Yenivar,

            /// <summary>🔴 Yoxlana bilmədi ✗ (internet yoxdur ✓)</summary>
            Xeta
        }

        /// <summary>📊 Cari vəziyyət ✓</summary>
        public static Vəziyyət CariVəziyyət { get; private set; } = Vəziyyət.Yoxlanilmadi;

        /// <summary>📢 Vəziyyət dəyişdi ✓ (UI düyməsi yenilənir ✓)</summary>
        public static event Action? VeziyyetDeyisdi;

        /// <summary>📊 Vəziyyəti dəyişir ✓</summary>
        private static void VeziyyetQur(Vəziyyət yeni)
        {
            try
            {
                CariVəziyyət = yeni;
                VeziyyetDeyisdi?.Invoke();
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════
        //  📊 VƏZİYYƏT ✓
        // ══════════════════════════════════════════════════════════════════

        /// <summary>🏷️ <b>Cari versiya</b> ✓ (assembly-dən ✓ — csproj <c>&lt;Version&gt;</c>)</summary>
        public static Version CariVersiya
        {
            get
            {
                try
                {
                    var a = typeof(GuncellemeXidmeti).Assembly;

                    return a.GetName().Version ?? new Version(6, 0, 0);
                }
                catch
                {
                    return new Version(6, 0, 0);
                }
            }
        }

        /// <summary>🔢 Göstərmək üçün versiya ✓ (6.1 → «6.1» ✓)</summary>
        public static string CariVersiyaMetni
        {
            get
            {
                var v = CariVersiya;

                return v.Build > 0
                    ? $"{v.Major}.{v.Minor}.{v.Build}"
                    : $"{v.Major}.{v.Minor}";
            }
        }

        /// <summary>📦 Son tapılan güncəlləmə ✓ (yoxdursa <c>null</c> ✓)</summary>
        public static GuncellemeMelumati? Tapilan { get; private set; }

        /// <summary>🕒 Son yoxlama vaxtı ✓</summary>
        public static DateTime? SonYoxlama { get; private set; }

        /// <summary>⏰ «SONRA» basılıb? ✓ (bu sessiyada təkrar soruşulmur ✗ — növbəti açılışda gəlir ✓)</summary>
        public static bool SonrayaAtildi { get; private set; }

        /// <summary>🔒 Yoxlama gedir? ✓ (paralel çağırışların qarşısı ✓)</summary>
        private static bool _gedir;

        /// <summary>🌐 Ümumi HTTP klienti ✓</summary>
        private static readonly HttpClient Http = HttpYarat();

        /// <summary>📢 Güncəlləmə tapıldı ✓ (UI abunə olur ✓)</summary>
        public static event Action<GuncellemeMelumati>? Tapildi;

        /// <summary>⏰ «SONRA» — bu sessiyada bir daha göstərilmir ✗</summary>
        public static void SonrayaAt() => SonrayaAtildi = true;

        /// <summary>🔁 Yenidən göstərməyə icazə ✓ (əl ilə yoxlama ✓)</summary>
        public static void Sifirla() => SonrayaAtildi = false;

        /// <summary>🌐 HTTP klienti ✓ (GitHub «User-Agent» tələb edir ✗✓✓)</summary>
        private static HttpClient HttpYarat()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };

            try
            {
                h.DefaultRequestHeaders.UserAgent.ParseAdd("021Cars-Updater/1.0");
                h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                h.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            }
            catch { }

            return h;
        }

        // ══════════════════════════════════════════════════════════════════
        //  🌐 ① YOXLAMA ✓✓✓ — GitHub-dan ən son buraxılışı oxuyur ✓
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 🌐 <b>GÜNCƏLLƏMƏNİ YOXLAYIR</b> ✓✓✓
        /// <para>Yeni versiya varsa <see cref="Tapildi"/> hadisəsi işə düşür ✓</para>
        /// </summary>
        /// <returns>Yeni versiya varsa məlumat ✓ · yoxdursa <c>null</c> ✓</returns>
        public static async Task<GuncellemeMelumati?> YoxlaAsync(CancellationToken ct = default)
        {
            if (_gedir) return Tapilan;      // 🔒 artıq yoxlanılır ✓
            _gedir = true;

            VeziyyetQur(Vəziyyət.Yoxlanilir);   // 🔍 düymə «yoxlanılır» olur ✓

            try
            {
                using var cavab = await Http.GetAsync(ApiYolu, ct).ConfigureAwait(false);

                SonYoxlama = DateTime.Now;

                // ℹ️ 404 = hələ HEÇ BİR buraxılış yoxdur ✗ (problem deyil ✓)
                if (!cavab.IsSuccessStatusCode)
                {
                    Cas0201.Firebase.AppLogger.Melumat(
                        $"🚀 Güncəlləmə yoxlaması: {(int)cavab.StatusCode} ✓ (buraxılış yoxdur ✗)");

                    VeziyyetQur(Vəziyyət.EnSon);   // 🟢 ən son versiyadadır ✓
                    return null;
                }

                var json = await cavab.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var m = JsonSənədOxu(json);

                if (m is null)
                {
                    Cas0201.Firebase.AppLogger.Melumat("🚀 Güncəlləmə yoxlaması: buraxılış oxunmadı ✗");
                    VeziyyetQur(Vəziyyət.EnSon);
                    return null;
                }

                // ⚖️ Versiya müqayisəsi ✓ — yalnız YENİ varsa ✓
                if (!Yenidir(m.Versiya))
                {
                    Cas0201.Firebase.AppLogger.Melumat(
                        $"🚀 Güncəlləmə yox ✓ — cari {CariVersiyaMetni} · son {m.Versiya}");

                    Tapilan = null;
                    VeziyyetQur(Vəziyyət.EnSon);
                    return null;
                }

                Tapilan = m;
                VeziyyetQur(Vəziyyət.Yenivar);   // 🎉 yeni versiya var ✓

                Cas0201.Firebase.AppLogger.Melumat(
                    $"🎉 YENİ GÜNCƏLLƏMƏ TAPILDI ✓ — {CariVersiyaMetni} → {m.Versiya} ✓");

                try { Tapildi?.Invoke(m); } catch { }

                return m;
            }
            catch (Exception ex)
            {
                // 🛡️ İnternet yoxdur ✗ / GitHub əlçatmaz ✗ → SƏSSİZCƏ keçir ✓
                Cas0201.Firebase.AppLogger.Melumat("🚀 Güncəlləmə yoxlanmadı ✓ — " + ex.Message);
                VeziyyetQur(Vəziyyət.Xeta);   // 🔴 yoxlana bilmədi ✗
                return null;
            }
            finally
            {
                _gedir = false;
            }
        }

        /// <summary>📄 GitHub JSON cavabını <see cref="GuncellemeMelumati"/>-ya çevirir ✓</summary>
        private static GuncellemeMelumati? JsonSənədOxu(string json)
        {
            try
            {
                using var sənəd = JsonDocument.Parse(json);
                var kök = sənəd.RootElement;

                var teq = kök.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";

                var m = new GuncellemeMelumati
                {
                    Teq = teq,
                    Versiya = TəmizVersiya(teq),
                    Ad = kök.TryGetProperty("name", out var ad) ? ad.GetString() ?? "" : "",
                    Qeyd = kök.TryGetProperty("body", out var q) ? q.GetString() ?? "" : "",
                    SehifeUrl = kök.TryGetProperty("html_url", out var s) ? s.GetString() ?? "" : ""
                };

                if (kök.TryGetProperty("published_at", out var tar) &&
                    DateTime.TryParse(tar.GetString(), out var dt))
                {
                    m.Tarix = dt;
                }

                // 📛 Installer faylını tap ✓ (yoxdursa ilk .exe ✓)
                if (kök.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    JsonElement? seçilən = null;

                    foreach (var a in assets.EnumerateArray())
                    {
                        var n = a.TryGetProperty("name", out var na) ? na.GetString() ?? "" : "";

                        if (seçilən is null && n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            seçilən = a;
                        }

                        if (string.Equals(n, AssetAdi, StringComparison.OrdinalIgnoreCase))
                        {
                            seçilən = a;
                            break;
                        }
                    }

                    if (seçilən is JsonElement sa)
                    {
                        m.YuklemeUrl = sa.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString() ?? "" : "";

                        m.Olcu = sa.TryGetProperty("size", out var o) && o.TryGetInt64(out var uzun)
                            ? uzun : 0;
                    }
                }

                if (string.IsNullOrWhiteSpace(m.YuklemeUrl)) return null;
                if (string.IsNullOrWhiteSpace(m.Versiya)) return null;

                return m;
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "güncəlləmə JSON");
                return null;
            }
        }

        /// <summary>🏷️ <c>v6.1.2</c> → <c>6.1.2</c> ✓ (yalnız rəqəm və nöqtə qalır ✓)</summary>
        public static string TəmizVersiya(string? teq)
        {
            try
            {
                var s = (teq ?? "").Trim().TrimStart('v', 'V').Trim();

                var təmiz = new System.Text.StringBuilder();

                foreach (var c in s)
                {
                    if (char.IsDigit(c) || c == '.') təmiz.Append(c);
                    else if (c == '-') break;          // 🚧 «-beta» kimi sonluqlar kəsilir ✓
                }

                var nəticə = təmiz.ToString().Trim('.');

                // «6» → «6.0» ✓ · «6.1.» → «6.1» ✓
                var hissə = nəticə.Split('.');

                if (hissə.Length == 1) return nəticə + ".0";

                return nəticə;
            }
            catch
            {
                return "";
            }
        }

        /// <summary>⚖️ Tapılan versiya CARİDƏN YENİDİR? ✓</summary>
        public static bool Yenidir(string? versiya)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(versiya)) return false;

                if (!Version.TryParse(TəmizVersiya(versiya), out var uzaq)) return false;

                var cari = CariVersiya;

                // 4 hissəyə tamamlayırıq ✓ (6.1 → 6.1.0.0 ✓)
                var u = new Version(uzaq.Major, uzaq.Minor,
                    Math.Max(uzaq.Build, 0), Math.Max(uzaq.Revision, 0));

                var c = new Version(cari.Major, cari.Minor,
                    Math.Max(cari.Build, 0), Math.Max(cari.Revision, 0));

                return u > c;
            }
            catch
            {
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  ⬇️ ② YÜKLƏMƏ ✓✓✓ — installer faylını TEMP-ə endirir ✓
        // ══════════════════════════════════════════════════════════════════

        /// <summary>📂 Yüklənən installer-in saxlandığı qovluq ✓</summary>
        public static string YolQovlug
        {
            get
            {
                var q = Path.Combine(Path.GetTempPath(), "021Cars_Guncelleme");

                try { Directory.CreateDirectory(q); } catch { }

                return q;
            }
        }

        /// <summary>
        /// ⬇️ <b>INSTALLER-İ YÜKLƏYİR</b> ✓✓✓ — faizlə ✓ (progress ✓)
        /// </summary>
        /// <returns>Uğurlu olarsa faylın yolu ✓ · olmazsa <c>null</c> ✓</returns>
        public static async Task<string?> YukleAsync(
            GuncellemeMelumati m, IProgress<double>? faiz = null, CancellationToken ct = default)
        {
            try
            {
                if (m is null || string.IsNullOrWhiteSpace(m.YuklemeUrl)) return null;

                var hedef = Path.Combine(YolQovlug, AssetAdi);

                // 🧹 köhnə fayl varsa silinir ✓ (kilidlidirsə ad dəyişir ✓)
                try { if (File.Exists(hedef)) File.Delete(hedef); }
                catch { hedef = Path.Combine(YolQovlug, $"setup_{DateTime.Now:HHmmss}.exe"); }

                Cas0201.Firebase.AppLogger.Melumat($"⬇️ Güncəlləmə yüklənir ✓ → {hedef}");

                using var cavab = await Http
                    .GetAsync(m.YuklemeUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                cavab.EnsureSuccessStatusCode();

                var cəmi = cavab.Content.Headers.ContentLength ?? m.Olcu;

                await using (var giriş = await cavab.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
                await using (var çıxış = new FileStream(hedef, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true))
                {
                    var bufer = new byte[81920];
                    long indi = 0;
                    int oxundu;

                    while ((oxundu = await giriş.ReadAsync(bufer, ct).ConfigureAwait(false)) > 0)
                    {
                        await çıxış.WriteAsync(bufer.AsMemory(0, oxundu), ct).ConfigureAwait(false);
                        indi += oxundu;

                        if (cəmi > 0)
                        {
                            try { faiz?.Report(indi * 100.0 / cəmi); } catch { }
                        }
                    }
                }

                var ölçü = new FileInfo(hedef).Length;

                // ⚠️ Yarımçıq yüklənmə ✗ (10 MB-dan kiçik ✗) → uğursuz ✓
                if (ölçü < 10 * 1024 * 1024)
                {
                    Cas0201.Firebase.AppLogger.Melumat($"⚠️ Yükləmə yarımçıq ✗ — yalnız {ölçü / 1024 / 1024} MB ✓");
                    try { File.Delete(hedef); } catch { }
                    return null;
                }

                // 🧹 ⚠️ WINDOWS «İNTERNETDƏN YÜKLƏNDİ» İŞARƏSİ SİLİNİR ✓✓✓ — ★ VACİB ★
                //    (Zone.Identifier ✗ → SmartScreen installer-i bloklaya bilər ✗
                //     → istifadəçi «heç nə olmur» deyir ✗✓✓)
                try
                {
                    var zona = hedef + ":Zone.Identifier";
                    if (File.Exists(zona)) File.Delete(zona);
                }
                catch { }

                Cas0201.Firebase.AppLogger.Melumat($"✅ Güncəlləmə yükləndi ✓ — {ölçü / 1024 / 1024} MB ✓");
                return hedef;
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "güncəlləmə yükləmə");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  🚀 ③ TƏTBİQ ✓✓✓ — installer-i işə salır ✓ və proqram bağlanır ✗
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 🚪 <b>PROQRAM BAĞLANMALIDIR</b> ✓✓✓ — WPF tətbiqi bu hadisəyə abunə olur ✓
        /// <para>
        /// ⚠️ Bu xidmət WPF-dən ASILI DEYİL ✗ (Veb layihəsi də onu kompilyasiya edir ✓)
        /// — buna görə <c>Application.Shutdown()</c> BURADA ÇAĞIRILMIR ✗✓✓
        /// </para>
        /// </summary>
        public static event Action? CixisLazim;

        /// <summary>
        /// 🚀 <b>GÜNCƏLLƏMƏNİ BAŞLADIR</b> ✓✓✓ — installer «--guncelle» rejimində açılır ✓
        /// <para>
        /// ⚠️ Bu metod proqramın <b>BAĞLANMASINI XAHİŞ EDİR</b> ✗ — fayllar kiliddən
        /// azad olmalıdır ✓ (installer sonra proqramı ÖZÜ yenidən açır ✓✓✓)
        /// </para>
        /// </summary>
        /// <returns>Uğurlu olarsa <c>true</c> ✓ (<see cref="CixisLazim"/> işə düşür ✓)</returns>
        public static bool BaslatGuncelleme(string installerYolu)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(installerYolu) || !File.Exists(installerYolu))
                {
                    Cas0201.Firebase.AppLogger.Melumat("🚀 Güncəlləmə başladılmadı ✗ — installer tapılmadı ✗");
                    return false;
                }

                var hədəf = Cas0201.Kok.Qovluq;

                Cas0201.Firebase.AppLogger.Melumat(
                    $"🚀 Güncəlləmə başladılır ✓ — installer: {installerYolu} · hədəf: {hədəf}");

                var psi = new ProcessStartInfo(installerYolu)
                {
                    // 📌 ① «--guncelle» = güncəlləmə rejimi ✓ ② hədəf qovluq ✓
                    Arguments = $"--guncelle \"{hədəf}\"",
                    UseShellExecute = true,
                    Verb = "runas",                      // 🔐 admin ✓ (Program Files ✓)
                    WorkingDirectory = Path.GetDirectoryName(installerYolu) ?? YolQovlug
                };

                Process.Start(psi);

                // ⏳ Kilid açılsın deyə kiçik fasilə ✓ → sonra proqram bağlanır ✗
                Task.Delay(900).ContinueWith(_ =>
                {
                    try { CixisLazim?.Invoke(); } catch { }
                });

                return true;
            }
            catch (Exception ex)
            {
                Cas0201.Firebase.AppLogger.Xeta(ex, "güncəlləmə başlatma");
                return false;
            }
        }
    }
}
