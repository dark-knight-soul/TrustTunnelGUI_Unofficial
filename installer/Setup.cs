using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using TrustTunnelGui;

[assembly: AssemblyTitle("TrustTunnel GUI Setup")]
[assembly: AssemblyProduct("TrustTunnel GUI")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace TrustTunnelSetup
{
    // Setup strings (Russian or English, by the Windows UI language).
    static class S
    {
        static readonly bool Ru = new[] { "ru", "be", "uk", "kk" }.Contains(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

        static string T(string ru, string en) { return Ru ? ru : en; }

        public static readonly string Title = T("Установка TrustTunnel GUI", "TrustTunnel GUI Setup");
        public static readonly string Description = T(
            "Графическая оболочка для консольного клиента TrustTunnel. Вместе с приложением будут установлены консольный клиент и драйвер Wintun.",
            "A graphical interface for the TrustTunnel console client. The console client and the Wintun driver are installed together with the app.");
        public static readonly string Unofficial = T("Неофициальная сборка", "Unofficial build");
        public static readonly string Version = T("Версия {0}", "Version {0}");
        public static readonly string UpdateNote = T("Установленная версия {0} будет обновлена, настройки сохранятся", "The installed version {0} will be updated; settings are kept");
        public static readonly string InstallFolder = T("Папка установки", "Install folder");
        public static readonly string Browse = T("Обзор…", "Browse…");
        public static readonly string DesktopShortcut = T("Создать ярлык на рабочем столе", "Create a desktop shortcut");
        public static readonly string LaunchAfter = T("Запустить TrustTunnel GUI после установки", "Launch TrustTunnel GUI after setup");
        public static readonly string Install = T("Установить", "Install");
        public static readonly string Update = T("Обновить", "Update");
        public static readonly string Cancel = T("Отмена", "Cancel");
        public static readonly string Installing = T("Установка…", "Installing…");
        public static readonly string CopyingFiles = T("Копирование файлов…", "Copying files…");
        public static readonly string CreatingShortcuts = T("Создание ярлыков…", "Creating shortcuts…");
        public static readonly string Registering = T("Регистрация в системе…", "Registering the app…");
        public static readonly string Done = T("Готово", "Done");
        public static readonly string Installed = T("TrustTunnel GUI {0} установлен", "TrustTunnel GUI {0} has been installed");
        public static readonly string InstalledHint = T("Приложение доступно в меню «Пуск». При подключении в режиме VPN (TUN) оно попросит права администратора.",
            "The app is available in the Start menu. When connecting in VPN (TUN) mode it asks for administrator rights.");
        public static readonly string Finish = T("Готово", "Finish");
        public static readonly string Close = T("Закрыть", "Close");
        public static readonly string Failed = T("Не удалось завершить операцию", "The operation failed");
        public static readonly string CloseApp = T("Закройте TrustTunnel GUI (значок в области уведомлений → «Выйти») и повторите попытку",
            "Close TrustTunnel GUI (notification area icon → Quit) and try again");
        public static readonly string FolderEmpty = T("Укажите папку установки", "Choose an install folder");
        public static readonly string UninstallTitle = T("Удаление TrustTunnel GUI", "Uninstall TrustTunnel GUI");
        public static readonly string UninstallDescription = T("Приложение, консольный клиент и ярлыки будут удалены из папки {0}.",
            "The app, the console client and the shortcuts will be removed from {0}.");
        public static readonly string RemoveData = T("Удалить также настройки и список серверов", "Also remove settings and the server list");
        public static readonly string Uninstall = T("Удалить", "Uninstall");
        public static readonly string Uninstalling = T("Удаление…", "Uninstalling…");
        public static readonly string Uninstalled = T("TrustTunnel GUI удалён", "TrustTunnel GUI has been uninstalled");
    }

    static class Product
    {
        public const string Name = "TrustTunnel GUI";
        public const string Exe = "TrustTunnelGUI.exe";
        public const string Uninstaller = "uninstall.exe";
        public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\TrustTunnelGUI";
        public const string Publisher = "TrustTunnel GUI (unofficial)";
        public const string Url = "https://github.com/dark-knight-soul/TrustTunnelGUI_Unofficial";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        public static string DefaultDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Name); }
        }

        public static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), Name + ".lnk"); }
        }

        public static string DesktopShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), Name + ".lnk"); }
        }

        public static string UserDataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TrustTunnelGUI"); }
        }

        static RegistryKey Hklm()
        {
            return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }

        // Install location and version of an existing installation, if any.
        public static bool FindInstalled(out string dir, out string version)
        {
            dir = null;
            version = null;
            try
            {
                using (var root = Hklm())
                using (var key = root.OpenSubKey(UninstallKey))
                {
                    if (key == null) return false;
                    dir = key.GetValue("InstallLocation") as string;
                    version = key.GetValue("DisplayVersion") as string;
                    return !string.IsNullOrEmpty(dir);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void Register(string dir, long sizeBytes)
        {
            using (var root = Hklm())
            using (var key = root.CreateSubKey(UninstallKey))
            {
                var exe = Path.Combine(dir, Exe);
                var uninstaller = Path.Combine(dir, Uninstaller);
                key.SetValue("DisplayName", Name);
                key.SetValue("DisplayVersion", Version);
                key.SetValue("Publisher", Publisher);
                key.SetValue("URLInfoAbout", Url);
                key.SetValue("DisplayIcon", exe + ",0");
                key.SetValue("InstallLocation", dir);
                key.SetValue("UninstallString", "\"" + uninstaller + "\" --uninstall");
                key.SetValue("QuietUninstallString", "\"" + uninstaller + "\" --uninstall --silent");
                key.SetValue("EstimatedSize", (int)(sizeBytes / 1024), RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        public static void Unregister()
        {
            using (var root = Hklm()) root.DeleteSubKeyTree(UninstallKey, false);
        }

        public static bool AppIsRunning()
        {
            return Process.GetProcessesByName("TrustTunnelGUI").Length > 0 || Process.GetProcessesByName("trusttunnel_client").Length > 0;
        }

        public static void CreateShortcut(string lnk, string target)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            var shell = Activator.CreateInstance(shellType);
            var shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
            var t = shortcut.GetType();
            t.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
            t.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(target) });
            t.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { target + ",0" });
            t.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { Name });
            t.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        // Removes launch-at-login entries the app may have created (Run key and scheduled task).
        public static void RemoveAutostart()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    if (key != null && key.GetValue(Name) != null) key.DeleteValue(Name);
            }
            catch { }
            try
            {
                var psi = new ProcessStartInfo("schtasks.exe", "/Delete /TN \"" + Name + "\" /F") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var p = Process.Start(psi)) p.WaitForExit(10000);
            }
            catch { }
        }

        public static Stream Payload()
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
        }
    }

    // Installation / removal steps, reporting progress (0..1) and a status line.
    static class Engine
    {
        public static void Install(string dir, bool desktopShortcut, Action<double, string> progress)
        {
            progress(0, S.CopyingFiles);
            Directory.CreateDirectory(dir);
            long total = 0;
            using (var payload = Product.Payload())
            {
                if (payload == null) throw new InvalidOperationException("payload.zip is missing");
                using (var zip = new ZipArchive(payload, ZipArchiveMode.Read))
                {
                    var entries = zip.Entries.Where(e => e.Name.Length > 0).ToList();
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        var target = Path.GetFullPath(Path.Combine(dir, entry.FullName.Replace('/', '\\')));
                        if (!target.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase)) continue;
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        using (var src = entry.Open())
                        using (var dst = new FileStream(target, FileMode.Create, FileAccess.Write))
                            src.CopyTo(dst);
                        total += entry.Length;
                        progress(0.8 * (i + 1) / entries.Count, S.CopyingFiles);
                    }
                }
            }

            var self = Assembly.GetExecutingAssembly().Location;
            var uninstaller = Path.Combine(dir, Product.Uninstaller);
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(uninstaller), StringComparison.OrdinalIgnoreCase))
                File.Copy(self, uninstaller, true);
            total += new FileInfo(uninstaller).Length;

            progress(0.85, S.CreatingShortcuts);
            var exe = Path.Combine(dir, Product.Exe);
            Product.CreateShortcut(Product.StartMenuShortcut, exe);
            if (desktopShortcut) Product.CreateShortcut(Product.DesktopShortcut, exe);
            else if (File.Exists(Product.DesktopShortcut)) File.Delete(Product.DesktopShortcut);

            progress(0.95, S.Registering);
            Product.Register(dir, total);
            progress(1, S.Done);
        }

        public static void Uninstall(string dir, bool removeData, Action<double, string> progress)
        {
            progress(0.1, S.Uninstalling);
            Product.RemoveAutostart();
            foreach (var lnk in new[] { Product.StartMenuShortcut, Product.DesktopShortcut })
                if (File.Exists(lnk)) File.Delete(lnk);

            progress(0.3, S.Uninstalling);
            var self = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
            if (Directory.Exists(dir))
            {
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); } catch { }
                }
                foreach (var sub in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                    try { Directory.Delete(sub, false); } catch { }
            }

            progress(0.7, S.Uninstalling);
            if (removeData && Directory.Exists(Product.UserDataDir))
                try { Directory.Delete(Product.UserDataDir, true); } catch { }
            Product.Unregister();
            progress(1, S.Done);
        }

        // The running uninstaller cannot delete itself; a short-lived cmd finishes the job.
        public static void ScheduleSelfDelete(string dir)
        {
            var self = Assembly.GetExecutingAssembly().Location;
            if (!self.StartsWith(dir, StringComparison.OrdinalIgnoreCase)) return;
            var cmd = "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + self + "\" & rmdir \"" + dir + "\"";
            Process.Start(new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, UseShellExecute = false, WorkingDirectory = Path.GetTempPath() });
        }

        // Started through explorer.exe so the app runs with the user's normal rights, not the installer's.
        public static void Launch(string dir)
        {
            try { Process.Start("explorer.exe", "\"" + Path.Combine(dir, Product.Exe) + "\""); } catch { }
        }
    }

    class SetupApp : Application
    {
        [STAThread]
        static int Main(string[] args)
        {
            bool uninstall = args.Contains("--uninstall");
            bool silent = args.Contains("--silent");
            string installedDir, installedVersion;
            bool installed = Product.FindInstalled(out installedDir, out installedVersion);

            if (silent) return RunSilent(uninstall, installed ? installedDir : Product.DefaultDir);

            var app = new SetupApp { ShutdownMode = ShutdownMode.OnMainWindowClose };
            Theme.Install(app, "system");
#if DEVTOOLS
            int themeIdx = Array.IndexOf(args, "--theme");
            if (themeIdx >= 0) Theme.Install(app, args[themeIdx + 1]);
            int idx = Array.IndexOf(args, "--render");
            if (idx >= 0) { DevRender.Run(app, args[idx + 1]); return 0; }
#endif
            var dir = uninstall
                ? (installed ? installedDir : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                : (installed ? installedDir : Product.DefaultDir);
            var window = new SetupWindow(uninstall, dir, installed && !uninstall ? installedVersion : null);
            app.Run(window);
            return 0;
        }

        static int RunSilent(bool uninstall, string dir)
        {
            try
            {
                if (Product.AppIsRunning()) return 2;
                Action<double, string> none = delegate { };
                if (uninstall)
                {
                    Engine.Uninstall(dir, false, none);
                    Engine.ScheduleSelfDelete(dir);
                }
                else
                {
                    Engine.Install(dir, false, none);
                }
                return 0;
            }
            catch
            {
                return 1;
            }
        }
    }

    class SetupWindow : Window
    {
        readonly bool _uninstall;
        readonly string _updateFrom;
        readonly Border _host = new Border();
        string _dir;
        bool _busy;

        public SetupWindow(bool uninstall, string dir, string updateFrom)
        {
            _uninstall = uninstall;
            _dir = dir;
            _updateFrom = updateFrom;
            Title = uninstall ? S.UninstallTitle : S.Title;
            Width = 680;
            Height = 520;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = C.BackgroundSystem;
            FontFamily = Theme.Font;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Icon = Logo();
            SourceInitialized += delegate { Theme.ApplyTitleBar(this); };

            var card = new Border { Background = C.Background, CornerRadius = new CornerRadius(16), Margin = new Thickness(16), Child = _host };
            UI.ClipRounded(card, 16);
            var root = new Grid();
            root.Children.Add(card);
            Content = root;

            if (uninstall) ShowUninstallPage(); else ShowWelcomePage();
        }

        public static ImageSource Logo()
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app_icon.ico");
            if (stream == null) return null;
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
        }

        // Page frame: header with logo, body, footer with buttons.
        void SetPage(UIElement body, params Button[] buttons)
        {
            var dock = new DockPanel();

            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(32, 28, 32, 0) };
            DockPanel.SetDock(header, Dock.Top);
            var logo = new Image { Source = Logo(), Width = 56, Height = 56, Margin = new Thickness(0, 0, 16, 0) };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            header.Children.Add(logo);
            var titles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titles.Children.Add(UI.Text(Product.Name, 24));
            titles.Children.Add(UI.BodyMedium(string.Format(S.Version, Product.Version) + " · " + S.Unofficial, C.NeutralDark));
            header.Children.Add(titles);
            dock.Children.Add(header);

            if (buttons.Length > 0)
            {
                var footer = new DockPanel();
                DockPanel.SetDock(footer, Dock.Bottom);
                var div = UI.Divider();
                DockPanel.SetDock(div, Dock.Top);
                footer.Children.Add(div);
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(16) };
                foreach (var b in buttons) { b.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(b); }
                footer.Children.Add(row);
                dock.Children.Add(footer);
            }

            var bodyHost = new Border { Margin = new Thickness(32, 24, 32, 16), Child = body };
            dock.Children.Add(bodyHost);
            _host.Child = dock;
        }

        TextBlock ErrorText()
        {
            var t = UI.BodyMedium("", C.Error);
            t.Margin = new Thickness(0, 12, 0, 0);
            t.Visibility = Visibility.Collapsed;
            return t;
        }

        static void ShowError(TextBlock target, string message)
        {
            target.Text = message;
            target.Visibility = Visibility.Visible;
        }

        void ShowWelcomePage()
        {
            var body = new StackPanel();
            body.Children.Add(UI.BodyMedium(S.Description, C.NeutralDark));
            if (_updateFrom != null)
            {
                var note = UI.BodyMedium(string.Format(S.UpdateNote, _updateFrom), C.AccentText);
                note.Margin = new Thickness(0, 8, 0, 0);
                body.Children.Add(note);
            }

            var label = UI.Text(S.InstallFolder, 13, FontWeights.SemiBold, C.NeutralDark);
            label.Margin = new Thickness(0, 24, 0, 6);
            body.Children.Add(label);
            var row = new DockPanel();
            var browse = UI.Outlined(S.Browse, null, delegate { });
            browse.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(browse, Dock.Right);
            row.Children.Add(browse);
            var path = new TextBox { Style = UI.S("TextBoxField"), Text = _dir, IsEnabled = _updateFrom == null };
            row.Children.Add(path);
            browse.IsEnabled = _updateFrom == null;
            browse.Click += delegate
            {
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog { SelectedPath = path.Text, ShowNewFolderButton = true })
                {
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        path.Text = dlg.SelectedPath.EndsWith(Product.Name, StringComparison.OrdinalIgnoreCase) ? dlg.SelectedPath : Path.Combine(dlg.SelectedPath, Product.Name);
                }
            };
            body.Children.Add(row);

            var desktop = new CheckBox { Style = UI.S("Check"), Content = S.DesktopShortcut, IsChecked = _updateFrom == null || File.Exists(Product.DesktopShortcut), Margin = new Thickness(-10, 16, 0, 0) };
            var launch = new CheckBox { Style = UI.S("Check"), Content = S.LaunchAfter, IsChecked = true, Margin = new Thickness(-10, 0, 0, 0) };
            body.Children.Add(desktop);
            body.Children.Add(launch);
            var error = ErrorText();
            body.Children.Add(error);

            var cancel = UI.TextButton(S.Cancel, Close);
            var install = UI.Filled(_updateFrom != null ? S.Update : S.Install, delegate
            {
                var dir = path.Text.Trim();
                if (dir.Length == 0) { ShowError(error, S.FolderEmpty); return; }
                if (Product.AppIsRunning()) { ShowError(error, S.CloseApp); return; }
                _dir = dir;
                RunWork(S.Installing, delegate(Action<double, string> p) { Engine.Install(dir, desktop.IsChecked == true, p); },
                    delegate { ShowDonePage(string.Format(S.Installed, Product.Version), S.InstalledHint, launch.IsChecked == true); });
            });
            install.IsDefault = true;
            SetPage(body, cancel, install);
        }

        void ShowUninstallPage()
        {
            var body = new StackPanel();
            var title = UI.Text(S.UninstallTitle, 20);
            title.Margin = new Thickness(0, 0, 0, 8);
            body.Children.Add(title);
            body.Children.Add(UI.BodyMedium(string.Format(S.UninstallDescription, _dir), C.NeutralDark));
            var removeData = new CheckBox { Style = UI.S("Check"), Content = S.RemoveData, Margin = new Thickness(-10, 16, 0, 0) };
            body.Children.Add(removeData);
            var error = ErrorText();
            body.Children.Add(error);

            var cancel = UI.TextButton(S.Cancel, Close);
            var remove = UI.Filled(S.Uninstall, delegate
            {
                if (Product.AppIsRunning()) { ShowError(error, S.CloseApp); return; }
                bool data = removeData.IsChecked == true;
                RunWork(S.Uninstalling, delegate(Action<double, string> p) { Engine.Uninstall(_dir, data, p); },
                    delegate { ShowDonePage(S.Uninstalled, null, false); });
            }, true);
            SetPage(body, cancel, remove);
        }

        // Progress page; work runs on a background thread.
        void RunWork(string title, Action<Action<double, string>> work, Action done)
        {
            _busy = true;
            var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var t = UI.Text(title, 20);
            t.Margin = new Thickness(0, 0, 0, 16);
            body.Children.Add(t);
            var track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = C.BackgroundAdditional };
            var fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = C.Accent, HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            var bar = new Grid();
            bar.Children.Add(track);
            bar.Children.Add(fill);
            body.Children.Add(bar);
            var status = UI.BodyMedium("", C.NeutralDark);
            status.Margin = new Thickness(0, 12, 0, 0);
            body.Children.Add(status);
            SetPage(body);

            Action<double, string> report = delegate(double value, string text)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    fill.BeginAnimation(WidthProperty, new DoubleAnimation(Math.Max(0, bar.ActualWidth * value), TimeSpan.FromMilliseconds(150)));
                    status.Text = text;
                }));
            };
            var thread = new Thread(delegate()
            {
                Exception failure = null;
                try { work(report); }
                catch (Exception ex) { failure = ex; }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    if (failure == null) done();
                    else ShowFailedPage(failure.Message);
                }));
            }) { IsBackground = true };
            thread.Start();
        }

        void ShowDonePage(string title, string hint, bool launch)
        {
            var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var circle = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(32), Background = C.Accent, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
            var check = UI.Icon(Icons.Check, 36, C.White);
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
            circle.Child = check;
            body.Children.Add(circle);
            body.Children.Add(UI.Text(title, 20));
            if (hint != null)
            {
                var h = UI.BodyMedium(hint, C.NeutralDark);
                h.Margin = new Thickness(0, 8, 0, 0);
                body.Children.Add(h);
            }
            var finish = UI.Filled(_uninstall ? S.Close : S.Finish, delegate
            {
                if (_uninstall) Engine.ScheduleSelfDelete(_dir);
                else if (launch) Engine.Launch(_dir);
                Close();
            });
            finish.IsDefault = true;
            SetPage(body, finish);
        }

        void ShowFailedPage(string message)
        {
            var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            body.Children.Add(UI.Text(S.Failed, 20));
            var m = UI.BodyMedium(message, C.Error);
            m.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(m);
            SetPage(body, UI.Filled(S.Close, Close));
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_busy) e.Cancel = true;
            base.OnClosing(e);
        }

