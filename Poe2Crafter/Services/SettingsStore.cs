using System.IO;
using System.Text.Json;

namespace Poe2Crafter.Services;

public class TargetSetting
{
    public string GroupId { get; set; } = "";
    public int    Tier    { get; set; }
    public bool   IsExact { get; set; }
}

public class AppSettings
{
    public string? Slot       { get; set; }
    public string? ArmourBase { get; set; }
    public string? JewelType  { get; set; }
    public List<TargetSetting> Targets { get; set; } = [];

    public bool IsBlockingEnabled { get; set; } = true;
    public bool IsAutoMode        { get; set; }

    public int  CurrencyX   { get; set; }
    public int  CurrencyY   { get; set; }
    public int  ItemX       { get; set; }
    public int  ItemY       { get; set; }
    public bool CurrencySet { get; set; }
    public bool ItemSet     { get; set; }

    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop  { get; set; } = double.NaN;
}

public static class SettingsStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Poe2Crafter", "settings.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* corrupt file → start fresh */ }
        return new();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
        }
        catch { /* not fatal */ }
    }
}
