using System.Windows;
using System.Windows.Threading;

namespace StrinowaWPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppPaths.Initialize();
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                ReportError(args.Exception.GetBaseException(), "Background task");
                args.SetObserved();
            };
            base.OnStartup(e);

            // to force high DPI rendering behavior
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(
                    System.Windows.Markup.XmlLanguage.GetLanguage("en-US")));
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportError(e.Exception, "Unhandled exception");
            e.Handled = true;
        }

        public static void ReportError(Exception exception, string? context = null)
        {
            var app = Current;
            var dispatcher = app?.Dispatcher;
            if (dispatcher == null) return;
            dispatcher.BeginInvoke(() =>
            {
                if (app!.MainWindow is MainWindow main)
                    main.ShowModernError(exception, context);
                else
                    SoundEffects.Error();
            });
        }
    }
}
