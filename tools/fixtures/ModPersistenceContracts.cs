using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Persistence;

namespace ModAPI.Core
{
    // Host seams for isolated persistence tests.
    public interface ISaveSystem
    {
        string GetCurrentSlotPath();
        int ActiveSlotIndex { get; }
        void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class;
    }
    internal static class MMLog
    {
        internal static void WriteDebug(string value) { }
        internal static void WriteInfo(string value) { }
        internal static void WriteWarning(string value) { }
        internal static void WriteError(string value) { }
        internal static void WarnOnce(string key, string value) { }
    }
    internal static class PluginRunner { internal static bool IsQuitting = false; }
    internal static class GameRuntimeApiIds
    {
        internal const string GameLifecycle = "lifecycle", SaveRuntime = "save";
    }
    internal static class ModAPIRegistry
    {
        internal static readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        internal static bool IsAPIRegistered(string key) { return Values.ContainsKey(key); }
        internal static T GetAPI<T>(string key) { return (T)Values[key]; }
    }
}

public static class ModPersistenceContracts
{
    private static int _checks;
    private static void Check(bool result, string name)
    {
        if (!result) throw new Exception(name);
        _checks++;
    }
    public static void Run(string root)
    {
        Host host = new Host(root);
        ModAPIRegistry.Values[GameRuntimeApiIds.GameLifecycle] = host;
        ModAPIRegistry.Values[GameRuntimeApiIds.SaveRuntime] = host;
        SaveSystemImpl first = SaveSystemImpl.GetForMod("test.mod");
        SaveSystemImpl second = SaveSystemImpl.GetForMod("test.mod");
        Check(object.ReferenceEquals(first, second), "plugins share the mod registry");
        Data a = new Data(), b = new Data();
        first.RegisterModData("a", a);
        second.RegisterModData("b", b);
        a.Value = 11; b.Value = 22;
        host.Save();
        string file = Path.Combine(root, "mods/test.mod/data.json");
        string saved = File.ReadAllText(file);
        Check(saved.Contains("\"a\"") && saved.Contains("\"b\""), "both plugins save their entries");
        SaveSystemImpl.GetForMod("test.mod");
        host.Save();
        Check(File.ReadAllText(file) == saved, "empty plugin does not erase entries");
        a.Value = 99; b.Value = 99;
        host.Load();
        Check(a.Value == 11 && b.Value == 22, "load restores both plugins");
        Check(a.Restores == 1 && b.Restores == 1, "callbacks run once across load phases");
        a.Value = 77; b.Value = 88;
        host.Load();
        Check(a.Value == 11 && b.Value == 22 && a.Restores == 2, "same save reload restores state and callbacks");
        a.FailPrepare = true;
        host.Save();
        Check(File.ReadAllText(file) == saved, "preparation failure preserves previous file");
        a.FailPrepare = false; a.FailLegacy = true;
        host.Save();
        Check(File.ReadAllText(file) == saved, "legacy preparation failure preserves previous file");
        a.FailLegacy = false;
        int[] array = new int[] { 3, 4 };
        first.RegisterModData("array", array);
        host.Save(); array[0] = 90;
        host.Load();
        Check(array[0] == 3 && array[1] == 4, "root array restores in place");
        bool rejected = false;
        try { PersistenceFieldGraphSerializer.DeserializeOverwrite("[1]", array); }
        catch (FormatException) { rejected = true; }
        Check(rejected && array[0] == 3, "array length mismatch fails without mutation");
        Check(!object.ReferenceEquals(first, SaveSystemImpl.GetForMod("other.mod")), "different mods retain separate registries");
        Console.WriteLine("Persistence contracts: " + _checks + " passed, 0 failed.");
    }
    private sealed class Data : IModPersistenceLifecycle, IModPersistenceLogic
    {
        public int Value;
        [NonSerialized] public int Restores;
        [NonSerialized] public bool FailPrepare, FailLegacy;
        public void PrepareForSave(IModSaveContext context)
        {
            if (FailPrepare) { Value = 0; throw new Exception("prepare failure"); }
        }
        public void OnSaving(IModSaveContext context)
        {
            if (FailLegacy) { Value = 0; throw new Exception("legacy failure"); }
        }
        public void OnLoaded(IModSaveContext context) { }
        public void RestoreAfterLoad(IModSaveContext context) { Restores++; }
        public bool ValidateAfterLoad(IModSaveContext context, out string message) { message = null; return true; }
    }
    private sealed class Host : IGameLifecycleSource, ISaveRuntimeAdapter
    {
        private readonly string _root;
        internal Host(string root) { _root = root; }
        public event Action<object> BeforeSave, BeforeLoadSceneContents, AfterLoad;
        public event Action SessionStarted { add { } remove { } }
        public event Action NewGame { add { } remove { } }
        internal void Save() { BeforeSave(null); }
        internal void Load() { BeforeLoadSceneContents(null); AfterLoad(null); AfterLoad(null); }
        public string GetCurrentSlotPath() { return _root; }
        public int ActiveSlotIndex { get { return 1; } }
        public IModSaveContext GetCurrentSaveContext() { return new ModSaveContext(_root, 1, "standard", "same-save", null); }
        public void EnsureRuntimeReady() { }
        public void ResetRuntimeState() { }
        public string GetQuitHeartbeatDetail() { return "test"; }
    }
}
