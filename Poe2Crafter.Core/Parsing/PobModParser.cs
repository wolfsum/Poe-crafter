using System.Globalization;
using System.Text.RegularExpressions;
using Poe2Crafter.Core.Models;

namespace Poe2Crafter.Core.Parsing;

public static class PobModParser
{
    private static readonly Regex EntryRegex = new(
        @"\[""(?<id>[^""]+)""\]\s*=\s*\{(?<body>.+)\},?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex TypeRegex     = new(@"\btype\s*=\s*""(?<v>[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex AffixRegex    = new(@"\baffix\s*=\s*""(?<v>[^""]*)""", RegexOptions.Compiled);
    private static readonly Regex LevelRegex    = new(@"\blevel\s*=\s*(?<v>\d+)", RegexOptions.Compiled);
    private static readonly Regex GroupRegex    = new(@"\bgroup\s*=\s*""(?<v>[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex HashRegex     = new(@"\btradeHash\s*=\s*(?<v>\d+)", RegexOptions.Compiled);
    private static readonly Regex ModTagsRegex  = new(@"\bmodTags\s*=\s*\{(?<v>[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex WeightKeyRegex = new(@"\bweightKey\s*=\s*\{(?<v>[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex WeightValRegex = new(@"\bweightVal\s*=\s*\{(?<v>[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex QuotedRegex   = new(@"""(?<t>[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex NumberRegex   = new(@"\b(?<n>\d+)\b", RegexOptions.Compiled);

    // Matches the affix field — end position used to find all following stat strings
    private static readonly Regex AffixFieldRegex = new(
        @"\baffix\s*=\s*""[^""]*""", RegexOptions.Compiled);

    // First display string inside tradeHashes = { [123] = { "..." } }
    private static readonly Regex TradeTextRegex = new(
        @"tradeHashes\s*=\s*\{\s*\[\d+\]\s*=\s*\{\s*""(?<t>[^""]+)""", RegexOptions.Compiled);

    private static readonly Regex TierRegex = new(@"(\d+)_?$", RegexOptions.Compiled);

    private static readonly Regex RangeExtract = new(
        @"\((\d+(?:\.\d+)?)-(\d+(?:\.\d+)?)\)", RegexOptions.Compiled);

    // Tags that are not real item types and should be excluded from ItemTags
    private static readonly HashSet<string> ExcludedTags = [
        "default", "fishing_rod",
        "no_chaos_spell_mods", "no_cold_spell_mods", "no_fire_spell_mods",
        "no_lightning_spell_mods", "no_physical_spell_mods",
    ];

    public static IReadOnlyList<ModDefinition> ParseFile(string filePath, string source = "item")
    {
        var lines = File.ReadAllLines(filePath);
        var results = new List<ModDefinition>(lines.Length);

        foreach (var line in lines)
        {
            var entry = EntryRegex.Match(line);
            if (!entry.Success) continue;

            var id   = entry.Groups["id"].Value;
            var body = entry.Groups["body"].Value;

            var typeMatch      = TypeRegex.Match(body);
            var affixMatch     = AffixRegex.Match(body);
            var affixFieldMatch = AffixFieldRegex.Match(body);
            var levelMatch     = LevelRegex.Match(body);
            var groupMatch     = GroupRegex.Match(body);
            var hashMatch      = HashRegex.Match(body);
            var modTagsMatch   = ModTagsRegex.Match(body);
            var weightKeyMatch = WeightKeyRegex.Match(body);
            var weightValMatch = WeightValRegex.Match(body);

            if (!typeMatch.Success || !groupMatch.Success || !affixFieldMatch.Success) continue;

            var templates = ExtractTemplates(affixFieldMatch, body);
            if (templates.Length == 0) continue;

            var modTags = modTagsMatch.Success
                ? QuotedRegex.Matches(modTagsMatch.Groups["v"].Value)
                    .Select(m => m.Groups["t"].Value)
                    .ToArray()
                : [];

            var itemTags = ParseItemTags(weightKeyMatch, weightValMatch);

            var tierMatch = TierRegex.Match(id);
            var template  = templates[0];

            // Radius jewel mods store only the granted effect as template;
            // the real in-game line lives in tradeHashes:
            // "Small Passive Skills in Radius also grant (1-2)% increased Accuracy Rating"
            var tradeMatch = TradeTextRegex.Match(body);
            if (tradeMatch.Success)
            {
                var tradeText = tradeMatch.Groups["t"].Value;
                if (tradeText.Length > template.Length && tradeText.Contains(template, StringComparison.Ordinal))
                {
                    template     = tradeText;
                    templates[0] = tradeText; // keep Templates in sync — group display names use it
                }
            }

            var (vMin, vMax) = ExtractRanges(template);

            results.Add(new ModDefinition
            {
                Id        = id,
                Type      = typeMatch.Groups["v"].Value,
                AffixName = affixMatch.Success ? affixMatch.Groups["v"].Value : "",
                Template  = template,
                Templates = templates,
                MatchRegex = BuildMatchRegex(template),
                Group     = groupMatch.Groups["v"].Value,
                Tier      = tierMatch.Success ? int.Parse(tierMatch.Groups[1].Value) : 0,
                MinLevel  = levelMatch.Success ? int.Parse(levelMatch.Groups["v"].Value) : 0,
                TradeHash = hashMatch.Success ? uint.Parse(hashMatch.Groups["v"].Value) : 0,
                Tags      = modTags,
                Source    = source,
                ValuesMin = vMin,
                ValuesMax = vMax,
                ItemTags  = itemTags,
            });
        }

        return results;
    }

    private static string[] ParseItemTags(Match keyMatch, Match valMatch)
    {
        if (!keyMatch.Success || !valMatch.Success) return [];

        var keys = QuotedRegex.Matches(keyMatch.Groups["v"].Value)
            .Select(m => m.Groups["t"].Value)
            .ToArray();

        var vals = NumberRegex.Matches(valMatch.Groups["v"].Value)
            .Select(m => int.Parse(m.Groups["n"].Value))
            .ToArray();

        var result = new List<string>(keys.Length);
        for (int i = 0; i < keys.Length && i < vals.Length; i++)
        {
            if (vals[i] > 0 && !ExcludedTags.Contains(keys[i]))
                result.Add(keys[i]);
        }
        return result.ToArray();
    }

    // Extract all positional stat strings that appear after affix="..." and before statOrder={
    private static string[] ExtractTemplates(Match affixFieldMatch, string body)
    {
        var afterAffix = affixFieldMatch.Index + affixFieldMatch.Length;
        var statOrderIdx = body.IndexOf("statOrder", afterAffix, StringComparison.Ordinal);
        if (statOrderIdx < 0) return [];

        var region = body.Substring(afterAffix, statOrderIdx - afterAffix);
        return QuotedRegex.Matches(region)
            .Select(m => m.Groups["t"].Value)
            .ToArray();
    }

    private static (double[] min, double[] max) ExtractRanges(string template)
    {
        var matches = RangeExtract.Matches(template);
        var min = new double[matches.Count];
        var max = new double[matches.Count];
        for (int i = 0; i < matches.Count; i++)
        {
            min[i] = double.Parse(matches[i].Groups[1].Value, CultureInfo.InvariantCulture);
            max[i] = double.Parse(matches[i].Groups[2].Value, CultureInfo.InvariantCulture);
        }
        return (min, max);
    }

    private static string BuildMatchRegex(string template)
    {
        var parts = Regex.Split(template, @"\(\d+(?:\.\d+)?-\d+(?:\.\d+)?\)");
        return string.Join(@"(\d+(?:\.\d+)?)", parts.Select(Regex.Escape));
    }
}
