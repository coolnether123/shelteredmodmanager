// Compiled inside an isolated namespace with the production collection owners.
namespace ModAPI.Core { }
namespace UnityEngine { public struct Vector2 { } public struct Vector3 { } public struct Color { } }
namespace HarmonyLib {
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string method) { } }
}
namespace ModAPI.Harmony {
    public enum PatchDomain { SaveFlow }
    public enum PatchStartupTiming { BootCritical, GameplayDeferred }
    public class PatchPolicyAttribute : Attribute {
        public PatchPolicyAttribute(PatchDomain domain, string name) { }
        public string TargetBehavior { get; set; } public string FailureMode { get; set; }
        public string RollbackStrategy { get; set; } public PatchStartupTiming StartupTiming { get; set; }
    }
}
public interface ISaveable {
    bool IsReadyForLoad(); bool IsRelocationEnabled(); bool SaveLoad(SaveData data);
}
public class SaveManager {
    public static SaveManager instance = new SaveManager();
    public void RegisterSaveable(ISaveable collection) { }
}
public class GameTime { }
public sealed class SaveData {
    public bool isLoading; public bool isSaving { get { return !isLoading; } }
    public readonly Dictionary<string, object> Values;
    public readonly HashSet<string> Groups;
    private readonly Stack<string> parents = new Stack<string>(); private string group = "";
    public SaveData() { Values = new Dictionary<string, object>(); Groups = new HashSet<string>(); }
    public SaveData(SaveData saved) { Values = saved.Values; Groups = saved.Groups; isLoading = true; }
    public void GroupStart(string name) {
        string path = group + "/" + name;
        if (isLoading && !Groups.Contains(path)) throw new MissingGroupException();
        Groups.Add(path); parents.Push(group); group = path;
    }
    public void GroupEnd() { group = parents.Pop(); }
    public bool SaveLoad<T>(string key, ref T value) {
        string path = group + "/" + key;
        if (!isLoading) { Values[path] = value; return true; }
        object saved; if (!Values.TryGetValue(path, out saved)) return false;
        value = (T)saved; return true;
    }
    public class MissingGroupException : Exception { }
}
public static class PersistenceContractFixture {
    public static int Count;
    private static void Check(bool condition, string message) { Count++; if (!condition) throw new Exception(message); }
    public static void Verify() {
        var list = new ShelteredAPI.Persistence.ShelteredPersistentList<int>("fixture.list");
        var dict = new ShelteredAPI.Persistence.ShelteredPersistentDictionary<string>("fixture.dict");
        list.Add(7); dict["value"] = "saved";
        var saved = new SaveData(); list.SaveLoad(saved); dict.SaveLoad(saved);
        list.Add(8); dict["other"] = "stale";
        var loaded = new SaveData(saved); list.SaveLoad(loaded); dict.SaveLoad(loaded);
        Check(list.Count == 1 && list[0] == 7, "List must round-trip and replace the old contents");
        Check(dict.Count == 1 && dict["value"] == "saved", "Dictionary must round-trip and replace the old contents");
        var absent = new SaveData { isLoading = true };
        try { list.SaveLoad(absent); } catch (SaveData.MissingGroupException) { }
        try { dict.SaveLoad(absent); } catch (SaveData.MissingGroupException) { }
        Check(list.Count == 0 && dict.Count == 0, "Missing groups must not retain data from another slot");
        list.Add(99); dict["old"] = "old";
        var reset = typeof(ShelteredAPI.Persistence.ModPersistence).GetNestedType("GameTime_Awake_Patch", System.Reflection.BindingFlags.NonPublic);
        reset.GetMethod("Postfix", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        Check(list.Count == 0 && dict.Count == 0, "Starting a new game must clear the previous session's collections");
    }
}
