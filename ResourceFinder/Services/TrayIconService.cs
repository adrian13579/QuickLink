using System.Runtime.InteropServices;

namespace ResourceFinder.Services;

public sealed class TrayIconService : IDisposable
{
    // ── Shell / tray ────────────────────────────────────────────────────────
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType,
        int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    // ── Context menu ────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    // ── Window subclassing (comctl32) ───────────────────────────────────────
    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);

    // ── Types ───────────────────────────────────────────────────────────────
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint   cbSize;
        public IntPtr hWnd;
        public uint   uID;
        public uint   uFlags;
        public uint   uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint   dwState;
        public uint   dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint   uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint   dwInfoFlags;
        public Guid   guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    // ── Constants ───────────────────────────────────────────────────────────
    private const uint NIM_ADD        = 0;
    private const uint NIM_DELETE     = 2;
    private const uint NIM_SETVERSION = 4;

    private const uint NIF_MESSAGE = 0x0001;
    private const uint NIF_ICON    = 0x0002;
    private const uint NIF_TIP     = 0x0004;
    private const uint NIF_SHOWTIP = 0x0080;

    private const uint NOTIFYICON_VERSION_4 = 4;

    private const uint IMAGE_ICON      = 1;
    private const uint LR_LOADFROMFILE = 0x0010;

    private const uint MF_STRING    = 0x0000;
    private const uint MF_SEPARATOR = 0x0800;

    private const uint TPM_RETURNCMD = 0x0100;

    // Custom callback message sent by the tray icon (WM_APP + 1)
    private const uint WM_TRAYICON = 0x8001;

    // Notification codes in LOWORD(lParam) for NOTIFYICON_VERSION_4
    private const uint WM_LBUTTONUP     = 0x0202;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP     = 0x0205;

    private const int    MENU_SHOW   = 100;
    private const int    MENU_EXIT   = 101;
    private const nuint  SUBCLASS_ID = 2; // HotkeyService uses 1

    // ── State ───────────────────────────────────────────────────────────────
    public event Action? ShowRequested;
    public event Action? ExitRequested;

    private IntPtr        _hwnd;
    private IntPtr        _hIcon;
    private SubclassProc? _subclassProc;
    private bool          _subclassed;
    private bool          _added;
    private uint          _taskbarRestartMsg;

    // ── Public API ──────────────────────────────────────────────────────────
    public void Initialize(IntPtr hwnd)
    {
        _hwnd              = hwnd;
        _taskbarRestartMsg = RegisterWindowMessage("TaskbarCreated");

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _hIcon = File.Exists(iconPath)
            ? LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE)
            : IntPtr.Zero;

        AddTrayIcon();

        _subclassProc = WndProcSubclass;
        _subclassed   = SetWindowSubclass(hwnd, _subclassProc, SUBCLASS_ID, 0);
    }

    public void Dispose()
    {
        if (_added && _hwnd != IntPtr.Zero)
        {
            var nid = MakeNid(0);
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _added = false;
        }

        if (_subclassed && _hwnd != IntPtr.Zero && _subclassProc != null)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, SUBCLASS_ID);
            _subclassed = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }

    // ── Internal ────────────────────────────────────────────────────────────
    private void AddTrayIcon()
    {
        var nid = MakeNid(NIF_ICON | NIF_MESSAGE | NIF_TIP | NIF_SHOWTIP);
        nid.uCallbackMessage = WM_TRAYICON;
        nid.hIcon            = _hIcon;
        nid.szTip            = "ResourceFinder";
        Shell_NotifyIcon(NIM_ADD, ref nid);

        var ver = MakeNid(0);
        ver.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref ver);

        _added = true;
    }

    private NOTIFYICONDATA MakeNid(uint flags)
    {
        var nid = new NOTIFYICONDATA
        {
            cbSize      = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd        = _hwnd,
            uID         = 1,
            uFlags      = flags,
            szTip       = string.Empty,
            szInfo      = string.Empty,
            szInfoTitle = string.Empty,
        };
        return nid;
    }

    private IntPtr WndProcSubclass(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == WM_TRAYICON)
        {
            var notification = (uint)(lParam.ToInt64() & 0xFFFF);
            switch (notification)
            {
                case WM_LBUTTONUP:
                case WM_LBUTTONDBLCLK:
                    ShowRequested?.Invoke();
                    break;
                case WM_RBUTTONUP:
                    ShowContextMenu();
                    break;
            }
            return IntPtr.Zero;
        }

        // Re-add tray icon if Explorer restarts and recreates the taskbar
        if (_taskbarRestartMsg != 0 && uMsg == _taskbarRestartMsg)
            AddTrayIcon();

        return DefSubclassProc(hwnd, uMsg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING,    (nuint)MENU_SHOW, "Show ResourceFinder");
        AppendMenu(menu, MF_SEPARATOR, 0,                null);
        AppendMenu(menu, MF_STRING,    (nuint)MENU_EXIT, "Exit");

        GetCursorPos(out var pt);
        SetForegroundWindow(_hwnd); // required so menu dismisses on click-away
        int cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD, pt.X, pt.Y, _hwnd, IntPtr.Zero);
        DestroyMenu(menu);

        if (cmd == MENU_SHOW) ShowRequested?.Invoke();
        else if (cmd == MENU_EXIT) ExitRequested?.Invoke();
    }
}
