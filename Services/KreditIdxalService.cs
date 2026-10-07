using System.Globalization;
using System.Text;
using EnterpriseAeroStudio.Models;
using Microsoft.Extensions.Logging;

namespace EnterpriseAeroStudio.Services
{
    /// <inheritdoc />
    public sealed class KreditIdxalService : IKreditIdxalService
    {
        private readonly ICarService _cars;
        private readonly ICreditService _kreditler;
        private readonly IMohletService _mohletler;
        private readonly ILogger<KreditIdxalService> _logger;

        /// <summary>Dəstəklənən tarix formatları («09.07.2023» ✓).</summary>
        private static readonly string[] TarixFormatlari =
        {
            "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
            "yyyy-MM-dd", "dd-MM-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy/MM/dd"
        };

        public KreditIdxalService(
            ICarService cars,
            ICreditService kreditler,
            IMohletService mohletler,
            ILogger<KreditIdxalService> logger)
        {
            _cars = cars;
            _kreditler = kreditler;
            _mohletler = mohletler;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Numune => IdxalMetni.Numune;

        // ====================================================================
        //  🔎 YOXLAMA  — bazaya HEÇ NƏ yazılmır ✗✓✓
        // ====================================================================

        /// <inheritdoc />
        public KreditIdxalNeticesi Yoxla(string metn)
        {
            var netice = new KreditIdxalNeticesi();

            if (string.IsNullOrWhiteSpace(metn))
            {
                netice.Ugurlu = false;
                netice.Xeta = "Mətn boşdur — kredit məlumatını (və ya nümunəni) yapışdırın.";
                return netice;
            }

            var bloklar = BloklariAyir(metn);

            if (bloklar.Count == 0)
            {
                netice.Ugurlu = false;
                netice.Xeta = "Heç bir kredit bloku tapılmadı — «Açar: Dəyər» sətirlərindən istifadə edin (nümunə üçün «📋 Nümunə»).";
                return netice;
            }

            netice.Setirler.Add($"🧾 {bloklar.Count} kredit bloku təhlil edildi.");

            var sira = 1;

            foreach (var blok in bloklar)
            {
                var kart = KartQur(blok, out var xeta);

                if (kart is null || xeta is not null)
                {
                    netice.Ugurlu = false;
                    netice.Xeta = $"❌ {blok.Basliq} → {xeta}";
                    netice.Setirler.Add(netice.Xeta);
                    return netice;
                }

                netice.Kartlar.Add(kart);
                netice.UmumiKok += kart.Kok;
                netice.UmumiMaya += kart.Maya;

                netice.Setirler.Add($"   ✅ #{sira}  {kart.Basliq}");
                netice.Setirler.Add(
                    $"        kök {kart.KokMetni} · ümumi {kart.UmumiMetni} · faiz {kart.FaizMetni} · " +
                    $"aylıq {kart.AylikMetni} × {kart.MuddetMetni}");
                netice.Setirler.Add(
                    $"        ödənilmiş {kart.OdenilmisMetni} · qalıq nisyə {kart.QaliqNisyeMetni} · " +
                    $"maya {kart.MayaMetni}");

                if (kart.CerimeCemi > 0m)
                {
                    netice.Setirler.Add(
                        $"        ⚖️ cərimələr {kart.CerimeMetni} " +
                        $"({kart.Setirler.Count(s => s.Nov == "⚖️ Cərimə")} qeyd) " +
                        "→ 💵 KASSA-ya yazılır ✓ (tərəfdaşlara BÖLÜNMÜR ✗)");
                }

                if (kart.MohletCemi > 0m)
                {
                    netice.Setirler.Add(
                        $"        ⏳ ilkin ödənişə möhlət {kart.MohletMetni} " +
                        $"({kart.Setirler.Count(s => s.Nov == "⏳ Möhlət")} qeyd)");
                }

                if (!string.IsNullOrWhiteSpace(kart.OdenisGunAraligi))
                {
                    netice.Setirler.Add($"        📅 ödəniş günləri: {kart.OdenisGunAraligi}");
                }

                if (kart.Qrafikden)
                {
                    netice.Setirler.Add(
                        $"        📅 cədvəl İSTİFADƏÇİDƏN götürüldü (maya və paylar olduğu kimi ✓) — {kart.Setirler.Count(s => s.Nov == "Taksit")} sətir");
                }

                if (!string.IsNullOrWhiteSpace(kart.BolguXulase))
                {
                    netice.Setirler.Add($"        bölgü → {kart.BolguXulase}");
                }

                sira++;
            }

            netice.Ugurlu = true;
            netice.KreditSayi = netice.Kartlar.Count;

            netice.Setirler.Add(string.Empty);
            netice.Setirler.Add(
                $"📊 CƏMİ: {netice.Kartlar.Count} kredit · kök {Pul(netice.UmumiKok)} · maya {Pul(netice.UmumiMaya)}");

            return netice;
        }

        // ====================================================================
        //  📥 İDXAL  — uğurlu təhlildən sonra bazaya yazılır ✓✓✓
        // ====================================================================

        /// <inheritdoc />
        public async Task<KreditIdxalNeticesi> IcraEtAsync(
            string metn,
            bool maasinXeyiriniBol = false,
            CancellationToken cancellationToken = default)
        {
            var netice = Yoxla(metn);

            if (!netice.Ugurlu)
            {
                return netice;
            }

            var bloklar = BloklariAyir(metn);

            // Mövcud avtomobillər — nömrəyə görə (təkrar yaratmamaq üçün ✓).
            var movcud = (await _cars.GetAllCarsAsync(cancellationToken))
                .Where(c => !string.IsNullOrWhiteSpace(c.QeydiyyatNisani))
                .GroupBy(c => c.QeydiyyatNisani.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // ⏭️ Mövcud müqavilə nömrələri — TƏKRAR idxalın qarşısı alınır ✓✓✓
            var movcudMuqavileler = (await _kreditler.GetCreditsAsync(cancellationToken))
                .Select(k => (k.MuqavileNomresi ?? string.Empty).Trim())
                .Where(x => x.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < bloklar.Count; i++)
            {
                var blok = bloklar[i];
                var kart = netice.Kartlar[i];

                // ⏭️ Bu müqavilə artıq varsa → KEÇİLİR ✗ (köhnə məlumat dəyişmir ✓)
                if (!string.IsNullOrWhiteSpace(blok.Muqavile)
                    && movcudMuqavileler.Contains(blok.Muqavile.Trim()))
                {
                    netice.KecirilenKredit++;
                    netice.Setirler.Add(
                        $"   ⏭️ {blok.Basliq} → «{blok.Muqavile.Trim()}» artıq mövcuddur, KEÇİLDİ ✗");
                    continue;
                }

                // ---- 1) 🚘 Avtomobil -----------------------------------------
                CarItem? car = null;

                if (!string.IsNullOrWhiteSpace(blok.Nomre)
                    && movcud.TryGetValue(blok.Nomre.Trim(), out var tapilan))
                {
                    car = tapilan;
                }

                if (car is null)
                {
                    car = new CarItem
                    {
                        Marka = string.IsNullOrWhiteSpace(blok.Masin) ? "—" : blok.Masin.Trim(),
                        QeydiyyatNisani = blok.Nomre.Trim(),
                        Il = blok.Il,
                        AlisUsulu = Catalog.CashPurchase,
                        AlisQiymeti = blok.Maya > 0m
                            ? blok.Maya
                            : blok.AlisQiymeti + blok.ElaveXercler,
                        SatisQiymeti = blok.SatisQiymeti,
                        AlisTarixi = blok.Baslama ?? DateTime.Today,
                        Status = Catalog.CreditStatus
                    };

                    await _cars.AddCarAsync(car, cancellationToken);
                    netice.MasinSayi++;

                    if (!string.IsNullOrWhiteSpace(blok.Nomre))
                    {
                        movcud[blok.Nomre.Trim()] = car;
                    }
                }
                else if (!string.Equals(car.Status, Catalog.CreditStatus, StringComparison.Ordinal))
                {
                    await _cars.SetStatusAsync(car.Id, Catalog.CreditStatus, cancellationToken);
                }

                // ---- 2) 📄 Kredit -------------------------------------------
                var muqavile = blok.Muqavile.Trim();

                if (string.IsNullOrWhiteSpace(muqavile))
                {
                    muqavile = await NovbetiMuqavileAsync(cancellationToken);
                }

                var credit = new Credit
                {
                    MuqavileNomresi = muqavile,
                    Mustəri = blok.Musteri.Trim(),
                    CarId = car.Id,
                    Mebleg = kart.Kok + blok.Ilkin,
                    IlkinOdenis = blok.Ilkin,
                    FaizDerecesi = kart.Faiz,
                    MuddetAy = kart.Muddet,
                    AylıqOdenis = kart.Aylik,
                    BaslamaTarixi = blok.Baslama
                        ?? blok.Qrafik.OrderBy(q => q.Tarix).FirstOrDefault()?.Tarix
                        ?? blok.Odenisler.OrderBy(o => o.Tarix).FirstOrDefault()?.Tarix
                        ?? DateTime.Today,
                    Status = "Aktiv",
                    Qeyd = BuildQeyd(blok)
                };

                await _kreditler.AddCreditAsync(credit, cancellationToken);
                movcudMuqavileler.Add(muqavile);

                // ---- 3) 👥 XEYİR bölgüsü + 💰 ödənişlər -----------------------
                await OdenisleriYazAsync(blok, kart, credit.Id, maasinXeyiriniBol, netice, cancellationToken);

                _logger.LogInformation(
                    "🧾 Kredit idxalı: {Basliq} → avtomobil #{CarId}, kredit #{CreditId}, {Say} ödəniş ✓",
                    kart.Basliq, car.Id, credit.Id, kart.Setirler.Count(s => s.Odenilib));

                // 🎯 Tam ödənilibsə → kredit AVTOMATİK BAĞLANACAQ ✓ (istifadəçiyə xəbər ✓)
                if (kart.Umumi > 0m && kart.Odenilmis >= kart.Umumi - 0.50m)
                {
                    netice.Setirler.Add(
                        $"   🎯 {kart.Basliq} → TAM ÖDƏNİLİB ({kart.OdenilmisMetni} / {kart.UmumiMetni}) " +
                        "→ kredit BAĞLANIR və avtomobil arxivə keçir ✓  «🗄️ Satılan & Krediti Bitmiş» tabında görünür ✓");
                }
            }

            netice.Setirler.Add(string.Empty);
            netice.Setirler.Add(
                $"📥 İDXAL TAMAMLANDI ✓ — {netice.KreditSayi} kredit · {netice.MasinSayi} yeni avtomobil · " +
                $"{netice.OdenisSayi} ödəniş · {netice.CerimeSayi} cərimə · {netice.MohletSayi} möhlət" +
                (netice.MohletOdenisSayi > 0 ? $" (⏳ {netice.MohletOdenisSayi} möhlət ödənildi ✓)" : string.Empty) +
                $" · {netice.PaySayi} tərəfdaş payı" +
                (netice.KecirilenKredit > 0 ? $" · {netice.KecirilenKredit} kredit KEÇİLDİ ⏭️" : string.Empty));

            return netice;
        }

        /// <summary>Kreditin XEYİR bölgüsünü və faktiki ödənişlərini bazaya yazır ✓.</summary>
        private async Task OdenisleriYazAsync(
            KreditIdxalBloku blok,
            KreditIdxalKart kart,
            int creditId,
            bool maasinXeyiriniBol,
            KreditIdxalNeticesi netice,
            CancellationToken cancellationToken)
        {
            // 👥 Kreditin XEYİR bölgüsü (istəyə bağlı ✓)
            if (maasinXeyiriniBol && blok.Maya > 0m && blok.SatisQiymeti > blok.Maya)
            {
                var xeyirPaylari = PayRows(blok);
                PartnerMath.Distribute(blok.SatisQiymeti - blok.Maya, xeyirPaylari);

                await _kreditler.SaveCreditSharesAsync(
                    creditId,
                    true,
                    xeyirPaylari.Where(p => p.Mebleg > 0m).ToList(),   // Mebleg ✓
                    cancellationToken);

                netice.PaySayi += xeyirPaylari.Count(p => p.Mebleg > 0m);   // Mebleg ✓
            }

            // ================================================================
            //  ⏳ İLKİN ÖDƏNİŞƏ MÖHLƏTLƏR — ƏVVƏLCƏ yazılır ✓✓✓
            // -----------------------------------------------------------------
            //  «25.04.2024 tarixədək əlavə 2000 AZN» → OdenisMohlet (ödənilməmiş ✓)
            //  Sonra qrafikdəki «möhlətin ödənilən pulu» sətri onu
            //  «ÖDƏNİLDİ» işarələyəcək ✓ (pul kassaya düşür ✓ — kredit taksiti
            //  kimi SAYILMIR ✗✓✓ → kredit vaxtından tez bağlanmır ✓)
            // ================================================================
            var mohletler = new List<OdenisMohlet>();

            if (blok.Mohletler.Count > 0)
            {
                foreach (var m in blok.Mohletler.OrderBy(x => x.Tarix))
                {
                    mohletler.Add(new OdenisMohlet
                    {
                        Menbe = OdenisMohlet.MenbeIlkinOdenis,
                        CreditId = creditId,
                        Tarix = m.Tarix == default ? (blok.Baslama ?? DateTime.Today) : m.Tarix,
                        Mebleg = m.Mebleg,
                        OdenisUsulu = Catalog.PaymentMethods[0],
                        Odenilib = false,
                        Qeyd = m.Qeyd
                    });
                }

                await _mohletler.SaveForCreditAsync(creditId, mohletler, cancellationToken);

                netice.MohletSayi += mohletler.Count;

                // ⚠ ID-lər üçün yenidən oxunur ✓ (SetOdenildiAsync üçün lazımdır)
                mohletler = (await _mohletler.GetForCreditAsync(creditId, cancellationToken))
                    .OrderBy(m => m.Tarix)
                    .ToList();
            }

            // 💰 ÖDƏNİŞLƏR — dəqiq qrafik varsa paylar OLDUĞU KİMİ yazılır ✓✓✓
            if (blok.Qrafik.Count > 0)
            {
                var bugun = DateTime.Today;
                var taksitNo = 0;

                foreach (var q in blok.Qrafik.OrderBy(x => x.Tarix))
                {
                    if (q.Tarix.Date > bugun)
                    {
                        continue;   // 🕓 gələcək ödəniş — hələ YAZILMIR ✗
                    }

                    // ⏳ İLKİN MÖHLƏTİN ÖDƏNİŞİ — kredit taksiti DEYİL ✗✓✓
                    //    (kredit balansına əlavə olunmur ✗, möhlət «ödənilib» olur ✓)
                    if (q.MohletOdenisi)
                    {
                        await MohletiOdenildiAsync(mohletler, q, netice, cancellationToken);
                        continue;
                    }

                    taksitNo++;

                    var verilenPaylar = q.Paylar
                        .Where(p => p.Mebleg > 0m)
                        .Select((p, i) => new PartnerShare
                        {
                            Terefdas = p.Ad,
                            Mebleg = p.Mebleg,
                            Aktiv = true,
                            Sira = i
                        })
                        .ToList();

                    await _kreditler.AddTransactionAsync(new CreditTransaction
                    {
                        CreditId = creditId,
                        Nov = "Gəlir",
                        InstallmentNo = taksitNo,
                        Mebleg = q.Odenis,
                        Tarix = q.Tarix,
                        Tesvir = string.IsNullOrWhiteSpace(q.Qeyd)
                            ? $"🧾 Kredit idxalı — {taksitNo}-ci sətir"
                            : $"🧾 {q.Qeyd}",
                        TerefdasBolguTetbiqOlunub = verilenPaylar.Count > 0,
                        BolguBazasi = q.Menfeet > 0m ? q.Menfeet : null,
                        TerefdasPaylari = verilenPaylar
                    }, cancellationToken);

                    netice.OdenisSayi++;
                    netice.PaySayi += verilenPaylar.Count;
                }
            }
            else
            {
                foreach (var setir in kart.Setirler.Where(s => s.Odenilib))
            {
                var paylar = PayRows(blok);
                PartnerMath.Distribute(setir.Menfeet, paylar);

                var hereket = new CreditTransaction
                {
                    CreditId = creditId,
                    Nov = "Gəlir",
                    InstallmentNo = setir.Ay,
                    Mebleg = setir.Odenis,
                    Tarix = setir.Tarix,
                    Tesvir = $"🧾 Kredit idxalı — {setir.Ay}-ci taksit",
                    TerefdasBolguTetbiqOlunub = true,
                    BolguBazasi = setir.Menfeet,
                    TerefdasPaylari = paylar.Where(p => p.Mebleg > 0m).ToList()   // Mebleg ✓
                };

                await _kreditler.AddTransactionAsync(hereket, cancellationToken);

                netice.OdenisSayi++;
                netice.PaySayi += hereket.TerefdasPaylari.Count;
                }
            }

            // ⚖️ GECİKMƏ CƏRİMƏLƏRİ ✓✓✓ (paylar «CreditService» tərəfindən
            //    qalıq payçıları arasında yarı-yarıya AVTOMATİK yazılır ✓)
            foreach (var g in blok.Gecikmeler.OrderBy(x => x.Tarix))
            {
                await _kreditler.AddTransactionAsync(new CreditTransaction
                {
                    CreditId = creditId,
                    Nov = "Gecikmə",
                    Mebleg = g.Mebleg,
                    Tarix = g.Tarix,
                    Tesvir = string.IsNullOrWhiteSpace(g.Qeyd) ? "Gecikmə cəriməsi" : g.Qeyd,
                    Odenilib = g.Odenilib
                }, cancellationToken);

                netice.CerimeSayi++;
            }
        }

        /// <summary>
        /// Kredit üçün zəngin QEYD mətni: istifadəçinin qeydi + ödəniş günləri +
        /// ilkin ödənişə möhlət xülasəsi ✓✓✓
        /// </summary>
        private static string BuildQeyd(KreditIdxalBloku blok)
        {
            var hisseler = new List<string>();

            if (!string.IsNullOrWhiteSpace(blok.Qeyd))
            {
                hisseler.Add(blok.Qeyd.Trim());
            }

            if (!string.IsNullOrWhiteSpace(blok.OdenisGunAraligi))
            {
                hisseler.Add("Ödəniş günləri: " + blok.OdenisGunAraligi.Trim());
            }

            if (blok.Mohletler.Count > 0)
            {
                hisseler.Add($"İlkin ödənişə möhlət: {Pul(blok.Mohletler.Sum(m => m.Mebleg))}");
            }

            var netice = string.Join(" · ", hisseler);

            return netice.Length > 480 ? netice[..480] : netice;
        }

        /// <summary>Növbəti sərbəst müqavilə nömrəsi («M-0042» ✓).</summary>
        private async Task<string> NovbetiMuqavileAsync(CancellationToken cancellationToken)
        {
            var movcud = await _kreditler.GetCreditsAsync(cancellationToken);
            var max = 0;

            foreach (var kredit in movcud)
            {
                var reqemler = new string((kredit.MuqavileNomresi ?? string.Empty).Where(char.IsDigit).ToArray());

                if (int.TryParse(reqemler, out var deyer) && deyer > max)
                {
                    max = deyer;
                }
            }

            return "M-" + (max + 1).ToString("D4", CultureInfo.InvariantCulture);
        }

        // ====================================================================
        //  🧩 MƏTNİN TƏHLİLİ  — bloklara ayırma ✓✓✓
        // ====================================================================

        /// <summary>Toplu mətni kredit bloklarına ayırır («---» ayırıcısı ilə ✓).</summary>
        private static List<KreditIdxalBloku> BloklariAyir(string metn)
        {
            var netice = new List<KreditIdxalBloku>();
            var cari = new KreditIdxalBloku();
            var bolme = string.Empty;
            var odemeRejimi = false;
            List<string>? qrafikBasliqlari = null;

            void Bitir()
            {
                if (BlokDoludur(cari))
                {
                    netice.Add(cari);
                }

                cari = new KreditIdxalBloku();
                bolme = string.Empty;
                odemeRejimi = false;
                qrafikBasliqlari = null;
            }

            foreach (var xam in metn.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var setir = xam.Trim();

                if (setir.Length == 0)
                {
                    odemeRejimi = false;
                    continue;
                }

                // Blok ayırıcı: --- və ya ===
                if (setir.Length >= 3 && (setir.All(c => c == '-') || setir.All(c => c == '=')))
                {
                    Bitir();
                    continue;
                }

                // [BÖLMƏ] başlığı → növbəti sətirlərin MƏNASI dəyişir ✓✓✓
                if (setir.Length >= 3 && setir[0] == '[' && setir[^1] == ']')
                {
                    bolme = AcarNorm(setir[1..^1]);
                    odemeRejimi = bolme.Contains("odenis", StringComparison.Ordinal)
                                  && !bolme.Contains("mohlet", StringComparison.Ordinal);
                    continue;
                }

                // 👥 TƏRƏFDAŞLAR bölməsi — «Musa | 50% | KarM»
                if (bolme.Contains("terefdas", StringComparison.Ordinal)
                    || bolme.Contains("pay bolgusu", StringComparison.Ordinal))
                {
                    TerefdasCedvelSetiri(cari, setir);
                    continue;
                }

                // 📅 AYLİQ QRAFİK bölməsi — «Tarix | Ödəniş | Maya | KarMusa | …»
                if (bolme.Contains("qrafik", StringComparison.Ordinal)
                    || QrafikBasliqmi(setir))
                {
                    if (QrafikBasliqmi(setir))
                    {
                        qrafikBasliqlari = setir.TrimStart('#', ' ', '\t')
                            .Split('|')
                            .Select(x => x.Trim())
                            .ToList();
                    }
                    else if (qrafikBasliqlari is null)
                    {
                        // başlıq yoxdur → «Tarix | Ödəniş | Maya» fərz edilir ✓
                        qrafikBasliqlari = new List<string> { "Tarix", "Ödəniş", "Maya" };
                        QrafikSetiriEkle(cari, setir, qrafikBasliqlari);
                    }
                    else
                    {
                        QrafikSetiriEkle(cari, setir, qrafikBasliqlari);
                    }

                    continue;
                }

                // ⚖️ GECİKMƏLƏR bölməsi — «07.05.2025 | 20.00 | Gecikmə cəriməsi»
                if (bolme.Contains("gecikme", StringComparison.Ordinal)
                    || bolme.Contains("cerime", StringComparison.Ordinal)
                    || bolme.Contains("ceza", StringComparison.Ordinal))
                {
                    if (!setir.StartsWith('#'))
                    {
                        GecikmeSetiri(cari, setir);
                    }

                    continue;
                }

                // 💰 Ödənişlər (köhnə üsul: «Ödənişlər:» açarı və ya bölmə ✓)
                if (odemeRejimi)
                {
                    var od = OdenisOku(setir);

                    if (od is not null)
                    {
                        cari.Odenisler.Add(od);
                        continue;
                    }

                    odemeRejimi = false;
                }

                var (acar, deyer) = AcarDeyerAyir(setir);

                if (acar is null)
                {
                    // 📅 Cədvəl sətri («… | … | …») → ödəniş kimi OXUNMUR ✗✓✓
                    if (setir.Contains('|'))
                    {
                        continue;
                    }

                    var od = OdenisOku(setir);

                    if (od is not null)
                    {
                        cari.Odenisler.Add(od);
                    }

                    continue;
                }

                var a = AcarNorm(acar);

                if (a is "odenisler" or "odenis" or "taksitler" or "payments" or "hereketler")
                {
                    odemeRejimi = true;
                    bolme = "odenisler";

                    if (!string.IsNullOrWhiteSpace(deyer))
                    {
                        var od = OdenisOku(deyer);

                        if (od is not null)
                        {
                            cari.Odenisler.Add(od);
                        }
                    }

                    continue;
                }

                SetKey(cari, a, deyer);
            }

            Bitir();

            for (var i = 0; i < netice.Count; i++)
            {
                netice[i].Sira = i + 1;
            }

            return netice;
        }

        /// <summary>Blokda mənalı məlumat varmı?</summary>
        private static bool BlokDoludur(KreditIdxalBloku b)
            => !string.IsNullOrWhiteSpace(b.Masin)
               || !string.IsNullOrWhiteSpace(b.Nomre)
               || !string.IsNullOrWhiteSpace(b.Musteri)
               || !string.IsNullOrWhiteSpace(b.Muqavile)
               || b.Kok > 0m
               || b.Umumi > 0m
               || b.Aylik > 0m
               || b.Muddet > 0
               || b.Odenisler.Count > 0
               || b.Qrafik.Count > 0
               || b.Terefdaslar.Count > 0;

        /// <summary>«Açar: dəyər» sətrini ayırır (':' və ya tab ✓).</summary>
        private static (string? Acar, string Deyer) AcarDeyerAyir(string setir)
        {
            var iki = setir.IndexOf(':');

            if (iki > 0)
            {
                return (setir[..iki].Trim(), setir[(iki + 1)..].Trim());
            }

            var tab = setir.IndexOf('\t');

            if (tab > 0)
            {
                return (setir[..tab].Trim(), setir[(tab + 1)..].Trim());
            }

            return (null, string.Empty);
        }

        /// <summary>📅 Qrafik BAŞLIQ sətri? («# Tarix | Ödəniş | Maya | KarMusa | …» ✓)</summary>
        private static bool QrafikBasliqmi(string setir)
        {
            var ilk = setir.TrimStart('#', ' ', '\t').Split('|')[0].Trim();

            if (AcarNorm(ilk) is not ("tarix" or "tarih" or "date"))
            {
                return false;
            }

            var tam = AcarNorm(setir);

            return tam.Contains("maya", StringComparison.Ordinal)
                || tam.Contains("kar", StringComparison.Ordinal);
        }

        /// <summary>Sütun başlığının indeksini tapır (normallaşdırılmış ✓).</summary>
        private static int KolonIndeksi(List<string> basliqlar, params string[] adaylar)
        {
            for (var i = 0; i < basliqlar.Count; i++)
            {
                var n = AcarNorm(basliqlar[i]);

                foreach (var a in adaylar)
                {
                    if (n == a || n.Contains(a, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>«KarMusa» → «Musa» ✓.</summary>
        private static string PayAdi(string basliq)
        {
            var t = basliq.Trim().TrimStart('#').Trim();

            if (t.Length > 3 && AcarNorm(t).StartsWith("kar", StringComparison.Ordinal))
            {
                t = t[3..].Trim();
            }

            return t.Trim('-', ':', '.', ' ');
        }

        /// <summary>📅 Qrafik CƏDVƏL sətrini oxuyur («03.02.2024 | 0.00 | 0.00 | 600.00 | …» ✓).</summary>
        private static void QrafikSetiriEkle(KreditIdxalBloku b, string setir, List<string> basliqlar)
        {
            var hucreler = setir.Split('|').Select(x => x.Trim()).ToArray();

            var tarixIdx = KolonIndeksi(basliqlar, "tarix", "tarih", "date");
            var odenisIdx = KolonIndeksi(basliqlar, "odenis", "payment", "mebleg");
            var mayaIdx = KolonIndeksi(basliqlar, "maya", "kok");
            var qeydIdx = KolonIndeksi(basliqlar, "qeyd", "note", "izah");

            if (tarixIdx < 0 || tarixIdx >= hucreler.Length)
            {
                return;
            }

            var tarix = TarixOku(hucreler[tarixIdx]);

            if (tarix is null)
            {
                return;
            }

            var setr = new KreditIdxalQrafikSetir
            {
                Tarix = tarix.Value,
                Odenis = odenisIdx >= 0 && odenisIdx < hucreler.Length
                    ? PulOku(hucreler[odenisIdx]) ?? 0m
                    : 0m,
                Maya = mayaIdx >= 0 && mayaIdx < hucreler.Length
                    ? PulOku(hucreler[mayaIdx]) ?? 0m
                    : 0m,
                Qeyd = qeydIdx >= 0 && qeydIdx < hucreler.Length ? hucreler[qeydIdx] : string.Empty
            };

            // ⏳ «İlkin ödəniş möhlətinin ödənilən pulu» sətri → kredit taksiti DEYİL ✗✓✓
            var nQeyd = AcarNorm(setr.Qeyd);

            setr.MohletOdenisi =
                nQeyd.Contains("mohlet", StringComparison.Ordinal)
                || (nQeyd.Contains("ilkin", StringComparison.Ordinal)
                    && nQeyd.Contains("oden", StringComparison.Ordinal));

            for (var i = 0; i < basliqlar.Count && i < hucreler.Length; i++)
            {
                if (i == tarixIdx || i == odenisIdx || i == mayaIdx || i == qeydIdx)
                {
                    continue;
                }

                if (!AcarNorm(basliqlar[i]).StartsWith("kar", StringComparison.Ordinal))
                {
                    continue;
                }

                var ad = PayAdi(basliqlar[i]);

                if (ad.Length == 0)
                {
                    continue;
                }

                setr.Paylar.Add(new KreditIdxalPay
                {
                    Ad = ad,
                    Mebleg = PulOku(hucreler[i]) ?? 0m
                });
            }

            b.Qrafik.Add(setr);
        }

        /// <summary>👥 Tərəfdaş CƏDVƏL sətri — «Musa | 50% | KarM» ✓.</summary>
        private static void TerefdasCedvelSetiri(KreditIdxalBloku b, string setir)
        {
            var p = setir.Split('|').Select(x => x.Trim()).ToArray();

            if (p.Length == 0 || p[0].Length == 0)
            {
                return;
            }

            var ad = p[0].TrimStart('-', '•', '*', '#').Trim();

            // Başlıq sətri («Ad | Faiz | Rol») → keçilir ✓
            var nAd = AcarNorm(ad);

            if (nAd is "ad" or "terefdas" or "name" or "ad soyad" or "terefdas adi")
            {
                return;
            }

            var faiz = 0m;
            var qalig = false;
            var rol = p.Length >= 3 ? p[2].Trim() : string.Empty;

            if (p.Length >= 2)
            {
                if (AcarNorm(p[1]).Contains("qalig", StringComparison.Ordinal))
                {
                    qalig = true;
                }

                var f = PulOku(p[1]);

                if (f is not null)
                {
                    faiz = f.Value;
                }
            }

            if (ad.Length == 0)
            {
                return;
            }

            b.Terefdaslar.Add(new KreditIdxalTerefdas
            {
                Ad = ad,
                Faiz = faiz,
                QaligPayi = qalig || faiz <= 0m,
                Rol = rol
            });
        }

        /// <summary>⚖️ Gecikmə cəriməsi sətri — «07.05.2025 | 20.00 | Qeyd» ✓.</summary>
        private static void GecikmeSetiri(KreditIdxalBloku b, string setir)
        {
            var p = setir.Split('|').Select(x => x.Trim()).ToArray();

            // Boşluqla ayrılmış köhnə üsul: «07.05.2025 20.00» ✓
            if (p.Length < 2)
            {
                var sade = OdenisOku(setir);

                if (sade is not null && sade.Tarix != default)
                {
                    b.Gecikmeler.Add(new KreditIdxalGecikme
                    {
                        Tarix = sade.Tarix,
                        Mebleg = sade.Mebleg,
                        Qeyd = "Gecikmə cəriməsi",
                        Odenilib = true
                    });
                }

                return;
            }

            var tarix = TarixOku(p[0]);

            if (tarix is null)
            {
                return;
            }

            var mebleg = PulOku(p[1]);

            if (mebleg is null || mebleg <= 0m)
            {
                return;
            }

            var qeyd = p.Length >= 3 && p[2].Length > 0 ? p[2] : "Gecikmə cəriməsi";

            var odenilib = true;

            if (p.Length >= 4)
            {
                var n = AcarNorm(p[3]);

                if (n.Contains("odenilme", StringComparison.Ordinal)
                    || n.Contains("gozlen", StringComparison.Ordinal)
                    || n.Contains("yox", StringComparison.Ordinal)
                    || n.Contains("qaliq", StringComparison.Ordinal))
                {
                    odenilib = false;
                }
            }

            b.Gecikmeler.Add(new KreditIdxalGecikme
            {
                Tarix = tarix.Value,
                Mebleg = mebleg.Value,
                Qeyd = qeyd,
                Odenilib = odenilib
            });
        }

        /// <summary>
        /// ⏳ İlkin ödəniş MÖHLƏTİ sətrini oxuyur —
        /// «25.04.2024 tarixədək əlavə 2000 AZN ödəniləcək» ✓✓✓
        /// </summary>
        private static void MohletOku(KreditIdxalBloku b, string deyer)
        {
            if (string.IsNullOrWhiteSpace(deyer))
            {
                return;
            }

            DateTime? tarix = null;
            var sb = new StringBuilder();

            foreach (var tok in deyer.Split(' ', '\t'))
            {
                var t = tok.Trim();

                if (t.Length == 0)
                {
                    continue;
                }

                var td = TarixOku(t);

                if (td is not null && tarix is null)
                {
                    tarix = td;
                    continue;   // tarix məbləğə qarışmasın ✓
                }

                sb.Append(t).Append(' ');
            }

            var mebleg = PulOku(sb.ToString());

            if (mebleg is null || mebleg <= 0m)
            {
                return;
            }

            b.Mohletler.Add(new KreditIdxalMohlet
            {
                Tarix = tarix ?? default,
                Mebleg = mebleg.Value,
                Qeyd = deyer.Trim()
            });
        }

        /// <summary>Tanınmış açarı bloka yazır (normalizə edilmiş açar ✓).</summary>
        private static void SetKey(KreditIdxalBloku b, string a, string? deyer)
        {
            var d = (deyer ?? string.Empty).Trim();

            switch (a)
            {
                case "masin" or "marka" or "model" or "avtomobil" or "avto" or "car":
                    b.Masin = string.IsNullOrWhiteSpace(b.Masin) ? d : b.Masin + " " + d;
                    break;

                case "nomre" or "qeydiyyat" or "dovlet" or "dovlet nomresi" or "nisan" or "plate":
                    b.Nomre = d;
                    break;

                case "il" or "ili" or "year":
                    b.Il = (int)(PulOku(d) ?? 0m);
                    break;

                case "musteri" or "alici" or "muşteri" or "customer":
                    b.Musteri = d;
                    break;

                case "muqavile" or "müqavilə" or "muqavile nomresi" or "muqavile no" or "muqavile nomre"
                    or "sozlesme" or "sozlesme no" or "contract" or "contract no":
                    b.Muqavile = d;
                    break;

                case "kok" or "borc" or "kredit" or "kreditlesdirilen" or "esas borc" or "principal":
                    b.Kok = PulOku(d) ?? 0m;
                    break;

                case "ilkin" or "ilkin odenis" or "beh" or "avans" or "pesin":
                    b.Ilkin = PulOku(d) ?? 0m;
                    break;

                case "umumi" or "umumi mebleg" or "qiymet" or "toplam" or "cemi" or "odenilecek":
                    b.Umumi = PulOku(d) ?? 0m;
                    break;

                case "faiz" or "faiz derecesi" or "interest":
                    b.Faiz = PulOku(d) ?? 0m;
                    break;

                case "muddet" or "ay" or "muddet ay" or "months":
                    b.Muddet = (int)(PulOku(d) ?? 0m);
                    break;

                case "aylik" or "aylik odenis" or "aylik taksit" or "taksit"
                    or "ayliq" or "ayliq odenis" or "ayliq taksit" or "ayliq odenish":
                    b.Aylik = PulOku(d) ?? 0m;
                    break;

                case "baslama" or "baslama tarixi" or "tarix" or "start":
                    b.Baslama = TarixOku(d);
                    break;

                case "maya" or "maya deyeri" or "maya qiymeti" or "cost":
                    b.Maya = PulOku(d) ?? 0m;
                    break;

                // 🛠️ ÜMUMİ MAYA — KÖK bazası ✓✓✓
                case "umumi maya" or "umumi maya deyeri" or "umumi maya qiymeti":
                    b.UmumiMaya = PulOku(d) ?? 0m;

                    if (b.UmumiMaya > 0m)
                    {
                        b.Maya = b.UmumiMaya;
                    }

                    break;

                // 🚘 Avtomobilin ALIŞ qiyməti ✓
                case "alis qiymeti" or "alis qiymet" or "alis" or "purchase" or "alis meblegi":
                    b.AlisQiymeti = PulOku(d) ?? 0m;
                    break;

                // ➕ ƏLAVƏ XƏRCLƏR ✓
                case "elave xercler" or "elave xerc" or "xercler" or "elave xercleri" or "extra":
                    b.ElaveXercler = PulOku(d) ?? 0m;
                    break;

                // 💳 STANDART AYLIO ÖDƏNİŞ (qrafik üçün ✓)
                case "standart aylik odenis" or "standart aylik" or "standart ayliq odenis"
                    or "standart ayliq" or "standart odenis" or "aylik odenis standart":
                    b.Aylik = PulOku(d) ?? 0m;
                    break;

                case "satis" or "satis qiymeti" or "satis qiymet" or "sale":
                    b.SatisQiymeti = PulOku(d) ?? 0m;
                    break;

                case "terefdaslar" or "terefdas" or "bolgu" or "paylar" or "partners":
                    b.Terefdaslar = TerefdasOku(d);
                    break;

                case "qeyd" or "note" or "izah" or "comment":
                    b.Qeyd = d;
                    break;

                case "ilkin odenis mohleti" or "ilkin mohleti" or "ilkin odenis mohlet"
                    or "beh mohleti" or "ilkin odenis mohletleri" or "mohlet":
                    MohletOku(b, d);
                    break;

                case "odenis gun araligi" or "odenis gunu" or "odenis araligi"
                    or "odenis gunleri" or "odenis gun":
                    b.OdenisGunAraligi = d;
                    break;
            }
        }

        // ====================================================================
        //  📊 ÖNİZLƏMƏ  — aylıq cədvəl (maya · mənfəət · paylar ✓)
        // ====================================================================

        /// <summary>
        /// Bir blokdan önizləmə kartını qurur: çatışmayan dəyərləri (faiz · kök · aylıq)
        /// avtomatik tapır və aylıq cədvəli hesablayır ✓✓✓
        /// </summary>
        private static KreditIdxalKart? KartQur(KreditIdxalBloku blok, out string? xeta)
        {
            xeta = null;

            // 📅 İSTİFADƏÇİNİN VERDİYİ DƏQİQ QRAFİK varsa — maya və paylar
            //    HESABLANMIR ✗, olduğu kimi götürülür ✓✓✓
            if (blok.Qrafik.Count > 0)
            {
                return QrafikdenKart(blok, out xeta);
            }

            var odenisler = blok.Odenisler.Where(o => o.Mebleg > 0m).OrderBy(o => o.Tarix).ToList();

            var baslama = blok.Baslama
                ?? (odenisler.Count > 0 ? odenisler[0].Tarix : (DateTime?)null);

            // ---- Müddət ----
            var muddet = blok.Muddet;

            if (muddet <= 0 && baslama is DateTime b0 && odenisler.Count > 0)
            {
                muddet = odenisler.Max(o => TaksitNo(o.Tarix, b0));
            }

            if (muddet <= 0 && blok.Kok > 0m && blok.Aylik > 0m)
            {
                muddet = (int)Math.Ceiling(blok.Kok / blok.Aylik);
            }

            // ---- KÖK (kreditləşdirilən) ----
            var kok = blok.Kok;

            if (kok <= 0m && blok.SatisQiymeti > 0m)
            {
                kok = Math.Max(0m, blok.SatisQiymeti - blok.Ilkin);
            }

            if (kok <= 0m && blok.Umumi > 0m && blok.Faiz > 0m)
            {
                kok = Math.Round(blok.Umumi / (1m + blok.Faiz / 100m), 2);
            }

            // ---- FAİZ ----
            var faiz = blok.Faiz;

            if (faiz <= 0m && kok > 0m && blok.Umumi > kok)
            {
                faiz = Math.Round((blok.Umumi / kok - 1m) * 100m, 4);
            }

            if (faiz <= 0m && kok > 0m && muddet > 0 && blok.Aylik > 0m && blok.Aylik * muddet > kok)
            {
                faiz = Math.Round((blok.Aylik * muddet / kok - 1m) * 100m, 4);
            }

            // ---- Validasiya ----
            if (kok <= 0m)
            {
                xeta = "Kök (kreditləşdirilən borc) tapılmadı — «Kök», yaxud «Satış qiyməti» + «İlkin» yazın.";
                return null;
            }

            if (muddet <= 0)
            {
                xeta = "Müddət (ay) tapılmadı — «Müddət: 30» kimi yazın.";
                return null;
            }

            baslama ??= DateTime.Today;

            // ---- Müvəqqəti kredit obyekti (düsturlar TƏKRARLANMASIN ✓✓✓) ----
            var gecici = new Credit
            {
                Mebleg = kok + blok.Ilkin,
                IlkinOdenis = blok.Ilkin,
                FaizDerecesi = faiz,
                MuddetAy = muddet,
                AylıqOdenis = blok.Aylik,
                BaslamaTarixi = baslama.Value
            };

            var aylik = blok.Aylik > 0m
                ? blok.Aylik
                : Math.Round(gecici.KreditQiymeti / muddet, 2);

            if (aylik <= 0m)
            {
                xeta = "Aylıq ödəniş hesablanmadı — «Aylıq» yazın.";
                return null;
            }

            gecici.AylıqOdenis = aylik;

            return SetirleriQur(blok, gecici, kok, faiz, aylik, muddet, baslama.Value, odenisler);
        }

        /// <summary>
        /// 📅 İstifadəçinin verdiyi DƏQİQ qrafikdən cədvəl qurur ✓✓✓
        /// <para>Maya və hər tərəfdaşın payı <b>OLDUĞU KİMİ</b> götürülür — hesablanmır ✓</para>
        /// </summary>
        private static KreditIdxalKart? QrafikdenKart(KreditIdxalBloku blok, out string? xeta)
        {
            xeta = null;

            var qrafik = blok.Qrafik.OrderBy(q => q.Tarix).ToList();
            var baslama = blok.Baslama ?? qrafik[0].Tarix;

            // 👥 tərəfdaş adları — sütun sırası ilə ✓
            var adlar = new List<string>();

            foreach (var q in qrafik)
            {
                foreach (var p in q.Paylar)
                {
                    if (!adlar.Any(a => string.Equals(a, p.Ad, StringComparison.OrdinalIgnoreCase)))
                    {
                        adlar.Add(p.Ad);
                    }
                }
            }

            // ⏳ MÖHLƏT ÖDƏNİŞLƏRİ — kredit taksiti DEYİL ✗✓✓ (ayrı hesablanır)
            var taksitler = qrafik.Where(q => !q.MohletOdenisi).ToList();

            var mayaCemi = taksitler.Sum(q => q.Maya);
            var taksitCemi = taksitler.Sum(q => q.Odenis);

            // ---- KÖK: «Kök» → (Satış Qiyməti − İlkin) → qrafik maya cəmi → ümumi maya ----
            var kok = blok.Kok;

            if (kok <= 0m && blok.SatisQiymeti > 0m && blok.SatisQiymeti > blok.Ilkin)
            {
                kok = blok.SatisQiymeti - blok.Ilkin;
            }

            if (kok <= 0m && mayaCemi > 0m) kok = mayaCemi;
            if (kok <= 0m && blok.UmumiMaya > 0m) kok = blok.UmumiMaya;

            if (kok <= 0m)
            {
                xeta = "Kök müəyyən edilmədi — «Satış Qiyməti» + «İlkin Ödəniş» və ya «Kök» yazın.";
                return null;
            }

            var muddet = blok.Muddet > 0 ? blok.Muddet : Math.Max(1, taksitler.Count);

            // ================================================================
            //  💰 KREDİTİN ÜMUMİ DƏYƏRİ (HƏDƏF)
            // -----------------------------------------------------------------
            //  ★ «Aylıq × Müddət» VƏ ödənilmiş taksitlərin cəmi — BÖYÜYÜ ★ ✓✓✓
            //  • qrafik TAMDIRSA      → cəmi hədəf olur ✓ (artıq ödənişlər də SAYILIR ✓)
            //  • qrafik QİSMƏN (cari) → «aylıq × müddət» hədəf olur ✓✓✓
            //    → kredit YALNIZ tam ödəniləndə «Bağlı» olur ✗ (vaxtından tez YOX ✗)
            // ================================================================
            var hedef = Math.Max(blok.Aylik * muddet, taksitCemi);

            var faiz = blok.Faiz;

            if (faiz <= 0m && hedef > kok)
            {
                faiz = Math.Round((hedef / kok - 1m) * 100m, 4);
            }

            var gecici = new Credit
            {
                Mebleg = kok + blok.Ilkin,
                IlkinOdenis = blok.Ilkin,
                FaizDerecesi = faiz,
                MuddetAy = muddet,
                AylıqOdenis = blok.Aylik,
                BaslamaTarixi = baslama
            };

            var aylik = blok.Aylik > 0m
                ? blok.Aylik
                : Math.Round(gecici.KreditQiymeti / Math.Max(1, muddet), 2);

            gecici.AylıqOdenis = aylik;

            // ---- SƏTİRLƏR (verildiyi kimi ✓) ----
            var setirler = new List<KreditIdxalSetir>();
            var odenilmisKok = 0m;
            var odenilmisCemi = 0m;
            var bugun = DateTime.Today;
            var taksitNo = 0;

            for (var i = 0; i < qrafik.Count; i++)
            {
                var q = qrafik[i];
                var odenilib = q.Tarix.Date <= bugun;

                // ⏳ İLKİN MÖHLƏTİN ÖDƏNİŞİ — kredit taksiti DEYİL ✗✓✓
                //    (kreditin ödəniş cəminə DAXİL EDİLMİR ✗ → vaxtından tez bağlanmır ✓)
                if (q.MohletOdenisi)
                {
                    setirler.Add(new KreditIdxalSetir
                    {
                        Kredit = blok.Basliq,
                        Ay = 0,
                        Nov = "⏳ Möhlət ödənişi",
                        Tarix = q.Tarix,
                        Odenis = q.Odenis,
                        Bolgu = string.IsNullOrWhiteSpace(q.Qeyd)
                            ? "İlkin ödənişə möhlətin ödənişi"
                            : q.Qeyd,
                        Odenilib = odenilib
                    });

                    continue;
                }

                taksitNo++;
                odenilmisKok += q.Maya;

                if (odenilib)
                {
                    odenilmisCemi += q.Odenis;
                }

                var deyerler = adlar
                    .Select(a => new KreditIdxalPay
                    {
                        Ad = a,
                        Mebleg = q.Paylar
                            .FirstOrDefault(p => string.Equals(p.Ad, a, StringComparison.OrdinalIgnoreCase))
                            ?.Mebleg ?? 0m
                    })
                    .ToList();

                setirler.Add(new KreditIdxalSetir
                {
                    Kredit = blok.Basliq,
                    Ay = taksitNo,
                    Nov = "Taksit",
                    Tarix = q.Tarix,
                    Odenis = q.Odenis,
                    Maya = q.Maya,
                    Menfeet = q.Menfeet,
                    Kar1 = deyerler.ElementAtOrDefault(0)?.Mebleg ?? 0m,
                    Kar2 = deyerler.ElementAtOrDefault(1)?.Mebleg ?? 0m,
                    Kar3 = deyerler.ElementAtOrDefault(2)?.Mebleg ?? 0m,
                    Kar4 = deyerler.ElementAtOrDefault(3)?.Mebleg ?? 0m,
                    Kar1Ad = adlar.ElementAtOrDefault(0) ?? string.Empty,
                    Kar2Ad = adlar.ElementAtOrDefault(1) ?? string.Empty,
                    Kar3Ad = adlar.ElementAtOrDefault(2) ?? string.Empty,
                    Kar4Ad = adlar.ElementAtOrDefault(3) ?? string.Empty,
                    Bolgu = string.Join(
                        " · ",
                        deyerler.Where(x => x.Mebleg > 0m).Select(x => $"{x.Ad} {x.Mebleg:N2} ₼")),
                    QaliqKok = Math.Max(0m, kok - odenilmisKok),
                    Odenilib = odenilib
                });
            }

            CerimeleriEkle(blok, setirler, out var cerimeCemi, out var cerimeOdenilmis);
            var mohletCemi = MohletleriEkle(blok, setirler, baslama);

            var umumi = gecici.KreditQiymeti;
            var qaliqNisye = Math.Max(0m, umumi + cerimeCemi - odenilmisCemi - cerimeOdenilmis);

            return new KreditIdxalKart
            {
                Basliq = blok.Basliq,
                Kok = kok,
                Ilkin = blok.Ilkin,
                Umumi = umumi,
                Faiz = faiz,
                Aylik = aylik,
                Muddet = muddet,
                Maya = blok.Maya,
                SatisQiymeti = blok.SatisQiymeti,
                Odenilmis = odenilmisCemi,
                CerimeCemi = cerimeCemi,
                MohletCemi = mohletCemi,
                OdenisGunAraligi = blok.OdenisGunAraligi,
                QaliqNisye = qaliqNisye,
                Setirler = setirler,
                Kar1Ad = adlar.ElementAtOrDefault(0) ?? string.Empty,
                Kar2Ad = adlar.ElementAtOrDefault(1) ?? string.Empty,
                Kar3Ad = adlar.ElementAtOrDefault(2) ?? string.Empty,
                Kar4Ad = adlar.ElementAtOrDefault(3) ?? string.Empty,
                Qrafikden = true,
                BolguXulase = string.Join(" · ", adlar)
            };
        }

        /// <summary>Aylıq ödəniş cədvəlini qurur (maya · mənfəət · tərəfdaş payları ✓).</summary>
        private static KreditIdxalKart SetirleriQur(
            KreditIdxalBloku blok,
            Credit gecici,
            decimal kok,
            decimal faiz,
            decimal aylik,
            int muddet,
            DateTime baslama,
            List<KreditIdxalOdenis> odenisler)
        {
            // Ödənişləri taksit nömrəsinə görə qruplaşdırırıq.
            var xerite = new Dictionary<int, List<KreditIdxalOdenis>>();
            var tarixsiz = new List<KreditIdxalOdenis>();

            foreach (var o in odenisler)
            {
                if (o.Tarix == default)
                {
                    tarixsiz.Add(o);
                    continue;
                }

                var no = TaksitNo(o.Tarix, baslama);

                if (!xerite.TryGetValue(no, out var liste))
                {
                    liste = new List<KreditIdxalOdenis>();
                    xerite[no] = liste;
                }

                liste.Add(o);
            }

            // Tarixi olmayan ödənişlər — boş taksitlərə ardıcıl yerləşdirilir ✓.
            var bosNo = 1;

            foreach (var o in tarixsiz)
            {
                while (xerite.ContainsKey(bosNo))
                {
                    bosNo++;
                }

                xerite[bosNo] = new List<KreditIdxalOdenis> { o };
            }

            var setirSayi = muddet;

            if (xerite.Count > 0)
            {
                setirSayi = Math.Max(setirSayi, xerite.Keys.Max());
            }

            var setirler = new List<KreditIdxalSetir>();
            var odenilmisKok = 0m;
            var odenilmisCemi = 0m;

            for (var i = 1; i <= setirSayi; i++)
            {
                var planTarix = baslama.AddMonths(i - 1);
                var ode = xerite.TryGetValue(i, out var liste) ? liste : null;

                var mebleg = ode is not null ? ode.Sum(x => x.Mebleg) : aylik;

                var tarix = ode is not null ? ode.Max(x => x.Tarix) : planTarix;

                if (tarix == default)
                {
                    tarix = planTarix;
                }

                // ★ Vahid düsturlar: Credit.OdenisKokPayi / OdenisMenfeetBazasi ✓✓✓
                var kokPayi = gecici.OdenisKokPayi(mebleg, odenilmisKok);
                var menfeet = gecici.OdenisMenfeetBazasi(mebleg, odenilmisKok);

                var paylar = PayRows(blok);
                PartnerMath.Distribute(menfeet, paylar);

                // KAR₁ / KAR₂ — ƏN ÇOX pay alan iki tərəfdaş ✓✓✓
                var ikili = paylar
                    .OrderByDescending(p => p.Mebleg)
                    .ThenBy(p => p.Sira)
                    .Take(2)
                    .ToList();

                odenilmisKok += kokPayi;

                if (ode is not null)
                {
                    odenilmisCemi += mebleg;
                }

                setirler.Add(new KreditIdxalSetir
                {
                    Kredit = blok.Basliq,
                    Ay = i,
                    Tarix = tarix,
                    Odenis = mebleg,
                    Maya = kokPayi,
                    Menfeet = menfeet,
                    Kar1 = ikili.Count > 0 ? ikili[0].Mebleg : 0m,
                    Kar2 = ikili.Count > 1 ? ikili[1].Mebleg : 0m,
                    Kar1Ad = ikili.Count > 0 ? ikili[0].Terefdas : string.Empty,
                    Kar2Ad = ikili.Count > 1 ? ikili[1].Terefdas : string.Empty,
                    Bolgu = string.Join(
                        " · ",
                        paylar.Where(p => p.Mebleg > 0m).Select(p => $"{p.Terefdas} {p.Mebleg:N2} ₼")),   // Mebleg ✓
                    QaliqKok = Math.Max(0m, kok - odenilmisKok),
                    Odenilib = ode is not null
                });
            }

            // ---- ⚖️ GECİKMƏ CƏRİMƏLƏRİ + ⏳ MÖHLƏTLƏR ----
            CerimeleriEkle(blok, setirler, out var cerimeCemi, out var cerimeOdenilmis);
            var mohletCemi = MohletleriEkle(blok, setirler, baslama);

            var payModel = PayRows(blok);
            var umumi = gecici.KreditQiymeti;
            var ilkTaksit = setirler.FirstOrDefault(s => s.Nov == "Taksit");

            // 💰 QALIQ NİSYƏ = ümumi qiymət + cərimələr − (ödənişlər + ödənilmiş cərimələr) ✓
            var qaliqNisye = Math.Max(0m, umumi + cerimeCemi - odenilmisCemi - cerimeOdenilmis);

            return new KreditIdxalKart
            {
                Basliq = blok.Basliq,
                Kok = kok,
                Ilkin = blok.Ilkin,
                Umumi = umumi,
                Faiz = faiz,
                Aylik = aylik,
                Muddet = muddet,
                Maya = blok.Maya,
                SatisQiymeti = blok.SatisQiymeti,
                Odenilmis = odenilmisCemi,
                CerimeCemi = cerimeCemi,
                MohletCemi = mohletCemi,
                OdenisGunAraligi = blok.OdenisGunAraligi,
                QaliqNisye = qaliqNisye,
                Setirler = setirler,
                Kar1Ad = ilkTaksit?.Kar1Ad is { Length: > 0 } k1
                    ? k1
                    : payModel.Count > 0 ? payModel[0].Terefdas : string.Empty,
                Kar2Ad = ilkTaksit?.Kar2Ad is { Length: > 0 } k2
                    ? k2
                    : payModel.Count > 1 ? payModel[1].Terefdas : string.Empty,
                BolguXulase = ilkTaksit is not null && ilkTaksit.Bolgu.Length > 0
                    ? ilkTaksit.Bolgu
                    : string.Empty
            };
        }

        /// <summary>
        /// ⏳ <b>İLKİN ÖDƏNİŞ MÖHLƏTİNİ «ÖDƏNİLDİ» EDİR</b> ✓✓✓
        /// <para>
        /// Qrafikdəki «İlkin ödəniş möhlətinin ödənilən pulu» sətri → kredit
        /// taksiti DEYİL ✗, möhlətin ödənişidir ✓. Uyğun <see cref="OdenisMohlet"/>
        /// tapılır və <c>SetOdenildiAsync</c> ilə bağlanır ✓ — beləliklə pul
        /// <b>kassaya düşür</b> ✓, kredit balansı isə ARTMI <b>şişmir</b> ✗✓✓
        /// </para>
        /// </summary>
        private async Task MohletiOdenildiAsync(
            List<OdenisMohlet> mohletler,
            KreditIdxalQrafikSetir q,
            KreditIdxalNeticesi netice,
            CancellationToken cancellationToken)
        {
            // Əvvəlcə MƏBLƏĞƏ görə uyğun möhləti axtarırıq ✓
            var hedef = mohletler.FirstOrDefault(m =>
                !m.Odenilib && Math.Abs(m.Mebleg - q.Odenis) < 0.01m);

            // Tapılmadısa — ilk ödənilməmiş möhlət ✓
            hedef ??= mohletler.FirstOrDefault(m => !m.Odenilib);

            if (hedef is null || hedef.Id <= 0)
            {
                // Möhlət yazılmayıbsa → adi ödəniş kimi qeyd olunur ✓ (yuxarıda ✗)
                netice.Setirler.Add(
                    $"   ℹ️ «{q.Qeyd}» ({Pul(q.Odenis)}) — uyğun ilkin möhlət tapılmadı, ödəniş yazılmadı ✗");
                return;
            }

            var oldu = await _mohletler.SetOdenildiAsync(hedef.Id, true, q.Tarix, cancellationToken);

            if (oldu)
            {
                netice.MohletOdenisSayi++;

                netice.Setirler.Add(
                    $"   ⏳ İlkin ödəniş möhləti ÖDƏNİLDİ ✓ — {Pul(hedef.Mebleg)} · {q.Tarix:dd.MM.yyyy} " +
                    "(pul kassaya düşdü ✓ · kredit balansına SAYILMADI ✗)");
            }
        }

        /// <summary>⚖️ Cərimə sətirlərini əlavə edir (qalıq payçıları arasında yarı-yarıya ✓).</summary>
        private static void CerimeleriEkle(
            KreditIdxalBloku blok,
            List<KreditIdxalSetir> setirler,
            out decimal cerimeCemi,
            out decimal cerimeOdenilmis)
        {
            cerimeCemi = 0m;
            cerimeOdenilmis = 0m;

            foreach (var g in blok.Gecikmeler.OrderBy(x => x.Tarix))
            {
                cerimeCemi += g.Mebleg;

                if (g.Odenilib)
                {
                    cerimeOdenilmis += g.Mebleg;
                }

                // ⚠️ CƏRİMƏ TƏRƏFDAŞLARA BÖLÜNMÜR ✗✓✓ (v6.2.28) —
                //    pul YALNIZ «💵 Kassa»ya (GƏLİRƏ) yazılır ✓
                setirler.Add(new KreditIdxalSetir
                {
                    Kredit = blok.Basliq,
                    Ay = 0,
                    Nov = "⚖️ Cərimə",
                    Tarix = g.Tarix,
                    Odenis = g.Mebleg,
                    Bolgu = "💵 KASSA (gəlir) — tərəfdaşlara BÖLÜNMÜR ✗",
                    Odenilib = g.Odenilib
                });
            }
        }

        /// <summary>⏳ Möhlət sətirlərini əlavə edir və cəmini qaytarır ✓.</summary>
        private static decimal MohletleriEkle(
            KreditIdxalBloku blok,
            List<KreditIdxalSetir> setirler,
            DateTime baslama)
        {
            var cemi = 0m;

            foreach (var m in blok.Mohletler.OrderBy(x => x.Tarix))
            {
                cemi += m.Mebleg;

                // ⏳ Bu möhlət qrafikdə ÖDƏNİLİBSƏ → «ödənilib» göstərilir ✓✓✓
                var odenilib = blok.Qrafik.Any(q =>
                    q.MohletOdenisi && Math.Abs(q.Odenis - m.Mebleg) < 0.01m);

                setirler.Add(new KreditIdxalSetir
                {
                    Kredit = blok.Basliq,
                    Ay = 0,
                    Nov = "⏳ Möhlət",
                    Tarix = m.Tarix == default ? baslama : m.Tarix,
                    Odenis = m.Mebleg,
                    Bolgu = string.IsNullOrWhiteSpace(m.Qeyd) ? "İlkin ödənişə möhlət" : m.Qeyd,
                    Odenilib = odenilib
                });
            }

            return cemi;
        }

        /// <summary>Blokun tərəfdaş sətirləri (boşdursa standart qalıq payçıları ✓).</summary>
        private static List<PartnerShare> PayRows(KreditIdxalBloku blok)
        {
            if (blok.Terefdaslar.Count == 0)
            {
                return PartnerMath.CreateDefaultRows(yalnizQaligPaycilari: true);
            }

            var netice = new List<PartnerShare>();
            var sira = 0;

            foreach (var t in blok.Terefdaslar)
            {
                netice.Add(new PartnerShare
                {
                    Terefdas = t.Ad,
                    Faiz = t.Faiz,
                    QaligPayi = t.QaligPayi,
                    Aktiv = true,
                    Sira = sira++
                });
            }

            return netice;
        }

        // ====================================================================
        //  🔤 AŞAĞI SƏVİYYƏLİ KÖMƏKÇİLƏR
        // ====================================================================

        /// <summary>
        /// Açar mətnini müqayisə üçün sadələşdirir: kiçik hərf + Azərbaycan
        /// hərfləri ASCII-yə çevrilir («İlkin ödəniş» → «ilkin odenis» ✓).
        /// </summary>
        private static string AcarNorm(string metn)
        {
            // Mötərizə içi atılır: «İlkin Ödəniş (Beh)» → «İlkin Ödəniş» ✓
            var temiz = metn;

            while (true)
            {
                var a = temiz.IndexOf('(');
                var b = a >= 0 ? temiz.IndexOf(')', a) : -1;

                if (a < 0 || b < 0)
                {
                    break;
                }

                temiz = temiz.Remove(a, b - a + 1);
            }

            var sb = new StringBuilder();

            foreach (var xam in temiz.Trim())
            {
                var ch = char.ToLowerInvariant(xam);

                sb.Append(ch switch
                {
                    // ⚠ «İ» (U+0130) .NET-də ToLowerInvariant ilə DÜŞMÜR ✗ →
                    //   açıq şəkildə 'i'-yə çevrilir ✓✓✓ (əks halda «İlkin: 6000» tanınmır ✗)
                    'İ' => 'i',
                    'ı' => 'i',
                    'ə' or 'Ə' or 'Ә' => 'e',
                    'ö' => 'o',
                    'ü' => 'u',
                    'ç' => 'c',
                    'ş' => 's',
                    'ğ' => 'g',
                    'â' => 'a',
                    '\u0307' => '\0',   // «İ» kiçildikdə yaranan nöqtə atılır
                    _ => ch
                });
            }

            var netice = sb.ToString().Replace("\0", string.Empty).Trim();

            while (netice.Contains("  ", StringComparison.Ordinal))
            {
                netice = netice.Replace("  ", " ", StringComparison.Ordinal);
            }

            return netice;
        }

        /// <summary>
        /// Pul mətnini rəqəmə çevirir — «22 200,50» · «22200.50» · «12,000»
        /// formatlarının hamısını başa düşür ✓.
        /// </summary>
        private static decimal? PulOku(string? metn)
        {
            if (string.IsNullOrWhiteSpace(metn))
            {
                return null;
            }

            var t = metn.Trim()
                .Replace("₼", string.Empty)
                .Replace("\u00A0", string.Empty);

            var sb = new StringBuilder();

            foreach (var ch in t)
            {
                if (char.IsDigit(ch) || ch is ',' or '.' or '-' or '+')
                {
                    sb.Append(ch);
                }
            }

            t = sb.ToString();

            if (t.Length == 0)
            {
                return null;
            }

            var sonVergul = t.LastIndexOf(',');
            var sonNogte = t.LastIndexOf('.');
            string tamHisse;
            var kesrHisse = string.Empty;

            if (sonVergul >= 0 && sonNogte >= 0)
            {
                if (sonVergul > sonNogte)
                {
                    tamHisse = t[..sonVergul].Replace(".", string.Empty);
                    kesrHisse = t[(sonVergul + 1)..];
                }
                else
                {
                    tamHisse = t[..sonNogte].Replace(",", string.Empty);
                    kesrHisse = t[(sonNogte + 1)..];
                }
            }
            else if (sonVergul >= 0)
            {
                var kesr = t[(sonVergul + 1)..];

                if (kesr.Length == 3 && t.Length > 4)
                {
                    tamHisse = t.Replace(",", string.Empty);
                }
                else
                {
                    tamHisse = t[..sonVergul];
                    kesrHisse = kesr;
                }
            }
            else if (sonNogte >= 0 && t.IndexOf('.') == sonNogte)
            {
                var kesr = t[(sonNogte + 1)..];

                if (kesr.Length == 3)
                {
                    tamHisse = t.Replace(".", string.Empty);
                }
                else
                {
                    tamHisse = t[..sonNogte];
                    kesrHisse = kesr;
                }
            }
            else
            {
                tamHisse = t.Replace(".", string.Empty).Replace(",", string.Empty);
            }

            if (tamHisse.Length == 0 || tamHisse is "-" or "+")
            {
                tamHisse = "0";
            }

            var tam = tamHisse + (kesrHisse.Length > 0 ? "." + kesrHisse : string.Empty);

            return decimal.TryParse(
                tam,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var deyer)
                ? deyer
                : null;
        }

        /// <summary>«09.07.2023» kimi tarixi oxuyur (oxunmazsa <c>null</c> ✓).</summary>
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

        /// <summary>«03.03.2024 654» kimi ödəniş sətrini oxuyur ✓.</summary>
        private static KreditIdxalOdenis? OdenisOku(string setir)
        {
            var s = setir.Trim().TrimStart('-', '•', '*', '·', '\t').Trim();

            // Format: [tarix] [ayırıcı] məbləğ  ·  məs. «03.03.2024 654» ✓
            var bosluq = s.IndexOfAny(new[] { ' ', '\t', '-', ':', '=', '→' });

            if (bosluq > 0)
            {
                var sol = s[..bosluq].Trim().TrimEnd(':');
                var sag = s[(bosluq + 1)..].Trim().TrimStart('-', ':', '=', '→').Trim();

                var tarix = TarixOku(sol);

                if (tarix is not null)
                {
                    var mebleg = PulOku(sag);

                    return mebleg is > 0m
                        ? new KreditIdxalOdenis { Tarix = tarix.Value, Mebleg = mebleg.Value }
                        : null;
                }
            }

            // Yalnız məbləğ (tarix yoxdur) → tarix boş qalır, sıra ilə yer tapır ✓
            var yalniz = PulOku(s);

            return yalniz is > 0m
                ? new KreditIdxalOdenis { Tarix = default, Mebleg = yalniz.Value }
                : null;
        }

        /// <summary>«Asif, Musa» və ya «Zaur 6%, Asif qalıq» sətrini oxuyur ✓.</summary>
        private static List<KreditIdxalTerefdas> TerefdasOku(string? metn)
        {
            var netice = new List<KreditIdxalTerefdas>();

            if (string.IsNullOrWhiteSpace(metn))
            {
                return netice;
            }

            foreach (var parca in metn.Split(new[] { ',', ';', '|', '·', '\n' },
                                             StringSplitOptions.RemoveEmptyEntries))
            {
                var s = parca.Trim();

                if (s.Length == 0)
                {
                    continue;
                }

                var norm = AcarNorm(s);

                var qalig = norm.Contains("qalig", StringComparison.Ordinal)
                            || norm.EndsWith(" q", StringComparison.Ordinal)
                            || norm == "q";

                var faiz = PulOku(s);
                var ad = s;

                // Rəqəm, «%», «faiz», «qalıq» sözləri addan təmizlənir ✓
                foreach (var syz in new[] { "faiz", "%", "qalıq", "qalig", "pay", "payı", "payi" })
                {
                    ad = ad.Replace(syz, string.Empty, StringComparison.OrdinalIgnoreCase);
                }

                if (faiz is not null)
                {
                    ad = ad.Replace(faiz.Value.ToString("0.##", CultureInfo.InvariantCulture), string.Empty);
                    ad = ad.Replace(faiz.Value.ToString("0", CultureInfo.InvariantCulture), string.Empty);
                }

                ad = ad.Trim(' ', '-', ':', ',', ';', '|', '·');

                if (ad.Length == 0)
                {
                    continue;
                }

                netice.Add(new KreditIdxalTerefdas
                {
                    Ad = ad,
                    Faiz = faiz ?? 0m,
                    QaligPayi = qalig || faiz is null
                });
            }

            return netice;
        }

        /// <summary>Tarixə uyğun taksit nömrəsi (1-ci taksit = başlama ayı ✓).</summary>
        private static int TaksitNo(DateTime tarix, DateTime baslama)
        {
            var no = ((tarix.Year - baslama.Year) * 12) + (tarix.Month - baslama.Month) + 1;

            return no < 1 ? 1 : no;
        }

        /// <summary>«11 200,00 ₼» ✓.</summary>
        private static string Pul(decimal mebleg)
            => mebleg.ToString("N2", CultureInfo.InvariantCulture).Replace(",", " ") + " ₼";
    }
}
