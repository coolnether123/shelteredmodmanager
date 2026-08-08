using System;
using System.Collections.Generic;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal sealed class ScenarioCommandDispatcher
    {
        private readonly List<IScenarioCommandHandler> _handlers = new List<IScenarioCommandHandler>();
        private readonly Dictionary<Type, IScenarioCommandHandler> _handlerByCommandType =
            new Dictionary<Type, IScenarioCommandHandler>();

        public void Register(IScenarioCommandHandler handler)
        {
            if (handler == null)
                return;

            _handlers.Add(handler);
            _handlerByCommandType.Clear();
        }

        public ScenarioCommandDispatchResult DispatchDetailed(
            ScenarioAuthoringState state,
            ScenarioAuthoringCommand command)
        {
            if (command == null)
                return ScenarioCommandDispatchResult.Unhandled();

            Type commandType = command.GetType();
            IScenarioCommandHandler handler;
            if (!_handlerByCommandType.TryGetValue(commandType, out handler))
            {
                handler = FindSoleHandler(command);
                _handlerByCommandType.Add(commandType, handler);
            }

            return handler != null
                ? handler.Handle(state, command)
                : ScenarioCommandDispatchResult.Unhandled();
        }

        private IScenarioCommandHandler FindSoleHandler(ScenarioAuthoringCommand command)
        {
            IScenarioCommandHandler owner = null;
            for (int i = 0; i < _handlers.Count; i++)
            {
                IScenarioCommandHandler candidate = _handlers[i];
                if (candidate == null || !candidate.CanHandle(command))
                    continue;
                if (owner != null)
                {
                    throw new InvalidOperationException(
                        "Multiple scenario command handlers own " + command.GetType().FullName + ".");
                }
                owner = candidate;
            }

            return owner;
        }
    }

    internal struct ScenarioCommandDispatchResult
    {
        public bool Handled { get; set; }
        public bool Changed { get; set; }
        public string Message { get; set; }

        public static ScenarioCommandDispatchResult Unhandled()
        {
            return new ScenarioCommandDispatchResult();
        }
    }
}
