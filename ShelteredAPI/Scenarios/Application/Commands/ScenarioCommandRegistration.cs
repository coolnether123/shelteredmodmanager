using System;

namespace ShelteredAPI.Scenarios
{
    internal delegate bool ScenarioCommandCallback(ScenarioAuthoringState state, string actionId, out string message);

    internal sealed class ScenarioCommandRegistration : IScenarioCommandHandler
    {
        private readonly Func<string, bool> _matcher;
        private readonly ScenarioCommandCallback _callback;

        private ScenarioCommandRegistration(Func<string, bool> matcher, ScenarioCommandCallback callback)
        {
            _matcher = matcher;
            _callback = callback;
        }

        public static ScenarioCommandRegistration ForExact(string actionId, ScenarioCommandCallback callback)
        {
            return new ScenarioCommandRegistration(
                delegate(string candidate) { return string.Equals(candidate, actionId, StringComparison.Ordinal); },
                callback);
        }

        public static ScenarioCommandRegistration ForPrefix(string prefix, ScenarioCommandCallback callback)
        {
            return new ScenarioCommandRegistration(
                delegate(string candidate) { return !string.IsNullOrEmpty(candidate) && candidate.StartsWith(prefix, StringComparison.Ordinal); },
                callback);
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = _matcher != null && _matcher(actionId);
            if (!handled)
            {
                message = null;
                return false;
            }

            if (_callback == null)
            {
                message = null;
                return false;
            }

            return _callback(state, actionId, out message);
        }
    }
}
