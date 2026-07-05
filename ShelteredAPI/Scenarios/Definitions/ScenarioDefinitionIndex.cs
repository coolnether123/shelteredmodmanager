using System;
using System.Collections.Generic;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Domain.Bunker;
namespace ShelteredAPI.Scenarios.Definitions{
    internal sealed class ScenarioDefinitionIndex
    {
        private readonly HashSet<string> _gates;
        private readonly HashSet<string> _triggers;
        private readonly HashSet<string> _quests;
        private readonly HashSet<string> _conditions;
        private readonly HashSet<string> _expansions;
        private readonly HashSet<string> _objects;
        private readonly HashSet<string> _futureSurvivors;
        private readonly HashSet<string> _familySurvivors;

        private ScenarioDefinitionIndex()
        {
            _gates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _triggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _quests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _conditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _expansions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _objects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _futureSurvivors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _familySurvivors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public ScenarioDefinitionIndex(ScenarioDefinition definition)
            : this()
        {
            AddGates(definition);
            AddTriggers(definition != null ? definition.TriggersAndEvents : null);
            AddQuests(definition);
            AddConditions(definition != null ? definition.WinLossConditions : null);
            AddExpansions(definition);
            AddObjects(definition);
            AddSurvivors(definition);
        }

        public ScenarioDefinitionIndex(TriggersAndEventsDefinition triggersAndEvents)
            : this()
        {
            AddTriggers(triggersAndEvents);
        }

        public ScenarioDefinitionIndex(WinLossConditionsDefinition conditions)
            : this()
        {
            AddConditions(conditions);
        }

        public bool HasGate(string id)
        {
            return Has(_gates, id);
        }

        public bool HasTrigger(string id)
        {
            return Has(_triggers, id);
        }

        public bool HasQuest(string id)
        {
            return Has(_quests, id);
        }

        public bool HasCondition(string id)
        {
            return Has(_conditions, id);
        }

        public bool HasExpansion(string id)
        {
            return Has(_expansions, id);
        }

        public bool HasObject(string id)
        {
            return Has(_objects, id);
        }

        public bool HasFutureSurvivor(string id)
        {
            return Has(_futureSurvivors, id);
        }

        public bool HasFamilySurvivor(string id)
        {
            return Has(_familySurvivors, id);
        }

        private static bool Has(HashSet<string> ids, string id)
        {
            return !string.IsNullOrEmpty(id) && ids != null && ids.Contains(id);
        }

        private void AddGates(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.Gates != null && i < definition.Gates.Count; i++)
                Add(_gates, definition.Gates[i] != null ? definition.Gates[i].Id : null);
        }

        private void AddTriggers(TriggersAndEventsDefinition events)
        {
            for (int i = 0; events != null && events.Triggers != null && i < events.Triggers.Count; i++)
                Add(_triggers, events.Triggers[i] != null ? events.Triggers[i].Id : null);
        }

        private void AddQuests(ScenarioDefinition definition)
        {
            QuestAuthoringDefinition quests = definition != null ? definition.Quests : null;
            for (int i = 0; quests != null && quests.Quests != null && i < quests.Quests.Count; i++)
                Add(_quests, quests.Quests[i] != null ? quests.Quests[i].Id : null);
        }

        private void AddConditions(WinLossConditionsDefinition conditions)
        {
            for (int i = 0; conditions != null && conditions.WinConditions != null && i < conditions.WinConditions.Count; i++)
                Add(_conditions, conditions.WinConditions[i] != null ? conditions.WinConditions[i].Id : null);
            for (int i = 0; conditions != null && conditions.LossConditions != null && i < conditions.LossConditions.Count; i++)
                Add(_conditions, conditions.LossConditions[i] != null ? conditions.LossConditions[i].Id : null);
        }

        private void AddExpansions(ScenarioDefinition definition)
        {
            ScenarioBunkerGridDefinition grid = definition != null ? definition.BunkerGrid : null;
            for (int i = 0; grid != null && grid.Expansions != null && i < grid.Expansions.Count; i++)
                Add(_expansions, grid.Expansions[i] != null ? grid.Expansions[i].Id : null);
        }

        private void AddObjects(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
                Add(_objects, definition.BunkerEdits.ObjectPlacements[i] != null ? definition.BunkerEdits.ObjectPlacements[i].ScenarioObjectId : null);
        }

        private void AddSurvivors(ScenarioDefinition definition)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
                Add(_futureSurvivors, definition.FamilySetup.FutureSurvivors[i] != null ? definition.FamilySetup.FutureSurvivors[i].Id : null);
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null && i < definition.FamilySetup.Members.Count; i++)
                Add(_familySurvivors, definition.FamilySetup.Members[i] != null ? definition.FamilySetup.Members[i].Name : null);
        }

        private static void Add(HashSet<string> ids, string id)
        {
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }
    }

    internal static class ScenarioDefinitionLookup
    {
        public static bool HasGate(ScenarioDefinition definition, string id)
        {
            return new ScenarioDefinitionIndex(definition).HasGate(id);
        }

        public static bool HasTrigger(ScenarioDefinition definition, string id)
        {
            return new ScenarioDefinitionIndex(definition).HasTrigger(id);
        }

        public static bool HasTrigger(TriggersAndEventsDefinition triggersAndEvents, string id)
        {
            return new ScenarioDefinitionIndex(triggersAndEvents).HasTrigger(id);
        }

        public static bool HasQuest(ScenarioDefinition definition, string id)
        {
            return new ScenarioDefinitionIndex(definition).HasQuest(id);
        }

        public static bool HasCondition(ScenarioDefinition definition, string id)
        {
            return new ScenarioDefinitionIndex(definition).HasCondition(id);
        }

        public static bool HasCondition(WinLossConditionsDefinition conditions, string id)
        {
            return new ScenarioDefinitionIndex(conditions).HasCondition(id);
        }

        public static bool HasExpansion(ScenarioDefinition definition, string id)
        {
            return new ScenarioDefinitionIndex(definition).HasExpansion(id);
        }

        public static bool HasObject(ScenarioDefinition definition, string id)
        {
            return new ScenarioDefinitionIndex(definition).HasObject(id);
        }

        public static bool HasFutureSurvivor(ScenarioDefinition definition, string id)
        {
            return new ScenarioDefinitionIndex(definition).HasFutureSurvivor(id);
        }
    }
}
