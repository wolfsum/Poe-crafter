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
    private const int CtrlCDelayMs = 150; // wait for PoE2 to process the currency use

    private readonly MainViewModel _vm;
    private readonly ClipboardWatcher _watcher = new();
    private readonly MouseHooker      _hooker  = new();
    private readonly AutoCrafter      _crafter = new();
    private DispatcherTimer? _clipTimer;
    private bool _calibratingCurrency;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _hooker.ClickPassed       += OnLeftClickPassed;
        _hooker.PositionCaptured  += OnPositionCaptured;
        _vm.PropertyChanged       += OnVmPropertyChanged;
        _vm.SetCurrencyCommand.Executed += () => _calibratingCurrency = true;
        _vm.SetItemCommand.Executed     += () => _calibratingCurrency = false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _watcher.Attach(this);
        _watcher.Changed += OnClipboardChanged;

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        source.AddHook(WndProc);
        NativeMethods.RegisterHotKey(source.Handle, NativeMethods.HOTKEY_TOGGLE, 0, NativeMethods.VK_F10);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == NativeMethods.HOTKEY_TOGGLE)
        {
            _vm.ToggleRunningCommand.Execute(null);
            handled = true;
        }
        return IntPtr.Zero;
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

    // ── Mouse hook / auto-craft ───────────────────────────────────────
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (e.PropertyName == nameof(MainViewModel.IsRunning))
        {
            if (_vm.IsRunning)
            {
                _hooker.Start(hwnd);
                if (_vm.IsAutoMode)
                {
                    if (!_vm.CurrencySet || !_vm.ItemSet)
                    {
                        _vm.IsRunning = false;
                        return;
                    }
                    _crafter.Start(
                        () => _vm.IsStop,
                        () => _vm.LastItemHash,
                        () => Dispatcher.Invoke(() => _vm.IsRunning = false));
                }
            }
            else
            {
                _crafter.Stop();
                _hooker.Stop();
            }
        }
        else if (e.PropertyName == nameof(MainViewModel.IsStop))
        {
            if (_vm.IsRunning && !_vm.IsAutoMode)
                _hooker.Blocking = _vm.IsStop && _vm.IsBlockingEnabled;
        }
        else if (e.PropertyName == nameof(MainViewModel.IsCapturing) && _vm.IsCapturing)
        {
            _hooker.StartCapture(hwnd);
        }
    }

    private void OnPositionCaptured(NativeMethods.POINT pt)
    {
        Dispatcher.Invoke(() =>
        {
            if (_calibratingCurrency)
            {
                _crafter.CurrencyPos      = pt;
                _vm.CurrencySet           = true;
                _calibratingCurrency      = false;
            }
            else
            {
                _crafter.ItemPos = pt;
                _vm.ItemSet      = true;
            }
            _vm.IsCapturing = false;
        });
    }

    // Called when a left click passes through the hook — auto-send Ctrl+C after delay
    private void OnLeftClickPassed()
    {
        if (_vm.IsBlockingEnabled) _hooker.Blocking = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(CtrlCDelayMs) };
        timer.Tick += (_, _) => { timer.Stop(); SendCtrlC(); };
        timer.Start();
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
        NativeMethods.UnregisterHotKey(new WindowInteropHelper(this).Handle, NativeMethods.HOTKEY_TOGGLE);
        _hooker.Dispose();
        _watcher.Dispose();
        base.OnClosed(e);
    }
}
