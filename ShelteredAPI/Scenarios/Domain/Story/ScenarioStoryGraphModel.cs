namespace ShelteredAPI.Scenarios.Domain.Story{
    /// <summary>
    /// What a Story Map node represents: a primary story stage, or a small terminal
    /// outcome leaf (ends conversation, recruits a survivor, completes the scenario,
    /// or a broken route pointing at a missing stage).
    /// </summary>
    internal enum ScenarioStoryGraphNodeKind
    {
        Stage = 0,
        Terminal = 1
    }

    /// <summary>
    /// Problem state of a node, mirrored from the shared story-flow validation:
    /// Ok (readable), Unreachable (cannot be reached from the first stage), or
    /// Broken (an Error-severity problem such as a missing route or reference).
    /// </summary>
    internal enum ScenarioStoryGraphNodeStatus
    {
        Ok = 0,
        Unreachable = 1,
        Broken = 2
    }

    internal enum ScenarioStoryGraphEdgeStatus
    {
        Ok = 0,
        Broken = 1
    }

    /// <summary>
    /// One node on the Story Map. Positions are computed by the deterministic layered
    /// layout in <c>ScenarioStoryGraphBuilder</c>; the renderer only draws them.
    /// </summary>
    internal sealed class ScenarioStoryGraphNode
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public ScenarioStoryGraphNodeKind Kind { get; set; }
        public ScenarioStoryGraphNodeStatus Status { get; set; }

        /// <summary>Owning flow-stage index (-1 for a terminal leaf that is not a stage).</summary>
        public int StageIndex { get; set; }

        /// <summary>Spoken dialogue lines across the stage's encounter steps.</summary>
        public int LineCount { get; set; }

        /// <summary>Encounter steps in the stage.</summary>
        public int StepCount { get; set; }

        /// <summary>Number of validation problems attributed to this node.</summary>
        public int ProblemCount { get; set; }

        /// <summary>Plain-language first problem message, or null when clean.</summary>
        public string ProblemSummary { get; set; }

        /// <summary>Hover summary phrase describing where the stage routes.</summary>
        public string Tooltip { get; set; }

        /// <summary>Action id that opens this node's stage in the focused editor (null when none).</summary>
        public string NavActionId { get; set; }

        // Deterministic layered layout output.
        public int Column { get; set; }
        public int Row { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    /// <summary>A directed route between two nodes (a stage transition or an outcome).</summary>
    internal sealed class ScenarioStoryGraphEdge
    {
        public string FromNodeId { get; set; }
        public string ToNodeId { get; set; }
        public string Label { get; set; }
        public ScenarioStoryGraphEdgeStatus Status { get; set; }
    }

    /// <summary>
    /// The whole Story Map: nodes (stages plus terminal leaves), edges (routes), the
    /// canvas size the deterministic layout produced, and a degrade note when the flow
    /// exceeds the readable node cap.
    /// </summary>
    internal sealed class ScenarioStoryGraphModel
    {
        public ScenarioStoryGraphNode[] Nodes { get; set; }
        public ScenarioStoryGraphEdge[] Edges { get; set; }

        /// <summary>True when the flow exceeded the node cap and the map was trimmed.</summary>
        public bool Truncated { get; set; }

        /// <summary>Plain-language note shown when the map is empty or truncated.</summary>
        public string Note { get; set; }

        public int Columns { get; set; }
        public int Rows { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }

        public int NodeCount { get { return Nodes != null ? Nodes.Length : 0; } }
        public int EdgeCount { get { return Edges != null ? Edges.Length : 0; } }
    }
}
