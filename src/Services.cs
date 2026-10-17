using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Threading;

namespace TrustTunnelGui
{
    // Latency of each server, without authentication: for HTTP/2 the time to open a TCP connection,
    // for HTTP/3 the round trip of a QUIC version negotiation over UDP.
    class PingService
    {
        public const int Unreachable = -1;
        const int TimeoutMs = 3000;
        const int MinVisibleMs = 600;

        public event Action Updated;

        readonly Dispatcher _ui;
        readonly Dictionary<string, int> _results = new Dictionary<string, int>();
        readonly HashSet<string> _running = new HashSet<string>();
        DateTime _lastRun = DateTime.MinValue;

        public PingService(Dispatcher ui)
        {
            _ui = ui;
        }

        // Milliseconds, Unreachable, or null while unknown.
        public int? Result(string serverId)
        {
            int ms;
            return _results.TryGetValue(serverId, out ms) ? ms : (int?)null;
        }

        public bool IsRunning(string serverId)
        {
            return _running.Contains(serverId);
        }

        public bool AnyRunning { get { return _running.Count > 0; } }

        // Local time of the last completed check, or null.
        public DateTime? LastChecked { get; private set; }

        public void PingAllIfStale(IEnumerable<ServerInfo> servers, TimeSpan maxAge)
        {
            if (DateTime.UtcNow - _lastRun > maxAge) PingAll(servers);
        }

