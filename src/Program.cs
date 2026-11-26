using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32;
using WF = System.Windows.Forms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: System.Reflection.AssemblyTitle("Ready Launcher")]
[assembly: System.Reflection.AssemblyProduct("Ready Launcher")]
[assembly: System.Reflection.AssemblyVersion("2.4.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.4.0.0")]

namespace ReadyLauncher
{
    internal static class Program
    {
        /// <summary>The app's name, as shown in window titles, message boxes and the notification area.</summary>
        public const string AppName = "Ready Launcher";

        // A test copy (READYLAUNCHER_DATA set, or READYLAUNCHER_TEST_INSTANCE for a portable copy under test) gets its own
        // instance names so it never talks to the real one.
        internal static readonly string Suffix = TestSuffix();
        internal static readonly string MutexName = @"Local\ReadyLauncher.SingleInstance" + Suffix;
        public static readonly string ShowEventName = @"Local\ReadyLauncher.Show" + Suffix;
        // The same app under its name before 1.55 (WinLauncher).
        const string OldMutexName = @"Local\WinLauncher.SingleInstance";
        public static bool TestMode { get { return Suffix.Length > 0; } }

        static string TestSuffix()
        {
            string d = Environment.GetEnvironmentVariable("READYLAUNCHER_DATA");
            if (string.IsNullOrEmpty(d)) d = Environment.GetEnvironmentVariable("READYLAUNCHER_TEST_INSTANCE");
            return string.IsNullOrEmpty(d) ? "" : ".test" + (d.ToLowerInvariant().GetHashCode() & 0x7fffffff);
        }

