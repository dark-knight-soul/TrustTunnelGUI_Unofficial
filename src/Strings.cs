using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace TrustTunnelGui
{
    // UI strings. Field initializers are Russian; English (intl_en.arb of the Flutter client) is applied on demand.
    static class L
    {
        public static string AppName = "TrustTunnel";

        public static string Servers = "Серверы";
        public static string Routing = "Маршрутизация";
        public static string Settings = "Настройки";

        public static string AddServer = "Добавить сервер";
        public static string EditServer = "Изменить сервер";
        public static string Add = "Добавить";
        public static string Save = "Сохранить";
        public static string Create = "Создать";
        public static string Cancel = "Отмена";
        public static string Delete = "Удалить";
        public static string Confirm = "Подтвердить";

        public static string ServerName = "Название сервера";
        public static string Address = "Адрес";
        public static string AddressHint = "IPv4/IPv6 или домен";
        public static string AddressHelper = "Если порт не указан, используется 443";
        public static string DomainLabel = "Доменное имя из сертификата сервера";
        public static string DomainHint = "Домен или IP-адрес";
        public static string CustomSni = "Пользовательский SNI";
        public static string CustomSniHint = "example.com";
        public static string Username = "Имя пользователя";
        public static string UsernameHint = "Введите имя пользователя";
        public static string Password = "Пароль";
        public static string PasswordHint = "Введите пароль";
        public static string Protocol = "Протокол";
        public static string Http2 = "HTTP/2";
        public static string Quic = "QUIC";
        public static string RoutingProfile = "Профиль маршрутизации";
        public static string DnsLabel = "Адреса DNS-серверов, по одному на строку";
        public static string DnsHint = "IP-адрес, tls://, https://, quic://, h3:// или sdns://";
        public static string DnsHelper = "Если не указано, используется 46.243.231.30 (AdGuard DNS без фильтрации)";
        public static string ClientRandom = "Client Random, hex-последовательность";
        public static string ClientRandomHint = "abcdef, 0a0b0c/0f0f0f";
        public static string ClientRandomHelper = "Максимальная длина — 64 символа";
        public static string PemLabel = "Самоподписанный сертификат, PEM-файл";
        public static string Import = "Импорт";
        public static string Remove = "Убрать";
        public static string Ipv6Label = "Разрешить IPv6-подключения через сервер";

        public static string ImportFromLink = "Импорт по ссылке";
        public static string ImportFromFile = "Импорт из файла";
        public static string ImportLinkTitle = "Импорт по ссылке tt://";
        public static string ImportLinkHint = "tt://...";
        public static string ImportFailed = "Не удалось импортировать конфигурацию";
        public static string Imported = "Данные сервера заполнены";
        public static string WizardNotFound = "setup_wizard.exe не найден";

        public static string ServersEmptyTitle = "Создайте первый сервер";
        public static string ServersEmptyDescription = "Добавьте сервер для безопасной и анонимной передачи данных";

        public static string FillField = "Заполните это поле";
        public static string NameUsed = "Это название уже используется";
        public static string AddressWrong = "Используйте формат x.x.x.x, x:x:x:x:x:x:x:x или домен";
        public static string DomainWrong = "Используйте доменное имя или IP-адрес";
        public static string RuleWrong = "Некорректные правила";
        public static string DnsWrong = "Используйте IP-адреса, tls://, https://, quic://, h3:// или sdns://";
        public static string SniWrong = "Используйте формат example.com";
        public static string TlsWrongField = "Hex-последовательность допускает только символы 0-9, a-f";
        public static string TlsWrongValue = "Значение должно содержать чётное число символов";
        public static string TlsOutOfBounds = "Маска и значение должны быть одинаковой длины";
        public static string TooLong = "Слишком длинное значение";

        public static string DeleteServer = "Удалить сервер";
        public static string DeleteServerTitle = "Удалить сервер?";
        public static string DeleteServerDescription = "Сервер {0} будет удалён";
        public static string ServerDeleted = "Сервер {0} удалён";
        public static string ServerCreated = "Сервер {0} создан";
        public static string ChangesSaved = "Изменения сохранены";

        public static string DefaultProfile = "Профиль по умолчанию";
        public static string ProfileName = "Название профиля";
        public static string EditProfileName = "Изменить название";
        public static string DeleteProfile = "Удалить профиль";
        public static string DeleteProfileTitle = "Удалить профиль?";
        public static string DeleteProfileDescription = "Профиль {0} будет удалён";
        public static string ProfileDeleted = "Профиль {0} удалён";
        public static string ProfileCreated = "Профиль {0} создан";
        public static string AddProfile = "Добавить профиль";
        public static string Bypass = "Обход";
        public static string Vpn = "VPN";
        public static string RulesHint = "Правила маршрутизации могут быть следующих типов:\n- Домены любого уровня\n- IP-адрес\n- IP-адрес с портом\n- IP-адрес с маской";
        public static string ChangeDefaultRouting = "Изменить маршрутизацию по умолчанию";
        public static string ChangeDefaultRoutingDescription = "Весь трафик по умолчанию будет маршрутизироваться по этому правилу";
        public static string DeleteAllRules = "Удалить все правила";
        public static string DeleteAllRulesTitle = "Удалить все правила?";
        public static string DeleteAllRulesDescription = "Все правила из профиля {0} будут удалены";
        public static string AllRulesDeleted = "Все правила удалены";
        public static string DefaultRoutingVpn = "По умолчанию весь трафик идёт через VPN, кроме правил из списка «Обход»";
        public static string DefaultRoutingBypass = "По умолчанию весь трафик идёт напрямую, кроме правил из списка «VPN»";
        public static string ProfileInUse = "Профиль используется серверами: {0}. Они будут переключены на профиль по умолчанию";

        public static string DiscardChangesTitle = "Отменить изменения?";
        public static string DiscardChangesDescription = "Вы уверены, что хотите отменить все изменения?";
        public static string DiscardChanges = "Отменить изменения";

        public static string ConnectionLog = "Журнал подключений";
        public static string ConnectionLogEmptyTitle = "Подключений пока нет";
        public static string ConnectionLogEmptyDescription = "Здесь отображается журнал VPN-клиента. Подключитесь к серверу, чтобы появились первые записи";
        public static string CopyLog = "Копировать";
        public static string ClearLog = "Очистить";
        public static string OpenLogFolder = "Открыть папку журналов";
        public static string LogCopied = "Журнал скопирован";

        public static string AppLogging = "Журналирование";
        public static string LoggingLevel = "Уровень журналирования";
        public static string LoggingDefault = "По умолчанию";
        public static string LoggingDefaultDescription = "Записываются основные события";
        public static string LoggingDebug = "Отладка";
        public static string LoggingDebugDescription = "Записываются дополнительные события. Используется для диагностики проблем";
        public static string LoggingAppliesOnReconnect = "Новый уровень применится при следующем подключении";

        public static string LaunchAndConnection = "Запуск и подключение";
        public static string Launch = "Запуск";
        public static string LaunchAtLogin = "Запускать приложение при входе в систему";
        public static string OpenMainWindow = "Автоматически открывать главное окно";
        public static string Connection = "Подключение";
        public static string AutoConnect = "Автоподключение при запуске приложения";
        public static string AutoConnectNoServers = "Нет добавленных серверов. Чтобы применить настройку, добавьте сервер";
        public static string KillSwitch = "Kill switch";
        public static string KillSwitchDescription = "Блокировать трафик, если соединение с сервером потеряно";
        public static string LaunchAtLoginFailed = "Не удалось изменить автозапуск";

        public static string ConnectionMode = "Режим подключения";
        public static string Mode = "Режим";
        public static string ModeTun = "VPN (TUN)";
        public static string ModeTunDescription = "Весь трафик системы идёт через сервер";
        public static string ModeSocks = "Локальный SOCKS5-прокси";
        public static string ModeSocksDescription = "Через сервер идёт трафик только тех приложений, в которых указан прокси";
        public static string SocksSection = "SOCKS5-прокси";
        public static string SocksPort = "Порт";
        public static string PortWrong = "Укажите порт от 1 до 65535";
        public static string SocksLan = "Доступ из локальной сети";
        public static string SocksLanDescription = "Другие устройства в вашей сети смогут использовать прокси";
        public static string SocksAuthHelper = "Необязательно. Если не указаны, прокси работает без аутентификации";
        public static string SocksAuthPair = "Укажите и имя пользователя, и пароль";
        public static string SocksLanNoAuth = "Без пароля прокси сможет использовать любое устройство в сети";
        public static string ProxyBanner = "Режим прокси: SOCKS5 {0}";
        public static string ProxyBannerLan = "В локальной сети: {0}";
        public static string AddressCopied = "Адрес скопирован";
        public static string CopyAddress = "Копировать адрес";

        public static string AdminRequired = "Нужны права администратора";
        public static string AdminRequiredDescription = "Режиму VPN (TUN) нужны права администратора, чтобы создать сетевой адаптер и изменить маршруты. Перезапустить приложение от имени администратора?\n\nРежим локального SOCKS5-прокси работает без них.";
        public static string Restart = "Перезапустить";
        public static string RunAsAdmin = "Всегда запускать от имени администратора";
        public static string RunAsAdminDescription = "Подключение в режиме VPN (TUN) будет работать без дополнительных запросов";
        public static string AutostartNeedsAdmin = "Автозапуск был включён от имени администратора. Отключить его можно только с правами администратора";
        public static string AdminSuffix = " — администратор";
        public static string AlreadyRunning = "TrustTunnel уже запущен от имени администратора. Откройте его через значок в области уведомлений";

        public static string Appearance = "Внешний вид";
        public static string Theme = "Тема";
        public static string ThemeSystem = "Как в системе";
        public static string ThemeLight = "Светлая";
        public static string ThemeDark = "Тёмная";
        public static string Language = "Язык";
        public static string LanguageSystem = "Как в системе";
        public static string LanguageRussian = "Русский";
        public static string LanguageEnglish = "English";

        public static string ExcludedRoutes = "Исключённые маршруты";
        public static string ExcludedRoutesHint = "Подсети в формате CIDR, по одной на строку";
        public static string ExcludedRoutesWrong = "Некорректная подсеть: {0}";
        public static string FollowOnGithub = "Мы на GitHub";
        public static string About = "О программе";
        public static string AboutDescription = "Графическая оболочка для консольного клиента TrustTunnel";
        public static string GuiVersion = "Версия GUI";
        public static string ClientVersion = "Версия клиента";

        public static string Connected = "Подключено";
        public static string Connecting = "Подключение…";
        public static string Disconnecting = "Отключение…";
        public static string Disconnected = "Отключено";
        public static string Connect = "Подключиться";
        public static string Disconnect = "Отключиться";
        public static string ConnectTo = "Подключиться к";
        public static string TrayStatus = "Статус: {0}";
        public static string TrayOpen = "Открыть TrustTunnel";
        public static string TrayQuit = "Выйти из TrustTunnel";
        public static string TrayHiddenTitle = "TrustTunnel работает в фоне";
        public static string TrayHiddenText = "Приложение свёрнуто в область уведомлений";

        public static string ExitTitle = "Выйти из TrustTunnel?";
        public static string ExitDescription = "Вы будете отключены от активного сервера";
        public static string Quit = "Выйти";
        public static string DontQuit = "Не выходить";

        public static string ClientNotFound = "Файл trusttunnel_client.exe не найден рядом с приложением";
        public static string ConnectFailed = "Не удалось подключиться";
        public static string ConnectionLost = "Клиент завершил работу";
        public static string SomethingWrong = "Что-то пошло не так. Повторите попытку позже";

        static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "Servers", "Servers" }, { "Routing", "Routing" }, { "Settings", "Settings" },
            { "AddServer", "Add server" }, { "EditServer", "Edit server" }, { "Add", "Add" }, { "Save", "Save" },
            { "Create", "Create" }, { "Cancel", "Cancel" }, { "Delete", "Delete" }, { "Confirm", "Confirm" },
            { "ServerName", "Server name" }, { "Address", "Address" }, { "AddressHint", "IPv4/IPv6 or domain" },
            { "AddressHelper", "If port is not specified, 443 is used" }, { "DomainLabel", "Domain name from server certificate" },
            { "DomainHint", "Domain or IP address" }, { "CustomSni", "Custom SNI" }, { "Username", "Username" },
            { "UsernameHint", "Enter username" }, { "Password", "Password" }, { "PasswordHint", "Enter password" },
            { "Protocol", "Protocol" }, { "RoutingProfile", "Routing profile" },
            { "DnsLabel", "DNS server addresses, one per line" }, { "DnsHint", "IP address, tls://, https://, quic://, h3://, or sdns://" },
            { "DnsHelper", "If not specified, 46.243.231.30 (AdGuard DNS Non-filtering) is used" },
            { "ClientRandom", "Client Random, hex sequence" }, { "ClientRandomHelper", "Max length — 64 characters" },
            { "PemLabel", "Self-signed certificate, PEM file" }, { "Import", "Import" }, { "Remove", "Remove" },
            { "Ipv6Label", "Allow IPv6 connections via the server" },
            { "ImportFromLink", "Import from link" }, { "ImportFromFile", "Import from file" }, { "ImportLinkTitle", "Import tt:// link" },
            { "ImportFailed", "Could not import the configuration" }, { "Imported", "Server details filled in" },
            { "WizardNotFound", "setup_wizard.exe not found" },
            { "ServersEmptyTitle", "Create your first server" }, { "ServersEmptyDescription", "Create a server for secure and anonymous data transfer" },
            { "FillField", "Please fill out this field" }, { "NameUsed", "This name is already used" },
            { "AddressWrong", "Use the format x.x.x.x, x:x:x:x:x:x:x:x or a domain" }, { "DomainWrong", "Use a domain name or IP address" },
            { "RuleWrong", "Invalid rules" },
            { "DnsWrong", "Use IP addresses, tls://, https://, quic://, h3:// or sdns://" }, { "SniWrong", "Use the format example.com" },
            { "TlsWrongField", "Hex sequence only allows 0-9, a-f characters" }, { "TlsWrongValue", "Value should have an even number of characters" },
            { "TlsOutOfBounds", "Mask and value should have the same length" }, { "TooLong", "The value is too long" },
            { "DeleteServer", "Delete server" }, { "DeleteServerTitle", "Delete server?" }, { "DeleteServerDescription", "Server {0} will be deleted" },
            { "ServerDeleted", "Server {0} deleted" }, { "ServerCreated", "Server {0} created" }, { "ChangesSaved", "Changes saved" },
            { "DefaultProfile", "Default profile" }, { "ProfileName", "Profile name" }, { "EditProfileName", "Edit name" },
            { "DeleteProfile", "Delete profile" }, { "DeleteProfileTitle", "Delete profile?" }, { "DeleteProfileDescription", "{0} will be deleted" },
            { "ProfileDeleted", "Profile {0} deleted" }, { "ProfileCreated", "Profile {0} created" }, { "AddProfile", "Add profile" },
            { "Bypass", "Bypass" }, { "Vpn", "VPN" },
            { "RulesHint", "Routing rules can be of the following types:\n- Domains of any level\n- IP address\n- IP address with port\n- IP address with a mask" },
            { "ChangeDefaultRouting", "Change default routing" }, { "ChangeDefaultRoutingDescription", "All traffic will be routed by default according to this rule" },
            { "DeleteAllRules", "Delete all rules" }, { "DeleteAllRulesTitle", "Delete all rules?" },
            { "DeleteAllRulesDescription", "All rules from {0} will be deleted" }, { "AllRulesDeleted", "All rules deleted" },
            { "DefaultRoutingVpn", "By default all traffic goes through VPN, except the rules in the Bypass list" },
            { "DefaultRoutingBypass", "By default all traffic goes directly, except the rules in the VPN list" },
            { "ProfileInUse", "The profile is used by: {0}. These servers will be switched to the default profile" },
            { "DiscardChangesTitle", "Discard changes?" }, { "DiscardChangesDescription", "Are you sure you want to discard all changes?" },
            { "DiscardChanges", "Discard changes" },
            { "ConnectionLog", "Connection log" }, { "ConnectionLogEmptyTitle", "No connections yet" },
            { "ConnectionLogEmptyDescription", "The VPN client log is shown here. Connect to your server to add the first records" },
            { "CopyLog", "Copy" }, { "ClearLog", "Clear" }, { "OpenLogFolder", "Open log folder" }, { "LogCopied", "Log copied" },
            { "AppLogging", "App logging" }, { "LoggingLevel", "Logging level" }, { "LoggingDefault", "Default" },
            { "LoggingDefaultDescription", "Logs the main events" }, { "LoggingDebug", "Debug" },
            { "LoggingDebugDescription", "Logs extra events. Used for diagnosing problems" },
            { "LoggingAppliesOnReconnect", "The new level applies on the next connection" },
            { "LaunchAndConnection", "Launch and connection" }, { "Launch", "Launch" }, { "LaunchAtLogin", "Launch app at system login" },
            { "OpenMainWindow", "Open Home screen automatically" }, { "Connection", "Connection" },
            { "AutoConnect", "Auto-connect on app launch" }, { "AutoConnectNoServers", "No servers added. To apply the setting, add a server" },
            { "KillSwitchDescription", "Block traffic if the connection to the server is lost" },
            { "LaunchAtLoginFailed", "Could not change the launch setting" },
            { "ConnectionMode", "Connection mode" }, { "Mode", "Mode" }, { "ModeTunDescription", "All system traffic goes through the server" },
            { "ModeSocks", "Local SOCKS5 proxy" }, { "ModeSocksDescription", "Only apps configured to use the proxy send traffic through the server" },
            { "SocksSection", "SOCKS5 proxy" }, { "SocksPort", "Port" }, { "PortWrong", "Enter a port from 1 to 65535" },
            { "SocksLan", "Allow access from local network" }, { "SocksLanDescription", "Other devices on your network will be able to use the proxy" },
            { "SocksAuthHelper", "Optional. Without them the proxy accepts connections without authentication" },
            { "SocksAuthPair", "Set both username and password" },
            { "SocksLanNoAuth", "Without a password any device on the network can use the proxy" },
            { "ProxyBanner", "Proxy mode: SOCKS5 {0}" }, { "ProxyBannerLan", "On local network: {0}" },
            { "AddressCopied", "Address copied" }, { "CopyAddress", "Copy address" },
            { "AdminRequired", "Administrator rights required" },
            { "AdminRequiredDescription", "VPN (TUN) mode needs administrator rights to create the network adapter and change routes. Restart the app as administrator?\n\nThe local SOCKS5 proxy mode works without them." },
            { "Restart", "Restart" }, { "RunAsAdmin", "Always run as administrator" },
            { "RunAsAdminDescription", "Connecting in VPN (TUN) mode will work without extra prompts" },
            { "AutostartNeedsAdmin", "Launch at login was enabled as administrator. It can only be disabled with administrator rights" },
            { "AdminSuffix", " — administrator" },
            { "AlreadyRunning", "TrustTunnel is already running as administrator. Open it from the notification area icon" },
            { "Appearance", "Appearance" }, { "Theme", "Theme" }, { "ThemeSystem", "System default" }, { "ThemeLight", "Light" },
            { "ThemeDark", "Dark" }, { "Language", "Language" }, { "LanguageSystem", "System default" },
            { "ExcludedRoutes", "Excluded routes" }, { "ExcludedRoutesHint", "Subnets in CIDR notation, one per line" },
            { "ExcludedRoutesWrong", "Invalid subnet: {0}" }, { "FollowOnGithub", "Follow us on GitHub" }, { "About", "About" },
            { "AboutDescription", "Graphical interface for the TrustTunnel console client" }, { "GuiVersion", "GUI version" },
            { "ClientVersion", "Client version" },
            { "Connected", "Connected" }, { "Connecting", "Connecting…" }, { "Disconnecting", "Disconnecting…" },
            { "Disconnected", "Disconnected" }, { "Connect", "Connect" }, { "Disconnect", "Disconnect" }, { "ConnectTo", "Connect to" },
            { "TrayStatus", "Status: {0}" }, { "TrayOpen", "Open TrustTunnel" }, { "TrayQuit", "Quit TrustTunnel" },
            { "TrayHiddenTitle", "TrustTunnel is running in the background" }, { "TrayHiddenText", "The app was minimized to the notification area" },
            { "ExitTitle", "Quit TrustTunnel?" }, { "ExitDescription", "This will disconnect you from the active server" },
            { "Quit", "Quit" }, { "DontQuit", "Don’t quit" },
            { "ClientNotFound", "trusttunnel_client.exe was not found next to the app" }, { "ConnectFailed", "Could not connect" },
            { "ConnectionLost", "The client has stopped" }, { "SomethingWrong", "Something went wrong. Please try again later" }
        };

        static Dictionary<FieldInfo, string> _russian;

        public static string Current { get; private set; }

        // lang: "system" | "ru" | "en"
        public static void Apply(string lang)
        {
            var fields = typeof(L).GetFields(BindingFlags.Public | BindingFlags.Static);
            if (_russian == null)
            {
                _russian = new Dictionary<FieldInfo, string>();
                foreach (var f in fields) if (f.FieldType == typeof(string)) _russian[f] = (string)f.GetValue(null);
            }
            Current = Resolve(lang);
            foreach (var pair in _russian)
            {
                string value;
                if (Current == "en" && English.TryGetValue(pair.Key.Name, out value)) pair.Key.SetValue(null, value);
                else pair.Key.SetValue(null, pair.Value);
            }
        }

        public static string Resolve(string lang)
        {
            if (lang == "ru" || lang == "en") return lang;
            var system = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return system == "ru" || system == "be" || system == "uk" || system == "kk" ? "ru" : "en";
        }
    }
}
