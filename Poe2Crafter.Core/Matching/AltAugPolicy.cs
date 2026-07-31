using Poe2Crafter.Core.Parsing;

namespace Poe2Crafter.Core.Matching;

// Orb the auto-crafter should apply next (Alt+Aug mode). Chaos/Alt mode always
// uses UseAlt — the single calibrated currency slot.
public enum CraftAction { Stop, UseAlt, UseAug, Abort }

public static class AltAugPolicy
{
    // Decide the next orb after a clipboard evaluation.
    //
    // Aug only when the item is Magic, one affix SIDE is empty, and that empty
    // side is still needed (e.g. hunting a Prefix, item only has a Suffix → Aug).
    // Multi-line affixes (minion Attack+Cast Speed) count as ONE mod / one side
    // even though the clipboard shows two lines with independent rolls.
    public static CraftAction Decide(
        ParsedItem item,
        IReadOnlyList<TargetCondition> targets,
        MatchResult result,
        ModDatabase db)
    {
        if (result.AllMatched) return CraftAction.Stop;
        if (targets.Count == 0) return CraftAction.Abort;

        if (!IsMagic(item.Rarity))
            return CraftAction.Abort;

        var missSides = result.Misses
            .Select(m => ParseAffix(m.ModType))
            .Where(a => a != AffixType.Unknown)
            .ToHashSet();

        if (missSides.Count == 0)
            return CraftAction.UseAlt;

        var (hasPrefix, hasSuffix) = DetectFilledSides(item, db);

        // Empty side that we still need → Aug fills it. Do NOT use Mods.Count:
        // a dual-stat suffix is 2 clipboard lines but only one Suffix slot.
        if (!hasPrefix && hasSuffix && missSides.Contains(AffixType.Prefix))
            return CraftAction.UseAug;
        if (hasPrefix && !hasSuffix && missSides.Contains(AffixType.Suffix))
            return CraftAction.UseAug;

        return CraftAction.UseAlt;
    }

    public static string? AbortReason(ParsedItem item) =>
        IsMagic(item.Rarity) ? null : "Alt+Aug работает только на Magic-предметах";

    // Which Pref/Suf slots are occupied — uses MatchLines so dual-stat affixes
    // (Attack Speed + Cast Speed) resolve as a single Suffix, not two unknowns.
    private static (bool hasPrefix, bool hasSuffix) DetectFilledSides(ParsedItem item, ModDatabase db)
    {
        bool hasPrefix = false, hasSuffix = false;
        var covered = new bool[item.Mods.Count];

        foreach (var lm in db.MatchLines(item.Mods, item.ItemClass))
        {
            for (int k = 0; k < lm.LineCount && lm.Index + k < covered.Length; k++)
                covered[lm.Index + k] = true;

            var side = ParseAffix(lm.Match.Mod.Type);
            if (side == AffixType.Prefix) hasPrefix = true;
            if (side == AffixType.Suffix) hasSuffix = true;
        }

        // Advanced-desc annotations on lines the DB didn't claim
        for (int i = 0; i < item.Mods.Count; i++)
        {
            if (covered[i]) continue;
            var side = item.Mods[i].AffixType;
            if (side == AffixType.Prefix) hasPrefix = true;
            if (side == AffixType.Suffix) hasSuffix = true;
        }

        return (hasPrefix, hasSuffix);
    }

    private static bool IsMagic(string rarity) =>
        rarity.Equals("Magic", StringComparison.OrdinalIgnoreCase);

    private static AffixType ParseAffix(string? modType) =>
        modType?.Equals("Prefix", StringComparison.OrdinalIgnoreCase) == true ? AffixType.Prefix
      : modType?.Equals("Suffix", StringComparison.OrdinalIgnoreCase) == true ? AffixType.Suffix
      : AffixType.Unknown;
}
