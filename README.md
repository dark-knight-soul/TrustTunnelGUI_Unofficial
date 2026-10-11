<p align="center">
  <img src="docs/logo.png" width="96" alt="TrustTunnel GUI">
</p>

<h1 align="center">TrustTunnel GUI для Windows</h1>

<p align="center">
  Неофициальный графический клиент для <a href="https://github.com/TrustTunnel/TrustTunnelClient">TrustTunnel</a>
  с интерфейсом в стиле <a href="https://github.com/TrustTunnel/TrustTunnelFlutterClient">TrustTunnel Flutter Client</a>
</p>

<p align="center">
  <img src="docs/badges/version.svg" alt="version 1.2.0">
  <img src="docs/badges/windows.svg" alt="Windows 10 | 11">
  <img src="docs/badges/dotnet.svg" alt=".NET Framework 4.8">
  <img src="docs/badges/license.svg" alt="license Apache-2.0">
  <img src="docs/badges/status.svg" alt="status unofficial">
</p>

<p align="center">
  <img src="docs/screenshots/servers-light.png" width="49%" alt="Серверы, светлая тема">
  <img src="docs/screenshots/servers-dark.png" width="49%" alt="Серверы, тёмная тема">
</p>

> [!NOTE]
> Это сторонний проект, он не связан с авторами TrustTunnel. GUI управляет официальным консольным клиентом
> `trusttunnel_client.exe`: формирует для него конфигурацию, запускает его и показывает его журнал.

## Содержание

