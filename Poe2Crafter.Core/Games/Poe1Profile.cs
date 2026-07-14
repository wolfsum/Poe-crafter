using Poe2Crafter.Core.Models;

namespace Poe2Crafter.Core.Games;

// Path of Exile 1 profile. PoB1 mod data uses the same Lua record format as
// PoB2, so the parser/matcher are reused unchanged — only slots, bases,
// influences and weightKey tags differ.
public sealed class Poe1Profile : GameProfile
{
    public override string Key  => "poe1";
    public override string Name => "Path of Exile 1";

    public override string PobFolderName => "Path of Building Community";
    public override IReadOnlyList<string> ModFiles =>
        ["ModItem.lua", "ModJewel.lua", "ModJewelAbyss.lua", "ModJewelCluster.lua", "ModVeiled.lua"];

    public override IReadOnlyList<ItemSlot> Slots =>
    [
        ItemSlot.Ring, ItemSlot.Amulet, ItemSlot.Belt, ItemSlot.Quiver,
        ItemSlot.Helmet, ItemSlot.Gloves, ItemSlot.Boots, ItemSlot.BodyArmour, ItemSlot.Shield,
        ItemSlot.Jewel, ItemSlot.AbyssJewel, ItemSlot.ClusterJewel,
        ItemSlot.Claw, ItemSlot.Dagger, ItemSlot.Wand, ItemSlot.Sceptre,
        ItemSlot.OneHandSword, ItemSlot.OneHandAxe, ItemSlot.OneHandMace,
        ItemSlot.Bow, ItemSlot.Staff,
        ItemSlot.TwoHandSword, ItemSlot.TwoHandAxe, ItemSlot.TwoHandMace,
    ];

    public override IReadOnlyDictionary<ItemSlot, string> SlotDisplayNames => new Dictionary<ItemSlot, string>
    {
        [ItemSlot.Ring]         = "Ring",
        [ItemSlot.Amulet]       = "Amulet",
        [ItemSlot.Belt]         = "Belt",
        [ItemSlot.Quiver]       = "Quiver",
        [ItemSlot.Helmet]       = "Helmet",
        [ItemSlot.Gloves]       = "Gloves",
        [ItemSlot.Boots]        = "Boots",
        [ItemSlot.BodyArmour]   = "Body Armour",
        [ItemSlot.Shield]       = "Shield",
        [ItemSlot.Jewel]        = "Jewel",
        [ItemSlot.AbyssJewel]   = "Abyss Jewel",
        [ItemSlot.ClusterJewel] = "Cluster Jewel",
        [ItemSlot.Claw]         = "Claw",
        [ItemSlot.Dagger]       = "Dagger",
        [ItemSlot.Wand]         = "Wand",
        [ItemSlot.Sceptre]      = "Sceptre",
        [ItemSlot.OneHandSword] = "One-Hand Sword",
        [ItemSlot.OneHandAxe]   = "One-Hand Axe",
        [ItemSlot.OneHandMace]  = "One-Hand Mace",
        [ItemSlot.Bow]          = "Bow",
        [ItemSlot.Staff]        = "Staff",
        [ItemSlot.TwoHandSword] = "Two-Hand Sword",
        [ItemSlot.TwoHandAxe]   = "Two-Hand Axe",
        [ItemSlot.TwoHandMace]  = "Two-Hand Mace",
    };

    public override IReadOnlyDictionary<ArmourBase, string> ArmourBaseDisplayNames => new Dictionary<ArmourBase, string>
    {
        [ArmourBase.None]   = "—",
        [ArmourBase.Str]    = "Armour (Str)",
        [ArmourBase.Dex]    = "Evasion (Dex)",
        [ArmourBase.Int]    = "Energy Shield (Int)",
        [ArmourBase.StrDex] = "Armour / Evasion",
        [ArmourBase.StrInt] = "Armour / ES",
        [ArmourBase.DexInt] = "Evasion / ES",
        [ArmourBase.All]    = "Armour / Evasion / ES",
    };

