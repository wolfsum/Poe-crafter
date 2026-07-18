namespace Poe2Crafter.Core.Models;

public record ModDefinition
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string AffixName { get; init; } = "";
    public string Template { get; init; } = "";   // primary stat line (used for regex matching)
    public string[] Templates { get; init; } = []; // all stat lines
    public string MatchRegex { get; init; } = "";
    public string Group { get; init; } = "";
    public int Tier { get; init; }
    public int MinLevel { get; init; }
    public uint TradeHash { get; init; }
    public string[] Tags { get; init; } = [];
    public string Source { get; init; } = "";
    // Parsed min/max per range in template, e.g. "+(5-8) to Strength" → Min=[5], Max=[8]
    public double[] ValuesMin { get; init; } = [];
    public double[] ValuesMax { get; init; } = [];
    // Item type tags where this mod can spawn (weightKey entries with weight > 0, excluding "default").
    // Used for embedded mods that have no raw weight table.
    public string[] ItemTags { get; init; } = [];
    // Raw ordered weightKey/weightVal pairs from PoB data. Game semantics: the
    // FIRST key the item's tag set contains decides the spawn weight ("default"
    // matches everything) — later entries are ignored. E.g. life is
    // { fishing_rod=0, weapon=0, default=1000 }: banned on weapons, rolls anywhere else.
    public string[] WeightKeys { get; init; } = [];
    public int[]    WeightVals { get; init; } = [];
}
