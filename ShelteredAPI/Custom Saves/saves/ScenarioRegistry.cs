using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Saves
{
    public static class ScenarioRegistry
    {
        private static readonly Dictionary<string, ScenarioDescriptor> _map = new Dictionary<string, ScenarioDescriptor>(StringComparer.OrdinalIgnoreCase);

        public static void RegisterScenario(ScenarioDescriptor desc)
        {
            if (desc == null || string.IsNullOrEmpty(desc.id)) return;
            _map[desc.id] = desc;
        }

        public static ScenarioDescriptor GetScenario(string scenarioId)
        {
            ScenarioDescriptor d;
            if (_map.TryGetValue(scenarioId, out d)) return d;
            return new ScenarioDescriptor { id = scenarioId ?? "Unknown", displayName = scenarioId ?? "Unknown" };
        }

        public static ScenarioDescriptor[] ListScenarios()
        {
            var arr = new ScenarioDescriptor[_map.Count];
            int i = 0;
            foreach (var kv in _map) arr[i++] = kv.Value;
            return arr;
        }
    }
}

