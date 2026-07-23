using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StrinowaWPF;

public partial class Bookmarks : Window
{
    readonly MainWindow _host;
    double _scale = 1;

    static readonly string[] OverseasGames =
        ["Game_Release", "Game_Preview", "Game_Test", "Game_PreTest", "Game_CE", "Game_TestServer", "Game_TW"];
    static readonly string[] OverseasLaunchers =
        ["Launcher_Release", "Launcher_Preview", "Launcher_Test", "Launcher_PreTest", "Launcher_Steam", "Launcher_Epic", "Launcher_TestServer"];
    static readonly string[] CnGames =
        ["Game_Release", "Game_Test", "Game_Dev", "Game_TYF", "Game_KOL", "Game_Expr", "Game_QQ"];
    static readonly string[] CnLaunchers =
        ["Launcher_Release", "Launcher_Test", "Launcher_Dev", "Launcher_TYF", "Launcher_KOL", "Launcher_QQ", "Launcher_Expr"];

    public Bookmarks(MainWindow host)
    {
        InitializeComponent();
        _host = host;
        Owner = host;
        Populate(OverseasGame, OverseasGames);
        Populate(OverseasLauncher, OverseasLaunchers);
        Populate(CnGame, CnGames);
        Populate(CnLauncher, CnLaunchers);
        ApplyTheme();
        ApplyUiScale(host.CurrentUiScale);
    }

    void Populate(Panel panel, string[] branches)
    {
        foreach (var branch in branches)
        {
            var button = new Button
            {
                Content = branch,
                Tag = branch,
                Style = (Style)FindResource("BookmarkButton"),
                ToolTip = $"Index {branch} on PC",
            };
            button.Click += Branch_Click;
            panel.Children.Add(button);
        }
    }

    void Branch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string branch)
        {
            Close();
            // Bookmark feature disabled for now.
            // _host.ScanBookmark(branch);
        }
    }

    public void ApplyUiScale(int percent)
    {
        _scale = Math.Clamp(percent, 50, 200) / 100.0;
        RootBorder.LayoutTransform = new ScaleTransform(_scale, _scale);
        Width = 540 * _scale;
        Height = 620 * _scale;
    }

    public void ApplyTheme()
    {
        var light = AppTheme.CurrentTheme == LauncherTheme.Light;
        var midnight = AppTheme.CurrentTheme == LauncherTheme.Midnight;
        var acrylic = AppTheme.CurrentTheme == LauncherTheme.Acrylic;
        var bg = light ? Color.FromRgb(0xE8, 0xE8, 0xF0) : midnight ? Colors.Black : Color.FromRgb(0x1A, 0x1A, 0x1F);
        var surface = light ? Color.FromRgb(0xF4, 0xF4, 0xFC) : Color.FromRgb(0x22, 0x22, 0x2B);
        var border = light ? Color.FromRgb(0xB0, 0xB0, 0xC8) : Color.FromRgb(0x4A, 0x4A, 0x58);
        var text = light ? Color.FromRgb(0x2A, 0x2A, 0x34) : Color.FromRgb(0xF0, 0xF0, 0xF4);
        var muted = light ? Color.FromRgb(0x66, 0x66, 0x80) : Color.FromRgb(0xA0, 0xA0, 0xB0);
        Resources["WindowBackgroundBrush"] = new SolidColorBrush(acrylic ? Color.FromArgb(0xE8, bg.R, bg.G, bg.B) : bg);
        Resources["WindowBorderBrush"] = new SolidColorBrush(border);
        Resources["ControlSurfaceBrush"] = new SolidColorBrush(surface);
        Resources["ControlHoverBrush"] = new SolidColorBrush(light ? Color.FromRgb(0xDE, 0xDE, 0xEC) : Color.FromRgb(0x3A, 0x3A, 0x48));
        Resources["ControlBorderBrush"] = new SolidColorBrush(border);
        Resources["ControlTextBrush"] = new SolidColorBrush(text);
        Resources["MutedTextBrush"] = new SolidColorBrush(muted);
        Resources["AccentBrush"] = new SolidColorBrush(Color.FromRgb(0xDC, 0x32, 0x78));
        RootBorder.Background = (Brush)Resources["WindowBackgroundBrush"];
        RootBorder.BorderBrush = (Brush)Resources["WindowBorderBrush"];
    }

    void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
