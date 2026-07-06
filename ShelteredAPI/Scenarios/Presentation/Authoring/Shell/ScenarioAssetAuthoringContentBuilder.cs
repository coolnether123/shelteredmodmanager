using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Presentation.Inspector;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioAssetAuthoringContentBuilder
    {
        private readonly ScenarioAssetPlacementContentBuilder _placementContentBuilder;
        private readonly ScenarioSelectedAssetEditorContentBuilder _editorContentBuilder;

        public ScenarioAssetAuthoringContentBuilder(
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioSpriteRuntimeResolver runtimeResolver)
        {
            _placementContentBuilder = new ScenarioAssetPlacementContentBuilder(
                sectionHub,
                selectionScopeService,
                runtimeResolver);
            _editorContentBuilder = new ScenarioSelectedAssetEditorContentBuilder(sectionHub);
        }

        public List<ScenarioAuthoringInspectorSection> BuildAssetPlacementSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            return _placementContentBuilder.Build(state, editorSession, target);
        }

        public List<ScenarioAuthoringInspectorSection> BuildSelectedAssetEditorSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            return _editorContentBuilder.Build(state, editorSession, target);
        }

        public ScenarioSpriteSwapAuthoringService.CustomEditorModel BuildCustomEditorModel(ScenarioAuthoringState state)
        {
            return _editorContentBuilder.BuildCustomEditorModel(state);
        }
    }

    internal sealed class ScenarioSelectedAssetEditorContentBuilder
    {
        private readonly IScenarioAuthoringSectionHub _sectionHub;

        public ScenarioSelectedAssetEditorContentBuilder(IScenarioAuthoringSectionHub sectionHub)
        {
            _sectionHub = sectionHub;
        }

        public List<ScenarioAuthoringInspectorSection> Build(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = BuildSpriteSwapSections(state, editorSession, target);
            if (sections.Count == 0)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "asset_editor",
                    Title = "Asset Editing",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Text("This target does not expose a compatible editable sprite asset.")
                    }
                });
            }

            return sections;
        }

        public ScenarioSpriteSwapAuthoringService.CustomEditorModel BuildCustomEditorModel(ScenarioAuthoringState state)
        {
            return _sectionHub.SpriteSwap.GetCustomEditorModel(state);
        }

        private List<ScenarioAuthoringInspectorSection> BuildSpriteSwapSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioSpriteSwapAuthoringService.SpritePickerModel model = _sectionHub.SpriteSwap.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (model == null || model.Target == null)
                return sections;

            bool editorOpen = state != null
                && state.SpriteSwapPicker != null
                && state.SpriteSwapPicker.IsOpen
                && ScenarioAuthoringPresentationBuilder.SameTarget(state.SpriteSwapPicker.Target, target);
            string previewLabel = state != null && state.SpriteSwapPicker != null
                ? state.SpriteSwapPicker.PreviewCandidateLabel
                : null;
            bool showAdvancedDetails = ShowAdvancedDetails(state);

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Current Look", ScenarioInspectorItemFactory.Safe(model.Target.SpriteName)));
            items.Add(ScenarioInspectorItemFactory.Property("Replacement", !string.IsNullOrEmpty(previewLabel) ? previewLabel : (model.HasActiveRule ? ScenarioInspectorItemFactory.Safe(model.ActiveRuleSummary) : "<none>")));
            items.Add(ScenarioInspectorItemFactory.Property("Options", ScenarioAssetAuthoringContentMetrics.CountCandidates(model.VanillaCandidates).ToString() + " vanilla / " + ScenarioAssetAuthoringContentMetrics.CountCandidates(model.ModdedCandidates).ToString() + " scenario"));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                editorOpen ? "Sprite Browser Open" : "Edit Look",
                "Open the Art sprite browser to preview replacements, import PNGs, or edit pixels.",
                true,
                editorOpen)));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_swap",
                Title = "Look",
                Expanded = false,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            });
            if (showAdvancedDetails)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "sprite_swap_advanced",
                    Title = "Advanced Details",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Component", model.Target.Kind.ToString()),
                        ScenarioInspectorItemFactory.Property("Current Map", ScenarioInspectorItemFactory.Safe(model.Target.TextureName)),
                        ScenarioInspectorItemFactory.Property("Stored As", ScenarioInspectorItemFactory.Safe(model.XmlPathHint)),
                        ScenarioInspectorItemFactory.Property("PNG Import Folder", ScenarioInspectorItemFactory.Safe(ScenarioPngImportService.GetImportFolderPath(state != null ? state.ActiveScenarioFilePath : null)))
                    }
                });
            }
            return sections;
        }

        private static bool ShowAdvancedDetails(ScenarioAuthoringState state)
        {
            return state != null
                && state.Settings != null
                && state.Settings.GetBool("debug.show_advanced_details", false);
        }

        private static string FriendlyKindLabel(ScenarioAuthoringTargetKind kind)
        {
            switch (kind)
            {
                case ScenarioAuthoringTargetKind.SceneSprite: return "Scene Art";
                case ScenarioAuthoringTargetKind.Background: return "Background Art";
                case ScenarioAuthoringTargetKind.PlaceableObject: return "Shelter Object";
                case ScenarioAuthoringTargetKind.Character: return "Survivor";
                case ScenarioAuthoringTargetKind.Tile: return "Shelter Tile";
                default: return kind.ToString();
            }
        }

        private static string FriendlyKindLabel(ScenarioSpriteTargetComponentKind kind)
        {
            switch (kind)
            {
                case ScenarioSpriteTargetComponentKind.SpriteRenderer: return "Sprite Renderer";
                case ScenarioSpriteTargetComponentKind.UI2DSprite: return "UI Sprite";
                case ScenarioSpriteTargetComponentKind.Auto: return "Auto";
                default: return kind.ToString();
            }
        }
    }

    internal sealed class ScenarioAssetPlacementContentBuilder
    {
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioSelectionScopeService _selectionScopeService;
        private readonly ScenarioSpriteRuntimeResolver _runtimeResolver;

        public ScenarioAssetPlacementContentBuilder(
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioSpriteRuntimeResolver runtimeResolver)
        {
            _sectionHub = sectionHub;
            _selectionScopeService = selectionScopeService;
            _runtimeResolver = runtimeResolver;
        }

        public List<ScenarioAuthoringInspectorSection> Build(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            string scopeReason;
            if (target != null && !_selectionScopeService.CanSelectTargetForCurrentStage(state, target, out scopeReason))
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "asset_scope_blocked",
                    Title = "Scope",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[] { ScenarioInspectorItemFactory.Text(scopeReason) }
                });
                return sections;
            }

            sections.Add(BuildPlacementBrowserGuidanceSection());
            List<ScenarioAuthoringInspectorSection> placementSections = BuildSceneSpritePlacementSections(state, editorSession, target);
            for (int i = 0; i < placementSections.Count; i++)
                sections.Add(placementSections[i]);

            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementBrowserGuidanceSection()
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_placement_browser",
                Title = "Asset Placement",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text("This browser is only for placing snapped scene sprites. Select an existing asset and use the Inspector's Edit Asset action to change that asset.")
                }
            };
        }

        private List<ScenarioAuthoringInspectorSection> BuildSceneSpritePlacementSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioSceneSpritePlacementAuthoringService.PlacementPickerModel model = _sectionHub.SceneSpritePlacement.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (model == null)
                return sections;

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            bool showAdvancedDetails = ShowAdvancedDetails(state);
            items.Add(ScenarioInspectorItemFactory.Text(
                ScenarioAuthoringPresentationBuilder.FormatTarget(target),
                target != null ? target.Kind.ToString() : "<none>",
                null,
                "AN",
                ResolvePreviewSprite(target),
                true));
            items.Add(ScenarioInspectorItemFactory.Property("Anchor", ScenarioAuthoringPresentationBuilder.FormatTarget(target)));
            items.Add(ScenarioInspectorItemFactory.Property("Grid", target != null && target.GridX.HasValue && target.GridY.HasValue ? (target.GridX.Value + "," + target.GridY.Value) : "<none>"));
            items.Add(ScenarioInspectorItemFactory.Property("Active Placement", model.ActivePlacement != null ? ScenarioInspectorItemFactory.Safe(model.ActivePlacement.Id) : "<none>"));
            items.Add(ScenarioInspectorItemFactory.Property("Active Sprite", !string.IsNullOrEmpty(model.ActiveCandidateLabel) ? ScenarioInspectorItemFactory.Safe(model.ActiveCandidateLabel) : "<none>"));
            items.Add(ScenarioInspectorItemFactory.Property("Placement Preview", model.PlacementActive ? "Active" : "Inactive"));
            items.Add(ScenarioInspectorItemFactory.Property("Compatibility", ScenarioInspectorItemFactory.Safe(model.CompatibilitySummary)));
            items.Add(ScenarioInspectorItemFactory.Property("Vanilla Options", ScenarioAssetAuthoringContentMetrics.CountCandidates(model.VanillaCandidates).ToString()));
            items.Add(ScenarioInspectorItemFactory.Property("Modded Options", ScenarioAssetAuthoringContentMetrics.CountCandidates(model.ModdedCandidates).ToString()));
            items.Add(ScenarioInspectorItemFactory.Text(ScenarioInspectorItemFactory.Safe(model.PlacementSummary)));
            items.Add(ScenarioInspectorItemFactory.Text(ScenarioInspectorItemFactory.Safe(model.GuidanceMessage)));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove,
                "Remove Placement",
                "Remove the selected authored scene sprite placement from the draft.",
                model.ActivePlacement != null,
                false)));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionSceneSpritePlacementCancel,
                "Cancel Active Placement",
                "Stop the current scene sprite placement preview without changing the draft.",
                model.PlacementActive,
                false)));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "scene_sprite",
                Title = "Scene Sprite Placement",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            });
            if (showAdvancedDetails)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "scene_sprite_advanced",
                    Title = "Advanced Details",
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Stored As", ScenarioInspectorItemFactory.Safe(model.XmlPathHint)),
                        ScenarioInspectorItemFactory.Property("Filtered People", model.BlockedPeople.ToString()),
                        ScenarioInspectorItemFactory.Property("Filtered Objects", model.BlockedInteractiveObjects.ToString()),
                        ScenarioInspectorItemFactory.Property("Filtered Pathing", model.BlockedPathfindingActors.ToString()),
                        ScenarioInspectorItemFactory.Property("Filtered Gameplay", model.BlockedGameplayAssets.ToString()),
                        ScenarioInspectorItemFactory.Text("Serializer path: AssetReferences > SceneSpritePlacements > Placement.")
                    }
                });
            }
            sections.Add(BuildPlacementCandidateSection("scene_sprite_vanilla", "Vanilla Sprites", model.VanillaCandidates, "No loaded vanilla/runtime sprites are available.", model.ActiveCandidateToken));
            sections.Add(BuildPlacementCandidateSection("scene_sprite_modded", "Scenario Sprites", model.ModdedCandidates, "No scenario custom sprites are available.", model.ActiveCandidateToken));
            return sections;
        }

        private static bool ShowAdvancedDetails(ScenarioAuthoringState state)
        {
            return state != null
                && state.Settings != null
                && state.Settings.GetBool("debug.show_advanced_details", false);
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage,
            string activeToken)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Count", ScenarioAssetAuthoringContentMetrics.CountCandidates(candidates).ToString()));
            if (candidates == null || candidates.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text(emptyMessage));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    bool active = string.Equals(candidate.Token, activeToken, StringComparison.Ordinal);
                    items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioSceneSpritePlacementAuthoringService.BuildApplyActionId(candidate.Token),
                        ScenarioAuthoringPresentationBuilder.CleanCandidateLabel(candidate.Label),
                        candidate.Hint,
                        true,
                        active,
                        "RT",
                        candidate.SourceName,
                        active ? "ACTIVE" : ScenarioAuthoringPresentationBuilder.BuildCandidateBadge(candidate),
                        candidate.Sprite)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = title,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
            };
        }

        private Sprite ResolvePreviewSprite(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            ScenarioSpriteRuntimeResolver.ResolvedTarget resolvedTarget;
            return _runtimeResolver.TryResolve(target, out resolvedTarget) && resolvedTarget != null
                ? resolvedTarget.CurrentSprite
                : null;
        }
    }

    internal static class ScenarioAssetAuthoringContentMetrics
    {
        public static int CountCandidates(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates)
        {
            return candidates != null ? candidates.Count : 0;
        }
    }
}
