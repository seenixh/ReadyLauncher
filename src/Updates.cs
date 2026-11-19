using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web.Script.Serialization;

namespace ReadyLauncher
{
    /// <summary>The newest release on GitHub.</summary>
    internal class UpdateInfo
    {
        public Version Version;
        public string PageUrl, ExeUrl, Notes;
        public string VersionText { get { return Updates.Text(Version); } }
    }

    /// <summary>Checking GitHub for a newer Ready Launcher, downloading it, and swapping it in.</summary>
    internal static class Updates
    {
        public const string ProjectUrl = "https://github.com/seenixh/ReadyLauncher";
        const string LatestApi = "https://api.github.com/repos/seenixh/ReadyLauncher/releases/latest";
        const string ExeName = "ReadyLauncher";

        /// <summary>This copy's version (a test copy can pretend to be older with READYLAUNCHER_FAKE_VERSION).</summary>
        public static Version Current
        {
            get
            {
                Version v;
                string fake = Program.TestMode ? Environment.GetEnvironmentVariable("READYLAUNCHER_FAKE_VERSION") : null;
                if (!string.IsNullOrEmpty(fake) && Version.TryParse(fake, out v)) return Normalize(v);
                return Normalize(Assembly.GetExecutingAssembly().GetName().Version);
            }
        }

        public static string CurrentText { get { return Text(Current); } }

        /// <summary>"1.7" or "1.7.2".</summary>
        public static string Text(Version v)
        {
            return v.Build > 0 ? v.ToString(3) : v.ToString(2);
        }

        static Version Normalize(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }

        /// <summary>"v1.7.0", "1.7" or "Ready Launcher 1.7" -> 1.7.0; null if there's no version in it.</summary>
        public static Version Parse(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            int start = 0;
            while (start < tag.Length && !char.IsDigit(tag[start])) start++;
            int end = start;
            while (end < tag.Length && (char.IsDigit(tag[end]) || tag[end] == '.')) end++;
            string s = tag.Substring(start, end - start).Trim('.');
            if (s.IndexOf('.') < 0) s += ".0";
            Version v;
            return Version.TryParse(s, out v) ? Normalize(v) : null;
        }

        static HttpWebRequest Request(string url)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch (Exception) { }
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = ExeName + "/" + CurrentText;
            req.Timeout = 20000;
            req.ReadWriteTimeout = 20000;
            return req;
        }

        /// <summary>Ask GitHub for the newest release. Throws with a readable message if it can't be reached.</summary>
        public static UpdateInfo Latest()
        {
            string url = LatestApi;
            if (Program.TestMode && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("READYLAUNCHER_UPDATE_URL")))
                url = Environment.GetEnvironmentVariable("READYLAUNCHER_UPDATE_URL");
            string json;
            try
            {
                var req = Request(url);
                req.Accept = "application/vnd.github+json";
                using (var resp = req.GetResponse())
                using (var r = new StreamReader(resp.GetResponseStream()))
                    json = r.ReadToEnd();
            }
            catch (WebException ex)
            {
                throw new Exception(Explain(ex), ex);
            }
            var o = new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            if (o == null) throw new Exception("GitHub's answer couldn't be read.");
            string tag = Get(o, "tag_name");
            var v = Parse(tag);
            if (v == null) throw new Exception("The newest release's version (\"" + tag + "\") couldn't be read.");
            var info = new UpdateInfo { Version = v, PageUrl = Get(o, "html_url") ?? ProjectUrl + "/releases/latest", Notes = Get(o, "body") ?? "" };
            object assets;
            if (o.TryGetValue("assets", out assets) && assets is object[])
            {
                foreach (var a in (object[])assets)
                {
                    var d = a as Dictionary<string, object>;
                    if (d == null) continue;
                    string name = Get(d, "name") ?? "", link = Get(d, "browser_download_url");
                    if (link == null || !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (info.ExeUrl == null || name.Equals(ExeName + ".exe", StringComparison.OrdinalIgnoreCase)) info.ExeUrl = link;
                }
            }
            return info;
        }

