using Microsoft.UI.Xaml.Navigation;

namespace WinPrompter
{
    public partial class App : Application
    {
        private MainWindow? _window;

        /// <summary>File path passed on command line (set by Program.cs before app starts).</summary>
        public static string? StartupFilePath { get; set; }

        public static string CrashLogPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinPrompter", "crash.log");

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;

            // Catch all unobserved Task exceptions (async void / fire-and-forget)
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                LogCrash("UnobservedTask", args.Exception);
                args.SetObserved();
            };

            // Catch truly unhandled exceptions on any thread
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                    LogCrash("AppDomain", ex);
            };
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash("UIThread", e.Exception);
            e.Handled = true;
        }

        public static void LogCrash(string source, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
                var inner = ex.InnerException != null
                    ? $"\n  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}\n  {ex.InnerException.StackTrace}"
                    : "";
                File.AppendAllText(CrashLogPath,
                    $"[{DateTime.Now:O}] [{source}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}{inner}\n\n");
            }
            catch { /* last resort — don't throw from logger */ }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}

