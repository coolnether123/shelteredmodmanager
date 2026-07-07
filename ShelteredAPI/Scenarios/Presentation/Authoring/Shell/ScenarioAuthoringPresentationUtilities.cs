using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal static class ScenarioAuthoringPresentationUtilities
    {
        public static ScenarioAuthoringInspectorAction Action(
            string id,
            string label,
            string hint,
            bool enabled,
            bool emphasized,
            string iconText = null,
            string detail = null,
            string badge = null,
            Sprite previewSprite = null)
        {
            return ScenarioInspectorItemFactory.Action(id, label, hint, enabled, emphasized, iconText, detail, badge, previewSprite);
        }

        public static ScenarioAuthoringInspectorItem Text(string value)
        {
            return ScenarioInspectorItemFactory.Text(value);
        }

        public static ScenarioAuthoringInspectorItem Property(string label, string value)
        {
            return ScenarioInspectorItemFactory.Property(label, value);
        }

        public static ScenarioAuthoringInspectorItem Property(string label, string value, string detail)
        {
            return ScenarioInspectorItemFactory.Property(label, value, detail);
        }

        public static ScenarioAuthoringInspectorItem ActionItem(ScenarioAuthoringInspectorAction action)
        {
            return ScenarioInspectorItemFactory.ActionItem(action);
        }

        public static string Safe(string value)
        {
            return ScenarioInspectorItemFactory.Safe(value);
        }

        public static string FormatTarget(ScenarioAuthoringTarget target)
        {
            return target != null ? target.DisplayName + " (" + target.Kind + ")" : "No target selected";
        }

        public static int CountDirtyFlags(ScenarioEditorSession editorSession)
        {
            return editorSession != null && editorSession.DirtyFlags != null ? editorSession.DirtyFlags.Count : 0;
        }

        public static int CountSpriteSwaps(ScenarioDefinition definition)
        {
            return definition != null
                && definition.AssetReferences != null
                && definition.AssetReferences.SpriteSwaps != null
                    ? definition.AssetReferences.SpriteSwaps.Count
                    : 0;
        }

        public static int CountSceneSpritePlacements(ScenarioDefinition definition)
        {
            return definition != null
                && definition.AssetReferences != null
                && definition.AssetReferences.SceneSpritePlacements != null
                    ? definition.AssetReferences.SceneSpritePlacements.Count
                    : 0;
        }

        public static int CountFamilyMembers(ScenarioDefinition definition)
        {
            return definition != null
                && definition.FamilySetup != null
                && definition.FamilySetup.Members != null
                    ? definition.FamilySetup.Members.Count
                    : 0;
        }

        public static int CountInventoryStacks(ScenarioDefinition definition)
        {
            return definition != null
                && definition.StartingInventory != null
                && definition.StartingInventory.Items != null
                    ? definition.StartingInventory.Items.Count
                    : 0;
        }

        public static int CountInventoryTotal(ScenarioDefinition definition)
        {
            int total = 0;
            for (int i = 0; definition != null && definition.StartingInventory != null && definition.StartingInventory.Items != null && i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry entry = definition.StartingInventory.Items[i];
                if (entry != null)
                    total += entry.Quantity;
            }

            return total;
        }

        public static int CountObjectPlacements(ScenarioDefinition definition)
        {
            return definition != null
                && definition.BunkerEdits != null
                && definition.BunkerEdits.ObjectPlacements != null
                    ? definition.BunkerEdits.ObjectPlacements.Count
                    : 0;
        }

        public static string SummarizeFamily(ScenarioDefinition definition)
        {
            int members = CountFamilyMembers(definition);
            int future = definition != null
                && definition.FamilySetup != null
                && definition.FamilySetup.FutureSurvivors != null
                    ? definition.FamilySetup.FutureSurvivors.Count
                    : 0;
            return members == 0 && future == 0
                ? "No starting or future survivors have been authored yet."
                : members + " starting survivor(s), " + future + " future survivor(s).";
        }

        public static string SummarizeInventory(ScenarioDefinition definition)
        {
            int stacks = CountInventoryStacks(definition);
            int total = CountInventoryTotal(definition);
            return stacks == 0
                ? "Shelter storage has no authored starting items yet."
                : stacks + " item stack(s), " + total + " total item(s).";
        }

        public static string SummarizeObjectPlacements(ScenarioDefinition definition)
        {
            int count = CountObjectPlacements(definition);
            return count == 0
                ? "No shelter object placements have been captured yet."
                : count + " shelter object placement(s) captured.";
        }
    }
}
