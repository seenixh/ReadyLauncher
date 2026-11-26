using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReadyLauncher
{
    /// <summary>The bar of hardware readings above the launcher.</summary>
    internal partial class LauncherWindow
    {
        const double SensorGap = 6;   // px between the sensor bar and the launcher (or a strip above it)
        SensorWindow sensorWindow;
        SensorHub sensorHub;
        // Recent numbers per sensor for the tiles' graphs (kept while the sensor window is rebuilt), and the latest readings.
        readonly Dictionary<string, SensorHistory> sensorHistory = new Dictionary<string, SensorHistory>();
        List<SensorReading> lastSensorReadings;

        /// <summary>The latest readings (for the warning levels window in Settings: their units and current values).</summary>
        public List<SensorReading> SensorReadingsNow()
        {
            return lastSensorReadings == null ? new List<SensorReading>() : lastSensorReadings.ToList();
        }

        /// <summary>Add each reading's number to its sensor's history (readings that are text, or from an app that isn't active, add nothing).</summary>
        void RecordSensors(List<SensorReading> readings)
        {
            lastSensorReadings = readings;
            var now = DateTime.Now;   // one time for the whole set (they're read together)
            foreach (var r in readings)
            {
                if (r.Scale == GraphScale.None || r.Inactive) continue;
                SensorHistory h;
                if (!sensorHistory.TryGetValue(r.Info.Id, out h)) sensorHistory[r.Info.Id] = h = new SensorHistory();
                h.Add(r.Number, r.Scale, now);
            }
            // Sensors no longer chosen don't need a history.
            foreach (var id in sensorHistory.Keys.Where(k => !S.Sensors.Contains(k)).ToList()) sensorHistory.Remove(id);
        }

        /// <summary>Show / refresh / hide the sensor bar to match the settings and the launcher.</summary>
        void UpdateSensors()
        {
            bool wanted = S.Sensors.Count > 0 && !S.SensorsHidden && IsVisible;
            if (!wanted)
            {
                if (sensorHub != null) sensorHub.Stop();
                if (sensorWindow != null)
                {
                    if (S.Sensors.Count == 0) { sensorWindow.Close(); sensorWindow = null; }
                    else sensorWindow.Hide();
                }
                return;
            }
            bool created = sensorWindow == null;
            if (sensorWindow == null)
            {
                // Background: the chosen color, or the launcher's; text: the chosen color, or whatever reads well on it.
                Color backColor = string.IsNullOrWhiteSpace(S.SensorBackColor) ? bg : ParseColor(S.SensorBackColor, bg);
                Color textColor = string.IsNullOrWhiteSpace(S.SensorTextColor) ? TextColorFor(Luminance(backColor)) : ParseColor(S.SensorTextColor, Colors.White);
                var back = new SolidColorBrush(Color.FromArgb((byte)(255 * Math.Max(0.55, bgAlpha)), backColor.R, backColor.G, backColor.B));
                var border = new SolidColorBrush(Color.FromArgb(0x99, FrameBorder.R, FrameBorder.G, FrameBorder.B));
                sensorWindow = new SensorWindow(back, textColor, border);
                Satellite(sensorWindow);
                sensorWindow.Topmost = !inBackground;
                sensorWindow.SizeChanged += delegate { PlaceSensors(); };
                sensorWindow.OrderChanged += ids =>
                {
                    // Dragged into a new order: keep it (anything not on the bar right now keeps its place at the end).
                    S.Sensors = ids.Concat(S.Sensors.Where(x => !ids.Contains(x))).ToList();
                    app.Save();
                    if (sensorHub != null) sensorHub.Configure(S.Sensors, S.KnownSensors, S.SensorInterval, S.PingHost, S.ExternalSensors);
                };
                sensorWindow.History = sensorHistory;
                sensorWindow.LevelsFor = r => S.SensorWarnings ? SensorLevels.For(S.SensorAlerts, r.Info, r.Kind, r.Unit) : null;
                sensorWindow.LevelsRequested += r =>
                {
                    // One sensor's levels, from its right-click menu: kept (and shown) right away.
                    var row = SensorLevels.Row(S.SensorAlerts, r.Info, r);
                    if (row == null) return;
                    var chosen = WarningLevelsWindow.Edit(sensorWindow, app, S.SettingsTheme, new List<LevelRow> { row }, S.SensorWarnings);
                    if (chosen == null) return;
                    SensorLevels.Keep(S.SensorAlerts, row, chosen[row.Info.Id]);
                    app.Save();
                    if (sensorWindow != null) sensorWindow.Refresh();
                };
                sensorWindow.GraphToggled += (id, on) =>
                {
                    S.SensorNoGraph.Remove(id);
                    if (!on) S.SensorNoGraph.Add(id);
                    app.Save();
                    if (sensorWindow != null) sensorWindow.SetTiles(S.SensorTiles, S.SensorGraphLength, S.SensorNoGraph, S.SensorTilesCentered);
                };
            }
            sensorWindow.SetAlignment(S.SensorAlign);
            sensorWindow.SetSize(S.SensorSize);
            sensorWindow.SetTiles(S.SensorTiles, S.SensorGraphLength, S.SensorNoGraph, S.SensorTilesCentered);
            sensorWindow.SetFillBottom(S.SensorFillBottom);
            sensorWindow.SetLocked(S.LayoutLocked);
            if (sensorWindow.Owner == null) sensorWindow.Owner = this;
            sensorWindow.Show();
            PlaceSensors();
            // A window made again (new colors, say) shows the latest readings right away instead of waiting for the next ones.
            if (created && lastSensorReadings != null)
            {
                var still = lastSensorReadings.Where(r => S.Sensors.Contains(r.Info.Id)).ToList();
                if (still.Count > 0) sensorWindow.ShowReadings(still);
            }
            if (sensorHub == null)
                sensorHub = new SensorHub(Dispatcher, readings => { RecordSensors(readings); if (sensorWindow != null) sensorWindow.ShowReadings(readings); });
            sensorHub.Configure(S.Sensors, S.KnownSensors, S.SensorInterval, S.PingHost, S.ExternalSensors);
            sensorHub.Start();
        }

        Border sensorButton;

        /// <summary>The header button that hides or shows the sensor bar (only there when sensors are chosen).</summary>
        Border SensorButton()
        {
            sensorButton = GlyphButton("", "", b => ToggleSensors());
            System.Windows.Automation.AutomationProperties.SetName(sensorButton, "Hide or show sensors");
            ShowSensorButtonState();
            return sensorButton;
        }

        void ShowSensorButtonState()
        {
            if (sensorButton == null) return;
            sensorButton.ToolTip = S.SensorsHidden ? "Show sensors" : "Hide sensors";
            sensorButton.Child.Opacity = S.SensorsHidden ? 0.4 : 1;   // dimmed while the sensors are hidden
        }

        public void ToggleSensors()
        {
            if (S.Sensors.Count == 0) return;
            S.SensorsHidden = !S.SensorsHidden;
            app.Save();
            UpdateSensors();
            ShowSensorButtonState();
        }

        void HideSensors()
        {
            if (sensorHub != null) sensorHub.Stop();
            if (sensorWindow != null) sensorWindow.Hide();
        }

        void CloseSensors()
        {
            if (sensorWindow != null) { sensorWindow.Close(); sensorWindow = null; }
        }

        /// <summary>Same width as the launcher, just above it (or above a lamp strip or detached strip at the top).</summary>
        void PlaceSensors()
        {
            if (sensorWindow == null || !sensorWindow.IsVisible) return;
            double gap = SensorGap / DpiScale;
            double panelL = Left + Shadow, panelW = ActualWidth - 2 * Shadow;
            double top = TopEdge();
            if (dockWindow != null && dockWindow.IsVisible && dockArea != null && S.DockPos == DockPosition.Top)
                top = dockWindow.Top + dockWindow.ActualHeight - dockArea.ActualHeight;
            sensorWindow.Width = Math.Max(160, panelW);
            sensorWindow.Left = panelL;
            // Never above the top of the screen (a launcher near the top with many sensors would push it off).
            sensorWindow.Top = Math.Max(SystemParameters.VirtualScreenTop, top - gap - sensorWindow.ActualHeight);
        }
    }
}
