// ============================================================================
//  📦 021Cars — QURAŞDIRICI (Program.cs) ✓✓✓
// ----------------------------------------------------------------------------
//  ✅ İKİ REJİM (eyni .exe ✓):
//     ① 021Cars_Installer.exe  → 📦 QURAŞDIRIR ✓
//     ② Uninstaller.exe         → ❌ SİLİR ✓✓✓ (adı belədirsə ✓ avtomatik silmə rejimi ✓)
//  ✅ Admin hüququ tələb olunur ✓ (Program Files + reyestr üçün ✓)
//  ✅ Konsol YOXDUR ✗ — təmiz WPF pəncərə ✓ (professional ✓)
// ============================================================================

using System;
using System.Windows;

namespace Cas0201.Setup
{
    /// <summary>🚀 Giriş nöqtəsi ✓ — rejimi adı ilə təyin edir ✓</summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnMainWindowClose
                };

                app.Run(new InstallerWindow());
            }
            catch (Exception ex)
            {
                try
                {
                    MessageBox.Show("⚠️ Xəta ✗: " + ex.Message,
                        "021Cars — Quraşdırıcı", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
            }
        }
    }
}
