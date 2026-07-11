using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Application.Effects{
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
            bool retryable;
            return Dispatch(definition, effect, state, out message, out retryable);
        }

        public bool Dispatch(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message, out bool retryable)
        {
            message = null;
            retryable = false;
            if (effect == null)
                return true;

            for (int i = 0; i < _handlers.Count; i++)
            {
                IScenarioEffectHandler handler = _handlers[i];
                if (handler != null && handler.CanHandle(effect.Kind))
                {
                    IScenarioRetryableEffectHandler retryableHandler = handler as IScenarioRetryableEffectHandler;
                    return retryableHandler != null
                        ? retryableHandler.Handle(definition, effect, state, out message, out retryable)
                        : handler.Handle(definition, effect, state, out message);
                }
            }

            message = "No effect handler registered for " + effect.Kind + ".";
            return false;
        }
    }
}
