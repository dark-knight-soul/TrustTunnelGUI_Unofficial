using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Color = System.Drawing.Color;
using Size = System.Drawing.Size;
using ImageSource = System.Windows.Media.ImageSource;
using WinForms = System.Windows.Forms;

[assembly: AssemblyTitle("TrustTunnel GUI")]
[assembly: AssemblyProduct("TrustTunnel GUI")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace TrustTunnelGui
{
    class App : Application
    {
        public static App I;
        public static string BaseDir;
        public static string ClientExe;
        public static string WizardExe;
        public static string DataDir;
        public static string ExePath;
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        public static readonly bool IsElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        const string MutexName = "Local\\TrustTunnelGUI_Instance";
        const string ShowEventName = "Local\\TrustTunnelGUI_Show";

        public string LogDir;
        public Store Store;
        public VpnManager Vpn;
        public MainWindow Window;
        Tray _tray;
        string _clientVersion;
        static Mutex _mutex;
        bool _autostart;

        [STAThread]
        static int Main(string[] args)
        {
            InitPaths();
#if DEVTOOLS
            if (DevTools.Run(args)) return 0;
#endif
            // After an elevation restart the previous instance may still be shutting down.
            if (!AcquireInstance(args.Contains("--wait-instance") ? 15000 : 0))
            {
                SignalRunningInstance();
                return 0;
            }
            EventWaitHandle showEvent = null;
            try { showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName); }
            catch (Exception ex) { Log.Write("Show event unavailable: " + ex.Message); }

            string connectId = null;
            int idx = Array.IndexOf(args, "--connect");
            if (idx >= 0 && idx + 1 < args.Length) connectId = args[idx + 1];

            var app = new App();
            if (!app.Init(args.Contains("--autostart"), showEvent, connectId)) return 0;
            app.Run();
            return 0;
        }

        static bool AcquireInstance(int waitMs)
        {
            try
            {
                _mutex = new Mutex(false, MutexName);
                try { return _mutex.WaitOne(waitMs); }
                catch (AbandonedMutexException) { return true; }
            }
            catch (UnauthorizedAccessException)
            {
                // The running instance is elevated; a non-elevated process cannot open its mutex.
                return false;
            }
        }

        static void SignalRunningInstance()
        {
            try
            {
                EventWaitHandle.OpenExisting(ShowEventName).Set();
            }
            catch
            {
                L.Apply("system");
                MessageBox.Show(L.AlreadyRunning, L.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        static void ReleaseInstance()
        {
            try { if (_mutex != null) { _mutex.ReleaseMutex(); _mutex.Dispose(); } } catch { }
            _mutex = null;
        }

        // Starts a new elevated copy (UAC prompt) and closes this one. Returns false if the user declined.
        public bool RestartElevated(string connectServerId)
        {
            var args = "--wait-instance" + (_autostart ? " --autostart" : "") + (connectServerId != null ? " --connect " + connectServerId : "");
            try
            {
                Process.Start(new ProcessStartInfo(ExePath, args) { UseShellExecute = true, Verb = "runas", WorkingDirectory = BaseDir });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Log.Write("Elevation declined: " + ex.Message);
                return false;
            }
            Log.Write("Restarting elevated");
            if (_tray != null) _tray.Dispose();
            if (Window != null) { Window.AllowClose = true; Window.Close(); }
            if (Vpn != null) Vpn.DisconnectAndWait(8000);
            ReleaseInstance();
            // During Init the dispatcher loop has not started yet; Main simply returns then.
            if (Window != null) Shutdown();
            return true;
        }

        public void AskRestartElevated(string connectServerId)
        {
            ShowMainWindow();
            Window.ShowDialog(L.AdminRequired, UI.BodyMedium(L.AdminRequiredDescription),
                DialogAction.Cancel(),
                new DialogAction(L.Restart, delegate { RestartElevated(connectServerId); return true; }) { IsDefault = true });
        }

        // Applies theme and language from the settings; rebuilds the open window when it already exists.
        public void ApplyAppearance(Action restore)
        {
            Theme.Install(this, Store.Data.Settings.Theme);
            L.Apply(Store.Data.Settings.Language);
            if (Window != null) Window.Rebuild(restore);
            if (_tray != null) _tray.Update();
        }

        public static bool IsPortable;

        public static void InitPaths()
        {
            ExePath = Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(ExePath);

            // The client is shipped next to the exe; development builds also find it in a "client" folder up the tree.
            BaseDir = exeDir;
            var dir = exeDir;
            for (int level = 0; level < 4 && dir != null; level++, dir = Path.GetDirectoryName(dir))
            {
                if (File.Exists(Path.Combine(dir, "trusttunnel_client.exe"))) { BaseDir = dir; break; }
                if (File.Exists(Path.Combine(dir, "client", "trusttunnel_client.exe"))) { BaseDir = Path.Combine(dir, "client"); break; }
            }
            ClientExe = Path.Combine(BaseDir, "trusttunnel_client.exe");
            WizardExe = Path.Combine(BaseDir, "setup_wizard.exe");

            // Portable build: a "portable" marker file next to the exe keeps all data in .\data.
            IsPortable = File.Exists(Path.Combine(exeDir, "portable"));
            DataDir = IsPortable
                ? Path.Combine(exeDir, "data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrustTunnelGUI");
        }

        // Returns false when the app restarted itself elevated and this instance should not run.
        public bool Init(bool autostart, EventWaitHandle showEvent, string connectServerId)
        {
            I = this;
            _autostart = autostart;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LogDir = Path.Combine(DataDir, "logs");
            Directory.CreateDirectory(LogDir);
            Log.File = Path.Combine(LogDir, "gui.log");
            Log.Write("Start " + Version + (autostart ? " (autostart)" : "") + (IsElevated ? " (elevated)" : ""));

            DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Write("UI error: " + e.Exception);
                e.Handled = true;
                if (Window != null) Window.ShowSnack(L.SomethingWrong);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Log.Write("Fatal: " + e.ExceptionObject);
                if (Vpn != null) Vpn.DisconnectAndWait(5000);
            };
            SessionEnding += delegate { Vpn.DisconnectAndWait(5000); };

            Store = new Store(Path.Combine(DataDir, "data.json"));
            ImportLegacyConfig();
            var settings = Store.Data.Settings;

            Theme.Install(this, settings.Theme);
            L.Apply(settings.Language);

            if (settings.RunAsAdmin && !IsElevated && RestartElevated(connectServerId)) return false;

            Vpn = new VpnManager(ClientExe, Path.Combine(DataDir, "trusttunnel_client.toml"), Path.Combine(LogDir, "client.log"), Dispatcher);
            Vpn.Failed += OnVpnFailed;

            Window = new MainWindow();
            _tray = new Tray(this);
            Vpn.StateChanged += _tray.Update;

            if (showEvent != null)
            {
                var waiter = new Thread(delegate()
                {
                    while (true)
                    {
                        showEvent.WaitOne();
                        Dispatcher.BeginInvoke(new Action(ShowMainWindow));
                    }
                }) { IsBackground = true };
                waiter.Start();
            }

            // Follow the Windows light/dark setting while the theme is "system".
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += delegate(object s, Microsoft.Win32.UserPreferenceChangedEventArgs e)
            {
                if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (Store.Data.Settings.Theme == "system" && Theme.Resolve("system") != C.IsDark) ApplyAppearance(null);
                }));
            };

            if (!autostart || settings.OpenMainWindowOnLaunch || connectServerId != null) Window.Show();

            var toConnect = connectServerId != null ? Store.Server(connectServerId) : null;
            if (toConnect == null && settings.AutoConnectOnLaunch)
                toConnect = Store.Server(settings.SelectedServerId) ?? Store.Data.Servers.FirstOrDefault();
            if (toConnect != null) Connect(toConnect);
            return true;
        }

        // The old WinForms GUI kept the config next to the exe; import it once as the first server.
        void ImportLegacyConfig()
        {
            var settings = Store.Data.Settings;
            if (settings.LegacyImportDone) return;
            settings.LegacyImportDone = true;
            var legacy = Path.Combine(BaseDir, "trusttunnel_client.toml");
            try
            {
                if (Store.Data.Servers.Count == 0 && File.Exists(legacy))
                {
                    var server = ServerImport.FromClientConfig(File.ReadAllText(legacy));
                    if (server != null && server.Address.Length > 0)
                    {
                        Store.Data.Servers.Add(server);
                        settings.SelectedServerId = server.Id;
                        Log.Write("Imported legacy config " + legacy);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Legacy import failed: " + ex.Message);
            }
            Store.Save();
        }

        // ---- VPN actions ----

        // ServersCard.onPressed: toggles the picked server or switches to another one.
        public void ToggleServer(ServerInfo server)
        {
            if (Vpn.ActiveServerId == server.Id && Vpn.State != VpnState.Disconnected)
            {
                if (Vpn.State != VpnState.Disconnecting) Vpn.Disconnect();
                return;
            }
            Connect(server);
        }

        public void Connect(ServerInfo server)
        {
            Store.Data.Settings.SelectedServerId = server.Id;
            Store.Save();
            if (Store.Data.Settings.SocksAdapterMissing)
            {
                OnVpnFailed(L.AdapterUnavailable);
                return;
            }
            // TUN needs an elevated client; SOCKS mode works with normal rights.
            if (!Store.Data.Settings.IsSocks && !IsElevated)
            {
                AskRestartElevated(server.Id);
                return;
            }
            var id = server.Id;
            Vpn.Connect(id, delegate { return BuildConfig(id); }, Store.Data.Settings.LogLevel == "debug" ? "debug" : "info");
        }

        public void Disconnect()
        {
            Vpn.Disconnect();
        }

        string BuildConfig(string serverId)
        {
            var server = Store.Server(serverId);
            if (server == null) return null;
            var profile = Store.Profile(server.RoutingProfileId) ?? Store.Profile(RoutingProfile.DefaultId);
            return ConfigBuilder.Build(server, profile, Store.Data.Settings);
        }

        // Restarts the tunnel when settings used by the active connection change.
        public void ReconnectIfActive(Func<ServerInfo, bool> affected)
        {
            if (Vpn.State == VpnState.Disconnected || Vpn.State == VpnState.Disconnecting) return;
            var server = Store.Server(Vpn.ActiveServerId);
            if (server != null && affected(server)) Connect(server);
        }

        public void DeleteServer(ServerInfo server)
        {
            if (Vpn.ActiveServerId == server.Id && Vpn.State != VpnState.Disconnected) Vpn.Disconnect();
            Store.Data.Servers.Remove(server);
            if (Store.Data.Settings.SelectedServerId == server.Id) Store.Data.Settings.SelectedServerId = "";
            Store.Save();
        }

        void OnVpnFailed(string message)
        {
            if (Window.IsVisible) Window.ShowSnack(message);
            else _tray.Balloon(L.AppName, message, true);
        }

        public ServerInfo SelectedServer
        {
            get { return Store.Server(Store.Data.Settings.SelectedServerId) ?? Store.Data.Servers.FirstOrDefault(); }
        }

        // ---- Import ----

        public ServerInfo ImportDeepLink(string link)
        {
            if (!link.StartsWith("tt://", StringComparison.OrdinalIgnoreCase)) throw new Exception(L.ImportFailed);
            return RunWizard("--deeplink \"" + link.Replace("\"", "") + "\"");
        }

        public ServerInfo ImportConfigFile(string path)
        {
            var text = File.ReadAllText(path);
            if (text.Contains("[endpoint]"))
            {
                var server = ServerImport.FromClientConfig(text);
                if (server == null) throw new Exception(L.ImportFailed);
                return server;
            }
            // Endpoint-generated client config: let setup_wizard convert it.
            return RunWizard("--endpoint_config \"" + path + "\"");
        }

        ServerInfo RunWizard(string args)
        {
            if (!File.Exists(WizardExe)) throw new Exception("setup_wizard.exe не найден");
            var tmp = Path.Combine(Path.GetTempPath(), "trusttunnel_import_" + Guid.NewGuid().ToString("N") + ".toml");
            try
            {
                var psi = new ProcessStartInfo(WizardExe, "--mode non-interactive " + args + " --settings \"" + tmp + "\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    WorkingDirectory = BaseDir
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardInput.Close();
                    var stdoutTask = p.StandardOutput.ReadToEndAsync();
                    var stderrTask = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(20000))
                    {
                        try { p.Kill(); } catch { }
                        throw new Exception(L.ImportFailed);
                    }
                    var output = (stderrTask.Result + "\n" + stdoutTask.Result).Trim();
                    if (p.ExitCode != 0 || !File.Exists(tmp))
                    {
                        var line = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && l.ToLowerInvariant().Contains("error"));
                        throw new Exception(L.ImportFailed + (line != null ? ": " + line : ""));
                    }
                }
                var server = ServerImport.FromClientConfig(File.ReadAllText(tmp));
                if (server == null) throw new Exception(L.ImportFailed);
                return server;
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        public void GetClientVersion(Action<string> callback)
        {
            if (_clientVersion != null) { callback(_clientVersion); return; }
            ThreadPool.QueueUserWorkItem(delegate
            {
                string v = "—";
                try
                {
                    var psi = new ProcessStartInfo(ClientExe, "--version") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                    using (var p = Process.Start(psi))
                    {
                        var output = p.StandardOutput.ReadToEnd();
                        p.WaitForExit(5000);
                        var line = output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
                        if (line != null) v = line;
                    }
                }
                catch { }
                _clientVersion = v;
                Dispatcher.BeginInvoke(new Action(delegate { callback(v); }));
            });
        }

        // ---- Window / exit ----

        public void ShowMainWindow()
        {
            if (!Window.IsVisible) Window.Show();
            if (Window.WindowState == WindowState.Minimized) Window.WindowState = WindowState.Normal;
            Window.Activate();
            Window.Topmost = true;
            Window.Topmost = false;
            Window.Focus();
        }

        public void OnWindowHidden()
        {
            if (Store.Data.Settings.TrayHintShown) return;
            Store.Data.Settings.TrayHintShown = true;
            Store.Save();
            _tray.Balloon(L.TrayHiddenTitle, L.TrayHiddenText, false);
        }

        // WindowsExitDialog: confirm when a connection is active.
        public void RequestExit()
        {
            if (Vpn.State == VpnState.Disconnected)
            {
                DoExit();
                return;
            }
            ShowMainWindow();
            Window.ShowDialog(L.ExitTitle, UI.BodyMedium(L.ExitDescription),
                DialogAction.Cancel(L.DontQuit),
                new DialogAction(L.Quit, delegate { DoExit(); return true; }) { Danger = true });
        }

        void DoExit()
        {
            Log.Write("Exit");
            _tray.Dispose();
            Window.AllowClose = true;
            Window.Close();
            Vpn.DisconnectAndWait(8000);
            Shutdown();
        }

        // ---- Helpers ----

        public static ImageSource LoadImage(string name)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) return null;
            return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }

        public static ImageSource LoadLargestIcon(string name)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) return null;
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        }

        public static Stream Resource(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        // explorer.exe hands the request to the user's shell, so the browser is not started elevated.
        public static void OpenUrl(string url)
        {
            try { Process.Start("explorer.exe", url); } catch { }
        }

        public static void OpenFolder(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch { }
        }
    }

    // Notification area icon with the same menu as the Flutter TrayMenuScope.
    class Tray : IDisposable
    {
        readonly App _app;
        readonly WinForms.NotifyIcon _icon;
        readonly WinForms.ContextMenuStrip _menu;
        readonly Icon _on, _off, _busy;

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        public Tray(App app)
        {
            _app = app;
            var size = WinForms.SystemInformation.SmallIconSize;
            _off = MakeIcon(size, null, true);
            _busy = MakeIcon(size, Color.FromArgb(0xF0, 0x84, 0x00), false);
            _on = MakeIcon(size, Color.FromArgb(0x4C, 0xAF, 0x50), false);

            _menu = new WinForms.ContextMenuStrip { ShowImageMargin = false, Font = new Font("Segoe UI", 9f) };
            _menu.Opening += delegate { BuildMenu(); };
            _icon = new WinForms.NotifyIcon { ContextMenuStrip = _menu, Visible = true };
            _icon.MouseClick += delegate(object s, WinForms.MouseEventArgs e)
            {
                if (e.Button == WinForms.MouseButtons.Left) _app.ShowMainWindow();
            };
            _icon.BalloonTipClicked += delegate { _app.ShowMainWindow(); };
            Update();
        }

        internal static Icon MakeIcon(Size size, Color? dot, bool gray)
        {
            using (var stream = App.Resource("app_icon.ico"))
            using (var source = new Icon(stream, size))
            using (var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    var rect = new Rectangle(0, 0, size.Width, size.Height);
                    using (var src = source.ToBitmap())
                    {
                        if (gray)
                        {
                            var matrix = new ColorMatrix(new[]
                            {
                                new[] { 0.3f, 0.3f, 0.3f, 0, 0 },
                                new[] { 0.59f, 0.59f, 0.59f, 0, 0 },
                                new[] { 0.11f, 0.11f, 0.11f, 0, 0 },
                                new[] { 0, 0, 0, 0.75f, 0 },
                                new[] { 0, 0, 0, 0, 1f }
                            });
                            using (var attrs = new ImageAttributes())
                            {
                                attrs.SetColorMatrix(matrix);
                                g.DrawImage(src, rect, 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
                            }
                        }
                        else
                        {
                            g.DrawImage(src, rect);
                        }
                    }
                    if (dot.HasValue)
                    {
                        int d = Math.Max(6, size.Width * 7 / 16);
                        var r = new Rectangle(size.Width - d, size.Height - d, d - 1, d - 1);
                        using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, Rectangle.Inflate(r, 1, 1));
                        using (var fill = new SolidBrush(dot.Value)) g.FillEllipse(fill, r);
                    }
                }
                var handle = bmp.GetHicon();
                var icon = (Icon)Icon.FromHandle(handle).Clone();
                DestroyIcon(handle);
                return icon;
            }
        }

        public void Update()
        {
            var vpn = _app.Vpn;
            string status;
            switch (vpn.State)
            {
                case VpnState.Connected: _icon.Icon = _on; status = L.Connected; break;
                case VpnState.Connecting: _icon.Icon = _busy; status = L.Connecting; break;
                case VpnState.Disconnecting: _icon.Icon = _busy; status = L.Disconnecting; break;
                default: _icon.Icon = _off; status = L.Disconnected; break;
            }
            var server = _app.Store.Server(vpn.ActiveServerId);
            var text = L.AppName + " — " + status + (server != null && vpn.State != VpnState.Disconnected ? " (" + server.Name + ")" : "");
            _icon.Text = text.Length > 63 ? text.Substring(0, 62) + "…" : text;
        }

        void BuildMenu()
        {
            _menu.Items.Clear();
            _menu.Renderer = new WinForms.ToolStripProfessionalRenderer(new TrayColors());
            _menu.BackColor = TrayColors.Back;
            _menu.ForeColor = TrayColors.Fore;
            var vpn = _app.Vpn;
            string status = vpn.State == VpnState.Connected ? L.Connected
                : vpn.State == VpnState.Connecting ? L.Connecting
                : vpn.State == VpnState.Disconnecting ? L.Disconnecting : L.Disconnected;
            _menu.Items.Add(new WinForms.ToolStripMenuItem(string.Format(L.TrayStatus, status)) { Enabled = false });

            var selected = _app.SelectedServer;
            if (vpn.State == VpnState.Disconnected)
            {
                var connect = new WinForms.ToolStripMenuItem(selected != null ? L.Connect + " (" + selected.Name + ")" : L.Connect) { Enabled = selected != null };
                connect.Click += delegate { if (selected != null) _app.Connect(selected); };
                _menu.Items.Add(connect);
            }
            else
            {
                var disconnect = new WinForms.ToolStripMenuItem(L.Disconnect) { Enabled = vpn.State != VpnState.Disconnecting };
                disconnect.Click += delegate { _app.Disconnect(); };
                _menu.Items.Add(disconnect);
            }

            var servers = _app.Store.Data.Servers;
            if (servers.Count > 0)
            {
                var connectTo = new WinForms.ToolStripMenuItem(L.ConnectTo);
                connectTo.DropDown.Renderer = _menu.Renderer;
                connectTo.DropDown.BackColor = TrayColors.Back;
                foreach (var s in servers)
                {
                    var server = s;
                    var item = new WinForms.ToolStripMenuItem(server.Name)
                    {
                        Checked = vpn.State != VpnState.Disconnected && vpn.ActiveServerId == server.Id
                    };
                    item.ForeColor = TrayColors.Fore;
                    item.Click += delegate { _app.Connect(server); };
                    connectTo.DropDownItems.Add(item);
                }
                _menu.Items.Add(connectTo);
            }

            _menu.Items.Add(new WinForms.ToolStripSeparator());
            var open = new WinForms.ToolStripMenuItem(L.TrayOpen) { Font = new Font(_menu.Font, System.Drawing.FontStyle.Bold) };
            open.Click += delegate { _app.ShowMainWindow(); };
            _menu.Items.Add(open);
            var quit = new WinForms.ToolStripMenuItem(L.TrayQuit);
            quit.Click += delegate { _app.Dispatcher.BeginInvoke(new Action(_app.RequestExit)); };
            _menu.Items.Add(quit);
        }

        public void Balloon(string title, string text, bool error)
        {
            _icon.ShowBalloonTip(5000, title, text, error ? WinForms.ToolTipIcon.Warning : WinForms.ToolTipIcon.Info);
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
        }
    }

    // Tray menu colours that follow the app theme.
    class TrayColors : WinForms.ProfessionalColorTable
    {
        static Color Hex(string hex)
        {
            var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return Color.FromArgb(c.A, c.R, c.G, c.B);
        }

        public static Color Back { get { return Hex(C.P.Elevated); } }
        public static Color Fore { get { return Hex(C.P.NeutralBlack); } }
        static Color Hover { get { return Hex(C.P.BackgroundAdditional); } }
        static Color Border { get { return Hex(C.IsDark ? "#3A414B" : "#D1D8E0"); } }

        public override Color ToolStripDropDownBackground { get { return Back; } }
        public override Color MenuBorder { get { return Border; } }
        public override Color MenuItemBorder { get { return Hover; } }
        public override Color MenuItemSelected { get { return Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
        public override Color ImageMarginGradientBegin { get { return Back; } }
        public override Color ImageMarginGradientMiddle { get { return Back; } }
        public override Color ImageMarginGradientEnd { get { return Back; } }
        public override Color SeparatorDark { get { return Border; } }
        public override Color SeparatorLight { get { return Back; } }
        public override Color CheckBackground { get { return Hover; } }
        public override Color CheckSelectedBackground { get { return Hover; } }
    }

    // "Launch at login" through Task Scheduler: the app needs administrator rights,
    // and a scheduled task with the highest run level starts it without a UAC prompt.
    static class Autostart
    {
        const string TaskName = "TrustTunnel GUI";

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "TrustTunnel GUI";

        public static bool TaskExists()
        {
            return Schtasks("/Query /TN \"" + TaskName + "\"") == 0;
        }

        static bool RunKeyExists()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && key.GetValue(RunValue) != null;
            }
            catch { return false; }
        }

        public static bool IsEnabled()
        {
            return RunKeyExists() || TaskExists();
        }

        // Elevated: a scheduled task with the highest run level (starts elevated without UAC).
        // Not elevated: the per-user Run key (starts with normal rights).
        public static bool Set(bool enable, string exePath)
        {
            if (!enable)
            {
                SetRunKey(null);
                if (TaskExists() && Schtasks("/Delete /TN \"" + TaskName + "\" /F") != 0) return false;
                return !IsEnabled();
            }
            if (!App.IsElevated) return SetRunKey("\"" + exePath + "\" --autostart");
            SetRunKey(null);

            var user = WindowsIdentity.GetCurrent().Name;
            var xml =
"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\n" +
"<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\n" +
"  <RegistrationInfo><Description>Starts TrustTunnel GUI at logon</Description></RegistrationInfo>\n" +
"  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + Esc(user) + "</UserId><Delay>PT5S</Delay></LogonTrigger></Triggers>\n" +
"  <Principals><Principal id=\"Author\"><UserId>" + Esc(user) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\n" +
"  <Settings>\n" +
"    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>\n" +
"    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>\n" +
"    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>\n" +
"    <AllowHardTerminate>true</AllowHardTerminate>\n" +
"    <StartWhenAvailable>false</StartWhenAvailable>\n" +
"    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>\n" +
"    <IdleSettings><StopOnIdleEnd>false</StopOnIdleEnd><RestartOnIdle>false</RestartOnIdle></IdleSettings>\n" +
"    <AllowStartOnDemand>true</AllowStartOnDemand>\n" +
"    <Enabled>true</Enabled>\n" +
"    <Hidden>false</Hidden>\n" +
"    <RunOnlyIfIdle>false</RunOnlyIfIdle>\n" +
"    <WakeToRun>false</WakeToRun>\n" +
"    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>\n" +
"    <Priority>7</Priority>\n" +
"  </Settings>\n" +
"  <Actions Context=\"Author\"><Exec><Command>" + Esc(exePath) + "</Command><Arguments>--autostart</Arguments><WorkingDirectory>" + Esc(Path.GetDirectoryName(exePath)) + "</WorkingDirectory></Exec></Actions>\n" +
"</Task>\n";
            var file = Path.Combine(Path.GetTempPath(), "trusttunnel_gui_task.xml");
            try
            {
                File.WriteAllText(file, xml, Encoding.Unicode);
                return Schtasks("/Create /TN \"" + TaskName + "\" /XML \"" + file + "\" /F") == 0;
            }
            finally
            {
                try { File.Delete(file); } catch { }
            }
        }

        static bool SetRunKey(string command)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (command == null) { if (key.GetValue(RunValue) != null) key.DeleteValue(RunValue); }
                    else key.SetValue(RunValue, command);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Run key: " + ex.Message);
                return false;
            }
        }

        static string Esc(string s)
        {
            return SecurityElement.Escape(s);
        }

        static int Schtasks(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", args)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    p.WaitForExit(10000);
                    if (p.ExitCode != 0) Log.Write("schtasks " + args + " -> " + p.ExitCode);
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Log.Write("schtasks failed: " + ex.Message);
                return -1;
            }
        }
    }
}
