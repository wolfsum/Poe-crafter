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

    // Jewel sub-types offered for a slot (jewel colours / abyss eyes / cluster
    // sizes differ per slot in PoE1). Default: the profile-wide list.
    public virtual IReadOnlyDictionary<JewelType, string> JewelTypesFor(ItemSlot slot) => JewelTypeDisplayNames;

    // Cluster jewel bases ("Life", "Fire Damage", …) for a given cluster size.
    public virtual IReadOnlyList<ClusterBase> ClusterBasesFor(JewelType size) => [];

    // Load auxiliary data files (e.g. ClusterJewels.lua) once dataDir is known.
    public virtual void LoadAuxData(string dataDir) { }

    // Whether in-game "(Tier: N)" annotations use the same ascending numbering
    // as the PoB mod ids. True in PoE2 (higher = better, matches DB); false in
    // PoE1 (game counts down from the top — rely on value ranges instead).
    public virtual bool UseAnnotationTiers => true;

    // Central weightKey-tag assembly for the current selection.
    public abstract IReadOnlySet<string> BuildTags(SlotSelection sel);

    // Whether mods from a given source (mod file name / "embedded" / "tablet")
    // may spawn on a slot. Needed because "default" weight keys are catch-alls
    // WITHIN their item domain only — without this, item mods with default>0
    // would leak into jewels and vice versa.
    public virtual bool SourceAllowed(string source, ItemSlot slot) => true;

    protected static readonly IReadOnlyDictionary<ArmourBase, string> EmptyArmour   = new Dictionary<ArmourBase, string>();
    protected static readonly IReadOnlyDictionary<JewelType, string>  EmptyJewel    = new Dictionary<JewelType, string>();
    protected static readonly IReadOnlyDictionary<TabletType, string> EmptyTablet   = new Dictionary<TabletType, string>();
    protected static readonly IReadOnlyDictionary<Influence, string>  EmptyInfluence = new Dictionary<Influence, string>();
}
