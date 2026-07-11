using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Story;
using ShelteredAPI.Scenarios.Domain.Validation;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    /// <summary>
    /// Builds the primary authoring Story Map: a visual overview of a scenario's story
    /// stages and how they connect. It is deliberately NOT a third flow walker. Nodes and
    /// their problems come from the existing shared logic:
    ///   * <see cref="ScenarioStoryFlowValidationAnalyzer"/> supplies problems, including
    ///     which stages are unreachable and which routes are broken;
    ///   * <see cref="ScenarioReferenceIndex"/> (Find Usages) supplies every stage-to-stage
    ///     route (unanswered-call routes and delayed stage changes) as the graph edges;
    ///   * <see cref="ScenarioStoryScriptViewBuilder"/> supplies the plain-language route
    ///     phrasing reused for hover tooltips.
    /// Layout is a deterministic storyboard grid: stages read left-to-right on one row and
    /// outcome/missing leaves sit directly below the stage that owns their route.
    /// </summary>
    internal static class ScenarioStoryGraphBuilder
    {
        // Readable node cap. Beyond this the flow degrades to the first stages plus a note.
        public const int MaxStageNodes = 50;

        // Layout metrics (deterministic; renderer draws at these sizes).
        public const float StageCardWidth = 184f;
        public const float StageCardHeight = 78f;
        public const float TerminalCardWidth = 132f;
        public const float TerminalCardHeight = 46f;
        public const float ColumnGap = 58f;
        public const float RowGap = 42f;
        public const float Margin = 20f;

        private const float CellWidth = StageCardWidth + ColumnGap;

        public static ScenarioStoryGraphModel Build(ScenarioDefinition definition)
        {
            return Build(definition, null);
        }

        /// <summary>
        /// Build the model. <paramref name="issues"/> may be supplied by the caller (the
        /// story page already computes them); when null the shared analyzer is run here so
        /// the builder is self-contained for fixtures and tests.
        /// </summary>
        public static ScenarioStoryGraphModel Build(ScenarioDefinition definition, ScenarioStoryFlowIssue[] issues)
        {
            ScenarioStoryGraphModel model = new ScenarioStoryGraphModel();
            ScenarioFlowDefinition flow = definition != null ? definition.ScenarioFlow : null;
            if (flow == null || flow.Stages == null || flow.Stages.Count == 0)
            {
                model.Nodes = new ScenarioStoryGraphNode[0];
                model.Edges = new ScenarioStoryGraphEdge[0];
                model.Note = "No story stages yet. Add a stage to start the map.";
                return model;
            }

            if (issues == null)
                issues = new ScenarioStoryFlowValidationAnalyzer().Analyze(definition);

            int stageCount = flow.Stages.Count;
            int includedStages = stageCount;
            if (includedStages > MaxStageNodes)
            {
                includedStages = MaxStageNodes;
                model.Truncated = true;
                model.Note = "Showing the first " + MaxStageNodes.ToString(CultureInfo.InvariantCulture)
                    + " of " + stageCount.ToString(CultureInfo.InvariantCulture)
                    + " stages. Trim the flow or use the stage list below for the rest.";
            }

            // --- Stage nodes (enumeration of the flow, not a walk). ---
            List<ScenarioStoryGraphNode> nodes = new List<ScenarioStoryGraphNode>();
            ScenarioStoryGraphNode[] stageNodes = new ScenarioStoryGraphNode[includedStages];
            for (int i = 0; i < includedStages; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                ScenarioStoryGraphNode node = new ScenarioStoryGraphNode
                {
                    Id = StageNodeId(i),
                    Label = DisplayStageTitle(stage, i),
                    Kind = ScenarioStoryGraphNodeKind.Stage,
                    StageIndex = i,
                    StepCount = CountSteps(stage),
                    LineCount = CountLines(stage),
                    NavActionId = ScenarioStoryFocusedEditorActions.StageOpen(i),
                    Tooltip = BuildStageTooltip(definition, stage)
                };
                ApplyStageProblems(node, issues, i);
                stageNodes[i] = node;
                nodes.Add(node);
            }

            // --- Edges = stage-to-stage routes via the shared reference index. ---
            List<ScenarioStoryGraphEdge> edges = new List<ScenarioStoryGraphEdge>();
            Dictionary<string, ScenarioStoryGraphNode> missingTargets = new Dictionary<string, ScenarioStoryGraphNode>(StringComparer.OrdinalIgnoreCase);

            List<ScenarioReferenceUsage> usages = ScenarioReferenceIndex.Collect(definition);
            for (int u = 0; u < usages.Count; u++)
            {
                ScenarioReferenceUsage usage = usages[u];
                if (usage == null || usage.TargetKind != ScenarioReferenceTargetKind.Stage)
                    continue;
                string targetId = TrimToNull(usage.ReferencedId);
                if (targetId == null)
                    continue;
                int source = usage.OwnerStageIndex;
                if (source < 0 || source >= includedStages)
                    continue;

                int target = FindStageIndex(flow, targetId, includedStages);
                if (target >= 0)
                {
                    edges.Add(Edge(stageNodes[source].Id, StageNodeId(target), usage.DisplayLabel, ScenarioStoryGraphEdgeStatus.Ok));
                }
                else
                {
                    ScenarioStoryGraphNode missing;
                    string key = targetId.ToLowerInvariant();
                    if (!missingTargets.TryGetValue(key, out missing))
                    {
                        missing = new ScenarioStoryGraphNode
                        {
                            Id = "missing:" + key,
                            Label = "missing '" + targetId + "'",
                            Kind = ScenarioStoryGraphNodeKind.Terminal,
                            Status = ScenarioStoryGraphNodeStatus.Broken,
                            StageIndex = -1,
                            Tooltip = "This route points at a stage id that does not exist."
                        };
                        missingTargets[key] = missing;
                        nodes.Add(missing);
                    }
                    edges.Add(Edge(stageNodes[source].Id, missing.Id, usage.DisplayLabel, ScenarioStoryGraphEdgeStatus.Broken));
                }
            }

            // --- Terminal outcome leaves (small nodes hung off their owning stage). ---
            for (int i = 0; i < includedStages; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                string outcomeLabel;
                if (TryDescribeTerminalOutcome(stage, out outcomeLabel))
                {
                    ScenarioStoryGraphNode leaf = new ScenarioStoryGraphNode
                    {
                        Id = "outcome:" + i.ToString(CultureInfo.InvariantCulture),
                        Label = outcomeLabel,
                        Kind = ScenarioStoryGraphNodeKind.Terminal,
                        Status = ScenarioStoryGraphNodeStatus.Ok,
                        StageIndex = -1,
                        NavActionId = ScenarioStoryFocusedEditorActions.StageOpen(i),
                        Tooltip = "Outcome of " + stageNodes[i].Label + "."
                    };
                    nodes.Add(leaf);
                    edges.Add(Edge(stageNodes[i].Id, leaf.Id, outcomeLabel, ScenarioStoryGraphEdgeStatus.Ok));
                }
            }

            // --- Deterministic storyboard layout. ---
            LayoutStoryboard(nodes, stageNodes, edges, model);

            model.Nodes = nodes.ToArray();
            model.Edges = edges.ToArray();
            return model;
        }

        // === Layout ======================================================================

        private static void LayoutStoryboard(
            List<ScenarioStoryGraphNode> nodes,
            ScenarioStoryGraphNode[] stageNodes,
            List<ScenarioStoryGraphEdge> edges,
            ScenarioStoryGraphModel model)
        {
            Dictionary<string, int> stageIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
            int[] nextLeafRow = new int[stageNodes.Length];
            for (int i = 0; i < stageNodes.Length; i++)
            {
                ScenarioStoryGraphNode stage = stageNodes[i];
                stageIndexById[stage.Id] = i;
                nextLeafRow[i] = 1;
                PlaceStageNode(stage, i);
            }

            int maxLeafRow = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                ScenarioStoryGraphNode node = nodes[i];
                if (node == null || node.Kind != ScenarioStoryGraphNodeKind.Terminal)
                    continue;
                int owner = FindOwnerStageIndex(node.Id, edges, stageIndexById);
                if (owner < 0 || owner >= stageNodes.Length)
                    owner = 0;
                int row = nextLeafRow[owner]++;
                node.Column = owner;
                node.Row = row;
                node.Width = TerminalCardWidth;
                node.Height = TerminalCardHeight;
                node.X = Margin + (owner * CellWidth) + ((StageCardWidth - TerminalCardWidth) * 0.5f);
                node.Y = Margin + StageCardHeight + RowGap + ((row - 1) * (TerminalCardHeight + 18f));
                maxLeafRow = Math.Max(maxLeafRow, row);
            }

            model.Columns = stageNodes.Length;
            model.Rows = 1 + maxLeafRow;
            model.Width = (Margin * 2f) + (stageNodes.Length * StageCardWidth)
                + (Math.Max(0, stageNodes.Length - 1) * ColumnGap);
            model.Height = (Margin * 2f) + StageCardHeight;
            if (maxLeafRow > 0)
                model.Height += RowGap + (maxLeafRow * TerminalCardHeight) + ((maxLeafRow - 1) * 18f);
        }

        private static void PlaceStageNode(ScenarioStoryGraphNode node, int column)
        {
            node.Column = column;
            node.Row = 0;
            node.X = Margin + (column * CellWidth);
            node.Y = Margin;
            node.Width = StageCardWidth;
            node.Height = StageCardHeight;
        }

        private static int FindOwnerStageIndex(
            string nodeId,
            List<ScenarioStoryGraphEdge> edges,
            Dictionary<string, int> stageIndexById)
        {
            for (int i = 0; edges != null && i < edges.Count; i++)
            {
                ScenarioStoryGraphEdge edge = edges[i];
                int owner;
                if (edge != null
                    && string.Equals(edge.ToNodeId, nodeId, StringComparison.Ordinal)
                    && stageIndexById.TryGetValue(edge.FromNodeId ?? string.Empty, out owner))
                    return owner;
            }
            return OutcomeOwnerIndex(nodeId);
        }

        // === Problems ====================================================================

        private static void ApplyStageProblems(ScenarioStoryGraphNode node, ScenarioStoryFlowIssue[] issues, int stageIndex)
        {
            bool hasError = false;
            bool unreachable = false;
            int count = 0;
            string first = null;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                ScenarioStoryFlowIssue issue = issues[i];
                if (issue == null || issue.StageIndex != stageIndex)
                    continue;
                count++;
                if (first == null)
                    first = issue.Message;
                if (issue.Severity == ScenarioIssueSeverity.Error)
                    hasError = true;
                if (!string.IsNullOrEmpty(issue.Code) && issue.Code.IndexOf("unreachable", StringComparison.OrdinalIgnoreCase) >= 0)
                    unreachable = true;
            }

            node.ProblemCount = count;
            node.ProblemSummary = first;
            node.Status = hasError
                ? ScenarioStoryGraphNodeStatus.Broken
                : (unreachable ? ScenarioStoryGraphNodeStatus.Unreachable : ScenarioStoryGraphNodeStatus.Ok);
        }

        // === Terminal outcomes ===========================================================

        private static bool TryDescribeTerminalOutcome(ScenarioFlowStageDefinition stage, out string label)
        {
            label = null;
            if (stage == null)
                return false;

            if (CompletesScenario(stage))
            {
                label = "Ends scenario";
                return true;
            }
            if (RecruitsAnyone(stage))
            {
                label = "Recruits survivor";
                return true;
            }
            if (!HasOutgoingStageRoute(stage))
            {
                label = "Ends conversation";
                return true;
            }
            return false;
        }

        private static bool CompletesScenario(ScenarioFlowStageDefinition stage)
        {
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                ScenarioEncounterEndOptionsDefinition end = step != null ? step.EndOptions : null;
                if (end != null && (end.CompleteQuest || end.CompleteParentScenario
                    || string.Equals(end.Type, "CompleteQuest", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private static bool RecruitsAnyone(ScenarioFlowStageDefinition stage)
        {
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step != null && step.CharacterIdsToRecruit != null && step.CharacterIdsToRecruit.Count > 0)
                    return true;
            }
            return false;
        }

        private static bool HasOutgoingStageRoute(ScenarioFlowStageDefinition stage)
        {
            if (TrimToNull(stage.UnansweredNextStage) != null)
                return true;
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step != null && step.StageChange != null && TrimToNull(step.StageChange.Id) != null)
                    return true;
            }
            return false;
        }

        // === Tooltip (reuses STORYUX route phrasing) =====================================

        private static string BuildStageTooltip(ScenarioDefinition definition, ScenarioFlowStageDefinition stage)
        {
            if (stage == null)
                return null;

            List<string> parts = new List<string>();
            int steps = CountSteps(stage);
            parts.Add(steps == 1 ? "1 scene" : steps.ToString(CultureInfo.InvariantCulture) + " scenes");

            ScenarioIntercomStageDefinition first = FirstStep(stage);
            if (first != null)
                parts.Add("When it ends: " + ScenarioStoryScriptViewBuilder.DescribeStepEnding(definition, stage, first));

            if (TrimToNull(stage.UnansweredNextStage) != null)
                parts.Add("If ignored, continues to '" + stage.UnansweredNextStage + "'");

            return string.Join(". ", parts.ToArray()) + ".";
        }

        // === Small helpers ===============================================================

        private static ScenarioStoryGraphEdge Edge(string from, string to, string label, ScenarioStoryGraphEdgeStatus status)
        {
            return new ScenarioStoryGraphEdge { FromNodeId = from, ToNodeId = to, Label = label, Status = status };
        }

        private static string StageNodeId(int index)
        {
            return "stage:" + index.ToString(CultureInfo.InvariantCulture);
        }

        private static int OutcomeOwnerIndex(string id)
        {
            if (string.IsNullOrEmpty(id) || !id.StartsWith("outcome:", StringComparison.Ordinal))
                return -1;
            int value;
            return int.TryParse(id.Substring("outcome:".Length), out value) ? value : -1;
        }

        private static int FindStageIndex(ScenarioFlowDefinition flow, string id, int limit)
        {
            for (int i = 0; i < limit && i < flow.Stages.Count; i++)
            {
                ScenarioFlowStageDefinition stage = flow.Stages[i];
                if (stage != null && string.Equals(TrimToNull(stage.Id), id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static ScenarioIntercomStageDefinition FirstStep(ScenarioFlowStageDefinition stage)
        {
            for (int i = 0; stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
                if (stage.IntercomStages[i] != null)
                    return stage.IntercomStages[i];
            return null;
        }

        private static int CountSteps(ScenarioFlowStageDefinition stage)
        {
            return stage != null && stage.IntercomStages != null ? stage.IntercomStages.Count : 0;
        }

        private static int CountLines(ScenarioFlowStageDefinition stage)
        {
            int lines = 0;
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step != null && step.Dialogue != null)
                    lines += step.Dialogue.Count;
            }
            return lines;
        }

        private static string DisplayStageTitle(ScenarioFlowStageDefinition stage, int index)
        {
            for (int i = 0; stage != null && stage.IntercomStages != null && i < stage.IntercomStages.Count; i++)
            {
                ScenarioIntercomStageDefinition step = stage.IntercomStages[i];
                if (step != null && !string.IsNullOrEmpty(step.StageDescriptionKey))
                    return step.StageDescriptionKey;
            }
            if (stage != null && !string.IsNullOrEmpty(stage.Id))
                return stage.Id;
            return "Stage " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
