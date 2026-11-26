using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReadyLauncher
{
    /// <summary>The notes window beside the launcher, and the edges that side windows line up against.</summary>
    internal partial class LauncherWindow
    {
        NotesWindow notesWindow;
        DispatcherTimer notesSaveTimer;

        bool NotesActive { get { return notesWindow != null && notesWindow.IsActive; } }

        void UpdateNotes()
        {
            if (S.NotesPos == NotesPlacement.Off) { CloseNotes(); return; }
            if (!IsVisible) { if (notesWindow != null) notesWindow.Hide(); return; }
            if (notesWindow == null)
            {
                var border = new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Max(0.4, bgAlpha)), FrameBorder.R, FrameBorder.G, FrameBorder.B));
                if (Data.NoteTabs.Count == 0) Data.Normalize();
                notesWindow = new NotesWindow(NotesWindowBrush(), NotesChromeColor, TextColorFor(Luminance(bg)), border, NotesAreaColor(),
                                              Data.NoteTabs, Data.NoteTabIndex, S.NotesFontSize);
                Satellite(notesWindow);
                notesWindow.Topmost = !inBackground;
                notesWindow.TextEdited += OnNotesEdited;
                notesWindow.EscapePressed += HideLauncher;
                notesWindow.SaveRequested += SaveNotesToFile;
                notesWindow.WidthDragged += w =>
                {
                    S.NotesWidth = (int)Math.Round(Math.Max(160, Math.Min(800, w * DpiScale)));   // the same range as in Settings
                    PlaceNotes();
                    PlaceOutsideVolume();
                };
                notesWindow.WidthDragEnded += delegate { app.Save(); };
                notesWindow.PickColor = current =>
                {
                    app.ModalOpen = true;
                    try { return ColorPickerDialog.Pick(notesWindow, current); }
                    finally { app.ModalOpen = false; }
                };
                notesWindow.Confirm = question =>
                {
                    app.ModalOpen = true;
                    try { return MessageBox.Show(notesWindow, question, Program.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes; }
                    finally { app.ModalOpen = false; }
                };
                notesWindow.Deactivated += OnDeactivated;   // clicking away from the notes counts as clicking away from the launcher
                notesWindow.SizeChanged += delegate { PlaceOutsideVolume(); };
            }
            if (notesWindow.Owner == null) notesWindow.Owner = this;
            PlaceNotes(true);   // in place before it shows
            notesWindow.Show();
            PlaceNotes();
        }

        /// <summary>Save typing shortly after it stops, rather than on every key.</summary>
        void OnNotesEdited()
        {
            if (notesSaveTimer == null)
            {
                notesSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
                notesSaveTimer.Tick += delegate { FlushNotes(); };
            }
            notesSaveTimer.Stop();
            notesSaveTimer.Start();
        }

        void FlushNotes()
        {
            if (notesSaveTimer != null) notesSaveTimer.Stop();
            if (notesWindow == null || !notesWindow.Dirty) return;
            Data.NoteTabs = notesWindow.Tabs();
            Data.NoteTabIndex = notesWindow.SelectedTab;
            Data.NotesText = AllNotesText(Data.NoteTabs);   // plain text too, for older versions of the launcher
            notesWindow.Dirty = false;
            app.Save();
        }

        /// <summary>Every tab as plain text; with several tabs, each starts with its name.</summary>
        static string AllNotesText(List<NoteTab> tabs)
        {
            if (tabs.Count == 1) return tabs[0].PlainText();
            var sb = new System.Text.StringBuilder();
            foreach (var t in tabs)
            {
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append("== ").Append(t.Name).Append(" ==\n").Append(t.PlainText());
            }
            return sb.ToString();
        }

        /// <summary>The notes window itself: the launcher's color, as see-through as the launcher.</summary>
        Brush NotesWindowBrush() { return new SolidColorBrush(Color.FromArgb((byte)(255 * bgAlpha), bg.R, bg.G, bg.B)); }

        /// <summary>The color behind the notes' text, or null for the default light tint.</summary>
        Color? NotesAreaColor()
        {
            Color c;
            return NoteDocs.TryColor(S.NotesBackColor, out c) ? c : (Color?)null;
        }

        /// <summary>A notes background picked from the notes' right-click menu ("" = the default light tint).</summary>
        void SetNotesBackground(string hex)
        {
            S.NotesBackColor = hex ?? "";
            app.Save();
            if (notesWindow != null) notesWindow.ApplyColors(NotesWindowBrush(), NotesChromeColor, TextColorFor(Luminance(bg)), NotesAreaColor());
            OnNotesEdited();
        }

        /// <summary>
        /// Test copies only: "notes:tab,paragraph,command" selects a paragraph's text in a tab and runs a formatting command
        /// on it (bold, italic, underline, strike, bullets, numbering, check, tick, size24, red, highlight, newtab, menu);
        /// "notesback:#RRGGBB" picks a background. Each waits a few seconds so the notes window is up.
        /// </summary>
        static int notesTestStep;
        bool RunNotesTestAction(string action)
        {
            bool format = action.StartsWith("notes:"), back = action.StartsWith("notesback:");
            if (!format && !back) return false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3 + 0.5 * notesTestStep++) };
            timer.Tick += delegate
            {
                timer.Stop();
                if (notesWindow == null) return;
                if (back) { SetNotesBackground(action.Substring(10)); return; }
                var parts = action.Substring(6).Split(',');
                notesWindow.TestSelect(int.Parse(parts[0]), int.Parse(parts[1]));
                notesWindow.TestCommand(parts[2]);
                FlushNotes();
            };
            timer.Start();
            return true;
        }

        /// <summary>Write all the notes to a .txt file the user picks.</summary>
        void SaveNotesToFile()
        {
            if (notesWindow == null) return;
            FlushNotes();
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save notes",
                FileName = "Notes " + DateTime.Now.ToString("yyyy-MM-dd") + ".txt",
                DefaultExt = ".txt",
                Filter = "Text files (*.txt)|*.txt|All files|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true
            };
            bool? ok;
            app.ModalOpen = true;
            try { ok = dlg.ShowDialog(notesWindow); }
            finally { app.ModalOpen = false; }
            if (ok != true) return;
            try
            {
                // Windows line endings, so the file looks right in every editor.
                string text = AllNotesText(notesWindow.Tabs()).Replace("\r\n", "\n").Replace("\n", "\r\n");
                System.IO.File.WriteAllText(dlg.FileName, text, new System.Text.UTF8Encoding(true));
                notesWindow.ShowSaved(System.IO.Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                MessageBox.Show(notesWindow, "The notes couldn't be saved.\n\n" + ex.Message, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        void CloseNotes()
        {
            if (notesWindow == null) return;
            FlushNotes();
            var w = notesWindow;
            notesWindow = null;
            w.Close();
        }

        public void SetNotesPlacement(NotesPlacement pos)
        {
            S.NotesPos = pos;
            app.Save();
            CloseNotes();
            UpdateNotes();
            PlaceOutsideVolume();
        }

        void PlaceNotes(bool evenHidden = false)
        {
            if (notesWindow == null || (!notesWindow.IsVisible && !evenHidden)) return;
            double gap = Math.Max(0, S.NotesGap) / DpiScale;
            double w = Math.Max(160, S.NotesWidth) / DpiScale;
            notesWindow.Width = w;
            notesWindow.ResizeOnLeft = S.NotesPos == NotesPlacement.Left;   // the edge away from the launcher can be dragged
            notesWindow.Height = Math.Max(100, ActualHeight - 2 * Shadow);
            notesWindow.Top = Top + Shadow;
            notesWindow.Left = S.NotesPos == NotesPlacement.Left ? OuterLeft(false) - gap - w : OuterRight(false) + gap;
        }

        /// <summary>
        /// The visible left edge of the launcher, or of a detached strip on its left; with
        /// <paramref name="withNotes"/>, a notes window on the left counts too.
        /// </summary>
        double OuterLeft(bool withNotes)
        {
            double e = Left + Shadow;
            if (dockWindow != null && dockWindow.IsVisible && dockArea != null && S.DockPos == DockPosition.Left)
                e = dockWindow.Left + dockWindow.ActualWidth - dockArea.ActualWidth;
            if (withNotes && notesWindow != null && notesWindow.IsVisible && S.NotesPos == NotesPlacement.Left)
                e = notesWindow.Left;
            return e;
        }

        double OuterRight(bool withNotes)
        {
            double e = Left + ActualWidth - Shadow;
            if (dockWindow != null && dockWindow.IsVisible && dockArea != null && S.DockPos == DockPosition.Right)
                e = dockWindow.Left + dockArea.ActualWidth;
            if (withNotes && notesWindow != null && notesWindow.IsVisible && S.NotesPos == NotesPlacement.Right)
                e = notesWindow.Left + notesWindow.Width;
            return e;
        }

        // ---------- saving / swapping all data ----------

        /// <summary>Put window size/position and unsaved notes into the data before it is written out.</summary>
        public void PrepareForSave()
        {
            RememberBounds();
            FlushNotes();
        }

        /// <summary>Before an import replaces all the data: save what is open and let go of the old data.</summary>
        public void PrepareForDataSwap()
        {
            CloseFolder();
            if (decorDrag != null) { var v = decorDrag.View; decorDrag = null; v.ReleaseMouseCapture(); }
            PrepareForSave();
            CloseNotes();   // reopened by ApplySettings with the imported notes
        }
    }
}
