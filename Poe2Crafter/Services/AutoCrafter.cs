using System.Runtime.InteropServices;
using Poe2Crafter.Core.Matching;

namespace Poe2Crafter.Services;

public sealed class AutoCrafter
{
    private static readonly Random _rng = new();
    private CancellationTokenSource? _cts;

    public NativeMethods.POINT CurrencyPos    { get; set; } // Alt / primary
    public NativeMethods.POINT AugCurrencyPos { get; set; }

    // When true, pick Alt vs Aug from getAction after each eval. When false,
    // always use CurrencyPos (chaos/alt spam).
    public bool AltAugMode { get; set; }

    // The queue of item slots to craft, in order. Each is rolled until its
    // targets are hit, then the crafter advances to the next.
    public IReadOnlyList<NativeMethods.POINT> ItemPositions { get; set; } = [];

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start(Func<bool> shouldStop, Func<string?> getItemHash, Func<int> getEvalSeq,
                      Func<CraftAction> getAction,
                      Action<int> onItemStart, Action<string?> onStopped)
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => RunLoop(shouldStop, getItemHash, getEvalSeq, getAction, onItemStart, onStopped, _cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    private enum HeldOrb { None, Alt, Aug }

    // ── Main loop ─────────────────────────────────────────────────────
    private async Task RunLoop(Func<bool> shouldStop, Func<string?> getItemHash, Func<int> getEvalSeq,
                               Func<CraftAction> getAction,
                               Action<int> onItemStart, Action<string?> onStopped, CancellationToken ct)
    {
        string? stopReason = null;
        var held = HeldOrb.None;
        try
        {
            var items = ItemPositions;
            if (items.Count == 0) { onStopped("Не задана позиция предмета"); return; }

            // Don't touch the mouse until PoE2 is the active window
            while (!ct.IsCancellationRequested && !IsPoE2Active())
                await Delay(300, 0, ct);
            ct.ThrowIfCancellationRequested();

            // Human flow: Shift is held while applying a stack. Switching Alt↔Aug
            // releases Shift, right-clicks the other stack, and re-holds.
            bool shiftFreeCopy = false;
            held = await PickupOrbAsync(HeldOrb.Alt, held, ct);

            // Craft each queued item in turn; advance once its targets are hit.
            for (int idx = 0; idx < items.Count; idx++)
            {
                ct.ThrowIfCancellationRequested();
                onItemStart(idx);
                (bool matched, stopReason, shiftFreeCopy, held) =
                    await RunItem(items[idx], shiftFreeCopy, held,
                        shouldStop, getItemHash, getEvalSeq, getAction, ct);
                if (!matched) break; // hit a failure — surface the reason and stop
            }

            if (stopReason == null && !ct.IsCancellationRequested && items.Count > 1)
                stopReason = $"Готово — скрафчено предметов: {items.Count}";
        }
        catch (OperationCanceledException) { }
        finally
        {
            ShiftUp(); // safety: never leave Shift physically held after stop
            onStopped(stopReason);
        }
    }

    private async Task<(bool matched, string? reason, bool shiftFreeCopy, HeldOrb held)> RunItem(
        NativeMethods.POINT itemPos, bool shiftFreeCopy, HeldOrb held,
        Func<bool> shouldStop, Func<string?> getItemHash, Func<int> getEvalSeq,
        Func<CraftAction> getAction, CancellationToken ct)
    {
        int cycleCount    = 0;
        int sameHashCount = 0;
        int failedCycles  = 0;
        string? prevHash  = null;

        // Baseline check: read what's already on this item BEFORE spending an orb.
        {
            await MoveSmooth(GetCursor(), Jitter(itemPos, 4), ct);
            await Delay(Rng(40, 80), 0, ct);
            int seqBefore = getEvalSeq();
            if (shiftFreeCopy) ShiftUp();
            await SendCtrlCAsync(ct);
            bool ev = await WaitEval(getEvalSeq, seqBefore, 1500, ct);
            if (shiftFreeCopy)
            {
                // Re-hold whatever we were applying
                held = await PickupOrbAsync(held == HeldOrb.None ? HeldOrb.Alt : held, HeldOrb.None, ct);
            }
            if (ev && shouldStop()) return (true, null, shiftFreeCopy, held);
            if (ev)
            {
                var baselined = getAction();
                if (baselined == CraftAction.Abort)
                    return (false, "Alt+Aug работает только на Magic-предметах", shiftFreeCopy, held);
                held = await EnsureOrbForActionAsync(baselined, held, ct);
            }
        }

        while (!ct.IsCancellationRequested)
        {
            if (!IsPoE2Active()) { await Delay(300, 0, ct); continue; }

            ShiftDown(); // no-op when already held
            var target = Jitter(itemPos, 4);
            await MoveSmooth(GetCursor(), target, ct);
            await Delay(Rng(40, 80), 0, ct);
            await ClickAsync(right: false, ct);

            // Wait for PoE2 to process the orb use
            await Delay(Rng(130, 180), 15, ct);

            int seqBefore = getEvalSeq();
            if (shiftFreeCopy) ShiftUp();
            await SendCtrlCAsync(ct);
            bool evaluated = await WaitEval(getEvalSeq, seqBefore, 700, ct);

            if (!evaluated)
            {
                await SendCtrlCAsync(ct);
                evaluated = await WaitEval(getEvalSeq, seqBefore, 700, ct);
            }

            if (!evaluated && !shiftFreeCopy)
            {
                ShiftUp();
                await Delay(60, 20, ct);
                await SendCtrlCAsync(ct);
                evaluated = await WaitEval(getEvalSeq, seqBefore, 1200, ct);
                if (evaluated)
                {
                    shiftFreeCopy = true;
                    held = await PickupOrbAsync(held == HeldOrb.None ? HeldOrb.Alt : held, HeldOrb.None, ct);
                }
            }
            await Delay(Rng(40, 80), 0, ct);

            if (shouldStop()) return (true, null, shiftFreeCopy, held);

            if (!evaluated)
            {
                if (++failedCycles >= 5)
                    return (false, "Предмет не считывается — проверь позицию Item", shiftFreeCopy, held);
                continue;
            }

            var action = getAction();
            if (action == CraftAction.Abort)
                return (false, "Alt+Aug работает только на Magic-предметах", shiftFreeCopy, held);

            var hash = getItemHash();
            failedCycles = (hash == null || hash == "") ? failedCycles + 1 : 0;
            if (failedCycles >= 5)
                return (false, "Не читается предмет — проверь позицию Item", shiftFreeCopy, held);

            sameHashCount = (hash != null && hash == prevHash && hash != "") ? sameHashCount + 1 : 0;
            prevHash = hash;
            if (sameHashCount >= 5)
                return (false, "Предмет не меняется — валюта закончилась?", shiftFreeCopy, held);
            if (sameHashCount == 3)
            {
                // Re-pickup the CURRENT orb — don't treat a needed Alt↔Aug switch
                // as an empty stack (switch resets sameHash via a real change).
                held = await PickupOrbAsync(held == HeldOrb.None ? HeldOrb.Alt : held, HeldOrb.None, ct);
            }

            // Switch stack before the next apply if the policy wants the other orb
            held = await EnsureOrbForActionAsync(action, held, ct);

            cycleCount++;

            if (cycleCount % Rng(8, 18) == 0)
                await Delay(Rng(600, 1500), 150, ct);
        }

        ct.ThrowIfCancellationRequested();
        return (false, null, shiftFreeCopy, held);
    }

    private async Task<HeldOrb> EnsureOrbForActionAsync(CraftAction action, HeldOrb held, CancellationToken ct)
    {
        var want = (!AltAugMode || action != CraftAction.UseAug) ? HeldOrb.Alt : HeldOrb.Aug;
        return await PickupOrbAsync(want, held, ct);
    }

    // Pick up want if it isn't already held. Passing held=None forces a re-pickup
    // of the same stack (after Shift was released for a shift-free copy).
    private async Task<HeldOrb> PickupOrbAsync(HeldOrb want, HeldOrb held, CancellationToken ct)
    {
        if (want == HeldOrb.None) want = HeldOrb.Alt;
        if (want == held)
        {
            ShiftDown();
            return held;
        }

        if (held != HeldOrb.None)
        {
            ShiftUp(); // drop apply mode before grabbing the other stack
            await Delay(80, 20, ct);
        }

        ShiftDown();
        var pos = want == HeldOrb.Aug ? AugCurrencyPos : CurrencyPos;
        await MoveSmooth(GetCursor(), Jitter(pos, 3), ct);
        await Delay(60, 20, ct);
        await ClickAsync(right: true, ct);
        await Delay(200, 50, ct);
        LogInput(want == HeldOrb.Aug ? "pickup AUG" : "pickup ALT");
        return want;
    }

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

    // ── Mouse helpers ─────────────────────────────────────────────────
    // Human-ish pointer travel: distance-scaled duration, ease-in-out timing,
    // a soft sideways arc, and a tiny end correction. Fast enough to not feel
    // sluggish, curved enough to not look like a teleport on a ruler.
    private static async Task MoveSmooth(NativeMethods.POINT from, NativeMethods.POINT to, CancellationToken ct)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < 1f)
        {
            NativeMethods.SetCursorPos(to.X, to.Y);
            return;
        }

