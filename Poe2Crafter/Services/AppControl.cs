using System.Diagnostics;
using System.Windows;

namespace Poe2Crafter.Services;

public static class AppControl
{
    // Relaunch the current exe and shut down. Used after switching game profile.
    public static void Restart()
    {
        var exe = Environment.ProcessPath;
        if (exe != null) Process.Start(exe);
        Application.Current.Shutdown();
    }
}