    public override IReadOnlyDictionary<JewelType, string> JewelTypeDisplayNames => new Dictionary<JewelType, string>
    {
        [JewelType.ClusterSmall]  = "Small Cluster",
        [JewelType.ClusterMedium] = "Medium Cluster",
        [JewelType.ClusterLarge]  = "Large Cluster",
    };

    public override IReadOnlyDictionary<Influence, string> InfluenceDisplayNames => new Dictionary<Influence, string>
    {
        [Influence.None]     = "— No influence",
        [Influence.Shaper]   = "Shaper",
        [Influence.Elder]    = "Elder",
        [Influence.Crusader] = "Crusader",
        [Influence.Redeemer] = "Redeemer",
        [Influence.Hunter]   = "Hunter",
        [Influence.Warlord]  = "Warlord",
    };

    // PoB internal weight-tag names for influences (verified against PoE Wiki)
    private static string InfluenceTag(Influence inf) => inf switch
    {
        Influence.Shaper   => "shaper",
        Influence.Elder    => "elder",
        Influence.Crusader => "crusader",
        Influence.Redeemer => "eyrie",
        Influence.Hunter   => "basilisk",
        Influence.Warlord  => "adjudicator",
        _                  => "",
    };

    private static readonly HashSet<ItemSlot> ArmourSlots =
        [ItemSlot.Helmet, ItemSlot.Gloves, ItemSlot.Boots, ItemSlot.BodyArmour, ItemSlot.Shield];

    // Jewels can't take influence; everything else in PoE1 can
    private static readonly HashSet<ItemSlot> NoInfluence =
        [ItemSlot.Jewel, ItemSlot.AbyssJewel, ItemSlot.ClusterJewel];

    public override bool ShowBaseFor(ItemSlot slot)      => ArmourSlots.Contains(slot);
    public override bool ShowJewelTypeFor(ItemSlot slot) => slot == ItemSlot.ClusterJewel;
    public override bool ShowInfluenceFor(ItemSlot slot) => !NoInfluence.Contains(slot);

    public override IReadOnlySet<string> BuildTags(SlotSelection sel)
    {
        var slotTags = SlotTags(sel.Slot).ToList();
        var tags = new HashSet<string>(slotTags);

        if (ArmourSlots.Contains(sel.Slot) && sel.ArmourBase != ArmourBase.None)
            foreach (var t in ArmourBaseTags(sel.ArmourBase)) tags.Add(t);

        // Influence tag is "{primarySlotTag}_{influence}", e.g. amulet_shaper,
        // body_armour_elder, sword_basilisk — primary is the first slot tag.
        if (!NoInfluence.Contains(sel.Slot) && sel.Influence != Influence.None && slotTags.Count > 0)
            tags.Add($"{slotTags[0]}_{InfluenceTag(sel.Influence)}");

        if (sel.Slot == ItemSlot.ClusterJewel)
        {
            tags.Add(sel.JewelType switch
            {
                JewelType.ClusterMedium => "expansion_jewel_medium",
                JewelType.ClusterLarge  => "expansion_jewel_large",
                _                       => "expansion_jewel_small",
            });
            // Notables are keyed by the jewel's base enchant type — include all
            // so every notable is listable; text matching stays exact anyway
            foreach (var t in AfflictionTags) tags.Add(t);
        }

        return tags;
    }