- [Возможности](#возможности)
- [Скачать](#скачать)
- [Быстрый старт](#быстрый-старт)
- [Режимы подключения](#режимы-подключения)
- [Скриншоты](#скриншоты)
- [Как это устроено](#как-это-устроено)
- [Структура проекта](#структура-проекта)
- [Сборка из исходников](#сборка-из-исходников)
- [Где хранятся данные](#где-хранятся-данные)
- [Решение проблем](#решение-проблем)
- [English](#english)

## Возможности

- **Серверы.** Добавление и редактирование, импорт ссылки `tt://` и файла `.toml`, протоколы HTTP/2 и QUIC,
  свои DNS-серверы, пользовательский SNI, Client Random, самоподписанный сертификат.
- **Маршрутизация.** Профили с правилами «Обход» и «VPN» (домены, IP, IP с портом, подсети) и выбор маршрутизации по умолчанию.
- **Два режима.** VPN (TUN) для всего трафика системы или локальный SOCKS5-прокси для выбранных приложений,
  в том числе для других устройств в локальной сети.
- **Внешний вид.** Светлая и тёмная тема (или как в Windows), русский и английский язык.
- **Сетевые адаптеры.** Можно выбрать исходящий адаптер для туннеля и адаптер, на котором SOCKS5-прокси принимает подключения.
- **Пинг серверов.** Время отклика каждого сервера прямо в списке.
- **Журнал подключений.** Как во Flutter-клиенте: домен, адрес и решение по каждому соединению (VPN / Обход / Блок), с поиском.
  Плюс полный журнал клиента с подсветкой уровней.
- **Автопереподключение.** Если клиент неожиданно завершился, подключение восстанавливается с нарастающей паузой.
- **Трей.** Статус подключения в значке, быстрое подключение и отключение, выбор сервера.
- **Запуск.** Автозапуск при входе в систему, автоподключение, kill switch. Права администратора запрашиваются только для режима VPN (TUN).
- **Расширенные настройки.** anti-DPI, отключение проверки сертификата, MTU, системный DNS, постквантовый обмен ключами.
- **Резервная копия.** Экспорт и импорт серверов и профилей; пароли — только в зашифрованном виде.
- **Обновления.** Проверка новой версии на GitHub.
- **Надёжность.** Клиент останавливается через Ctrl+C, чтобы восстановить маршруты и DNS. Пароли хранятся
  в зашифрованном виде (DPAPI), а конфигурация с паролем существует на диске только пока клиент работает.

## Скачать

Готовые сборки — на странице [Releases](https://github.com/dark-knight-soul/TrustTunnelGUI_Unofficial/releases).
В обе сборки уже входит консольный клиент TrustTunnel и драйвер Wintun.

| Вариант | Файл | Для чего |
|---|---|---|
| **Установщик** | `TrustTunnelGUI-1.2.0-setup.exe` | Установка в `Program Files`, ярлыки, удаление через «Установленные приложения». Настройки — в `%APPDATA%\TrustTunnelGUI`. |
| **Portable** | `TrustTunnelGUI-1.2.0-portable.zip` | Распаковать в любую папку и запустить `TrustTunnelGUI.exe`. Настройки и журналы — в папке `data` рядом с программой. |

Нужна Windows 10 или 11 (x64); .NET Framework 4.8 уже входит в систему.

<p align="center">
  <img src="docs/screenshots/setup-light.png" width="49%" alt="Установщик">
  <img src="docs/screenshots/setup-dark.png" width="49%" alt="Установщик, тёмная тема">
</p>

Установщик поддерживает тихий режим для автоматизации:

```bat
TrustTunnelGUI-1.2.0-setup.exe --silent
"C:\Program Files\TrustTunnel GUI\uninstall.exe" --uninstall --silent
```

## Быстрый старт

1. Запустите TrustTunnel GUI и нажмите **«Создать»** или **«Добавить сервер»**.
2. Заполните адрес, доменное имя из сертификата, логин и пароль. Можно и проще: нажмите значок ссылки
   и вставьте `tt://`-ссылку от администратора сервера или выберите `.toml`-файл конфигурации.
3. Нажмите круглую кнопку питания напротив сервера. Синяя кнопка означает, что подключение установлено.

Для режима VPN (TUN) приложение предложит перезапуститься от имени администратора: без этих прав нельзя
создать сетевой адаптер. Чтобы не видеть этот запрос, включите
*Настройки → Запуск и подключение → Всегда запускать от имени администратора*.

## Режимы подключения

Режим выбирается в *Настройки → Режим подключения*.

| | VPN (TUN) | Локальный SOCKS5-прокси |
|---|---|---|
| Какой трафик идёт через сервер | весь трафик системы | только приложений, где указан прокси |
| Права администратора | нужны | не нужны |
| Настройка приложений | не требуется | указать `127.0.0.1:1080` (порт меняется) в настройках прокси |
| Другие устройства в сети | — | по желанию: на всех адаптерах или только на выбранном, с логином и паролем |
| Исключённые маршруты, DNS системы | применяются | не применяются |
| Выбор сетевого адаптера | исходящий: через какой адаптер идти к серверу | входящий: на каком адаптере принимать подключения |

<p align="center">
  <img src="docs/screenshots/connection-mode.png" width="49%" alt="Режим подключения">
  <img src="docs/screenshots/servers-socks.png" width="49%" alt="Адрес прокси на экране серверов">
</p>

**Исходящий адаптер (TUN).** Если у компьютера несколько выходов в интернет (например, Ethernet и Wi-Fi или LTE-модем), в блоке *VPN (TUN)* можно выбрать, через какой из них клиент подключается к серверу. При подключении GUI передаёт клиенту индекс этого интерфейса в параметре `bound_if`. В списке только адаптеры со шлюзом. Значение «Автоматически» оставляет выбор клиенту.

**Выбор сетевого адаптера.** Если в компьютере несколько адаптеров (например, Ethernet в домашней сети, Wi-Fi
в гостевой и виртуальный Hyper-V), прокси можно открыть только на одном из них. Для этого в списке
*Сетевой адаптер* вместо «Все адаптеры» выберите нужный. Прокси будет слушать только IP этого адаптера.
Запоминается сам адаптер, а не его адрес, поэтому смена IP по DHCP ничего не ломает. Если выбранный адаптер
отключён или не получил IPv4-адрес, подключение не запустится, а приложение подскажет, в чём дело:
прокси не откроется тихо на остальных адаптерах.

<p align="center">
  <img src="docs/screenshots/connection-mode-adapter.png" width="70%" alt="Выбор сетевого адаптера">
</p>

## Скриншоты

| | |
|---|---|
| ![Журнал подключений](docs/screenshots/connection-log.png) | ![Журнал клиента](docs/screenshots/log-dark.png) |
| Журнал подключений: куда и как ушло каждое соединение | Журнал клиента |
| ![Сервер](docs/screenshots/server-edit.png) | ![Дополнительно](docs/screenshots/server-advanced.png) |
| Редактирование сервера | Расширенные параметры сервера |
| ![Правила маршрутизации](docs/screenshots/routing-rules.png) | ![Резервная копия](docs/screenshots/backup.png) |
| Правила профиля маршрутизации | Резервная копия |
| ![Запуск](docs/screenshots/launch.png) | ![Запуск, тёмная тема](docs/screenshots/launch-dark.png) |
| Запуск, подключение, обновления | Тёмная тема |
| ![Settings](docs/screenshots/settings-en.png) | ![Appearance](docs/screenshots/appearance-en.png) |
| Английский интерфейс | Тема и язык |
| ![Пустой список](docs/screenshots/servers-empty.png) | ![Удаление](docs/screenshots/uninstall-dark.png) |
| Первый запуск | Удаление программы |

## Как это устроено

```mermaid
flowchart LR
    UI["TrustTunnel GUI<br/>(WPF)"] -- "серверы, профили,<br/>настройки" --> Store[("data.json<br/>%APPDATA% или .\data")]
    UI -- "генерирует TOML" --> Config["trusttunnel_client.toml<br/>(только на время работы)"]
    UI -- "запуск / Ctrl+C" --> Client["trusttunnel_client.exe"]
    Config --> Client
    Client -- "stdout: raise_state VPN_SS_*" --> UI
    Client --> TUN["Wintun-адаптер<br/>(режим VPN)"]
    Client --> SOCKS["SOCKS5 127.0.0.1:1080<br/>(режим прокси)"]
    TUN --> Server(("Сервер<br/>TrustTunnel"))
    SOCKS --> Server
```

- **Конфигурация.** Конфиг строится так же, как его строит `ConfigurationEncoder` во Flutter-клиенте:
  `vpn_mode = general | selective` зависит от маршрутизации профиля по умолчанию, а правила
  второго списка превращаются в `exclusions` (к доменам добавляется маска `*.`). Затем добавляется
  секция `[endpoint]` и одна из секций `[listener.tun]` или `[listener.socks]`.
- **Состояние.** GUI читает вывод клиента и смотрит на строки `raise_state: [..] VPN_SS_CONNECTING / CONNECTED / …`.
- **Остановка.** GUI подключается к скрытой консоли клиента и отправляет Ctrl+C, после чего клиент сам
  убирает маршруты и DNS. Если клиент не завершился за 8 секунд, процесс завершается принудительно.
- **Права администратора.** Само приложение запускается с обычными правами (`asInvoker`). Для режима TUN
  оно перезапускает себя через UAC с ключом `--connect <сервер>`.
- **Журнал подключений.** На уровне «Отладка» клиент пишет по каждому соединению строки с номером `[L:x-R:y]`:
  `New client connection request: откуда->куда (proto: …)`, `Found domain in … data: домен`,
  `Final action: VPN_FCA_TUNNEL | BYPASS | REJECT` и `Destroyed (download=…, upload=…)`. GUI собирает из них таблицу.
- **Автопереподключение.** Если клиент завершился сам, а до этого успел подключиться, GUI запускает его снова
  через 2, 5, 10, 20 и 30 с (до 10 попыток) или сразу, как только Windows сообщит о появлении сети.

## Структура проекта

```
├── src/                     приложение
│   ├── App.cs               точка входа, единственный экземпляр, трей, автозапуск, импорт
│   ├── Shell.cs             главное окно, навигация, диалоги, уведомления, поля ввода
│   ├── Pages.cs             экраны: серверы, маршрутизация, настройки и их подстраницы
│   ├── Theme.cs             палитры (светлая/тёмная), стили WPF, иконки Material
│   ├── Model.cs             данные, хранение, генератор и парсер TOML, валидация
│   ├── Vpn.cs               запуск клиента, разбор состояний и журнала подключений, остановка через Ctrl+C
│   ├── Services.cs          пинг серверов, проверка обновлений
│   ├── Backup.cs            экспорт и импорт резервной копии (AES-256 + HMAC)
│   ├── Strings.cs           строки интерфейса (ru / en)
│   ├── DevTools.cs          самотесты и рендер скриншотов (только в сборке -Dev)
│   ├── app.manifest
│   └── res/app_icon.ico
├── installer/
│   ├── Setup.cs             установщик и деинсталлятор (WPF, тот же стиль)
│   └── setup.manifest
├── scripts/
│   ├── build.cmd            сборка одной командой
│   └── package.ps1          компиляция, portable, установщик
├── .github/workflows/
│   └── build.yml            сборка в GitHub Actions и публикация релиза по тегу
├── client/                  сюда кладётся консольный клиент (не хранится в git)
├── docs/                    логотип и скриншоты
├── CHANGELOG.md
└── LICENSE
```

## Сборка из исходников

Visual Studio не нужна: используется компилятор C# из .NET Framework 4.8, который есть в Windows.

1. Положите файлы консольного клиента в `client\` (см. [client/README.md](client/README.md)).
2. Запустите сборку:

```bat
scripts\build.cmd
```

Результат появится в `dist\`:

```
dist/
├── portable/
│   ├── TrustTunnelGUI-1.2.0-portable/      готовая к запуску папка
│   └── TrustTunnelGUI-1.2.0-portable.zip
└── installer/
    └── TrustTunnelGUI-1.2.0-setup.exe
```

Отладочная сборка `scripts\build.cmd -Dev` создаёт `dist\dev\TrustTunnelGUI.dev.exe` с самотестами:

| Ключ | Что делает |
|---|---|
| `--screenshots <папка>` | рендерит все экраны в PNG без показа окна |
| `--test-config <sample.toml> <папка>` | проверяет генерацию и разбор TOML и валидацию полей |
| `--test-vpn <config.toml> <result.txt>` | полный цикл: запуск клиента, отслеживание состояния, остановка через Ctrl+C |
| `--print-paths <файл>` | показывает найденные пути клиента и данных |
| `--test-features <папка>` | журнал подключений, резервная копия, пинг, проверка обновлений, расширенные параметры |

Отладочный установщик `dist\dev\Setup.dev.exe --render <папка> [--theme light|dark]` рендерит свои экраны.

### Релизы через GitHub Actions

Workflow [`.github/workflows/build.yml`](.github/workflows/build.yml) собирает пакеты на `windows-latest`.
Консольный клиент он скачивает из релизов TrustTunnel (версия задана в `CLIENT_VERSION`).

- **Тег `vX.Y.Z`**: сборка и публикация релиза с установщиком, portable-архивом, разделом из CHANGELOG и SHA-256.
  Тег должен совпадать с версией в `src/App.cs` и `installer/Setup.cs`.
- **Run workflow** на вкладке Actions: только сборка, файлы доступны в артефактах запуска.
- **Подпись exe** (по желанию): добавьте секреты репозитория `SIGN_PFX_BASE64` (PFX-файл в base64)
  и `SIGN_PFX_PASSWORD`. Тогда приложение и установщик будут подписаны через `signtool`, и SmartScreen перестанет
  предупреждать, когда у сертификата накопится репутация. Без секретов шаг пропускается.

```bat
git tag v1.2.0
git push origin v1.2.0
```

## Где хранятся данные

| | Установленная версия | Portable |
|---|---|---|
| Серверы и настройки | `%APPDATA%\TrustTunnelGUI\data.json` | `.\data\data.json` |
| Журналы GUI и клиента | `%APPDATA%\TrustTunnelGUI\logs` | `.\data\logs` |
| Конфиг клиента (на время работы) | `%APPDATA%\TrustTunnelGUI\trusttunnel_client.toml` | `.\data\trusttunnel_client.toml` |

Пароли серверов и прокси зашифрованы DPAPI для текущего пользователя Windows: файл `data.json`, перенесённый
на другой компьютер, не раскроет пароли, но и не подставит их.

## Решение проблем

<details>
<summary><b>Статус долго остаётся «Подключение…»</b></summary>

Откройте *Настройки → Журнал подключений*. Строки `WARN Failed to ping location` значат, что сервер недоступен
по указанному адресу и порту. Строки `ERROR` обычно указывают на неверный логин, пароль или сертификат.
Для подробного журнала включите *Настройки → Журналирование → Отладка* и переподключитесь.
</details>

<details>
<summary><b>Другие устройства не подключаются к прокси</b></summary>

Включите *Доступ из локальной сети* и разрешите `trusttunnel_client.exe` в брандмауэре Windows
(обычно Windows сама показывает запрос при первом запуске в этом режиме). Используйте адрес, который показан на экране серверов.
</details>

<details>
<summary><b>Как перенести серверы из старой версии GUI</b></summary>

Если рядом с программой лежит `trusttunnel_client.toml`, при первом запуске он импортируется как сервер.
Любой `.toml` можно импортировать и вручную: *Добавить сервер → значок файла*.
</details>

## English

**TrustTunnel GUI** is an unofficial Windows front end for the TrustTunnel console client. Its interface follows
the official Flutter client. You can manage servers (with `tt://` and `.toml` import) and routing profiles,
connect through a system-wide TUN adapter or a local SOCKS5 proxy, pick a light or dark theme, and use the
English or Russian UI. Download the installer or the portable zip from
[Releases](https://github.com/dark-knight-soul/TrustTunnelGUI_Unofficial/releases), or build both with
`scripts\build.cmd` after putting the client release files into `client\`. Only the .NET Framework 4.8
compiler that ships with Windows is required.

## Благодарности и лицензия

- [TrustTunnel](https://github.com/TrustTunnel): консольный клиент, `setup_wizard` и дизайн Flutter-клиента,
  на который ориентируется этот интерфейс.
- [Wintun](https://www.wintun.net/): драйвер виртуального сетевого адаптера (лицензия входит в сборки как `WINTUN_LICENSE.txt`).
- Иконки — [Material Icons](https://fonts.google.com/icons) (Apache-2.0).

Код этого репозитория распространяется по лицензии [Apache-2.0](LICENSE).
