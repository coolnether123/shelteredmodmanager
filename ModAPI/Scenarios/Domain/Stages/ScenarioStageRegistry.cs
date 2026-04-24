using System;
using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public sealed class ScenarioStageRegistry
    {
        private readonly List<ScenarioStageDefinition> _definitions;

        public ScenarioStageRegistry()
        {
            _definitions = new List<ScenarioStageDefinition>();
            Register(new ScenarioStageDefinition(ScenarioStageKind.Bunker, "Bunker", 0, ScenarioStageKind.None, true, true, "Shelter structure, visual layers, and authored bunker edits."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.BunkerBackground, "Background", 0, ScenarioStageKind.Bunker, false, true, "Back-layer bunker authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.BunkerSurface, "Surface", 1, ScenarioStageKind.Bunker, false, true, "Surface-layer bunker authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.BunkerInside, "Inside", 2, ScenarioStageKind.Bunker, false, true, "Interior bunker authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.InventoryStorage, "Inventory / Storage", 1, ScenarioStageKind.None, true, true, "Starting inventory and storage authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.People, "People", 2, ScenarioStageKind.None, true, true, "Family and character authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.Events, "Events", 3, ScenarioStageKind.None, true, true, "Triggers and event authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.Quests, "Quests", 4, ScenarioStageKind.None, true, true, "Quest authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.Map, "Map", 5, ScenarioStageKind.None, true, true, "Map-facing scenario authoring."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.Test, "Test", 6, ScenarioStageKind.None, true, true, "Playtest and live-apply workflow."));
            Register(new ScenarioStageDefinition(ScenarioStageKind.Publish, "Publish", 7, ScenarioStageKind.None, true, true, "Validation and publish readiness."));
        }

        public ScenarioStageDefinition[] GetAll()
        {
            return _definitions.ToArray();
        }

        public ScenarioStageDefinition[] GetTopLevel()
        {
            List<ScenarioStageDefinition> result = new List<ScenarioStageDefinition>();
            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioStageDefinition definition = _definitions[i];
                if (definition != null && definition.IsTopLevel)
                    result.Add(definition);
            }

            return result.ToArray();
        }

        public ScenarioStageDefinition[] GetChildren(ScenarioStageKind parentKind)
        {
            List<ScenarioStageDefinition> result = new List<ScenarioStageDefinition>();
            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioStageDefinition definition = _definitions[i];
                if (definition != null && definition.ParentKind == parentKind)
                    result.Add(definition);
            }

            return result.ToArray();
        }

        public ScenarioStageDefinition Find(ScenarioStageKind kind)
        {
            for (int i = 0; i < _definitions.Count; i++)
            {
                ScenarioStageDefinition definition = _definitions[i];
                if (definition != null && definition.Kind == kind)
                    return definition;
            }

            return null;
        }

        private void Register(ScenarioStageDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            if (Find(definition.Kind) != null)
                throw new InvalidOperationException("Scenario stage is already registered: " + definition.Kind);

            _definitions.Add(definition);
            _definitions.Sort(CompareDefinitions);
        }

        private static int CompareDefinitions(ScenarioStageDefinition left, ScenarioStageDefinition right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int parent = left.ParentKind.CompareTo(right.ParentKind);
            if (parent != 0)
                return parent;

            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;

            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
