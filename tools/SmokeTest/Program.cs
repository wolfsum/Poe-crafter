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
{ Prefix Modifier "Flowing" (Tier: 1) }
Notable Passive Skills in Radius also grant 5(3-7)% increased Critical Hit Chance
{ Suffix Modifier "of Precision" (Tier: 1) }
Small Passive Skills in Radius also grant 2(1-2)% increased Accuracy Rating
--------
Place into an allocated Jewel Socket on the Passive Skill Tree.
""";

PrintParse("Time-Lost Emerald (annotated)", clip);

// Rare with annotations, implicit, enchant and rune — only prefix/suffix must count
var clip2 = """
Item Class: Body Armours
Rarity: Rare
Doom Shelter
Full Plate
--------
Armour: 270 (augmented)
--------
Item Level: 82
--------
{ Implicit Modifier }
+25(20-30) to Spirit (implicit)
--------
{ Rune Modifier }
+12% to Fire Resistance (rune)
{ Prefix Modifier "Hale" (Tier: 5) — Life }
+62(60-80) to maximum Life
{ Suffix Modifier "of the Span" (Tier: 3) — Elemental, Resistance }
+10(5-10)% to all Elemental Resistances
--------
Corrupted
""";

PrintParse("Rare armour (annotated, implicit+rune)", clip2);

// Plain copy without annotations (fallback path)
var clip3 = """
Item Class: Body Armours
Rarity: Rare
Doom Shelter
Full Plate
--------
Item Level: 82
--------
+12% to Fire Resistance (rune)
+62 to maximum Life
""";

PrintParse("Rare armour (plain copy)", clip3);

void PrintParse(string title, string text)
{
    var item = ItemParser.TryParse(text);
    Console.WriteLine($"\n=== {title}: {(item is null ? "NULL!" : $"{item.Mods.Count} mod lines")} ===");
    if (item is null) return;
    foreach (var mod in item.Mods)
    {
        var matches = db.Match(mod.Text);
        Console.WriteLine($"  '{mod.Text}' (annTier={mod.Tier}) -> {matches.Count} match(es)");
        foreach (var m in matches.Take(3))
            Console.WriteLine($"      [{m.Mod.Group}] dbT{m.Mod.Tier} {m.Mod.Template}");
    }
}
