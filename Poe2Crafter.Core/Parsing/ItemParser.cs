using System.Text.RegularExpressions;

namespace Poe2Crafter.Core.Parsing;

public enum AffixType { Unknown, Prefix, Suffix }

// One craftable mod line. Tier comes from the "{ Prefix Modifier ... (Tier: 5) }"
// annotation when present; 0 = unknown (infer from value ranges).
// AffixType is filled from the Prefix/Suffix annotation when advanced item text is on.
public record ParsedModLine(string Text, int Tier, AffixType AffixType = AffixType.Unknown);

public record ParsedItem(
    string ItemClass,
    string Rarity,
    int ItemLevel,
    IReadOnlyList<ParsedModLine> Mods
);

public static class ItemParser
{
    private static readonly Regex ItemLevelRegex = new(@"^Item Level:\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex ItemClassRegex = new(@"^Item Class:\s*(.+)", RegexOptions.Compiled);
    private static readonly Regex RarityRegex    = new(@"^Rarity:\s*(.+)", RegexOptions.Compiled);

    // Annotation lines from advanced item descriptions:
    // { Prefix Modifier "Hale" (Tier: 5) — Life }
    private static readonly Regex AnnotationRegex = new(@"^\{(?<body>.*)\}$", RegexOptions.Compiled);
    private static readonly Regex TierRegex       = new(@"\(Tier:\s*(?<t>\d+)\)", RegexOptions.Compiled);

    // Roll-range hints PoE2 appends after values: "+62(60-80) to maximum Life" → "+62 to maximum Life"
    private static readonly Regex RangeHintRegex = new(@"(?<=\d)\(\d+(?:\.\d+)?-\d+(?:\.\d+)?\)", RegexOptions.Compiled);

    // Plain-copy markers for non-craftable lines (no annotations available)
    private static readonly string[] SkipMarkers = ["(implicit)", "(enchant)", "(rune)"];

    // Lines that are item state markers, not mods
    private static readonly HashSet<string> SkipLines = new(StringComparer.OrdinalIgnoreCase)
    {
        "Corrupted", "Fractured Item", "Unidentified",
    };

    public static ParsedItem? TryParse(string clipboardText)
    {
        if (string.IsNullOrWhiteSpace(clipboardText)) return null;

        var lines = clipboardText.ReplaceLineEndings("\n").Split('\n');

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

        // Walk lines after "Item Level:". With annotations (advanced descriptions) we keep
        // only Prefix/Suffix/Unique mod lines and read tiers; implicits, enchants and runes
        // are excluded so they can't false-trigger a STOP.
        var mods = new List<ParsedModLine>();
        bool sawAnnotations = false;
        bool keepCurrent    = false;
        int  currentTier    = 0;
        AffixType currentAffix = AffixType.Unknown;

        foreach (var raw in lines.Skip(itemLevelIdx + 1))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (line == "--------")
            {
                keepCurrent = false; // section break ends the current annotation scope
                continue;
            }

            var ann = AnnotationRegex.Match(line);
            if (ann.Success)
            {
                sawAnnotations = true;
                var body = ann.Groups["body"].Value;
                keepCurrent = body.Contains("Prefix Modifier") ||
                              body.Contains("Suffix Modifier") ||
                              body.Contains("Unique Modifier");
                currentAffix = body.Contains("Prefix Modifier") ? AffixType.Prefix
                             : body.Contains("Suffix Modifier") ? AffixType.Suffix
                             : AffixType.Unknown;
                var tm = TierRegex.Match(body);
                currentTier = tm.Success ? int.Parse(tm.Groups["t"].Value) : 0;
                continue;
            }

            if (SkipLines.Contains(line)) continue;

            var text = RangeHintRegex.Replace(line, "");

            if (sawAnnotations)
            {
                if (keepCurrent)
                    mods.Add(new ParsedModLine(text, currentTier, currentAffix));
            }
            else
            {
                // Plain copy without annotations — keep everything except marked lines
                if (!SkipMarkers.Any(m => text.EndsWith(m, StringComparison.OrdinalIgnoreCase)))
                    mods.Add(new ParsedModLine(text, 0));
            }
        }

        if (mods.Count == 0) return null;

        return new ParsedItem(itemClass, rarity, itemLevel, mods);
    }
}
