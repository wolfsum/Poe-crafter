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
            // The mod database comes from Path of Building's data files — without
            // an installed PoB there is nothing to craft against. Explain instead
            // of silently opening a file picker.
            var pob = profile.Key == "poe2" ? "Path of Building (PoE2)" : "Path of Building Community";
            var res = MessageBox.Show(
                $"{pob} не найден.\n\n" +
                $"Ожидаемый путь с данными:\n{dataDir}\n\n" +
                $"База модов берётся из файлов PoB — установи {pob} и перезапусти.\n\n" +
                "Либо нажми OK и укажи ModItem.lua вручную (нестандартная установка).",
                $"{profile.Name} Crafter — PoB не найден",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res != MessageBoxResult.OK) { Shutdown(); return; }

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title    = $"Locate ModItem.lua ({profile.Name} PoB Data folder)",
                Filter   = "Lua files (*.lua)|*.lua",
                FileName = "ModItem.lua",
            };
            if (dlg.ShowDialog() != true) { Shutdown(); return; }
            dataDir = Path.GetDirectoryName(dlg.FileName)!;
        }

        profile.LoadAuxData(dataDir); // e.g. cluster jewel base catalog

        var mods = new List<ModDefinition>();
        foreach (var file in profile.ModFiles)
        {
            var path = Path.Combine(dataDir, file);
            if (File.Exists(path))
                mods.AddRange(PobModParser.ParseFile(path, source: file));
        }
        mods.AddRange(profile.EmbeddedMods);

        var db = new ModDatabase(mods);
        var vm = new MainViewModel(db, profile);
        new MainWindow(vm).Show();
    }
}
