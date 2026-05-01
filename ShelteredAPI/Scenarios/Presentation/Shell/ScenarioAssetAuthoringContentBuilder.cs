using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
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
                        ScenarioAuthoringPresentationBuilder.Text("This target does not expose a compatible editable sprite asset.")
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

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioAuthoringPresentationBuilder.Text(
                ScenarioAuthoringPresentationBuilder.Safe(model.Target.SpriteName),
                ScenarioAuthoringPresentationBuilder.Safe(model.Target.TextureName),
                model.Target.Kind.ToString(),
                "SP",
                model.Target.CurrentSprite,
                true));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Component", model.Target.Kind.ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Current Sprite", ScenarioAuthoringPresentationBuilder.Safe(model.Target.SpriteName)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Current Map", ScenarioAuthoringPresentationBuilder.Safe(model.Target.TextureName)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Active Swap", ScenarioAuthoringPresentationBuilder.Safe(model.ActiveRuleSummary)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Compatibility", ScenarioAuthoringPresentationBuilder.Safe(model.CompatibilitySummary)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Stored As", ScenarioAuthoringPresentationBuilder.Safe(model.XmlPathHint)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("PNG Import Folder", ScenarioAuthoringPresentationBuilder.Safe(ScenarioPngImportService.GetImportFolderPath(state != null ? state.ActiveScenarioFilePath : null))));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Compatible Vanilla", ScenarioAssetAuthoringContentMetrics.CountCandidates(model.VanillaCandidates).ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Compatible Modded", ScenarioAssetAuthoringContentMetrics.CountCandidates(model.ModdedCandidates).ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Editor", editorOpen ? "Open" : "Closed"));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Preview", !string.IsNullOrEmpty(previewLabel) ? previewLabel : "<none>"));
            items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                editorOpen ? "Asset Editor Open" : "Edit Asset",
                "Open the dedicated asset editor, preview compatible replacements in real time, then save or cancel.",
                true,
                editorOpen)));
            items.Add(ScenarioAuthoringPresentationBuilder.Text(ScenarioAuthoringPresentationBuilder.Safe(model.GuidanceMessage)));
            items.Add(ScenarioAuthoringPresentationBuilder.Text("This follows the same serializer shape other scenario packs use: AssetReferences > SpriteSwaps > Swap."));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_swap",
                Title = "Asset Editing",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            });
            return sections;
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
                    Items = new[] { ScenarioAuthoringPresentationBuilder.Text(scopeReason) }
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
                    ScenarioAuthoringPresentationBuilder.Text("This browser is only for placing snapped scene sprites. Select an existing asset and use the Inspector's Edit Asset action to change that asset.")
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
            items.Add(ScenarioAuthoringPresentationBuilder.Text(
                ScenarioAuthoringPresentationBuilder.FormatTarget(target),
                target != null ? target.Kind.ToString() : "<none>",
                null,
                "AN",
                ResolvePreviewSprite(target),
                true));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Anchor", ScenarioAuthoringPresentationBuilder.FormatTarget(target)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Grid", target != null && target.GridX.HasValue && target.GridY.HasValue ? (target.GridX.Value + "," + target.GridY.Value) : "<none>"));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Active Placement", model.ActivePlacement != null ? ScenarioAuthoringPresentationBuilder.Safe(model.ActivePlacement.Id) : "<none>"));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Active Sprite", !string.IsNullOrEmpty(model.ActiveCandidateLabel) ? ScenarioAuthoringPresentationBuilder.Safe(model.ActiveCandidateLabel) : "<none>"));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Placement Preview", model.PlacementActive ? "Active" : "Inactive"));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Compatibility", ScenarioAuthoringPresentationBuilder.Safe(model.CompatibilitySummary)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Stored As", ScenarioAuthoringPresentationBuilder.Safe(model.XmlPathHint)));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Vanilla Options", ScenarioAssetAuthoringContentMetrics.CountCandidates(model.VanillaCandidates).ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Modded Options", ScenarioAssetAuthoringContentMetrics.CountCandidates(model.ModdedCandidates).ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Filtered People", model.BlockedPeople.ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Filtered Objects", model.BlockedInteractiveObjects.ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Filtered Pathing", model.BlockedPathfindingActors.ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Filtered Gameplay", model.BlockedGameplayAssets.ToString()));
            items.Add(ScenarioAuthoringPresentationBuilder.Text(ScenarioAuthoringPresentationBuilder.Safe(model.PlacementSummary)));
            items.Add(ScenarioAuthoringPresentationBuilder.Text(ScenarioAuthoringPresentationBuilder.Safe(model.GuidanceMessage)));
            items.Add(ScenarioAuthoringPresentationBuilder.Text("This follows the same serializer shape other scenario packs use: AssetReferences > SceneSpritePlacements > Placement."));
            items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove,
                "Remove Placement",
                "Remove the selected authored scene sprite placement from the draft.",
                model.ActivePlacement != null,
                false)));
            items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
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
            sections.Add(BuildPlacementCandidateSection("scene_sprite_vanilla", "Vanilla Sprites", model.VanillaCandidates, "No loaded vanilla/runtime sprites are available.", model.ActiveCandidateToken));
            sections.Add(BuildPlacementCandidateSection("scene_sprite_modded", "Scenario Sprites", model.ModdedCandidates, "No scenario custom sprites are available.", model.ActiveCandidateToken));
            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage,
            string activeToken)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Count", ScenarioAssetAuthoringContentMetrics.CountCandidates(candidates).ToString()));
            if (candidates == null || candidates.Count == 0)
            {
                items.Add(ScenarioAuthoringPresentationBuilder.Text(emptyMessage));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    bool active = string.Equals(candidate.Token, activeToken, StringComparison.Ordinal);
                    items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
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
