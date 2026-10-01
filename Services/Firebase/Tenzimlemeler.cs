// ============================================================================
//  ⚙️ 021Cars — BULUD TƏNZİMLƏMƏLƏRİ (12)  ★ YALNIZ SECCAD + ASIF ★
// ----------------------------------------------------------------------------
//  ✅ ⏱️ Firebase yazma fasiləsi — İSTİFADƏÇİ ÖZÜ TƏYİN EDİR ✓ (saniyə ✓)
//  ✅ 💾 USB yedək fasiləsi — İSTİFADƏÇİ ÖZÜ TƏYİN EDİR ✓ (dəqiqə ✓)
//  ✅ 🔐 <b>İCAZƏ:</b> yalnız <c>Seccad (BAŞ ADMIN)</c> və <c>Asif (BAŞ İNZİBATÇI)</c> ✓
//     — <c>Sahil</c> görməz və dəyişə bilməz ✗ (AuthService.TenzimlemelerGoruner ✓)
//  ✅ 💾 Yerli JSON faylında ✓ + 🔥 Firebase <c>_tenzimleme</c> nodunda saxlanılır ✓
//  ✅ Bir kompüter dəyişəndə → digər kompüterlər də AVTOMATİK götürür ✓✓✓
// ============================================================================

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseAeroStudio.Services;

namespace Cas0201.Firebase
{
    /// <summary>
    /// ⚙️ <b>BULUD TƏNZİMLƏMƏLƏRİ</b> ✓✓✓ — Seccad &amp; Asif dəyişə bilər ✓
    /// </summary>
    public sealed class BuludAyarlari
    {
        /// <summary>⏱️ Buluda yazma fasiləsi (saniyə ✓ · 2–3600 ✓ · default <b>5</b> ✓)</summary>
        public int FirebaseSaniye { get; set; } = 10;

        /// <summary>💾 USB yedəyi fasiləsi — <b>SANİYƏ</b> ✓ (5–86400 ✓ · default <b>30</b> ✓)</summary>
        public int UsbSaniye { get; set; } = 300;

        /// <summary>🖥️ Kompüterə (yerli) yedək fasiləsi — <b>SANİYƏ</b> ✓ (5–86400 ✓ · default <b>60</b> ✓)</summary>
        public int YerliSaniye { get; set; } = 300;

        /// <summary>🔄 Avtomatik sinxron işləsin? ✓</summary>
        public bool Avtomatik { get; set; } = true;

        /// <summary>🖥️ Son dəyişən cihaz ✓ (audit ✓)</summary>
        public string SonDeyisen { get; set; } = "";

        /// <summary>🕒 Son dəyişiklik vaxtı ✓ (UTC ISO-8601 ✓)</summary>
        public string Yenilenme { get; set; } = "";

        /// <summary>
        /// 💾 Ayar faylı — ★ <b>QURAŞDIRMA QOVLUĞU</b> ★ ✓✓✓
        /// <para>(installer-də seçilən yer ✓ — default <c>C:\Program Files\021Cars</c> ✓ · AppData DEYİL ✗)</para>
        /// </summary>
        [JsonIgnore]
        public static string Fayl { get; } = Path.Combine(
            AppContext.BaseDirectory, "bulud_ayarlari.json");

        /// <summary>🔥 Firebase-dəki nod ✓</summary>
        [JsonIgnore] public const string NodeAdi = "_tenzimleme";

        /// <summary>⚙️ Cari ayarlar ✓ (tətbiq boyu BİR ✓)</summary>
        [JsonIgnore] public static BuludAyarlari Cari { get; private set; } = new();

        /// <summary>
        /// 🆕 <b>TƏMİZ QURAŞDIRMA?</b> ✓✓✓ — yerli <c>bulud_ayarlari.json</c> faylı
        /// <b>YOX</b> ikən <c>true</c> olur ✓
        /// <para>
        /// ⚠ Nə üçün vacibdir? Təmiz quraşdırmada tətbiq <b>HEÇ NƏ</b> çəkməməlidir ✗ —
        /// nə verilənlər ✓, nə də buluddaki ayarlar ✓ (əks halda buluddaki
        /// «Avtomatik: true» yerli «söndürülmüş» vəziyyəti geri yandırırdı ✗✓✓✓)
        /// </para>
        /// <para>
        /// 🔓 İstifadəçi ayarı ÖZÜ saxlayan kimi (<c>💾 YADDA SAXLA</c> ✓) bu qapı açılır ✓
        /// </para>
        /// </summary>
        [JsonIgnore] public static bool TemizQurasdirma { get; private set; }