        public void PingAll(IEnumerable<ServerInfo> servers)
        {
            _lastRun = DateTime.UtcNow;
            foreach (var s in servers)
            {
                var id = s.Id;
                var address = s.Address;
                bool quic = s.Protocol == "http3";
                if (_running.Contains(id)) continue;
                _running.Add(id);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    var sw = Stopwatch.StartNew();
                    int ms = quic ? MeasureQuic(address) : Measure(address);
                    // A fast server answers in ~20 ms; keep the progress visible long enough to be noticed.
                    var left = MinVisibleMs - (int)sw.ElapsedMilliseconds;
                    if (left > 0) Thread.Sleep(left);
                    _ui.BeginInvoke(new Action(delegate
                    {
                        _running.Remove(id);
                        _results[id] = ms;
                        LastChecked = DateTime.Now;
                        if (Updated != null) Updated();
                    }));
                });
            }
            if (Updated != null) Updated();
        }

        public static int Measure(string address)
        {
            string host;
            int port;
            if (!SplitAddress(address, out host, out port)) return Unreachable;
            try
            {
                var ips = Dns.GetHostAddresses(host);
                if (ips.Length == 0) return Unreachable;
                using (var client = new TcpClient(ips[0].AddressFamily))
                {
                    var sw = Stopwatch.StartNew();
                    var task = client.ConnectAsync(ips[0], port);
                    if (!task.Wait(TimeoutMs) || !client.Connected) return Unreachable;
                    return (int)Math.Max(1, sw.ElapsedMilliseconds);
                }
            }
            catch
            {
                return Unreachable;
            }
        }

        // Sends a QUIC Initial with a reserved version (RFC 9000, section 6.3): a QUIC server answers with
        // Version Negotiation without starting a handshake. The datagram is padded to 1200 bytes,
        // the smallest one a server responds to.
        public static int MeasureQuic(string address)
        {
            string host;
            int port;
            if (!SplitAddress(address, out host, out port)) return Unreachable;
            try
            {
                var ips = Dns.GetHostAddresses(host);
                if (ips.Length == 0) return Unreachable;
                using (var udp = new UdpClient(ips[0].AddressFamily))
                {
                    var packet = new byte[1200];
                    var cid = new byte[8];
                    new Random().NextBytes(cid);
                    packet[0] = 0xC0;
                    packet[1] = 0x1A; packet[2] = 0x2A; packet[3] = 0x3A; packet[4] = 0x4A;
                    packet[5] = 8;
                    Array.Copy(cid, 0, packet, 6, 8);
                    packet[14] = 8;
                    Array.Copy(cid, 0, packet, 15, 8);

                    udp.Connect(ips[0], port);
                    var sw = Stopwatch.StartNew();
                    udp.Send(packet, packet.Length);
                    var receive = udp.ReceiveAsync();
                    if (!receive.Wait(TimeoutMs)) return Unreachable;
                    var reply = receive.Result.Buffer;
                    bool negotiation = reply.Length >= 7 && (reply[0] & 0x80) != 0
                        && reply[1] == 0 && reply[2] == 0 && reply[3] == 0 && reply[4] == 0;
                    return negotiation ? (int)Math.Max(1, sw.ElapsedMilliseconds) : Unreachable;
                }
            }
            catch
            {
                return Unreachable;
            }
        }

        // host, host:port, ipv4, ipv4:port, ipv6, [ipv6]:port
        public static bool SplitAddress(string address, out string host, out int port)
        {
            address = (address ?? "").Trim();
            port = Defaults.Port;
            host = address;
            if (address.Length == 0) return false;
            if (address.StartsWith("["))
            {
                int close = address.IndexOf(']');
                if (close < 0) return false;
                host = address.Substring(1, close - 1);
                var rest = address.Substring(close + 1);
                return rest.Length == 0 || (rest.StartsWith(":") && int.TryParse(rest.Substring(1), out port));
            }
            if (address.Split(':').Length > 2) return true; // bare IPv6
            int colon = address.LastIndexOf(':');
            if (colon > 0)
            {
                host = address.Substring(0, colon);
                return int.TryParse(address.Substring(colon + 1), out port);
            }
            return true;
        }
    }

    class ReleaseInfo
    {
        public Version Version;
        public string Tag;
        public string Url;
        // The installer asset; SetupSha256 is null when GitHub gives no digest, and then it is not installed automatically.
        public string SetupName;
        public string SetupUrl;
        public long SetupSize;
        public string SetupSha256;
    }

    // Looks for a newer release on GitHub.
    static class UpdateChecker
    {
        public const string Repo = "dark-knight-soul/TrustTunnelGUI_Unofficial";
        public const string RepoUrl = "https://github.com/" + Repo;

        public static ReleaseInfo Latest()
        {
            // .NET Framework apps without a target framework attribute default to old TLS versions.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + Repo + "/releases/latest");
            request.UserAgent = "TrustTunnelGUI/" + App.Version;
            request.Accept = "application/vnd.github+json";
            request.Timeout = 10000;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
                var tag = Convert.ToString(json["tag_name"], CultureInfo.InvariantCulture);
                Version version;
                if (!Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out version)) return null;
                var release = new ReleaseInfo { Version = version, Tag = tag, Url = Convert.ToString(json["html_url"], CultureInfo.InvariantCulture) };
                var setupName = "TrustTunnelGUI-" + tag.TrimStart('v', 'V') + "-setup.exe";
                object assets;
                if (json.TryGetValue("assets", out assets) && assets is System.Collections.IEnumerable)
                {
                    foreach (var item in (System.Collections.IEnumerable)assets)
                    {
                        var asset = item as Dictionary<string, object>;
                        if (asset == null || Field(asset, "name") != setupName) continue;
                        release.SetupName = setupName;
                        release.SetupUrl = Field(asset, "browser_download_url");
                        release.SetupSize = Convert.ToInt64(asset["size"], CultureInfo.InvariantCulture);
                        var digest = Field(asset, "digest");
                        if (digest != null && digest.StartsWith("sha256:", StringComparison.Ordinal))
                            release.SetupSha256 = digest.Substring(7).ToLowerInvariant();
                    }
                }
                return release;
            }
        }

        static string Field(Dictionary<string, object> json, string key)
        {
            object value;
            return json.TryGetValue(key, out value) && value != null ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;
        }

        public static bool IsNewer(ReleaseInfo release)
        {
            return release != null && release.Version > new Version(App.Version);
        }
    }

    // Downloads the installer of a release and runs it in update mode (it closes the app, installs and starts it again).
    // The file is checked against the SHA-256 digest GitHub publishes for the asset.
    static class Updater
    {
        const string DirPrefix = "TrustTunnelGUI-update-";

        // Only an installed copy updates itself; a portable one keeps opening the release page.
        public static bool CanInstall(ReleaseInfo release)
        {
            return App.IsInstalled && release.SetupSha256 != null && release.SetupSize > 0 &&
                   release.SetupUrl != null && release.SetupUrl.StartsWith(UpdateChecker.RepoUrl + "/releases/download/", StringComparison.Ordinal);
        }

        // Each download gets a new folder, so nothing prepared in advance can stand in for it.
        public static string Download(ReleaseInfo release)
        {
            var dir = Path.Combine(Path.GetTempPath(), DirPrefix + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                return Download(release, Path.Combine(dir, release.SetupName));
            }
            catch
            {
                try { Directory.Delete(dir, true); } catch { }
                throw;
            }
        }

        static string Download(ReleaseInfo release, string path)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(release.SetupUrl);
            request.UserAgent = "TrustTunnelGUI/" + App.Version;
            request.Timeout = 15000;
            request.ReadWriteTimeout = 30000;
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                if (response.ResponseUri.Scheme != Uri.UriSchemeHttps) throw new IOException("Download redirected to " + response.ResponseUri.Scheme);
                using (var src = response.GetResponseStream())
                using (var dst = File.Create(path))
                {
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = src.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        total += read;
                        if (total > release.SetupSize) throw new IOException("The download is larger than expected");
                        dst.Write(buffer, 0, read);
                    }
                }
            }
            using (var file = File.OpenRead(path))
                if (!Matches(file, release)) throw new IOException("Checksum mismatch");
            return path;
        }

        // The file stays open without write or delete sharing until the installer has started: nothing can swap it
        // after the check (an elevated app starts it without a UAC prompt). Throws Win32Exception if UAC is declined.
        public static void Start(string path, ReleaseInfo release)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (!Matches(file, release)) throw new IOException("Checksum mismatch");
                var args = "--update" + (App.IsElevated ? " --run-elevated" : "");
                Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) });
            }
        }

        // Folders of earlier updates; the one of a running installer is skipped.
        public static void CleanUp()
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), DirPrefix + "*"))
                    try { Directory.Delete(dir, true); } catch { }
            }
            catch { }
        }

        static bool Matches(Stream file, ReleaseInfo release)
        {
            if (file.Length != release.SetupSize) return false;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant() == release.SetupSha256;
        }
    }
}
