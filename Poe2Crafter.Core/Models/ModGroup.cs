using System.Text.RegularExpressions;

namespace Poe2Crafter.Core.Models;

public class ModGroup
{
    private static readonly Regex RangePattern = new(@"\(\d+(?:\.\d+)?-\d+(?:\.\d+)?\)", RegexOptions.Compiled);

    public string GroupId { get; }
    public string DisplayName { get; }     // "+#% to Fire Resistance"
    public string ModType { get; }         // "Prefix" / "Suffix"
    public IReadOnlyList<ModDefinition> Tiers { get; }  // best tier first

    public ModGroup(string groupId, IEnumerable<ModDefinition> tiers)
    {
        GroupId = groupId;
        Tiers = tiers.OrderByDescending(t => t.Tier).ToList();
        var representative = Tiers[0];
        DisplayName = string.Join(" / ", representative.Templates.Select(t => RangePattern.Replace(t, "#")));
        ModType = representative.Type;
    }
}
