using StrinowaWPF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;

namespace StrinowaWPF
{
    public static class TC
    {
        // These are theme-shifted at runtime by ApplyTheme() do not freeze
        public static SolidColorBrush Normal = new(Color.FromRgb(0xF0, 0xF0, 0xF0));
        public static SolidColorBrush CN = new(Color.FromRgb(0xBB, 0xBB, 0xBB));
        public static SolidColorBrush Release = new(Color.FromRgb(0x99, 0x99, 0x99));
        public static SolidColorBrush Deprecated = new(Color.FromRgb(0x55, 0x55, 0x55));
        // animated wave brush driven by DevkitColorAnimator do not freeze
        public static SolidColorBrush Devkit = new(Color.FromRgb(0xFF, 0x00, 0x4D));

        // some of these arent used
        public static readonly SolidColorBrush Dim = new(Color.FromRgb(0x88, 0x88, 0x88));
        public static readonly SolidColorBrush Dev = new(Color.FromRgb(0xB4, 0x28, 0x78));
        public static readonly SolidColorBrush Removed = new(Color.FromRgb(0x8C, 0x1E, 0x5A));
        public static readonly SolidColorBrush Pink = new(Color.FromRgb(0xDC, 0x32, 0x78));
        public static readonly SolidColorBrush Info = new(Color.FromRgb(0xC8, 0x40, 0x80));
        public static readonly SolidColorBrush Ok = new(Color.FromRgb(0x55, 0xCC, 0x88));
        public static readonly SolidColorBrush Warn = new(Color.FromRgb(0xFF, 0xCC, 0x44));
        public static readonly SolidColorBrush Purple = new(Color.FromRgb(0x8C, 0x00, 0xC8));
        public static readonly SolidColorBrush Bold = new(Color.FromRgb(0xFF, 0xFF, 0xFF));
        public static readonly SolidColorBrush BrfOk = new(Color.FromRgb(0xFF, 0x60, 0x8C));
        public static readonly SolidColorBrush BrfMiss = new(Color.FromRgb(0x8C, 0x1E, 0x5A));
    }

    record VersionItem(string RelPath, string Branch, string Version, string Url, string Dest)
    {
        public long? Size { get; set; }
    }

    class Config
    {
        public int Width { get; set; } = 900;
        public int Height { get; set; } = 560;
        public string Theme { get; set; } = "Dark";
        public string Color { get; set; } = "AdaptiveWhiteBlack";
        public string Lang { get; set; } = "English";
        public bool ClearLog { get; set; } = false;
        public bool SaveBrf { get; set; } = false;
        public int SpeedKBs { get; set; } = 51200;
        public bool FmtAsked { get; set; } = false;
        public bool Fmt7z { get; set; } = true;
        public bool FmtExe { get; set; } = false;

        static readonly string Path = System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "conf.ini");

        public static Config Load()
        {
            var cfg = new Config();
            if (!File.Exists(Path)) { cfg.Save(); return cfg; }
            foreach (var line in File.ReadAllLines(Path))
            {
                var ln = line.Trim();
                if (ln.StartsWith(';') || ln.StartsWith('#') || !ln.Contains('=')) continue;
                var idx = ln.IndexOf('=');
                var key = ln[..idx].Trim().ToLowerInvariant();
                var val = ln[(idx + 1)..].Trim();
                if (key == "width" && int.TryParse(val, out int w)) cfg.Width = Math.Max(560, w);
                if (key == "height" && int.TryParse(val, out int h)) cfg.Height = Math.Max(380, h);
                if (key == "theme") cfg.Theme = val;
                if (key == "color") cfg.Color = val;
                if (key == "lang") cfg.Lang = val;
                if (key == "clearlog" && bool.TryParse(val, out bool cl)) cfg.ClearLog = cl;
                if (key == "savebrf" && bool.TryParse(val, out bool sb)) cfg.SaveBrf = sb;
                if (key == "speedkbs" && int.TryParse(val, out int sp)) cfg.SpeedKBs = Math.Max(64, sp);
                if (key == "fmtasked" && bool.TryParse(val, out bool fa)) cfg.FmtAsked = fa;
                if (key == "fmt7z" && bool.TryParse(val, out bool f7)) cfg.Fmt7z = f7;
                if (key == "fmtexe" && bool.TryParse(val, out bool fe)) cfg.FmtExe = fe;
            }
            return cfg;
        }

        public void Save()
        {
            File.WriteAllText(Path,
                $"; strinowa downloader - conf.ini\n" +
                $"width={Width}\nheight={Height}\n" +
                $"theme={Theme}\ncolor={Color}\nlang={Lang}\n" +
                $"clearlog={ClearLog}\nsavebrf={SaveBrf}\n" +
                $"speedkbs={SpeedKBs}\n" +
                $"fmtasked={FmtAsked}\nfmt7z={Fmt7z}\nfmtexe={FmtExe}\n");
        }
    }

    record TermSpan(string Text, SolidColorBrush Color, bool Bold = false);

    public partial class MainWindow : Window
    {
        const string OS_ROOT = "https://resource-download.strinova.com/Client/Win/GameDepot";
        const string CN_ROOT = "https://klbq-cdn-1300343128.cos.ap-shanghai.tencentcos.cn/Client/Win/GameDepot";

        const string PC_ROOT = "https://klbqcp-client-cdn.gxpan.cn/Client/Win/GameDepot";
        const string QQ_ROOT = "https://down.klbq.qq.com/Client/Win/GameDepot";

        //const string CN_ROOT2 = "https://klbq-cdn-1300343128.cos.ap-shanghai.myqcloud.com"
        //const string OS_ROOT2 = "https://klbq-overseas-cdn-1251001060.cos.ap-guangzhou.myqcloud.com/"
        //const string PM_ROOT = "http://192.168.16.77/PMGame"

        // PM is internally used in CN. uncomment once you have IDreamSky Feishu VPN
        // should be referenced elsewhere. If i did it right. lol although claude hopefully didnt fck it?
        // youll add "pm" to the valid source list in DispatchAsync and bruteforce source selectors
        // pmroot will still manifest differently. It might lack PAKs

        //OTHER
        //klbqcm-pack.dl.gxpan.cn
        //klbqcp-tiyan-dir.gxpan.cn:11711
        //2026-06-21

        static readonly HttpClient Http = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxConnectionsPerServer = 32,
        })
        { Timeout = TimeSpan.FromSeconds(30) };

        Config _cfg = new();
        bool _busy = false;
        string _currentHint = "<channel>  <OS|CN|PC|QQ>  <version>  [-b | -lb]";
        readonly List<string> _history = new();
        int _histIdx = -1;

        long _dlTotal = 0;
        long _dlDone = 0;
        int _dlFilesTotal = 0;
        int _dlFilesDone = 0;
        long _dlBytes = 0;
