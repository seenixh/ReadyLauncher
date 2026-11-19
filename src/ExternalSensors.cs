using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net;
using System.Text;

namespace ReadyLauncher
{
    /// <summary>One reading from HWiNFO or Libre Hardware Monitor.</summary>
    internal class ExternalReading
    {
        public string Id, Group, Name, Label, Value;
        public double Number = double.NaN;   // for the tiles' mini graphs
        public GraphScale Scale;
        public string Kind = "";
        public string Unit = "";    // the unit warning levels are typed in
        public double Factor = 1;   // Number / Factor = the reading in that unit

        /// <summary>The graph's scale and color, from a unit like "°C", "RPM", "%" or "MB/s" (HWiNFO, and Libre Hardware Monitor's web page).</summary>
        public ExternalReading WithUnit(double number, string unit)
        {
            string u = (unit ?? "").Trim();
            Number = number;
            Scale = u == "%" ? GraphScale.Percent : u == "Yes/No" ? GraphScale.OnOff : u.EndsWith("/s") ? GraphScale.FromZero : GraphScale.Range;
            Kind = u.Contains("°") ? "temp" : u == "RPM" ? "fan" : u == "V" || u == "mV" || u == "W" || u == "A" ? "power"
                 : u == "MHz" || u == "GHz" ? "clock" : u == "%" ? "load" : u.EndsWith("/s") ? "rate" : u == "MB" || u == "GB" ? "data" : "";
            Unit = u == "B/s" ? "MB/s" : u;   // a speed read from Libre Hardware Monitor's page comes back in bytes per second
            Factor = u == "B/s" ? 1048576 : 1;
            return this;
        }

        /// <summary>The same from a Libre Hardware Monitor sensor type ("Temperature", "Fan", "Load"...).</summary>
        public ExternalReading WithType(double number, string type)
        {
            Number = number;
            Scale = GraphScale.Range;
            Factor = 1;
            switch (type)
            {
                case "Temperature": Kind = "temp"; Unit = "°C"; break;
                case "Fan": Kind = "fan"; Unit = "RPM"; break;
                case "Voltage": Kind = "power"; Unit = "V"; break;
                case "Power": Kind = "power"; Unit = "W"; break;
                case "Current": Kind = "power"; Unit = "A"; break;
                case "Clock": Kind = "clock"; Unit = "MHz"; break;
                case "Load": case "Control": case "Level": case "Humidity": Kind = "load"; Unit = "%"; Scale = GraphScale.Percent; break;
                case "Throughput": Kind = "rate"; Unit = "MB/s"; Factor = 1048576; Scale = GraphScale.FromZero; break;
                case "Data": Kind = "data"; Unit = "GB"; break;
                case "SmallData": Kind = "data"; Unit = "MB"; break;
                case "Frequency": Unit = "Hz"; break;
                case "Flow": Unit = "L/h"; break;
                case "Noise": Unit = "dBA"; break;
                case "TimeSpan": Unit = "seconds"; break;
            }
            return this;
        }

