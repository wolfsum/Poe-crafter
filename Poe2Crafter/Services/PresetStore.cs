using System.IO;
using System.Text.Json;

namespace Poe2Crafter.Services;

// One saved target line. Only the identity is stored (group + tier + match mode)
// — display names come from the live mod database, so a preset survives a data
// update that renames a mod.
public class PresetTarget
{
    public string GroupId { get; set; } = "";
    public int    Tier    { get; set; }
    public bool   Exact   { get; set; }
}

// A named craft setup: the whole item context plus the target list. The context
// has to travel with the targets — a group id alone means nothing until the slot
// and base are selected, because the available groups depend on them.
// Auto-craft screen positions are deliberately NOT part of a preset: they are
// tied to the current resolution and stash layout, not to the craft.
public class CraftPreset
{
    public string  Name        { get; set; } = "";
    public string  Game        { get; set; } = "";  // "poe1" / "poe2"
    public string? Slot        { get; set; }
    public string? ArmourBase  { get; set; }
    public string? JewelType   { get; set; }
    public string? TabletType  { get; set; }
    public string? Influence   { get; set; }
    public string? ClusterBase { get; set; }
    public string? CraftMode   { get; set; }  // "AltAug" / "ChaosAlt"
    public int     StopAfter   { get; set; }
    public List<PresetTarget> Targets { get; set; } = [];

    public override string ToString() => Name;
}

public static class PresetStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Poe2Crafter", "presets.json");

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<CraftPreset> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<CraftPreset>>(File.ReadAllText(FilePath)) ?? [];
        }
        catch { /* corrupt file → start with an empty list, never crash on startup */ }
        return [];
    }

    public static void Save(IEnumerable<CraftPreset> presets)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(presets, Options));
        }
        catch { /* not fatal */ }
    }
}
