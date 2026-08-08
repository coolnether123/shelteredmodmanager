namespace ShelteredAPI.Scenarios.Public{
    public sealed class ScenarioRuntimeSnapshot
    {
        private readonly System.Collections.Generic.List<ScenarioRuntimeActionSnapshot> _actions =
            new System.Collections.Generic.List<ScenarioRuntimeActionSnapshot>();
        private readonly System.Collections.Generic.List<ScenarioRuntimeFlagSnapshot> _flags =
            new System.Collections.Generic.List<ScenarioRuntimeFlagSnapshot>();

        public string ScenarioId { get; internal set; }
        public string ScenarioVersion { get; internal set; }
        public string RuntimeBindingId { get; internal set; }
        public string Outcome { get; internal set; }
        public string OutcomeConditionId { get; internal set; }
        public int LastProcessedDay { get; internal set; }
        public int LastProcessedHour { get; internal set; }
        public int LastProcessedMinute { get; internal set; }
        public ScenarioRuntimeActionSnapshot[] Actions { get { return _actions.ToArray(); } }
        public ScenarioRuntimeFlagSnapshot[] Flags { get { return _flags.ToArray(); } }

        internal void AddAction(ScenarioRuntimeActionSnapshot action) { _actions.Add(action); }
        internal void AddFlag(ScenarioRuntimeFlagSnapshot flag) { _flags.Add(flag); }
    }

    public sealed class ScenarioRuntimeActionSnapshot
    {
        public string ActionKey { get; internal set; }
        public string ActionType { get; internal set; }
        public int Day { get; internal set; }
        public int Hour { get; internal set; }
        public int Minute { get; internal set; }
        public string Status { get; internal set; }
        public string Message { get; internal set; }
    }

    public sealed class ScenarioRuntimeFlagSnapshot
    {
        public string Id { get; internal set; }
        public string Value { get; internal set; }
    }

    public sealed class ScenarioRuntimeExecutionEntrySnapshot
    {
        public int Day { get; internal set; }
        public int Hour { get; internal set; }
        public int Minute { get; internal set; }
        public string ElementId { get; internal set; }
        public string DisplayName { get; internal set; }
        public string Kind { get; internal set; }
        public string Outcome { get; internal set; }
        public string ConditionSummary { get; internal set; }
        public string Detail { get; internal set; }
        public string PlainLanguage { get; internal set; }
    }

    /// <summary>Value-only map loot entry returned by the canonical runtime planner.</summary>
    public sealed class ScenarioMapLootEntrySnapshot
    {
        public string ItemId { get; internal set; }
        public int Quantity { get; internal set; }
        public bool Hidden { get; internal set; }
        public string HiddenUnlockItemId { get; internal set; }
    }
}
