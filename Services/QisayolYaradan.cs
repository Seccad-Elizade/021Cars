using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Cas0201.Setup
{
    /// <summary>
    /// 🔗 <b>WINDOWS QISAYOL (.lnk) YARADAN</b> ✓✓✓ (v6.2.21)
    /// <para><b>⚠ TARİXÇƏ (istifadəçi şikayətləri ✓):</b></para>
    /// <list type="number">
    ///   <item>① <c>dynamic</c> + <c>WScript.Shell</c> ✗ → <b>trimmed</b> publish-də
    ///         <c>RuntimeBinderException</c> ✗ → <c>catch</c> UDURDU ✗ →
    ///         <b>qısayol HEÇ VAXT YARANMIRDI</b> ✗✓✓</item>
    ///   <item>② COM interop (<c>IShellLinkW</c>) ✗ → <c>Save()</c> xəta ATMASINA
    ///         baxmayaraq <b>fayl YARANMIRDI</b> ✗✓✓ (trimdə sükutla no-op ✗)</item>
    ///   <item>③ PowerShell <c>-Command</c> ✗ → dırnaq escaping-i skripti SINDIRIRDI ✗</item>
    ///   <item>④ PowerShell + müvəqqəti <c>.ps1</c> ✗ → <b>KODLAŞDIRMA</b> problemi ✗:
    ///         «—» ✗ «ə» ✗ hərfləri pozulurdu ✗ → yol səhv olurdu ✗ →
    ///         «The shortcut pathname must end with .lnk» xətası ✗✓✓</item>
    /// </list>
    /// <para><b>✅ SON HƏLL (5 qat ✓):</b></para>
    /// <list type="number">
    ///   <item>① COM interop ✓ (sürətli yol ✓ — adətən yetər ✓)</item>
    ///   <item>② <b>PowerShell <c>-EncodedCommand</c></b> ✓✓✓ (UTF-16LE + Base64 ✓)
    ///         → fayl YOX ✗ · dırnaq YOX ✗ · <b>kodlaşdırma YOX</b> ✗✓✓</item>
    ///   <item>③ <c>ExecutionPolicy Bypass</c> ✓ · ④ <c>powershell.exe</c> TAM YOL ✓</item>
    ///   <item>⑤ Hər addımdan sonra <c>File.Exists</c> YOXLAMASI ✓ →
    ///         uğursuzluq SƏSSİZCƏ keçmir ✗ · 🩺 loq yazılır ✓✓✓</item>
    /// </list>
    /// <para>🛡️ Heç bir halda İSTİSNA atılmır ✗ — hər şey <c>true/false</c> qaytarır ✓</para>
    /// </summary>
    internal static class QisayolYaradan
    {
        // ---- 🧩 COM interfeysləri (erkən bağlama ✓ trim-safe ✓✓✓) ----

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLink
        {
        }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
                int cch, IntPtr pfd, int fFlags);

            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);

            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);

            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);

            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);

            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);

            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);

            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath,
                int cch, out int piIcon);

            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);

            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);

            void Resolve(IntPtr hwnd, int fFlags);

            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        // ====================================================================
        //  ★ ƏSAS METOD ★
        // ====================================================================

        /// <summary>🔗 Qısayolu yaradır ✓ — <b>həqiqətən yaranıbsa</b> <c>true</c> ✓✓✓</summary>
        public static bool Yarat(string lnkYolu, string hedefExe, string işQovluğu, string tesvir = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(lnkYolu) || string.IsNullOrWhiteSpace(hedefExe))
                {
                    return false;
                }

                if (System.IO.File.Exists(lnkYolu))
                {
                    return true;   // ✔ artıq var ✓ → toxunulmur ✗
                }

                var qovluq = System.IO.Path.GetDirectoryName(lnkYolu);

                if (string.IsNullOrWhiteSpace(qovluq) || !System.IO.Directory.Exists(qovluq))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(işQovluğu))
                {
                    işQovluğu = System.IO.Path.GetDirectoryName(hedefExe) ?? string.Empty;
                }

                // ---- ① COM interop ✓ (sürətli yol ✓) ----
                if (ComIleYarat(lnkYolu, hedefExe, işQovluğu, tesvir))
                {
                    return true;
                }

                // ---- ② PowerShell -EncodedCommand ✓✓✓ (ZƏMANƏTLİ ✓) ----
                if (PowerShellIleYarat(lnkYolu, hedefExe, işQovluğu, tesvir))
                {
                    return true;
                }

                LogYaz($"❌ HƏR İKİ ÜSUL ALINMADI ✗ — LNK={lnkYolu} · EXE={hedefExe}");

                return false;
            }
            catch (Exception ex)
            {
                LogYaz("❌ GÖZLƏNİLMƏZ XƏTA: " + ex.GetType().Name + " — " + ex.Message);
                return false;
            }
        }

        /// <summary>① COM (IShellLinkW) ilə yaradır ✓ — fayl varsa <c>true</c> ✓</summary>
        private static bool ComIleYarat(string lnkYolu, string hedefExe, string işQovluğu, string tesvir)
        {
            try
            {
                var link = (IShellLinkW)new ShellLink();

                link.SetPath(hedefExe);
                link.SetWorkingDirectory(işQovluğu);
                link.SetIconLocation(hedefExe, 0);

                if (!string.IsNullOrWhiteSpace(tesvir))
                {
                    link.SetDescription(tesvir);
                }

                ((IPersistFile)link).Save(lnkYolu, true);

                return System.IO.File.Exists(lnkYolu);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ② <b>PowerShell <c>-EncodedCommand</c></b> ✓✓✓ — ⚠ SON ZƏMANƏT ✓
        /// <para>
        /// ⚠ Skript <b>UTF-16LE + Base64</b> kimi verilir ✓ →
        /// • müvəqqəti fayl LAZIM DEYİL ✗ · dırnaq problemi YOX ✗ ·
        /// <b>KODLAŞDIRMA problemi YOX</b> ✗✓✓ («—» · «ə» düzgün ötürülür ✓)
        /// </para>
        /// </summary>
        private static bool PowerShellIleYarat(string lnkYolu, string hedefExe,
            string işQovluğu, string tesvir)
        {
            try
            {
                // 🔐 HƏR SAHƏ AYRICA QORUNUR ✓ (bir sahə xəta versə də Save() icra olunur ✓)
                var skript =
                    "$ErrorActionPreference = 'Continue'" + Environment.NewLine +
                    "$w = New-Object -ComObject WScript.Shell" + Environment.NewLine +
                    "$s = $w.CreateShortcut(" + PS(lnkYolu) + ")" + Environment.NewLine +
                    "$s.TargetPath = " + PS(hedefExe) + Environment.NewLine +
                    "try { $s.WorkingDirectory = " + PS(işQovluğu) + " } catch { }" + Environment.NewLine +
                    "try { $s.IconLocation = " + PS(hedefExe + ",0") + " } catch { }" + Environment.NewLine +
                    "try { $s.Description = " + PS(tesvir) + " } catch { }" + Environment.NewLine +
                    "$s.Save()" + Environment.NewLine;

                // ✅ `-EncodedCommand` RƏSMİ formatı: UTF-16LE + Base64 ✓✓✓
                var baza64 = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(skript));

                // ⚠ `powershell.exe` TAM YOL ilə ✓ — PATH-da olmaya bilər ✗ (sandbox ✓)
                var psYolu = System.IO.Path.Combine(
                    Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

                if (!System.IO.File.Exists(psYolu))
                {
                    psYolu = "powershell.exe";   // ehtiyat ✓
                }

                using var proc = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = psYolu,
                        Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden " +
                                    "-ExecutionPolicy Bypass -EncodedCommand " + baza64,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });

                if (proc is null)
                {
                    return false;
                }

                var çıxış = proc.StandardOutput.ReadToEnd();
                var xəta = proc.StandardError.ReadToEnd();

                proc.WaitForExit(20000);

                var oldu = System.IO.File.Exists(lnkYolu);

                if (!oldu || xəta.Length > 0)
                {
                    LogYaz($"PS_EXIT={proc.ExitCode} · LNK={lnkYolu}"
                           + Environment.NewLine + "STDOUT: " + çıxış.Trim()
                           + Environment.NewLine + "STDERR: " + xəta.Trim());
                }

                return oldu;
            }
            catch (Exception ex)
            {
                LogYaz("❌ PowerShell xətası: " + ex.GetType().Name + " — " + ex.Message);
                return false;
            }
        }

        /// <summary>🔐 PowerShell üçün TƏK DIRNAQLI literal ✓ (təhlükəsiz ✓)</summary>
        private static string PS(string mətn) =>
            "'" + (mətn ?? string.Empty).Replace("'", "''") + "'";

        /// <summary>🩺 Diaqnostika loqu ✓ — <c>%TEMP%\021Cars_qisayol.log</c> ✓✓✓</summary>
        private static void LogYaz(string mətn)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "021Cars_qisayol.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {mətn}"
                    + Environment.NewLine + Environment.NewLine,
                    new System.Text.UTF8Encoding(true));
            }
            catch { }
        }
    }
}
