using System.Diagnostics;

namespace Poe2Crafter.Services;

public static class Poe2Process
{
    private static readonly string[] Names =
        ["PathOfExile", "PathOfExile_x64", "PathOfExile2", "PathOfExileSteam"];

    // Cached "is any PoE2 process running" — refreshed every 5 s, used as a
    // fallback so the tool stays testable outside the game
    private static bool _anyRunning;
    private static DateTime _anyCheckedAt = DateTime.MinValue;

    // Window handle belongs to a PoE2 process
    public static bool IsPoe2Window(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return false;
        try
        {
            using var p = Process.GetProcessById((int)pid);
            return Names.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static bool AnyRunning()
    {
        if ((DateTime.UtcNow - _anyCheckedAt).TotalSeconds > 5)
        {
            _anyRunning = false;
            foreach (var name in Names)
            {
                var procs = Process.GetProcessesByName(name);
                if (procs.Length > 0) _anyRunning = true;
                foreach (var p in procs) p.Dispose();
            }
            _anyCheckedAt = DateTime.UtcNow;
        }
        return _anyRunning;
    }
}
