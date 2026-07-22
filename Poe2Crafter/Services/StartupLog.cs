using System.IO;

namespace Poe2Crafter.Services;

// Step-by-step startup trace to %APPDATA%\Poe2Crafter\startup.log. Each line is
// flushed to disk immediately (AppendAllText opens/closes per call), so even a
// hard crash mid-startup leaves the last reached step on disk — telling us
// exactly where it died on a machine we can't debug on. Overwritten each launch.
public static class StartupLog
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Poe2Crafter", "startup.log");

    public static void Begin()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var header =
                $"=== Poe2Crafter startup {DateTime.Now:u} ===\n" +
                $"OS       : {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "x64" : "x86")})\n" +
                $"Runtime  : {Environment.Version}\n" +
                $"Exe      : {Environment.ProcessPath}\n" +
                $"User     : {Environment.UserName}\n\n";
            File.WriteAllText(Path, header);
        }
        catch { /* logging must never break startup */ }
    }

    public static void Write(string msg)
    {
        try { File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff}  {msg}\n"); }
        catch { /* ignore */ }
    }
}
