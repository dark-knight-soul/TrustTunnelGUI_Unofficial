@echo off
rem Builds TrustTunnelGUI.exe next to trusttunnel_client.exe using the C# compiler shipped with .NET Framework 4.x.
rem Usage: build.cmd          - release build
rem        build.cmd dev OUT  - development build with test hooks, written to OUT
setlocal
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FW%\WPF
cd /d "%~dp0"

set OUT=..\TrustTunnelGUI.exe
set EXTRA=
if /i "%1"=="dev" (
  set OUT=%2
  set EXTRA=/define:DEVTOOLS
)

"%FW%\csc.exe" /nologo /target:winexe /platform:anycpu /optimize+ %EXTRA% ^
  /out:"%OUT%" /win32icon:res\app_icon.ico /win32manifest:app.manifest ^
  /resource:res\app_icon.ico,app_icon.ico ^
  /r:"%WPF%\PresentationFramework.dll" /r:"%WPF%\PresentationCore.dll" /r:"%WPF%\WindowsBase.dll" ^
  /r:"%FW%\System.Xaml.dll" /r:"%FW%\System.Windows.Forms.dll" /r:"%FW%\System.Drawing.dll" ^
  /r:"%FW%\System.Web.Extensions.dll" /r:"%FW%\System.Security.dll" /r:System.Core.dll /r:System.Xml.dll ^
  App.cs Theme.cs Shell.cs Pages.cs Model.cs Vpn.cs Strings.cs DevTools.cs
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Built %OUT%
