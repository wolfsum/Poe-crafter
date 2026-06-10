namespace Poe2Crafter.Core.Models;

// PoE2 jewel types. Tags verified against PoB2 ModJewel.lua weightKeys:
// strjewel / dexjewel / intjewel and *_radius_jewel for Time-Lost rares.
public enum JewelType
{
    None,
    Ruby,             // Str  → strjewel
    Emerald,          // Dex  → dexjewel
    Sapphire,         // Int  → intjewel
    TimeLostRuby,     // str_radius_jewel
    TimeLostEmerald,  // dex_radius_jewel
    TimeLostSapphire, // int_radius_jewel
}
