using System.Globalization;
using System.Text.RegularExpressions;
using Poe2Crafter.Core.Games;
using Poe2Crafter.Core.Models;

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

    // All mod groups available for the given selection under the active game
    // profile. Groups are sorted alphabetically, tiers within each best-first.
    public IReadOnlyList<ModGroup> GetGroups(GameProfile profile, SlotSelection selection)
    {
        var tags = profile.BuildTags(selection);

        return AllMods
            .Where(m => profile.SourceAllowed(m.Source, selection.Slot) && CanSpawn(m, tags))
            .GroupBy(m => m.Group)
            .Select(g => new ModGroup(g.Key, g))
            .OrderBy(g => g.DisplayName)
            .ToList();
    }

    // Game spawn-weight semantics: walk the mod's weightKey list in order, the
    // first key present in the item's tag set decides ("default" always matches).
    // Universal mods like life are { fishing_rod=0, weapon=0, default=1000 } —
    // the old ItemTags check dropped them everywhere because it ignored "default".
    private static bool CanSpawn(ModDefinition m, IReadOnlySet<string> tags)
    {
        if (m.WeightKeys.Length == 0)
            return m.ItemTags.Any(tags.Contains); // embedded mods: plain tag match

        for (int i = 0; i < m.WeightKeys.Length && i < m.WeightVals.Length; i++)
        {
            var key = m.WeightKeys[i];
            if (key == "default" || tags.Contains(key))
                return m.WeightVals[i] > 0;
        }
        return false;
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
