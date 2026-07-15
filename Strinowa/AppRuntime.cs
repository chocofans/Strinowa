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
