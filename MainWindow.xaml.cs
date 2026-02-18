using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinPrompter.Helpers;
using WinPrompter.Services;

namespace WinPrompter;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly MarkdownService _markdownService = new();
    private readonly SettingsService _settingsService = new();
    private WebViewBridge? _bridge;
    private DispatcherTimer? _overlayHideTimer;

    public MainWindow()
    {
        this.InitializeComponent();

        // Load saved settings
        _vm.LoadSettings(_settingsService);

        // Set up overlay auto-hide timer
        _overlayHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _overlayHideTimer.Tick += (_, _) =>
        {
            HideOverlay();
            _overlayHideTimer.Stop();
        };

        // Populate theme combo
        for (int i = 0; i < MainViewModel.ThemeDisplayNames.Length; i++)
            ThemeCombo.Items.Add(MainViewModel.ThemeDisplayNames[i]);
        ThemeCombo.SelectedIndex = Array.IndexOf(MainViewModel.AvailableThemes, _vm.CurrentTheme);

        // Update UI from view model
        SpeedText.Text = $"{_vm.Speed:F2}x";
        FontSizeText.Text = $"{_vm.FontSize:F0}";
        OpacitySlider.Value = _vm.Opacity * 100;

        // Configure window
        WindowHelper.ConfigureAsFloatingPrompter(this);
        if (_vm.Opacity < 1.0)
            WindowHelper.SetOpacity(this, _vm.Opacity);

        // Keyboard handling on the root grid
        RootGrid.KeyDown += RootGrid_KeyDown;
        RootGrid.PointerPressed += RootGrid_PointerPressed;

        // Initialize WebView2
        InitializeWebViewAsync();
    }

    private async void InitializeWebViewAsync()
    {
        await PrompterWebView.EnsureCoreWebView2Async();

        var packagePath = AppContext.BaseDirectory;
        PrompterWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.local",
            Path.Combine(packagePath, "Assets", "Web"),
            CoreWebView2HostResourceAccessKind.Allow);

        // Disable dev tools and context menu for clean UX
        PrompterWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        PrompterWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        PrompterWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        PrompterWebView.CoreWebView2.Settings.IsZoomControlEnabled = false;

        _bridge = new WebViewBridge(PrompterWebView);
        _bridge.Initialize();
        _bridge.MessageReceived += OnWebViewMessage;

        PrompterWebView.CoreWebView2.Navigate("https://app.local/teleprompter.html");

        // Wait for page load then apply settings
        PrompterWebView.CoreWebView2.NavigationCompleted += async (_, _) =>
        {
            await _bridge.SetThemeAsync(_vm.CurrentTheme);
            await _bridge.SetFontSizeAsync(_vm.FontSize);
            await _bridge.SetSpeedAsync(_vm.Speed);
            await _bridge.SetMirrorAsync(_vm.IsMirrorMode);

            // Show welcome message if no script loaded
            if (!_vm.HasScript)
            {
                var welcomeHtml = "<div class='welcome-message'><h1>WinPrompter</h1><p>Right-click or hover at top for controls<br/>Ctrl+O to open · Ctrl+V to paste</p></div>";
                await _bridge.SetContentAsync(welcomeHtml);
            }
        };
    }

    private void OnWebViewMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\"));
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();

            DispatcherQueue.TryEnqueue(() =>
            {
                switch (type)
                {
                    case "progress":
                        _vm.Progress = root.GetProperty("percent").GetDouble();
                        break;
                    case "playbackChanged":
                        _vm.IsPlaying = root.GetProperty("isPlaying").GetBoolean();
                        PlayIcon.Glyph = _vm.IsPlaying ? "\uE769" : "\uE768"; // Pause : Play
                        break;
                    case "scrollComplete":
                        _vm.IsPlaying = false;
                        PlayIcon.Glyph = "\uE768";
                        break;
                    case "keydown":
                        HandleWebViewKeyDown(root);
                        break;
                }
            });
        }
        catch
        {
            // Ignore malformed messages — sometimes the JSON from WebView2 is double-encoded
            try
            {
                // Try parsing as a raw JSON string (double-encoded)
                var unescaped = JsonSerializer.Deserialize<string>(json);
                if (unescaped != null)
                {
                    using var doc = JsonDocument.Parse(unescaped);
                    var root = doc.RootElement;
                    var type = root.GetProperty("type").GetString();

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        switch (type)
                        {
                            case "progress":
                                _vm.Progress = root.GetProperty("percent").GetDouble();
                                break;
                            case "playbackChanged":
                                _vm.IsPlaying = root.GetProperty("isPlaying").GetBoolean();
                                PlayIcon.Glyph = _vm.IsPlaying ? "\uE769" : "\uE768";
                                break;
                            case "scrollComplete":
                                _vm.IsPlaying = false;
                                PlayIcon.Glyph = "\uE768";
                                break;
                            case "keydown":
                                HandleWebViewKeyDown(root);
                                break;
                        }
                    });
                }
            }
            catch { /* truly malformed, ignore */ }
        }
    }

    private void HandleWebViewKeyDown(JsonElement root)
    {
        var key = root.GetProperty("key").GetString() ?? "";
        var ctrl = root.GetProperty("ctrl").GetBoolean();
        var alt = root.GetProperty("alt").GetBoolean();

        ProcessShortcut(key, ctrl, alt);
    }

    private async void ProcessShortcut(string key, bool ctrl, bool alt)
    {
        if (_bridge == null) return;

        if (key == " ")
        {
            await TogglePlayPause();
        }
        else if (key == "Escape")
        {
            await StopPlayback();
        }
        else if (ctrl && (key == "+" || key == "="))
        {
            _vm.IncreaseFontSize();
            await _bridge.SetFontSizeAsync(_vm.FontSize);
            FontSizeText.Text = $"{_vm.FontSize:F0}";
            SaveSettings();
        }
        else if (ctrl && key == "-")
        {
            _vm.DecreaseFontSize();
            await _bridge.SetFontSizeAsync(_vm.FontSize);
            FontSizeText.Text = $"{_vm.FontSize:F0}";
            SaveSettings();
        }
        else if (alt && (key == "ArrowUp" || key == "Up"))
        {
            _vm.IncreaseSpeed();
            await _bridge.SetSpeedAsync(_vm.Speed);
            SpeedText.Text = $"{_vm.Speed:F2}x";
            SaveSettings();
        }
        else if (alt && (key == "ArrowDown" || key == "Down"))
        {
            _vm.DecreaseSpeed();
            await _bridge.SetSpeedAsync(_vm.Speed);
            SpeedText.Text = $"{_vm.Speed:F2}x";
            SaveSettings();
        }
        else if (key == "F11" || (!ctrl && !alt && key == "f"))
        {
            WindowHelper.ToggleFullscreen(this);
        }
        else if (!ctrl && !alt && key == "m")
        {
            _vm.ToggleMirror();
            await _bridge.SetMirrorAsync(_vm.IsMirrorMode);
            BtnMirror.IsChecked = _vm.IsMirrorMode;
            SaveSettings();
        }
        else if (ctrl && key == "o")
        {
            await OpenFileAsync();
        }
        else if (ctrl && key == "v")
        {
            await PasteFromClipboardAsync();
        }
        else if (key == "[")
        {
            _vm.Opacity = Math.Max(0.1, _vm.Opacity - 0.1);
            WindowHelper.SetOpacity(this, _vm.Opacity);
            OpacitySlider.Value = _vm.Opacity * 100;
            SaveSettings();
        }
        else if (key == "]")
        {
            _vm.Opacity = Math.Min(1.0, _vm.Opacity + 0.1);
            WindowHelper.SetOpacity(this, _vm.Opacity);
            OpacitySlider.Value = _vm.Opacity * 100;
            SaveSettings();
        }
    }

    // ── Playback ──

    private async Task TogglePlayPause()
    {
        if (_bridge == null) return;
        _vm.TogglePlayPause();
        if (_vm.IsPlaying)
            await _bridge.PlayAsync();
        else
            await _bridge.PauseAsync();
        PlayIcon.Glyph = _vm.IsPlaying ? "\uE769" : "\uE768";
    }

    private async Task StopPlayback()
    {
        if (_bridge == null) return;
        _vm.StopPlayback();
        await _bridge.StopAsync();
        PlayIcon.Glyph = "\uE768";
    }

    // ── File handling ──

    private async Task OpenFileAsync()
    {
        var picker = new FileOpenPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".markdown");

        // Initialize with window handle
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            var text = await FileIO.ReadTextAsync(file);
            await LoadMarkdownAsync(text, file.Name);
        }
    }

    private async Task PasteFromClipboardAsync()
    {
        var content = Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.Text))
        {
            var text = await content.GetTextAsync();
            if (!string.IsNullOrWhiteSpace(text))
                await LoadMarkdownAsync(text, "Clipboard");
        }
    }

    private async Task LoadMarkdownAsync(string markdown, string sourceName)
    {
        if (_bridge == null) return;

        _vm.ScriptMarkdown = markdown;
        _vm.CurrentFileName = sourceName;
        _vm.HasScript = true;

        var html = _markdownService.ConvertToHtml(markdown);
        _vm.HtmlContent = html;

        await _bridge.SetContentAsync(html);
    }

    // ── Drag & Drop ──

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Load script";
        }
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            foreach (var item in items)
            {
                if (item is StorageFile file &&
                    (file.FileType == ".md" || file.FileType == ".txt" || file.FileType == ".markdown"))
                {
                    var text = await FileIO.ReadTextAsync(file);
                    await LoadMarkdownAsync(text, file.Name);
                    break;
                }
            }
        }
    }

    // ── Overlay ──

    private void ShowOverlay()
    {
        OverlayPanel.Visibility = Visibility.Visible;
        _vm.IsOverlayVisible = true;
        _overlayHideTimer?.Stop();
        _overlayHideTimer?.Start();
    }

    private void HideOverlay()
    {
        OverlayPanel.Visibility = Visibility.Collapsed;
        _vm.IsOverlayVisible = false;
    }

    private void HoverZone_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ShowOverlay();
    }

    private void RootGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(RootGrid).Properties.IsRightButtonPressed)
        {
            if (_vm.IsOverlayVisible)
                HideOverlay();
            else
                ShowOverlay();
        }
    }

    // ── Keyboard (XAML level) ──

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        var alt = InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        var key = e.Key switch
        {
            Windows.System.VirtualKey.Space => " ",
            Windows.System.VirtualKey.Escape => "Escape",
            Windows.System.VirtualKey.F11 => "F11",
            Windows.System.VirtualKey.Up => "ArrowUp",
            Windows.System.VirtualKey.Down => "ArrowDown",
            _ => e.Key.ToString().ToLowerInvariant()
        };

        ProcessShortcut(key, ctrl, alt);
    }

    // ── Button handlers ──

    private async void BtnOpen_Click(object sender, RoutedEventArgs e) => await OpenFileAsync();
    private async void BtnPaste_Click(object sender, RoutedEventArgs e) => await PasteFromClipboardAsync();
    private async void BtnPlay_Click(object sender, RoutedEventArgs e) => await TogglePlayPause();
    private async void BtnStop_Click(object sender, RoutedEventArgs e) => await StopPlayback();

    private async void BtnSpeedUp_Click(object sender, RoutedEventArgs e)
    {
        _vm.IncreaseSpeed();
        if (_bridge != null) await _bridge.SetSpeedAsync(_vm.Speed);
        SpeedText.Text = $"{_vm.Speed:F2}x";
        SaveSettings();
    }

    private async void BtnSpeedDown_Click(object sender, RoutedEventArgs e)
    {
        _vm.DecreaseSpeed();
        if (_bridge != null) await _bridge.SetSpeedAsync(_vm.Speed);
        SpeedText.Text = $"{_vm.Speed:F2}x";
        SaveSettings();
    }

    private async void BtnFontUp_Click(object sender, RoutedEventArgs e)
    {
        _vm.IncreaseFontSize();
        if (_bridge != null) await _bridge.SetFontSizeAsync(_vm.FontSize);
        FontSizeText.Text = $"{_vm.FontSize:F0}";
        SaveSettings();
    }

    private async void BtnFontDown_Click(object sender, RoutedEventArgs e)
    {
        _vm.DecreaseFontSize();
        if (_bridge != null) await _bridge.SetFontSizeAsync(_vm.FontSize);
        FontSizeText.Text = $"{_vm.FontSize:F0}";
        SaveSettings();
    }

    private async void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedIndex < 0) return;
        _vm.CurrentTheme = MainViewModel.AvailableThemes[ThemeCombo.SelectedIndex];
        if (_bridge != null) await _bridge.SetThemeAsync(_vm.CurrentTheme);

        // Update window background to match theme
        var bgColor = _vm.CurrentTheme switch
        {
            "light" => Microsoft.UI.Colors.WhiteSmoke,
            "studio" => Windows.UI.Color.FromArgb(255, 30, 30, 30),
            "warm" => Windows.UI.Color.FromArgb(255, 44, 24, 16),
            "green-screen" => Windows.UI.Color.FromArgb(255, 0, 177, 64),
            "high-contrast" => Microsoft.UI.Colors.Black,
            _ => Microsoft.UI.Colors.Black,
        };
        RootGrid.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(bgColor);

        SaveSettings();
    }

    private async void BtnMirror_Click(object sender, RoutedEventArgs e)
    {
        _vm.ToggleMirror();
        if (_bridge != null) await _bridge.SetMirrorAsync(_vm.IsMirrorMode);
        SaveSettings();
    }

    private void OpacitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _vm.Opacity = e.NewValue / 100.0;
        WindowHelper.SetOpacity(this, _vm.Opacity);
        SaveSettings();
    }

    private void BtnFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowHelper.ToggleFullscreen(this);
    }

    private void SaveSettings()
    {
        _vm.SaveSettings(_settingsService);
    }
}
