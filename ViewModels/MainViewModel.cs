namespace WinPrompter.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {
        [ObservableProperty] private string _scriptMarkdown = string.Empty;
        [ObservableProperty] private string _htmlContent = string.Empty;
        [ObservableProperty] private bool _isPlaying;
        [ObservableProperty] private double _speed = 1.0;
        [ObservableProperty] private double _fontSize = 36;
        [ObservableProperty] private string _currentTheme = "classic";
        [ObservableProperty] private bool _isMirrorMode;
        [ObservableProperty] private double _opacity = 1.0;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private bool _isOverlayVisible;
        [ObservableProperty] private string _currentFileName = string.Empty;
        [ObservableProperty] private bool _isVoiceMode;
        [ObservableProperty] private bool _hasScript;

        public MainViewModel()
        {
            Title = "WinPrompter";
        }

        public void LoadSettings(Services.SettingsService settings)
        {
            FontSize = settings.FontSize;
            Speed = settings.Speed;
            CurrentTheme = settings.Theme;
            IsMirrorMode = settings.MirrorMode;
            Opacity = settings.Opacity;
        }

        public void SaveSettings(Services.SettingsService settings)
        {
            settings.FontSize = FontSize;
            settings.Speed = Speed;
            settings.Theme = CurrentTheme;
            settings.MirrorMode = IsMirrorMode;
            settings.Opacity = Opacity;
        }

        [RelayCommand]
        public void IncreaseSpeed()
        {
            Speed = Math.Min(5.0, Math.Round(Speed + 0.25, 2));
        }

        [RelayCommand]
        public void DecreaseSpeed()
        {
            Speed = Math.Max(0.25, Math.Round(Speed - 0.25, 2));
        }

        [RelayCommand]
        public void IncreaseFontSize()
        {
            FontSize = Math.Min(120, FontSize + 4);
        }

        [RelayCommand]
        public void DecreaseFontSize()
        {
            FontSize = Math.Max(24, FontSize - 4);
        }

        [RelayCommand]
        public void TogglePlayPause()
        {
            IsPlaying = !IsPlaying;
        }

        [RelayCommand]
        public void StopPlayback()
        {
            IsPlaying = false;
            Progress = 0;
        }

        [RelayCommand]
        public void ToggleMirror()
        {
            IsMirrorMode = !IsMirrorMode;
        }

        [RelayCommand]
        public void ToggleOverlay()
        {
            IsOverlayVisible = !IsOverlayVisible;
        }

        public static string[] AvailableThemes => ["classic", "light", "high-contrast", "green-screen", "studio", "warm"];
        public static string[] ThemeDisplayNames => ["Classic", "Light", "High Contrast", "Green Screen", "Studio", "Warm"];
    }
}