#if DEVTOOLS
        public void DevShow(string page)
        {
            switch (page)
            {
                case "progress":
                    _busy = false;
                    RunWork(S.Installing, delegate(Action<double, string> p) { p(0.55, S.CopyingFiles); Thread.Sleep(5000); }, delegate { });
                    break;
                case "done": ShowDonePage(string.Format(S.Installed, Product.Version), S.InstalledHint, true); break;
            }
        }
#endif
    }

#if DEVTOOLS
    static class DevRender
    {
        public static void Run(Application app, string outDir)
        {
            Directory.CreateDirectory(outDir);
            var shots = new List<KeyValuePair<string, Func<SetupWindow>>>
            {
                new KeyValuePair<string, Func<SetupWindow>>("setup_welcome", delegate { return new SetupWindow(false, Product.DefaultDir, null); }),
                new KeyValuePair<string, Func<SetupWindow>>("setup_progress", delegate { var w = new SetupWindow(false, Product.DefaultDir, null); w.Loaded += delegate { w.DevShow("progress"); }; return w; }),
                new KeyValuePair<string, Func<SetupWindow>>("setup_done", delegate { var w = new SetupWindow(false, Product.DefaultDir, null); w.Loaded += delegate { w.DevShow("done"); }; return w; }),
                new KeyValuePair<string, Func<SetupWindow>>("setup_uninstall", delegate { return new SetupWindow(true, Product.DefaultDir, null); })
            };
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int i = 0;
            Action next = null;
            next = delegate
            {
                if (i >= shots.Count) { app.Shutdown(); return; }
                var shot = shots[i++];
                var w = shot.Value();
                w.Left = -20000;
                w.ShowActivated = false;
                w.WindowStartupLocation = WindowStartupLocation.Manual;
                w.Show();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
                timer.Tick += delegate
                {
                    timer.Stop();
                    var el = (FrameworkElement)w.Content;
                    var dpi = VisualTreeHelper.GetDpi(el);
                    var rtb = new RenderTargetBitmap((int)(el.ActualWidth * dpi.DpiScaleX), (int)(el.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                    var visual = new DrawingVisual();
                    using (var ctx = visual.RenderOpen())
                    {
                        ctx.DrawRectangle(C.BackgroundSystem, null, new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                        ctx.DrawRectangle(new VisualBrush(el) { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, el.ActualWidth, el.ActualHeight), AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, null, new Rect(0, 0, el.ActualWidth, el.ActualHeight));
                    }
                    rtb.Render(visual);
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    using (var fs = File.Create(Path.Combine(outDir, shot.Key + ".png"))) enc.Save(fs);
                    w.Hide();
                    next();
                };
                timer.Start();
            };
            app.Dispatcher.BeginInvoke(next);
            app.Run();
        }
    }
#endif
}
