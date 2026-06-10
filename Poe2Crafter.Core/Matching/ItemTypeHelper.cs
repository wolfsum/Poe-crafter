using Poe2Crafter.Core.Models;

namespace Poe2Crafter.Core.Matching;

public static class ItemTypeHelper
{
    public static readonly IReadOnlyDictionary<ItemSlot, string> SlotDisplayNames =
        new Dictionary<ItemSlot, string>
        {
            [ItemSlot.Ring]      = "Ring",
            [ItemSlot.Amulet]    = "Amulet",
            [ItemSlot.Belt]      = "Belt",
            [ItemSlot.Talisman]  = "Talisman",
            [ItemSlot.Quiver]    = "Quiver",
            [ItemSlot.Focus]     = "Focus",
            [ItemSlot.Jewel]     = "Jewel",
            [ItemSlot.Helmet]    = "Helmet",
            [ItemSlot.Gloves]    = "Gloves",
            [ItemSlot.Boots]     = "Boots",
            [ItemSlot.BodyArmour]= "Body Armour",
            [ItemSlot.Shield]    = "Shield",
            [ItemSlot.Mace]      = "Mace",
            [ItemSlot.Axe]       = "Axe",
            [ItemSlot.Sword]     = "Sword",
            [ItemSlot.Spear]     = "Spear",
            [ItemSlot.Flail]     = "Flail",
            [ItemSlot.Crossbow]  = "Crossbow",
            [ItemSlot.Bow]       = "Bow",
            [ItemSlot.Dagger]    = "Dagger",
            [ItemSlot.Claw]      = "Claw",
            [ItemSlot.Wand]      = "Wand",
            [ItemSlot.Sceptre]   = "Sceptre",
            [ItemSlot.Staff]     = "Staff",
            [ItemSlot.Warstaff]  = "Warstaff",
            [ItemSlot.Trap]      = "Trap",
        };

    public static readonly IReadOnlyDictionary<ArmourBase, string> ArmourBaseDisplayNames =
        new Dictionary<ArmourBase, string>
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

    public static readonly IReadOnlyDictionary<JewelType, string> JewelTypeDisplayNames =
        new Dictionary<JewelType, string>
        {
            [JewelType.Ruby]             = "Ruby (Str)",
            [JewelType.Emerald]          = "Emerald (Dex)",
            [JewelType.Sapphire]         = "Sapphire (Int)",
            [JewelType.TimeLostRuby]     = "Time-Lost Ruby",
            [JewelType.TimeLostEmerald]  = "Time-Lost Emerald",
            [JewelType.TimeLostSapphire] = "Time-Lost Sapphire",
        };

    // Armour slots that need a base type selection
    public static readonly IReadOnlySet<ItemSlot> ArmourSlots = new HashSet<ItemSlot>
    {
        ItemSlot.Helmet, ItemSlot.Gloves, ItemSlot.Boots,
        ItemSlot.BodyArmour, ItemSlot.Shield,
    };

    // Jewel slot that needs a type selection
    public static readonly IReadOnlySet<ItemSlot> JewelSlots = new HashSet<ItemSlot> { ItemSlot.Jewel };

    public static IReadOnlySet<string> GetTags(ItemSlot slot, ArmourBase armourBase = ArmourBase.None, JewelType jewelType = JewelType.None)
    {
        var tags = new HashSet<string>(SlotTags(slot));

        if (ArmourSlots.Contains(slot) && armourBase != ArmourBase.None)
            foreach (var t in ArmourBaseTags(armourBase))
                tags.Add(t);

        if (JewelSlots.Contains(slot) && jewelType != JewelType.None)
            foreach (var t in JewelTypeTags(jewelType))
                tags.Add(t);

        return tags;
    }

    private static IEnumerable<string> SlotTags(ItemSlot slot) => slot switch
    {
        ItemSlot.Ring       => ["ring"],
        ItemSlot.Amulet     => ["amulet"],
        ItemSlot.Belt       => ["belt"],
        ItemSlot.Talisman   => ["talisman"],
        ItemSlot.Quiver     => ["quiver"],
        ItemSlot.Focus      => ["focus"],
        ItemSlot.Jewel      => ["jewel"],
        ItemSlot.Helmet     => ["helmet", "armour"],
        ItemSlot.Gloves     => ["gloves", "armour"],
        ItemSlot.Boots      => ["boots", "armour"],
        ItemSlot.BodyArmour => ["body_armour", "armour"],
        ItemSlot.Shield     => ["shield", "str_shield", "str_dex_shield", "str_int_shield", "armour"],
        ItemSlot.Mace       => ["mace", "one_hand_weapon", "weapon"],
        ItemSlot.Axe        => ["axe", "one_hand_weapon", "weapon"],
        ItemSlot.Sword      => ["sword", "one_hand_weapon", "weapon"],
        ItemSlot.Spear      => ["spear", "two_hand_weapon", "weapon"],
        ItemSlot.Flail      => ["flail", "one_hand_weapon", "weapon"],
        ItemSlot.Crossbow   => ["crossbow", "two_hand_weapon", "weapon", "ranged"],
        ItemSlot.Bow        => ["bow", "two_hand_weapon", "weapon", "ranged"],
        ItemSlot.Dagger     => ["dagger", "one_hand_weapon", "weapon"],
        ItemSlot.Claw       => ["claw", "one_hand_weapon", "weapon"],
        ItemSlot.Wand       => ["wand", "one_hand_weapon", "weapon"],
        ItemSlot.Sceptre    => ["sceptre", "one_hand_weapon", "weapon"],
        ItemSlot.Staff      => ["staff", "two_hand_weapon", "weapon"],
        ItemSlot.Warstaff   => ["warstaff", "two_hand_weapon", "weapon"],
        ItemSlot.Trap       => ["trap"],
        _                   => [],
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

    // Tags from PoB2 ModJewel.lua weightKeys
    private static IEnumerable<string> JewelTypeTags(JewelType jewelType) => jewelType switch
    {
        JewelType.Ruby             => ["strjewel"],
        JewelType.Emerald          => ["dexjewel"],
        JewelType.Sapphire         => ["intjewel"],
        JewelType.TimeLostRuby     => ["str_radius_jewel", "radius_jewel"],
        JewelType.TimeLostEmerald  => ["dex_radius_jewel", "radius_jewel"],
        JewelType.TimeLostSapphire => ["int_radius_jewel", "radius_jewel"],
        _                          => [],
    };
}