        bool micro = dist < 40;
        // ~0.35–0.5 ms/px for longer hops, clamped so inventory jumps stay snappy
        int totalMs = micro
            ? Rng(20, 45)
            : (int)Math.Clamp(dist * (0.34f + _rng.NextSingle() * 0.16f) + Rng(25, 55), 110, 360);
        int steps = micro ? Rng(4, 8) : Rng(20, 34);

        // Unit direction + perpendicular for a natural arc (not a perfectly straight bezier)
        float ux = dx / dist, uy = dy / dist;
        float px = -uy, py = ux;
        float bend = micro
            ? _rng.Next(-4, 5)
            : (_rng.Next(0, 2) == 0 ? 1 : -1) * Rng(28, 72) * Math.Clamp(dist / 450f, 0.45f, 1.2f);

        float cp1x = from.X + dx * 0.28f + px * bend;
        float cp1y = from.Y + dy * 0.28f + py * bend;
        float cp2x = from.X + dx * 0.72f + px * bend * 0.55f;
        float cp2y = from.Y + dy * 0.72f + py * bend * 0.55f;

        // Slight overshoot past the target, then settle — humans rarely nail the pixel first try
        float overshoot = micro ? 0 : Rng(2, 7);
        float endX = to.X + ux * overshoot;
        float endY = to.Y + uy * overshoot;