        [STAThread]
        static void Main(string[] args)
        {
            bool updated = args.Any(a => a.Equals("--updated", StringComparison.OrdinalIgnoreCase));
            bool created;
            Mutex first;
            // Started by an update: the old copy may still be closing, so wait for it (up to 15 seconds).
            for (int tries = 0; ; tries++)
            {
                first = new Mutex(true, MutexName, out created);
                if (created || !updated || tries >= 75) break;
                first.Dispose();
                Thread.Sleep(200);
            }
            using (var mutex = first)
            {
                if (created && !TestMode && OldCopyRunning())
                {
                    // Two copies would fight over the hotkey and the startup entry.
                    MessageBox.Show("The older version of this launcher (WinLauncher) is still running.\n\n"
                        + "Right-click its icon in the notification area and choose Exit, then start " + AppName + " again.",
                        AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (!created)
                {
                    // Already running (e.g. the pinned taskbar icon was clicked): ask that copy to come to the front.
                    Native.AllowSetForegroundWindow(Native.ASFW_ANY);
                    try { using (var ev = EventWaitHandle.OpenExisting(ShowEventName)) ev.Set(); }
                    catch (Exception) { }
                    return;
                }
                bool background = args.Any(a => a.Equals("--background", StringComparison.OrdinalIgnoreCase));
                new LauncherApp(background, updated).Run();
                GC.KeepAlive(mutex);
            }
        }

        static bool OldCopyRunning()
        {
            Mutex m;
            if (!Mutex.TryOpenExisting(OldMutexName, out m)) return false;
            m.Dispose();
            return true;
        }
    }

    /// <summary>Owns the data, tray icon, global hotkey and the launcher window.</summary>
    internal class LauncherApp : Application
    {
        /// <summary>A tooltip that is plain text shows it in a block at most 440 wide, wrapped at the words.</summary>
        static void WrapToolTip(object sender, RoutedEventArgs e)
        {
            var tip = sender as System.Windows.Controls.ToolTip;
            var text = tip == null ? null : tip.Content as string;
            if (text == null) return;
            tip.Content = new System.Windows.Controls.TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
        }

        const int HotkeyId = 0x4C31;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ReadyLauncher";
        const string OldRunValue = "WinLauncher";   // the startup entry's name before 1.55

        /// <summary>The startup entries' key; a test copy can point it at a key of its own with READYLAUNCHER_RUN_KEY.</summary>
        static string RunKeyPath
        {
            get
            {
                string test = Program.TestMode ? Environment.GetEnvironmentVariable("READYLAUNCHER_RUN_KEY") : null;
                return string.IsNullOrEmpty(test) ? RunKey : test;
            }
        }

        /// <summary>A test copy may only change startup entries in a test key of its own, never the real one.</summary>
        static bool MayChangeStartup { get { return !Program.TestMode || RunKeyPath != RunKey; } }

        public LauncherData Data;
        public LauncherWindow Launcher;
        public bool ModalOpen;
        readonly bool startHidden, justUpdated;
        WF.NotifyIcon tray;
        HwndSource hotkeyWindow;
        EventWaitHandle showEvent;
        SettingsWindow settings;

        public LauncherApp(bool startHidden, bool justUpdated)
        {
            this.startHidden = startHidden;
            this.justUpdated = justUpdated;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // Long tooltips wrap instead of running across the screen in one line (every window in the app).
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ToolTip), System.Windows.Controls.ToolTip.OpenedEvent, new RoutedEventHandler(WrapToolTip));
            DispatcherUnhandledException += (s, ex) =>
            {
                ex.Handled = true;
                if (Program.TestMode)   // test copies: keep the details for whoever is testing
                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(LauncherData.DataFolder, "crash.txt"), ex.Exception + "\r\n\r\n"); } catch (Exception) { }
                MessageBox.Show("Something went wrong:\n\n" + ex.Exception.Message, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Warning,
                    MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            };
            LauncherData.CopyFromOldName();   // first start after the rename: bring the setup along
            Data = LauncherData.Load();
            // Icons dragged in from the Start menu by older versions: a path Windows can show the real icon for.
            bool repaired = false;
            foreach (var it in Data.AllItems().ToList())
            {
                // Also apps dropped from a Start menu folder by older versions, saved under their app ID alone.
                bool appId = it.Kind == ItemKind.App && ShellHelper.IsBareName(it.Path);
                if (!ShellHelper.NeedsFixing(it.Path) && !appId) continue;
                string better = ShellHelper.FixShellPath(it.Path);
                if (better == it.Path) continue;
                if (appId && it.Name == it.Path) it.Name = ShellHelper.DisplayName(better);   // "AcrobatReader" becomes "Adobe Acrobat"
                it.Path = better;
                repaired = true;
            }
            if (repaired) Data.Save();
            Themes.EnsurePictures(Data.Settings);   // a theme's picture that went missing is drawn again
            Launcher = new LauncherWindow(this);
            CreateTray();
            // A sign-out or restart that's canceled leaves the launcher running; one that goes ahead closes it, saved.
            if (!SessionEndGuard.Install(this, ExitApp))
                Dispatcher.BeginInvoke(new Action(() => SessionEndGuard.Install(this, ExitApp)), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            CreateHotkeyWindow();
            if (!Program.TestMode) RegisterStartupHotkey();
            MoveOldStartupEntry();
            ListenForShowRequests();
            if (!startHidden) Launcher.ShowLauncher();
            CleanUpAfterUpdate();
            StartAutoBackups();
            if (justUpdated) Notify("Updated to version " + Updates.CurrentText + ".");
            if (Program.TestMode && Environment.GetEnvironmentVariable("READYLAUNCHER_OPEN_SETTINGS") == "1") OpenSettings();
            if (Program.TestMode)
            {
                // Test copies only: run a menu command, export or import without needing a mouse or file dialogs.
                string actions = Environment.GetEnvironmentVariable("READYLAUNCHER_TEST_ACTION");
                if (!string.IsNullOrEmpty(actions))
                foreach (var one in actions.Split('|'))
                {
                    string action = one;   // several actions, separated by |, run in order
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (action.StartsWith("snapshot:") || action.StartsWith("snapshotat:"))
                        {
                            // Give icons and the quick-launch strip a moment to load first (snapshotat:<seconds>:<folder> waits longer,
                            // e.g. for the sensor graphs to fill up).
                            double wait = 4;
                            string folder = action.Substring(9);
                            if (action.StartsWith("snapshotat:"))
                            {
                                string rest = action.Substring(11);
                                int colon = rest.IndexOf(':');
                                wait = double.Parse(rest.Substring(0, colon), System.Globalization.CultureInfo.InvariantCulture);
                                folder = rest.Substring(colon + 1);
                            }
                            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(wait) };
                            t.Tick += delegate { t.Stop(); Launcher.SaveSnapshots(folder, 2); };
                            t.Start();
                        }
                        else if (action.StartsWith("settingsshot:")) SettingsPicture(action.Substring(13));
                        else if (action.StartsWith("export:")) ExportTo(action.Substring(7));
                        else if (action.StartsWith("import:")) ImportBackup(Backup.Read(action.Substring(7)));
                        else Launcher.RunTestAction(action);
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
        }

        public void Save() { Data.Save(); }

        /// <summary>A short message from the tray icon.</summary>
        public void Notify(string text)
        {
            try { tray.ShowBalloonTip(6000, Program.AppName, text, WF.ToolTipIcon.Info); } catch (Exception) { }
        }

        // ---------- global hotkey ----------

        void CreateHotkeyWindow()
        {
            var p = new HwndSourceParameters("ReadyLauncherHotkey") { Width = 0, Height = 0, WindowStyle = 0, ParentWindow = new IntPtr(-3) }; // HWND_MESSAGE
            hotkeyWindow = new HwndSource(p);
            hotkeyWindow.AddHook(WndProc);
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                Launcher.Toggle();
                handled = true;
            }
            return IntPtr.Zero;
        }