        /// <summary>
        /// A number and unit from text like "45.0 °C", "45,0 °C" or "1.2 MB/s" (speeds come back in bytes per second,
        /// so a graph can mix KB/s and MB/s readings).
        /// </summary>
        public static bool ParseValue(string text, out double v, out string unit)
        {
            v = double.NaN;
            unit = "";
            var m = System.Text.RegularExpressions.Regex.Match(text ?? "", @"^\s*(-?\d+(?:[.,]\d+)*)\s*(.*)$");
            if (!m.Success) return false;
            string n = m.Groups[1].Value;
            unit = m.Groups[2].Value.Trim();
            int dot = n.LastIndexOf('.'), comma = n.LastIndexOf(',');
            if (dot >= 0 && comma >= 0)
            {
                int mark = Math.Max(dot, comma);   // with both, the last one is the decimal mark
                n = n.Substring(0, mark).Replace(".", "").Replace(",", "") + "." + n.Substring(mark + 1);
            }
            else if (comma >= 0) n = n.Split(',').Length == 2 ? n.Replace(',', '.') : n.Replace(",", "");
            else if (n.Split('.').Length > 2) n = n.Replace(".", "");
            if (!double.TryParse(n, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return false;
            if (unit.EndsWith("/s") && unit.Length > 3)
            {
                char prefix = char.ToUpperInvariant(unit[0]);
                double factor = prefix == 'K' ? 1024 : prefix == 'M' ? 1048576 : prefix == 'G' ? 1073741824.0 : 1;
                if (factor > 1) { v *= factor; unit = "B/s"; }
            }
            return true;
        }
    }

    /// <summary>
    /// HWiNFO shares its sensors through a block of shared memory while it runs with
    /// "Shared Memory Support" switched on (Settings in HWiNFO). Layout from HWiNFO's published SDK.
    /// </summary>
    internal static class HwinfoSensors
    {
        const string MapName = @"Global\HWiNFO_SENS_SM2";
        const uint Signature = 0x53695748;   // "HWiS"

        /// <summary>Test copies only: act as if HWiNFO were closed while the file named by READYLAUNCHER_HIDE_HWINFO exists.</summary>
        static bool HiddenForTest()
        {
            if (!Program.TestMode) return false;
            string f = Environment.GetEnvironmentVariable("READYLAUNCHER_HIDE_HWINFO");
            return !string.IsNullOrEmpty(f) && File.Exists(f);
        }

        /// <summary>Quick check (no reading): is HWiNFO running with its sensor sharing on?</summary>
        public static bool IsSharing()
        {
            if (HiddenForTest()) return false;
            foreach (var name in new[] { MapName, "HWiNFO_SENS_SM2" })
            {
                try
                {
                    using (var map = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read))
                    using (var view = map.CreateViewAccessor(0, 4, MemoryMappedFileAccess.Read))
                        return view.ReadUInt32(0) == Signature;
                }
                catch (Exception) { }
            }
            return false;
        }

        public static List<ExternalReading> Read(out string problem)
        {
            problem = null;
            if (HiddenForTest()) { problem = "HWiNFO isn't sharing its sensors."; return null; }
            MemoryMappedFile map = null;
            foreach (var name in new[] { MapName, "HWiNFO_SENS_SM2" })
            {
                try { map = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read); break; }
                catch (Exception) { }
            }
            if (map == null)
            {
                problem = "HWiNFO isn't sharing its sensors.";
                return null;
            }
            try
            {
                using (map)
                using (var view = map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                {
                    if (view.ReadUInt32(0) != Signature) { problem = "HWiNFO's sensor sharing has stopped."; return null; }
                    uint sensorOffset = view.ReadUInt32(20), sensorSize = view.ReadUInt32(24), sensorCount = view.ReadUInt32(28);
                    uint readingOffset = view.ReadUInt32(32), readingSize = view.ReadUInt32(36), readingCount = view.ReadUInt32(40);
                    var ansi = Encoding.Default;

                    var sensors = new List<Tuple<string, string>>();   // (id part, name)
                    for (uint i = 0; i < sensorCount && i < 4096; i++)
                    {
                        long at = sensorOffset + (long)i * sensorSize;
                        uint id = view.ReadUInt32(at), inst = view.ReadUInt32(at + 4);
                        string name = Text(view, at + 136, 128, ansi);
                        if (name.Length == 0) name = Text(view, at + 8, 128, ansi);
                        sensors.Add(Tuple.Create(id.ToString("X") + "-" + inst, name));
                    }

                    var list = new List<ExternalReading>();
                    for (uint i = 0; i < readingCount && i < 16384; i++)
                    {
                        long at = readingOffset + (long)i * readingSize;
                        uint type = view.ReadUInt32(at), sensorIndex = view.ReadUInt32(at + 4), readingId = view.ReadUInt32(at + 8);
                        if (type == 0 || sensorIndex >= sensors.Count) continue;
                        string label = Text(view, at + 140, 128, ansi);
                        if (label.Length == 0) label = Text(view, at + 12, 128, ansi);
                        string unit = Text(view, at + 268, 16, ansi);
                        double value = view.ReadDouble(at + 284);
                        var sensor = sensors[(int)sensorIndex];
                        list.Add(new ExternalReading
                        {
                            Id = "hwi|" + sensor.Item1 + "|" + readingId.ToString("X"),
                            Group = "HWiNFO: " + sensor.Item2,
                            Name = label, Label = label,
                            Value = Format(value, unit)
                        }.WithUnit(value, unit));
                    }
                    return list;
                }
            }
            catch (Exception ex)
            {
                problem = "HWiNFO's sensors couldn't be read (" + ex.Message + ").";
                return null;
            }
        }

        static string Text(MemoryMappedViewAccessor view, long at, int length, Encoding enc)
        {
            var bytes = new byte[length];
            view.ReadArray(at, bytes, 0, length);
            int end = Array.IndexOf(bytes, (byte)0);
            return enc.GetString(bytes, 0, end < 0 ? length : end).Trim();
        }

        static string Format(double v, string unit)
        {
            string u = (unit ?? "").Trim();
            string n;
            if (u == "V") n = v.ToString("0.000");
            else if (u == "W" || u == "A" || u == "GB" || u == "x") n = v.ToString("0.0");
            else if (u.Contains("°") || u == "%" || u == "RPM" || u == "MHz" || u == "MB" || u == "Yes/No") n = v.ToString("0");
            else n = Math.Abs(v) >= 100 ? v.ToString("0") : v.ToString("0.##");
            if (u == "Yes/No") return v != 0 ? "Yes" : "No";
            return u.Length == 0 ? n : n + " " + u;
        }
    }

