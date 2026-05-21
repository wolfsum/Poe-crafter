using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Poe2Crafter.Services;

public sealed class MouseHooker : IDisposable
{
    private const int    WH_MOUSE_LL   = 14;
    private const int    WM_LBUTTONDOWN = 0x0201;
    private const int    WM_MOUSEMOVE   = 0x0200;
    private const double BlockRadius    = 80; // pixels to move before auto-unblock

    private readonly NativeMethods.LowLevelMouseProc _proc; // keep ref → prevent GC

    private IntPtr _hook;
    private IntPtr _ownHwnd;
    private IntPtr _poe2Hwnd;

    private bool              _blocking;
    private NativeMethods.POINT _blockOrigin;

    public event Action? ClickPassed; // fires when a left click goes through (use for auto Ctrl+C)

    public bool Blocking
    {
        get => _blocking;
        set
        {
            _blocking = value;
            if (value) NativeMethods.GetCursorPos(out _blockOrigin);
        }
    }

    public MouseHooker() => _proc = HookProc;

    public bool Start(IntPtr ownHwnd)
    {
        if (_hook != IntPtr.Zero) return true;
        _ownHwnd  = ownHwnd;
        _poe2Hwnd = FindPoE2Window();
        using var mod = Process.GetCurrentProcess().MainModule!;
        _hook = NativeMethods.SetWindowsHookEx(
            WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(mod.ModuleName!), 0);
        return _hook != IntPtr.Zero;
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook     = IntPtr.Zero;
        _blocking = false;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var fg = NativeMethods.GetForegroundWindow();

            // Never interfere with our own UI
            if (fg != _ownHwnd)
            {
                // Only act when PoE2 is active (or if we couldn't find PoE2 window)
                bool inPoE2 = _poe2Hwnd == IntPtr.Zero || fg == _poe2Hwnd;

                if (inPoE2)
                {
                    if (wParam == (IntPtr)WM_MOUSEMOVE && _blocking)
                    {
                        var ms = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                        var dx = ms.pt.X - _blockOrigin.X;
                        var dy = ms.pt.Y - _blockOrigin.Y;
                        if (dx * dx + dy * dy > BlockRadius * BlockRadius)
                            _blocking = false;
                    }
                    else if (wParam == (IntPtr)WM_LBUTTONDOWN)
                    {
                        if (_blocking) return (IntPtr)1; // swallow click
                        ClickPassed?.Invoke();
                    }
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static IntPtr FindPoE2Window()
    {
        foreach (var name in new[] { "PathOfExile", "PathOfExile_x64", "PathOfExile2" })
            foreach (var p in Process.GetProcessesByName(name))
                if (p.MainWindowHandle != IntPtr.Zero)
                    return p.MainWindowHandle;
        return IntPtr.Zero;
    }

    public void Dispose() => Stop();
}
