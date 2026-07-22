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

        // Surface any crash instead of the process vanishing with no window —
        // the only way to diagnose failures on machines we can't reproduce on.
        DispatcherUnhandledException += (_, ex) =>
        {
            ReportFatal(ex.Exception);
            ex.Handled = true;
            Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            ReportFatal(ex.ExceptionObject as Exception);

        StartupLog.Begin();
        try
        {
            RunStartup(e);
        }
        catch (Exception ex)
        {
            StartupLog.Write($"FATAL: {ex}");
            ReportFatal(ex);
            Shutdown();
        }
    }

    private void RunStartup(StartupEventArgs e)
    {
        StartupLog.Write("Loading settings...");
        var settings = SettingsStore.Load();
        StartupLog.Write($"Settings loaded. GameVersion={settings.GameVersion ?? "(none)"}");

        // First run (no saved game) → ask; otherwise use the saved profile
        var profile = GameProfiles.ByKey(settings.GameVersion);
        if (settings.GameVersion is null)
        {
            StartupLog.Write("No saved game - showing picker.");
            var chosen = GamePicker.Choose();
            if (chosen is null) { StartupLog.Write("Picker cancelled - exiting."); Shutdown(); return; }
            profile = chosen;
            settings.GameVersion = profile.Key;
            SettingsStore.Save(settings);
            StartupLog.Write($"Picked {profile.Key}, settings saved.");
        }
        StartupLog.Write($"Active profile: {profile.Key} ({profile.Name})");

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            profile.PobFolderName, "Data");
        StartupLog.Write($"Data dir: {dataDir}");

        // The main mod file differs per game (PoE2: ModItem.lua, PoE1: ModExplicit.lua) —
        // ModFiles[0] is the profile's primary/probe file.
        var probeFile = profile.ModFiles[0];
        var probeExists = File.Exists(Path.Combine(dataDir, probeFile));
        StartupLog.Write($"Probe {probeFile}: {(probeExists ? "found" : "MISSING")}");
        if (!probeExists)
        {
            // The mod database comes from Path of Building's data files — without
            // an installed PoB there is nothing to craft against. Explain instead
            // of silently opening a file picker.
            var pob = profile.Key == "poe2" ? "Path of Building (PoE2)" : "Path of Building Community";
            var res = MessageBox.Show(
                $"{pob} не найден.\n\n" +
                $"Ожидаемый путь с данными:\n{dataDir}\n\n" +
                $"База модов берётся из файлов PoB — установи {pob} и перезапусти.\n\n" +
                $"Либо нажми OK и укажи {probeFile} вручную (нестандартная установка).",
                $"{profile.Name} Crafter — PoB не найден",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res != MessageBoxResult.OK) { Shutdown(); return; }

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title    = $"Locate {probeFile} ({profile.Name} PoB Data folder)",
                Filter   = "Lua files (*.lua)|*.lua",
                FileName = probeFile,
            };
            if (dlg.ShowDialog() != true) { StartupLog.Write("File picker cancelled - exiting."); Shutdown(); return; }
            dataDir = Path.GetDirectoryName(dlg.FileName)!;
            StartupLog.Write($"User picked data dir: {dataDir}");
        }

        StartupLog.Write("Loading aux data...");
        profile.LoadAuxData(dataDir); // e.g. cluster jewel base catalog

        var mods = new List<ModDefinition>();
        foreach (var file in profile.ModFiles)
        {
            var path = Path.Combine(dataDir, file);
            if (File.Exists(path))
            {
                var parsed = PobModParser.ParseFile(path, source: file);
                mods.AddRange(parsed);
                StartupLog.Write($"Parsed {file}: {parsed.Count} mods");
            }
            else StartupLog.Write($"Skipped {file}: not present");
        }
        mods.AddRange(profile.EmbeddedMods);
        StartupLog.Write($"Total mods (incl. {profile.EmbeddedMods.Count} embedded): {mods.Count}");

        if (mods.Count == 0)
        {
            MessageBox.Show(
                $"Из данных PoB не удалось прочитать ни одного мода.\n\n" +
                $"Папка: {dataDir}\n" +
                $"Файлы: {string.Join(", ", profile.ModFiles)}\n\n" +
                "Возможно, версия Path of Building несовместима — обнови PoB и перезапусти.",
                $"{profile.Name} Crafter — пустая база модов",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        StartupLog.Write("Building mod database...");
        var db = new ModDatabase(mods);
        StartupLog.Write("Building view model...");
        var vm = new MainViewModel(db, profile);
        StartupLog.Write("Showing main window...");
        new MainWindow(vm).Show();
        StartupLog.Write("Startup complete - window shown.");
    }

    // Log the crash to %APPDATA%\Poe2Crafter\crash.log and show it, so failures
    // on machines we can't debug on are diagnosable from the message alone.
    private static void ReportFatal(Exception? ex)
    {
        var text = ex?.ToString() ?? "Unknown error";
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Poe2Crafter");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "crash.log"),
                $"{DateTime.Now:u}\n{text}\n");
        }
        catch { /* logging must never mask the original error */ }

        MessageBox.Show(
            $"Приложение упало при запуске:\n\n{text}\n\n" +
            "Скопируй этот текст (он также сохранён в %APPDATA%\\Poe2Crafter\\crash.log).",
            "Poe2Crafter — ошибка",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
