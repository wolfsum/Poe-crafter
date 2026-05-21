using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Poe2Crafter.Services;
using Poe2Crafter.ViewModels;

namespace Poe2Crafter;

public partial class MainWindow : Window
{
    private const int CtrlCDelayMs = 350; // wait for PoE2 to process the currency use

    private readonly MainViewModel _vm;
    private readonly ClipboardWatcher _watcher = new();
    private readonly MouseHooker _hooker = new();
    private DispatcherTimer? _clipTimer;
    private DispatcherTimer? _ctrlCTimer;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _hooker.ClickPassed += OnLeftClickPassed;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _watcher.Attach(this);
        _watcher.Changed += OnClipboardChanged;
    }

    // ── Clipboard ─────────────────────────────────────────────────────
    private void OnClipboardChanged()
    {
        _clipTimer?.Stop();
        _clipTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _clipTimer.Tick += (_, _) =>
        {
            _clipTimer!.Stop();
            try
            {
                var text = Clipboard.ContainsText() ? Clipboard.GetText() : "";
                _vm.OnClipboardChanged(text);
            }
            catch (COMException) { }
        };
        _clipTimer.Start();
    }

    // ── Mouse hook ────────────────────────────────────────────────────
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRunning))
        {
            if (_vm.IsRunning)
                _hooker.Start(new WindowInteropHelper(this).Handle);
            else
                _hooker.Stop();
        }
        else if (e.PropertyName == nameof(MainViewModel.IsStop))
        {
            // Block next click when STOP; unblock when GO
            if (_vm.IsRunning)
                _hooker.Blocking = _vm.IsStop;
        }
    }

    // Called when a left click passes through the hook — auto-send Ctrl+C after delay
    private void OnLeftClickPassed()
    {
        _ctrlCTimer?.Stop();
        _ctrlCTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CtrlCDelayMs) };
        _ctrlCTimer.Tick += (_, _) =>
        {
            _ctrlCTimer!.Stop();
            SendCtrlC();
        };
        _ctrlCTimer.Start();
    }

    private static void SendCtrlC()
    {
        var inputs = new NativeMethods.INPUT[]
        {
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_CONTROL } } },
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_C } } },
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_C,       dwFlags = NativeMethods.KEYEVENTF_KEYUP } } },
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_CONTROL, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } },
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    protected override void OnClosed(EventArgs e)
    {
        _hooker.Dispose();
        _watcher.Dispose();
        base.OnClosed(e);
    }
}
