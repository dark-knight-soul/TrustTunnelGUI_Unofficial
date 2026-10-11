using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

        public VpnState State { get; private set; }
        public string ActiveServerId { get; private set; }
        public readonly List<LogLine> Lines = new List<LogLine>();

        const int MaxLines = 5000;

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
            ActiveServerId = serverId;
            SetState(VpnState.Connecting);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configFile));
                File.WriteAllText(_configFile, config, new UTF8Encoding(false));
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
            var text = e.Data;
            _ui.BeginInvoke(new Action(delegate { HandleLine(text); }));
        }

        void HandleLine(string text)
        {
            var level = ParseLevel(text);
            AddLine(text, level);
            if (level == LogLevel.Error) _lastError = StripPrefix(text);

            int idx = text.IndexOf("raise_state:", StringComparison.Ordinal);
            if (idx < 0 || _stopRequested) return;
            if (text.IndexOf("VPN_SS_CONNECTED", idx, StringComparison.Ordinal) >= 0)
            {
                StopDisconnectedTimer();
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
            StopDisconnectedTimer();
            bool expected = _stopRequested;
            bool wasConnected = State == VpnState.Connected;
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
                var reason = string.IsNullOrEmpty(_lastError) ? "" : ": " + _lastError;
                RaiseFailed((wasConnected ? L.ConnectionLost : L.ConnectFailed) + reason);
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

    static class ConsoleCtrl
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

        [DllImport("kernel32.dll")]
        static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);

        static readonly object Sync = new object();

        // Sends Ctrl+C to a console process started with CreateNoWindow (it owns a hidden console).
        public static bool SendCtrlC(int pid)
        {
            lock (Sync)
            {
                FreeConsole();
                if (!AttachConsole((uint)pid)) return false;
                SetConsoleCtrlHandler(IntPtr.Zero, true); // do not terminate ourselves
                bool ok = GenerateConsoleCtrlEvent(0, 0);
                FreeConsole();
                // The event is delivered asynchronously; keep ignoring it for a moment.
                Thread.Sleep(300);
                SetConsoleCtrlHandler(IntPtr.Zero, false);
                return ok;
            }
        }
    }
}
