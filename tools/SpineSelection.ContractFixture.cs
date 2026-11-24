public sealed class GameObject { public int layer; }
public sealed class Transform { public GameObject gameObject = new GameObject(); }
public struct Vector3 { public static Vector3 zero; public Vector3(float x, float y, float z) { } }
public static class UIWidget { public enum Pivot { Left } }
public static class NGUIText { public enum Alignment { Center } }
public sealed class UILabel { public UIWidget.Pivot pivot; public NGUIText.Alignment alignment; public int width; public string text; public GameObject gameObject = new GameObject(); }
public static class NGUITools { public static GameObject AddChild(GameObject parent) { return new GameObject(); } public static void SetLayer(GameObject obj, int layer) { } }
public static class UIUtil {
    public static Action Click;
    public static UILabel CreateLabelQuick(GameObject obj, string text, int size, Vector3 pos) { return new UILabel { text = text }; }
    public static void CreateButton(GameObject obj, object template, string text, int width, int height, Vector3 pos, Action click) { Click = click; }
}
public static class SpineWidgetFactory { public static object ButtonTemplate; }
public sealed class ModSettingsPanel { }
public sealed class SettingDefinition { public Type EnumType; public object Value; public Func<object, IEnumerable<string>> GetOptions; }
public static class SpineWidgetRuntime {
    public static string GetLabel(SettingDefinition def) { return "fixture"; }
    public static void SetTooltip(GameObject obj, SettingDefinition def) { }
    public static T GetValue<T>(SettingDefinition def, object settings) { return (T)def.Value; }
    public static bool TryApplyValue(SettingDefinition def, object settings, object value) { def.Value = value; return true; }
    public static void NotifyChange(SettingDefinition def, object settings, ModSettingsPanel panel) { }
}
public enum Selection { First, Second }
public enum EmptySelection { }
public static class SpineSelectionContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var def = new SettingDefinition { EnumType = typeof(Selection), Value = (Selection)99 };
        SpineSelectionWidgetBuilder.CreateEnumWidget(def, new Transform(), null, null); UIUtil.Click();
        Check((Selection)def.Value == Selection.First, "An unknown enum selection must cycle to the first value");
        UIUtil.Click(); Check((Selection)def.Value == Selection.Second, "An existing enum selection must cycle normally");
        def.EnumType = typeof(EmptySelection); def.Value = null;
        SpineSelectionWidgetBuilder.CreateEnumWidget(def, new Transform(), null, null); UIUtil.Click();
        Check(def.Value == null, "An empty enum must leave its value unchanged");
        def = new SettingDefinition { Value = "removed", GetOptions = ignored => new[] { "first", "second" } };
        SpineSelectionWidgetBuilder.CreateChoiceWidget(def, new Transform(), null, null); UIUtil.Click();
        Check((string)def.Value == "first", "A removed choice must cycle to the first option");
        UIUtil.Click(); Check((string)def.Value == "second", "An existing choice must cycle normally");
        def.GetOptions = ignored => new string[0]; UIUtil.Click();
        Check((string)def.Value == "second", "An empty choice list must leave its value unchanged");
    }
}
