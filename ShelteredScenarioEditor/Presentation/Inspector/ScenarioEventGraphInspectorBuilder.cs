using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
namespace ShelteredScenarioEditor.Presentation.Inspector{
    internal static class ScenarioEventGraphInspectorBuilder
    {
        public static List<ScenarioAuthoringInspectorItem> BuildItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringGraphNodeViewModel> nodes = new List<ScenarioAuthoringGraphNodeViewModel>();
            List<ScenarioAuthoringGraphEdgeViewModel> edges = new List<ScenarioAuthoringGraphEdgeViewModel>();
            ScenarioDefinitionReferenceIndex index = ShelteredScenarioAuthoring.IndexDefinition(definition);

            AddTriggerNodes(definition, nodes);
            AddGateNodes(definition, nodes, edges);
            AddScheduledActionNodes(definition, index, nodes, edges);
            AddQuestNodes(definition, index, nodes, edges);
            AddWinLossNodes(definition, nodes);

            return BuildInspectorItems(nodes, edges);
        }

        private static void AddTriggerNodes(ScenarioDefinition definition, List<ScenarioAuthoringGraphNodeViewModel> nodes)
        {
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                string id = "trigger:" + ScenarioInspectorItemFactory.Safe(trigger != null ? trigger.Id : null);
                nodes.Add(GraphNode(id, ScenarioInspectorItemFactory.Safe(trigger != null ? trigger.Id : null), "Trigger", trigger != null ? trigger.Type : "Unknown", "OK", null));
            }
        }

        private static void AddGateNodes(
            ScenarioDefinition definition,
            List<ScenarioAuthoringGraphNodeViewModel> nodes,
            List<ScenarioAuthoringGraphEdgeViewModel> edges)
        {
            for (int i = 0; definition != null && definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                string id = "gate:" + ScenarioInspectorItemFactory.Safe(gate != null ? gate.Id : null);
                string label = !string.IsNullOrEmpty(gate != null ? gate.DisplayName : null)
                    ? gate.DisplayName
                    : ScenarioInspectorItemFactory.Safe(gate != null ? gate.Id : null);
                string detail = CountConditions(gate != null ? gate.Conditions : null).ToString(CultureInfo.InvariantCulture) + " condition(s)";
                nodes.Add(GraphNode(id, label, "Gate", detail, "OK", null));
                AppendConditionEdges(edges, id, gate != null ? gate.Conditions : null);
            }
        }

        private static void AddScheduledActionNodes(
            ScenarioDefinition definition,
            ScenarioDefinitionReferenceIndex index,
            List<ScenarioAuthoringGraphNodeViewModel> nodes,
            List<ScenarioAuthoringGraphEdgeViewModel> edges)
        {
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                string actionId = !string.IsNullOrEmpty(action != null ? action.Id : null)
                    ? action.Id
                    : "scheduled_" + i.ToString(CultureInfo.InvariantCulture);
                string nodeId = "scheduled:" + actionId;
                bool hasBrokenGate = action != null && !string.IsNullOrEmpty(action.GateId) && !index.HasGate(action.GateId);
                string status = hasBrokenGate ? "Broken gate" : "OK";
                nodes.Add(GraphNode(
                    nodeId,
                    actionId,
                    "Scheduled Action",
                    (action != null ? action.ActionType : "Unknown") + " / " + ScenarioScheduleFormatter.Format(action != null ? action.DueTime : null),
                    status,
                    ScenarioInspectorItemFactory.Action(TimelineNavigationCommand.OpenEntry(actionId), "Focus", "Focus this scheduled action on the timeline.", true, status != "OK", "EV")));

                if (action != null && !string.IsNullOrEmpty(action.GateId))
                    edges.Add(GraphEdge("gate:" + action.GateId, nodeId, "allows", index.HasGate(action.GateId) ? "OK" : "Broken"));

                AddScheduledConditionNodes(action, nodeId, nodes, edges);
                AddScheduledEffectNodes(index, action, actionId, nodeId, nodes, edges);
            }
        }

        private static void AddScheduledConditionNodes(
            ScenarioScheduledActionDefinition action,
            string nodeId,
            List<ScenarioAuthoringGraphNodeViewModel> nodes,
            List<ScenarioAuthoringGraphEdgeViewModel> edges)
        {
            for (int c = 0; action != null && action.ConditionRefs != null && c < action.ConditionRefs.Count; c++)
            {
                ScenarioConditionRef condition = action.ConditionRefs[c];
                string conditionId = "condition:" + ScenarioInspectorItemFactory.Safe(condition != null ? condition.Id : null);
                nodes.Add(GraphNode(conditionId, ScenarioInspectorItemFactory.Safe(condition != null ? condition.Id : null), "Condition", condition != null ? condition.Kind.ToString() : "Unknown", "Inline", null));
                edges.Add(GraphEdge(conditionId, nodeId, "required", "OK"));
            }
        }

        private static void AddScheduledEffectNodes(
            ScenarioDefinitionReferenceIndex index,
            ScenarioScheduledActionDefinition action,
            string actionId,
            string nodeId,
            List<ScenarioAuthoringGraphNodeViewModel> nodes,
            List<ScenarioAuthoringGraphEdgeViewModel> edges)
        {
            for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
            {
                ScenarioEffectDefinition effect = action.Effects[e];
                bool broken = IsEffectBroken(index, effect);
                string effectId = "effect:" + actionId + ":" + e.ToString(CultureInfo.InvariantCulture);
                nodes.Add(GraphNode(effectId, effect != null ? effect.Kind.ToString() : "Effect", "Effect", ScenarioAuthoringPresentationBuilder.FormatEffectTarget(effect), broken ? "Broken reference" : "OK", null));
                edges.Add(GraphEdge(nodeId, effectId, "fires", broken ? "Broken" : "OK"));
            }
        }

        private static void AddQuestNodes(
            ScenarioDefinition definition,
            ScenarioDefinitionReferenceIndex index,
            List<ScenarioAuthoringGraphNodeViewModel> nodes,
            List<ScenarioAuthoringGraphEdgeViewModel> edges)
        {
            for (int i = 0; definition != null && definition.Quests != null && definition.Quests.Quests != null && i < definition.Quests.Quests.Count; i++)
            {
                QuestDefinition quest = definition.Quests.Quests[i];
                string questId = !string.IsNullOrEmpty(quest != null ? quest.Id : null)
                    ? quest.Id
                    : "quest_" + i.ToString(CultureInfo.InvariantCulture);
                string nodeId = "quest:" + questId;
                bool hasBrokenTrigger = quest != null && !string.IsNullOrEmpty(quest.StartTriggerId) && !index.HasTrigger(quest.StartTriggerId);
                string status = hasBrokenTrigger ? "Broken trigger" : "OK";
                string label = !string.IsNullOrEmpty(quest != null ? quest.Title : null) ? quest.Title : questId;
                nodes.Add(GraphNode(nodeId, label, "Quest", ScenarioScheduleFormatter.Format(quest != null ? quest.ScheduledStart : null), status, null));
                if (quest != null && !string.IsNullOrEmpty(quest.StartTriggerId))
                    edges.Add(GraphEdge("trigger:" + quest.StartTriggerId, nodeId, "starts", index.HasTrigger(quest.StartTriggerId) ? "OK" : "Broken"));
            }
        }

        private static void AddWinLossNodes(ScenarioDefinition definition, List<ScenarioAuthoringGraphNodeViewModel> nodes)
        {
            for (int i = 0; definition != null && definition.WinLossConditions != null && definition.WinLossConditions.WinConditions != null && i < definition.WinLossConditions.WinConditions.Count; i++)
            {
                ScenarioConditionRef condition = definition.WinLossConditions.WinConditions[i];
                nodes.Add(GraphNode("win:" + ScenarioInspectorItemFactory.Safe(condition != null ? condition.Id : null), ScenarioInspectorItemFactory.Safe(condition != null ? condition.Id : null), "Win Condition", condition != null ? condition.Kind.ToString() : "Unknown", "OK", null));
            }

            for (int i = 0; definition != null && definition.WinLossConditions != null && definition.WinLossConditions.LossConditions != null && i < definition.WinLossConditions.LossConditions.Count; i++)
            {
                ScenarioConditionRef condition = definition.WinLossConditions.LossConditions[i];
                nodes.Add(GraphNode("loss:" + ScenarioInspectorItemFactory.Safe(condition != null ? condition.Id : null), ScenarioInspectorItemFactory.Safe(condition != null ? condition.Id : null), "Loss Condition", condition != null ? condition.Kind.ToString() : "Unknown", "OK", null));
            }
        }

        private static List<ScenarioAuthoringInspectorItem> BuildInspectorItems(
            List<ScenarioAuthoringGraphNodeViewModel> nodes,
            List<ScenarioAuthoringGraphEdgeViewModel> edges)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Nodes", nodes.Count.ToString(CultureInfo.InvariantCulture)));
            items.Add(ScenarioInspectorItemFactory.Property("Edges", edges.Count.ToString(CultureInfo.InvariantCulture)));
            AppendNodeItems(items, nodes);
            AppendEdgeItems(items, edges);

            if (nodes.Count == 0 && edges.Count == 0)
                items.Add(ScenarioInspectorItemFactory.Text("No trigger, gate, scheduled action, quest, or win/loss graph data is authored yet."));

            return items;
        }

        private static void AppendNodeItems(List<ScenarioAuthoringInspectorItem> items, List<ScenarioAuthoringGraphNodeViewModel> nodes)
        {
            for (int i = 0; i < nodes.Count && i < 16; i++)
            {
                ScenarioAuthoringGraphNodeViewModel node = nodes[i];
                if (node == null)
                    continue;

                if (node.PrimaryAction != null)
                    items.Add(ScenarioInspectorItemFactory.ActionItem(node.PrimaryAction));

                items.Add(ScenarioInspectorItemFactory.Property(node.Kind + ": " + node.Label, node.Detail + " / " + node.Status));
            }
        }

        private static void AppendEdgeItems(List<ScenarioAuthoringInspectorItem> items, List<ScenarioAuthoringGraphEdgeViewModel> edges)
        {
            for (int i = 0; i < edges.Count && i < 16; i++)
            {
                ScenarioAuthoringGraphEdgeViewModel edge = edges[i];
                if (edge != null)
                    items.Add(ScenarioInspectorItemFactory.Property("Edge " + edge.Label, edge.FromNodeId + " -> " + edge.ToNodeId + " / " + edge.Status));
            }
        }

        private static ScenarioAuthoringGraphNodeViewModel GraphNode(string id, string label, string kind, string detail, string status, ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringGraphNodeViewModel
            {
                Id = id,
                Label = label,
                Kind = kind,
                Detail = detail,
                Status = status,
                PrimaryAction = action
            };
        }

        private static ScenarioAuthoringGraphEdgeViewModel GraphEdge(string from, string to, string label, string status)
        {
            return new ScenarioAuthoringGraphEdgeViewModel
            {
                FromNodeId = from,
                ToNodeId = to,
                Label = label,
                Status = status
            };
        }

        private static void AppendConditionEdges(List<ScenarioAuthoringGraphEdgeViewModel> edges, string gateNodeId, ScenarioConditionGroup group)
        {
            if (edges == null || group == null)
                return;

            for (int i = 0; group.Conditions != null && i < group.Conditions.Count; i++)
            {
                ScenarioConditionRef condition = group.Conditions[i];
                if (condition != null && !string.IsNullOrEmpty(condition.Id))
                    edges.Add(GraphEdge("condition:" + condition.Id, gateNodeId, group.Mode.ToString(), "OK"));
            }

            for (int i = 0; group.Groups != null && i < group.Groups.Count; i++)
                AppendConditionEdges(edges, gateNodeId, group.Groups[i]);
        }

        private static int CountConditions(ScenarioConditionGroup group)
        {
            if (group == null)
                return 0;

            int count = group.Conditions != null ? group.Conditions.Count : 0;
            for (int i = 0; group.Groups != null && i < group.Groups.Count; i++)
                count += CountConditions(group.Groups[i]);
            return count;
        }

        private static bool IsEffectBroken(ScenarioDefinitionReferenceIndex index, ScenarioEffectDefinition effect)
        {
            if (effect == null)
                return true;

            if (!string.IsNullOrEmpty(effect.QuestId) && !index.HasQuest(effect.QuestId))
                return true;

            return false;
        }
    }
}
