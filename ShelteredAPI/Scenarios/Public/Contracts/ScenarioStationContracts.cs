namespace ShelteredAPI.Scenarios.Public{
    /// <summary>Detached station level, upgrade-path, and safe-stat state.</summary>
    public sealed class ScenarioStationUpgradeSnapshot
    {
        private readonly System.Collections.Generic.List<ScenarioStationUpgradePathSnapshot> _paths =
            new System.Collections.Generic.List<ScenarioStationUpgradePathSnapshot>();
        private readonly System.Collections.Generic.List<ScenarioStationStatSnapshot> _stats =
            new System.Collections.Generic.List<ScenarioStationStatSnapshot>();

        public string ObjectType { get; internal set; }
        public int Level { get; internal set; }
        public int MinLevel { get; internal set; }
        public int MaxLevel { get; internal set; }
        public ScenarioStationUpgradePathSnapshot[] Paths { get { return _paths.ToArray(); } }
        public ScenarioStationStatSnapshot[] Stats { get { return _stats.ToArray(); } }

        internal void AddPath(ScenarioStationUpgradePathSnapshot path) { _paths.Add(path); }
        internal void AddStat(ScenarioStationStatSnapshot stat) { _stats.Add(stat); }
    }

    /// <summary>Detached state for one supported vanilla station upgrade path.</summary>
    public sealed class ScenarioStationUpgradePathSnapshot
    {
        public string Name { get; internal set; }
        public int Level { get; internal set; }
        public int CurrentLevel { get; internal set; }
        public int MaxLevel { get; internal set; }
    }

    /// <summary>Detached state and safe bounds for one supported station stat override.</summary>
    public sealed class ScenarioStationStatSnapshot
    {
        public string Name { get; internal set; }
        public string Label { get; internal set; }
        public float Value { get; internal set; }
        public float MinValue { get; internal set; }
        public float MaxValue { get; internal set; }
        public float Step { get; internal set; }
        public bool HasOverride { get; internal set; }
        public string Detail { get; internal set; }
    }
}
