using System.ComponentModel.DataAnnotations.Schema;

namespace EnterpriseAeroStudio.Models
{
    /// <summary>
    /// <b>💵 KASSA CƏDVƏLİNİN BİR SƏTRİ</b> — kassaya daxil olan / kassadan
    /// çıxan pulun vahid jurnal sətri ✓✓✓
    /// <para>
    /// Sətirlərin <b>BÖYÜK HİSSƏSİ AVTOMATİK</b> yaranır (bax:
    /// <see cref="Services.KassaHesabi"/>) ✓:
    /// </para>
    /// <list type="bullet">
    ///   <item>💰 satışların nağd/köçürmə hissəsi ✓ (barter hissəsi YOX ✗)</item>
    ///   <item>🏦 kredit ilkin ödənişləri (avanslar) ✓</item>
    ///   <item>💳 kredit ödənişləri ✓ (erkən bağlama ✓ ödənilmiş gecikmə ✓)</item>
    ///   <item>⏳ ödənilmiş möhlətlər ✓ (yalnız pul GƏLDİKDƏ ✓)</item>
    ///   <item>💸 BÜTÜN xərc qeydləri ✓ + kredit əlavə xərcləri ✓</item>
    ///   <item>👥 tərəfdaşlara FAKTİKİ VERİLƏN pul ✓ (hesablanan pay kassadan DƏRHAL çıxmır ✗)</item>
    ///   <item>✍ istifadəçinin əl ilə yazdığı kassa hərəkətləri ✓</item>
    /// </list>
    /// </summary>
    public sealed class KassaSetiri
    {
        /// <summary>Əməliyyatın tarixi.</summary>
        public DateTime Tarix { get; init; }

        /// <summary>Növ: <see cref="KassaHereket.NovDaxilolma"/> və ya <see cref="KassaHereket.NovXerc"/>.</summary>
        public string Nov { get; init; } = KassaHereket.NovDaxilolma;

        /// <summary>Qrup (kartlar üçün): «Satış» · «İlkin ödəniş» · «Kredit» · «Möhlət» · «Xərc» · «Tərəfdaş» · «Əl ilə».</summary>
        public string Qrup { get; init; } = string.Empty;

        /// <summary>Kateqoriya mətni: «💵 Nağd satış» · «💸 Mühərrik xərci» …</summary>
        public string Kateqoriya { get; init; } = string.Empty;

        /// <summary>İzahat — müqavilə №, müştəri, maşın, xərc təyinatı ✓.</summary>
        public string Aciqlama { get; init; } = string.Empty;

        /// <summary>Məbləğ (₼) — həmişə MÜSBƏT; istiqamət <see cref="Nov"/> ilə təyin olunur.</summary>
        public decimal Mebleg { get; init; }

        /// <summary>Ödəniş üsulu (Nağd · Kart / Köçürmə).</summary>
        public string OdenisUsulu { get; init; } = string.Empty;

        /// <summary>Daxilolmadırmı? (<c>false</c> = xərc)</summary>
        [NotMapped]
        public bool IsDaxilolma => Nov == KassaHereket.NovDaxilolma;

        /// <summary>İşarəli məbləğ: daxilolma <c>+</c>, xərc <c>−</c>.</summary>
        [NotMapped]
        public decimal IsareliMebleg => IsDaxilolma ? Mebleg : -Mebleg;

        [NotMapped]
        public string TarixMetni => Tarix.ToString("dd.MM.yyyy");

        /// <summary>İşarəli məbləğ mətni: «+1 500,00 ₼» · «−700,00 ₼».</summary>
        [NotMapped]
        public string MeblegMetni =>
            $"{(IsDaxilolma ? "+" : "−")}{Mebleg:N2} ₼";

        [NotMapped]
        public string NovMetni => IsDaxilolma ? "➕ Daxilolma" : "➖ Xərc";

        [NotMapped]
        public string MeblegRengi => IsDaxilolma ? "#34D399" : "#FB7185";

        /// <summary>Ay mətni: «Sentyabr 2026».</summary>
        [NotMapped]
        public string AyMetni => PartnerMonthlyRow.AyAdi(Tarix);

