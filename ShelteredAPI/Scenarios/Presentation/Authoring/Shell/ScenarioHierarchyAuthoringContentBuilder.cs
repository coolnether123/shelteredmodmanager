using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Shared;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioLayerAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Layers; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "layers",
                    Title = "Layers",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Item.Property("Shelter Tiles", "Visible / Locked"),
                        Item.Property("Shelter Objects", "Visible / Locked"),
                        Item.Property("Scene Art", "Visible"),
                        Item.Property("Triggers", "Visible"),
                        Item.Property("Pathing", "Visible"),
                        Item.Property("Regions", "Visible")
                    }
                }
            };
        }
    }

    internal sealed class ScenarioHierarchyAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Hierarchy; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioDefinition definition = context != null ? context.Definition : null;
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildSummarySection(state, definition));
            sections.Add(BuildBunkerSection(definition));
            sections.Add(BuildLiveObjectsSection());
            sections.Add(BuildCharactersSection(definition));
            sections.Add(BuildEventSection(definition));
            sections.Add(BuildAssetSection(definition));
            return sections.ToArray();
        }

        private static ScenarioAuthoringInspectorSection BuildSummarySection(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_summary",
                Title = "Scene Hierarchy",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    Item.Property("Stage", state != null ? state.ActiveStage.ToString() : "Unknown"),
                    Item.Property("Tool", state != null ? state.ActiveTool.ToString() : "Unknown"),
                    Item.Property("Scenario", Item.Safe(definition != null ? definition.DisplayName : null)),
                    Item.Property("Selection", Item.FormatTarget(state != null ? state.SelectedTarget : null))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildBunkerSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int roomChanges = definition != null && definition.BunkerEdits != null && definition.BunkerEdits.RoomChanges != null ? definition.BunkerEdits.RoomChanges.Count : 0;
            int placements = definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null ? definition.BunkerEdits.ObjectPlacements.Count : 0;
            items.Add(Item.Property("Authored Room Edits", roomChanges.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Authored Object Placements", placements.ToString(CultureInfo.InvariantCulture)));
            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count && i < 8; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;

                string label = !string.IsNullOrEmpty(placement.DefinitionReference) ? placement.DefinitionReference : (!string.IsNullOrEmpty(placement.PrefabReference) ? placement.PrefabReference : placement.ScenarioObjectId);
                items.Add(Item.Property(Item.Safe(label), FormatVector(placement.Position) + " / " + placement.StartState));
            }

            if (items.Count == 2)
                items.Add(Item.Text("No authored bunker placements are in this draft yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_bunker",
                Title = "Bunker / Rooms / Props",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildLiveObjectsSection()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            Obj_Base[] objects = UnityEngine.Object.FindObjectsOfType<Obj_Base>();
            int count = 0;
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                Obj_Base obj = objects[i];
                if (obj == null || obj.gameObject == null)
                    continue;

                count++;
                if (items.Count < 10)
                    items.Add(Item.ActionItem(BuildTargetAction(obj.gameObject, ScenarioAuthoringTargetKind.PlaceableObject, "OB")));
            }

            items.Insert(0, Item.Property("Live Shelter Objects", count.ToString(CultureInfo.InvariantCulture)));
            if (items.Count == 1)
                items.Add(Item.Text("No live shelter objects are currently discoverable."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_live_objects",
                Title = "Live Objects",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildCharactersSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int authored = definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null ? definition.FamilySetup.Members.Count : 0;
            int future = definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null ? definition.FamilySetup.FutureSurvivors.Count : 0;
            items.Add(Item.Property("Authored Starting Survivors", authored.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Future Survivors", future.ToString(CultureInfo.InvariantCulture)));

            FamilyManager manager = FamilyManager.Instance;
            List<FamilyMember> members = manager != null ? manager.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count && i < 8; i++)
            {
                FamilyMember member = members[i];
                if (member != null && member.gameObject != null)
                    items.Add(Item.ActionItem(BuildTargetAction(member.gameObject, ScenarioAuthoringTargetKind.Character, "PP")));
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_characters",
                Title = "Characters",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildEventSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Triggers", definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null ? definition.TriggersAndEvents.Triggers.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Weather Events", definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.WeatherEvents != null ? definition.TriggersAndEvents.WeatherEvents.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Scheduled Actions", definition != null && definition.ScheduledActions != null ? definition.ScheduledActions.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Gates", definition != null && definition.Gates != null ? definition.Gates.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Quests", definition != null && definition.Quests != null && definition.Quests.Quests != null ? definition.Quests.Quests.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_events",
                Title = "Triggers / Events / Quests",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildAssetSection(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Sprite Swaps", Item.CountSpriteSwaps(definition).ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Scene Sprite Placements", Item.CountSceneSpritePlacements(definition).ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Custom Sprites", definition != null && definition.AssetReferences != null && definition.AssetReferences.CustomSprites != null ? definition.AssetReferences.CustomSprites.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Sprite Patches", definition != null && definition.AssetReferences != null && definition.AssetReferences.SpritePatches != null ? definition.AssetReferences.SpritePatches.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_assets",
                Title = "Surface / Background / FX",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorAction BuildTargetAction(GameObject gameObject, ScenarioAuthoringTargetKind kind, string badge)
        {
            string label = gameObject != null && !string.IsNullOrEmpty(gameObject.name) ? gameObject.name : kind.ToString();
            string id = kind + ":" + (gameObject != null && gameObject.transform != null ? gameObject.transform.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "0");
            return Item.Action(
                ScenarioAuthoringActionIds.ActionHierarchySelectPrefix + id,
                label,
                gameObject != null ? BuildHierarchyPath(gameObject.transform) : "Missing runtime object",
                gameObject != null,
                false,
                badge);
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string FormatVector(ScenarioVector3 value)
        {
            if (value == null)
                return "<none>";

            return value.X.ToString("0.##", CultureInfo.InvariantCulture)
                + ","
                + value.Y.ToString("0.##", CultureInfo.InvariantCulture)
                + ","
                + value.Z.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ScenarioSelectionStackAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.SelectionStack; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            List<ScenarioAuthoringInspectorItem> summary = new List<ScenarioAuthoringInspectorItem>();
            int count = state != null && state.SelectionStack != null ? state.SelectionStack.Count : 0;
            summary.Add(Item.Property("Candidates", count.ToString(CultureInfo.InvariantCulture)));
            summary.Add(Item.Property("Active Row", count > 0 ? (Mathf.Clamp(state.ActiveSelectionStackIndex, 0, count - 1) + 1).ToString(CultureInfo.InvariantCulture) : "<none>"));
            summary.Add(Item.Property("Hovered", Item.FormatTarget(state != null ? state.HoveredTarget : null)));
            summary.Add(Item.Property("Selected", Item.FormatTarget(state != null ? state.SelectedTarget : null)));

            List<ScenarioAuthoringInspectorItem> rows = new List<ScenarioAuthoringInspectorItem>();
            if (count == 0)
            {
                rows.Add(Item.Text("Hold Ctrl and hover the bunker view to list selectable objects under the cursor."));
            }
            else
            {
                for (int i = 0; i < state.SelectionStack.Count; i++)
                {
                    ScenarioAuthoringTarget target = state.SelectionStack[i];
                    if (target == null)
                        continue;

                    bool active = i == state.ActiveSelectionStackIndex;
                    rows.Add(Item.ActionItem(Item.Action(
                        ScenarioAuthoringActionIds.ActionSelectionStackSelectPrefix + i.ToString(CultureInfo.InvariantCulture),
                        (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + Item.Safe(target.DisplayName),
                        target.Kind + " / " + Item.Safe(target.TransformPath),
                        true,
                        active,
                        active ? "ON" : "ST",
                        FormatGrid(target))));
                }
            }

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "selection_stack_summary",
                    Title = "Selection Stack",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = summary.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "selection_stack_rows",
                    Title = "Candidates Under Cursor",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = rows.ToArray()
                }
            };
        }

        private static string FormatGrid(ScenarioAuthoringTarget target)
        {
            if (target == null || !target.GridX.HasValue || !target.GridY.HasValue)
                return target != null ? target.Kind.ToString() : string.Empty;

            return "Grid " + target.GridX.Value.ToString(CultureInfo.InvariantCulture)
                + ","
                + target.GridY.Value.ToString(CultureInfo.InvariantCulture);
        }
    }

}
