using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace TrustTunnelGui
{
    public enum VpnState { Disconnected, Connecting, Connected, Disconnecting }

    public enum LogLevel { Trace, Debug, Info, Warn, Error }

    public class LogLine
    {
        public string Text { get; set; }
        public LogLevel Level { get; set; }

        public System.Windows.Media.Brush Brush
        {
            get
            {
                switch (Level)
                {
                    case LogLevel.Error: return C.LogError;
                    case LogLevel.Warn: return C.LogWarn;
                    case LogLevel.Debug:
                    case LogLevel.Trace: return C.LogDebug;
                    default: return C.NeutralBlack;
                }
            }
        }
    }

    // Runs trusttunnel_client.exe and tracks its state from "raise_state: [..] VPN_SS_*" log lines.
    class VpnManager
    {
        public event Action StateChanged;
        public event Action<LogLine> LineAdded;
        public event Action LogCleared;
        public event Action<string> Failed;
        // The client stopped on its own (crash, lost connection, core gave up); argument: it had been connected.
        public event Action<bool> UnexpectedExit;
        public event Action<QueryEntry> QueryAdded;
        public event Action QueriesCleared;

        public VpnState State { get; private set; }
        public string ActiveServerId { get; private set; }
        public readonly List<LogLine> Lines = new List<LogLine>();
        public readonly List<QueryEntry> Queries = new List<QueryEntry>();

        const int MaxLines = 5000;
        const int MaxQueries = 3000;

        readonly QueryLogParser _queryParser = new QueryLogParser();
        readonly System.Collections.Concurrent.ConcurrentQueue<string> _incoming = new System.Collections.Concurrent.ConcurrentQueue<string>();
        DispatcherTimer _drainTimer;
        bool _reachedConnected;
        bool _coreGaveUp;

        readonly string _clientExe;
        readonly string _configFile;
        readonly string _logFile;
        readonly Dispatcher _ui;

        Process _process;
        bool _stopRequested;
        string _lastError;
        Func<string> _pendingConfig;
        string _pendingServerId;
        DispatcherTimer _disconnectedTimer;
        StreamWriter _logWriter;

        public VpnManager(string clientExe, string configFile, string logFile, Dispatcher ui)
        {
            _clientExe = clientExe;
            _configFile = configFile;
            _logFile = logFile;
            _ui = ui;
            State = VpnState.Disconnected;
            // Client output is handled in batches: debug logging can produce thousands of lines per second.
            _drainTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, delegate { Drain(); }, ui);
            _drainTimer.Start();
            _queryParser.Added += delegate(QueryEntry q)
            {
                Queries.Add(q);
                if (Queries.Count > MaxQueries) Queries.RemoveRange(0, Queries.Count - MaxQueries);
                if (QueryAdded != null) QueryAdded(q);
            };
            // Left over after a crash or power loss; it holds the server password in plain text.
            DeleteConfig();
        }

        // The config holds the password in plain text: only the current user, administrators and SYSTEM may open it,
        // whatever the folder allows (a portable copy may sit in a shared folder or on a USB drive).
        void WriteConfig(string config)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_configFile));
            DeleteConfig();
            var security = new FileSecurity();
            security.SetAccessRuleProtection(true, false);
            foreach (var sid in new[] { WindowsIdentity.GetCurrent().User, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null) })
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
            var bytes = new UTF8Encoding(false).GetBytes(config);
            using (var stream = File.Create(_configFile, 4096, FileOptions.None, security))
                stream.Write(bytes, 0, bytes.Length);
        }

        void Drain()
        {
            string line;
            int budget = 5000;
            while (budget-- > 0 && _incoming.TryDequeue(out line)) HandleLine(line);
        }

        public void ClearQueries()
        {
            Queries.Clear();
            _queryParser.Reset();
            if (QueriesCleared != null) QueriesCleared();
        }

        public bool ClientExists { get { return File.Exists(_clientExe); } }

        public bool IsBusy { get { return State != VpnState.Disconnected; } }

        // configFactory is evaluated right before launch so a reconnect picks up fresh settings.
        public void Connect(string serverId, Func<string> configFactory, string logLevel)
        {
            if (_process != null)
            {
                _pendingServerId = serverId;
                _pendingConfig = configFactory;
                _pendingLogLevel = logLevel;
                ActiveServerId = serverId;
                StopCurrent();
                return;
            }
            Start(serverId, configFactory(), logLevel);
        }

        string _pendingLogLevel;

        void Start(string serverId, string config, string logLevel)
        {
            if (config == null)
            {
                SetState(VpnState.Disconnected);
                return;
            }
            if (!ClientExists)
            {
                RaiseFailed(L.ClientNotFound);
                return;
            }
            _stopRequested = false;
            _lastError = null;
            _reachedConnected = false;
            _coreGaveUp = false;
            ActiveServerId = serverId;
            SetState(VpnState.Connecting);
            try
            {
                WriteConfig(config);
                OpenLogWriter();
                AddLine("---- " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ----", LogLevel.Info);

                var psi = new ProcessStartInfo(_clientExe, "-c \"" + _configFile + "\" -l " + logLevel)
                {
                    WorkingDirectory = Path.GetDirectoryName(_clientExe),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
                p.OutputDataReceived += OnData;
                p.ErrorDataReceived += OnData;
                p.Exited += OnExited;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                _process = p;
            }
            catch (Exception ex)
            {
                _process = null;
                DeleteConfig();
                SetState(VpnState.Disconnected);
                RaiseFailed(L.ConnectFailed + ": " + ex.Message);
            }
        }

        public void Disconnect()
        {
            _pendingServerId = null;
            _pendingConfig = null;
            StopCurrent();
        }

        void StopCurrent()
        {
            var p = _process;
            if (p == null)
            {
                SetState(VpnState.Disconnected);
                return;
            }
            if (_stopRequested) return;
            _stopRequested = true;
            SetState(VpnState.Disconnecting);
            ThreadPool.QueueUserWorkItem(delegate { StopProcess(p, 8000); });
        }

        // Blocking variant used when the application exits.
        public void DisconnectAndWait(int timeoutMs)
        {
            var p = _process;
            _pendingServerId = null;
            _pendingConfig = null;
            if (p == null) return;
            _stopRequested = true;
            StopProcess(p, timeoutMs);
        }

        // Ask the client to shut down gracefully (it restores routes and DNS), kill it on timeout.
        static void StopProcess(Process p, int timeoutMs)
        {
            try
            {
                if (p.HasExited) return;
                if (ConsoleCtrl.SendCtrlC(p.Id) && p.WaitForExit(timeoutMs)) return;
                if (!p.HasExited) p.Kill();
                p.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Log.Write("Failed to stop client: " + ex.Message);
            }
        }

        public void ClearLog()
        {
            Lines.Clear();
            if (LogCleared != null) LogCleared();
        }

        void OnData(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            _incoming.Enqueue(e.Data);
        }

        void HandleLine(string text)
        {
            var level = ParseLevel(text);
            AddLine(text, level);
            if (level == LogLevel.Error) _lastError = StripPrefix(text);
            _queryParser.Feed(text);

            int idx = text.IndexOf("raise_state:", StringComparison.Ordinal);
            if (idx < 0 || _stopRequested) return;
            if (text.IndexOf("VPN_SS_CONNECTED", idx, StringComparison.Ordinal) >= 0)
            {
                StopDisconnectedTimer();
                _reachedConnected = true;
                SetState(VpnState.Connected);
            }
            else if (text.IndexOf("VPN_SS_DISCONNECTED", idx, StringComparison.Ordinal) >= 0)
            {
                // The core gave up; if the process does not exit by itself, stop it.
                SetState(VpnState.Connecting);
                StartDisconnectedTimer();
            }
            else if (text.IndexOf("VPN_SS_", idx, StringComparison.Ordinal) >= 0)
            {
                StopDisconnectedTimer();
                SetState(VpnState.Connecting);
            }
        }

        void StartDisconnectedTimer()
        {
            StopDisconnectedTimer();
            _disconnectedTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Normal, delegate
            {
                StopDisconnectedTimer();
                if (_process == null || _stopRequested) return;
                var error = _lastError;
                _coreGaveUp = true;
                Disconnect();
                RaiseFailed(L.ConnectFailed + (string.IsNullOrEmpty(error) ? "" : ": " + error));
            }, _ui);
            _disconnectedTimer.Start();
        }

        void StopDisconnectedTimer()
        {
            if (_disconnectedTimer != null) _disconnectedTimer.Stop();
            _disconnectedTimer = null;
        }

        void OnExited(object sender, EventArgs e)
        {
            var p = (Process)sender;
            int code = -1;
            try
            {
                // Make sure the asynchronous readers have drained the pipes.
                p.WaitForExit();
                code = p.ExitCode;
            }
            catch { }
            _ui.BeginInvoke(new Action(delegate { HandleExit(p, code); }));
        }

        void HandleExit(Process p, int code)
        {
            if (p != _process) return;
            Drain();
            StopDisconnectedTimer();
            bool expected = _stopRequested && !_coreGaveUp;
            bool wasConnected = State == VpnState.Connected;
            bool everConnected = _reachedConnected;
            bool coreGaveUp = _coreGaveUp;
            _queryParser.Reset();
            _process = null;
            _stopRequested = false;
            p.Dispose();
            DeleteConfig();
            AddLine("---- exit code " + code + " ----", code == 0 ? LogLevel.Info : LogLevel.Warn);
            CloseLogWriter();
            SetState(VpnState.Disconnected);

            if (_pendingConfig != null)
            {
                var id = _pendingServerId;
                var cfg = _pendingConfig;
                var lvl = _pendingLogLevel;
                _pendingServerId = null;
                _pendingConfig = null;
                Start(id, cfg(), lvl);
                return;
            }
            if (!expected)
            {
                // When the core gave up the failure message has already been shown.
                if (!coreGaveUp)
                {
                    var reason = string.IsNullOrEmpty(_lastError) ? "" : ": " + _lastError;
                    RaiseFailed((wasConnected ? L.ConnectionLost : L.ConnectFailed) + reason);
                }
                if (UnexpectedExit != null) UnexpectedExit(everConnected);
            }
        }

        void SetState(VpnState s)
        {
            if (State == s) return;
            State = s;
            if (StateChanged != null) StateChanged();
        }

        void RaiseFailed(string message)
        {
            if (Failed != null) Failed(message);
        }

        void AddLine(string text, LogLevel level)
        {
            var line = new LogLine { Text = text, Level = level };
            Lines.Add(line);
            if (Lines.Count > MaxLines) Lines.RemoveRange(0, Lines.Count - MaxLines);
            try { if (_logWriter != null) _logWriter.WriteLine(text); } catch { }
            if (LineAdded != null) LineAdded(line);
        }

        void OpenLogWriter()
        {
            CloseLogWriter();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_logFile));
                if (File.Exists(_logFile) && new FileInfo(_logFile).Length > 10 * 1024 * 1024)
                    File.Copy(_logFile, _logFile + ".old", true);
                bool append = File.Exists(_logFile) && new FileInfo(_logFile).Length <= 10 * 1024 * 1024;
                _logWriter = new StreamWriter(_logFile, append, new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                Log.Write("Cannot open client log: " + ex.Message);
            }
        }

        void CloseLogWriter()
        {
            try { if (_logWriter != null) _logWriter.Dispose(); } catch { }
            _logWriter = null;
        }

        void DeleteConfig()
        {
            // The config contains the password in plain text; keep it on disk only while the client runs.
            try { if (File.Exists(_configFile)) File.Delete(_configFile); } catch { }
        }

        static LogLevel ParseLevel(string line)
        {
            if (line.Contains(" ERROR ")) return LogLevel.Error;
            if (line.Contains(" WARN ")) return LogLevel.Warn;
            if (line.Contains(" DEBUG ")) return LogLevel.Debug;
            if (line.Contains(" TRACE ")) return LogLevel.Trace;
            return LogLevel.Info;
        }

        // "11.10.2026 08:59:17.632327 ERROR [123] MODULE func: message" -> "MODULE func: message"
        static string StripPrefix(string line)
        {
            int b = line.IndexOf("] ", StringComparison.Ordinal);
            var s = b >= 0 ? line.Substring(b + 2) : line;
            return s.Length > 200 ? s.Substring(0, 200) + "…" : s;
        }
    }

    // One routed connection for the connection log (Flutter "query log" analogue).
    public class QueryEntry : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public DateTime Time { get; set; }
        public string Source { get; set; }
        public string Destination { get; set; }
        public string Protocol { get; set; }
        string _domain;
        string _action;
        long _download = -1, _upload = -1;

        public string Domain { get { return _domain; } set { _domain = value; Changed("Domain"); Changed("Title"); } }
        public string Action { get { return _action; } set { _action = value; Changed("Action"); Changed("ActionText"); Changed("ActionBrush"); } }

        public void SetTraffic(long download, long upload)
        {
            _download = download;
            _upload = upload;
            Changed("Subtitle");
        }

        public string TimeText { get { return Time.ToString("HH:mm:ss"); } }

        public string Title { get { return !string.IsNullOrEmpty(_domain) ? _domain : HostOf(Destination); } }

        public string Subtitle
        {
            get
            {
                var s = Destination + " · " + Protocol;
                if (_download >= 0) s += " · ↓ " + Bytes(_download) + " ↑ " + Bytes(_upload);
                return s;
            }
        }

        public string ActionText
        {
            get
            {
                switch (_action)
                {
                    case "TUNNEL": return L.Vpn;
                    case "BYPASS": return L.Bypass;
                    case "REJECT": return L.Blocked;
                    default: return _action ?? "";
                }
            }
        }

        public System.Windows.Media.Brush ActionBrush
        {
            get
            {
                switch (_action)
                {
                    case "TUNNEL": return C.Accent;
                    case "REJECT": return C.Error;
                    default: return C.PowerOff;
                }
            }
        }

        public bool Matches(string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return (Title ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (Destination ?? "").IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   ActionText.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string HostOf(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint)) return "";
            if (endpoint.StartsWith("["))
            {
                int close = endpoint.IndexOf(']');
                return close > 0 ? endpoint.Substring(1, close - 1) : endpoint;
            }
            int colon = endpoint.LastIndexOf(':');
            return colon > 0 && endpoint.IndexOf(':') == colon ? endpoint.Substring(0, colon) : endpoint;
        }

        static string Bytes(long b)
        {
            if (b < 1024) return b + " B";
            if (b < 1024 * 1024) return (b / 1024.0).ToString("0.#") + " KB";
            return (b / 1024.0 / 1024.0).ToString("0.#") + " MB";
        }

        void Changed(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }

    // Builds connection records from the client's per-connection debug lines:
    //   "[L:12-R:34] New client connection request: 10.0.0.2:5000->1.2.3.4:443 (proto: TCP)"
    //   "[L:12-R:34] Found domain in SNI data: example.com"
    //   "[L:12-R:34] Final action: VPN_FCA_TUNNEL | VPN_FCA_BYPASS | VPN_FCA_REJECT"
    //   "[L:12-R:34] Destroyed (download=1024, upload=512)"
    class QueryLogParser
    {
        static readonly System.Text.RegularExpressions.Regex ConnRx =
            new System.Text.RegularExpressions.Regex(@"\[L:(\d+)-R:(\d+)\] (.+)$", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex RequestRx =
            new System.Text.RegularExpressions.Regex(@"^New client connection request: (\S+)->(\S+) \(proto: (\w+)\)", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex DomainRx =
            new System.Text.RegularExpressions.Regex(@"^Found domain in \S+ data: (\S+)", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex ActionRx =
            new System.Text.RegularExpressions.Regex(@"^Final action: VPN_FCA_(\w+)", System.Text.RegularExpressions.RegexOptions.Compiled);
        static readonly System.Text.RegularExpressions.Regex DestroyedRx =
            new System.Text.RegularExpressions.Regex(@"^Destroyed \(download=(\d+), upload=(\d+)\)", System.Text.RegularExpressions.RegexOptions.Compiled);

        public event Action<QueryEntry> Added;

        readonly Dictionary<string, QueryEntry> _open = new Dictionary<string, QueryEntry>();
        readonly Dictionary<string, bool> _emitted = new Dictionary<string, bool>();
        readonly Queue<string> _order = new Queue<string>();

        public void Reset()
        {
            _open.Clear();
            _emitted.Clear();
            _order.Clear();
        }

        public void Feed(string line)
        {
            if (line.IndexOf("[L:", StringComparison.Ordinal) < 0) return;
            var m = ConnRx.Match(line);
            if (!m.Success) return;
            var key = m.Groups[1].Value + "-" + m.Groups[2].Value;
            var message = m.Groups[3].Value;
            QueryEntry entry;
            _open.TryGetValue(key, out entry);

            var r = RequestRx.Match(message);
            if (r.Success)
            {
                entry = new QueryEntry { Time = DateTime.Now, Source = r.Groups[1].Value, Destination = r.Groups[2].Value, Protocol = r.Groups[3].Value.ToUpperInvariant() };
                _open[key] = entry;
                _emitted.Remove(key);
                _order.Enqueue(key);
                while (_order.Count > 5000)
                {
                    var old = _order.Dequeue();
                    _open.Remove(old);
                    _emitted.Remove(old);
                }
                return;
            }
            if (entry == null) return;

            var d = DomainRx.Match(message);
            if (d.Success)
            {
                entry.Domain = d.Groups[1].Value;
                return;
            }
            var a = ActionRx.Match(message);
            if (a.Success)
            {
                entry.Action = a.Groups[1].Value.ToUpperInvariant();
                if (!_emitted.ContainsKey(key))
                {
                    _emitted[key] = true;
                    if (Added != null) Added(entry);
                }
                return;
            }
            var x = DestroyedRx.Match(message);
            if (x.Success)
            {
                entry.SetTraffic(long.Parse(x.Groups[1].Value), long.Parse(x.Groups[2].Value));
                _open.Remove(key);
            }
        }
    }
}
