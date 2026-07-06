using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Shared;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioHierarchyAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Hierarchy; } }
        private static Obj_Base[] _cachedLiveObjects;
        private static int _cachedLiveObjectsFrame = -1;

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioDefinition definition = context != null ? context.Definition : null;
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildSummarySection(state, definition));
            sections.Add(BuildBunkerSection(state, definition));
            sections.Add(BuildLiveObjectsSection(state));
            sections.Add(BuildCharactersSection(state, definition));
            sections.Add(BuildEventSection(state, definition));
            sections.Add(BuildAssetSection(state, definition));
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
                    Item.Property("Selection", FormatTargetConcept(state != null ? state.SelectedTarget : null, ShowAdvancedDetails(state)))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildBunkerSection(ScenarioAuthoringState state, ScenarioDefinition definition)
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

                items.Add(Item.ActionItem(BuildObjectPlacementAction(state, placement, i)));
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

        private static ScenarioAuthoringInspectorSection BuildLiveObjectsSection(ScenarioAuthoringState state)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            Obj_Base[] objects = GetLiveObjects();
            int count = 0;
            for (int i = 0; objects != null && i < objects.Length; i++)
            {
                Obj_Base obj = objects[i];
                if (obj == null || obj.gameObject == null)
                    continue;

                count++;
                if (items.Count < 10)
                    items.Add(Item.ActionItem(BuildTargetAction(state, obj.gameObject, ScenarioAuthoringTargetKind.PlaceableObject, "OB")));
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

        internal static void InvalidateLiveObjectCache()
        {
            _cachedLiveObjects = null;
            _cachedLiveObjectsFrame = -1;
        }

        private static Obj_Base[] GetLiveObjects()
        {
            if (_cachedLiveObjects != null
                && (_cachedLiveObjectsFrame < 0 || Time.frameCount - _cachedLiveObjectsFrame < 60))
            {
                return _cachedLiveObjects;
            }

            _cachedLiveObjects = UnityEngine.Object.FindObjectsOfType<Obj_Base>();
            _cachedLiveObjectsFrame = Time.frameCount;
            return _cachedLiveObjects;
        }

        private static ScenarioAuthoringInspectorSection BuildCharactersSection(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int authored = definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null ? definition.FamilySetup.Members.Count : 0;
            int future = definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null ? definition.FamilySetup.FutureSurvivors.Count : 0;
            items.Add(Item.Property("Authored Starting Survivors", authored.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Future Survivors", future.ToString(CultureInfo.InvariantCulture)));
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null && i < definition.FamilySetup.Members.Count && i < 8; i++)
            {
                FamilyMemberConfig member = definition.FamilySetup.Members[i];
                string label = member != null && !string.IsNullOrEmpty(member.Name) ? member.Name : "Starting Survivor " + (i + 1).ToString(CultureInfo.InvariantCulture);
                items.Add(Item.ActionItem(BuildNavigationAction(
                    ScenarioAuthoringActionIds.ActionToolFamily,
                    label,
                    "Open Cast to edit this starting survivor.",
                    "CAST",
                    false)));
            }
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count && i < 4; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                string label = survivor != null && survivor.Survivor != null && !string.IsNullOrEmpty(survivor.Survivor.Name) ? survivor.Survivor.Name : "Future Survivor " + (i + 1).ToString(CultureInfo.InvariantCulture);
                items.Add(Item.ActionItem(BuildNavigationAction(
                    ScenarioAuthoringActionIds.ActionToolFamily,
                    label,
                    "Open Cast to edit this future survivor arrival.",
                    "FUT",
                    false)));
            }

            FamilyManager manager = FamilyManager.Instance;
            List<FamilyMember> members = manager != null ? manager.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count && i < 8; i++)
            {
                FamilyMember member = members[i];
                if (member != null && member.gameObject != null)
                    items.Add(Item.ActionItem(BuildTargetAction(state, member.gameObject, ScenarioAuthoringTargetKind.Character, "PP")));
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

        private static ScenarioAuthoringInspectorSection BuildEventSection(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Triggers", definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null ? definition.TriggersAndEvents.Triggers.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Weather Events", definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.WeatherEvents != null ? definition.TriggersAndEvents.WeatherEvents.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Scheduled Actions", definition != null && definition.ScheduledActions != null ? definition.ScheduledActions.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Gates", definition != null && definition.Gates != null ? definition.Gates.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Quests", definition != null && definition.Quests != null && definition.Quests.Quests != null ? definition.Quests.Quests.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count && i < 4; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                items.Add(Item.ActionItem(BuildNavigationAction(
                    ScenarioAuthoringActionIds.ActionShellOpenTimeline,
                    !string.IsNullOrEmpty(trigger != null ? trigger.Id : null) ? trigger.Id : "Trigger " + (i + 1).ToString(CultureInfo.InvariantCulture),
                    "Open Timeline to inspect this trigger.",
                    "TR",
                    state != null && state.ActiveStage == ScenarioStageKind.Events)));
            }
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count && i < 4; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                items.Add(Item.ActionItem(BuildNavigationAction(
                    ScenarioAuthoringActionIds.ActionShellOpenTimeline,
                    !string.IsNullOrEmpty(action != null ? action.Id : null) ? action.Id : "Scheduled Action " + (i + 1).ToString(CultureInfo.InvariantCulture),
                    "Open Timeline to inspect this scheduled action.",
                    "EV",
                    state != null && state.ActiveStage == ScenarioStageKind.Events)));
            }
            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_events",
                Title = "Triggers / Events / Quests",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildAssetSection(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Sprite Swaps", Item.CountSpriteSwaps(definition).ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Scene Sprite Placements", Item.CountSceneSpritePlacements(definition).ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Custom Sprites", definition != null && definition.AssetReferences != null && definition.AssetReferences.CustomSprites != null ? definition.AssetReferences.CustomSprites.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            items.Add(Item.Property("Sprite Patches", definition != null && definition.AssetReferences != null && definition.AssetReferences.SpritePatches != null ? definition.AssetReferences.SpritePatches.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            for (int i = 0; definition != null && definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null && i < definition.AssetReferences.SceneSpritePlacements.Count && i < 6; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                items.Add(Item.ActionItem(BuildNavigationAction(
                    ScenarioAuthoringActionIds.ActionToolAssets,
                    !string.IsNullOrEmpty(placement != null ? placement.SpriteId : null) ? placement.SpriteId : "Scene Sprite " + (i + 1).ToString(CultureInfo.InvariantCulture),
                    "Open Art to edit scene sprite placements.",
                    "ART",
                    state != null && state.ActiveTool == ScenarioAuthoringTool.Assets)));
            }
            return new ScenarioAuthoringInspectorSection
            {
                Id = "hierarchy_assets",
                Title = "Surface / Background / FX",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorAction BuildObjectPlacementAction(ScenarioAuthoringState state, ObjectPlacement placement, int index)
        {
            string label = !string.IsNullOrEmpty(placement.DefinitionReference) ? placement.DefinitionReference : (!string.IsNullOrEmpty(placement.PrefabReference) ? placement.PrefabReference : placement.ScenarioObjectId);
            string id = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : placement.RuntimeBindingKey;
            ScenarioObjectPlacementRuntimeBinding binding = ScenarioObjectPlacementRuntimeBinding.Find(id);
            if (binding != null && binding.gameObject != null)
                return BuildTargetAction(state, binding.gameObject, ScenarioAuthoringTargetKind.PlaceableObject, "OBJ", placement.ScenarioObjectId);

            return BuildNavigationAction(
                ScenarioAuthoringActionIds.ActionToolObjects,
                Item.Safe(label),
                "Open Objects to review this draft placement at " + FormatVector(placement.Position) + " / " + placement.StartState + ".",
                "OBJ",
                false);
        }

        private static ScenarioAuthoringInspectorAction BuildTargetAction(ScenarioAuthoringState state, GameObject gameObject, ScenarioAuthoringTargetKind kind, string badge)
        {
            return BuildTargetAction(state, gameObject, kind, badge, null);
        }

        private static ScenarioAuthoringInspectorAction BuildTargetAction(ScenarioAuthoringState state, GameObject gameObject, ScenarioAuthoringTargetKind kind, string badge, string scenarioReferenceId)
        {
            string label = gameObject != null && !string.IsNullOrEmpty(gameObject.name) ? gameObject.name : kind.ToString();
            string id = kind + ":" + (gameObject != null && gameObject.transform != null ? gameObject.transform.GetInstanceID().ToString(CultureInfo.InvariantCulture) : "0");
            bool activeSelected = IsTargetSelected(state, id, scenarioReferenceId, gameObject);
            bool selected = activeSelected || IsTargetInMultiSelection(state, id, scenarioReferenceId, gameObject);
            bool hovered = !selected && IsTargetHovered(state, id, scenarioReferenceId, gameObject);
            bool showAdvancedDetails = ShowAdvancedDetails(state);
            return Item.Action(
                ScenarioAuthoringActionIds.ActionHierarchySelectPrefix + id,
                label,
                FormatRowDetail(FormatGameObjectConcept(gameObject, kind, showAdvancedDetails), activeSelected, selected, hovered),
                gameObject != null,
                activeSelected,
                selected ? "SEL" : (hovered ? "HOV" : badge));
        }

        private static ScenarioAuthoringInspectorAction BuildNavigationAction(string actionId, string label, string detail, string badge, bool emphasized)
        {
            return Item.Action(actionId, Item.Safe(label), detail, true, emphasized, badge, detail);
        }

        private static bool IsTargetSelected(ScenarioAuthoringState state, string id, string scenarioReferenceId, GameObject gameObject)
        {
            return IsSameTarget(state != null ? state.SelectedTarget : null, id, scenarioReferenceId, gameObject);
        }

        private static bool IsTargetHovered(ScenarioAuthoringState state, string id, string scenarioReferenceId, GameObject gameObject)
        {
            return IsSameTarget(state != null ? state.HoveredTarget : null, id, scenarioReferenceId, gameObject);
        }

        private static bool IsTargetInMultiSelection(ScenarioAuthoringState state, string id, string scenarioReferenceId, GameObject gameObject)
        {
            for (int i = 0; state != null && state.MultiSelection != null && i < state.MultiSelection.Count; i++)
            {
                if (IsSameTarget(state.MultiSelection[i], id, scenarioReferenceId, gameObject))
                    return true;
            }

            return false;
        }

        private static bool IsSameTarget(ScenarioAuthoringTarget target, string id, string scenarioReferenceId, GameObject gameObject)
        {
            if (target == null)
                return false;
            if (!string.IsNullOrEmpty(target.Id) && string.Equals(target.Id, id, System.StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(scenarioReferenceId) && string.Equals(target.ScenarioReferenceId, scenarioReferenceId, System.StringComparison.OrdinalIgnoreCase))
                return true;
            return gameObject != null
                && !string.IsNullOrEmpty(target.TransformPath)
                && string.Equals(target.TransformPath, BuildHierarchyPath(gameObject.transform), System.StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatRowDetail(string detail, bool activeSelected, bool selected, bool hovered)
        {
            if (activeSelected)
                return detail + " / selected";
            if (selected)
                return detail + " / selected (multi)";
            if (hovered)
                return detail + " / hovered";
            return detail;
        }

        private static string FormatGameObjectConcept(GameObject gameObject, ScenarioAuthoringTargetKind kind, bool showAdvancedDetails)
        {
            if (gameObject == null)
                return "Missing runtime object";

            List<string> parts = new List<string>();
            parts.Add(FormatKind(kind));
            Vector3 position = gameObject.transform != null ? gameObject.transform.position : Vector3.zero;
            parts.Add("at " + position.x.ToString("0.#", CultureInfo.InvariantCulture)
                + ", "
                + position.y.ToString("0.#", CultureInfo.InvariantCulture));
            if (showAdvancedDetails)
                parts.Add(BuildHierarchyPath(gameObject.transform));
            return string.Join(" / ", parts.ToArray());
        }

        internal static string FormatTargetConcept(ScenarioAuthoringTarget target, bool showAdvancedDetails)
        {
            if (target == null)
                return "No selection";

            List<string> parts = new List<string>();
            parts.Add(Item.Safe(target.DisplayName));
            parts.Add(FormatKind(target.Kind));
            if (target.GridX.HasValue && target.GridY.HasValue)
            {
                parts.Add("grid " + target.GridX.Value.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + target.GridY.Value.ToString(CultureInfo.InvariantCulture));
            }
            else if (target.WorldPosition != Vector3.zero)
            {
                Vector3 position = target.WorldPosition;
                parts.Add("at " + position.x.ToString("0.#", CultureInfo.InvariantCulture)
                    + ", "
                    + position.y.ToString("0.#", CultureInfo.InvariantCulture));
            }

            if (showAdvancedDetails && !string.IsNullOrEmpty(target.TransformPath))
                parts.Add(target.TransformPath);
            return string.Join(" / ", parts.ToArray());
        }

        internal static string FormatKind(ScenarioAuthoringTargetKind kind)
        {
            switch (kind)
            {
                case ScenarioAuthoringTargetKind.PlaceableObject: return "Shelter Object";
                case ScenarioAuthoringTargetKind.Character: return "Survivor";
                case ScenarioAuthoringTargetKind.Tile: return "Room Tile";
                case ScenarioAuthoringTargetKind.SceneSprite: return "Scene Art";
                case ScenarioAuthoringTargetKind.Background: return "Background";
                default: return kind.ToString();
            }
        }

        internal static bool ShowAdvancedDetails(ScenarioAuthoringState state)
        {
            return state != null
                && state.Settings != null
                && state.Settings.GetBool("debug.show_advanced_details", false);
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
            int activeIndex = count > 0 ? Mathf.Clamp(state.ActiveSelectionStackIndex, 0, count - 1) : -1;
            bool showAdvancedDetails = ScenarioHierarchyAuthoringContentBuilder.ShowAdvancedDetails(state);
            summary.Add(Item.Property("Candidates", count.ToString(CultureInfo.InvariantCulture)));
            summary.Add(Item.Property("Target", count > 0 ? ("Target " + (activeIndex + 1).ToString(CultureInfo.InvariantCulture) + " of " + count.ToString(CultureInfo.InvariantCulture)) : "No captured stack"));
            summary.Add(Item.Property("Hovered", ScenarioHierarchyAuthoringContentBuilder.FormatTargetConcept(state != null ? state.HoveredTarget : null, showAdvancedDetails)));
            summary.Add(Item.Property("Selected", ScenarioHierarchyAuthoringContentBuilder.FormatTargetConcept(state != null ? state.SelectedTarget : null, showAdvancedDetails)));

            List<ScenarioAuthoringInspectorItem> rows = new List<ScenarioAuthoringInspectorItem>();
            if (count == 0)
            {
                rows.Add(Item.Text("Click a shelter target to capture the selectable stack at that point."));
            }
            else
            {
                for (int i = 0; i < state.SelectionStack.Count; i++)
                {
                    ScenarioAuthoringTarget target = state.SelectionStack[i];
                    if (target == null)
                        continue;

                    bool active = i == state.ActiveSelectionStackIndex;
                    bool activeSelected = SameTarget(state.SelectedTarget, target);
                    bool selected = activeSelected || IsInMultiSelection(state, target);
                    bool hovered = !selected && SameTarget(state.HoveredTarget, target);
                    bool emphasized = activeSelected || (state.SelectedTarget == null && active);
                    rows.Add(Item.ActionItem(Item.Action(
                        ScenarioAuthoringActionIds.ActionSelectionStackSelectPrefix + i.ToString(CultureInfo.InvariantCulture),
                        (i + 1).ToString(CultureInfo.InvariantCulture) + ". " + Item.Safe(target.DisplayName),
                        FormatStackDetail(target, activeSelected, selected, hovered, active, showAdvancedDetails),
                        true,
                        emphasized,
                        selected ? "SEL" : (hovered ? "HOV" : (active ? "ON" : "ST")),
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
                    Title = "Captured Candidates",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = rows.ToArray()
                }
            };
        }

        private static string FormatGrid(ScenarioAuthoringTarget target)
        {
            if (target == null || !target.GridX.HasValue || !target.GridY.HasValue)
                return target != null ? ScenarioHierarchyAuthoringContentBuilder.FormatKind(target.Kind) : string.Empty;

            return "Grid " + target.GridX.Value.ToString(CultureInfo.InvariantCulture)
                + ","
                + target.GridY.Value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatStackDetail(ScenarioAuthoringTarget target, bool activeSelected, bool selected, bool hovered, bool active, bool showAdvancedDetails)
        {
            string detail = ScenarioHierarchyAuthoringContentBuilder.FormatTargetConcept(target, showAdvancedDetails);
            if (activeSelected)
                return detail + " / selected";
            if (selected)
                return detail + " / selected (multi)";
            if (hovered)
                return detail + " / hovered";
            if (active)
                return detail + " / active";
            return detail;
        }

        private static bool IsInMultiSelection(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            for (int i = 0; state != null && state.MultiSelection != null && i < state.MultiSelection.Count; i++)
            {
                if (SameTarget(state.MultiSelection[i], target))
                    return true;
            }

            return false;
        }

        private static bool SameTarget(ScenarioAuthoringTarget left, ScenarioAuthoringTarget right)
        {
            if (left == null || right == null)
                return false;
            if (!string.IsNullOrEmpty(left.Id) && !string.IsNullOrEmpty(right.Id))
                return string.Equals(left.Id, right.Id, System.StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(left.ScenarioReferenceId) && !string.IsNullOrEmpty(right.ScenarioReferenceId))
                return string.Equals(left.ScenarioReferenceId, right.ScenarioReferenceId, System.StringComparison.OrdinalIgnoreCase);
            return !string.IsNullOrEmpty(left.TransformPath)
                && string.Equals(left.TransformPath, right.TransformPath, System.StringComparison.OrdinalIgnoreCase);
        }
    }

}
