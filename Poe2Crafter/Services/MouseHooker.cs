using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Poe2Crafter.Services;

public sealed class MouseHooker : IDisposable
{
    private const int WH_MOUSE_LL    = 14;
    private const int WM_LBUTTONDOWN = 0x0201;

    private readonly NativeMethods.LowLevelMouseProc _proc; // keep ref → prevent GC

    private IntPtr _hook;
    private IntPtr _ownHwnd;
    private bool   _running;   // hook belongs to a Start/Stop session (not just capture)
    private bool   _capturing;

    // Foreground-window check cache — re-evaluated only when focus changes
    private IntPtr _cachedFg;
    private bool   _cachedInPoe2;

    // Fires on a real (non-injected) left click in the game while a session is
    // running — the reader uses it to copy-evaluate the item after each craft.
    public event Action?                      ClickPassed;
    public event Action<NativeMethods.POINT>? PositionCaptured;

    public MouseHooker() => _proc = HookProc;

    public bool Start(IntPtr ownHwnd)
    {
        _ownHwnd = ownHwnd;
        _running = true;
        return _hook != IntPtr.Zero || Attach();
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
        _running = false;
        Unhook();
    }

    private void Unhook()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
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
                        if (!_running) Unhook(); // keep the hook if a session owns it
                        PositionCaptured?.Invoke(ms.pt);
                        return (IntPtr)1; // swallow calibration click
                    }

                    // Real click in the game during a session → let it through
                    // and notify the reader so it copy-evaluates the result.
                    if (_running && InPoe2(fg) && wParam == (IntPtr)WM_LBUTTONDOWN)
                    {
                        StartupLog.Write($"[MANUAL] click in game at ({ms.pt.X},{ms.pt.Y}) — reading");
                        ClickPassed?.Invoke();
                    }
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // True when fg is a PoE2 window; if PoE2 isn't running at all, any window
    // counts — keeps the tool testable outside the game
    private bool InPoe2(IntPtr fg)
    {
        if (fg != _cachedFg)
        {
            _cachedFg     = fg;
            _cachedInPoe2 = Poe2Process.IsPoe2Window(fg);
        }
        return _cachedInPoe2 || !Poe2Process.AnyRunning();
    }

    public void Dispose() => Stop();
}
