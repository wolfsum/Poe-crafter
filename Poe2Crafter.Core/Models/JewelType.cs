namespace Poe2Crafter.Core.Models;

// Jewel sub-types. PoE2 tags verified against PoB2 ModJewel.lua weightKeys:
// strjewel / dexjewel / intjewel and *_radius_jewel for Time-Lost.
// Diamond rolls mods of all three colours; Time-Lost Diamond — all radius pools.
// Cluster* are PoE1 cluster jewel sizes (expansion_jewel_* weightKeys).
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
    ClusterSmall,     // PoE1: expansion_jewel_small
    ClusterMedium,    // PoE1: expansion_jewel_medium
    ClusterLarge,     // PoE1: expansion_jewel_large
    Crimson,          // PoE1 Str  → not_dex + not_int pools
    Viridian,         // PoE1 Dex  → not_str + not_int
    Cobalt,           // PoE1 Int  → not_str + not_dex
    Prismatic,        // PoE1 any colour → all three pools
    MurderousEye,     // PoE1 abyss → abyss_jewel_melee
    SearchingEye,     // PoE1 abyss → abyss_jewel_ranged
    HypnoticEye,      // PoE1 abyss → abyss_jewel_caster
    GhastlyEye,       // PoE1 abyss → abyss_jewel_summoner
}
