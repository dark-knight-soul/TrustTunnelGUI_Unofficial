#if DEVTOOLS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TrustTunnelGui
{
    // Development-only hooks (built with /define:DEVTOOLS): offscreen screenshots and self-tests.
    static class DevTools
    {
        public static bool Run(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--screenshots") { Screenshots(args[1]); return true; }
            if (args.Length >= 3 && args[0] == "--test-stop") { TestStop(args[1], args[2]); return true; }
            if (args.Length >= 3 && args[0] == "--test-config") { TestConfig(args[1], args[2]); return true; }
            if (args.Length >= 3 && args[0] == "--test-vpn") { TestVpn(args[1], args[2]); return true; }
            if (args.Length >= 2 && args[0] == "--print-paths")
            {
                File.WriteAllText(args[1], "portable=" + App.IsPortable + "\nbase=" + App.BaseDir + "\nclient=" + App.ClientExe +
                    " exists=" + File.Exists(App.ClientExe) + "\ndata=" + App.DataDir + "\n");
                return true;
            }
            return false;
        }

        // Full VpnManager cycle with a SOCKS listener config (no TUN, no system changes).
        static void TestVpn(string toml, string resultFile)
        {
            var sb = new StringBuilder();
            var sw = Stopwatch.StartNew();
            var dispatcher = Dispatcher.CurrentDispatcher;
            var dir = Path.Combine(Path.GetTempPath(), "tt_gui_test");
            var vpn = new VpnManager(App.ClientExe, Path.Combine(dir, "c.toml"), Path.Combine(dir, "client.log"), dispatcher);
            vpn.StateChanged += delegate { sb.AppendLine(sw.ElapsedMilliseconds + " ms: state " + vpn.State); };
            vpn.Failed += delegate(string m) { sb.AppendLine(sw.ElapsedMilliseconds + " ms: failed " + m); };
            var frame = new DispatcherFrame();
            vpn.Connect("srv", delegate { return File.ReadAllText(toml); }, "info");
            var t1 = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            t1.Tick += delegate
            {
                t1.Stop();
                sb.AppendLine("config exists while running: " + File.Exists(Path.Combine(dir, "c.toml")));
                vpn.Disconnect();
            };
            t1.Start();
            var t2 = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            t2.Tick += delegate { t2.Stop(); sb.AppendLine("timeout"); frame.Continue = false; };
            t2.Start();
            vpn.StateChanged += delegate { if (vpn.State == VpnState.Disconnected) frame.Continue = false; };
            Dispatcher.PushFrame(frame);
            sb.AppendLine("config deleted after stop: " + !File.Exists(Path.Combine(dir, "c.toml")));
            sb.AppendLine("log lines: " + vpn.Lines.Count + ", last: " + vpn.Lines.Last().Text);
            File.WriteAllText(resultFile, sb.ToString(), new UTF8Encoding(false));
        }

        static void TestConfig(string sampleToml, string outDir)
        {
            var sb = new StringBuilder();
            var imported = ServerImport.FromClientConfig(File.ReadAllText(sampleToml));
            sb.AppendLine("import: name=" + imported.Name + " address=" + imported.Address + " domain=" + imported.Domain +
                " user=" + imported.Username + " pass=" + imported.Password + " proto=" + imported.Protocol +
                " ipv6=" + imported.Ipv6 + " dns=" + string.Join("|", imported.DnsServers) + " cert=" + imported.Certificate.Length);

            var server = new ServerInfo
            {
                Name = "Test", Address = "1.2.3.4", Domain = "vpn.example.com", Username = "user",
                Protocol = "http3", DnsServers = new List<string> { "tls://1.1.1.1", "8.8.8.8" }, ClientRandom = "abcd/ffff",
                Certificate = "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----", Ipv6 = false
            };
            server.Password = "p\"a\\ss";
            var profile = new RoutingProfile { Name = "P", DefaultMode = "vpn", BypassRules = new List<string> { "example.com", "*.test.org", "10.1.0.0/16", "1.1.1.1:53" } };
            var cfg = ConfigBuilder.Build(server, profile, new AppSettings());
            File.WriteAllText(Path.Combine(outDir, "built.toml"), cfg, new UTF8Encoding(false));

            var socksSettings = new AppSettings { ListenerMode = "socks", SocksPort = 18081, SocksUsername = "u" };
            socksSettings.SocksPassword = "p";
            var local = new ServerInfo { Name = "Local", Address = "127.0.0.1:44399", Domain = "vpn.example.com", Username = "user" };
            local.Password = "pass";
            File.WriteAllText(Path.Combine(outDir, "built_socks.toml"), ConfigBuilder.Build(local, profile, socksSettings), new UTF8Encoding(false));

            // LAN adapter binding.
            foreach (var a in Net.Adapters()) sb.AppendLine("adapter: " + a.Label + " virtual=" + a.IsVirtual + " id=" + a.Id);
            var lan = new AppSettings { ListenerMode = "socks", SocksPort = 1080 };
            sb.AppendLine("lan off: " + lan.SocksAddress);
            lan.SocksAllowLan = true;
            sb.AppendLine("lan all: " + lan.SocksAddress + " hint=" + string.Join(",", Net.ProxyLanAddresses(lan)));
            var first = Net.Adapters().FirstOrDefault();
            if (first != null)
            {
                lan.SocksAdapterId = first.Id;
                sb.AppendLine("lan adapter: " + lan.SocksAddress + " missing=" + lan.SocksAdapterMissing + " hint=" + string.Join(",", Net.ProxyLanAddresses(lan)));
                var adapterCfg = ConfigBuilder.Build(local, profile, lan);
                sb.AppendLine("config: " + adapterCfg.Split('\n').First(l => l.StartsWith("address =")).Trim());
            }
            lan.SocksAdapterId = "{00000000-0000-0000-0000-000000000000}";
            sb.AppendLine("lan gone adapter: " + lan.SocksAddress + " missing=" + lan.SocksAdapterMissing);

            // TUN outbound adapter (bound_if).
            foreach (var a in Net.OutboundAdapters()) sb.AppendLine("outbound: " + a.Label + " index=" + a.Index);
            var tun = new AppSettings();
            sb.AppendLine("tun auto: bound_if=\"" + tun.TunBoundInterface + "\" missing=" + tun.TunAdapterMissing);
            var outbound = Net.OutboundAdapters().FirstOrDefault();
            if (outbound != null)
            {
                tun.TunAdapterId = outbound.Id;
                var tunCfg = ConfigBuilder.Build(local, profile, tun);
                sb.AppendLine("tun adapter: " + tunCfg.Split('\n').First(l => l.StartsWith("bound_if")).Trim() + " missing=" + tun.TunAdapterMissing);
            }
            tun.TunAdapterId = "{00000000-0000-0000-0000-000000000000}";
            sb.AppendLine("tun gone adapter: missing=" + tun.TunAdapterMissing + " bound_if=" + (tun.TunBoundInterface ?? "null"));

            var back = ServerImport.FromClientConfig(cfg);
            sb.AppendLine("roundtrip: address=" + back.Address + " pass=" + back.Password + " proto=" + back.Protocol + " cr=" + back.ClientRandom + " cert=" + back.Certificate.Replace("\n", "\\n"));
            sb.AppendLine("valid addr: " + string.Join(",", new[] { "1.2.3.4", "1.2.3.4:443", "vpn.example.com", "vpn.example.com:8443", "::1", "[::1]:443", "bad host", "1.2.3.4:99999" }.Select(a => a + "=" + Validation.IsAddress(a))));
            sb.AppendLine("valid dns: " + string.Join(",", new[] { "8.8.8.8", "8.8.8.8:53", "tls://1.1.1.1", "https://dns.adguard.com/dns-query", "dns.google" }.Select(a => a + "=" + Validation.IsDnsUpstream(a))));
            sb.AppendLine("valid rules: " + string.Join(",", new[] { "example.com", "*.example.com", "1.2.3.4", "1.2.3.4:80", "10.0.0.0/8", "[::1]:80", "bad rule!" }.Select(a => a + "=" + Validation.IsRule(a))));
            sb.AppendLine("client random: " + string.Join(",", new[] { "abcd", "abc", "abcd/ff", "zz", "abcd/ffff" }.Select(a => a + "=" + (Validation.ClientRandomError(a) ?? "ok"))));
            File.WriteAllText(Path.Combine(outDir, "test_config.txt"), sb.ToString(), new UTF8Encoding(false));

            // Tray icons at 32 px, saved side by side.
            using (var strip = new System.Drawing.Bitmap(32 * 3 + 16, 32))
            using (var g = System.Drawing.Graphics.FromImage(strip))
            {
                g.Clear(System.Drawing.Color.FromArgb(32, 32, 32));
                var size = new System.Drawing.Size(32, 32);
                var icons = new[]
                {
                    Tray.MakeIcon(size, null, true),
                    Tray.MakeIcon(size, System.Drawing.Color.FromArgb(0xF0, 0x84, 0x00), false),
                    Tray.MakeIcon(size, System.Drawing.Color.FromArgb(0x4C, 0xAF, 0x50), false)
                };
                for (int i = 0; i < icons.Length; i++) g.DrawIcon(icons[i], i * 40, 0);
                strip.Save(Path.Combine(outDir, "tray_icons.png"), System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        static void TestStop(string toml, string resultFile)
        {
            var sb = new StringBuilder();
            var psi = new ProcessStartInfo(App.ClientExe, "-c \"" + toml + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = App.BaseDir
            };
            var p = new Process { StartInfo = psi };
            var lines = new List<string>();
            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (lines) lines.Add(e.Data); };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (lines) lines.Add(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            Thread.Sleep(3000);
            var sw = Stopwatch.StartNew();
            bool sent = ConsoleCtrl.SendCtrlC(p.Id);
            bool exited = p.WaitForExit(8000);
            sb.AppendLine("ctrl+c sent=" + sent + " exited=" + exited + " in " + sw.ElapsedMilliseconds + " ms");
            if (!exited) { p.Kill(); p.WaitForExit(); }
            else p.WaitForExit();
            sb.AppendLine("exit code=" + p.ExitCode);
            lock (lines) foreach (var l in lines.Skip(Math.Max(0, lines.Count - 25))) sb.AppendLine(l);
            File.WriteAllText(resultFile, sb.ToString(), new UTF8Encoding(false));
        }

        static void Screenshots(string outDir)
        {
            Directory.CreateDirectory(outDir);
            var dataDir = Path.Combine(outDir, "data");
            if (Directory.Exists(dataDir)) Directory.Delete(dataDir, true);
            App.DataDir = dataDir;

            var app = new App();
            App.I = app;
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Theme.Install(app, "light");
            L.Apply("ru");
            app.LogDir = Path.Combine(dataDir, "logs");
            Directory.CreateDirectory(app.LogDir);
            app.Store = new Store(Path.Combine(dataDir, "data.json"));
            app.Store.Data.Settings.LegacyImportDone = true;
            app.Vpn = new VpnManager(App.ClientExe, Path.Combine(dataDir, "c.toml"), Path.Combine(app.LogDir, "client.log"), app.Dispatcher);

            var w = new MainWindow { Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false };
            app.Window = w;
            w.Show();

            var steps = new List<KeyValuePair<string, Action>>();
            steps.Add(Step("01_servers_empty", delegate { w.SelectTab(0); }));
            steps.Add(Step("02_servers_list", delegate
            {
                var s1 = new ServerInfo { Name = "Нидерланды", Address = "185.12.44.10", Domain = "nl.example.com", Username = "user" };
                s1.Password = "secret";
                var s2 = new ServerInfo { Name = "Германия — Франкфурт", Address = "de.example.com:8443", Domain = "de.example.com", Username = "user", Protocol = "http3" };
                s2.Password = "secret";
                var s3 = new ServerInfo { Name = "Домашний сервер", Address = "[2001:db8::1]:443", Domain = "home.example.org", Username = "me" };
                s3.Password = "secret";
                app.Store.Data.Servers.AddRange(new[] { s1, s2, s3 });
                app.Store.Data.Profiles.Add(new RoutingProfile { Name = "Работа", DefaultMode = "bypass", VpnRules = new List<string> { "corp.example.com", "10.20.0.0/16" } });
                app.Store.Save();
                SetVpnState(app.Vpn, VpnState.Connected, s1.Id);
                w.SelectTab(0);
            }));
            steps.Add(Step("03_servers_connecting", delegate { SetVpnState(app.Vpn, VpnState.Connecting, app.Store.Data.Servers[1].Id); w.SelectTab(0); }));
            steps.Add(Step("04_server_add", delegate { SetVpnState(app.Vpn, VpnState.Disconnected, null); w.Push(new ServerDetailsPage(w, null)); }));
            steps.Add(Step("05_server_edit", delegate { w.SelectTab(0); w.Push(new ServerDetailsPage(w, app.Store.Data.Servers[1])); }));
            steps.Add(Step("06_server_edit_scrolled", delegate
            {
                var sv = Find<System.Windows.Controls.ScrollViewer>(w.TopPage);
                if (sv != null) sv.ScrollToEnd();
            }));
            steps.Add(Step("07_server_validation", delegate
            {
                w.SelectTab(0);
                var page = new ServerDetailsPage(w, null);
                w.Push(page);
                page.GetType().GetMethod("Submit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(page, null);
            }));
            steps.Add(Step("08_delete_dialog", delegate
            {
                w.SelectTab(0);
                var page = new ServerDetailsPage(w, app.Store.Data.Servers[0]);
                w.Push(page);
                page.GetType().GetMethod("AskDelete", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(page, null);
            }));
            steps.Add(Step("09_routing", delegate { w.CloseDialog(); w.SelectTab(1); }));
            steps.Add(Step("10_routing_details", delegate { w.Push(new RoutingDetailsPage(w, app.Store.Data.Profiles[1])); }));
            steps.Add(Step("11_routing_details_empty", delegate { w.SelectTab(1); w.Push(new RoutingDetailsPage(w, app.Store.Data.Profiles[0])); }));
            steps.Add(Step("12_settings", delegate { w.SelectTab(2); }));
            steps.Add(Step("13_log_empty", delegate { w.Push(new ConnectionLogPage(w)); }));
            steps.Add(Step("14_log", delegate
            {
                var add = typeof(VpnManager).GetMethod("AddLine", BindingFlags.NonPublic | BindingFlags.Instance);
                add.Invoke(app.Vpn, new object[] { "11.10.2026 08:59:17.639957 INFO  [25460] VPNCORE raise_state: [0] VPN_SS_CONNECTING", LogLevel.Info });
                add.Invoke(app.Vpn, new object[] { "11.10.2026 08:59:17.640140 INFO  [25460] SOCKS5_LISTENER socks5_listener_start: Listening on 127.0.0.1:18080", LogLevel.Info });
                add.Invoke(app.Vpn, new object[] { "11.10.2026 08:59:19.681653 WARN  [25460] VPNCORE pinger_handler: [0] Failed to ping location", LogLevel.Warn });
                add.Invoke(app.Vpn, new object[] { "11.10.2026 08:59:20.100000 ERROR [25460] VPNCORE connect: [0] Authorization failed", LogLevel.Error });
                add.Invoke(app.Vpn, new object[] { "11.10.2026 08:59:20.200000 DEBUG [25460] VPNCORE something: debug line", LogLevel.Debug });
            }));
            steps.Add(Step("15_logging", delegate { w.SelectTab(2); w.Push(new AppLoggingPage(w)); }));
            steps.Add(Step("16_launch", delegate { w.SelectTab(2); w.Push(new LaunchAndConnectionPage(w)); }));
            steps.Add(Step("17_excluded", delegate { w.SelectTab(2); w.Push(new ExcludedRoutesPage(w)); }));
            steps.Add(Step("18_about", delegate { w.SelectTab(2); w.Push(new AboutPage(w)); }));
            steps.Add(Step("19_snackbar", delegate { w.SelectTab(0); w.ShowSnack(L.ServerCreated, "Нидерланды"); }));
            steps.Add(Step("20_name_dialog", delegate
            {
                w.SelectTab(1);
                var page = (RoutingPage)w.TopPage;
                page.GetType().GetMethod("AddProfile", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(page, null);
            }));
            steps.Add(Step("21_small_window", delegate { w.CloseDialog(); w.Width = 720; w.Height = 520; w.SelectTab(0); w.Push(new ServerDetailsPage(w, null)); }));
            steps.Add(Step("22_appearance", delegate { w.Width = 1040; w.Height = 720; w.SelectTab(2); w.Push(new AppearancePage(w)); }));
            steps.Add(Step("23_connection_mode", delegate { w.SelectTab(2); w.Push(new ConnectionModePage(w)); }));
            steps.Add(Step("24_connection_mode_socks", delegate
            {
                var s = app.Store.Data.Settings;
                s.ListenerMode = "socks";
                s.SocksAllowLan = true;
                w.SelectTab(2);
                w.Push(new ConnectionModePage(w));
            }));
            steps.Add(Step("24b_connection_mode_adapter", delegate
            {
                var s = app.Store.Data.Settings;
                var adapter = Net.Adapters().FirstOrDefault(a => !a.IsVirtual) ?? Net.Adapters().FirstOrDefault();
                if (adapter != null) { s.SocksAdapterId = adapter.Id; s.SocksAdapterName = adapter.Name; }
                s.SocksUsername = "user";
                s.SocksPassword = "secret";
                w.SelectTab(2);
                w.Push(new ConnectionModePage(w));
                var sv = Find<System.Windows.Controls.ScrollViewer>(w.TopPage);
                if (sv != null) sv.ScrollToVerticalOffset(250);
            }));
            steps.Add(Step("25_servers_socks_banner", delegate { SetVpnState(app.Vpn, VpnState.Connected, app.Store.Data.Servers[0].Id); w.SelectTab(0); }));
            steps.Add(Step("26_admin_dialog", delegate { app.Store.Data.Settings.ListenerMode = "tun"; app.Store.Data.Settings.SocksAllowLan = false; SetVpnState(app.Vpn, VpnState.Disconnected, null); w.SelectTab(0); w.ShowDialog(L.AdminRequired, UI.BodyMedium(L.AdminRequiredDescription), DialogAction.Cancel(), new DialogAction(L.Restart, delegate { return true; })); }));
            steps.Add(Step("30_dark_servers", delegate
            {
                w.CloseDialog();
                app.Store.Data.Settings.Theme = "dark";
                app.ApplyAppearance(null);
                SetVpnState(app.Vpn, VpnState.Connected, app.Store.Data.Servers[0].Id);
                w.SelectTab(0);
            }));
            steps.Add(Step("31_dark_server_edit", delegate { w.Push(new ServerDetailsPage(w, app.Store.Data.Servers[1])); }));
            steps.Add(Step("32_dark_validation", delegate
            {
                w.SelectTab(0);
                var page = new ServerDetailsPage(w, null);
                w.Push(page);
                page.GetType().GetMethod("Submit", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(page, null);
            }));
            steps.Add(Step("33_dark_routing_details", delegate { w.SelectTab(1); w.Push(new RoutingDetailsPage(w, app.Store.Data.Profiles[1])); }));
            steps.Add(Step("34_dark_settings", delegate { w.SelectTab(2); }));
            steps.Add(Step("35_dark_launch", delegate { w.Push(new LaunchAndConnectionPage(w)); }));
            steps.Add(Step("36_dark_dialog", delegate
            {
                w.SelectTab(0);
                w.ShowDialog(L.DeleteServerTitle, UI.BodyMedium(string.Format(L.DeleteServerDescription, "Test")), DialogAction.Cancel(), new DialogAction(L.Delete, delegate { return true; }) { Danger = true });
                w.ShowSnack(L.ServerCreated, "Test");
            }));
            steps.Add(Step("37_dark_log", delegate { w.CloseDialog(); w.SelectTab(2); w.Push(new ConnectionLogPage(w)); }));
            steps.Add(Step("40_en_servers", delegate
            {
                app.Store.Data.Settings.Language = "en";
                app.Store.Data.Settings.ListenerMode = "socks";
                app.ApplyAppearance(null);
                w.SelectTab(0);
            }));
            steps.Add(Step("41_en_server_add", delegate { w.Push(new ServerDetailsPage(w, null)); }));
            steps.Add(Step("42_en_routing", delegate { w.SelectTab(1); }));
            steps.Add(Step("43_en_settings", delegate { w.SelectTab(2); }));
            steps.Add(Step("44_en_connection_mode", delegate { w.Push(new ConnectionModePage(w)); }));
            steps.Add(Step("45_en_light_appearance", delegate
            {
                app.Store.Data.Settings.Theme = "light";
                app.ApplyAppearance(delegate { w.SelectTab(2); w.Push(new AppearancePage(w)); });
            }));

            int index = 0;
            Action next = null;
            next = delegate
            {
                if (index >= steps.Count)
                {
                    w.AllowClose = true;
                    w.Close();
                    app.Shutdown();
                    return;
                }
                var step = steps[index++];
                try { step.Value(); }
                catch (Exception ex) { File.AppendAllText(Path.Combine(outDir, "errors.txt"), step.Key + ": " + ex + "\n"); }
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    try { Capture((FrameworkElement)w.Content, Path.Combine(outDir, step.Key + ".png")); }
                    catch (Exception ex) { File.AppendAllText(Path.Combine(outDir, "errors.txt"), step.Key + " capture: " + ex + "\n"); }
                    next();
                };
                timer.Start();
            };
            app.Dispatcher.BeginInvoke(next, DispatcherPriority.ApplicationIdle);
            app.Run();
        }

        static KeyValuePair<string, Action> Step(string name, Action a)
        {
            return new KeyValuePair<string, Action>(name, a);
        }

        static void SetVpnState(VpnManager vpn, VpnState state, string serverId)
        {
            typeof(VpnManager).GetProperty("ActiveServerId").SetValue(vpn, serverId);
            typeof(VpnManager).GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(vpn, new object[] { state });
        }

        static T Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T) return (T)c;
                var r = Find<T>(c);
                if (r != null) return r;
            }
            return null;
        }

        static void Capture(FrameworkElement element, string file)
        {
            var dpi = VisualTreeHelper.GetDpi(element);
            int width = (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX);
            int height = (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY);
            var rtb = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (var ctx = visual.RenderOpen())
            {
                ctx.DrawRectangle(C.BackgroundSystem, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                var brush = new VisualBrush(element)
                {
                    Stretch = Stretch.None,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(0, 0, element.ActualWidth, element.ActualHeight),
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top
                };
                ctx.DrawRectangle(brush, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            }
            rtb.Render(visual);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(file)) enc.Save(fs);
        }
    }
}
#endif
