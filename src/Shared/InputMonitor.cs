using System.Runtime.InteropServices;

namespace ThimbleBerry.Screensavers;

public enum RealInputKind { Key, MouseButton, MouseWheel, MouseMove }

/// <summary>
/// Watches system-wide keyboard and mouse input through low-level hooks and reports
/// only input that came from a person. Keep-awake tools such as Caffeine simulate key
/// presses (usually F15) to reset the Windows idle timer; those events carry the
/// "injected" flag, and we drop them. Must be created on a thread that pumps messages
/// (the WinForms UI thread does).
/// </summary>
public sealed class InputMonitor : IDisposable
{
    private readonly Native.LowLevelProc _keyboardProc;
    private readonly Native.LowLevelProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;

    /// <summary>Raised for real (non-synthetic) input. Point is the cursor position for mouse events.</summary>
    public event Action<RealInputKind, Point>? RealInput;

    /// <summary>Environment.TickCount64 of the most recent real input.</summary>
    public long LastRealInputTick { get; private set; } = Environment.TickCount64;

    public bool IsActive => _keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero;

    /// <summary>Settings that decide what counts as synthetic input. Can be swapped when the file changes.</summary>
    public SaverSettings Settings { get; set; }

    /// <summary>
    /// While the screensaver is showing, swallow real key presses and clicks so the key that
    /// wakes the screen doesn't also type into whatever app is underneath. Synthetic input
    /// is always passed through so keep-awake tools keep working.
    /// </summary>
    public bool SwallowRealInput { get; set; }

    public InputMonitor(SaverSettings settings)
    {
        Settings = settings;
        // Keep delegates in fields so the GC can't collect them while hooked.
        _keyboardProc = KeyboardHook;
        _mouseProc = MouseHook;
        var module = Native.GetModuleHandle(null);
        _keyboardHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _keyboardProc, module, 0);
        _mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _mouseProc, module, 0);
    }

    private IntPtr KeyboardHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            bool injected = (data.flags & (Native.LLKHF_INJECTED | Native.LLKHF_LOWER_IL_INJECTED)) != 0;
            bool phantomKey = data.vkCode is >= 0x7C and <= 0x87; // F13..F24
            bool real = !(injected && Settings.IgnoreInjectedInput) && !(phantomKey && Settings.IgnoreF13ToF24);
            if (real)
            {
                if (msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN)
                    Report(RealInputKind.Key, Point.Empty);
                if (SwallowRealInput) return 1;
            }
        }
        return Native.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
            bool injected = (data.flags & (Native.LLMHF_INJECTED | Native.LLMHF_LOWER_IL_INJECTED)) != 0;
            if (!(injected && Settings.IgnoreInjectedInput))
            {
                var pt = new Point(data.pt.X, data.pt.Y);
                int msg = (int)wParam;
                switch (msg)
                {
                    case Native.WM_MOUSEMOVE:
                        Report(RealInputKind.MouseMove, pt);
                        break;
                    case Native.WM_LBUTTONDOWN or Native.WM_RBUTTONDOWN or Native.WM_MBUTTONDOWN or Native.WM_XBUTTONDOWN:
                        Report(RealInputKind.MouseButton, pt);
                        break;
                    case Native.WM_MOUSEWHEEL or Native.WM_MOUSEHWHEEL:
                        Report(RealInputKind.MouseWheel, pt);
                        break;
                }
                // Swallow clicks and wheel (not movement) while the saver is up.
                if (SwallowRealInput && msg != Native.WM_MOUSEMOVE) return 1;
            }
        }
        return Native.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void Report(RealInputKind kind, Point pt)
    {
        LastRealInputTick = Environment.TickCount64;
        // Hook callbacks must return quickly or Windows silently removes the hook.
        try { RealInput?.Invoke(kind, pt); } catch { /* never let a handler break the hook */ }
    }

    public void Dispose()
    {
        if (_keyboardHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
        if (_mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
    }
}
