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
    /// Layout is a deterministic layered pass (BFS depth = column, siblings stacked) so the
    /// same scenario always produces the same node positions.
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
        public const float ColumnGap = 72f;
        public const float RowGap = 26f;
        public const float Margin = 20f;

        private const float CellWidth = StageCardWidth + ColumnGap;
        private const float CellHeight = StageCardHeight + RowGap;

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
            List<int>[] children = new List<int>[includedStages];
            for (int i = 0; i < includedStages; i++)
                children[i] = new List<int>();
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
                    if (target != source && !children[source].Contains(target))
                        children[source].Add(target);
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
            List<ScenarioStoryGraphNode> terminals = new List<ScenarioStoryGraphNode>();
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
                    terminals.Add(leaf);
                    nodes.Add(leaf);
                    edges.Add(Edge(stageNodes[i].Id, leaf.Id, outcomeLabel, ScenarioStoryGraphEdgeStatus.Ok));
                }
            }

            // --- Deterministic layered layout. ---
            AssignColumns(stageNodes, children);
            LayoutColumns(nodes, stageNodes, terminals, missingTargets, model);

            model.Nodes = nodes.ToArray();
            model.Edges = edges.ToArray();
            return model;
        }

        // === Layout ======================================================================

        // BFS depth = column. Deterministic: children are processed in ascending stage-index
        // order, unreachable subgraphs are laid out to the right of the reachable ones.
        private static void AssignColumns(ScenarioStoryGraphNode[] stageNodes, List<int>[] children)
        {
            int count = stageNodes.Length;
            int[] depth = new int[count];
            for (int i = 0; i < count; i++)
                depth[i] = -1;

            Queue<int> queue = new Queue<int>();
            if (count > 0)
            {
                depth[0] = 0;
                queue.Enqueue(0);
            }
            BfsAssign(queue, depth, children);

            // Lay any unreachable stages out in index order, seeded past the reachable columns.
            int maxDepth = MaxAssigned(depth);
            for (int i = 0; i < count; i++)
            {
                if (depth[i] != -1)
                    continue;
                int seed = maxDepth + 2;
                depth[i] = seed;
                queue.Enqueue(i);
                BfsAssign(queue, depth, children);
                maxDepth = MaxAssigned(depth);
            }

            for (int i = 0; i < count; i++)
                stageNodes[i].Column = depth[i] < 0 ? 0 : depth[i];
        }

        private static void BfsAssign(Queue<int> queue, int[] depth, List<int>[] children)
        {
            while (queue.Count > 0)
            {
                int s = queue.Dequeue();
                List<int> kids = children[s];
                kids.Sort();
                for (int k = 0; k < kids.Count; k++)
                {
                    int t = kids[k];
                    if (depth[t] == -1)
                    {
                        depth[t] = depth[s] + 1;
                        queue.Enqueue(t);
                    }
                }
            }
        }

        private static int MaxAssigned(int[] depth)
        {
            int max = 0;
            for (int i = 0; i < depth.Length; i++)
                if (depth[i] > max)
                    max = depth[i];
            return max;
        }

        private static void LayoutColumns(
            List<ScenarioStoryGraphNode> nodes,
            ScenarioStoryGraphNode[] stageNodes,
            List<ScenarioStoryGraphNode> terminals,
            Dictionary<string, ScenarioStoryGraphNode> missingTargets,
            ScenarioStoryGraphModel model)
        {
            // Terminal/missing leaves live one column to the right of their owning stage.
            for (int i = 0; i < terminals.Count; i++)
            {
                ScenarioStoryGraphNode leaf = terminals[i];
                int owner = OutcomeOwnerIndex(leaf.Id);
                leaf.Column = (owner >= 0 && owner < stageNodes.Length ? stageNodes[owner].Column : 0) + 1;
            }
            foreach (KeyValuePair<string, ScenarioStoryGraphNode> pair in missingTargets)
            {
                int owner = MinSourceColumnFor(stageNodes, pair.Value, model);
                pair.Value.Column = owner + 1;
            }

            // Row = order within a column: stage nodes by index, then terminal leaves.
            int maxColumn = 0;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Column > maxColumn)
                    maxColumn = nodes[i].Column;

            int maxRow = 0;
            for (int c = 0; c <= maxColumn; c++)
            {
                int row = 0;
                for (int i = 0; i < nodes.Count; i++)
                {
                    ScenarioStoryGraphNode node = nodes[i];
                    if (node.Column != c || node.Kind != ScenarioStoryGraphNodeKind.Stage)
                        continue;
                    PlaceNode(node, c, row++);
                }
                for (int i = 0; i < nodes.Count; i++)
                {
                    ScenarioStoryGraphNode node = nodes[i];
                    if (node.Column != c || node.Kind != ScenarioStoryGraphNodeKind.Terminal)
                        continue;
                    PlaceNode(node, c, row++);
                }
                if (row > maxRow)
                    maxRow = row;
            }

            model.Columns = maxColumn + 1;
            model.Rows = maxRow;
            model.Width = (Margin * 2f) + ((maxColumn + 1) * StageCardWidth) + (maxColumn * ColumnGap);
            model.Height = (Margin * 2f) + (maxRow * StageCardHeight) + (Math.Max(0, maxRow - 1) * RowGap);
        }

        private static void PlaceNode(ScenarioStoryGraphNode node, int column, int row)
        {
            node.Column = column;
            node.Row = row;
            node.X = Margin + (column * CellWidth);
            node.Y = Margin + (row * CellHeight);
            if (node.Kind == ScenarioStoryGraphNodeKind.Terminal)
            {
                node.Width = TerminalCardWidth;
                node.Height = TerminalCardHeight;
                // Vertically centre the smaller terminal card inside its row slot.
                node.Y += (StageCardHeight - TerminalCardHeight) * 0.5f;
            }
            else
            {
                node.Width = StageCardWidth;
                node.Height = StageCardHeight;
            }
        }

        private static int MinSourceColumnFor(ScenarioStoryGraphNode[] stageNodes, ScenarioStoryGraphNode missing, ScenarioStoryGraphModel model)
        {
            // Missing-target leaves sit past the earliest stage that references them; the
            // simple, deterministic choice is one column past the highest stage column.
            int max = 0;
            for (int i = 0; i < stageNodes.Length; i++)
                if (stageNodes[i].Column > max)
                    max = stageNodes[i].Column;
            return max;
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
