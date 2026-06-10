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

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Path of Building Community (PoE2)", "Data");

        List<string> luaFiles = [];
        if (Directory.Exists(dataDir))
            luaFiles = Directory.GetFiles(dataDir, "*.lua").ToList();

        if (luaFiles.Count == 0)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Locate Mod*.lua files (select one from Data folder)",
                Filter = "Lua files (*.lua)|*.lua",
            };
            if (dlg.ShowDialog() != true) { Shutdown(); return; }
            dataDir = Path.GetDirectoryName(dlg.FileName)!;
            luaFiles = Directory.GetFiles(dataDir, "*.lua").ToList();
        }

        var mods = new List<ModDefinition>();
        foreach (var file in luaFiles.OrderBy(f => f))
            mods.AddRange(PobModParser.ParseFile(file));

        var db = new ModDatabase(mods);
        var vm = new MainViewModel(db);
        new MainWindow(vm).Show();
    }
}
