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
    // Latency of each server: time to open a TCP connection to its address and port (no authentication).
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
                if (_running.Contains(id)) continue;
                _running.Add(id);
                ThreadPool.QueueUserWorkItem(delegate
                {
                    var sw = Stopwatch.StartNew();
                    int ms = Measure(address);
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
                return new ReleaseInfo { Version = version, Tag = tag, Url = Convert.ToString(json["html_url"], CultureInfo.InvariantCulture) };
            }
        }

        public static bool IsNewer(ReleaseInfo release)
        {
            return release != null && release.Version > new Version(App.Version);
        }
    }
}
