using System.Collections.Generic;

using ShelteredAPI.Scenarios.Application.Authoring;
namespace ShelteredAPI.Scenarios.Application.Commands{
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
            ScenarioCommandDispatchResult result = DispatchDetailed(state, actionId);
            message = result.Message;
            return result.Result;
        }

        public ScenarioCommandDispatchResult DispatchDetailed(ScenarioAuthoringState state, string actionId)
        {
            ScenarioCommandDispatchResult result = new ScenarioCommandDispatchResult();
            for (int i = 0; i < _handlers.Count; i++)
            {
                IScenarioCommandHandler handler = _handlers[i];
                if (handler == null)
                    continue;

                bool handled;
                string message;
                bool changed = handler.TryHandle(state, actionId, out handled, out message);
                if (handled)
                {
                    result.Handled = true;
                    result.Changed = changed;
                    result.Message = message;
                    result.Result = changed;
                    return result;
                }
            }

            return result;
        }
    }

    internal sealed class ScenarioCommandDispatchResult
    {
        public bool Handled { get; set; }
        public bool Changed { get; set; }
        public bool Result { get; set; }
        public string Message { get; set; }
    }
}
