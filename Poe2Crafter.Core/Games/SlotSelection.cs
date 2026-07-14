using Poe2Crafter.Core.Models;

namespace Poe2Crafter.Core.Games;

// The full set of user choices that select which mod pool applies.
// Fields not relevant to a slot/game stay at their None default.
public record SlotSelection(
    ItemSlot Slot,
    ArmourBase ArmourBase = ArmourBase.None,
    JewelType JewelType = JewelType.None,
    TabletType TabletType = TabletType.None,
    Influence Influence = Influence.None,
    string? ClusterBase = null); // affliction_* tag of the cluster jewel base

// A PoE1 cluster jewel base type ("Life" → affliction_maximum_life)
public record ClusterBase(string Name, string Tag)
{
    public override string ToString() => Name;
}
