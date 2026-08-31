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

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        Title = $"{vm.GameName} Crafter v{UpdateService.Current.ToString(3)}";
        DataContext = _vm = vm;
        _hooker.ClickPassed       += OnLeftClickPassed;
        _hooker.PositionCaptured  += OnPositionCaptured;
        _vm.PropertyChanged       += OnVmPropertyChanged;
        _vm.UpdateCommand.Executed      += OnUpdateRequested;
        _vm.SwitchGameCommand.Executed  += OnSwitchGame;
        _vm.StopRequested               += OnStopRequested;

        LoadSettings();

        // Full lifecycle trace: tells us whether the window actually paints
        // (ContentRendered), where it lands on screen, and exactly how it goes
        // away — a logged Closed/Closing means we shut ourselves down; silence
        // after "window shown" means something killed the process from outside.
        Loaded          += (_, _) => StartupLog.Write("Window.Loaded");
        ContentRendered += (_, _) => StartupLog.Write(
            $"Window.ContentRendered - Left={Left} Top={Top} W={ActualWidth} H={ActualHeight} " +
            $"State={WindowState} Visible={IsVisible} " +
            $"onScreen={IsOnScreen()} vScreen=({SystemParameters.VirtualScreenLeft},{SystemParameters.VirtualScreenTop} " +
            $"{SystemParameters.VirtualScreenWidth}x{SystemParameters.VirtualScreenHeight})");
        Activated       += (_, _) => StartupLog.Write("Window.Activated");
        Deactivated     += (_, _) => StartupLog.Write("Window.Deactivated");
        StateChanged    += (_, _) => StartupLog.Write($"Window.StateChanged -> {WindowState}");
        Closing         += (_, ev) => StartupLog.Write($"Window.Closing (cancel={ev.Cancel})");
        Closed          += (_, _) => StartupLog.Write("Window.Closed");

        UpdateService.CleanupOldBinary();
        _ = CheckForUpdateAsync(silent: true);
    }

    // Does the window rect intersect any part of the virtual desktop? If not,
    // it opened off-screen (e.g. saved position from a monitor that's gone).
    private bool IsOnScreen()
    {
        var l = SystemParameters.VirtualScreenLeft;
        var t = SystemParameters.VirtualScreenTop;
        var r = l + SystemParameters.VirtualScreenWidth;
        var b = t + SystemParameters.VirtualScreenHeight;
        return Left < r && Left + Math.Max(ActualWidth, 100) > l &&
               Top  < b && Top  + Math.Max(ActualHeight, 100) > t;
    }

    // ── Game switch ───────────────────────────────────────────────────
    private string? _gameOverride; // set while switching so late saves keep the new game

    private void OnSwitchGame()
    {
        _gameOverride = _vm.GameKey == "poe2" ? "poe1" : "poe2";
        SaveSettings(); // persist BEFORE the new process starts reading settings
        AppControl.Restart();
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

    // Spend limit reached — stop the whole session (crafter + hook) the same way
    // the Stop button does, then say why.
    private void OnStopRequested(string reason)
    {
        _vm.IsRunning = false;
        _vm.ShowNotice(reason);
    }

    private void LoadSettings()
    {
        var s = SettingsStore.Load();
        if (!double.IsNaN(s.WindowLeft)) Left = s.WindowLeft;
        if (!double.IsNaN(s.WindowTop))  Top  = s.WindowTop;

        _vm.ApplySettings(s);

        _crafter.CurrencyPos = new NativeMethods.POINT { X = s.CurrencyX, Y = s.CurrencyY };
        _vm.CurrencySet      = s.CurrencySet;
        _crafter.AugCurrencyPos = new NativeMethods.POINT { X = s.AugCurrencyX, Y = s.AugCurrencyY };
        _vm.AugCurrencySet      = s.AugCurrencySet;

        // Restore the item queue. Migrate a legacy single-item position into a
        // one-slot queue when no slot list was saved.
        var slots = s.ItemSlots;
        if (slots.Count == 0 && s.ItemSet)
            slots = [new ItemSlotSetting { X = s.ItemX, Y = s.ItemY, IsSet = true }];

        _vm.ItemCount = Math.Max(MainViewModel.MinItems, slots.Count);
        for (int i = 0; i < _vm.ItemSlots.Count && i < slots.Count; i++)
        {
            _vm.ItemSlots[i].Pos   = new NativeMethods.POINT { X = slots[i].X, Y = slots[i].Y };
            _vm.ItemSlots[i].IsSet = slots[i].IsSet;
        }
    }

    private void SaveSettings()
    {
        var s = new AppSettings();
        _vm.FillSettings(s);
        s.GameVersion = _gameOverride ?? _vm.GameKey;
        s.CurrencyX   = _crafter.CurrencyPos.X;
        s.CurrencyY   = _crafter.CurrencyPos.Y;
        s.CurrencySet = _vm.CurrencySet;
        s.AugCurrencyX   = _crafter.AugCurrencyPos.X;
        s.AugCurrencyY   = _crafter.AugCurrencyPos.Y;
        s.AugCurrencySet = _vm.AugCurrencySet;
        s.ItemSlots   = _vm.ItemSlots
            .Select(sl => new ItemSlotSetting { X = sl.Pos.X, Y = sl.Pos.Y, IsSet = sl.IsSet })
            .ToList();
        s.WindowLeft  = Left;
        s.WindowTop   = Top;
        SettingsStore.Save(s);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        StartupLog.Write("OnSourceInitialized: attaching clipboard watcher...");
        base.OnSourceInitialized(e);
        _watcher.Attach(this);
        _watcher.Changed += OnClipboardChanged;

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        source.AddHook(WndProc);
        var hotkeyOk = NativeMethods.RegisterHotKey(source.Handle, NativeMethods.HOTKEY_TOGGLE, 0, NativeMethods.VK_F10);
        StartupLog.Write($"OnSourceInitialized done. F10 hotkey registered={hotkeyOk}");
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
        // 100 ms coalesces PoE's burst of clipboard events per copy. Cutting this
        // shorter reads the clipboard before the last event lands — AutoCrafter
        // then sees the PREVIOUS roll's text again, misreads it as "unchanged",
        // and repicks currency thinking it ran out.
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
                if (!_vm.IsAutoMode && _vm.IsRunning)
                    StartupLog.Write($"[MANUAL] item evaluated -> IsStop={_vm.IsStop} hash={Short(_vm.LastItemHash)}");
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
                    if (!_vm.CurrencySet || !_vm.AllItemsSet)
                    {
                        _vm.IsRunning = false;
                        return;
                    }
                    if (_vm.IsAltAugMode && !_vm.AugCurrencySet)
                    {
                        _vm.IsRunning = false;
                        _vm.ShowNotice("Alt+Aug: задай позицию Aug (Set Aug)");
                        return;
                    }
                    if (_vm.IsAltAugMode && _vm.TargetMods.Count == 0)
                    {
                        _vm.IsRunning = false;
                        _vm.ShowNotice("Alt+Aug: добавь целевой мод (Prefix или Suffix)");
                        return;
                    }
                    _crafter.ItemPositions = _vm.ItemSlots.Select(sl => sl.Pos).ToList();
                    _crafter.AltAugMode   = _vm.IsAltAugMode;
                    int total = _vm.ItemSlots.Count;
                    _crafter.Start(
                        () => _vm.IsStop,
                        () => _vm.LastItemHash,
                        () => _vm.EvalSeq,
                        () => _vm.LastCraftAction,
                        idx => Dispatcher.Invoke(() =>
                            _vm.AutoProgress = total > 1 ? $"Крафчу {idx + 1} / {total}" : ""),
                        reason => Dispatcher.Invoke(() =>
                        {
                            _vm.IsRunning = false;
                            if (reason != null) _vm.ShowNotice(reason);
                        }),
                        () => _vm.RegisterApply());
                }
            }
            else
            {
                _crafter.Stop();
                _hooker.Stop();
            }
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
            if (_vm.CapturingCurrency)
            {
                _crafter.CurrencyPos = pt;
                _vm.CurrencySet      = true;
            }
            else if (_vm.CapturingAug)
            {
                _crafter.AugCurrencyPos = pt;
                _vm.AugCurrencySet      = true;
            }
            else if (_vm.CapturingSlot is { } slot)
            {
                slot.Pos   = pt;
                slot.IsSet = true;
            }
            _vm.IsCapturing = false;
        });
    }

    private int _clickGen; // bumps per click so a newer click supersedes an older read loop

    // Manual mode: after each real click in the game, copy-evaluate the item so
    // the status panel updates and the beep fires the instant a target is hit.
    // Ctrl+C is re-sent until an evaluation lands (the game sometimes drops the
    // keystroke). No mouse blocking — a low-level hook can't stop PoE's raw
    // input anyway; the beep is the alert.
    private async void OnLeftClickPassed()
    {
        // Manual mode: a click that reached the game is an orb use — the tally
        // confirms it only if the follow-up read returns a readable item.
        _vm.RegisterApply();

        int gen = ++_clickGen;
        int seqBefore = _vm.EvalSeq;
        await Task.Delay(CtrlCDelayMs);

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            if (gen != _clickGen) return; // a newer click took over

            await SendCtrlCAsync();

            for (int w = 0; w < 12 && _vm.EvalSeq == seqBefore; w++)
                await Task.Delay(40);

            if (gen != _clickGen) return;
            if (_vm.EvalSeq != seqBefore) return; // read landed — VM applied the verdict
        }
    }

    // First 8 chars of a hash for compact logs
    private static string Short(string? h) => string.IsNullOrEmpty(h) ? "<none>" : h[..Math.Min(8, h.Length)];

    // Copy the hovered item with Ctrl+C. Do NOT touch the user's held Shift:
    // AutoCrafter copies fine with Shift held the whole time, so Shift+Ctrl+C
    // copies correctly — releasing it here only dropped the currency-apply hold.
    // The one thing that matters is holding C for a beat instead of a 0ms batch,
    // which the game's per-frame input polling would miss. Reliability comes
    // from the caller RE-SENDING this until an evaluation actually lands.
    private static async Task SendCtrlCAsync()
    {
        SendKey(NativeMethods.VK_CONTROL, down: true);
        await Task.Delay(20);
        SendKey(NativeMethods.VK_C, down: true);
        await Task.Delay(50);
        SendKey(NativeMethods.VK_C, down: false);
        await Task.Delay(20);
        SendKey(NativeMethods.VK_CONTROL, down: false);
    }

    private static void SendKey(int vk, bool down)
    {
        var inputs = new NativeMethods.INPUT[]
        {
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new()
            {
                wVk = (ushort)vk,
                dwFlags = down ? 0 : NativeMethods.KEYEVENTF_KEYUP,
            } } },
        };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
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
