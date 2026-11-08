public sealed class CustomScenarioEventArgs { public string EventType = "Selected"; }
namespace ModAPI.Core {
    public sealed class ModEntry { public string Id = "fixture"; }
    public static class ModRegistry { public static bool TryGetModByAssembly(Assembly assembly, out ModEntry entry) { entry = null; return false; } }
    public static class MMLog { public static void WarnOnce(string key, string message) { } }
}
public sealed partial class ScenarioEventsContractFixture {
    public static int Count;
    public static void Verify() {
        int delivered = 0;
        Action<CustomScenarioEventArgs> handlers = delegate { throw new Exception("failed subscriber"); };
        handlers += delegate { delivered++; };
        Raise(handlers, new CustomScenarioEventArgs());
        Count++;
        if (delivered != 1) throw new Exception("Scenario lifecycle events must reach later subscribers after an earlier subscriber throws");
        Raise(null, new CustomScenarioEventArgs());
        Raise(handlers, null);
        Count++;
    }
}
