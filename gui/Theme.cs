using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace TrustTunnelGui
{
    // Colour tokens. Light values are the Flutter client's LightTheme; the dark set follows the same roles.
    class Palette
    {
        public string Accent, AccentText, AccentAdditional, NavSelected, NavSelectedIcon, Error,
            Background, Elevated, BackgroundAdditional, BackgroundSystem,
            NeutralLight, NeutralDark, NeutralBlack, NeutralBlend, Scrim, PowerOff,
            SnackBackground, SnackText, LogWarn, LogError, LogDebug, LogSelected,
            SwitchOffTrack, SwitchOffThumb, Selection, OnAccent, NavHover, HoverCircle;

        public static readonly Palette Light = new Palette
        {
            Accent = "#3972AA", AccentText = "#3972AA", AccentAdditional = "#4F8AC4",
            NavSelected = "#404F8AC4", NavSelectedIcon = "#2D5986", Error = "#F44725",
            Background = "#FFFFFF", Elevated = "#FFFFFF", BackgroundAdditional = "#F6F7F9", BackgroundSystem = "#E6EAEF",
            NeutralLight = "#9AA8B8", NeutralDark = "#74869C", NeutralBlack = "#333E4C", NeutralBlend = "#1A30353B",
            Scrim = "#66000000", PowerOff = "#74869C", SnackBackground = "#333E4C", SnackText = "#FFFFFF",
            LogWarn = "#C76300", LogError = "#D02C0B", LogDebug = "#74869C", LogSelected = "#334F8AC4",
            SwitchOffTrack = "#F6F7F9", SwitchOffThumb = "#74869C", Selection = "#4F8AC4", OnAccent = "#FFFFFF",
            NavHover = "#14333E4C", HoverCircle = "#E6EAEF"
        };

        public static readonly Palette Dark = new Palette
        {
            Accent = "#3D7BBD", AccentText = "#7FB0E3", AccentAdditional = "#6FA3D6",
            NavSelected = "#554F8AC4", NavSelectedIcon = "#D3E5F8", Error = "#FF6B4F",
            Background = "#1D2127", Elevated = "#272C34", BackgroundAdditional = "#262B33", BackgroundSystem = "#121417",
            NeutralLight = "#6C7C8F", NeutralDark = "#9AA8B8", NeutralBlack = "#E3E8EE", NeutralBlend = "#22FFFFFF",
            Scrim = "#99000000", PowerOff = "#4E5B6B", SnackBackground = "#E3E8EE", SnackText = "#1D2127",
            LogWarn = "#F0A848", LogError = "#FF7B61", LogDebug = "#8797AB", LogSelected = "#554F8AC4",
            SwitchOffTrack = "#262B33", SwitchOffThumb = "#9AA8B8", Selection = "#4F8AC4", OnAccent = "#FFFFFF",
            NavHover = "#1AFFFFFF", HoverCircle = "#2E343D"
        };
    }

    // Brushes of the active palette; pages read them while building, so a theme switch rebuilds the UI.
    static class C
    {
        public static bool IsDark { get; private set; }
        public static Palette P { get; private set; }

        public static SolidColorBrush Accent, AccentText, AccentAdditional, Error, Background, Elevated,
            BackgroundAdditional, BackgroundSystem, NeutralLight, NeutralDark, NeutralBlack, NeutralBlend,
            Scrim, PowerOff, SnackBackground, SnackText, LogWarn, LogError, LogDebug, White;

        static C()
        {
            Apply(false);
        }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            var p = dark ? Palette.Dark : Palette.Light;
            P = p;
            Accent = B(p.Accent); AccentText = B(p.AccentText); AccentAdditional = B(p.AccentAdditional);
            Error = B(p.Error); Background = B(p.Background); Elevated = B(p.Elevated);
            BackgroundAdditional = B(p.BackgroundAdditional); BackgroundSystem = B(p.BackgroundSystem);
            NeutralLight = B(p.NeutralLight); NeutralDark = B(p.NeutralDark); NeutralBlack = B(p.NeutralBlack);
            NeutralBlend = B(p.NeutralBlend); Scrim = B(p.Scrim); PowerOff = B(p.PowerOff);
            SnackBackground = B(p.SnackBackground); SnackText = B(p.SnackText);
            LogWarn = B(p.LogWarn); LogError = B(p.LogError); LogDebug = B(p.LogDebug); White = B(p.OnAccent);
        }

        public static SolidColorBrush B(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            return b;
        }
    }

    // Material icons (24x24 viewbox), same set as AssetIcons in the Flutter client.
    static class Icons
    {
        public static readonly Geometry Dns = G("M20 13H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1v-6c0-.55-.45-1-1-1zM7 19c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2zM20 3H4c-.55 0-1 .45-1 1v6c0 .55.45 1 1 1h16c.55 0 1-.45 1-1V4c0-.55-.45-1-1-1zM7 9c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z");
        public static readonly Geometry Route = G("M19 15.18V7c0-2.21-1.79-4-4-4s-4 1.79-4 4v10c0 1.1-.9 2-2 2s-2-.9-2-2V8.82C8.16 8.4 9 7.3 9 6c0-1.66-1.34-3-3-3S3 4.34 3 6c0 1.3.84 2.4 2 2.82V17c0 2.21 1.79 4 4 4s4-1.79 4-4V7c0-1.1.9-2 2-2s2 .9 2 2v8.18c-1.16.41-2 1.51-2 2.82 0 1.66 1.34 3 3 3s3-1.34 3-3c0-1.3-.84-2.4-2-2.82z");
        public static readonly Geometry Settings = G("M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z");
        public static readonly Geometry Power = G("M13 3h-2v10h2V3zm4.83 2.17l-1.42 1.42C17.99 7.86 19 9.81 19 12c0 3.87-3.13 7-7 7s-7-3.13-7-7c0-2.19 1.01-4.14 2.58-5.42L6.17 5.17C4.23 6.82 3 9.26 3 12c0 4.97 4.03 9 9 9s9-4.03 9-9c0-2.74-1.23-5.18-3.17-6.83z");
        public static readonly Geometry Update = G("M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z");
        public static readonly Geometry Add = G("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z");
        public static readonly Geometry NavigateNext = G("M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z");
        public static readonly Geometry Back = G("M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z");
        public static readonly Geometry Close = G("M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");
        public static readonly Geometry MoreVert = G("M12 8c1.1 0 2-.9 2-2s-.9-2-2-2-2 .9-2 2 .9 2 2 2zm0 2c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm0 6c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z");
        public static readonly Geometry Delete = G("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z");
        public static readonly Geometry Edit = G("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z");
        public static readonly Geometry ArrowDropDown = G("M7 10l5 5 5-5z");
        public static readonly Geometry Check = G("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");
        public static readonly Geometry Visibility = G("M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z");
        public static readonly Geometry VisibilityOff = G("M12 7c2.76 0 5 2.24 5 5 0 .65-.13 1.26-.36 1.83l2.92 2.92c1.51-1.26 2.7-2.89 3.43-4.75-1.73-4.39-6-7.5-11-7.5-1.4 0-2.74.25-3.98.7l2.16 2.16C10.74 7.13 11.35 7 12 7zM2 4.27l2.28 2.28.46.46C3.08 8.3 1.78 10.02 1 12c1.73 4.39 6 7.5 11 7.5 1.55 0 3.03-.3 4.38-.84l.42.42L19.73 22 21 20.73 3.27 3 2 4.27zM7.53 9.8l1.55 1.55c-.05.21-.08.43-.08.65 0 1.66 1.34 3 3 3 .22 0 .44-.03.65-.08l1.55 1.55c-.67.33-1.41.53-2.2.53-2.76 0-5-2.24-5-5 0-.79.2-1.53.53-2.2zm4.31-.78l3.15 3.15.02-.16c0-1.66-1.34-3-3-3l-.17.01z");
        public static readonly Geometry Link = G("M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z");
        public static readonly Geometry FileOpen = G("M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zM6 20V4h7v5h5v11H6z");
        public static readonly Geometry Copy = G("M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z");
        public static readonly Geometry Folder = G("M10 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2h-8l-2-2z");
        public static readonly Geometry ClearAll = G("M5 13h14v-2H5v2zm-2 4h14v-2H3v2zM7 7v2h14V7H7z");
        public static readonly Geometry OpenInNew = G("M19 19H5V5h7V3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7zM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7z");
        public static readonly Geometry ListAlt = G("M19 5v14H5V5h14m1.1-2H3.9c-.5 0-.9.4-.9.9v16.2c0 .4.4.9.9.9h16.2c.4 0 .9-.5.9-.9V3.9c0-.5-.5-.9-.9-.9zM11 7h6v2h-6V7zm0 4h6v2h-6v-2zm0 4h6v2h-6zM7 7h2v2H7zm0 4h2v2H7zm0 4h2v2H7z");

        static Geometry G(string data)
        {
            var g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }
    }

    static class Theme
    {
        public static readonly FontFamily Font = new FontFamily("Segoe UI");
        public static readonly FontFamily Mono = new FontFamily("Cascadia Mono, Consolas");

        static ResourceDictionary _installed;

        // theme: "system" | "light" | "dark". Replaces the styles; existing elements keep the old ones until rebuilt.
        public static void Install(Application app, string theme)
        {
            C.Apply(Resolve(theme));
            var xaml = BuildXaml();
            foreach (var f in typeof(Palette).GetFields())
                if (f.FieldType == typeof(string)) xaml = xaml.Replace("{{" + f.Name + "}}", (string)f.GetValue(C.P));
            var dict = (ResourceDictionary)XamlReader.Parse(xaml);
            if (_installed != null) app.Resources.MergedDictionaries.Remove(_installed);
            app.Resources.MergedDictionaries.Add(dict);
            _installed = dict;
        }

        public static bool Resolve(string theme)
        {
            if (theme == "dark") return true;
            if (theme == "light") return false;
            return SystemPrefersDark();
        }

        public static bool SystemPrefersDark()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // Dark caption on Windows 10/11 and, on Windows 11, a caption colour that blends with the rail.
        public static void ApplyTitleBar(Window window)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int dark = C.IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, 20, ref dark, 4) != 0) DwmSetWindowAttribute(hwnd, 19, ref dark, 4);
                var c = C.BackgroundSystem.Color;
                int colorRef = c.R | (c.G << 8) | (c.B << 16);
                DwmSetWindowAttribute(hwnd, 35, ref colorRef, 4);
            }
            catch { }
        }

        static string ButtonStyle(string key, string bg, string fg, string radius, string height, string padding,
            string weight, string overlay, string hover, string pressed, string border)
        {
            return
"<Style x:Key='" + key + "' TargetType='Button'>" +
"  <Setter Property='Background' Value='" + bg + "'/>" +
"  <Setter Property='Foreground' Value='" + fg + "'/>" +
"  <Setter Property='FontSize' Value='14'/>" +
"  <Setter Property='FontWeight' Value='" + weight + "'/>" +
(height != null ? "  <Setter Property='Height' Value='" + height + "'/>" : "") +
"  <Setter Property='Padding' Value='" + padding + "'/>" +
"  <Setter Property='Cursor' Value='Hand'/>" +
"  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>" +
"  <Setter Property='HorizontalContentAlignment' Value='Center'/>" +
"  <Setter Property='VerticalContentAlignment' Value='Center'/>" +
"  <Setter Property='Template'><Setter.Value>" +
"    <ControlTemplate TargetType='Button'>" +
"      <Grid SnapsToDevicePixels='True'>" +
"        <Border x:Name='bd' Background='{TemplateBinding Background}' CornerRadius='" + radius + "'" +
            (border != null ? " BorderBrush='" + border + "' BorderThickness='1'" : "") + "/>" +
"        <Border x:Name='ov' Background='" + overlay + "' Opacity='0' CornerRadius='" + radius + "'/>" +
"        <ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/>" +
"      </Grid>" +
"      <ControlTemplate.Triggers>" +
"        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='ov' Property='Opacity' Value='" + hover + "'/></Trigger>" +
"        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='ov' Property='Opacity' Value='" + hover + "'/></Trigger>" +
"        <Trigger Property='IsPressed' Value='True'><Setter TargetName='ov' Property='Opacity' Value='" + pressed + "'/></Trigger>" +
"        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/><Setter Property='Cursor' Value='Arrow'/></Trigger>" +
"      </ControlTemplate.Triggers>" +
"    </ControlTemplate>" +
"  </Setter.Value></Setter>" +
"</Style>";
        }

        static string FieldStyle(string target)
        {
            return
"<Style x:Key='" + target + "Field' TargetType='" + target + "'>" +
"  <Setter Property='FontSize' Value='14'/>" +
"  <Setter Property='Foreground' Value='{{NeutralBlack}}'/>" +
"  <Setter Property='Background' Value='Transparent'/>" +
"  <Setter Property='BorderBrush' Value='{{NeutralLight}}'/>" +
"  <Setter Property='Padding' Value='12,10'/>" +
"  <Setter Property='CaretBrush' Value='{{AccentText}}'/>" +
"  <Setter Property='SelectionBrush' Value='{{Selection}}'/>" +
"  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>" +
"  <Setter Property='VerticalContentAlignment' Value='Center'/>" +
"  <Setter Property='Template'><Setter.Value>" +
"    <ControlTemplate TargetType='" + target + "'>" +
"      <Grid SnapsToDevicePixels='True'>" +
"        <Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='4'/>" +
"        <Border x:Name='fc' BorderBrush='{{AccentText}}' BorderThickness='2' CornerRadius='4' Visibility='Collapsed'/>" +
"        <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='{TemplateBinding VerticalContentAlignment}' Focusable='False'/>" +
"      </Grid>" +
"      <ControlTemplate.Triggers>" +
"        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='{{NeutralBlack}}'/></Trigger>" +
"        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='fc' Property='Visibility' Value='Visible'/></Trigger>" +
"        <Trigger Property='Tag' Value='error'><Setter TargetName='bd' Property='BorderBrush' Value='{{Error}}'/><Setter TargetName='fc' Property='BorderBrush' Value='{{Error}}'/></Trigger>" +
"        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>" +
"      </ControlTemplate.Triggers>" +
"    </ControlTemplate>" +
"  </Setter.Value></Setter>" +
"</Style>";
        }

        static string BuildXaml()
        {
            return
"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +

ButtonStyle("FilledButton", "{{Accent}}", "White", "20", "40", "24,0", "SemiBold", "Black", "0.10", "0.20", null) +
ButtonStyle("DangerButton", "{{Error}}", "White", "20", "40", "24,0", "SemiBold", "Black", "0.08", "0.16", null) +
ButtonStyle("TextButton", "Transparent", "{{AccentText}}", "20", "40", "12,0", "SemiBold", "{{AccentText}}", "0.08", "0.14", null) +
ButtonStyle("DangerTextButton", "Transparent", "{{Error}}", "20", "40", "12,0", "SemiBold", "{{Error}}", "0.08", "0.14", null) +
ButtonStyle("OutlinedButton", "Transparent", "{{AccentText}}", "20", "40", "20,0", "SemiBold", "{{AccentText}}", "0.08", "0.14", "{{NeutralLight}}") +
ButtonStyle("IconButton", "Transparent", "{{NeutralBlack}}", "20", "40", "0", "Normal", "{{NeutralBlack}}", "0.08", "0.14", null) +
ButtonStyle("PowerButton", "{{PowerOff}}", "White", "20", "40", "0", "Normal", "Black", "0.12", "0.22", null) +
ButtonStyle("Fab", "{{Accent}}", "White", "16", "56", "16,0,20,0", "SemiBold", "Black", "0.10", "0.20", null) +
ButtonStyle("ListRow", "Transparent", "{{NeutralBlack}}", "0", null, "0", "Normal", "{{NeutralBlack}}", "0.04", "0.08", null) +
ButtonStyle("MenuRow", "Transparent", "{{NeutralBlack}}", "0", "48", "12,0,24,0", "Normal", "{{NeutralBlack}}", "0.06", "0.10", null) +
ButtonStyle("FieldButton", "Transparent", "{{NeutralBlack}}", "4", null, "12,10,8,10", "Normal", "{{NeutralBlack}}", "0.03", "0.06", "{{NeutralLight}}") +

FieldStyle("TextBox") +
FieldStyle("PasswordBox") +

// Navigation rail destination (Material 3 NavigationRail with labels).
"<Style x:Key='NavItem' TargetType='RadioButton'>" +
"  <Setter Property='Cursor' Value='Hand'/>" +
"  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>" +
"  <Setter Property='Margin' Value='0,0,0,12'/>" +
"  <Setter Property='Template'><Setter.Value>" +
"    <ControlTemplate TargetType='RadioButton'>" +
"      <StackPanel Background='Transparent' Width='96'>" +
"        <Border x:Name='pill' Width='56' Height='32' CornerRadius='16' Background='Transparent' HorizontalAlignment='Center'>" +
"          <Path x:Name='ic' Data='{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}' Fill='{{NeutralBlack}}' Width='24' Height='24' Stretch='None' HorizontalAlignment='Center' VerticalAlignment='Center'/>" +
"        </Border>" +
"        <ContentPresenter x:Name='lb' HorizontalAlignment='Center' Margin='0,4,0,0' TextBlock.FontSize='12' TextBlock.FontWeight='Medium' TextBlock.Foreground='{{NeutralBlack}}'/>" +
"      </StackPanel>" +
"      <ControlTemplate.Triggers>" +
"        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='pill' Property='Background' Value='{{NavHover}}'/></Trigger>" +
"        <Trigger Property='IsChecked' Value='True'><Setter TargetName='pill' Property='Background' Value='{{NavSelected}}'/><Setter TargetName='lb' Property='TextBlock.FontWeight' Value='Bold'/><Setter TargetName='ic' Property='Fill' Value='{{NavSelectedIcon}}'/></Trigger>" +
"      </ControlTemplate.Triggers>" +
"    </ControlTemplate>" +
"  </Setter.Value></Setter>" +
"</Style>" +

"<Style x:Key='Radio' TargetType='RadioButton'>" +
"  <Setter Property='Cursor' Value='Hand'/>" +
"  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>" +
"  <Setter Property='Foreground' Value='{{NeutralBlack}}'/>" +
"  <Setter Property='FontSize' Value='14'/>" +
"  <Setter Property='Template'><Setter.Value>" +
"    <ControlTemplate TargetType='RadioButton'>" +
"      <Grid Background='Transparent'>" +
"        <Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>" +
"        <Grid Width='40' Height='40' VerticalAlignment='Center'>" +
"          <Ellipse x:Name='hov' Fill='{{HoverCircle}}' Opacity='0'/>" +
"          <Ellipse x:Name='ring' Width='20' Height='20' Stroke='{{NeutralBlack}}' StrokeThickness='2'/>" +
"          <Ellipse x:Name='dot' Width='10' Height='10' Fill='{{Accent}}' Visibility='Collapsed'/>" +
"        </Grid>" +
"        <ContentPresenter Grid.Column='1' VerticalAlignment='Center' Margin='8,0,0,0'/>" +
"      </Grid>" +
"      <ControlTemplate.Triggers>" +
"        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='hov' Property='Opacity' Value='1'/></Trigger>" +
"        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='hov' Property='Opacity' Value='1'/></Trigger>" +
"        <Trigger Property='IsChecked' Value='True'><Setter TargetName='ring' Property='Stroke' Value='{{Accent}}'/><Setter TargetName='dot' Property='Visibility' Value='Visible'/></Trigger>" +
"        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>" +
"      </ControlTemplate.Triggers>" +
"    </ControlTemplate>" +
"  </Setter.Value></Setter>" +
"</Style>" +

"<Style x:Key='Check' TargetType='CheckBox'>" +
"  <Setter Property='Cursor' Value='Hand'/>" +
"  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>" +
"  <Setter Property='Foreground' Value='{{NeutralBlack}}'/>" +
"  <Setter Property='FontSize' Value='14'/>" +
"  <Setter Property='Template'><Setter.Value>" +
"    <ControlTemplate TargetType='CheckBox'>" +
"      <Grid Background='Transparent'>" +
"        <Grid.ColumnDefinitions><ColumnDefinition Width='Auto'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>" +
"        <Grid Width='40' Height='40' VerticalAlignment='Center'>" +
"          <Ellipse x:Name='hov' Fill='{{HoverCircle}}' Opacity='0'/>" +
"          <Border x:Name='box' Width='18' Height='18' CornerRadius='2' BorderThickness='2' BorderBrush='{{NeutralLight}}' Background='Transparent'>" +
"            <Path x:Name='mark' Data='M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z' Fill='White' Stretch='Uniform' Margin='1' Visibility='Collapsed'/>" +
"          </Border>" +
"        </Grid>" +
"        <ContentPresenter Grid.Column='1' VerticalAlignment='Center' Margin='8,0,0,0'/>" +
"      </Grid>" +
"      <ControlTemplate.Triggers>" +
"        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='hov' Property='Opacity' Value='1'/></Trigger>" +
"        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='hov' Property='Opacity' Value='1'/></Trigger>" +
"        <Trigger Property='IsChecked' Value='True'><Setter TargetName='box' Property='Background' Value='{{Accent}}'/><Setter TargetName='box' Property='BorderBrush' Value='{{Accent}}'/><Setter TargetName='mark' Property='Visibility' Value='Visible'/></Trigger>" +
"        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>" +
"      </ControlTemplate.Triggers>" +
"    </ControlTemplate>" +
"  </Setter.Value></Setter>" +
"</Style>" +

"<Style x:Key='Switch' TargetType='ToggleButton'>" +
"  <Setter Property='Cursor' Value='Hand'/>" +
"  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>" +
"  <Setter Property='Template'><Setter.Value>" +
"    <ControlTemplate TargetType='ToggleButton'>" +
"      <Grid Width='52' Height='32' Background='Transparent'>" +
"        <Border x:Name='track' CornerRadius='16' Background='{{SwitchOffTrack}}' BorderBrush='{{SwitchOffThumb}}' BorderThickness='2'/>" +
"        <Ellipse x:Name='thumb' Width='16' Height='16' Fill='{{SwitchOffThumb}}' HorizontalAlignment='Left' Margin='8,0,0,0'/>" +
"      </Grid>" +
"      <ControlTemplate.Triggers>" +
"        <Trigger Property='IsChecked' Value='True'>" +
"          <Setter TargetName='track' Property='Background' Value='{{Accent}}'/>" +
"          <Setter TargetName='track' Property='BorderBrush' Value='{{Accent}}'/>" +
"          <Setter TargetName='thumb' Property='Width' Value='24'/>" +
"          <Setter TargetName='thumb' Property='Height' Value='24'/>" +
"          <Setter TargetName='thumb' Property='Fill' Value='White'/>" +
"          <Setter TargetName='thumb' Property='HorizontalAlignment' Value='Right'/>" +
"          <Setter TargetName='thumb' Property='Margin' Value='0,0,4,0'/>" +
"        </Trigger>" +
"        <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>" +
"      </ControlTemplate.Triggers>" +
"    </ControlTemplate>" +
"  </Setter.Value></Setter>" +
"</Style>" +

// Thin overlay-like scrollbars.
"<ControlTemplate x:Key='ThumbTpl' TargetType='Thumb'>" +
"  <Border x:Name='t' CornerRadius='3' Background='{{NeutralLight}}' Opacity='0.55' Margin='2'/>" +
"  <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='t' Property='Opacity' Value='0.9'/></Trigger>" +
"  <Trigger Property='IsDragging' Value='True'><Setter TargetName='t' Property='Opacity' Value='1'/></Trigger></ControlTemplate.Triggers>" +
"</ControlTemplate>" +
"<Style TargetType='ScrollBar'>" +
"  <Setter Property='Width' Value='10'/>" +
"  <Setter Property='MinWidth' Value='10'/>" +
"  <Setter Property='Template'><Setter.Value>" +
"    <ControlTemplate TargetType='ScrollBar'>" +
"      <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.Thumb><Thumb Template='{StaticResource ThumbTpl}'/></Track.Thumb></Track></Grid>" +
"    </ControlTemplate>" +
"  </Setter.Value></Setter>" +
"  <Style.Triggers>" +
"    <Trigger Property='Orientation' Value='Horizontal'>" +
"      <Setter Property='Width' Value='Auto'/><Setter Property='MinWidth' Value='0'/>" +
"      <Setter Property='Height' Value='10'/><Setter Property='MinHeight' Value='10'/>" +
"      <Setter Property='Template'><Setter.Value>" +
"        <ControlTemplate TargetType='ScrollBar'>" +
"          <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='False'><Track.Thumb><Thumb Template='{StaticResource ThumbTpl}'/></Track.Thumb></Track></Grid>" +
"        </ControlTemplate>" +
"      </Setter.Value></Setter>" +
"    </Trigger>" +
"  </Style.Triggers>" +
"</Style>" +

"<Style TargetType='ToolTip'>" +
"  <Setter Property='Background' Value='{{SnackBackground}}'/>" +
"  <Setter Property='Foreground' Value='{{SnackText}}'/>" +
"  <Setter Property='BorderThickness' Value='0'/>" +
"  <Setter Property='FontSize' Value='12'/>" +
"  <Setter Property='Padding' Value='8,4'/>" +
"  <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToolTip'>" +
"    <Border Background='{TemplateBinding Background}' CornerRadius='4' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border>" +
"  </ControlTemplate></Setter.Value></Setter>" +
"</Style>" +

"</ResourceDictionary>";
        }
    }

    // Small factory helpers so pages read like the Flutter widget tree.
    static class UI
    {
        public static Style S(string key)
        {
            return (Style)Application.Current.FindResource(key);
        }

        public static TextBlock Text(string text, double size = 14, FontWeight? weight = null, Brush color = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                FontWeight = weight ?? FontWeights.Normal,
                Foreground = color ?? C.NeutralBlack,
                TextWrapping = TextWrapping.Wrap
            };
        }

        // Flutter text theme equivalents.
        public static TextBlock TitleLarge(string t) { return Text(t, 22); }
        public static TextBlock TitleMedium(string t) { return Text(t, 16, FontWeights.SemiBold); }
        public static TextBlock TitleSmall(string t) { return Text(t, 14, FontWeights.SemiBold); }
        public static TextBlock BodyLarge(string t) { return Text(t, 16); }
        public static TextBlock BodyMedium(string t, Brush c = null) { return Text(t, 14, null, c); }
        public static TextBlock BodySmall(string t, Brush c = null) { return Text(t, 12, null, c ?? C.NeutralDark); }

        public static FrameworkElement Icon(Geometry g, double size = 24, Brush fill = null)
        {
            var p = new System.Windows.Shapes.Path
            {
                Data = g,
                Fill = fill ?? C.NeutralBlack,
                Width = 24,
                Height = 24,
                Stretch = Stretch.None,
                SnapsToDevicePixels = true
            };
            if (Math.Abs(size - 24) > 0.1)
                p.LayoutTransform = new ScaleTransform(size / 24.0, size / 24.0);
            return p;
        }

        public static Button IconButton(Geometry g, string tooltip, Action click, Brush fill = null)
        {
            var b = new Button { Style = S("IconButton"), Width = 40, Content = Icon(g, 24, fill) };
            if (!string.IsNullOrEmpty(tooltip)) b.ToolTip = tooltip;
            b.Click += delegate { click(); };
            return b;
        }

        public static Button Filled(string text, Action click, bool danger = false)
        {
            var b = new Button { Style = S(danger ? "DangerButton" : "FilledButton"), Content = text };
            b.Click += delegate { click(); };
            return b;
        }

        public static Button TextButton(string text, Action click, bool danger = false)
        {
            var b = new Button { Style = S(danger ? "DangerTextButton" : "TextButton"), Content = text };
            b.Click += delegate { click(); };
            return b;
        }

        public static Button Outlined(string text, Geometry icon, Action click)
        {
            object content = text;
            if (icon != null)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var ic = Icon(icon, 18, C.AccentText);
                ic.Margin = new Thickness(-4, 0, 8, 0);
                ic.VerticalAlignment = VerticalAlignment.Center;
                sp.Children.Add(ic);
                sp.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
                content = sp;
            }
            var b = new Button { Style = S("OutlinedButton"), Content = content };
            b.Click += delegate { click(); };
            return b;
        }

        // Left-aligned element that stretches up to maxWidth.
        public static FrameworkElement MaxWidthLeft(UIElement child, double maxWidth)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = maxWidth });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(child);
            return g;
        }
        public static Border Divider()
        {
            return new Border { Height = 1, Background = C.NeutralBlend, SnapsToDevicePixels = true };
        }

        public static Border VerticalDivider()
        {
            return new Border { Width = 1, Background = C.NeutralBlend, Margin = new Thickness(0, 16, 0, 16), SnapsToDevicePixels = true };
        }

        public static ScrollViewer Scroll(UIElement content)
        {
            return new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false
            };
        }

        public static Effect Shadow(double depth = 2, double blur = 8, double opacity = 0.25)
        {
            return new DropShadowEffect { BlurRadius = blur, ShadowDepth = depth, Direction = 270, Opacity = opacity, Color = Colors.Black };
        }

        // Rounded clip that follows the element size (WPF does not clip children to CornerRadius).
        public static void ClipRounded(FrameworkElement fe, double radius)
        {
            SizeChangedEventHandler update = delegate
            {
                fe.Clip = new RectangleGeometry(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight), radius, radius);
            };
            fe.SizeChanged += update;
        }

        // Anchored popup menu (PopupMenuButton / DropdownMenu analogue).
        public static void ShowMenu(FrameworkElement anchor, IList<MenuEntry> entries, double minWidth = 0)
        {
            var popup = new Popup
            {
                PlacementTarget = anchor,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };
            var list = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
            foreach (var e in entries)
            {
                var entry = e;
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Brush color = entry.Danger ? (Brush)C.Error : C.NeutralBlack;
                if (entry.Icon != null)
                {
                    var ic = Icon(entry.Icon, 24, color);
                    ic.Margin = new Thickness(0, 0, 12, 0);
                    ic.VerticalAlignment = VerticalAlignment.Center;
                    row.Children.Add(ic);
                }
                var tb = BodyLarge(entry.Text);
                tb.Foreground = color;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.TextWrapping = TextWrapping.NoWrap;
                if (entry.Selected) tb.FontWeight = FontWeights.SemiBold;
                Grid.SetColumn(tb, 1);
                row.Children.Add(tb);
                var btn = new Button { Style = S("MenuRow"), Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch };
                if (entry.Selected) btn.Background = C.BackgroundAdditional;
                btn.Click += delegate { popup.IsOpen = false; if (entry.Action != null) entry.Action(); };
                list.Children.Add(btn);
            }
            var card = new Border
            {
                Background = C.Elevated,
                CornerRadius = new CornerRadius(4),
                Child = list,
                Margin = new Thickness(8, 4, 8, 12),
                MinWidth = Math.Max(minWidth, 180),
                Effect = Shadow(3, 12, 0.22)
            };
            popup.Child = card;
            popup.IsOpen = true;
        }
    }

    class MenuEntry
    {
        public string Text;
        public Geometry Icon;
        public Action Action;
        public bool Danger;
        public bool Selected;

        public MenuEntry(string text, Geometry icon, Action action, bool danger = false)
        {
            Text = text; Icon = icon; Action = action; Danger = danger;
        }
    }
}
