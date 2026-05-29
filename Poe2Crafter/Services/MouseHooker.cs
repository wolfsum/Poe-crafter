using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Poe2Crafter.Services;

public sealed class MouseHooker : IDisposable
{
    private const int    WH_MOUSE_LL   = 14;
    private const int    WM_LBUTTONDOWN = 0x0201;
    private const int    WM_MOUSEMOVE   = 0x0200;
    private const double BlockRadius    = 200; // pixels to move before auto-unblock

    private readonly NativeMethods.LowLevelMouseProc _proc; // keep ref → prevent GC

    private IntPtr _hook;
    private IntPtr _ownHwnd;
    private IntPtr _poe2Hwnd;

    private bool              _blocking;
    private NativeMethods.POINT _blockOrigin;
    private bool              _capturing;

    public event Action?                        ClickPassed;
    public event Action<NativeMethods.POINT>?   PositionCaptured;

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
        return Attach();
    }

    public void StartCapture(IntPtr ownHwnd)
    {
        _ownHwnd   = ownHwnd;
        _capturing = true;
        if (_hook == IntPtr.Zero) Attach();
    }

    private bool Attach()
    {
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
            var ms = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            // Skip injected (synthetic) clicks — those are from AutoCrafter
            bool injected = (ms.flags & NativeMethods.LLMHF_INJECTED) != 0;
            if (!injected)
            {
                var fg = NativeMethods.GetForegroundWindow();
                if (fg != _ownHwnd)
                {
                    // Calibration mode — capture next click position
                    if (_capturing && wParam == (IntPtr)WM_LBUTTONDOWN)
                    {
                        _capturing = false;
                        if (!_blocking) Stop(); // unhook if not running
                        PositionCaptured?.Invoke(ms.pt);
                        return (IntPtr)1; // swallow calibration click
                    }

                    bool inPoE2 = _poe2Hwnd == IntPtr.Zero || fg == _poe2Hwnd;
                    if (inPoE2)
                    {
                        if (wParam == (IntPtr)WM_MOUSEMOVE && _blocking)
                        {
                            var dx = ms.pt.X - _blockOrigin.X;
                            var dy = ms.pt.Y - _blockOrigin.Y;
                            if (dx * dx + dy * dy > BlockRadius * BlockRadius)
                                _blocking = false;
                        }
                        else if (wParam == (IntPtr)WM_LBUTTONDOWN)
                        {
                            if (_blocking) return (IntPtr)1;
                            ClickPassed?.Invoke();
                        }
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