    /// <summary>
    /// Libre Hardware Monitor shares its sensors with Windows (WMI) when it runs as administrator, and
    /// through a small local web page when its "Remote Web Server" option is on.
    /// </summary>
    internal static class LibreSensors
    {
        public static List<ExternalReading> Read(out string problem)
        {
            problem = null;
            var list = ReadWmi();
            if (list != null && list.Count > 0) return list;
            list = ReadWeb();
            if (list != null && list.Count > 0) return list;
            problem = "Libre Hardware Monitor isn't sharing its sensors.";
            return null;
        }

        static List<ExternalReading> ReadWmi()
        {
            try
            {
                var hardware = new Dictionary<string, string>();
                using (var s = new System.Management.ManagementObjectSearcher(@"root\LibreHardwareMonitor", "SELECT Identifier, Name FROM Hardware"))
                    foreach (System.Management.ManagementObject o in s.Get())
                        using (o) hardware[(string)o["Identifier"]] = (string)o["Name"];
                var list = new List<ExternalReading>();
                using (var s = new System.Management.ManagementObjectSearcher(@"root\LibreHardwareMonitor", "SELECT Identifier, Name, SensorType, Value, Parent FROM Sensor"))
                    foreach (System.Management.ManagementObject o in s.Get())
                        using (o)
                        {
                            string parent = (string)o["Parent"] ?? "", hw;
                            hardware.TryGetValue(parent, out hw);
                            string type = (string)o["SensorType"] ?? "";
                            list.Add(new ExternalReading
                            {
                                Id = "lhm|" + o["Identifier"],
                                Group = "Libre Hardware Monitor: " + (hw ?? parent),
                                Name = o["Name"] + " (" + TypeName(type) + ")",
                                Label = (string)o["Name"],
                                Value = Format(Convert.ToDouble(o["Value"]), type)
                            }.WithType(Convert.ToDouble(o["Value"]), type));
                        }
                return list;
            }
            catch (Exception) { return null; }
        }

        /// <summary>The web page's address: the port comes from Libre Hardware Monitor's own settings file.</summary>
        static string WebAddress()
        {
            string test = Program.TestMode ? Environment.GetEnvironmentVariable("READYLAUNCHER_LHM_URL") : null;
            if (!string.IsNullOrEmpty(test)) return test;
            int port = 8085;
            try
            {
                foreach (var p in Process.GetProcessesByName("LibreHardwareMonitor"))
                    using (p)
                    {
                        string cfg = Path.Combine(Path.GetDirectoryName(p.MainModule.FileName), "LibreHardwareMonitor.config");
                        if (!File.Exists(cfg)) continue;
                        var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(cfg), "key=\"listenerPort\"\\s+value=\"(\\d+)\"");
                        if (m.Success) port = int.Parse(m.Groups[1].Value);
                    }
            }
            catch (Exception) { }
            return "http://localhost:" + port + "/data.json";
        }

