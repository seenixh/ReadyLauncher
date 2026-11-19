using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace ReadyLauncher
{
    /// <summary>
    /// Finding and removing the PawnIO driver, which Libre Hardware Monitor (and some other monitoring apps) install.
    /// The launcher itself never runs elevated: removal starts PawnIO's own signed uninstaller with "Run as administrator",
    /// so Windows asks the user first. Only if pieces are left after that does it ask again for Windows' own tools
    /// (pnputil, sc) to remove them.
    /// </summary>
    internal static class PawnIo
    {
        internal enum State { NotInstalled, Installed, RestartNeeded }

        const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\PawnIO";

        /// <summary>Test copies only: a file that stands in for the driver (there = installed), so the real one is never touched.</summary>
        static string FakeDriver
        {
            get
            {
                if (!Program.TestMode) return null;
                string f = Environment.GetEnvironmentVariable("READYLAUNCHER_FAKE_PAWNIO");
                return string.IsNullOrEmpty(f) ? null : f;
            }
        }

        /// <summary>Quick look (registry only): is anything of PawnIO's still listed? Used to notice changes while Settings is open.</summary>
        public static bool QuickLook()
        {
            string fake = FakeDriver;
            if (fake != null) return File.Exists(fake);
            string a, b, c, d;
            return FindInstalledApp(out a, out b, out c, out d) || ServiceExists();
        }

        /// <summary>Full check, including Windows' driver store (takes a fraction of a second; call it off the UI thread).</summary>
        public static State Check(out string version)
        {
            version = null;
            string fake = FakeDriver;
            if (fake != null)
            {
                if (!File.Exists(fake)) return State.NotInstalled;
                version = "2.2.0.0";
                return State.Installed;
            }
            string quiet, normal, folder;
            bool app = FindInstalledApp(out quiet, out normal, out folder, out version);
            if (app || DriverPackages().Count > 0) return State.Installed;
            if (!ServiceExists()) return State.NotInstalled;
            // Only the service is left: still loaded (or marked for deletion) means Windows finishes removing it at restart.
            return ServiceRunning() || ServiceMarkedForDeletion() ? State.RestartNeeded : State.Installed;
        }

        /// <summary>
        /// Removes PawnIO completely. Windows asks for permission for each step that needs it. Returns a sentence for the
        /// user, or null when they said no to Windows' permission prompt.
        /// </summary>
        public static string Remove()
        {
            string fake = FakeDriver;
            if (fake != null)
            {
                // Test copies: "uninstall" the stand-in file the way the real one is uninstalled (another process, waited for).
                bool canceled;
                Run(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/c del \"" + fake + "\"", false, out canceled);
                return Outcome();
            }
            if (Program.TestMode) return "This is a test copy, so the real PawnIO driver was left alone.";

            // 1. PawnIO's own uninstaller, when it's in the installed apps list (that's how Libre Hardware Monitor,
            //    winget and PawnIO's setup install it).
            string quiet, normal, folder, version;
            if (FindInstalledApp(out quiet, out normal, out folder, out version))
            {
                string command = !string.IsNullOrEmpty(quiet) ? quiet : normal;
                string exe, args;
                if (!string.IsNullOrEmpty(command) && SplitCommand(command, out exe, out args) && File.Exists(exe))
                {
                    bool canceled;
                    Run(exe, args, true, out canceled);
                    if (canceled) return null;
                    // Some uninstallers hand over to a copy of themselves and return early: give it time to finish.
                    for (int i = 0; i < 60 && FindInstalledApp(out quiet, out normal, out folder, out version); i++) Thread.Sleep(1000);
                }
            }

            // 2. Anything left behind: the driver package in Windows' driver store, or a service entry that isn't in use.
            foreach (var package in DriverPackages())
            {
                bool canceled;
                Run(Path.Combine(Environment.SystemDirectory, "pnputil.exe"), "/delete-driver " + package + " /uninstall /force", true, out canceled);
                if (canceled) return null;
            }
            if (ServiceExists() && !ServiceRunning() && !ServiceMarkedForDeletion())
            {
                bool canceled;
                Run(Path.Combine(Environment.SystemDirectory, "sc.exe"), "delete PawnIO", true, out canceled);
                if (canceled) return null;
            }
            return Outcome();
        }

        static string Outcome()
        {
            string version;
            switch (Check(out version))
            {
                case State.NotInstalled: return "The PawnIO driver has been removed.";
                case State.RestartNeeded: return "The PawnIO driver has been removed. Restart Windows to finish.";
                default:
                    return "The PawnIO driver couldn't be removed completely. Close any app that uses it (like Libre Hardware Monitor "
                         + "or Fan Control), restart Windows, and then try again.";
            }
        }

        // ---------- what's installed ----------

        /// <summary>PawnIO in the installed apps list (all users).</summary>
        static bool FindInstalledApp(out string quietUninstall, out string uninstall, out string folder, out string version)
        {
            quietUninstall = uninstall = folder = version = null;
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (var root = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (root == null) continue;
                        foreach (var name in root.GetSubKeyNames())
                            using (var k = root.OpenSubKey(name))
                            {
                                string display = k == null ? null : k.GetValue("DisplayName") as string;
                                if (display == null || !display.StartsWith("PawnIO", StringComparison.OrdinalIgnoreCase)) continue;
                                quietUninstall = k.GetValue("QuietUninstallString") as string;
                                uninstall = k.GetValue("UninstallString") as string;
                                folder = k.GetValue("InstallLocation") as string;
                                version = k.GetValue("DisplayVersion") as string;
                                return true;
                            }
                    }
                }
                catch (Exception) { }
            }
            return false;
        }

        static bool ServiceExists()
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(ServiceKey)) return k != null; }
            catch (Exception) { return false; }
        }

        static bool ServiceMarkedForDeletion()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(ServiceKey))
                {
                    object flag = k == null ? null : k.GetValue("DeleteFlag");
                    return flag is int && (int)flag != 0;
                }
            }
            catch (Exception) { return false; }
        }

        static bool ServiceRunning()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController("PawnIO"))
                    return sc.Status != System.ServiceProcess.ServiceControllerStatus.Stopped;
            }
            catch (Exception) { return false; }
        }

        /// <summary>PawnIO's driver packages in Windows' driver store ("oem21.inf"...), from pnputil's list.</summary>
        static List<string> DriverPackages()
        {
            var found = new List<string>();
            try
            {
                var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "pnputil.exe"), "/enum-drivers")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                string text;
                using (var p = Process.Start(psi))
                {
                    text = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(30000);
                }
                // One block per driver package, separated by blank lines. The labels depend on Windows' language, but the
                // file names don't: a block that mentions pawnio.inf names its package as oemNN.inf.
                foreach (var block in Regex.Split(text, @"\r?\n\s*\r?\n"))
                {
                    if (block.IndexOf("pawnio.inf", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var m = Regex.Match(block, @"\boem\d+\.inf\b", RegexOptions.IgnoreCase);
                    if (m.Success && !found.Contains(m.Value.ToLowerInvariant())) found.Add(m.Value.ToLowerInvariant());
                }
            }
            catch (Exception) { }
            return found;
        }

        // ---------- running things ----------

        /// <summary>"C:\Program Files\PawnIO\uninstall.exe" -uninstall -silent  ->  the program and its arguments.</summary>
        static bool SplitCommand(string command, out string exe, out string args)
        {
            exe = args = null;
            command = command.Trim();
            if (command.StartsWith("\""))
            {
                int end = command.IndexOf('"', 1);
                if (end < 0) return false;
                exe = command.Substring(1, end - 1);
                args = command.Substring(end + 1).Trim();
            }
            else
            {
                int exeEnd = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                if (exeEnd < 0) return false;
                exe = command.Substring(0, exeEnd + 4);
                args = command.Substring(exeEnd + 4).Trim();
            }
            return true;
        }

        /// <summary>Runs a program and waits for it (up to 10 minutes). Elevated runs show Windows' permission prompt first.</summary>
        static void Run(string exe, string args, bool elevated, out bool canceled)
        {
            canceled = false;
            try
            {
                var psi = new ProcessStartInfo(exe, args) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
                if (elevated) psi.Verb = "runas";
                using (var p = Process.Start(psi))
                    if (p != null && !p.WaitForExit(10 * 60 * 1000)) { }
            }
            catch (Win32Exception ex) { if (ex.NativeErrorCode == 1223) canceled = true; }   // "No" on the permission prompt
            catch (Exception) { }
        }
    }
}
