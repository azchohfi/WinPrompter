using Microsoft.UI.Xaml.Navigation;

namespace WinPrompter
{
    public partial class App : Application
    {
        private MainWindow? _window;

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinPrompter", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath,
                $"[{DateTime.Now:O}] {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}\n\n");
            e.Handled = true;
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}

