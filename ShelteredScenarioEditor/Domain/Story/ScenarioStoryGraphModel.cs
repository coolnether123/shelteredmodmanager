using ShelteredScenarioEditor.Application.Commands;

namespace ShelteredScenarioEditor.Domain.Story
{
    internal enum ScenarioStoryGraphNodeKind
    {
        Stage = 0,
        Terminal = 1
    }

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

    internal sealed class ScenarioStoryGraphNode
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public ScenarioStoryGraphNodeKind Kind { get; set; }
        public ScenarioStoryGraphNodeStatus Status { get; set; }
        public int StageIndex { get; set; }
        public int LineCount { get; set; }
        public int StepCount { get; set; }
        public int ProblemCount { get; set; }
        public string ProblemSummary { get; set; }
        public string Tooltip { get; set; }
        public string NavigationAutomationId { get; set; }
        public ScenarioAuthoringCommand NavigationCommand { get; set; }
        public int Column { get; set; }
        public int Row { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    internal sealed class ScenarioStoryGraphEdge
    {
        public string FromNodeId { get; set; }
        public string ToNodeId { get; set; }
        public string Label { get; set; }
        public ScenarioStoryGraphEdgeStatus Status { get; set; }
    }

    internal sealed class ScenarioStoryGraphModel
    {
        public ScenarioStoryGraphNode[] Nodes { get; set; }
        public ScenarioStoryGraphEdge[] Edges { get; set; }
        public bool Truncated { get; set; }
        public string Note { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int NodeCount { get { return Nodes != null ? Nodes.Length : 0; } }
        public int EdgeCount { get { return Edges != null ? Edges.Length : 0; } }
    }
}
