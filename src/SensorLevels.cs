using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ReadyLauncher
{
    /// <summary>A sensor's warning and critical levels (NaN: none), in the unit its levels are typed in.</summary>
    internal class Levels
    {
        public double Warning = double.NaN, Critical = double.NaN;
        public bool LowIsBad;

        public bool Any { get { return !double.IsNaN(Warning) || !double.IsNaN(Critical); } }

        public bool Same(Levels o)
        {
            if (o == null) return !Any;
            if (!Any && !o.Any) return true;
            Func<double, double, bool> eq = (a, b) => (double.IsNaN(a) && double.IsNaN(b)) || Math.Abs(a - b) < 1e-9;
            return eq(Warning, o.Warning) && eq(Critical, o.Critical) && LowIsBad == o.LowIsBad;
        }

        public static Levels From(SensorAlert a)
        {
            return new Levels { Warning = a.Warning, Critical = a.Critical, LowIsBad = a.LowIsBad };
        }
    }

    /// <summary>One line of the warning levels window: the sensor, its unit, the built-in levels and the ones in use.</summary>
    internal class LevelRow
    {
        public SensorInfo Info;
        public string Unit = "";
        public double Factor = 1;
        public Levels Default, Current;
        public double Now = double.NaN;   // the latest reading, in Unit
    }

    /// <summary>
    /// Warning colors: which readings have levels, in what unit, the built-in levels, and whether a reading is past them.
    /// </summary>
    internal static class SensorLevels
    {
        public const int Normal = 0, Warn = 1, Critical = 2;

        /// <summary>
        /// The unit a Windows sensor's levels are typed in, and how many of the reading's own units make one of it
        /// (bytes per second -> MB/s). null: the reading is text (or online / offline), so it has no levels.
        /// </summary>
        public static string WindowsUnit(string id, out double factor)
        {
            factor = 1;
            switch (id)
            {
                case "cpu.load": case "gpu.load": case "mem.used": case "mem.percent": case "mem.commit": case "bat.percent": return "%";
                case "cpu.speed": return "GHz";
                case "sys.processes": return "processes";
                case "sys.threads": return "threads";
                case "gpu.dedicated": case "gpu.shared": factor = 1073741824.0; return "GB";
                case "inet.ping": return "ms";
                case "bat.time": return "minutes";
            }
            var parts = id.Split('|');
            if (parts[0] == "core" || parts[0] == "gpu") return "%";
            if (parts[0] == "disk" && parts.Length > 2)
            {
                if (parts[2] == "active") return "%";
                factor = 1048576;
                return "MB/s";
            }
            if (parts[0] == "net" && parts.Length > 2)
            {
                if (parts[2] == "down" || parts[2] == "up") { factor = 1048576; return "MB/s"; }
                if (parts[2] == "signal") return "%";
            }
            return null;
        }

        /// <summary>The built-in levels (none for readings where no value is a problem, like network speed or a clock).</summary>
        public static Levels Default(SensorInfo info, string kind, string unit)
        {
            string id = info.Id;
            Func<double, double, bool, Levels> l = (w, c, low) => new Levels { Warning = w, Critical = c, LowIsBad = low };
            if (id == "cpu.load" || id == "gpu.load" || id.StartsWith("core|") || id.StartsWith("gpu|")) return l(85, 95, false);
            if (id == "mem.used" || id == "mem.percent" || id == "mem.commit") return l(85, 95, false);
            if (id.StartsWith("disk|") && id.EndsWith("|active")) return l(90, 100, false);
            if (id == "inet.ping") return l(100, 250, false);
            if (id == "bat.percent") return l(20, 10, true);
            if (id == "bat.time") return l(30, 10, true);
            if (id.StartsWith("net|") && id.EndsWith("|signal")) return l(40, 20, true);
            if (kind == "temp") return (unit ?? "").Contains("F") ? l(176, 194, false) : l(80, 90, false);
            if (kind == "load")
            {
                // Processor and graphics load from HWiNFO / Libre Hardware Monitor; not memory, fan or battery percentages.
                string name = ((info.Label ?? "") + " " + (info.Name ?? "")).ToLowerInvariant();
                if (!name.Contains("memory") && new[] { "cpu", "gpu", "core", "total", "usage", "utility", "utilization", "d3d" }.Any(w => name.Contains(w)))
                    return l(85, 95, false);
            }
            return new Levels();
        }

        /// <summary>Your own levels for a sensor if you changed them, otherwise the built-in ones.</summary>
        public static Levels For(List<SensorAlert> mine, SensorInfo info, string kind, string unit)
        {
            var a = mine.FirstOrDefault(x => x.Id == info.Id);
            return a != null ? Levels.From(a) : Default(info, kind, unit);
        }

        /// <summary>Normal, Warn or Critical for a reading's number (in the reading's own units).</summary>
        public static int Level(Levels l, double number, double factor)
        {
            if (l == null || double.IsNaN(number)) return Normal;
            double v = number / (factor > 0 ? factor : 1);
            if (l.LowIsBad)
            {
                if (!double.IsNaN(l.Critical) && v <= l.Critical) return Critical;
                if (!double.IsNaN(l.Warning) && v <= l.Warning) return Warn;
                return Normal;
            }
            if (!double.IsNaN(l.Critical) && v >= l.Critical) return Critical;
            if (!double.IsNaN(l.Warning) && v >= l.Warning) return Warn;
            return Normal;
        }

        /// <summary>A line for the levels window, or null for a sensor that's text (it has no levels).</summary>
        public static LevelRow Row(List<SensorAlert> mine, SensorInfo info, SensorReading r)
        {
            if (r != null && r.Scale == GraphScale.OnOff) return null;
            string unit, kind = "";
            double factor;
            if (info.Source.Length > 0)
            {
                // HWiNFO / Libre Hardware Monitor: the unit comes with the reading (unknown while the app is closed).
                if (r != null && r.Scale == GraphScale.None && !r.Inactive) return null;
                unit = r != null && !r.Inactive ? r.Unit ?? "" : "";
                factor = r != null && r.Factor > 0 ? r.Factor : 1;
                kind = r != null ? r.Kind ?? "" : "";
            }
            else
            {
                unit = WindowsUnit(info.Id, out factor);
                if (unit == null) return null;
            }
            return new LevelRow
            {
                Info = info, Unit = unit, Factor = factor, Default = Default(info, kind, unit), Current = For(mine, info, kind, unit),
                Now = r == null || r.Inactive ? double.NaN : r.Number / factor
            };
        }

        /// <summary>Keep the levels chosen for a sensor: saved only when they differ from the built-in ones.</summary>
        public static void Keep(List<SensorAlert> mine, LevelRow row, Levels chosen)
        {
            mine.RemoveAll(a => a.Id == row.Info.Id);
            if (!chosen.Same(row.Default))
                mine.Add(new SensorAlert { Id = row.Info.Id, Warning = chosen.Warning, Critical = chosen.Critical, LowIsBad = chosen.LowIsBad });
        }

        /// <summary>A number with its unit, rounded for reading: "87%", "62.5 °C", "3.45 GHz", "120 ms".</summary>
        public static string WithUnit(double v, string unit)
        {
            string n = Show(Math.Round(v, Math.Abs(v) >= 100 ? 0 : Math.Abs(v) >= 10 ? 1 : 2));
            unit = unit ?? "";
            return unit.Length == 0 ? n : unit == "%" ? n + "%" : n + " " + unit;
        }

        /// <summary>One of a reading's numbers (in its own units) the way people read it: bytes as KB / MB / GB, speeds per second.</summary>
        public static string Format(SensorReading r, double number)
        {
            double f = r.Factor > 0 ? r.Factor : 1;
            if (f > 1) return SensorHub.Size(Math.Max(0, number)) + ((r.Unit ?? "").EndsWith("/s") ? "/s" : "");
            if (r.Unit == "%" && r.Info.Source.Length == 0) return Math.Round(number) + "%";   // Windows shows whole percentages
            return WithUnit(number, r.Unit);
        }

        /// <summary>A level as it's shown in a box: "85", "0.5", or "" for none.</summary>
        public static string Show(double v)
        {
            return double.IsNaN(v) ? "" : v.ToString(Math.Abs(v) >= 100 ? "0.#" : "0.###", CultureInfo.CurrentCulture);
        }

        /// <summary>A typed level ("85", "0.5" or "0,5"); empty is none. false: not a number.</summary>
        public static bool Parse(string text, out double v)
        {
            v = double.NaN;
            string t = (text ?? "").Trim();
            if (t.Length == 0) return true;
            return double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out v)
                || double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }
    }
}
