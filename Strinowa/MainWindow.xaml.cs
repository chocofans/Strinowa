using StrinowaWPF;
using System;
using System.Collections.Concurrent;
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

    sealed class DownloadJob
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public required string[] Destinations { get; init; }
        public long TotalBytes { get; init; }
        public int FileCount { get; init; }
        public long CompletedBytes;
        public long TransferredBytes;
        public int CompletedFiles;
        public DateTime StartedUtc { get; } = DateTime.UtcNow;
        public string? Activity;
        public string? ActivityDetail;
    }

    sealed class AsyncManualResetEvent
    {
        readonly object _sync = new();
        TaskCompletionSource<bool> _source;

        public AsyncManualResetEvent(bool signaled)
        {
            _source = CreateSource();
            if (signaled) _source.TrySetResult(true);
        }

        public Task WaitAsync()
        {
            lock (_sync) return _source.Task;
        }

        public void Set()
        {
            lock (_sync) _source.TrySetResult(true);
        }

        public void Reset()
        {
            lock (_sync)
                if (_source.Task.IsCompleted) _source = CreateSource();
        }

        static TaskCompletionSource<bool> CreateSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        public int UiScale { get; set; } = 100;
        public bool Fmt7z { get; set; } = true;
        public bool FmtExe { get; set; } = false;
        public bool WinBuildScan { get; set; } = true;
        public bool DispatchWinBuild { get; set; } = true;
        public bool ShowDevkits { get; set; } = false;

        static string Path => AppPaths.ConfigPath;

        public static Config Load()
        {
            var cfg = new Config();
            if (!File.Exists(Path)) { cfg.Save(); return cfg; }
            bool removeHiddenDevkitKey = false;
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
                if (key == "uiscale" && int.TryParse(val, out int us)) cfg.UiScale = Math.Clamp(us, 50, 200);
                if (key == "fmt7z" && bool.TryParse(val, out bool f7)) cfg.Fmt7z = f7;
                if (key == "fmtexe" && bool.TryParse(val, out bool fe)) cfg.FmtExe = fe;
                if (key == "winbuildscan" && bool.TryParse(val, out bool ws)) cfg.WinBuildScan = ws;
                if (key == "dispatchwinbuild" && bool.TryParse(val, out bool dw)) cfg.DispatchWinBuild = dw;
                if (key == "showdevkits" && bool.TryParse(val, out bool sd))
                {
                    cfg.ShowDevkits = sd;
                    removeHiddenDevkitKey = !sd;
                }
            }
            if (removeHiddenDevkitKey) cfg.Save();
            return cfg;
        }

        public void Save()
        {
            File.WriteAllText(Path,
                $"; strinowa downloader - conf.ini\n" +
                $"width={Width}\nheight={Height}\n" +
                $"theme={Theme}\ncolor={Color}\nlang={Lang}\n" +
                $"clearlog={ClearLog}\nsavebrf={SaveBrf}\n" +
                $"speedkbs={SpeedKBs}\nuiscale={UiScale}\n" +
                $"fmt7z={Fmt7z}\nfmtexe={FmtExe}\n" +
                $"WinBuildScan={WinBuildScan}\nDispatchWinBuild={DispatchWinBuild}\n" +
                (ShowDevkits ? "ShowDevkits=True\n" : ""));
        }
    }

    record TermSpan(string Text, SolidColorBrush Color, bool Bold = false);

    public partial class MainWindow : Window
    {
        static readonly bool debug = false;

        const string OS_ROOT = "https://resource-download.strinova.com/Client/Win/GameDepot";
        const string CN_ROOT = "https://klbq-cdn-1300343128.cos.ap-shanghai.myqcloud.com/Client/Win/GameDepot";
        const string CN_ALL_ROOT = "https://klbq-cdn-1300343128.cos.ap-shanghai.myqcloud.com/Client/Win/GameDepot";

        const string PC_ROOT = "https://klbqcp-client-cdn.gxpan.cn/Client/Win/GameDepot";
        const string QQ_ROOT = "https://down.klbq.qq.com/Client/Win/GameDepot";

        //const string CN_ROOT2 = "https://klbq-cdn-1300343128.cos.ap-shanghai.myqcloud.com"
        //const string OS_ROOT2 = "https://klbq-overseas-cdn-1251001060.cos.ap-guangzhou.myqcloud.com/"
        //const string PM_ROOT = "http://192.168.16.77/PMGame"

        // PM is internally used in CN. uncomment once you have IDreamSky Feishu VPN
        // should be referenced elsewhere. If i did it right.
        // youll add "pm" to the valid source list in DispatchAsync and bruteforce source selectors
        // pmroot will still manifest differently. It might lack PAKs

        //OTHER
        //klbqcm-pack.dl.gxpan.cn
        //klbqcp-tiyan-dir.gxpan.cn:11711
        //2026-06-21

        //klbqcm-client-cdn
        //2026-07-14

        static readonly HttpClient Http = new(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            MaxConnectionsPerServer = 32,
        })
        { Timeout = TimeSpan.FromSeconds(30) };

        Config _cfg = new();
        bool _busy = false;
        bool _versionClickBusy;
        string _currentHint = "<channel>  <OS|CN|PC>  <version>  [-b]";
        readonly List<string> _history = new();
        int _histIdx = -1;
        string _historyDraft = "";
        string _bruteSource = "os";
        bool _bruteLauncherMode = true;
        bool _bruteSaveTxt = true;
        CancellationTokenSource? _bruteCts;
        bool _bruteOwnsDownloadPanel;
        Manifest? _manifestScanner;
        double _uiScale = 1.0;

        readonly object _downloadJobsLock = new();
        readonly Dictionary<string, DownloadJob> _downloadJobs = new(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _activeDownloadDestinations = new(StringComparer.OrdinalIgnoreCase);
        readonly SemaphoreSlim _downloadSlots = new(8, 8);
        System.Windows.Threading.DispatcherTimer? _dlTimer;
        TaskCompletionSource<bool>? _downloadConfirmSource;
        List<AllDownloadChoice> _multiDownloadChoices = [];
        readonly HashSet<string> _multiDownloadSelected = new(StringComparer.OrdinalIgnoreCase);
        bool _launcherFormatConfirmation;
        bool _downloadPreparationVisible;
        long? _launcher7zSize;
        long? _launcherExeSize;
        long _dlLastSampleBytes;
        DateTime _dlLastSampleUtc = DateTime.UtcNow;
        double _dlSmoothedBytesPerSecond;
        bool _downloadCompletionVisible;
        string? _completedDownloadFolder;
        string _completedDownloadLabel = "Download complete";

        DispatcherTimer? _debugTimer;
        DispatcherTimer? _clipboardToastTimer;
        readonly Stopwatch _debugUptime = Stopwatch.StartNew();
        bool _debugVisible;
        bool _debugChordLatched;
        long _debugLastTransferred;
        DateTime _debugLastSampleUtc = DateTime.UtcNow;
        TimeSpan _debugLastCpuTime;
        double _debugUiLagMs;

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
        readonly List<DevkitTerminalWave> _terminalDevkitWaves = new();

        public MainWindow()
        {
            InitializeComponent();
            ContentRendered += Window_ContentRendered;
            Closed += (_, _) =>
            {
                StopDebugOverlay();
                _downloadConfirmSource?.TrySetResult(false);
            };
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Strinowa-WPF-Downloader/1.11");
            TC.Devkit = _devkitAnim.Brush;
            if (debug) SetDebugOverlayVisible(true);
        }

        void Window_ContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= Window_ContentRendered;
            if (AppTheme.CurrentTheme != LauncherTheme.Acrylic) return;

            Dispatcher.BeginInvoke(() =>
            {
                ApplyCurrentWindowDimensions();
                ApplyTheme();
                UpdateWindowClip();
            }, DispatcherPriority.ContextIdle);
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _cfg = Config.Load();

            if (Enum.TryParse<LauncherTheme>(_cfg.Theme, out var t)) AppTheme.CurrentTheme = t;
            if (Enum.TryParse<TermColorPreset>(_cfg.Color, out var c)) AppTheme.CurrentTermPreset = c;
            if (Enum.TryParse<AppLanguage>(_cfg.Lang, out var l)) Strings.Lang = l;
            AppSettings.ClearLogOnFinish = _cfg.ClearLog;
            AppSettings.SaveBruteforceToFile = _cfg.SaveBrf;
            AppSettings.SpeedLimitKBs = _cfg.SpeedKBs;
            ApplyUiScale(_cfg.UiScale);
            AppSettings.LauncherDefault7z = _cfg.Fmt7z;
            AppSettings.LauncherDefaultExe = _cfg.FmtExe;
            AppSettings.ShowDevkits = _cfg.ShowDevkits;

            ApplyTheme();
            InputHint.Text = Strings.Get("hint");
            InputBox.Focus();
            ShowHeader();
            UpdateAdvancedBruteUi();
            _isFirstOpen = false;
        }

        public int CurrentUiScale => (int)(_uiScale * 100);
        double WindowFrameAllowance => 0;

        void ApplyCurrentWindowDimensions()
        {
            Width = (_cfg.Width + WindowFrameAllowance) * _uiScale;
            Height = (_cfg.Height + WindowFrameAllowance) * _uiScale;
        }

        public void ApplyUiScale(int percent)
        {
            _uiScale = Math.Clamp(percent, 50, 200) / 100.0;
            _cfg.UiScale = (int)Math.Round(_uiScale * 100);
            MinWidth = 760 * _uiScale;
            MinHeight = 380 * _uiScale;
            OuterBorder.LayoutTransform = new ScaleTransform(_uiScale, _uiScale);
            ApplyCurrentWindowDimensions();
            UpdateWindowClip();
        }

        public void ApplyWindowPreset(int width, int height)
        {
            _cfg.Width = width; _cfg.Height = height;
            ApplyCurrentWindowDimensions();
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
            Title = Strings.Get("title");
            TitleVersionText.Text = Strings.Get("title");
            bool isLight = AppTheme.CurrentTheme == LauncherTheme.Light;
            bool isMidnight = AppTheme.CurrentTheme == LauncherTheme.Midnight;
            bool isAcrylic = AppTheme.CurrentTheme == LauncherTheme.Acrylic;
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
                case LauncherTheme.Acrylic:
                    bgCol = C(0x18, 0x26, 0x26, 0x26); titleCol = C(0x24, 0x30, 0x30, 0x30);
                    sepCol = C(0x2A, 0xA8, 0xA8, 0xA8); inputCol = C(0x30, 0x1C, 0x1C, 0x1C);
                    break;
                default: // Light
                    bgCol = C(0xE8, 0xE8, 0xF0); titleCol = C(0xD8, 0xD8, 0xE8);
                    sepCol = C(0xB0, 0xB0, 0xC8); inputCol = C(0xF5, 0xF5, 0xFF);
                    break;
            }
            var borderCol = isAcrylic ? Colors.Transparent : isLight ? C(0xB0, 0xB0, 0xC8) : C(0x2E, 0x2E, 0x38);

            Resources["ControlSurfaceBrush"] = isAcrylic ? B(0x3C, 0x3A, 0x3A, 0x3A) : isLight ? B(0xE0, 0xE0, 0xEC) : B(0x24, 0x24, 0x2D);
            Resources["ControlHoverBrush"] = isAcrylic ? B(0x58, 0x58, 0x58, 0x58) : isLight ? B(0xC8, 0xC8, 0xD8) : B(0x33, 0x33, 0x41);
            Resources["ControlPressedBrush"] = isAcrylic ? B(0x68, 0x2B, 0x2B, 0x2B) : isLight ? B(0xB8, 0xB8, 0xCA) : B(0x4E, 0x4E, 0x5E);
            Resources["ControlTextBrush"] = isAcrylic ? B(0xF7, 0xFA, 0xFF) : isLight ? B(0x22, 0x22, 0x30) : B(0xF0, 0xF0, 0xF0);
            Resources["ScrollThumbBrush"] = isAcrylic ? B(0x28, 0xD0, 0xD0, 0xD0) : isLight ? B(0x78, 0x70, 0x70, 0x80) : B(0x3A, 0x3A, 0x4A);

            Animate(OuterBorder, Border.BackgroundProperty, bgCol);
            Animate(OuterBorder, Border.BorderBrushProperty, borderCol);
            OuterBorder.Margin = new Thickness(0);
            OuterBorder.BorderThickness = new Thickness(0);
            OuterBorder.Effect = null;
            Animate(TitleBarBorder, Border.BackgroundProperty, titleCol);
            AnimateRect(SepRect, sepCol);
            Animate(InputRowBorder, Border.BackgroundProperty, inputCol);
            Animate(InputRowBorder, Border.BorderBrushProperty, sepCol);

            var dlBgCol = isAcrylic ? C(0x28, 0x1C, 0x1C, 0x1C)
                           : isMidnight ? C(0x00, 0x00, 0x00)
                           : isLight ? C(0xE0, 0xE0, 0xEC)
                                         : C(0x0E, 0x0E, 0x16);
            var dlTrackCol = isAcrylic ? C(0x48, 0x58, 0x58, 0x58)
                           : isMidnight ? C(0x10, 0x10, 0x18)
                           : isLight ? C(0xC0, 0xC0, 0xD4)
                                        : C(0x1E, 0x1E, 0x2A);
            Animate(DownloadPanel, Border.BackgroundProperty, dlBgCol);
            Animate(DownloadPanel, Border.BorderBrushProperty, sepCol);
            Animate(DlTrack, Border.BackgroundProperty, dlTrackCol);

            if (isPink)
            {
                //Text1=#FF004D, Text2=#FF0077, Text3=#F03A95, Devkit wave #B31741â†’#B033A3, Deprecated=#610E60 2026-06-18
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
            else if (isAcrylic)
            {
                TC.Normal = B(0xF7, 0xFA, 0xFF);
                TC.CN = B(0xD7, 0xE0, 0xE9);
                TC.Release = B(0xB6, 0xC2, 0xD0);
                TC.Deprecated = B(0x7E, 0x8B, 0x9A);
                TerminalBox.Foreground = TC.Normal;

                DlLabel.Foreground = B(0xF7, 0xFA, 0xFF);
                DlStats.Foreground = B(0xD7, 0xE0, 0xE9);
                DlSubLabel.Foreground = B(0xD7, 0xE0, 0xE9);
                DlEta.Foreground = B(0xA8, 0xB5, 0xC4);

                SetDlFillColors(C(0xF4, 0xA7, 0xC5), C(0x91, 0xB9, 0xD8));
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


            var titleFg = isAcrylic ? B(0xF7, 0xFA, 0xFF) : isLight ? B(0x22, 0x22, 0x30) : B(0xDD, 0xDD, 0xDD);
            var hintFg = isAcrylic ? B(0xB2, 0xB2, 0xB2) : isLight ? B(0x77, 0x77, 0x99) : B(0x40, 0x40, 0x55);
            var inputFg = isAcrylic ? B(0xF7, 0xFA, 0xFF) : isLight ? B(0x11, 0x11, 0x22) : B(0xE8, 0xE8, 0xE8);
            var btnFg = isAcrylic ? B(0xF1, 0xF5, 0xFA) : isLight ? B(0x22, 0x22, 0x30) : B(0xCC, 0xCC, 0xCC);

            TitleVersionText.Foreground = titleFg;
            InputHint.Foreground = hintFg;
            InputBox.Foreground = inputFg;
            ChevronBlock.Foreground = B(0xDC, 0x32, 0x78);
            MinimizeBtn.Foreground = btnFg;
            CloseBtn.Foreground = btnFg;
            PauseBtn.Foreground = isAcrylic ? B(0xD7, 0xE0, 0xE9) : btnFg;
            ClipboardToast.Background = isAcrylic ? B(0xD8, 0x28, 0x28, 0x2E)
                : isLight ? B(0xF2, 0xF2, 0xF8)
                : isMidnight ? B(0xF0, 0x0E, 0x0E, 0x12)
                : B(0xF0, 0x20, 0x20, 0x26);
            ClipboardToast.BorderBrush = isAcrylic ? B(0x78, 0xE0, 0xE0, 0xE8)
                : isLight ? B(0xB0, 0xB0, 0xC8)
                : B(0x5A, 0x5A, 0x68);
            ClipboardToastText.Foreground = isLight ? B(0x22, 0x22, 0x30) : B(0xF4, 0xF4, 0xF6);
            LogoGlass.Background = isAcrylic ? B(0x48, 0xE8, 0xE8, 0xE8) : Brushes.Transparent;
            LogoGlass.BorderBrush = isAcrylic ? B(0x70, 0xFF, 0xFF, 0xFF) : Brushes.Transparent;
            AcrylicHelper.ApplyBackdrop(this, isAcrylic, borderlessFullWindow: true);
            InputHint.Text = Strings.Get("hint");
            TitleVersionText.Text = Strings.Get("title");

            // Since shit got fucky when i removed the old one, this replaces the old one with the new one.
            // run elements that hold a reference to this brush update automatically.
            _devkitAnim.Stop();
            _devkitAnim = new DevkitColorAnimator(AppTheme.CurrentTermPreset);
            TC.Devkit = _devkitAnim.Brush;

            RecolorTerminal();
            _manifestScanner?.ApplyTheme();
            UpdateAdvancedBruteUi();
            if (IsLoaded) ApplyCurrentWindowDimensions();
            UpdateWindowClip();
        }

        void UpdateWindowClip()
        {
            if (AppTheme.CurrentTheme != LauncherTheme.Acrylic ||
                OuterBorder.ActualWidth <= 0 || OuterBorder.ActualHeight <= 0)
            {
                OuterBorder.Clip = null;
                return;
            }

            OuterBorder.Clip = new RectangleGeometry(
                new Rect(0, 0, OuterBorder.ActualWidth, OuterBorder.ActualHeight), 10, 10);
        }

        void ActivateExistingDevkitWaves()
        {
            foreach (var block in TerminalBox.Document.Blocks.OfType<BlockUIContainer>())
            {
                if (block.Child is not TextBlock tb || !tb.Text.Contains("[Devkit]", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var run in tb.Inlines.OfType<System.Windows.Documents.Run>().ToList())
                {
                    if (string.IsNullOrWhiteSpace(run.Text) || run.Text.Contains("GMT+", StringComparison.OrdinalIgnoreCase)) continue;
                    var span = new System.Windows.Documents.Span { FontWeight = run.FontWeight };
                    tb.Inlines.InsertBefore(run, span);
                    tb.Inlines.Remove(run);
                    _terminalDevkitWaves.Add(new DevkitTerminalWave(span, run.Text, AppTheme.CurrentTermPreset));
                }
            }
        }
        void RecolorTerminal()
        {
            var brushMap = new Dictionary<SolidColorBrush, SolidColorBrush>
            {
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
                var wave = new TranslateTransform(-0.8, 0);
                gb.RelativeTransform = wave;
                wave.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(-0.8, 0.8, TimeSpan.FromSeconds(1.15))
                    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever,
                      EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } });
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
        static Color C(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
        static SolidColorBrush B(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
        static SolidColorBrush B(byte a, byte r, byte g, byte b) => new(Color.FromArgb(a, r, g, b));

        bool _isFirstOpen = true;

        void AppendLine(IEnumerable<TermSpan> spans, bool newline = true)
        {
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

        void AppendClickableLine(IEnumerable<TermSpan> spans, string clickCommand, string? copyUrl = null,
            bool hiddenBuild = false, IReadOnlyList<string>? matchingBranches = null)
        {
            var tb = new TextBlock
            {
                FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                FontSize = 13,
                Margin = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = copyUrl == null
                    ? $"Click to download: {clickCommand}"
                    : $"Click to download: {clickCommand}\nRight-click to copy link",
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
            if (hiddenBuild)
            {
                tb.Inlines.Add(new System.Windows.Documents.Run(" ⚑")
                {
                    Foreground = TC.Warn,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    ToolTip = "This is a Hidden build. It is not stored on the public manifest.",
                    Cursor = Cursors.Help,
                });
            }
            if (matchingBranches is { Count: > 1 })
            {
                tb.Inlines.Add(new System.Windows.Documents.Run(" ⧉")
                {
                    Foreground = TC.Info,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    ToolTip = $"Appears on Branches {string.Join(", ", matchingBranches)}",
                    Cursor = Cursors.Help,
                });
            }

            var cmd = clickCommand;
            tb.MouseLeftButtonDown += async (_, _) =>
            {
                var parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && parts[0].Equals("dl", StringComparison.OrdinalIgnoreCase) &&
                    _lastScanCtx != null)
                {
                    if (_versionClickBusy) return;
                    _versionClickBusy = true;
                    try
                    {
                        await HandleClickDownload(_lastScanCtx, parts[1], parts[2]);
                    }
                    catch (Exception ex)
                    {
                        AppendText($"  Could not open download: {ex.Message}", TC.Warn);
                        ShowModernError(ex, "Could not open download");
                    }
                    finally { _versionClickBusy = false; }
                    return;
                }

                if (_busy) return;
                InputBox.Text = cmd;
                InputBox.Focus();
                InputBox.CaretIndex = cmd.Length;
                InputHint.Visibility = Visibility.Collapsed;
            };
            if (!string.IsNullOrWhiteSpace(copyUrl))
            {
                tb.MouseRightButtonUp += (_, e) =>
                {
                    try
                    {
                        Clipboard.SetText(copyUrl);
                        ShowClipboardToast();
                    }
                    catch { }
                    e.Handled = true;
                };
            }
            tb.MouseEnter += (_, _) =>
            {
                foreach (var r in tb.Inlines.OfType<System.Windows.Documents.Run>())
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
                foreach (var r in tb.Inlines.OfType<System.Windows.Documents.Run>())
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

        void ShowClipboardToast(string text = "Copied link to clipboard")
        {
            SoundEffects.Popup();
            ShowToast(text, false);
        }

        public void ShowModernError(Exception exception, string? context = null)
        {
            SoundEffects.Error();
            var code = $"0x{exception.HResult:X8}";
            var heading = string.IsNullOrWhiteSpace(context) ? "Error" : context;
            ShowToast($"{heading}  •  {code}\n{exception.Message}", true);
        }

        void ShowToast(string text, bool error)
        {
            _clipboardToastTimer?.Stop();
            ClipboardToastText.Text = text;
            var light = AppTheme.CurrentTheme == LauncherTheme.Light;
            var acrylic = AppTheme.CurrentTheme == LauncherTheme.Acrylic;
            ClipboardToast.Background = acrylic ? B(0xE8, 0x28, 0x28, 0x2C)
                : light ? B(0xF4, 0xF4, 0xF8) : B(0xEE, 0x20, 0x20, 0x26);
            ClipboardToast.BorderBrush = error ? B(0xD0, 0xDC, 0x32, 0x78)
                : acrylic ? B(0x90, 0xC8, 0xC8, 0xD0) : B(0x70, 0x5A, 0x5A, 0x68);
            ClipboardToastText.Foreground = light ? B(0x22, 0x22, 0x2A) : B(0xF4, 0xF4, 0xF6);
            ClipboardToast.BeginAnimation(OpacityProperty, null);
            ClipboardToast.Visibility = Visibility.Visible;
            ClipboardToast.Opacity = 0;
            var move = new TranslateTransform(0, 8);
            ClipboardToast.RenderTransform = move;
            ClipboardToast.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
            move.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(160))
                { EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });

            _clipboardToastTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(error ? 5000 : 1500)
            };
            _clipboardToastTimer.Tick += (_, _) =>
            {
                _clipboardToastTimer?.Stop();
                var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fade.Completed += (_, _) => ClipboardToast.Visibility = Visibility.Collapsed;
                ClipboardToast.BeginAnimation(OpacityProperty, fade);
            };
            _clipboardToastTimer.Start();
        }

        static string BuildFileListUrl(string root, string branch, string version) =>
            branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase)
                ? $"{root}/{branch}/{version}/full_{version}.7z"
                : $"{root}/{branch}/{version}/full_zip/manifest.txt";

        async Task HandleClickDownload(ScanContext ctx, string version, string source)
        {
            var src = source.ToLower();
            if (ctx.MultiChoices?.TryGetValue(MultiChoiceKey(version, source), out var multiChoices) == true &&
                multiChoices.Count > 1)
            {
                var selected = await ConfirmMultiDownloadInPanelAsync(version, multiChoices);
                foreach (var choice in selected)
                    await CeciliaDownloadAsync(RootForSource(choice.Source), choice.ResolvedBranch,
                        choice.Version, null, true);
                return;
            }
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
                AppendText($"  source '{src.ToUpper()}' not found in scan â€” re-run the scan.", TC.Warn);
                return;
            }
            if (!entry.vers.Contains(version))
            {
                AppendText("  Version not found in this source.", TC.Warn);
                return;
            }

            var pickedBranch = entry.choice.GetValueOrDefault(version, ctx.Branch);
            await CeciliaDownloadAsync(entry.root, pickedBranch, version, null);
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
                NavigateCommandHistory(older: true);
                e.Handled = true; return;
            }
            if (e.Key == Key.Down)
            {
                NavigateCommandHistory(older: false);
                e.Handled = true; return;
            }
            if (e.Key != Key.Enter) return;

            var raw = InputBox.Text.Trim();
            InputBox.Text = "";
            InputHint.Visibility = Visibility.Visible;
            _histIdx = -1;
            _historyDraft = "";

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

        void NavigateCommandHistory(bool older)
        {
            if (_history.Count == 0) return;

            if (older)
            {
                if (_histIdx < 0) _historyDraft = InputBox.Text;
                _histIdx = Math.Min(_histIdx + 1, _history.Count - 1);
                InputBox.Text = _history[_history.Count - 1 - _histIdx];
            }
            else if (_histIdx > 0)
            {
                _histIdx--;
                InputBox.Text = _history[_history.Count - 1 - _histIdx];
            }
            else
            {
                _histIdx = -1;
                InputBox.Text = _historyDraft;
            }

            InputBox.Focus();
            InputBox.CaretIndex = InputBox.Text.Length;
            InputHint.Visibility = string.IsNullOrEmpty(InputBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                ShowModernError(ex, "Command error");
            }
            finally
            {
                _busy = false;
                ChevronBlock.Foreground = TC.Pink;
                SetHint(Strings.Get("hint"));
            }
        }

        async Task RunAdvancedBruteforceFromUiAsync()
        {
            await RunAdvancedBruteforceAsync(
                BruteChannelBox.Text.Trim(),
                _bruteSource,
                BruteStartBox.Text.Trim(),
                BruteEndBox.Text.Trim(),
                _bruteLauncherMode,
                _bruteSaveTxt);
        }

        void UpdateLoaderProgress(int step, int total, string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (_loaderBlock?.Child is StackPanel row)
                {
                    if (row.Children.Count >= 2 && row.Children[1] is TextBlock tb)
                    {
                        tb.Text = text;
                    }
                }
            });
        }

        void BruteSource_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string src)
            {
                _bruteSource = src;
                UpdateAdvancedBruteUi();
            }
        }

        void BruteMode_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string mode)
            {
                _bruteLauncherMode = mode == "launcher";
                UpdateAdvancedBruteUi();
            }
        }

        void BruteResult_Click(object s, RoutedEventArgs e)
        {
            if (s is Button b && b.Tag is string kind)
            {
                if (kind == "txt") _bruteSaveTxt = !_bruteSaveTxt;
                UpdateAdvancedBruteUi();
            }
        }

        async void BruteStart_Click(object s, RoutedEventArgs e)
        {
            if (_busy) return;
            var branch = BruteChannelBox.Text.Trim();
            var startV = BruteStartBox.Text.Trim();
            var finishV = BruteEndBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(branch))
            {
                AppendText("  Bruteforce channel is empty.", TC.Warn);
                return;
            }
            if (!IsValidVersion(startV) || !IsValidVersion(finishV))
            {
                AppendText("  Bruteforce versions must use a.b.c.d format.", TC.Warn);
                return;
            }

            _busy = true;
            ChevronBlock.Foreground = TC.Dim;
            try
            {
                await RunAdvancedBruteforceAsync(
                    branch, _bruteSource, startV, finishV,
                    _bruteLauncherMode, _bruteSaveTxt);
            }
            catch (Exception ex)
            {
                AppendText($"  bruteforce error: {ex.Message}", TC.Warn);
                ShowModernError(ex, "Scanner error");
            }
            finally
            {
                _busy = false;
                _bruteCts?.Dispose();
                _bruteCts = null;
                ChevronBlock.Foreground = TC.Pink;
                StopBruteProgress();
            }
        }

        void BruteCancel_Click(object s, RoutedEventArgs e)
        {
            _bruteCts?.Cancel();
        }

        void UpdateAdvancedBruteUi()
        {
            bool isAcrylic = AppTheme.CurrentTheme == LauncherTheme.Acrylic;
            var idle = Resources["ControlSurfaceBrush"] as Brush ?? B(0x24, 0x24, 0x2D);
            var text = Resources["ControlTextBrush"] as Brush ?? B(0xF0, 0xF0, 0xF0);
            void Set(Button b, bool on)
            {
                b.Background = on
                    ? (isAcrylic ? B(0x88, 0x70, 0x70, 0x70) : B(0x5A, 0x5A, 0x68))
                    : idle;
                b.Foreground = on ? Brushes.White : text;
            }
            Set(BruteOsBtn, _bruteSource == "os");
            Set(BruteCnBtn, _bruteSource == "cn");
            Set(BrutePcBtn, _bruteSource == "pc");
            Set(BruteLauncherBtn, _bruteLauncherMode);
            Set(BruteGameBtn, !_bruteLauncherMode);
            Set(BruteTxtBtn, _bruteSaveTxt);
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
                AppendText("  No scan context â€” run a scan first.", TC.Warn);
                return;
            }

            var (branch, hiddenVer, src, gameBrute, launcherBrute) = ParseBranchLine(raw);
            if (branch == null)
            {
                AppendText("  Invalid branch.", TC.Warn);
                ShowHeader();
                return;
            }

            if (gameBrute)
            {
                BruteChannelBox.Text = branch;
                await RunAdvancedBruteforceFromUiAsync();
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
            var ctx = branch.Equals("Game_All", StringComparison.OrdinalIgnoreCase) ||
                      branch.Equals("Launcher_All", StringComparison.OrdinalIgnoreCase)
                ? await ScanAllBranchesAsync(branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher" : "Game", src)
                : await ScanBranchAsync(branch, src, hiddenVer);
            if (ctx == null) { ShowHeader(); return; }

            SetHint("<version>  or  <branch>  or  <branch -b>");
            while (true)
            {
                var dlChoice = await AskInput("");
                if (string.IsNullOrWhiteSpace(dlChoice)) { ShowHeader(); break; }

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
                        ShowHeader();
                        await CeciliaDownloadAsync(baseRoot, pickedBranch, version, null);
                        ShowHeader();
                        SetHint("<version>  or  <branch>  or  <branch -b>");
                        found = true;
                        break;
                    }
                    if (!found) AppendText("  Version not found in the current results.", TC.Warn);
                    continue;
                }

                var (nb, nv, ns, gb2, lb2) = ParseBranchLine(dlChoice);
                if (gb2 && nb != null) { BruteChannelBox.Text = nb; await RunAdvancedBruteforceFromUiAsync(); ShowHeader(); continue; }
                if (nb != null)
                {
                    ShowHeader();
                    ctx = nb.Equals("Game_All", StringComparison.OrdinalIgnoreCase) ||
                          nb.Equals("Launcher_All", StringComparison.OrdinalIgnoreCase)
                        ? await ScanAllBranchesAsync(nb.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher" : "Game", ns)
                        : await ScanBranchAsync(nb, ns, nv);
                    if (ctx == null) { ShowHeader(); break; }
                    SetHint("<version>  or  <branch>  or  <branch -b>");
                    continue;
                }

                AppendText("  Invalid input. Enter a version like '1.2.3.4', a branch like 'Game_Test OS', or add -b.", TC.Warn);
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
        new("  â†’ Run ", TC.Dim),
        new("game bruteforce", TC.Info, true)
            });

            AppendLine(new TermSpan[]
            {
        new("  Game_<Channel> -b", TC.Release, true),
        new("  â†’ Run ", TC.Dim),
        new("advanced bruteforce panel", TC.Info, true)
            });

            AppendLine(new TermSpan[]
            {
        new("  Game_<Channel> CN", TC.Release, true),
        new("  â†’ Scan ", TC.Dim),
        new("CN builds for that branch", TC.Info)
            });

            AppendLine(new TermSpan[]
            {
        new("  Game_<Channel> <Version>", TC.Release, true),
        new("  â†’ Scan for a ", TC.Dim),
        new("hidden version", TC.Info, true),
        new(" on that branch", TC.Dim)
            });

            AppendLine(new TermSpan[]
            {
        new("  Game_All / Launcher_All", TC.Release, true),
        new("  â†’ Merge every known branch with dates and matches", TC.Dim)
            });

            AppendLine([]);

            AppendLine(new TermSpan[]
            {
        new("  You can also paste a direct URL to download files.", TC.Dim)
            });

            AppendLine(new TermSpan[]
            {
        new("  Confirmed downloads run in the background, so you can start another build immediately.", TC.Dim)
            });

            AppendLine([]);
        }

        async Task<ScanContext?> ScanAllBranchesAsync(string mode, string? allowedSource)
        {
            var sourceCodes = allowedSource?.ToLowerInvariant() switch
            {
                "os" => new[] { "OS" },
                "cn" => new[] { "CN", "QQ" },
                "pc" => new[] { "PC" },
                "qq" => new[] { "QQ" },
                _ => new[] { "OS", "CN", "PC", "QQ" },
            };
            var branchJobs = sourceCodes
                .SelectMany(source => BranchCatalog.Get(source, mode).Select(branch => (source, branch)))
                .Distinct().ToList();
            var publicEntries = new List<AllBuildSeed>();
            var publicKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var gate = new object();
            var started = Stopwatch.StartNew();
            var completed = 0;

            ShowLoader($"Manifesting branches [0/{branchJobs.Count}]  ETA --:--");
            await Parallel.ForEachAsync(branchJobs,
                new ParallelOptions { MaxDegreeOfParallelism = 16 },
                async (job, _) =>
                {
                    var root = RootForSource(job.source);
                    var (versions, reverse) = await GetVersionsQuietAsync($"{root}/{job.branch}/manifest.txt");
                    lock (gate)
                    {
                        foreach (var version in versions)
                        {
                            var resolved = mode.Equals("Launcher", StringComparison.OrdinalIgnoreCase)
                                ? job.branch
                                : reverse.TryGetValue(version, out var choices) && choices.Count > 0
                                    ? choices.FirstOrDefault(value => value.Equals(job.branch, StringComparison.OrdinalIgnoreCase)) ?? choices.First()
                                    : job.branch;
                            var key = $"{job.source}|{job.branch}|{version}";
                            if (!publicKeys.Add(key)) continue;
                            publicEntries.Add(new AllBuildSeed(version, job.branch, resolved, job.source, false));
                        }
                    }
                    var done = Interlocked.Increment(ref completed);
                    var remaining = TimeSpan.FromTicks((long)(started.Elapsed.Ticks *
                        Math.Max(0, branchJobs.Count - done) / (double)Math.Max(1, done)));
                    UpdateLoaderProgress(done, branchJobs.Count,
                        $"Manifesting branches [{done}/{branchJobs.Count}]  ETA {FormatEta(remaining)}");
                });
            HideLoader();

            var seeds = new List<AllBuildSeed>(publicEntries);
            if (_cfg.DispatchWinBuild)
            {
                foreach (var entry in WinBuildCatalog.Load())
                {
                    if (!entry.Mode.Equals(mode, StringComparison.OrdinalIgnoreCase) ||
                        !sourceCodes.Contains(entry.Source, StringComparer.OrdinalIgnoreCase)) continue;
                    var key = $"{entry.Source}|{entry.Branch}|{entry.Version}";
                    if (publicKeys.Contains(key)) continue;
                    seeds.Add(new AllBuildSeed(entry.Version, entry.Branch, entry.Branch, entry.Source, true));
                }
            }

            seeds = seeds.DistinctBy(seed =>
                $"{seed.Source}|{seed.DisplayBranch}|{seed.Version}", StringComparer.OrdinalIgnoreCase).ToList();
            if (seeds.Count == 0)
            {
                AppendText($"  No {mode.ToLowerInvariant()} builds were found.", TC.Warn);
                return null;
            }

            var probes = new ConcurrentDictionary<string, Lazy<Task<AllBuildProbe>>>(StringComparer.OrdinalIgnoreCase);
            var occurrences = new ConcurrentBag<AllBuildOccurrence>();
            started.Restart();
            completed = 0;
            ShowLoader($"Identifying builds [0/{seeds.Count}]  ETA --:--");
            await Parallel.ForEachAsync(seeds,
                new ParallelOptions { MaxDegreeOfParallelism = 32 },
                async (seed, _) =>
                {
                    var probeKey = $"{seed.Source}|{seed.ResolvedBranch}|{seed.Version}|{mode}";
                    var lazy = probes.GetOrAdd(probeKey, _ => new Lazy<Task<AllBuildProbe>>(
                        () => ProbeAllBuildAsync(seed.Source, seed.ResolvedBranch, seed.Version, mode),
                        LazyThreadSafetyMode.ExecutionAndPublication));
                    var probe = await lazy.Value;
                    if (!seed.Hidden || probe.Exists)
                        occurrences.Add(new AllBuildOccurrence(seed, probe));
                    var done = Interlocked.Increment(ref completed);
                    var remaining = TimeSpan.FromTicks((long)(started.Elapsed.Ticks *
                        Math.Max(0, seeds.Count - done) / (double)Math.Max(1, done)));
                    UpdateLoaderProgress(done, seeds.Count,
                        $"Identifying builds [{done}/{seeds.Count}]  ETA {FormatEta(remaining)}");
                });
            HideLoader();

            var found = occurrences.ToList();
            if (found.Count == 0)
            {
                AppendText($"  No valid {mode.ToLowerInvariant()} builds were found.", TC.Warn);
                return null;
            }

            var maps = BuildAllContextMaps(found);
            foreach (var region in new[] { "OS", "CN" })
            {
                var regionBuilds = region == "OS"
                    ? found.Where(item => item.Seed.Source == "OS").ToList()
                    : found.Where(item => item.Seed.Source != "OS").ToList();
                if (!_cfg.ShowDevkits)
                    regionBuilds = regionBuilds.Where(item => !IsDevkitType(item.Probe.Type)).ToList();
                if (regionBuilds.Count == 0) continue;

                var groups = regionBuilds.GroupBy(item => item.Seed.Version, StringComparer.OrdinalIgnoreCase)
                    .Select(group =>
                    {
                        var ordered = group.OrderBy(item => item.Probe.Exists ? 0 : 1)
                            .ThenBy(item => item.Probe.Date ?? DateTime.MaxValue)
                            .ThenBy(item => item.Seed.Source == "CN" ? 0 : item.Seed.Source == "PC" ? 1 : 2)
                            .ToList();
                        var branches = group.Select(item => BranchCatalog.ShortName(item.Seed.DisplayBranch))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
                        var dated = group.Where(item => item.Probe.Date.HasValue)
                            .Select(item => item.Probe.Date!.Value).ToList();
                        return new AllBuildGroup(group.Key, ordered[0], branches,
                            group.All(item => item.Seed.Hidden), group.Any(item => item.Probe.Exists),
                            dated.Count == 0 ? null : dated.Min());
                    })
                    .OrderBy(group => group.Exists ? 1 : 0)
                    .ThenBy(group => group.Date ?? DateTime.MaxValue)
                    .ThenBy(group => group.Version, new VersionComparer()).ToList();

                AppendLine([]);
                var regionName = region == "OS" ? "OS Strinowa" : "CN Strinowa";
                AppendLine([new($"  {regionName} {mode} All builds ({groups.Count}):", TC.Bold, true)]);
                var width = groups.Max(group => group.Version.Length) + 2;
                var tagWidth = groups.Max(group =>
                    (group.Branches.Count > 1 ? "[Multi]" : $"[{group.Branches[0]}]").Length) + 2;
                foreach (var group in groups)
                {
                    var primary = group.Primary;
                    var tag = group.Branches.Count > 1 ? "Multi" : group.Branches[0];
                    var tagText = $"[{tag}]";
                    var color = !group.Exists ? TC.Deprecated : primary.Probe.Type == "Devkit"
                        ? TC.Devkit : region == "OS" ? TC.Release : TC.Normal;
                    var date = group.Date.HasValue
                        ? group.Date.Value.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'") : "—";
                    var line = new List<TermSpan>
                    {
                        new("  ", TC.Normal),
                        new(group.Version.PadRight(width), color),
                        new($"  {tagText.PadRight(tagWidth)}", color),
                    };
                    if (group.Date.HasValue) line.Add(new($"  {date}", TC.Dim));
                    var root = RootForSource(primary.Seed.Source);
                    AppendClickableLine(line, $"dl {group.Version} {primary.Seed.Source.ToLowerInvariant()}",
                        BuildFileListUrl(root, primary.Seed.ResolvedBranch, group.Version), group.Hidden, group.Branches);
                }
            }

            var context = new ScanContext($"{mode}_All", allowedSource, null, maps,
                BuildAllDownloadChoices(found));
            _lastScanCtx = context;
            return context;
        }

        static string RootForSource(string source) => source.ToUpperInvariant() switch
        {
            "OS" => OS_ROOT,
            "CN" => CN_ALL_ROOT,
            "PC" => PC_ROOT,
            _ => QQ_ROOT,
        };

        static string FormatEta(TimeSpan value) => value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes:00}:{value.Seconds:00}";

        async Task<(List<string> vers, Dictionary<string, HashSet<string>> revmap)> GetVersionsQuietAsync(string url)
        {
            try
            {
                var text = await Http.GetStringAsync(url);
                if (string.IsNullOrWhiteSpace(text) || text.Contains("NoSuchKey") || text.Contains("does not exist"))
                    return ([], new Dictionary<string, HashSet<string>>());
                return ParseIndexText(text);
            }
            catch
            {
                return ([], new Dictionary<string, HashSet<string>>());
            }
        }

        async Task<AllBuildProbe> ProbeAllBuildAsync(string source, string branch, string version, string mode)
        {
            var root = RootForSource(source);
            if (mode.Equals("Launcher", StringComparison.OrdinalIgnoreCase))
            {
                var launcherName = branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase)
                    ? branch["Launcher_".Length..] : branch;
                var installerPrefix = source.Equals("OS", StringComparison.OrdinalIgnoreCase) ? "Strinova" : "Calabiyau";
                var archiveUrl = $"{root}/{branch}/{version}/full_{version}.7z";
                var installerUrl = $"{root}/{branch}/{version}/{installerPrefix}_Installer_{launcherName}_{version}.exe";
                var archiveTask = ProbeUrlAsync(archiveUrl);
                var installerTask = ProbeUrlAsync(installerUrl);
                await Task.WhenAll(archiveTask, installerTask);
                var archive = archiveTask.Result;
                var installer = installerTask.Result;
                var dates = new[] { archive.ts, installer.ts }.Where(date => date.HasValue)
                    .Select(date => date!.Value).ToList();
                if (dates.Count == 0)
                    dates.AddRange(await ProbeBuildDateFallbacksAsync(root, branch, version));
                return new AllBuildProbe(archive.ok || installer.ok,
                    dates.Count == 0 ? null : dates.Min(), "Release");
            }

            var (exists, date) = await ProbeUrlAsync(BuildFileListUrl(root, branch, version));
            if (!date.HasValue)
            {
                var dates = await ProbeBuildDateFallbacksAsync(root, branch, version);
                date = dates.Count == 0 ? null : dates.Min();
            }
            return new AllBuildProbe(exists, date, "Release");
        }

        async Task<List<DateTime>> ProbeBuildDateFallbacksAsync(string root, string branch, string version)
        {
            var versionManifest = ProbeUrlAsync($"{root}/{branch}/{version}/manifest.txt");
            var hiddenManifest = ProbeUrlAsync($"{root}/{branch}/manifest.txt_{version}");
            await Task.WhenAll(versionManifest, hiddenManifest);
            return new[] { versionManifest.Result.ts, hiddenManifest.Result.ts }
                .Where(date => date.HasValue).Select(date => date!.Value).ToList();
        }

        Dictionary<string, (string root, List<string> vers, Dictionary<string, string> types,
            Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists,
            Dictionary<string, string> choice)> BuildAllContextMaps(List<AllBuildOccurrence> occurrences)
        {
            var maps = new Dictionary<string, (string root, List<string> vers, Dictionary<string, string> types,
                Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists,
                Dictionary<string, string> choice)>();
            foreach (var sourceGroup in occurrences.GroupBy(item => item.Seed.Source, StringComparer.OrdinalIgnoreCase))
            {
                var versions = new List<string>();
                var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var dates = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
                var exists = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                var choice = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var versionGroup in sourceGroup.GroupBy(item => item.Seed.Version, StringComparer.OrdinalIgnoreCase))
                {
                    var primary = versionGroup.OrderBy(item => item.Probe.Exists ? 0 : 1)
                        .ThenBy(item => item.Probe.Date ?? DateTime.MaxValue).First();
                    versions.Add(versionGroup.Key);
                    types[versionGroup.Key] = primary.Probe.Type;
                    var dated = versionGroup.Where(item => item.Probe.Date.HasValue)
                        .Select(item => item.Probe.Date!.Value).ToList();
                    dates[versionGroup.Key] = dated.Count == 0 ? null : dated.Min();
                    exists[versionGroup.Key] = versionGroup.Any(item => item.Probe.Exists);
                    choice[versionGroup.Key] = primary.Seed.ResolvedBranch;
                }
                maps[sourceGroup.Key] = (RootForSource(sourceGroup.Key), versions, types, dates, exists, choice);
            }
            return maps;
        }

        static string MultiChoiceKey(string version, string source) =>
            $"{(source.Equals("OS", StringComparison.OrdinalIgnoreCase) ? "OS" : "CN")}|{version}";

        Dictionary<string, List<AllDownloadChoice>> BuildAllDownloadChoices(List<AllBuildOccurrence> occurrences)
        {
            var result = new Dictionary<string, List<AllDownloadChoice>>(StringComparer.OrdinalIgnoreCase);
            foreach (var regionGroup in occurrences.GroupBy(item =>
                         item.Seed.Source.Equals("OS", StringComparison.OrdinalIgnoreCase) ? "OS" : "CN"))
            {
                foreach (var versionGroup in regionGroup.GroupBy(item => item.Seed.Version, StringComparer.OrdinalIgnoreCase))
                {
                    var choices = versionGroup
                        .GroupBy(item => item.Seed.DisplayBranch, StringComparer.OrdinalIgnoreCase)
                        .Select(branchGroup =>
                        {
                            var primary = branchGroup.OrderBy(item => item.Probe.Exists ? 0 : 1)
                                .ThenBy(item => item.Probe.Date ?? DateTime.MaxValue)
                                .ThenBy(item => item.Seed.Source == "CN" ? 0 : item.Seed.Source == "PC" ? 1 : 2)
                                .First();
                            var dates = branchGroup.Where(item => item.Probe.Date.HasValue)
                                .Select(item => item.Probe.Date!.Value).ToList();
                            return new AllDownloadChoice(primary.Seed.Version, primary.Seed.DisplayBranch,
                                primary.Seed.ResolvedBranch, primary.Seed.Source,
                                dates.Count == 0 ? null : dates.Min(),
                                branchGroup.Any(item => item.Seed.Hidden),
                                branchGroup.Any(item => item.Probe.Exists), null);
                        })
                        .OrderBy(choice => choice.Date ?? DateTime.MaxValue)
                        .ThenBy(choice => choice.DisplayBranch, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (choices.Count > 1)
                        result[$"{regionGroup.Key}|{versionGroup.Key}"] = choices;
                }
            }
            return result;
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
            var publicManifestTasks = new Dictionary<string, Task<(List<string> vers, Dictionary<string, HashSet<string>> revmap)>>();
            if (hiddenVersion != null)
            {
                foreach (var code in new[] { "OS", "CN", "PC", "QQ" })
                {
                    if (allowedSource != null && !code.Equals(allowedSource, StringComparison.OrdinalIgnoreCase)) continue;
                    var root = code switch { "OS" => OS_ROOT, "CN" => CN_ROOT, "PC" => PC_ROOT, _ => QQ_ROOT };
                    publicManifestTasks[code] = GetVersionsAsync($"{root}/{branch}/manifest.txt");
                }
            }

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
            if (publicManifestTasks.Count > 0) await Task.WhenAll(publicManifestTasks.Values);
            var publicVersions = results.ToDictionary(
                pair => pair.Key,
                pair => new HashSet<string>(
                    publicManifestTasks.TryGetValue(pair.Key, out var publicTask)
                        ? publicTask.Result.vers
                        : pair.Value.vers,
                    StringComparer.OrdinalIgnoreCase));
            var hiddenBySource = results.Keys.ToDictionary(
                code => code,
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var mode = branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher" : "Game";

            foreach (var entry in _cfg.DispatchWinBuild ? WinBuildCatalog.Load() : [])
            {
                if (!entry.Branch.Equals(branch, StringComparison.OrdinalIgnoreCase) ||
                    !entry.Mode.Equals(mode, StringComparison.OrdinalIgnoreCase) ||
                    !results.TryGetValue(entry.Source, out var result) ||
                    allowedSource != null && !entry.Source.Equals(allowedSource, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!result.vers.Contains(entry.Version, StringComparer.OrdinalIgnoreCase))
                    result.vers.Add(entry.Version);
                if (!result.revmap.TryGetValue(entry.Version, out var branches))
                    result.revmap[entry.Version] = branches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                branches.Add(entry.Branch);
                if (!publicVersions[entry.Source].Contains(entry.Version))
                    hiddenBySource[entry.Source].Add(entry.Version);
            }

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

            Dictionary<string, ChinaBuildStatus>? chinaBuilds = null;
            if (!branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) &&
                (results["CN"].vers.Count > 0 || results["PC"].vers.Count > 0))
            {
                chinaBuilds = new Dictionary<string, ChinaBuildStatus>(StringComparer.OrdinalIgnoreCase);
                var chinaVersions = results["CN"].vers.Concat(results["PC"].vers)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var chinaGate = new object();
                await Parallel.ForEachAsync(chinaVersions,
                    new ParallelOptions { MaxDegreeOfParallelism = 32 },
                    async (version, _) =>
                    {
                        var cnKnown = maps.TryGetValue("CN", out var cnMap) && cnMap.exists.ContainsKey(version);
                        var pcKnown = maps.TryGetValue("PC", out var pcMap) && pcMap.exists.ContainsKey(version);
                        var cnExists = cnKnown && cnMap.exists.GetValueOrDefault(version);
                        var pcExists = pcKnown && pcMap.exists.GetValueOrDefault(version);
                        var cnDate = cnKnown ? cnMap.dates.GetValueOrDefault(version) : null;
                        var pcDate = pcKnown ? pcMap.dates.GetValueOrDefault(version) : null;
                        var resolvedBranch = cnKnown
                            ? cnMap.choice.GetValueOrDefault(version, branch)
                            : pcKnown ? pcMap.choice.GetValueOrDefault(version, branch) : branch;

                        if (!cnKnown)
                            (cnExists, cnDate) = await ProbeUrlAsync(BuildFileListUrl(CN_ROOT, resolvedBranch, version));
                        if (!pcKnown)
                            (pcExists, pcDate) = await ProbeUrlAsync(BuildFileListUrl(PC_ROOT, resolvedBranch, version));

                        var dates = new[] { cnExists ? cnDate : null, pcExists ? pcDate : null }
                            .Where(date => date.HasValue).Select(date => date!.Value).ToList();
                        var status = new ChinaBuildStatus(cnExists || pcExists,
                            dates.Count == 0 ? null : dates.Min());
                        lock (chinaGate) chinaBuilds[version] = status;
                    });

                foreach (var code in new[] { "CN", "PC" })
                {
                    if (!maps.TryGetValue(code, out var map)) continue;
                    foreach (var version in map.vers)
                        if (chinaBuilds.TryGetValue(version, out var status)) map.dates[version] = status.Date;
                }
            }

            foreach (var code in new[] { "OS", "CN", "PC", "QQ" })
            {
                if (!maps.TryGetValue(code, out var map)) continue;
                map.vers.RemoveAll(version => hiddenBySource[code].Contains(version) &&
                    !(chinaBuilds != null && (code == "CN" || code == "PC")
                        ? chinaBuilds.GetValueOrDefault(version)?.Exists == true
                        : map.exists.GetValueOrDefault(version)));
                hiddenBySource[code].RemoveWhere(version =>
                    !map.vers.Contains(version, StringComparer.OrdinalIgnoreCase));
            }

            AppendLine([]);

            var os = results["OS"].vers;
            var cn = results["CN"].vers;
            var pc = results["PC"].vers;

            if ((allowedSource == null || allowedSource == "os")
                && os.Count > 0 && maps.ContainsKey("OS"))
            {
                var m = maps["OS"];
                PrintVersionGroup($"OS {branch}", os, m.types, m.dates, m.exists,
                    branch, m.root, m.choice, "OS", hiddenBySource["OS"]);
            }
            if ((allowedSource == null || allowedSource == "pc" || allowedSource == "cn")
                && (pc.Count > 0 || cn.Count > 0))
            {
                if (pc.Count > 0 && maps.ContainsKey("PC"))
                {
                    var mpc = maps["PC"];
                    maps.TryGetValue("CN", out var mcnEntry);
                    var merged = pc.Concat(cn).Distinct().Where(v =>
                    {
                        var type = mcnEntry.types != null && mcnEntry.types.ContainsKey(v)
                            ? mcnEntry.types[v]
                            : mpc.types.GetValueOrDefault(v, "Devkit");
                        return _cfg.ShowDevkits || !IsDevkitType(type);
                    }).ToList();
                    if (merged.Count > 0)
                    {
                    var mergeTitle = $"{Strings.Get("china_client")} {(branch.StartsWith("Game_", StringComparison.OrdinalIgnoreCase) ? branch[5..] : branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher " + branch[9..] : branch)}";
                    AppendLine([]);
                    AppendLine([new($"  {mergeTitle} {Strings.Get("builds")} ({merged.Count}):", TC.Bold, true)]);

                    int colW = merged.Max(v => v.Length) + 2;

                    DateTime? MergedDate(string version) =>
                        chinaBuilds?.GetValueOrDefault(version)?.Date ??
                        (mcnEntry.dates != null && mcnEntry.dates.ContainsKey(version)
                            ? mcnEntry.dates[version]
                            : mpc.dates.GetValueOrDefault(version));
                    bool MergedExists(string version) =>
                        chinaBuilds?.GetValueOrDefault(version)?.Exists ??
                        (mpc.exists.ContainsKey(version)
                            ? mpc.exists[version]
                            : mcnEntry.exists == null || !mcnEntry.exists.ContainsKey(version) || mcnEntry.exists[version]);
                    merged = merged
                        .OrderBy(v => MergedExists(v) ? 1 : 0)
                        .ThenBy(v => MergedDate(v) ?? DateTime.MaxValue)
                        .ThenBy(v => v, new VersionComparer())
                        .ToList();

                    foreach (var v in merged)
                    {
                        bool inCN = cn.Contains(v);

                        var type =
                            mcnEntry.types != null && mcnEntry.types.ContainsKey(v) ? mcnEntry.types[v] :
                            mpc.types.GetValueOrDefault(v, "Devkit");
                        if (type == "Development") type = "Devkit";

                        var date = MergedDate(v);

                        var exists = MergedExists(v);

                        SolidColorBrush color;
                        if (!exists) color = TC.Deprecated;
                        else if (type == "Devkit") color = TC.Devkit;
                        else if (inCN) color = TC.Normal;
                        else color = TC.CN;

                        var dtStr = date.HasValue ? date.Value.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'") : "â€”";
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
                        var sourceEntry = inCN && !string.IsNullOrEmpty(mcnEntry.root) ? mcnEntry : mpc;
                        var resolvedBranch = sourceEntry.choice != null
                            ? sourceEntry.choice.GetValueOrDefault(v, branch)
                            : branch;
                        var hiddenBuild = !publicVersions["CN"].Contains(v) && !publicVersions["PC"].Contains(v);
                        AppendClickableLine(line, $"dl {v} {dlSrc}",
                            BuildFileListUrl(sourceEntry.root, resolvedBranch, v), hiddenBuild);
                    }
                    }
                }
                else if (cn.Count > 0 && maps.ContainsKey("CN"))
                {
                    var mcn = maps["CN"];
                    PrintVersionGroup(branch, cn, mcn.types, mcn.dates, mcn.exists,
                        branch, mcn.root, mcn.choice, "CN", hiddenBySource["CN"]);
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

        static bool IsDevkitType(string? type) =>
            type?.Equals("Devkit", StringComparison.OrdinalIgnoreCase) == true ||
            type?.Equals("Development", StringComparison.OrdinalIgnoreCase) == true;

        void PrintVersionGroup(string label, List<string> versions, Dictionary<string, string> types,
            Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists, string branch,
            string root, Dictionary<string, string> choice, string source = "OS",
            HashSet<string>? hiddenVersions = null)
        {
            if (!_cfg.ShowDevkits)
                versions = versions.Where(v => !IsDevkitType(types.GetValueOrDefault(v, "Devkit"))).ToList();
            if (versions.Count == 0) return;

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

            var orderedVersions = versions
                .OrderBy(v => exists.TryGetValue(v, out var present) && !present ? 0 : 1)
                .ThenBy(v => dates.GetValueOrDefault(v) ?? DateTime.MaxValue)
                .ThenBy(v => v, new VersionComparer())
                .ToList();
            var colW = orderedVersions.Max(v => v.Length) + 2;
            foreach (var v in orderedVersions)
            {
                var t = types.GetValueOrDefault(v, "Devkit");
                if (t == "Development") t = "Devkit";
                var removed = exists.TryGetValue(v, out var ex) && !ex;

                SolidColorBrush color;
                if (removed) color = TC.Deprecated;
                else if (t == "Devkit") color = TC.Devkit;
                else color = sourceColor;

                var dt = dates.GetValueOrDefault(v);
                var dtStr = dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'") : "â€”";
                var tag = removed ? $"[{Strings.Get("removed")}]" : $"[{t}]";

                var line = new List<TermSpan>
                {
                    new("  ", TC.Normal),
                    new($"{v.PadRight(colW)}", color),
                    new($"  {tag,-15}", color),
                };
                if (!removed)
                    line.Add(new($"  {dtStr}", TC.Dim));

                var resolvedBranch = choice.GetValueOrDefault(v, branch);
                AppendClickableLine(line, $"dl {v} {source.ToLower()}",
                    BuildFileListUrl(root, resolvedBranch, v), hiddenVersions?.Contains(v) == true);
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
                new ParallelOptions { MaxDegreeOfParallelism = 32 },
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

        async Task CeciliaDownloadAsync(string baseRoot, string branch, string version,
            string? manifestTextOpt, bool skipConfirmation = false)
        {
            var downloadRegion = baseRoot.Equals(OS_ROOT, StringComparison.OrdinalIgnoreCase) ? "OS" : "CN";
            var versionDirectory = $"{downloadRegion}_{branch}-{version}";
            ShowDownloadPreparation($"Preparing {branch} {version}", "Checking download files…");
            if (branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase))
            {
                var launcherName = branch["Launcher_".Length..];
                var installerPrefix = baseRoot.Equals(OS_ROOT, StringComparison.OrdinalIgnoreCase) ? "Strinova" : "Calabiyau";
                var exeFileName = $"{installerPrefix}_Installer_{launcherName}_{version}.exe";
                var lUrl7z = $"{baseRoot}/{branch}/{version}/full_{version}.7z";
                var lUrlExe = $"{baseRoot}/{branch}/{version}/{exeFileName}";
                var (has7z, _) = await ProbeUrlAsync(lUrl7z);
                var (hasExe, _) = await ProbeUrlAsync(lUrlExe);

                if (!has7z && !hasExe)
                {
                    RestoreDownloadPanelAfterPreparation();
                    ShowClipboardToast("This version is inaccessible or has been deleted");
                    return;
                }

                ShowDownloadPreparation($"Preparing {branch} {version}", "Reading file sizes…");
                var lSz = has7z ? await HeadSizeAsync(lUrl7z) : null;
                var eSz = hasExe ? await HeadSizeAsync(lUrlExe) : null;
                bool dl7z;
                bool dlExe;
                if (skipConfirmation)
                {
                    dl7z = has7z && AppSettings.LauncherDefault7z;
                    dlExe = hasExe && AppSettings.LauncherDefaultExe;
                    if (!dl7z && !dlExe)
                    {
                        dl7z = has7z;
                        dlExe = !has7z && hasExe;
                    }
                }
                else
                {
                    var selection = await ConfirmLauncherDownloadInPanelAsync(
                        $"Download {branch} {version}", has7z, lSz, hasExe, eSz);
                    if (!selection.Confirmed) return;
                    dl7z = selection.Download7z;
                    dlExe = selection.DownloadExe;
                }

                var launcherDownloads = new List<(string key, string label, string url, string dest, long size, string description)>();
                if (dl7z && has7z)
                {
                    var dest = Path.Combine("Launcher", versionDirectory, $"full_{version}.7z");
                    launcherDownloads.Add(($"launcher|{lUrl7z}", $"Downloading {version}.7z",
                        lUrl7z, dest, lSz ?? 0, $"7z {FormatSize(lSz)}"));
                }
                if (dlExe && hasExe)
                {
                    var dest = Path.Combine("Launcher", versionDirectory, exeFileName);
                    launcherDownloads.Add(($"launcher|{lUrlExe}", $"Downloading {exeFileName}",
                        lUrlExe, dest, eSz ?? 0, $"EXE {FormatSize(eSz)}"));
                }

                if (launcherDownloads.Count == 0) return;

                foreach (var item in launcherDownloads)
                    ScheduleSingleFileDownload(item.key, item.label, item.url, item.dest, item.size);

                return;
            }



            string manifestText;
            if (manifestTextOpt == null)
            {
                var vmUrl = $"{baseRoot}/{branch}/{version}/full_zip/manifest.txt";
                ShowDownloadPreparation($"Preparing {branch} {version}", "Fetching version manifest…");
                try
                {
                    manifestText = await Http.GetStringAsync(vmUrl);
                }
                catch
                {
                    RestoreDownloadPanelAfterPreparation();
                    AppendText("  Version manifest not available.", TC.Warn); return;
                }
            }
            else manifestText = manifestTextOpt;

            var triples = ParseVersionManifest(manifestText);
            if (triples.Count == 0)
            {
                RestoreDownloadPanelAfterPreparation();
                AppendText("  Manifest has no files.", TC.Warn);
                return;
            }

            var items = triples.Select(t =>
            {
                var url = $"{baseRoot}/{t.rel.TrimStart('/')}";
                var dest = Path.Combine("Game", versionDirectory, Path.GetFileName(t.rel));
                return new VersionItem(t.rel, t.branch, t.version, url, dest) { Size = null };
            }).ToList();

            ShowDownloadPreparation($"Preparing {branch} {version}", "Fetching file sizes…");
            await Parallel.ForEachAsync(items,
                new ParallelOptions { MaxDegreeOfParallelism = 32 },
                async (it, _) => { it.Size = await HeadSizeAsync(it.Url); });

            long? grand = items.All(i => i.Size.HasValue) ? items.Sum(i => i.Size!.Value) : (long?)null;

            var downloadDirectory = Path.Combine("Game", versionDirectory);
            Directory.CreateDirectory(downloadDirectory);
            var extractedArchives = LoadExtractedArchiveKeys(downloadDirectory);

            bool ItemIsComplete(VersionItem item) =>
                (File.Exists(item.Dest) &&
                 (!item.Size.HasValue || new FileInfo(item.Dest).Length == item.Size.Value)) ||
                (IsGameArchive(item.Dest) && extractedArchives.Contains(item.RelPath));

            var pending = items.Where(it => !ItemIsComplete(it)).ToList();

            long alreadyDone = items.Where(ItemIsComplete).Where(it => it.Size.HasValue)
                                    .Sum(it => it.Size!.Value);

            if (pending.Count == 0)
            {
                RestoreDownloadPanelAfterPreparation();
                AppendText("  All files are already downloaded.", TC.Ok);
                return;
            }

            var remaining = pending.All(i => i.Size.HasValue) ? pending.Sum(i => i.Size!.Value) : (long?)null;
            if (!skipConfirmation && !await ConfirmDownloadInPanelAsync(
                $"Download {branch} {version}", $"{pending.Count:N0} files  •  {FormatSize(remaining)}"))
                return;

            var job = TryStartDownloadJob(
                $"game|{baseRoot}|{branch}|{version}",
                $"Downloading {branch} {version}",
                pending.Select(item => item.Dest),
                grand ?? 0,
                items.Count,
                alreadyDone,
                items.Count - pending.Count);
            if (job == null)
            {
                AppendText("  That download is already active.", TC.Warn);
                return;
            }

            _ = RunGameDownloadJobAsync(job, items, pending);
        }

        void ShowDownloadPreparation(string label, string details)
        {
            if (_downloadConfirmSource != null) return;
            _downloadCompletionVisible = false;
            _completedDownloadFolder = null;
            DownloadCompletionActions.Visibility = Visibility.Collapsed;
            _downloadPreparationVisible = true;
            DownloadPanel.Visibility = Visibility.Visible;
            DlLabel.Text = label;
            DlStats.Text = details;
            DlStats.Visibility = Visibility.Visible;
            PauseBtn.Visibility = Visibility.Collapsed;
            DownloadConfirmBtn.Visibility = Visibility.Collapsed;
            DownloadCancelBtn.Visibility = Visibility.Collapsed;
            LauncherDownloadConfirmBtn.Visibility = Visibility.Collapsed;
            LauncherDownloadCancelBtn.Visibility = Visibility.Collapsed;
            LauncherFormatPanel.Visibility = Visibility.Collapsed;
            MultiDownloadPanel.Visibility = Visibility.Collapsed;
            DlTrack.Visibility = Visibility.Collapsed;
            DlFooterGrid.Visibility = Visibility.Collapsed;
        }

        void RestoreDownloadPanelAfterPreparation()
        {
            _downloadPreparationVisible = false;
            PauseBtn.Visibility = Visibility.Visible;
            DlTrack.Visibility = Visibility.Visible;
            DlFooterGrid.Visibility = Visibility.Visible;
            if (ActiveDownloads().Length == 0) DownloadPanel.Visibility = Visibility.Collapsed;
            else UpdateDlBar(null, EventArgs.Empty);
        }

        async Task<bool> ConfirmDownloadInPanelAsync(string label, string details)
        {
            if (_downloadConfirmSource != null) return false;
            _downloadPreparationVisible = false;
            _downloadConfirmSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            DownloadPanel.Visibility = Visibility.Visible;
            DlLabel.Text = label;
            DlStats.Text = details;
            DlStats.Visibility = Visibility.Visible;
            PauseBtn.Visibility = Visibility.Collapsed;
            LauncherFormatPanel.Visibility = Visibility.Collapsed;
            MultiDownloadPanel.Visibility = Visibility.Collapsed;
            LauncherDownloadConfirmBtn.Visibility = Visibility.Collapsed;
            LauncherDownloadCancelBtn.Visibility = Visibility.Collapsed;
            DownloadConfirmBtn.Visibility = Visibility.Visible;
            DownloadCancelBtn.Visibility = Visibility.Visible;
            DownloadConfirmBtn.IsEnabled = true;
            DownloadConfirmBtn.Opacity = 1.0;
            DlTrack.Visibility = Visibility.Collapsed;
            DlFooterGrid.Visibility = Visibility.Collapsed;

            try
            {
                return await _downloadConfirmSource.Task;
            }
            finally
            {
                _downloadConfirmSource = null;
                DownloadConfirmBtn.Visibility = Visibility.Collapsed;
                DownloadCancelBtn.Visibility = Visibility.Collapsed;
                DownloadConfirmBtn.IsEnabled = true;
                DownloadConfirmBtn.Opacity = 1.0;
                PauseBtn.Visibility = Visibility.Visible;
                DlTrack.Visibility = Visibility.Visible;
                DlFooterGrid.Visibility = Visibility.Visible;
                if (ActiveDownloads().Length == 0) DownloadPanel.Visibility = Visibility.Collapsed;
                else UpdateDlBar(null, EventArgs.Empty);
            }
        }

        async Task<(bool Confirmed, bool Download7z, bool DownloadExe)> ConfirmLauncherDownloadInPanelAsync(
            string label, bool has7z, long? size7z, bool hasExe, long? sizeExe)
        {
            if (_downloadConfirmSource != null) return (false, false, false);
            _downloadPreparationVisible = false;
            _downloadConfirmSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _launcherFormatConfirmation = true;

            DownloadPanel.Visibility = Visibility.Visible;
            DlLabel.Text = label;
            DlStats.Visibility = Visibility.Visible;
            PauseBtn.Visibility = Visibility.Collapsed;
            DlTrack.Visibility = Visibility.Collapsed;
            DlFooterGrid.Visibility = Visibility.Collapsed;
            LauncherFormatPanel.Visibility = Visibility.Visible;
            MultiDownloadPanel.Visibility = Visibility.Collapsed;

            Launcher7zToggle.IsEnabled = has7z;
            LauncherExeToggle.IsEnabled = hasExe;
            _launcher7zSize = size7z;
            _launcherExeSize = sizeExe;
            Launcher7zToggle.IsChecked = has7z && AppSettings.LauncherDefault7z;
            LauncherExeToggle.IsChecked = hasExe && AppSettings.LauncherDefaultExe;
            if (Launcher7zToggle.IsChecked != true && LauncherExeToggle.IsChecked != true)
            {
                if (has7z) Launcher7zToggle.IsChecked = true;
                else if (hasExe) LauncherExeToggle.IsChecked = true;
            }

            DownloadConfirmBtn.Visibility = Visibility.Collapsed;
            DownloadCancelBtn.Visibility = Visibility.Collapsed;
            LauncherDownloadConfirmBtn.Visibility = Visibility.Visible;
            LauncherDownloadCancelBtn.Visibility = Visibility.Visible;
            UpdateLauncherDownloadButton();

            try
            {
                bool confirmed = await _downloadConfirmSource.Task;
                return (confirmed,
                    confirmed && has7z && Launcher7zToggle.IsChecked == true,
                    confirmed && hasExe && LauncherExeToggle.IsChecked == true);
            }
            finally
            {
                _launcherFormatConfirmation = false;
                _downloadConfirmSource = null;
                LauncherFormatPanel.Visibility = Visibility.Collapsed;
                DownloadConfirmBtn.Visibility = Visibility.Collapsed;
                LauncherDownloadConfirmBtn.Visibility = Visibility.Collapsed;
                LauncherDownloadCancelBtn.Visibility = Visibility.Collapsed;
                LauncherDownloadConfirmBtn.IsEnabled = true;
                LauncherDownloadConfirmBtn.Opacity = 1.0;
                PauseBtn.Visibility = Visibility.Visible;
                DlTrack.Visibility = Visibility.Visible;
                DlFooterGrid.Visibility = Visibility.Visible;
                if (ActiveDownloads().Length == 0) DownloadPanel.Visibility = Visibility.Collapsed;
                else UpdateDlBar(null, EventArgs.Empty);
            }
        }

        void LauncherFormatToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!_launcherFormatConfirmation) return;
            SaveLauncherFormatPreferencesFromPanel();
            UpdateLauncherDownloadButton();
        }

        void SaveLauncherFormatPreferencesFromPanel()
        {
            if (Launcher7zToggle.IsEnabled)
                AppSettings.LauncherDefault7z = Launcher7zToggle.IsChecked == true;
            if (LauncherExeToggle.IsEnabled)
                AppSettings.LauncherDefaultExe = LauncherExeToggle.IsChecked == true;

            _cfg.Fmt7z = AppSettings.LauncherDefault7z;
            _cfg.FmtExe = AppSettings.LauncherDefaultExe;
            _cfg.Save();
        }

        static string MultiDownloadChoiceId(AllDownloadChoice choice) =>
            $"{choice.Source}|{choice.ResolvedBranch}|{choice.Version}";

        async Task<List<AllDownloadChoice>> ConfirmMultiDownloadInPanelAsync(
            string version, List<AllDownloadChoice> choices)
        {
            if (_downloadConfirmSource != null) return [];
            ShowDownloadPreparation($"Preparing multi-download {version}", "Reading branch sizes…");
            choices = await PopulateMultiDownloadSizesAsync(choices);
            _downloadPreparationVisible = false;
            _downloadConfirmSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _multiDownloadChoices = choices.OrderBy(choice => choice.Date ?? DateTime.MaxValue)
                .ThenBy(choice => choice.DisplayBranch, StringComparer.OrdinalIgnoreCase).ToList();
            _multiDownloadSelected.Clear();

            DownloadPanel.Visibility = Visibility.Visible;
            DlLabel.Text = $"Download {version} from multiple branches";
            DlStats.Text = $"{choices.Count} branches";
            DlStats.Visibility = Visibility.Visible;
            PauseBtn.Visibility = Visibility.Collapsed;
            DownloadConfirmBtn.Visibility = Visibility.Collapsed;
            DownloadCancelBtn.Visibility = Visibility.Collapsed;
            LauncherDownloadConfirmBtn.Visibility = Visibility.Collapsed;
            LauncherDownloadCancelBtn.Visibility = Visibility.Collapsed;
            LauncherFormatPanel.Visibility = Visibility.Collapsed;
            MultiDownloadPanel.Visibility = Visibility.Visible;
            DlTrack.Visibility = Visibility.Collapsed;
            DlFooterGrid.Visibility = Visibility.Collapsed;
            RenderMultiDownloadPage();

            try
            {
                var confirmed = await _downloadConfirmSource.Task;
                return confirmed
                    ? _multiDownloadChoices.Where(choice =>
                        _multiDownloadSelected.Contains(MultiDownloadChoiceId(choice))).ToList()
                    : [];
            }
            finally
            {
                _downloadConfirmSource = null;
                MultiDownloadPanel.Visibility = Visibility.Collapsed;
                MultiChoiceRows.Children.Clear();
                _multiDownloadChoices = [];
                _multiDownloadSelected.Clear();
                PauseBtn.Visibility = Visibility.Visible;
                DlTrack.Visibility = Visibility.Visible;
                DlFooterGrid.Visibility = Visibility.Visible;
                if (ActiveDownloads().Length == 0) DownloadPanel.Visibility = Visibility.Collapsed;
                else UpdateDlBar(null, EventArgs.Empty);
            }
        }

        void RenderMultiDownloadPage()
        {
            MultiChoiceRows.Children.Clear();
            foreach (var choice in _multiDownloadChoices)
            {
                var id = MultiDownloadChoiceId(choice);
                var indicatorText = new TextBlock
                {
                    Text = _multiDownloadSelected.Contains(id) ? "✓" : "",
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                };
                var indicator = new Border
                {
                    Width = 18,
                    Height = 18,
                    CornerRadius = new CornerRadius(5),
                    BorderThickness = new Thickness(1.4),
                    BorderBrush = TC.Pink,
                    Background = _multiDownloadSelected.Contains(id) ? TC.Pink : Brushes.Transparent,
                    Child = indicatorText,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                row.Children.Add(indicator);
                var branchText = new TextBlock
                {
                    Text = $"{choice.Version}   {BranchCatalog.ShortName(choice.DisplayBranch)}",
                    Foreground = (Brush)Resources["ControlTextBrush"],
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = choice.DisplayBranch,
                };
                Grid.SetColumn(branchText, 1);
                row.Children.Add(branchText);
                var sourceText = new TextBlock
                {
                    Text = choice.Size.HasValue
                        ? $"{choice.Source}  {FormatMultiSize(choice.Size.Value)}"
                        : choice.Source,
                    Foreground = TC.Info,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(sourceText, 2);
                row.Children.Add(sourceText);
                var dateText = new TextBlock
                {
                    Text = choice.Date?.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'") ?? "Date unavailable",
                    Foreground = TC.Dim,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                };
                Grid.SetColumn(dateText, 3);
                row.Children.Add(dateText);
                if (choice.Hidden)
                {
                    var hidden = new TextBlock
                    {
                        Text = "⚑",
                        Foreground = TC.Warn,
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ToolTip = "This is a Hidden build. It is not stored on the public manifest.",
                    };
                    Grid.SetColumn(hidden, 4);
                    row.Children.Add(hidden);
                }
                var toggle = new System.Windows.Controls.Primitives.ToggleButton
                {
                    Style = (Style)FindResource("MultiChoiceRow"),
                    Content = row,
                    IsChecked = _multiDownloadSelected.Contains(id),
                    IsEnabled = choice.Exists,
                    Tag = id,
                    ToolTip = choice.Exists ? choice.DisplayBranch : "This build is listed, but its downloadable files are unavailable.",
                };
                void UpdateSelection(bool selected)
                {
                    if (selected) _multiDownloadSelected.Add(id);
                    else _multiDownloadSelected.Remove(id);
                    indicatorText.Text = selected ? "✓" : "";
                    indicator.Background = selected ? TC.Pink : Brushes.Transparent;
                    UpdateMultiDownloadSelectionState();
                }
                toggle.Checked += (_, _) => UpdateSelection(true);
                toggle.Unchecked += (_, _) => UpdateSelection(false);
                MultiChoiceRows.Children.Add(toggle);
            }
            UpdateMultiDownloadSelectionState();
        }

        void UpdateMultiDownloadSelectionState()
        {
            var count = _multiDownloadSelected.Count;
            MultiSelectionCount.Text = $"{count} of {_multiDownloadChoices.Count} selected";
            MultiDownloadBtn.Content = count == 0 ? "Download selected" : $"Download selected ({count})";
            MultiDownloadBtn.IsEnabled = count > 0;
            MultiDownloadBtn.Opacity = count > 0 ? 1 : 0.45;
        }

        void MultiSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var choice in _multiDownloadChoices)
                if (choice.Exists) _multiDownloadSelected.Add(MultiDownloadChoiceId(choice));
            RenderMultiDownloadPage();
        }

        void MultiClear_Click(object sender, RoutedEventArgs e)
        {
            _multiDownloadSelected.Clear();
            RenderMultiDownloadPage();
        }

        void MultiCancel_Click(object sender, RoutedEventArgs e) =>
            _downloadConfirmSource?.TrySetResult(false);

        void MultiDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_multiDownloadSelected.Count > 0)
                _downloadConfirmSource?.TrySetResult(true);
        }

        async Task<List<AllDownloadChoice>> PopulateMultiDownloadSizesAsync(List<AllDownloadChoice> choices)
        {
            var result = choices.ToArray();
            await Parallel.ForEachAsync(Enumerable.Range(0, result.Length),
                new ParallelOptions { MaxDegreeOfParallelism = 12 },
                async (index, _) =>
                {
                    var choice = result[index];
                    if (!choice.ResolvedBranch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase)) return;
                    var root = RootForSource(choice.Source);
                    var archiveUrl = $"{root}/{choice.ResolvedBranch}/{choice.Version}/full_{choice.Version}.7z";
                    var size = await HeadSizeAsync(archiveUrl);
                    if (!size.HasValue)
                    {
                        var launcherName = choice.ResolvedBranch["Launcher_".Length..];
                        var prefix = choice.Source.Equals("OS", StringComparison.OrdinalIgnoreCase) ? "Strinova" : "Calabiyau";
                        var installerUrl = $"{root}/{choice.ResolvedBranch}/{choice.Version}/{prefix}_Installer_{launcherName}_{choice.Version}.exe";
                        size = await HeadSizeAsync(installerUrl);
                    }
                    result[index] = choice with { Size = size };
                });
            return result.ToList();
        }

        static string FormatMultiSize(long bytes) => $"{bytes / 1048576d:F3}MB";

        void UpdateLauncherDownloadButton()
        {
            LauncherDownloadConfirmBtn.IsEnabled =
                (Launcher7zToggle.IsEnabled && Launcher7zToggle.IsChecked == true) ||
                (LauncherExeToggle.IsEnabled && LauncherExeToggle.IsChecked == true);
            LauncherDownloadConfirmBtn.Opacity = LauncherDownloadConfirmBtn.IsEnabled ? 1.0 : 0.45;

            var selectedSizes = new List<string>(2);
            if (Launcher7zToggle.IsEnabled && Launcher7zToggle.IsChecked == true)
                selectedSizes.Add($"7z {FormatSize(_launcher7zSize)}");
            if (LauncherExeToggle.IsEnabled && LauncherExeToggle.IsChecked == true)
                selectedSizes.Add($"EXE {FormatSize(_launcherExeSize)}");
            DlStats.Text = selectedSizes.Count > 0
                ? string.Join("  +  ", selectedSizes)
                : "Select a format";
        }

        void DownloadConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_launcherFormatConfirmation && !LauncherDownloadConfirmBtn.IsEnabled) return;
            if (_launcherFormatConfirmation) SaveLauncherFormatPreferencesFromPanel();
            _downloadConfirmSource?.TrySetResult(true);
        }

        void DownloadCancelBtn_Click(object sender, RoutedEventArgs e) =>
            _downloadConfirmSource?.TrySetResult(false);
        DownloadJob? TryStartDownloadJob(
            string key,
            string label,
            IEnumerable<string> destinations,
            long totalBytes,
            int fileCount,
            long completedBytes = 0,
            int completedFiles = 0)
        {
            var fullDestinations = destinations
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var job = new DownloadJob
            {
                Key = key,
                Label = label,
                Destinations = fullDestinations,
                TotalBytes = totalBytes,
                FileCount = fileCount,
                CompletedBytes = completedBytes,
                CompletedFiles = completedFiles,
            };

            lock (_downloadJobsLock)
            {
                if (_downloadJobs.ContainsKey(key) ||
                    fullDestinations.Any(_activeDownloadDestinations.Contains)) return null;
                _downloadJobs.Add(key, job);
                foreach (var destination in fullDestinations)
                    _activeDownloadDestinations.Add(destination);
            }

            Dispatcher.Invoke(() =>
            {
                _downloadCompletionVisible = false;
                _completedDownloadFolder = null;
                DownloadCompletionActions.Visibility = Visibility.Collapsed;
                DownloadPanel.Visibility = Visibility.Visible;
                var active = ActiveDownloads();
                _dlLastSampleBytes = active.Sum(item => Interlocked.Read(ref item.TransferredBytes));
                _dlLastSampleUtc = DateTime.UtcNow;
                if (_dlTimer == null)
                {
                    _dlTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    _dlTimer.Tick += UpdateDlBar;
                    _dlTimer.Start();
                }
                UpdateDlBar(null, EventArgs.Empty);
            });
            return job;
        }

        void CompleteDownloadJob(DownloadJob job, string? completedFolder = null, string? completedLabel = null)
        {
            lock (_downloadJobsLock)
            {
                _downloadJobs.Remove(job.Key);
                foreach (var destination in job.Destinations)
                    _activeDownloadDestinations.Remove(destination);
            }
            Dispatcher.Invoke(() =>
            {
                if (!string.IsNullOrWhiteSpace(completedFolder))
                {
                    _downloadCompletionVisible = true;
                    _completedDownloadFolder = Path.GetFullPath(completedFolder);
                    _completedDownloadLabel = completedLabel ?? "Download complete";
                    SoundEffects.DownloadFinished();
                }
                UpdateDlBar(null, EventArgs.Empty);
            });
        }

        void ShowCompletedDownload()
        {
            DownloadPanel.Visibility = Visibility.Visible;
            PauseBtn.Visibility = Visibility.Collapsed;
            DownloadConfirmBtn.Visibility = Visibility.Collapsed;
            DownloadCancelBtn.Visibility = Visibility.Collapsed;
            DownloadCompletionActions.Visibility = Visibility.Visible;
            DlLabel.Text = _completedDownloadLabel;
            DlStats.Text = "";
            DlSubLabel.Text = _completedDownloadFolder ?? "";
            DlEta.Text = "";
            DlFill.Margin = new Thickness(0);
            DlFill.Width = Math.Max(0, DownloadPanel.ActualWidth - 28);
        }

        bool IsOnlyActiveDownload(DownloadJob job)
        {
            lock (_downloadJobsLock)
                return _downloadJobs.Count == 1 && _downloadJobs.ContainsKey(job.Key);
        }

        bool CanClearLogFor(DownloadJob job) =>
            AppSettings.ClearLogOnFinish && !_busy && _promptSem == null && IsOnlyActiveDownload(job);

        DownloadJob[] ActiveDownloads()
        {
            lock (_downloadJobsLock) return _downloadJobs.Values.ToArray();
        }

        void SetDownloadActivity(DownloadJob job, string? activity, string? detail = null)
        {
            job.Activity = activity;
            job.ActivityDetail = detail;
            if (Dispatcher.CheckAccess()) UpdateDlBar(null, EventArgs.Empty);
            else Dispatcher.Invoke(() => UpdateDlBar(null, EventArgs.Empty));
        }

        double _dlFillWavePhase = 0;

        void UpdateDlBar(object? s, EventArgs e)
        {
            if (_downloadConfirmSource != null || _downloadPreparationVisible) return;
            var jobs = ActiveDownloads();
            if (jobs.Length == 0)
            {
                _dlTimer?.Stop();
                _dlTimer = null;
                if (_isPaused)
                {
                    _isPaused = false;
                    _pauseGate.Set();
                    PauseBtn.Content = "\uE769";
                    PauseBtn.ToolTip = Strings.Get("pause");
                }
                _dlSmoothedBytesPerSecond = 0;
                if (_downloadCompletionVisible)
                {
                    ShowCompletedDownload();
                    return;
                }
                DownloadPanel.Visibility = Visibility.Collapsed;
                DlFill.Margin = new Thickness(0);
                DlFill.Width = 0;
                return;
            }

            var staged = jobs.Where(job => !string.IsNullOrWhiteSpace(job.Activity)).ToArray();
            if (staged.Length > 0)
            {
                var stage = staged[0];
                DlLabel.Text = jobs.Length == 1 ? stage.Activity! : $"{jobs.Length} downloads • {stage.Activity}";
                DlStats.Text = stage.ActivityDetail ?? "";
                DlSubLabel.Text = "Downloader activity";
                DlEta.Text = "";
                DlFill.Margin = new Thickness(0);
                DlFill.Width = Math.Max(0, DownloadPanel.ActualWidth - 28);
                return;
            }

            var done = jobs.Sum(j => Interlocked.Read(ref j.CompletedBytes));
            var transferred = jobs.Sum(j => Interlocked.Read(ref j.TransferredBytes));
            var total = jobs.Sum(j => j.TotalBytes);
            var allSizesKnown = jobs.All(j => j.TotalBytes > 0);
            var filesDone = jobs.Sum(j => Volatile.Read(ref j.CompletedFiles));
            var fileCount = jobs.Sum(j => j.FileCount);
            var now = DateTime.UtcNow;
            var sampleSeconds = Math.Max(0.001, (now - _dlLastSampleUtc).TotalSeconds);
            var sampleBytes = Math.Max(0, transferred - _dlLastSampleBytes);
            var sampleSpeed = sampleBytes / sampleSeconds;
            if (_isPaused)
                _dlSmoothedBytesPerSecond = 0;
            else if (sampleBytes > 0)
                _dlSmoothedBytesPerSecond = _dlSmoothedBytesPerSecond <= 0
                    ? sampleSpeed
                    : _dlSmoothedBytesPerSecond * 0.72 + sampleSpeed * 0.28;
            else
                _dlSmoothedBytesPerSecond *= 0.82;
            var speed = _dlSmoothedBytesPerSecond;
            _dlLastSampleBytes = transferred;
            _dlLastSampleUtc = now;
            var frac = allSizesKnown && total > 0 ? Math.Min(1.0, (double)done / total) : -1;
            var baseLabel = jobs.Length == 1 ? jobs[0].Label : $"{jobs.Length} downloads active";

            DlStats.Text = $"{filesDone}/{fileCount} files";
            DlSubLabel.Text = done > 0
                ? $"{FormatSize(done)} / {(allSizesKnown ? FormatSize(total) : "?")}   {(_isPaused ? "Paused" : $"{FormatSize((long)speed)}/s")}"
                : "Starting downloads...";
            DlEta.Text = _isPaused ? "Paused" : "";

            if (frac >= 0)
            {
                var trackW = Math.Max(0, DownloadPanel.ActualWidth - 28);
                DlFill.Width = Math.Max(0, frac * trackW);
                DlFill.Margin = new Thickness(0);
                DlLabel.Text = $"{(_isPaused ? "Paused - " : "")}{baseLabel}  {(int)(frac * 100)}%";

                if (!_isPaused && speed > 0 && total > done)
                {
                    var eta = TimeSpan.FromSeconds(Math.Max(1, (total - done) / speed));
                    DlEta.Text = eta.TotalHours >= 1 ? $"ETA {eta:hh\\:mm\\:ss}" : $"ETA {eta:mm\\:ss}";
                }
            }
            else
            {
                DlLabel.Text = $"{(_isPaused ? "Paused - " : "")}{baseLabel}";
                var trackW = Math.Max(1, DownloadPanel.ActualWidth - 28 - 60);
                DlFill.Width = 60;
                if (!_isPaused)
                {
                    var elapsed = Math.Max(0.1, (now - jobs.Min(j => j.StartedUtc)).TotalSeconds);
                    var off = (int)(elapsed * 160) % (int)Math.Max(1, trackW);
                    DlFill.Margin = new Thickness(off, 0, 0, 0);
                }
            }

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

        bool _isPaused = false;
        readonly AsyncManualResetEvent _pauseGate = new(true);

        public void TogglePause()
        {
            _isPaused = !_isPaused;
            if (_isPaused) _pauseGate.Reset();
            else _pauseGate.Set();
            _dlLastSampleBytes = ActiveDownloads().Sum(job => Interlocked.Read(ref job.TransferredBytes));
            _dlLastSampleUtc = DateTime.UtcNow;
            _dlSmoothedBytesPerSecond = 0;
            UpdateDlBar(null, EventArgs.Empty);
        }

        async Task DownloadFileAsync(string url, string dest, Action<long>? onProgress)
        {
            var tmp = dest + ".part";
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            await _pauseGate.WaitAsync();
            HttpResponseMessage response;
            using (var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90)))
                response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, requestTimeout.Token);
            using var resp = response;
            resp.EnsureSuccessStatusCode();
            if (File.Exists(tmp)) File.Delete(tmp);
            await using var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
            await using var net = await resp.Content.ReadAsStreamAsync();
            var buf = new byte[1 << 16]; // 64 KB chunks for accurate throttle
            int read;
            long chunkBytes = 0;
            var chunkStart = DateTime.UtcNow;

            while (true)
            {
                var pauseWaitStarted = Stopwatch.GetTimestamp();
                await _pauseGate.WaitAsync();
                var pauseWaitSeconds = (Stopwatch.GetTimestamp() - pauseWaitStarted) / (double)Stopwatch.Frequency;
                if (pauseWaitSeconds > 0.05)
                {
                    chunkBytes = 0;
                    chunkStart = DateTime.UtcNow;
                }
                using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                read = await net.ReadAsync(buf, readTimeout.Token);
                if (read == 0) break;
                pauseWaitStarted = Stopwatch.GetTimestamp();
                await _pauseGate.WaitAsync();
                pauseWaitSeconds = (Stopwatch.GetTimestamp() - pauseWaitStarted) / (double)Stopwatch.Frequency;
                if (pauseWaitSeconds > 0.05)
                {
                    chunkBytes = 0;
                    chunkStart = DateTime.UtcNow;
                }

                await fs.WriteAsync(buf.AsMemory(0, read));
                onProgress?.Invoke(read);
                chunkBytes += read;

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

        static bool IsGameArchive(string path)
        {
            var name = System.IO.Path.GetFileName(path);
            return name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith(".minizip", StringComparison.OrdinalIgnoreCase) ||
                   Regex.IsMatch(name, @"\.(?:7z|zip)\.\d{3}$", RegexOptions.IgnoreCase) ||
                   name.EndsWith(".001", StringComparison.OrdinalIgnoreCase);
        }

        static string ExtractionMarkerPath(string directory) =>
            System.IO.Path.Combine(directory, ".strinowa-extracted");

        static HashSet<string> LoadExtractedArchiveKeys(string directory)
        {
            var marker = ExtractionMarkerPath(directory);
            try
            {
                return File.Exists(marker)
                    ? File.ReadAllLines(marker).Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
            catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
        }

        static void SaveExtractedArchiveKeys(string directory, HashSet<string> keys)
        {
            var marker = ExtractionMarkerPath(directory);
            var temporary = marker + ".tmp";
            File.WriteAllLines(temporary, keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
            File.Move(temporary, marker, true);
        }

        async Task<(bool Success, string Error)> ExtractArchiveHereAsync(
            string archivePath, string outputDirectory)
        {

            var sevenZip = BundledTools.GetSevenZipPath();
            if (sevenZip == null) return (false, "The bundled extractor could not be loaded.");
            try
            {
                var start = new System.Diagnostics.ProcessStartInfo(sevenZip)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = outputDirectory,
                };
                start.ArgumentList.Add("x");
                start.ArgumentList.Add("-y");
                start.ArgumentList.Add("-aoa");
                start.ArgumentList.Add("-bsp0");
                start.ArgumentList.Add("-o" + outputDirectory);
                start.ArgumentList.Add(System.IO.Path.GetFullPath(archivePath));
                using var process = System.Diagnostics.Process.Start(start);
                if (process == null) return (false, "7za.exe could not be started.");
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                var output = await outputTask;
                var error = await errorTask;
                if (process.ExitCode == 0) return (true, "");
                var detail = string.IsNullOrWhiteSpace(error) ? output : error;
                return (false, string.IsNullOrWhiteSpace(detail)
                    ? $"7za exited with code {process.ExitCode}."
                    : detail.Trim().Split('\n').Last().Trim());
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        async Task<(int Extracted, int Failed)> ExtractGameArchivesAsync(DownloadJob job, List<VersionItem> items)
        {
            var archives = items.Where(item => IsGameArchive(item.Dest)).ToList();
            if (archives.Count == 0) return (0, 0);

            var outputDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(items[0].Dest))!;
            var extractedKeys = LoadExtractedArchiveKeys(outputDirectory);
            int extracted = 0, failed = 0, processed = 0;

            foreach (var archive in archives)
            {
                var fileName = System.IO.Path.GetFileName(archive.Dest);
                var volumeMatch = Regex.Match(fileName, @"^(.*)\.(\d{3})$");
                if (volumeMatch.Success && volumeMatch.Groups[2].Value != "001") continue;
                if (extractedKeys.Contains(archive.RelPath) || !File.Exists(archive.Dest)) continue;

                processed++;
                SetDownloadActivity(job, "Extracting game resources", $"{processed}/{archives.Count}  •  {fileName}");
                var result = await ExtractArchiveHereAsync(archive.Dest, outputDirectory);
                if (!result.Success)
                {
                    failed++;
                    continue;
                }

                var related = new List<VersionItem> { archive };
                if (volumeMatch.Success)
                {
                    var baseName = volumeMatch.Groups[1].Value;
                    related = archives.Where(item =>
                    {
                        var candidate = System.IO.Path.GetFileName(item.Dest);
                        return Regex.IsMatch(candidate, "^" + Regex.Escape(baseName) + @"\.\d{3}$",
                            RegexOptions.IgnoreCase);
                    }).ToList();
                }

                foreach (var part in related)
                {
                    if (File.Exists(part.Dest)) File.Delete(part.Dest);
                    extractedKeys.Add(part.RelPath);
                }
                SaveExtractedArchiveKeys(outputDirectory, extractedKeys);
                extracted++;
            }

            return (extracted, failed);
        }

        bool ScheduleSingleFileDownload(
            string key, string label, string url, string dest, long totalBytes)
        {
            var job = TryStartDownloadJob(key, label, [dest], totalBytes, 1);
            if (job == null)
            {
                AppendText("  That download is already active.", TC.Warn);
                return false;
            }

            _ = RunSingleFileDownloadJobAsync(job, url, dest);
            return true;
        }

        async Task RunSingleFileDownloadJobAsync(DownloadJob job, string url, string dest)
        {
            string? completedFolder = null;
            string? completedLabel = null;
            try
            {
                await _downloadSlots.WaitAsync();
                try
                {
                    await DownloadWithRetriesAsync(job, url, dest);
                    Interlocked.Increment(ref job.CompletedFiles);
                }
                finally { _downloadSlots.Release(); }

                if (job.Key.StartsWith("launcher|", StringComparison.OrdinalIgnoreCase) &&
                    dest.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(dest))!;
                    SetDownloadActivity(job, "Extracting launcher", Path.GetFileName(dest));
                    var extraction = await ExtractArchiveHereAsync(dest, outputDirectory);
                    if (extraction.Success)
                    {
                        File.Delete(dest);
                        SetDownloadActivity(job, "Launcher ready", outputDirectory);
                        completedFolder = outputDirectory;
                        completedLabel = "Launcher download complete";
                    }
                    else
                    {
                        SetDownloadActivity(job, "Launcher extraction failed", "Archive retained  •  " + extraction.Error);
                        App.ReportError(new IOException(extraction.Error), "Launcher extraction failed");
                        await Task.Delay(1800);
                    }
                }
                else
                {
                    SetDownloadActivity(job, "Download complete", dest);
                    completedFolder = Path.GetDirectoryName(Path.GetFullPath(dest));
                    completedLabel = job.Key.StartsWith("launcher|", StringComparison.OrdinalIgnoreCase)
                        ? "Launcher download complete"
                        : "Download complete";
                }
            }
            catch (Exception ex)
            {
                SetDownloadActivity(job, "Download failed", $"{Path.GetFileName(dest)}  •  {ex.Message}");
                App.ReportError(ex, "Download failed");
                await Task.Delay(1800);
            }
            finally
            {
                CompleteDownloadJob(job, completedFolder, completedLabel);
            }
        }

        async Task RunGameDownloadJobAsync(
            DownloadJob job, List<VersionItem> items, List<VersionItem> pending)
        {
            string? completedFolder = null;
            string? completedLabel = null;
            try
            {
                using var perJobSlots = new SemaphoreSlim(4, 4);
                var tasks = pending.Select(async item =>
                {
                    await perJobSlots.WaitAsync();
                    await _downloadSlots.WaitAsync();
                    try
                    {
                        await DownloadWithRetriesAsync(job, item.Url, item.Dest);
                        Interlocked.Increment(ref job.CompletedFiles);
                    }
                    finally
                    {
                        _downloadSlots.Release();
                        perJobSlots.Release();
                    }
                });

                await Task.WhenAll(tasks);

                var extraction = await ExtractGameArchivesAsync(job, items);

                var paks = items.Where(it =>
                    it.RelPath.EndsWith(".pak", StringComparison.OrdinalIgnoreCase)).ToList();
                SetDownloadActivity(job, "Verifying game files", $"Checking {paks.Count} file(s)");
                int mismatches = 0;
                foreach (var item in paks)
                {
                    var expected = item.Size;
                    long actual = File.Exists(item.Dest) ? new FileInfo(item.Dest).Length : -1;
                    if (expected.HasValue && actual != expected.Value)
                        mismatches++;
                }

                if (mismatches > 0)
                {
                    SetDownloadActivity(job, "Game verification failed", $"{mismatches} file(s) did not match");
                    App.ReportError(new InvalidDataException($"{mismatches} downloaded file(s) did not match their manifest size."), "Game verification failed");
                    await Task.Delay(1800);
                }
                else if (extraction.Failed > 0)
                {
                    SetDownloadActivity(job, "Download complete with extraction warnings", $"{extraction.Failed} archive(s) retained");
                    App.ReportError(new IOException($"{extraction.Failed} archive(s) could not be extracted and were retained."), "Extraction warning");
                    completedFolder = Path.GetDirectoryName(Path.GetFullPath(items[0].Dest));
                    completedLabel = "Game downloaded with extraction warnings";
                }
                else
                {
                    var detail = extraction.Extracted > 0
                        ? $"{extraction.Extracted} archive(s) extracted  •  {paks.Count} file(s) verified"
                        : $"{paks.Count} file(s) verified";
                    SetDownloadActivity(job, "Game download complete", detail);
                    completedFolder = Path.GetDirectoryName(Path.GetFullPath(items[0].Dest));
                    completedLabel = "Game download complete";
                }
            }
            catch (Exception ex)
            {
                SetDownloadActivity(job, "Download failed", $"{job.Label}  •  {ex.Message}");
                App.ReportError(ex, "Download failed");
                await Task.Delay(1800);
            }
            finally
            {
                CompleteDownloadJob(job, completedFolder, completedLabel);
            }
        }

        async Task DownloadWithRetriesAsync(
            DownloadJob job, string url, string dest, int maxAttempts = 4)
        {
            Exception? lastError = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                long attemptBytes = 0;
                try
                {
                    SetDownloadActivity(job, null);
                    await DownloadFileAsync(url, dest, bytes =>
                    {
                        Interlocked.Add(ref attemptBytes, bytes);
                        Interlocked.Add(ref job.CompletedBytes, bytes);
                        Interlocked.Add(ref job.TransferredBytes, bytes);
                    });
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Interlocked.Add(ref job.CompletedBytes, -Interlocked.Read(ref attemptBytes));
                    if (attempt == maxAttempts) break;
                    var timedOut = ex is TaskCanceledException or TimeoutException ||
                                   ex.InnerException is TaskCanceledException or TimeoutException;
                    SetDownloadActivity(job,
                        timedOut ? "Connection timed out — retrying" : "Connection interrupted — retrying",
                        $"{Path.GetFileName(dest)}  •  attempt {attempt + 1}/{maxAttempts}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)));
                }
            }

            throw new IOException($"Failed after {maxAttempts} attempts", lastError);
        }

        async Task LinkDownloaderAsync(string url)
        {
            AppendText("  Listing filesâ€¦", TC.Dim);
            var (items, rootName) = await ListingAsync(url);
            if (items.Count == 0) { AppendText("  No files found.", TC.Warn); return; }
            Directory.CreateDirectory(rootName);

            var files = items.Select(item =>
            {
                var path = new Uri(item.Url).AbsolutePath.TrimStart('/');
                var dest = Path.Combine(rootName, path.Replace('/', Path.DirectorySeparatorChar));
                return (item.Url, item.Size, Dest: dest);
            }).ToList();
            var pending = files.Where(file =>
                !File.Exists(file.Dest) ||
                (file.Size.HasValue && new FileInfo(file.Dest).Length != file.Size.Value)).ToList();
            var total = files.All(file => file.Size.HasValue)
                ? files.Sum(file => file.Size!.Value)
                : 0;
            var alreadyDone = files.Where(file => !pending.Contains(file) && file.Size.HasValue)
                .Sum(file => file.Size!.Value);

            if (pending.Count == 0)
            {
                AppendText("  All files are already downloaded.", TC.Ok);
                return;
            }

            var job = TryStartDownloadJob(
                $"link|{url.TrimEnd('/')}",
                $"Downloading {items.Count} files",
                pending.Select(file => file.Dest),
                total,
                files.Count,
                alreadyDone,
                files.Count - pending.Count);
            if (job == null)
            {
                AppendText("  That download is already active.", TC.Warn);
                return;
            }

            _ = RunLinkDownloadJobAsync(job, pending);
            AppendText($"  Download started in the background: {items.Count} files", TC.Ok);
        }

        async Task RunLinkDownloadJobAsync(
            DownloadJob job, List<(string Url, long? Size, string Dest)> pending)
        {
            try
            {
                using var perJobSlots = new SemaphoreSlim(4, 4);
                var tasks = pending.Select(async file =>
                {
                    await perJobSlots.WaitAsync();
                    await _downloadSlots.WaitAsync();
                    try
                    {
                        await DownloadWithRetriesAsync(job, file.Url, file.Dest);
                        Interlocked.Increment(ref job.CompletedFiles);
                    }
                    finally
                    {
                        _downloadSlots.Release();
                        perJobSlots.Release();
                    }
                });
                await Task.WhenAll(tasks);

                Dispatcher.Invoke(() =>
                {
                    if (CanClearLogFor(job)) ClearTerminal();
                    AppendText("  Download complete.", TC.Ok);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                    AppendText($"  Download failed ({job.Label}): {ex.Message}", TC.Warn));
                App.ReportError(ex, "Download failed");
            }
            finally
            {
                CompleteDownloadJob(job);
            }
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

        async Task RunAdvancedBruteforceAsync(
            string branch,
            string source,
            string startV,
            string finishV,
            bool launcherMode,
            bool saveTxt)
        {
            if (string.IsNullOrWhiteSpace(branch))
            {
                AppendText("  Bruteforce channel is empty.", TC.Warn);
                return;
            }
            if (!IsValidVersion(startV) || !IsValidVersion(finishV))
            {
                AppendText("  invalid version format", TC.Warn);
                return;
            }

            var (a1, b1, c1, _) = ParseVer(startV);
            var (a2, b2, c2, _) = ParseVer(finishV);
            if (a1 != a2 || b1 != b2 || c1 != c2)
            {
                AppendText("  advanced bruteforce only varies the 4th version segment.", TC.Warn);
                return;
            }

            source = source.ToLowerInvariant();
            if (source is not ("os" or "cn" or "pc"))
            {
                AppendText("  invalid source - choose OS, CN, or PC", TC.Warn);
                return;
            }

            _bruteCts?.Cancel();
            _bruteCts?.Dispose();
            _bruteCts = new CancellationTokenSource();
            var token = _bruteCts.Token;

            var seq = BuildAdvancedVersionSeq(startV, finishV);
            int total = seq.Count, ok = 0, skip = 0, err = 0, done = 0;
            var found = new List<(string ver, string url, string mode, string source, string branch)>();
            var started = DateTime.UtcNow;

            AppendLine([]);
            AppendLine([
                new("  Bruteforcing", TC.Bold, true),
                new("  ", TC.Dim),
                new(launcherMode ? "Launcher" : "Game", TC.Release, true),
                new($"  {branch}  {source.ToUpper()}  {startV} - {finishV}", TC.Dim),
            ]);
            AppendLine([]);

            StartBruteProgress($"{startV} - {finishV}", total);

            foreach (var ver in seq)
            {
                token.ThrowIfCancellationRequested();

                var url = BuildBruteforceProbeUrl(branch, source, ver, launcherMode);
                done++;
                try
                {
                    bool exists;
                    if (launcherMode)
                    {
                        var (probeOk, _) = await ProbeUrlAsync(url);
                        exists = probeOk;
                    }
                    else
                    {
                        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                        var body = await resp.Content.ReadAsStringAsync(token);
                        exists = resp.IsSuccessStatusCode
                            && !body.Contains("NoSuchKey")
                            && !body.Contains("does not exist");
                    }

                    if (exists)
                    {
                        ok++;
                        found.Add((ver, url, launcherMode ? "Launcher" : "Game", source.ToUpper(), branch));
                        AppendLine([
                            new("Found: ", TC.Normal, true),
                            new(ver, TC.Ok, true),
                        ]);
                    }
                }
                catch (TaskCanceledException) when (!token.IsCancellationRequested)
                {
                    skip++;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    err++;
                }

                UpdateBruteProgress(done, total, $"{startV} - {finishV}", ok, started);
                try
                {
                    await Task.Delay(launcherMode ? 300 : 200, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    break;
                }
            }

            StopBruteProgress();

            if (token.IsCancellationRequested)
            {
                AppendText("  Bruteforce cancelled.", TC.Warn);
                return;
            }

            AppendLine([]);
            AppendLine([
                new("  found: ", TC.Dim), new(ok.ToString(), TC.Ok, true),
                new($" / {total}", TC.Dim),
                new("  timeout: ", TC.Dim), new(skip.ToString(), skip > 0 ? TC.Warn : TC.Dim, skip > 0),
                new("  error: ", TC.Dim), new(err.ToString(), err > 0 ? TC.Warn : TC.Dim, err > 0),
            ]);

            if (saveTxt && found.Count > 0)
                SaveBruteforceTxt(source, startV, finishV, ok, total, skip, err, found);
            if (_cfg.WinBuildScan && found.Count > 0)
                SaveWinBuildsXml(found);
        }

        List<string> BuildAdvancedVersionSeq(string startV, string finishV)
        {
            var (a1, b1, c1, d1) = ParseVer(startV);
            var (_, _, _, d2) = ParseVer(finishV);
            bool down = d1 > d2;
            var seq = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int d = d1; down ? d >= d2 : d <= d2; d += down ? -1 : 1)
            {
                foreach (var tail in BuildNumberVariants(d))
                {
                    var ver = $"{a1}.{b1}.{c1}.{tail}";
                    if (seen.Add(ver)) seq.Add(ver);
                }
            }
            return seq;
        }

        static IEnumerable<string> BuildNumberVariants(int value)
        {
            yield return value.ToString("0000");
            yield return value.ToString("000");
            yield return value.ToString("00");
            yield return value.ToString("0");
        }

        string BuildBruteforceProbeUrl(string branch, string source, string version, bool launcherMode)
        {
            var vt = ParseVer(version);
            var root = launcherMode
                ? source switch { "cn" => CN_ROOT, "pc" => PC_ROOT, _ => OS_ROOT }
                : ChooseRootGame(source, vt);
            return launcherMode
                ? $"{root}/{branch}/{version}/full_{version}.7z"
                : $"{root}/{branch}/{version}/full_zip/manifest.txt";
        }

        void StartBruteProgress(string range, int total)
        {
            Dispatcher.Invoke(() =>
            {
                _bruteOwnsDownloadPanel = ActiveDownloads().Length == 0;
                if (!_bruteOwnsDownloadPanel) return;
                _dlTimer?.Stop();
                _dlTimer = null;
                DlLabel.Text = "Bruteforcing";
                DlStats.Text = "";
                DlSubLabel.Text = range;
                DlEta.Text = "";
                DlFill.Width = 0;
                DownloadPanel.Visibility = Visibility.Visible;
            });
        }

        void UpdateBruteProgress(int done, int total, string range, int found, DateTime started)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_bruteOwnsDownloadPanel) return;
                var frac = total > 0 ? Math.Min(1.0, (double)done / total) : 0;
                var trackW = Math.Max(0, DownloadPanel.ActualWidth - 28);
                DlFill.Width = frac * trackW;
                DlSubLabel.Text = $"{range}     Found: {found}";
                var elapsed = Math.Max(0.1, (DateTime.UtcNow - started).TotalSeconds);
                var per = done / elapsed;
                var eta = per > 0 ? TimeSpan.FromSeconds((total - done) / per) : TimeSpan.Zero;
                DlEta.Text = $"ETA: {eta:mm\\m\\ ss\\s}";
            });
        }

        void StopBruteProgress()
        {
            Dispatcher.Invoke(() =>
            {
                if (!_bruteOwnsDownloadPanel) return;
                _bruteOwnsDownloadPanel = false;
                DownloadPanel.Visibility = Visibility.Collapsed;
                DlFill.Width = 0;
            });
        }

        void SaveBruteforceTxt(
            string source,
            string startV,
            string finishV,
            int ok,
            int total,
            int skip,
            int err,
            List<(string ver, string url, string mode, string source, string branch)> found)
        {
            var shortVer = string.Join(".", startV.Split('.').Take(3));
            var fname = $"StrinowaF1-{source.ToUpper()}-{shortVer}.txt";
            try
            {
                var lines = new List<string>
                {
                    $"Strinowa Advanced Bruteforce - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"Branch: {found[0].branch}  Source: {source.ToUpper()}  Range: {startV} -> {finishV}",
                    $"Found: {ok}/{total}  Timeouts: {skip}  Errors: {err}",
                    new string('─', 66),
                };
                foreach (var hit in found)
                    lines.Add($"{hit.ver,-22}  {hit.url}");
                File.WriteAllLines(fname, lines, Encoding.UTF8);
                AppendLine([new("  saved -> ", TC.Dim), new(fname, TC.Ok)]);
            }
            catch (Exception ex)
            {
                AppendText($"  save failed: {ex.Message}", TC.Warn);
                ShowModernError(ex, "Save failed");
            }
        }

        void SaveWinBuildsXml(List<(string ver, string url, string mode, string source, string branch)> found)
        {
            try
            {
                WinBuildCatalog.Upsert(found.Select(hit => new WinBuildEntry(
                    hit.ver, hit.branch, hit.source, hit.mode, hit.url, DateTime.UtcNow)));
            }
            catch (Exception ex)
            {
                AppendText($"  XML save failed: {ex.Message}", TC.Warn);
                ShowModernError(ex, "WinBuilds save failed");
            }
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
                AppendLine([new("  invalid source â€” must be os, cn, pc or qq", TC.Warn)]);
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
                new("  Â·  ", TC.Dim),      new(source.ToUpper(), TC.Normal,  true),
                new($"  Â·  {total} version{(total == 1 ? "" : "s")}  Â·  {startV} â†’ {finishV}", TC.Dim),
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
                        $"Strinowa Bruteforce â€” {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        $"Branch: {branch}  Source: {source.ToUpper()}  Range: {startV} â†’ {finishV}",
                        $"Found: {ok}/{total}  Timeouts: {skip}  Errors: {err}",
                        new string('─', 66),
                    };
                    foreach (var (v, u) in found)
                        lines.Add($"{v,-22}  {u}");
                    File.WriteAllLines(fname, lines, System.Text.Encoding.UTF8);
                    AppendLine([new("  saved  â†’  ", TC.Dim), new(fname, TC.Ok)]);
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
            AppendLine([new("  â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”", TC.Pink)]);
            AppendLine([new("  â”‚  LAUNCHER BRUTEFORCE                            â”‚", TC.Pink)]);
            AppendLine([new("  â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜", TC.Pink)]);
            AppendLine([]);

            var srcRaw = (await AskInput("  source  [OS / CN / PC / QQ] >")).Trim().ToLower();
            if (!new[] { "os", "cn", "pc", "qq" }.Contains(srcRaw))
            {
                AppendLine([new("  invalid source â€” must be os, cn, pc or qq", TC.Warn)]);
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
                AppendLine([new("  launcher bruteforce only varies the 4th version segment â€” use matching a.b.c prefix", TC.Warn)]);
                return;
            }

            bool down = d1 > d2;
            int total = Math.Abs(d1 - d2) + 1;
            int ok = 0, skip = 0, err = 0, done = 0;
            var found = new List<(string ver, string url)>();

            AppendLine([]);
            AppendLine([
                new("  branch  ", TC.Dim), new(branch,           TC.Release, true),
                new("  Â·  ", TC.Dim),      new(source.ToUpper(), TC.Normal,  true),
                new($"  Â·  {total} version{(total == 1 ? "" : "s")}  Â·  {startV} â†’ {finishV}", TC.Dim),
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
                        $"Strinowa Launcher Bruteforce â€” {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                        $"Branch: {branch}  Source: {source.ToUpper()}  Range: {startV} â†’ {finishV}",
                        $"Found: {ok}/{total}  Timeouts: {skip}  Errors: {err}",
                        new string('─', 66),
                    };
                    foreach (var (v, u) in found)
                        lines.Add($"{v,-22}  {u}");
                    File.WriteAllLines(fname, lines, System.Text.Encoding.UTF8);
                    AppendLine([new("  saved  â†’  ", TC.Dim), new(fname, TC.Ok)]);
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
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return (new(), new());
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

                    var lhs = parts[0].Split(new[] { "->", "-&gt;" }, StringSplitOptions.None);
                    var ver = (lhs.Length >= 2 ? lhs[1] : lhs[0]).Trim();

                    var rel = parts[1].Trim().TrimStart('/');
                    var segs = rel.Split('/');
                    if (segs.Length < 2) continue;

                    var folder = segs[0]; // "Launcher" for launcher, branch subfolder for game
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
            while (parts.Count > 0 && parts[^1].ToLower() is "-b")
            {
                gb = true;
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

        void SetDebugOverlayVisible(bool visible)
        {
            _debugVisible = visible;
            DebugOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (!visible)
            {
                StopDebugOverlay();
                return;
            }

            _debugLastSampleUtc = DateTime.UtcNow;
            _debugLastTransferred = ActiveDownloads().Sum(job => Interlocked.Read(ref job.TransferredBytes));
            using (var process = Process.GetCurrentProcess())
            {
                process.Refresh();
                _debugLastCpuTime = process.TotalProcessorTime;
            }
            _debugTimer ??= new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(750),
            };
            _debugTimer.Tick -= DebugTimer_Tick;
            _debugTimer.Tick += DebugTimer_Tick;
            _debugTimer.Start();
            UpdateDebugOverlay();
        }

        void StopDebugOverlay()
        {
            if (_debugTimer == null) return;
            _debugTimer.Stop();
            _debugTimer.Tick -= DebugTimer_Tick;
            _debugTimer = null;
        }

        void DebugTimer_Tick(object? sender, EventArgs e) => UpdateDebugOverlay();

        void UpdateDebugOverlay()
        {
            if (!_debugVisible || !IsLoaded) return;
            try
            {
                var now = DateTime.UtcNow;
                var sampleSeconds = Math.Max(0.001, (now - _debugLastSampleUtc).TotalSeconds);
                _debugUiLagMs = Math.Max(0, sampleSeconds * 1000 - 750);

                var jobs = ActiveDownloads();
                var transferred = jobs.Sum(job => Interlocked.Read(ref job.TransferredBytes));
                var completed = jobs.Sum(job => Interlocked.Read(ref job.CompletedBytes));
                var total = jobs.Sum(job => job.TotalBytes);
                var instantBytesPerSecond = jobs.Length == 0
                    ? 0
                    : Math.Max(0, transferred - _debugLastTransferred) / sampleSeconds;

                int destinationCount;
                lock (_downloadJobsLock) destinationCount = _activeDownloadDestinations.Count;

                ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
                ThreadPool.GetMaxThreads(out var maxWorkers, out var maxIo);
                var gc = GC.GetGCMemoryInfo();
                var managed = GC.GetTotalMemory(false);

                using var process = Process.GetCurrentProcess();
                process.Refresh();
                var cpuDelta = (process.TotalProcessorTime - _debugLastCpuTime).TotalSeconds;
                var cpuPercent = Math.Max(0, cpuDelta / sampleSeconds / Environment.ProcessorCount * 100);

                var text = new StringBuilder(1600);
                text.AppendLine("STRINOWA DEBUG  [Ctrl+F9+F10]");
                text.AppendLine($"uptime {_debugUptime.Elapsed:dd\\.hh\\:mm\\:ss} | theme {AppTheme.CurrentTheme} | scale {CurrentUiScale}% | UI lag {_debugUiLagMs:F1} ms");
                text.AppendLine($"CPU {cpuPercent:F1}% | process threads {process.Threads.Count} | handles {process.HandleCount}");
                text.AppendLine($"working {DebugBytes(process.WorkingSet64)} | private {DebugBytes(process.PrivateMemorySize64)} | virtual {DebugBytes(process.VirtualMemorySize64)}");
                text.AppendLine($"managed {DebugBytes(managed)} | heap {DebugBytes(gc.HeapSizeBytes)} | fragmented {DebugBytes(gc.FragmentedBytes)}");
                text.AppendLine($"memory load {DebugBytes(gc.MemoryLoadBytes)} / {DebugBytes(gc.HighMemoryLoadThresholdBytes)} | available {DebugBytes(gc.TotalAvailableMemoryBytes)}");
                text.AppendLine($"GC collections G0/G1/G2 {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)} | finalizers pending {GC.GetGCMemoryInfo().FinalizationPendingCount}");
                text.AppendLine($"thread pool workers {maxWorkers - availableWorkers}/{maxWorkers} | IO {maxIo - availableIo}/{maxIo} | pool threads {ThreadPool.ThreadCount}");
                text.AppendLine($"pool pending {ThreadPool.PendingWorkItemCount:N0} | completed {ThreadPool.CompletedWorkItemCount:N0} | UI thread {Environment.CurrentManagedThreadId}");
                text.AppendLine();
                text.AppendLine($"downloads {jobs.Length} | destinations {destinationCount} | global slots {8 - _downloadSlots.CurrentCount}/8 | paused {_isPaused}");
                text.AppendLine($"instant {DebugBytes((long)instantBytesPerSecond)}/s | transferred {DebugBytes(transferred)} | progress {DebugBytes(completed)} / {(total > 0 ? DebugBytes(total) : "unknown")}");
                foreach (var job in jobs.Take(6))
                {
                    var age = Math.Max(0.001, (now - job.StartedUtc).TotalSeconds);
                    var jobTransferred = Interlocked.Read(ref job.TransferredBytes);
                    var jobCompleted = Interlocked.Read(ref job.CompletedBytes);
                    var files = Volatile.Read(ref job.CompletedFiles);
                    text.AppendLine($"  {job.Label}");
                    text.AppendLine($"    {files}/{job.FileCount} files | {DebugBytes(jobCompleted)}/{(job.TotalBytes > 0 ? DebugBytes(job.TotalBytes) : "?")} | avg {DebugBytes((long)(jobTransferred / age))}/s");
                }
                if (jobs.Length > 6) text.AppendLine($"  ...and {jobs.Length - 6} more jobs");
                text.AppendLine();
                text.AppendLine($"state busy {_busy} | prompt {(_promptSem != null ? _promptMode : "none")} | scanner {_manifestScanner?.IsVisible == true} | brute {_bruteCts != null}");
                text.AppendLine($"terminal blocks {TerminalBox.Document.Blocks.Count} | history {_history.Count} | devkit animators {_terminalDevkitWaves.Count}");
                text.AppendLine($"timers download {_dlTimer?.IsEnabled == true} | debug {_debugTimer?.IsEnabled == true} | dispatcher shutdown {Dispatcher.HasShutdownStarted}");

                DebugText.Text = text.ToString();
                _debugLastTransferred = transferred;
                _debugLastSampleUtc = now;
                _debugLastCpuTime = process.TotalProcessorTime;
            }
            catch (Exception ex)
            {
                DebugText.Text = $"STRINOWA DEBUG\nDiagnostics sampling failed: {ex.Message}";
            }
        }

        static string DebugBytes(long bytes)
        {
            string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
            double value = Math.Max(0, bytes);
            var unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return $"{value:F1} {units[unit]}";
        }

        void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key is Key.F9 or Key.F10 ||
                !Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
                _debugChordLatched = false;
        }

        void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Up or Key.Down)
            {
                NavigateCommandHistory(e.Key == Key.Up);
                e.Handled = true;
                return;
            }

            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool debugChord = ctrl && Keyboard.IsKeyDown(Key.F9) && Keyboard.IsKeyDown(Key.F10);
            if (debugChord)
            {
                if (!_debugChordLatched)
                {
                    _debugChordLatched = true;
                    SetDebugOverlayVisible(!_debugVisible);
                }
                e.Handled = true;
                return;
            }
            if (ctrl && e.Key == Key.D)
            {
                e.Handled = true;
                EnableDevkits();
                return;
            }
            if (e.Key != Key.B || !ctrl || !Keyboard.IsKeyDown(Key.A)) return;

            e.Handled = true;
            if (_manifestScanner == null || !_manifestScanner.IsVisible)
            {
                _manifestScanner = new Manifest(_cfg.WinBuildScan);
                _manifestScanner.Closed += (_, _) => _manifestScanner = null;
                _manifestScanner.Show();
            }
            else
            {
                _manifestScanner.Activate();
            }
        }
        void PauseBtn_Click(object s, RoutedEventArgs e)
        {
            TogglePause();
            PauseBtn.Content = _isPaused ? "\uE768" : "\uE769";
            PauseBtn.ToolTip = _isPaused ? Strings.Get("resume") : Strings.Get("pause");
        }

        public void EnableDevkits()
        {
            if (_cfg.ShowDevkits) return;
            _cfg.ShowDevkits = true;
            AppSettings.ShowDevkits = true;
            _cfg.Save();
            ShowClipboardToast("Devkit builds permanently enabled");
        }

        void DownloadCompleteCloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _downloadCompletionVisible = false;
            _completedDownloadFolder = null;
            DownloadCompletionActions.Visibility = Visibility.Collapsed;
            UpdateDlBar(null, EventArgs.Empty);
        }

        void DownloadOpenFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_completedDownloadFolder) ||
                !Directory.Exists(_completedDownloadFolder)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = _completedDownloadFolder,
                UseShellExecute = true,
            });
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
            _cfg.Width = Math.Max(560, (int)Math.Round(ActualWidth / _uiScale - WindowFrameAllowance));
            _cfg.Height = Math.Max(380, (int)Math.Round(ActualHeight / _uiScale - WindowFrameAllowance));
            _cfg.Theme = AppTheme.CurrentTheme.ToString();
            _cfg.Color = AppTheme.CurrentTermPreset.ToString();
            _cfg.Lang = Strings.Lang.ToString();
            _cfg.ClearLog = AppSettings.ClearLogOnFinish;
            _cfg.SaveBrf = AppSettings.SaveBruteforceToFile;
            _cfg.SpeedKBs = AppSettings.SpeedLimitKBs;
            _cfg.UiScale = CurrentUiScale;
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
        void Window_SizeChanged(object s, SizeChangedEventArgs e) => UpdateWindowClip();
        void LogoBtn_Click(object s, RoutedEventArgs e)
        {
            var dlg = new About();
            dlg.Show();
        }

        void SettingsBtn_Click(object s, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(this);
            dlg.Closed += (_, _) => Dispatcher.BeginInvoke(ApplyTheme, DispatcherPriority.Loaded);
            dlg.Show();
        }

        /*
        void GameBtn_Click(object s, RoutedEventArgs e)
        {
            var dlg = new GameWindow();
            dlg.Show();
        }
        */
    }

    sealed class DevkitTerminalWave
    {
        readonly System.Windows.Documents.Span _target;
        readonly string _text;
        readonly DispatcherTimer _timer;
        TermColorPreset _preset;
        double _phase;

        public DevkitTerminalWave(System.Windows.Documents.Span target, string text, TermColorPreset preset)
        {
            _target = target; _text = text; _preset = preset;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _timer.Tick += (_, _) => Draw();
            _timer.Start(); Draw();
        }
        public void SetPreset(TermColorPreset preset) { _preset = preset; Draw(); }
        void Draw()
        {
            _phase = (_phase + 0.12) % (Math.PI * 2);
            var hi = _preset == TermColorPreset.PinkAccent ? Color.FromRgb(0xFF, 0x00, 0x4D) : Color.FromRgb(0xFF, 0xFF, 0xFF);
            var lo = _preset == TermColorPreset.PinkAccent ? Color.FromRgb(0x61, 0x0E, 0x60) : Color.FromRgb(0x55, 0x55, 0x66);
            _target.Inlines.Clear();
            for (var i = 0; i < _text.Length; i++)
            {
                var mix = (Math.Sin(_phase + i * 0.55) + 1.0) / 2.0;
                _target.Inlines.Add(new System.Windows.Documents.Run(_text[i].ToString()) { Foreground = new SolidColorBrush(Color.FromRgb((byte)(lo.R + (hi.R - lo.R) * mix), (byte)(lo.G + (hi.G - lo.G) * mix), (byte)(lo.B + (hi.B - lo.B) * mix))) });
            }
        }
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
            Dictionary<string, string> choice)> Maps,
        Dictionary<string, List<AllDownloadChoice>>? MultiChoices = null);

    record BuildResult(
        Dictionary<string, string> Types,
        Dictionary<string, DateTime?> Dates,
        Dictionary<string, bool> Exists,
        Dictionary<string, string> Choice);

    sealed record ChinaBuildStatus(bool Exists, DateTime? Date);
    sealed record AllBuildSeed(string Version, string DisplayBranch, string ResolvedBranch, string Source, bool Hidden);
    sealed record AllBuildProbe(bool Exists, DateTime? Date, string Type);
    sealed record AllBuildOccurrence(AllBuildSeed Seed, AllBuildProbe Probe);
    sealed record AllBuildGroup(string Version, AllBuildOccurrence Primary, List<string> Branches,
        bool Hidden, bool Exists, DateTime? Date);
    sealed record AllDownloadChoice(string Version, string DisplayBranch, string ResolvedBranch,
        string Source, DateTime? Date, bool Hidden, bool Exists, long? Size);
}
