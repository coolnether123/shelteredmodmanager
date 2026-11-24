namespace UnityEngine {
    public class GameObject { public bool Destroyed; }
    public class Object { public static void Destroy(GameObject value) { value.Destroyed = true; } }
}
namespace ModAPI.Core { internal static class MMLog { public static void WriteError(string message) { } } }
namespace ShelteredAPI.Content { }
namespace ShelteredAPI.UI.Internal.Runtime {
    internal sealed class RuntimeUiPanelRecord {
        public string PanelId;
        public string OwnerId;
        public UnityEngine.GameObject Root;
        public bool RebindRequested = false;
        public Action<RuntimeUiPanelRecord> Close;
    }
    public static class RuntimeUiContractFixture {
        public static int Count;
        private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
        public static void Verify() {
            Action<RuntimeUiPanelRecord> fail = delegate { throw new InvalidOperationException("close callback failed"); };
            var root = new UnityEngine.GameObject();
            var record = new RuntimeUiPanelRecord { PanelId = "one", OwnerId = "owner", Root = root, Close = fail };
            RuntimeUiRegistry.Register(record);
            RuntimeUiRegistry.Close("one");
            Check(root.Destroyed && record.Root == null && !RuntimeUiRegistry.Contains("one"), "A throwing close callback must not leave a visible unregistered panel");
            root = new UnityEngine.GameObject();
            record = new RuntimeUiPanelRecord { PanelId = "two", OwnerId = "owner", Root = root, Close = fail };
            RuntimeUiRegistry.Register(record);
            RuntimeUiRegistry.CloseOwner("owner");
            Check(root.Destroyed && record.Root == null, "Owner cleanup must destroy panels after close callbacks fail");
            root = new UnityEngine.GameObject();
            record = new RuntimeUiPanelRecord { PanelId = "three", Root = root, Close = fail };
            RuntimeUiRegistry.Register(record);
            RuntimeUiRegistry.CloseAll();
            Check(root.Destroyed && record.Root == null, "Session cleanup must destroy panels after close callbacks fail");
        }
    }
}
