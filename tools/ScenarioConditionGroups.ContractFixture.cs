public enum ScenarioConditionGroupMode { All, Any }
public enum ScenarioConditionKind { TimeReached }
public sealed class ScenarioConditionGroup {
    public ScenarioConditionGroupMode Mode;
    public List<ScenarioConditionRef> Conditions = new List<ScenarioConditionRef>();
    public List<ScenarioConditionGroup> Groups = new List<ScenarioConditionGroup>();
}
public sealed class ScenarioConditionRef {
    public string Id, TargetId, Comparison, StatId, TraitId, FlagId, FlagValue;
    public ScenarioConditionKind Kind;
    public int Quantity, StatValue;
    public object ActorRef, Time;
    public List<object> Properties = new List<object>();
}
public sealed class ScenarioDefinition { }
public sealed class ScenarioRuntimeState { }
public static class ScenarioActorXmlSerializer { public static object ReadActorRef(XmlElement element) { return null; } }
public sealed partial class ScenarioConditionGroupsContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    private static XmlElement Child(XmlElement parent, string name) { return parent == null ? null : parent[name]; }
    private static T ReadEnumAttribute<T>(XmlElement element, string name, T fallback) { return element.HasAttribute(name) ? (T)Enum.Parse(typeof(T), element.GetAttribute(name)) : fallback; }
    private static string AttributeOrChild(XmlElement element, string name, string child) { return element.GetAttribute(name); }
    private static int ReadIntAttribute(XmlElement element, string name, int fallback) { int value; return int.TryParse(element.GetAttribute(name), out value) ? value : fallback; }
    private static object ReadOptionalScheduleTime(XmlElement element) { return null; }
    private static void ReadProperties(XmlElement element, List<object> properties) { }
    private bool IsConditionSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason) { reason = null; return condition.TargetId == "pass"; }
    public static void Verify() {
        var document = new XmlDocument();
        document.LoadXml("<Group mode='All'><Conditions><ConditionRef targetId='pass'/></Conditions><Group mode='Any'><Conditions><ConditionRef targetId='fail'/><ConditionRef targetId='pass'/></Conditions><Group mode='All'><Conditions><ConditionRef targetId='fail'/></Conditions></Group></Group></Group>");
        var root = ReadConditionGroup(document.DocumentElement);
        Check(root.Conditions.Count == 1 && root.Groups.Count == 1, "A group must own only its direct condition list");
        Check(root.Groups[0].Conditions.Count == 2 && root.Groups[0].Groups[0].Conditions.Count == 1, "Nested groups must retain their own conditions");
        string reason;
        Check(new ScenarioConditionGroupsContractFixture().IsGroupSatisfied(new ScenarioDefinition(), root, new ScenarioRuntimeState(), out reason), "All containing an Any group must preserve Any semantics after XML load");
        document.LoadXml("<Group mode='All'><ConditionRef targetId='pass'/><Group mode='Any'><ConditionRef targetId='fail'/><ConditionRef targetId='pass'/></Group></Group>");
        var legacy = ReadConditionGroup(document.DocumentElement);
        Check(legacy.Conditions.Count == 1 && legacy.Groups[0].Conditions.Count == 2, "Direct condition syntax must not flatten descendant groups either");
    }
}
