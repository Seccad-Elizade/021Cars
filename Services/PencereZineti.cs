using System;
using System.Runtime.InteropServices;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// 🎨 <b>PƏNCƏRƏ ZİNƏTİ</b> — yalnız GÖRÜNÜŞ ✓ (v6.2.46)
    /// <para>
    /// ⚠ ƏVVƏL: proqram qaranlıq, amma başlıq zolağı AÇIQ BOZ idi ✗
    /// → iki fərqli üslub yan-yana ✗ → gözə batır və yorurdu ✗
    /// </para>
    /// <para>
    /// ✅ İNDİ: başlıq zolağı da qaranlıq ✓ — bütün pəncərə vahid görünür ✓✓✓
    /// </para>
    /// <para>
    /// ⚠ <b>QEYD:</b> bu fayl WPF tiplərindən ASILI DEYİL ✗ (yalnız IntPtr ✓) —
    /// çünki «Web» layihəsi də <c>Services\*.cs</c> fayllarını yığır ✓✓✓
    /// </para>
    /// <para>
    /// 🛡️ <b>TƏHLÜKƏSİZLİK:</b> hər şey <c>try/catch</c> içindədir ✓ —
    /// köhnə Windows ✗ · DWM olmayan sistem ✗ → HEÇ NƏ olmur, proqram işləyir ✓✓✓
    /// ⚠ Funksiyaya heç bir müdaxilə YOXDUR ✗ — yalnız rəng dəyişir ✓
    /// </para>
    /// </summary>
    public static class PencereZineti
    {
        // 🌑 Win 10 (1903+) · Win 11 ✓
        private const int QARANLIQ_REJIM = 20;

        // 🌑 Win 10 (1809) — köhnə atribut nömrəsi ✓
        private const int QARANLIQ_REJIM_KOHNE = 19;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr tutacaq, int atribut, ref int deyer, int olcu);

        /// <summary>
        /// 🌑 <b>Başlıq zolağını qaranlıq edir</b> ✓
        /// <para>⚠ <paramref name="tutacaq"/> = pəncərənin HWND-sidir ✓
        /// (WPF-də: <c>new WindowInteropHelper(pəncərə).Handle</c> ✓)</para>
        /// <para>🛡️ Alınmasa — səssizcə keçir ✓ (xəta göstərilmir ✗)</para>
        /// </summary>
        public static void KoyuBasliq(IntPtr tutacaq)
        {
            try
            {
                if (tutacaq == IntPtr.Zero) return;   // ⚠ hələ yaranmayıb ✗

                int aciq = 1;

                var netice = DwmSetWindowAttribute(tutacaq, QARANLIQ_REJIM, ref aciq, sizeof(int));

                if (netice != 0)
                {
                    DwmSetWindowAttribute(tutacaq, QARANLIQ_REJIM_KOHNE, ref aciq, sizeof(int));
                }
            }
            catch
            {
                // 🛡️ Səssizcə keçir ✓ — heç bir xəta göstərilmir ✓
            }
        }
    }
}

