using Poe2Crafter.Core.Parsing;

namespace Poe2Crafter.Core.Matching;

public class CraftMatcher(ModDatabase db, bool useAnnotationTiers = true)
{
    public MatchResult Check(ParsedItem item, IReadOnlyList<TargetCondition> targets)
    {
        if (targets.Count == 0)
            return new MatchResult(true, false, [], []);

        // Match every mod line against the database. The tier parsed from the
        // in-game annotation wins over the DB tier inferred from value ranges —
        // but only when the game's tier numbering matches the DB (PoE2). PoE1
        // counts tiers down from the top, so there we trust the value ranges.
        var allMatches = item.Mods
            .SelectMany(line => db.Match(line.Text)
                .Select(m => (line, m, tier: useAnnotationTiers && line.Tier > 0 ? line.Tier : m.Mod.Tier)))
            .ToList();

        var hits   = new List<HitInfo>();
        var misses = new List<TargetCondition>();

        foreach (var target in targets)
        {
            var hit = allMatches.FirstOrDefault(x =>
                x.m.Mod.Group == target.GroupId &&
                (target.Tier == 0 || // untiered mods (jewels): any roll counts
                 (target.Mode == TierMatchMode.AtLeast
                    ? x.tier >= target.Tier
                    : x.tier == target.Tier)));

            if (hit != default)
                hits.Add(new HitInfo(target, hit.line.Text, hit.m.PrimaryValue, hit.tier));
            else
                misses.Add(target);
        }

        return new MatchResult(
            HasItem:    true,
            AllMatched: misses.Count == 0,
            Hits:       hits,
            Misses:     misses);
    }
}
