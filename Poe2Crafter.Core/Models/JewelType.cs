namespace Poe2Crafter.Core.Models;

// PoE2 jewel types. Tags verified against PoB2 ModJewel.lua weightKeys:
// strjewel / dexjewel / intjewel and *_radius_jewel for Time-Lost.
// Diamond rolls mods of all three colours; Time-Lost Diamond — all radius pools.
public enum JewelType
{
    None,
    Ruby,             // Str  → strjewel
    Emerald,          // Dex  → dexjewel
    Sapphire,         // Int  → intjewel
    Diamond,          // strjewel + dexjewel + intjewel
    TimeLostRuby,     // str_radius_jewel
    TimeLostEmerald,  // dex_radius_jewel
    TimeLostSapphire, // int_radius_jewel
    TimeLostDiamond,  // all radius pools
}
