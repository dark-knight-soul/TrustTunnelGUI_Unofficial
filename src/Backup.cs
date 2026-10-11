using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace TrustTunnelGui
{
    // Export/import of servers, routing profiles and excluded routes to a portable JSON file.
    // Passwords are DPAPI-bound to this PC, so they are only exported when the user sets a backup password:
    // then they are encrypted with AES-256-CBC (key from PBKDF2-SHA256) and authenticated with HMAC-SHA256.
    class BackupFile
    {
        public const string FormatName = "trusttunnel-gui-backup";

        public string Format = FormatName;
        public int Version = 1;
        public string App = "";
        public string Created = "";
        public List<ServerInfo> Servers = new List<ServerInfo>();
        public List<RoutingProfile> Profiles = new List<RoutingProfile>();
        public List<string> ExcludedRoutes = new List<string>();
        public BackupSecrets Secrets;
    }

    class BackupSecrets
    {
        public int Iterations;
        public string Salt = "";
        public string Iv = "";
        public string Data = "";
        public string Mac = "";
    }

    class BackupException : Exception
    {
        public BackupException(string message) : base(message) { }
    }

    static class Backup
    {
        const int Iterations = 200000;

        static JavaScriptSerializer Json()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        public static void Export(AppData data, string path, string password)
        {
            var file = new BackupFile
            {
                App = TrustTunnelGui.App.Version,
                Created = DateTime.UtcNow.ToString("o"),
                Profiles = data.Profiles,
                ExcludedRoutes = data.Settings.ExcludedRoutes
            };
            var secrets = new Dictionary<string, string>();
            foreach (var s in data.Servers)
            {
                var copy = s.Clone();
                copy.PasswordProtected = "";
                file.Servers.Add(copy);
                var pass = s.Password;
                if (pass.Length > 0) secrets[s.Id] = pass;
            }
            if (!string.IsNullOrEmpty(password) && secrets.Count > 0)
                file.Secrets = Encrypt(Json().Serialize(secrets), password);
            File.WriteAllText(path, Json().Serialize(file), new UTF8Encoding(false));
        }

        public static BackupFile Read(string path)
        {
            BackupFile file;
            try
            {
                file = Json().Deserialize<BackupFile>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                throw new BackupException(L.BackupInvalid);
            }
            if (file == null || file.Format != BackupFile.FormatName) throw new BackupException(L.BackupInvalid);
            if (file.Servers == null) file.Servers = new List<ServerInfo>();
            if (file.Profiles == null) file.Profiles = new List<RoutingProfile>();
            if (file.ExcludedRoutes == null) file.ExcludedRoutes = new List<string>();
            return file;
        }

        // Returns server id -> password; throws BackupException on a wrong password.
        public static Dictionary<string, string> Passwords(BackupFile file, string password)
        {
            if (file.Secrets == null || string.IsNullOrEmpty(password)) return new Dictionary<string, string>();
            var json = Decrypt(file.Secrets, password);
            return Json().Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        // replace = true: the backup replaces servers, profiles and excluded routes; otherwise it is merged in.
        public static int Apply(AppData data, BackupFile file, Dictionary<string, string> passwords, bool replace)
        {
            var profileIds = new Dictionary<string, string>();
            if (replace)
            {
                data.Profiles.Clear();
                data.Servers.Clear();
                data.Settings.ExcludedRoutes = new List<string>(file.ExcludedRoutes);
                data.Settings.SelectedServerId = "";
            }
            else
            {
                data.Settings.ExcludedRoutes = data.Settings.ExcludedRoutes.Union(file.ExcludedRoutes).ToList();
            }

            foreach (var p in file.Profiles)
            {
                if (p.VpnRules == null) p.VpnRules = new List<string>();
                if (p.BypassRules == null) p.BypassRules = new List<string>();
                var existingDefault = data.Profiles.FirstOrDefault(x => x.IsDefault);
                if (p.IsDefault && existingDefault != null)
                {
                    // Merge into the local default profile.
                    existingDefault.VpnRules = existingDefault.VpnRules.Union(p.VpnRules).ToList();
                    existingDefault.BypassRules = existingDefault.BypassRules.Union(p.BypassRules).ToList();
                    profileIds[p.Id] = existingDefault.Id;
                    continue;
                }
                var copy = new RoutingProfile
                {
                    Id = p.IsDefault ? RoutingProfile.DefaultId : Guid.NewGuid().ToString("N"),
                    Name = p.IsDefault ? p.Name : UniqueName(p.Name, data.Profiles.Select(x => x.DisplayName)),
                    DefaultMode = p.DefaultMode == "bypass" ? "bypass" : "vpn",
                    VpnRules = new List<string>(p.VpnRules),
                    BypassRules = new List<string>(p.BypassRules)
                };
                if (copy.IsDefault) data.Profiles.Insert(0, copy); else data.Profiles.Add(copy);
                profileIds[p.Id] = copy.Id;
            }
            if (!data.Profiles.Any(x => x.IsDefault))
                data.Profiles.Insert(0, new RoutingProfile { Id = RoutingProfile.DefaultId, Name = "" });

            int count = 0;
            foreach (var s in file.Servers)
            {
                var copy = s.Clone();
                var oldId = s.Id;
                copy.Id = Guid.NewGuid().ToString("N");
                copy.Name = UniqueName(s.Name, data.Servers.Select(x => x.Name));
                if (copy.DnsServers == null) copy.DnsServers = new List<string>();
                string profileId;
                copy.RoutingProfileId = profileIds.TryGetValue(s.RoutingProfileId ?? "", out profileId) ? profileId : RoutingProfile.DefaultId;
                string pass;
                copy.Password = passwords.TryGetValue(oldId, out pass) ? pass : "";
                data.Servers.Add(copy);
                count++;
            }
            return count;
        }

        static string UniqueName(string name, IEnumerable<string> existing)
        {
            var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(name)) return name;
            for (int i = 2; ; i++)
            {
                var candidate = name + " (" + i + ")";
                if (!taken.Contains(candidate)) return candidate;
            }
        }

        static void DeriveKeys(string password, byte[] salt, int iterations, out byte[] encKey, out byte[] macKey)
        {
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256))
            {
                var bytes = kdf.GetBytes(64);
                encKey = bytes.Take(32).ToArray();
                macKey = bytes.Skip(32).ToArray();
            }
        }

        static BackupSecrets Encrypt(string plain, string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] encKey, macKey;
            DeriveKeys(password, salt, Iterations, out encKey, out macKey);
            using (var aes = Aes.Create())
            {
                aes.Key = encKey;
                aes.GenerateIV();
                byte[] cipher;
                using (var enc = aes.CreateEncryptor())
                {
                    var data = Encoding.UTF8.GetBytes(plain);
                    cipher = enc.TransformFinalBlock(data, 0, data.Length);
                }
                byte[] mac;
                using (var hmac = new HMACSHA256(macKey)) mac = hmac.ComputeHash(aes.IV.Concat(cipher).ToArray());
                return new BackupSecrets
                {
                    Iterations = Iterations,
                    Salt = Convert.ToBase64String(salt),
                    Iv = Convert.ToBase64String(aes.IV),
                    Data = Convert.ToBase64String(cipher),
                    Mac = Convert.ToBase64String(mac)
                };
            }
        }

        static string Decrypt(BackupSecrets secrets, string password)
        {
            byte[] salt, iv, cipher, mac;
            try
            {
                salt = Convert.FromBase64String(secrets.Salt);
                iv = Convert.FromBase64String(secrets.Iv);
                cipher = Convert.FromBase64String(secrets.Data);
                mac = Convert.FromBase64String(secrets.Mac);
            }
            catch (FormatException)
            {
                throw new BackupException(L.BackupInvalid);
            }
            byte[] encKey, macKey;
            DeriveKeys(password, salt, secrets.Iterations > 0 ? secrets.Iterations : Iterations, out encKey, out macKey);
            byte[] expected;
            using (var hmac = new HMACSHA256(macKey)) expected = hmac.ComputeHash(iv.Concat(cipher).ToArray());
            if (!FixedTimeEquals(expected, mac)) throw new BackupException(L.BackupWrongPassword);
            using (var aes = Aes.Create())
            {
                aes.Key = encKey;
                aes.IV = iv;
                using (var dec = aes.CreateDecryptor())
                    return Encoding.UTF8.GetString(dec.TransformFinalBlock(cipher, 0, cipher.Length));
            }
        }

        static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
