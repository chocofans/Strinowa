using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StrinowaWPF
{
    public enum LauncherTheme { Dark, Midnight, Light }

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
        const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        public static void EnableAcrylic(Window w)
        {
            try { var hwnd = new WindowInteropHelper(w).Handle; int v = 4; DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref v, sizeof(int)); }
            catch { }
        }
        public static void DisableAcrylic(Window w)
        {
            try { var hwnd = new WindowInteropHelper(w).Handle; int v = 0; DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref v, sizeof(int)); }
            catch { }
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
            ["title"] = "Strinova v0.70 Beta 1",
            ["settings"] = "Settings",
            ["about"] = "About",
            ["launcher_theme"] = "LAUNCHER THEME",
            ["theme_dark"] = "Dark",
            ["theme_dark_sub"] = "Default",
            ["theme_midnight"] = "Midnight",
            ["theme_midnight_sub"] = "Pure black",
            ["theme_light"] = "Light",
            ["theme_light_sub"] = "Bright UI",
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
            ["apply"] = "Apply",
            ["cancel"] = "Cancel",
            ["ok"] = "OK",
            ["hint"] = "<Game_Branch>  <OS|CN|PC|QQ>  <version>  [-b | -lb]",
            ["downloading"] = "Downloading\u2026",
            ["about_ver"] = "Version 0.70 Beta 1 (Build 0.70.0254.20260626.0939)",
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
            ["title"] = "Strinowa v0.70 Beta 1",
            ["settings"] = "\u8bbe\u7f6e",
            ["about"] = "\u5173\u4e8e",
            ["launcher_theme"] = "\u542f\u52a8\u5668\u4e3b\u9898",
            ["theme_dark"] = "\u6df1\u8272",
            ["theme_dark_sub"] = "\u9ed8\u8ba4",
            ["theme_midnight"] = "\u5348\u591c",
            ["theme_midnight_sub"] = "\u7eaf\u9ed1",
            ["theme_light"] = "\u6d45\u8272",
            ["theme_light_sub"] = "\u660e\u4eae\u754c\u9762",
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
            ["apply"] = "\u5e94\u7528",
            ["cancel"] = "\u53d6\u6d88",
            ["ok"] = "\u786e\u5b9a",
            ["hint"] = "<\u6e38\u620f\u5206\u652f>  <OS|CN|PC|QQ>  <\u7248\u672c>  [-b | -lb]",
            ["downloading"] = "\u6b63\u5728\u4e0b\u8f7d\u2026",
            ["about_ver"] = "\u7248\u672c 0.70 Beta 1\uff08\u5185\u90e8\u7248\u672c 0.70.0254.20260626.0939\uff09",
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
            ["title"] = "Strinowa v0.70 Beta 1",
            ["settings"] = "Ustawienia",
            ["about"] = "O programie",
            ["launcher_theme"] = "MOTYW LAUNCHERA",
            ["theme_dark"] = "Ciemny",
            ["theme_dark_sub"] = "Domy\u015blny",
            ["theme_midnight"] = "P\u00f3\u0142noc",
            ["theme_midnight_sub"] = "Czysta czer\u0144",
            ["theme_light"] = "Jasny",
            ["theme_light_sub"] = "Jasny interfejs",
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
            ["apply"] = "Zastosuj",
            ["cancel"] = "Anuluj",
            ["ok"] = "OK",
            ["hint"] = "<Ga\u0142\u0105\u017a_Gry>  <OS|CN|PC|QQ>  <wersja>  [-b | -lb]",
            ["downloading"] = "Pobieranie\u2026",
            ["about_ver"] = "Wersja 0.70 Beta 1 (Build 0.70.0254.20260626.0939)",
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
            ["resume"] = "Wznów",
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
        public static bool LauncherFormatAsked { get; set; } = false;
        public static bool LauncherDefault7z { get; set; } = true;
        public static bool LauncherDefaultExe { get; set; } = false;
    }

    public static class AppTheme
    {
        public static LauncherTheme CurrentTheme { get; set; } = LauncherTheme.Dark;
        public static TermColorPreset CurrentTermPreset { get; set; } = TermColorPreset.AdaptiveWhiteBlack;

        public static void Apply(MainWindow w) => w.ApplyTheme();

        public static void ApplyToAbout(AboutWindow a)
        {
            bool isLight = CurrentTheme == LauncherTheme.Light;
            bool isMidnight = CurrentTheme == LauncherTheme.Midnight;

            a.RootBorder.Background = isMidnight ? B(0, 0, 0) : isLight ? B(0xEC, 0xEC, 0xF4) : B(0x1A, 0x1A, 0x1F);
            a.RootBorder.BorderBrush = isLight ? B(0xB0, 0xB0, 0xC8) : B(0x2E, 0x2E, 0x38);
            a.TitleBarBorder.Background = isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xE8) : B(0x11, 0x11, 0x15);
            a.TitleText.Foreground = isLight ? B(0x22, 0x22, 0x30) : B(0xDD, 0xDD, 0xDD);
            a.SepRect.Fill = isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);
            a.AppVersion.Foreground = isLight ? B(0x55, 0x55, 0x77) : B(0x88, 0x88, 0x88);
            a.AppDesc.Foreground = isLight ? B(0x22, 0x22, 0x30) : B(0xCC, 0xCC, 0xCC);
            a.AppCredit.Foreground = isLight ? B(0x55, 0x55, 0x77) : B(0x88, 0x88, 0x88);
            a.FooterBorder.Background = isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xEC) : B(0x0E, 0x0E, 0x16);
            a.FooterBorder.BorderBrush = isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);

            a.TitleText.Text = Strings.Get("about");
            a.AppVersion.Text = Strings.Get("about_ver");
            a.AppDesc.Text = Strings.Get("about_desc");
            a.AppCredit.Text = Strings.Get("about_credit");
        }

        public static SolidColorBrush GetNormalBrush() => TC.Normal;
        public static SolidColorBrush GetCNBrush() => TC.CN;

        static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
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
        // Pink palette: #FF004D → #FF0077 → #F03A95 → wave down to #B31741 → #B033A3
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
        LauncherTheme _pendingTheme;
        TermColorPreset _pendingTermPreset;
        AppLanguage _pendingLang;
        DevkitWaveAnimator? _waveAnim;

        public SettingsWindow()
        {
            InitializeComponent();
            _pendingTheme = AppTheme.CurrentTheme;
            _pendingTermPreset = AppTheme.CurrentTermPreset;
            _pendingLang = Strings.Lang;
            ClearLogCheck.IsChecked = AppSettings.ClearLogOnFinish;
            SaveBruteforceCheck.IsChecked = AppSettings.SaveBruteforceToFile;
            SpeedBox.Text = AppSettings.SpeedLimitKBs.ToString();
            if (Owner is MainWindow mwInit)
            {
                WinWidthBox.Text = ((int)(mwInit.Width - 16)).ToString();
                WinHeightBox.Text = ((int)(mwInit.Height - 16)).ToString();
            }
            RefreshFmtSelection();
            ApplyWindowTheme();
            RefreshThemeSelection();
            RefreshTermColorSelection();
            RefreshLangSelection();
            UpdatePreview();
            StartWavePreview();
            ApplyLangToUI();
        }

        void StartWavePreview()
        {
            _waveAnim?.Stop();
            _waveAnim = new DevkitWaveAnimator(_pendingTermPreset, PreviewDevkit);
        }

        // this does NOT update the preview pane bc thats UpdatePreview() its below here
        void ApplyWindowTheme()
        {
            bool isLight = AppTheme.CurrentTheme == LauncherTheme.Light;
            bool isMidnight = AppTheme.CurrentTheme == LauncherTheme.Midnight;

            var bg = isMidnight ? B(0, 0, 0) : isLight ? B(0xE8, 0xE8, 0xF0) : B(0x1A, 0x1A, 0x1F);
            var border = isLight ? B(0xB0, 0xB0, 0xC8) : B(0x2E, 0x2E, 0x38);
            var titleBg = isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xE8) : B(0x11, 0x11, 0x15);
            var sep = isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);
            var footBg = isMidnight ? B(0, 0, 0) : isLight ? B(0xF5, 0xF5, 0xFF) : B(0x0E, 0x0E, 0x16);
            var textFg = isLight ? B(0x22, 0x22, 0x30) : B(0xDD, 0xDD, 0xDD);
            var subFg = isLight ? B(0x55, 0x55, 0x77) : B(0x88, 0x88, 0x88);
            var tileBg = isMidnight ? B(0x08, 0x08, 0x08) : isLight ? B(0xE0, 0xE0, 0xEC) : B(0x13, 0x13, 0x1A);
            var pink = B(0xDC, 0x32, 0x78);

            AB(RootBorder, bg.Color, border.Color);
            AB(TitleBarBorder, titleBg.Color);
            AT(TitleText, textFg.Color);
            AF(SepRect, sep.Color);
            AB(FooterBorder, footBg.Color, sep.Color);
            ScrollContent.Background = bg;

            AT(SectionTheme, pink.Color);
            AT(SectionTerminal, pink.Color);
            AT(SectionPreview, pink.Color);
            AT(SectionLang, pink.Color);
            AT(SectionOptions, pink.Color);

            AT(TerminalColorNote, (isLight ? B(0x55, 0x55, 0x77) : B(0x66, 0x66, 0x88)).Color);
            AT(ClearLogCheck, (isLight ? B(0x22, 0x22, 0x30) : B(0xCC, 0xCC, 0xCC)).Color);
            AT(SaveBruteforceCheck, (isLight ? B(0x22, 0x22, 0x30) : B(0xCC, 0xCC, 0xCC)).Color);

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

            var prevBg = isMidnight ? B(0, 0, 0) : isLight ? B(0xD8, 0xD8, 0xEC) : B(0x0E, 0x0E, 0x16);
            var border = isLight ? B(0xB0, 0xB0, 0xC8) : B(0x22, 0x22, 0x30);
            AB(PreviewBorder, prevBg.Color, border.Color);

            Color cT1, cT2, cT3, cDeprecated;

            if (_pendingTermPreset == TermColorPreset.PinkAccent)
            {
                // pink: Text1=#FF004D, Text2=#FF0077, Text3=#F03A95, Deprecated=#610E60
                cT1 = C(0xFF, 0x00, 0x4D);
                cT2 = C(0xFF, 0x00, 0x77);
                cT3 = C(0xF0, 0x3A, 0x95);
                cDeprecated = C(0x61, 0x0E, 0x60);
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
            ApplyBtn.Content = Strings.Get("apply");
            CancelBtn.Content = Strings.Get("cancel");

            ThemeDarkLabel.Text = Strings.Get("theme_dark");
            ThemeDarkSub.Text = Strings.Get("theme_dark_sub");
            ThemeMidnightLabel.Text = Strings.Get("theme_midnight");
            ThemeMidnightSub.Text = Strings.Get("theme_midnight_sub");
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
            ThemeDark.BorderBrush = ThemeMidnight.BorderBrush = ThemeLight.BorderBrush = dim;
            switch (_pendingTheme)
            {
                case LauncherTheme.Dark: ThemeDark.BorderBrush = accent; break;
                case LauncherTheme.Midnight: ThemeMidnight.BorderBrush = accent; break;
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
            if (int.TryParse(SpeedBox.Text.Trim(), out int spd) && spd >= 64)
                AppSettings.SpeedLimitKBs = spd;
            if (int.TryParse(WinWidthBox.Text.Trim(), out int ww) && ww >= 560 &&
                int.TryParse(WinHeightBox.Text.Trim(), out int wh) && wh >= 380 &&
                Owner is MainWindow mwSize)
            {
                mwSize.Width = ww + 16;
                mwSize.Height = wh + 16;
            }

            if (Owner is MainWindow mw)
                mw.ApplyTheme();

            _waveAnim?.Stop();
            Close();
        }

        void FmtAsk_Click(object s, MouseButtonEventArgs e)
        {
            AppSettings.LauncherFormatAsked = false;
            RefreshFmtSelection();
        }
        void Fmt7z_Click(object s, MouseButtonEventArgs e)
        {
            AppSettings.LauncherFormatAsked = true;
            AppSettings.LauncherDefault7z = true;
            AppSettings.LauncherDefaultExe = false;
            RefreshFmtSelection();
        }
        void FmtExe_Click(object s, MouseButtonEventArgs e)
        {
            AppSettings.LauncherFormatAsked = true;
            AppSettings.LauncherDefault7z = false;
            AppSettings.LauncherDefaultExe = true;
            RefreshFmtSelection();
        }

        void RefreshFmtSelection()
        {
            var dim = B(0x2E, 0x2E, 0x38);
            var accent = B(0xDC, 0x32, 0x78);
            FmtAsk.BorderBrush = dim;
            Fmt7z.BorderBrush = dim;
            FmtExe.BorderBrush = dim;
            if (!AppSettings.LauncherFormatAsked) FmtAsk.BorderBrush = accent;
            else if (AppSettings.LauncherDefault7z && !AppSettings.LauncherDefaultExe) Fmt7z.BorderBrush = accent;
            else if (!AppSettings.LauncherDefault7z && AppSettings.LauncherDefaultExe) FmtExe.BorderBrush = accent;
        }

        void CloseBtn_Click(object s, RoutedEventArgs e) { _waveAnim?.Stop(); Close(); }

        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        }

        // Helpers for animated color transitions on chrome elements
        void AB(Border el, Color bg, Color? bd = null)
        {
            AnimBrush(el, Border.BackgroundProperty, bg);
            if (bd.HasValue) AnimBrush(el, Border.BorderBrushProperty, bd.Value);
        }
        void AF(Rectangle el, Color to)
        {
            if (el.Fill is SolidColorBrush b) b.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
            else el.Fill = new SolidColorBrush(to);
        }
        void AT(TextBlock el, Color to)
        {
            if (el.Foreground is SolidColorBrush b) b.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
            else el.Foreground = new SolidColorBrush(to);
        }
        void AT(ContentControl el, Color to)
        {
            if (el.Foreground is SolidColorBrush b) b.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
            else el.Foreground = new SolidColorBrush(to);
        }
        void AnimBrush(Border el, DependencyProperty prop, Color to)
        {
            if (el.GetValue(prop) is SolidColorBrush b) b.BeginAnimation(SolidColorBrush.ColorProperty, CA(to));
            else el.SetValue(prop, new SolidColorBrush(to));
        }
        static ColorAnimation CA(Color to) => new(to, TimeSpan.FromMilliseconds(220))
        { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };

        static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
        static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    }
}