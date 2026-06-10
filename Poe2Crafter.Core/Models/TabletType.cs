namespace Poe2Crafter.Core.Models;

// PoE2 precursor tablet types (mod pools embedded from poe2wiki — absent in PoB2 data)
// Expedition tablets are absent in the current patch — re-add if they return
public enum TabletType
{
    None,
    Irradiated,
    Abyss,
    Breach,
    Delirium,
    Overseer,
    Ritual,
    Temple,
}
