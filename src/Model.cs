using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace TrustTunnelGui
{
    public class ServerInfo
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "";
        public string Address = "";
        public string Domain = "";
        public string CustomSni = "";
        public string Username = "";
        // Stored encrypted with DPAPI (current Windows user); see Password property.
        public string PasswordProtected = "";
        public string Protocol = "http2"; // http2 | http3
        public string RoutingProfileId = RoutingProfile.DefaultId;
        public List<string> DnsServers = new List<string>();
        public string ClientRandom = "";
        public string Certificate = "";
        public string CertificateName = "";
        public bool Ipv6 = true;

        [ScriptIgnore]
        public string Password
        {
            get { return Secret.Unprotect(PasswordProtected); }
            set { PasswordProtected = Secret.Protect(value ?? ""); }
        }

        public ServerInfo Clone()
        {
            var s = (ServerInfo)MemberwiseClone();
            s.DnsServers = new List<string>(DnsServers);
            return s;
        }
    }

    public class RoutingProfile
    {
        public const string DefaultId = "default";

        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "";
        public string DefaultMode = "vpn"; // vpn | bypass
        public List<string> VpnRules = new List<string>();
        public List<string> BypassRules = new List<string>();

        [ScriptIgnore]
        public bool IsDefault { get { return Id == DefaultId; } }

        // The default profile keeps an empty name and follows the UI language until the user renames it.
        [ScriptIgnore]
        public string DisplayName { get { return IsDefault && string.IsNullOrEmpty(Name) ? L.DefaultProfile : Name; } }
    }

    public class AppSettings
    {
        public string LogLevel = "info"; // info | debug
        public bool OpenMainWindowOnLaunch = true;
        public bool AutoConnectOnLaunch = false;
        public bool KillSwitch = true;
        public string SelectedServerId = "";
        public bool TrayHintShown = false;
        public bool LegacyImportDone = false;
        public string Theme = "system";    // system | light | dark
        public string Language = "system"; // system | ru | en
        public bool RunAsAdmin = false;     // relaunch elevated at start
        public string ListenerMode = "tun"; // tun | socks
        public int SocksPort = 1080;
        public bool SocksAllowLan = false;
        public string SocksUsername = "";
        public string SocksPasswordProtected = "";

        [ScriptIgnore]
        public string SocksPassword
        {
            get { return Secret.Unprotect(SocksPasswordProtected); }
            set { SocksPasswordProtected = Secret.Protect(value ?? ""); }
        }

        [ScriptIgnore]
        public bool IsSocks { get { return ListenerMode == "socks"; } }

        [ScriptIgnore]
        public string SocksAddress
        {
            get
            {
                if (!SocksAllowLan) return "127.0.0.1:" + SocksPort;
                if (string.IsNullOrEmpty(SocksAdapterId)) return "0.0.0.0:" + SocksPort;
                // A chosen adapter that is gone must not silently widen access to every interface.
                return (Net.AdapterIp(SocksAdapterId) ?? "127.0.0.1") + ":" + SocksPort;
            }
        }

        // LAN access bound to one adapter (empty = all adapters). The IP is resolved on every connection.
        public string SocksAdapterId = "";
        public string SocksAdapterName = "";

        [ScriptIgnore]
        public bool SocksAdapterMissing
        {
            get { return IsSocks && SocksAllowLan && !string.IsNullOrEmpty(SocksAdapterId) && Net.AdapterIp(SocksAdapterId) == null; }
        }
        public List<string> ExcludedRoutes = new List<string>(Defaults.ExcludedRoutes);
    }

    public class AppData
    {
        public List<ServerInfo> Servers = new List<ServerInfo>();
        public List<RoutingProfile> Profiles = new List<RoutingProfile>();
        public AppSettings Settings = new AppSettings();
    }

    static class Defaults
    {
        // Same defaults as the Flutter client's AppDatabase.
        public static readonly string[] ExcludedRoutes =
        {
            "10.0.0.0/8", "100.64.0.0/10", "169.254.0.0/16", "172.16.0.0/12",
            "192.0.0.0/24", "192.168.0.0/16", "255.255.255.255/32"
        };
        public static readonly string[] IncludedRoutes = { "0.0.0.0/0", "2000::/3" };
        public const int Port = 443;
        public const int Mtu = 1500;
    }

    static class Secret
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TrustTunnelGui");

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        public static string Unprotect(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return "";
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return "";
            }
        }
    }

    // Persists AppData as JSON in %APPDATA%\TrustTunnelGUI.
    class Store
    {
        public AppData Data { get; private set; }
        public event Action Changed;

        readonly string _file;

        public Store(string file)
        {
            _file = file;
            Load();
        }

        void Load()
        {
            AppData data = null;
            try
            {
                if (File.Exists(_file))
                    data = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Deserialize<AppData>(File.ReadAllText(_file, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Log.Write("Failed to read settings: " + ex.Message);
                try { File.Copy(_file, _file + ".broken", true); } catch { }
            }
            Data = data ?? new AppData();
            if (Data.Settings == null) Data.Settings = new AppSettings();
            if (Data.Settings.ExcludedRoutes == null) Data.Settings.ExcludedRoutes = new List<string>(Defaults.ExcludedRoutes);
            if (Data.Servers == null) Data.Servers = new List<ServerInfo>();
            if (Data.Profiles == null) Data.Profiles = new List<RoutingProfile>();
            if (!Data.Profiles.Any(p => p.IsDefault))
                Data.Profiles.Insert(0, new RoutingProfile { Id = RoutingProfile.DefaultId, Name = "" });
            foreach (var p in Data.Profiles)
            {
                if (p.IsDefault && (p.Name == "Профиль по умолчанию" || p.Name == "Default profile")) p.Name = "";
                if (p.Name == null) p.Name = "";
                if (p.VpnRules == null) p.VpnRules = new List<string>();
                if (p.BypassRules == null) p.BypassRules = new List<string>();
            }
            foreach (var s in Data.Servers)
            {
                if (s.DnsServers == null) s.DnsServers = new List<string>();
                if (Profile(s.RoutingProfileId) == null) s.RoutingProfileId = RoutingProfile.DefaultId;
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file));
                var json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(Data);
                var tmp = _file + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                if (File.Exists(_file)) File.Replace(tmp, _file, null);
                else File.Move(tmp, _file);
            }
            catch (Exception ex)
            {
                Log.Write("Failed to save settings: " + ex.Message);
            }
            if (Changed != null) Changed();
        }

        public ServerInfo Server(string id)
        {
            return Data.Servers.FirstOrDefault(s => s.Id == id);
        }

        public RoutingProfile Profile(string id)
        {
            return Data.Profiles.FirstOrDefault(p => p.Id == id);
        }
    }

    // Builds trusttunnel_client.toml the same way the Flutter vpn_plugin ConfigurationEncoder does.
    static class ConfigBuilder
    {
        public static string Build(ServerInfo server, RoutingProfile profile, AppSettings settings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Generated by TrustTunnel GUI. Do not edit: the file is rewritten on every connection.");
            Kv(sb, "loglevel", Str(settings.LogLevel == "debug" ? "debug" : "info"));
            Kv(sb, "vpn_mode", Str(profile.DefaultMode == "bypass" ? "selective" : "general"));
            Kv(sb, "killswitch_enabled", settings.KillSwitch ? "true" : "false");
            Kv(sb, "post_quantum_group_enabled", "true");
            Kv(sb, "exclusions", List(Exclusions(profile)));
            sb.AppendLine();

            sb.AppendLine("[endpoint]");
            Kv(sb, "hostname", Str(server.Domain.Trim()));
            Kv(sb, "addresses", List(new[] { NormalizeAddress(server.Address.Trim()) }));
            Kv(sb, "custom_sni", Str(server.CustomSni.Trim()));
            Kv(sb, "has_ipv6", server.Ipv6 ? "true" : "false");
            Kv(sb, "username", Str(server.Username));
            Kv(sb, "password", Str(server.Password));
            Kv(sb, "client_random", Str(server.ClientRandom.Trim()));
            Kv(sb, "skip_verification", "false");
            Kv(sb, "certificate", Pem(server.Certificate));
            Kv(sb, "upstream_protocol", Str(server.Protocol == "http3" ? "http3" : "http2"));
            Kv(sb, "anti_dpi", "false");
            Kv(sb, "dns_upstreams", List(server.DnsServers));
            sb.AppendLine();

            // Only one listener type may be present in the config.
            sb.AppendLine("[listener]");
            sb.AppendLine();
            if (settings.IsSocks)
            {
                sb.AppendLine("[listener.socks]");
                Kv(sb, "address", Str(settings.SocksAddress));
                Kv(sb, "username", Str(settings.SocksUsername));
                Kv(sb, "password", Str(settings.SocksPassword));
                return sb.ToString();
            }
            sb.AppendLine("[listener.tun]");
            Kv(sb, "bound_if", Str(""));
            Kv(sb, "included_routes", List(Defaults.IncludedRoutes));
            Kv(sb, "excluded_routes", List(settings.ExcludedRoutes));
            Kv(sb, "mtu_size", Defaults.Mtu.ToString(CultureInfo.InvariantCulture));
            Kv(sb, "change_system_dns", "true");
            return sb.ToString();
        }

        // Rules of the non-default list become exclusions; domains also get a "*." twin (Flutter _getExclusionsByMode).
        public static List<string> Exclusions(RoutingProfile profile)
        {
            var rules = profile.DefaultMode == "bypass" ? profile.VpnRules : profile.BypassRules;
            var addresses = new List<string>();
            var domains = new List<string>();
            foreach (var raw in rules)
            {
                var rule = raw.Trim();
                if (rule.Length == 0) continue;
                if (Validation.IsDomain(rule, true))
                {
                    var d = rule.ToLowerInvariant();
                    if (!domains.Contains(d)) domains.Add(d);
                    if (!d.StartsWith("*.") && !domains.Contains("*." + d)) domains.Add("*." + d);
                }
                else if (!addresses.Contains(rule))
                {
                    addresses.Add(rule);
                }
            }
            addresses.AddRange(domains);
            return addresses;
        }

        public static string NormalizeAddress(string address)
        {
            bool ipv6Literal = address.Count(c => c == ':') > 1;
            if (ipv6Literal)
            {
                if (address.StartsWith("[") && address.Contains("]:")) return address;
                var host = address.StartsWith("[") ? address : "[" + address + "]";
                return host + ":" + Defaults.Port;
            }
            return address.Contains(":") ? address : address + ":" + Defaults.Port;
        }

        static void Kv(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append(" = ").AppendLine(value);
        }

        public static string Str(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var ch in s ?? "")
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("X4"));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        static string List(IEnumerable<string> items)
        {
            return "[" + string.Join(", ", items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => Str(i.Trim()))) + "]";
        }

        static string Pem(string pem)
        {
            if (string.IsNullOrWhiteSpace(pem)) return "\"\"";
            var normalized = pem.Replace("\r\n", "\n").Trim();
            // PEM never contains quotes or backslashes, so a multi-line basic string is safe.
            return "\"\"\"\n" + normalized + "\n\"\"\"";
        }
    }

    // Minimal TOML reader: enough for configs produced by setup_wizard / trusttunnel_endpoint.
    class TomlDocument
    {
        public readonly Dictionary<string, Dictionary<string, object>> Tables = new Dictionary<string, Dictionary<string, object>>();

        public static TomlDocument Parse(string text)
        {
            var doc = new TomlDocument();
            var table = doc.Table("");
            int i = 0;
            text = text.Replace("\r\n", "\n");
            while (i < text.Length)
            {
                SkipSpaceAndComments(text, ref i);
                if (i >= text.Length) break;
                if (text[i] == '[')
                {
                    int end = text.IndexOf('\n', i);
                    if (end < 0) end = text.Length;
                    var header = text.Substring(i, end - i);
                    var close = header.IndexOf(']');
                    var name = header.Substring(1, close > 0 ? close - 1 : header.Length - 1).Trim('[', ']', ' ');
                    table = doc.Table(name);
                    i = end;
                    continue;
                }
                int eq = text.IndexOf('=', i);
                int nl = text.IndexOf('\n', i);
                if (eq < 0 || (nl >= 0 && nl < eq)) { i = nl < 0 ? text.Length : nl + 1; continue; }
                var key = text.Substring(i, eq - i).Trim().Trim('"');
                i = eq + 1;
                SkipInlineSpace(text, ref i);
                table[key] = ParseValue(text, ref i);
                int lineEnd = text.IndexOf('\n', i);
                i = lineEnd < 0 ? text.Length : lineEnd + 1;
            }
            return doc;
        }

        public Dictionary<string, object> Table(string name)
        {
            Dictionary<string, object> t;
            if (!Tables.TryGetValue(name, out t))
            {
                t = new Dictionary<string, object>();
                Tables[name] = t;
            }
            return t;
        }

        public string GetString(string table, string key, string fallback = "")
        {
            object v;
            if (Table(table).TryGetValue(key, out v) && v != null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            return fallback;
        }

        public bool GetBool(string table, string key, bool fallback)
        {
            object v;
            if (Table(table).TryGetValue(key, out v) && v is bool) return (bool)v;
            return fallback;
        }

        public List<string> GetList(string table, string key)
        {
            object v;
            var list = new List<string>();
            if (Table(table).TryGetValue(key, out v))
            {
                var items = v as List<object>;
                if (items != null) list.AddRange(items.Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)));
                else if (v is string && ((string)v).Length > 0) list.Add((string)v);
            }
            return list;
        }

        static void SkipInlineSpace(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        }

        static void SkipSpaceAndComments(string s, ref int i)
        {
            while (i < s.Length)
            {
                if (char.IsWhiteSpace(s[i])) { i++; continue; }
                if (s[i] == '#')
                {
                    int nl = s.IndexOf('\n', i);
                    i = nl < 0 ? s.Length : nl + 1;
                    continue;
                }
                break;
            }
        }

        static object ParseValue(string s, ref int i)
        {
            if (i >= s.Length) return "";
            if (s.Substring(i).StartsWith("\"\"\"")) return Multi(s, ref i, "\"\"\"", true);
            if (s.Substring(i).StartsWith("'''")) return Multi(s, ref i, "'''", false);
            char c = s[i];
            if (c == '"') return Basic(s, ref i);
            if (c == '\'')
            {
                int end = s.IndexOf('\'', i + 1);
                if (end < 0) end = s.Length;
                var lit = s.Substring(i + 1, end - i - 1);
                i = Math.Min(end + 1, s.Length);
                return lit;
            }
            if (c == '[')
            {
                var list = new List<object>();
                i++;
                while (i < s.Length)
                {
                    SkipSpaceAndComments(s, ref i);
                    if (i >= s.Length) break;
                    if (s[i] == ']') { i++; break; }
                    if (s[i] == ',') { i++; continue; }
                    list.Add(ParseValue(s, ref i));
                }
                return list;
            }
            int start = i;
            while (i < s.Length && s[i] != ',' && s[i] != ']' && s[i] != '\n' && s[i] != '#') i++;
            var token = s.Substring(start, i - start).Trim();
            if (token == "true") return true;
            if (token == "false") return false;
            long n;
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return token;
        }

        static string Multi(string s, ref int i, string delim, bool escapes)
        {
            i += 3;
            if (i < s.Length && s[i] == '\n') i++;
            int end = s.IndexOf(delim, i, StringComparison.Ordinal);
            if (end < 0) end = s.Length;
            var raw = s.Substring(i, end - i);
            i = Math.Min(end + 3, s.Length);
            return escapes ? Unescape(raw) : raw;
        }

        static string Basic(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++;
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    sb.Append(s[i]).Append(s[i + 1]);
                    i += 2;
                    continue;
                }
                sb.Append(s[i]);
                i++;
            }
            i++;
            return Unescape(sb.ToString());
        }

        static string Unescape(string raw)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c != '\\' || i + 1 >= raw.Length) { sb.Append(c); continue; }
                char n = raw[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'u':
                        if (i + 4 < raw.Length)
                        {
                            sb.Append((char)Convert.ToInt32(raw.Substring(i + 1, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }
    }

    static class ServerImport
    {
        // Converts a client config (trusttunnel_client.toml) to a server entry.
        public static ServerInfo FromClientConfig(string toml)
        {
            var doc = TomlDocument.Parse(toml);
            var addresses = doc.GetList("endpoint", "addresses");
            var hostname = doc.GetString("endpoint", "hostname");
            if (addresses.Count == 0 && hostname.Length == 0) return null;
            var server = new ServerInfo
            {
                Name = hostname.Length > 0 ? hostname : addresses[0],
                Address = addresses.Count > 0 ? addresses[0] : hostname,
                Domain = hostname,
                CustomSni = doc.GetString("endpoint", "custom_sni"),
                Username = doc.GetString("endpoint", "username"),
                Protocol = doc.GetString("endpoint", "upstream_protocol") == "http3" ? "http3" : "http2",
                DnsServers = doc.GetList("endpoint", "dns_upstreams"),
                ClientRandom = doc.GetString("endpoint", "client_random"),
                Certificate = doc.GetString("endpoint", "certificate").Trim(),
                Ipv6 = doc.GetBool("endpoint", "has_ipv6", true)
            };
            server.Password = doc.GetString("endpoint", "password");
            if (server.Address.EndsWith(":" + Defaults.Port)) server.Address = server.Address.Substring(0, server.Address.Length - 4);
            if (server.Certificate.Length > 0) server.CertificateName = "certificate.pem";
            return server;
        }
    }

    static class Validation
    {
        static readonly Regex DomainRx = new Regex(@"^(?=.{1,253}$)(\*\.)?([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)*[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.IgnoreCase);
        static readonly Regex HexRx = new Regex("^[0-9a-fA-F]*$");

        public static bool IsIp(string s)
        {
            IPAddress ip;
            return IPAddress.TryParse(s.Trim('[', ']'), out ip);
        }

        public static bool IsDomain(string s, bool allowFirstLevel)
        {
            if (IsIp(s)) return false;
            if (!DomainRx.IsMatch(s)) return false;
            if (!allowFirstLevel && !s.Contains(".")) return false;
            // A plain number list like "1.2.3" is not a domain.
            return s.Any(char.IsLetter);
        }

        public static bool IsHostOrIp(string s)
        {
            return IsIp(s) || IsDomain(s, true);
        }

        // host, host:port, ipv4, ipv4:port, ipv6, [ipv6]:port
        public static bool IsAddress(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return false;
            if (s.StartsWith("["))
            {
                int close = s.IndexOf(']');
                if (close < 0) return false;
                var ip = s.Substring(1, close - 1);
                var rest = s.Substring(close + 1);
                if (!IsIp(ip)) return false;
                return rest.Length == 0 || (rest.StartsWith(":") && IsPort(rest.Substring(1)));
            }
            if (s.Count(c => c == ':') > 1) return IsIp(s);
            var parts = s.Split(':');
            if (parts.Length == 2 && !IsPort(parts[1])) return false;
            return IsHostOrIp(parts[0]);
        }

        static bool IsPort(string s)
        {
            int p;
            return int.TryParse(s, out p) && p > 0 && p < 65536;
        }

        public static bool IsDnsUpstream(string s)
        {
            s = s.Trim();
            var schemes = new[] { "tls://", "https://", "quic://", "h3://", "sdns://", "tcp://", "udp://" };
            foreach (var sc in schemes)
                if (s.StartsWith(sc, StringComparison.OrdinalIgnoreCase)) return s.Length > sc.Length;
            return IsAddress(s) && !IsDomain(s.Split(':')[0], true);
        }

        public static bool IsCidr(string s)
        {
            var parts = s.Trim().Split('/');
            if (parts.Length != 2) return false;
            IPAddress ip;
            int mask;
            if (!IPAddress.TryParse(parts[0], out ip) || !int.TryParse(parts[1], out mask)) return false;
            int max = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
            return mask >= 0 && mask <= max;
        }

        // Routing rule: domain, IP, IP:port, CIDR.
        public static bool IsRule(string s)
        {
            s = s.Trim();
            return IsDomain(s, true) || IsIp(s) || IsCidr(s) || IsAddress(s);
        }

        // Returns null when valid, otherwise an error message (Flutter ValidationUtils.validateTlsPrefix).
        public static string ClientRandomError(string s)
        {
            s = s.Trim();
            if (s.Length == 0) return null;
            if (s.Length > 64) return L.TooLong;
            var parts = s.Split('/');
            if (parts.Length > 2) return L.TlsWrongField;
            foreach (var p in parts)
            {
                if (!HexRx.IsMatch(p)) return L.TlsWrongField;
                if (p.Length % 2 != 0) return parts.Length == 2 ? L.TlsOutOfBounds : L.TlsWrongValue;
            }
            if (parts.Length == 2 && parts[0].Length != parts[1].Length) return L.TlsOutOfBounds;
            return null;
        }

        public static List<string> Lines(string text)
        {
            return (text ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        }
    }

    static class Log
    {
        public static string File;

        public static void Write(string message)
        {
            try
            {
                if (File == null) return;
                System.IO.File.AppendAllText(File, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + message + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}
