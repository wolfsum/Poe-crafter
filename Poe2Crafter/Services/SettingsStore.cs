using System.IO;
using System.Text.Json;

namespace Poe2Crafter.Services;

public class AppSettings
{
    public string? GameVersion { get; set; } // "poe1" / "poe2" — selected game profile
    public string? Slot       { get; set; }
    public string? ArmourBase { get; set; }
    public string? JewelType  { get; set; }
    public string? TabletType { get; set; }
    public string? Influence  { get; set; }
    public string? ClusterBase { get; set; } // affliction_* tag
    // Target mods are deliberately NOT persisted — re-adding them takes seconds
    // and a stale list from a past session caused surprise STOP/GO states.

    public bool IsAutoMode        { get; set; }

    // "ChaosAlt" (single currency) or "AltAug" (Alt + Aug dual currency)
    public string CraftMode { get; set; } = "ChaosAlt";

    public int  StopAfter  { get; set; } // 0 = unlimited
    public int  TotalSpent { get; set; } // lifetime orb tally, survives restarts

    public int  CurrencyX   { get; set; }
    public int  CurrencyY   { get; set; }
    public bool CurrencySet { get; set; }

    // Augmentation slot — only used in Alt+Aug mode
    public int  AugCurrencyX   { get; set; }
    public int  AugCurrencyY   { get; set; }
    public bool AugCurrencySet { get; set; }

    // Legacy single-item position (pre-queue). Kept for one-way migration into
    // ItemSlots when loading old settings files; no longer written.
    public int  ItemX       { get; set; }
    public int  ItemY       { get; set; }
    public bool ItemSet     { get; set; }

    // The item-craft queue: one entry per slot, in order.
    public List<ItemSlotSetting> ItemSlots { get; set; } = new();

    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop  { get; set; } = double.NaN;
}

public class ItemSlotSetting
{
    public int  X     { get; set; }
    public int  Y     { get; set; }
    public bool IsSet { get; set; }
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