        for (int i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            float t = EaseInOut((float)i / steps);
            int x = (int)Bezier(from.X, cp1x, cp2x, endX, t);
            int y = (int)Bezier(from.Y, cp1y, cp2y, endY, t);
            // Micro jitter on mid-path steps only — keeps the trail from looking sampled
            if (!micro && i > 1 && i < steps)
            {
                x += _rng.Next(-1, 2);
                y += _rng.Next(-1, 2);
            }
            NativeMethods.SetCursorPos(x, y);
            // Slightly uneven step timing (humans don't clock equal intervals)
            int slice = Math.Max(1, totalMs / steps + _rng.Next(-2, 3));
            await Task.Delay(slice, ct);
        }

        // Final settle on the real target
        if (overshoot > 0)
        {
            await Task.Delay(Rng(12, 28), ct);
            NativeMethods.SetCursorPos(to.X, to.Y);
        }
    }

    // Smoothstep — accelerate out, decelerate in
    private static float EaseInOut(float t) => t * t * (3f - 2f * t);

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

    private static async Task SendCtrlCAsync(CancellationToken ct)
    {
        LogInput("Ctrl+C (read item)");
        SendKey(NativeMethods.VK_CONTROL, down: true);
        await Delay(20, 5, ct);
        SendKey(NativeMethods.VK_C, down: true);
        await Delay(50, 15, ct);
        SendKey(NativeMethods.VK_C, down: false);
        await Delay(20, 5, ct);
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

    private static NativeMethods.POINT Jitter(NativeMethods.POINT p, int r) =>
        new() { X = p.X + _rng.Next(-r, r), Y = p.Y + _rng.Next(-r, r) };

    private static NativeMethods.POINT GetCursor()
    {
        NativeMethods.GetCursorPos(out var p);
        return p;
    }

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
