using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace StrinowaWPF;

public partial class Manifest : Window
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    static readonly Regex FullVersion = new(@"^(?<prefix>\d{1,4}(?:\.\d{1,4}){2})\.(?<build>\d{1,6})$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex BuildOnly = new(@"^\d{1,6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Dictionary<string, string> Roots = new()
    {
        ["OS"] = "https://resource-download.strinova.com/Client/Win/GameDepot/",
        ["CN"] = "https://klbq-cdn-1300343128.cos.ap-shanghai.tencentcos.cn/Client/Win/GameDepot/",
        ["PC"] = "https://klbqcp-client-cdn.gxpan.cn/Client/Win/GameDepot/",
        ["QQ"] = "https://down.klbq.qq.com/Client/Win/GameDepot/",
    };

    CancellationTokenSource? _scanCts;
    double _progressFraction;
    List<ScanResult> _completedResults = new();
    string? _completedChannel;
    string? _savedOutputPath;
    readonly bool _winBuildScan;

    public Manifest(bool winBuildScan = true, int uiScale = 100)
    {
        _winBuildScan = winBuildScan;
        InitializeComponent();
        ApplyUiScale(uiScale);
        AppTheme.ApplyToManifest(this);
        ScannerProgressTrack.SizeChanged += (_, _) => SetProgress(_progressFraction);
        SaveResultsButton.IsEnabled = false;
    }

    public void ApplyUiScale(int percent)
    {
        WindowScale.Apply(this, RootBorder, percent, 610, 520);
    }

    public void ApplyTheme()
    {
        bool isLight = AppTheme.CurrentTheme == LauncherTheme.Light;
        bool isMidnight = AppTheme.CurrentTheme == LauncherTheme.Midnight;
        bool isAcrylic = AppTheme.CurrentTheme == LauncherTheme.Acrylic;
        bool pinkText = AppTheme.CurrentTermPreset == TermColorPreset.PinkAccent;
        var bg = isAcrylic ? BrushA(0x18, 0x26, 0x26, 0x26) : Brush(isMidnight ? 0x00 : isLight ? 0xE8 : 0x1A, isMidnight ? 0x00 : isLight ? 0xE8 : 0x1A, isMidnight ? 0x00 : isLight ? 0xF0 : 0x1F);
        var title = isAcrylic ? BrushA(0x24, 0x30, 0x30, 0x30) : Brush(isMidnight ? 0x00 : isLight ? 0xD8 : 0x11, isMidnight ? 0x00 : isLight ? 0xD8 : 0x11, isMidnight ? 0x00 : isLight ? 0xE8 : 0x15);
        var input = isAcrylic ? BrushA(0x38, 0x1C, 0x1C, 0x1C) : Brush(isMidnight ? 0x0C : isLight ? 0xF5 : 0x24, isMidnight ? 0x0C : isLight ? 0xF5 : 0x24, isMidnight ? 0x12 : isLight ? 0xFF : 0x2D);
        var results = isAcrylic ? BrushA(0x24, 0x1C, 0x1C, 0x1C) : input;
        var border = isAcrylic ? Brushes.Transparent : Brush(isLight ? 0xB0 : 0x2E, isLight ? 0xB0 : 0x2E, isLight ? 0xC8 : 0x38);
        var sep = isAcrylic ? BrushA(0x38, 0xA8, 0xA8, 0xA8) : Brush(isLight ? 0xB0 : 0x22, isLight ? 0xB0 : 0x22, isLight ? 0xC8 : 0x30);
        var text = isAcrylic ? Brush(0xF7, 0xFA, 0xFF) : Brush(isLight ? 0x22 : 0xE8, isLight ? 0x22 : 0xE8, isLight ? 0x30 : 0xE8);
        var muted = isAcrylic ? Brush(0xD0, 0xD0, 0xD0) : Brush(isLight ? 0x55 : 0x99, isLight ? 0x55 : 0x99, isLight ? 0x77 : 0xAA);
        var accent = pinkText ? Brush(0xFF, 0x00, 0x4D) : Brush(0xDC, 0x32, 0x78);

        RootBorder.Background = bg; RootBorder.BorderBrush = border;
        RootBorder.Margin = new Thickness(0);
        RootBorder.BorderThickness = new Thickness(0);
        RootBorder.Effect = null;
        TitleBarBorder.Background = title; SepRect.Fill = sep; ContentPanel.Background = isAcrylic ? Brushes.Transparent : bg;
        TitleText.Foreground = text; TitleVersion.Foreground = muted; TitleMark.Foreground = accent;
        SectionTitle.Foreground = accent; SectionSubtitle.Foreground = muted;
        foreach (var label in new[] { ServerLabel, ChannelLabel, RateLabel, HighLabel, LowLabel }) label.Foreground = muted;
        StatusText.Foreground = muted;
        foreach (var field in new[] { ChannelBox, RateBox, HighBox, LowBox })
        { field.Background = input; field.Foreground = text; field.BorderBrush = border; }
        ServerBox.Background = input; ServerBox.Foreground = text; ServerBox.BorderBrush = border;
        ResultsBorder.Background = results; ResultsBorder.BorderBrush = border;
        ResultsBorder.BorderThickness = isAcrylic ? new Thickness(0) : new Thickness(1);
        StartButton.Background = accent; StartButton.Foreground = Brushes.White;
        CancelButton.Background = isAcrylic ? BrushA(0x40, 0x42, 0x42, 0x42) : isLight ? Brush(0xC8, 0xC8, 0xD8) : Brush(0x38, 0x38, 0x48);
        CancelButton.Foreground = text;
        ScannerProgressTrack.Background = isAcrylic ? BrushA(0x30, 0x20, 0x20, 0x20) : isLight ? Brush(0xC8, 0xC8, 0xD8) : Brush(0x20, 0x20, 0x28);
        ScannerProgressFill.Background = BuildProgressWave(accent.Color, isLight ? Color.FromRgb(0xF3, 0xA0, 0xBE) : Color.FromRgb(0x8C, 0x00, 0xC8));
        SaveResultsButton.Foreground = text;
        SaveResultsButton.Background = isAcrylic ? BrushA(0x40, 0x42, 0x42, 0x42) : isLight ? Brush(0xC8, 0xC8, 0xD8) : Brush(0x38, 0x38, 0x48);
        CleanWinBuildsButton.Foreground = text;
        CleanWinBuildsButton.Background = isAcrylic ? BrushA(0x40, 0x42, 0x42, 0x42) : isLight ? Brush(0xC8, 0xC8, 0xD8) : Brush(0x38, 0x38, 0x48);
        foreach (ComboBoxItem item in ServerBox.Items) { item.Foreground = text; item.Background = input; }
        AcrylicHelper.ApplyBackdrop(this, isAcrylic);
    }

    static SolidColorBrush Brush(int r, int g, int b) => new(Color.FromRgb((byte)r, (byte)g, (byte)b));
    static SolidColorBrush BrushA(int a, int r, int g, int b) => new(Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b));

    void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
    static LinearGradientBrush BuildProgressWave(Color first, Color second)
    {
        var brush = new LinearGradientBrush(first, second, 0) { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        var shift = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        brush.GradientStops[0].BeginAnimation(GradientStop.OffsetProperty, shift);
        brush.GradientStops[1].BeginAnimation(GradientStop.OffsetProperty, shift);
        return brush;
    }
    void AddResultButton(string version, string branch, string url, bool allBranches)
    {
        var label = allBranches ? $"{version} {BranchCatalog.ShortName(branch)}" : version;
        var button = new Button { Content = label, Tag = url, Style = (Style)FindResource("ResultLink"), FontFamily = new FontFamily("Cascadia Mono, Consolas"), ToolTip = "Open manifest" };
        button.Background = AppTheme.CurrentTheme == LauncherTheme.Acrylic ? BrushA(0x38, 0x42, 0x42, 0x42) : Brush(0x25, 0x25, 0x32); button.BorderBrush = Brush(0xDC, 0x32, 0x78); button.Foreground = Brushes.White;
        button.Click += (_, _) => Process.Start(new ProcessStartInfo((string)button.Tag) { UseShellExecute = true });
        ResultsPanel.Children.Add(button);
    }
    async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_scanCts != null) return;
        try
        {
            var channel = ChannelBox.Text.Trim().Trim('/');
            var server = ((ServerBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "OS").ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(channel)) throw new ArgumentException("Channel cannot be empty.");
            if (!int.TryParse(RateBox.Text.Trim(), out var rate) || rate < 1) throw new ArgumentException("Checks per second must be a positive number.");

            var plan = BuildScanPlan(HighBox.Text, LowBox.Text, server);

            var mode = channel.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher" : "Game";
            var allBranches = channel.Equals("Game_All", StringComparison.OrdinalIgnoreCase) ||
                              channel.Equals("Launcher_All", StringComparison.OrdinalIgnoreCase);
            var branches = allBranches ? BranchCatalog.Get(server, mode) : new[] { channel };
            var total = checked(plan.Total * branches.Count);
            _scanCts = new CancellationTokenSource();
            StartButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            ResultsPanel.Children.Clear();
            _completedResults.Clear();
            _completedChannel = null;
            ClearSavedStatusLink();
            SaveResultsButton.Content = "Save links to TXT";
            SaveResultsButton.IsEnabled = false;
            SetProgress(0);
            StatusText.Text = $"Preparing {total:N0} manifest paths…";
            await ScanAsync(Roots[server], server, channel, mode, branches, plan.Values, rate, total, _scanCts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            App.ReportError(ex, "Scanner error");
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            StartButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    void Cancel_Click(object sender, RoutedEventArgs e) => _scanCts?.Cancel();

    sealed record ScanPlan(long Total, IEnumerable<string> Values);

    static ScanPlan BuildScanPlan(string highValue, string lowValue, string server)
    {
        var high = ParseBoundary(highValue, "Highest");
        var low = ParseBoundary(lowValue, "Lowest");
        var highPrefix = high.prefix ?? low.prefix ?? "0.0.0";
        var lowPrefix = low.prefix ?? highPrefix;

        var hp = ParsePrefix(highPrefix);
        var lp = ParsePrefix(lowPrefix);
        var hd = int.Parse(high.digits);
        var ld = int.Parse(low.digits);
        if (low.digits.Length > high.digits.Length)
            throw new ArgumentException("The lowest build cannot use more digits than the highest build.");
        var highVersion = (hp.a, hp.b, hp.c, hd);
        var lowVersion = (lp.a, lp.b, lp.c, ld);
        if (CompareVersion(highVersion, lowVersion) < 0)
            throw new ArgumentException("The lowest version/build cannot be greater than the highest version/build.");

        int maxWidth = high.digits.Length;
        bool expandShortForms = low.digits.Length < maxWidth;
        bool skipOs010 = server.Equals("OS", StringComparison.OrdinalIgnoreCase);

        IEnumerable<(int a, int b, int c)> Prefixes()
        {
            for (int a = hp.a; a >= lp.a; a--)
            {
                int bStart = a == hp.a ? hp.b : 10;
                int bEnd = a == lp.a ? lp.b : 0;
                bStart = Math.Min(10, Math.Max(0, bStart));
                for (int b = bStart; b >= bEnd; b--)
                {
                    int cStart = hp.c;
                    int cEnd = a == lp.a && b == lp.b ? lp.c : 0;
                    for (int c = cStart; c >= cEnd; c--)
                        yield return (a, b, c);
                }
            }
        }

        long BuildCount()
        {
            long count = 0;
            foreach (var (a, b, c) in Prefixes())
            {
                if (skipOs010 && a == 0 && b == 10 && c == 0) continue;
                count += CountBuilds(hd, ld, maxWidth, expandShortForms);
            }
            return count;
        }

        IEnumerable<string> Values()
        {
            foreach (var (a, b, c) in Prefixes())
            {
                if (skipOs010 && a == 0 && b == 10 && c == 0) continue;
                for (int build = hd; build >= ld; build--)
                    foreach (var suffix in BuildVariants(build, maxWidth, expandShortForms))
                        yield return $"{a}.{b}.{c}.{suffix}";
            }
        }

        return new ScanPlan(BuildCount(), Values());
    }

    static (int a, int b, int c) ParsePrefix(string prefix)
    {
        var parts = prefix.Split('.');
        if (parts.Length != 3) throw new ArgumentException("Version prefixes must contain three numeric segments.");
        return (int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
    }

    static int CompareVersion((int a, int b, int c, int d) x, (int a, int b, int c, int d) y)
    {
        var a = x.a.CompareTo(y.a); if (a != 0) return a;
        var b = x.b.CompareTo(y.b); if (b != 0) return b;
        var c = x.c.CompareTo(y.c); return c != 0 ? c : x.d.CompareTo(y.d);
    }

    static long CountBuilds(int high, int low, int maxWidth, bool expandShortForms)
    {
        long count = 0;
        for (var build = high; build >= low; build--)
        {
            count++;
            if (expandShortForms)
                count += Math.Max(0, maxWidth - Math.Max(1, build.ToString().Length));
        }
        return count;
    }

    static IEnumerable<string> BuildVariants(int build, int maxWidth, bool expandShortForms)
    {
        yield return build.ToString($"D{maxWidth}");
        if (!expandShortForms) yield break;
        var shortest = Math.Max(1, build.ToString().Length);
        for (var width = maxWidth - 1; width >= shortest; width--)
            yield return build.ToString($"D{width}");
    }

    static (string? prefix, string digits) ParseBoundary(string value, string label)
    {
        value = value.Trim();
        var m = FullVersion.Match(value);
        if (m.Success) return (m.Groups["prefix"].Value, m.Groups["build"].Value);
        if (BuildOnly.IsMatch(value)) return (null, value);
        throw new ArgumentException($"{label} value must be a build number or a four-part version (a.b.c.dddddd).");
    }

    async Task ScanAsync(string root, string server, string channel, string mode, IReadOnlyList<string> branches,
        IEnumerable<string> versions, int rate, long total, CancellationToken token)
    {
        var queue = Channel.CreateBounded<(string version, string branch)>(new BoundedChannelOptions(256) { SingleWriter = true });
        var found = new ConcurrentBag<ScanResult>();
        var pacer = new RequestPacer(rate);
        var started = Stopwatch.StartNew();
        var allBranches = branches.Count > 1;
        long done = 0;
        var workers = Enumerable.Range(0, Math.Clamp(rate / 4, 8, 64)).Select(_ => Task.Run(async () =>
        {
            await foreach (var item in queue.Reader.ReadAllAsync(token))
            {
                await pacer.WaitAsync(token);
                var url = mode == "Launcher"
                    ? $"{root}{item.branch}/{item.version}/full_{item.version}.7z"
                    : $"{root}{item.branch}/{item.version}/manifest.txt";
                var exists = await IsManifestAsync(url, token);
                if (!exists && mode == "Launcher")
                {
                    await pacer.WaitAsync(token);
                    var launcherName = item.branch.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase)
                        ? item.branch["Launcher_".Length..] : item.branch;
                    var installerPrefix = server == "OS" ? "Strinova" : "Calabiyau";
                    url = $"{root}{item.branch}/{item.version}/{installerPrefix}_Installer_{launcherName}_{item.version}.exe";
                    exists = await IsManifestAsync(url, token);
                }
                if (exists)
                {
                    found.Add(new ScanResult(item.version, item.branch, url));
                    await Dispatcher.InvokeAsync(() => AddResultButton(item.version, item.branch, url, allBranches));
                }
                var current = Interlocked.Increment(ref done);
                if (current % 20 == 0 || current == total)
                    await Dispatcher.InvokeAsync(() => UpdateProgress(current, total, found.Count, started.Elapsed));
            }
        }, token)).ToArray();

        try
        {
            foreach (var branch in branches)
                foreach (var version in versions)
                    await queue.Writer.WriteAsync((version, branch), token);
            queue.Writer.TryComplete();
            await Task.WhenAll(workers);
        }
        finally
        {
            queue.Writer.TryComplete();
        }

        UpdateProgress(done, total, found.Count, started.Elapsed);
        var ordered = found
            .GroupBy(result => result.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(result => result.Version, NumericVersionComparer.Instance)
            .ThenBy(result => result.Branch, StringComparer.OrdinalIgnoreCase)
            .ToList();
        ResultsPanel.Children.Clear();
        foreach (var result in ordered) AddResultButton(result.Version, result.Branch, result.Url, allBranches);

        if (found.Count == 0)
        {
            StatusText.Text = $"Complete — no manifests found ({done:N0}/{total:N0})";
            return;
        }
        _completedResults = ordered;
        _completedChannel = channel;
        if (_winBuildScan)
            WinBuildCatalog.Upsert(ordered.Select(result => new WinBuildEntry(
                result.Version,
                result.Branch,
                server,
                channel.StartsWith("Launcher_", StringComparison.OrdinalIgnoreCase) ? "Launcher" : "Game",
                result.Url,
                DateTime.UtcNow)));
        SaveResultsButton.Content = "Save links to TXT";
        SaveResultsButton.IsEnabled = true;
        StatusText.Text = $"Complete — {ordered.Count} found.";
    }

    async void SaveResults_Click(object sender, RoutedEventArgs e)
    {
        if (_completedResults.Count == 0 || string.IsNullOrWhiteSpace(_completedChannel)) return;
        SaveResultsButton.IsEnabled = false;
        try
        {
            var output = NextOutputPath(_completedChannel);
            await File.WriteAllLinesAsync(output, _completedResults.Select(result => result.Url));
            SaveResultsButton.Content = "Saved";
            _savedOutputPath = output;
            StatusText.Text = $"Saved {_completedResults.Count} links to {output}";
            StatusText.TextDecorations = TextDecorations.Underline;
            StatusText.Cursor = Cursors.Hand;
            StatusText.ToolTip = output;
        }
        catch (Exception ex)
        {
            SaveResultsButton.Content = "Save links to TXT";
            SaveResultsButton.IsEnabled = true;
            StatusText.Text = $"Save failed: {ex.Message}";
            App.ReportError(ex, "Save failed");
        }
    }

    void ClearSavedStatusLink()
    {
        _savedOutputPath = null;
        StatusText.TextDecorations = null;
        StatusText.Cursor = Cursors.Arrow;
        StatusText.ToolTip = null;
    }

    void StatusText_Click(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_savedOutputPath) || !File.Exists(_savedOutputPath)) return;
        Process.Start(new ProcessStartInfo(_savedOutputPath) { UseShellExecute = true });
        e.Handled = true;
    }

    async void CleanWinBuilds_Click(object sender, RoutedEventArgs e)
    {
        if (_scanCts != null) return;
        var entries = WinBuildCatalog.Load();
        if (entries.Count == 0)
        {
            StatusText.Text = "WinBuilds is already empty.";
            return;
        }

        CleanWinBuildsButton.IsEnabled = false;
        StartButton.IsEnabled = false;
        try
        {
            var remove = new List<WinBuildEntry>();
            var groups = entries.GroupBy(entry => $"{entry.Source}|{entry.Branch}", StringComparer.OrdinalIgnoreCase).ToList();
            var completed = 0;
            foreach (var group in groups)
            {
                var first = group.First();
                if (Roots.TryGetValue(first.Source, out var root))
                {
                    try
                    {
                        var manifest = await Http.GetStringAsync($"{root}{first.Branch}/manifest.txt");
                        var publicVersions = Regex.Matches(manifest, @"(?<!\d)(?:\d+\.){2,3}\d+(?!\d)")
                            .Select(match => match.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        remove.AddRange(group.Where(entry => publicVersions.Contains(entry.Version)));
                    }
                    catch { }
                }
                completed++;
                StatusText.Text = $"Cleaning WinBuilds [{completed}/{groups.Count}]";
            }
            var removed = WinBuildCatalog.Remove(remove);
            StatusText.Text = $"Clean complete — removed {removed}, kept {entries.Count - removed}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Clean failed: {ex.Message}";
            App.ReportError(ex, "WinBuilds cleanup failed");
        }
        finally
        {
            CleanWinBuildsButton.IsEnabled = true;
            StartButton.IsEnabled = true;
        }
    }

    async Task<bool> IsManifestAsync(string url, CancellationToken token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, 4095);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode) return false;
            var sample = await response.Content.ReadAsByteArrayAsync(token);
            return !Encoding.UTF8.GetString(sample).Contains("<Code>NoSuchKey</Code>", StringComparison.OrdinalIgnoreCase);
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!token.IsCancellationRequested) { return false; }
    }

    void SetProgress(double fraction)
    {
        _progressFraction = fraction;
        ScannerProgressFill.Width = Math.Max(0, ScannerProgressTrack.ActualWidth * fraction);
    }
    void UpdateProgress(long done, long total, int hits, TimeSpan elapsed)
    {
        SetProgress(total == 0 ? 0 : Math.Min(1, (double)done / total));
        var perSecond = elapsed.TotalSeconds <= 0 ? 0 : done / elapsed.TotalSeconds;
        var remaining = perSecond <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Max(0, total - done) / perSecond);
        var eta = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : $"{remaining.Minutes:00}:{remaining.Seconds:00}";
        StatusText.Text = $"[{done:N0}/{total:N0}]  {perSecond:N0}/s  •  {hits} found  •  ETA {eta}";
    }

    static string NextOutputPath(string channel)
    {
        var name = channel.ToUpperInvariant();
        name = name.StartsWith("LAUNCHER_") ? "L" + name[9..] : name.StartsWith("GAME_") ? name[5..] : name;
        name = Regex.Replace(name, "[^A-Z0-9_-]+", "-").Trim('-');
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Strinowa");
        Directory.CreateDirectory(folder);
        for (var number = 1; ; number++)
        {
            var path = Path.Combine(folder, $"{name}-F{number}.txt");
            if (!File.Exists(path)) return path;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _scanCts?.Cancel();
        base.OnClosed(e);
    }

    sealed class RequestPacer(int perSecond)
    {
        readonly object _gate = new();
        readonly double _interval = 1.0 / perSecond;
        double _next;
        public async Task WaitAsync(CancellationToken token)
        {
            double scheduled;
            lock (_gate)
            {
                var now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
                scheduled = Math.Max(now, _next);
                _next = scheduled + _interval;
            }
            var wait = scheduled - Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), token);
        }
    }

    sealed record ScanResult(string Version, string Branch, string Url);

    sealed class NumericVersionComparer : IComparer<string>
    {
        public static NumericVersionComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            var a = left.Split('.').Select(int.Parse).ToArray();
            var b = right.Split('.').Select(int.Parse).ToArray();
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                var av = i < a.Length ? a[i] : 0;
                var bv = i < b.Length ? b[i] : 0;
                var comparison = av.CompareTo(bv);
                if (comparison != 0) return comparison;
            }
            return string.Compare(left, right, StringComparison.Ordinal);
        }
    }
}







