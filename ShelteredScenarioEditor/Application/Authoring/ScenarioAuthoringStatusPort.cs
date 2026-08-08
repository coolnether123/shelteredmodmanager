using System;

using ModAPI.Core;

namespace ShelteredScenarioEditor.Application.Authoring
{
    internal sealed class ScenarioAuthoringStatusPort
    {
        internal event Action<string> Published;

        internal void Publish(string message)
        {
            Action<string> handler = Published;
            if (handler == null)
                return;

            Delegate[] subscribers = handler.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try { ((Action<string>)subscribers[i])(message); }
                catch (Exception ex) { MMLog.WriteWarning("[ScenarioAuthoringStatusPort] Subscriber failed: " + ex.Message); }
            }
        }
    }
}
