using Poe2Crafter.Core.Games;
using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Parsing;

var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

foreach (var profile in GameProfiles.All)
{
    Console.WriteLine($"\n════════ {profile.Name} ({profile.Key}) ════════");
    var dataDir = Path.Combine(appData, profile.PobFolderName, "Data");
    if (!Directory.Exists(dataDir)) { Console.WriteLine($"  !! PoB data not found: {dataDir}"); continue; }

    var mods = new List<ModDefinition>();
    foreach (var f in profile.ModFiles)
    {
        var p = Path.Combine(dataDir, f);
        if (File.Exists(p)) mods.AddRange(PobModParser.ParseFile(p));
        else Console.WriteLine($"  !! missing {f}");
    }
    mods.AddRange(profile.EmbeddedMods);

    var db = new ModDatabase(mods);
    Console.WriteLine($"Mods loaded: {mods.Count}, slots: {profile.Slots.Count}");

    foreach (var slot in profile.Slots)
    {
        var sel = new SlotSelection(slot,
            profile.ShowBaseFor(slot) ? ArmourBase.Str : ArmourBase.None,
            profile.ShowJewelTypeFor(slot) ? profile.JewelTypeDisplayNames.Keys.First() : JewelType.None,
            profile.ShowTabletTypeFor(slot) ? profile.TabletTypeDisplayNames.Keys.First() : TabletType.None);
        var n = db.GetGroups(profile, sel).Count;
        var flag = n == 0 ? "  !! ZERO" : "";
        Console.WriteLine($"  {profile.SlotDisplayNames[slot],-16} {n,4} groups{flag}");
    }

    if (profile.Key == "poe1")
    {
        // Influence narrows/extends the pool
        var plain   = db.GetGroups(profile, new SlotSelection(ItemSlot.Amulet)).Count;
        var shaper  = db.GetGroups(profile, new SlotSelection(ItemSlot.Amulet, Influence: Influence.Shaper)).Count;
        var hunter  = db.GetGroups(profile, new SlotSelection(ItemSlot.Amulet, Influence: Influence.Hunter)).Count;
        Console.WriteLine($"\n  Amulet plain={plain}  +Shaper={shaper}  +Hunter={hunter} (influences must add groups)");

        // PoE1 clipboard sample: rare amulet with a shaper mod, tier annotations
        var clip = """
Item Class: Amulets
Rarity: Rare
Gale Locket
Onyx Amulet
--------
Requirements:
Level: 60
--------
Item Level: 84
--------
{ Implicit Modifier — Attribute }
+16 to all Attributes (implicit)
--------
{ Prefix Modifier "Rotund" (Tier: 4) — Life }
+70(65-74) to maximum Life
{ Suffix Modifier "of the Gale" (Tier: 2) — Elemental, Cold, Resistance }
+41(36-41)% to Cold Resistance
--------
Corrupted
""";
        var item = ItemParser.TryParse(clip);
        Console.WriteLine($"\n  PoE1 clipboard: {(item is null ? "NULL!" : $"{item.Mods.Count} mods (implicit excluded)")}");
        if (item != null)
            foreach (var m in item.Mods)
                Console.WriteLine($"    '{m.Text}' (annTier={m.Tier}) -> {db.Match(m.Text).Count} match(es)");
    }

    if (profile.Key == "poe2")
    {
        var clip = """
Item Class: Body Armours
Rarity: Rare
Doom Shelter
Full Plate
--------
Item Level: 82
--------
{ Prefix Modifier "Hale" (Tier: 5) — Life }
+62(60-80) to maximum Life
""";
        var item = ItemParser.TryParse(clip);
        Console.WriteLine($"\n  PoE2 clipboard: {(item is null ? "NULL!" : $"{item.Mods.Count} mods")}");
        if (item != null)
            foreach (var m in item.Mods)
                Console.WriteLine($"    '{m.Text}' (annTier={m.Tier}) -> {db.Match(m.Text).Count} match(es)");
    }
}
