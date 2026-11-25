using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Manager;
using Manager.Core.Services;
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

    [STAThread]
    private static int Main()
    {
        try
        {
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

            var sheltered = Assembly.LoadFrom("ShelteredAPI.dll");
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
