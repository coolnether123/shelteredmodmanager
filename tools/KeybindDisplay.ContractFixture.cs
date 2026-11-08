public enum KeyCode { None, Escape, F }
public class UILabel { public string text; }
public class MonoBehaviour { public readonly GameObject gameObject = new GameObject(); }
public class GameObject { public string name = "test"; }
public static class Time { public static int frameCount; public static float realtimeSinceStartup; }
public static class MMLog { public static void WriteInfo(string text) {} }
public static class KeybindDisplayContractFixture {
    public static int Count;
    private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
    private static void Invoke(object listener, string name, params object[] args) {
        listener.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(listener, args);
    }
    public static void Verify() {
        var listener = new KeybindCaptureListener { ValueLabel = new UILabel(), DisplayTextProvider = () => "F", OnCaptured = code => {}, OnCanceled = () => {} };
        Invoke(listener, "Update");
        Check(listener.ValueLabel.text == "F", "The provider must render the initial binding");
        listener.StartCapture();
        Check(listener.ValueLabel.text == "PRESS KEY...", "Capture must show its prompt");
        Invoke(listener, "CancelCapture", false); Invoke(listener, "Update");
        Check(listener.ValueLabel.text == "F", "Cancelling capture must restore an unchanged binding");
        listener.StartCapture(); Time.realtimeSinceStartup = 1; UnityEngine.Input.Pressed = KeyCode.F;
        Invoke(listener, "Update"); UnityEngine.Input.Pressed = KeyCode.None; Invoke(listener, "Update");
        Check(listener.ValueLabel.text == "F", "Capturing the existing key must restore the binding text");
    }
}
