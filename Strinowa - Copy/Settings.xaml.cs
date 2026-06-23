using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace StrinowaWPF
{
    // Theme mode options
    public enum LauncherTheme { Dark, Midnight, Light }

    // Terminal text color preset options
    public enum TermColorPreset { AdaptiveWhiteBlack, PinkAccent }

    // Holds the active theme/color state shared across windows
    public static class AppTheme
    {
        public static LauncherTheme CurrentTheme { get; set; } = LauncherTheme.Dark;
        public static TermColorPreset CurrentTermPreset { get; set; } = TermColorPreset.AdaptiveWhiteBlack;

        // Called from MainWindow to apply current theme to its resources
        public static void Apply(MainWindow w)
        {
            var res = w.Resources;

            switch (CurrentTheme)
            {
                case LauncherTheme.Dark:
                    res["BgBrush"]       = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1F));
                    res["TitleBarBrush"] = new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x15));
                    res["BorderBrush2"]  = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38));
                    res["InputBgBrush"]  = new SolidColorBrush(Color.FromRgb(0x13, 0x13, 0x1A));
                    break;

                case LauncherTheme.Midnight:
                    res["BgBrush"]       = new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x16));
                    res["TitleBarBrush"] = new SolidColorBrush(Color.FromRgb(0x09, 0x09, 0x0E));
                    res["BorderBrush2"]  = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x26));
                    res["InputBgBrush"]  = new SolidColorBrush(Color.FromRgb(0x09, 0x09, 0x12));
                    break;

                case LauncherTheme.Light:
                    res["BgBrush"]       = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xF0));
                    res["TitleBarBrush"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xE8));
                    res["BorderBrush2"]  = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xC8));
                    res["InputBgBrush"]  = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xFF));
                    break;
            }

            // Terminal normal text — shifts with theme
            switch (CurrentTermPreset)
            {
                case TermColorPreset.AdaptiveWhiteBlack:
                    res["TerminalFg"] = CurrentTheme == LauncherTheme.Light
                        ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30))
                        : new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));

                    // CN text: darker in dark, lighter in light
                    TC.CN = CurrentTheme == LauncherTheme.Light
                        ? new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66))
                        : new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xBB));
                    break;

                case TermColorPreset.PinkAccent:
                    res["TerminalFg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0xA0));
                    TC.CN = new SolidColorBrush(Color.FromRgb(0x88, 0x44, 0x68));
                    break;
            }
        }

        // Returns the correct normal brush for the current theme+preset without touching resources
        public static SolidColorBrush GetNormalBrush()
        {
            return CurrentTermPreset switch
            {
                TermColorPreset.PinkAccent => new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0xA0)),
                _ => CurrentTheme == LauncherTheme.Light
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30))
                    : new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
            };
        }

        // CN text color — slightly dimmer than normal. Writable so Apply() can override.
        public static SolidColorBrush GetCNBrush() => TC.CN;

        // Devkit text is always animated — see DevkitColorAnimator
        // No static color is returned; the animator drives the brush directly.
    }

    // Drives the animated shifting palette on devkit text blocks
    public class DevkitColorAnimator
    {
        // Minimal palette — muted enough to stand out without being garish
        static readonly Color[] _palette =
        {
            Color.FromRgb(0x9B, 0x30, 0x78),
            Color.FromRgb(0xB4, 0x28, 0x78),
            Color.FromRgb(0x8C, 0x00, 0xC8),
            Color.FromRgb(0x7A, 0x10, 0xA0),
            Color.FromRgb(0xA0, 0x20, 0x90),
        };

        readonly SolidColorBrush _brush;
        readonly DispatcherTimer _timer;
        int _index = 0;

        public SolidColorBrush Brush => _brush;

        public DevkitColorAnimator()
        {
            _brush = new SolidColorBrush(_palette[0]);
            _brush.Freeze();  // will not freeze — we animate it below
            _brush = new SolidColorBrush(_palette[0]);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
            _timer.Tick += Tick;
            _timer.Start();
        }

        void Tick(object? sender, EventArgs e)
        {
            _index = (_index + 1) % _palette.Length;
            var anim = new ColorAnimation(_palette[_index], TimeSpan.FromMilliseconds(900))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            _brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        public void Stop() => _timer.Stop();
    }

    public partial class SettingsWindow : Window
    {
        // Pending selections (applied only on Apply click)
        LauncherTheme _pendingTheme;
        TermColorPreset _pendingTermPreset;

        // Preview animator for devkit row
        readonly DevkitColorAnimator _devkitAnim = new();

        public SettingsWindow()
        {
            InitializeComponent();
            _pendingTheme      = AppTheme.CurrentTheme;
            _pendingTermPreset = AppTheme.CurrentTermPreset;
            RefreshThemeSelection();
            RefreshTermColorSelection();
            PreviewDevkit.Foreground = _devkitAnim.Brush;
            UpdatePreview();
        }

        void RefreshThemeSelection()
        {
            ThemeDark    .BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38));
            ThemeMidnight.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38));
            ThemeLight   .BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38));

            var accent = new SolidColorBrush(Color.FromRgb(0xDC, 0x32, 0x78));
            switch (_pendingTheme)
            {
                case LauncherTheme.Dark:     ThemeDark    .BorderBrush = accent; break;
                case LauncherTheme.Midnight: ThemeMidnight.BorderBrush = accent; break;
                case LauncherTheme.Light:    ThemeLight   .BorderBrush = accent; break;
            }
        }

        void RefreshTermColorSelection()
        {
            TermColorA.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38));
            TermColorB.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38));

            var accent = new SolidColorBrush(Color.FromRgb(0xDC, 0x32, 0x78));
            switch (_pendingTermPreset)
            {
                case TermColorPreset.AdaptiveWhiteBlack: TermColorA.BorderBrush = accent; break;
                case TermColorPreset.PinkAccent:         TermColorB.BorderBrush = accent; break;
            }
        }

        void UpdatePreview()
        {
            bool isLight = _pendingTheme == LauncherTheme.Light;

            // Normal text preview
            PreviewNormal.Foreground = _pendingTermPreset switch
            {
                TermColorPreset.PinkAccent => new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0xA0)),
                _ => isLight
                    ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30))
                    : new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
            };

            // CN text preview — dimmed relative to normal
            PreviewCN.Foreground = _pendingTermPreset switch
            {
                TermColorPreset.PinkAccent => new SolidColorBrush(Color.FromRgb(0x88, 0x44, 0x68)),
                _ => isLight
                    ? new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66))
                    : new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xBB)),
            };

            // Release color stays pink always
            PreviewRelease.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0xA0));

            // Devkit is always animated — driven by _devkitAnim.Brush
            // PreviewDevkit.Foreground is already bound in constructor
        }

        void ThemeDark_Click(object s, MouseButtonEventArgs e)
        {
            _pendingTheme = LauncherTheme.Dark;
            RefreshThemeSelection();
            UpdatePreview();
        }

        void ThemeMidnight_Click(object s, MouseButtonEventArgs e)
        {
            _pendingTheme = LauncherTheme.Midnight;
            RefreshThemeSelection();
            UpdatePreview();
        }

        void ThemeLight_Click(object s, MouseButtonEventArgs e)
        {
            _pendingTheme = LauncherTheme.Light;
            RefreshThemeSelection();
            UpdatePreview();
        }

        void TermColorA_Click(object s, MouseButtonEventArgs e)
        {
            _pendingTermPreset = TermColorPreset.AdaptiveWhiteBlack;
            RefreshTermColorSelection();
            UpdatePreview();
        }

        void TermColorB_Click(object s, MouseButtonEventArgs e)
        {
            _pendingTermPreset = TermColorPreset.PinkAccent;
            RefreshTermColorSelection();
            UpdatePreview();
        }

        void ApplyBtn_Click(object s, RoutedEventArgs e)
        {
            AppTheme.CurrentTheme      = _pendingTheme;
            AppTheme.CurrentTermPreset = _pendingTermPreset;
            if (Owner is MainWindow mw)
                AppTheme.Apply(mw);
            _devkitAnim.Stop();
            Close();
        }

        void CloseBtn_Click(object s, RoutedEventArgs e)
        {
            _devkitAnim.Stop();
            Close();
        }

        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e) => DragMove();
    }
}
