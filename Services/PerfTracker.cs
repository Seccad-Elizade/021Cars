using System;
using System.Threading;

namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// ⏱️ <b>FAZA İZLƏYİCİSİ</b> ✓✓✓ (v6.2.81)
    /// <para>
    /// Ağır əməliyyatlar başlamazdan əvvəl cari «fazanı» burada qeyd edirik ✓
    /// (məs. «Kreditlər · cədvəl yazılır»). UI donarsa
    /// <see cref="UiWatchdog"/> loga <b>hansı fazada</b> donduğunu yazır ✓✓✓
    /// </para>
    /// <para>🛡️ Yalnız statik sətir saxlayır — heç bir yük yaratmır ✗</para>
    /// </summary>
    public static class PerfTracker
    {
        private static string _faza = "—";

        /// <summary>Cari ağır əməliyyatın adı ✓</summary>
        public static string Faza => Volatile.Read(ref _faza);

        /// <summary>Fazanı qeyd edir ✓</summary>
        public static void Qur(string faza) => Volatile.Write(ref _faza, faza ?? "—");

        /// <summary>Fazanı təmizləyir ✓</summary>
        public static void Bitir() => Volatile.Write(ref _faza, "—");
    }
}