#pragma warning disable CS0414
        long _dlBytesLast = 0; // <-- DO NOT REMOVE IT FUCKS EVERYTHING
        DateTime _dlStart;
        System.Windows.Threading.DispatcherTimer? _dlTimer;

        SemaphoreSlim? _promptSem;
        bool _promptResult;
        string _promptMode = ""; // input
        string _promptInputResult = "";

        bool SameVersions(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        DevkitColorAnimator _devkitAnim = new(TermColorPreset.AdaptiveWhiteBlack);

        public MainWindow()
        {
            InitializeComponent();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Strinowa-WPF-Downloader/0.70-Beta1");
            TC.Devkit = _devkitAnim.Brush;
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _cfg = Config.Load();
            Width = _cfg.Width + 16;
            Height = _cfg.Height + 16;

            if (Enum.TryParse<LauncherTheme>(_cfg.Theme, out var t)) AppTheme.CurrentTheme = t;
            if (Enum.TryParse<TermColorPreset>(_cfg.Color, out var c)) AppTheme.CurrentTermPreset = c;
            if (Enum.TryParse<AppLanguage>(_cfg.Lang, out var l)) Strings.Lang = l;
            AppSettings.ClearLogOnFinish = _cfg.ClearLog;
            AppSettings.SaveBruteforceToFile = _cfg.SaveBrf;
            AppSettings.SpeedLimitKBs = _cfg.SpeedKBs;
            AppSettings.LauncherFormatAsked = _cfg.FmtAsked;
            AppSettings.LauncherDefault7z = _cfg.Fmt7z;
            AppSettings.LauncherDefaultExe = _cfg.FmtExe;

            ApplyTheme();
            InputHint.Text = Strings.Get("hint");
            InputBox.Focus();
            ShowHeader();
            _isFirstOpen = false;
        }

        void ShowHeader()
        {
            ClearTerminal();
        }

        void AppendGradientBar()
        {
            var para = new Paragraph { Margin = new Thickness(0), LineHeight = 18 };
            var spans = new (Color c, int len)[]
            {
                (Color.FromRgb(0xDC,0x1E,0x50), 20),
                (Color.FromRgb(0xC0,0x10,0x78), 20),
                (Color.FromRgb(0xA0,0x08,0xA0), 20),
                (Color.FromRgb(0x8C,0x00,0xC8), 20),
            };
            foreach (var (c, len) in spans)
            {
                var run = new Run(new string('═', len))
                {
                    Foreground = new SolidColorBrush(c),
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = 13,
                };
                para.Inlines.Add(run);
            }
            TerminalBox.Document.Blocks.Add(para);
        }

        void ClearTerminal()
        {
            TerminalBox.Document.Blocks.Clear();
            _hasStatusLine = false;
        }

        public void ApplyTheme()
        {
            bool isLight = AppTheme.CurrentTheme == LauncherTheme.Light;
            bool isMidnight = AppTheme.CurrentTheme == LauncherTheme.Midnight;
            bool isPink = AppTheme.CurrentTermPreset == TermColorPreset.PinkAccent;

            // aurora
            // if (AppTheme.CurrentTheme == LauncherTheme.MidnightAurora)
            // {
            //     _aurora ??= new AuroraAnimator(OuterBorder);
            //     ApplyTermColors(isLight: false, isPink);
            //     return;
            // }
            // _aurora?.Stop(); _aurora = null;

            // ADD GLASS

            // ADD ACRYLLIC

            Color bgCol, titleCol, sepCol, inputCol;
            switch (AppTheme.CurrentTheme)
            {
                case LauncherTheme.Dark:
                    bgCol = C(0x1A, 0x1A, 0x1F); titleCol = C(0x11, 0x11, 0x15);
                    sepCol = C(0x22, 0x22, 0x30); inputCol = C(0x13, 0x13, 0x1A);
                    break;
                case LauncherTheme.Midnight:
                    bgCol = C(0x00, 0x00, 0x00); titleCol = C(0x00, 0x00, 0x00);
                    sepCol = C(0x18, 0x18, 0x26); inputCol = C(0x00, 0x00, 0x00);
                    break;
                default: // Light
                    bgCol = C(0xE8, 0xE8, 0xF0); titleCol = C(0xD8, 0xD8, 0xE8);
                    sepCol = C(0xB0, 0xB0, 0xC8); inputCol = C(0xF5, 0xF5, 0xFF);
                    break;
            }
            var borderCol = isLight ? C(0xB0, 0xB0, 0xC8) : C(0x2E, 0x2E, 0x38);

            Animate(OuterBorder, Border.BackgroundProperty, bgCol);
            Animate(OuterBorder, Border.BorderBrushProperty, borderCol);
            Animate(TitleBarBorder, Border.BackgroundProperty, titleCol);
            AnimateRect(SepRect, sepCol);
            Animate(InputRowBorder, Border.BackgroundProperty, inputCol);
            Animate(InputRowBorder, Border.BorderBrushProperty, sepCol);

            var dlBgCol = isMidnight ? C(0x00, 0x00, 0x00)
                           : isLight ? C(0xE0, 0xE0, 0xEC)
                                        : C(0x0E, 0x0E, 0x16);
            var dlTrackCol = isMidnight ? C(0x10, 0x10, 0x18)
                           : isLight ? C(0xC0, 0xC0, 0xD4)
                                        : C(0x1E, 0x1E, 0x2A);
            Animate(DownloadPanel, Border.BackgroundProperty, dlBgCol);
            Animate(DownloadPanel, Border.BorderBrushProperty, sepCol);
            Animate(DlTrack, Border.BackgroundProperty, dlTrackCol);

            if (isPink)
            {
                //Text1=#FF004D, Text2=#FF0077, Text3=#F03A95, Devkit wave #B31741→#B033A3, Deprecated=#610E60 2026-06-18
                TC.Normal = B(0xFF, 0x00, 0x4D);
                TC.CN = B(0xFF, 0x00, 0x77);
                TC.Release = B(0xF0, 0x3A, 0x95);
                TC.Deprecated = B(0x61, 0x0E, 0x60);
                TerminalBox.Foreground = TC.Normal;

                DlLabel.Foreground = B(0xFF, 0x00, 0x4D);
                DlStats.Foreground = B(0xFF, 0x00, 0x77);
                DlSubLabel.Foreground = B(0xFF, 0x00, 0x77);
                DlEta.Foreground = B(0x61, 0x0E, 0x60);

                SetDlFillColors(C(0xFF, 0x00, 0x4D), C(0x61, 0x0E, 0x60));
            }
            else if (isLight)
            {
                TC.Normal = B(0x33, 0x33, 0x44);
                TC.CN = B(0x66, 0x66, 0x88);
                TC.Release = B(0x77, 0x77, 0x99);
                TC.Deprecated = B(0xAA, 0xAA, 0xBB);
                TerminalBox.Foreground = TC.Normal;

                DlLabel.Foreground = B(0x33, 0x33, 0x44);
                DlStats.Foreground = B(0x66, 0x66, 0x88);
                DlSubLabel.Foreground = B(0x66, 0x66, 0x88);
                DlEta.Foreground = B(0xAA, 0xAA, 0xBB);

                SetDlFillColors(C(0x22, 0x22, 0x33), C(0x88, 0x88, 0xAA));
            }
            else
            {
                TC.Normal = B(0xF0, 0xF0, 0xF0);
                TC.CN = B(0xBB, 0xBB, 0xBB);
                TC.Release = B(0x99, 0x99, 0x99);
                TC.Deprecated = B(0x55, 0x55, 0x55);
                TerminalBox.Foreground = TC.Normal;

                DlLabel.Foreground = B(0xF0, 0xF0, 0xF0);
                DlStats.Foreground = B(0xBB, 0xBB, 0xBB);
                DlSubLabel.Foreground = B(0xBB, 0xBB, 0xBB);
                DlEta.Foreground = B(0x77, 0x77, 0x77);

                SetDlFillColors(C(0xFF, 0xFF, 0xFF), C(0x88, 0x88, 0x88));
            }


            var titleFg = isLight ? B(0x22, 0x22, 0x30) : B(0xDD, 0xDD, 0xDD);
            var hintFg = isLight ? B(0x77, 0x77, 0x99) : B(0x40, 0x40, 0x55);
            var inputFg = isLight ? B(0x11, 0x11, 0x22) : B(0xE8, 0xE8, 0xE8);
            var btnFg = isLight ? B(0x22, 0x22, 0x30) : B(0xCC, 0xCC, 0xCC);

            TitleVersionText.Foreground = titleFg;
            InputHint.Foreground = hintFg;
            InputBox.Foreground = inputFg;
            ChevronBlock.Foreground = B(0xDC, 0x32, 0x78);
            MinimizeBtn.Foreground = btnFg;
            CloseBtn.Foreground = btnFg;
            InputHint.Text = Strings.Get("hint");
            TitleVersionText.Text = Strings.Get("title");

            // Since shit got fucky when i removed the old one, this replaces the old one with the new one.
            // run elements that hold a reference to this brush update automatically.
            _devkitAnim.Stop();
            _devkitAnim = new DevkitColorAnimator(AppTheme.CurrentTermPreset);
            TC.Devkit = _devkitAnim.Brush;

            // Recolor all existing terminal runs so theme change applies immediately
            RecolorTerminal();
        }

        // Walk all blocks in the terminal and re-apply the correct brush for each color identity
        void RecolorTerminal()
        {
            var brushMap = new Dictionary<SolidColorBrush, SolidColorBrush>
            {
                // map old instances to current TC brushes by color proximity is fragile —
                // instead we compare reference to known TC fields set before calling this
            };

            foreach (var block in TerminalBox.Document.Blocks.ToList())
            {
                if (block is BlockUIContainer buc && buc.Child is TextBlock tb)
                {
                    foreach (var inline in tb.Inlines.ToList())
                    {
                        if (inline is Run r && r.Foreground is SolidColorBrush rb)
                            r.Foreground = RemapBrush(rb);
                    }
                }
            }
        }

        SolidColorBrush RemapBrush(SolidColorBrush old)
        {
            // remap by approximate color identity — covers the most common cases
            var c = old.Color;
            if (IsClose(c, TC.Normal.Color)) return TC.Normal;
            if (IsClose(c, TC.CN.Color)) return TC.CN;
            if (IsClose(c, TC.Release.Color)) return TC.Release;
            if (IsClose(c, TC.Deprecated.Color)) return TC.Deprecated;
            return old;
        }

        static bool IsClose(Color a, Color b, int tol = 12) =>
            Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol;

        void SetDlFillColors(Color hi, Color lo)
        {
            if (DlFill.Background is LinearGradientBrush gb && gb.GradientStops.Count >= 3)
            {
                var dur = TimeSpan.FromMilliseconds(400);
                var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
                gb.GradientStops[0].BeginAnimation(GradientStop.ColorProperty,
                    new ColorAnimation(hi, dur) { EasingFunction = ease });
                gb.GradientStops[1].BeginAnimation(GradientStop.ColorProperty,
                    new ColorAnimation(lo, dur) { EasingFunction = ease });
                gb.GradientStops[2].BeginAnimation(GradientStop.ColorProperty,
                    new ColorAnimation(hi, dur) { EasingFunction = ease });
            }
        }

        static void Animate(Border el, DependencyProperty prop, Color to)
        {
            var dur = TimeSpan.FromMilliseconds(300);
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            var current = el.GetValue(prop);
            Color from = current is SolidColorBrush scb ? scb.Color : Colors.Transparent;
            var brush = new SolidColorBrush(from);
            el.SetValue(prop, brush);
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(to, dur) { EasingFunction = ease });
        }

        static void AnimateRect(System.Windows.Shapes.Rectangle el, Color to)
        {
            var dur = TimeSpan.FromMilliseconds(300);
            var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
            Color from = el.Fill is SolidColorBrush scb ? scb.Color : Colors.Transparent;
            var brush = new SolidColorBrush(from);
            el.Fill = brush;
            brush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(to, dur) { EasingFunction = ease });
        }

        static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
        static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));

        bool _isFirstOpen = true;

        void AppendLine(IEnumerable<TermSpan> spans, bool newline = true)
        {
            //  (UIElement — has RenderTransform)
            var tb = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 13,
                Margin = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
            };

            bool any = false;
            foreach (var sp in spans)
            {
                var run = new System.Windows.Documents.Run(sp.Text);
                // devkit brush
                run.Foreground = ReferenceEquals(sp.Color, TC.Devkit) ? TC.Devkit : sp.Color;
                if (sp.Bold) run.FontWeight = FontWeights.Bold;
                tb.Inlines.Add(run);
                any = true;
            }
            if (!any) tb.Inlines.Add(new System.Windows.Documents.Run(""));

            var dur = _isFirstOpen ? TimeSpan.FromMilliseconds(80) : TimeSpan.FromMilliseconds(120);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var xlate = new TranslateTransform(-16, 0);
            tb.RenderTransform = xlate;
            var slideAnim = new DoubleAnimation(0, dur) { EasingFunction = ease };
            xlate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

            var container = new BlockUIContainer(tb) { Margin = new Thickness(0) };
            TerminalBox.Document.Blocks.Add(container);
            ScrollToBottom();
        }

        void AppendText(string text, SolidColorBrush? color = null, bool bold = false)
            => AppendLine([new(text, color ?? TC.Normal, bold)]);

        void AppendClickableLine(IEnumerable<TermSpan> spans, string clickCommand)
        {
            var tb = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 13,
                Margin = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"Click to download: {clickCommand}",
            };
            bool any = false;
            foreach (var sp in spans)
            {
                var run = new System.Windows.Documents.Run(sp.Text) { Foreground = sp.Color };
                if (sp.Bold) run.FontWeight = FontWeights.Bold;
                tb.Inlines.Add(run);
                any = true;
            }
            if (!any) tb.Inlines.Add(new System.Windows.Documents.Run(""));

            var cmd = clickCommand;
            tb.MouseLeftButtonDown += (_, _) =>
            {
                if (!_busy)
                {
                    InputBox.Text = cmd;
                    InputBox.Focus();
                    InputBox.CaretIndex = cmd.Length;
                    InputHint.Visibility = Visibility.Collapsed;
                }
            };
            tb.MouseEnter += (_, _) =>
            {
                foreach (System.Windows.Documents.Run r in tb.Inlines)
                {
                    if (r.Foreground is SolidColorBrush b && b != TC.Removed && b != TC.Dim)
                    {
                        var lighter = new SolidColorBrush(b.Color);
                        lighter.Opacity = 0.75;
                        r.Tag = r.Foreground;
                        r.Foreground = lighter;
                    }
                }
            };
            tb.MouseLeave += (_, _) =>
            {
                foreach (System.Windows.Documents.Run r in tb.Inlines)
                {
                    if (r.Tag is Brush original)
                        r.Foreground = original;
                }
            };

            var dur = _isFirstOpen ? TimeSpan.FromMilliseconds(80) : TimeSpan.FromMilliseconds(120);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var xlate = new TranslateTransform(-16, 0);
            tb.RenderTransform = xlate;
            var slideAnim = new DoubleAnimation(0, dur) { EasingFunction = ease };
            xlate.BeginAnimation(TranslateTransform.XProperty, slideAnim);

            TerminalBox.Document.Blocks.Add(new BlockUIContainer(tb) { Margin = new Thickness(0) });
            ScrollToBottom();
        }

        async Task HandleClickDownload(ScanContext ctx, string version, string source)
        {
            var src = source.ToLower();
            if (!ctx.Maps.TryGetValue(src.ToUpper(), out var entry))
            {
                // try case-insensitive lookup
                foreach (var kv in ctx.Maps)
                {
                    if (kv.Key.ToLower() == src) { entry = kv.Value; break; }
                }
            }
            if (string.IsNullOrEmpty(entry.root))
            {
                AppendText($"  source '{src.ToUpper()}' not found in scan — re-run the scan.", TC.Warn);
                return;
            }
            AppendLine([]);
            AppendLine([
                new("  auto-download  ", TC.Dim),
                new(version, entry.types.GetValueOrDefault(version, "Devkit") == "Release" ? TC.Release : TC.Devkit, true),
                new($"  [{src.ToUpper()}]", TC.Dim),
            ]);
            // compose the command string the same way the manual path does and re-dispatch
            await RunCommandAsync($"{ctx.Branch} {src.ToUpper()} {version}");
        }

        BlockUIContainer? _loaderBlock = null;
        DispatcherTimer? _loaderTimer = null;
        double _loaderPhase = 0;

        void ShowLoader(string label = "")
        {
            var canvas = new System.Windows.Controls.Canvas
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(12, 4, 0, 4),
            };

            var dot1 = new System.Windows.Shapes.Ellipse
            { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromArgb(0xC0, 0xE1, 0x14, 0x62)) };
            var dot2 = new System.Windows.Shapes.Ellipse
            { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromArgb(0xC0, 0x6F, 0xCA, 0xDC)) };
            var dot3 = new System.Windows.Shapes.Ellipse
            { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromArgb(0xC0, 0x3D, 0xB8, 0x8F)) };
            var dot4 = new System.Windows.Shapes.Ellipse
            { Width = 6, Height = 6, Fill = new SolidColorBrush(Color.FromArgb(0xC0, 0xE9, 0xA9, 0x20)) };

            canvas.Children.Add(dot1);
            canvas.Children.Add(dot2);
            canvas.Children.Add(dot3);
            canvas.Children.Add(dot4);

            var row = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(0),
            };
            row.Children.Add(canvas);
            if (!string.IsNullOrEmpty(label))
            {
                row.Children.Add(new TextBlock
                {
                    Text = label,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = 13,
                    Foreground = TC.Dim,
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }

            _loaderBlock = new BlockUIContainer(row) { Margin = new Thickness(0) };
            TerminalBox.Document.Blocks.Add(_loaderBlock);
            ScrollToBottom();

            _loaderPhase = 0;
            _loaderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _loaderTimer.Tick += (_, _) =>
            {
                _loaderPhase += 0.08;
                double cx = 11, cy = 11, r = 10;
                double a1 = _loaderPhase, a2 = _loaderPhase + Math.PI, a3 = _loaderPhase + Math.PI / 2, a4 = _loaderPhase + 3 * Math.PI / 2;
                System.Windows.Controls.Canvas.SetLeft(dot1, cx + r * Math.Cos(a1) - 3);
                System.Windows.Controls.Canvas.SetTop(dot1, cy + r * Math.Sin(a1) - 3);
                System.Windows.Controls.Canvas.SetLeft(dot2, cx + r * Math.Cos(a2) - 3);
                System.Windows.Controls.Canvas.SetTop(dot2, cy + r * Math.Sin(a2) - 3);
                System.Windows.Controls.Canvas.SetLeft(dot3, cx + r * Math.Cos(a3) - 3);
                System.Windows.Controls.Canvas.SetTop(dot3, cy + r * Math.Sin(a3) - 3);
                System.Windows.Controls.Canvas.SetLeft(dot4, cx + r * Math.Cos(a4) - 3);
                System.Windows.Controls.Canvas.SetTop(dot4, cy + r * Math.Sin(a4) - 3);
            };
            _loaderTimer.Start();
        }

        void HideLoader()
        {
            _loaderTimer?.Stop();
            _loaderTimer = null;
            if (_loaderBlock != null)
            {
                TerminalBox.Document.Blocks.Remove(_loaderBlock);
                _loaderBlock = null;
            }
        }

        void ScrollToBottom() => TermScroll.ScrollToEnd();

        void SetHint(string hint)
        {
            _currentHint = hint;
            InputHint.Text = hint;
        }

        void InputBox_TextChanged(object s, TextChangedEventArgs e)
        {
            InputHint.Visibility = string.IsNullOrEmpty(InputBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        async void InputBox_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                if (_history.Count == 0) return;
                _histIdx = Math.Min(_histIdx + 1, _history.Count - 1);
                InputBox.Text = _history[_history.Count - 1 - _histIdx];
                InputBox.CaretIndex = InputBox.Text.Length;
                e.Handled = true; return;
            }
            if (e.Key == Key.Down)
            {
                if (_histIdx <= 0) { _histIdx = -1; InputBox.Text = ""; return; }
                _histIdx--;
                InputBox.Text = _history[_history.Count - 1 - _histIdx];
                InputBox.CaretIndex = InputBox.Text.Length;
                e.Handled = true; return;
            }
            if (e.Key != Key.Enter) return;

            var raw = InputBox.Text.Trim();
            InputBox.Text = "";
            InputHint.Visibility = Visibility.Visible;
            _histIdx = -1;

            if (!string.IsNullOrEmpty(raw))
                _history.Add(raw);

            if (_promptSem != null && _promptMode == "yesno")
            {
                AppendLine([new("> ", TC.Pink, true), new(raw, TC.Normal)]);
                var lo = raw.ToLower();
                _promptResult = lo is "y" or "yes";
                _promptSem.Release();
                return;
            }
            if (_promptSem != null && _promptMode == "input")
            {
                AppendLine([new("> ", TC.Pink, true), new(raw, TC.Normal)]);
                _promptInputResult = raw;
                _promptSem.Release();
                return;
            }

            if (_busy) return;
            AppendLine([new("> ", TC.Pink, true), new(raw, TC.Normal)]);
            await RunCommandAsync(raw);
        }

        async Task<bool> AskYesNo(string question, bool defaultNo = true)
        {
            var suffix = defaultNo ? "[y/N]" : "[Y/n]";
            AppendLine([new($"  {question} {suffix}", TC.Dim)]);
            SetHint("y / n");
            _promptSem = new SemaphoreSlim(0, 1);
            _promptMode = "yesno";
            await _promptSem.WaitAsync();
            _promptSem = null;
            _promptMode = "";
            SetHint(_currentHint);
            return _promptResult;
        }

        async Task<string> AskInput(string prompt)
        {
            AppendLine([new($"  {prompt}", TC.Info)]);
            SetHint("");
            _promptSem = new SemaphoreSlim(0, 1);
            _promptMode = "input";
            await _promptSem.WaitAsync();
            _promptSem = null;
            _promptMode = "";
            return _promptInputResult;
        }

        async Task RunCommandAsync(string raw)
        {
            _busy = true;
            ChevronBlock.Foreground = TC.Dim;
            try
            {
                await DispatchAsync(raw);
            }
            catch (Exception ex)
            {
                AppendText($"  error: {ex.Message}", TC.Warn);
            }
            finally
            {
                _busy = false;
                ChevronBlock.Foreground = TC.Pink;
                SetHint(Strings.Get("hint"));
            }
        }

        void UpdateLoaderProgress(int step, int total, string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (_loaderBlock?.Child is StackPanel row)
                {
                    // label is the second child (index 1)
                    if (row.Children.Count >= 2 && row.Children[1] is TextBlock tb)
                    {
                        tb.Text = text;
                    }
                }
            });
        }
        async Task DispatchAsync(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                AppendText("  no branch provided.", TC.Dim);
                return;
            }

            // cls / clear comment out 2026-06-10
            var rawLo = raw.Trim().ToLowerInvariant();
            if (rawLo is "cls" or "clear")
            {
                ClearTerminal();
                ShowHeader();
                return;
            }

            if (IsUrl(raw))
            {
                AppendText("  detected link mode", TC.Info);
                await LinkDownloaderAsync(raw);
                ShowHeader();
                return;
            }

            // 
            if (rawLo.StartsWith("dl "))
            {
                var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var dlVer = parts[1];
                    var dlSrc = parts[2].ToLower();
                    if (_lastScanCtx != null)
                    {
                        await HandleClickDownload(_lastScanCtx, dlVer, dlSrc);
                        return;
                    }
                }
                AppendText("  No scan context — run a scan first.", TC.Warn);
                return;
            }

            var (branch, hiddenVer, src, gameBrute, launcherBrute) = ParseBranchLine(raw);
            if (branch == null)
            {
                AppendText("  Invalid branch.", TC.Warn);
                ShowHeader();
                return;
            }

            if (launcherBrute)
            {
                await RunBruteforceLauncherAsync(branch);
                ShowHeader();
                return;
            }
            if (gameBrute)
            {
                await RunBruteforceAsync(branch);
                ShowHeader();
                return;
            }
            if (raw.Equals("help", StringComparison.OrdinalIgnoreCase) ||
            raw.Equals("?", StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp();
                return;
            }

            ShowHeader();
            var ctx = await ScanBranchAsync(branch, src, hiddenVer);
            if (ctx == null) { ShowHeader(); return; }

            SetHint("<version>  or  <branch>  or  <branch -b>  or  <branch -lb>");
            while (true)
            {
                var dlChoice = await AskInput("");
                if (string.IsNullOrWhiteSpace(dlChoice)) { ShowHeader(); break; }

                // allow commands inside the post-scan loop
                var dlLo = dlChoice.Trim().ToLowerInvariant();
                if (dlLo is "cls" or "clear") { ClearTerminal(); ShowHeader(); continue; }
                if (dlLo is "help" or "?") { ShowHelp(); continue; }
                if (dlLo is "branch" or "branches") { ShowBranches(); continue; }
                if (dlLo is "exit" or "back" or "q") { ShowHeader(); break; }

                if (Regex.IsMatch(dlChoice, @"^\d+(?:\.\d+){1,}$"))
                {
                    var version = dlChoice;
                    var priority = ctx.AllowedSource != null
                        ? new[] { ctx.AllowedSource.ToUpper() }
                        : new[] { "OS", "CN", "PC", "QQ" };

                    bool found = false;
                    foreach (var code in priority)
                    {
                        if (!ctx.Maps.TryGetValue(code, out var mapEntry)) continue;
                        var (baseRoot, vrs, types, _, _, choice) = mapEntry;
                        if (!vrs.Contains(version)) continue;
                        var pickedBranch = choice.GetValueOrDefault(version, ctx.Branch);
                        var buildType = types.GetValueOrDefault(version, "Devkit");
                        if (buildType == "Development") buildType = "Devkit";
                        var buildColor = buildType == "Release" ? TC.Release : TC.Devkit;
                        AppendLine([
                            new($"  Detected build type ({LinkLabel(baseRoot)}): ", TC.Dim),
                            new($"[{buildType}]", buildColor),
                            new($" (branch: {pickedBranch})", TC.Dim),
                        ]);
                        if (await AskYesNo($"Download {pickedBranch} {version} from {LinkLabel(baseRoot)}?", defaultNo: true))
                        {
                            ShowHeader();
                            await CeciliaDownloadAsync(baseRoot, pickedBranch, version, null);
                        }
                        else AppendText("  Cancelled.", TC.Dim);

                        if (!await AskYesNo("Download another build?", defaultNo: true)) return;
                        ShowHeader();
                        SetHint("<version>  or  <branch>  or  <branch -b>  or  <branch -lb>");
                        found = true;
                        break;
                    }
                    if (!found) AppendText("  Version not found in the current results.", TC.Warn);
                    continue;
                }

                var (nb, nv, ns, gb2, lb2) = ParseBranchLine(dlChoice);
                if (lb2 && nb != null) { await RunBruteforceLauncherAsync(nb); ShowHeader(); continue; }
                if (gb2 && nb != null) { await RunBruteforceAsync(nb); ShowHeader(); continue; }
                if (nb != null)
                {
                    ShowHeader();
                    ctx = await ScanBranchAsync(nb, ns, nv);
                    if (ctx == null) { ShowHeader(); break; }
                    SetHint("<version>  or  <branch>  or  <branch -b>  or  <branch -lb>");
                    continue;
                }

                AppendText("  Invalid input. Enter a version like '1.2.3.4', a branch like 'Game_Test OS', or add -b / -lb.", TC.Warn);
            }
        }

        void ShowBranches()
        {
            if (_lastScanCtx == null || _lastScanCtx.Maps.Count == 0)
            {
                AppendText("  No branch scan loaded.", TC.Warn);
                return;
            }

            AppendLine([]);
            AppendLine([new($"  Branches for {_lastScanCtx.Branch}:", TC.Bold, true)]);

            foreach (var kv in _lastScanCtx.Maps.OrderBy(k => k.Key))
            {
                var versions = kv.Value.vers;
                AppendLine([
                    new("  ", TC.Normal),
                    new(kv.Key.PadRight(4), TC.Release, true),
                    new($"  {versions.Count} version{(versions.Count == 1 ? "" : "s")}", TC.Dim),
                ]);
            }

            AppendLine([]);
        }

        void ShowHelp()
        {
            AppendLine([]);

            AppendLine(new TermSpan[]
            {
        new("  Strinowa Command Help", TC.Bold, true)
            });

            AppendLine([]);

            AppendLine(new TermSpan[]
            {
        new("  Game_<Channel> -b", TC.Release, true),
        new("  → Run ", TC.Dim),
        new("game bruteforce", TC.Info, true)
            });

            AppendLine(new TermSpan[]
            {
        new("  Launcher_<Channel> -lb", TC.Release, true),
        new("  → Run ", TC.Dim),
        new("launcher bruteforce", TC.Info, true)
            });

            AppendLine(new TermSpan[]
            {
        new("  Game_<Channel> CN", TC.Release, true),
        new("  → Scan ", TC.Dim),
        new("CN builds for that branch", TC.Info)
            });

            AppendLine(new TermSpan[]
            {
        new("  Game_<Channel> <Version>", TC.Release, true),
        new("  → Scan for a ", TC.Dim),
        new("hidden version", TC.Info, true),
        new(" on that branch", TC.Dim)
            });

            AppendLine([]);

            AppendLine(new TermSpan[]
            {
        new("  You can also paste a direct URL to download files.", TC.Dim)
            });

            AppendLine([]);
        }

        async Task<ScanContext?> ScanBranchAsync(string branch, string? allowedSource, string? hiddenVersion)
        {
            var tasks = new Dictionary<string, Task<(List<string> vers, Dictionary<string, HashSet<string>> revmap)>>
            {
                ["OS"] = allowedSource == null || allowedSource == "os"
                    ? (hiddenVersion != null
                        ? GetHiddenVersionsAsync(OS_ROOT, branch, hiddenVersion, quiet: false)
                        : GetVersionsAsync($"{OS_ROOT}/{branch}/manifest.txt"))
                    : Task.FromResult((new List<string>(), new Dictionary<string, HashSet<string>>())),
                ["CN"] = allowedSource == null || allowedSource == "cn"
                    ? (hiddenVersion != null
                        ? GetHiddenVersionsAsync(CN_ROOT, branch, hiddenVersion, quiet: allowedSource == null)
                        : GetVersionsAsync($"{CN_ROOT}/{branch}/manifest.txt"))
                    : Task.FromResult((new List<string>(), new Dictionary<string, HashSet<string>>())),
                ["PC"] = allowedSource == null || allowedSource == "pc"
                    ? (hiddenVersion != null
                        ? GetHiddenVersionsAsync(PC_ROOT, branch, hiddenVersion, quiet: allowedSource == null)
                        : GetVersionsAsync($"{PC_ROOT}/{branch}/manifest.txt"))
                    : Task.FromResult((new List<string>(), new Dictionary<string, HashSet<string>>())),
                ["QQ"] = allowedSource == null || allowedSource == "qq"
                    ? (hiddenVersion != null
                        ? GetHiddenVersionsAsync(QQ_ROOT, branch, hiddenVersion, quiet: allowedSource == null)
                        : GetVersionsAsync($"{QQ_ROOT}/{branch}/manifest.txt"))
                    : Task.FromResult((new List<string>(), new Dictionary<string, HashSet<string>>())),
            };

            int totalSteps = tasks.Count;
            int step = 0;

            ShowLoader(Strings.Get("locating"));
            foreach (var key in tasks.Keys.ToList())
            {
                step++;
                UpdateLoaderProgress(step, totalSteps, $"{Strings.Get("locating")} [{step}/{totalSteps}]");
                await tasks[key];
            }
            HideLoader();

            var results = tasks.ToDictionary(kv => kv.Key, kv => kv.Value.Result);

            bool anyFound = results.Values.Any(r => r.vers.Count > 0);
            if (!anyFound)
            {
                AppendText("  No versions found for the given branch on OS or CN or PC or QQ.", TC.Warn);
                return null;
            }

            var buildTasks = new Dictionary<string, Task<BuildResult>>();
            string? prevCode = null;
            BuildResult? prevResult = null;

            //foreach (var code in new[] { "OS", "CN", "PC", "QQ", PM })
            foreach (var code in new[] { "OS", "CN", "PC", "QQ" }) // priority order so dont change.
            {
                var (vers, revmap) = results[code];
                if (vers.Count == 0) continue;

                if (prevCode != null && prevResult != null
                    && vers.SequenceEqual(results[prevCode!].vers))
                {
                    var capturedResult = prevResult;
                    buildTasks[code] = Task.FromResult(capturedResult);
                }
                else
                {
                    var capturedRoot = code switch { "OS" => OS_ROOT, "CN" => CN_ROOT, "PC" => PC_ROOT, _ => QQ_ROOT };
                    var capturedRevmap = revmap;
                    var capturedVers = vers;
                    buildTasks[code] = BuildVersAsync(capturedRoot, branch, capturedVers, capturedRevmap);
                }
                prevCode = code;
            }
            if (buildTasks.Count > 0)
            {
                ShowLoader(Strings.Get("identifying"));
                await Task.WhenAll(buildTasks.Values);
                HideLoader();
            }
            SetStatusLine($"  {Strings.Get("compiling")}");
            await Task.Delay(200);
            ClearStatusLine();

            var maps = new Dictionary<string, (string root, List<string> vers, Dictionary<string, string> types,
                Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists, Dictionary<string, string> choice)>();

            foreach (var code in new[] { "OS", "CN", "PC", "QQ" })
            {
                var (vers, _) = results[code];
                if (vers.Count == 0) continue;
                var root = code switch { "OS" => OS_ROOT, "CN" => CN_ROOT, "PC" => PC_ROOT, _ => QQ_ROOT };
                if (buildTasks.TryGetValue(code, out var bt))
                {
                    var br = bt.Result;
                    maps[code] = (root, vers, br.Types, br.Dates, br.Exists, br.Choice);
                }
            }

            AppendLine([]);

            var os = results["OS"].vers;
            var cn = results["CN"].vers;
            var pc = results["PC"].vers;

            if ((allowedSource == null || allowedSource == "os")
                && os.Count > 0 && maps.ContainsKey("OS"))
            {
                var m = maps["OS"];
                PrintVersionGroup($"OS {branch}", os, m.types, m.dates, m.exists, branch, "OS");
            }
            if ((allowedSource == null || allowedSource == "pc" || allowedSource == "cn")
                && (pc.Count > 0 || cn.Count > 0))
            {
                if (pc.Count > 0 && maps.ContainsKey("PC"))
                {
                    var mpc = maps["PC"];
                    var merged = pc.Concat(cn).Distinct().OrderBy(v => v, new VersionComparer()).ToList();

                    var mergeTitle = $"{Strings.Get("china_client")} {(branch.StartsWith("Game_", StringComparison.OrdinalIgnoreCase) ? branch[5..] : branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher " + branch[9..] : branch)}";
                    AppendLine([]);
                    AppendLine([new($"  {mergeTitle} {Strings.Get("builds")} ({merged.Count}):", TC.Bold, true)]);

                    int colW = merged.Max(v => v.Length) + 2;
                    maps.TryGetValue("CN", out var mcnEntry);

                    foreach (var v in merged)
                    {
                        bool inCN = cn.Contains(v);

                        var type =
                            mcnEntry.types != null && mcnEntry.types.ContainsKey(v) ? mcnEntry.types[v] :
                            mpc.types.GetValueOrDefault(v, "Devkit");
                        if (type == "Development") type = "Devkit";

                        var date =
                            mcnEntry.dates != null && mcnEntry.dates.ContainsKey(v) ? mcnEntry.dates[v] :
                            mpc.dates.GetValueOrDefault(v);

                        var exists =
                            mpc.exists.ContainsKey(v)
                                ? mpc.exists[v]
                                : (mcnEntry.exists != null && mcnEntry.exists.ContainsKey(v)
                                    ? mcnEntry.exists[v] : true);

                        SolidColorBrush color;
                        if (!exists) color = TC.Deprecated;
                        else if (type == "Devkit") color = TC.Devkit;
                        else if (inCN) color = TC.Normal;
                        else color = TC.CN;

                        var dtStr = date.HasValue ? date.Value.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'") : "—";
                        var tag = !exists ? $"[{Strings.Get("removed")}]" : $"[{type}]";

                        var line = new List<TermSpan>
                        {
                            new("  ", TC.Normal),
                            new($"{v.PadRight(colW)}", color),
                            new($"  {tag,-15}", color),
                        };
                        if (exists)
                            line.Add(new($"  {dtStr}", TC.Dim));

                        var dlSrc = inCN ? "cn" : "pc";
                        AppendClickableLine(line, $"dl {v} {dlSrc}");
                    }
                }
                else if (cn.Count > 0 && maps.ContainsKey("CN"))
                {
                    var mcn = maps["CN"];
                    PrintVersionGroup(branch, cn, mcn.types, mcn.dates, mcn.exists, branch, "CN");
                }
            }

            maps = MergeSources(maps);
            var ctx = new ScanContext(branch, allowedSource, hiddenVersion, maps);
            _lastScanCtx = ctx;
            return ctx;
        }
        bool _hasStatusLine = false;
        ScanContext? _lastScanCtx = null;

        void SetStatusLine(string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_hasStatusLine)
                {
                    var tb0 = new TextBlock
                    {
                        FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                        FontSize = 13,
                        Foreground = TC.Dim,
                        Margin = new Thickness(0),
                    };
                    TerminalBox.Document.Blocks.Add(new BlockUIContainer(tb0) { Margin = new Thickness(0) });
                    _hasStatusLine = true;
                }

                if (TerminalBox.Document.Blocks.LastBlock is not BlockUIContainer buc) return;
                if (buc.Child is not TextBlock tb) return;
                tb.Inlines.Clear();
                tb.Inlines.Add(new System.Windows.Documents.Run("  " + text)
                {
                    Foreground = TC.Dim,
                });

                ScrollToBottom();
            });
        }
        static string PrettyBranchName(string branch, string source)
        {
            var region = source.ToUpper() is "OS" ? Strings.Get("global_client") : Strings.Get("china_client");
            var stripped = branch.StartsWith("Game_", StringComparison.OrdinalIgnoreCase) ? branch[5..]
                         : branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher " + branch[9..]
                         : branch;
            return $"{region} {stripped}";
        }

        void PrintVersionGroup(string label, List<string> versions, Dictionary<string, string> types,
            Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists, string branch,
            string source = "OS")
        {
            SolidColorBrush sourceColor = source.ToUpper() switch
            {
                "CN" => TC.Normal,
                "PC" => TC.CN,
                "QQ" => TC.CN,
                _ => TC.Release,
            };

            var prettyName = PrettyBranchName(branch, source);
            var buildsWord = Strings.Get("builds");

            AppendLine([]);
            AppendLine([new($"  {prettyName} {buildsWord} ({versions.Count}):", TC.Bold, true)]);

            var colW = versions.Max(v => v.Length) + 2;
            foreach (var v in versions)
            {
                var t = types.GetValueOrDefault(v, "Devkit");
                if (t == "Development") t = "Devkit";
                var removed = exists.TryGetValue(v, out var ex) && !ex;

                SolidColorBrush color;
                if (removed) color = TC.Deprecated;
                else if (t == "Devkit") color = TC.Devkit;
                else color = sourceColor;

                var dt = dates.GetValueOrDefault(v);
                var dtStr = dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'") : "—";
                var tag = removed ? $"[{Strings.Get("removed")}]" : $"[{t}]";

                var line = new List<TermSpan>
                {
                    new("  ", TC.Normal),
                    new($"{v.PadRight(colW)}", color),
                    new($"  {tag,-15}", color),
                };
                if (!removed)
                    line.Add(new($"  {dtStr}", TC.Dim));

                AppendClickableLine(line, $"dl {v} {source.ToLower()}");
            }
        }

        void ClearStatusLine()
        {
            Dispatcher.Invoke(() =>
            {
                if (_hasStatusLine && TerminalBox.Document.Blocks.Count > 0)
                {
                    TerminalBox.Document.Blocks.Remove(TerminalBox.Document.Blocks.LastBlock!);
                    _hasStatusLine = false;
                }
            });
        }


        async Task<BuildResult> BuildVersAsync(string baseRoot, string branch,
            List<string> versions, Dictionary<string, HashSet<string>> revmap)
        {
            var types = new Dictionary<string, string>();
            var dates = new Dictionary<string, DateTime?>();
            var exists = new Dictionary<string, bool>();
            var choice = new Dictionary<string, string>();
            var lk = new object();

            await Parallel.ForEachAsync(versions,
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                async (v, ct) =>
                {
                    if (branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase))
                    {
                        var url = $"{baseRoot}/{branch}/{v}/full_{v}.7z";
                        var (ok, lmts) = await ProbeUrlAsync(url);
                        lock (lk)
                        {
                            types[v] = "Release";
                            dates[v] = lmts;
                            exists[v] = ok;
                            choice[v] = branch;
                        }
                        return;
                    }

                    var cands = BranchCandidates(v, revmap, branch);
                    string? foundType = null;
                    DateTime? foundDate = null;
                    bool foundEx = false;
                    string? foundBranch = null;

                    foreach (var cand in cands)
                    {
                        try
                        {
                            if (cand.StartsWith("Launcher", StringComparison.OrdinalIgnoreCase))
                            {
                                var url = $"{baseRoot}/{cand}/{v}/full_{v}.7z";
                                var (ok, lmts) = await ProbeUrlAsync(url);
                                if (ok)
                                {
                                    foundType = "Release"; foundDate = lmts;
                                    foundEx = true; foundBranch = cand; break;
                                }
                            }
                            else
                            {
                                var url = $"{baseRoot}/{cand}/{v}/full_zip/manifest.txt";
                                using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                                if (resp.IsSuccessStatusCode)
                                {
                                    var body = await resp.Content.ReadAsStringAsync(ct);
                                    foundType = IsRelease(body) ? "Release" : "Devkit";
                                    foundDate = ParseLastMod(resp);
                                    foundEx = true;
                                    foundBranch = cand;
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                    lock (lk)
                    {
                        types[v] = foundType ?? "Devkit";
                        dates[v] = foundDate;
                        exists[v] = foundEx;
                        choice[v] = foundBranch ?? (cands.Count > 0 ? cands[0] : branch);
                    }
                });

            return new BuildResult(types, dates, exists, choice);
        }

        async Task CeciliaDownloadAsync(string baseRoot, string branch, string version, string? manifestTextOpt)
        {
            if (branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase))
            {
                // Launcher manifests list /Launcher/<ver>/..., but CDN downloads live directly under the launcher branch.
                var launcherName = branch["Launcher_".Length..];
                var installerPrefix = baseRoot.Equals(OS_ROOT, StringComparison.OrdinalIgnoreCase) ? "Strinova" : "Calabiyau";
                var exeFileName = $"{installerPrefix}_Installer_{launcherName}_{version}.exe";
                var lUrl7z = $"{baseRoot}/{branch}/{version}/full_{version}.7z";
                var lUrlExe = $"{baseRoot}/{branch}/{version}/{exeFileName}";
                var (has7z, _) = await ProbeUrlAsync(lUrl7z);
                var (hasExe, _) = await ProbeUrlAsync(lUrlExe);

                if (!has7z && !hasExe)
                {
                    AppendText($"  Launcher {version} is not available on this CDN.", TC.Warn);
                    return;
                }

                bool dl7z = false, dlExe = false;
                if (AppSettings.LauncherFormatAsked)
                {
                    dl7z = AppSettings.LauncherDefault7z && has7z;
                    dlExe = AppSettings.LauncherDefaultExe && hasExe;
                    if (!dl7z && !dlExe) dl7z = has7z;
                }
                else
                {
                    AppendLine([new($"  Launcher {version} — select format:", TC.Normal)]);
                    if (has7z) AppendLine([new("    [1]  7z archive", TC.Release)]);
                    if (hasExe) AppendLine([new("    [2]  EXE installer", TC.Release)]);
                    if (has7z && hasExe) AppendLine([new("    [3]  Both", TC.Dim)]);
                    var pick = (await AskInput("  choice >")).Trim();
                    dl7z = pick is "1" or "3" || (!hasExe && has7z);
                    dlExe = pick is "2" or "3" || (!has7z && hasExe);
                    if (!dl7z && !dlExe) { AppendText("  Cancelled.", TC.Dim); return; }
                }

                if (dl7z && has7z)
                {
                    var lSz = await HeadSizeAsync(lUrl7z);
                    if (!await AskYesNo($"Download {branch} {version} 7z ({FormatSize(lSz)})?", defaultNo: true))
                    { AppendText("  Cancelled.", TC.Dim); }
                    else
                    {
                        var dest = Path.Combine($"{branch}-{version}", $"full_{version}.7z");
                        Directory.CreateDirectory($"{branch}-{version}");
                        StartDlBar($"Downloading {version}.7z", lSz ?? 0, 1);
                        await DownloadFileAsync(lUrl7z, dest, null);
                        StopDlBar();
                        AppendText($"  Saved \u2192 {dest}", TC.Ok);
                    }
                }
                if (dlExe && hasExe)
                {
                    var eSz = await HeadSizeAsync(lUrlExe);
                    if (!await AskYesNo($"Download {branch} {version} EXE ({FormatSize(eSz)})?", defaultNo: true))
                    { AppendText("  Cancelled (EXE).", TC.Dim); }
                    else
                    {
                        var dest = Path.Combine($"{branch}-{version}", exeFileName);
                        Directory.CreateDirectory($"{branch}-{version}");
                        StartDlBar($"Downloading {exeFileName}", eSz ?? 0, 1);
                        await DownloadFileAsync(lUrlExe, dest, null);
                        StopDlBar();
                        AppendText($"  Saved \u2192 {dest}", TC.Ok);
                    }
                }

                if (AppSettings.ClearLogOnFinish) ClearTerminal();
                AppendText("  Launcher download complete.", TC.Ok);
                return;
            }



            string manifestText;
            if (manifestTextOpt == null)
            {
                var vmUrl = $"{baseRoot}/{branch}/{version}/full_zip/manifest.txt";
                AppendText($"  Fetching version manifest…", TC.Dim);
                try
                {
                    manifestText = await Http.GetStringAsync(vmUrl);
                }
                catch
                {
                    AppendText("  Version manifest not available.", TC.Warn); return;
                }
            }
            else manifestText = manifestTextOpt;

            var triples = ParseVersionManifest(manifestText);
            if (triples.Count == 0) { AppendText("  Manifest has no files.", TC.Warn); return; }

            var items = triples.Select(t =>
            {
                var url = $"{baseRoot}/{t.rel.TrimStart('/')}";
                var dest = Path.Combine($"{branch}-{version}", Path.GetFileName(t.rel));
                return new VersionItem(t.rel, t.branch, t.version, url, dest) { Size = null };
            }).ToList();

            AppendText("  Fetching file sizes…", TC.Dim);
            await Parallel.ForEachAsync(items,
                new ParallelOptions { MaxDegreeOfParallelism = 32 },
                async (it, _) => { it.Size = await HeadSizeAsync(it.Url); });

            long? grand = items.All(i => i.Size.HasValue) ? items.Sum(i => i.Size!.Value) : (long?)null;
            string szLabel = grand.HasValue ? FormatSize(grand.Value) : "unknown size";

            if (!await AskYesNo($"Download {branch} {version} ({szLabel})?", defaultNo: true))
            { AppendText("  Cancelled.", TC.Dim); return; }

            Directory.CreateDirectory($"{branch}-{version}");

            var pending = items.Where(it =>
                !File.Exists(it.Dest) ||
                (it.Size.HasValue && new FileInfo(it.Dest).Length != it.Size.Value)).ToList();

            long alreadyDone = items.Where(it => !pending.Contains(it) && it.Size.HasValue)
                                    .Sum(it => it.Size!.Value);

            _dlTotal = grand ?? 0;
            _dlDone = alreadyDone;
            _dlBytes = 0;
            _dlBytesLast = 0;
            _dlFilesTotal = items.Count;
            _dlFilesDone = items.Count - pending.Count;
            _dlStart = DateTime.UtcNow;

            StartDlBar($"Downloading: {version} ({szLabel})", grand ?? 0, items.Count);

            var sem = new SemaphoreSlim(4, 4);
            var dlLock = new object();
            var tasks = pending.Select(async it =>
            {
                await sem.WaitAsync();
                try
                {
                    bool downloaded = false;
                    while (!downloaded)
                    {
                        try
                        {
                            await DownloadFileAsync(it.Url, it.Dest, (bytes) =>
                            {
                                lock (dlLock)
                                {
                                    _dlDone += bytes;
                                    _dlBytes += bytes;
                                }
                            });
                            downloaded = true;
                            lock (dlLock) { _dlFilesDone++; }
                        }
                        catch
                        {
                            Dispatcher.Invoke(() =>
                                AppendText("  Download paused — connection failed. Press Enter to retry.", TC.Warn));
                            await AskInput("Press Enter to retry…");
                        }
                    }
                }
                finally { sem.Release(); }
            });

            await Task.WhenAll(tasks);
            StopDlBar();

            // verification for slow wifi. Add this as an option later in settings 2026-06-19
            var paks = items.Where(it => it.RelPath.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)).ToList();
            var mismatches = new List<string>();
            foreach (var it in paks)
            {
                var expected = it.Size;
                long actual = File.Exists(it.Dest) ? new FileInfo(it.Dest).Length : -1;
                if (expected.HasValue && actual != expected.Value)
                    mismatches.Add($"  {Path.GetFileName(it.Dest)}: expected {FormatSize(expected.Value)}, got {FormatSize(actual)}");
            }
            if (mismatches.Count > 0)
            {
                AppendText("  Pak size verification failed:", TC.Warn);
                foreach (var m in mismatches) AppendText(m, TC.Warn);
            }
            else AppendText($"  Pak size verification passed ({paks.Count} file(s)).", TC.Ok);

            if (AppSettings.ClearLogOnFinish) ClearTerminal();
            AppendText("  Your Strinowa client has downloaded.", TC.Ok);
        }
        void StartDlBar(string label, long total, int fileCount)
        {
            Dispatcher.Invoke(() =>
            {
                _dlTotal = total; _dlFilesDone = 0; _dlFilesTotal = fileCount;
                _dlDone = 0; _dlBytes = 0; _dlBytesLast = 0;
                _dlStart = DateTime.UtcNow;
                DlLabel.Text = label;
                DlStats.Text = "";
                DlSubLabel.Text = "";
                DlEta.Text = "";
                DlFill.Width = 0;
                DownloadPanel.Visibility = Visibility.Visible;

                _dlTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                _dlTimer.Tick += UpdateDlBar;
                _dlTimer.Start();
            });
        }

        double _dlFillWavePhase = 0;

        void UpdateDlBar(object? s, EventArgs e)
        {
            var elapsed = (DateTime.UtcNow - _dlStart).TotalSeconds;
            var speed = elapsed > 1.0 ? _dlBytes / elapsed : 0;
            var done = _dlDone;
            var total = _dlTotal;
            double frac = total > 0 ? Math.Min(1.0, (double)done / total) : -1;

            DlStats.Text = $"{_dlFilesDone}/{_dlFilesTotal} files";
            if (done > 0)
                DlSubLabel.Text = $"{FormatSize(done)} / {(total > 0 ? FormatSize(total) : "?")}   {FormatSize((long)speed)}/s";

            if (frac >= 0 && done > 0)
            {
                var trackW = Math.Max(0, DownloadPanel.ActualWidth - 28);
                DlFill.Width = Math.Max(0, frac * trackW);
                DlFill.Margin = new Thickness(0);
                var pct = (int)(frac * 100);
                DlLabel.Text = DlLabel.Text.Split('[')[0].TrimEnd() + $"  {pct}%";

                if (speed > 0 && total > done)
                {
                    var eta = TimeSpan.FromSeconds((total - done) / speed);
                    DlEta.Text = $"ETA {eta:mm\\:ss}";
                }

                // wave gradient on the fill bar
                _dlFillWavePhase = (_dlFillWavePhase + 0.06) % (Math.PI * 2);
                double wave = (Math.Sin(_dlFillWavePhase) + 1.0) / 2.0;
                var hiR = (byte)(0xDC + (int)((0xFF - 0xDC) * wave));
                var lo = (byte)(0x32 + (int)((0x80 - 0x32) * wave));
                if (DlFill.Background is LinearGradientBrush gb && gb.GradientStops.Count >= 2)
                {
                    gb.GradientStops[0].Color = Color.FromRgb(hiR, lo, 0x78);
                    gb.GradientStops[1].Color = Color.FromRgb(0x8C, (byte)(wave * 0x60), 0xC8);
                }
            }
            else if (done == 0)
            {
                // indeterminate bounce when total unknown or no bytes received yet
                var trackW = Math.Max(1, DownloadPanel.ActualWidth - 28 - 60);
                DlFill.Width = 60;
                var off = (int)(elapsed * 160) % (int)Math.Max(1, trackW);
                DlFill.Margin = new Thickness(off, 0, 0, 0);
            }
        }

        void StopDlBar()
        {
            Dispatcher.Invoke(() =>
            {
                _dlTimer?.Stop();
                _dlTimer = null;
                DownloadPanel.Visibility = Visibility.Collapsed;
                DlFill.Margin = new Thickness(0);
                DlFill.Width = 0;
            });
        }

        bool _isPaused = false;
        SemaphoreSlim _pauseSem = new(1, 1);

        public void TogglePause()
        {
            _isPaused = !_isPaused;
            if (!_isPaused) _pauseSem.Release();
        }

        async Task DownloadFileAsync(string url, string dest, Action<long>? onProgress)
        {
            var tmp = dest + ".part";
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            if (File.Exists(tmp)) File.Delete(tmp);
            await using var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
            await using var net = await resp.Content.ReadAsStreamAsync();
            var buf = new byte[1 << 16]; // 64 KB chunks for accurate throttle
            int read;
            long chunkBytes = 0;
            var chunkStart = DateTime.UtcNow;

            while ((read = await net.ReadAsync(buf)) > 0)
            {
                // pause support
                if (_isPaused)
                {
                    _pauseSem = new SemaphoreSlim(0, 1);
                    await _pauseSem.WaitAsync();
                }

                await fs.WriteAsync(buf.AsMemory(0, read));
                onProgress?.Invoke(read);
                chunkBytes += read;

                // speed throttle
                var limitBps = (long)AppSettings.SpeedLimitKBs * 1024;
                if (limitBps > 0)
                {
                    var elapsed = (DateTime.UtcNow - chunkStart).TotalSeconds;
                    var expectedSec = (double)chunkBytes / limitBps;
                    var wait = expectedSec - elapsed;
                    if (wait > 0.005)
                        await Task.Delay(TimeSpan.FromSeconds(wait));
                    if (chunkBytes >= limitBps)
                    {
                        chunkBytes = 0;
                        chunkStart = DateTime.UtcNow;
                    }
                }
            }
            fs.Close();
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
        }

        async Task LinkDownloaderAsync(string url)
        {
            AppendText("  Listing files…", TC.Dim);
            var (items, rootName) = await ListingAsync(url);
            if (items.Count == 0) { AppendText("  No files found.", TC.Warn); return; }
            Directory.CreateDirectory(rootName);
            StartDlBar($"Downloading {items.Count} files", 0, items.Count);
            var sem = new SemaphoreSlim(8, 8);
            await Parallel.ForEachAsync(items,
                new ParallelOptions { MaxDegreeOfParallelism = 8 },
                async (item, _) =>
                {
                    await sem.WaitAsync();
                    try
                    {
                        var path = new Uri(item.Url).AbsolutePath.TrimStart('/');
                        var dest = Path.Combine(rootName, path.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        await DownloadFileAsync(item.Url, dest, _ => { });
                        Interlocked.Increment(ref _dlFilesDone);
                    }
                    catch { }
                    finally { sem.Release(); }
                });
            StopDlBar();
            AppendText("  Download complete.", TC.Ok);
        }

        async Task<(List<(string Url, long? Size)> items, string rootName)> ListingAsync(string url)
        {
            var resp = await Http.GetStringAsync(url);
            var items = new List<(string, long?)>();
            var root = Regex.Replace(url.TrimEnd('/'), @"[^\w._-]", "_");

            if (resp.TrimStart().StartsWith("<") && (resp.Contains("<ListBucketResult") || resp.Contains("<Contents>")))
            {
                var xml = XDocument.Parse(resp);
                var ns = xml.Root?.Name.Namespace ?? XNamespace.None;
                var baseUri = new Uri(url);
                foreach (var c in xml.Descendants(ns + "Contents"))
                {
                    var key = c.Element(ns + "Key")?.Value ?? "";
                    var sz = long.TryParse(c.Element(ns + "Size")?.Value, out var s) ? s : (long?)null;
                    items.Add(($"{baseUri.Scheme}://{baseUri.Host}/{key.TrimStart('/')}", sz));
                }
            }
            else
            {
                var lines = resp.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
                foreach (var ln in lines)
                    items.Add((ln.StartsWith("http") ? ln : url.TrimEnd('/') + "/" + ln.TrimStart('/'), null));
            }
            return (items, root);
        }

        async Task RunBruteforceAsync(string branch)
        {
            AppendLine([]);
            AppendLine([new("", TC.Pink)]);
            AppendLine([new("Strinova BF v040", TC.Pink)]);
            AppendLine([new("", TC.Pink)]);
            AppendLine([]);

            var srcRaw = (await AskInput("<OS / CN / PC />")).Trim().ToLower();
            if (!new[] { "os", "cn", "pc", "qq" }.Contains(srcRaw)) //might as well remove QQ but claude said no
            {
                AppendLine([new("  invalid source — must be os, cn, pc or qq", TC.Warn)]);
                return;
            }
            var source = srcRaw;
            var isQq = source == "qq";

            var startV = (await AskInput("<version number 1>")).Trim();
            if (!IsValidVersion(startV))
            { AppendLine([new("  invalid version format", TC.Warn)]); return; }

            var finishV = (await AskInput("<version number 2>")).Trim();
            if (!IsValidVersion(finishV))
            { AppendLine([new("  invalid version format", TC.Warn)]); return; }

            var seq = BuildGameSeq(startV, finishV);
            int total = seq.Count, ok = 0, skip = 0, err = 0, done = 0;
            var found = new List<(string ver, string url)>();

            AppendLine([]);
            AppendLine([
                new("  branch  ", TC.Dim), new(branch,           TC.Release, true),
                new("  ·  ", TC.Dim),      new(source.ToUpper(), TC.Normal,  true),
                new($"  ·  {total} version{(total == 1 ? "" : "s")}  ·  {startV} → {finishV}", TC.Dim),
            ]);
            AppendLine([]);
            AppendLine([new("  version              status      url", TC.Dim)]);
            AppendLine([new("  " + new string('─', 66), TC.Dim)]);

            foreach (var (a, b, c, d) in seq)
            {
                var ver = $"{a}.{b}.{c}.{d}";
                var root = ChooseRootGame(source, (a, b, c, d));
                var url = $"{root}/{branch}/{ver}/full_zip/manifest.txt";
                done++;
                try
                {
                    using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    var body = await resp.Content.ReadAsStringAsync();
                    if (resp.IsSuccessStatusCode
                        && !body.Contains("NoSuchKey")
                        && !body.Contains("does not exist"))
                    {
                        ok++;
                        found.Add((ver, url));
                        AppendLine([
                            new($"  {ver,-22}", TC.Normal),
                            new("FOUND      ", TC.Ok, true),
                            new(url, TC.Dim),
                        ]);
                    }
                    else
                    {
                        SetStatusLine($"  [{done}/{total}]  {ver}  miss");
                    }
                }
                catch (TaskCanceledException)
                {
                    skip++;
                    AppendLine([
                        new($"  {ver,-22}", TC.Normal),
                        new("TIMEOUT    ", TC.Warn, true),
                        new("request timed out", TC.Dim),
                    ]);
                }
                catch (Exception ex)
                {
                    err++;
                    AppendLine([
                        new($"  {ver,-22}", TC.Normal),
                        new("ERROR      ", TC.Warn, true),
                        new(ex.Message.Length > 48 ? ex.Message[..48] : ex.Message, TC.Dim),
                    ]);
                }
                await Task.Delay(isQq ? 1000 : 200);
            }

            ClearStatusLine();
            AppendLine([]);
            AppendLine([new("  " + new string('─', 66), TC.Pink)]);
            AppendLine([
                new("  found   ", TC.Dim), new($"{ok}", TC.Ok, true),
                new($" / {total}", TC.Dim),
                new("    timeout  ", TC.Dim), new($"{skip}", skip > 0 ? TC.Warn : TC.Dim, skip > 0),
                new("    error  ", TC.Dim),  new($"{err}",  err  > 0 ? TC.Warn : TC.Dim, err  > 0),
            ]);
            AppendLine([]);

            if (AppSettings.SaveBruteforceToFile && found.Count > 0)
            {
                var shortVer = string.Join(".", startV.Split('.').Take(3));
                var fname = $"StrinowaF1-{source.ToUpper()}-{shortVer}.txt";
                try
                {
                    var lines = new System.Collections.Generic.List<string>
                    {
                        $"Strinowa Bruteforce — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        $"Branch: {branch}  Source: {source.ToUpper()}  Range: {startV} → {finishV}",
                        $"Found: {ok}/{total}  Timeouts: {skip}  Errors: {err}",
                        new string('─', 66),
                    };
                    foreach (var (v, u) in found)
                        lines.Add($"{v,-22}  {u}");
                    File.WriteAllLines(fname, lines, System.Text.Encoding.UTF8);
                    AppendLine([new("  saved  →  ", TC.Dim), new(fname, TC.Ok)]);
                }
                catch (Exception ex)
                {
                    AppendLine([new($"  save failed: {ex.Message}", TC.Warn)]);
                }
                AppendLine([]);
            }

            await AskYesNo("  Press Enter to continue");
        }
        Dictionary<string, (string root, List<string> vers, Dictionary<string, string> types,
    Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists,
    Dictionary<string, string> choice)>
MergeSources(Dictionary<string, (string root, List<string> vers, Dictionary<string, string> types,
    Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists,
    Dictionary<string, string> choice)> maps)
        {
            if (maps.ContainsKey("CN") && maps.ContainsKey("QQ"))
            {
                var cn = maps["CN"];
                var qq = maps["QQ"];
                if (cn.vers.Count == qq.vers.Count)
                    maps.Remove("QQ");
            }

            if (maps.ContainsKey("CN") && maps.ContainsKey("PC"))
            {
                var cn = maps["CN"];
                var pc = maps["PC"];

                if (pc.vers.Count > cn.vers.Count)
                {
                    var mergedDates = new Dictionary<string, DateTime?>();

                    foreach (var v in pc.vers)
                    {
                        if (cn.dates.TryGetValue(v, out var cnDate) && cnDate != null)
                            mergedDates[v] = cnDate;
                        else if (pc.dates.TryGetValue(v, out var pcDate))
                            mergedDates[v] = pcDate;
                    }

                    maps["PC"] = (pc.root, pc.vers, pc.types, mergedDates, pc.exists, pc.choice);
                    maps.Remove("CN");
                }
                else
                {
                    maps.Remove("PC");
                }
            }

            return maps;
        }
        async Task RunBruteforceLauncherAsync(string branch) // i made claude do this part. source match later and fix speeds 2026-06-15
        {
            AppendLine([]);
            AppendLine([new("  ┌─────────────────────────────────────────────────┐", TC.Pink)]);
            AppendLine([new("  │  LAUNCHER BRUTEFORCE                            │", TC.Pink)]);
            AppendLine([new("  └─────────────────────────────────────────────────┘", TC.Pink)]);
            AppendLine([]);

            var srcRaw = (await AskInput("  source  [OS / CN / PC / QQ] >")).Trim().ToLower();
            if (!new[] { "os", "cn", "pc", "qq" }.Contains(srcRaw))
            {
                AppendLine([new("  invalid source — must be os, cn, pc or qq", TC.Warn)]);
                return;
            }
            var source = srcRaw;
            var root = source switch { "cn" => CN_ROOT, "pc" => PC_ROOT, "qq" => QQ_ROOT, _ => OS_ROOT };
            var isQq = source == "qq";

            var startV = (await AskInput("  start version   (e.g. 0.9.1.640) >")).Trim();
            if (!IsValidVersion(startV))
            { AppendLine([new("  invalid version format", TC.Warn)]); return; }

            var finishV = (await AskInput("  end version     (e.g. 0.9.1.620) >")).Trim();
            if (!IsValidVersion(finishV))
            { AppendLine([new("  invalid version format", TC.Warn)]); return; }

            var (a1, b1, c1, d1) = ParseVer(startV);
            var (a2, b2, c2, d2) = ParseVer(finishV);

            // validate same major.minor.patch
            if (a1 != a2 || b1 != b2 || c1 != c2)
            {
                AppendLine([new("  launcher bruteforce only varies the 4th version segment — use matching a.b.c prefix", TC.Warn)]);
                return;
            }

            bool down = d1 > d2;
            int total = Math.Abs(d1 - d2) + 1;
            int ok = 0, skip = 0, err = 0, done = 0;
            var found = new List<(string ver, string url)>();

            AppendLine([]);
            AppendLine([
                new("  branch  ", TC.Dim), new(branch,           TC.Release, true),
                new("  ·  ", TC.Dim),      new(source.ToUpper(), TC.Normal,  true),
                new($"  ·  {total} version{(total == 1 ? "" : "s")}  ·  {startV} → {finishV}", TC.Dim),
            ]);
            AppendLine([]);
            AppendLine([new("  version              status      url", TC.Dim)]);
            AppendLine([new("  " + new string('─', 66), TC.Dim)]);

            for (int d = d1; down ? d >= d2 : d <= d2; d += down ? -1 : 1)
            {
                var ver = $"{a1}.{b1}.{c1}.{d}";
                var url = $"{root}/{branch}/{ver}/full_{ver}.7z";
                done++;
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                    using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    if (resp.IsSuccessStatusCode)
                    {
                        ok++;
                        found.Add((ver, url));
                        AppendLine([
                            new($"  {ver,-22}", TC.Normal),
                            new("FOUND      ", TC.Ok, true),
                            new(url, TC.Dim),
                        ]);
                    }
                    else
                    {
                        SetStatusLine($"  [{done}/{total}]  {ver}  miss");
                    }
                }
                catch (TaskCanceledException)
                {
                    skip++;
                    AppendLine([
                        new($"  {ver,-22}", TC.Normal),
                        new("TIMEOUT    ", TC.Warn, true),
                        new("request timed out", TC.Dim),
                    ]);
                }
                catch (Exception ex)
                {
                    err++;
                    AppendLine([
                        new($"  {ver,-22}", TC.Normal),
                        new("ERROR      ", TC.Warn, true),
                        new(ex.Message.Length > 48 ? ex.Message[..48] : ex.Message, TC.Dim),
                    ]);
                }
                await Task.Delay(isQq ? 1000 : 300);
            }

            ClearStatusLine();
            AppendLine([]);
            AppendLine([new("  " + new string('─', 66), TC.Pink)]);
            AppendLine([
                new("  found   ", TC.Dim), new($"{ok}", TC.Ok, true),
                new($" / {total}", TC.Dim),
                new("    timeout  ", TC.Dim), new($"{skip}", skip > 0 ? TC.Warn : TC.Dim, skip > 0),
                new("    error  ", TC.Dim),  new($"{err}",  err  > 0 ? TC.Warn : TC.Dim, err  > 0),
            ]);
            AppendLine([]);

            if (AppSettings.SaveBruteforceToFile && found.Count > 0)
            {
                var shortVer = string.Join(".", startV.Split('.').Take(3));
                var fname = $"StrinowaF1-{source.ToUpper()}-{shortVer}.txt";
                try
                {
                    var lines = new System.Collections.Generic.List<string>
                    {
                        $"Strinowa Launcher Bruteforce — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        $"Branch: {branch}  Source: {source.ToUpper()}  Range: {startV} → {finishV}",
                        $"Found: {ok}/{total}  Timeouts: {skip}  Errors: {err}",
                        new string('─', 66),
                    };
                    foreach (var (v, u) in found)
                        lines.Add($"{v,-22}  {u}");
                    File.WriteAllLines(fname, lines, System.Text.Encoding.UTF8);
                    AppendLine([new("  saved  →  ", TC.Dim), new(fname, TC.Ok)]);
                }
                catch (Exception ex)
                {
                    AppendLine([new($"  save failed: {ex.Message}", TC.Warn)]);
                }
                AppendLine([]);
            }

            await AskYesNo("  Press Enter to continue");
        }

        List<(int, int, int, int)> BuildGameSeq(string startV, string finishV)
        {
            var (a1, b1, c1, d1) = ParseVer(startV);
            var (a2, b2, c2, d2) = ParseVer(finishV);
            bool down = CompareVer((a1, b1, c1, d1), (a2, b2, c2, d2)) > 0;
            var seq = new List<(int, int, int, int)>();
            var visited = new HashSet<(int, int, int, int)>();
            int ca = a1, cb = b1, cc = c1, cd = d1;
            int safety = 0;
            while (true)
            {
                var t = (ca, cb, cc, cd);
                if (!visited.Contains(t)) { visited.Add(t); seq.Add(t); }
                if (t == (a2, b2, c2, d2)) break;
                if (down) { cd--; if (cd < 0) { cc--; if (cc < 0) { cb--; cc = 9; } cd = 50; } }
                else { cd++; if (cd > 50) { cc++; cd = 0; } }
                if (++safety > 100_000) break;
            }
            return seq;
        }

        async Task<(bool ok, DateTime? ts)> ProbeUrlAsync(string url)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                using var r = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (r.IsSuccessStatusCode)
                    return (true, ParseLastMod(r));
            }
            catch { }
            return (false, null);
        }

        async Task<long?> HeadSizeAsync(string url)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Head, url);
                using var r = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                if (r.IsSuccessStatusCode && r.Content.Headers.ContentLength.HasValue)
                    return r.Content.Headers.ContentLength.Value;
            }
            catch { }
            return null;
        }

        async Task<(List<string> vers, Dictionary<string, HashSet<string>> revmap)> GetVersionsAsync(string indexUrl)
        {
            try
            {
                var text = await Http.GetStringAsync(indexUrl);
                if (string.IsNullOrWhiteSpace(text) || text.Contains("NoSuchKey") || text.Contains("does not exist"))
                {
                    AppendText($"  manifest not found: {indexUrl}", TC.Warn);
                    return (new(), new());
                }
                return ParseIndexText(text);
            }
            catch (Exception ex)
            {
                AppendText($"  manifest fetch failed: {ex.Message}", TC.Warn);
                return (new(), new());
            }
        }

        async Task<(List<string> vers, Dictionary<string, HashSet<string>> revmap)>
            GetHiddenVersionsAsync(string baseRoot, string branch, string version, bool quiet)
        {
            var url = $"{baseRoot}/{branch}/manifest.txt_{version}";
            try
            {
                if (!quiet) AppendText($"  Fetching hidden: {url}", TC.Dim);
                var text = await Http.GetStringAsync(url);
                return ParseIndexText(text);
            }
            catch { return (new(), new()); }
        }

        (List<string> vers, Dictionary<string, HashSet<string>> revmap) ParseIndexText(string text)
        {
            var branches = new Dictionary<string, HashSet<string>>();
            string? lastVer = null;
            bool inLast = false;
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var ln = raw.Trim();
                if (string.IsNullOrEmpty(ln)) continue;
                if (ln == "--last_version--") { inLast = true; continue; }
                if (inLast) { lastVer = ln; inLast = false; continue; }
                if ((ln.Contains("->") || ln.Contains("-&gt;")) && ln.Contains("|"))
                {
                    var parts = ln.Split('|');
                    if (parts.Length < 2) continue;

                    // LHS is "from_ver->to_ver" — use the right side (to_ver)
                    var lhs = parts[0].Split(new[] { "->", "-&gt;" }, StringSplitOptions.None);
                    var ver = (lhs.Length >= 2 ? lhs[1] : lhs[0]).Trim();

                    // RHS path: /Launcher/<ver>/full_<ver>.7z  OR  /<subfolder>/<ver>/filename
                    var rel = parts[1].Trim().TrimStart('/');
                    var segs = rel.Split('/');
                    if (segs.Length < 2) continue;

                    var folder = segs[0]; // "Launcher" for launcher, branch subfolder for game
                    // If first path segment is "Launcher" (case-insensitive), the version is segs[1]
                    // and we register it under the "Launcher" key which maps to the branch being scanned
                    if (folder.Equals("Launcher", StringComparison.OrdinalIgnoreCase))
                    {
                        var actualVer = IsValidVersion(ver) ? ver : (segs.Length > 1 ? segs[1] : ver);
                        branches.TryAdd("Launcher", new());
                        branches["Launcher"].Add(actualVer);
                    }
                    else
                    {
                        var actualVer = IsValidVersion(ver) ? ver : (IsValidVersion(segs[1]) ? segs[1] : ver);
                        branches.TryAdd(folder, new());
                        branches[folder].Add(actualVer);
                    }
                }
            }
            if (lastVer != null)
                foreach (var b in branches.Keys) branches[b].Add(lastVer);

            var revmap = new Dictionary<string, HashSet<string>>();
            foreach (var (b, vs) in branches)
                foreach (var v in vs)
                {
                    revmap.TryAdd(v, new());
                    revmap[v].Add(b);
                }

            var allVers = branches.Values.SelectMany(v => v).Distinct()
                .OrderBy(v => v, new VersionComparer()).ToList();
            return (allVers, revmap);
        }

        List<(string rel, string branch, string version)> ParseVersionManifest(string text)
        {
            var seen = new HashSet<string>();
            var items = new List<(string, string, string)>();
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var ln = raw.Trim();
                if (string.IsNullOrEmpty(ln) || !ln.Contains(':')) continue;
                var rel = ln.Split(':')[0].Trim().TrimStart('/');
                if (seen.Contains(rel)) continue;
                var segs = rel.Split('/');
                if (segs.Length < 3) continue;
                seen.Add(rel);
                items.Add((rel, segs[0], segs[1]));
            }
            return items;
        }

        List<string> BranchCandidates(string version, Dictionary<string, HashSet<string>> revmap, string userBranch)
        {
            var cands = revmap.TryGetValue(version, out var set) ? set.ToList() : new List<string>();
            if (cands.Contains(userBranch)) { cands.Remove(userBranch); cands.Insert(0, userBranch); }
            return cands.Count > 0 ? cands : new List<string> { userBranch };
        }

        static bool IsUrl(string s)
        {
            try { var u = new Uri(s); return u.Scheme is "http" or "https"; }
            catch { return false; }
        }

        static bool IsRelease(string body) =>
            body.Contains("PMGame-Win64-Shipping.exe") ||   //2021-01-01 - 2023-06-07
            body.Contains("Strinova-Win64-Shipping.exe") || //OS
            body.Contains("Calabiyau-Win64-Shipping.exe");  //CN
                                                            //CYGame-Win64-Shipping.exe?

        static DateTime? ParseLastMod(HttpResponseMessage r)
        {
            var lm = r.Content.Headers.LastModified ?? r.Headers.Date;
            return lm.HasValue ? lm.Value.UtcDateTime : null;
        }

        static string LinkLabel(string baseRoot) => baseRoot switch
        {
            var s when s.Contains("resource-download.strinova.com") => "Overseas",
            var s when s.Contains("tencentcos") => "China",
            var s when s.Contains("gxpan.cn") => "ClientPC",
            var s when s.Contains("down.klbq.qq.com") => "QQ",
            _ => baseRoot,
        };

        static string FormatSize(long? n)
        {
            if (n == null || n <= 0) return "unknown";
            double f = n.Value;
            foreach (var u in new[] { "B", "KB", "MB", "GB", "TB" })
            {
                if (f < 1024 || u == "TB") return $"{f:F2} {u}";
                f /= 1024;
            }
            return $"{f:F2} B";
        }

        static bool IsValidVersion(string v) =>
            Regex.IsMatch(v.Trim(), @"^\d+\.\d+\.\d+\.\d+$");

        static (int a, int b, int c, int d) ParseVer(string v)
        {
            var p = v.Trim().Split('.');
            return (int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
        }

        static int CompareVer((int, int, int, int) x, (int, int, int, int) y)
        {
            var (xa, xb, xc, xd) = x; var (ya, yb, yc, yd) = y;
            if (xa != ya) return xa.CompareTo(ya);
            if (xb != yb) return xb.CompareTo(yb);
            if (xc != yc) return xc.CompareTo(yc);
            return xd.CompareTo(yd);
        }

        static string ChooseRootGame(string source, (int a, int b, int c, int d) vt) => source switch
        {
            "os" => OS_ROOT,
            "pc" => PC_ROOT,
            "qq" => QQ_ROOT,
            _ => vt.a * 1000 + vt.b > 1009 ? PC_ROOT : CN_ROOT,
        };

        static (string? branch, string? hidden, string? src, bool gameBrute, bool launcherBrute)
            ParseBranchLine(string s)
        {
            var parts = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            bool gb = false, lb = false;
            while (parts.Count > 0 && parts[^1].ToLower() is "-b" or "-lb")
            {
                if (parts[^1].ToLower() == "-lb") lb = true;
                else gb = true;
                parts.RemoveAt(parts.Count - 1);
            }
            if (parts.Count == 0) return (null, null, null, gb, lb);
            var branch = LooksLikeBranch(parts[0]) ? parts[0] : null;
            string? ver = null, src = null;
            if (parts.Count >= 2 && Regex.IsMatch(parts[1], @"^\d+(?:\.\d+){1,}$"))
            {
                ver = parts[1];
                if (parts.Count >= 3 && parts[2].ToLower() is "os" or "cn" or "pc" or "qq")
                    src = parts[2].ToLower();
            }
            else if (parts.Count >= 2 && parts[1].ToLower() is "os" or "cn" or "pc" or "qq")
                src = parts[1].ToLower();
            return (branch, ver, src, gb, lb);
        }

        static bool LooksLikeBranch(string s) =>
            Regex.IsMatch(s, @"^[A-Za-z0-9._-]+$") &&
            (s.Contains('_') || (s.StartsWith("Game", StringComparison.OrdinalIgnoreCase) && s.Length >= 5)
                             || (s.StartsWith("Launcher", StringComparison.OrdinalIgnoreCase) && s.Length >= 9));

        void PauseBtn_Click(object s, RoutedEventArgs e)
        {
            TogglePause();
            PauseBtn.Content = _isPaused ? "\uE768" : "\uE769";
            PauseBtn.ToolTip = _isPaused ? Strings.Get("resume") : Strings.Get("pause");
        }

        void ReportBugBtn_Click(object s, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/chocofans/Strinowa/issues",
                UseShellExecute = true,
            });
        }

        void CloseBtn_Click(object s, RoutedEventArgs e)
        {
            _cfg.Width = (int)(ActualWidth - 16);
            _cfg.Height = (int)(ActualHeight - 16);
            _cfg.Theme = AppTheme.CurrentTheme.ToString();
            _cfg.Color = AppTheme.CurrentTermPreset.ToString();
            _cfg.Lang = Strings.Lang.ToString();
            _cfg.ClearLog = AppSettings.ClearLogOnFinish;
            _cfg.SaveBrf = AppSettings.SaveBruteforceToFile;
            _cfg.SpeedKBs = AppSettings.SpeedLimitKBs;
            _cfg.FmtAsked = AppSettings.LauncherFormatAsked;
            _cfg.Fmt7z = AppSettings.LauncherDefault7z;
            _cfg.FmtExe = AppSettings.LauncherDefaultExe;
            _cfg.Save();
            Close();
        }
        void MinimizeBtn_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        void TitleBar_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal : WindowState.Maximized;
            }
            else DragMove();
        }
        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e) { }
        void Window_SizeChanged(object s, SizeChangedEventArgs e) { }
        void LogoBtn_Click(object s, RoutedEventArgs e)
        {
            var dlg = new AboutWindow { Owner = this };
            dlg.ShowDialog();
        }

        void SettingsBtn_Click(object s, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow { Owner = this };
            dlg.ShowDialog();
        }

        /*
        void GameBtn_Click(object s, RoutedEventArgs e)
        {
            var dlg = new GameWindow { Owner = this };
            dlg.ShowDialog();
        }
        */
    }

    class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1; if (y == null) return 1;
            var xs = x.Split('.'); var ys = y.Split('.');
            for (int i = 0; i < Math.Max(xs.Length, ys.Length); i++)
            {
                int xi = i < xs.Length && int.TryParse(xs[i], out int xv) ? xv : 0;
                int yi = i < ys.Length && int.TryParse(ys[i], out int yv) ? yv : 0;
                if (xi != yi) return xi.CompareTo(yi);
            }
            return 0;
        }
    }

    record ScanContext(
        string Branch,
        string? AllowedSource,
        string? HiddenVersion,
        Dictionary<string, (string root, List<string> vers, Dictionary<string, string> types,
            Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists,
            Dictionary<string, string> choice)> Maps);

    record BuildResult(
        Dictionary<string, string> Types,
        Dictionary<string, DateTime?> Dates,
        Dictionary<string, bool> Exists,
        Dictionary<string, string> Choice);
}