using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioEffectDispatcher
    {
        private readonly List<IScenarioEffectHandler> _handlers = new List<IScenarioEffectHandler>();

        public void Register(IScenarioEffectHandler handler)
        {
            if (handler != null && !_handlers.Contains(handler))
                _handlers.Add(handler);
        }

        public bool CanHandle(ScenarioEffectKind kind)
        {
            for (int i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i] != null && _handlers[i].CanHandle(kind))
                    return true;
            }
            return false;
        }

        public bool Dispatch(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            message = null;
            if (effect == null)
                return true;

            for (int i = 0; i < _handlers.Count; i++)
            {
                IScenarioEffectHandler handler = _handlers[i];
                if (handler != null && handler.CanHandle(effect.Kind))
                    return handler.Handle(definition, effect, state, out message);
            }

            message = "No effect handler registered for " + effect.Kind + ".";
            return false;
        }
    }
}