        /// <summary>Filtr/axtarış üçün birləşmiş mətn ✓.</summary>
        [NotMapped]
        public string AxtarisMetni =>
            $"{TarixMetni} {NovMetni} {Qrup} {Kateqoriya} {Aciqlama} {OdenisUsulu}".ToLowerInvariant();

        public override string ToString() =>
            $"{TarixMetni} · {Kateqoriya} · {(IsDaxilolma ? "+" : "−")}{Mebleg:N2} ₼";
    }

    /// <summary>
    /// <b>📊 KASSA HESABATI</b> — bir dövr üzrə kassanın tam açılışı ✓✓✓
    /// (<b>Kassa tabı</b> · <b>Maliyyə Paneli</b> · <b>Veb Dashboard</b> BUNU işlədir ✓)
    /// </summary>
    public sealed class KassaHesabati
    {
        /// <summary>Jurnal sətirləri (tarixə görə sıralı).</summary>
        public IReadOnlyList<KassaSetiri> Setirler { get; init; } = Array.Empty<KassaSetiri>();

        /// <summary>Dövrdən ƏVVƏLKİ kassa qalığıdır? (açılış qalığı ✓)</summary>
        public decimal AcilisQaligi { get; init; }

        // ---------- DAXİLOLMALAR (kassaya GƏLƏN pul) ----------
        public decimal SatisDaxilolma { get; init; }
        public decimal IlkinOdenisDaxilolma { get; init; }
        public decimal KreditDaxilolma { get; init; }
        public decimal MohletDaxilolma { get; init; }
        public decimal ElIleDaxilolma { get; init; }

        // ---------- XƏRCLƏR (kassadan ÇIXAN pul) ----------
        public decimal XercCemi { get; init; }
        public decimal KreditXerci { get; init; }

        /// <summary>
        /// 👥 Tərəfdaşlara <b>FAKTİKİ VERİLƏN</b> pul (₼) — yalnız
        /// «👥 Tərəfdaşlar» tabındaki <b>«pul ver»</b> əməliyyatları ✓✓✓
        /// <para>
        /// ⚠ <b>Hesablanmış bölgü DEYİL</b> ✗ — bax <see cref="TerefdasPayi"/>
        /// </para>
        /// </summary>
        public decimal TerefdasOdenilen { get; init; }

        public decimal ElIleXerci { get; init; }

        // ---------- 👥 TƏRƏFDAŞ PAYLARI (kassada QALAN pul ✓) ----------

        /// <summary>
        /// 👥 <b>Dövrdə HESABLANAN tərəfdaş payları</b> (₼) ✓✓✓ — maşının
        /// «xeyir»indən paylara ayrılan məbləğ.
        /// <para>
        /// ⚠ Bu pul <b>KASSADAN ÇIXMIR</b> ✗✓ — tərəfdaşa faktiki ödəniş
        /// edilənə qədər <b>kassada qalır</b> ✓ (yalnız məlumat üçündür ✓).
        /// Xərcə <b>DÜŞMÜR</b> ✗.
        /// </para>
        /// </summary>
        public decimal TerefdasPayi { get; init; }

        /// <summary>
        /// 👥 <b>Hələ tərəfdaşlara verilməli olan məbləğ</b> (₼) — bütün vaxt
        /// üzrə «hesablanmış paylar − faktiki verilən» ✓✓✓
        /// <para>
        /// Bu pul <b>HƏLƏ KASSADADIR</b> ✓ — amma tərəfdaşlara aiddir ✓.
        /// Ödəniş edildikdə kassa qalığı azalır ✓ (XƏRC olur ✓).
        /// </para>
        /// </summary>
        public decimal TerefdasVerilmeli { get; init; }

        /// <summary>Cəmi daxilolma (₼).</summary>
        public decimal Daxilolma =>
            SatisDaxilolma + IlkinOdenisDaxilolma + KreditDaxilolma + MohletDaxilolma + ElIleDaxilolma;

