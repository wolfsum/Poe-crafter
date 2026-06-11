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
    private DispatcherTimer? _unblockTimer;
    private bool _calibratingCurrency;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        Title = $"PoE2 Crafter v{UpdateService.Current.ToString(3)}";
        DataContext = _vm = vm;
        _hooker.ClickPassed       += OnLeftClickPassed;
        _hooker.PositionCaptured  += OnPositionCaptured;
        _vm.PropertyChanged       += OnVmPropertyChanged;
        _vm.SetCurrencyCommand.Executed += () => _calibratingCurrency = true;
        _vm.SetItemCommand.Executed     += () => _calibratingCurrency = false;
        _vm.UpdateCommand.Executed      += OnUpdateRequested;

        LoadSettings();

        UpdateService.CleanupOldBinary();
        _ = CheckForUpdateAsync(silent: true);
    }

    // ── Updates ───────────────────────────────────────────────────────
    private UpdateService.UpdateInfo? _pendingUpdate;
    private bool _updateBusy;

    private async Task CheckForUpdateAsync(bool silent)
    {
        if (!silent) _vm.UpdateText = "Checking…";

        var (info, error) = await UpdateService.CheckAsync();
        _pendingUpdate = info;

        if (info != null)
        {
            _vm.UpdateText = $"⬆ Get v{info.Version.ToString(3)}";
        }
        else if (!silent)
        {
            if (error != null)
            {
                _vm.UpdateText = "⟳ Check";
                _vm.ShowNotice($"Update check: {error}");
            }
            else
            {
                _vm.UpdateText = "✓ Latest";
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                t.Tick += (_, _) => { t.Stop(); _vm.UpdateText = "⟳ Check"; };
                t.Start();
            }
        }
    }

    private async void OnUpdateRequested()
    {
        if (_updateBusy) return;
        _updateBusy = true;
        try
        {
            if (_pendingUpdate is null)
            {
                await CheckForUpdateAsync(silent: false);
            }
            else
            {
                _vm.UpdateText = "Downloading…";
                await UpdateService.DownloadAndApplyAsync(_pendingUpdate);
            }
        }
        catch (Exception ex)
        {
            _vm.UpdateText = "⟳ Check";
            _vm.ShowNotice($"Update error: {ex.Message}");
        }
        finally { _updateBusy = false; }
    }

    private void LoadSettings()
    {
        var s = SettingsStore.Load();
        if (!double.IsNaN(s.WindowLeft)) Left = s.WindowLeft;
        if (!double.IsNaN(s.WindowTop))  Top  = s.WindowTop;

        _vm.ApplySettings(s);

        _crafter.CurrencyPos = new NativeMethods.POINT { X = s.CurrencyX, Y = s.CurrencyY };
        _crafter.ItemPos     = new NativeMethods.POINT { X = s.ItemX,     Y = s.ItemY };
        _vm.CurrencySet      = s.CurrencySet;
        _vm.ItemSet          = s.ItemSet;
    }

    private void SaveSettings()
    {
        var s = new AppSettings();
        _vm.FillSettings(s);
        s.CurrencyX   = _crafter.CurrencyPos.X;
        s.CurrencyY   = _crafter.CurrencyPos.Y;
        s.ItemX       = _crafter.ItemPos.X;
        s.ItemY       = _crafter.ItemPos.Y;
        s.CurrencySet = _vm.CurrencySet;
        s.ItemSet     = _vm.ItemSet;
        s.WindowLeft  = Left;
        s.WindowTop   = Top;
        SettingsStore.Save(s);
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
            finally
            {
                // Sync blocking state after every clipboard read
                _unblockTimer?.Stop();
                if (!_vm.IsAutoMode && _vm.IsRunning)
                    _hooker.Blocking = _vm.IsStop && _vm.IsBlockingEnabled;
            }
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
                        () => _vm.EvalSeq,
                        reason => Dispatcher.Invoke(() =>
                        {
                            _vm.IsRunning = false;
                            if (reason != null) _vm.ShowNotice(reason);
                        }));
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
        if (_vm.IsBlockingEnabled)
        {
            _hooker.Blocking = true;
            // Safety: a click that produced no clipboard update (ground, stash tab…)
            // must not leave the block stuck
            _unblockTimer?.Stop();
            _unblockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _unblockTimer.Tick += (_, _) =>
            {
                _unblockTimer!.Stop();
                if (!_vm.IsStop) _hooker.Blocking = false;
            };
            _unblockTimer.Start();
        }
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
        SaveSettings();
        NativeMethods.UnregisterHotKey(new WindowInteropHelper(this).Handle, NativeMethods.HOTKEY_TOGGLE);
        _crafter.Stop();
        _hooker.Dispose();
        _watcher.Dispose();
        base.OnClosed(e);
    }
}
