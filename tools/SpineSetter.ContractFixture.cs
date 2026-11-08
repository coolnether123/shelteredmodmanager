public sealed class SettingDefinition {
    public string Id = "fixture"; public Type EnumType; public object DefaultValue;
    public Func<object, object, bool> Validate; public Action<object, object> Setter;
}
public static class MMLog { public static void WriteError(string text) { } }
public enum SetterChoice { First, Second }
public static class SpineSetterContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var def = new SettingDefinition { Setter = (obj, value) => { throw new InvalidOperationException("fixture"); } };
        Check(!SpineWidgetRuntime.TryApplyValue(def, null, 3), "A throwing setter must not report a successful setting change");
        def.Setter = null;
        Check(!SpineWidgetRuntime.TryApplyValue(def, null, 3), "An absent setter must not report a successful change");
        object stored = null; def.DefaultValue = 1; def.Setter = (obj, value) => stored = value;
        Check(SpineWidgetRuntime.TryApplyValue(def, null, "3") && (int)stored == 3, "Successful integer conversion must still apply");
        def.EnumType = typeof(SetterChoice);
        Check(SpineWidgetRuntime.TryApplyValue(def, null, "Second") && (SetterChoice)stored == SetterChoice.Second, "Enum conversion must still apply");
        Check(!SpineWidgetRuntime.TryApplyValue(def, null, "missing"), "A failed conversion must report failure");
        def.Validate = (value, obj) => false;
        Check(!SpineWidgetRuntime.TryApplyValue(def, null, "First") && (SetterChoice)stored == SetterChoice.Second, "Validation failure must preserve the previous value");
    }
}
