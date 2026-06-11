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

            // Pick up currency stack once at start (right-click puts the stack on cursor)
            await MoveSmooth(GetCursor(), CurrencyPos, ct);
            await Delay(60, 20, ct);
            await ClickAsync(right: true, ct);
            await Delay(200, 50, ct);

            while (!ct.IsCancellationRequested)
            {
                if (!IsPoE2Active()) { await Delay(300, 0, ct); continue; }

                // Shift+click applies currency repeatedly without dropping it
                // from the cursor (plain click would pick the item up instead)
                var target = Jitter(ItemPos, 4);
                await MoveSmooth(GetCursor(), target, ct);
                await Delay(Rng(40, 80), 0, ct);

                SendShift(down: true);
                try
                {
                    await Delay(Rng(30, 60), 0, ct);
                    await ClickAsync(right: false, ct);
                    await Delay(Rng(30, 60), 0, ct);
                }
                finally { SendShift(down: false); } // never leave Shift stuck

                // Wait for PoE2 to process the orb use
                await Delay(Rng(130, 180), 15, ct);

                // Copy item stats and wait until the app actually evaluated the
                // clipboard (PoE2 fires several clipboard events per copy — fixed
                // delays raced and could skip the evaluation)
                int seqBefore = getEvalSeq();
                SendCtrlC();

                int waitedMs = 0;
                while (!ct.IsCancellationRequested && getEvalSeq() == seqBefore && waitedMs < 1500)
                {
                    await Task.Delay(50, ct);
                    waitedMs += 50;
                }
                bool evaluated = getEvalSeq() != seqBefore;
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
                if (sameHashCount >= 3)
                {
                    stopReason = "Предмет не меняется — валюта закончилась?";
                    break;
                }

                cycleCount++;

                // Human-like occasional pause (every 8–18 clicks, 0.8–2.5s)
                if (cycleCount % Rng(8, 18) == 0)
                    await Delay(Rng(800, 2500), 200, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { onStopped(stopReason); }
    }

    // ── Mouse helpers ─────────────────────────────────────────────────
    private static async Task MoveSmooth(NativeMethods.POINT from, NativeMethods.POINT to, CancellationToken ct)
    {
        int steps    = Rng(14, 22);
        int totalMs  = Rng(70, 130);

        // Cubic bezier control points — random slight curve
        float cp1x = from.X + (to.X - from.X) * 0.3f + _rng.Next(-35, 35);
        float cp1y = from.Y + (to.Y - from.Y) * 0.3f + _rng.Next(-35, 35);
        float cp2x = from.X + (to.X - from.X) * 0.7f + _rng.Next(-25, 25);
        float cp2y = from.Y + (to.Y - from.Y) * 0.7f + _rng.Next(-25, 25);

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
        uint flag = right
            ? (down ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_RIGHTUP)
            : (down ? NativeMethods.MOUSEEVENTF_LEFTDOWN  : NativeMethods.MOUSEEVENTF_LEFTUP);
        var inputs = new[] { MouseInput(flag) };
        NativeMethods.SendInput(1, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendShift(bool down)
    {
        var inputs = new NativeMethods.INPUT[]
        {
            new() { type = NativeMethods.INPUT_KEYBOARD, u = new() { ki = new()
            {
                wVk     = NativeMethods.VK_SHIFT,
                dwFlags = down ? 0 : NativeMethods.KEYEVENTF_KEYUP,
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
