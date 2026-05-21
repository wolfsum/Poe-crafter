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
    // Item type tags where this mod can spawn (weightKey entries with weight > 0, excluding "default")
    public string[] ItemTags { get; init; } = [];
}
