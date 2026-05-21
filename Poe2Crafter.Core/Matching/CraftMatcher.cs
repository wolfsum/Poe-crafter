using Poe2Crafter.Core.Parsing;

namespace Poe2Crafter.Core.Matching;

public class CraftMatcher(ModDatabase db)
{
    public MatchResult Check(ParsedItem item, IReadOnlyList<TargetCondition> targets)
    {
        if (targets.Count == 0)
            return new MatchResult(true, false, [], []);

        // Match every mod line in the item against the database
        var allMatches = item.ModLines
            .SelectMany(line => db.Match(line).Select(m => (line, m)))
            .ToList();

        var hits   = new List<HitInfo>();
        var misses = new List<TargetCondition>();

        foreach (var target in targets)
        {
            var hit = allMatches.FirstOrDefault(x =>
                x.m.Mod.Group == target.GroupId &&
                (target.Mode == TierMatchMode.AtLeast
                    ? x.m.Mod.Tier >= target.Tier
                    : x.m.Mod.Tier == target.Tier));

            if (hit != default)
                hits.Add(new HitInfo(target, hit.line, hit.m.PrimaryValue, hit.m.Mod.Tier));
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
