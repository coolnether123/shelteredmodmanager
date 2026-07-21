using System;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Story;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    /// <summary>Executable contract for the storyboard row and owner-aligned outcome layout.</summary>
    internal static class ScenarioStoryGraphUxVerification
    {
        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            definition.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition
            {
                Id = "stage_1",
                UnansweredNextStage = "stage_2"
            });
            ScenarioFlowStageDefinition recruiting = new ScenarioFlowStageDefinition { Id = "stage_2" };
            ScenarioIntercomStageDefinition recruitStep = new ScenarioIntercomStageDefinition();
            recruitStep.CharacterIdsToRecruit.Add("visitor");
            recruiting.IntercomStages.Add(recruitStep);
            definition.ScenarioFlow.Stages.Add(recruiting);
            definition.ScenarioFlow.Stages.Add(new ScenarioFlowStageDefinition { Id = "stage_3" });

            ScenarioStoryGraphModel graph = ScenarioStoryGraphBuilder.Build(definition);
            ScenarioStoryGraphNode[] stages = FindNodes(graph, ScenarioStoryGraphNodeKind.Stage);
            Assert(stages.Length == 3, "Story graph fixture did not retain all three stages.", result);
            Assert(stages.Length == 3
                    && NearlyEqual(stages[0].Y, stages[1].Y)
                    && NearlyEqual(stages[1].Y, stages[2].Y)
                    && stages[0].X < stages[1].X
                    && stages[1].X < stages[2].X,
                "Story graph stages were not arranged left-to-right on one grid row.", result);

            ScenarioStoryGraphNode recruit = FindNode(graph, "outcome:1");
            ScenarioStoryGraphNode ending = FindNode(graph, "outcome:2");
            Assert(IsAlignedBelow(stages, 1, recruit),
                "Recruit outcome was not aligned below its source stage.", result);
            Assert(IsAlignedBelow(stages, 2, ending),
                "Conversation outcome was not aligned below its source stage.", result);
        }

        private static ScenarioStoryGraphNode[] FindNodes(ScenarioStoryGraphModel graph, ScenarioStoryGraphNodeKind kind)
        {
            System.Collections.Generic.List<ScenarioStoryGraphNode> nodes = new System.Collections.Generic.List<ScenarioStoryGraphNode>();
            for (int i = 0; graph != null && graph.Nodes != null && i < graph.Nodes.Length; i++)
                if (graph.Nodes[i] != null && graph.Nodes[i].Kind == kind)
                    nodes.Add(graph.Nodes[i]);
            return nodes.ToArray();
        }

        private static ScenarioStoryGraphNode FindNode(ScenarioStoryGraphModel graph, string id)
        {
            for (int i = 0; graph != null && graph.Nodes != null && i < graph.Nodes.Length; i++)
                if (graph.Nodes[i] != null && string.Equals(graph.Nodes[i].Id, id, StringComparison.Ordinal))
                    return graph.Nodes[i];
            return null;
        }

        private static bool IsAlignedBelow(ScenarioStoryGraphNode[] stages, int owner, ScenarioStoryGraphNode outcome)
        {
            if (stages == null || owner < 0 || owner >= stages.Length || outcome == null)
                return false;
            ScenarioStoryGraphNode stage = stages[owner];
            float stageCenter = stage.X + (stage.Width * 0.5f);
            float outcomeCenter = outcome.X + (outcome.Width * 0.5f);
            return outcome.Y > stage.Y + stage.Height && NearlyEqual(stageCenter, outcomeCenter);
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) < 0.01f;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null)
                result.AddError(message);
        }
    }
}
