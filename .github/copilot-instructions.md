# Copilot Instructions for WinPrompter

## Project Overview

WinPrompter is a modern Windows 11 teleprompter application built with WinUI 3 / Windows App SDK. It renders markdown scripts as scrolling text in a floating borderless always-on-top window with voice-driven auto-advance.

## Build & Run

- **Build**: `dotnet build WinPrompter.csproj -p:Platform=ARM64` (or `x64`/`x86`)
- **Run**: `dotnet run --project WinPrompter.csproj -p:Platform=ARM64`
- **Platform must be specified** — `AnyCPU` is not supported by WinUI 3.
- No Visual Studio required — everything works via `dotnet` CLI.
- No test project exists yet.

## Architecture

- **Framework**: WinUI 3 (Windows App SDK 1.8), .NET 10, CommunityToolkit.Mvvm
- **Unpackaged mode**: `WindowsPackageType=None` — no MSIX deployment needed for dev. `Package.Current` APIs are unavailable; use `AppContext.BaseDirectory` instead.
- **Rendering**: WebView2 hosts `Assets/Web/teleprompter.html` which handles all scroll/theme/font rendering via JS.
- **MVVM**: CommunityToolkit.Mvvm with field-based `[ObservableProperty]` syntax (not partial properties — the source generator doesn't produce implementations with v8.4.0 on WinUI 3).
- **Custom entry point**: `Program.cs` with `DISABLE_XAML_GENERATED_MAIN` for single-instance Mutex + named pipe IPC.

## Key Technical Constraints

### CommunityToolkit.Mvvm
- **Must use field-based `[ObservableProperty]`**: `[ObservableProperty] private string _myField = "";`
- Partial property syntax does NOT work — source generator doesn't produce implementations.
- Warning `MVVMTK0045` is suppressed in csproj via `<NoWarn>`.

### WebView2 HWND Z-Order
- WebView2 is an out-of-process HWND that sits ABOVE all XAML content.
- **Never overlay XAML controls on top of WebView2** — they will be unclickable.
- The toolbar is in `Grid.Row=1` below the WebView, not overlaid on it.

### JS ↔ C# Interop
- C# → JS: `CoreWebView2.ExecuteScriptAsync("prompter.methodName(...)")`
- JS → C#: `window.chrome.webview.postMessage(JSON.stringify(msg))`
- Messages may be double-JSON-encoded — the handler tries both single and double parsing.
- **Critical**: When processing messages in `DispatcherQueue.TryEnqueue`, extract ALL values from `JsonDocument` into local primitives BEFORE dispatching. The `using var doc` disposes before the lambda runs (use-after-free crash).

### Win32 Interop (P/Invoke)
- `AllowUnsafeBlocks` is required for `[LibraryImport]` source generation.
- On ARM64, many Win32 functions need explicit `EntryPoint` with W suffix (e.g., `ExtractIconW`, `LoadIconW`).
- `[DllImport]` must be used instead of `[LibraryImport]` for structs with `ByValTStr` (e.g., NOTIFYICONDATA).
- **Never use GDI window regions** (`SetWindowRgn`, `CreateRoundRectRgn`) — they conflict with the WinUI 3 compositor and cause native crashes. Use XAML `CornerRadius` instead.

### System Tray
- WinUI 3 has no built-in tray support. Uses Win32 `Shell_NotifyIcon` with a hidden message-only window (`HWND_MESSAGE` parent).
- **Never subclass the main WinUI window's wndproc** for tray messages — it causes native crashes when modal dialogs (file picker) pump messages.

### AppWindow.Changed Re-entrancy
- Calling `AppWindow.Move()` inside a `Changed` handler triggers another `Changed` event → infinite loop → `CoreMessagingXP.dll` crash.
- Always use a boolean guard (`_isHandlingResize`).

### Slider XAML
- Do not set `Value` in XAML if `Minimum`/`Maximum` are also set — `Value` is assigned before `Minimum`, causing `XamlParseException`. Set `Value` in code-behind after initialization.

## Project Structure

```
WinPrompter/
├── Program.cs                    # Entry point: single-instance Mutex + named pipe IPC
├── App.xaml.cs                   # App startup, crash logging (3 handlers)
├── MainWindow.xaml/.cs           # Main UI + all app logic (~700 lines)
├── ViewModels/
│   ├── BaseViewModel.cs          # ObservableObject base
│   └── MainViewModel.cs          # All observable properties + commands
├── Services/
│   ├── MarkdownService.cs        # Markdig pipeline, word extraction
│   ├── SettingsService.cs        # JSON settings at %LocalAppData%/WinPrompter/
│   ├── SpeechService.cs          # Windows.Media.SpeechRecognition continuous dictation
│   └── VoiceAdvanceService.cs    # Speech → fuzzy match → scroll orchestrator
├── Helpers/
│   ├── WebViewBridge.cs          # C# ↔ JS interop wrapper
│   ├── WindowHelper.cs           # Win32: borderless, positioning, opacity, fullscreen
│   ├── FuzzyMatcher.cs           # Levenshtein sliding-window matcher
│   └── TrayIconHelper.cs         # Win32 Shell_NotifyIcon
├── Models/
│   └── ScriptWordIndex.cs        # Word sequence with char offset mapping
├── Assets/
│   └── Web/
│       ├── teleprompter.html     # WebView2 host page
│       ├── teleprompter.css      # 6 themes, typography, progress bar
│       └── teleprompter.js       # Scroll engine, all JS APIs
├── .github/
│   └── workflows/
│       ├── ci.yml                # Build on push/PR (x64 + arm64)
│       └── release.yml           # MSIX build + Store deploy on tag
└── docs/
    └── STORE_DEPLOYMENT.md       # Store submission setup guide
```

## Crash Logging

- Managed crashes: `App.LogCrash(context, exception)` → `%LOCALAPPDATA%/WinPrompter/crash.log`
- Three handlers in App.xaml.cs: `UnhandledException`, `UnobservedTaskException`, `AppDomain.UnhandledException`
- Native crashes (stowed exceptions like `0xc000027b`) bypass all managed handlers — check Windows Event Log: `Get-WinEvent -LogName Application -MaxEvents 10`

## Settings

Stored as JSON at `%LOCALAPPDATA%/WinPrompter/settings.json`. Properties: FontSize, Speed, Theme, MirrorMode, Opacity, CountdownEnabled, VoiceSensitivity, RecentFiles.

## Themes

6 built-in themes: `classic` (default, black/white), `light`, `high-contrast`, `green-screen`, `studio`, `warm`. Defined in `teleprompter.css` and mapped in `MainViewModel.AvailableThemes`.

## Keyboard Shortcuts

- Space: Play/Pause, Escape: Stop, Ctrl+O: Open, Ctrl+V: Paste
- Ctrl+/Ctrl-: Font size, Alt+Up/Down: Speed
- F/F11: Fullscreen, M: Mirror, V: Voice, [/]: Opacity