        static List<ExternalReading> ReadWeb()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(WebAddress());
                req.Timeout = 1500;
                req.ReadWriteTimeout = 1500;
                req.Proxy = null;
                string json;
                using (var resp = req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) json = r.ReadToEnd();
                var root = new System.Web.Script.Serialization.JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json) as IDictionary<string, object>;
                var list = new List<ExternalReading>();
                if (root != null) Walk(root, 0, null, list);
                return list;
            }
            catch (Exception) { return null; }
        }

        // The page is a tree: (top) > computer > hardware > sensor kind > sensors (leaves carry "SensorId").
        static void Walk(IDictionary<string, object> node, int depth, string hardware, List<ExternalReading> list)
        {
            string text = node.ContainsKey("Text") ? node["Text"] as string : "";
            if (depth == 2) hardware = text;
            object id;
            if (node.TryGetValue("SensorId", out id) && id is string && ((string)id).Length > 0)
            {
                string type = node.ContainsKey("Type") ? node["Type"] as string ?? "" : "";
                var reading = new ExternalReading
                {
                    Id = "lhm|" + id,
                    Group = "Libre Hardware Monitor: " + (hardware ?? ""),
                    Name = text + (type.Length > 0 ? " (" + TypeName(type) + ")" : ""),
                    Label = text,
                    Value = node.ContainsKey("Value") ? Convert.ToString(node["Value"]) : ""
                };
                double number; string unit;
                if (ExternalReading.ParseValue(reading.Value, out number, out unit))
                {
                    // The page's sensor type, when it gives one, says more than the unit does.
                    if (type.Length > 0) reading.WithType(number, type); else reading.WithUnit(number, unit);
                }
                list.Add(reading);
            }
            object children;
            if (node.TryGetValue("Children", out children) && children is IEnumerable)
                foreach (var c in (IEnumerable)children)
                {
                    var child = c as IDictionary<string, object>;
                    if (child != null) Walk(child, depth + 1, hardware, list);
                }
        }

        internal static string TypeName(string type)
        {
            switch (type)
            {
                case "Temperature": return "temperature";
                case "Fan": return "fan speed";
                case "Voltage": return "voltage";
                case "Clock": return "clock";
                case "Load": return "load";
                case "Power": return "power";
                case "Current": return "current";
                case "Control": return "fan control";
                case "Throughput": return "speed";
                case "Data": case "SmallData": return "data";
                case "Level": return "level";
                case "TimeSpan": return "time";
                default: return type.ToLowerInvariant();
            }
        }

        internal static string Format(double v, string type)
        {
            switch (type)
            {
                case "Temperature": return v.ToString("0") + " \u00B0C";
                case "Fan": return v.ToString("0") + " RPM";
                case "Voltage": return v.ToString("0.000") + " V";
                case "Clock": return v.ToString("0") + " MHz";
                case "Load": case "Control": case "Level": case "Humidity": return v.ToString("0") + "%";
                case "Power": return v.ToString("0.0") + " W";
                case "Current": return v.ToString("0.00") + " A";
                case "Data": return v.ToString("0.0") + " GB";
                case "SmallData": return v.ToString("0") + " MB";
                case "Throughput": return SensorHub.Size(v) + "/s";
                case "Frequency": return v.ToString("0") + " Hz";
                case "Flow": return v.ToString("0.0") + " L/h";
                case "Energy": return v.ToString("0") + " mWh";
                case "Noise": return v.ToString("0") + " dBA";
                case "Timing": return v.ToString("0.0") + " ns";
                case "TimeSpan": return TimeSpan.FromSeconds(v).ToString(v >= 3600 ? @"h\:mm\:ss" : @"m\:ss");
                default: return v.ToString("0.##");
            }
        }
    }
}
