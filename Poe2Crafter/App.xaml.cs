using System.IO;
using System.Windows;
using Poe2Crafter.Core.Matching;
using Poe2Crafter.Core.Parsing;
using Poe2Crafter.ViewModels;

namespace Poe2Crafter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var pobPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Path of Building Community (PoE2)", "Data", "ModItem.lua");

        if (!File.Exists(pobPath))
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title    = "Locate ModItem.lua",
                Filter   = "Lua files (*.lua)|*.lua",
                FileName = "ModItem.lua",
            };
            if (dlg.ShowDialog() != true) { Shutdown(); return; }
            pobPath = dlg.FileName;
        }

        var mods = PobModParser.ParseFile(pobPath);
        var db   = new ModDatabase(mods);
        var vm   = new MainViewModel(db);

        new MainWindow(vm).Show();
    }
}
