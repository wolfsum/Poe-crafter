namespace Poe2Crafter.Core.Matching;

public enum TierMatchMode { AtLeast, Exact }

public record TargetCondition(string GroupId, int Tier, string DisplayName, TierMatchMode Mode = TierMatchMode.AtLeast);

public record HitInfo(TargetCondition Target, string MatchedLine, double Value, int Tier);

public record MatchResult(
    bool HasItem,
    bool AllMatched,
    IReadOnlyList<HitInfo> Hits,
    IReadOnlyList<TargetCondition> Misses
)
{
    public static readonly MatchResult Idle =
        new(false, false, [], []);
}
