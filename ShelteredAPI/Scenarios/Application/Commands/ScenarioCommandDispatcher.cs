using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioCommandDispatcher
    {
        private readonly List<IScenarioCommandHandler> _handlers = new List<IScenarioCommandHandler>();

        public ScenarioCommandDispatcher()
        {
        }

        public ScenarioCommandDispatcher(IEnumerable<IScenarioCommandHandler> handlers)
        {
            if (handlers == null)
                return;

            foreach (IScenarioCommandHandler handler in handlers)
                Register(handler);
        }

        public void Register(IScenarioCommandHandler handler)
        {
            if (handler != null)
                _handlers.Add(handler);
        }

        public bool Dispatch(ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            for (int i = 0; i < _handlers.Count; i++)
            {
                IScenarioCommandHandler handler = _handlers[i];
                if (handler == null)
                    continue;

                bool handled;
                bool changed = handler.TryHandle(state, actionId, out handled, out message);
                if (handled)
                    return changed || !string.IsNullOrEmpty(message);
            }

            return false;
        }
    }
}
