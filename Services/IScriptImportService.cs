using EnterpriseAeroStudio.Models;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 📜 <b>SKRİPT (JSON) İDXALI</b> ✓✓✓ — istifadəçinin yapışdırdığı mətn
    /// siyahısından avtomobilləri, onların xərclərini, tarixlərini və qeydlərini
    /// proqrama <b>dəqiq</b> köçürür.
    /// <para>
    /// İki rejim:
    /// </para>
    /// <list type="bullet">
    ///   <item><b>🔎 Yoxlama</b> (<see cref="YoxlaAsync"/>) — heç nə yazılmır ✗;
    ///     nə olacağı (cəmlər · uyğunlaşdırılan kateqoriyalar · yeni qruplar) göstərilir ✓</item>
    ///   <item><b>📥 İcra</b> (<see cref="IcraEtAsync"/>) — avtomobillər və xərclər
    ///     <b>əsl</b> kimi bazaya yazılır ✓✓✓</item>
    /// </list>
    /// </summary>
    public interface IScriptImportService
    {
        /// <summary>🔎 Yalnız yoxlayır (baza DƏYİŞMİR ✗) — nəticə hesabatı qaytarır ✓.</summary>
        Task<ScriptImportSonuc> YoxlaAsync(string json, CancellationToken cancellationToken = default);

        /// <summary>
        /// 📥 Skripti <b>icra edir</b>: avtomobillər + xərclər + qeydlər yazılır ✓✓✓
        /// </summary>
        Task<ScriptImportSonuc> IcraEtAsync(string json, CancellationToken cancellationToken = default);
    }
}
