using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace Poe2Crafter.Services;

public static class UpdateService
{
    private const string ApiUrl =
        "https://api.github.com/repos/wolfsum/Poe-crafter/releases/latest";

    public record UpdateInfo(Version Version, string DownloadUrl);

    public static Version Current =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    private static HttpClient NewClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Poe2Crafter"); // GitHub API requires UA
        return http;
    }

    // Info when a newer release with an exe asset exists; Error when the check
    // itself failed (offline, 404 on private repo, …). (null, null) = up to date.
    public static async Task<(UpdateInfo? Info, string? Error)> CheckAsync()
    {
        try
        {
            using var http = NewClient();
            var json = await http.GetStringAsync(ApiUrl);
            using var doc = JsonDocument.Parse(json);

            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
                return (null, $"Bad release tag: {tag}");
            if (latest <= Current) return (null, null);

            foreach (var asset in doc.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return (new UpdateInfo(latest, asset.GetProperty("browser_download_url").GetString()!), null);
            }
            return (null, "Release has no exe asset");
        }
        catch (HttpRequestException ex)
        {
            return (null, ex.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "404 — repo is private or no releases"
                : $"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    // Download new exe, swap binaries (running exe can be renamed, not overwritten),
    // start the new version and exit.
    public static async Task DownloadAndApplyAsync(UpdateInfo info)
    {
        var exePath = Environment.ProcessPath!;
        var dir     = Path.GetDirectoryName(exePath)!;
        var newPath = Path.Combine(dir, "Poe2Crafter.new.exe");
        var oldPath = Path.Combine(dir, "Poe2Crafter.old.exe");

        using (var http = NewClient())
        await using (var src = await http.GetStreamAsync(info.DownloadUrl))
        await using (var dst = File.Create(newPath))
            await src.CopyToAsync(dst);

        if (File.Exists(oldPath)) File.Delete(oldPath);
        File.Move(exePath, oldPath);
        File.Move(newPath, exePath);

        Process.Start(exePath);
        System.Windows.Application.Current.Shutdown();
    }

    // Remove the previous binary left from the last update
    public static void CleanupOldBinary()
    {
        try
        {
            var old = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, "Poe2Crafter.old.exe");
            if (File.Exists(old)) File.Delete(old);
        }
        catch { /* still locked by the exiting process — next run will get it */ }
    }
}
