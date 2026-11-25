using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Manager.Core.Services;
using Microsoft.CSharp;
using Smm.FirstRun;

public static class FirstRunMigrationFixture
{
    private static string _root;
    private static string _legacyBinaries;

    public static int Run(string output, TextWriter report)
    {
        _root = Path.Combine(output, "synthetic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Action[] tests =
        {
            DetectFlatInstall, ImportWrapper, KeepCurrentOnConflict, UseOldOnConflict,
            RetryInterruptedSnapshot, RefuseCorruptBackup, BlockSaveWithoutBackup, RestoreOriginalFiles
        };

        int failed = 0;
        try
        {
            _legacyBinaries = Path.Combine(_root, "legacy-metadata");
            Directory.CreateDirectory(_legacyBinaries);
            CompileVersionFixture("Manager.exe");
            CompileVersionFixture("ModAPI.dll");
            foreach (var test in tests)
            {
                try { test(); }
                catch (Exception error)
                {
                    failed++;
                    report.WriteLine(test.Method.Name + ": " + error);
                }
            }
        }
        finally
        {
            Directory.Delete(_root, true);
        }
        report.WriteLine("First-run migration: {0} passed, {1} failed", tests.Length - failed, failed);
        return failed == 0 ? 0 : 1;
    }

    private static void DetectFlatInstall()
    {
        string game = Game("flat");
        PlaceLegacyBinaries(game);
        Write(game, "mods/loadorder.json", "{\"order\":[\"b\",\"a\"]}");
        Write(game, "SMM/bin/mod_manager.ini", "DarkMode=True");
        var migration = Migration(game);
        Require(migration.IsLegacyRoot, "Both legacy assemblies should be identified by metadata.");
        Require(!migration.FindWrappers().Any(), "A flat install has no wrapper.");
        File.WriteAllText(Path.Combine(game, "SMM/Manager.exe"), "non-managed fixture");
        Require(!migration.IsLegacyRoot, "Invalid binary metadata must not identify an install.");
        new PreUpgradeSaveBackup(game).Ensure();
        Require(Read(game, "mods/loadorder.json") == "{\"order\":[\"b\",\"a\"]}", "Flat order should stay intact.");
        Require(Read(game, "SMM/bin/mod_manager.ini") == "DarkMode=True", "Flat settings should stay intact.");
    }

    private static void ImportWrapper()
    {
        string game = Game("wrapper");
        string steam = Wrapper(game, "SMM_Steam-GOG_v1.2.2");
        string epic = Wrapper(game, "SMM_Epic_v1.2.2");
        string other = Wrapper(game, "SMM_test_v1.2.2");
        Write(game, "SMM_fake_v1.2.2/SMM/Manager.exe", "unverified");
        Write(steam, "SMM/bin/mod_manager.ini", "DarkMode=True\nGamePath=wrong\nUnknownPreference=ignore");
        Write(steam, "mods/Example/Config/user.json", "old config");
        Write(steam, "mods/loadorder.json", "{\"order\":[\"second\",\"first\"]}");
        Write(steam, "mods/ModAPI/Saves/Standard/Slot_4/SaveData.xml", "<old/>");
        var migration = Migration(game);
        Require(!migration.IsLegacyRoot, "Surviving wrapper binaries must not identify the root.");
        Require(migration.FindWrappers().Count() == 3
            && migration.FindWrappers().Contains(epic) && migration.FindWrappers().Contains(other),
            "Both released names and a verified custom wrapper should be detected.");
        new PreUpgradeSaveBackup(game).Ensure();
        Require(migration.Import(steam, UnexpectedPrompt), "The wrapper should be imported.");
        string settings = Read(game, "SMM/bin/mod_manager.ini");
        Require(settings.Contains("DarkMode=True") && !settings.Contains("wrong") && !settings.Contains("UnknownPreference"),
            "Import the shipped preferences and set the real executable path.");
        Require(Read(game, "mods/loadorder.json") == Read(steam, "mods/loadorder.json"), "Order must match byte for byte.");
        Require(Read(game, "mods/Example/Config/user.json") == "old config", "Mod settings should follow the mod.");
        Require(Read(game, "mods/ModAPI/Saves/Standard/Slot_4/SaveData.xml") == "<old/>", "Wrapper save data should follow the mod tree.");
        Require(!migration.Import(steam, UnexpectedPrompt), "A successful import should not repeat.");
        Require(Directory.Exists(steam), "Import must leave the wrapper in place.");
        migration.Tidy(steam);
        var archived = Directory.GetDirectories(Path.Combine(game, "SMM/Backups"), "old-1.2.2-*").Single();
        Require(!Directory.Exists(steam) && Directory.Exists(Path.Combine(archived, Path.GetFileName(steam))),
            "Tidy should move the complete wrapper to a dated backup.");
    }

    private static void KeepCurrentOnConflict()
    {
        string game = Game("keep-current");
        string old = MakeConflict(game);
        var migration = Migration(game);
        Require(!migration.Import(old, () => MigrationChoice.Later), "Cancel should defer import.");
        Require(!File.Exists(Path.Combine(game, "mods/OnlyOld/unique.txt")), "Cancel must leave the game root alone.");
        Require(migration.Import(old, () => MigrationChoice.KeepCurrent), "Keep current should finish the import.");
        Require(Read(game, "mods/loadorder.json") == "{\"order\":[\"current\"]}", "Keep current order.");
        Require(Read(game, "mods/Both/current.txt") == "current", "Keep current mod package.");
        Require(!File.Exists(Path.Combine(game, "mods/Both/old.txt")), "Do not mix mod versions.");
        Require(Read(game, "mods/OnlyOld/unique.txt") == "old", "Copy a mod unique to the old install.");
        Require(Read(game, "mods/ModAPI/Saves/only-old.xml") == "old", "Merge non-conflicting ModAPI saves.");
        Require(Read(game, "mods/ModAPI/Saves/conflict.xml") == "current", "Keep the selected ModAPI save.");
    }

    private static void UseOldOnConflict()
    {
        string game = Game("use-old");
        string old = MakeConflict(game);
        var migration = Migration(game);
        Require(migration.Import(old, () => MigrationChoice.UseOld), "Use old should finish the import.");
        Require(Read(game, "mods/loadorder.json") == "{\"order\":[]}", "Preserve an explicitly empty enabled list.");
        Require(Read(game, "mods/Both/old.txt") == "old", "Install the complete legacy mod folder.");
        Require(!File.Exists(Path.Combine(game, "mods/Both/current.txt")), "Do not mix mod versions.");
        Require(Read(game, "mods/ModAPI/Saves/only-current.xml") == "current", "Keep non-conflicting current saves.");
        Require(Read(game, "mods/ModAPI/Saves/conflict.xml") == "old", "Use the selected ModAPI save.");
        string previous = Path.Combine(game, "SMM/Backups/import-" + Path.GetFileName(old), "previous");
        Require(Read(previous, "mods/Both/current.txt") == "current", "Archive the displaced mod package.");
        Require(Read(previous, "mods/loadorder.json") == "{\"order\":[\"current\"]}", "Archive the displaced enabled list.");
        Require(Read(previous, "mod_manager.ini").Contains("DarkMode=False"), "Archive the replaced preference.");
        Require(Read(game, "SMM/bin/mod_manager.ini").Contains("EnableNexusIntegration=False"), "Keep 2.0-only settings.");
    }

    private static void RetryInterruptedSnapshot()
    {
        string game = Game("retry");
        Write(game, "SMM/Backups/.snapshot-interrupted/saves/slot.dat", "partial");
        Write(game, "saves/slot.dat", "original");
        Write(game, "mods/ModAPI/Saves/fixture.xml", "<original/>");
        Directory.CreateDirectory(Path.Combine(game, "saves/empty-folder"));
        var backup = new PreUpgradeSaveBackup(game);
        using (File.Open(Path.Combine(game, "saves/slot.dat"), FileMode.Open, FileAccess.Write, FileShare.None))
        {
            ExpectIOException(() => backup.Ensure());
            Require(!backup.HasBackup, "A failed copy must never publish a completed snapshot.");
        }
        string captured = backup.Ensure();
        Require(backup.Ensure() == captured, "Retry should publish exactly one backup.");
        Require(Read(captured, "saves/slot.dat") == "original", "Partial files must not be reused.");
        Require(Directory.Exists(Path.Combine(captured, "saves/empty-folder")), "Include empty source folders.");
        Require(Read(captured, "mods/ModAPI/Saves/fixture.xml") == "<original/>", "Capture the ModAPI data tree.");
    }

    private static void RefuseCorruptBackup()
    {
        string game = Game("bad-hash");
        Write(game, "saves/slot.dat", "original");
        var backup = new PreUpgradeSaveBackup(game);
        string captured = backup.Ensure();
        Write(captured, "saves/slot.dat", "tampered");
        Write(game, "saves/slot.dat", "2.0 progress");
        Require(backup.Ensure() == captured, "Later damage to a published backup must not block launches or saving.");
        ExpectIOException(backup.Restore);
        Require(Read(game, "saves/slot.dat") == "2.0 progress", "A corrupt snapshot must not modify live saves.");
        Require(Read(captured, "saves/slot.dat") == "tampered", "A corrupt snapshot must not be replaced by current data.");
    }

    private static void BlockSaveWithoutBackup()
    {
        string game = Game("guard");
        Write(game, "saves/slot.dat", "original");
        ModAPI.Core.ModApiPaths.GameRoot = game;
        var saveManager = new SaveManager();
        var platform = new PlatformSave_PC();
        using (File.Open(Path.Combine(game, "saves/slot.dat"), FileMode.Open, FileAccess.Write, FileShare.None))
            ShelteredAPI.Core.FirstRunSaveGuard.Initialize();

        Require(!new PreUpgradeSaveBackup(game).HasBackup, "No pre-2.0 snapshot should exist for this fixture.");
        Require(!saveManager.StartSave() && !platform.PlatformSave(), "Both save entry points must return failure.");
        Require(Read(game, "saves/slot.dat") == "original", "The guarded save must leave the original bytes in place.");
        Require(MessageBox.LastMessage.Contains("saving is blocked"), "The player should see a save failure message.");
        Require(ModAPI.Core.MMLog.LastError.Contains("backup failed"), "The snapshot failure should be logged.");
    }

    private static void RestoreOriginalFiles()
    {
        string game = Game("restore");
        Write(game, "saves/slot.dat", "original");
        Write(game, "mods/ModAPI/Saves/fixture.xml", "<original/>");
        var backup = new PreUpgradeSaveBackup(game);
        backup.Ensure();
        Write(game, "saves/slot.dat", "2.0 progress");
        Write(game, "saves/new.dat", "new slot");
        File.Delete(Path.Combine(game, "mods/ModAPI/Saves/fixture.xml"));
        backup.Restore();
        Require(Read(game, "saves/slot.dat") == "original", "Restore the original vanilla save.");
        Require(Read(game, "mods/ModAPI/Saves/fixture.xml") == "<original/>", "Restore missing ModAPI saves.");
        Require(Read(game, "saves/new.dat") == "new slot", "Restore should keep unmatched saves.");
        string beforeRestore = Directory.GetDirectories(Path.Combine(game, "SMM/Backups"), "before-restore-*").Single();
        Require(Read(beforeRestore, "saves/slot.dat") == "2.0 progress", "Keep the current save in a separate backup.");
    }

    private static string MakeConflict(string game)
    {
        string old = Wrapper(game, "SMM_Steam-GOG_v1.2.2");
        Write(game, "SMM/bin/mod_manager.ini", "DarkMode=False\nEnableNexusIntegration=False");
        Write(old, "SMM/bin/mod_manager.ini", "DarkMode=True");
        Write(game, "mods/loadorder.json", "{\"order\":[\"current\"]}");
        Write(old, "mods/loadorder.json", "{\"order\":[]}");
        Write(game, "mods/Both/current.txt", "current");
        Write(old, "mods/Both/old.txt", "old");
        Write(old, "mods/OnlyOld/unique.txt", "old");
        Write(game, "mods/ModAPI/Saves/only-current.xml", "current");
        Write(old, "mods/ModAPI/Saves/only-old.xml", "old");
        Write(game, "mods/ModAPI/Saves/conflict.xml", "current");
        Write(old, "mods/ModAPI/Saves/conflict.xml", "old");
        new PreUpgradeSaveBackup(game).Ensure();
        return old;
    }

    private static string Game(string name)
    {
        string game = Path.Combine(_root, name);
        Write(game, "Sheltered.exe", "synthetic");
        return game;
    }

    private static LegacyInstallMigration Migration(string game)
    {
        return new LegacyInstallMigration(Path.Combine(game, "Sheltered.exe"));
    }

    private static string Wrapper(string game, string name)
    {
        string root = Path.Combine(game, name);
        PlaceLegacyBinaries(root);
        return root;
    }

    private static void PlaceLegacyBinaries(string root)
    {
        string smm = Path.Combine(root, "SMM");
        Directory.CreateDirectory(smm);
        foreach (string source in Directory.GetFiles(_legacyBinaries))
            File.Copy(source, Path.Combine(smm, Path.GetFileName(source)));
    }

    private static void CompileVersionFixture(string name)
    {
        using (var compiler = new CSharpCodeProvider())
        {
            var arguments = new CompilerParameters { OutputAssembly = Path.Combine(_legacyBinaries, name) };
            var compiled = compiler.CompileAssemblyFromSource(arguments,
                "[assembly: System.Reflection.AssemblyVersion(\"1.2.2.0\")] public class Synthetic {}");
            Require(!compiled.Errors.HasErrors, "Could not compile legacy metadata fixture.");
        }
    }

    private static void Write(string root, string path, string contents)
    {
        string file = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllText(file, contents);
    }

    private static string Read(string root, string file) { return File.ReadAllText(Path.Combine(root, file)); }
    private static MigrationChoice UnexpectedPrompt() { throw new InvalidOperationException("Unexpected conflict prompt."); }
    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
    private static void ExpectIOException(Action action)
    {
        try { action(); }
        catch (IOException) { return; }
        throw new InvalidOperationException("Expected an I/O failure.");
    }
}

// Minimal game boundaries. The fixture uses the production guard and the shipped Harmony DLL.
internal class SaveManager
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal bool StartSave()
    {
        File.WriteAllText(Path.Combine(ModAPI.Core.ModApiPaths.GameRoot, "saves/slot.dat"), "game overwrite");
        return true;
    }
}

internal class PlatformSave_PC
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal bool PlatformSave()
    {
        File.WriteAllText(Path.Combine(ModAPI.Core.ModApiPaths.GameRoot, "saves/slot.dat"), "platform overwrite");
        return true;
    }
}

internal enum MessageBoxButtons { Okay_Button }
internal static class MessageBox
{
    internal static string LastMessage;
    internal static void Show(MessageBoxButtons buttons, string message, object a, object b, object c, bool d)
    {
        LastMessage = message;
    }
}

namespace ModAPI.Core
{
    internal static class ModApiPaths { internal static string GameRoot; }
    internal static class MMLog
    {
        internal static string LastError;
        internal static void WriteError(string message) { LastError = message; }
    }
}

namespace ShelteredAPI.Saves { internal class SaveRegistryCore { } }
namespace ShelteredAPI.Saves.Backups { internal class SaveBackupRepository { } }
namespace ShelteredAPI.Saves.Runtime { internal class PlatformSaveProxy { } }
