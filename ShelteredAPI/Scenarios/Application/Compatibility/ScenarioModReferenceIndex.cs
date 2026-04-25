using System;
using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioModReferenceIndex
    {
        private readonly List<ScenarioModDependencyDefinition> _dependencies = new List<ScenarioModDependencyDefinition>();
        private readonly List<string> _unknownReferences = new List<string>();

        public IList<ScenarioModDependencyDefinition> Dependencies
        {
            get { return _dependencies; }
        }

        public IList<string> UnknownReferences
        {
            get { return _unknownReferences; }
        }

        public void Add(string modId, string version, ScenarioModDependencyKind kind, ScenarioModReferenceReason reason, string reference, bool manual)
        {
            if (string.IsNullOrEmpty(modId))
            {
                if (!string.IsNullOrEmpty(reference))
                    _unknownReferences.Add(reference);
                return;
            }

            ScenarioModDependencyDefinition dependency = Find(modId);
            if (dependency == null)
            {
                dependency = new ScenarioModDependencyDefinition();
                dependency.ModId = modId;
                dependency.Version = version;
                dependency.Kind = kind;
                dependency.Manual = manual;
                _dependencies.Add(dependency);
            }

            if (!dependency.Reasons.Contains(reason))
                dependency.Reasons.Add(reason);
            if (!string.IsNullOrEmpty(reference) && !Contains(dependency.ContentReferences, reference))
                dependency.ContentReferences.Add(reference);
            if (manual)
                dependency.Manual = true;
            if (dependency.Kind != ScenarioModDependencyKind.Required && kind == ScenarioModDependencyKind.Required)
                dependency.Kind = kind;
        }

        private ScenarioModDependencyDefinition Find(string modId)
        {
            for (int i = 0; i < _dependencies.Count; i++)
            {
                if (_dependencies[i] != null && string.Equals(_dependencies[i].ModId, modId, StringComparison.OrdinalIgnoreCase))
                    return _dependencies[i];
            }
            return null;
        }

        private static bool Contains(IList<string> values, string value)
        {
            for (int i = 0; values != null && i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
