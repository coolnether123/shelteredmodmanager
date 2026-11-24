public enum SliderStepMode { Continuous, Stepped }
public sealed class SettingDefinition { public float? StepSize; public SliderStepMode SliderStepMode; public object Value; }
public sealed class UIInput { public string value; public bool isSelected; }
public static class Mathf {
    public static int Max(int left, int right) { return Math.Max(left, right); }
    public static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
    public static int RoundToInt(float value) { return (int)Math.Round(value); }
    public static float Round(float value) { return (float)Math.Round(value); }
}
public static class SpineWidgetRuntime {
    public static bool TryApplyValue(SettingDefinition def, object obj, object value) { def.Value = value; return true; }
    public static void NotifyChange(SettingDefinition def, object obj, object panel) { }
    public static T GetValue<T>(SettingDefinition def, object obj) { return (T)def.Value; }
}
public static partial class SliderSubmitContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    public static void Verify() {
        var def = new SettingDefinition { StepSize = 5, SliderStepMode = SliderStepMode.Stepped, Value = 3 };
        Submit(def, "14", 3, 14, true);
        Check((int)def.Value == 13, "Typed integer steps must use the slider minimum as their origin");
        Submit(def, "3", 3, 14, true);
        Check((int)def.Value == 3, "Typing the minimum must preserve the minimum");
        def.SliderStepMode = SliderStepMode.Continuous;
        Submit(def, "99", 3, 14, true);
        Check((int)def.Value == 14, "Typed values must stay within the declared range");
        def.Value = 0f; Submit(def, "0.75", 0, 1, false);
        Check(Math.Abs((float)def.Value - 0.75f) < 0.001f, "Continuous float submission must remain supported");
    }
}
