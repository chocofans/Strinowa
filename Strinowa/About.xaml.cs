using System.Windows;
using System.Windows.Input;

namespace StrinowaWPF
{
    public partial class About : Window
    {
        public About(MainWindow? host = null)
        {
            InitializeComponent();
            ApplyUiScale(host?.CurrentUiScale ?? 100);
            AppTheme.ApplyToAbout(this);
        }

        public void ApplyUiScale(int percent)
        {
            WindowScale.Apply(this, RootBorder, percent, 480, 298);
        }

        void CloseBtn_Click(object s, RoutedEventArgs e) => Close();

        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
