using System.Windows;
using System.Windows.Interop;

namespace Poe2Crafter.Services;

public sealed class ClipboardWatcher : IDisposable
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    private HwndSource? _source;
    public event Action? Changed;

    public bool Attach(Window window)
    {
        _source = (HwndSource?)PresentationSource.FromVisual(window);
        if (_source is null) return false;

        _source.AddHook(WndProc);
        bool ok = NativeMethods.AddClipboardFormatListener(_source.Handle);
        return ok;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
            Changed?.Invoke();
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null) return;
        NativeMethods.RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(WndProc);
    }
}