        /// <summary>📣 Ayar dəyişdi ✓ (UI + körpü yenilənir ✓)</summary>
        public static event Action? Deyisdi;

        /// <summary>
        /// 🔐 <b>İCAZƏ — yalnız SECCAD və ASIF</b> ✓✓✓
        /// <para>
        /// <c>AuthService.Cari.TenzimlemelerGoruner</c> → Admin ✓ və Asif ✓ üçün
        /// <c>true</c>, <b>Sahil üçün <c>false</c></b> ✗ — bu xassə <b>həmin</b> qaydaya bağlıdır ✓
        /// </para>
        /// </summary>
        public static bool IcazeVar => AuthService.Cari?.TenzimlemelerGoruner == true;

        /// <summary>📝 Dəyişiklik icazəsi (UI düymələri üçün ✓)</summary>
        public static string IcazeMetni => IcazeVar
            ? $"✅ Dəyişiklik icazəniz var ✓ ({AuthService.Cari?.RolMetni})"
            : "⛔ Bu ayarları yalnız 👑 BAŞ ADMIN və 🛡️ BAŞ İNZİBATÇI dəyişə bilər ✗";

        /// <summary>📖 Yerli fayldan yükləyir ✓ (yoxdursa standard ✓)</summary>
        public static void Yukle()
        {
            // 🆕 ★ TƏMİZ QURAŞDIRMA AŞKARLANMASI ★ ✓✓✓
            //   ⚠️ Fayl YOXDURSA → bu, YENİ quraşdırmadır ✓ →
            //      bulud sinxronizasiyası STANDART OLARAQ SÖNÜLÜ başlayır ✗✓✓
            //   (əks halda tətbiq ilk saniyədən buluddaki KÖHNƏ maşınları/kreditləri
            //    yerli bazaya «çəkirdi» ✗ → müştəri paketində başqasının datası
            //    görünürdü ✗✓✓✓ — istifadəçi tələbi: «içində heç bir maşın datası olmadan» ✓)
            var faylVar = File.Exists(Fayl);

            try
            {
                if (faylVar)
                {
                    Cari = JsonSerializer.Deserialize<BuludAyarlari>(File.ReadAllText(Fayl))
                           ?? new BuludAyarlari();
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeberdarliq($"⚠️ Ayar faylı oxunmadı ✗ → standart ✓ — {ex.Message}");
                Cari = new BuludAyarlari();
            }

            if (!faylVar)
            {
                TemizQurasdirma = true;      // 🆕 ★ təmiz quraşdırma qapısı ★ ✓✓✓
                Cari.Avtomatik = false;
                Cari.SonDeyisen = Environment.MachineName;
                Cari.Yenilenme = FirebaseOptions.UtcIndi();

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Fayl) ?? ".");
                    File.WriteAllText(Fayl, JsonSerializer.Serialize(Cari,
                        new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { }

                AppLogger.Melumat(
                    "🆕 Təmiz quraşdırma ✓ — bulud sinxronizasiyası SÖNÜLÜ başladı ✗ " +
                    "(tətbiq BOŞ açılır ✓ · yandırmaq üçün: ⚙️ Tənzimləmələr → ☁️ BULUD SİNXRON ✓)");
            }

            Normalize();
            Deyisdi?.Invoke();
        }

        /// <summary>
        /// ⬇️ <b>İLK YÜKLƏMƏYƏ İCAZƏ VER</b> ✓✓✓  (v6.2.12 — YENİ KOMPÜTER)
        /// <para>
        /// Yeni kompüterdə tətbiq BOŞ açılır ✓ — istifadəçi «☁️ Buluddan götür» seçəndə
        /// bu metod çağırılır və:
        /// </para>
        /// <list type="number">
        ///   <item>🆕 təmiz quraşdırma qapısı <b>AÇILIR</b> ✓ (ayarlar buluddan da oxuna bilər ✓)</item>
        ///   <item>☁️ bulud sinxronizasiyası <b>AKTİV</b> edilir ✓</item>
        ///   <item>💾 vəziyyət YERLİ fayla yazılır ✓ (növbəti açılışda da aktiv qalsın ✓)</item>
        /// </list>
        /// <para>
        /// ⚠ <b>BULUDA YAZILMIR</b> ✗ — bu, qəsdən belədir ✓: bir kompüterin «ilk yükləmə»
        /// seçimi digər kompüterlərin ayarını DƏYİŞMƏMƏLİDİR ✗✓✓
        /// </para>
        /// <para>
        /// 🔓 İCAZƏ TƏLƏB OLUNMUR ✗ — bu, ayar dəyişikliyi DEYİL ✓, ilk quraşdırma
        /// bərpasıdır ✓ (hansı istifadəçi daxil olubsa da işləyir ✓)
        /// </para>
        /// </summary>
        public static void IlkYuklemeyeIzinVer()
        {
            TemizQurasdirma = false;
            Cari.Avtomatik = true;
            Cari.SonDeyisen = Environment.MachineName;
            Cari.Yenilenme = FirebaseOptions.UtcIndi();

            Normalize();
            YerliFaylaYaz();

            AppLogger.Melumat(
                "⬇️ İLK YÜKLƏMƏ rejimi ✓ — bulud sinxronizasiyası AKTİV edildi ✓ " +
                "(məlumat buluddan götürülür ✓)");

            Deyisdi?.Invoke();
        }

        /// <summary>💾 Cari ayarları YALNIZ yerli fayla yazır ✓ (buluda TOXUNMUR ✗)</summary>
        private static void YerliFaylaYaz()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Fayl) ?? ".");
                File.WriteAllText(Fayl, JsonSerializer.Serialize(Cari,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "ayar yerli fayl");
            }
        }

        /// <summary>
        /// 💾 <b>YADDA SAXLA</b> ✓✓✓ — həm YERLİ fayla ✓ həm BULUDA ✓
        /// <para>🔐 Yalnız <see cref="IcazeVar"/> (Seccad ✓ · Asif ✓) icazəlidir ✗</para>
        /// </summary>
        public static async Task<string> YaddaSaxlaAsync(
            FirebaseRestClient? klient = null,
            int? firebaseSaniye = null,
            int? usbSaniye = null,
            int? yerliSaniye = null,
            bool? avtomatik = null,
            CancellationToken ct = default)
        {
            if (!IcazeVar)
            {
                return "⛔ İcazə yoxdur ✗ — bu ayarları yalnız 👑 BAŞ ADMIN (Seccad) və " +
                       "🛡️ BAŞ İNZİBATÇI (Asif) dəyişə bilər ✗";
            }

            try
            {
                if (firebaseSaniye.HasValue) Cari.FirebaseSaniye = firebaseSaniye.Value;
                if (usbSaniye.HasValue) Cari.UsbSaniye = usbSaniye.Value;
                if (yerliSaniye.HasValue) Cari.YerliSaniye = yerliSaniye.Value;
                if (avtomatik.HasValue) Cari.Avtomatik = avtomatik.Value;

                // 🔓 İstifadəçi (👑 Admin / 🛡️ Asif) ayarı ÖZÜ saxladı ✓ →
                //    təmiz quraşdırma qapısı AÇILIR ✓ (bulud sinxron artıq mümkündür ✓✓✓)
                TemizQurasdirma = false;

                Normalize();

                Cari.SonDeyisen = Environment.MachineName;
                Cari.Yenilenme = FirebaseOptions.UtcIndi();

                // 💾 ① YERLİ FAYL ✓
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Fayl) ?? ".");
                    File.WriteAllText(Fayl, JsonSerializer.Serialize(Cari,
                        new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    AppLogger.Xeberdarliq("⚠️ Ayar faylı yazılmadı ✗ — " + ex.Message);
                }

                // 🔥 ② FIREBASE ✓ (digər kompüterlər də götürsün ✓)
                var bulud = "yalnız yerli ✓";

                if (klient is not null)
                {
                    var uğur = await klient.YenileAsync(NodeAdi, new Dictionary<string, object?>
                    {
                        ["firebaseSaniye"] = Cari.FirebaseSaniye,
                        ["usbSaniye"] = Cari.UsbSaniye,
                        ["yerliSaniye"] = Cari.YerliSaniye,
                        ["avtomatik"] = Cari.Avtomatik,
                        ["sonDeyisen"] = Cari.SonDeyisen,
                        ["yenilenme"] = Cari.Yenilenme
                    }, ct).ConfigureAwait(false);

                    bulud = uğur ? "bulud ✓ + yerli ✓" : "yalnız yerli ✓ (bulud əlçatmaz ✗)";
                }

                AppLogger.Melumat(
                    $"⚙️ Tənzimləmələr dəyişdi ✓ — ⏱️ {Cari.FirebaseSaniye} san · " +
                    $"💾 USB {Cari.UsbSaniye} san · 🖥️ Kompüter {Cari.YerliSaniye} san · 🔥 {bulud}");

                Deyisdi?.Invoke();

                return $"✅ Yadda saxlanıldı ✓ — ⏱️ Firebase: hər {Cari.FirebaseSaniye} saniyə ✓ · " +
                       $"💾 USB: hər {Cari.UsbSaniye} saniyə ✓ · " +
                       $"🖥️ Kompüter: hər {Cari.YerliSaniye} saniyə ✓ · 🔥 {bulud}";
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "ayar yadda saxlama");
                return "⚠️ Yadda saxlama xətası ✗ — " + ex.Message;
            }
        }

        /// <summary>☁️ Buluddaki ayarları götürür ✓ (başqa kompüter dəyişibsə ✓ · LWW ✓)</summary>
        public static async Task YukleBuluddanAsync(
            FirebaseRestClient klient, CancellationToken ct = default)
        {
            // 🆕 ★ TƏMİZ QURAŞDIRMA ★ ✓✓✓ — yerli ayar faylı YOX idi ✓ →
            //   buluddan AYAR ÇƏKİLMİR ✗✓✓ (əks halda buluddaki «Avtomatik: true»
            //   yerli «söndürülmüş» vəziyyəti DƏRHAL geri yandırırdı ✗ →
            //   yeni quraşdırmada KÖHNƏ maşınlar yerli bazaya çəkilirdi ✗✗✗)
            //   ✔ İstifadəçi ayarı ÖZÜ saxlayan kimi («💾 YADDA SAXLA» ✓) bu qapı açılır ✓
            if (TemizQurasdirma)
            {
                return;
            }

            try
            {
                var node = await klient.OxuAsync(NodeAdi, ct).ConfigureAwait(false);

                if (node is null || node.Count == 0) return;

                var uzaqVaxt = node["yenilenme"]?.ToString() ?? "";

                // ⚖️ LWW: bulud daha təzədirsə → götür ✓
                if (string.CompareOrdinal(uzaqVaxt, Cari.Yenilenme) <= 0) return;

                Cari.FirebaseSaniye = node["firebaseSaniye"]?.GetValue<int>() ?? Cari.FirebaseSaniye;
                Cari.UsbSaniye = node["usbSaniye"]?.GetValue<int>() ?? Cari.UsbSaniye;
                Cari.YerliSaniye = node["yerliSaniye"]?.GetValue<int>() ?? Cari.YerliSaniye;
                Cari.Avtomatik = node["avtomatik"]?.GetValue<bool>() ?? Cari.Avtomatik;
                Cari.SonDeyisen = node["sonDeyisen"]?.ToString() ?? "";
                Cari.Yenilenme = uzaqVaxt;

                Normalize();

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Fayl) ?? ".");
                    File.WriteAllText(Fayl, JsonSerializer.Serialize(Cari,
                        new JsonSerializerOptions { WriteIndented = true }));
                }
                catch { }

                AppLogger.Melumat(
                    $"⚙️ Ayarlar BULUDDAN götürüldü ✓ — ⏱️ {Cari.FirebaseSaniye} san · " +
                    $"💾 USB {Cari.UsbSaniye} san · 🖥️ Kompüter {Cari.YerliSaniye} san ✓ ({Cari.SonDeyisen})");

                Deyisdi?.Invoke();
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "ayar buluddan");
            }
        }

        /// <summary>⚙️ <b>AYARLARI KÖRPÜYƏ TƏTBİQ EDİR</b> ✓✓✓ — canlı ✓ (dərhal qüvvəyə minir ✓)</summary>
        public static void TətbiqEt(BuludKopru? kopru)
        {
            try
            {
                if (kopru is null) return;

                // 🩹 ① ƏVVƏLCƏ «Cari» ÖZÜ DÜZƏLDİLİR ✓✓✓ — ★ VACİB ★
                //    ⚠️ Buluddan/fayldan 5/5/5 gəlsə belə ✗ → yaddaşda artıq
                //    TƏHLÜKƏSİZ dəyərlər olur ✓ (əvvəl yalnız körpüyə tətbiq
                //    olunurdu ✗ → panel/loq «5 san» göstərirdi ✗✓✓)
                var evvelki = (Cari.FirebaseSaniye, Cari.UsbSaniye, Cari.YerliSaniye);
                Normalize();

                // 🩹 ② Düzəliş varsa → YERLİ FAYLA da yazılır ✓ («5 san» HEÇ VAXT qalmır ✗✓✓)
                if (evvelki != (Cari.FirebaseSaniye, Cari.UsbSaniye, Cari.YerliSaniye))
                {
                    try
                    {
                        File.WriteAllText(Fayl, JsonSerializer.Serialize(Cari,
                            new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch { }
                }

                // 🛡️ TƏHLÜKƏLİ SƏRHƏDLƏR ✓✓✓ — buluddan səhv dəyər gəlsə belə
                //    disk BOĞULMUR ✗ (2169 qeyd hər saniyə yenidən yazılmır ✗✓✓)
                kopru.FasileSaniye = Math.Clamp(Cari.FirebaseSaniye, 10, 3600);   // ⏱️ ✓
                kopru.YedekSaniye = Math.Clamp(Cari.UsbSaniye, 120, 86400);       // 💾 ✓
                kopru.YerliYedekSaniye = Math.Clamp(Cari.YerliSaniye, 120, 86400); // 🖥️ ✓

                // ☁️ ③ AKTİV/SÖNÜLÜ ✓✓✓ — ★ KÖRPÜ AÇARI ★
                //    false → körpü HEÇ İŞLƏMİR ✗ (buluddan heç nə OXUNMUR ✗✓✓)
                //    ✔ Təmiz quraşdırmada standart olaraq SÖNÜLÜDÜR ✓
                kopru.Aktiv = Cari.Avtomatik;
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "ayar tətbiqi");
            }
        }

        /// <summary>🔢 Dəyərləri təhlükəsiz sərhədə salır ✓</summary>
        private static void Normalize()
        {
            // ⚠️ AŞAĞI HƏDDLƏR ARTIRILDI ✓✓✓ — ★ DONMANIN QARŞISI ★
            //   (2169 qeyd hər 5-10 saniyədə yenidən yazılırdı ✗ → disk 100% ✗ → donma ✗✓✓)
            Cari.FirebaseSaniye = Math.Clamp(Cari.FirebaseSaniye, 10, 3600);
            Cari.UsbSaniye = Math.Clamp(Cari.UsbSaniye, 120, 86400);
            Cari.YerliSaniye = Math.Clamp(Cari.YerliSaniye, 120, 86400);
        }

        /// <summary>📋 Status mətni ✓ (panel başlığı üçün ✓)</summary>
        public static string StatusMetni =>
            $"⏱️ Firebase: hər {Cari.FirebaseSaniye} saniyə ✓ · " +
            $"💾 USB: hər {Cari.UsbSaniye} saniyə ✓ · " +
            $"🖥️ Kompüter: hər {Cari.YerliSaniye} saniyə ✓ · ✍️ son: {Cari.SonDeyisen} ✓";


    }
}
