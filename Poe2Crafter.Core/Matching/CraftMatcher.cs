using Poe2Crafter.Core.Parsing;

namespace Poe2Crafter.Core.Matching;

public class CraftMatcher(ModDatabase db, bool useAnnotationTiers = true)
{
    public MatchResult Check(ParsedItem item, IReadOnlyList<TargetCondition> targets)
    {
        if (targets.Count == 0)
            return new MatchResult(true, false, [], []);

        var hits   = new List<HitInfo>();
        var misses = new List<TargetCondition>();

        // Match each target by its GroupId directly. Do NOT assign each clipboard
        // line to a single global winner first — the same text often exists in
        // both gear and jewel/abyss pools under different groups (e.g. +mana →
        // IncreasedMana vs AbyssJewelMana), and the wrong winner made targets
        // miss systematically.
        foreach (var target in targets)
        {
            var lm = db.MatchGroup(item.Mods, target.GroupId, item.ItemClass);
            if (lm is null)
            {
                misses.Add(target);
                continue;
            }

            int tier = useAnnotationTiers && lm.FirstLine.Tier > 0
                ? lm.FirstLine.Tier
                : lm.Match.Mod.Tier;

            // Untiered jewel mods (Tier 0 in PoB) always pass the tier check —
            // the UI still shows a single "tier" row but there's nothing to compare.
            bool tierOk = target.Tier == 0
                || lm.Match.Mod.Tier == 0
                || (target.Mode == TierMatchMode.AtLeast ? tier >= target.Tier : tier == target.Tier);

            if (!tierOk)
            {
                misses.Add(target);
                continue;
            }

            var text = lm.DisplayText
                ?? (lm.LineCount == 1
                    ? lm.FirstLine.Text
                    : string.Join(" / ", item.Mods.Skip(lm.Index).Take(lm.LineCount).Select(x => x.Text)));

            hits.Add(new HitInfo(target, text, lm.Match.PrimaryValue, tier));
        }

        return new MatchResult(
            HasItem:    true,
            AllMatched: misses.Count == 0,
            Hits:       hits,
            Misses:     misses);
    }
}
