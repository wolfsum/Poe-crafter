using System.Runtime.InteropServices;

namespace Poe2Crafter.Services;

public sealed class AutoCrafter
{
    private static readonly Random _rng = new();
    private CancellationTokenSource? _cts;

    public NativeMethods.POINT CurrencyPos { get; set; }
    public NativeMethods.POINT ItemPos     { get; set; }

    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public void Start(Func<bool> shouldStop, Func<string?> getItemHash, Action onStopped)
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => RunLoop(shouldStop, getItemHash, onStopped, _cts.Token));
    }

    public void Stop() => _cts?.Cancel();

    // ── Main loop ─────────────────────────────────────────────────────
    private async Task RunLoop(Func<bool> shouldStop, Func<string?> getItemHash, Action onStopped, CancellationToken ct)
    {
        try
        {
            int cycleCount    = 0;
            int sameHashCount = 0;
            string? prevHash  = null;

            // Pick up currency stack once at start
            await MoveSmooth(GetCursor(), CurrencyPos, ct);
            await Delay(60, 20, ct);
            Click(CurrencyPos, right: true);
            await Delay(200, 50, ct);

            while (!ct.IsCancellationRequested)
            {
                if (!IsPoE2Active()) { await Delay(300, 0, ct); continue; }

                // Click item (with slight position jitter every time)
                var target = Jitter(ItemPos, 4);
                await MoveSmooth(GetCursor(), target, ct);
                await Delay(Rng(40, 80), 0, ct);
                Click(target, right: false);

                // Wait for PoE2 to process the orb use
                await Delay(Rng(130, 180), 15, ct);

                // Copy item stats
                SendCtrlC();

                // Wait for clipboard + VM to process
                await Delay(Rng(220, 280), 30, ct);

                if (shouldStop()) break;

                // Safety: item hash unchanged → currency ran out or missed the item
                var hash = getItemHash();
                sameHashCount = (hash != null && hash == prevHash) ? sameHashCount + 1 : 0;
                prevHash = hash;
                if (sameHashCount >= 3) break;

                cycleCount++;

                // Human-like occasional pause (every 8–18 clicks, 0.8–2.5s)
                if (cycleCount % Rng(8, 18) == 0)
                    await Delay(Rng(800, 2500), 200, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally { onStopped(); }
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

    private static void Click(NativeMethods.POINT p, bool right)
    {
        uint down = right ? NativeMethods.MOUSEEVENTF_RIGHTDOWN : NativeMethods.MOUSEEVENTF_LEFTDOWN;
        uint up   = right ? NativeMethods.MOUSEEVENTF_RIGHTUP   : NativeMethods.MOUSEEVENTF_LEFTUP;

        var inputs = new NativeMethods.INPUT[]
        {
            MouseInput(down),
            MouseInput(up),
        };
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
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

    private static bool IsPoE2Active()
    {
        var fg = NativeMethods.GetForegroundWindow();
        foreach (var name in new[] { "PathOfExile", "PathOfExile_x64", "PathOfExile2" })
        {
            var procs = System.Diagnostics.Process.GetProcessesByName(name);
            bool found = procs.Any(p => p.MainWindowHandle == fg);
            foreach (var p in procs) p.Dispose();
            if (found) return true;
        }
        return false;
    }

    private static float Bezier(float p0, float p1, float p2, float p3, float t)
    {
        float u = 1 - t;
        return u*u*u*p0 + 3*u*u*t*p1 + 3*u*t*t*p2 + t*t*t*p3;
    }

    private static Task Delay(int ms, int spread, CancellationToken ct) =>
        Task.Delay(Math.Max(1, ms + _rng.Next(-spread, spread)), ct);

    private static int Rng(int min, int max) => _rng.Next(min, max);
}