        /// <summary>Cəmi xərc (₼).</summary>
        public decimal Xerc => XercCemi + KreditXerci + TerefdasOdenilen + ElIleXerci;

        /// <summary>Dövr üzrə xalis kassa axını (₼).</summary>
        public decimal Xalis => Daxilolma - Xerc;

        /// <summary>Dövrün SONUNDA kassa qalığı (₼) — açılış + xalis ✓.</summary>
        public decimal Qaliq => AcilisQaligi + Xalis;

        /// <summary>
        /// ⚖️ <b>ÖZ qalıq</b> (₼) = kassa qalığı − tərəfdaşlara verilməli ✓✓✓
        /// <para>
        /// «Bu pul bizimdir, yoxsa tərəfdaşların?» sualına cavab ✓ —
        /// tərəfdaş payı ödənilməyənə qədər kassada qalır, amma onlara aiddir ✓
        /// </para>
        /// </summary>
        public decimal OzQaliq => Qaliq - TerefdasVerilmeli;

        /// <summary>Gözlənilən (hələ ödənilməmiş) möhlətlərin cəmi (₼) ✓.</summary>
        public decimal GozlenilenMohlet { get; init; }

        /// <summary>Vaxtı keçmiş möhlətlərin cəmi (₼) ⚠.</summary>
        public decimal GecikmisMohlet { get; init; }

        /// <summary>Gözlənilən möhlət varsa proqnoz qalıq (₼) ✓.</summary>
        public decimal ProqnozQaliq => Qaliq + GozlenilenMohlet;

        /// <summary>ÖZ proqnoz qalıq (₼) = öz qalıq + gözlənilən möhlətlər ✓.</summary>
        public decimal OzProqnozQaliq => ProqnozQaliq - TerefdasVerilmeli;

        /// <summary>Jurnal sətri sayı.</summary>
        public int SetirSayi => Setirler.Count;
    }

    /// <summary>
    /// 💵 <b>KASSA QRUPLARI ÜZRƏ CƏM</b> — «Satış» · «İlkin ödəniş» · «Kredit» ·
    /// «Möhlət» · «Xərc» · «Tərəfdaş» · «Əl ilə» ✓✓✓
    /// <para>
    /// <b>Kassa tabı</b> (masaüstü ✓) və <b>Veb Kassa səhifəsi</b> (✓) bu sətirləri göstərir —
    /// hansı mənbədən nə qədər pul girdi ✓ / çıxdı ✗ ✓
    /// </para>
    /// <para>
    /// ⚙️ Dəyərlər <see cref="KassaSetiri"/> sətirlərindən hesablanır ✓
    /// (düstur TƏKRARLANMIR ✗ — vahid mənbə <see cref="Services.KassaHesabi"/> ✓✓✓)
    /// </para>
    /// </summary>
    public sealed class KassaQrupCem
    {
        /// <summary>Qrup adı: «Satış» · «Xərc» · «Tərəfdaş» …</summary>
        public string Qrup { get; init; } = string.Empty;

        /// <summary>Qrup üzrə GƏLƏN pul (₼) ✓</summary>
        public decimal Gelir { get; init; }

        /// <summary>Qrup üzrə ÇIXAN pul (₼) ✗</summary>
        public decimal Xerc { get; init; }

        /// <summary>Qrupda sətir sayı.</summary>
        public int SetirSayi { get; init; }

        /// <summary>Xalis: gəlir − xərc (₼).</summary>
        [NotMapped]
        public decimal Xalis => Gelir - Xerc;

        /// <summary>Gəlir mətni: «+1 500,00 ₼» (sıfırdırsa «—»).</summary>
        [NotMapped]
        public string GelirMetni => Gelir <= 0m ? "—" : $"+{Gelir:N2} ₼";

        /// <summary>Xərc mətni: «−700,00 ₼» (sıfırdırsa «—»).</summary>
        [NotMapped]
        public string XercMetni => Xerc <= 0m ? "—" : $"−{Xerc:N2} ₼";

        public override string ToString() => $"{Qrup}: {GelirMetni} / {XercMetni} = {Xalis:N2} ₼";
    }
}
