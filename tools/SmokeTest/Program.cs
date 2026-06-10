using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Parsing;

var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Path of Building Community (PoE2)", "Data");

var mods = new List<ModDefinition>();
foreach (var f in new[] { "ModItem.lua", "ModJewel.lua" })
    mods.AddRange(PobModParser.ParseFile(Path.Combine(dataDir, f)));

var db = new ModDatabase(mods);
Console.WriteLine($"Total mods loaded: {mods.Count}");

foreach (var jt in new[] { JewelType.Ruby, JewelType.Emerald, JewelType.Sapphire, JewelType.Diamond,
                           JewelType.TimeLostRuby, JewelType.TimeLostEmerald, JewelType.TimeLostSapphire, JewelType.TimeLostDiamond })
{
    var groups = db.GetGroups(ItemSlot.Jewel, jewelType: jt);
    Console.WriteLine($"\n=== {jt}: {groups.Count} groups ===");
    foreach (var g in groups.Take(4))
        Console.WriteLine($"  {g.DisplayName}");
}

// Simulate real PoE2 clipboard: a Time-Lost Emerald with range hints
var clip = """
Item Class: Jewels
Rarity: Rare
Entropy Bliss
Time-Lost Emerald
--------
Radius: Small
--------
Item Level: 81
--------
Notable Passive Skills in Radius also grant 5(3-7)% increased Critical Hit Chance
Small Passive Skills in Radius also grant 2(1-2)% increased Accuracy Rating
--------
Place into an allocated Jewel Socket on the Passive Skill Tree.
""";

var item = ItemParser.TryParse(clip);
Console.WriteLine($"\n=== Clipboard parse: {(item is null ? "NULL!" : $"{item.ModLines.Length} mod lines")} ===");
if (item != null)
{
    foreach (var line in item.ModLines)
    {
        var matches = db.Match(line);
        Console.WriteLine($"  '{line}' -> {matches.Count} match(es)");
        foreach (var m in matches.Take(3))
            Console.WriteLine($"      [{m.Mod.Group}] T{m.Mod.Tier} {m.Mod.Template}");
    }
}

// And a regular rare with range hints (the original "missed mods" bug)
var clip2 = """
Item Class: Body Armours
Rarity: Rare
Doom Shelter
Full Plate
--------
Armour: 270
--------
Item Level: 82
--------
+62(60-80) to maximum Life
+10(5-10)% to all Elemental Resistances
""";

var item2 = ItemParser.TryParse(clip2);
Console.WriteLine($"\n=== Rare armour parse: {(item2 is null ? "NULL!" : $"{item2.ModLines.Length} mod lines")} ===");
if (item2 != null)
{
    foreach (var line in item2.ModLines)
    {
        var matches = db.Match(line);
        Console.WriteLine($"  '{line}' -> {matches.Count} match(es)");
        foreach (var m in matches.Take(3))
            Console.WriteLine($"      [{m.Mod.Group}] T{m.Mod.Tier}");
    }
}
