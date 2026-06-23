using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace StrinowaWPF
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            ApplyTheme();
        }

        void ApplyTheme()
        {
            bool isLight = AppTheme.CurrentTheme == LauncherTheme.Light;

            RootBorder.Background    = isLight
                ? new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xF4))
                : new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1F));
            RootBorder.BorderBrush   = isLight
                ? new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xC8))
                : new SolidColorBrush(Color.FromRgb(0x2E, 0x2E, 0x38));
            TitleBarBorder.Background = isLight
                ? new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xE8))
                : new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x15));
            TitleText.Foreground     = isLight
                ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30))
                : new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            SepRect.Fill             = isLight
                ? new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xC8))
                : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30));
            AppVersion.Foreground    = isLight
                ? new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x77))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            AppDesc.Foreground       = isLight
                ? new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30))
                : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            AppCredit.Foreground     = isLight
                ? new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x77))
                : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            FooterBorder.Background  = isLight
                ? new SolidColorBrush(Color.FromRgb(0xD8, 0xD8, 0xEC))
                : new SolidColorBrush(Color.FromRgb(0x0E, 0x0E, 0x16));
            FooterBorder.BorderBrush = isLight
                ? new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xC8))
                : new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x30));
        }

        void CloseBtn_Click(object s, RoutedEventArgs e) => Close();
        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e) => DragMove();
    }
}
