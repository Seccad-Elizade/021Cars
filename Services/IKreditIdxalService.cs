namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🧾 <b>«KREDİT VƏ SKRİPT İDXAL PANELİ» XİDMƏTİ</b> ✓✓✓
    /// <list type="number">
    ///   <item><b>Təhlil</b> — toplu mətni bloklara bölür (maşın · kredit · ödənişlər ✓)</item>
    ///   <item><b>Önizləmə</b> — aylıq cədvəli hesablayır (maya · mənfəət · tərəfdaş payları ✓)</item>
    ///   <item><b>İdxal</b> — avtomobil · kredit · tərəfdaş bölgüsü · ödənişləri bazaya yazır ✓</item>
    /// </list>
    /// <para>
    /// Panel bazaya heç bir yazmadan ƏVVƏL nəticəni göstərir ✓ («Yoxla» düyməsi ✓) —
    /// yalnız təsdiqdən sonra yazır ✓✓✓
    /// </para>
    /// </summary>
    public interface IKreditIdxalService
    {
        /// <summary>📋 Nümunə mətn («Example» düyməsi üçün ✓).</summary>
        string Numune { get; }

        /// <summary>
        /// 🔎 <b>YALNIZ YOXLAYIR</b> — mətni təhlil edir, cədvəlləri hesablayır və
        /// nəticəni qaytarır ✓. <b>Bazaya heç nə yazılmır</b> ✗✓✓
        /// </summary>
        KreditIdxalNeticesi Yoxla(string metn);

        /// <summary>
        /// 📥 <b>İDXAL EDİR</b> — yoxlama ilə eyni təhlili aparır və uğurlu olduqda
        /// bütün məlumatları (avtomobil · kredit · tərəfdaş payları · ödənişlər)
        /// <b>bazaya yazır</b> ✓✓✓
        /// </summary>
        Task<KreditIdxalNeticesi> IcraEtAsync(
            string metn,
            bool maasinXeyiriniBol = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 📥 <b>MÖVCUD KREDİTƏ CƏDVƏL (SKRİPT) YAZIR</b> ✓✓✓ (v6.2.32)
        /// <para>
        /// ★ İstifadəçi tələbi: «Kredit Əlavə Gəlir/Xərc» tabında <b>«Növ» = Skript idxal</b>
        /// seçilir ✓ → müqavilə seçilir ✓ → aylıq qrafik cədvəli yapışdırılır ✓ →
        /// hər sətir <b>«Gəlir»</b> qeydi kimi bazaya yazılır ✓ (tərəfdaş payları ilə ✓).
        /// </para>
        /// <para>
        /// ⚠ Avtomobil/kredit <b>YARADILMIR</b> ✗ — yalnız ödənişlər əlavə olunur ✓.
        /// Tərəfdaş sütunları <c>Kar&lt;Ad&gt;</c> formasındadır ✓ — <b>yeni tərəfdaş</b>
        /// adı yazılsa da avtomatik tanınır ✓ (<c>KarTural</c> → <c>Tural</c> ✓).
        /// </para>
        /// <example>
        /// <code>
        /// # Tarix | Ödəniş | Maya | KarMusa | KarAsif | KarZaur | KarAsiman | KarEşqin |
        /// 02.03.2024 | 654.00 | 326.00 | 164.00 | 164.00 | 0.00 | 0.00 | 0.00 |
        /// </code>
        /// </example>
        /// </summary>
        Task<KreditIdxalNeticesi> CedveliKrediteYazAsync(
            int creditId,
            string metn,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 🔍 <b>5 BLOKLU «SKRİPT İDXALI» MƏTNİNİ YOXLAYIR</b> ✓✓✓ (v6.2.72)
        /// <para>
        /// Mətn <b>5 blokdan</b> ibarətdir (bloklar BOŞ SƏTİR ilə ayrılır ✓):
        /// ① Tarix · ② Məbləğ · ③ Maya · ④ Musa · ⑤ Asif
        /// </para>
        /// <para><b>Yoxlanış:</b> 5 blok · hər blokda eyni say · tarix formatı ·
        /// rəqəm formatı · <c>Maya + Musa + Asif = Məbləğ</c> · mənfi dəyər ·
        /// müqavilə seçilibmi ✓ · təkrar tarix isə yalnız <b>xəbərdarlıq</b> ⚠</para>
        /// <para>
        /// ⚠ <b>Bazaya heç nə yazılmır</b> ✗ — yalnız önizləmə ✓
        /// (yazma: <see cref="SkriptiKrediteYazAsync"/> ✓).
        /// </para>
        /// </summary>
        /// <param name="metn">Yapışdırılan 5 bloklu mətn ✓</param>
        /// <param name="creditId">Seçilmiş müqavilə (<c>null</c> → xəta ✗)</param>
        KreditSkriptYoxlama SkriptiYoxla(string? metn, int? creditId = null);

        /// <summary>
        /// 📥 <b>YOXLANILMIŞ SKRİPTİ SEÇİLMİŞ KREDİTƏ YAZIR</b> ✓✓✓ (v6.2.72)
        /// <para>
        /// Hər sətir üçün <see cref="Models.CreditTransaction"/> yaradılır ✓:
        /// <c>Nov = "Gəlir"</c> · <c>Tarix</c> · <c>Mebleg</c> · <c>Odenilib = true</c> ·
        /// <c>BolguBazasi = Mebleg − Maya</c> · <c>TerefdasBolguTetbiqOlunub = true</c> ·
        /// <c>TerefdasPaylari = Musa + Asif</c> (<c>Faiz = 0</c> ✓).
        /// </para>
        /// <para>
        /// ⚠ Əvvəlcə <see cref="SkriptiYoxla"/> çağırılır — xəta varsa
        /// <b>heç nə yazılmır</b> ✗✓✓
        /// </para>
        /// </summary>
        /// <param name="creditId">Müqavilənin İD-si ✓</param>
        /// <param name="metn">Yoxlanılacaq 5 bloklu mətn ✓</param>
        Task<KreditIdxalNeticesi> SkriptiKrediteYazAsync(
            int creditId,
            string metn,
            CancellationToken cancellationToken = default);
    }
}
