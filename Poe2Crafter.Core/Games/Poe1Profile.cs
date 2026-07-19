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

    // PoE1 game tiers count down from the top — annotation numbers don't match
    // PoB's ascending ids, so tier inference falls back to value ranges
    public override bool UseAnnotationTiers => false;

    // Mods PoB models in code rather than data: the cluster passive-effect
    // prefix. (Jewel-socket prefix is legacy — sockets are inherent since 3.17.)
    public override IReadOnlyList<ModDefinition> EmbeddedMods
    {
        get
        {
            ModDefinition Make(string id, string affix, string template, int tier, int lvl) => new()
            {
                Id = id, Type = "Prefix", AffixName = affix,
                Template = template, Templates = [template],
                MatchRegex = Parsing.PobModParser.BuildMatchRegex(template),
                Group = "AfflictionJewelSmallPassivesHaveIncreasedEffect",
                Tier = tier, MinLevel = lvl, Source = "embedded",
                ItemTags = ["expansion_jewel_small", "expansion_jewel_medium", "expansion_jewel_large"],
            };

            return
            [
                Make("ClusterSmallPassiveEffect1", "Potent",   "Added Small Passive Skills have 25% increased Effect", 1, 1),
                Make("ClusterSmallPassiveEffect2", "Powerful", "Added Small Passive Skills have 35% increased Effect", 2, 84),
            ];
        }
    }

    public override IReadOnlyList<ItemSlot> Slots =>
    [
        ItemSlot.Ring, ItemSlot.Amulet, ItemSlot.Belt, ItemSlot.Quiver,
        ItemSlot.Helmet, ItemSlot.Gloves, ItemSlot.Boots, ItemSlot.BodyArmour, ItemSlot.Shield,
        ItemSlot.Jewel, ItemSlot.AbyssJewel, ItemSlot.ClusterJewel,
        ItemSlot.Claw, ItemSlot.Dagger, ItemSlot.Wand, ItemSlot.ConvokingWand, ItemSlot.Sceptre,
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
        [ItemSlot.ConvokingWand] = "Wand (Convoking)",
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

    public override IReadOnlyDictionary<JewelType, string> JewelTypesFor(ItemSlot slot) => slot switch
    {
        ItemSlot.Jewel => new Dictionary<JewelType, string>
        {
            [JewelType.Crimson]   = "Crimson (Str)",
            [JewelType.Viridian]  = "Viridian (Dex)",
            [JewelType.Cobalt]    = "Cobalt (Int)",
            [JewelType.Prismatic] = "Prismatic (all)",
        },
        ItemSlot.AbyssJewel => new Dictionary<JewelType, string>
        {
            [JewelType.MurderousEye] = "Murderous Eye (melee)",
            [JewelType.SearchingEye] = "Searching Eye (ranged)",
            [JewelType.HypnoticEye]  = "Hypnotic Eye (caster)",
            [JewelType.GhastlyEye]   = "Ghastly Eye (minion)",
        },
        ItemSlot.ClusterJewel => new Dictionary<JewelType, string>
        {
            [JewelType.ClusterSmall]  = "Small Cluster",
            [JewelType.ClusterMedium] = "Medium Cluster",
            [JewelType.ClusterLarge]  = "Large Cluster",
        },
        _ => EmptyJewel,
    };

    // Parsed from ClusterJewels.lua by LoadAuxData
    private readonly Dictionary<JewelType, List<ClusterBase>> _clusterBases = [];

    public override IReadOnlyList<ClusterBase> ClusterBasesFor(JewelType size) =>
        _clusterBases.TryGetValue(size, out var list) ? list : [];

    public override void LoadAuxData(string dataDir)
    {
        var path = Path.Combine(dataDir, "ClusterJewels.lua");
        if (!File.Exists(path)) return;

        _clusterBases.Clear();
        JewelType? size = null;
        string? pendingTag = null;

        var sizeRegex = new System.Text.RegularExpressions.Regex(@"\[""(Small|Medium|Large) Cluster Jewel""\]");
        var tagRegex  = new System.Text.RegularExpressions.Regex(@"^\s*\[""(?<t>affliction_[a-z_]+)""\]\s*=\s*\{");
        var nameRegex = new System.Text.RegularExpressions.Regex(@"^\s*name\s*=\s*""(?<n>[^""]+)""");

        foreach (var line in File.ReadLines(path))
        {
            var sm = sizeRegex.Match(line);
            if (sm.Success)
            {
                size = sm.Groups[1].Value switch
                {
                    "Small"  => JewelType.ClusterSmall,
                    "Medium" => JewelType.ClusterMedium,
                    _        => JewelType.ClusterLarge,
                };
                pendingTag = null;
                continue;
            }

            var tm = tagRegex.Match(line);
            if (tm.Success) { pendingTag = tm.Groups["t"].Value; continue; }

            var nm = nameRegex.Match(line);
            if (nm.Success && size is not null && pendingTag is not null)
            {
                var list = _clusterBases.TryGetValue(size.Value, out var l) ? l : _clusterBases[size.Value] = [];
                if (!list.Any(b => b.Tag == pendingTag))
                    list.Add(new ClusterBase(nm.Groups["n"].Value, pendingTag));
                pendingTag = null;
            }
        }

        foreach (var list in _clusterBases.Values)
            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
    }

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

    // "default" weight keys are catch-alls within one item domain — keep item,
    // jewel, abyss and cluster pools from leaking into each other. (ModJewel
    // also feeds abyss jewels: its keys reference "abyss_jewel" explicitly.)
    public override bool SourceAllowed(string source, ItemSlot slot) => slot switch
    {
        _ when source == "embedded" => true, // tag-gated explicitly, no default key
        ItemSlot.Jewel        => source == "ModJewel.lua",
        ItemSlot.AbyssJewel   => source is "ModJewel.lua" or "ModJewelAbyss.lua",
        ItemSlot.ClusterJewel => source == "ModJewelCluster.lua",
        _                     => source is "ModItem.lua" or "ModVeiled.lua",
    };

    // Delve* (fossil-only) and Necropolis* (3.24 coffins, content removed) mods
    // keep nonzero spawn weights in PoB data because those craft systems reuse
    // the weight tables — but chaos/alt/essence rolling can never hit them.
    // Verified against poedb: filtering these makes pool weights match exactly.
    public override bool ModAllowed(ModDefinition mod) =>
        !mod.Id.StartsWith("Delve", StringComparison.Ordinal) &&
        !mod.Id.StartsWith("Necropolis", StringComparison.Ordinal);

    public override bool ShowBaseFor(ItemSlot slot)      => ArmourSlots.Contains(slot);
    public override bool ShowJewelTypeFor(ItemSlot slot) =>
        slot is ItemSlot.Jewel or ItemSlot.AbyssJewel or ItemSlot.ClusterJewel;
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

        // Base jewels: colour pools are expressed as "not_<other colour>" keys —
        // e.g. a "not_int" mod rolls on Crimson and Viridian. Prismatic has all.
        if (sel.Slot == ItemSlot.Jewel)
            foreach (var t in sel.JewelType switch
            {
                JewelType.Crimson   => new[] { "not_dex", "not_int" },
                JewelType.Viridian  => ["not_str", "not_int"],
                JewelType.Cobalt    => ["not_str", "not_dex"],
                _                   => ["not_str", "not_dex", "not_int"], // Prismatic / unset
            }) tags.Add(t);

        if (sel.Slot == ItemSlot.AbyssJewel)
            tags.Add(sel.JewelType switch
            {
                JewelType.SearchingEye => "abyss_jewel_ranged",
                JewelType.HypnoticEye  => "abyss_jewel_caster",
                JewelType.GhastlyEye   => "abyss_jewel_summoner",
                _                      => "abyss_jewel_melee", // Murderous / unset
            });

        if (sel.Slot == ItemSlot.ClusterJewel)
        {
            tags.Add(sel.JewelType switch
            {
                JewelType.ClusterMedium => "expansion_jewel_medium",
                JewelType.ClusterLarge  => "expansion_jewel_large",
                _                       => "expansion_jewel_small",
            });
            // Notable pool is defined by the jewel's base enchant type
            if (sel.ClusterBase is not null)
                tags.Add(sel.ClusterBase);
            else
                foreach (var b in ClusterBasesFor(sel.JewelType)) tags.Add(b.Tag);
        }

        return tags;
    }


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
        // Convoking Wand: normal wand pool + minion mods gated by the base tag
        ItemSlot.ConvokingWand => ["wand", "one_hand_weapon", "weapon", "weapon_can_roll_minion_modifiers"],
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
