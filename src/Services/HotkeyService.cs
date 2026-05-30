using System.Runtime.InteropServices;

namespace QuickLink.Services;

public sealed class HotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProc pfnSubclass, nuint uIdSubclass, nuint dwRefData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProc pfnSubclass, nuint uIdSubclass);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr SubclassProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData);

    private const uint WM_HOTKEY   = 0x0312;
    private const uint MOD_ALT     = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT   = 0x0004;
    private const uint MOD_WIN     = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int  HOTKEY_ID   = 0x4242;

    public event Action? HotkeyPressed;

    /// <summary>Non-null when the last TryRegister call failed.</summary>
    public string? LastRegisterError { get; private set; }

    private SubclassProc? _subclassProc;
    private IntPtr _hwnd;
    private bool _subclassed;
    private bool _registered;

    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _subclassProc = WndProcSubclass;
        _subclassed = SetWindowSubclass(hwnd, _subclassProc, 1, 0);
    }

    /// <summary>Returns null on success, or an error message the caller can display.</summary>
    public string? TryRegister(string hotkeyString)
    {
        LastRegisterError = null;
        Unregister();

        if (!TryParse(hotkeyString, out var mods, out var vk))
        {
            LastRegisterError = "Invalid hotkey format.";
            return LastRegisterError;
        }

        bool ok = RegisterHotKey(_hwnd, HOTKEY_ID, mods | MOD_NOREPEAT, vk);
        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            LastRegisterError = err == 1409
                ? "This hotkey is already in use by another application."
                : $"Could not register hotkey (error {err}).";
            return LastRegisterError;
        }

        _registered = true;
        return null;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
            _registered = false;
        }
    }

    private IntPtr WndProcSubclass(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam, nuint uIdSubclass, nuint dwRefData)
    {
        if (uMsg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            HotkeyPressed?.Invoke();
        return DefSubclassProc(hwnd, uMsg, wParam, lParam);
    }

    public static bool TryParse(string hotkeyString, out uint modifiers, out uint virtualKey)
    {
        modifiers  = 0;
        virtualKey = 0;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        string keyPart  = parts[^1];
        string[] modParts = parts[..^1];

        foreach (var mod in modParts)
        {
            switch (mod.ToLowerInvariant())
            {
                case "ctrl":
                case "control":  modifiers |= MOD_CONTROL; break;
                case "alt":      modifiers |= MOD_ALT;     break;
                case "shift":    modifiers |= MOD_SHIFT;   break;
                case "win":
                case "windows":  modifiers |= MOD_WIN;     break;
                default: return false;
            }
        }

        virtualKey = keyPart.ToLowerInvariant() switch
        {
            "space"    => 0x20,
            "tab"      => 0x09,
            "enter"    => 0x0D,
            "insert"   => 0x2D,
            "delete"   => 0x2E,
            "home"     => 0x24,
            "end"      => 0x23,
            "pageup"   => 0x21,
            "pagedown" => 0x22,
            "left"     => 0x25,
            "up"       => 0x26,
            "right"    => 0x27,
            "down"     => 0x28,
            "f1"  => 0x70, "f2"  => 0x71, "f3"  => 0x72,  "f4"  => 0x73,
            "f5"  => 0x74, "f6"  => 0x75, "f7"  => 0x76,  "f8"  => 0x77,
            "f9"  => 0x78, "f10" => 0x79, "f11" => 0x7A,  "f12" => 0x7B,
            "0" => 0x30, "1" => 0x31, "2" => 0x32, "3" => 0x33, "4" => 0x34,
            "5" => 0x35, "6" => 0x36, "7" => 0x37, "8" => 0x38, "9" => 0x39,
            var s when s.Length == 1 && s[0] >= 'a' && s[0] <= 'z' => (uint)(s[0] - 'a' + 'A'),
            _ => 0
        };

        return virtualKey != 0;
    }

    public void Dispose()
    {
        Unregister();
        if (_subclassed && _hwnd != IntPtr.Zero && _subclassProc != null)
        {
            RemoveWindowSubclass(_hwnd, _subclassProc, 1);
            _subclassed = false;
        }
    }
}