        static string Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) ? v as string : null;
        }

        static string Explain(WebException ex)
        {
            var http = ex.Response as HttpWebResponse;
            if (http != null)
            {
                if (http.StatusCode == HttpStatusCode.NotFound) return "No releases were found on GitHub.";
                if ((int)http.StatusCode == 403 || (int)http.StatusCode == 429)
                    return "GitHub is limiting how often this network can check. Try again in an hour.";
                return "GitHub answered with an error (" + (int)http.StatusCode + " " + http.StatusDescription + ").";
            }
            switch (ex.Status)
            {
                case WebExceptionStatus.NameResolutionFailure:
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.Timeout:
                    return "GitHub couldn't be reached. Check your internet connection.";
                default:
                    return ex.Message;
            }
        }

        /// <summary>
        /// Download the release's ReadyLauncher.exe to a temporary folder and make sure it really is a newer
        /// Ready Launcher. Returns the file's path. <paramref name="progress"/> gets 0-100 (from a background thread).
        /// </summary>
        public static string Download(UpdateInfo u, Action<int> progress)
        {
            string dir = Path.Combine(Path.GetTempPath(), ExeName + "-update");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, ExeName + "-" + u.VersionText + ".exe");
            try
            {
                var req = Request(u.ExeUrl);
                using (var resp = req.GetResponse())
                using (var input = resp.GetResponseStream())
                using (var output = File.Create(file))
                {
                    long total = resp.ContentLength, done = 0;
                    var buffer = new byte[65536];
                    int n, last = -1;
                    while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, n);
                        done += n;
                        int pct = total > 0 ? (int)(done * 100 / total) : 0;
                        if (pct != last && progress != null) { last = pct; progress(pct); }
                    }
                }
            }
            catch (WebException ex)
            {
                throw new Exception(Explain(ex), ex);
            }

            // Only swap in something that is a Ready Launcher, and newer than this one.
            AssemblyName name;
            try { name = AssemblyName.GetAssemblyName(file); }
            catch (Exception) { name = null; }
            if (name == null || !string.Equals(name.Name, ExeName, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(file);
                throw new Exception("The downloaded file isn't " + Program.AppName + ".");
            }
            if (Normalize(name.Version) <= Current)
            {
                TryDelete(file);
                throw new Exception("The downloaded file is version " + Text(Normalize(name.Version)) + ", which isn't newer than this one.");
            }
            return file;
        }

        /// <summary>The copy that was replaced by an update (removed the next time the launcher starts).</summary>
        public static string OldCopy { get { return Assembly.GetExecutingAssembly().Location + ".old"; } }

        /// <summary>
        /// Put <paramref name="downloaded"/> in place of the running ReadyLauncher.exe (Windows allows renaming a running
        /// program, not overwriting it). Returns null on success, or what went wrong (with everything put back).
        /// </summary>
        public static string Install(string downloaded)
        {
            string exe = Assembly.GetExecutingAssembly().Location, old = OldCopy;
            try
            {
                if (File.Exists(old)) { File.SetAttributes(old, FileAttributes.Normal); File.Delete(old); }
                File.Move(exe, old);
            }
            catch (Exception ex)
            {
                return "The update couldn't be installed, because " + Path.GetFileName(exe) + " can't be replaced in its folder (" + ex.Message
                     + "). You can download it from the project page instead.";
            }
            try { File.Copy(downloaded, exe); }
            catch (Exception ex)
            {
                try { File.Move(old, exe); } catch (Exception) { }
                return "The update couldn't be installed (" + ex.Message + ").";
            }
            try { File.SetAttributes(old, FileAttributes.Hidden); } catch (Exception) { }
            TryDelete(downloaded);
            return null;
        }

        /// <summary>Undo <see cref="Install"/> (the new copy couldn't be started).</summary>
        public static void Restore()
        {
            string exe = Assembly.GetExecutingAssembly().Location, old = OldCopy;
            try
            {
                if (!File.Exists(old)) return;
                if (File.Exists(exe)) File.Delete(exe);
                File.Move(old, exe);
                File.SetAttributes(exe, FileAttributes.Normal);
            }
            catch (Exception) { }
        }

        /// <summary>Remove the copy an update replaced. Returns false while it's still in use (the old copy is closing).</summary>
        public static bool CleanUp()
        {
            string old = OldCopy;
            if (!File.Exists(old)) return true;
            try
            {
                File.SetAttributes(old, FileAttributes.Normal);
                File.Delete(old);
                return true;
            }
            catch (Exception) { return false; }
        }

        static void TryDelete(string file)
        {
            try { File.Delete(file); } catch (Exception) { }
        }
    }
}
