using System.IO;
using System.Windows;
using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Parsing;
using Poe2Crafter.ViewModels;

namespace Poe2Crafter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Only craftable-mod files — the Data folder also holds huge irrelevant
        // files (QueryMods, ModItemExclusive…) that bloat the DB and slow matching
        string[] modFiles = ["ModItem.lua", "ModJewel.lua"];

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Path of Building Community (PoE2)", "Data");

        if (!File.Exists(Path.Combine(dataDir, "ModItem.lua")))
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title    = "Locate ModItem.lua (PoB2 Data folder)",
                Filter   = "Lua files (*.lua)|*.lua",
                FileName = "ModItem.lua",
            };
            if (dlg.ShowDialog() != true) { Shutdown(); return; }
            dataDir = Path.GetDirectoryName(dlg.FileName)!;
        }

        var mods = new List<ModDefinition>();
        foreach (var file in modFiles)
        {
            var path = Path.Combine(dataDir, file);
            if (File.Exists(path))
                mods.AddRange(PobModParser.ParseFile(path));
        }

        var db = new ModDatabase(mods);
        var vm = new MainViewModel(db);
        new MainWindow(vm).Show();
    }
}
