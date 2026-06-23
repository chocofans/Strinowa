using System.Windows;
using System.Windows.Input;

namespace StrinowaWPF
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            AppTheme.ApplyToAbout(this);
        }

        void CloseBtn_Click(object s, RoutedEventArgs e) => Close();

        void Window_MouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
