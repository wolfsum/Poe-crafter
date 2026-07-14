namespace Poe2Crafter.Core.Models;

// PoE1 item influences. PoB internal weight-tag names differ from display names
// (mapped inside Poe1Profile). PoE2 has no influences → always None.
public enum Influence
{
    None,
    Shaper,
    Elder,
    Crusader,
    Redeemer,
    Hunter,
    Warlord,
}
