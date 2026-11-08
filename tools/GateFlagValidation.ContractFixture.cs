public enum ScenarioConditionKind { QuestActive, QuestCompleted, QuestFailed, BunkerExpansionUnlocked, ScenarioFlagSet, TimeReached, SurvivorPresent, SurvivorStatCheck, SurvivorTraitCheck, ItemQuantityAvailable, TechnologyUnlocked, CustomTrigger }
public sealed class ScenarioConditionRef { public ScenarioConditionKind Kind; public string TargetId, FlagId; public object ActorRef; public ScheduleTime Time; public int Quantity; }
public sealed class ScheduleTime { public int Day, Hour, Minute; }
public sealed class ScenarioConditionGroup { public List<ScenarioConditionRef> Conditions = new List<ScenarioConditionRef>(); public List<ScenarioConditionGroup> Groups = new List<ScenarioConditionGroup>(); }
public sealed class ScenarioGateDefinition { public string Id; public ScenarioConditionGroup Conditions = new ScenarioConditionGroup(); }
public sealed class ScenarioScheduledActionDefinition { public string Id, GateId; }
public sealed class JournalDef { public List<ScenarioScheduledActionDefinition> Entries = new List<ScenarioScheduledActionDefinition>(); }
public sealed class ScenarioDefinition { public List<ScenarioGateDefinition> Gates = new List<ScenarioGateDefinition>(); public List<ScenarioScheduledActionDefinition> ScheduledActions = new List<ScenarioScheduledActionDefinition>(); public JournalDef Journal = new JournalDef(); }
public interface IScenarioValidationRule { void Validate(ScenarioDefinition definition, string path, ValidationSummary summary); }
public sealed class ValidationSummary { public readonly List<string> Errors = new List<string>(); public void AddError(string code, string message) { Errors.Add(code); } }
public sealed class ScenarioDefinitionIndex {
    private readonly ScenarioDefinition _definition;
    public ScenarioDefinitionIndex(ScenarioDefinition value) { _definition = value; }
    public bool HasGate(string id) { return _definition.Gates.Any(g => g.Id == id); }
    public bool HasQuest(string id) { return false; }
    public bool HasExpansion(string id) { return false; }
    public bool HasFamilySurvivor(string id) { return false; }
    public bool HasFutureSurvivor(string id) { return false; }
    public bool HasTrigger(string id) { return false; }
}
public static class ScenarioActorReferenceIndex { public static bool Contains(ScenarioDefinition d, object actor, bool a, bool b) { return true; } public static string Format(object actor) { return "actor"; } }
public static class ItemManager { public enum ItemType { Item } }
public static class ContentInjector { public static bool ResolveItemType(string id, out ItemManager.ItemType type) { type = ItemManager.ItemType.Item; return true; } }
public static class GateFlagValidationContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    private static ScenarioGateDefinition Gate(string id, string flag) {
        var gate = new ScenarioGateDefinition { Id = id };
        gate.Conditions.Conditions.Add(new ScenarioConditionRef { Kind = ScenarioConditionKind.ScenarioFlagSet, TargetId = flag });
        return gate;
    }
    public static void Verify() {
        var def = new ScenarioDefinition(); def.Gates.Add(Gate("ready", "ready"));
        var summary = new ValidationSummary(); new GateConditionValidationRule().Validate(def, null, summary);
        Check(summary.Errors.Count == 0, "A flag named after its gate is a flag lookup, not a circular gate dependency");
        def.Gates.Clear(); def.Gates.Add(Gate("A", "B")); def.Gates.Add(Gate("B", "A"));
        summary = new ValidationSummary(); new GateConditionValidationRule().Validate(def, null, summary);
        Check(summary.Errors.Count == 0, "Independent flag names must not become gate dependency edges");
        def.Gates.Clear(); def.Gates.Add(Gate("A", "B")); def.Gates.Add(Gate("B", "C")); def.Gates.Add(Gate("C", "B"));
        summary = new ValidationSummary(); new GateConditionValidationRule().Validate(def, null, summary);
        Check(summary.Errors.Count == 0, "A flag-name chain into a repeated name must terminate without inventing gate edges");
        def.ScheduledActions.Add(new ScenarioScheduledActionDefinition { Id = "bad", GateId = "missing" });
        summary = new ValidationSummary(); new GateConditionValidationRule().Validate(def, null, summary);
        Check(summary.Errors.Contains("events.action.unknown_gate"), "Real action gate references must still be checked");
    }
}
