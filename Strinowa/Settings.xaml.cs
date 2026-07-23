using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StrinowaWPF
{
    public enum LauncherTheme { Dark, Midnight, Acrylic, Light }

    // aurora
    // public enum LauncherTheme { Dark, Midnight, MidnightAurora, Light }
    //
    // public class AuroraAnimator
    // {
    //     static readonly Color[] _stops =
    //     {
    //         Color.FromRgb(0x09, 0x04, 0x1A), Color.FromRgb(0x12, 0x04, 0x28),
    //         Color.FromRgb(0x04, 0x08, 0x22), Color.FromRgb(0x0A, 0x02, 0x18),
    //     };
    //     readonly LinearGradientBrush _brush;
    //     readonly DispatcherTimer _timer;
    //     readonly Border _target;
    //     int _phase = 0;
    //     public AuroraAnimator(Border target)
    //     {
    //         _target = target;
    //         _brush = new LinearGradientBrush { StartPoint = new System.Windows.Point(0,0), EndPoint = new System.Windows.Point(1,1) };
    //         _brush.GradientStops.Add(new GradientStop(_stops[0], 0.0));
    //         _brush.GradientStops.Add(new GradientStop(_stops[1], 0.5));
    //         _brush.GradientStops.Add(new GradientStop(_stops[2], 1.0));
    //         _target.Background = _brush;
    //         _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3200) };
    //         _timer.Tick += Tick;
    //         _timer.Start();
    //     }
    //     void Tick(object? s, EventArgs e)
    //     {
    //         _phase = (_phase + 1) % _stops.Length;
    //         int n = _stops.Length;
    //         var dur = TimeSpan.FromMilliseconds(2800);
    //         var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
    //         _brush.GradientStops[0].BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(_stops[_phase % n], dur) { EasingFunction = ease });
    //         _brush.GradientStops[1].BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(_stops[(_phase+1) % n], dur) { EasingFunction = ease });
    //         _brush.GradientStops[2].BeginAnimation(GradientStop.ColorProperty, new ColorAnimation(_stops[(_phase+2) % n], dur) { EasingFunction = ease });
    //     }
    //     public void Stop() { _timer.Stop(); _target.Background = new SolidColorBrush(Color.FromRgb(0,0,0)); }
    // }
    // aurora

    public enum TermColorPreset { AdaptiveWhiteBlack, PinkAccent }
    public enum AppLanguage { English, Chinese, Polish }

    // acrylic/mica blur via DWM
    // add glass later. Export texture from 4015/4020 and 4042/4051. Maybe 5098/5112?
    public static class AcrylicHelper
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        [DllImport("dwmapi.dll")]
        static extern int DwmEnableBlurBehindWindow(IntPtr hwnd, ref DwmBlurBehind blurBehind);

        [DllImport("dwmapi.dll")]
        static extern int DwmFlush();

        [DllImport("user32.dll")]
        static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        struct Margins
        {
            public int Left;
            public int Right;
            public int Top;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct DwmBlurBehind
        {
            public uint Flags;
            public int Enable;
            public IntPtr BlurRegion;
            public int TransitionOnMaximized;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMSBT_NONE = 1;
        const int DWMSBT_ACRYLIC = 3;
        const int DWMWCP_ROUND = 2;
        const int DWM_BB_ENABLE = 1;
        const int WCA_ACCENT_POLICY = 19;
        const int ACCENT_DISABLED = 0;
        const int ACCENT_ENABLE_BLURBEHIND = 3;
        const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

        public static bool ApplyBackdrop(Window window, bool enabled, bool borderlessFullWindow = false)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero)
                {
                    EventHandler? applyWhenReady = null;
                    applyWhenReady = (_, _) =>
                    {
                        window.SourceInitialized -= applyWhenReady;
                        ApplyBackdrop(window, enabled, borderlessFullWindow);
                    };
                    window.SourceInitialized += applyWhenReady;
                    return false;
                }

                int corner = DWMWCP_ROUND;
                int dark = enabled ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

                int none = DWMSBT_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));
                var blurOff = new DwmBlurBehind { Flags = DWM_BB_ENABLE, Enable = 0 };
                DwmEnableBlurBehindWindow(hwnd, ref blurOff);

                SetAccent(hwnd, ACCENT_DISABLED, 0u);
                DwmFlush();
                int accentState = enabled ? ACCENT_ENABLE_ACRYLICBLURBEHIND : ACCENT_DISABLED;
                uint tint = enabled
                    ? (borderlessFullWindow ? 0x202B2B2Bu : 0x302B2B2Bu)
                    : 0u;
                bool accentApplied = SetAccent(hwnd, accentState, tint);

                var margins = new Margins();
                if (HwndSource.FromHwnd(hwnd) is { CompositionTarget: { } target })
                    target.BackgroundColor = Colors.Transparent;
                DwmExtendFrameIntoClientArea(hwnd, ref margins);
                window.InvalidateVisual();
                return accentApplied;
            }
            catch
            {
                return false;
            }
        }

        static bool SetAccent(IntPtr hwnd, int state, uint gradientColor)
        {
            var accent = new AccentPolicy
            {
                AccentState = state,
                AccentFlags = 0,
                GradientColor = gradientColor,
                AnimationId = 0,
            };
                int accentSize = Marshal.SizeOf<AccentPolicy>();
                IntPtr accentPointer = Marshal.AllocHGlobal(accentSize);
                try
                {
                    Marshal.StructureToPtr(accent, accentPointer, false);
                    var compositionData = new WindowCompositionAttributeData
                    {
                        Attribute = WCA_ACCENT_POLICY,
                        Data = accentPointer,
                        SizeOfData = accentSize,
                    };
                return SetWindowCompositionAttribute(hwnd, ref compositionData) != 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(accentPointer);
                }
        }
    }

    public static class Strings
    {
        public static AppLanguage Lang { get; set; } = AppLanguage.English;

        public static string Get(string key)
        {
            if (Lang == AppLanguage.Chinese && _cn.TryGetValue(key, out var cn)) return cn;  //cn
            if (Lang == AppLanguage.Polish && _pl.TryGetValue(key, out var pl)) return pl;  //pl
            return _en.TryGetValue(key, out var en) ? en : key;
        }

        static readonly Dictionary<string, string> _en = new() //the ai did the translations. sorry if they are poor.
        {
            ["title"] = "Strinowa v0.77.90",
            ["settings"] = "Settings",
            ["about"] = "About",
            ["launcher_theme"] = "LAUNCHER THEME",
            ["theme_dark"] = "Dark",
            ["theme_dark_sub"] = "Default",
            ["theme_midnight"] = "Midnight",
            ["theme_midnight_sub"] = "Pure black",
            ["theme_light"] = "Light",
            ["theme_light_sub"] = "Bright UI",
            ["theme_acrylic"] = "Acrylic",
            ["theme_acrylic_sub"] = "Blurred glass",
            ["terminal_color"] = "TERMINAL TEXT COLOR",
            ["term_note"] = "Text colors adapt to theme and build type.",
            ["term_a_label"] = "Adaptive White / Black",
            ["term_a_desc"] = "An Adaptive White/Black text to match the color",
            ["term_b_label"] = "Pink Accent",
            ["term_b_desc"] = "A pretty pink gradient",
            ["preview"] = "PREVIEW",
            ["preview_t1"] = "Text 1",
            ["preview_t2"] = "Text 2",
            ["preview_t3"] = "Text 3",
            ["preview_devkit"] = "Devkit",
            ["preview_deprecated"] = "Deprecated",
            ["language"] = "LANGUAGE",
            ["options"] = "OPTIONS",
            ["clear_log"] = "Clear log when download finishes",
            ["save_bruteforce"] = "Save bruteforce results to file",
            ["download_locations"] = "DOWNLOAD LOCATIONS",
            ["game_location"] = "GAME DOWNLOAD LOCATION",
            ["launcher_location"] = "LAUNCHER DOWNLOAD LOCATION",
            ["browse"] = "Browse",
            ["ask_download_location"] = "Ask every time where to download",
            ["apply"] = "Apply",
            ["cancel"] = "Cancel",
            ["ok"] = "OK",
            ["hint"] = "<Game_Branch>  <OS|CN|PC>  <version>  [-b]",
            ["downloading"] = "Downloading\u2026",
            ["about_ver"] = LauncherIdentity.AboutVersionEnglish,
            ["about_desc"] = "Strinova game client downloader and version manager.",
            ["about_credit"] = "by Cecilia \u00b7 zrobione dla Tosia \u2665",
            ["about_license"] = "Licensed under PolyForm Noncommercial 1.0",
            ["about_license_url"] = "https://polyformproject.org/licenses/noncommercial/1.0.0/",
            ["about_private"] = "This product is for private use only.",
            ["global_client"] = "Global Client",
            ["china_client"] = "China Client",
            ["builds"] = "builds",
            ["removed"] = "Deprecated",
            ["pause"] = "Pause",
            ["resume"] = "Resume",
            ["locating"] = "Locating Versions",
            ["identifying"] = "Identifying build types\u2026",
            ["compiling"] = "Compiling build list\u2026",
            ["fetching_manifest"] = "Fetching version manifest\u2026",
            ["fetching_sizes"] = "Fetching file sizes\u2026",
            ["report_bug"] = "Report a bug or an issue",
            ["speed_limit"] = "DOWNLOAD SPEED (KB/s)",
            ["window_size"] = "WINDOW SIZE",
            ["launcher_format"] = "LAUNCHER FORMAT",
            ["launcher_fmt_ask"] = "Ask each time",
            ["launcher_fmt_7z"] = "7z archive",
            ["launcher_fmt_exe"] = "EXE installer",
            ["launcher_fmt_both"] = "Both",
        };

        //cn
        static readonly Dictionary<string, string> _cn = new()
        {
            ["title"] = "Strinowa v0.77.90",
            ["settings"] = "\u8bbe\u7f6e",
            ["about"] = "\u5173\u4e8e",
            ["launcher_theme"] = "\u542f\u52a8\u5668\u4e3b\u9898",
            ["theme_dark"] = "\u6df1\u8272",
            ["theme_dark_sub"] = "\u9ed8\u8ba4",
            ["theme_midnight"] = "\u5348\u591c",
            ["theme_midnight_sub"] = "\u7eaf\u9ed1",
            ["theme_light"] = "\u6d45\u8272",
            ["theme_light_sub"] = "\u660e\u4eae\u754c\u9762",
            ["theme_acrylic"] = "\u4e9a\u514b\u529b",
            ["theme_acrylic_sub"] = "\u6a21\u7cca\u73bb\u7483",
            ["terminal_color"] = "\u7ec8\u7aef\u6587\u5b57\u989c\u8272",
            ["term_note"] = "\u6587\u5b57\u989c\u8272\u968f\u4e3b\u9898\u548c\u7248\u672c\u7c7b\u578b\u53d8\u5316\u3002",
            ["term_a_label"] = "\u81ea\u9002\u5e94 \u767d / \u9ed1",
            ["term_a_desc"] = "\u81ea\u9002\u5e94\u767d/\u9ed1\u6587\u5b57\uff0c\u914d\u5408\u4e3b\u9898\u989c\u8272",
            ["term_b_label"] = "\u7c89\u8272\u5f3a\u8c03",
            ["term_b_desc"] = "\u7cbe\u81f4\u7684\u7c89\u8272\u6e10\u53d8",
            ["preview"] = "\u9884\u89c8",
            ["preview_t1"] = "\u6587\u672c 1",
            ["preview_t2"] = "\u6587\u672c 2",
            ["preview_t3"] = "\u6587\u672c 3",
            ["preview_devkit"] = "\u5f00\u53d1\u5957\u4ef6",
            ["preview_deprecated"] = "\u5df2\u5e9f\u5f03",
            ["language"] = "\u8bed\u8a00",
            ["options"] = "\u9009\u9879",
            ["clear_log"] = "\u4e0b\u8f7d\u5b8c\u6210\u540e\u6e05\u9664\u65e5\u5fd7",
            ["save_bruteforce"] = "\u5c06\u66b4\u529b\u7834\u89e3\u7ed3\u679c\u4fdd\u5b58\u5230\u6587\u4ef6",
            ["download_locations"] = "\u4e0b\u8f7d\u4f4d\u7f6e",
            ["game_location"] = "\u6e38\u620f\u4e0b\u8f7d\u4f4d\u7f6e",
            ["launcher_location"] = "\u542f\u52a8\u5668\u4e0b\u8f7d\u4f4d\u7f6e",
            ["browse"] = "\u6d4f\u89c8",
            ["ask_download_location"] = "\u6bcf\u6b21\u8be2\u95ee\u4e0b\u8f7d\u4f4d\u7f6e",
            ["apply"] = "\u5e94\u7528",
            ["cancel"] = "\u53d6\u6d88",
            ["ok"] = "\u786e\u5b9a",
            ["hint"] = "<\u6e38\u620f\u5206\u652f>  <OS|CN|PC>  <\u7248\u672c>  [-b]",
            ["downloading"] = "\u6b63\u5728\u4e0b\u8f7d\u2026",
            ["about_ver"] = LauncherIdentity.AboutVersionChinese,
            ["about_desc"] = "\u5361\u62c9\u5f7c\u4e18\u6e38\u620f\u5ba2\u6237\u7aef\u4e0b\u8f7d\u5668\u548c\u7248\u672c\u7ba1\u7406\u5668\u3002",
            ["about_credit"] = "by Cecilia \u00b7 \u4e3a Tosia \u5236\u4f5c \u2665",
            ["about_license"] = "\u6388\u6743\u5e94\u7528 PolyForm \u975e\u5546\u4e1a 1.0 \u8bb8\u53ef",
            ["about_license_url"] = "https://polyformproject.org/licenses/noncommercial/1.0.0/",
            ["about_private"] = "\u672c\u4ea7\u54c1\u4ec5\u4f9b\u79c1\u4eba\u4f7f\u7528\u3002",
            ["global_client"] = "\u6d77\u5916\u5ba2\u6237\u7aef",
            ["china_client"] = "\u5361\u62c9\u5f7c\u5a03\u5ba2\u6237\u7aef",
            ["builds"] = "\u7248\u672c",
            ["removed"] = "\u5df2\u5e9f\u5f03",
            ["pause"] = "\u6682\u505c",
            ["resume"] = "\u7ee7\u7eed",
            ["locating"] = "\u5b9a\u4f4d\u7248\u672c\u2026",
            ["identifying"] = "\u8bc6\u522b\u7248\u672c\u7c7b\u578b\u2026",
            ["compiling"] = "\u7f16\u8f91\u7248\u672c\u5217\u8868\u2026",
            ["fetching_manifest"] = "\u83b7\u53d6\u6e05\u5355\u2026",
            ["fetching_sizes"] = "\u83b7\u53d6\u6587\u4ef6\u5927\u5c0f\u2026",
            ["report_bug"] = "\u62a5\u544a\u9519\u8bef\u6216\u95ee\u9898",
            ["speed_limit"] = "\u4e0b\u8f7d\u901f\u5ea6 (KB/s)",
            ["window_size"] = "\u7a97\u53e3\u5c3a\u5bf8",
            ["launcher_format"] = "\u542f\u52a8\u5668\u683c\u5f0f",
            ["launcher_fmt_ask"] = "\u6bcf\u6b21\u8be2\u95ee",
            ["launcher_fmt_7z"] = "7z \u538b\u7f29\u5305",
            ["launcher_fmt_exe"] = "EXE \u5b89\u88c5\u5305",
            ["launcher_fmt_both"] = "\u4e24\u8005",
        };

        //pl
        static readonly Dictionary<string, string> _pl = new()
        {
            ["title"] = "Strinowa v0.77.90",
            ["settings"] = "Ustawienia",
            ["about"] = "O programie",
            ["launcher_theme"] = "MOTYW LAUNCHERA",
            ["theme_dark"] = "Ciemny",
            ["theme_dark_sub"] = "Domy\u015blny",
            ["theme_midnight"] = "P\u00f3\u0142noc",
            ["theme_midnight_sub"] = "Czysta czer\u0144",
            ["theme_light"] = "Jasny",
            ["theme_light_sub"] = "Jasny interfejs",
            ["theme_acrylic"] = "Akryl",
            ["theme_acrylic_sub"] = "Rozmyte szk\u0142o",
            ["terminal_color"] = "KOLOR TEKSTU TERMINALA",
            ["term_note"] = "Kolory tekstu dopasowuj\u0105 si\u0119 do motywu i typu wersji.",
            ["term_a_label"] = "Adaptacyjny Bia\u0142y / Czarny",
            ["term_a_desc"] = "Adaptacyjny bia\u0142y/czarny tekst dopasowany do koloru",
            ["term_b_label"] = "R\u00f3\u017cowy akcent",
            ["term_b_desc"] = "\u0141adny r\u00f3\u017cowy gradient",
            ["preview"] = "PODGL\u0104D",
            ["preview_t1"] = "Tekst 1",
            ["preview_t2"] = "Tekst 2",
            ["preview_t3"] = "Tekst 3",
            ["preview_devkit"] = "Devkit",
            ["preview_deprecated"] = "Przestarza\u0142y",
            ["language"] = "J\u0118ZYK",
            ["options"] = "OPCJE",
            ["clear_log"] = "Wyczy\u015b\u0107 log po zako\u0144czeniu pobierania",
            ["save_bruteforce"] = "Zapisz wyniki bruteforce do pliku",
            ["download_locations"] = "LOKALIZACJE POBIERANIA",
            ["game_location"] = "LOKALIZACJA POBIERANIA GRY",
            ["launcher_location"] = "LOKALIZACJA POBIERANIA LAUNCHERA",
            ["browse"] = "Przegl\u0105daj",
            ["ask_download_location"] = "Pytaj za ka\u017cdym razem, gdzie pobra\u0107",
            ["apply"] = "Zastosuj",
            ["cancel"] = "Anuluj",
            ["ok"] = "OK",
            ["hint"] = "<Ga\u0142\u0105\u017a_Gry>  <OS|CN|PC>  <wersja>  [-b]",
            ["downloading"] = "Pobieranie\u2026",
            ["about_ver"] = LauncherIdentity.AboutVersionPolish,
            ["about_desc"] = "Pobieracz klienta gry Kalabijau i mened\u017cer wersji.",
            ["about_credit"] = "by Cecilia \u00b7 zrobione dla Tosia \u2665",
            ["about_license"] = "Licencja PolyForm Niekomercyjna 1.0",
            ["about_license_url"] = "https://polyformproject.org/licenses/noncommercial/1.0.0/",
            ["about_private"] = "Ten produkt jest przeznaczony wy\u0142\u0105cznie do prywatnego u\u017cytku.",
            ["global_client"] = "Klient Globalny",
            ["china_client"] = "Klient Chi\u0144ski",
            ["builds"] = "wersji",
            ["removed"] = "Przestarza\u0142y",
            ["pause"] = "Wstrzymaj",
            ["resume"] = "Wzn\u00F3w",
            ["locating"] = "Wyszukiwanie wersji\u2026",
            ["identifying"] = "Identyfikacja typ\u00f3w\u2026",
            ["compiling"] = "Kompilowanie listy\u2026",
            ["fetching_manifest"] = "Pobieranie manifestu\u2026",
            ["fetching_sizes"] = "Pobieranie rozmiar\u00f3w\u2026",
            ["report_bug"] = "Zg\u0142o\u015b b\u0142\u0105d lub problem",
            ["speed_limit"] = "PR\u0116DKO\u015a\u0106 POBIERANIA (KB/s)",
            ["window_size"] = "ROZMIAR OKNA",
            ["launcher_format"] = "FORMAT LAUNCHERA",
            ["launcher_fmt_ask"] = "Pytaj za ka\u017cdym razem",
            ["launcher_fmt_7z"] = "Archiwum 7z",
            ["launcher_fmt_exe"] = "Instalator EXE",
            ["launcher_fmt_both"] = "Oba",
        };
    }

    public static class AppSettings
    {
        public static bool ClearLogOnFinish { get; set; } = false;
        public static bool SaveBruteforceToFile { get; set; } = false;
        public static bool AcrylicEnabled { get; set; } = false;
        public static double AcrylicOpacity { get; set; } = 0.85;
        public static int SpeedLimitKBs { get; set; } = 51200;
        public static bool LauncherDefault7z { get; set; } = true;
        public static bool LauncherDefaultExe { get; set; } = false;
        public static bool ShowDevkits { get; set; } = false;
        public static string GameDownloadLocation { get; set; } = "Game";
        public static string LauncherDownloadLocation { get; set; } = "Launcher";
        public static bool AskDownloadLocation { get; set; } = false;
    }

    public static class AppTheme
    {
        public static LauncherTheme CurrentTheme { get; set; } = LauncherTheme.Dark;
        public static TermColorPreset CurrentTermPreset { get; set; } = TermColorPreset.AdaptiveWhiteBlack;

        public static void Apply(MainWindow w) => w.ApplyTheme();
        public static void ApplyToManifest(Manifest w) => w.ApplyTheme();

        public static void ApplyToAbout(About a)
        {
            bool isLight = CurrentTheme == LauncherTheme.Light;
            bool isMidnight = CurrentTheme == LauncherTheme.Midnight;
            bool isAcrylic = CurrentTheme == LauncherTheme.Acrylic;

            a.RootBorder.Background = isAcrylic ? B(0x18, 0x26, 0x26, 0x26) : isMidnight ? B(0, 0, 0) : isLight ? B(0xEC, 0xEC, 0xF4) : B(0x1A, 0x1A, 0x1F);
            a.RootBorder.BorderBrush = isAcrylic ? Brushes.Transparent : isLight ? B(0xB0, 0xB0, 0xC8) : B(0x2E, 0x2E, 0x38);
            a.RootBorder.Margin = new Thickness(0);
            a.RootBorder.BorderThickness = new Thickness(0);
            a.RootBorder.Effect = null;
            a.TitleBarBorder.Background = isAcrylic ? B(0x24, 0x30, 0x30, 0x30) : isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xE8) : B(0x11, 0x11, 0x15);
            a.TitleText.Foreground = isAcrylic ? B(0xF7, 0xFA, 0xFF) : isLight ? B(0x22, 0x22, 0x30) : B(0xDD, 0xDD, 0xDD);
            a.SepRect.Fill = isAcrylic ? B(0x38, 0xA8, 0xA8, 0xA8) : isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);
            a.AppVersion.Foreground = isAcrylic ? B(0xD0, 0xD0, 0xD0) : isLight ? B(0x55, 0x55, 0x77) : B(0x88, 0x88, 0x88);
            a.AppDesc.Foreground = isAcrylic ? B(0xF1, 0xF5, 0xFA) : isLight ? B(0x22, 0x22, 0x30) : B(0xCC, 0xCC, 0xCC);
            a.AppCredit.Foreground = isAcrylic ? B(0xD0, 0xD0, 0xD0) : isLight ? B(0x55, 0x55, 0x77) : B(0x88, 0x88, 0x88);
            a.FooterBorder.Background = isAcrylic ? B(0x30, 0x1C, 0x1C, 0x1C) : isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xEC) : B(0x0E, 0x0E, 0x16);
            a.FooterBorder.BorderBrush = isAcrylic ? B(0x38, 0xA8, 0xA8, 0xA8) : isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);
            a.AppIconGlass.Background = isAcrylic ? B(0x48, 0xE8, 0xE8, 0xE8) : Brushes.Transparent;
            a.AppIconGlass.BorderBrush = isAcrylic ? B(0xB0, 0xFF, 0xFF, 0xFF) : Brushes.Transparent;
            a.OkButton.Background = isAcrylic ? B(0xD8, 0xD9, 0x3F, 0x82) : B(0xDC, 0x32, 0x78);
            AcrylicHelper.ApplyBackdrop(a, isAcrylic, borderlessFullWindow: true);

            a.TitleText.Text = Strings.Get("about");
            a.AppVersion.Text = Strings.Get("about_ver");
            a.AppDesc.Text = Strings.Get("about_desc");
            a.AppCredit.Text = Strings.Get("about_credit");
        }

        public static SolidColorBrush GetNormalBrush() => TC.Normal;
        public static SolidColorBrush GetCNBrush() => TC.CN;

        static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
        static SolidColorBrush B(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));
    }

    public class DevkitWaveAnimator
    {
        readonly DispatcherTimer _timer;
        readonly TermColorPreset _preset;
        readonly TextBlock _target;
        double _phase = 0.0;
        string _text = "";

        public DevkitWaveAnimator(TermColorPreset preset, TextBlock target)
        {
            _preset = preset;
            _target = target;
            _text = target.Text;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += Tick;
            _timer.Start();
        }

        Color Hi() => _preset == TermColorPreset.PinkAccent
            ? Color.FromRgb(0xFF, 0x00, 0x4D) : Color.FromRgb(0xFF, 0xFF, 0xFF);
        Color Lo() => _preset == TermColorPreset.PinkAccent
            ? Color.FromRgb(0x61, 0x0E, 0x60) : Color.FromRgb(0x55, 0x55, 0x66);

        void Tick(object? s, EventArgs e)
        {
            _phase = (_phase + 0.12) % (Math.PI * 2);
            if (_text.Length == 0) return;

            _target.Inlines.Clear();
            for (int i = 0; i < _text.Length; i++)
            {
                double wave = (Math.Sin(_phase + i * 0.55) + 1.0) / 2.0;
                var hi = Hi(); var lo = Lo();
                var col = Color.FromRgb(
                    (byte)(lo.R + (hi.R - lo.R) * wave),
                    (byte)(lo.G + (hi.G - lo.G) * wave),
                    (byte)(lo.B + (hi.B - lo.B) * wave));
                _target.Inlines.Add(new System.Windows.Documents.Run(_text[i].ToString())
                {
                    Foreground = new SolidColorBrush(col),
                });
            }
        }

        public void Stop()
        {
            _timer.Stop();
            _target.Inlines.Clear();
            _target.Inlines.Add(new System.Windows.Documents.Run(_text));
        }
    }

    public class DevkitColorAnimator
    {
        static readonly Color[] _pinkPalette =
        {
            Color.FromRgb(0xFF, 0x00, 0x4D),
            Color.FromRgb(0xFF, 0x00, 0x77),
            Color.FromRgb(0xF0, 0x3A, 0x95),
            Color.FromRgb(0xB3, 0x17, 0x41),
            Color.FromRgb(0xB0, 0x33, 0xA3),
        };

        static readonly Color[] _adaptivePalette =
        {
            Color.FromRgb(0xFF, 0xFF, 0xFF),
            Color.FromRgb(0xCC, 0xCC, 0xCC),
            Color.FromRgb(0x88, 0x88, 0x99),
            Color.FromRgb(0xCC, 0xCC, 0xCC),
            Color.FromRgb(0xFF, 0xFF, 0xFF),
        };

        readonly SolidColorBrush _brush;
        readonly DispatcherTimer _timer;
        readonly Color[] _palette;
        int _index = 0;

        public SolidColorBrush Brush => _brush;

        public DevkitColorAnimator(TermColorPreset preset)
        {
            _palette = preset == TermColorPreset.PinkAccent ? _pinkPalette : _adaptivePalette;
            _brush = new SolidColorBrush(_palette[0]);
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(480) };
            _timer.Tick += Tick;
            _timer.Start();
        }

        void Tick(object? sender, EventArgs e)
        {
            _index = (_index + 1) % _palette.Length;
            _brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(_palette[_index], TimeSpan.FromMilliseconds(420))
                { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
        }

        public void Stop() => _timer.Stop();
    }

    public partial class SettingsWindow : Window
    {
        readonly MainWindow? _host;
        LauncherTheme _pendingTheme;
        TermColorPreset _pendingTermPreset;
        AppLanguage _pendingLang;
        DevkitWaveAnimator? _waveAnim;
        readonly List<BitmapImage> _acrylicPreviewBackgrounds = new();
        DispatcherTimer? _acrylicPreviewTimer;
        int _acrylicPreviewIndex;
        bool _previewBackgroundAIsActive = true;
        int _originalUiScale = 100;
        bool _scalePreviewReady;
        bool _settingsApplied;

        public SettingsWindow(MainWindow? host = null)
        {
            _host = host;
            InitializeComponent();
            ApplyUiScale(host?.CurrentUiScale ?? 100);
            Loaded += (_, _) =>
            {
                SyncWindowSettings();
                UpdateDevkitPreviewVisibility();
            };
            PreviewKeyDown += SettingsWindow_PreviewKeyDown;
            PreviewBorder.SizeChanged += (_, _) => UpdatePreviewClip();
            Closed += (_, _) =>
            {
                _waveAnim?.Stop();
                _acrylicPreviewTimer?.Stop();
                if (!_settingsApplied) _host?.ApplyUiScale(_originalUiScale);
            };
            _pendingTheme = AppTheme.CurrentTheme;
            _pendingTermPreset = AppTheme.CurrentTermPreset;
            _pendingLang = Strings.Lang;
            ClearLogCheck.IsChecked = AppSettings.ClearLogOnFinish;
            SaveBruteforceCheck.IsChecked = AppSettings.SaveBruteforceToFile;
            GameLocationBox.Text = string.IsNullOrWhiteSpace(AppSettings.GameDownloadLocation) ? "Game" : AppSettings.GameDownloadLocation;
            LauncherLocationBox.Text = string.IsNullOrWhiteSpace(AppSettings.LauncherDownloadLocation) ? "Launcher" : AppSettings.LauncherDownloadLocation;
            AskLocationCheck.IsChecked = AppSettings.AskDownloadLocation;
            SpeedBox.Text = AppSettings.SpeedLimitKBs.ToString();
            if (_host is MainWindow mwInit)
            {
                UiScaleSlider.Value = mwInit.CurrentUiScale;
                UiScaleValue.Text = $"{mwInit.CurrentUiScale}%";
            }
            ApplyWindowTheme();
            RefreshThemeSelection();
            RefreshTermColorSelection();
            RefreshLangSelection();
            LoadAcrylicPreviewBackgrounds();
            UpdatePreview();
            StartWavePreview();
            ApplyLangToUI();
        }

        public void ApplyUiScale(int percent)
        {
            WindowScale.Apply(this, RootBorder, percent, 460, 640);
        }

        void StartWavePreview()
        {
            _waveAnim?.Stop();
            if (!AppSettings.ShowDevkits) return;
            _waveAnim = new DevkitWaveAnimator(_pendingTermPreset, PreviewDevkit);
        }

        void UpdateDevkitPreviewVisibility()
        {
            PreviewDevkit.Visibility = AppSettings.ShowDevkits ? Visibility.Visible : Visibility.Collapsed;
            if (AppSettings.ShowDevkits) StartWavePreview();
            else
            {
                _waveAnim?.Stop();
                _waveAnim = null;
            }
        }

        void SettingsWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.D || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            e.Handled = true;
            _host?.EnableDevkits();
            UpdateDevkitPreviewVisibility();
        }

        void UpdatePreviewClip()
        {
            if (PreviewCarouselHost.ActualWidth <= 0 || PreviewCarouselHost.ActualHeight <= 0) return;
            PreviewCarouselHost.Clip = new RectangleGeometry(
                new Rect(0, 0, PreviewCarouselHost.ActualWidth, PreviewCarouselHost.ActualHeight), 6, 6);
        }

        void LoadAcrylicPreviewBackgrounds()
        {
            foreach (var fileName in new[] { "bg.png", "bg1.png", "bg.jpg" })
            {
                try
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri($"pack://application:,,,/{fileName}", UriKind.Absolute);
                    image.EndInit();
                    image.Freeze();
                    _acrylicPreviewBackgrounds.Add(image);
                }
                catch
                {
                }
            }

            if (_acrylicPreviewBackgrounds.Count > 0)
            {
                PreviewBackgroundA.Source = _acrylicPreviewBackgrounds[0];
                _acrylicPreviewIndex = 0;
                _previewBackgroundAIsActive = true;
            }

            _acrylicPreviewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _acrylicPreviewTimer.Tick += (_, _) => AdvanceAcrylicPreview();
            _acrylicPreviewTimer.Start();
        }

        void AdvanceAcrylicPreview()
        {
            if (_pendingTheme != LauncherTheme.Acrylic || _acrylicPreviewBackgrounds.Count < 2) return;

            _acrylicPreviewIndex = (_acrylicPreviewIndex + 1) % _acrylicPreviewBackgrounds.Count;
            var incoming = _previewBackgroundAIsActive ? PreviewBackgroundB : PreviewBackgroundA;
            var outgoing = _previewBackgroundAIsActive ? PreviewBackgroundA : PreviewBackgroundB;
            incoming.Source = _acrylicPreviewBackgrounds[_acrylicPreviewIndex];
            incoming.BeginAnimation(OpacityProperty, new DoubleAnimation(0.82, TimeSpan.FromMilliseconds(650))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            outgoing.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(650))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
            _previewBackgroundAIsActive = !_previewBackgroundAIsActive;
        }

        void UpdateAcrylicPreviewVisibility()
        {
            bool show = _pendingTheme == LauncherTheme.Acrylic;
            PreviewBackdropFallback.Opacity = show ? 1 : 0;
            PreviewAcrylicTint.Opacity = show ? 1 : 0;
            PreviewBackgroundA.BeginAnimation(OpacityProperty, null);
            PreviewBackgroundB.BeginAnimation(OpacityProperty, null);

            if (!show)
            {
                PreviewBackgroundA.Opacity = PreviewBackgroundB.Opacity = 0;
                return;
            }

            var active = _previewBackgroundAIsActive ? PreviewBackgroundA : PreviewBackgroundB;
            var inactive = _previewBackgroundAIsActive ? PreviewBackgroundB : PreviewBackgroundA;
            active.Opacity = _acrylicPreviewBackgrounds.Count > 0 ? 0.82 : 0;
            inactive.Opacity = 0;
        }

        // this does NOT update the preview pane bc thats UpdatePreview() its below here
        void SyncWindowSettings()
        {
            if (_host is MainWindow owner)
            {
                _scalePreviewReady = false;
                _originalUiScale = owner.CurrentUiScale;
                UiScaleSlider.Value = owner.CurrentUiScale;
                UiScaleValue.Text = $"{owner.CurrentUiScale}%";
                ApplyUiScale(owner.CurrentUiScale);
                _scalePreviewReady = true;
            }
        }
        void ApplyWindowTheme()
        {
            bool isLight = AppTheme.CurrentTheme == LauncherTheme.Light;
            bool isMidnight = AppTheme.CurrentTheme == LauncherTheme.Midnight;
            bool isAcrylic = AppTheme.CurrentTheme == LauncherTheme.Acrylic;

            var bg = isAcrylic ? B(0x20, 0x26, 0x26, 0x26) : isMidnight ? B(0, 0, 0) : isLight ? B(0xE8, 0xE8, 0xF0) : B(0x1A, 0x1A, 0x1F);
            var border = isAcrylic ? Brushes.Transparent : isLight ? B(0xB0, 0xB0, 0xC8) : B(0x2E, 0x2E, 0x38);
            var titleBg = isAcrylic ? B(0x2C, 0x30, 0x30, 0x30) : isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xE8) : B(0x11, 0x11, 0x15);
            var sep = isAcrylic ? B(0x38, 0xA8, 0xA8, 0xA8) : isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);
            var footBg = isAcrylic ? B(0x30, 0x1C, 0x1C, 0x1C) : isMidnight ? B(0, 0, 0) : isLight ? B(0xF5, 0xF5, 0xFF) : B(0x0E, 0x0E, 0x16);
            var textFg = isAcrylic ? B(0xF7, 0xFA, 0xFF) : isLight ? B(0x22, 0x22, 0x30) : B(0xDD, 0xDD, 0xDD);
            var subFg = isAcrylic ? B(0xD0, 0xD0, 0xD0) : isLight ? B(0x55, 0x55, 0x77) : B(0x88, 0x88, 0x88);
            var tileBg = isAcrylic ? B(0x30, 0x3A, 0x3A, 0x3A) : isMidnight ? B(0x08, 0x08, 0x08) : isLight ? B(0xE0, 0xE0, 0xEC) : B(0x13, 0x13, 0x1A);
            var pink = isAcrylic ? B(0xFF, 0x78, 0xAE) : B(0xDC, 0x32, 0x78);
            var accent = AppTheme.CurrentTermPreset == TermColorPreset.PinkAccent ? pink : (isLight ? B(0x22, 0x22, 0x30) : B(0xF0, 0xF0, 0xF0));

            Resources["SettingsControlBrush"] = isAcrylic ? B(0x30, 0x3A, 0x3A, 0x3A) : tileBg;
            Resources["SettingsControlHoverBrush"] = isAcrylic ? B(0x48, 0x58, 0x58, 0x58) : isLight ? B(0xC8, 0xC8, 0xD8) : B(0x33, 0x33, 0x41);
            Resources["SettingsControlPressedBrush"] = isAcrylic ? B(0x58, 0x2B, 0x2B, 0x2B) : isLight ? B(0xB8, 0xB8, 0xCA) : B(0x1C, 0x1C, 0x28);
            Resources["SettingsBorderBrush"] = border;
            Resources["SettingsTextBrush"] = textFg;
            Resources["SettingsMutedBrush"] = subFg;
            Resources["SettingsAccentBrush"] = accent;
            Resources["SettingsTrackBrush"] = isAcrylic ? B(0x70, 0x78, 0x78, 0x82)
                : isLight ? B(0xB8, 0xB8, 0xC8)
                : isMidnight ? B(0x28, 0x28, 0x32)
                : B(0x2E, 0x2E, 0x38);
            UiScaleSlider.Foreground = accent;
            SettingsScroll.Foreground = AppTheme.CurrentTermPreset == TermColorPreset.PinkAccent ? pink : (isLight ? B(0x55, 0x55, 0x77) : B(0x88, 0x88, 0xA0));

            AB(RootBorder, bg.Color, border.Color);
            RootBorder.Margin = new Thickness(0);
            RootBorder.BorderThickness = new Thickness(0);
            RootBorder.Effect = null;
            AB(TitleBarBorder, titleBg.Color);
            AT(TitleText, textFg.Color);
            AF(SepRect, sep.Color);
            AB(FooterBorder, footBg.Color, sep.Color);
            ScrollContent.Background = isAcrylic ? Brushes.Transparent : bg;
            AcrylicHelper.ApplyBackdrop(this, isAcrylic);

            AT(SectionTheme, accent.Color);
            AT(SectionTerminal, accent.Color);
            AT(SectionPreview, accent.Color);
            AT(SectionLang, accent.Color);
            AT(SectionOptions, accent.Color);
            AT(SectionLocations, accent.Color);
            AT(GameLocationLabel, subFg.Color);
            AT(LauncherLocationLabel, subFg.Color);

            AT(TerminalColorNote, (isAcrylic ? subFg : isLight ? B(0x55, 0x55, 0x77) : B(0x66, 0x66, 0x88)).Color);
            AT(ClearLogCheck, textFg.Color);
            AT(SaveBruteforceCheck, textFg.Color);
            AT(AskLocationCheck, textFg.Color);
            foreach (var box in new[] { GameLocationBox, LauncherLocationBox })
            {
                box.Foreground = textFg;
                box.CaretBrush = accent;
            }
            foreach (var button in new[] { BrowseGameLocationBtn, BrowseLauncherLocationBtn })
            {
                button.Background = tileBg;
                button.Foreground = textFg;
                button.BorderBrush = border;
            }
            ApplyBtn.Background = isAcrylic ? B(0xD8, 0xD9, 0x3F, 0x82) : B(0xDC, 0x32, 0x78);
            CancelBtn.Background = isAcrylic ? B(0x40, 0x42, 0x42, 0x42) : tileBg;
            CancelBtn.Foreground = textFg;

            AB(TermColorAInner, tileBg.Color);
            AB(TermColorBInner, tileBg.Color);
            AB(LangEnInner, tileBg.Color);
            AB(LangCnInner, tileBg.Color);  //cn
            AB(LangPlInner, tileBg.Color);  //pl

            AT(TermColorALabel, textFg.Color);
            AT(TermColorADesc, subFg.Color);
            AT(TermColorBDesc, B(0x88, 0x44, 0x68).Color);

            AT(LangEnLabel, textFg.Color); AT(LangEnSub, subFg.Color);
            AT(LangCnLabel, textFg.Color); AT(LangCnSub, subFg.Color);  //cn
            AT(LangPlLabel, textFg.Color); AT(LangPlSub, subFg.Color);  //pl
        }

        void UpdatePreview()
        {
            bool isLight = _pendingTheme == LauncherTheme.Light;
            bool isMidnight = _pendingTheme == LauncherTheme.Midnight;
            bool isAcrylic = _pendingTheme == LauncherTheme.Acrylic;

            var prevBg = isAcrylic ? Brushes.Transparent : isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xEC) : B(0x0E, 0x0E, 0x16);
            var border = isAcrylic ? Brushes.Transparent : isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);
            AB(PreviewBorder, prevBg.Color, border.Color);
            PreviewBorder.BorderThickness = isAcrylic ? new Thickness(0) : new Thickness(1);
            UpdateAcrylicPreviewVisibility();

            Color cT1, cT2, cT3, cDeprecated;

            if (_pendingTermPreset == TermColorPreset.PinkAccent)
            {
                // pink: Text1=#FF004D, Text2=#FF0077, Text3=#F03A95, Deprecated=#610E60
                cT1 = C(0xFF, 0x00, 0x4D);
                cT2 = C(0xFF, 0x00, 0x77);
                cT3 = C(0xF0, 0x3A, 0x95);
                cDeprecated = C(0x61, 0x0E, 0x60);
            }
            else if (isAcrylic)
            {
                cT1 = C(0xF7, 0xFA, 0xFF);
                cT2 = C(0xD7, 0xE0, 0xE9);
                cT3 = C(0xB6, 0xC2, 0xD0);
                cDeprecated = C(0x7E, 0x8B, 0x9A);
            }
            else if (isLight)
            {
                cT1 = C(0x22, 0x22, 0x33);
                cT2 = C(0x55, 0x55, 0x77);
                cT3 = C(0x77, 0x77, 0x99);
                cDeprecated = C(0xAA, 0xAA, 0xBB);
            }
            else
            {
                cT1 = C(0xF0, 0xF0, 0xF0);
                cT2 = C(0xBB, 0xBB, 0xBB);
                cT3 = C(0x99, 0x99, 0x99);
                cDeprecated = C(0x55, 0x55, 0x55);
            }

            AT(PreviewT1, cT1);
            AT(PreviewT2, cT2);
            AT(PreviewT3, cT3);
            AT(PreviewDeprecated, cDeprecated);
        }

        void ApplyLangToUI()
        {
            var tmp = Strings.Lang;
            Strings.Lang = _pendingLang;

            TitleText.Text = Strings.Get("settings");
            SectionTheme.Text = Strings.Get("launcher_theme");
            SectionTerminal.Text = Strings.Get("terminal_color");
            TerminalColorNote.Text = Strings.Get("term_note");
            SectionPreview.Text = Strings.Get("preview");
            SectionLang.Text = Strings.Get("language");
            SectionOptions.Text = Strings.Get("options");
            ClearLogCheck.Content = Strings.Get("clear_log");
            SaveBruteforceCheck.Content = Strings.Get("save_bruteforce");
            SectionLocations.Text = Strings.Get("download_locations");
            GameLocationLabel.Text = Strings.Get("game_location");
            LauncherLocationLabel.Text = Strings.Get("launcher_location");
            BrowseGameLocationBtn.Content = Strings.Get("browse");
            BrowseLauncherLocationBtn.Content = Strings.Get("browse");
            AskLocationCheck.Content = Strings.Get("ask_download_location");
            ApplyBtn.Content = Strings.Get("apply");
            CancelBtn.Content = Strings.Get("cancel");

            ThemeDarkLabel.Text = Strings.Get("theme_dark");
            ThemeDarkSub.Text = Strings.Get("theme_dark_sub");
            ThemeMidnightLabel.Text = Strings.Get("theme_midnight");
            ThemeMidnightSub.Text = Strings.Get("theme_midnight_sub");
            ThemeAcrylicLabel.Text = Strings.Get("theme_acrylic");
            ThemeAcrylicSub.Text = Strings.Get("theme_acrylic_sub");
            ThemeLightLabel.Text = Strings.Get("theme_light");
            ThemeLightSub.Text = Strings.Get("theme_light_sub");
            TermColorALabel.Text = Strings.Get("term_a_label");
            TermColorADesc.Text = Strings.Get("term_a_desc");
            TermColorBLabel.Text = Strings.Get("term_b_label");
            TermColorBDesc.Text = Strings.Get("term_b_desc");


            string devkitText = Strings.Get("preview_devkit");

            if (_waveAnim != null)
            {
                _waveAnim.Stop();
                PreviewDevkit.Text = devkitText;
                _waveAnim = new DevkitWaveAnimator(_pendingTermPreset, PreviewDevkit);
            }

            Strings.Lang = tmp;
        }



        void RefreshThemeSelection()
        {
            var dim = B(0x2E, 0x2E, 0x38); var accent = B(0xDC, 0x32, 0x78);
            ThemeDark.BorderBrush = ThemeMidnight.BorderBrush = ThemeAcrylic.BorderBrush = ThemeLight.BorderBrush = dim;
            switch (_pendingTheme)
            {
                case LauncherTheme.Dark: ThemeDark.BorderBrush = accent; break;
                case LauncherTheme.Midnight: ThemeMidnight.BorderBrush = accent; break;
                case LauncherTheme.Acrylic: ThemeAcrylic.BorderBrush = accent; break;
                case LauncherTheme.Light: ThemeLight.BorderBrush = accent; break;
            }
        }

        void RefreshTermColorSelection()
        {
            var dim = B(0x2E, 0x2E, 0x38); var accent = B(0xDC, 0x32, 0x78);
            TermColorA.BorderBrush = TermColorB.BorderBrush = dim;
            switch (_pendingTermPreset)
            {
                case TermColorPreset.AdaptiveWhiteBlack: TermColorA.BorderBrush = accent; break;
                case TermColorPreset.PinkAccent: TermColorB.BorderBrush = accent; break;
            }
        }

        void RefreshLangSelection()
        {
            var dim = B(0x2E, 0x2E, 0x38); var accent = B(0xDC, 0x32, 0x78);
            LangEn.BorderBrush = LangCn.BorderBrush = LangPl.BorderBrush = dim;
            switch (_pendingLang)
            {
                case AppLanguage.English: LangEn.BorderBrush = accent; break;
                case AppLanguage.Chinese: LangCn.BorderBrush = accent; break;
                case AppLanguage.Polish: LangPl.BorderBrush = accent; break;
            }
        }

        static void PulseBtn(Border el)
        {
            var st = new ScaleTransform(1, 1);
            el.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            el.RenderTransform = st;
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0.93, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)),
                new SineEase { EasingMode = EasingMode.EaseInOut }));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)),
                new SineEase { EasingMode = EasingMode.EaseOut }));
            st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        void ThemeDark_Click(object s, MouseButtonEventArgs e)
        { PulseBtn(ThemeDark); _pendingTheme = LauncherTheme.Dark; RefreshThemeSelection(); UpdatePreview(); }

        void ThemeMidnight_Click(object s, MouseButtonEventArgs e)
        { PulseBtn(ThemeMidnight); _pendingTheme = LauncherTheme.Midnight; RefreshThemeSelection(); UpdatePreview(); }

        void ThemeAcrylic_Click(object s, MouseButtonEventArgs e)
        { PulseBtn(ThemeAcrylic); _pendingTheme = LauncherTheme.Acrylic; RefreshThemeSelection(); UpdatePreview(); }

        void ThemeLight_Click(object s, MouseButtonEventArgs e)
        { PulseBtn(ThemeLight); _pendingTheme = LauncherTheme.Light; RefreshThemeSelection(); UpdatePreview(); }

        void TermColorA_Click(object s, MouseButtonEventArgs e)
        {
            PulseBtn(TermColorA);
            _pendingTermPreset = TermColorPreset.AdaptiveWhiteBlack;
            RefreshTermColorSelection();
            RestartWave();
            UpdatePreview();
        }

        void TermColorB_Click(object s, MouseButtonEventArgs e)
        {
            PulseBtn(TermColorB);
            _pendingTermPreset = TermColorPreset.PinkAccent;
            RefreshTermColorSelection();
            RestartWave();
            UpdatePreview();
        }

        void RestartWave()
        {
            _waveAnim?.Stop();
            PreviewDevkit.Inlines.Clear();
            PreviewDevkit.Text = Strings.Get("preview_devkit");
            _waveAnim = new DevkitWaveAnimator(_pendingTermPreset, PreviewDevkit);
        }

        void LangEn_Click(object s, MouseButtonEventArgs e) { PulseBtn(LangEn); _pendingLang = AppLanguage.English; RefreshLangSelection(); ApplyLangToUI(); }  //en
        void LangCn_Click(object s, MouseButtonEventArgs e) { PulseBtn(LangCn); _pendingLang = AppLanguage.Chinese; RefreshLangSelection(); ApplyLangToUI(); }  //cn
        void LangPl_Click(object s, MouseButtonEventArgs e) { PulseBtn(LangPl); _pendingLang = AppLanguage.Polish; RefreshLangSelection(); ApplyLangToUI(); }  //pl

        void ApplyBtn_Click(object s, RoutedEventArgs e)
        {
            var st = new ScaleTransform(1, 1);
            ApplyBtn.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            ApplyBtn.RenderTransform = st;
            var pa = new DoubleAnimationUsingKeyFrames();
            pa.KeyFrames.Add(new EasingDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70)), new SineEase { EasingMode = EasingMode.EaseInOut }));
            pa.KeyFrames.Add(new EasingDoubleKeyFrame(1.00, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)), new SineEase { EasingMode = EasingMode.EaseOut }));
            st.BeginAnimation(ScaleTransform.ScaleXProperty, pa);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, pa);

            AppTheme.CurrentTheme = _pendingTheme;
            AppTheme.CurrentTermPreset = _pendingTermPreset;
            Strings.Lang = _pendingLang;
            AppSettings.ClearLogOnFinish = ClearLogCheck.IsChecked == true;
            AppSettings.SaveBruteforceToFile = SaveBruteforceCheck.IsChecked == true;
            AppSettings.GameDownloadLocation = NormalizeLocation(GameLocationBox.Text, "Game");
            AppSettings.LauncherDownloadLocation = NormalizeLocation(LauncherLocationBox.Text, "Launcher");
            AppSettings.AskDownloadLocation = AskLocationCheck.IsChecked == true;
            if (int.TryParse(SpeedBox.Text.Trim(), out int spd) && spd >= 64)
                AppSettings.SpeedLimitKBs = spd;
            if (_host is MainWindow mwSize)
            {
                if (SizePresetBox.SelectedItem is ComboBoxItem preset)
                {
                    var dimensions = (preset.Tag?.ToString() ?? "900,560").Split(',');
                    if (dimensions.Length == 2 && int.TryParse(dimensions[0], out int ww) && int.TryParse(dimensions[1], out int wh))
                        mwSize.ApplyWindowPreset(ww, wh);
                }
                mwSize.ApplyUiScale((int)UiScaleSlider.Value);
            }

            _settingsApplied = true;
            _waveAnim?.Stop();
            _acrylicPreviewTimer?.Stop();
            Close();
        }

        static string NormalizeLocation(string? value, string fallback)
        {
            var normalized = (value ?? string.Empty).Trim().Trim('"');
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        void BrowseGameLocation_Click(object sender, RoutedEventArgs e) => BrowseFolder(GameLocationBox);
        void BrowseLauncherLocation_Click(object sender, RoutedEventArgs e) => BrowseFolder(LauncherLocationBox);

        static void BrowseFolder(TextBox target)
        {
            var initial = target.Text.Trim().Trim('"');
            if (!Directory.Exists(initial))
                initial = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select download folder",
                InitialDirectory = initial,
                Multiselect = false,
            };
            if (dialog.ShowDialog() == true)
                target.Text = dialog.FolderName;
        }

        void UiScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (UiScaleValue == null) return;
            UiScaleValue.Text = $"{(int)e.NewValue}%";
            var scale = new ScaleTransform(1, 1);
            UiScaleValue.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            UiScaleValue.RenderTransform = scale;
            var pulse = new DoubleAnimation(1.16, 1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } };
            scale.ScaleX = 1.16; scale.ScaleY = 1.16;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
            var sliderPulse = new DoubleAnimation(0.90, 1.0, TimeSpan.FromMilliseconds(140))
            { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } };
            UiScaleSlider.BeginAnimation(OpacityProperty, sliderPulse);

            ApplyUiScale((int)e.NewValue);
            if (_scalePreviewReady) _host?.ApplyUiScale((int)e.NewValue);
        }
        void CloseBtn_Click(object s, RoutedEventArgs e)
        {
            _waveAnim?.Stop();
            _acrylicPreviewTimer?.Stop();
            Close();
        }

        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        void AB(Border el, Color bg, Color? bd = null)
        {
            AnimBrush(el, Border.BackgroundProperty, bg);
            if (bd.HasValue) AnimBrush(el, Border.BorderBrushProperty, bd.Value);
        }
        void AF(Rectangle el, Color to)
        {
            var brush = MutableBrush(el.Fill, to);
            el.Fill = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
        }
        void AT(TextBlock el, Color to)
        {
            var brush = MutableBrush(el.Foreground, to);
            el.Foreground = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
        }
        void AT(ContentControl el, Color to)
        {
            var brush = MutableBrush(el.Foreground, to);
            el.Foreground = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
        }
        void AnimBrush(Border el, DependencyProperty prop, Color to)
        {
            var brush = MutableBrush(el.GetValue(prop) as Brush, to);
            el.SetValue(prop, brush);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
        }
        static SolidColorBrush MutableBrush(Brush? source, Color fallback)
        {
            return source is SolidColorBrush solid
                ? new SolidColorBrush(solid.Color) { Opacity = solid.Opacity }
                : new SolidColorBrush(fallback);
        }
        static ColorAnimation CA(Color to) => new(to, TimeSpan.FromMilliseconds(220))
        { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };

        static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
        static SolidColorBrush B(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));
        static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    }
}
