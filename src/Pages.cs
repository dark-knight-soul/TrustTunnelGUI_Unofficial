using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TrustTunnelGui
{
    // ===================== Servers =====================

    class ServersPage : Page
    {
        readonly ScrollViewer _scroll = UI.Scroll(null);

        readonly Button _pingButton;

        public ServersPage(MainWindow main) : base(main)
        {
            _pingButton = UI.IconButton(Icons.Speed, L.PingServers, delegate { App.I.Ping.PingAll(App.I.Store.Data.Servers); });
            SetAppBar(L.Servers, null, _pingButton);
        }

        // Spinning, disabled while measuring; the tooltip tells when the last check finished.
        void UpdatePingButton()
        {
            var ping = App.I.Ping;
            bool running = ping.AnyRunning;
            _pingButton.IsEnabled = !running;
            _pingButton.Visibility = App.I.Store.Data.Servers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            var icon = UI.Icon(running ? Icons.Update : Icons.Speed, 24, C.NeutralBlack);
            if (running)
            {
                var rotate = new RotateTransform();
                icon.RenderTransform = rotate;
                icon.RenderTransformOrigin = new Point(0.5, 0.5);
                rotate.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
            }
            _pingButton.Content = icon;
            _pingButton.ToolTip = running ? L.PingRunning
                : ping.LastChecked.HasValue ? string.Format(L.PingLastChecked, ping.LastChecked.Value.ToString("HH:mm:ss")) : L.PingServers;
        }

        public override void OnShown()
        {
            App.I.Vpn.StateChanged -= Render;
            App.I.Vpn.StateChanged += Render;
            App.I.Store.Changed -= Render;
            App.I.Store.Changed += Render;
            App.I.Ping.Updated -= Render;
            App.I.Ping.Updated += Render;
            App.I.ReconnectChanged -= Render;
            App.I.ReconnectChanged += Render;
            Render();
            App.I.Ping.PingAllIfStale(App.I.Store.Data.Servers, TimeSpan.FromMinutes(1));
        }

        public override void OnRemoved()
        {
            App.I.Vpn.StateChanged -= Render;
            App.I.Store.Changed -= Render;
            App.I.Ping.Updated -= Render;
            App.I.ReconnectChanged -= Render;
        }

        // " · 42 ms" in green/orange, " · unreachable" in red, "…" while measuring.
        static void PingBadge(ServerInfo s, out string text, out Brush brush)
        {
            var ping = App.I.Ping;
            var ms = ping.Result(s.Id);
            brush = C.NeutralLight;
            if (ping.IsRunning(s.Id)) { text = " · …"; return; }
            if (!ms.HasValue) { text = null; return; }
            if (ms.Value == PingService.Unreachable) { text = " · " + L.Unreachable; brush = C.Error; return; }
            text = " · " + string.Format(L.PingMs, ms.Value);
            brush = ms.Value < 150 ? C.Success : C.Attention;
        }

        void Render()
        {
            UpdatePingButton();
            var servers = App.I.Store.Data.Servers;
            if (servers.Count == 0)
            {
                SetFab(null, null, null);
                SetBody(Placeholder(Icons.Dns, L.ServersEmptyTitle, L.ServersEmptyDescription, L.Create, OpenAdd));
                return;
            }
            var list = new StackPanel { Margin = new Thickness(0, 0, 0, 88) };
            if (App.I.Store.Data.Settings.IsSocks) list.Children.Add(ProxyBanner());
            for (int i = 0; i < servers.Count; i++)
            {
                var s = servers[i];
                string badge;
                Brush badgeBrush;
                PingBadge(s, out badge, out badgeBrush);
                list.Children.Add(Tiles.Separated(s.Name, s.Address, delegate { Main.Push(new ServerDetailsPage(Main, s)); }, PowerButton(s), true, badge, badgeBrush));
                if (i != servers.Count - 1) list.Children.Add(UI.Divider());
            }
            _scroll.Content = list;
            SetBody(_scroll);
            SetFab(L.AddServer, Icons.Add, OpenAdd);
        }

        void OpenAdd()
        {
            Main.Push(new ServerDetailsPage(Main, null));
        }

        // Shown in SOCKS mode: where apps should point their proxy settings.
        FrameworkElement ProxyBanner()
        {
            var s = App.I.Store.Data.Settings;
            var local = "127.0.0.1:" + s.SocksPort;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = UI.Icon(Icons.Link, 24, C.AccentText);
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 16, 0);
            grid.Children.Add(icon);
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            texts.Children.Add(UI.TitleSmall(string.Format(L.ProxyBanner, local)));
            if (s.SocksAllowLan)
            {
                var ips = Net.ProxyLanAddresses(s);
                if (s.SocksAdapterMissing) texts.Children.Add(UI.BodyMedium(L.AdapterUnavailable, C.Error));
                else if (ips.Count > 0) texts.Children.Add(UI.BodyMedium(string.Format(L.ProxyBannerLan, string.Join(", ", ips)), C.NeutralDark));
            }
            Grid.SetColumn(texts, 1);
            grid.Children.Add(texts);
            var copy = UI.IconButton(Icons.Copy, L.CopyAddress, delegate
            {
                try { Clipboard.SetText(local); } catch { }
                Main.ShowSnack(L.AddressCopied);
            });
            Grid.SetColumn(copy, 2);
            grid.Children.Add(copy);
            var row = new Button { Style = UI.S("ListRow"), Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(16, 8, 8, 8) };
            row.Click += delegate { Main.Push(new ConnectionModePage(Main)); };
            var card = new Border
            {
                Background = C.BackgroundAdditional,
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(16, 0, 16, 8),
                Child = row
            };
            UI.ClipRounded(card, 12);
            return card;
        }

        // ServersCardConnectionButton: power icon, accent when connected, spinning while pending.
        static FrameworkElement PowerButton(ServerInfo server)
        {
            var vpn = App.I.Vpn;
            var state = vpn.ActiveServerId == server.Id ? vpn.State : VpnState.Disconnected;
            bool pending = state == VpnState.Connecting || state == VpnState.Disconnecting ||
                           (App.I.IsReconnecting && vpn.ActiveServerId == server.Id);
            bool selected = pending || state == VpnState.Connected;

            var icon = UI.Icon(pending ? Icons.Update : Icons.Power, 24, C.White);
            if (pending)
            {
                var rotate = new RotateTransform();
                icon.RenderTransform = rotate;
                icon.RenderTransformOrigin = new Point(0.5, 0.5);
                rotate.BeginAnimation(RotateTransform.AngleProperty,
                    new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1)) { RepeatBehavior = RepeatBehavior.Forever });
            }
            var b = new Button
            {
                Style = UI.S("PowerButton"),
                Width = 40,
                Content = icon,
                Background = selected ? C.Accent : C.PowerOff,
                ToolTip = pending && state == VpnState.Disconnected ? L.Reconnecting
                    : state == VpnState.Disconnected ? L.Connect : (state == VpnState.Connected ? L.Disconnect : (state == VpnState.Connecting ? L.Connecting : L.Disconnecting))
            };
            b.Click += delegate { App.I.ToggleServer(server); };
            return b;
        }
    }

    class ServerDetailsPage : Page
    {
        readonly ServerInfo _existing;
        readonly FormField _name = new FormField(L.ServerName, L.ServerName);
        readonly FormField _address = new FormField(L.Address, L.AddressHint, L.AddressHelper);
        readonly FormField _domain = new FormField(L.DomainLabel, L.DomainHint);
        readonly FormField _sni = new FormField(L.CustomSni, L.CustomSniHint);
        readonly FormField _username = new FormField(L.Username, L.UsernameHint);
        readonly FormField _password = new FormField(L.Password, L.PasswordHint, null, 1, true);
        readonly RadioButton _http2 = new RadioButton();
        readonly RadioButton _quic = new RadioButton();
        readonly DropdownField _profile = new DropdownField(L.RoutingProfile);
        readonly FormField _dns = new FormField(L.DnsLabel, L.DnsHint, L.DnsHelper, 4);
        readonly FormField _clientRandom = new FormField(L.ClientRandom, L.ClientRandomHint, L.ClientRandomHelper);
        readonly CheckBox _ipv6 = new CheckBox();
        readonly CheckBox _antiDpi = new CheckBox();
        readonly CheckBox _skipVerify = new CheckBox();
        readonly TextBlock _skipWarning = UI.BodySmall("", C.Error);
        readonly TextBlock _certName = UI.BodyMedium("", C.NeutralDark);
        Button _certRemove;
        string _certificate = "";
        string _certificateName = "";
        string _initial;

        public ServerDetailsPage(MainWindow main, ServerInfo existing) : base(main)
        {
            _existing = existing;
            if (existing == null)
            {
                SetAppBar(L.AddServer, Icons.Close,
                    UI.IconButton(Icons.Link, L.ImportFromLink, ImportFromLink),
                    UI.IconButton(Icons.FileOpen, L.ImportFromFile, ImportFromFile));
            }
            else
            {
                SetAppBar(L.EditServer, Icons.Close, UI.IconButton(Icons.Delete, L.DeleteServer, AskDelete, C.Error));
            }

            var form = new StackPanel { MaxWidth = 640, Margin = new Thickness(24, 8, 24, 24) };
            form.Children.Add(_name);
            form.Children.Add(_address);
            form.Children.Add(_domain);
            form.Children.Add(_sni);
            form.Children.Add(_username);
            form.Children.Add(_password);

            var protoLabel = UI.Text(L.Protocol, 13, FontWeights.SemiBold, C.NeutralDark);
            form.Children.Add(protoLabel);
            var protoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-10, 0, 0, 14) };
            _http2.Style = UI.S("Radio"); _http2.Content = L.Http2; _http2.GroupName = "proto"; _http2.Margin = new Thickness(0, 0, 24, 0);
            _quic.Style = UI.S("Radio"); _quic.Content = L.Quic; _quic.GroupName = "proto";
            protoRow.Children.Add(_http2);
            protoRow.Children.Add(_quic);
            form.Children.Add(protoRow);

            form.Children.Add(_profile);
            form.Children.Add(_dns);
            form.Children.Add(_clientRandom);

            var certLabel = UI.Text(L.PemLabel, 13, FontWeights.SemiBold, C.NeutralDark);
            certLabel.Margin = new Thickness(0, 0, 0, 6);
            form.Children.Add(certLabel);
            var certRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            certRow.Children.Add(UI.Outlined(L.Import, Icons.FileOpen, ImportCertificate));
            _certName.VerticalAlignment = VerticalAlignment.Center;
            _certName.Margin = new Thickness(16, 0, 4, 0);
            certRow.Children.Add(_certName);
            _certRemove = UI.IconButton(Icons.Close, L.Remove, delegate { SetCertificate("", ""); }, C.NeutralDark);
            certRow.Children.Add(_certRemove);
            form.Children.Add(certRow);

            _ipv6.Style = UI.S("Check");
            _ipv6.Content = L.Ipv6Label;
            _ipv6.Margin = new Thickness(-10, 0, 0, 0);
            form.Children.Add(_ipv6);

            var advanced = UI.Text(L.Advanced, 14, FontWeights.SemiBold, C.AccentText);
            advanced.Margin = new Thickness(0, 20, 0, 4);
            form.Children.Add(advanced);
            _antiDpi.Style = UI.S("Check");
            _antiDpi.Content = CheckContent(L.AntiDpi, L.AntiDpiDescription);
            _antiDpi.Margin = new Thickness(-10, 0, 0, 0);
            form.Children.Add(_antiDpi);
            _skipVerify.Style = UI.S("Check");
            _skipVerify.Content = CheckContent(L.SkipVerification, L.SkipVerificationDescription);
            _skipVerify.Margin = new Thickness(-10, 0, 0, 0);
            form.Children.Add(_skipVerify);
            _skipWarning.Text = L.SkipVerificationWarning;
            _skipWarning.Margin = new Thickness(38, 0, 0, 0);
            form.Children.Add(_skipWarning);
            _skipVerify.Checked += delegate { _skipWarning.Visibility = Visibility.Visible; };
            _skipVerify.Unchecked += delegate { _skipWarning.Visibility = Visibility.Collapsed; };

            SetBody(UI.Scroll(form));

            var submit = UI.Filled(existing == null ? L.Add : L.Save, Submit);
            submit.HorizontalAlignment = HorizontalAlignment.Right;
            SetBottom(submit);

            Fill(existing ?? new ServerInfo());
            _initial = Snapshot();
        }

        void Fill(ServerInfo s)
        {
            _name.Value = s.Name;
            _address.Value = s.Address;
            _domain.Value = s.Domain;
            _sni.Value = s.CustomSni;
            _username.Value = s.Username;
            _password.Value = s.Password;
            _http2.IsChecked = s.Protocol != "http3";
            _quic.IsChecked = s.Protocol == "http3";
            var profiles = App.I.Store.Data.Profiles.Select(p => new KeyValuePair<string, string>(p.Id, p.DisplayName));
            _profile.SetItems(profiles, _profile.SelectedKey ?? s.RoutingProfileId);
            _dns.Value = string.Join("\n", s.DnsServers);
            _clientRandom.Value = s.ClientRandom;
            _ipv6.IsChecked = s.Ipv6;
            _antiDpi.IsChecked = s.AntiDpi;
            _skipVerify.IsChecked = s.SkipVerification;
            _skipWarning.Visibility = s.SkipVerification ? Visibility.Visible : Visibility.Collapsed;
            SetCertificate(s.Certificate, s.CertificateName);
        }

        static StackPanel CheckContent(string title, string description)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
            sp.Children.Add(UI.BodyMedium(title));
            sp.Children.Add(UI.BodySmall(description));
            return sp;
        }

        void SetCertificate(string pem, string name)
        {
            _certificate = pem ?? "";
            _certificateName = string.IsNullOrEmpty(_certificate) ? "" : (string.IsNullOrEmpty(name) ? "certificate.pem" : name);
            _certName.Text = _certificateName;
            _certRemove.Visibility = _certificate.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        string Snapshot()
        {
            return string.Join("\u0001", _name.Value, _address.Value, _domain.Value, _sni.Value, _username.Value, _password.Value,
                _quic.IsChecked == true ? "3" : "2", _profile.SelectedKey, _dns.Value, _clientRandom.Value, _ipv6.IsChecked == true ? "1" : "0", _certificate,
                _antiDpi.IsChecked == true ? "1" : "0", _skipVerify.IsChecked == true ? "1" : "0");
        }

        public override bool IsDirty { get { return Snapshot() != _initial; } }

        void ImportCertificate()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "PEM (*.pem;*.crt;*.cer)|*.pem;*.crt;*.cer|*.*|*.*" };
            if (dlg.ShowDialog(Main) != true) return;
            try
            {
                var text = File.ReadAllText(dlg.FileName);
                if (!text.Contains("-----BEGIN"))
                {
                    Main.ShowSnack(L.ImportFailed);
                    return;
                }
                SetCertificate(text.Trim(), Path.GetFileName(dlg.FileName));
            }
            catch (Exception ex)
            {
                Main.ShowSnack(L.ImportFailed + ": " + ex.Message);
            }
        }

        void ImportFromLink()
        {
            var field = new FormField(null, L.ImportLinkHint) { Width = 460 };
            Main.ShowDialog(L.ImportLinkTitle, field,
                DialogAction.Cancel(),
                new DialogAction(L.Import, delegate
                {
                    var link = field.Value.Trim();
                    if (link.Length == 0) { field.Error = L.FillField; return false; }
                    try
                    {
                        ApplyImported(App.I.ImportDeepLink(link));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        field.Error = ex.Message;
                        return false;
                    }
                }) { IsDefault = true });
        }

        void ImportFromFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "TOML (*.toml)|*.toml|*.*|*.*" };
            if (dlg.ShowDialog(Main) != true) return;
            try
            {
                ApplyImported(App.I.ImportConfigFile(dlg.FileName));
            }
            catch (Exception ex)
            {
                Main.ShowSnack(L.ImportFailed + ": " + ex.Message);
            }
        }

        void ApplyImported(ServerInfo s)
        {
            var keepProfile = _profile.SelectedKey;
            if (string.IsNullOrWhiteSpace(_name.Value) == false) s.Name = _name.Value;
            Fill(s);
            _profile.SetItems(App.I.Store.Data.Profiles.Select(p => new KeyValuePair<string, string>(p.Id, p.DisplayName)), keepProfile);
            Main.ShowSnack(L.Imported);
        }

        bool Validate()
        {
            var fields = new List<KeyValuePair<FormField, string>>();
            var name = _name.Value.Trim();
            string err = null;
            if (name.Length == 0) err = L.FillField;
            else if (App.I.Store.Data.Servers.Any(s => s != _existing && string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase))) err = L.NameUsed;
            fields.Add(new KeyValuePair<FormField, string>(_name, err));

            var address = _address.Value.Trim();
            fields.Add(new KeyValuePair<FormField, string>(_address, address.Length == 0 ? L.FillField : (Validation.IsAddress(address) ? null : L.AddressWrong)));

            var domain = _domain.Value.Trim();
            fields.Add(new KeyValuePair<FormField, string>(_domain, domain.Length == 0 ? L.FillField : (Validation.IsHostOrIp(domain) ? null : L.DomainWrong)));

            var sni = _sni.Value.Trim();
            fields.Add(new KeyValuePair<FormField, string>(_sni, sni.Length == 0 || Validation.IsDomain(sni, true) ? null : L.SniWrong));

            fields.Add(new KeyValuePair<FormField, string>(_username, _username.Value.Trim().Length == 0 ? L.FillField : null));
            fields.Add(new KeyValuePair<FormField, string>(_password, _password.Value.Length == 0 ? L.FillField : null));

            var dns = Validation.Lines(_dns.Value);
            fields.Add(new KeyValuePair<FormField, string>(_dns, dns.All(Validation.IsDnsUpstream) ? null : L.DnsWrong));
            fields.Add(new KeyValuePair<FormField, string>(_clientRandom, Validation.ClientRandomError(_clientRandom.Value)));

            FormField firstInvalid = null;
            foreach (var f in fields)
            {
                f.Key.Error = f.Value;
                if (f.Value != null && firstInvalid == null) firstInvalid = f.Key;
            }
            if (firstInvalid != null)
            {
                firstInvalid.FocusInput();
                firstInvalid.BringIntoView();
                return false;
            }
            return true;
        }

        void Submit()
        {
            if (!Validate()) return;
            var store = App.I.Store;
            var s = _existing ?? new ServerInfo();
            s.Name = _name.Value.Trim();
            s.Address = _address.Value.Trim();
            s.Domain = _domain.Value.Trim();
            s.CustomSni = _sni.Value.Trim();
            s.Username = _username.Value.Trim();
            s.Password = _password.Value;
            s.Protocol = _quic.IsChecked == true ? "http3" : "http2";
            s.RoutingProfileId = _profile.SelectedKey ?? RoutingProfile.DefaultId;
            s.DnsServers = Validation.Lines(_dns.Value);
            s.ClientRandom = _clientRandom.Value.Trim();
            s.Ipv6 = _ipv6.IsChecked == true;
            s.AntiDpi = _antiDpi.IsChecked == true;
            s.SkipVerification = _skipVerify.IsChecked == true;
            s.Certificate = _certificate;
            s.CertificateName = _certificateName;

            if (_existing == null)
            {
                store.Data.Servers.Add(s);
                store.Save();
                Main.ShowSnack(L.ServerCreated, s.Name);
            }
            else
            {
                store.Save();
                Main.ShowSnack(L.ChangesSaved);
                App.I.ReconnectIfActive(delegate(ServerInfo active) { return active.Id == s.Id; });
            }
            Main.Pop(true);
        }

        void AskDelete()
        {
            var s = _existing;
            Main.ShowDialog(L.DeleteServerTitle, UI.BodyMedium(string.Format(L.DeleteServerDescription, s.Name)),
                DialogAction.Cancel(),
                new DialogAction(L.Delete, delegate
                {
                    App.I.DeleteServer(s);
                    Main.ShowSnack(L.ServerDeleted, s.Name);
                    Main.Pop(true);
                    return true;
                }) { Danger = true });
        }
    }

    // ===================== Routing =====================

    class RoutingPage : Page
    {
        readonly ScrollViewer _scroll = UI.Scroll(null);

        public RoutingPage(MainWindow main) : base(main)
        {
            SetAppBar(L.Routing, null);
            SetBody(_scroll);
            SetFab(L.AddProfile, Icons.Add, AddProfile);
        }

        public override void OnShown()
        {
            Render();
        }

        void Render()
        {
            var list = new StackPanel { Margin = new Thickness(0, 0, 0, 88) };
            var profiles = App.I.Store.Data.Profiles;
            for (int i = 0; i < profiles.Count; i++)
            {
                var p = profiles[i];
                Button more = null;
                more = UI.IconButton(Icons.MoreVert, null, delegate { ShowMenu(more, p); });
                list.Children.Add(Tiles.Separated(p.DisplayName, null, delegate { Main.Push(new RoutingDetailsPage(Main, p)); }, more, false));
                if (i != profiles.Count - 1) list.Children.Add(UI.Divider());
            }
            _scroll.Content = list;
        }

        void ShowMenu(Button anchor, RoutingProfile p)
        {
            var entries = new List<MenuEntry> { new MenuEntry(L.EditProfileName, Icons.Edit, delegate { EditName(p); }) };
            if (!p.IsDefault) entries.Add(new MenuEntry(L.DeleteProfile, Icons.Delete, delegate { AskDelete(p); }, true));
            UI.ShowMenu(anchor, entries);
        }

        // RoutingEditNameDialog; returns the validated name through onSave.
        void NameDialog(string title, string current, string excludeId, string actionText, Action<string> onSave)
        {
            var field = new FormField(L.ProfileName, L.ProfileName) { Width = 420, Margin = new Thickness(0) };
            field.Value = current;
            Main.ShowDialog(title, field,
                DialogAction.Cancel(),
                new DialogAction(actionText, delegate
                {
                    var name = field.Value.Trim();
                    if (name.Length == 0) { field.Error = L.FillField; return false; }
                    if (App.I.Store.Data.Profiles.Any(x => x.Id != excludeId && string.Equals(x.DisplayName.Trim(), name, StringComparison.OrdinalIgnoreCase)))
                    {
                        field.Error = L.NameUsed;
                        return false;
                    }
                    onSave(name);
                    return true;
                }) { IsDefault = true });
        }

        void AddProfile()
        {
            NameDialog(L.AddProfile, "", null, L.Create, delegate(string name)
            {
                var p = new RoutingProfile { Name = name };
                App.I.Store.Data.Profiles.Add(p);
                App.I.Store.Save();
                Main.ShowSnack(L.ProfileCreated, name);
                Render();
                Main.Push(new RoutingDetailsPage(Main, p));
            });
        }

        void EditName(RoutingProfile p)
        {
            NameDialog(L.EditProfileName, p.DisplayName, p.Id, L.Save, delegate(string name)
            {
                p.Name = name;
                App.I.Store.Save();
                Main.ShowSnack(L.ChangesSaved);
                Render();
            });
        }

        void AskDelete(RoutingProfile p)
        {
            var userServers = App.I.Store.Data.Servers.Where(s => s.RoutingProfileId == p.Id).ToList();
            var users = userServers.Select(s => s.Name).ToList();
            var content = new StackPanel();
            content.Children.Add(UI.BodyMedium(string.Format(L.DeleteProfileDescription, p.DisplayName)));
            if (users.Count > 0)
            {
                var note = UI.BodyMedium(string.Format(L.ProfileInUse, string.Join(", ", users)), C.NeutralDark);
                note.Margin = new Thickness(0, 8, 0, 0);
                content.Children.Add(note);
            }
            Main.ShowDialog(L.DeleteProfileTitle, content,
                DialogAction.Cancel(),
                new DialogAction(L.Delete, delegate
                {
                    foreach (var s in App.I.Store.Data.Servers.Where(s => s.RoutingProfileId == p.Id))
                        s.RoutingProfileId = RoutingProfile.DefaultId;
                    App.I.Store.Data.Profiles.Remove(p);
                    App.I.Store.Save();
                    Main.ShowSnack(L.ProfileDeleted, p.DisplayName);
                    Render();
                    App.I.ReconnectIfActive(delegate(ServerInfo active) { return userServers.Contains(active); });
                    return true;
                }) { Danger = true });
        }
    }

    class RoutingDetailsPage : Page
    {
        readonly RoutingProfile _profile;
        readonly TextBox _bypass;
        readonly TextBox _vpn;
        readonly TextBlock _bypassError = UI.BodySmall("", C.Error);
        readonly TextBlock _vpnError = UI.BodySmall("", C.Error);
        readonly TextBlock _modeText = UI.BodyMedium("", C.NeutralDark);
        readonly Button _save;
        string _initial;

        public RoutingDetailsPage(MainWindow main, RoutingProfile profile) : base(main)
        {
            _profile = profile;
            Button more = null;
            more = UI.IconButton(Icons.MoreVert, null, delegate
            {
                UI.ShowMenu(more, new List<MenuEntry>
                {
                    new MenuEntry(L.ChangeDefaultRouting, Icons.Route, ChangeDefaultRouting),
                    new MenuEntry(L.DeleteAllRules, Icons.Delete, AskDeleteAll, true)
                });
            });
            SetAppBar(profile.DisplayName, Icons.Back, more);

            var grid = new Grid { Margin = new Thickness(16) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var modeRow = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            var change = UI.TextButton(L.ChangeDefaultRouting, ChangeDefaultRouting);
            DockPanel.SetDock(change, Dock.Right);
            modeRow.Children.Add(change);
            _modeText.VerticalAlignment = VerticalAlignment.Center;
            modeRow.Children.Add(_modeText);
            Grid.SetColumnSpan(modeRow, 3);
            grid.Children.Add(modeRow);

            var bypassBox = RulesBox(L.Bypass, _bypassError, out _bypass);
            Grid.SetRow(bypassBox, 1);
            grid.Children.Add(bypassBox);
            var vpnBox = RulesBox(L.Vpn, _vpnError, out _vpn);
            Grid.SetRow(vpnBox, 1);
            Grid.SetColumn(vpnBox, 2);
            grid.Children.Add(vpnBox);
            SetBody(grid);

            _save = UI.Filled(L.Save, Save);
            _save.HorizontalAlignment = HorizontalAlignment.Right;
            SetBottom(_save);

            Load();
        }

        static FrameworkElement RulesBox(string label, TextBlock error, out TextBox box)
        {
            var dock = new DockPanel();
            var lb = UI.Text(label, 13, FontWeights.SemiBold, C.NeutralDark);
            lb.Margin = new Thickness(0, 0, 0, 6);
            DockPanel.SetDock(lb, Dock.Top);
            dock.Children.Add(lb);
            error.Margin = new Thickness(14, 4, 14, 0);
            error.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(error, Dock.Bottom);
            dock.Children.Add(error);

            var grid = new Grid();
            var tb = new TextBox
            {
                Style = UI.S("TextBoxField"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalContentAlignment = VerticalAlignment.Top,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = Theme.Mono,
                FontSize = 13
            };
            grid.Children.Add(tb);
            var hint = new TextBlock { Text = L.RulesHint, Foreground = C.NeutralLight, FontSize = 14, Margin = new Thickness(14, 10, 14, 10), IsHitTestVisible = false, TextWrapping = TextWrapping.Wrap };
            grid.Children.Add(hint);
            tb.TextChanged += delegate { hint.Visibility = tb.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; };
            dock.Children.Add(grid);
            box = tb;
            return dock;
        }

        void Load()
        {
            _bypass.Text = string.Join("\n", _profile.BypassRules);
            _vpn.Text = string.Join("\n", _profile.VpnRules);
            _bypass.TextChanged += delegate { UpdateSave(); };
            _vpn.TextChanged += delegate { UpdateSave(); };
            _initial = _bypass.Text + "\u0001" + _vpn.Text;
            UpdateMode();
            UpdateSave();
        }

        void UpdateMode()
        {
            _modeText.Text = _profile.DefaultMode == "bypass" ? L.DefaultRoutingBypass : L.DefaultRoutingVpn;
        }

        void UpdateSave()
        {
            _save.IsEnabled = IsDirty;
        }

        public override bool IsDirty { get { return _bypass.Text + "\u0001" + _vpn.Text != _initial; } }

        static bool CheckRules(TextBox box, TextBlock error, out List<string> rules)
        {
            rules = Validation.Lines(box.Text.Replace("\r", ""));
            var bad = rules.Where(r => !Validation.IsRule(r)).Take(3).ToList();
            box.Tag = bad.Count > 0 ? "error" : null;
            error.Text = bad.Count > 0 ? L.DomainWrong + ": " + string.Join(", ", bad) : "";
            error.Visibility = bad.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            return bad.Count == 0;
        }

        void Save()
        {
            List<string> bypass, vpn;
            bool ok = CheckRules(_bypass, _bypassError, out bypass);
            ok &= CheckRules(_vpn, _vpnError, out vpn);
            if (!ok) return;
            _profile.BypassRules = bypass.Distinct().ToList();
            _profile.VpnRules = vpn.Distinct().ToList();
            App.I.Store.Save();
            _bypass.Text = string.Join("\n", _profile.BypassRules);
            _vpn.Text = string.Join("\n", _profile.VpnRules);
            _initial = _bypass.Text + "\u0001" + _vpn.Text;
            UpdateSave();
            Main.ShowSnack(L.ChangesSaved);
            ReconnectUsers();
        }

        void ReconnectUsers()
        {
            var id = _profile.Id;
            App.I.ReconnectIfActive(delegate(ServerInfo active) { return active.RoutingProfileId == id; });
        }

        // RoutingDetailsChangeRoutingDialog.
        void ChangeDefaultRouting()
        {
            string mode = _profile.DefaultMode;
            var panel = new StackPanel();
            var desc = UI.BodyMedium(L.ChangeDefaultRoutingDescription, C.NeutralDark);
            desc.Margin = new Thickness(0, 0, 0, 8);
            panel.Children.Add(desc);
            var vpnRb = Tiles.Radio(L.Vpn, null, "defmode", mode != "bypass", delegate { mode = "vpn"; });
            var bypassRb = Tiles.Radio(L.Bypass, null, "defmode", mode == "bypass", delegate { mode = "bypass"; });
            vpnRb.Margin = new Thickness(-10, 0, 0, 0);
            bypassRb.Margin = new Thickness(-10, 0, 0, 0);
            panel.Children.Add(vpnRb);
            panel.Children.Add(bypassRb);
            Main.ShowDialog(L.ChangeDefaultRouting, panel,
                DialogAction.Cancel(),
                new DialogAction(L.Save, delegate
                {
                    if (mode != _profile.DefaultMode)
                    {
                        _profile.DefaultMode = mode;
                        App.I.Store.Save();
                        UpdateMode();
                        Main.ShowSnack(L.ChangesSaved);
                        ReconnectUsers();
                    }
                    return true;
                }) { IsDefault = true });
        }

        void AskDeleteAll()
        {
            Main.ShowDialog(L.DeleteAllRulesTitle, UI.BodyMedium(string.Format(L.DeleteAllRulesDescription, _profile.DisplayName)),
                DialogAction.Cancel(),
                new DialogAction(L.Delete, delegate
                {
                    _profile.BypassRules.Clear();
                    _profile.VpnRules.Clear();
                    App.I.Store.Save();
                    _bypass.Text = "";
                    _vpn.Text = "";
                    _initial = "\u0001";
                    UpdateSave();
                    Main.ShowSnack(L.AllRulesDeleted);
                    ReconnectUsers();
                    return true;
                }) { Danger = true });
        }
    }

    // ===================== Settings =====================

    class SettingsPage : Page
    {
        public SettingsPage(MainWindow main) : base(main)
        {
            SetAppBar(L.Settings, null);
            var list = new StackPanel();
            list.Children.Add(Tiles.Arrow(L.ConnectionLog, delegate { Main.Push(new QueryLogPage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.ClientLog, delegate { Main.Push(new ConnectionLogPage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.AppLogging, delegate { Main.Push(new AppLoggingPage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.LaunchAndConnection, delegate { Main.Push(new LaunchAndConnectionPage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.ConnectionMode, delegate { Main.Push(new ConnectionModePage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.ExcludedRoutes, delegate { Main.Push(new ExcludedRoutesPage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.Appearance, delegate { Main.Push(new AppearancePage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.BackupTitle, delegate { Main.Push(new BackupPage(Main)); }));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.FollowOnGithub, delegate { App.OpenUrl("https://github.com/TrustTunnel"); }, Icons.OpenInNew));
            list.Children.Add(UI.Divider());
            list.Children.Add(Tiles.Arrow(L.About, delegate { Main.Push(new AboutPage(Main)); }));
            SetBody(UI.Scroll(list));
        }
    }

    class ConnectionLogPage : Page
    {
        readonly ObservableCollection<LogLine> _items = new ObservableCollection<LogLine>();
        readonly ListBox _list;
        readonly Grid _host = new Grid();
        readonly FrameworkElement _empty;

        const string ItemTemplateXaml =
"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
"  <TextBlock Text='{Binding Text}' Foreground='{Binding Brush}' TextWrapping='NoWrap'/>" +
"</DataTemplate>";

        internal static string ItemStyleXaml()
        {
            return
"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'>" +
"  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>" +
"  <Setter Property='Padding' Value='16,1'/>" +
"  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'>" +
"    <Border x:Name='bd' Background='Transparent' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border>" +
"    <ControlTemplate.Triggers>" +
"      <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Background' Value='" + C.P.NavHover + "'/></Trigger>" +
"      <Trigger Property='IsSelected' Value='True'><Setter TargetName='bd' Property='Background' Value='" + C.P.LogSelected + "'/></Trigger>" +
"    </ControlTemplate.Triggers>" +
"  </ControlTemplate></Setter.Value></Setter>" +
"</Style>";
        }

        public ConnectionLogPage(MainWindow main) : base(main)
        {
            SetAppBar(L.ClientLog, Icons.Back,
                UI.IconButton(Icons.Copy, L.CopyLog, CopyAll),
                UI.IconButton(Icons.ClearAll, L.ClearLog, delegate { App.I.Vpn.ClearLog(); }),
                UI.IconButton(Icons.Folder, L.OpenLogFolder, delegate { App.OpenFolder(App.I.LogDir); }));

            _list = new ListBox
            {
                ItemsSource = _items,
                BorderThickness = new Thickness(0),
                Background = C.Background,
                FontFamily = Theme.Mono,
                FontSize = 12,
                SelectionMode = SelectionMode.Extended,
                ItemTemplate = (DataTemplate)XamlReader.Parse(ItemTemplateXaml),
                ItemContainerStyle = (Style)XamlReader.Parse(ItemStyleXaml()),
                Padding = new Thickness(0, 4, 0, 4)
            };
            VirtualizingStackPanel.SetIsVirtualizing(_list, true);
            VirtualizingStackPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
            _list.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, delegate { CopySelected(); }));

            _empty = Placeholder(Icons.ListAlt, L.ClientLogEmptyTitle, L.ClientLogEmptyDescription, null, null);
            _host.Children.Add(_list);
            _host.Children.Add(_empty);
            SetBody(_host);

            foreach (var l in App.I.Vpn.Lines) _items.Add(l);
            UpdateEmpty();
        }

        public override void OnShown()
        {
            App.I.Vpn.LineAdded -= OnLine;
            App.I.Vpn.LineAdded += OnLine;
            App.I.Vpn.LogCleared -= OnCleared;
            App.I.Vpn.LogCleared += OnCleared;
            ScrollToEnd();
        }

        public override void OnRemoved()
        {
            App.I.Vpn.LineAdded -= OnLine;
            App.I.Vpn.LogCleared -= OnCleared;
        }

        void OnLine(LogLine line)
        {
            var sv = FindScroll(_list);
            bool atEnd = sv == null || sv.VerticalOffset >= sv.ScrollableHeight - 1;
            _items.Add(line);
            while (_items.Count > 5000) _items.RemoveAt(0);
            UpdateEmpty();
            if (atEnd) ScrollToEnd();
        }

        void OnCleared()
        {
            _items.Clear();
            UpdateEmpty();
        }

        void UpdateEmpty()
        {
            _empty.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _list.Visibility = _items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        void ScrollToEnd()
        {
            if (_items.Count > 0) _list.ScrollIntoView(_items[_items.Count - 1]);
        }

        static ScrollViewer FindScroll(DependencyObject d)
        {
            if (d is ScrollViewer) return (ScrollViewer)d;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            {
                var r = FindScroll(VisualTreeHelper.GetChild(d, i));
                if (r != null) return r;
            }
            return null;
        }

        void CopySelected()
        {
            var lines = _list.SelectedItems.Cast<LogLine>().OrderBy(l => _items.IndexOf(l)).Select(l => l.Text);
            SetClipboard(string.Join(Environment.NewLine, lines));
        }

        void CopyAll()
        {
            SetClipboard(string.Join(Environment.NewLine, _items.Select(l => l.Text)));
            Main.ShowSnack(L.LogCopied);
        }

        static void SetClipboard(string text)
        {
            try { Clipboard.SetText(text ?? ""); } catch { }
        }
    }

    // Connection log (Flutter QueryLogScreen): one row per connection with the routing decision.
    class QueryLogPage : Page
    {
        readonly ObservableCollection<QueryEntry> _items = new ObservableCollection<QueryEntry>();
        readonly ListBox _list;
        readonly FrameworkElement _empty;
        readonly Border _debugBanner;
        readonly TextBox _search;
        string _filter = "";
        const int MaxShown = 3000;

        static string ItemTemplateXaml()
        {
            var p = C.P;
            return
"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" +
"  <Grid Margin='0,6'>" +
"    <Grid.ColumnDefinitions><ColumnDefinition Width='72'/><ColumnDefinition Width='*'/><ColumnDefinition Width='Auto'/></Grid.ColumnDefinitions>" +
"    <TextBlock Text='{Binding TimeText}' Foreground='" + p.NeutralDark + "' FontSize='12' VerticalAlignment='Center'/>" +
"    <StackPanel Grid.Column='1' Margin='0,0,12,0'>" +
"      <TextBlock Text='{Binding Title}' Foreground='" + p.NeutralBlack + "' FontSize='14' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/>" +
"      <TextBlock Text='{Binding Subtitle}' Foreground='" + p.NeutralLight + "' FontSize='12' TextTrimming='CharacterEllipsis'/>" +
"    </StackPanel>" +
"    <Border Grid.Column='2' Background='{Binding ActionBrush}' CornerRadius='10' Padding='10,2' VerticalAlignment='Center'>" +
"      <TextBlock Text='{Binding ActionText}' Foreground='" + p.OnAccent + "' FontSize='12' FontWeight='SemiBold'/>" +
"    </Border>" +
"  </Grid>" +
"</DataTemplate>";
        }

        public QueryLogPage(MainWindow main) : base(main)
        {
            SetAppBar(L.ConnectionLog, Icons.Back, UI.IconButton(Icons.ClearAll, L.ClearLog, delegate { App.I.Vpn.ClearQueries(); }));

            var dock = new DockPanel();

            _debugBanner = new Border { Background = C.BackgroundAdditional, CornerRadius = new CornerRadius(12), Margin = new Thickness(16, 0, 16, 8), Padding = new Thickness(16, 8, 8, 8) };
            var bannerRow = new DockPanel();
            var enable = UI.TextButton(L.EnableDebug, EnableDebug);
            DockPanel.SetDock(enable, Dock.Right);
            bannerRow.Children.Add(enable);
            var bannerText = UI.BodyMedium(L.QueryLogNeedsDebug, C.NeutralDark);
            bannerText.VerticalAlignment = VerticalAlignment.Center;
            bannerRow.Children.Add(bannerText);
            _debugBanner.Child = bannerRow;
            DockPanel.SetDock(_debugBanner, Dock.Top);
            dock.Children.Add(_debugBanner);

            var searchGrid = new Grid { Margin = new Thickness(16, 0, 16, 8) };
            _search = new TextBox { Style = UI.S("TextBoxField"), Padding = new Thickness(40, 10, 12, 10) };
            searchGrid.Children.Add(_search);
            var searchIcon = UI.Icon(Icons.Search, 20, C.NeutralDark);
            searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
            searchIcon.VerticalAlignment = VerticalAlignment.Center;
            searchIcon.Margin = new Thickness(12, 0, 0, 0);
            searchIcon.IsHitTestVisible = false;
            searchGrid.Children.Add(searchIcon);
            var hint = new TextBlock { Text = L.Search, Foreground = C.NeutralLight, FontSize = 14, Margin = new Thickness(42, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            searchGrid.Children.Add(hint);
            _search.TextChanged += delegate
            {
                hint.Visibility = _search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                _filter = _search.Text.Trim();
                Reload();
            };
            DockPanel.SetDock(searchGrid, Dock.Top);
            dock.Children.Add(searchGrid);

            _list = new ListBox
            {
                ItemsSource = _items,
                BorderThickness = new Thickness(0),
                Background = C.Background,
                ItemTemplate = (DataTemplate)XamlReader.Parse(ItemTemplateXaml()),
                ItemContainerStyle = (Style)XamlReader.Parse(ConnectionLogPage.ItemStyleXaml()),
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
            VirtualizingStackPanel.SetIsVirtualizing(_list, true);
            VirtualizingStackPanel.SetVirtualizationMode(_list, VirtualizationMode.Recycling);
            _empty = Placeholder(Icons.ListAlt, L.ConnectionLogEmptyTitle, L.ConnectionLogEmptyDescription, null, null);
            var host = new Grid();
            host.Children.Add(_list);
            host.Children.Add(_empty);
            dock.Children.Add(host);
            SetBody(dock);

            UpdateBanner();
            Reload();
        }

        public override void OnShown()
        {
            App.I.Vpn.QueryAdded -= OnAdded;
            App.I.Vpn.QueryAdded += OnAdded;
            App.I.Vpn.QueriesCleared -= Reload;
            App.I.Vpn.QueriesCleared += Reload;
        }

        public override void OnRemoved()
        {
            App.I.Vpn.QueryAdded -= OnAdded;
            App.I.Vpn.QueriesCleared -= Reload;
        }

        void UpdateBanner()
        {
            _debugBanner.Visibility = App.I.Store.Data.Settings.LogLevel == "debug" ? Visibility.Collapsed : Visibility.Visible;
        }

        void EnableDebug()
        {
            App.I.Store.Data.Settings.LogLevel = "debug";
            App.I.Store.Save();
            UpdateBanner();
            // The client reads the level at start, so an active connection is restarted.
            App.I.ReconnectIfActive(delegate { return true; });
            Main.ShowSnack(L.ChangesSaved);
        }

        void Reload()
        {
            _items.Clear();
            var queries = App.I.Vpn.Queries;
            for (int i = queries.Count - 1; i >= 0 && _items.Count < MaxShown; i--)
                if (queries[i].Matches(_filter)) _items.Add(queries[i]);
            UpdateEmpty();
        }

        void OnAdded(QueryEntry entry)
        {
            if (!entry.Matches(_filter)) return;
            _items.Insert(0, entry);
            if (_items.Count > MaxShown) _items.RemoveAt(_items.Count - 1);
            UpdateEmpty();
        }

        void UpdateEmpty()
        {
            bool empty = _items.Count == 0;
            _empty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            _list.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    class BackupPage : Page
    {
        public BackupPage(MainWindow main) : base(main)
        {
            SetAppBar(L.BackupTitle, Icons.Back);
            var list = new StackPanel();

            list.Children.Add(Tiles.Section(L.Export));
            var exportText = UI.BodyMedium(L.ExportDescription, C.NeutralDark);
            exportText.Margin = new Thickness(16, 4, 16, 12);
            list.Children.Add(exportText);
            var export = UI.Filled(L.ExportButton, Export);
            export.HorizontalAlignment = HorizontalAlignment.Left;
            export.Margin = new Thickness(16, 0, 16, 8);
            list.Children.Add(export);

            list.Children.Add(Tiles.Section(L.Import));
            var importText = UI.BodyMedium(L.ImportDescription, C.NeutralDark);
            importText.Margin = new Thickness(16, 4, 16, 12);
            list.Children.Add(importText);
            var import = UI.Outlined(L.ImportButton, Icons.FileOpen, Import);
            import.HorizontalAlignment = HorizontalAlignment.Left;
            import.Margin = new Thickness(16, 0, 16, 8);
            list.Children.Add(import);

            SetBody(UI.Scroll(list));
        }

        void Export()
        {
            var field = new FormField(L.BackupPassword, L.PasswordHint, L.BackupPasswordHelper, 1, true) { Width = 440, Margin = new Thickness(0) };
            Main.ShowDialog(L.ExportButton, field,
                DialogAction.Cancel(),
                new DialogAction(L.Continue, delegate
                {
                    var password = field.Value;
                    Dispatcher.BeginInvoke(new Action(delegate { SaveBackup(password); }));
                    return true;
                }) { IsDefault = true });
        }

        void SaveBackup(string password)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = L.BackupFilter + " (*.ttgui.json)|*.ttgui.json|JSON (*.json)|*.json",
                FileName = "trusttunnel-gui-" + DateTime.Now.ToString("yyyy-MM-dd") + ".ttgui.json"
            };
            if (dlg.ShowDialog(Main) != true) return;
            try
            {
                Backup.Export(App.I.Store.Data, dlg.FileName, password);
                var n = App.I.Store.Data.Servers.Count;
                Main.ShowSnack(string.Format(L.BackupExported, n) + (string.IsNullOrEmpty(password) && n > 0 ? " " + L.PasswordsNotExported : ""));
            }
            catch (Exception ex)
            {
                Main.ShowSnack(L.SomethingWrong + ": " + ex.Message);
            }
        }

        void Import()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = L.BackupFilter + " (*.ttgui.json;*.json)|*.ttgui.json;*.json|*.*|*.*" };
            if (dlg.ShowDialog(Main) != true) return;
            BackupFile file;
            try
            {
                file = Backup.Read(dlg.FileName);
            }
            catch (BackupException ex)
            {
                Main.ShowSnack(ex.Message);
                return;
            }
            if (file.Secrets == null)
            {
                ChooseMode(file, new Dictionary<string, string>());
                return;
            }
            var field = new FormField(L.BackupPassword, L.PasswordHint, null, 1, true) { Width = 440, Margin = new Thickness(0) };
            Main.ShowDialog(L.BackupProtected, field,
                new DialogAction(L.WithoutPasswords, delegate
                {
                    Dispatcher.BeginInvoke(new Action(delegate { ChooseMode(file, new Dictionary<string, string>()); }));
                    return true;
                }) { IsCancel = true },
                new DialogAction(L.Continue, delegate
                {
                    try
                    {
                        var passwords = Backup.Passwords(file, field.Value);
                        Dispatcher.BeginInvoke(new Action(delegate { ChooseMode(file, passwords); }));
                        return true;
                    }
                    catch (BackupException ex)
                    {
                        field.Error = ex.Message;
                        return false;
                    }
                }) { IsDefault = true });
        }

        void ChooseMode(BackupFile file, Dictionary<string, string> passwords)
        {
            var description = UI.BodyMedium(string.Format(L.ImportModeDescription, file.Servers.Count, file.Profiles.Count(p => !p.IsDefault)));
            Main.ShowDialog(L.ImportModeTitle, description,
                DialogAction.Cancel(),
                new DialogAction(L.ReplaceAll, delegate { Apply(file, passwords, true); return true; }) { Danger = true },
                new DialogAction(L.AddToExisting, delegate { Apply(file, passwords, false); return true; }) { IsDefault = true });
        }

        void Apply(BackupFile file, Dictionary<string, string> passwords, bool replace)
        {
            if (replace && App.I.Vpn.State != VpnState.Disconnected) App.I.Disconnect();
            int count = Backup.Apply(App.I.Store.Data, file, passwords, replace);
            App.I.Store.Save();
            bool missingPasswords = file.Servers.Any(s => !passwords.ContainsKey(s.Id));
            Main.ShowSnack(string.Format(L.BackupImported, count) + (missingPasswords && count > 0 ? " " + L.PasswordsNotImported : ""));
        }
    }

    class AppLoggingPage : Page
    {
        public AppLoggingPage(MainWindow main) : base(main)
        {
            SetAppBar(L.AppLogging, Icons.Back);
            var settings = App.I.Store.Data.Settings;
            var list = new StackPanel();
            list.Children.Add(Tiles.Section(L.LoggingLevel));
            list.Children.Add(Tiles.Radio(L.LoggingDefault, L.LoggingDefaultDescription, "loglevel", settings.LogLevel != "debug", delegate { SetLevel("info"); }));
            list.Children.Add(Tiles.Radio(L.LoggingDebug, L.LoggingDebugDescription, "loglevel", settings.LogLevel == "debug", delegate { SetLevel("debug"); }));
            SetBody(UI.Scroll(list));
        }

        void SetLevel(string level)
        {
            var settings = App.I.Store.Data.Settings;
            if (settings.LogLevel == level) return;
            settings.LogLevel = level;
            App.I.Store.Save();
            Main.ShowSnack(App.I.Vpn.IsBusy ? L.LoggingAppliesOnReconnect : L.ChangesSaved);
        }
    }

    class LaunchAndConnectionPage : Page
    {
        System.Windows.Controls.Primitives.ToggleButton _openWindow;

        public LaunchAndConnectionPage(MainWindow main) : base(main)
        {
            SetAppBar(L.LaunchAndConnection, Icons.Back);
            var settings = App.I.Store.Data.Settings;
            var list = new StackPanel();

            list.Children.Add(Tiles.Section(L.Launch));
            System.Windows.Controls.Primitives.ToggleButton launch;
            list.Children.Add(Tiles.Switch(L.LaunchAtLogin, null, Autostart.IsEnabled(), delegate(bool on)
            {
                if (!Autostart.Set(on, App.ExePath))
                    Main.ShowSnack(!on && !App.IsElevated && Autostart.TaskExists() ? L.AutostartNeedsAdmin : L.LaunchAtLoginFailed);
                Refresh();
            }, out launch));
            _launch = launch;
            list.Children.Add(Tiles.Switch(L.OpenMainWindow, null, settings.OpenMainWindowOnLaunch, delegate(bool on)
            {
                settings.OpenMainWindowOnLaunch = on;
                App.I.Store.Save();
            }, out _openWindow));
            System.Windows.Controls.Primitives.ToggleButton admin = null;
            list.Children.Add(Tiles.Switch(L.RunAsAdmin, L.RunAsAdminDescription, settings.RunAsAdmin, delegate(bool on)
            {
                settings.RunAsAdmin = on;
                App.I.Store.Save();
                if (on && !App.IsElevated && !App.I.RestartElevated(null))
                {
                    // UAC declined: keep the previous behaviour.
                    settings.RunAsAdmin = false;
                    App.I.Store.Save();
                    admin.IsChecked = false;
                }
            }, out admin));

            list.Children.Add(Tiles.Section(L.Connection));
            bool hasServers = App.I.Store.Data.Servers.Count > 0;
            System.Windows.Controls.Primitives.ToggleButton auto;
            list.Children.Add(Tiles.Switch(L.AutoConnect, hasServers ? null : L.AutoConnectNoServers, settings.AutoConnectOnLaunch && hasServers, delegate(bool on)
            {
                settings.AutoConnectOnLaunch = on;
                App.I.Store.Save();
            }, out auto));
            auto.IsEnabled = hasServers;
            System.Windows.Controls.Primitives.ToggleButton kill;
            list.Children.Add(Tiles.Switch(L.KillSwitch, L.KillSwitchDescription, settings.KillSwitch, delegate(bool on)
            {
                settings.KillSwitch = on;
                App.I.Store.Save();
                App.I.ReconnectIfActive(delegate { return true; });
            }, out kill));
            System.Windows.Controls.Primitives.ToggleButton reconnect;
            list.Children.Add(Tiles.Switch(L.AutoReconnect, L.AutoReconnectDescription, settings.AutoReconnect, delegate(bool on)
            {
                settings.AutoReconnect = on;
                App.I.Store.Save();
                if (!on) App.I.CancelReconnect();
            }, out reconnect));

            list.Children.Add(Tiles.Section(L.Updates));
            System.Windows.Controls.Primitives.ToggleButton updates;
            list.Children.Add(Tiles.Switch(L.CheckUpdates, L.CheckUpdatesDescription, settings.CheckUpdates, delegate(bool on)
            {
                settings.CheckUpdates = on;
                App.I.Store.Save();
            }, out updates));

            SetBody(UI.Scroll(list));
            Refresh();
        }

        System.Windows.Controls.Primitives.ToggleButton _launch;

        void Refresh()
        {
            bool on = Autostart.IsEnabled();
            _launch.IsChecked = on;
            _openWindow.IsEnabled = on;
        }
    }

    class AppearancePage : Page
    {
        public AppearancePage(MainWindow main) : base(main)
        {
            SetAppBar(L.Appearance, Icons.Back);
            var s = App.I.Store.Data.Settings;
            var list = new StackPanel();
            list.Children.Add(Tiles.Section(L.Theme));
            list.Children.Add(Tiles.Radio(L.ThemeSystem, null, "theme", s.Theme == "system", delegate { Set("system", null); }));
            list.Children.Add(Tiles.Radio(L.ThemeLight, null, "theme", s.Theme == "light", delegate { Set("light", null); }));
            list.Children.Add(Tiles.Radio(L.ThemeDark, null, "theme", s.Theme == "dark", delegate { Set("dark", null); }));
            list.Children.Add(Tiles.Section(L.Language));
            list.Children.Add(Tiles.Radio(L.LanguageSystem, null, "lang", s.Language == "system", delegate { Set(null, "system"); }));
            list.Children.Add(Tiles.Radio(L.LanguageRussian, null, "lang", s.Language == "ru", delegate { Set(null, "ru"); }));
            list.Children.Add(Tiles.Radio(L.LanguageEnglish, null, "lang", s.Language == "en", delegate { Set(null, "en"); }));
            SetBody(UI.Scroll(list));
        }

        void Set(string theme, string language)
        {
            var s = App.I.Store.Data.Settings;
            if (theme != null) s.Theme = theme;
            if (language != null) s.Language = language;
            App.I.Store.Save();
            var window = Main;
            // Rebuild after the click handler returns, then come back to this page.
            Dispatcher.BeginInvoke(new Action(delegate
            {
                App.I.ApplyAppearance(delegate
                {
                    window.SelectTab(2);
                    window.Push(new AppearancePage(window));
                });
            }));
        }
    }

    // Listener selection: TUN (whole system) or a local SOCKS5 proxy ([listener.socks] of the client config).
    class ConnectionModePage : Page
    {
        readonly RadioButton _tun;
        readonly RadioButton _socks;
        readonly StackPanel _socksPanel = new StackPanel();
        readonly FormField _port = new FormField(L.SocksPort, "1080");
        readonly FormField _username = new FormField(L.Username, L.UsernameHint, L.SocksAuthHelper);
        readonly FormField _password = new FormField(L.Password, L.PasswordHint, null, 1, true);
        readonly TextBlock _lanInfo = UI.BodyMedium("", C.NeutralDark);
        readonly DropdownField _adapter = new DropdownField(L.SocksAdapter);
        readonly FrameworkElement _adapterPanel;
        readonly StackPanel _tunPanel = new StackPanel();
        readonly DropdownField _tunAdapter = new DropdownField(L.TunAdapter);
        readonly TextBlock _tunInfo = UI.BodyMedium("", C.NeutralDark);
        readonly FormField _mtu = new FormField(L.Mtu, Defaults.Mtu.ToString(), L.MtuHelper);
        System.Windows.Controls.Primitives.ToggleButton _dnsSwitch;
        System.Windows.Controls.Primitives.ToggleButton _pqSwitch;
        List<NetAdapter> _outbound = new List<NetAdapter>();
        readonly Button _save;
        List<NetAdapter> _adapters = new List<NetAdapter>();
        System.Windows.Controls.Primitives.ToggleButton _lan;
        string _initial;

        public ConnectionModePage(MainWindow main) : base(main)
        {
            SetAppBar(L.ConnectionMode, Icons.Back);
            var s = App.I.Store.Data.Settings;
            var list = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            list.Children.Add(Tiles.Section(L.Mode));
            var tunDescription = L.ModeTunDescription + (App.IsElevated ? "" : ". " + L.AdminRequired);
            _tun = Tiles.Radio(L.ModeTun, tunDescription, "listener", !s.IsSocks, delegate { Update(); });
            _socks = Tiles.Radio(L.ModeSocks, L.ModeSocksDescription, "listener", s.IsSocks, delegate { Update(); });
            list.Children.Add(_tun);
            list.Children.Add(_socks);

            // TUN: adapter used for the connection to the server (bound_if).
            _tunPanel.Children.Add(Tiles.Section(L.ModeTun));
            var tunForm = new StackPanel { Margin = new Thickness(16, 8, 16, 0) };
            var tunRow = new DockPanel();
            var tunRefresh = UI.IconButton(Icons.Update, L.RefreshAdapters, delegate { LoadOutbound(_tunAdapter.SelectedKey); Update(); });
            tunRefresh.VerticalAlignment = VerticalAlignment.Bottom;
            tunRefresh.Margin = new Thickness(8, 0, 0, 21);
            DockPanel.SetDock(tunRefresh, Dock.Right);
            tunRow.Children.Add(tunRefresh);
            tunRow.Children.Add(_tunAdapter);
            tunForm.Children.Add(tunRow);
            _tunPanel.Children.Add(UI.MaxWidthLeft(tunForm, 640));
            _tunInfo.Margin = new Thickness(16, 0, 16, 12);
            _tunPanel.Children.Add(_tunInfo);
            var mtuForm = new StackPanel { Margin = new Thickness(16, 8, 16, 0) };
            _mtu.Value = s.Mtu.ToString();
            mtuForm.Children.Add(_mtu);
            _tunPanel.Children.Add(UI.MaxWidthLeft(mtuForm, 640));
            _tunPanel.Children.Add(Tiles.Switch(L.ChangeSystemDns, L.ChangeSystemDnsDescription, s.ChangeSystemDns, delegate { Update(); }, out _dnsSwitch));
            list.Children.Add(_tunPanel);
            LoadOutbound(s.TunAdapterId);
            _tunAdapter.Changed += Update;

            _socksPanel.Children.Add(Tiles.Section(L.SocksSection));
            var form = new StackPanel { Margin = new Thickness(16, 8, 16, 0) };
            _port.Value = s.SocksPort.ToString();
            form.Children.Add(_port);
            _socksPanel.Children.Add(UI.MaxWidthLeft(form, 640));
            _socksPanel.Children.Add(Tiles.Switch(L.SocksLan, L.SocksLanDescription, s.SocksAllowLan, delegate { Update(); }, out _lan));

            // Which adapter the proxy listens on when LAN access is on.
            var adapterForm = new StackPanel { Margin = new Thickness(16, 8, 16, 0) };
            var adapterRow = new DockPanel();
            var refresh = UI.IconButton(Icons.Update, L.RefreshAdapters, delegate { LoadAdapters(_adapter.SelectedKey); Update(); });
            refresh.VerticalAlignment = VerticalAlignment.Bottom;
            refresh.Margin = new Thickness(8, 0, 0, 21);
            DockPanel.SetDock(refresh, Dock.Right);
            adapterRow.Children.Add(refresh);
            adapterRow.Children.Add(_adapter);
            adapterForm.Children.Add(adapterRow);
            _adapterPanel = UI.MaxWidthLeft(adapterForm, 640);
            _socksPanel.Children.Add(_adapterPanel);
            LoadAdapters(s.SocksAdapterId);
            _adapter.Changed += Update;

            _lanInfo.Margin = new Thickness(16, 0, 16, 12);
            _socksPanel.Children.Add(_lanInfo);
            var auth = new StackPanel { Margin = new Thickness(16, 8, 16, 0) };
            _username.Value = s.SocksUsername;
            _password.Value = s.SocksPassword;
            auth.Children.Add(_username);
            auth.Children.Add(_password);
            _socksPanel.Children.Add(UI.MaxWidthLeft(auth, 640));
            list.Children.Add(_socksPanel);

            list.Children.Add(Tiles.Section(L.Advanced));
            list.Children.Add(Tiles.Switch(L.PostQuantum, L.PostQuantumDescription, s.PostQuantum, delegate { Update(); }, out _pqSwitch));
            _mtu.Changed += Update;
            SetBody(UI.Scroll(list));

            _save = UI.Filled(L.Save, Save);
            _save.HorizontalAlignment = HorizontalAlignment.Right;
            SetBottom(_save);

            _port.Changed += Update;
            _username.Changed += Update;
            _password.Changed += Update;
            _initial = Snapshot();
            Update();
        }

        // "All adapters" plus every active adapter; a saved adapter that is gone stays selectable and is marked unavailable.
        void LoadAdapters(string selectedId)
        {
            _adapters = Net.Adapters();
            var items = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("", L.SocksAdapterAll) };
            items.AddRange(_adapters.Select(a => new KeyValuePair<string, string>(a.Id, a.Label)));
            var saved = App.I.Store.Data.Settings;
            if (!string.IsNullOrEmpty(selectedId) && _adapters.All(a => a.Id != selectedId))
            {
                var name = selectedId == saved.SocksAdapterId && saved.SocksAdapterName.Length > 0 ? saved.SocksAdapterName : selectedId;
                items.Add(new KeyValuePair<string, string>(selectedId, string.Format(L.AdapterUnavailableItem, name)));
            }
            _adapter.SetItems(items, selectedId ?? "");
        }

        void LoadOutbound(string selectedId)
        {
            _outbound = Net.OutboundAdapters();
            var items = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("", L.TunAdapterAuto) };
            items.AddRange(_outbound.Select(a => new KeyValuePair<string, string>(a.Id, a.Label)));
            var saved = App.I.Store.Data.Settings;
            if (!string.IsNullOrEmpty(selectedId) && _outbound.All(a => a.Id != selectedId))
            {
                var name = selectedId == saved.TunAdapterId && saved.TunAdapterName.Length > 0 ? saved.TunAdapterName : selectedId;
                items.Add(new KeyValuePair<string, string>(selectedId, string.Format(L.AdapterUnavailableItem, name)));
            }
            _tunAdapter.SetItems(items, selectedId ?? "");
        }

        string Snapshot()
        {
            return string.Join("\u0001", _socks.IsChecked == true ? "socks" : "tun", _tunAdapter.SelectedKey ?? "", _mtu.Value,
                _dnsSwitch != null && _dnsSwitch.IsChecked == true ? "1" : "0", _pqSwitch != null && _pqSwitch.IsChecked == true ? "1" : "0", _port.Value,
                _lan != null && _lan.IsChecked == true ? "1" : "0", _adapter.SelectedKey ?? "", _username.Value, _password.Value);
        }

        public override bool IsDirty { get { return Snapshot() != _initial; } }

        void Update()
        {
            if (_save == null) return;
            bool socks = _socks.IsChecked == true;
            _socksPanel.IsEnabled = socks;
            _socksPanel.Opacity = socks ? 1 : 0.5;
            _tunPanel.IsEnabled = !socks;
            _tunPanel.Opacity = socks ? 0.5 : 1;
            var tunKey = _tunAdapter.SelectedKey ?? "";
            if (tunKey.Length == 0) _tunInfo.Text = L.TunAdapterAutoHint;
            else _tunInfo.Text = _outbound.Any(a => a.Id == tunKey) ? L.TunAdapterHint : L.TunAdapterUnavailable;
            bool lan = _lan.IsChecked == true;
            _adapterPanel.Visibility = lan ? Visibility.Visible : Visibility.Collapsed;
            int port;
            var portText = int.TryParse(_port.Value.Trim(), out port) ? port.ToString() : "1080";
            if (lan)
            {
                string text;
                var key = _adapter.SelectedKey ?? "";
                if (key.Length == 0)
                {
                    var ips = _adapters.Where(a => !a.IsVirtual).Select(a => a.Ip + ":" + portText).ToList();
                    text = string.Format(L.ProxyBannerLan, ips.Count > 0 ? string.Join(", ", ips) : "0.0.0.0:" + portText) + "\n" + L.SocksAdapterAllHint;
                }
                else
                {
                    var adapter = _adapters.FirstOrDefault(a => a.Id == key);
                    text = adapter != null
                        ? string.Format(L.ProxyBannerLan, adapter.Ip + ":" + portText) + "\n" + L.SocksAdapterHint
                        : L.AdapterUnavailable;
                }
                if (_username.Value.Trim().Length == 0) text += "\n" + L.SocksLanNoAuth;
                _lanInfo.Text = text;
                _lanInfo.Visibility = Visibility.Visible;
            }
            else
            {
                _lanInfo.Visibility = Visibility.Collapsed;
            }
            _save.IsEnabled = IsDirty;
        }

        void Save()
        {
            bool socks = _socks.IsChecked == true;
            int port = 0;
            bool portOk = int.TryParse(_port.Value.Trim(), out port) && port > 0 && port < 65536;
            _port.Error = !socks || portOk ? null : L.PortWrong;
            var user = _username.Value.Trim();
            var pass = _password.Value;
            string pairError = (user.Length == 0) != (pass.Length == 0) ? L.SocksAuthPair : null;
            _username.Error = socks && user.Length == 0 ? pairError : null;
            _password.Error = socks && pass.Length == 0 ? pairError : null;
            int mtu;
            bool mtuOk = int.TryParse(_mtu.Value.Trim(), out mtu) && mtu >= Defaults.MinMtu && mtu <= Defaults.MaxMtu;
            _mtu.Error = mtuOk ? null : string.Format(L.MtuWrong, Defaults.MinMtu, Defaults.MaxMtu);
            if (!mtuOk || (socks && (!portOk || pairError != null))) return;

            var s = App.I.Store.Data.Settings;
            s.ListenerMode = socks ? "socks" : "tun";
            s.Mtu = mtu;
            s.ChangeSystemDns = _dnsSwitch.IsChecked == true;
            s.PostQuantum = _pqSwitch.IsChecked == true;
            var tunId = _tunAdapter.SelectedKey ?? "";
            var tunAdapter = _outbound.FirstOrDefault(a => a.Id == tunId);
            if (tunId != s.TunAdapterId || tunAdapter != null) s.TunAdapterName = tunAdapter != null ? tunAdapter.Name : "";
            s.TunAdapterId = tunId;
            if (portOk) s.SocksPort = port;
            s.SocksAllowLan = _lan.IsChecked == true;
            var adapterId = _adapter.SelectedKey ?? "";
            var adapter = _adapters.FirstOrDefault(a => a.Id == adapterId);
            if (adapterId != s.SocksAdapterId || adapter != null) s.SocksAdapterName = adapter != null ? adapter.Name : "";
            s.SocksAdapterId = adapterId;
            s.SocksUsername = user;
            s.SocksPassword = pass;
            App.I.Store.Save();
            _initial = Snapshot();
            Update();
            Main.ShowSnack(L.ChangesSaved);
            App.I.ReconnectIfActive(delegate { return true; });
        }
    }

    class NetAdapter
    {
        public string Id;
        public string Name;
        public string Description;
        public string Ip;
        public bool IsVirtual;
        public int Index;        // IPv4 interface index, as in "route print"
        public bool HasGateway;  // can reach the internet on its own

        public string Label { get { return Name + " — " + Ip; } }
    }

    static class Net
    {
        // Active adapters with an IPv4 address that can accept LAN connections (no loopback, tunnels or Wintun).
        public static List<NetAdapter> Adapters()
        {
            var result = new List<NetAdapter>();
            try
            {
                foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;
                    if (nic.Description.IndexOf("wintun", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    var ip = nic.GetIPProperties().UnicastAddresses
                        .Select(a => a.Address)
                        .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !a.ToString().StartsWith("169.254."));
                    if (ip == null) continue;
                    var props = nic.GetIPProperties();
                    int index = 0;
                    try { var v4 = props.GetIPv4Properties(); if (v4 != null) index = v4.Index; } catch { }
                    result.Add(new NetAdapter
                    {
                        Id = nic.Id,
                        Name = nic.Name,
                        Description = nic.Description,
                        Ip = ip.ToString(),
                        IsVirtual = nic.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    nic.Description.IndexOf("hyper-v", StringComparison.OrdinalIgnoreCase) >= 0,
                        Index = index,
                        HasGateway = props.GatewayAddresses.Any(g =>
                            g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !g.Address.Equals(System.Net.IPAddress.Any))
                    });
                }
            }
            catch { }
            return result;
        }

        // Adapters that can carry the tunnel to the server (have an IPv4 gateway and an interface index).
        public static List<NetAdapter> OutboundAdapters()
        {
            return Adapters().Where(a => a.HasGateway && a.Index > 0).ToList();
        }

        public static int? OutboundIndex(string id)
        {
            var adapter = OutboundAdapters().FirstOrDefault(a => a.Id == id);
            return adapter == null ? (int?)null : adapter.Index;
        }

        public static string AdapterIp(string id)
        {
            var adapter = Adapters().FirstOrDefault(a => a.Id == id);
            return adapter == null ? null : adapter.Ip;
        }

        // Addresses other devices can use to reach the proxy with the current settings.
        public static List<string> ProxyLanAddresses(AppSettings s)
        {
            if (string.IsNullOrEmpty(s.SocksAdapterId))
                return Adapters().Where(a => !a.IsVirtual).Select(a => a.Ip + ":" + s.SocksPort).ToList();
            var ip = AdapterIp(s.SocksAdapterId);
            return ip == null ? new List<string>() : new List<string> { ip + ":" + s.SocksPort };
        }
    }

    class ExcludedRoutesPage : Page
    {
        readonly TextBox _box;
        readonly TextBlock _error = UI.BodySmall("", C.NeutralDark);
        readonly Button _save;
        string _initial;

        public ExcludedRoutesPage(MainWindow main) : base(main)
        {
            SetAppBar(L.ExcludedRoutes, Icons.Back);
            var dock = new DockPanel { Margin = new Thickness(16) };
            _error.Text = L.ExcludedRoutesHint;
            _error.Margin = new Thickness(14, 4, 14, 0);
            DockPanel.SetDock(_error, Dock.Bottom);
            dock.Children.Add(_error);
            _box = new TextBox
            {
                Style = UI.S("TextBoxField"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalContentAlignment = VerticalAlignment.Top,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = Theme.Mono,
                FontSize = 13,
                Text = string.Join("\n", App.I.Store.Data.Settings.ExcludedRoutes)
            };
            var grid = new Grid();
            grid.Children.Add(_box);
            var hint = new TextBlock { Text = L.ExcludedRoutesHint, Foreground = C.NeutralLight, FontSize = 14, Margin = new Thickness(14, 10, 14, 10), IsHitTestVisible = false };
            grid.Children.Add(hint);
            dock.Children.Add(grid);
            SetBody(dock);

            _save = UI.Filled(L.Save, Save);
            _save.HorizontalAlignment = HorizontalAlignment.Right;
            SetBottom(_save);

            _initial = _box.Text;
            _box.TextChanged += delegate
            {
                hint.Visibility = _box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
                _save.IsEnabled = IsDirty;
            };
            hint.Visibility = _box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _save.IsEnabled = false;
        }

        public override bool IsDirty { get { return _box.Text != _initial; } }

        void Save()
        {
            var routes = Validation.Lines(_box.Text.Replace("\r", ""));
            var bad = routes.FirstOrDefault(r => !Validation.IsCidr(r));
            if (bad != null)
            {
                _box.Tag = "error";
                _error.Text = string.Format(L.ExcludedRoutesWrong, bad);
                _error.Foreground = C.Error;
                return;
            }
            _box.Tag = null;
            _error.Text = L.ExcludedRoutesHint;
            _error.Foreground = C.NeutralDark;
            App.I.Store.Data.Settings.ExcludedRoutes = routes.Distinct().ToList();
            App.I.Store.Save();
            _box.Text = string.Join("\n", App.I.Store.Data.Settings.ExcludedRoutes);
            _initial = _box.Text;
            _save.IsEnabled = false;
            Main.ShowSnack(L.ChangesSaved);
            App.I.ReconnectIfActive(delegate { return true; });
        }
    }

    class AboutPage : Page
    {
        readonly TextBlock _clientVersion = UI.BodyMedium("…", C.NeutralDark);

        public AboutPage(MainWindow main) : base(main)
        {
            SetAppBar(L.About, Icons.Back);
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24), MaxWidth = 460 };
            var logo = new Image
            {
                Source = App.LoadLargestIcon("app_icon.ico"),
                Width = 96,
                Height = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            sp.Children.Add(logo);
            var title = UI.Text(L.AppName, 24);
            title.TextAlignment = TextAlignment.Center;
            sp.Children.Add(title);
            var desc = UI.BodyMedium(L.AboutDescription, C.NeutralDark);
            desc.TextAlignment = TextAlignment.Center;
            desc.Margin = new Thickness(0, 6, 0, 20);
            sp.Children.Add(desc);

            sp.Children.Add(VersionRow(L.GuiVersion, UI.BodyMedium(App.Version + (App.IsPortable ? " (portable)" : ""), C.NeutralDark)));
            sp.Children.Add(VersionRow(L.ClientVersion, _clientVersion));

            var links = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) };
            var update = UI.Filled(L.CheckForUpdates, delegate { App.I.CheckForUpdates(true); });
            update.HorizontalAlignment = HorizontalAlignment.Center;
            update.Margin = new Thickness(0, 20, 0, 0);
            sp.Children.Add(update);
            links.Children.Add(UI.TextButton("TrustTunnel GUI", delegate { App.OpenUrl(UpdateChecker.RepoUrl); }));
            links.Children.Add(UI.TextButton("TrustTunnel Client", delegate { App.OpenUrl("https://github.com/TrustTunnel/TrustTunnelClient"); }));
            links.Children.Add(UI.TextButton("TrustTunnel", delegate { App.OpenUrl("https://github.com/TrustTunnel/TrustTunnel"); }));
            sp.Children.Add(links);
            SetBody(UI.Scroll(sp));

            App.I.GetClientVersion(delegate(string v) { _clientVersion.Text = v; });
        }

        static FrameworkElement VersionRow(string label, TextBlock value)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4), Width = 320 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(UI.BodyMedium(label));
            value.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(value, 1);
            g.Children.Add(value);
            return g;
        }
    }
}