    // All notable pools from ModJewelCluster.lua (weightKey "affliction_*")
    private static readonly string[] AfflictionTags =
    [
        "affliction_area_damage", "affliction_armour", "affliction_attack_damage_",
        "affliction_attack_damage_while_dual_wielding_", "affliction_attack_damage_while_holding_a_shield",
        "affliction_axe_and_sword_damage", "affliction_bow_damage", "affliction_brand_damage",
        "affliction_chance_to_block", "affliction_chance_to_dodge_attacks", "affliction_channelling_skill_damage",
        "affliction_chaos_damage", "affliction_chaos_damage_over_time_multiplier", "affliction_chaos_resistance",
        "affliction_cold_damage", "affliction_cold_damage_over_time_multiplier", "affliction_cold_resistance",
        "affliction_critical_chance", "affliction_curse_effect_small", "affliction_dagger_and_claw_damage",
        "affliction_damage_over_time_multiplier", "affliction_damage_while_you_have_a_herald",
        "affliction_damage_with_two_handed_melee_weapons", "affliction_elemental_damage", "affliction_evasion",
        "affliction_fire_damage", "affliction_fire_damage_over_time_multiplier", "affliction_fire_resistance",
        "affliction_flask_duration", "affliction_life_and_mana_recovery_from_flasks", "affliction_lightning_damage",
        "affliction_lightning_resistance", "affliction_mace_and_staff_damage", "affliction_maximum_energy_shield",
        "affliction_maximum_life", "affliction_maximum_mana", "affliction_minion_damage",
        "affliction_minion_damage_while_you_have_a_herald", "affliction_minion_life", "affliction_physical_damage",
        "affliction_physical_damage_over_time_multiplier", "affliction_projectile_damage",
        "affliction_reservation_efficiency_small", "affliction_spell_damage", "affliction_totem_damage",
        "affliction_trap_and_mine_damage", "affliction_wand_damage", "affliction_warcry_buff_effect",
    ];

    private static IEnumerable<string> SlotTags(ItemSlot slot) => slot switch
    {
        ItemSlot.Ring         => ["ring"],
        ItemSlot.Amulet       => ["amulet"],
        ItemSlot.Belt         => ["belt"],
        ItemSlot.Quiver       => ["quiver"],
        ItemSlot.Helmet       => ["helmet", "armour"],
        ItemSlot.Gloves       => ["gloves", "armour"],
        ItemSlot.Boots        => ["boots", "armour"],
        ItemSlot.BodyArmour   => ["body_armour", "armour"],
        ItemSlot.Shield       => ["shield", "armour"],
        ItemSlot.Jewel        => ["jewel"],
        ItemSlot.AbyssJewel   => ["abyss_jewel"],
        ItemSlot.Claw         => ["claw", "one_hand_weapon", "weapon"],
        ItemSlot.Dagger       => ["dagger", "one_hand_weapon", "weapon"],
        ItemSlot.Wand         => ["wand", "one_hand_weapon", "weapon"],
        ItemSlot.Sceptre      => ["sceptre", "one_hand_weapon", "weapon"],
        ItemSlot.OneHandSword => ["sword", "one_hand_weapon", "weapon"],
        ItemSlot.OneHandAxe   => ["axe", "one_hand_weapon", "weapon"],
        ItemSlot.OneHandMace  => ["mace", "one_hand_weapon", "weapon"],
        ItemSlot.Bow          => ["bow", "two_hand_weapon", "weapon"],
        ItemSlot.Staff        => ["staff", "two_hand_weapon", "weapon"],
        ItemSlot.TwoHandSword => ["sword", "two_hand_weapon", "weapon"],
        ItemSlot.TwoHandAxe   => ["axe", "two_hand_weapon", "weapon"],
        ItemSlot.TwoHandMace  => ["mace", "two_hand_weapon", "weapon"],
        _                     => [],
    };

    private static IEnumerable<string> ArmourBaseTags(ArmourBase armourBase) => armourBase switch
    {
        ArmourBase.Str    => ["str_armour"],
        ArmourBase.Dex    => ["dex_armour"],
        ArmourBase.Int    => ["int_armour"],
        ArmourBase.StrDex => ["str_dex_armour"],
        ArmourBase.StrInt => ["str_int_armour"],
        ArmourBase.DexInt => ["dex_int_armour"],
        ArmourBase.All    => ["str_dex_int_armour"],
        _                 => [],
    };
}
