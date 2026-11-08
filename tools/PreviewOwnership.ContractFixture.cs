public enum ScenarioSpriteTargetComponentKind { Sprite }
public sealed class ScenarioRuntimeBinding { public bool IsPreview; public string RunId; }
public sealed class ScenarioPreviewResult { public bool Started; }
public sealed class Bindings {
    public ScenarioRuntimeBinding CurrentBinding;
    public void SetBinding(ScenarioRuntimeBinding value) { CurrentBinding = value; }
}
public sealed class DefinitionResolver {
    public string Owner;
    public void ClearPreview(string id) { if (Owner == id) Owner = null; }
}
public interface IScenarioSpriteSwapEngine { void Clear(string reason); }
public interface IScenarioSceneSpritePlacementEngine { void Clear(string reason); }
public sealed class Engines : IScenarioSpriteSwapEngine, IScenarioSceneSpritePlacementEngine {
    public int Clears;
    public void Clear(string reason) { Clears++; }
}
public static class ScenarioRuntimeCompositionRoot {
    public static Engines Value = new Engines();
    public static T Resolve<T>() { return (T)(object)Value; }
}
public static class ModRandomBridge {
    public static bool Fixed;
    public static void SetScenarioFixedSeedActive(bool active) { Fixed = active; }
}
public static class ScenarioRuntimeAssetFacade {
    public static int Stops;
    public static void StopRuntimeSpriteAnimation(string path, ScenarioSpriteTargetComponentKind kind) { Stops++; }
}
public static class MMLog { public static void WriteWarning(string text) {} }
public sealed partial class ScenarioPreviewSession {
    private readonly string _runId;
    private readonly Bindings _bindings;
    private readonly DefinitionResolver _definitions;
    private ScenarioRuntimeBinding _binding;
    private object _definition;
    private int? _questInstanceId;
    private bool _disposed;
    private readonly List<string> _animationPreviewPaths = new List<string> { "shared-path" };
    private readonly List<ScenarioSpriteTargetComponentKind> _animationPreviewKinds = new List<ScenarioSpriteTargetComponentKind> { ScenarioSpriteTargetComponentKind.Sprite };
    public ScenarioPreviewResult StartResult { get; private set; }
    public int QuestAbandons;
    private void AbandonQuestCarrier() { QuestAbandons++; }
    public ScenarioPreviewSession(string id, Bindings bindings, DefinitionResolver definitions) {
        _runId = id; _bindings = bindings; _definitions = definitions; StartResult = new ScenarioPreviewResult { Started = true };
    }
    public bool Active { get { return IsActive; } }
}
public static class PreviewOwnershipContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var bindings = new Bindings { CurrentBinding = new ScenarioRuntimeBinding { IsPreview = true, RunId = "B" } };
        var definitions = new DefinitionResolver { Owner = "B" };
        var old = new ScenarioPreviewSession("A", bindings, definitions);
        var current = new ScenarioPreviewSession("B", bindings, definitions);
        Check(!old.Active && current.Active, "A replaced preview must cease to own runtime operations");
        ModRandomBridge.Fixed = true; old.Dispose();
        Check(ModRandomBridge.Fixed, "Disposing an old preview must retain the current preview seed policy");
        Check(ScenarioRuntimeCompositionRoot.Value.Clears == 0 && ScenarioRuntimeAssetFacade.Stops == 0, "Disposing an old preview must retain current scene assets and animations");
        Check(bindings.CurrentBinding.RunId == "B" && definitions.Owner == "B", "An old preview must retain the current binding and definition");
        Check(old.QuestAbandons == 1, "An old preview must still release its own quest carrier");
        current.Dispose(); current.Dispose();
        Check(!current.Active && !ModRandomBridge.Fixed && bindings.CurrentBinding == null && definitions.Owner == null, "The current owner must release binding, definition and fixed seed");
        Check(ScenarioRuntimeCompositionRoot.Value.Clears == 2 && ScenarioRuntimeAssetFacade.Stops == 1 && current.QuestAbandons == 1, "Current cleanup must run once");
    }
}
