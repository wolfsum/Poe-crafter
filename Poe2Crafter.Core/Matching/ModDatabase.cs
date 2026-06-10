using System.Globalization;
using System.Text.RegularExpressions;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Matching;

namespace Poe2Crafter.Core.Matching;

public class ModDatabase
{
    private readonly List<(Regex Compiled, ModDefinition Mod)> _entries;

    public IReadOnlyList<ModDefinition> AllMods { get; }

    public ModDatabase(IEnumerable<ModDefinition> mods)
    {
        AllMods = mods.ToList();
        // No RegexOptions.Compiled: with thousands of patterns the JIT cost on first
        // match caused a multi-second freeze; interpreted matching is fast enough
        _entries = AllMods
            .Select(m => (
                new Regex(m.MatchRegex, RegexOptions.IgnoreCase),
                m))
            .ToList();
    }

    // All mod groups available for a given item slot + armour base.
    // Groups are sorted alphabetically, tiers within each group best-first.
    public IReadOnlyList<ModGroup> GetGroups(ItemSlot slot, ArmourBase armourBase = ArmourBase.None, JewelType jewelType = JewelType.None, TabletType tabletType = TabletType.None)
    {
        var tags = ItemTypeHelper.GetTags(slot, armourBase, jewelType, tabletType);

        return AllMods
            .Where(m => m.ItemTags.Any(tags.Contains))
            .GroupBy(m => m.Group)
            .Select(g => new ModGroup(g.Key, g))
            .OrderBy(g => g.DisplayName)
            .ToList();
    }

    // Match a single mod line from clipboard against the database.
    // Returns one result per matching mod — caller picks the best tier.
    public IReadOnlyList<ModMatch> Match(string line)
    {
        var results = new List<ModMatch>();

        foreach (var (regex, mod) in _entries)
        {
            var m = regex.Match(line);
            if (!m.Success) continue;

            var values = ExtractValues(m);

            // If we know the expected range, verify the first value fits
            if (mod.ValuesMin.Length > 0 && values.Length > 0)
            {
                if (values[0] < mod.ValuesMin[0] || values[0] > mod.ValuesMax[0])
                    continue;
            }

            results.Add(new ModMatch(mod, values));
        }

        return results;
    }

    // Match all mod lines from a parsed item at once.
    // Returns only the highest tier match per group (most specific).
    public IReadOnlyList<ModMatch> MatchItem(IEnumerable<string> modLines)
    {
        var allMatches = modLines
            .SelectMany(Match)
            .ToList();

        // Keep highest tier per group
        return allMatches
            .GroupBy(x => x.Mod.Group)
            .Select(g => g.OrderByDescending(x => x.Mod.Tier).First())
            .ToList();
    }

    private static double[] ExtractValues(Match m)
    {
        var values = new List<double>();
        for (int i = 1; i < m.Groups.Count; i++)
        {
            if (double.TryParse(m.Groups[i].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v))
                values.Add(v);
        }
        return values.ToArray();
    }
}
