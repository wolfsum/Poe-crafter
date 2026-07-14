using Poe2Crafter.Core.Models;

namespace Poe2Crafter.Core.Games;

// Everything game-specific lives behind this contract: where the PoB data is,
// which mod files/embedded mods feed the DB, which slots/dimensions the UI
// offers, and how a user's SlotSelection maps to weightKey tags.
public abstract class GameProfile
{
    public abstract string Key  { get; } // "poe1" / "poe2" — persisted in settings
    public abstract string Name { get; } // human label for UI

    // PoB "Data" folder name under %AppData%, and the mod files to load from it.
    public abstract string PobFolderName { get; }
    public abstract IReadOnlyList<string> ModFiles { get; }

    // Mods not present in PoB data (e.g. PoE2 tablets), merged into the DB.
    public virtual IReadOnlyList<ModDefinition> EmbeddedMods => [];

    // Slots offered, in the order/where relevant. Display names for each choice.
    public abstract IReadOnlyList<ItemSlot> Slots { get; }
    public abstract IReadOnlyDictionary<ItemSlot, string> SlotDisplayNames { get; }
    public virtual IReadOnlyDictionary<ArmourBase, string> ArmourBaseDisplayNames => EmptyArmour;
    public virtual IReadOnlyDictionary<JewelType, string>  JewelTypeDisplayNames  => EmptyJewel;
    public virtual IReadOnlyDictionary<TabletType, string> TabletTypeDisplayNames => EmptyTablet;
    public virtual IReadOnlyDictionary<Influence, string>  InfluenceDisplayNames  => EmptyInfluence;

    // Which extra dimension combobox to show for a given slot.
    public abstract bool ShowBaseFor(ItemSlot slot);
    public virtual bool ShowJewelTypeFor(ItemSlot slot)  => false;
    public virtual bool ShowTabletTypeFor(ItemSlot slot) => false;
    public virtual bool ShowInfluenceFor(ItemSlot slot)  => false;

    // Central weightKey-tag assembly for the current selection.
    public abstract IReadOnlySet<string> BuildTags(SlotSelection sel);

    protected static readonly IReadOnlyDictionary<ArmourBase, string> EmptyArmour   = new Dictionary<ArmourBase, string>();
    protected static readonly IReadOnlyDictionary<JewelType, string>  EmptyJewel    = new Dictionary<JewelType, string>();
    protected static readonly IReadOnlyDictionary<TabletType, string> EmptyTablet   = new Dictionary<TabletType, string>();
    protected static readonly IReadOnlyDictionary<Influence, string>  EmptyInfluence = new Dictionary<Influence, string>();
}
