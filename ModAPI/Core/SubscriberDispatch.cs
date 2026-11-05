using System;
using System.Reflection;

namespace ModAPI.Core
{
    // Shared by framework and game bridges. Always pass the original multicast delegate.
    internal static class SubscriberDispatch
    {
        internal static void Invoke(Delegate subscribers, string eventName, params object[] arguments)
        {
            if (subscribers == null) return;
            foreach (Delegate handler in subscribers.GetInvocationList())
            {
                try { handler.DynamicInvoke(arguments); }
                catch (Exception error)
                {
                    Type ownerType = handler.Target != null ? handler.Target.GetType() : handler.Method.DeclaringType;
                    Assembly assembly = ownerType != null ? ownerType.Assembly : handler.Method.Module.Assembly;
                    ModEntry entry;
                    string owner = ModRegistry.TryGetModByAssembly(assembly, out entry) && entry != null
                        ? entry.Id : assembly.GetName().Name;
                    Exception cause = error is TargetInvocationException && error.InnerException != null
                        ? error.InnerException : error;
                    string identity = assembly.FullName + ":" + handler.Method.Module.ModuleVersionId
                        + ":" + handler.Method.MetadataToken;
                    MMLog.WarnOnce("SubscriberDispatch." + eventName + "." + identity,
                        "[" + owner + "] " + eventName + " handler " + handler.Method.Name + " threw: " + cause);
                }
            }
        }
    }
}
