using System.Collections.Generic;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Scheduling;

namespace ShelteredAPI.Scenarios.Domain.Journal
{
    public enum ScenarioJournalEntryMode
    {
        Once = 0,
        Repeat = 1
    }

    public enum ScenarioJournalVanillaCategory
    {
        Death = 0,
        Exploration = 1,
        Combat = 2,
        ExplorersNotReturning = 3,
        Visitor = 4
    }

    public sealed class JournalDefinition
    {
        public JournalDefinition()
        {
            Entries = new List<JournalEntryDefinition>();
            VanillaPolicy = new JournalVanillaPolicyDefinition();
        }

        public List<JournalEntryDefinition> Entries { get; private set; }
        public JournalVanillaPolicyDefinition VanillaPolicy { get; set; }
    }

    public sealed class JournalEntryDefinition
    {
        public JournalEntryDefinition()
        {
            Mode = ScenarioJournalEntryMode.Once;
            DueTime = new ScenarioScheduleTime();
            Conditions = new List<ScenarioConditionRef>();
        }

        public string Id { get; set; }
        public string Text { get; set; }
        public ScenarioActorRef Writer { get; set; }
        public ScenarioScheduleTime DueTime { get; set; }
        public string TriggerId { get; set; }
        public string GateId { get; set; }
        public ScenarioJournalEntryMode Mode { get; set; }
        public int CooldownMinutes { get; set; }
        public List<ScenarioConditionRef> Conditions { get; private set; }
    }

    public sealed class JournalVanillaPolicyDefinition
    {
        public JournalVanillaPolicyDefinition()
        {
            SuppressedCategories = new List<ScenarioJournalVanillaCategory>();
        }

        public bool SuppressFirstEntry { get; set; }
        public List<ScenarioJournalVanillaCategory> SuppressedCategories { get; private set; }
    }
}
