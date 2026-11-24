public class Obj_Base { public int objectId = 1; }
public class FamilyMember { }
public class Int_Base { }
public static class ObjectManager { public enum ObjectType { Undefined, Workbench } }
namespace ModAPI.Core { public static class MMLog { public static void WriteError(string message) { } } }
namespace ShelteredAPI.Hooks { }
namespace ShelteredAPI.Content { public static class ModLocalization { public static void Set(string key, string text) { } } }
namespace ShelteredAPI.Interactions {
    public class InteractionRegistry {
        public static InteractionRegistry For(ObjectManager.ObjectType type) { return new InteractionRegistry(); }
        public InteractionRegistry Add(string id, Type type) { return this; }
        public InteractionRegistry WithPriority(int priority) { return this; }
        public InteractionRegistry OnInjected(Action<Obj_Base, Int_Base> action) { return this; }
        public void Register() { }
    }
}
namespace ShelteredAPI.UI.Runtime {
    public sealed class ObjectPanelRegistration {
        public string InteractionId, InteractionText, ObjectId = null;
        public int Priority = 0;
        public ObjectManager.ObjectType ObjectType = ObjectManager.ObjectType.Workbench;
        public Action<ObjectPanelContext> Open;
        public Func<ObjectPanelContext, bool> CanOpen = null;
    }
    public sealed class ObjectPanelContext { public ObjectPanelContext(string id, Obj_Base target, FamilyMember member) { } }
}
namespace ShelteredAPI.UI.Internal.Runtime {
    internal sealed class RuntimeObjectPanelInteraction : Int_Base { public void Configure(string id) { } }
    public static class ObjectPanelContractFixture {
        public static int Count;
        private static void Check(bool value, string message) { Count++; if (!value) throw new Exception(message); }
        public static void Verify() {
            int oldCalls = 0, newCalls = 0;
            var first = new ShelteredAPI.UI.Runtime.ObjectPanelRegistration { InteractionId = "lease", InteractionText = "Open", Open = delegate { oldCalls++; } };
            IDisposable oldLease = RuntimeObjectPanelRegistry.Register(first);
            var second = new ShelteredAPI.UI.Runtime.ObjectPanelRegistration { InteractionId = "lease", InteractionText = "Open", Open = delegate { newCalls++; } };
            IDisposable newLease = RuntimeObjectPanelRegistry.Register(second);
            oldLease.Dispose();
            Check(RuntimeObjectPanelRegistry.TryOpen("shelteredapi.runtimeui.lease", new Obj_Base(), null) && oldCalls == 0 && newCalls == 1, "Disposing a replaced registration must preserve the new callback");
            newLease.Dispose();
            Check(!RuntimeObjectPanelRegistry.CanOpen("shelteredapi.runtimeui.lease", new Obj_Base(), null), "Disposing the current registration must disable its interaction");
            oldLease.Dispose(); newLease.Dispose();
            Check(!RuntimeObjectPanelRegistry.CanOpen("shelteredapi.runtimeui.lease", new Obj_Base(), null), "Repeated lease disposal must stay harmless");
        }
    }
}
