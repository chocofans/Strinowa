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
using System.Xml.Linq;

namespace StrinowaWPF
{
    public static class TC
    {
        public static readonly SolidColorBrush Normal = new(Color.FromRgb(0xE8, 0xE8, 0xE8));
        public static readonly SolidColorBrush Dim = new(Color.FromRgb(0x88, 0x88, 0x88));
        public static readonly SolidColorBrush Release = new(Color.FromRgb(0xFF, 0x50, 0xA0));
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

        // CN build text — slightly dimmed vs Normal; shifts with theme via AppTheme.Apply()
        public static SolidColorBrush CN = new(Color.FromRgb(0xAA, 0xAA, 0xBB));

        // Devkit animated brush — driven by the DevkitColorAnimator on MainWindow
        // Do not freeze; the animator mutates it via ColorAnimation.
        public static SolidColorBrush Devkit = new(Color.FromRgb(0x9B, 0x30, 0x78));
    }

    record VersionItem(string RelPath, string Branch, string Version, string Url, string Dest)
    {
        public long? Size { get; set; }
    }

    class Config
    {
        public int Width { get; set; } = 900;
        public int Height { get; set; } = 560;

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
            }
            return cfg;
        }

        public void Save()
        {
            File.WriteAllText(Path,
                $"; strinowa downloader - conf.ini\n" +
                $"; Window size on startup.\n" +
                $"width={Width}\n" +
                $"height={Height}\n");
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
        //const string PM_ROOT = "192.168.16.77/PMGame"

        //OTHER
        //klbqcm-pack.dl.gxpan.cn
        //klbqcp-tiyan-dir.gxpan.cn:11711
        //2026-05-20

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

        readonly DevkitColorAnimator _devkitAnim = new();

        public MainWindow()
        {
            InitializeComponent();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Strinowa-WPF-Downloader/0.66.93");
            // Devkit brush is shared via TC.Devkit; the animator drives it from startup.
            TC.Devkit = _devkitAnim.Brush;
        }

        void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _cfg = Config.Load();
            Width = _cfg.Width + 16; // +16 for shadow margin
            Height = _cfg.Height + 16;
            InputBox.Focus();
            ShowHeader();
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
        }

        void AppendLine(IEnumerable<TermSpan> spans, bool newline = true)
        {
            var para = new Paragraph { Margin = new Thickness(0), LineHeight = 20 };
            bool any = false;
            foreach (var sp in spans)
            {
                var run = new Run(sp.Text)
                {
                    Foreground = sp.Color,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = 13,
                };
                if (sp.Bold) run.FontWeight = FontWeights.Bold;
                para.Inlines.Add(run);
                any = true;
            }
            if (!any) para.Inlines.Add(new Run("") { FontSize = 13 });
            TerminalBox.Document.Blocks.Add(para);
            ScrollToBottom();
        }

        void AppendText(string text, SolidColorBrush? color = null, bool bold = false)
            => AppendLine([new(text, color ?? TC.Normal, bold)]);

        void ScrollToBottom()
        {
            TermScroll.ScrollToEnd();
        }

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
                SetHint("<Game_Branch>  <OS|CN|PC|QQ>  <version>  [-b | -lb]");
            }
        }

        async Task DispatchAsync(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                AppendText("  no branch provided.", TC.Dim);
                return;
            }

            if (IsUrl(raw))
            {
                AppendText("  detected link mode", TC.Info);
                await LinkDownloaderAsync(raw);
                ShowHeader();
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
                        AppendLine([
                            new($"  Detected build type ({LinkLabel(baseRoot)}): ", TC.Dim),
                            new($"[{types.GetValueOrDefault(version, "Development")}]", TC.Release),
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

            foreach (var key in tasks.Keys.ToList())
            {
                step++;
                SetStatusLine($"Fetching {key} Builds [{step}/{totalSteps}]");
                await tasks[key];
            }

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

            foreach (var code in new[] { "OS", "CN", "PC", "QQ" }) // priority order so dont change
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
                await Task.WhenAll(buildTasks.Values);
            SetStatusLine("Compiling Build List [100%]");
            await Task.Delay(300);
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
                PrintVersionGroup($"OS {branch}", os, m.types, m.dates, m.exists, branch);
            }
            if ((allowedSource == null || allowedSource == "pc" || allowedSource == "cn")
                && (pc.Count > 0 || cn.Count > 0))
            {
                if (pc.Count > 0 && maps.ContainsKey("PC"))
                {
                    var mpc = maps["PC"];
                    var merged = pc.Concat(cn).Distinct().OrderBy(v => v, new VersionComparer()).ToList();
                    var cnSet = new HashSet<string>(cn);
                    AppendLine([]);
                    AppendLine(new TermSpan[]
                    {
            new("  ", TC.Normal),
            new("PC", TC.Release, true),
            new(" / ", TC.Normal),
            new("CN", TC.Dev, true),
            new($" {branch} builds ({merged.Count}):", TC.Bold, true),
                    });

                    int colW = merged.Max(v => v.Length) + 2;



                    foreach (var v in merged)
                    {
                        bool inPC = pc.Contains(v);
                        bool inCN = cn.Contains(v);

                        var type =
                            maps["CN"].types.ContainsKey(v) ? maps["CN"].types[v] :
                            mpc.types.GetValueOrDefault(v, "Development");

                        var date =
                            maps["CN"].dates.ContainsKey(v) ? maps["CN"].dates[v] :
                            mpc.dates.GetValueOrDefault(v);

                        var exists =
                            mpc.exists.ContainsKey(v)
                                ? mpc.exists[v]
                                : maps["CN"].exists.GetValueOrDefault(v, true);

                        bool isCN = inCN;

                        var color = isCN ? TC.Dev : TC.Release;

                        var dtStr = date.HasValue
                            ? date.Value.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'")
                            : "unknown";

                        var line = new List<TermSpan>

                            {
                            new("  ", TC.Normal),
                            new($"{v.PadRight(colW)}", color),
                            new($"  [{type}]", color),
                            };
                        if (!exists)
                            line.Add(new(" (File has been removed)", TC.Removed));
                        else
                            line.Add(new($"  — {dtStr}", TC.Dim));

                        AppendLine(line);
                    }

                }
                else if (cn.Count > 0 && maps.ContainsKey("CN"))
                {
                    var mcn = maps["CN"];
                    PrintVersionGroup($"CN {branch}", cn, mcn.types, mcn.dates, mcn.exists, branch);
                }
            }

            maps = MergeSources(maps);
            return new ScanContext(branch, allowedSource, hiddenVersion, maps);
        }
        bool _hasStatusLine = false;

        void SetStatusLine(string text)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_hasStatusLine)
                {
                    var para = new Paragraph { Margin = new Thickness(0), LineHeight = 20 };
                    TerminalBox.Document.Blocks.Add(para);
                    _hasStatusLine = true;
                }

                var p = (Paragraph)TerminalBox.Document.Blocks.LastBlock!;
                p.Inlines.Clear();
                p.Inlines.Add(new Run("  " + text)
                {
                    Foreground = TC.Dim,
                    FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                    FontSize = 13
                });

                ScrollToBottom();
            });
        }
        void PrintVersionGroup(string label, List<string> versions, Dictionary<string, string> types,
            Dictionary<string, DateTime?> dates, Dictionary<string, bool> exists, string branch)
        {
            AppendLine([]);
            AppendLine([new($"  {label} builds ({versions.Count}):", TC.Bold, true)]);
            var colW = versions.Max(v => v.Length) + 2;
            foreach (var v in versions)
            {
                var t = types.GetValueOrDefault(v, "Development");
                var removed = exists.TryGetValue(v, out var ex) && !ex;
                var color = removed ? TC.Removed : (t == "Release" ? TC.Release : TC.Dev);
                var dt = dates.GetValueOrDefault(v);
                var dtStr = dt.HasValue ? dt.Value.ToString("yyyy-MM-dd HH:mm:ss 'GMT+01'") : "unknown";
                var typeTag = $"[{t}]";

                var line = new List<TermSpan>
                {
                    new("  ", TC.Normal),
                    new($"{v.PadRight(colW)}", color),
                    new($"  {typeTag,-13}", color),
                };
                if (removed)
                    line.Add(new(" (File has been removed)", TC.Removed));
                else
                    line.Add(new($"  — {dtStr}", TC.Dim));
                AppendLine(line);
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
                                    foundType = IsRelease(body) ? "Release" : "Development";
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
                        types[v] = foundType ?? "Development";
                        dates[v] = foundDate;
                        exists[v] = foundEx;
                        choice[v] = foundBranch ?? (cands.Count > 0 ? cands[0] : branch);
                    }
                });

            return new BuildResult(types, dates, exists, choice);
        }

        async Task CeciliaDownloadAsync(string baseRoot, string branch, string version, string? manifestTextOpt)
        {
            if (branch.StartsWith("Launcher", StringComparison.OrdinalIgnoreCase))
            {
                var lUrl = $"{baseRoot}/{branch}/{version}/full_{version}.7z";
                var (ok, _) = await ProbeUrlAsync(lUrl);
                if (!ok) { AppendText("  The provided launcher build is not available.", TC.Warn); return; }
                var lSz = await HeadSizeAsync(lUrl);
                if (!await AskYesNo($"Download launcher {branch} {version} ({FormatSize(lSz)})?", defaultNo: true))
                { AppendText("  Cancelled.", TC.Dim); return; }
                var lDest = Path.Combine($"{branch}-{version}", $"full_{version}.7z");
                Directory.CreateDirectory($"{branch}-{version}");
                StartDlBar($"Downloading launcher {version}", 0, 1);
                await DownloadFileAsync(lUrl, lDest, null);
                StopDlBar();
                AppendText("  Your Strinowa launcher has downloaded.", TC.Ok);
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

            // Pak size verification
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

        void UpdateDlBar(object? s, EventArgs e)
        {
            var elapsed = (DateTime.UtcNow - _dlStart).TotalSeconds;
            var speed = elapsed > 0.5 ? _dlBytes / elapsed : 0;
            var done = _dlDone;
            var total = _dlTotal;
            double frac = total > 0 ? Math.Min(1.0, (double)done / total) : -1;

            DlStats.Text = $"{_dlFilesDone}/{_dlFilesTotal} files";
            DlSubLabel.Text = $"{FormatSize(done)} / {(total > 0 ? FormatSize(total) : "?")}   {FormatSize((long)speed)}/s";

            if (frac >= 0)
            {
                var trackW = DownloadPanel.ActualWidth - 28;
                DlFill.Width = Math.Max(0, frac * trackW);
                var pct = (int)(frac * 100);
                DlLabel.Text = DlLabel.Text.Split('[')[0].TrimEnd() +
                               $"  [{new string('█', pct / 5)}{new string('░', 20 - pct / 5)}]  {pct}%";

                if (speed > 0 && total > done)
                {
                    var eta = TimeSpan.FromSeconds((total - done) / speed);
                    DlEta.Text = $"ETA {eta:mm\\:ss}";
                }
            }
            else
            {
                DlFill.Width = 60;
                var off = (int)(elapsed * 200) % (int)Math.Max(1, DownloadPanel.ActualWidth - 28 - 60);
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

        async Task DownloadFileAsync(string url, string dest, Action<long>? onProgress)
        {
            var tmp = dest + ".part";
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            if (File.Exists(tmp)) File.Delete(tmp);
            await using var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
            await using var net = await resp.Content.ReadAsStreamAsync();
            var buf = new byte[1 << 20]; // 1 MB buffer
            int read;
            while ((read = await net.ReadAsync(buf)) > 0)
            {
                await fs.WriteAsync(buf.AsMemory(0, read));
                onProgress?.Invoke(read);
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
            AppendLine([new("  strinowab — game bruteforce", TC.Info, true)]);
            var srcRaw = (await AskInput("source [OS/CN/PC/QQ] >")).Trim().ToLower();
            var source = new[] { "os", "cn", "pc", "qq" }.Contains(srcRaw) ? srcRaw : "cn";
            var isQq = source == "qq";

            var startV = (await AskInput("start version (e.g. 1.9.1.11) >")).Trim();
            var finishV = (await AskInput("finish version (e.g. 1.9.1.1) >")).Trim();
            if (!IsValidVersion(startV) || !IsValidVersion(finishV))
            { AppendText("  invalid version format", TC.Warn); return; }

            var seq = BuildGameSeq(startV, finishV);
            int ok = 0, skip = 0, total = seq.Count;
            AppendLine([new($"  Checking {total} versions…", TC.Dim)]);

            foreach (var (a, b, c, d) in seq)
            {
                var ver = $"{a}.{b}.{c}.{d}";
                var vt = (a, b, c, d);
                var root = ChooseRootGame(source, vt);
                var url = $"{root}/{branch}/{ver}/full_zip/manifest.txt";
                try
                {
                    using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    var body = await resp.Content.ReadAsStringAsync();
                    if (resp.IsSuccessStatusCode && !body.Contains("NoSuchKey") && !body.Contains("does not exist"))
                    {
                        ok++;
                        AppendLine([new($"  OK   {ver,-18} {url}", TC.BrfOk)]);
                    }
                }
                catch (TaskCanceledException) { skip++; AppendLine([new($"  TIMEOUT {ver} (skipped)", TC.BrfMiss)]); }
                catch { }
                await Task.Delay(isQq ? 1000 : 200);
            }
            AppendLine([new($"  done. ok={ok}/{total}  skipped(timeout)={skip}", TC.Info)]);
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
        async Task RunBruteforceLauncherAsync(string branch)
        {
            AppendLine([new("  strinowab — launcher bruteforce", TC.Info, true)]);
            var srcRaw = (await AskInput("source [OS/CN/PC/QQ] >")).Trim().ToLower();
            var source = new[] { "os", "cn", "pc", "qq" }.Contains(srcRaw) ? srcRaw : "os";
            var root = source switch { "cn" => CN_ROOT, "pc" => PC_ROOT, "qq" => QQ_ROOT, _ => OS_ROOT };
            var isQq = source == "qq";

            var startV = (await AskInput("start version (e.g. 0.9.1.640) >")).Trim();
            var finishV = (await AskInput("finish version (e.g. 0.9.1.620) >")).Trim();
            if (!IsValidVersion(startV) || !IsValidVersion(finishV))
            { AppendText("  invalid version format", TC.Warn); return; }

            var (a1, b1, c1, d1) = ParseVer(startV);
            var (a2, b2, c2, d2) = ParseVer(finishV);
            bool down = CompareVer((a1, b1, c1, d1), (a2, b2, c2, d2)) > 0;
            int ok = 0, skip = 0;

            for (int d = d1; down ? d >= d2 : d <= d2; d += down ? -1 : 1)
            {
                var ver = $"{a1}.{b1}.{c1}.{d}";
                var url = $"{root}/{branch}/{ver}/full_{ver}.7z";
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                    using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                    if (resp.IsSuccessStatusCode)
                    { ok++; AppendLine([new($"  OK   {ver,-18} {url}", TC.BrfOk)]); }
                }
                catch (TaskCanceledException) { skip++; AppendLine([new($"  TIMEOUT {ver} (skipped)", TC.BrfMiss)]); }
                catch { }
                await Task.Delay(isQq ? 1000 : 300);
            }
            AppendLine([new($"  done. ok={ok}  skipped={skip}", TC.Info)]);
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
                return ParseIndexText(text);
            }
            catch { return (new(), new()); }
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
                    var rel = parts[1].Trim().TrimStart('/');
                    var segs = rel.Split('/');
                    if (segs.Length < 2) continue;
                    branches.TryAdd(segs[0], new());
                    branches[segs[0]].Add(segs[1]);
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
            body.Contains("PMGame-Win64-Shipping.exe") ||
            body.Contains("Strinova-Win64-Shipping.exe") ||
            body.Contains("Calabiyau-Win64-Shipping.exe");

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

        void CloseBtn_Click(object s, RoutedEventArgs e)
        {
            _cfg.Width = (int)(ActualWidth - 16);
            _cfg.Height = (int)(ActualHeight - 16);
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

        // Logo icon click opens About window
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