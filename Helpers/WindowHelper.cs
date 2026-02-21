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
            presenter.IsAlwaysOnTop = true;
        }

        // Strip all non-client chrome via Win32 styles to remove the gray bar
        var hWnd = WindowNative.GetWindowHandle(window);
        const int GWL_STYLE = -16;
        const nint WS_CAPTION = 0x00C00000;
        const nint WS_THICKFRAME = 0x00040000;
        const nint WS_SYSMENU = 0x00080000;
        var style = GetWindowLongPtr(hWnd, GWL_STYLE);
        style &= ~WS_CAPTION;   // Remove title bar
        style |= WS_THICKFRAME; // Keep resize grip
        style &= ~WS_SYSMENU;   // Remove system menu
        SetWindowLongPtr(hWnd, GWL_STYLE, style);

        // Force redraw with new styles
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

        PositionTopCenter(appWindow, width, height);

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
            appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        }
    }

    public static void SetOpacity(Window window, double opacity)
    {
        var hWnd = WindowNative.GetWindowHandle(window);
        const int GWL_EXSTYLE = -20;
        const nint WS_EX_LAYERED = 0x00080000;

        var style = GetWindowLongPtr(hWnd, GWL_EXSTYLE);
        SetWindowLongPtr(hWnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
        SetLayeredWindowAttributes(hWnd, 0, (byte)(Math.Clamp(opacity, 0.1, 1.0) * 255), 0x02);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);
}
