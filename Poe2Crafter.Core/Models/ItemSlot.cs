namespace Poe2Crafter.Core.Models;

public enum ItemSlot
{
    // Accessories
    Ring,
    Amulet,
    Belt,
    Talisman,
    Quiver,
    Focus,
    Jewel,
    Tablet,

    // Armour slots (need ArmourBase)
    Helmet,
    Gloves,
    Boots,
    BodyArmour,
    Shield,

    // Weapons
    Mace,
    Axe,
    Sword,
    Spear,
    Flail,
    Crossbow,
    Bow,
    Dagger,
    Claw,
    Wand,
    Sceptre,
    Staff,
    Warstaff,
    Trap,

    // PoE1-specific: 1h/2h weapons are distinct mod pools, abyss jewels
    OneHandSword,
    OneHandAxe,
    OneHandMace,
    TwoHandSword,
    TwoHandAxe,
    TwoHandMace,
    AbyssJewel,
    ClusterJewel,
}
