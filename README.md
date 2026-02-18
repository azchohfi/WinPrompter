# WinPrompter

A modern teleprompter for Windows 11.

## Features

- Load markdown scripts (`.md`, `.txt`) or paste from clipboard
- Smooth GPU-accelerated scrolling with adjustable speed
- 6 built-in themes (Classic, Light, High Contrast, Green Screen, Studio, Warm)
- Voice auto-advance using on-device speech recognition
- Mirror mode for beam-splitter teleprompter setups
- Adjustable font size (24pt–120pt), opacity, and window size
- Drag & drop file loading
- Cue markers for auto-pause (`<!-- pause 3s -->`)
- Section jump via markdown headings
- Countdown timer before scroll starts
- WPM display and progress indicator
- Fullscreen mode
- Recent files list

## Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| Space | Play / Pause |
| Escape | Stop & reset |
| Ctrl + O | Open file |
| Ctrl + V | Paste from clipboard |
| Ctrl + Plus | Increase font size |
| Ctrl + Minus | Decrease font size |
| Alt + ↑ | Increase speed |
| Alt + ↓ | Decrease speed |
| F / F11 | Toggle fullscreen |
| M | Toggle mirror mode |
| V | Toggle voice auto-advance |
| [ | Decrease opacity |
| ] | Increase opacity |

## Getting Started

### Prerequisites

- Windows 10/11
- .NET 10 SDK
- Windows App SDK 1.8+

### Build & Run

```
dotnet build WinPrompter.csproj -p:Platform=x64
dotnet run --project WinPrompter.csproj -p:Platform=x64
```

## Technology

- WinUI 3 / Windows App SDK
- WebView2 for rendering
- Markdig for markdown parsing
- Windows.Media.SpeechRecognition for voice
- CommunityToolkit.Mvvm

## License

MIT
