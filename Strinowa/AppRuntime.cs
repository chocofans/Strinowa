using System.IO;
using System.Windows;
using System.Windows.Media;

namespace StrinowaWPF;

internal static class AppPaths
{
    public static string ResourceDirectory => Path.Combine(AppContext.BaseDirectory, "Resource");
    public static string ConfigPath => Path.Combine(ResourceDirectory, "conf.ini");
    public static string WinBuildsPath => Path.Combine(ResourceDirectory, "WinBuilds.xml");

    public static void Initialize()
    {
        Directory.CreateDirectory(ResourceDirectory);
        MoveLegacyFile("conf.ini", ConfigPath);
        MoveLegacyFile("WinBuilds.xml", WinBuildsPath);
        MoveLegacyFile("WinBuilds.XML", WinBuildsPath);
    }

    static void MoveLegacyFile(string name, string destination)
    {
        var legacy = Path.Combine(AppContext.BaseDirectory, name);
        if (File.Exists(destination) || !File.Exists(legacy)) return;
        try { File.Move(legacy, destination); }
        catch { File.Copy(legacy, destination, false); }
    }
}

internal static class LauncherIdentity
{
    public const string BrandVersion = "0.77.90";
    public const string BuildNumber = "0723";
    public const string BuildDate = "20260723";
    public const string BuildTime = "1216";
    public const string AboutVersionEnglish = "Version 0.77.90.0723.20260723.1216 Beta";
    public const string AboutVersionChinese = "\u7248\u672c 0.77.90.0723.20260723.1216 \u6d4b\u8bd5\u7248";
    public const string AboutVersionPolish = "Wersja 0.77.90.0723.20260723.1216 Beta";
    public const string UserAgent = "Strinowa-WPF-Downloader/0.77.90";
}

internal static class WindowScale
{
    public static int Clamp(int percent) => Math.Clamp(percent, 50, 200);

    public static void Apply(Window window, FrameworkElement root, int percent, double baseWidth, double baseHeight)
    {
        var scale = Clamp(percent) / 100.0;
        var centerX = double.IsNaN(window.Left) ? double.NaN : window.Left + window.Width / 2;
        var centerY = double.IsNaN(window.Top) ? double.NaN : window.Top + window.Height / 2;

        root.LayoutTransform = new ScaleTransform(scale, scale);
        window.Width = baseWidth * scale;
        window.Height = baseHeight * scale;
        window.MinWidth = baseWidth * scale;
        window.MinHeight = baseHeight * scale;

        if (!double.IsNaN(centerX)) window.Left = centerX - window.Width / 2;
        if (!double.IsNaN(centerY)) window.Top = centerY - window.Height / 2;
    }
}

internal static class SoundEffects
{
    static readonly object Gate = new();
    static readonly List<MediaPlayer> Players = [];

    public static void DownloadFinished() => Play("01.mp3");
    public static void Error() => Play("02.mp3");
    public static void Popup() => Play("03.mp3");

    static void Play(string fileName)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        if (!dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => Play(fileName));
            return;
        }

        var path = Resolve(fileName);
        if (path == null) return;
        try
        {
            var player = new MediaPlayer { Volume = 0.8 };
            player.MediaEnded += (_, _) => Release(player);
            player.MediaFailed += (_, _) => Release(player);
            lock (Gate) Players.Add(player);
            player.Open(new Uri(path, UriKind.Absolute));
            player.Play();
        }
        catch { }
    }

    static void Release(MediaPlayer player)
    {
        player.Close();
        lock (Gate) Players.Remove(player);
    }

    static string? Resolve(string fileName)
    {
        var resourceFile = Path.Combine(AppPaths.ResourceDirectory, fileName);
        if (File.Exists(resourceFile)) return resourceFile;
        var developmentFile = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(developmentFile)) return developmentFile;
        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "Strinowa", "Sounds");
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, fileName);
            if (File.Exists(target)) return target;
            var stream = Application.GetResourceStream(new Uri(fileName, UriKind.Relative))?.Stream;
            if (stream == null) return null;
            using (stream)
            using (var output = File.Create(target)) stream.CopyTo(output);
            return target;
        }
        catch { return null; }
    }
}

internal static class BundledTools
{
    static readonly object Gate = new();
    static string? _sevenZipPath;

    public static string? GetSevenZipPath()
    {
        lock (Gate)
        {
            if (_sevenZipPath != null && File.Exists(_sevenZipPath)) return _sevenZipPath;
            var directory = Path.Combine(Path.GetTempPath(), "Strinowa", "7zip");
            Directory.CreateDirectory(directory);
            foreach (var name in new[] { "7za.exe", "7za.dll", "7zxa.dll" })
            {
                var target = Path.Combine(directory, name);
                var source = Path.Combine(AppPaths.ResourceDirectory, name);
                if (File.Exists(source)) File.Copy(source, target, true);
                else
                {
                    var stream = Application.GetResourceStream(new Uri(name, UriKind.Relative))?.Stream;
                    if (stream == null) return null;
                    using (stream)
                    using (var output = File.Create(target)) stream.CopyTo(output);
                }
            }
            _sevenZipPath = Path.Combine(directory, "7za.exe");
            return _sevenZipPath;
        }
    }
}
