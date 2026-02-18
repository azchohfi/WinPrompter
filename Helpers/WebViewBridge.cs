using System.Text.Json;

namespace WinPrompter.Helpers;

public class WebViewBridge
{
    private readonly WebView2 _webView;

    public event Action<string>? MessageReceived;

    public WebViewBridge(WebView2 webView)
    {
        _webView = webView;
    }

    public void Initialize()
    {
        _webView.CoreWebView2.WebMessageReceived += (_, e) =>
        {
            MessageReceived?.Invoke(e.WebMessageAsJson);
        };
    }

    public async Task SetContentAsync(string html)
        => await ExecuteAsync($"prompter.setContent({JsonEncode(html)})");

    public async Task SetThemeAsync(string theme)
        => await ExecuteAsync($"prompter.setTheme('{theme}')");

    public async Task SetFontSizeAsync(double size)
        => await ExecuteAsync($"prompter.setFontSize({size})");

    public async Task PlayAsync()
        => await ExecuteAsync("prompter.play()");

    public async Task PauseAsync()
        => await ExecuteAsync("prompter.pause()");

    public async Task StopAsync()
        => await ExecuteAsync("prompter.stop()");

    public async Task SetSpeedAsync(double speed)
        => await ExecuteAsync($"prompter.setSpeed({speed})");

    public async Task SetMirrorAsync(bool enabled)
        => await ExecuteAsync($"prompter.setMirror({(enabled ? "true" : "false")})");

    public async Task ScrollToPositionAsync(double percent)
        => await ExecuteAsync($"prompter.scrollToPosition({percent})");

    public async Task ScrollToWordIndexAsync(int charOffset)
        => await ExecuteAsync($"prompter.scrollToWordIndex({charOffset})");

    public async Task<string> GetHeadingsAsync()
        => await ExecuteAsync("prompter.getHeadings()") ?? "[]";

    public async Task PlayWithCountdownAsync(int seconds)
        => await ExecuteAsync($"prompter.playWithCountdown({seconds})");

    private async Task<string?> ExecuteAsync(string script)
    {
        try
        {
            return await _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            return null;
        }
    }

    private static string JsonEncode(string value)
        => JsonSerializer.Serialize(value);
}
