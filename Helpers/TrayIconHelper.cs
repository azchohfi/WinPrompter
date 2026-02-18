using System.Runtime.InteropServices;
using WinRT.Interop;

namespace WinPrompter.Helpers;

/// <summary>
/// Minimal Win32 system tray icon using Shell_NotifyIcon.
/// Shows a tray icon with a right-click context menu (Show / Exit).
/// </summary>
public sealed partial class TrayIconHelper : IDisposable
{
    private const int WM_APP_TRAYICON = 0x8000 + 1;
    private const int WM_COMMAND = 0x0111;
    private const int NIM_ADD = 0x00;
    private const int NIM_DELETE = 0x02;
    private const int NIM_MODIFY = 0x01;
    private const int NIF_MESSAGE = 0x01;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;
    private const int TPM_RIGHTALIGN = 0x0008;
    private const int TPM_BOTTOMALIGN = 0x0020;
    private const int MF_STRING = 0x0000;
    private const int MF_SEPARATOR = 0x0800;
    private const int IDM_SHOW = 1001;
    private const int IDM_EXIT = 1002;

    private IntPtr _hWnd;
    private IntPtr _hIcon;
    private IntPtr _hMenu;
    private NOTIFYICONDATA _nid;
    private readonly nint _origWndProc;
    private readonly WNDPROC _wndProcDelegate;
    private bool _disposed;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    private delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public TrayIconHelper(Window window)
    {
        _hWnd = WindowNative.GetWindowHandle(window);

        // Load app icon from exe, fall back to default app icon
        _hIcon = ExtractIcon(IntPtr.Zero, Environment.ProcessPath ?? "", 0);
        if (_hIcon == IntPtr.Zero)
            _hIcon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION

        // Build context menu
        _hMenu = CreatePopupMenu();
        AppendMenu(_hMenu, MF_STRING, IDM_SHOW, "Show WinPrompter");
        AppendMenu(_hMenu, MF_SEPARATOR, 0, null);
        AppendMenu(_hMenu, MF_STRING, IDM_EXIT, "Exit");

        // Subclass the window to intercept tray messages
        _wndProcDelegate = TrayWndProc;
        _origWndProc = SetWindowLongPtr(_hWnd, -4 /* GWL_WNDPROC */,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));

        // Add tray icon
        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_APP_TRAYICON,
            hIcon = _hIcon,
            szTip = "WinPrompter"
        };
        Shell_NotifyIcon(NIM_ADD, ref _nid);
    }

    private IntPtr TrayWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == WM_APP_TRAYICON)
            {
                int eventId = (int)(lParam & 0xFFFF);
                if (eventId == WM_LBUTTONDBLCLK)
                {
                    ShowRequested?.Invoke();
                }
                else if (eventId == WM_RBUTTONUP)
                {
                    GetCursorPos(out var pt);
                    SetForegroundWindow(hWnd);
                    TrackPopupMenu(_hMenu, TPM_RIGHTALIGN | TPM_BOTTOMALIGN, pt.X, pt.Y, 0, hWnd, IntPtr.Zero);
                    PostMessage(hWnd, 0 /* WM_NULL */, IntPtr.Zero, IntPtr.Zero);
                }
                return IntPtr.Zero;
            }

            if (msg == WM_COMMAND)
            {
                int id = (int)(wParam & 0xFFFF);
                if (id == IDM_SHOW) ShowRequested?.Invoke();
                else if (id == IDM_EXIT) ExitRequested?.Invoke();
                return IntPtr.Zero;
            }
        }
        catch { /* Don't let exceptions in wndproc crash the app */ }

        return CallWindowProc(_origWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Shell_NotifyIcon(NIM_DELETE, ref _nid);
        if (_hMenu != IntPtr.Zero) DestroyMenu(_hMenu);
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);

        // Restore original wndproc
        if (_origWndProc != IntPtr.Zero)
            SetWindowLongPtr(_hWnd, -4, _origWndProc);

        GC.SuppressFinalize(this);
    }

    // ── Win32 Interop ──

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    private static partial IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [LibraryImport("user32.dll")]
    private static partial IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
    private static extern bool AppendMenu(IntPtr hMenu, int uFlags, int uIDNewItem, string? lpNewItem);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TrackPopupMenu(IntPtr hMenu, int uFlags, int x, int y,
        int nReserved, IntPtr hWnd, IntPtr prcRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(IntPtr hMenu);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr hIcon);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static partial IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
        return new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
}
