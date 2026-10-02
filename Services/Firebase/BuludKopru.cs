// ============================================================================
//  🌉 021Cars — BULUD KÖRPÜSÜ (10)  ★ TƏLƏB: «HƏR ŞEY 5 SANİYƏDƏN BİR BULUDA» ★
// ----------------------------------------------------------------------------
//  ✅ Tətbiqin SQLite-daki BÜTÜN məlumatını oxuyur ✓
//  ✅ Dəyişəni Firebase-ə PUT edir ✓ — HƏR 5 SANİYƏDƏN BİR ✓✓✓
//  ✅ Yalnız DƏYİŞƏNLƏR göndərilir ✓ (JSON hash müqayisəsi ✓ — trafik yox ✗)
//  ✅ SQLite-dan silinən sətir → Firebase-də SOFT-DELETE ✓ (isDeleted=true ✓)
//  ✅ 📎 PDF/ŞƏKİL/MEDIA GÖNDƏRİLMİR ✗✓✓ — onlar YALNIZ fləşkartda qalır ✓
//  ✅ Xəta → app_errors.log ✓ — körpü heç vaxt proqramı çökdürmür ✗
// ============================================================================

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using EnterpriseAeroStudio.Data;
using EnterpriseAeroStudio.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cas0201.Firebase
{
    /// <summary>
    /// 🌉 <b>SQLite → FIREBASE KÖRPÜSÜ</b> ✓✓✓
    /// <para>
    /// Hər <see cref="FasileSaniye"/> saniyədən bir (default <b>5</b> ✓) bütün cədvəlləri oxuyur ✓,
    /// dəyişənləri buluda PUT edir ✓, silinənləri soft-delete edir ✓.
    /// </para>
    /// </summary>
    public sealed class BuludKopru : IDisposable
    {
        private readonly IServiceProvider _srv;
        private readonly FirebaseRestClient _klient;
        private readonly FirebaseOptions _o;
        private CancellationTokenSource _cts = new();

        /// <summary>🧵 Dövrün tək işləməsini təmin edən kilid ✓✓✓</summary>
        private readonly object _dovruKilidi = new();

        /// <summary>
        /// 💾 <b>YEDƏK YAZMA QAPISI</b> ✓✓✓ — ★ VACİB ★
        /// <para>
        /// ⚠️ Eyni anda iki yazma cəhdi olsa ✗ → <c>The process cannot access the file … because
        /// it is being used by another process</c> ✗ (loqda görülən REAL xəta ✓) →
        /// ona görə bütün yedəklər <b>NÖVBƏ İLƏ</b> yazılır ✓
        /// </para>
        /// </summary>
        private static readonly SemaphoreSlim YedekQapisi = new(1, 1);

        /// <summary>🧠 Son göndərilən halların hash-i ✓ («cars/12» → hash ✓)</summary>
        private readonly Dictionary<string, string> _sonHash = new();

        /// <summary>
        /// 🧠 <b>SİNXRON YADDAŞI YÜKLƏNDİ?</b> ✓✓✓  (v6.2.14)
        /// <para>
        /// İstifadəçi tələbi: «eyni məlumatlardırsa save-larda HEÇ NƏ
        /// yazılıb silinməməlidir» ✓ — əvvəl hash yalnız <b>yaddaşda</b> idi ✗
        /// → proqram hər açılışda <b>bütün 2567 qeydi TƏKRAR buluda yazırdı</b> ✗✓✓
        /// </para>
        /// <para>✅ İNDİ: hash-lər «bulud_izleme» cədvəlindən yüklənir ✓ → dəyişməyən qeyd GÖNDƏRİLMİR ✗✓✓</para>
        /// </summary>
        private bool _izlemeYuklendi;

        /// <summary>🧠 Buludda mövcud olan açarlar ✓ (silmə aşkarlaması üçün ✓)</summary>
        private readonly Dictionary<string, HashSet<string>> _sonAçarlar = new();

        /// <summary>⏱️ Sinxron fasiləsi (saniyə ✓) — <b>5</b> ✓ (istifadəçi standartı ✓)</summary>
        public int FasileSaniye { get; set; } = 5;

        /// <summary>
        /// ☁️ <b>BULUD SİNXRONİZASİYASI AKTİVDİRMİ?</b> ✓✓✓
        /// <para>
        /// <c>false</c> → körpü <b>HEÇ İŞLƏMİR</b> ✗ — buluddan heç nə OXUNMUR və
        /// heç nə YAZILMIR ✗✓✓ (təmiz quraşdırma ✓: tətbiq <b>BOŞ</b> açılır ✓)
        /// </para>
        /// <para>⚙️ Dəyər <see cref="BuludAyarlari.Cari"/>.<c>Avtomatik</c>-dən gəlir ✓</para>
        /// </summary>
        public bool Aktiv { get; set; } = true;

        /// <summary>
        /// 🗑️ <b>FİZİKİ SİLİNƏ?</b> ✓✓✓ — <c>true</c> (default ✓):
        /// SQLite-dan silinən qeyd buluddan <b>TAM SİLİNİR</b> ✓ (yer tutmur ✗ ✓)
        /// <para><c>false</c> → yalnız soft-delete ✓ (isDeleted = true ✓)</para>
        /// </summary>
        public bool FizikiSil { get; set; } = true;

        /// <summary>
        /// 🗑️ <b>Tombstone-ların buluddan silinmə müddəti</b> ✓✓✓ (GÜN ✓)
        /// <para>
        /// ⚠️ <b>60 gündən az OLMAMALIDIR</b> ✗ — əks halda oflayn qalan kompüterlər
        /// silinməni görməyə bilər ✗ ✓✓✓
        /// </para>
        /// </summary>
        public int ZibilSaxlamaGun { get; set; } = 60;

        /// <summary>
        /// 🔢 <b>Son dövrdə BULUDDAN yerli bazaya tətbiq olunan dəyişiklik sayı</b> ✓✓✓
        /// (v6.2.12 — «N qeyd götürüldü ✓» mesajı üçün ✓)
        /// </summary>
        public int SonCekilenSayi { get; private set; }

        /// <summary>🔄 Hazırda sinxron gedir? ✓ (nazik loading bar üçün ✓)</summary>
        public bool SinxronGedir { get; private set; }

        /// <summary>
        /// 🔢 <b>MƏLUMAT VERSİYASI</b> ✓✓✓ — hər REAL dəyişiklikdə artır ✓
        /// <para>
        /// UI bunu müqayisə edir ✓: <b>versiya dəyişməyibsə → HEÇ BİR yeniləmə edilmir</b> ✗✓✓✓
        /// (buna görə formalarda seçilmiş variantlar / yazılan məlumat SİLİNMİR ✗)
        /// </para>
        /// </summary>
        public long Versiya { get; private set; }

        /// <summary>🕒 Cari dövrün başlama vaxtı ✓ (loading bar üçün ✓)</summary>
        public DateTime? DovruBaslama { get; private set; }

        /// <summary>
        /// 💾 <b>FLƏŞKART HAZIRDIR?</b> ✓✓✓ — <b>CANLI YOXLAMA</b> ✗✓✓
        /// <para>
        /// ⚠ Əvvəl keşlənmiş vəziyyətə baxırdı ✗ → fləşkart çıxarılsa da
        /// <c>true</c> qalırdı ✗ → status dairəsi QIRMIZI yerinə NARINCI olurdu ✗✓✓
        /// </para>
        /// <para>✅ İndi hər dəfə diskin HƏQİQƏTƏN mövcudluğu yoxlanılır ✓</para>
        /// </summary>
        public bool UsbHazir
        {
            get
            {
                try
                {
                    if (_usb is null || !_usb.Hazir) return false;

                    // 🔌 CANLI: fləşkartın kökü HƏQİQƏTƏN varmı? ✓
                    return _usb.KokYol is not null && Directory.Exists(_usb.KokYol);
                }
                catch
                {
                    return false;   // 🔴 çıxarılıb ✗
                }
            }
        }

        /// <summary>🔥 Bulud bağlantısı varmı? ✓ (status dairəsi üçün ✓)</summary>
        public bool Onlayn => _klient.Onlayn;

        /// <summary>💾 USB-yə JSON yedək fasiləsi — <b>SANİYƏ</b> ✓ — <b>30</b> ✓</summary>
        public int YedekSaniye { get; set; } = 300;

        /// <summary>🖥️ Kompüterə (yerli) yedək fasiləsi — <b>SANİYƏ</b> ✓ — <b>60</b> ✓</summary>
        public int YerliYedekSaniye { get; set; } = 300;

        /// <summary>🕒 Son USB yedəyi vaxtı ✓</summary>
        public DateTime? SonYedek { get; private set; }

        /// <summary>🕒 Son kompüter yedəyi vaxtı ✓</summary>
        public DateTime? SonYerliYedek { get; private set; }

        /// <summary>
        /// 🔢 <b>YEDƏKDƏN SONRAKI VERSİYA</b> ✓✓✓ —
        /// ⚠️ dəyişiklik olmayıbsa yedək <b>TƏKRAR YAZILMIR</b> ✗✓✓✓
        /// <para>
        /// ★ DONMANIN HƏLLİ ★ — əvvəl 2169 qeyd hər 5-10 saniyədə yenidən
        /// yazılırdı ✗ → disk 100% ✗ → UI donurdu ✗✓✓
        /// </para>
        /// </summary>
        private long _sonYerliYedekVersiya = -1;

        /// <inheritdoc cref="_sonYerliYedekVersiya" />
        private long _sonYedekVersiya = -1;

        /// <summary>🗑️ Bu sessiyada buluddan silinən qeyd sayı ✓</summary>
        public int SilinenSayi { get; private set; }

        /// <summary>✅ Körpü işləyir? ✓</summary>
        public bool Isleyir { get; private set; }

        /// <summary>🕒 Son uğurlu sinxron vaxtı ✓</summary>
        public DateTime? SonUgur { get; private set; }

        /// <summary>📊 Son dövrdə göndərilən qeyd sayı ✓</summary>
        public int SonPushSayi { get; private set; }

        /// <summary>📊 Ümumi göndərilən qeyd sayı ✓</summary>
        public long UmumiPush { get; private set; }

        /// <summary>🗑️ Soft-delete edilənlərin sayı ✓</summary>
        public int SonSilmeSayi { get; private set; }

        /// <summary>❌ Son xəta mətni ✓ (UI üçün ✓)</summary>
        public string? SonXeta { get; private set; }

        /// <summary>📣 Vəziyyət dəyişdi ✓ (UI yeniləsin ✓)</summary>
        public event Action? VeziyyetDeyisdi;

        public BuludKopru(
            IServiceProvider srv,
            FirebaseRestClient klient,
            FirebaseOptions options)
        {
            _srv = srv ?? throw new ArgumentNullException(nameof(srv));
            _klient = klient ?? throw new ArgumentNullException(nameof(klient));
            _o = options ?? throw new ArgumentNullException(nameof(options));
        }

        // --------------------------------------------------------------------
        //  🚀 BAŞLAT / DAYANDIR ✓
        // --------------------------------------------------------------------

        /// <summary>
        /// 🚀 Körpünü arxa fonda işə salır ✓ (UI donmur ✗ ✓)
        /// <para>
        /// ⚠️ <b>DÜZƏLİŞ:</b> əvvəl <c>Dayandir()</c>-dən sonra təkrar <c>Basla()</c> çağırılsa ✗
        /// <see cref="_cts"/> artıq ləğv olunmuşdu ✗ → yeni dövr DƏRHAL susurdu ✗✓✓
        /// (indi hər başlatmada <b>TƏZƏ</b> <see cref="CancellationTokenSource"/> yaradılır ✓)
        /// </para>
        /// <para>⚠️ Eyni anda <b>YALNIZ BİR</b> dövr işləyir ✓ (təkrar başlatma qarşısı alınır ✗✓✓)</para>
        /// </summary>
        /// <summary>
        /// 🧠 <b>SİNXRON YADDAŞINI YÜKLƏYİR</b> ✓✓✓  (v6.2.14)
        /// <para>
        /// «bulud_izleme» cədvəlindən hər qeydin son hash-i oxunur ✓ →
        /// <c>_sonHash</c> (göndərmə yoxlaması ✓) və <c>_sonAçarlar</c>
        /// (silinmə aşkarlaması ✓) doldurulur ✓✓✓
        /// </para>
        /// <para>
        /// ⚠ İstifadəçi tələbi: «eyni məlumatlardırsa save-larda HEÇ NƏ yazılıb
        /// silinməməlidir» ✓. Əvvəl hash yalnız YADDAŞDA idi ✗ → proqram hər
        /// açılışda <b>bütün 2567 qeydi TƏKRAR buluda yazırdı</b> ✗✓✓
        /// ✅ İNDİ: dəyişməyən qeydlər TƏKRAR GÖNDƏRİLMİR ✗✓✓
        /// </para>
        /// </summary>
        private void İzlemeYukle(AppDbContext db)
        {
            if (_izlemeYuklendi)
            {
                return;
            }

            _izlemeYuklendi = true;

            try
            {
                using var cx = new SqliteConnection(db.Database.GetConnectionString());
                cx.Open();

                CedveliYarat(cx);

                using var əmr = cx.CreateCommand();
                əmr.CommandText = "SELECT Kol, ElementId, Hash FROM bulud_izleme;";

                using var oxu = əmr.ExecuteReader();

                while (oxu.Read())
                {
                    var kol = oxu.GetString(0);
                    var id = oxu.GetString(1);
                    var hash = oxu.GetString(2);

                    _sonHash[kol + "/" + id] = hash;

                    if (!_sonAçarlar.TryGetValue(kol, out var set))
                    {
                        set = new HashSet<string>(StringComparer.Ordinal);
                        _sonAçarlar[kol] = set;
                    }

                    set.Add(id);
                }

                AppLogger.Melumat(
                    $"🧠 Sinxron yaddaşı yükləndi ✓ — {_sonHash.Count} qeydin hashi ✓ " +
                    "(dəyişməyənlər TƏKRAR YAZILMIR ✗✓✓)");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "izləmə yükləmə");
            }
        }

        public void Basla()
        {
            // ☁️ SÖNÜLÜDÜRSƏ → HEÇ BAŞLAMIR ✗✓✓ (təmiz quraşdırma ✓ — buluddan heç nə oxunmur ✗)
            if (!Aktiv)
            {
                AppLogger.Melumat(
                    "☁️ Bulud körpüsü SÖNÜLÜDÜR ✗ — avtomatik sinxron başlamadı ✓ " +
                    "(yandırmaq: ⚙️ Tənzimləmələr → ☁️ BULUD SİNXRON ✓)");
                return;
            }

            // ================================================================
            //  ⚡ v6.2.16 — «DƏRHAL GÖNDƏR» ABUNƏLİYİ ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ İstifadəçi tələbi: «bir kompüterdə maşın əlavə edirəm,
            //    o birində dərhal görünsün» ✓✓✓
            //  → hər yazmadan sonra dövr DƏRHAL oyanır ✓ (5 saniyə gözləmir ✗)
            //  🛡️ İki dəfə abunə olmamaq üçün əvvəlcə AYRILIR ✓✓✓
            // ================================================================
            AppDbContext.VerilənlərDəyişdi -= TezGonder;
            AppDbContext.VerilənlərDəyişdi += TezGonder;

            lock (_dovruKilidi)
            {
                if (Isleyir) return;                  // 🔁 artıq işləyir ✓ — ikinci dövr AÇILMIR ✗

                if (_cts.IsCancellationRequested)     // 🛑 dayandırılmışdı ✓ → təzəsi yaradılır ✓
                {
                    try { _cts.Dispose(); } catch { }
                    _cts = new CancellationTokenSource();
                }

                Isleyir = true;
            }

            VeziyyetDeyisdi?.Invoke();

            AppLogger.Melumat(
                $"🌉 BULUD KÖRPÜSÜ BAŞLADI ✓ — hər {Math.Max(5, FasileSaniye)} saniyədən bir ✓ " +
                "(📎 PDF/media XARİCDİR ✗ — onlar fləşkartda qalır ✓)");

            var token = _cts.Token;

            _ = Task.Run(() => DovruAsync(token));
        }

        /// <summary>
        /// ⬇️ <b>İLK YÜKLƏMƏ — YENİ KOMPÜTER</b> ✓✓✓  (v6.2.12)
        /// <para>
        /// 🆕 <b>PROBLEM:</b> yeni kompüterdə təmiz quraşdırma → bulud sinxronizasiyası
        /// qəsdən SÖNÜLÜ açılır ✓ (başqasının datası görünməsin ✗) → istifadəçi məlumatı
        /// görmür ✗ və <b>💾 USB taxmağa MƏCBUR</b> qalırdı ✗✓✓
        /// </para>
        /// <para>
        /// ✅ <b>HƏLL:</b> bu metod BİR ÇAĞIRIŞDA hər şeyi edir:
        /// </para>
        /// <list type="number">
        ///   <item>🔓 təmiz quraşdırma qapısı açılır ✓</item>
        ///   <item>☁️ bulud sinxronizasiyası AKTİV edilir ✓ (yerli fayla yazılır ✓)</item>
        ///   <item>▶️ körpü başladılır ✓ (hər 10 saniyədə avtomatik sinxron ✓)</item>
        ///   <item>⬇️ <b>DƏRHAL</b> bir tam dövr işlədilir → bütün məlumat (maşın ✓ kredit ✓
        ///   satış ✓ xərc ✓ tərəfdaş ✓) buluddan yerli bazaya <b>çəkilir</b> ✓✓✓</item>
        /// </list>
        /// <returns>Yerli bazaya tətbiq olunan dəyişiklik sayı ✓</returns>
        /// </summary>
        public async Task<int> IlkYuklemeAsync(CancellationToken ct = default)
        {
            AppLogger.Melumat("⬇️ İLK YÜKLƏMƏ başladı ✓ — buluddan məlumat götürülür…");

            // ① 🔓 Təmiz quraşdırma qapısı açılır + sinxron AKTİV edilir ✓
            BuludAyarlari.IlkYuklemeyeIzinVer();

            // ② ⚙️ Ayarlar körpüyə dərhal tətbiq olunur ✓ (Aktiv = true ✓)
            BuludAyarlari.TətbiqEt(this);

            // ③ ▶️ Avtomatik dövr başladılır ✓ (artıq açıqdırsa təkrar açılmır ✗)
            Basla();

            // ④ ⬇️ DƏRHAL bir tam sinxron dövrü → məlumat aşağı çəkilir ✓✓✓
            await SinxronDovruAsync(ct).ConfigureAwait(false);

            AppLogger.Melumat(
                $"⬇️ İLK YÜKLƏMƏ bitdi ✓ — {SonCekilenSayi} dəyişiklik yerli bazaya yazıldı ✓" +
                (SonXeta is null ? " ✓" : $" · ⚠ xəta: {SonXeta}"));

            return SonCekilenSayi;
        }

        /// <summary>
        /// 🔥 <b>BULUDU TAM SIFIRLA + YERLİ MƏLUMATI GÖNDƏR</b> ✓✓✓  (v6.2.13)
        /// <para>
        /// İstifadəçi tələbi: «Firebasedaki bütün dataları sil və proqramımızdaki
        /// datanı ötür Firebase-ə» ✓✓✓
        /// </para>
        /// <list type="number">
        ///   <item>🗑️ <paramref name="buluduSil"/> = true → buludun kökü
        ///   (<c>021Cars</c>) <b>TAM SİLİNİR</b> ✓ (bütün köhnə/natamam məlumat ✗)</item>
        ///   <item>🧠 yaddaşdaki hash/açar xəritələri təmizlənir ✓ →
        ///   heç bir qeyd «dəyişməyib» sayılmır ✗✓✓</item>
        ///   <item>🗄️ yerli <c>bulud_izleme</c> cədvəli təmizlənir ✓</item>
        ///   <item>⬆️ <b>YERLİ BAZADAKI BÜTÜN MƏLUMAT</b> buluda göndərilir ✓✓✓</item>
        /// </list>
        /// <para>
        /// ⚠ Nəticə: bulud yerli bazanın <b>TAM surəti</b> olur ✓ —
        /// digər kompüterlər növbəti sinxronda hər şeyi alır ✓✓✓
        /// </para>
        /// </summary>
        public async Task<(int Push, int Silme)> HamisiniGonderAsync(
            bool buluduSil, CancellationToken ct = default)
        {
            try
            {
                // ☁️ Körpü aktiv olmalıdır ✓ (yoxdursa heç nə göndərilmir ✗)
                Aktiv = true;

                // ① 🗑️ BULUDU TAM SIFIRLA ✓
                if (buluduSil)
                {
                    var uğur = await _klient.FizikiSilAsync(string.Empty, ct).ConfigureAwait(false);

                    AppLogger.Melumat(uğur
                        ? "🔥 BULUD TAM SIFIRLANDI ✓ — köhnə məlumat silindi ✓"
                        : "⚠️ Bulud sıfırlanmadı ✗ — əlaqəni yoxlayın ✓");
                }

                // ② 🧠 Hash/açar xəritələri təmizlə ✓ → HAMISI «dəyişib» sayılır ✓
                _sonHash.Clear();
                _sonAçarlar.Clear();

                // ③ 🗄️ Yerli sinxron izlərini təmizlə ✓
                await IzlemeTemizleAsync(ct).ConfigureAwait(false);

                // ④ ⬆️ YERLİ MƏLUMATI GÖNDƏR ✓✓✓
                await BirDovruAsync(ct).ConfigureAwait(false);

                AppLogger.Melumat(
                    $"🔥 HAMISI GÖNDƏRİLDİ ✓ — ☁️ {SonPushSayi} qeyd buluda yazıldı ✓ · 🗑️ {SonSilmeSayi} ✓");

                return (SonPushSayi, SonSilmeSayi);
            }
            catch (Exception ex)
            {
                SonXeta = ex.Message;
                AppLogger.Xeta(ex, "hamisini göndər");
                return (0, 0);
            }
        }

        /// <summary>🗄️ <c>bulud_izleme</c> cədvəlini TAM təmizləyir ✓ (köhnə «sinxronlandı» izləri ✗)</summary>
        private async Task IzlemeTemizleAsync(CancellationToken ct)
        {
            try
            {
                using var qap = _srv.CreateScope();
                using var db = qap.ServiceProvider.GetRequiredService<AppDbContext>();

                using var cx = new SqliteConnection(db.Database.GetConnectionString());
                await cx.OpenAsync(ct).ConfigureAwait(false);

                CedveliYarat(cx);

                using var əmr = cx.CreateCommand();
                əmr.CommandText = "DELETE FROM bulud_izleme;";

                var say = await əmr.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                AppLogger.Melumat($"🧹 Bulud izləmə cədvəli təmizləndi ✓ — {say} köhnə iz silindi ✓");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "izləmə təmizləmə");
            }
        }

        /// <summary>
        /// 🩺 <b>BAĞLANTINI YOXLA</b> ✓✓✓ (v6.2.13) — oxu <b>və</b> yazma testi ✓
        /// <para>⚠ Bu metod <b>HEÇ BİR XƏTANI GİZLƏTMİR</b> ✗ — HTTP kodlarını olduğu kimi qaytarır ✓✓✓</para>
        /// </summary>
        public Task<string> SinaqEtAsync(CancellationToken ct = default) => _klient.SinaqEtAsync(ct);

        /// <summary>🛑 Körpünü dayandırır ✓ (təkrar çağırış TƏHLÜKƏSİZ ✓)</summary>
        public void Dayandir()
        {
            try
            {
                // ⚡ «dərhal göndər» abunəliyi GÖTÜRÜLÜR ✗✓✓ (yaddaş sızmasın ✗)
                AppDbContext.VerilənlərDəyişdi -= TezGonder;

                lock (_dovruKilidi)
                {
                    if (!Isleyir && _cts.IsCancellationRequested) return;   // 🔁 artıq dayanıb ✓

                    if (!_cts.IsCancellationRequested) _cts.Cancel();
                    Isleyir = false;
                }

                VeziyyetDeyisdi?.Invoke();
                AppLogger.Melumat("🛑 Bulud körpüsü dayandırıldı ✓");
            }
            catch (Exception ex) { AppLogger.Xeta(ex, "körpü dayandırma"); }
        }

        /// <summary>🔁 Əsas dövr — 5 saniyə ✓✓✓</summary>
        private async Task DovruAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // 🔄 İKİ TƏRƏFLİ: əvvəl buluddan çək ✓ → sonra buluda göndər ✓
                    await SinxronDovruAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    // 🛡️ Heç bir halda proqram çökmür ✗✓✓
                    SonXeta = ex.Message;
                    AppLogger.Xeta(ex, "körpü dövrü");
                }

                try
                {
                    // ================================================================
                    //  ⚡ v6.2.16 — DƏRHAL GÖNDƏR ✓✓✓
                    //  ----------------------------------------------------------------
                    //  ⚠ Yerli dəyişiklik oldusa (məs. maşın əlavə edildi ✓) →
                    //    fasilə GÖZLƏNİLMİR ✗ → 400 ms sonra dövr keçir ✓✓✓
                    //    (istifadəçi: «dərhal o birində görünsün» ✓)
                    // ================================================================
                    var fasilə = _tezGonder
                        ? TimeSpan.FromMilliseconds(400)
                        : TimeSpan.FromSeconds(Math.Max(2, FasileSaniye));

                    _tezGonder = false;

                    await Task.Delay(fasilə, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        /// <summary>
        /// ⚡ <b>DƏRHAL GÖNDƏR SİQNALI</b> ✓✓✓ (v6.2.16) — <see cref="AppDbContext.VerilənlərDəyişdi"/>
        /// <para>
        /// Yazmadan sonra çağırılır ✓ → dövrün fasiləsi 400 ms-ə düşür ✓✓✓
        /// 🛡️ Yalnız bayraq qaldırılır ✗ — ayrı thread AÇILMIR ✗ (təhlükəsizdir ✓)
        /// </para>
        /// </summary>
        private void TezGonder()
        {
            _tezGonder = true;
        }

        /// <summary>⚡ «Dərhal göndər» bayrağı ✓ (yazma zamanı qalxır ✓)</summary>
        private volatile bool _tezGonder;

        /// <summary>
        /// 🔄 <b>İKİ TƏRƏFLİ SİNXRON DÖVRÜ</b> ✓✓✓
        /// <list type="number">
        ///   <item>① ☁️ <b>BULUDDAN ÇƏK</b> — LWW: yalnız <b>daha təzə</b> olanlar yerli bazaya tətbiq olunur ✓</item>
        ///   <item>② 🗑️ <b>TOMBSTONE</b> — başqa kompüter silibsə → yerli bazadan da SİLİNİR ✓✓✓ (BMW ssenarisi ✓)</item>
        ///   <item>③ ⬆️ <b>BULUDA GÖNDƏR</b> — yerli daha təzədirsə ✓ + yerli silinmələr → tombstone ✓</item>
        /// </list>
        /// </summary>
        public async Task SinxronDovruAsync(CancellationToken ct = default)
        {
            SinxronGedir = true;          // 📊 nazik loading bar üçün ✓
            DovruBaslama = DateTime.Now;  // 🕒 dövr başladı ✓

            try
            {
                using var qap = _srv.CreateScope();
                using var db = qap.ServiceProvider.GetRequiredService<AppDbContext>();

                using var cx = new SqliteConnection(db.Database.GetConnectionString());
                cx.Open();
                CedveliYarat(cx);

                // 🧠 Sinxron yaddaşını yüklə ✓ → dəyişməyən qeydlər TƏKRAR yazılmır ✓✓✓ (v6.2.14)
                İzlemeYukle(db);

                // ⚙️ TƏNZİMLƏMƏLƏRİ CANLI TƏTBİQ ET ✓ (Seccad/Asif dəyişəndə dərhal qüvvəyə minir ✓)
                BuludAyarlari.TətbiqEt(this);

                // ☁️ ① BULUDDAN OXU ✓ (hər kolleksiya BİR dəfə ✓)
                var bulud = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);

                foreach (var k in Kolleksiyalar)
                {
                    bulud[k] = await _klient.OxuAsync(k, ct).ConfigureAwait(false);
                }

                var zibil = await _klient.OxuAsync("_zibil", ct).ConfigureAwait(false);

                // 🔄 ② HƏR CƏDVƏL: ÇƏK + TƏTBİQ (LWW ✓)
                var çekilen = 0;

                var işlər = new Func<Task<int>>[]
                {
                    async () => await CədvəliSinxronlaAsync(db, cx, "cars",
                        await db.Cars.AsNoTracking().ToListAsync(ct),
                        a => Acar(a),
                        CarNode, bulud["cars"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "expenses",
                        await db.Expenses.AsNoTracking().ToListAsync(ct),
                        e => Acar(e),
                        XercNode, bulud["expenses"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "sales",
                        await db.Sales.AsNoTracking().ToListAsync(ct),
                        s => Acar(s),
                        SatisNode, bulud["sales"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "credits",
                        await db.Credits.AsNoTracking().ToListAsync(ct),
                        k => Acar(k),
                        KreditNode, bulud["credits"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "creditTransactions",
                        await db.CreditTransactions.AsNoTracking().ToListAsync(ct),
                        t => Acar(t),
                        EmeliyyatNode, bulud["creditTransactions"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "partners",
                        await db.Partners.AsNoTracking().ToListAsync(ct),
                        p => Acar(p),
                        TerefdasNode, bulud["partners"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "partnerShares",
                        await db.PartnerShares.AsNoTracking().ToListAsync(ct),
                        p => Acar(p),
                        PayNode, bulud["partnerShares"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "partnerPayments",
                        await db.PartnerPayments.AsNoTracking().ToListAsync(ct),
                        p => Acar(p),
                        OdenisNode, bulud["partnerPayments"], zibil, ct),

                    // ✅ v6.2.12 — ⏳ möhlətlər + 💵 kassa hərəkətləri də çəkilir ✓✓✓
                    async () => await CədvəliSinxronlaAsync(db, cx, "odenisMohlets",
                        await db.OdenisMohletler.AsNoTracking().ToListAsync(ct),
                        m => Acar(m),
                        MohletNode, bulud["odenisMohlets"], zibil, ct),

                    async () => await CədvəliSinxronlaAsync(db, cx, "kassaHereketleri",
                        await db.KassaHereketleri.AsNoTracking().ToListAsync(ct),
                        k => Acar(k),
                        KassaNode, bulud["kassaHereketleri"], zibil, ct)
                };

                foreach (var iş in işlər)
                {
                    çekilen += await iş().ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                SonCekilenSayi = çekilen;   // 📊 UI: «N qeyd götürüldü ✓» (v6.2.12)

                // ⬆️ ③ YERLİ → BULUD (dəyişənlər ✓ + silinənlər → tombstone ✓)
                await BirDovruAsync(ct).ConfigureAwait(false);

                if (çekilen > 0)
                {
                    AppLogger.Melumat($"⬇️ BULUDDAN: {çekilen} dəyişiklik yerli bazaya tətbiq olundu ✓");
                }

                // 🔢 REAL dəyişiklik oldusa → VERSİYANI ARTIR ✓✓✓
                //    (yalnız bundan sonra UI yeniləmə edir ✓ — boş dövrlərdə UI-a TOXUNULMUR ✗)
                if (çekilen > 0 || SonPushSayi > 0 || SonSilmeSayi > 0)
                {
                    Versiya++;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                SonXeta = ex.Message;
                AppLogger.Xeta(ex, "iki tərəfli sinxron");
            }
            finally
            {
                SinxronGedir = false; // 📊 loading bar gizlədilsin ✓
                VeziyyetDeyisdi?.Invoke();
            }
        }

        /// <summary>🔁 Bir dövrdə BÜTÜN cədvəlləri buluda göndərir ✓✓✓</summary>
        public async Task BirDovruAsync(CancellationToken ct = default)
        {
            var push = 0;
            var silme = 0;
            void Say(int x) => silme += x;

            try
            {
                using var qap = _srv.CreateScope();
                using var db = qap.ServiceProvider.GetRequiredService<AppDbContext>();

                // 🧠 Sinxron yaddaşını yüklə ✓ (ilk dövrdə ✓) — dəyişməyənlər göndərilmir ✓✓✓ (v6.2.14)
                İzlemeYukle(db);

                // 🗄️ İzləmə cədvəli üçün BİRBAŞA SQLite bağlantısı ✓ (izləri orada saxlayırıq ✓)
                using var cx = new SqliteConnection(db.Database.GetConnectionString());
                await cx.OpenAsync(ct).ConfigureAwait(false);
                CedveliYarat(cx);

                // ================================================================
                //  🔗 ƏLAQƏ XƏRİTƏLƏRİ ✓✓✓ (v6.2.16) — göndərilən node-lara
                //  valideynin GUID-i («carBuludId» ✓) yazılsın deyə ✓✓✓
                // ================================================================
                await XəritələriDoldurAsync(db, ct).ConfigureAwait(false);

                push += await CədvəlGonderAsync("cars",
                    await db.Cars.AsNoTracking().ToListAsync(ct), a => Acar(a), CarNode, ct, Say, cx);

                push += await CədvəlGonderAsync("expenses",
                    await db.Expenses.AsNoTracking().ToListAsync(ct), e => Acar(e), XercNode, ct, Say, cx);

                push += await CədvəlGonderAsync("sales",
                    await db.Sales.AsNoTracking().ToListAsync(ct), s => Acar(s), SatisNode, ct, Say, cx);

                push += await CədvəlGonderAsync("credits",
                    await db.Credits.AsNoTracking().ToListAsync(ct), k => Acar(k), KreditNode, ct, Say, cx);

                push += await CədvəlGonderAsync("creditTransactions",
                    await db.CreditTransactions.AsNoTracking().ToListAsync(ct),
                    t => Acar(t), EmeliyyatNode, ct, Say, cx);

                push += await CədvəlGonderAsync("partners",
                    await db.Partners.AsNoTracking().ToListAsync(ct), p => Acar(p), TerefdasNode, ct, Say, cx);

                push += await CədvəlGonderAsync("partnerShares",
                    await db.PartnerShares.AsNoTracking().ToListAsync(ct), p => Acar(p), PayNode, ct, Say, cx);

                push += await CədvəlGonderAsync("partnerPayments",
                    await db.PartnerPayments.AsNoTracking().ToListAsync(ct), p => Acar(p), OdenisNode, ct, Say, cx);

                // ================================================================
                //  ✅ v6.2.12 — ⏳ MÖHLƏTLƏR və 💵 KASSA HƏRƏKƏTLƏRİ də buluda gedir ✓✓✓
                //  (əvvəl YOX İDİ ✗ → yeni kompüterdə bu məlumat İTİRDİ ✗)
                // ================================================================
                push += await CədvəlGonderAsync("odenisMohlets",
                    await db.OdenisMohletler.AsNoTracking().ToListAsync(ct),
                    m => Acar(m), MohletNode, ct, Say, cx);

                push += await CədvəlGonderAsync("kassaHereketleri",
                    await db.KassaHereketleri.AsNoTracking().ToListAsync(ct),
                    k => Acar(k), KassaNode, ct, Say, cx);

                // 📎 ❌ MEDIA / PDF / ŞƏKİL GÖNDƏRİLMİR ✗✓✓ — YALNIZ fləşkartda qalır ✓
                // 🏷️ Xərc kataloqu buluda getmir ✗ (lokal arayış cədvəlidir ✓)

                await MetaYazAsync(push, silme, ct).ConfigureAwait(false);

                // 🧹 ① Köhnə SOFT-DELETE qalıqlarını buluddan SİL ✓ (yalnız BİR dəfə ✓)
                if (!_zibilTemizlendi)
                {
                    await ZibiliTemizleAsync(ct).ConfigureAwait(false);
                    _zibilTemizlendi = true;
                }

                // 💾 ② USB-yə JSON yedəyi + SON_SINXRON.txt ✓ (SANİYƏ ✓)
                await UsbYedekAsync(ct).ConfigureAwait(false);

                // 🖥️ ③ Kompüterə JSON yedəyi + SON_SINXRON.txt ✓ (SANİYƏ ✓)
                await YerliYedekAsync(ct).ConfigureAwait(false);

                SonPushSayi = push;
                SonSilmeSayi = silme;
                UmumiPush += push;
                SonUgur = DateTime.Now;
                SonXeta = null;

                if (push > 0 || silme > 0)
                {
                    AppLogger.Melumat($"☁️ BULUD: {push} qeyd göndərildi ✓ · 🗑️ {silme} soft-silindi ✓");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                SonXeta = ex.Message;
                AppLogger.Xeta(ex, "körpü dövrü");
            }
            finally
            {
                VeziyyetDeyisdi?.Invoke();
            }
        }

        /// <summary>
        /// 📤 Bir cədvəli göndərir ✓ — <b>YALNIZ DƏYİŞƏNLƏR</b> ✓✓✓
        /// <para>
        /// ① Məzmun hash-ı dəyişməyibsə → <b>göndərilmir</b> ✗ (trafik qənaəti ✓)<br/>
        /// ② Dəyişibsə → PUT + `updatedAt` yenilənir ✓<br/>
        /// ③ SQLite-dan silinənlər → buludda <b>SOFT-DELETE</b> ✓ (isDeleted = true ✓)
        /// </para>
        /// </summary>
        private async Task<int> CədvəlGonderAsync<T>(
            string kolleksiya,
            IReadOnlyList<T> sətirlər,
            Func<T, string> açarAl,
            Func<T, Dictionary<string, object?>> nodeAl,
            CancellationToken ct,
            Action<int> silmeSayğacı,
            SqliteConnection cx)
        {
            var göndərilən = 0;
            var indiki = new HashSet<string>(StringComparer.Ordinal);

            foreach (var sətr in sətirlər)
            {
                ct.ThrowIfCancellationRequested();

                var açar = açarAl(sətr);
                indiki.Add(açar);

                Dictionary<string, object?> node;

                try { node = nodeAl(sətr); }
                catch (Exception ex) { AppLogger.Xeta(ex, "xəritə: " + kolleksiya); continue; }

                var tamYol = kolleksiya + "/" + açar;

                // 🔑 HASH — audit sahələri DAXİL DEYİL ✗✓✓ (yalnız məzmun ✓)
                //    ⚠ v6.2.16: KANONİK hash ✓ — bulud tərəfi ilə EYNİ nəticə ✓✓✓
                var hash = HesablaMəzmun(node);

                if (_sonHash.TryGetValue(tamYol, out var köhnə) &&
                    string.Equals(köhnə, hash, StringComparison.Ordinal))
                {
                    continue; // 🔁 dəyişməyib ✓
                }

                // ⏱️ AUDİT — yalnız HƏQİQİ dəyişiklikdə ✓
                node["updatedAt"] = FirebaseOptions.UtcIndi();
                node["updatedBy"] = _o.CihazAdi;
                node["isDeleted"] = false;
                node["deletedAt"] = null;

                var json = System.Text.Json.JsonSerializer.Serialize(node, FirebaseOptions.Json);

                if (!await _klient.XamYazAsync(tamYol, json, ct).ConfigureAwait(false))
                {
                    AppLogger.Xeberdarliq(
                        $"🔌 Bulud əlçatmazdır ✗ — {kolleksiya} göndərilmədi ✓ (növbəti dövrdə təkrar ✓)");
                    break;
                }

                _sonHash[tamYol] = hash;
                göndərilən++;

                // ================================================================
                //  🧠 İZƏ SAL ✓✓✓  (v6.2.14 — ★ «TƏKRAR YAZMA»NIN QARŞISI ★)
                // ----------------------------------------------------------------
                //  ⚠ ƏVVƏL izlər yalnız YADDAŞDA idi ✗ → proqram hər açılışda
                //  bütün qeydləri TƏKRAR buluda yazırdı ✗✓✓ (2567 qeyd!)
                //  ✅ İNDİ: hər uğurlu yazma «bulud_izleme» cədvəlinə yazılır ✓
                //  → növbəti açılışda bu hash-lər yüklənir ✓ → DƏYİŞMƏYƏN qeyd
                //  GÖNDƏRİLMİR ✗✓✓ (istifadəçi tələbi: «heç nə yazılmasın» ✓)
                // ================================================================
                try
                {
                    IzlemeYaz(cx, kolleksiya, açar, hash, DateTime.UtcNow, false);
                }
                catch (Exception ex)
                {
                    AppLogger.Xeta(ex, "izləmə yazma");
                }
            }

            // 🗑️ SQLite-dan SİLİNƏNLƏR → BULUDDAN SİLİNİR ✓✓✓ (yer tutmasın ✓)
            // ================================================================
            //  🛑 v6.2.16 — ★ KÜTLƏVİ SİLMƏ QORUYUCUSU ★
            //  ----------------------------------------------------------------
            //  ⚠ İstifadəçi tələbi: «əvvəl əlavə olunan qeyd SİLİNMƏMƏLİDİR» ✓✓✓
            //  ⚠ TƏHLÜKƏ: baza sıfırlanırsa / açılmırsa ✗ → «hamısı silinib»
            //    görünür ✗ → BÜTÜN BULUD SİLİNƏRDİ ✗✓✓ (fəlakət ✗)
            //  ✅ İNDİ: itkinlərin sayı qeyri-təbii çoxdursa (10-dan artıq ✗
            //    VƏ eyni zamanda 25%-dən çox ✗) → HEÇ NƏ SİLİNMİR ✗ ·
            //    xəbərdarlıq yazılır ✓✓✓ (məlumat QORUNUR ✓)
            // ================================================================
            if (_sonAçarlar.TryGetValue(kolleksiya, out var əvvəlki))
            {
                var itkinlər = əvvəlki.Except(indiki).ToList();

                var qorxu = əvvəlki.Count > 0
                            && itkinlər.Count > 10
                            && itkinlər.Count * 4 > əvvəlki.Count;

                if (qorxu)
                {
                    AppLogger.Xeberdarliq(
                        $"🛑 {kolleksiya}: {itkinlər.Count} qeyd «yoxdur» görünür ✗ — bu QEYRİ-TƏBİİdir ✓ → " +
                        "TƏHLÜKƏSİZLİK üçün HEÇ NƏ SİLİNMƏDİ ✗✓✓ (məlumat QORUNDU ✓)");
                }
                else
                {
                    foreach (var itmiş in itkinlər)
                    {
                        var uğur = FizikiSil
                            ? await _klient.FizikiSilAsync(kolleksiya + "/" + itmiş, ct)
                                .ConfigureAwait(false)
                            : await SoftSilAsync(kolleksiya + "/" + itmiş, ct)
                                .ConfigureAwait(false);

                        if (!uğur) break; // 🔌 offline ✓ → növbəti dövrdə ✓

                        _sonHash.Remove(kolleksiya + "/" + itmiş);
                        SilinenSayi++;
                        silmeSayğacı(1);
                    }
                }
            }

            _sonAçarlar[kolleksiya] = indiki;
            return göndərilən;
        }

        /// <summary>🗑️ Buludda <b>SOFT-DELETE</b> ✓✓✓ (SQLite-dan silinən sətirlər üçün ✓)</summary>
        private async Task<bool> SoftSilAsync(string yol, CancellationToken ct)
        {
            try
            {
                var indi = FirebaseOptions.UtcIndi();

                return await _klient.YenileAsync(yol, new Dictionary<string, object?>
                {
                    ["isDeleted"] = true,
                    ["deletedAt"] = indi,
                    ["updatedAt"] = indi,
                    ["updatedBy"] = _o.CihazAdi
                }, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "soft-sil: " + yol);
                return false;
            }
        }

        /// <summary>🔑 Məzmun hash-ı ✓ (SHA-256 · hex ✓) — dəyişiklik aşkarlama ✓</summary>
        private static string Hesabla(string məzmun)
        {
            try
            {
                var baytlar = System.Text.Encoding.UTF8.GetBytes(məzmun);
                var hash = System.Security.Cryptography.SHA256.HashData(baytlar);
                return Convert.ToHexString(hash.AsSpan(0, 16));
            }
            catch
            {
                return məzmun.Length.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>📊 Status nodu ✓ (<c>021Cars/_meta</c> ✓ — konsolda görünür ✓)</summary>
        private async Task MetaYazAsync(int push, int silme, CancellationToken ct)
        {
            try
            {
                var meta = new Dictionary<string, object?>
                {
                    ["cihaz"] = _o.CihazAdi,
                    ["sonSinxron"] = FirebaseOptions.UtcIndi(),
                    ["fasileSaniye"] = FasileSaniye,
                    ["gonderilen"] = push,
                    ["silinen"] = silme,
                    ["veziyyet"] = _klient.Onlayn ? "onlayn ✓" : "oflayn ✗",
                    ["sayi"] = new Dictionary<string, object?>
                    {
                        ["cars"] = _sonAçarlar.TryGetValue("cars", out var c) ? c.Count : 0,
                        ["credits"] = _sonAçarlar.TryGetValue("credits", out var k) ? k.Count : 0,
                        ["sales"] = _sonAçarlar.TryGetValue("sales", out var s) ? s.Count : 0,
                        ["expenses"] = _sonAçarlar.TryGetValue("expenses", out var e) ? e.Count : 0,
                        ["partnerShares"] = _sonAçarlar.TryGetValue("partnerShares", out var p) ? p.Count : 0
                    }
                };

                await _klient.YenileAsync("_meta", meta, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "meta yazma");
            }
        }

        /// <summary>🧹 Resursları azad edir ✓ (exception ATILMIR ✗)</summary>
        public void Dispose()
        {
            try { Dayandir(); _cts.Dispose(); } catch { }
        }

        /// <summary>💾 Fləşkart (tənbəl yaradılır ✓)</summary>
        private UsbDriveDetector? _usb;

        /// <summary>🧹 Köhnə soft-delete qalıqları yalnız BİR DƏFƏ təmizlənir ✓</summary>
        private bool _zibilTemizlendi;

        /// <summary>
        /// 🧹 Buluddaki köhnə <b>SOFT-DELETE qalıqlarını FİZİKİ SİLİR</b> ✓✓✓
        /// <para>Yerli bazada olmayan + <c>isDeleted = true</c> olan qeydlər → silinir ✓ (yer boşalır ✓)</para>
        /// </summary>
        public async Task<int> ZibiliTemizleAsync(CancellationToken ct = default)
        {
            var say = 0;

            if (!FizikiSil) return 0;

            // ================================================================
            //  ⚠️ VACİB ✗✓✓✓: TOMBSTONE-lar DƏRHAL SİLİNMƏMƏLİDİR ✗
            // ----------------------------------------------------------------
            //  Əvvəl: silinən qeydin «zibil» yazısı dərhal buluddan silinirdi ✗
            //  → o anda OFLAYN olan / gecikən kompüterlər silinməni HEÇ VAXT
            //    öyrənmirdi ✗ → «bir kompüterdə sildim, digərində silinmədi» ✗✓✓
            //
            //  İNDİ: tombstone 60 GÜN saxlanılır ✓ → bütün kompüterlər MÜTLƏQ
            //  onu görüb silinməni tətbiq edir ✓✓✓
            //  (yalnız 60 gündən KÖHNƏ olanlar buluddan silinir ✓ — yer boşalır ✓)
            // ================================================================
            var hədd = DateTime.UtcNow.AddDays(-Math.Clamp(ZibilSaxlamaGun, 7, 3650));

            try
            {
                foreach (var kolleksiya in Kolleksiyalar)
                {
                    var xam = await _klient.OxuAsync(kolleksiya, ct).ConfigureAwait(false);

                    if (xam is null) continue;

                    foreach (var cüt in xam)
                    {
                        if (cüt.Value is not JsonObject o) continue;
                        if (o["isDeleted"]?.GetValue<bool>() != true) continue;

                        // ⚠ Yerli bazada VARDIRSA toxunmuruq ✗
                        if (_sonAçarlar.TryGetValue(kolleksiya, out var yerli) &&
                            yerli.Contains(cüt.Key))
                        {
                            continue;
                        }

                        // ⏳ TƏZƏ tombstone → SAXLA ✓✓✓ (digər kompüterlər hələ görməyə bilər ✗)
                        var silinmə = OxuVaxt(
                            o["silinmeTarixi"]?.ToString() ?? o["deletedAt"]?.ToString());

                        if (silinmə == DateTime.MinValue || silinmə > hədd)
                        {
                            continue;      // 🛡️ saxlanılır ✓ (60 gün ✓)
                        }

                        if (await _klient.FizikiSilAsync(kolleksiya + "/" + cüt.Key, ct)
                            .ConfigureAwait(false))
                        {
                            say++;
                        }
                    }
                }

                if (say > 0)
                {
                    AppLogger.Melumat($"🧹 Köhnə soft-delete qalıqları silindi ✓ — {say} qeyd ✓ (yer boşaldı ✓)");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "zibil təmizləmə");
            }

            return say;
        }

        /// <summary>
        /// 📦 <b>YERLİ MƏLUMATI JSON-A ÇEVİRİR</b> ✓✓✓ — 🔥 BULUD LAZIM DEYİL ✗
        /// <para>⚠ İnternet olmasa da yedək YAZILIR ✓ (yerli SQLite-dan oxunur ✓✓✓)</para>
        /// </summary>
        private async Task<Dictionary<string, JsonArray>> YerliJsonAsync(
            AppDbContext db, CancellationToken ct)
        {
            var nəticə = new Dictionary<string, JsonArray>(StringComparer.Ordinal);

            nəticə["cars"] = Çevir(await db.Cars.AsNoTracking().ToListAsync(ct), CarNode);
            nəticə["expenses"] = Çevir(await db.Expenses.AsNoTracking().ToListAsync(ct), XercNode);
            nəticə["sales"] = Çevir(await db.Sales.AsNoTracking().ToListAsync(ct), SatisNode);
            nəticə["credits"] = Çevir(await db.Credits.AsNoTracking().ToListAsync(ct), KreditNode);
            nəticə["creditTransactions"] =
                Çevir(await db.CreditTransactions.AsNoTracking().ToListAsync(ct), EmeliyyatNode);
            nəticə["partners"] = Çevir(await db.Partners.AsNoTracking().ToListAsync(ct), TerefdasNode);
            nəticə["partnerShares"] =
                Çevir(await db.PartnerShares.AsNoTracking().ToListAsync(ct), PayNode);
            nəticə["partnerPayments"] =
                Çevir(await db.PartnerPayments.AsNoTracking().ToListAsync(ct), OdenisNode);

            // ✅ v6.2.12 — ⏳ möhlətlər + 💵 kassa hərəkətləri də yedəyə daxildir ✓
            nəticə["odenisMohlets"] =
                Çevir(await db.OdenisMohletler.AsNoTracking().ToListAsync(ct), MohletNode);
            nəticə["kassaHereketleri"] =
                Çevir(await db.KassaHereketleri.AsNoTracking().ToListAsync(ct), KassaNode);

            return nəticə;
        }

        /// <summary>🔁 Sətirləri JSON massivinə çevirir ✓</summary>
        private static JsonArray Çevir<T>(
            IEnumerable<T> sətirlər,
            Func<T, Dictionary<string, object?>> nodeAl)
        {
            var massiv = new JsonArray();

            foreach (var sətr in sətirlər)
            {
                try
                {
                    massiv.Add(System.Text.Json.JsonSerializer
                        .SerializeToNode(nodeAl(sətr), FirebaseOptions.Json));
                }
                catch { }
            }

            return massiv;
        }

        /// <summary>
        /// 📎 <b>MEDIA FAYLLARINI USB-YƏ KÖÇÜRÜR</b> ✓✓✓
        /// <para>
        /// ⚠ <b>SƏBƏB:</b> fləşkart yoxkən əlavə olunan PDF/şəkil
        /// <c>%LocalAppData%\EnterpriseAeroStudio\Media</c>-də qalır ✗ →
        /// fləşkart taxılsa da USB-yə <b>HEÇ NƏ KÖÇÜRÜLMÜRDÜ</b> ✗✓✓
        /// </para>
        /// <para>
        /// ✅ <b>HƏLL:</b> fləşkart hazır olan kimi bütün media qeydləri yoxlanılır ✓,
        /// USB-də olmayanlar <b>eynistrukturla</b> kopyalanır ✓
        /// (<c>{Fləşkart}:\021Cars\Media\{RefType}\{qovluq}\{fayl}</c> ✓)
        /// </para>
        /// <para>📌 Yerli surət SİLİNMİR ✗ → fayl HƏM kompüterdə ✓ HƏM USB-də ✓ olur ✓✓✓</para>
        /// </summary>
        public async Task<int> MedianiKocurAsync(CancellationToken ct = default)
        {
            if (!UsbHazir || _usb?.KokYol is null) return 0;   // 🔌 fləşkart yoxdur ✗

            var say = 0;

            try
            {
                using var qap = _srv.CreateScope();
                using var db = qap.ServiceProvider.GetRequiredService<AppDbContext>();

                var usbMediaKök = Path.Combine(_usb.KokYol, "Media");
                Directory.CreateDirectory(usbMediaKök);

                var yerliKök = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EnterpriseAeroStudio", "Media");

                var sətirlər = await db.Attachments.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);

                foreach (var m in sətirlər)
                {
                    ct.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(m.StoredPath)) continue;
                    if (!File.Exists(m.StoredPath)) continue;   // 📄 fayl yoxdur ✗

                    // ✅ artıq USB-dədirsə keç ✗
                    if (m.StoredPath.StartsWith(_usb.KokYol, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        // 📁 EYNİ struktur: Media\{RefType}\{qovluq}\{fayl} ✓✓✓
                        var nisbi = m.StoredPath.StartsWith(yerliKök, StringComparison.OrdinalIgnoreCase)
                            ? Path.GetRelativePath(yerliKök, m.StoredPath)
                            : Path.Combine(string.IsNullOrWhiteSpace(m.RefType) ? "Diger" : m.RefType,
                                           Path.GetFileName(m.StoredPath));

                        if (nisbi.StartsWith("..", StringComparison.Ordinal))
                        {
                            nisbi = Path.Combine(string.IsNullOrWhiteSpace(m.RefType) ? "Diger" : m.RefType,
                                                 Path.GetFileName(m.StoredPath));
                        }

                        var hedef = Path.Combine(usbMediaKök, nisbi);

                        if (File.Exists(hedef))
                        {
                            continue;   // ✅ artıq kopyalanıb ✓
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(hedef) ?? usbMediaKök);
                        File.Copy(m.StoredPath, hedef, overwrite: false);

                        say++;
                        AppLogger.Melumat($"📎 USB-yə köçürüldü ✓ → {nisbi}");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Xeberdarliq($"📎 «{m.FileName}» köçürülmədi ✗ — {ex.Message}");
                    }
                }

                if (say > 0)
                {
                    AppLogger.Melumat($"📎 {say} media faylı USB-yə köçürüldü ✓ → {usbMediaKök} ✓✓✓");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "media köçürmə");
            }

            return say;
        }

        /// <summary>
        /// 💾 <b>FAYLI TƏHLÜKƏSİZ YAZIR</b> ✓✓✓ — ★ MÜTLƏQ ★
        /// <para>
        /// ① <b>Növbə:</b> eyni anda yalnız BİR yedək yazısı ✓ (<see cref="YedekQapisi"/> ✓)
        /// ② <b>Atomik:</b> əvvəl <c>*.tmp</c> yazılır ✓ → sonra <c>File.Move(…, overwrite)</c> ✓
        ///    (yarımçıq / boş JSON faylı HEÇ VAXT qalmır ✗✓✓)
        /// ③ <b>Təkrar cəhd:</b> fayl başqa prosesdə açıqdırsa ✓ (antivirus · Explorer ·
        ///    USB bərpa oxuması ✓) → 6 dəfə gözləyib yenidən yazır ✓✓✓
        /// </para>
        /// </summary>
        private static async Task FayliYazAsync(string yol, string mezmun, CancellationToken ct)
        {
            await YedekQapisi.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var muveqqeti = yol + ".tmp";

                await File.WriteAllTextAsync(muveqqeti, mezmun, ct).ConfigureAwait(false);

                for (var cehd = 0; ; cehd++)
                {
                    try
                    {
                        File.Move(muveqqeti, yol, overwrite: true);
                        return;
                    }
                    catch (Exception ex) when (cehd < 6 &&
                        (ex is IOException || ex is UnauthorizedAccessException))
                    {
                        await Task.Delay(150 * (cehd + 1), ct).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                YedekQapisi.Release();
            }
        }

        /// <summary>
        /// 🖥️ <b>KOMPÜTERƏ YEDƏK</b> ✓✓✓ — məlumat yerli fayllara yazılır ✓
        /// <para>📂 <c>%LOCALAPPDATA%\EnterpriseAeroStudio\Yedekler\Json\*.json</c> ✓ + <c>SON_SINXRON.txt</c> ✓</para>
        /// <para>⏱️ Fasilə: <see cref="YerliYedekSaniye"/> <b>SANİYƏ</b> ✓ (👑 Seccad / 🛡️ Asif dəyişir ✓)</para>
        /// </summary>
        public async Task YerliYedekAsync(CancellationToken ct = default)
        {
            try
            {
                if (SonYerliYedek.HasValue &&
                    (DateTime.Now - SonYerliYedek.Value).TotalSeconds <
                    Math.Max(5, YerliYedekSaniye))
                {
                    return;
                }

                // ================================================================
                //  ★ ƏSAS HƏLL ★ — 🔢 DƏYİŞİKLİK YOXDURSA YEDƏK YAZILMIR ✗✓✓✓
                // ----------------------------------------------------------------
                //  ⚠️ ƏVVƏL ✗: 2169 qeyd hər 5-10 saniyədə yenidən yazılırdı ✗ →
                //     disk 100% ✗ → proqram DONURDU ✗✓✓
                //  ✅ İNDİ: yalnız REAL dəyişiklik olanda ✓✓✓
                // ================================================================
                if (Versiya == _sonYerliYedekVersiya) return;

                _sonYerliYedekVersiya = Versiya;

                var tam = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EnterpriseAeroStudio", "Yedekler", "Json");

                Directory.CreateDirectory(tam);

                var qeyd = 0;

                // 📦 YERLİ BAZADAN oxunur ✓✓✓ (🔥 bulud LAZIM DEYİL ✗ — offline də işləyir ✓)
                using (var qap = _srv.CreateScope())
                using (var db = qap.ServiceProvider.GetRequiredService<AppDbContext>())
                {
                    var məlumat = await YerliJsonAsync(db, ct).ConfigureAwait(false);

                    foreach (var cüt in məlumat)
                    {
                        await FayliYazAsync(
                            Path.Combine(tam, cüt.Key + ".json"),
                            cüt.Value.ToJsonString(new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = true
                            }), ct).ConfigureAwait(false);

                        qeyd += cüt.Value.Count;
                    }
                }

                // 🫀 ÜRƏK DÖYÜNTÜSÜ ✓ (faylın tarixi HƏMİŞƏ TƏZƏ ✓)
                await FayliYazAsync(
                    Path.Combine(tam, "SON_SINXRON.txt"),
                    "021Cars — KOMPÜTER YEDƏYİ ✓" + Environment.NewLine +
                    "═══════════════════════════════════════" + Environment.NewLine +
                    $"📅 Tarix      : {DateTime.Now:dd.MM.yyyy HH:mm:ss}" + Environment.NewLine +
                    $"🖥️ Cihaz     : {_o.CihazAdi}" + Environment.NewLine +
                    $"⏱️ Fasilə    : {YerliYedekSaniye} saniyə ✓" + Environment.NewLine +
                    $"📦 Kolleksiya: {Kolleksiyalar.Length}" + Environment.NewLine +
                    $"📊 Qeyd sayı : {qeyd}" + Environment.NewLine,
                    ct).ConfigureAwait(false);

                SonYerliYedek = DateTime.Now;
                AppLogger.Melumat($"🖥️ Kompüter yedəyi yazıldı ✓ → {tam} · {qeyd} qeyd ✓");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "kompüter yedəyi");
            }
        }

        /// <summary>
        /// 💾 <b>USB-YƏ YEDƏK + ÜRƏK DÖYÜNTÜSÜ</b> ✓✓✓
        /// <para>
        /// Hər <see cref="YedekDakkasi"/> dəqiqədə buluddaki məlumat
        /// <c>{Fləşkart}\021Cars\Yedekler\Json\*.json</c> fayllarına yazılır ✓ +
        /// <c>SON_SINXRON.txt</c> yenilənir ✓ → faylların <b>tarixi həmişə təzə</b> olur ✓
        /// </para>
        /// 🔌 Fləşkart yoxdursa səssiz keçir ✓ (çökmə YOX ✗)
        /// </summary>
        public async Task UsbYedekAsync(CancellationToken ct = default)
        {
            try
            {
                if (SonYedek.HasValue &&
                    (DateTime.Now - SonYedek.Value).TotalSeconds < Math.Max(5, YedekSaniye))
                {
                    return;
                }

                // ★ ƏSAS HƏLL ★ — 🔢 DƏYİŞİKLİK YOXDURSA USB-YƏ YAZILMIR ✗✓✓✓
                //  (2169 qeyd + media hər dövrədə yenidən yazılırdı ✗ → donma ✗✓✓)
                if (Versiya == _sonYedekVersiya) return;

                _sonYedekVersiya = Versiya;

                _usb ??= new UsbDriveDetector();

                if (!_usb.Hazir) _usb.SkanEt();
                if (!_usb.Hazir) return; // 🔌 fləşkart yoxdur ✗

                if (!_usb.QovluqYarat(@"Yedekler\Json", out var tam) || tam is null) return;

                var qeyd = 0;

                // 📦 YERLİ BAZADAN oxunur ✓✓✓ (🔥 bulud LAZIM DEYİL ✗ — offline də işləyir ✓)
                using (var qap = _srv.CreateScope())
                using (var db = qap.ServiceProvider.GetRequiredService<AppDbContext>())
                {
                    var məlumat = await YerliJsonAsync(db, ct).ConfigureAwait(false);

                    foreach (var cüt in məlumat)
                    {
                        await FayliYazAsync(
                            Path.Combine(tam, cüt.Key + ".json"),
                            cüt.Value.ToJsonString(new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = true
                            }), ct).ConfigureAwait(false);

                        qeyd += cüt.Value.Count;
                    }
                }

                // 🫀 ÜRƏK DÖYÜNTÜSÜ — faylın «Date modified» HƏMİŞƏ TƏZƏ olur ✓✓✓
                await FayliYazAsync(
                    Path.Combine(tam, "SON_SINXRON.txt"),
                    "021Cars — USB YEDƏYİ ✓" + Environment.NewLine +
                    "═══════════════════════════════════════" + Environment.NewLine +
                    $"📅 Tarix      : {DateTime.Now:dd.MM.yyyy HH:mm:ss}" + Environment.NewLine +
                    $"🖥️ Cihaz     : {_o.CihazAdi}" + Environment.NewLine +
                    $"🔄 Fasilə    : {FasileSaniye} saniyə ✓" + Environment.NewLine +
                    $"📦 Kolleksiya: {Kolleksiyalar.Length}" + Environment.NewLine +
                    $"📊 Qeyd sayı : {qeyd}" + Environment.NewLine +
                    $"🌐 Vəziyyət  : {(_klient.Onlayn ? "onlayn ✓" : "oflayn ✗")}" + Environment.NewLine,
                    ct).ConfigureAwait(false);

                // 📎 MEDIA FAYLLARINI DA USB-YƏ KÖÇÜR ✓✓✓
                //    (fləşkart yoxkən əlavə olunan PDF/şəkillər də avtomatik gəlir ✓)
                var media = await MedianiKocurAsync(ct).ConfigureAwait(false);

                SonYedek = DateTime.Now;
                AppLogger.Melumat(
                    $"💾 USB yedəyi yazıldı ✓ → {tam} · {qeyd} qeyd ✓" +
                    (media > 0 ? $" · 📎 {media} fayl köçürüldü ✓✓✓" : ""));
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "USB yedək");
            }
        }

        /// <summary>
        /// 🔄 Bir cədvəli <b>iki tərəfli</b> sinxronlaşdırır ✓✓✓ —
        /// LWW (<i>Last Write Wins</i>) ilə: <b>ən təzə tarix üstün gəlir</b> ✓
        /// </summary>
        /// <returns>Yerli bazaya tətbiq olunan dəyişiklik sayı ✓</returns>
        private async Task<int> CədvəliSinxronlaAsync<T>(
            AppDbContext db,
            SqliteConnection cx,
            string kol,
            List<T> yerli,
            Func<T, string> açarAl,
            Func<T, Dictionary<string, object?>> nodeAl,
            JsonObject? bulud,
            JsonObject? zibil,
            CancellationToken ct)
            where T : class, IBuludIdli, new()
        {
            var izleme = IzlemeAl(cx, kol);
            var dəyişən = 0;

            // ================================================================
            //  🔗 ƏLAQƏ XƏRİTƏLƏRİ — hər cədvəldən ƏVVƏL yenilənir ✓✓✓ (v6.2.16)
            //  ----------------------------------------------------------------
            //  ⚠ Valideynlər ƏVVƏL gəlir ✓ (cars → sales/credits → …
            //    → odenisMohlets/kassaHereketleri ✓) → uşaqlar onları TAPIR ✓
            //  ⚠ SaveChanges BURADA çağırılır ✓ → əvvəlki cədvəlin yeni
            //    qeydləri bazaya düşür ✓ → xəritələrə daxil olur ✓✓✓
            // ================================================================
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await XəritələriDoldurAsync(db, ct).ConfigureAwait(false);

            // ① ☁️ BULUD → YERLİ ✓ (yalnız DAHA TƏZƏ olanlar ✓)
            if (bulud is not null)
            {
                foreach (var cüt in bulud)
                {
                    if (cüt.Value is not JsonObject o) continue;

                    var id = cüt.Key;
                    var uzaqVaxt = OxuVaxt(o["updatedAt"]?.ToString());
                    izleme.TryGetValue(id, out var iz);

                    // ⚖️ LWW: yerli daha təzədirsə → toxunmuruq ✗✓✓
                    if (uzaqVaxt <= (iz?.SonDeyisiklik ?? DateTime.MinValue)) continue;

                    var uzaqBuludId = o["buludId"]?.ToString();

                    var yerliSətr = yerli.FirstOrDefault(x => açarAl(x) == id);

                    // ================================================================
                    //  🆔 KİMLİK YOXLAMASI ✓✓✓ (v6.2.16 — ★ ƏSAS DÜZƏLİŞ ★)
                    // ----------------------------------------------------------------
                    //  ⚠ PROBLEM (istifadəçi: «bir kompüterə maşın əlavə edirəm,
                    //    o birində görünmür; əvvəlki qeyd silinir» ✗✓✓):
                    //    hər kompüter ÖZ rəqəm ID-sini verirdi ✗ → ikisi də
                    //    «232» yaradırdı ✗ → buluddan gələn qeyd YERLİ FƏRQLİ
                    //    qeydlə eyni açar altında idi ✗ → ya üzərinə yazırdı ✗,
                    //    ya da LWW «köhnədir» deyib HEÇ TƏTBİQ ETMİRDİ ✗✓✓
                    //  ✅ İNDİ: «buludId» (GUID ✓) FƏRQLİDIRSƏ → bu AYRI QEYDDİR ✓
                    //    → yerli «tutan» qeyd RE-KEY olunur ✓ (GUID alır ✓,
                    //      buluddakı köhnə rəqəm node-u təmizlənir ✓)
                    //    → gələn qeyd isə YENİ kimi əlavə olunur ✓✓✓
                    //    ⇒ HEÇ BİR MƏLUMAT İTMİR ✗ · HEÇ NƏ SİLİNMİR ✗✓✓
                    // ================================================================
                    if (yerliSətr is not null && !EyniQeyddir(uzaqBuludId, (IBuludIdli)yerliSətr))
                    {
                        await TutaniAzadEtAsync(db, cx, kol, (IBuludIdli)yerliSətr, ct)
                            .ConfigureAwait(false);

                        yerli.Remove(yerliSətr);
                        yerliSətr = null;   // ➕ aşağıda YENİ qeyd kimi əlavə olunur ✓
                    }

                    if (yerliSətr is null)
                    {
                        // ➕ YENİ QEYD (başqa kompüterdə yaranıb ✓)
                        var yeni = new T();
                        XüsuslarıTətbiq(yeni, o);

                        var kok = (IBuludIdli)yeni;

                        // 🔑 KİMLİK: GUID varsa → BuludId ✓ (açar odur ✓);
                        //    yoxsa köhnə rəqəm ID ✓ (hər iki kompüterdə eynidir ✓)
                        if (!string.IsNullOrWhiteSpace(uzaqBuludId))
                        {
                            kok.BuludId = uzaqBuludId;

                            // ⚠ GUID-li qeydin yerli ID-si SƏRBƏSTDİR ✓ →
                            //   EF özü YENİ ID verir ✓ (rəqəm toqquşması YARANMIR ✗✓✓)
                            kok.Id = 0;
                        }
                        else if (int.TryParse(id, NumberStyles.Integer,
                                 CultureInfo.InvariantCulture, out var uzaqId))
                        {
                            // 🏛️ KÖHNƏ qeyd ✓ — orijinal ID saxlanılır ✓
                            //   (əlaqələr carId/creditId ilə işləyir ✓)
                            kok.Id = uzaqId;
                        }

                        ƏlaqələriBağla(yeni, o);   // 🔗 *BuludId → yerli ID ✓✓✓

                        if (await YerliEkleAsync(db, yeni, ct).ConfigureAwait(false))
                        {
                            yerli.Add(yeni);

                            // ⚠ İzləməyə BULUDUN hash-i yazılır ✓ (yerlinin yox ✗) →
                            //   dərhal geri göndərilmir ✗ → «sonsuz dövr» olmur ✗✓✓
                            IzlemeYaz(cx, kol, id, HesablaMəzmun(o), uzaqVaxt, false);
                            dəyişən++;

                            KökXəritələrəƏlavəEt(yeni);   // 🔗 uşaqlar üçün ✓
                        }
                    }
                    else
                    {
                        // ============================================================
                        //  ♻️ MÖVCUD QEYD — YALNIZ GƏLİŞDİRİLƏN sahələr ✓
                        //  ⚠ `XüsuslarıTətbiq` artıq «id»/«buludId»-ni ötürmür ✗✓✓
                        //    → yerli AÇAR və ƏLAQƏLƏR toxunulmaz qalır ✓✓✓
                        // ============================================================
                        XüsuslarıTətbiq(yerliSətr, o);
                        db.Update(yerliSətr);
                        IzlemeYaz(cx, kol, id, HesablaMəzmun(o), uzaqVaxt, false);
                        dəyişən++;
                    }
                }
            }

            // ② 🗑️ TOMBSTONE → YERLİ BAZADAN SİL ✓✓✓ (<b>BMW SSENARİSİ ✓</b>)
            if (zibil?[kol] is JsonObject z)
            {
                foreach (var cüt in z)
                {
                    if (cüt.Value is not JsonObject t) continue;

                    var id = cüt.Key;
                    var silinməVaxtı = OxuVaxt(t["silinmeTarixi"]?.ToString());
                    izleme.TryGetValue(id, out var iz);

                    // ⚠ Heç vaxt bizdə olmayıbsa → yerli silmə lazım deyil ✗
                    if (iz is null) continue;

                    // ⚖️ LWW: yerli daha təzədirsə (bərpa olunub ✓) → silmirik ✗
                    if (silinməVaxtı <= iz.SonDeyisiklik) continue;

                    var yerliSətr = yerli.FirstOrDefault(x => açarAl(x) == id);

                    if (yerliSətr is not null)
                    {
                        db.Remove(yerliSətr);
                        yerli.Remove(yerliSətr);
                    }

                    IzlemeYaz(cx, kol, id, "", silinməVaxtı, true);
                    dəyişən++;

                    AppLogger.Melumat(
                        $"🗑️ {kol}/{id} — başqa kompüter silib ✓ → yerli bazadan da silindi ✓");
                }
            }

            return dəyişən;
        }

        /// <summary>
        /// 🔑 <b>MƏZMUN HASH-I (KANONİK)</b> ✓✓✓ — həm YERLİ, həm BULUD node-u üçün
        /// <b>EYNİ</b> nəticə verir ✓✓✓ (v6.2.16)
        /// <para>
        /// <b>Necə?</b>
        /// </para>
        /// <list type="number">
        ///   <item>Açarlar <b>ƏLİFBA SIRASI</b> ilə düzülür ✗✓✓ (JSON-da sıra fərqli ola bilər ✗)</item>
        ///   <item><c>null</c> dəyərlər ATILIR ✗✓✓ (Firebase <c>null</c>-u saxlamır ✗)</item>
        ///   <item>Audit / identifikator sahələri ATILIR ✗✓✓
        ///         (<c>id</c> · <c>buludId</c> · <c>updatedAt</c> · <c>updatedBy</c> ·
        ///         <c>isDeleted</c> · <c>deletedAt</c>)</item>
        ///   <item>Dəyərlər <b>eyni</b> JSON qaydası ilə yazılır ✓ (mətn <c>"..."</c> ·
        ///         rəqəm <c>8000</c> · məntiqi <c>true</c>)</item>
        /// </list>
        /// <para>
        /// ⚠ Bu olmasa iki kompüter <b>sonsuz</b> olaraq bir-birinin üzərinə yazırdı ✗✓✓
        /// (çünki yerli <c>id</c> fərqlidir ✗ və açar sırası fərqlidir ✗)
        /// </para>
        /// </summary>
        private static string HesablaMəzmun(Dictionary<string, object?> node)
        {
            var sətirlər = new List<(string Açar, string Dəyər)>();

            foreach (var (açar, dəyər) in node)
            {
                if (dəyər is null || AuditSahələri.Contains(açar, StringComparer.Ordinal))
                {
                    continue;
                }

                sətirlər.Add((açar, System.Text.Json.JsonSerializer.Serialize(dəyər, FirebaseOptions.Json)));
            }

            return KanonikHash(sətirlər);
        }

        /// <summary>☁️ Buluddaki node-un eyni kanonik hash-ı ✓✓✓ (v6.2.16)</summary>
        private static string HesablaMəzmun(JsonObject buludNodeu)
        {
            var sətirlər = new List<(string Açar, string Dəyər)>();

            foreach (var cüt in buludNodeu)
            {
                if (cüt.Value is null || AuditSahələri.Contains(cüt.Key, StringComparer.Ordinal))
                {
                    continue;
                }

                sətirlər.Add((cüt.Key, cüt.Value.ToJsonString(FirebaseOptions.Json)));
            }

            return KanonikHash(sətirlər);
        }

        /// <summary>🧮 Kanonik hash ✓ — açarlar sıralanır ✓, dəyərlər eyni formada ✓</summary>
        private static string KanonikHash(List<(string Açar, string Dəyər)> cütlər)
        {
            var sb = new System.Text.StringBuilder("{");
            var ilk = true;

            foreach (var (açar, dəyər) in cütlər.OrderBy(x => x.Açar, StringComparer.Ordinal))
            {
                if (!ilk)
                {
                    sb.Append(',');
                }

                sb.Append('"').Append(açar).Append("\":").Append(dəyər);
                ilk = false;
            }

            return Hesabla(sb.Append('}').ToString());
        }

        // ====================================================================
        //  🧠 İZLƏMƏ CƏDVƏLİ (bulud_izleme) — LWW-NİN ÜRƏYİ ✓✓✓
        //  Hər qeyd üçün: son hash ✓ + son dəyişiklik vaxtı ✓ + silinib? ✓
        //  ⚠ Tətbiqin öz modellərinə TOXUNULMUR ✗ — ayrıca cədvəl ✓
        // ====================================================================

        /// <summary>🕒 ISO-8601 mətnini UTC <see cref="DateTime"/>-a çevirir ✓</summary>
        private static DateTime OxuVaxt(string? iso) =>
            DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
                ? d
                : DateTime.MinValue;

        /// <summary>
        /// 📝 JSON node-un dəyərlərini obyektin xassələrinə tətbiq edir ✓✓✓
        /// <para>
        /// Ad üzrə uyğunlaşdırma (böyük/kiçik hərf fərqi nəzərə alınmır ✓) +
        /// tip çevrilməsi (string · int · decimal · bool · DateTime ✓).
        /// Bir sahə uğursuz olsa qalanları tətbiq olunur ✓ (xəta udulur ✓)
        /// </para>
        /// <para>⚠ Yalnız <c>updatedAt/updatedBy/isDeleted/deletedAt</c> ÖTÜRÜLÜR ✗ (yerli bazada yoxdur ✓)</para>
        /// </summary>
        private static void XüsuslarıTətbiq(object hədəf, JsonObject o)
        {
            var tip = hədəf.GetType();

            foreach (var cüt in o)
            {
                if (cüt.Value is null) continue;

                // ================================================================
                //  🚫 «id» və «buludId» ÖTÜRÜLMÜR ✗✓✓ (v6.2.16)
                // ----------------------------------------------------------------
                //  ⚠ Bunlar KİMLİKDİR ✗ — yerli bazada AYRICA idarə olunur ✓.
                //    Əks halda EF «açar dəyişdi» deyə sıradaş/dup yaradırdı ✗✓✓
                // ================================================================
                if (cüt.Key is "updatedAt" or "updatedBy" or "isDeleted" or "deletedAt"
                    or "id" or "buludId") continue;

                var p = tip.GetProperty(cüt.Key,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (p is null || !p.CanWrite || p.GetIndexParameters().Length > 0) continue;
                if (p.GetCustomAttribute<NotMappedAttribute>() is not null) continue;

                try
                {
                    var xam = cüt.Value.ToString();
                    var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                    var boş = string.IsNullOrWhiteSpace(xam);

                    if (t == typeof(string)) p.SetValue(hədəf, xam);
                    else if (boş) continue;
                    else if (t == typeof(int)) p.SetValue(hədəf, int.Parse(xam, CultureInfo.InvariantCulture));
                    else if (t == typeof(long)) p.SetValue(hədəf, long.Parse(xam, CultureInfo.InvariantCulture));
                    else if (t == typeof(decimal)) p.SetValue(hədəf, decimal.Parse(xam, CultureInfo.InvariantCulture));
                    else if (t == typeof(double)) p.SetValue(hədəf, double.Parse(xam, CultureInfo.InvariantCulture));
                    else if (t == typeof(bool)) p.SetValue(hədəf, bool.Parse(xam));
                    else if (t == typeof(DateTime))
                    {
                        p.SetValue(hədəf, DateTime.Parse(xam, CultureInfo.InvariantCulture,
                            DateTimeStyles.RoundtripKind | DateTimeStyles.AdjustToUniversal));
                    }
                }
                catch
                {
                    // 🛡️ bir sahə uğursuz oldu ✗ → qalanı tətbiq olunur ✓
                }
            }
        }

        /// <summary>
        /// ➕ Yerli bazaya <b>İD SAXLAYARAQ</b> əlavə edir ✓✓✓
        /// <para>
        /// ⚠ EF <c>Add</c> identity-yə görə İD-ni DƏYİŞƏRDİ ✗ → ona görə xam SQL işlədilir ✓
        /// (əlaqələr — <c>CarId</c> · <c>CreditId</c> — POZULMUR ✗✓✓)
        /// </para>
        /// <para>Uğursuz olarsa → EF ilə adi əlavə (yeni İD ✓) + loq ✓ (qeyd İTMİR ✗)</para>
        /// </summary>
        private static async Task<bool> YerliEkleAsync(
            AppDbContext db, object entity, CancellationToken ct)
        {
            var tip = entity.GetType();

            // ================================================================
            //  🔑 v6.2.16 — YENİ TİPLİ (GUID) QEYDLƏR ✗✓✓
            //  ----------------------------------------------------------------
            //  ⚠ Onların yerli ID-si SƏRBƏSTDİR ✗ → rəqəm toqquşması OLMAZ ✗✓✓
            //    → EF özü ardıcıl nömrə verir ✓ (ən təhlükəsiz yol ✓)
            //  ⚠ KÖHNƏ (rəqəm ID-li) qeydlər isə aşağıdaki XAM SQL ilə əlavə
            //    olunur ✓ → orijinal ID saxlanılır ✓ (əlaqələr pozulmur ✗✓✓)
            // ================================================================
            if (entity is IBuludIdli kokIdli && kokIdli.Id == 0)
            {
                try
                {
                    db.Add(entity);
                    AppLogger.Melumat($"➕ {tip.Name}: buluddan YENİ qeyd əlavə olundu ✓ (GUID açar ✓ · yeni yerli ID ✓)");
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.Xeta(ex, "yerli əlavə (GUID/yeni ID): " + tip.Name);
                    return false;
                }
            }

            try
            {
                await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);

                var cədvəl = db.Model.FindEntityType(tip)?.GetTableName() ?? tip.Name + "s";

                var sütunlar = new List<string>();
                var yerTutucular = new List<string>();
                var parametrlər = new List<SqliteParameter>();

                // ⚠️ YALNIZ EF-in bildiyi REAL sütunlar ✓✓✓ — ★ VACİB DÜZƏLİŞ ★
                //    (əvvəl BÜTÜN public xassələr götürülürdü ✗ → naviqasiya toplusu
                //     «Expenses» də sütun kimi SQL-ə düşürdü ✗ →
                //     «table Avtomobiller has no column named Expenses» ✗✓✓)
                var ef = db.Model.FindEntityType(tip);
                if (ef is null) return false;

                foreach (var xasse in ef.GetProperties())
                {
                    if (xasse.IsShadowProperty()) continue;

                    var p = tip.GetProperty(xasse.Name,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                    if (p is null || !p.CanRead || !p.CanWrite) continue;
                    if (p.GetIndexParameters().Length > 0) continue;

                    sütunlar.Add($"\"{p.Name}\"");
                    yerTutucular.Add("$p" + parametrlər.Count);
                    parametrlər.Add(new SqliteParameter("$p" + parametrlər.Count,
                        p.GetValue(entity) ?? DBNull.Value));
                }

                if (sütunlar.Count == 0) return false;

                using var əmr = ((SqliteConnection)db.Database.GetDbConnection()).CreateCommand();

                // 🛡️ INSERT OR IGNORE ✓ — başqa proses/nüsxə eyni anda əlavə edibsə ✗ →
                //    təkrar qeyd YARANMIR ✗✓✓ (əvvəl unikal ID xətası verirdi ✗)
                əmr.CommandText = $"INSERT OR IGNORE INTO \"{cədvəl}\" ({string.Join(", ", sütunlar)}) " +
                                  $"VALUES ({string.Join(", ", yerTutucular)});";

                foreach (var x in parametrlər) əmr.Parameters.Add(x);

                var təsir = await əmr.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

                // ================================================================
                //  🛑 v6.2.16 — ★ SƏSSİZ İTKİNİN QARŞISI ★
                //  ----------------------------------------------------------------
                //  ⚠ ƏVVƏL: «INSERT OR IGNORE» İD tutulanda SƏSSİZCƏ udurdu ✗
                //    → «0 sətir əlavə olundu» ✗ · heç bir xəta YOX ✗
                //    → qeyd yerli bazaya DÜŞMÜRDÜ ✗ və İZLƏMƏYƏ yazılırdı ✗
                //    → HEÇ VAXT gəlmirdi ✗ (istifadəçi: «o birində görünmür» ✗✓✓)
                //  ✅ İNDİ: nəticə YOXLANILIR ✓ → İD tutulubsa YENİ İD ilə
                //    əlavə olunur ✓ · BİR QEYD DƏ İTMİR ✗✓✓
                // ================================================================
                if (təsir <= 0)
                {
                    throw new InvalidOperationException(
                        "İD tutulub ✗ → qeyd yeni yerli İD ilə əlavə olunacaq ✓");
                }

                // 🔢 SQLite sayğacını düzəlt ✓ (sonrakı əlavələr toqquşmasın ✗✓✓)
                try
                {
                    using var düzəliş =
                        ((SqliteConnection)db.Database.GetDbConnection()).CreateCommand();
                    düzəliş.CommandText =
                        $"UPDATE sqlite_sequence SET seq = (SELECT MAX(\"Id\") FROM \"{cədvəl}\") " +
                        $"WHERE name = '{cədvəl}' " +
                        $"AND seq < (SELECT MAX(\"Id\") FROM \"{cədvəl}\");";
                    await düzəliş.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                catch { }

                // ⚠ v6.2.16: `ChangeTracker.Clear()` ÇIXARILDI ✗✓✓
                //   Səbəb: həmin dövrdə əvvəlki sətirlərin TƏTBİQ OLUNMUŞ
                //   dəyişikliklərini də SİLİRDİ ✗ (onlar növbəti dövrdə
                //   «artıq sinxronlaşıb» sayılıb TƏTBİQ OLUNMURDU ✗✓✓)
                AppLogger.Melumat($"➕ {cədvəl}: buluddan YENİ qeyd əlavə olundu ✓");
                return true;
            }
            catch (Exception ex)
            {
                // ================================================================
                //  🛡️ ZƏMANƏT: QEYD İTMİR ✗✓✓ (v6.2.16 — YENİ İD VERİLİR ✓)
                //  ----------------------------------------------------------------
                //  ⚠ ƏVVƏL burada entity eyni İD ilə `Add` olunurdu ✗ →
                //    yenə toqquşurdu ✗ → qeyd İTİRDİ ✗✓✓
                //  ✅ İNDİ: İD SIFIRLANIR ✓ → EF özü YENİ (boş) İD verir ✓
                //    → məlumat MÜTLƏQ yerli bazaya düşür ✓✓✓
                // ================================================================
                AppLogger.Xeberdarliq(
                    $"⚠️ {tip.Name} orijinal İD ilə əlavə olunmadı ✗ ({ex.Message}) → YENİ İD ilə əlavə olunur ✓");

                try
                {
                    if (entity is IBuludIdli sıfırla) sıfırla.Id = 0;

                    db.Add(entity);

                    AppLogger.Melumat($"➕ {tip.Name}: buluddan YENİ qeyd əlavə olundu ✓ (yeni yerli İD ✓ · məlumat İTMƏDİ ✗✓✓)");
                    return true;
                }
                catch (Exception ex2)
                {
                    AppLogger.Xeta(ex2, "yerli əlavə: " + tip.Name);
                    return false;
                }
            }
            finally
            {
                try { await db.Database.CloseConnectionAsync().ConfigureAwait(false); } catch { }
            }
        }

        private sealed class IzlemeSetri
        {
            public string Kol { get; set; } = "";
            public string ElementId { get; set; } = "";
            public string Hash { get; set; } = "";
            public DateTime SonDeyisiklik { get; set; } = DateTime.MinValue;
            public bool Silinib { get; set; }
        }

        /// <summary>🏗️ Cədvəli yaradır ✓ (yoxdursa ✓)</summary>
        private static void CedveliYarat(SqliteConnection cx)
        {
            using var əmr = cx.CreateCommand();
            əmr.CommandText =
                "CREATE TABLE IF NOT EXISTS bulud_izleme (" +
                "  Kol           TEXT    NOT NULL," +
                "  ElementId     TEXT    NOT NULL," +
                "  Hash          TEXT    NOT NULL," +
                "  SonDeyisiklik TEXT    NOT NULL," +
                "  Silinib       INTEGER NOT NULL DEFAULT 0," +
                "  PRIMARY KEY (Kol, ElementId));";
            əmr.ExecuteNonQuery();
        }

        /// <summary>📖 Bir kolleksiyanın izləmə sətirlərini oxuyur ✓</summary>
        private static Dictionary<string, IzlemeSetri> IzlemeAl(SqliteConnection cx, string kol)
        {
            var nəticə = new Dictionary<string, IzlemeSetri>(StringComparer.Ordinal);

            using var əmr = cx.CreateCommand();
            əmr.CommandText =
                "SELECT ElementId, Hash, SonDeyisiklik, Silinib FROM bulud_izleme WHERE Kol = $k;";
            əmr.Parameters.AddWithValue("$k", kol);

            using var oxu = əmr.ExecuteReader();

            while (oxu.Read())
            {
                var id = oxu.GetString(0);
                nəticə[id] = new IzlemeSetri
                {
                    Kol = kol,
                    ElementId = id,
                    Hash = oxu.GetString(1),
                    SonDeyisiklik = DateTime.TryParse(oxu.GetString(2), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
                        ? d : DateTime.MinValue,
                    Silinib = oxu.GetInt32(3) != 0
                };
            }

            return nəticə;
        }

        /// <summary>✍️ İzləmə sətrini yazır ✓ (INSERT OR REPLACE ✓)</summary>
        private static void IzlemeYaz(SqliteConnection cx, string kol, string id,
            string hash, DateTime vaxt, bool silinib)
        {
            using var əmr = cx.CreateCommand();
            əmr.CommandText =
                "INSERT OR REPLACE INTO bulud_izleme (Kol, ElementId, Hash, SonDeyisiklik, Silinib) " +
                "VALUES ($k, $id, $h, $v, $s);";
            əmr.Parameters.AddWithValue("$k", kol);
            əmr.Parameters.AddWithValue("$id", id);
            əmr.Parameters.AddWithValue("$h", hash);
            əmr.Parameters.AddWithValue("$v", vaxt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
            əmr.Parameters.AddWithValue("$s", silinib ? 1 : 0);
            əmr.ExecuteNonQuery();
        }

        /// <summary>🧹 İzləmə sətrini silir ✓ (tombstone müddəti bitəndə ✓)</summary>
        private static void IzlemeSil(SqliteConnection cx, string kol, string id)
        {
            using var əmr = cx.CreateCommand();
            əmr.CommandText = "DELETE FROM bulud_izleme WHERE Kol = $k AND ElementId = $id;";
            əmr.Parameters.AddWithValue("$k", kol);
            əmr.Parameters.AddWithValue("$id", id);
            əmr.ExecuteNonQuery();
        }

        /// <summary>
        /// 🗑️ <b>TOMBSTONE</b> — silinmə qeydi ✓✓✓
        /// <para>
        /// Fiziki silinsə digər kompüter «heç vaxt olmayıb» ✗ ilə «silinib» ✗
        /// fərqini BİLƏ BİLMƏZ ✗ → buna görə silinmə tarixi ayrıca yazılır ✓
        /// (çox kiçik qeyd ✓ · <see cref="ZibilMuddetGun"/> sonra təmizlənir ✓)
        /// </para>
        /// </summary>
        private async Task TombstoneYazAsync(string kolleksiya, string id, CancellationToken ct)
        {
            try
            {
                await _klient.YenileAsync($"_zibil/{kolleksiya}/{id}",
                    new Dictionary<string, object?>
                    {
                        ["silinmeTarixi"] = FirebaseOptions.UtcIndi(),
                        ["cihaz"] = _o.CihazAdi
                    }, ct).ConfigureAwait(false);
            }
            catch (Exception ex) { AppLogger.Xeta(ex, "tombstone: " + kolleksiya + "/" + id); }
        }

        /// <summary>🗑️ Köhnə tombstone-ları təmizləyir ✓ (90 gün ✓ — yer boşalır ✓)</summary>
        public int ZibilMuddetGun { get; set; } = 90;

        /// <summary>📋 Sinxronlaşdırılan kolleksiyalar ✓ (📎 media XARİC ✗)</summary>
        private static readonly string[] Kolleksiyalar =
        {
            "cars", "expenses", "sales", "credits", "creditTransactions",
            "partners", "partnerShares", "partnerPayments",

            // ================================================================
            //  ✅ v6.2.12 — ⏳ MÖHLƏTLƏR və 💵 KASSA HƏRƏKƏTLƏRİ də sinxronlaşır ✓✓✓
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL BUNLAR YOX İDİ ✗ → yeni kompüterə/müştəriyə məlumat
            //  götürəndə möhlətlər və əl ilə yazılmış kassa qeydləri İTİRDİ ✗✓✓
            //  (kassa proqnozu · ödəniş qrafiki · Bildirişlər səhv olurdu ✗)
            //  ⚠ SIRA VACİBDİR ✗ — valideynlər (credits · sales) YUXARIDADIR ✓
            // ================================================================
            "odenisMohlets", "kassaHereketleri"
        };

        /// <summary>
        /// 🔑 <b>SİNXRON AÇARI</b> ✓✓✓ (v6.2.16) —
        /// <see cref="IBuludIdli.SinxronAcar"/> ✓ (GUID varsa GUID ✓, yoxsa rəqəm ID ✓)
        /// <para>⚠ İnterfeys metodu birbaşa sinif üzərindən çağırılmır ✗ → bu köməkçi vasitəsilə ✓</para>
        /// </summary>
        private static string Acar<T>(T e) where T : IBuludIdli => e.SinxronAcar;

        /// <summary>
        /// #️⃣ <b>MƏZMUN HASH-I</b> ✓✓✓ (v6.2.16) — <b>yalnız BİZNES sahələri</b> ✓
        /// <list type="bullet">
        ///   <item><c>id</c> ✗ — hər kompüterdə FƏRQLİ ola bilər ✓ (əlaqələr artıq
        ///         <c>*BuludId</c> ilə qurulur ✓) → hash-a DAXİL DEYİL ✗✓✓</item>
        ///   <item><c>buludId</c> ✗ · <c>updatedAt</c> ✗ · <c>updatedBy</c> ✗ ·
        ///         <c>isDeleted</c> ✗ · <c>deletedAt</c> ✗ — audit sahələridir ✗</item>
        /// </list>
        /// <para>
        /// ⚠ Səbəb: əvvəl hash <c>id</c>-ni də əhatə edirdi ✗ → bir kompüter qeydi
        /// fərqli yerli ID ilə saxlayanda hash FƏRQLİ çıxırdı ✗ → iki kompüter
        /// sonsuz olaraq bir-birinin üzərinə yazırdı ✗✓✓ (sonsuz dövr ✗)
        /// </para>
        /// </summary>
        private static string KokHash(Dictionary<string, object?> node)
        {
            var təmiz = new Dictionary<string, object?>(node, StringComparer.Ordinal);

            foreach (var açar in AuditSahələri)
            {
                təmiz.Remove(açar);
            }

            return Hesabla(System.Text.Json.JsonSerializer.Serialize(təmiz, FirebaseOptions.Json));
        }

        /// <summary>🚫 Audit / identifikator sahələri — hash-a DAXİL DEYİL ✗✓✓</summary>
        private static readonly string[] AuditSahələri =
        {
            "id", "buludId", "updatedAt", "updatedBy", "isDeleted", "deletedAt"
        };

        // ====================================================================
        //  🆔 KİMLİK VƏ ƏLAQƏ KÖMƏKÇİLƏRİ ✓✓✓ (v6.2.16)
        // --------------------------------------------------------------------
        //  ★ İstifadəçi tələbi: «iki kompüterdə hər şey EYNİ olmalıdır;
        //    birində yaranan qeyd digərində də olmalıdır; əvvəlki SİLİNMƏMƏLİDİR» ✓✓✓
        // ====================================================================

        /// <summary>
        /// 🔍 <b>EYNİ QEYDDİR?</b> ✓✓✓ — «buludId» ilə KİMLİK müqayisəsi ✓
        /// <list type="bullet">
        ///   <item>buluddakı qeydin GUID-i YOXDUR ✗ (köhnə ✓) → yerli də köhnədirsə eyni ✓</item>
        ///   <item>GUID-lər FƏRQLİDİR ✗ → <b>AYRI qeydlərdir</b> ✗✓✓ (bax <see cref="TutaniAzadEtAsync"/>)</item>
        /// </list>
        /// </summary>
        private static bool EyniQeyddir(string? uzaqBuludId, IBuludIdli yerli)
            => string.IsNullOrWhiteSpace(uzaqBuludId)
                ? string.IsNullOrWhiteSpace(yerli.BuludId)
                : string.Equals(uzaqBuludId, yerli.BuludId, StringComparison.Ordinal);

        /// <summary>
        /// 🔓 <b>TOQQUŞMANI TƏMİRLƏYİR</b> ✓✓✓ (v6.2.16 — ★ MƏLUMAT İTMİR ★)
        /// <para>
        /// İki kompüter eyni rəqəm ID-ni («232») ayırmışdı ✗ → yerli qeyd RE-KEY
        /// olunur ✓ (GUID alır ✓) → artıq AYRI açarla yaşayır ✓ · <b>HEÇ NƏ
        /// SİLİNMİR</b> ✗✓✓ · yerli ID-si və bütün uşaqları TOXUNULMAZ qalır ✓✓✓
        /// </para>
        /// <para>
        /// ⚠ Buluddakı köhnə rəqəm node-u SİLİNMİR ✗ (digər kompüterin orada öz
        /// qeydi ola bilər ✗) — yalnız yerli <b>izləmə</b> sətri buraxılır ✓ →
        /// həmin node-u artıq biz «sahiblənmirik» ✗ → digər kompüter onu təmiz
        /// şəkildə yazır ✓✓✓
        /// </para>
        /// </summary>
        private async Task TutaniAzadEtAsync(AppDbContext db, SqliteConnection cx, string kol,
            IBuludIdli yerli, CancellationToken ct)
        {
            var köhnəAçar = yerli.SinxronAcar;   // ⚠ ƏVVƏLCƏ yadda saxla ✗✓✓

            try
            {
                // ① GUID verilir ✓ → qeyd artıq AYRI açarla yaşayır ✓
                if (string.IsNullOrWhiteSpace(yerli.BuludId))
                {
                    yerli.BuludId = Guid.NewGuid().ToString("N");
                }

                var yeniAçar = yerli.SinxronAcar;

                // ② Yerli bazada BİRBAŞA SQL ilə yazılır ✓
                //   (EF izləyicisi detached-dir ✗ · PK dəyişmir ✗ ✓)
                var cədvəl = db.Model.FindEntityType(yerli.GetType())?.GetTableName();

                if (cədvəl is not null)
                {
                    using var əmr = ((SqliteConnection)db.Database.GetDbConnection()).CreateCommand();
                    əmr.CommandText = $"UPDATE \"{cədvəl}\" SET \"BuludId\" = $g WHERE \"Id\" = $id;";
                    əmr.Parameters.AddWithValue("$g", yerli.BuludId!);
                    əmr.Parameters.AddWithValue("$id", yerli.Id);

                    await əmr.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                // ③ Yerli «iz» buraxılır ✓ (köhnə açar artıq bizim deyil ✗)
                _sonHash.Remove(kol + "/" + köhnəAçar);
                _sonAçarlar[kol]?.Remove(köhnəAçar);
                IzlemeSil(cx, kol, köhnəAçar);

                var qısa = yeniAçar[..Math.Min(8, yeniAçar.Length)];

                AppLogger.Xeberdarliq(
                    $"🔓 {kol}: «{köhnəAçar}» açarı tutulmuşdu ✓ → " +
                    $"«{qısa}…» GUID açarına keçirildi ✓ (qeyd SİLİNMƏDİ ✗✓✓)");
            }
            catch (Exception ex)
            {
                AppLogger.Xeta(ex, "toqquşma təmiri: " + kol);
            }
        }

        /// <summary>🔗 <c>*BuludId</c> sahələrini yerli ID-lərə çevirir ✓✓✓ (v6.2.16)</summary>
        private void ƏlaqələriBağla<T>(T yeni, JsonObject o) where T : IBuludIdli
        {
            int? Tap(Dictionary<string, int> xəritə, string sahə, int? cari)
            {
                var mətn = o[sahə]?.ToString();

                // Köhnə qeydlərdə bu sahə YOXDUR ✗ → rəqəm ID olduğu kimi qalır ✓
                if (string.IsNullOrWhiteSpace(mətn))
                {
                    return cari;
                }

                if (xəritə.TryGetValue(mətn, out var yerliId))
                {
                    return yerliId;
                }

                // ⚠ Valideyn hələ yerli bazada yoxdur ✗ → uydurma ID verilmir ✗
                AppLogger.Xeberdarliq(
                    $"🔗 Əlaqə tapılmadı ✗ — {sahə}={mətn[..Math.Min(8, mətn.Length)]}…");

                return null;
            }

            switch (yeni)
            {
                case ExpenseItem e:
                    e.CarId = Tap(_carKök, "carBuludId", e.CarId);
                    break;

                case Sale s:
                    s.CarId = Tap(_carKök, "carBuludId", s.CarId);
                    break;

                case Credit k:
                    k.CarId = Tap(_carKök, "carBuludId", k.CarId);
                    break;

                case CreditTransaction t:
                    t.CreditId = Tap(_kreditKök, "kreditBuludId", t.CreditId);
                    break;

                case PartnerShare p:
                    p.CreditTransactionId =
                        Tap(_emeliyyatKök, "kreditEmeliyyatBuludId", p.CreditTransactionId);
                    p.CreditId = Tap(_kreditKök, "kreditBuludId", p.CreditId);
                    p.SaleId = Tap(_satisKök, "satisBuludId", p.SaleId);
                    break;

                case OdenisMohlet m:
                    m.CreditId = Tap(_kreditKök, "kreditBuludId", m.CreditId);
                    m.SaleId = Tap(_satisKök, "satisBuludId", m.SaleId);
                    break;

                case KassaHereket k:
                    k.CreditId = Tap(_kreditKök, "kreditBuludId", k.CreditId);
                    k.SaleId = Tap(_satisKök, "satisBuludId", k.SaleId);
                    break;
            }
        }

        /// <summary>🔗 Yeni əlavə olunan qeydi KÖK xəritələrinə əlavə edir ✓ (uşaqlar üçün ✓)</summary>
        private void KökXəritələrəƏlavəEt<T>(T yeni) where T : IBuludIdli
        {
            if (string.IsNullOrWhiteSpace(yeni.BuludId))
            {
                return;   // köhnə qeyd ✗ → xəritədə yer almır ✓ (rəqəm ID işlədilir ✓)
            }

            switch (yeni)
            {
                case CarItem c: _carKök[c.BuludId!] = c.Id; break;
                case Credit k: _kreditKök[k.BuludId!] = k.Id; break;
                case Sale s: _satisKök[s.BuludId!] = s.Id; break;
                case CreditTransaction t: _emeliyyatKök[t.BuludId!] = t.Id; break;
            }
        }

        /// <summary>🔗 KÖK xəritələrini yerli bazadan doldurur ✓✓✓ (v6.2.16)</summary>
        private async Task XəritələriDoldurAsync(AppDbContext db, CancellationToken ct)
        {
            static Dictionary<string, int> Çevir(IEnumerable<(string? Kok, int Id)> sətirlər)
            {
                var nəticə = new Dictionary<string, int>(StringComparer.Ordinal);

                foreach (var (kok, id) in sətirlər)
                {
                    if (!string.IsNullOrWhiteSpace(kok))
                    {
                        nəticə[kok!] = id;
                    }
                }

                return nəticə;
            }

            var cars = await db.Cars.AsNoTracking()
                .Select(c => new { c.BuludId, c.Id }).ToListAsync(ct).ConfigureAwait(false);

            var kredits = await db.Credits.AsNoTracking()
                .Select(k => new { k.BuludId, k.Id }).ToListAsync(ct).ConfigureAwait(false);

            var sales = await db.Sales.AsNoTracking()
                .Select(s => new { s.BuludId, s.Id }).ToListAsync(ct).ConfigureAwait(false);

            var trxs = await db.CreditTransactions.AsNoTracking()
                .Select(t => new { t.BuludId, t.Id }).ToListAsync(ct).ConfigureAwait(false);

            _carKök = Çevir(cars.Select(x => (x.BuludId, x.Id)));
            _kreditKök = Çevir(kredits.Select(x => (x.BuludId, x.Id)));
            _satisKök = Çevir(sales.Select(x => (x.BuludId, x.Id)));
            _emeliyyatKök = Çevir(trxs.Select(x => (x.BuludId, x.Id)));

            _carKokId = TersÇevir(_carKök);
            _kreditKokId = TersÇevir(_kreditKök);
            _satisKokId = TersÇevir(_satisKök);
            _emeliyyatKokId = TersÇevir(_emeliyyatKök);
        }

        /// <summary>🔁 BuludId → yerli ID xəritəsini tərsinə çevirir ✓</summary>
        private static Dictionary<int, string> TersÇevir(Dictionary<string, int> xəritə)
        {
            var nəticə = new Dictionary<int, string>();

            foreach (var (kok, id) in xəritə)
            {
                nəticə[id] = kok;
            }

            return nəticə;
        }

        /// <summary>🔑 Yerli ID → BuludId ✓ (göndərmə zamanı əlaqə sahələri üçün ✓)</summary>
        private static string? KokAcar(Dictionary<int, string> xəritə, int? id)
            => id is int i && xəritə.TryGetValue(i, out var kok) ? kok : null;

        // ---- 🔗 Əlaqə xəritələri (hər dövrdə yenilənir ✓) ----
        private Dictionary<string, int> _carKök = new(StringComparer.Ordinal);
        private Dictionary<string, int> _kreditKök = new(StringComparer.Ordinal);
        private Dictionary<string, int> _satisKök = new(StringComparer.Ordinal);
        private Dictionary<string, int> _emeliyyatKök = new(StringComparer.Ordinal);

        private Dictionary<int, string> _carKokId = new();
        private Dictionary<int, string> _kreditKokId = new();
        private Dictionary<int, string> _satisKokId = new();
        private Dictionary<int, string> _emeliyyatKokId = new();

        /// <summary>🚗 <b>Avtomobil</b> → Firebase node ✓ (sənəd faylları DAXİL DEYİL ✗ ✓)</summary>
        private static Dictionary<string, object?> CarNode(CarItem a) => new()
        {
            ["id"] = a.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = a.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["marka"] = a.Marka,
            ["qeydiyyatNisani"] = a.QeydiyyatNisani,
            ["vin"] = a.Vin,
            ["il"] = a.Il,
            ["yurus"] = a.Yurus,
            ["yanacaq"] = a.Yanacaq,
            ["alisTarixi"] = a.AlisTarixi?.ToString("o", CultureInfo.InvariantCulture),
            ["alisSaati"] = a.AlisSaati,
            ["alisUsulu"] = a.AlisUsulu,
            ["alisQiymeti"] = (double)a.AlisQiymeti,
            ["satisQiymeti"] = (double)a.SatisQiymeti,
            ["status"] = a.Status,
            ["kreditNomresi"] = a.KreditNomresi,
            ["barterTesviri"] = a.BarterTesviri,
            ["barterDeyeri"] = (double)a.BarterDeyeri,
            ["barterCarId"] = a.BarterCarId?.ToString(CultureInfo.InvariantCulture),
            ["barterSaleId"] = a.BarterSaleId?.ToString(CultureInfo.InvariantCulture),
            ["isBarter"] = a.IsBarter,
            // ================================================================
            //  🔢 SIRA NÖMRƏSİ BULUDA YAZILIR ✓✓✓ (v6.2.15)
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL BU SAHƏ YOX İDİ ✗ → PC2 maşını buluddan götürəndə
            //     `SiraNomresi = 0` qalırdı ✗ → `SiraNomreleriniDuzeltAsync`
            //     ona «ən kiçik boş nömrə»ni verirdi ✗
            //     → PC1-də «308», PC2-də «188» görünürdü ✗✓✓ (istifadəçi şikayəti ✓)
            //  ✅ İNDİ: istifadəçinin ÖZ əl ilə verdiyi nömrə (1 · 17 · 19 · 34 ✓)
            //     buluda yazılır ✓ → hər iki kompüterdə EYNİ nömrə görünür ✓✓✓
            // ================================================================
            ["siraNomresi"] = a.SiraNomresi,
            ["yaradilmaTarixi"] = a.YaradilmaTarixi.ToString("o", CultureInfo.InvariantCulture)
        };

        /// <summary>💸 <b>Xərc</b> → Firebase node ✓</summary>
        private Dictionary<string, object?> XercNode(ExpenseItem e) => new()
        {
            ["id"] = e.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = e.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["tarix"] = e.Tarix.ToString("o", CultureInfo.InvariantCulture),
            ["teyinat"] = e.Teyinat,
            ["qrup"] = e.Qrup,
            ["kategoriya"] = e.Kategoriya,
            ["carId"] = e.CarId?.ToString(CultureInfo.InvariantCulture),
            ["carBuludId"] = KokAcar(_carKokId, e.CarId),
            ["mebleg"] = (double)e.Mebleg,
            ["odenisUsulu"] = e.OdenisUsulu,
            ["qeyd"] = e.Qeyd,
            ["yaradilmaTarixi"] = e.YaradilmaTarixi.ToString("o", CultureInfo.InvariantCulture)
        };

        /// <summary>🤝 <b>Satış</b> → Firebase node ✓</summary>
        private Dictionary<string, object?> SatisNode(Sale s) => new()
        {
            ["id"] = s.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = s.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["muqavileNomresi"] = s.MuqavileNomresi,
            ["musteri"] = s.Mustəri,
            ["carId"] = s.CarId?.ToString(CultureInfo.InvariantCulture),
            ["carBuludId"] = KokAcar(_carKokId, s.CarId),
            ["satisQiymeti"] = (double)s.SatisQiymeti,
            ["mayaDeyeri"] = (double)s.MayaDeyeri,
            ["menfeet"] = (double)s.Menfeet,
            ["satisTarixi"] = s.SatisTarixi.ToString("o", CultureInfo.InvariantCulture),
            ["odenisUsulu"] = s.OdenisUsulu,
            ["barterMebleg"] = (double)s.BarterMebleg,
            ["nagdMebleg"] = (double)s.NagdMebleg,
            ["barterTesviri"] = s.BarterTesviri,
            ["isBarter"] = s.IsBarter,
            ["qeyd"] = s.Qeyd
        };

        /// <summary>🏦 <b>Kredit</b> → Firebase node ✓</summary>
        private Dictionary<string, object?> KreditNode(Credit k) => new()
        {
            ["id"] = k.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = k.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["muqavileNomresi"] = k.MuqavileNomresi,
            ["musteri"] = k.Mustəri,
            ["carId"] = k.CarId?.ToString(CultureInfo.InvariantCulture),
            ["carBuludId"] = KokAcar(_carKokId, k.CarId),
            ["mebleg"] = (double)k.Mebleg,
            ["ilkinOdenis"] = (double)k.IlkinOdenis,
            ["kreditlesdirilen"] = (double)k.Kreditlesdirilen,
            ["faizDerecesi"] = (double)k.FaizDerecesi,
            ["faizMeblegi"] = (double)k.FaizMeblegi,
            ["kreditQiymeti"] = (double)k.KreditQiymeti,
            ["ayligOdenis"] = (double)k.AylıqOdenis,
            ["muddetAy"] = k.MuddetAy,
            ["baslamaTarixi"] = k.BaslamaTarixi.ToString("o", CultureInfo.InvariantCulture),
            ["status"] = k.Status,
            ["bitmisKredit"] = k.BitmisKredit,
            ["qeyd"] = k.Qeyd
        };

        /// <summary>💳 <b>Kredit əməliyyatı</b> → Firebase node ✓</summary>
        private Dictionary<string, object?> EmeliyyatNode(CreditTransaction t) => new()
        {
            ["id"] = t.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = t.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["creditId"] = t.CreditId?.ToString(CultureInfo.InvariantCulture),
            ["kreditBuludId"] = KokAcar(_kreditKokId, t.CreditId),
            ["nov"] = t.Nov,
            ["installmentNo"] = t.InstallmentNo,
            ["mebleg"] = (double)t.Mebleg,
            ["tarix"] = t.Tarix.ToString("o", CultureInfo.InvariantCulture),
            ["gosterilenTarix"] = t.GosterilenTarix.ToString("o", CultureInfo.InvariantCulture),
            ["mohletTarixi"] = t.MohletTarixi?.ToString("o", CultureInfo.InvariantCulture),
            ["gecikmeTarixi"] = t.GecikmeTarixi?.ToString("o", CultureInfo.InvariantCulture),
            ["tesvir"] = t.Tesvir,
            ["odenilib"] = t.Odenilib,
            ["bolguTetbiqOlunub"] = t.TerefdasBolguTetbiqOlunub,
            ["bolguBazasi"] = t.BolguBazasi.HasValue ? (double)t.BolguBazasi.Value : null
        };

        /// <summary>👥 <b>Tərəfdaş</b> → Firebase node ✓</summary>
        private static Dictionary<string, object?> TerefdasNode(Partner p) => new()
        {
            ["id"] = p.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = p.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["ad"] = p.Ad,
            ["faiz"] = (double)p.Faiz,
            ["qaligPayi"] = p.QaligPayi,
            ["aktiv"] = p.Aktiv,
            ["sira"] = p.Sira,
            ["qeyd"] = p.Qeyd
        };

        /// <summary>💰 <b>Tərəfdaş payı</b> → Firebase node ✓</summary>
        private Dictionary<string, object?> PayNode(PartnerShare p) => new()
        {
            ["id"] = p.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = p.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["creditTransactionId"] = p.CreditTransactionId?.ToString(CultureInfo.InvariantCulture),
            ["kreditEmeliyyatBuludId"] = KokAcar(_emeliyyatKokId, p.CreditTransactionId),
            ["creditId"] = p.CreditId?.ToString(CultureInfo.InvariantCulture),
            ["kreditBuludId"] = KokAcar(_kreditKokId, p.CreditId),
            ["saleId"] = p.SaleId?.ToString(CultureInfo.InvariantCulture),
            ["satisBuludId"] = KokAcar(_satisKokId, p.SaleId),
            ["terefdas"] = p.Terefdas,
            ["faiz"] = (double)p.Faiz,
            ["mebleg"] = (double)p.Mebleg,
            ["qaligPayi"] = p.QaligPayi,
            ["aktiv"] = p.Aktiv,
            ["sira"] = p.Sira
        };

        /// <summary>
        /// ⏳ <b>MÖHLƏT ödənişi</b> → Firebase node ✓✓✓  (v6.2.12)
        /// <para>İlkin ödəniş möhləti (kredit ✓) və nisyə satış möhləti (satış ✓).</para>
        /// </summary>
        private Dictionary<string, object?> MohletNode(OdenisMohlet m) => new()
        {
            ["id"] = m.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = m.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["menbe"] = m.Menbe,
            ["creditId"] = m.CreditId?.ToString(CultureInfo.InvariantCulture),
            ["kreditBuludId"] = KokAcar(_kreditKokId, m.CreditId),
            ["saleId"] = m.SaleId?.ToString(CultureInfo.InvariantCulture),
            ["satisBuludId"] = KokAcar(_satisKokId, m.SaleId),
            ["sira"] = m.Sira,
            ["tarix"] = m.Tarix.ToString("o", CultureInfo.InvariantCulture),
            ["mebleg"] = (double)m.Mebleg,
            ["odenisUsulu"] = m.OdenisUsulu,
            ["odenilib"] = m.Odenilib,
            ["odenilmeTarixi"] = m.OdenilmeTarixi?.ToString("o", CultureInfo.InvariantCulture),
            ["qeyd"] = m.Qeyd
        };

        /// <summary>
        /// 💵 <b>Kassa hərəkəti</b> (əl ilə gəlir/xərc) → Firebase node ✓✓✓  (v6.2.12)
        /// </summary>
        private Dictionary<string, object?> KassaNode(KassaHereket k) => new()
        {
            ["id"] = k.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = k.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["nov"] = k.Nov,
            ["tarix"] = k.Tarix.ToString("o", CultureInfo.InvariantCulture),
            ["kateqoriya"] = k.Kateqoriya,
            ["mebleg"] = (double)k.Mebleg,
            ["odenisUsulu"] = k.OdenisUsulu,
            ["creditId"] = k.CreditId?.ToString(CultureInfo.InvariantCulture),
            ["kreditBuludId"] = KokAcar(_kreditKokId, k.CreditId),
            ["saleId"] = k.SaleId?.ToString(CultureInfo.InvariantCulture),
            ["satisBuludId"] = KokAcar(_satisKokId, k.SaleId),
            ["qeyd"] = k.Qeyd
        };

        /// <summary>💵 <b>Tərəfdaş ödənişi</b> → Firebase node ✓</summary>
        private static Dictionary<string, object?> OdenisNode(PartnerPayment p) => new()
        {
            ["id"] = p.Id.ToString(CultureInfo.InvariantCulture),
            ["buludId"] = p.BuludId,   // 🔑 v6.2.16 → kimlik ✓ (toqquşma aşkarlaması ✓)
            ["terefdas"] = p.Terefdas,
            ["mebleg"] = (double)p.Mebleg,
            ["tarix"] = p.Tarix.ToString("o", CultureInfo.InvariantCulture),
            ["odenisUsulu"] = p.OdenisUsulu,
            ["qeyd"] = p.Qeyd
        };





    }
}
