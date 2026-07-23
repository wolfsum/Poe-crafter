using System.Runtime.InteropServices;

namespace Poe2Crafter.Services;

public sealed class AutoCrafter
{
    private static readonly Random _rng = new();
    private CancellationTokenSource? _cts;

    public NativeMethods.POINT CurrencyPos { get; set; }
    public NativeMethods.POINT ItemPos     { get; set; }

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start(Func<bool> shouldStop, Func<string?> getItemHash, Func<int> getEvalSeq, Action<string?> onStopped)
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => RunLoop(shouldStop, getItemHash, getEvalSeq, onStopped, _cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    // ── Main loop ─────────────────────────────────────────────────────
    private async Task RunLoop(Func<bool> shouldStop, Func<string?> getItemHash, Func<int> getEvalSeq, Action<string?> onStopped, CancellationToken ct)
    {
        string? stopReason = null;
        try
        {
            int cycleCount     = 0;
            int sameHashCount  = 0;
            int failedCycles   = 0;
            string? prevHash   = null;

            // Don't touch the mouse until PoE2 is the active window
            while (!ct.IsCancellationRequested && !IsPoE2Active())
                await Delay(300, 0, ct);
            ct.ThrowIfCancellationRequested();

            // Baseline check: read whatever is already on the item BEFORE touching
            // currency. If it already satisfies every target (item was already
            // rolled, or a previous session left it there), stop immediately
            // instead of wasting one orb and only noticing after the fact.
            {
                int seqBefore = getEvalSeq();
                SendCtrlC();
                int waitedMs = 0;
                while (!ct.IsCancellationRequested && getEvalSeq() == seqBefore && waitedMs < 1500)
                {
                    await Task.Delay(50, ct);
                    waitedMs += 50;
                }
                if (shouldStop()) return; // already matches — no currency spent, no click
            }

            // Human flow: Shift is held for the ENTIRE session — releasing it
            // drops the currency-apply mode, which is exactly what forced the
            // old code to trek back to the currency slot between applies.
            // Shift+right-click the currency once, then left-click the item
            // repeatedly; Ctrl+C is sent with Shift still held. Only if the
            // game refuses to copy under Shift do we fall back to releasing it
            // for the copy window (and remember that for later cycles).
            bool shiftFreeCopy = false;
            ShiftDown();
            await PickupCurrencyAsync(ct);

            while (!ct.IsCancellationRequested)
            {
                if (!IsPoE2Active()) { await Delay(300, 0, ct); continue; }

                ShiftDown(); // no-op when already held
                var target = Jitter(ItemPos, 4);
                await MoveSmooth(GetCursor(), target, ct);
                await Delay(Rng(40, 80), 0, ct);
                await ClickAsync(right: false, ct);

                // Wait for PoE2 to process the orb use
                await Delay(Rng(130, 180), 15, ct);

                // Copy item stats and wait until the app actually evaluated the
                // clipboard (PoE2 fires several clipboard events per copy — fixed
                // delays raced and could skip the evaluation)
                int seqBefore = getEvalSeq();
                if (shiftFreeCopy) ShiftUp();
                SendCtrlC();
                bool evaluated = await WaitEval(getEvalSeq, seqBefore, 700, ct);

                if (!evaluated)
                {
                    // A single missed copy is almost always the game dropping the
                    // Ctrl+C keystroke right after a click — resend in the SAME
                    // mode first. (Falling straight to a Shift-free retry here
                    // mis-diagnosed transient losses as "Shift blocks Ctrl+C" and
                    // permanently reverted to the currency-item-currency trek.)
                    SendCtrlC();
                    evaluated = await WaitEval(getEvalSeq, seqBefore, 700, ct);
                }

                if (!evaluated && !shiftFreeCopy)
                {
                    // Two shift-held copies ignored in a row — this build really
                    // does want Shift released for Ctrl+C. Remember that, and
                    // since releasing Shift drops the apply mode, restore it
                    // immediately instead of burning three no-op clicks.
                    ShiftUp();
                    await Delay(60, 20, ct);
                    SendCtrlC();
                    evaluated = await WaitEval(getEvalSeq, seqBefore, 1200, ct);
                    if (evaluated)
                    {
                        shiftFreeCopy = true;
                        ShiftDown();
                        await PickupCurrencyAsync(ct);
                    }
                }
                await Delay(Rng(40, 80), 0, ct); // let UI state settle

                if (shouldStop()) break; // target hit — STOP panel already shown

                if (!evaluated)
                {
                    if (++failedCycles >= 5)
                    {
                        stopReason = "Предмет не считывается — проверь позицию Item";
                        break;
                    }
                    continue;
                }

                // Safety: detect parse failures (empty hash) and item hash unchanged
                var hash = getItemHash();
                failedCycles = (hash == null || hash == "") ? failedCycles + 1 : 0;
                if (failedCycles >= 5)
                {
                    stopReason = "Не читается предмет — проверь позицию Item";
                    break;
                }

                sameHashCount = (hash != null && hash == prevHash && hash != "") ? sameHashCount + 1 : 0;
                prevHash = hash;
                if (sameHashCount >= 5)
                {
                    stopReason = "Предмет не меняется — валюта закончилась?";
                    break;
                }
                if (sameHashCount == 3)
                {
                    // Three identical rolls in a row before concluding the apply-mode
                    // dropped (missed Shift, lag) — re-pickup the stack once and
                    // retry. A genuinely empty slot keeps the hash frozen and we
                    // still stop at the fifth miss above.
                    ShiftDown();
                    await PickupCurrencyAsync(ct);
                }

                cycleCount++;

                // Human-like occasional pause (every 8–18 clicks)
                if (cycleCount % Rng(8, 18) == 0)
                    await Delay(Rng(600, 1500), 150, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            ShiftUp(); // safety: never leave Shift physically held after stop
            onStopped(stopReason);
        }
    }

    // Wait until the app processed a clipboard event newer than seqBefore
    private static async Task<bool> WaitEval(Func<int> getEvalSeq, int seqBefore, int timeoutMs, CancellationToken ct)
    {
        int waited = 0;
        while (!ct.IsCancellationRequested && getEvalSeq() == seqBefore && waited < timeoutMs)
        {
            await Task.Delay(50, ct);
            waited += 50;
        }
        return getEvalSeq() != seqBefore;
    }

    // Right-click the currency slot to put the stack on cursor (apply mode)
    private async Task PickupCurrencyAsync(CancellationToken ct)
    {
        await MoveSmooth(GetCursor(), Jitter(CurrencyPos, 3), ct);
        await Delay(60, 20, ct);
        await ClickAsync(right: true, ct);
        await Delay(200, 50, ct);
    }

    // ── Mouse helpers ─────────────────────────────────────────────────
    private static async Task MoveSmooth(NativeMethods.POINT from, NativeMethods.POINT to, CancellationToken ct)
    {
        float dist = MathF.Sqrt(MathF.Pow(to.X - from.X, 2) + MathF.Pow(to.Y - from.Y, 2));

        // Short hops (in-slot jitter between applies) get a quick micro-move
        // with tiny wobble so the cursor never wanders off the item
        bool micro   = dist < 40;
        int steps    = micro ? Rng(3, 6)   : Rng(14, 22);
        int totalMs  = micro ? Rng(15, 35) : Rng(70, 130);
        int w1       = micro ? 3 : 35;
        int w2       = micro ? 2 : 25;

        // Cubic bezier control points — random slight curve
        float cp1x = from.X + (to.X - from.X) * 0.3f + _rng.Next(-w1, w1);
        float cp1y = from.Y + (to.Y - from.Y) * 0.3f + _rng.Next(-w1, w1);
        float cp2x = from.X + (to.X - from.X) * 0.7f + _rng.Next(-w2, w2);
        float cp2y = from.Y + (to.Y - from.Y) * 0.7f + _rng.Next(-w2, w2);

        for (int i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            float t = (float)i / steps;
            int x = (int)Bezier(from.X, cp1x, cp2x, to.X, t);
            int y = (int)Bezier(from.Y, cp1y, cp2y, to.Y, t);
            NativeMethods.SetCursorPos(x, y);
            await Task.Delay(totalMs / steps, ct);
        }
    }

    // Press and release with a human-like hold — down+up in one batch can be
    // dropped by the game's per-frame input polling
    private static async Task ClickAsync(bool right, CancellationToken ct)
    {
        SendButton(right, down: true);
        await Delay(Rng(35, 75), 0, ct);
        SendButton(right, down: false);
    }

    private static void SendButton(bool right, bool down)
    {
        if (down) LogInput(right ? "R-click (currency pickup)" : "L-click (apply)");
        uint flag = right
            ? (down ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP)
            : (down ? NativeMethods.MOUSEEVENTF_LEFTDOWN  : NativeMethods.MOUSEEVENTF_LEFTUP);
        var inputs = new[] { MouseInput(flag) };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // Millisecond-timestamped trace of every physical input the crafter emits,
    // so a "double click" heard by ear shows up as two lines with a tiny delta —
    // and we can see whether it's two applies or an apply + a currency re-pickup.
    private static long _lastInputTick;
    private static void LogInput(string what)
    {
        long now   = Environment.TickCount64;
        long delta = _lastInputTick == 0 ? 0 : now - _lastInputTick;
        _lastInputTick = now;
        StartupLog.Write($"INPUT {what}  (Δ{delta}ms since last input)");
    }

    private bool _shiftHeld;

    private void ShiftDown() { if (!_shiftHeld) { SendShiftScan(true);  _shiftHeld = true;  } }
    private void ShiftUp()   { if (_shiftHeld) { SendShiftScan(false); _shiftHeld = false; } }

    // Shift as a scan-code event — PoE2 reads raw input and ignores a VK-only
    // modifier held across a mouse click (the click then quick-moves the item)
    private static void SendShiftScan(bool down)
    {
        LogInput(down ? "Shift DOWN" : "Shift UP");
        ushort scan = (ushort)NativeMethods.MapVirtualKey(NativeMethods.VK_SHIFT, NativeMethods.MAPVK_VK_TO_VSC);
        var inputs = new NativeMethods.INPUT[]
        {
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new()
            {
                wVk     = 0,
                wScan   = scan,
                dwFlags = NativeMethods.KEYEVENTF_SCANCODE | (down ? 0 : NativeMethods.KEYEVENTF_KEYUP),
            } } },
        };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static NativeMethods.INPUT MouseInput(uint flags) => new()
    {
        type = NativeMethods.INPUT_MOUSE,
        u    = new() { mi = new() { dwFlags = flags } }
    };

    private static void SendCtrlC()
    {
        LogInput("Ctrl+C (read item)");
        var inputs = new NativeMethods.INPUT[]
        {
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_CONTROL } } },
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_C } } },
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_C,       dwFlags = NativeMethods.KEYEVENTF_KEYUP } } },
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new() { wVk = NativeMethods.VK_CONTROL, dwFlags = NativeMethods.KEYEVENTF_KEYUP } } },
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // ── Utilities ─────────────────────────────────────────────────────
    private static NativeMethods.POINT Jitter(NativeMethods.POINT p, int r) =>
        new() { X = p.X + _rng.Next(-r, r), Y = p.Y + _rng.Next(-r, r) };

    private static NativeMethods.POINT GetCursor()
    {
        NativeMethods.GetCursorPos(out var p);
        return p;
    }

    // Strict: auto-clicking only happens with the real game focused
    private static bool IsPoE2Active() =>
        Poe2Process.IsPoe2Window(NativeMethods.GetForegroundWindow());

    private static float Bezier(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1 - t;
        return u*u*u*p0 + 3*u*u*t*p1 + 3*u*t*t*p2 + t*t*t*p3;
    }

    private static Task Delay(int ms, int spread, CancellationToken ct) =>
        Task.Delay(Math.Max(1, ms + _rng.Next(-spread, spread)), ct);

    private static int Rng(int min, int max) => _rng.Next(min, max);
}
