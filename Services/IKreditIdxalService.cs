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
    }
}
