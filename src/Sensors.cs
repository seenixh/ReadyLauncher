using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ReadyLauncher
{
    /// <summary>One reading the launcher can show, e.g. "CPU load".</summary>
    internal class SensorInfo
    {
        public readonly string Id, Group, Name, Label;   // Label is the short text on the bar
        public readonly string Source;                   // "" = Windows, "H" = HWiNFO, "L" = Libre Hardware Monitor
        public SensorInfo(string id, string group, string name, string label) : this(id, group, name, label, "") { }
        public SensorInfo(string id, string group, string name, string label, string source)
        { Id = id; Group = group; Name = name; Label = label; Source = source; }

        /// <summary>HWiNFO / Libre Hardware Monitor that isn't active right now (shown greyed out).</summary>
        public bool Inactive;

        public string SourceName { get { return Source == "H" ? "HWiNFO" : Source == "L" ? "Libre Hardware Monitor" : "Windows"; } }

        public static SensorInfo FromKnown(KnownSensor k)
        {
            return new SensorInfo(k.Id, k.Group, k.Name, k.Label, k.Source ?? "") { Inactive = true };
        }

        public KnownSensor ToKnown()
        {
            return new KnownSensor { Id = Id, Group = Group, Name = Name, Label = Label, Source = Source };
        }
    }

    /// <summary>How a sensor's mini graph is scaled (None: the reading is text, so it has no graph).</summary>
    internal enum GraphScale { None, Percent, FromZero, Range, OnOff }

    /// <summary>One update for the bar: the sensor, its text, and whether its app is active.</summary>
    internal class SensorReading
    {
        public SensorInfo Info;
        public string Value;
        public bool Inactive;
        public double Number = double.NaN;   // the reading as a number, for the graph (NaN: none right now)
        public GraphScale Scale;
        public string Kind = "";             // HWiNFO / Libre Hardware Monitor: "temp", "fan", "power"... (picks the graph color)
        public string Unit = "";             // the unit warning levels are typed in ("%", "°C", "ms", "MB/s"...)
        public double Factor = 1;            // Number / Factor = the reading in that unit
    }

    /// <summary>The sensors Windows itself can provide on this PC (no extra software needed).</summary>
    internal static class SensorCatalog
    {
        public const string Cpu = "Processor (CPU)", Memory = "Memory (RAM)", Gpu = "Graphics (GPU)", Internet = "Internet",
            Disks = "Disks and drives", Power = "Battery / UPS", SystemGroup = "System";

        static readonly string[][] Engines =
        {
            new[] { "3D", "GPU 3D load", "GPU 3D" }, new[] { "VideoDecode", "GPU video decoding", "Video decode" },
            new[] { "VideoEncode", "GPU video encoding", "Video encode" }, new[] { "Compute", "GPU compute", "GPU compute" },
            new[] { "Copy", "GPU copy engine", "GPU copy" }
        };

        /// <param name="external">Also list HWiNFO / Libre Hardware Monitor sensors, if either is running and sharing them.</param>
        public static List<SensorInfo> Available(bool external = false)
        {
            var list = new List<SensorInfo>();
            Action<string, string, string, string> add = (id, group, name, label) => list.Add(new SensorInfo(id, group, name, label));

            add("cpu.load", Cpu, "CPU load", "CPU");
            add("cpu.speed", Cpu, "CPU speed", "CPU speed");
            var cores = CoreInstances();
            for (int i = 0; i < cores.Count; i++) add("core|" + i, Cpu, "Core " + (i + 1) + " load", "Core " + (i + 1));
            add("cpu.top", Cpu, "Busiest app (CPU)", "Busiest");
            add("sys.processes", Cpu, "Running processes", "Processes");
            add("sys.threads", Cpu, "Threads", "Threads");

            add("mem.used", Memory, "RAM used of total", "RAM");
            add("mem.percent", Memory, "RAM in use (%)", "RAM");
            add("mem.commit", Memory, "Committed memory (RAM + page file)", "Committed");
            add("mem.top", Memory, "Biggest app (memory)", "Biggest");

            if (CategoryExists("GPU Engine"))
            {
                add("gpu.load", Gpu, "GPU load (busiest engine)", "GPU");
                var types = new HashSet<string>(GpuEngineTypes(), StringComparer.OrdinalIgnoreCase);
                foreach (var e in Engines) if (types.Contains(e[0])) add("gpu|" + e[0], Gpu, e[1], e[2]);
            }
            if (CategoryExists("GPU Adapter Memory"))
            {
                add("gpu.dedicated", Gpu, "GPU dedicated memory used", "GPU memory");
                add("gpu.shared", Gpu, "GPU shared memory used", "GPU shared");
            }

            // Connected adapters, plus each unconnected one once (not the extra "#2", "#4" copies a card can list).
            var duplicate = new System.Text.RegularExpressions.Regex(@"#\d+$");
            foreach (var nic in Adapters().Where(n => n.OperationalStatus == OperationalStatus.Up || !duplicate.IsMatch(n.Description.Trim())))
            {
                bool up = nic.OperationalStatus == OperationalStatus.Up;
                string g = "Network: " + nic.Name + (up ? "" : " (not connected right now)"), id = "net|" + nic.Id + "|";
                add(id + "down", g, nic.Name + " download speed", nic.Name + " ↓");
                add(id + "up", g, nic.Name + " upload speed", nic.Name + " ↑");
                add(id + "total", g, nic.Name + " data received / sent", nic.Name + " data");
                add(id + "link", g, nic.Name + " connection speed", nic.Name + " link");
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    add(id + "signal", g, nic.Name + " signal strength", nic.Name + " signal");
                    add(id + "ssid", g, nic.Name + " network name", nic.Name + " network");
                }
            }
            add("inet.ping", Internet, "Ping time", "Ping");
            add("inet.status", Internet, "Connection (online / offline)", "Internet");
            add("inet.ip", Internet, "Local IP address", "IP");

            foreach (var d in DiskInstances())
            {
                string id = "disk|" + d + "|";
                add(id + "read", Disks, "Disk " + d + " read speed", "Disk " + d + " read");
                add(id + "write", Disks, "Disk " + d + " write speed", "Disk " + d + " write");
                add(id + "active", Disks, "Disk " + d + " active time", "Disk " + d);
            }
            foreach (var drive in DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.Fixed && x.IsReady))
            {
                string letter = drive.Name.TrimEnd('\\');
                add("drive|" + letter + "|free", Disks, letter + " free space", letter + " free");
            }

            var power = System.Windows.Forms.SystemInformation.PowerStatus;
            if ((power.BatteryChargeStatus & System.Windows.Forms.BatteryChargeStatus.NoSystemBattery) == 0
                && power.BatteryChargeStatus != System.Windows.Forms.BatteryChargeStatus.Unknown)
            {
                add("bat.percent", Power, "Charge level", "Battery");
                add("bat.time", Power, "Time left on battery", "Time left");
            }
            add("sys.uptime", SystemGroup, "Time since Windows started", "Uptime");
            add("sys.pc", SystemGroup, "PC name", "PC");
            add("sys.user", SystemGroup, "User account name", "User");

            if (external)
            {
                string problem;
                foreach (var source in new[] { Tuple.Create("H", (Func<List<ExternalReading>>)(() => HwinfoSensors.Read(out problem))),
                                               Tuple.Create("L", (Func<List<ExternalReading>>)(() => LibreRunning() ? LibreSensors.Read(out problem) : null)) })
                {
                    List<ExternalReading> readings;
                    try { readings = source.Item2(); } catch (Exception) { readings = null; }
                    if (readings == null) continue;
                    var seen = new HashSet<string>();
                    foreach (var r in readings)
                        if (seen.Add(r.Id)) list.Add(new SensorInfo(r.Id, r.Group, r.Name, r.Label, source.Item1));
                }
            }
            return list;
        }

        public static bool LibreRunning()
        {
            var ps = Process.GetProcessesByName("LibreHardwareMonitor");
            foreach (var p in ps) p.Dispose();
            return ps.Length > 0 || (Program.TestMode && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("READYLAUNCHER_LHM_URL")));
        }

        static bool CategoryExists(string name)
        {
            try { return PerformanceCounterCategory.Exists(name); } catch (Exception) { return false; }
        }

        /// <summary>"0,0", "0,1"... in core order.</summary>
        public static List<string> CoreInstances()
        {
            try
            {
                return new PerformanceCounterCategory("Processor Information").GetInstanceNames()
                    .Where(n => !n.Contains("_Total") && n.Contains(","))
                    .OrderBy(n => int.Parse(n.Split(',')[0])).ThenBy(n => int.Parse(n.Split(',')[1])).ToList();
            }
            catch (Exception) { return new List<string>(); }
        }

        /// <summary>Physical disks as Windows names them, e.g. "0 C:".</summary>
        public static List<string> DiskInstances()
        {
            try { return new PerformanceCounterCategory("PhysicalDisk").GetInstanceNames().Where(n => n != "_Total").OrderBy(n => n).ToList(); }
            catch (Exception) { return new List<string>(); }
        }

        static IEnumerable<string> GpuEngineTypes()
        {
            try
            {
                return new PerformanceCounterCategory("GPU Engine").GetInstanceNames()
                    .Select(n => { int i = n.IndexOf("engtype_", StringComparison.OrdinalIgnoreCase); return i < 0 ? "" : n.Substring(i + 8); })
                    .Where(t => t.Length > 0).Distinct().ToList();
            }
            catch (Exception) { return new string[0]; }
        }

        static readonly string[] virtualWords = { "virtual", "hyper-v", "vmware", "virtualbox", "loopback", "tap-", "tunnel", "bluetooth", "wan miniport", "teredo", "isatap", "vethernet", "pseudo", "npcap", "wintun", "zerotier", "tailscale" };

        /// <summary>Real wired and Wi-Fi adapters (not virtual ones).</summary>
        public static List<NetworkInterface> Adapters()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces().Where(n =>
                {
                    var t = n.NetworkInterfaceType;
                    if (t != NetworkInterfaceType.Ethernet && t != NetworkInterfaceType.Wireless80211 && t != NetworkInterfaceType.GigabitEthernet
                        && t != NetworkInterfaceType.FastEthernetT && t != NetworkInterfaceType.FastEthernetFx && t != NetworkInterfaceType.Ethernet3Megabit) return false;
                    string text = (n.Name + " " + n.Description).ToLowerInvariant();
                    return !virtualWords.Any(w => text.Contains(w));
                }).OrderBy(n => n.OperationalStatus == OperationalStatus.Up ? 0 : 1).ThenBy(n => n.Name).ToList();
            }
            catch (Exception) { return new List<NetworkInterface>(); }
        }
    }

    /// <summary>
    /// Reads the chosen sensors on a background thread every few seconds and hands the results
    /// (sensor, text) to the UI thread.
    /// </summary>
    internal sealed class SensorHub
    {
        readonly Dispatcher ui;
        readonly Action<List<SensorReading>> deliver;
        readonly object gate = new object();
        Timer timer;
        bool running, dirty = true, external;
        List<string> ids = new List<string>();
        int intervalMs = 5000;
        string pingHost = "1.1.1.1";
        List<SensorInfo> wanted = new List<SensorInfo>();
        Dictionary<string, KnownSensor> known = new Dictionary<string, KnownSensor>();
        long lastRebuild;

        public SensorHub(Dispatcher ui, Action<List<SensorReading>> deliver)
        {
            this.ui = ui;
            this.deliver = deliver;
        }

        /// <param name="includeExternal">Look for HWiNFO / Libre Hardware Monitor (again every 30 seconds) for their sensors.</param>
        /// <param name="knownSensors">Their sensors seen before, so a chosen one stays on the bar (greyed out) while its app is closed.</param>
        public void Configure(IEnumerable<string> sensorIds, IEnumerable<KnownSensor> knownSensors, double seconds, string host, bool includeExternal)
        {
            lock (gate)
            {
                known = new Dictionary<string, KnownSensor>();
                foreach (var k in knownSensors) known[k.Id] = k;
                ids = sensorIds.ToList();
                intervalMs = (int)Math.Round(Math.Max(0.5, Math.Min(3600, seconds)) * 1000);
                pingHost = string.IsNullOrWhiteSpace(host) ? "1.1.1.1" : host.Trim();
                external = includeExternal;
                dirty = true;
            }
        }

        public void Start()
        {
            lock (gate)
            {
                if (running) return;
                running = true;
                if (timer == null) timer = new Timer(Tick);
                timer.Change(0, Timeout.Infinite);
            }
        }

        public void Stop()
        {
            lock (gate) { running = false; if (timer != null) timer.Change(Timeout.Infinite, Timeout.Infinite); }
        }

        void Tick(object state)
        {
            List<string> idsNow; string host; int interval; bool rebuild, ext;
            Dictionary<string, KnownSensor> knownNow;
            lock (gate)
            {
                if (!running) return;
                idsNow = ids; host = pingHost; interval = intervalMs; rebuild = dirty; dirty = false; ext = external; knownNow = known;
            }
            try
            {
                long now = Stopwatch.GetTimestamp();
                // HWiNFO / Libre Hardware Monitor may be started (or closed) at any time: look again every 30 s.
                bool wantsExternal = ext && idsNow.Any(i => i.StartsWith("hwi|") || i.StartsWith("lhm|"));
                if (wantsExternal && (now - lastRebuild) / (double)Stopwatch.Frequency > 30) rebuild = true;
                if (rebuild)
                {
                    var all = SensorCatalog.Available(wantsExternal);
                    // A chosen HWiNFO / Libre Hardware Monitor sensor whose app isn't active stays on the bar, greyed out.
                    wanted = idsNow.Select(id =>
                    {
                        var s = all.FirstOrDefault(x => x.Id == id);
                        KnownSensor k;
                        if (s == null && knownNow.TryGetValue(id, out k)) s = SensorInfo.FromKnown(k);
                        return s;
                    }).Where(s => s != null).ToList();
                    lastRebuild = now;
                }
                var results = Read(wanted, host);
                ui.BeginInvoke(new Action(() => deliver(results)));
            }
            catch (Exception) { }
            lock (gate) { if (running) timer.Change(interval, Timeout.Infinite); }
        }

        // ---------- reading ----------

        readonly Dictionary<string, PerformanceCounter> counters = new Dictionary<string, PerformanceCounter>();
        List<string> coreNames;
        Dictionary<string, CounterSample> gpuPrev = new Dictionary<string, CounterSample>();
        readonly Dictionary<string, long[]> netPrev = new Dictionary<string, long[]>();
        Dictionary<int, TimeSpan> procPrev;
        long procPrevStamp;
        string pingText = "…";
        double pingMs = double.NaN;
        int pinging;

        // The number behind the reading ReadOne just made, and how its graph is scaled.
        double num;
        GraphScale scale;

        string P(double v) { num = Math.Max(0, Math.Min(100, v)); scale = GraphScale.Percent; return Pct(v); }
        string R(double bytesPerSecond) { num = bytesPerSecond; scale = GraphScale.FromZero; return Rate(bytesPerSecond); }
        void N(double v, GraphScale s) { num = v; scale = s; }

        float Counter(string category, string counter, string instance)
        {
            string key = category + "|" + counter + "|" + instance;
            PerformanceCounter pc;
            if (!counters.TryGetValue(key, out pc))
            {
                pc = new PerformanceCounter(category, counter, instance, true);
                pc.NextValue();   // rates need two samples; the first one only primes the counter
                counters[key] = pc;
            }
            return pc.NextValue();
        }

        List<SensorReading> Read(List<SensorInfo> sensors, string host)
        {
            var results = new List<SensorReading>();
            MemoryStatus mem = null;
            Dictionary<string, double> gpu = null;
            string[] tops = null;
            Dictionary<string, NetworkInterface> nics = null;
            Dictionary<string, ExternalReading> hwinfo = null, libre = null;
            long now = Stopwatch.GetTimestamp();

            foreach (var s in sensors)
            {
                string v;
                try
                {
                    string id = s.Id;
                    if (s.Source == "H" || s.Source == "L")
                    {
                        // One snapshot per app per update, shared by all of its sensors; null = the app isn't active.
                        string problem;
                        if (s.Source == "H" && hwinfo == null) hwinfo = Snapshot(HwinfoSensors.Read(out problem));
                        if (s.Source == "L" && libre == null) libre = Snapshot(SensorCatalog.LibreRunning() ? LibreSensors.Read(out problem) : null);
                        var snap = s.Source == "H" ? hwinfo : libre;
                        if (snap.Count == 0)
                        {
                            results.Add(new SensorReading { Info = s, Value = "not active", Inactive = true });
                            continue;
                        }
                        ExternalReading found;
                        if (snap.TryGetValue(id, out found))
                            results.Add(new SensorReading { Info = s, Value = found.Value, Number = found.Number, Scale = found.Scale, Kind = found.Kind ?? "", Unit = found.Unit ?? "", Factor = found.Factor });
                        else results.Add(new SensorReading { Info = s, Value = "n/a" });
                        continue;
                    }
                    if (id.StartsWith("mem.") && id != "mem.top" && mem == null) mem = MemoryStatus.Get();
                    if (id.StartsWith("gpu") && gpu == null) gpu = GpuReadings();
                    if ((id == "cpu.top" || id == "mem.top") && tops == null) tops = BusiestApps(now);
                    if (id.StartsWith("net|") && nics == null)
                        nics = NetworkInterface.GetAllNetworkInterfaces().GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
                    num = double.NaN;
                    scale = GraphScale.None;
                    v = ReadOne(s, mem, gpu, tops, nics, now, host);
                }
                catch (Exception) { v = "n/a"; num = double.NaN; }
                double factor;
                string unit = SensorLevels.WindowsUnit(s.Id, out factor);
                results.Add(new SensorReading { Info = s, Value = v, Number = num, Scale = scale, Unit = unit ?? "", Factor = factor });
            }
            return results;
        }

        string ReadOne(SensorInfo s, MemoryStatus mem, Dictionary<string, double> gpu, string[] tops,
            Dictionary<string, NetworkInterface> nics, long now, string host)
        {
            string id = s.Id;
            switch (id)
            {
                case "cpu.load": return P(Counter("Processor Information", "% Processor Utility", "_Total"));
                case "cpu.speed":
                    double mhz = Counter("Processor Information", "Processor Frequency", "_Total") * Counter("Processor Information", "% Processor Performance", "_Total") / 100;
                    N(mhz / 1000, GraphScale.Range);
                    return (mhz / 1000).ToString("0.00") + " GHz";
                case "cpu.top": return tops[0];
                case "sys.processes": { float n = Counter("System", "Processes", ""); N(n, GraphScale.Range); return n.ToString("0"); }
                case "sys.threads": { float n = Counter("System", "Threads", ""); N(n, GraphScale.Range); return n.ToString("#,0"); }
                case "mem.used":
                    N(mem.TotalBytes > 0 ? mem.UsedBytes * 100.0 / mem.TotalBytes : double.NaN, GraphScale.Percent);
                    return (mem.UsedBytes / 1073741824.0).ToString("0.0") + " / " + (mem.TotalBytes / 1073741824.0).ToString("0.0") + " GB";
                case "mem.percent": N(mem.Load, GraphScale.Percent); return mem.Load + "%";
                case "mem.commit":
                    N(mem.CommitLimit > 0 ? mem.CommitUsed * 100.0 / mem.CommitLimit : double.NaN, GraphScale.Percent);
                    return (mem.CommitUsed / 1073741824.0).ToString("0.0") + " / " + (mem.CommitLimit / 1073741824.0).ToString("0.0") + " GB";
                case "mem.top": return tops[1];
                case "gpu.load": return P(gpu["*"]);
                case "gpu.dedicated": N(gpu["dedicated"], GraphScale.FromZero); return Size(gpu["dedicated"]);
                case "gpu.shared": N(gpu["shared"], GraphScale.FromZero); return Size(gpu["shared"]);
                case "inet.ping": { string t = Ping(host); N(pingMs, GraphScale.FromZero); return t; }
                case "inet.status": { bool on = NetworkInterface.GetIsNetworkAvailable(); N(on ? 1 : 0, GraphScale.OnOff); return on ? "online" : "offline"; }
                case "inet.ip": return LocalIp();
                case "bat.percent":
                    var p = System.Windows.Forms.SystemInformation.PowerStatus;
                    N(Math.Round(p.BatteryLifePercent * 100), GraphScale.Percent);
                    return Math.Round(p.BatteryLifePercent * 100) + "%" + (p.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? " (plugged in)" : "");
                case "bat.time":
                    int secs = System.Windows.Forms.SystemInformation.PowerStatus.BatteryLifeRemaining;
                    N(secs < 0 ? double.NaN : secs / 60.0, GraphScale.FromZero);
                    return secs < 0 ? "plugged in" : Duration(TimeSpan.FromSeconds(secs));
                case "sys.uptime": return Duration(TimeSpan.FromMilliseconds(GetTickCount64()));
                case "sys.pc": return Environment.MachineName;
                case "sys.user": return Environment.UserName;
            }
            var parts = id.Split('|');
            switch (parts[0])
            {
                case "core":
                    if (coreNames == null) coreNames = SensorCatalog.CoreInstances();
                    return P(Counter("Processor Information", "% Processor Utility", coreNames[int.Parse(parts[1])]));
                case "gpu":
                    double load;
                    return P(gpu.TryGetValue(parts[1], out load) ? load : 0);
                case "disk":
                    if (parts[2] == "read") return R(Counter("PhysicalDisk", "Disk Read Bytes/sec", parts[1]));
                    if (parts[2] == "write") return R(Counter("PhysicalDisk", "Disk Write Bytes/sec", parts[1]));
                    return P(100 - Counter("PhysicalDisk", "% Idle Time", parts[1]));
                case "drive":
                    var drive = new DriveInfo(parts[1]);
                    return Size(drive.AvailableFreeSpace) + " of " + Size(drive.TotalSize);
                case "net":
                    return Network(parts[1], parts[2], nics, now);
            }
            return "n/a";
        }

        static Dictionary<string, ExternalReading> Snapshot(List<ExternalReading> readings)
        {
            var map = new Dictionary<string, ExternalReading>();
            if (readings != null) foreach (var r in readings) map[r.Id] = r;
            return map;
        }

        string Network(string nicId, string what, Dictionary<string, NetworkInterface> nics, long now)
        {
            NetworkInterface nic;
            if (!nics.TryGetValue(nicId, out nic)) return "not found";
            bool up = nic.OperationalStatus == OperationalStatus.Up;
            if (what == "link") return up ? LinkSpeed(nic.Speed) : "disconnected";
            if (what == "signal" || what == "ssid")
            {
                if (!up) return "disconnected";
                int signal; string ssid;
                if (!Wlan.Current(new Guid(nicId), out signal, out ssid)) return "n/a";
                if (what == "signal") N(signal, GraphScale.Percent);
                return what == "signal" ? signal + "%" : ssid;
            }
            if (what == "down" || what == "up") N(double.NaN, GraphScale.FromZero);   // still a graph while disconnected (with a gap)
            var stats = nic.GetIPStatistics();
            long rx = stats.BytesReceived, tx = stats.BytesSent;
            if (what == "total") return "↓ " + Size(rx) + "  ↑ " + Size(tx);
            long[] prev;
            netPrev.TryGetValue(nicId, out prev);
            // Both directions share one sample, so it's only advanced once per tick.
            if (prev == null || prev[2] != now)
            {
                var sample = new[] { rx, tx, now, prev == null ? rx : prev[0], prev == null ? tx : prev[1], prev == null ? now : prev[2] };
                netPrev[nicId] = sample;
                prev = sample;
            }
            double seconds = (prev[2] - prev[5]) / (double)Stopwatch.Frequency;
            if (seconds <= 0) return up ? "0 B/s" : "disconnected";
            double bytes = what == "down" ? prev[0] - prev[3] : prev[1] - prev[4];
            return up ? R(Math.Max(0, bytes / seconds)) : "disconnected";
        }

        /// <summary>
        /// Graphics engines: Windows reports a load per app per engine; an engine's load is the sum over apps,
        /// and the GPU's load is its busiest engine (the same way Task Manager shows it).
        /// </summary>
        Dictionary<string, double> GpuReadings()
        {
            var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "*", 0 }, { "dedicated", 0 }, { "shared", 0 } };
            try
            {
                var data = new PerformanceCounterCategory("GPU Engine").ReadCategory();
                var util = data["utilization percentage"];
                var next = new Dictionary<string, CounterSample>();
                var perEngine = new Dictionary<string, double>();
                var typeOf = new Dictionary<string, string>();
                if (util != null)
                    foreach (InstanceData inst in util.Values)
                    {
                        string name = inst.InstanceName;
                        next[name] = inst.Sample;
                        CounterSample old;
                        if (!gpuPrev.TryGetValue(name, out old)) continue;
                        double v = CounterSample.Calculate(old, inst.Sample);
                        int at = name.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
                        string engine = at >= 0 ? name.Substring(at) : name;
                        int t = name.IndexOf("engtype_", StringComparison.OrdinalIgnoreCase);
                        double sum;
                        perEngine.TryGetValue(engine, out sum);
                        perEngine[engine] = sum + v;
                        typeOf[engine] = t >= 0 ? name.Substring(t + 8) : "";
                    }
                gpuPrev = next;
                foreach (var e in perEngine)
                {
                    string type = typeOf[e.Key];
                    double cur;
                    result.TryGetValue(type, out cur);
                    result[type] = Math.Max(cur, e.Value);
                    result["*"] = Math.Max(result["*"], e.Value);
                }
            }
            catch (Exception) { }
            try
            {
                var mem = new PerformanceCounterCategory("GPU Adapter Memory").ReadCategory();
                var ded = mem["dedicated usage"];
                var sh = mem["shared usage"];
                if (ded != null) foreach (InstanceData i in ded.Values) result["dedicated"] += i.RawValue;
                if (sh != null) foreach (InstanceData i in sh.Values) result["shared"] += i.RawValue;
            }
            catch (Exception) { }
            return result;
        }

        /// <summary>[busiest app by CPU, biggest app by memory]; apps with several processes are added up.</summary>
        string[] BusiestApps(long now)
        {
            var cpu = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var memory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var next = new Dictionary<int, TimeSpan>();
            double seconds = procPrev == null ? 0 : (now - procPrevStamp) / (double)Stopwatch.Frequency;
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName;
                    if (name == "Idle" || name == "System" || name == "Memory Compression") continue;
                    long ws; try { ws = p.WorkingSet64; } catch (Exception) { ws = 0; }
                    long m; memory.TryGetValue(name, out m); memory[name] = m + ws;
                    TimeSpan t;
                    try { t = p.TotalProcessorTime; } catch (Exception) { continue; }   // some system processes can't be read
                    next[p.Id] = t;
                    TimeSpan old;
                    if (procPrev != null && procPrev.TryGetValue(p.Id, out old))
                    {
                        double c; cpu.TryGetValue(name, out c);
                        cpu[name] = c + (t - old).TotalSeconds;
                    }
                }
                catch (Exception) { }
                finally { p.Dispose(); }
            }
            procPrev = next;
            procPrevStamp = now;
            string topCpu = "…", topMem = "…";
            if (cpu.Count > 0 && seconds > 0)
            {
                var top = cpu.OrderByDescending(x => x.Value).First();
                topCpu = top.Key + " " + Pct(top.Value / seconds / Environment.ProcessorCount * 100);
            }
            if (memory.Count > 0)
            {
                var top = memory.OrderByDescending(x => x.Value).First();
                topMem = top.Key + " " + Size(top.Value);
            }
            return new[] { topCpu, topMem };
        }

        string Ping(string host)
        {
            if (Interlocked.CompareExchange(ref pinging, 1, 0) == 0)
            {
                var ping = new Ping();
                ping.PingCompleted += (s, e) =>
                {
                    bool ok = e.Error == null && e.Reply != null && e.Reply.Status == IPStatus.Success;
                    pingMs = ok ? e.Reply.RoundtripTime : double.NaN;
                    pingText = e.Error != null || e.Reply == null ? "no reply"
                        : e.Reply.Status == IPStatus.Success ? e.Reply.RoundtripTime + " ms" : "timed out";
                    ping.Dispose();
                    Interlocked.Exchange(ref pinging, 0);
                };
                try { ping.SendAsync(host, 2000, null); }
                catch (Exception) { pingText = "no reply"; pingMs = double.NaN; ping.Dispose(); Interlocked.Exchange(ref pinging, 0); }
            }
            return pingText;
        }

        static string LocalIp()
        {
            foreach (var nic in SensorCatalog.Adapters().Where(n => n.OperationalStatus == OperationalStatus.Up))
                foreach (var a in nic.GetIPProperties().UnicastAddresses)
                    if (a.Address.AddressFamily == AddressFamily.InterNetwork) return a.Address.ToString();
            return "none";
        }

        // ---------- formatting ----------

        static string Pct(double v) { return Math.Round(Math.Max(0, Math.Min(100, v))) + "%"; }

        public static string Size(double bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (bytes >= 1024 && i < units.Length - 1) { bytes /= 1024; i++; }
            return (i == 0 || bytes >= 100 ? bytes.ToString("0") : bytes.ToString("0.0")) + " " + units[i];
        }

        static string Rate(double bytesPerSecond) { return Size(bytesPerSecond) + "/s"; }

        static string LinkSpeed(long bitsPerSecond)
        {
            if (bitsPerSecond <= 0) return "unknown";
            return bitsPerSecond >= 1000000000 ? (bitsPerSecond / 1e9).ToString("0.#") + " Gbps" : (bitsPerSecond / 1e6).ToString("0") + " Mbps";
        }

        static string Duration(TimeSpan t)
        {
            if (t.TotalDays >= 1) return (int)t.TotalDays + " d " + t.Hours + " h";
            if (t.TotalHours >= 1) return (int)t.TotalHours + " h " + t.Minutes + " min";
            return Math.Max(0, (int)t.TotalMinutes) + " min";
        }

        [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
    }

    /// <summary>RAM and committed memory, from Windows.</summary>
    internal class MemoryStatus
    {
        public long TotalBytes, UsedBytes, CommitLimit, CommitUsed;
        public int Load;

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

        public static MemoryStatus Get()
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (!GlobalMemoryStatusEx(ref m)) return new MemoryStatus();
            return new MemoryStatus
            {
                TotalBytes = (long)m.ullTotalPhys, UsedBytes = (long)(m.ullTotalPhys - m.ullAvailPhys), Load = (int)m.dwMemoryLoad,
                CommitLimit = (long)m.ullTotalPageFile, CommitUsed = (long)(m.ullTotalPageFile - m.ullAvailPageFile)
            };
        }
    }

    /// <summary>Wi-Fi signal and network name, from Windows' Wi-Fi service.</summary>
    internal static class Wlan
    {
        [DllImport("wlanapi.dll")] static extern int WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr handle);
        [DllImport("wlanapi.dll")] static extern int WlanQueryInterface(IntPtr handle, ref Guid iface, int opcode, IntPtr reserved, out int size, out IntPtr data, IntPtr type);
        [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr p);

        static IntPtr handle;

        public static bool Current(Guid iface, out int signal, out string ssid)
        {
            signal = 0; ssid = "";
            try
            {
                if (handle == IntPtr.Zero)
                {
                    uint v;
                    if (WlanOpenHandle(2, IntPtr.Zero, out v, out handle) != 0) { handle = IntPtr.Zero; return false; }
                }
                int size; IntPtr data;
                if (WlanQueryInterface(handle, ref iface, 7 /* current_connection */, IntPtr.Zero, out size, out data, IntPtr.Zero) != 0) return false;
                try
                {
                    // WLAN_CONNECTION_ATTRIBUTES: state, mode, profile name (256 chars), then the association:
                    // SSID length + 32 bytes at 520, ..., signal quality at 576.
                    int len = Math.Min(32, Marshal.ReadInt32(data, 520));
                    var bytes = new byte[len];
                    Marshal.Copy(data + 524, bytes, 0, len);
                    ssid = Encoding.UTF8.GetString(bytes);
                    signal = Marshal.ReadInt32(data, 576);
                    return true;
                }
                finally { WlanFreeMemory(data); }
            }
            catch (Exception) { return false; }
        }
    }

    /// <summary>Is HWiNFO or Libre Hardware Monitor installed / running?</summary>
    internal static class MonitorApps
    {
        public const string HwinfoUrl = "https://www.hwinfo.com/download/";
        public const string LibreUrl = "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/releases";

        public static string Check()
        {
            var sb = new StringBuilder();
            string hwVersion = Installed("HWiNFO");
            bool hwFile = new[] { @"%ProgramFiles%\HWiNFO64\HWiNFO64.exe", @"%ProgramFiles(x86)%\HWiNFO32\HWiNFO32.exe", @"%ProgramFiles%\HWiNFO\HWiNFO64.exe" }
                .Any(p => File.Exists(Environment.ExpandEnvironmentVariables(p)));
            bool hwRunning = Running("HWiNFO64", "HWiNFO32", "HWiNFO");
            sb.Append("HWiNFO: ");
            if (hwVersion != null) sb.Append("installed" + (hwVersion.Length > 0 ? " (version " + hwVersion + ")" : ""));
            else if (hwFile) sb.Append("installed");
            else sb.Append(hwRunning ? "found" : "not found");
            sb.Append(hwRunning ? ", running" : hwVersion != null || hwFile ? ", not running right now." : ".");
            if (hwRunning)
            {
                string problem;
                var r = HwinfoSensors.Read(out problem);
                sb.Append(r != null ? " and sharing " + r.Count + " sensors (listed above under \"HWiNFO\")."
                    : ", but not sharing its sensors. In HWiNFO, open Settings and tick \"Shared Memory Support\" (in the free version this switches itself off after 12 hours).");
            }

            string lhmVersion = Installed("Libre ?Hardware ?Monitor");
            bool lhmRunning = SensorCatalog.LibreRunning();
            sb.Append("\nLibre Hardware Monitor: ");
            if (lhmVersion != null) sb.Append("installed" + (lhmVersion.Length > 0 ? " (version " + lhmVersion + ")" : "") + (lhmRunning ? ", running" : ", not running right now."));
            else if (lhmRunning) sb.Append("running");
            else sb.Append("not found. It has no installer (you unzip it and run it), so if you've downloaded it, start it and check again.");
            if (lhmRunning)
            {
                string problem;
                var r = LibreSensors.Read(out problem);
                sb.Append(r != null ? " and sharing " + r.Count + " sensors (listed above under \"Libre Hardware Monitor\")."
                    : ", but not sharing its sensors. Either run it as administrator (it then shares them with Windows), "
                      + "or in Libre Hardware Monitor choose Options > Remote Web Server > Run.");
            }
            return sb.ToString();
        }

        static bool Running(params string[] names)
        {
            foreach (var n in names)
            {
                var ps = Process.GetProcessesByName(n);
                bool any = ps.Length > 0;
                foreach (var p in ps) p.Dispose();
                if (any) return true;
            }
            return false;
        }

        /// <summary>The version from the "installed apps" list, "" if it has none, or null if not installed.</summary>
        static string Installed(string namePattern)
        {
            var rx = new System.Text.RegularExpressions.Regex(namePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var roots = new[]
            {
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                Tuple.Create(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                Tuple.Create(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
            };
            foreach (var r in roots)
            {
                try
                {
                    using (var key = r.Item1.OpenSubKey(r.Item2))
                    {
                        if (key == null) continue;
                        foreach (var sub in key.GetSubKeyNames())
                            using (var k = key.OpenSubKey(sub))
                            {
                                var name = k == null ? null : k.GetValue("DisplayName") as string;
                                if (name != null && rx.IsMatch(name)) return (k.GetValue("DisplayVersion") as string) ?? "";
                            }
                    }
                }
                catch (Exception) { }
            }
            return null;
        }
    }
}
