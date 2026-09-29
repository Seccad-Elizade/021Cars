using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseAeroStudio.Data.Repositories;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class ScriptImportService : IScriptImportService
    {
        private readonly ICarService _cars;
        private readonly IExpenseService _xercler;
        private readonly IExpenseCatalogService _kataloq;
        private readonly ILogger<ScriptImportService> _logger;

        /// <summary>JSON oxuma ayarları (dözümlü ✓ — şərh və sonda vergülə icazə ✓).</summary>
        private static readonly JsonSerializerOptions JsonSecimleri = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        /// <summary>Dəstəklənən tarix formatları (məs. «09.07.2023» ✓).</summary>
        private static readonly string[] TarixFormatlari =
        {
            "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
            "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd"
        };

        /// <summary>Avtomobil xərci təyinatı (bütün idxal olunan xərclər üçün ✓).</summary>
        private const string Teyinat = Catalog.CarDestination;

        /// <summary>Standart qrup (heç bir qaydaya düşməyən hissələr üçün ✓).</summary>
        private const string StandartQrup = "⚙️ Mühərrik, Slesar & Ehtiyat Hissələri";

        private const string UstaQrupu = "👤 Usta & İşçilik Haqqı";
        private const string KuzovQrupu = "🛠️ Kuzov, Dəmirçi & Malyar";
        private const string YanacaqQrupu = "⛽ Yanacaq, Yuma & Nəqliyyat";
        private const string SenedQrupu = "📄 Sənədləşmə & DYP Xərcləri";
        private const string AlisQrupu = "💰 Alış & Maya Xərcləri";

        /// <summary>Bir uyğunlaşdırma qaydası: açar sözlər → qrup + (varsa) kateqoriya ✓.</summary>
        private sealed record Qayda(string[] Acarlar, string Qrup, string? Kategoriya);

        /// <summary>
        /// Ehtiyat hissəsi sözləri — <b>tək sözlü</b> təsvirin «şəxs adı» kimi
        /// səhv başa düşülməsinin qarşısını alır ✓ (məs. «Blok» · «Rele» ✓).
        /// </summary>
        private static readonly HashSet<string> HissaSozleri = new(StringComparer.Ordinal)
        {
            "blok", "rele", "datchik", "datcik", "sensor", "abs", "monitor", "natijitel",
            "termostat", "radiator", "akkumulyator", "akumulyator", "zaslonka", "kriska",
            "praklatka", "patrupka", "fartuk", "paduska", "silindr", "kamera", "teker",
            "dinamo", "sidenik", "motor", "pompa", "nasos", "generator", "starter",
            "hidrousilitel", "rulavoy", "naklatka", "tormoz", "amartizator", "prujina",
            "kardan", "reduktor", "salnik", "prokladka", "rolik", "podshipnik", "kolodka",
            "disk", "sterjen", "kusak", "zamok", "qapi", "steklo", "susge", "pult",
            "klapan", "forsunka", "karburator", "benzonasos", "balon", "masla",
            "antifriz", "konditsioner", "stabilizator", "shrus", "srus", "krestovina",
            "kulis", "bak", "fara", "yag"
        };

        /// <summary>
        /// Açar söz → qrup / kateqoriya cədvəli.
        /// <para><c>Kategoriya = null</c> → kateqoriya <b>skriptdəki adı ilə</b> yazılır ✓</para>
        /// <para>⚠ Sıra VACİBDİR ✗ — yuxarıdaki qayda aşağıdakından əvvəl yoxlanılır ✓</para>
        /// </summary>
        private static readonly Qayda[] Qaydalar =
        {
            // ── 📄 Sənədləşmə & DYP ───────────────────────────────────────────
            new(new[] { "qeydiyyat", "registr" }, SenedQrupu, "Qeydiyyat"),
            new(new[] { "cerime", "cerim" }, SenedQrupu, "Cərimə"),
            new(new[] { "sigorta" }, SenedQrupu, "Sığorta"),
            new(new[] { "texniki baxis", "baxis" }, SenedQrupu, "Texniki baxış"),
            new(new[] { "etibarname" }, SenedQrupu, "Etibarnamə"),
            new(new[] { "dyp", "rusum", "notarius" }, SenedQrupu, null),

            // ── ⛽ Yanacaq, yuma & nəqliyyat ─────────────────────────────────
            new(new[] { "benzin", "dizel", "yanacaq" }, YanacaqQrupu, "Benzin"),
            new(new[] { "moyka", "yuma" }, YanacaqQrupu, "Moyka"),
            new(new[] { "taksi" }, YanacaqQrupu, "Taksi"),
            new(new[] { "yol", "neqliyyat", "yuk", "reyd" }, YanacaqQrupu, null),

            // ── 👤 Usta & işçilik haqqı ──────────────────────────────────────
            new(new[] { "usta", "iscilik", "slesar", "motorist", "elektrik", "dizelci" },
                UstaQrupu, "Usta haqqı (Digər)"),

            // ── 🛠️ Kuzov, dəmirçi & malyar ───────────────────────────────────
            new(new[] { "demirci" }, KuzovQrupu, "Dəmirçi"),
            new(new[] { "malyar", "boya", "reng", "polirovka", "vinil", "kuzov", "kapot" },
                KuzovQrupu, "Malyar"),
            new(new[] { "fara", "fanar" }, KuzovQrupu, "Fara təmiri"),
            new(new[] { "rol cexol", "rol uzleme", "rol " }, KuzovQrupu, "Rol üzləmə"),

            // ── ⚙️ Ehtiyat hissələri ────────────────────────────────────────
            new(new[] { "duman", "isiq", "lampa", "stop siqnali" }, StandartQrup, "Lampa"),
            new(new[] { "muherrik", "motor temiri" }, StandartQrup, "Mühərrik təmiri"),
            new(new[] { "yag deyisme", "yag filter" }, StandartQrup, "Yağ dəyişmə"),
            new(new[] { "diaqnostika", "yoxlanis" }, StandartQrup, "Diaqnostika"),

            // ── 💰 Alış & maya ───────────────────────────────────────────────
            new(new[] { "baglanma", "masin baglanma" }, AlisQrupu, "Maşının bağlanma dəyəri"),
            new(new[] { "sahibine" }, AlisQrupu, "Maşın sahibinə verildi"),
            new(new[] { "beh" }, AlisQrupu, "Beh"),
            new(new[] { "calag" }, AlisQrupu, "Calağ"),
            new(new[] { "qaliq odenis" }, AlisQrupu, "Qalıq ödəniş")
        };

        /// <summary>Bir uyğunlaşdırma nəticəsi (qrup · kateqoriya · izah ✓).</summary>
        private sealed record UygunNetice(
            string Qrup,
            string Kategoriya,
            bool YeniQrup,
            bool YeniKategoriya,
            bool Uygunlasdirildi,
            string Izah);

        public ScriptImportService(
            ICarService cars,
            IExpenseService xercler,
            IExpenseCatalogService kataloq,
            ILogger<ScriptImportService> logger)
        {
            _cars = cars;
            _xercler = xercler;
            _kataloq = kataloq;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task<ScriptImportSonuc> YoxlaAsync(string json, CancellationToken cancellationToken = default)
            => IcrayaAsync(json, yaz: false, cancellationToken);

        /// <inheritdoc />
        public Task<ScriptImportSonuc> IcraEtAsync(string json, CancellationToken cancellationToken = default)
            => IcrayaAsync(json, yaz: true, cancellationToken);

        // ====================================================================
        //  📜 ƏSAS İDXAL MƏNTİQİ
        // --------------------------------------------------------------------
        //  «Yoxla» və «İcra et» EYNİ məntiqi işlədir ✓ — fərq yalnız `yaz`
        //  bayrağıdır ✓: yoxlama rejimində bazaya HEÇ NƏ yazılmır ✗✓✓
        // ====================================================================

        private async Task<ScriptImportSonuc> IcrayaAsync(
            string json,
            bool yaz,
            CancellationToken cancellationToken)
        {
            var sonuc = new ScriptImportSonuc { YalnizYoxlama = !yaz };

            if (string.IsNullOrWhiteSpace(json))
            {
                sonuc.Ugursuz("❌ Skript boşdur — JSON mətnini yapışdırın ✓");
                return sonuc;
            }

            List<ScriptCar>? siyahi;

            try
            {
                siyahi = JsonSerializer.Deserialize<List<ScriptCar>>(json.Trim(), JsonSecimleri);
            }
            catch (JsonException ex)
            {
                sonuc.Ugursuz("❌ JSON oxuna bilmədi (sözdizimi xətası ✓): " + ex.Message);
                return sonuc;
            }

            if (siyahi is null || siyahi.Count == 0)
            {
                sonuc.Ugursuz("❌ Skriptdə avtomobil yoxdur — format belə olmalıdır: " +
                              "[ { \"MarkaModel\": \"…\", \"Xercler\": [ … ] } ] ✓");
                return sonuc;
            }

            // Kateqoriya bazası BİR DƏFƏ oxunur ✓ (sürət ✓)
            var (kataloq, movcudQruplar) = await KataloquOkuAsync(cancellationToken);

            var yeniKategoriyalar = new HashSet<string>(StringComparer.Ordinal);
            var yeniQruplar = new HashSet<string>(StringComparer.Ordinal);
            var islenenNomreler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var qeydGozleyenler = new List<GozleyenQeyd>();

            sonuc.Yaz(yaz
                ? "📥 SKRİPT İDXALI — məlumatlar bazaya YAZILIR ✓"
                : "🔎 SKRİPT YOXLAMASI — heç nə yazılmır ✗ (yalnız nə olacağı göstərilir ✓)");
            sonuc.Yaz($"📅 {DateTime.Now:dd.MM.yyyy HH:mm} · 📜 skriptdə {siyahi.Count} avtomobil ✓");
            sonuc.Yaz(new string('─', 66));

            var sira = 0;

            foreach (var skript in siyahi)
            {
                sira++;
                sonuc.SkriptMasinSayi++;

                var netice = new ScriptMasinNeticesi
                {
                    Sira = sira,
                    Marka = skript.MarkaMetni,
                    Nomre = skript.NomreMetni,
                    Il = skript.Il ?? 0,
                    AlisQiymeti = skript.AlisQiymeti ?? 0m,
                    AlisTarixi = TarixOku(skript.AlisTarixi),
                    Qeyd = skript.QeydMetni
                };

                // ① Marka / model mütləqdir ✓
                if (netice.Marka.Length == 0)
                {
                    netice.Xeberdarliqlar.Add("❌ Marka/model boşdur — bu avtomobil keçildi ✗");
                    sonuc.XetaliMasin++;
                    sonuc.Masinlar.Add(netice);
                    continue;
                }

                if (skript.Il is null or <= 0)
                {
                    netice.Xeberdarliqlar.Add("⚠️ Buraxılış ili göstərilməyib (0 kimi yazılır).");
                }

                if (!string.IsNullOrWhiteSpace(skript.AlisTarixi) && netice.AlisTarixi is null)
                {
                    netice.Xeberdarliqlar.Add($"⚠️ Alış tarixi oxunmadı: «{skript.AlisTarixi}» → bu gün götürüldü.");
                }

                var alisTarixi = netice.AlisTarixi ?? DateTime.Today;

                // ② Təkrarlar: skript daxilində və artıq bazada olanlar ✓
                if (netice.Nomre.Length == 0)
                {
                    netice.Xeberdarliqlar.Add("⚠️ Dövlət nömrəsi boşdur.");
                }
                else if (!islenenNomreler.Add(MetinUygunlasdirici.Normallasdir(netice.Nomre)))
                {
                    netice.Xeberdarliqlar.Add($"⏭️ «{netice.Nomre}» skriptdə TƏKRARLANIR — ikinci dəfə yazılmadı ✗");
                    sonuc.KecirilenMasin++;
                    sonuc.Masinlar.Add(netice);
                    continue;
                }
                else if (await _cars.RegistrationExistsAsync(netice.Nomre, null, cancellationToken))
                {
                    netice.Xeberdarliqlar.Add($"⏭️ «{netice.Nomre}» ARTIQ proqramdadır — keçildi ✗ (köhnə məlumat DƏYİŞMİR ✓)");
                    sonuc.KecirilenMasin++;
                    sonuc.Masinlar.Add(netice);
                    continue;
                }

                await MasiniYazAsync(sonuc, netice, skript, alisTarixi, kataloq, movcudQruplar,
                                     yeniQruplar, yeniKategoriyalar, qeydGozleyenler, yaz, cancellationToken);

                // 💰 YEKUN CƏMLƏR (yalnız yazılan / yazılacaq avtomobillər ✓)
                sonuc.UmumiAlis += netice.AlisQiymeti;
                sonuc.UmumiXerc += netice.XercCemi;

                MasinHesabatiYaz(sonuc, netice);
            }

            await QeydGozleyenleriYazAsync(sonuc, qeydGozleyenler, yaz, cancellationToken);

            YekunYaz(sonuc);
            return sonuc;
        }

        /// <summary>Bir avtomobili və onun bütün xərclərini yazır (və ya yoxlayır ✓).</summary>
        private async Task MasiniYazAsync(
            ScriptImportSonuc sonuc,
            ScriptMasinNeticesi netice,
            ScriptCar skript,
            DateTime alisTarixi,
            List<KataloqSetiri> kataloq,
            HashSet<string> movcudQruplar,
            HashSet<string> yeniQruplar,
            HashSet<string> yeniKategoriyalar,
            List<GozleyenQeyd> qeydGozleyenler,
            bool yaz,
            CancellationToken cancellationToken)
        {
            var carId = 0;

            if (yaz)
            {
                var avtomobil = new CarItem
                {
                    Marka = netice.Marka,
                    QeydiyyatNisani = netice.Nomre,
                    Il = netice.Il,
                    AlisTarixi = alisTarixi,
                    AlisQiymeti = netice.AlisQiymeti,
                    AlisUsulu = Catalog.CashPurchase,
                    Status = Catalog.StockStatus,
                    SiraNomresi = skript.SiraNomresi ?? 0
                };

                // ⚠ AddCarAsync ÖZÜ: ① ardıcıl sıra nömrəsi verir ✓
                //                    ② alış qiyməti > 0 olduqda «Alış» xərcini yaradır ✓
                avtomobil = await _cars.AddCarAsync(avtomobil, cancellationToken);

                carId = avtomobil.Id;
                netice.SiraNomresi = avtomobil.SiraNomresi;
                netice.ElaveOlundu = true;
            }

            sonuc.ElaveOlunanMasin++;

            var xercler = skript.Xercler ?? new List<ScriptXerc>();
            var sonuncuIndeks = SonuncuXercIndeksi(xercler);

            for (var i = 0; i < xercler.Count; i++)
            {
                var xerc = xercler[i];
                var tasvir = xerc.TasvirMetni;
                var mebleg = xerc.MeblegDeyeri;

                if (tasvir.Length == 0 && mebleg == 0m)
                {
                    continue;   // tamamilə boş sətir ✓
                }

                if (tasvir.Length == 0)
                {
                    tasvir = "Xərc";
                }

                var tarix = TarixOku(xerc.Tarix);

                if (!string.IsNullOrWhiteSpace(xerc.Tarix) && tarix is null)
                {
                    netice.Xeberdarliqlar.Add($"⚠️ Xərc tarixi oxunmadı: «{xerc.Tarix}» → {alisTarixi:dd.MM.yyyy} götürüldü");
                }

                tarix ??= alisTarixi;

                var uygun = Uygunlasdir(tasvir, kataloq, movcudQruplar, yeniQruplar, yeniKategoriyalar);

                // 📝 QEYD YALNIZ «ƏN AXIRINCI» XƏRCƏ yazılır ✓✓✓
                var qeyd = i == sonuncuIndeks && netice.Qeyd.Length > 0 ? netice.Qeyd : string.Empty;

                if (yaz)
                {
                    if (uygun.YeniQrup)
                    {
                        await _kataloq.AddGroupAsync(Teyinat, uygun.Qrup, cancellationToken);
                    }

                    if (uygun.YeniKategoriya)
                    {
                        await _kataloq.AddCategoryAsync(Teyinat, uygun.Qrup, uygun.Kategoriya, cancellationToken);
                    }

                    await _xercler.AddExpenseAsync(new ExpenseItem
                    {
                        Tarix = tarix.Value,
                        Teyinat = Teyinat,
                        Qrup = uygun.Qrup,
                        Kategoriya = uygun.Kategoriya,
                        CarId = carId,
                        Mebleg = mebleg,
                        OdenisUsulu = Catalog.PaymentMethods[0],
                        Qeyd = qeyd
                    }, cancellationToken);
                }

                netice.XercSayi++;
                netice.XercCemi += mebleg;
                netice.Kateqoriyalar.Add($"{tasvir} → «{uygun.Kategoriya}» · {uygun.Izah}");

                if (qeyd.Length > 0)
                {
                    netice.QeydYeri = tasvir;
                }

                sonuc.ElaveOlunanXerc++;

                if (uygun.YeniKategoriya)
                {
                    sonuc.YeniKateqoriya++;
                }
                else if (uygun.Uygunlasdirildi)
                {
                    sonuc.UygunlasdirilanKateqoriya++;
                }

                if (uygun.YeniQrup)
                {
                    sonuc.YeniQrup++;
                }
            }

            // Xərc sətri olmayan avtomobilin qeydi → avtomatik «Alış» xərcinə ✓
            if (netice.Qeyd.Length > 0 && netice.XercSayi == 0 && yaz && carId > 0)
            {
                qeydGozleyenler.Add(new GozleyenQeyd(carId, netice));
                netice.Xeberdarliqlar.Add("📝 Qeyd avtomatik yaradılan «Alış» xərcinə yazılacaq ✓");
            }
        }

        /// <summary>Avtomobil üzrə hesabat sətrini gündəliyə yazır ✓.</summary>
        private static void MasinHesabatiYaz(ScriptImportSonuc sonuc, ScriptMasinNeticesi netice)
        {
            var sira = netice.SiraNomresi > 0 ? $" · 📶 sıra №{netice.SiraNomresi}" : string.Empty;

            sonuc.Yaz($"🚗 {netice.Sira}. «{netice.Marka}» · {Nomre(netice.Nomre)} · {netice.Il} il" +
                      $" · alış {Pul(netice.AlisQiymeti)} · xərc {Pul(netice.XercCemi)} ({netice.XercSayi} sətir)" +
                      $" · ÜMUMİ MAYA {Pul(netice.UmumiMaya)}{sira}");

            foreach (var setir in netice.Kateqoriyalar)
            {
                sonuc.Yaz("      • " + setir);
            }

            foreach (var xeberdarliq in netice.Xeberdarliqlar)
            {
                sonuc.Yaz("      " + xeberdarliq);
            }

            if (netice.Qeyd.Length > 0)
            {
                var yer = netice.QeydYeri.Length > 0 ? $" → «{netice.QeydYeri}» xərcinə" : string.Empty;
                sonuc.Yaz($"      📝 Qeyd{yer}: {netice.Qeyd}");
            }

            sonuc.Yaz(string.Empty);
        }

        /// <summary>
        /// Xərc sətri olmayan avtomobillərin qeydini avtomatik yaradılan
        /// «Alış» xərcinə yazır ✓ (nadir hal ✓).
        /// </summary>
        private async Task QeydGozleyenleriYazAsync(
            ScriptImportSonuc sonuc,
            List<GozleyenQeyd> gozleyenler,
            bool yaz,
            CancellationToken cancellationToken)
        {
            if (!yaz || gozleyenler.Count == 0)
            {
                return;
            }

            // ⚡🚀 YALNIZ LAZIM OLAN «Alış» QEYDLƏRİ OXUNUR ✓✓✓ (PERFORMANS)
            // ----------------------------------------------------------------
            //  ⚠ ƏVVƏL: BÜTÜN xərc cədvəli yaddaşa yüklənirdi ✗ (1 000 000
            //     sətirdə ~400 MB RAM + saniyələrlə gözləmə ✗✓✓)
            //  ✅ İNDİ: hər avtomobil üçün yalnız «Alış» qeydi SQL-dən gəlir ✓
            var hamisi = new List<ExpenseItem>();

            foreach (var carId in gozleyenler.Select(g => g.CarId).Distinct())
            {
                var səhifə = await _xercler.GetPageAsync(
                    0,
                    20,
                    new ExpenseFilter(Kategoriya: "Alış", CarId: carId),
                    ExpenseSort.Tarix,
                    true,
                    cancellationToken);

                hamisi.AddRange(səhifə.Setirler);
            }

            foreach (var gozleyen in gozleyenler)
            {
                var alis = hamisi.FirstOrDefault(e => e.CarId == gozleyen.CarId && e.Kategoriya == "Alış");

                if (alis is null)
                {
                    continue;
                }

                alis.Qeyd = Birlestir(alis.Qeyd, gozleyen.Netice.Qeyd);
                gozleyen.Netice.QeydYeri = alis.Kategoriya;

                await _xercler.UpdateExpenseAsync(alis, cancellationToken);
            }
        }

        /// <summary>Ümumi yekun blokunu gündəliyə yazır ✓.</summary>
        private static void YekunYaz(ScriptImportSonuc sonuc)
        {
            sonuc.Yaz(new string('─', 66));
            sonuc.Yaz(sonuc.YalnizYoxlama
                ? "📊 YEKUN — YOXLAMA (bazaya heç nə yazılmadı ✗)"
                : "📊 YEKUN — İDXAL TAMAMLANDI (hər şey yazıldı ✓)");
            sonuc.Yaz($"🚗 Avtomobil: {sonuc.ElaveOlunanMasin} {(sonuc.YalnizYoxlama ? "yazılacaq" : "yazıldı")} ✓" +
                      $" · {sonuc.KecirilenMasin} keçildi ⏭️ · {sonuc.XetaliMasin} xətalı ✗");
            sonuc.Yaz($"🧾 Xərc sətri: {sonuc.ElaveOlunanXerc} ✓");
            sonuc.Yaz($"🔤 Kateqoriya: {sonuc.UygunlasdirilanKateqoriya} uyğunlaşdırıldı ✓" +
                      $" · {sonuc.YeniKateqoriya} yeni yaradıldı ➕");

            if (sonuc.YeniQrup > 0)
            {
                sonuc.Yaz($"📁 Yeni qrup: {sonuc.YeniQrup} ➕");
            }

            sonuc.Yaz($"💰 Alış cəmi: {Pul(sonuc.UmumiAlis)} · Xərc cəmi: {Pul(sonuc.UmumiXerc)}");
            sonuc.Yaz($"⛓️ ÜMUMİ MAYA DƏYƏRİ: {Pul(sonuc.UmumiMaya)} ✓✓✓");
        }

        // ====================================================================
        //  🔤 KATEQORİYA UYĞUNLAŞDIRMASI
        // --------------------------------------------------------------------
        //  Sıra ilə yoxlanılır (birinci uyğun qayda qazanır ✓):
        //    ① TAM uyğunluq   → «Elsen» = «Elşən» ✓ (ə/ş/ç/ğ/ı fərqi nəzərə alınmır)
        //    ② AÇAR SÖZLƏR    → «Usta xərci» → «Usta haqqı (Digər)» ✓
        //    ③ OXŞARLIQ ≥62 % → «Maşın bağlanma…» ≈ «Maşının bağlanma…» ✓
        //    ④ ŞƏXS ADI       → tək sözlü ad → «👤 Usta & İşçilik» qrupunda ✓
        //    ⑤ YENİ KATEQORİYA → «⚙️ Mühərrik & Ehtiyat Hissələri» qrupunda ✓
        // ====================================================================

        /// <summary>Kateqoriya və qrup siyahılarını bazadan oxuyur (bir dəfə ✓).</summary>
        private async Task<(List<KataloqSetiri> Setirler, HashSet<string> Qruplar)> KataloquOkuAsync(
            CancellationToken cancellationToken)
        {
            var kataloq = await _kataloq.GetAllEntriesAsync(cancellationToken);

            var setirler = kataloq
                .Where(e => !string.IsNullOrWhiteSpace(e.Kategoriya))
                .Select(e => new KataloqSetiri(e.Teyinat, e.Qrup, e.Kategoriya))
                .ToList();

            var qruplar = new HashSet<string>(
                setirler.Select(s => s.Qrup)
                        .Concat(await _kataloq.GetAllGroupsAsync(cancellationToken)),
                StringComparer.Ordinal);

            return (setirler, qruplar);
        }

        /// <summary>
        /// Skriptdəki təsviri MÖVCUD kateqoriyaya uyğunlaşdırır ✓ —
        /// uyğun kateqoriya yoxdursa <b>yenisini yaradır</b> ✓✓✓
        /// </summary>
        private static UygunNetice Uygunlasdir(
            string tasvir,
            List<KataloqSetiri> kataloq,
            HashSet<string> movcudQruplar,
            HashSet<string> yeniQruplar,
            HashSet<string> yeniKategoriyalar)
        {
            var norm = MetinUygunlasdirici.Normallasdir(tasvir);
            var temiz = TasvirTemizle(tasvir);

            // ① TAM uyğunluq — ə/ş/ç/ğ/ı fərqi NƏZƏRƏ ALINMIR ✓✓✓
            var tam = kataloq.FirstOrDefault(k => MetinUygunlasdirici.Normallasdir(k.Kategoriya) == norm);

            if (tam is not null)
            {
                return new UygunNetice(tam.Qrup, tam.Kategoriya, false, false, true, "mövcud kateqoriya ✓");
            }

            // ② AÇAR SÖZ qaydaları (usta · qeydiyyat · çərimə · benzin · malyar …)
            foreach (var qayda in Qaydalar)
            {
                if (!qayda.Acarlar.Any(acar => norm.Contains(acar, StringComparison.Ordinal)))
                {
                    continue;
                }

                var hedef = qayda.Kategoriya ?? temiz;
                var hedefNorm = MetinUygunlasdirici.Normallasdir(hedef);

                // Qaydadaki kateqoriya bazada VARSA → onun ƏSL qrupu istifadə olunur ✓
                var movcud = kataloq.FirstOrDefault(k =>
                    MetinUygunlasdirici.Normallasdir(k.Kategoriya) == hedefNorm);

                if (movcud is not null)
                {
                    return new UygunNetice(movcud.Qrup, movcud.Kategoriya, false, false, true,
                                           $"açar söz → «{movcud.Kategoriya}» ✓");
                }

                return new UygunNetice(
                    qayda.Qrup,
                    hedef,
                    MovcudDeyil(movcudQruplar, yeniQruplar, qayda.Qrup),
                    YeniKategoriyaKimiYaz(yeniKategoriyalar, kataloq, qayda.Qrup, hedef),
                    false,
                    qayda.Kategoriya is null ? "açar söz → qrup ✓" : "açar söz → yeni kateqoriya ➕");
            }

            // ③ OXŞARLIQ (62 %-dən yuxarı ✓)
            var enYaxsi = kataloq
                .Select(k => new { Setir = k, Bal = MetinUygunlasdirici.Oxsarlıq(tasvir, k.Kategoriya) })
                .OrderByDescending(x => x.Bal)
                .FirstOrDefault();

            if (enYaxsi is not null && enYaxsi.Bal >= 0.62)
            {
                return new UygunNetice(enYaxsi.Setir.Qrup, enYaxsi.Setir.Kategoriya, false, false, true,
                                       $"oxşarlıq → «{enYaxsi.Setir.Kategoriya}» ✓");
            }

            // ④ ŞƏXS ADI ehtimalı (tək söz · böyük hərflə · hissə sözü deyil ✓)
            if (norm.Split(' ').Length == 1 &&
                MetinUygunlasdirici.BoyukHerfleBaslayir(tasvir) &&
                !HissaSozleri.Contains(norm))
            {
                return new UygunNetice(
                    UstaQrupu,
                    temiz,
                    MovcudDeyil(movcudQruplar, yeniQruplar, UstaQrupu),
                    YeniKategoriyaKimiYaz(yeniKategoriyalar, kataloq, UstaQrupu, temiz),
                    false,
                    "şəxs / usta adı ✓");
            }

            // ⑤ YENİ KATEQORİYA — ehtiyat hissələri qrupunda ✓
            return new UygunNetice(
                StandartQrup,
                temiz,
                MovcudDeyil(movcudQruplar, yeniQruplar, StandartQrup),
                YeniKategoriyaKimiYaz(yeniKategoriyalar, kataloq, StandartQrup, temiz),
                false,
                "yeni kateqoriya ➕");
        }

        /// <summary>Qrup mövcuddursa <c>false</c>; bu idxalda yeni yaradılacaqsa <c>true</c> ✓.</summary>
        private static bool MovcudDeyil(HashSet<string> movcudQruplar, HashSet<string> yeniQruplar, string qrup)
            => !movcudQruplar.Contains(qrup) && yeniQruplar.Add(qrup);

        /// <summary>
        /// Kateqoriya YENİ yaradılacaqmı? ✓
        /// (eyni ad bazada varsa → <c>false</c> ✓; eyni ad bu idxalda tək dəfə sayılır ✓)
        /// </summary>
        private static bool YeniKategoriyaKimiYaz(
            HashSet<string> yeniKategoriyalar,
            List<KataloqSetiri> kataloq,
            string qrup,
            string ad)
        {
            var norm = MetinUygunlasdirici.Normallasdir(ad);

            if (norm.Length == 0)
            {
                return false;
            }

            if (kataloq.Any(k => MetinUygunlasdirici.Normallasdir(k.Kategoriya) == norm))
            {
                return false;
            }

            return yeniKategoriyalar.Add(qrup + "│" + norm);
        }

        // ====================================================================
        //  🧰 KÖMƏKÇİ METODLAR
        // ====================================================================

        /// <summary>
        /// ƏN AXIRINCI xərcin indeksi ✓ — qeyd məhz ona yazılır ✓✓✓
        /// (tarixə görə ✓; tarixlər bərabərdirsə skriptdəki sıra ilə ✓)
        /// </summary>
        private static int SonuncuXercIndeksi(List<ScriptXerc> xercler)
        {
            var secilmis = -1;
            var secilmisTarix = DateTime.MinValue;

            for (var i = 0; i < xercler.Count; i++)
            {
                if (xercler[i].TasvirMetni.Length == 0 && xercler[i].MeblegDeyeri == 0m)
                {
                    continue;
                }

                var tarix = TarixOku(xercler[i].Tarix) ?? DateTime.MinValue;

                if (secilmis < 0 || tarix >= secilmisTarix)
                {
                    secilmis = i;
                    secilmisTarix = tarix;
                }
            }

            return secilmis;
        }

        /// <summary>Tarix mətnini oxuyur («09.07.2023» ✓ — oxunmazsa <c>null</c> ✓).</summary>
        private static DateTime? TarixOku(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return null;
            }

            var temiz = metn.Trim();

            if (DateTime.TryParseExact(temiz, TarixFormatlari, CultureInfo.InvariantCulture,
                                       DateTimeStyles.None, out var tam))
            {
                return tam.Date;
            }

            return DateTime.TryParse(temiz, CultureInfo.InvariantCulture, DateTimeStyles.None, out var serbest)
                ? serbest.Date
                : null;
        }

        /// <summary>Kateqoriya adını təmizləyir (artıq boşluqlar və uzunluq həddi ✓).</summary>
        private static string TasvirTemizle(string? tasvir)
        {
            var temiz = (tasvir ?? string.Empty).Trim();

            while (temiz.Contains("  ", StringComparison.Ordinal))
            {
                temiz = temiz.Replace("  ", " ", StringComparison.Ordinal);
            }

            temiz = temiz.Trim(' ', '.', ',', '-', ';', ':', '*', '/');

            if (temiz.Length == 0)
            {
                temiz = "Xərc";
            }

            // Baza sütunu 150 simvoldur ✓ → təhlükəsiz hədd ✓
            return temiz.Length > 120 ? temiz[..120].Trim() : temiz;
        }

        /// <summary>Pul mətni: «11 200.00 ₼» ✓ (kultura ayarından asılı deyil ✓).</summary>
        private static string Pul(decimal mebleg)
            => mebleg.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ") + " ₼";

        /// <summary>Nömrə mətni (boşdursa «—» ✓).</summary>
        private static string Nomre(string? nomre)
            => string.IsNullOrWhiteSpace(nomre) ? "—" : nomre.Trim();

        /// <summary>İki qeydi birləşdirir (boş olanlar atılır ✓ · 500 simvol həddi ✓).</summary>
        private static string Birlestir(string? evvelki, string? yeni)
        {
            var a = (evvelki ?? string.Empty).Trim();
            var b = (yeni ?? string.Empty).Trim();

            var netice = a.Length == 0 ? b : b.Length == 0 ? a : a + " · " + b;

            return netice.Length > 480 ? netice[..480] : netice;
        }

        /// <summary>Kataloq sətri (təyinat · qrup · kateqoriya ✓).</summary>
        private sealed record KataloqSetiri(string Teyinat, string Qrup, string Kategoriya);

        /// <summary>Qeydi «Alış» xərcinə yazılacaq avtomobil ✓.</summary>
        private sealed record GozleyenQeyd(int CarId, ScriptMasinNeticesi Netice);
    }
}

