using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using System.Runtime.InteropServices;

namespace WinPrompter.Helpers;

public static partial class WindowHelper
{
    public static AppWindow GetAppWindow(Window window)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    private static bool _isHandlingResize;

    public static void ConfigureAsFloatingPrompter(Window window, int width = 1400, int height = 300)
    {
        var appWindow = GetAppWindow(window);

        // Extend content into title bar and collapse it
        appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        // Hide border and title bar
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = true;
        }

        PositionTopCenter(appWindow, width, height);

        // Square top corners, rounded bottom corners
        ApplyBottomRoundedCorners(window, width, height);

        // Recenter horizontally when resized (with re-entrancy guard)
        appWindow.Changed += (aw, args) =>
        {
            if (!args.DidSizeChange || _isHandlingResize) return;
            _isHandlingResize = true;
            try
            {
                var display = DisplayArea.GetFromWindowId(aw.Id, DisplayAreaFallback.Primary);
                int x = (display.WorkArea.Width - aw.Size.Width) / 2;
                aw.Move(new Windows.Graphics.PointInt32(x, aw.Position.Y));
                ApplyBottomRoundedCorners(window, aw.Size.Width, aw.Size.Height);
            }
            catch { }
            finally { _isHandlingResize = false; }
        };
    }

    public static void PositionTopCenter(AppWindow appWindow, int width, int height)
    {
        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        int x = (displayArea.WorkArea.Width - width) / 2;
        appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, 0, width, height));
    }

    /// <summary>
    /// Apply a window region: square at top, rounded at bottom.
    /// </summary>
    private static void ApplyBottomRoundedCorners(Window window, int width, int height)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        var dpi = GetDpiForWindow(hWnd);
        double scale = dpi / 96.0;

        int w = (int)(width * scale);
        int h = (int)(height * scale);
        int radius = (int)(16 * scale);

        // Create a region that is square at the top and rounded at the bottom.
        // Combine a rectangle (top half) with a round-rect (bottom half).
        var topRect = CreateRectRgn(0, 0, w, h - radius);
        var bottomRound = CreateRoundRectRgn(0, h - radius * 2, w + 1, h + 1, radius * 2, radius * 2);
        CombineRgn(topRect, topRect, bottomRound, 2 /* RGN_OR */);
        SetWindowRgn(hWnd, topRect, true);
        DeleteObject(bottomRound);
    }

    public static void ToggleFullscreen(Window window)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen)
        {
            appWindow.SetPresenter(AppWindowPresenterKind.Default);
            ConfigureAsFloatingPrompter(window);
        }
        else
        {
            // Remove region clipping for fullscreen
            var hWnd = WindowNative.GetWindowHandle(window);
            SetWindowRgn(hWnd, IntPtr.Zero, true);
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
    }

    public static void SetOpacity(Window window, double opacity)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x00080000;

        var style = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
        SetLayeredWindowAttributes(hWnd, 0, (byte)(Math.Clamp(opacity, 0.1, 1.0) * 255), 0x02);
    }

    [LibraryImport("user32.dll")]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(IntPtr hWnd);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [LibraryImport("gdi32.dll")]
    private static partial int CombineRgn(IntPtr hrgnDest, IntPtr hrgnSrc1, IntPtr hrgnSrc2, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);
}