        public bool RegisterHotkey(bool showError)
        {
            Native.UnregisterHotKey(hotkeyWindow.Handle, HotkeyId);
            var s = Data.Settings;
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(s.HotkeyKey);
            bool ok = Native.RegisterHotKey(hotkeyWindow.Handle, HotkeyId, (uint)s.HotkeyModifiers | Native.MOD_NOREPEAT, vk);
            if (!ok && showError)
                tray.ShowBalloonTip(6000, Program.AppName,
                    "The shortcut " + AppSettings.HotkeyText(s.HotkeyModifiers, s.HotkeyKey) + " is already used by another app. Choose a different one in Settings.",
                    WF.ToolTipIcon.Warning);
            return ok;
        }

        static readonly Tuple<ModifierKeys, Key>[] FallbackHotkeys =
        {
            Tuple.Create(ModifierKeys.Control | ModifierKeys.Alt, Key.L),
            Tuple.Create(ModifierKeys.Control | ModifierKeys.Shift, Key.Space),
            Tuple.Create(ModifierKeys.Control | ModifierKeys.Alt, Key.Q),
            Tuple.Create(ModifierKeys.Windows | ModifierKeys.Alt, Key.L)
        };

        /// <summary>If the saved shortcut is owned by another app, switch to the first free fallback and say so.</summary>
        void RegisterStartupHotkey()
        {
            if (RegisterHotkey(false)) return;
            var s = Data.Settings;
            var wantedMods = s.HotkeyModifiers;
            var wantedKey = s.HotkeyKey;
            foreach (var f in FallbackHotkeys)
            {
                if (f.Item1 == wantedMods && f.Item2 == wantedKey) continue;
                s.HotkeyModifiers = f.Item1;
                s.HotkeyKey = f.Item2;
                if (!RegisterHotkey(false)) continue;
                Save();
                Launcher.ApplySettings();
                tray.ShowBalloonTip(8000, Program.AppName,
                    AppSettings.HotkeyText(wantedMods, wantedKey) + " is used by another app, so the launcher opens with "
                    + AppSettings.HotkeyText(f.Item1, f.Item2) + ". You can change this in Settings.", WF.ToolTipIcon.Info);
                return;
            }
            s.HotkeyModifiers = wantedMods;
            s.HotkeyKey = wantedKey;
            RegisterHotkey(true);
        }

        // ---------- second launch / pinned icon ----------

