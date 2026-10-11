# client/

Сюда кладутся файлы консольного клиента TrustTunnel для Windows. Сами бинарники в репозиторий не входят.

1. Скачайте архив `trusttunnel_client-<версия>-windows-x86_64` со страницы релизов
   [TrustTunnel/TrustTunnelClient](https://github.com/TrustTunnel/TrustTunnelClient/releases).
2. Распакуйте его содержимое в эту папку. Должно получиться так:

```
client/
├── trusttunnel_client.exe
├── setup_wizard.exe
├── wintun.dll
├── LICENSE.txt
└── WINTUN_LICENSE.txt
```

Скрипт `scripts\build.cmd` берёт файлы отсюда и упаковывает их в portable-версию и установщик.
Отладочные сборки (`build.cmd -Dev`) тоже находят клиент в этой папке.

GUI проверялся с клиентом **v1.1.11**.
