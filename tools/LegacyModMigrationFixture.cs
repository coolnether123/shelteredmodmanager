using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Manager;
using Manager.Controls;
using Manager.Core.Models;
using Manager.Core.Services;
using Manager.Views;
using ModAPI.Core;
using UnityEngine;

internal static class LegacyModMigrationFixture
{
    private const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Public;
    private const string UpdateMessage = "Built for SMM 1.2.2 \u2014 needs a 2.0 update";
    private static int _passed;

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
        _passed++;
    }

    private static ModEntry CreateMod(string id, string name)
    {
        string root = Path.GetFullPath(Path.Combine("mods", id));
        Directory.CreateDirectory(Path.Combine(root, "About"));
        File.WriteAllText(Path.Combine(Path.Combine(root, "About"), "About.json"),
            "{\"id\":\"" + id + "\",\"name\":\"" + name + "\",\"version\":\"1.0\"}");
        var entry = new ModEntry { Id = id, Name = name, Version = "1.0", RootPath = root,
            AssembliesPath = Path.Combine(root, "Assemblies") };
        Directory.CreateDirectory(entry.AssembliesPath);
        return entry;
    }

    private static bool IsLoaded(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == name)
                return true;
        foreach (var assembly in AppDomain.CurrentDomain.ReflectionOnlyGetAssemblies())
            if (assembly.GetName().Name == name)
                return true;
        return false;
    }

    private static void CheckSave(Assembly sheltered, string manifestFields, string expected)
    {
        var registry = sheltered.GetType("ShelteredAPI.Saves.SaveRegistryCore", true);
        var verify = sheltered.GetType("ShelteredAPI.Saves.Paging.SaveVerification", true);
        var manifest = registry.GetMethod("DeserializeSlotManifest", Internal | BindingFlags.Static)
            .Invoke(null, new object[] { "{" + manifestFields + "}" });
        string actual = verify.GetMethod("VerifyRequired").Invoke(null, new object[] { manifest }).ToString();
        Check(actual == expected, "Save verification expected " + expected + ", got " + actual + " for " + manifestFields);
    }

    private static void CheckImportedNexusUpgrade(string legacySmm, string replacementZip)
    {
        const string oldId = "coolnether123.FamilyExpansion";
        const string newId = "com.procreation.framework";
        string game = Path.GetFullPath("import-game");
        string wrapper = Path.Combine(game, "SMM_Steam-GOG_v1.2.2");
        string wrapperSmm = Path.Combine(wrapper, "SMM");
        Directory.CreateDirectory(wrapperSmm);
        foreach (string binary in new[] { "Manager.exe", "ModAPI.dll" })
            File.Copy(Path.Combine(legacySmm, binary), Path.Combine(wrapperSmm, binary));
        string oldMod = Path.Combine(Path.Combine(wrapper, "mods"), "FamilyExpansion");
        Directory.CreateDirectory(Path.Combine(oldMod, "About"));
        Directory.CreateDirectory(Path.Combine(oldMod, "Assemblies"));
        File.WriteAllText(Path.Combine(oldMod, "About/About.json"),
            "{\"id\":\"" + oldId + "\",\"name\":\"Family Expansion\",\"version\":\"1.0\"}");
        File.Copy("LegacyPlugin.dll", Path.Combine(oldMod, "Assemblies/LegacyPlugin.dll"));
        var order = new LoadOrderService();
        order.SaveOrder(Path.Combine(wrapper, "mods"), new[] { "first", oldId, "last" });

        var assembly = typeof(ModDiscoveryService).Assembly;
        var importType = assembly.GetType("Manager.Core.Services.LegacyInstallMigration", true);
        object import = Activator.CreateInstance(importType, Internal | BindingFlags.Instance, null,
            new object[] { Path.Combine(game, "Sheltered.exe") }, null);
        Check((bool)importType.GetMethod("Import", Internal | BindingFlags.Instance)
            .Invoke(import, new object[] { wrapper, null }), "The verified 1.2.2 wrapper imports");

        string modsPath = Path.Combine(game, "mods");
        var scanner = new ModDiscoveryService("2.0.0.0");
        var mods = scanner.DiscoverMods(modsPath);
        var old = mods.Find(mod => string.Equals(mod.Id, oldId, StringComparison.OrdinalIgnoreCase));
        Check(old != null, "The imported mod appears in the Manager");
        Check(old.StatusMessage == UpdateMessage && order.GetDisabledMods(mods, modsPath).Contains(old),
            "The imported mod is disabled and shown as needing a 2.0 update");
        using (var tab = new ModManagerTab())
        using (var details = new ModDetailsPanel())
        {
            typeof(ModManagerTab).GetField("_allMods", Internal | BindingFlags.Instance).SetValue(tab, mods);
            typeof(ModManagerTab).GetMethod("RefreshNexusStatusAsync", Internal | BindingFlags.Instance, null,
                new[] { typeof(bool) }, null).Invoke(tab, new object[] { false });
            details.ShowMod(old);
            var link = (LinkLabel)typeof(ModDetailsPanel).GetField("_nexusLink", Internal | BindingFlags.Instance).GetValue(details);
            string url = null;
            details.WebsiteClicked += delegate(object sender, string target) { url = target; };
            typeof(ModDetailsPanel).GetMethod("NexusLink_LinkClicked", Internal | BindingFlags.Instance)
                .Invoke(details, new object[] { link, null });
            Check(link.Visible && link.Text == "Find update" && url == "https://www.nexusmods.com/sheltered/mods/6",
                "Find update opens the registry's Family Expansion page");
        }

        var constructor = typeof(NexusInstallService).GetConstructor(Internal | BindingFlags.Instance, null,
            new[] { assembly.GetType("Manager.Core.Services.IArchiveExtractor", true), typeof(Action<string, string>) }, null);
        var installer = (NexusInstallService)constructor.Invoke(new object[] { null,
            new Action<string, string>(delegate(string source, string archive) { File.Copy(source, archive); }) });
        string error;
        var result = installer.DownloadAndInstall(replacementZip, modsPath,
            new NexusRemoteMod { GameDomain = old.NexusGameDomain, ModId = old.NexusModId },
            new NexusRemoteModFile { FileId = 1, Category = "MAIN" },
            NexusInstallTargetContext.FromInstalledMod(old), out error);
        Check(result != null && result.InstalledPath == old.RootPath, "The Nexus port replaces the imported package: " + error);
        mods = scanner.DiscoverMods(modsPath);
        var enabled = order.GetEnabledMods(mods, modsPath);
        Check(string.Join(",", order.ReadOrder(modsPath)) == "first," + newId + ",last" &&
            enabled.Exists(mod => mod.Id == newId) && !mods.Exists(mod => string.Equals(mod.Id, oldId, StringComparison.OrdinalIgnoreCase)),
            "The replacement occupies the imported slot and is enabled");
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            CheckImportedNexusUpgrade(args[0], args[1]);
            var legacy = CreateMod("fixture.legacy", "Legacy Pantry");
            var split = CreateMod("fixture.split", "Legacy Storage");
            var broken = CreateMod("fixture.broken", "Truncated Package");
            var locked = CreateMod("fixture.locked", "Unreadable Package");
            var failed = CreateMod("fixture.failed", "Failed Startup");
            var current = CreateMod("fixture.current", "Current Pantry");
            var library = CreateMod("fixture.library", "Current Library");
            var harmony = CreateMod("com.harmony.0harmony", "Harmony");
            File.Copy("LegacyPlugin.dll", Path.Combine(legacy.AssembliesPath, "LegacyPlugin.dll"));
            File.Copy("LegacySheltered.dll", Path.Combine(split.AssembliesPath, "LegacySheltered.dll"));
            File.Copy("PackageHelper.dll", Path.Combine(legacy.AssembliesPath, "00-PackageHelper.dll"));
            File.Copy("CurrentPlugin.dll", Path.Combine(current.AssembliesPath, "CurrentPlugin.dll"));
            File.Copy("FailedPlugin.dll", Path.Combine(failed.AssembliesPath, "FailedPlugin.dll"));
            File.Copy("CurrentLibrary.dll", Path.Combine(library.AssembliesPath, "CurrentLibrary.dll"));
            File.Copy("0Harmony.dll", Path.Combine(harmony.AssembliesPath, "0Harmony.dll"));
            File.WriteAllText(Path.Combine(broken.AssembliesPath, "Broken.dll"), "truncated");
            string lockedDll = Path.Combine(locked.AssembliesPath, "Locked.dll");
            File.Copy("PackageHelper.dll", lockedDll);

            var scanner = new ModDiscoveryService("2.0.0.0");
            var mods = scanner.DiscoverMods(Path.GetFullPath("mods"));
            var order = new LoadOrderService();
            string modsPath = Path.GetFullPath("mods");
            order.SaveOrder(modsPath, new[] { failed.Id, legacy.Id, current.Id });
            var item = mods.Find(mod => mod.Id == legacy.Id);
            Check(item.DisplayName == "Legacy Pantry" && item.StatusMessage == UpdateMessage, "Manager shows manifest name and upgrade text");
            Check(order.GetDisabledMods(mods, modsPath).Contains(item), "Legacy remains visible in the disabled list");
            Check(!order.GetEnabledMods(mods, modsPath).Contains(item), "A saved load-order entry cannot enable a legacy package");
            var nextOrder = LoadOrderService.KeepLegacySlots(mods, order.ReadOrder(modsPath), new[] { current.Id, failed.Id, split.Id });
            Check(string.Join(",", nextOrder.ToArray()) == current.Id + "," + legacy.Id + "," + failed.Id,
                "Order changes keep the reserved slot and cannot enable a new legacy ID");
            Check(AssemblyVersionChecker.GetModApiReferences("LegacySheltered.dll").Exists(
                reference => reference.ApiName == "ShelteredAPI" && reference.ApiVersion.StartsWith("0.")),
                "The shipped ShelteredAPI 0.x reference is identified");
            Check(!IsLoaded("LegacyPlugin") && !IsLoaded("LegacySheltered"), "Manager metadata inspection never loads legacy DLLs");

            var managerType = typeof(ModRuntime).Assembly.GetType("ModAPI.Core.PluginManager", true);
            var manager = managerType.GetMethod("getInstance").Invoke(null, null);
            managerType.GetField("_loaderRoot", Internal | BindingFlags.Instance).SetValue(manager, new GameObject("fixture"));
            Assembly.LoadFrom("0Harmony.dll");
            var sheltered = Assembly.LoadFrom("ShelteredAPI.dll");
            Directory.CreateDirectory("saves");
            File.WriteAllText(Path.Combine("saves", "slot.dat"), "fixture save");
            var guard = sheltered.GetType("ShelteredAPI.Core.FirstRunSaveGuard", true);
            guard.GetMethod("Initialize", Internal | BindingFlags.Static).Invoke(null, null);
            var snapshots = Directory.GetDirectories(Path.Combine("SMM", "Backups"), "pre-2.0-*");
            Check(snapshots.Length == 1 &&
                File.ReadAllText(Path.Combine(snapshots[0], "saves/slot.dat")) == "fixture save",
                "The runtime guard publishes a save snapshot before plugin loading");
            var entries = new List<ModEntry> { legacy, split, broken, locked, failed, current, library, harmony };
            managerType.GetProperty("LoadedMods").GetSetMethod(true).Invoke(null, new object[] { entries });

            using (File.Open(lockedDll, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var prepared = managerType.GetMethod("PrepareModLoads", Internal | BindingFlags.Instance)
                    .Invoke(manager, new object[] { entries });
                Check(((IList)prepared).Count == 4, "Only the four compatible packages are prepared");
                managerType.GetMethod("ActivatePreparedMods", Internal | BindingFlags.Instance)
                    .Invoke(manager, new[] { prepared });
            }

            Check(!IsLoaded("LegacyPlugin") && !IsLoaded("LegacySheltered") && !IsLoaded("PackageHelper"),
                "No DLL from legacy or unreadable packages is loaded");
            Check(File.Exists("initialized") && File.Exists("started"), "The 2.0 neighbour completes Initialize and Start");
            Check(File.Exists("failed-start"), "The startup failure fixture reaches Start");
            string log = File.ReadAllText(Path.Combine("SMM", "mod_manager.log"));
            Check(log.Contains("blocked mod 'fixture.legacy': " + UpdateMessage) && log.Contains("ModAPI 1.2.2.0"),
                "Startup log records the gated package and its reference");
            Check(log.Contains("blocked mod 'fixture.split': " + UpdateMessage) && log.Contains("ShelteredAPI 0."),
                "Startup log separately records the ShelteredAPI 0.x package");
            Check(log.Contains("blocked mod 'fixture.broken': Could not inspect assembly metadata") &&
                log.Contains("blocked mod 'fixture.locked': Could not inspect assembly metadata"),
                "Truncated and unreadable DLLs log their own errors");
            Check(!log.Contains("The pre-2.0 save backup failed"), "A successful snapshot preserves startup diagnostics");

            CheckSave(sheltered, "\"lastLoadedMods\":[{\"modId\":\"fixture.legacy\",\"version\":\"1.0\"}]", "Missing");
            CheckSave(sheltered, "\"lastLoadedMods\":[{\"modId\":\"fixture.failed\",\"version\":\"1.0\"}]", "Missing");
            CheckSave(sheltered, "\"lastLoadedMods\":[{\"modId\":\"fixture.absent\",\"version\":\"1.0\"}]", "Missing");
            CheckSave(sheltered, "", "Unknown");
            CheckSave(sheltered, "\"lastLoadedMods\":[]", "Match");
            CheckSave(sheltered, "\"lastLoadedMods\":[{\"modId\":\"fixture.current\",\"version\":\"1.0\"}]", "Match");
            CheckSave(sheltered, "\"lastLoadedMods\":[{\"modId\":\"fixture.library\",\"version\":\"1.0\"}]", "Match");
            CheckSave(sheltered, "\"lastLoadedMods\":[{\"modId\":\"com.harmony.0harmony\",\"version\":\"1.0\"}]", "Match");
            Console.WriteLine("Legacy migration fixture: {0} passed, 0 failed.", _passed);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Legacy migration fixture: {0} passed, 1 failed.\n{1}", _passed, ex);
            return 1;
        }
    }
}