        void ListenForShowRequests()
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowEventName);
            var t = new Thread(() =>
            {
                while (true)
                {
                    showEvent.WaitOne();
                    Dispatcher.BeginInvoke(new Action(() => Launcher.ShowLauncher()));
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- tray ----------

        void CreateTray()
        {
            tray = new WF.NotifyIcon { Text = Program.AppName, Icon = LoadTrayIcon(), Visible = true };
            var menu = new WF.ContextMenuStrip();
            menu.Items.Add("Open launcher", null, delegate { Launcher.ShowLauncher(); });
            menu.Items.Add("Settings...", null, delegate { OpenSettings(); });
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitApp(); });
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == WF.MouseButtons.Left) Launcher.Toggle(); };
        }

        static System.Drawing.Icon LoadTrayIcon()
        {
            try
            {
                using (var s = typeof(LauncherApp).Assembly.GetManifestResourceStream("ReadyLauncher.app.ico"))
                    return new System.Drawing.Icon(s, WF.SystemInformation.SmallIconSize);
            }
            catch (Exception) { return System.Drawing.SystemIcons.Application; }
        }

        // ---------- settings ----------

        /// <summary>
        /// Test copies only: "settingsshot:&lt;tab&gt;:&lt;file&gt;" draws Settings off screen (never shown where you can see
        /// it, and never focused), on that tab ("reminder": at the reminder), and saves it as a picture.
        /// </summary>
        void SettingsPicture(string arg)
        {
            int colon = arg.IndexOf(':');
            string tab = arg.Substring(0, colon), file = arg.Substring(colon + 1);
            var wait = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            wait.Tick += delegate
            {
                wait.Stop();
                var w = new SettingsWindow(this) { ShowActivated = false, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
                w.Left = -30000;
                w.Top = -30000;
                if (tab == "activity" || tab == "activityview") { w.ShowTab(SettingsWindow.LampStripTab); w.ShowActivity(); }
                else if (tab == "ball" || tab == "ballview") { w.ShowTab(SettingsWindow.LampStripTab); w.ShowEightBall(); }
                else if (tab == "reminder" || tab == "remindertest" || tab == "lampsize" || tab == "lampcolors" || tab == "numbers") { w.ShowTab(SettingsWindow.LampStripTab); w.ShowReminder(); } else w.ShowTab(int.Parse(tab));
                w.Show();
                var later = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                later.Tick += delegate
                {
                    later.Stop();
                    if (tab == "remindertest")
                    {
                        System.IO.File.WriteAllText(file + ".txt", w.TestReminders());
                        w.UpdateLayout();
                    }
                    if (tab == "lampsize" || tab == "lampcolors" || tab == "numbers" || tab == "activity" || tab == "ball")
                    {
                        System.IO.File.WriteAllText(file + ".txt", tab == "lampsize" ? w.TestLampSize() : tab == "numbers" ? w.TestNumbers() : tab == "activity" ? w.TestLampActivity()
                            : tab == "ball" ? w.TestEightBall() : w.TestLampColors());
                        w.UpdateLayout();
                    }
                    var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(w);
                    if (Environment.GetEnvironmentVariable("READYLAUNCHER_WORK_HEIGHT") != null) System.IO.File.WriteAllText(file + ".size.txt", "window " + w.ActualHeight + " high, at most " + w.MaxHeight);
                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(w.ActualWidth * dpi.DpiScaleX), (int)(w.ActualHeight * dpi.DpiScaleY),
                        96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, System.Windows.Media.PixelFormats.Pbgra32);
                    rtb.Render(w);
                    var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                    using (var fs = System.IO.File.Create(file)) enc.Save(fs);
                    w.Close();
                };
                later.Start();
            };
            wait.Start();
        }

        public void OpenSettings(int tab = 0)
        {
            if (settings != null) { settings.ShowTab(tab); settings.Activate(); return; }
            Launcher.RememberBounds();   // so "Custom size" shows the current size
            settings = new SettingsWindow(this);
            settings.ShowTab(tab);
            // Owned by the launcher, so Settings always stays in front of it, even when the launcher is clicked.
            try { new WindowInteropHelper(settings).Owner = new WindowInteropHelper(Launcher).EnsureHandle(); }
            catch (Exception) { }
            ModalOpen = true;
            settings.Closed += delegate { settings = null; ModalOpen = false; };
            settings.ShowActivated = !Program.TestMode;
            settings.Show();
            if (!Program.TestMode) settings.Activate();
        }

        /// <summary>Settings at the lamp strip's reminder.</summary>
        public void OpenReminderSettings()
        {
            OpenSettings(SettingsWindow.LampStripTab);
            if (settings != null) settings.ShowReminder();
        }

        public bool SettingsOpen { get { return settings != null; } }

        /// <summary>Settings closes when the launcher closes (changes not applied yet are dropped, as with Cancel).</summary>
        public void CloseSettings()
        {
            if (settings != null) settings.Close();
        }

        /// <summary>
        /// Put Settings back in front once the launcher has shown or rebuilt its windows (the notes, the strip, the
        /// sensor bar and the volume control each come to the front when they're shown).
        /// </summary>
        public void RaiseSettings()
        {
            var s = settings;
            if (s == null) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (settings != s || !s.IsVisible) return;
                var h = new WindowInteropHelper(s).Handle;
                if (h != IntPtr.Zero)
                    Native.SetWindowPos(h, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        public void SettingsChanged(bool startWithWindows, bool resize)
        {
            if (!Program.TestMode) RegisterHotkey(true);   // a test copy never takes the real shortcut
            SetStartup(startWithWindows);
            if (resize) Launcher.ApplyWindowSize();
            Save();
            Launcher.ApplySettings();
            RaiseSettings();
        }

        // ---------- updates ----------

        /// <summary>Swap in a downloaded update and restart into it. Returns null when restarting, or what went wrong.</summary>
        public string InstallUpdate(string downloaded)
        {
            string problem = Updates.Install(downloaded);
            if (problem != null) return problem;
            try
            {
                string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "--updated")
                {
                    UseShellExecute = false, WorkingDirectory = System.IO.Path.GetDirectoryName(exe)
                });
            }
            catch (Exception ex)
            {
                Updates.Restore();
                return "The new version couldn't be started (" + ex.Message + "), so nothing was changed.";
            }
            ExitApp();
            return null;
        }

        /// <summary>Remove the copy an update replaced; it may take a moment to finish closing.</summary>
        void CleanUpAfterUpdate()
        {
            if (Updates.CleanUp()) return;
            int tries = 0;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            t.Tick += delegate { if (Updates.CleanUp() || ++tries >= 30) t.Stop(); };
            t.Start();
        }

        // ---------- export / import ----------

        public const string BeforeImportFile = "launcher.before-import.xml";

        public void ExportTo(string file)
        {
            Launcher.PrepareForSave();
            Save();
            Backup.Export(Data, file);
        }

        // ---------- automatic backups ----------

        System.Windows.Threading.DispatcherTimer backupTimer;
        DateTime lastBackupWarning = DateTime.MinValue;

        /// <summary>"Launcher backup 2026-11-16 14-05-32.xml": the date and time it was made (Windows file names can't contain ':').</summary>
        public static string BackupFileName(DateTime when)
        {
            return "Launcher backup " + when.ToString("yyyy-MM-dd HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture) + ".xml";
        }

        /// <summary>Check for a due automatic backup shortly after start, then every 30 minutes.</summary>
        void StartAutoBackups()
        {
            backupTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(Program.TestMode ? 3 : 60) };
            backupTimer.Tick += delegate
            {
                backupTimer.Interval = TimeSpan.FromMinutes(30);
                AutoBackupIfDue(false);
            };
            backupTimer.Start();
        }

        /// <summary>
        /// Save an automatic backup if it's switched on and the chosen number of days has passed since the last one
        /// (or always, with <paramref name="now"/>). Returns the file saved, or null; <paramref name="problem"/> says why not.
        /// </summary>
        public string AutoBackupIfDue(bool now)
        {
            string problem;
            return AutoBackupIfDue(now, out problem);
        }

        public string AutoBackupIfDue(bool now, out string problem)
        {
            problem = null;
            var s = Data.Settings;
            if (!now && (!s.AutoBackup || DateTime.Now - s.LastAutoBackup < TimeSpan.FromDays(Math.Max(1, s.AutoBackupDays)))) return null;
            string folder = s.BackupFolder;
            try
            {
                System.IO.Directory.CreateDirectory(folder);
                DateTime when = DateTime.Now;
                string file = System.IO.Path.Combine(folder, BackupFileName(when));
                ExportTo(file);
                s.LastAutoBackup = when;
                Save();
                return file;
            }
            catch (Exception ex)
            {
                problem = ex.Message;
                // Say so (at most twice a day), e.g. when the backup drive isn't plugged in.
                if (!now && DateTime.Now - lastBackupWarning > TimeSpan.FromHours(12))
                {
                    lastBackupWarning = DateTime.Now;
                    Notify("The automatic backup couldn't be saved to " + folder + ". " + ex.Message);
                }
                return null;
            }
        }

        /// <summary>Swap in everything from a backup. The current setup is kept as launcher.before-import.xml.</summary>
        public void ImportBackup(LauncherBackup b)
        {
            Launcher.PrepareForDataSwap();
            Save();
            try { Data.WriteTo(System.IO.Path.Combine(LauncherData.DataFolder, BeforeImportFile)); }
            catch (Exception) { }
            Backup.RestorePictures(b);
            var d = b.Data;
            // Keep this PC's window position: the backup may come from a different screen layout.
            d.Settings.WinLeft = Data.Settings.WinLeft;
            d.Settings.WinTop = Data.Settings.WinTop;
            Data = d;
            Save();
            if (!Program.TestMode) RegisterHotkey(true);
            Launcher.ApplyWindowSize();
            Launcher.ApplySettings();
        }

        public static bool IsStartupEnabled()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                return k != null && k.GetValue(RunValue) != null;
        }

        static string StartupCommand { get { return "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\" --background"; } }

        static void SetStartup(bool on)
        {
            if (!MayChangeStartup) return;   // never repoint the real startup entry at a test copy
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (on) k.SetValue(RunValue, StartupCommand);
                    else k.DeleteValue(RunValue, false);
                }
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Starting with Windows was on under the old name (WinLauncher): keep it on, under the new name and for this copy.
        /// </summary>
        static void MoveOldStartupEntry()
        {
            if (!MayChangeStartup) return;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null || k.GetValue(OldRunValue) == null) return;
                    k.SetValue(RunValue, StartupCommand);
                    k.DeleteValue(OldRunValue, false);
                }
            }
            catch (Exception) { }
        }

        public void ExitApp()
        {
            Launcher.HideLauncher();
            Save();
            Native.UnregisterHotKey(hotkeyWindow.Handle, HotkeyId);
            tray.Visible = false;
            tray.Dispose();
            Launcher.AllowClose = true;
            Shutdown();
        }
    }
}
