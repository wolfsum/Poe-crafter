using System.IO;
using System.Windows;
using Poe2Crafter.Core.Games;
using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Models;
using Poe2Crafter.Core.Parsing;
using Poe2Crafter.Services;
using Poe2Crafter.ViewModels;

namespace Poe2Crafter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = SettingsStore.Load();

        // First run (no saved game) → ask; otherwise use the saved profile
        var profile = GameProfiles.ByKey(settings.GameVersion);
        if (settings.GameVersion is null)
        {
            var chosen = GamePicker.Choose();
            if (chosen is null) { Shutdown(); return; }
            profile = chosen;
            settings.GameVersion = profile.Key;
            SettingsStore.Save(settings);
        }

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            profile.PobFolderName, "Data");

        if (!File.Exists(Path.Combine(dataDir, "ModItem.lua")))
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title    = $"Locate ModItem.lua ({profile.Name} PoB Data folder)",
                Filter   = "Lua files (*.lua)|*.lua",
                FileName = "ModItem.lua",
            };
            if (dlg.ShowDialog() != true) { Shutdown(); return; }
            dataDir = Path.GetDirectoryName(dlg.FileName)!;
        }

        var mods = new List<ModDefinition>();
        foreach (var file in profile.ModFiles)
        {
            var path = Path.Combine(dataDir, file);
            if (File.Exists(path))
                mods.AddRange(PobModParser.ParseFile(path));
        }
        mods.AddRange(profile.EmbeddedMods);

        var db = new ModDatabase(mods);
        var vm = new MainViewModel(db, profile);
        new MainWindow(vm).Show();
    }
}
