using Microsoft.UI.Xaml.Navigation;

namespace WinPrompter
{
    public partial class App : Application
    {
        private MainWindow? _window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}

