using System.Text.RegularExpressions;

namespace Poe2Crafter.Core.Parsing;

public record ParsedItem(
    string ItemClass,
    string Rarity,
    int ItemLevel,
    string[] ModLines   // individual stat lines, ready for ModDatabase.Match()
);

public static class ItemParser
{
    private static readonly Regex ItemLevelRegex = new(@"^Item Level:\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex ItemClassRegex = new(@"^Item Class:\s*(.+)", RegexOptions.Compiled);
    private static readonly Regex RarityRegex    = new(@"^Rarity:\s*(.+)", RegexOptions.Compiled);
    // Format 2 annotation lines: { Prefix Modifier "Name" (Tier: 3) }
    private static readonly Regex AnnotationRegex = new(@"^\{.*\}$", RegexOptions.Compiled);

    // Roll-range hints PoE2 appends after values: "+62(60-80) to maximum Life" → "+62 to maximum Life"
    private static readonly Regex RangeHintRegex = new(@"(?<=\d)\(\d+(?:\.\d+)?-\d+(?:\.\d+)?\)", RegexOptions.Compiled);

    // Lines that are item state markers, not mods
    private static readonly HashSet<string> SkipLines = new(StringComparer.OrdinalIgnoreCase)
    {
        "Corrupted", "Fractured Item", "Synthesised Item", "Unidentified",
        "Shaper Item", "Elder Item", "Crusader Item", "Warlord Item",
        "Hunter Item", "Redeemer Item", "Searing Exarch Item", "Eater of Worlds Item",
    };

    public static ParsedItem? TryParse(string clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText)) return null;

        var lines = clipboardText.ReplaceLineEndings("\n").Split('\n');

        // Must start with "Item Class:" to be a PoE item
        if (!lines.Any(l => ItemClassRegex.IsMatch(l.Trim()))) return null;

        string itemClass = "";
        string rarity    = "";
        int itemLevel    = 0;
        int itemLevelIdx = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var icm  = ItemClassRegex.Match(line);
            var rm   = RarityRegex.Match(line);
            var ilm  = ItemLevelRegex.Match(line);

            if (icm.Success) itemClass = icm.Groups[1].Value.Trim();
            if (rm.Success)  rarity    = rm.Groups[1].Value.Trim();
            if (ilm.Success) { itemLevel = int.Parse(ilm.Groups[1].Value); itemLevelIdx = i; }
        }

        if (itemLevelIdx < 0) return null;

        // Everything after "Item Level:" line is mods (skip section separators and annotations)
        var modLines = lines
            .Skip(itemLevelIdx + 1)
            .Select(l => RangeHintRegex.Replace(l.Trim(), ""))
            .Where(l => l.Length > 0
                     && l != "--------"
                     && !AnnotationRegex.IsMatch(l)
                     && !SkipLines.Contains(l))
            .ToArray();

        if (modLines.Length == 0) return null;

        return new ParsedItem(itemClass, rarity, itemLevel, modLines);
    }
}
