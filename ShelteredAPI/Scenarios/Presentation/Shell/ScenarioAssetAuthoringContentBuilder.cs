using System;
using System.Collections.Generic;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAssetAuthoringContentBuilder
    {
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioSelectionScopeService _selectionScopeService;
        private readonly ScenarioSpriteRuntimeResolver _runtimeResolver;

        public ScenarioAssetAuthoringContentBuilder(
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioSpriteRuntimeResolver runtimeResolver)
        {
            _sectionHub = sectionHub;
            _selectionScopeService = selectionScopeService;
            _runtimeResolver = runtimeResolver;
        }

        public List<ScenarioAuthoringInspectorSection> BuildAssetSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildAssetModeSection(state));

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

            if (state != null && state.AssetMode == ScenarioAssetAuthoringMode.PlaceNew)
            {
                List<ScenarioAuthoringInspectorSection> placementSections = BuildSceneSpritePlacementSections(state, editorSession, target);
                for (int i = 0; i < placementSections.Count; i++)
                    sections.Add(placementSections[i]);
            }
            else
            {
                List<ScenarioAuthoringInspectorSection> spriteSections = BuildSpriteSwapSections(state, editorSession, target);
                for (int i = 0; i < spriteSections.Count; i++)
                    sections.Add(spriteSections[i]);
            }

            return sections;
        }

        public ScenarioSpriteSwapAuthoringService.CustomEditorModel BuildCustomEditorModel(ScenarioAuthoringState state)
        {
            return _sectionHub.SpriteSwap.GetCustomEditorModel(state);
        }

        private static ScenarioAuthoringInspectorSection BuildAssetModeSection(ScenarioAuthoringState state)
        {
            ScenarioAssetAuthoringMode mode = state != null ? state.AssetMode : ScenarioAssetAuthoringMode.ReplaceExisting;
            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_mode",
                Title = "Asset Picker",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ScenarioAuthoringPresentationBuilder.Property("Mode", mode.ToString()),
                    ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(ScenarioAuthoringActionIds.ActionAssetModeReplace, "Replace Existing", "Open the sprite picker for the selected visual target and save the change explicitly.", true, mode == ScenarioAssetAuthoringMode.ReplaceExisting, "RE", "Like-for-like runtime replacement.")),
                    ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(ScenarioAuthoringActionIds.ActionAssetModePlace, "Place New Snapped", "Create or update a snapped authored scene sprite placement.", true, mode == ScenarioAssetAuthoringMode.PlaceNew, "PL", "Snapped decorative scene placement."))
                }
            };
        }

        private List<ScenarioAuthoringInspectorSection> BuildSpriteSwapSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioSpriteSwapAuthoringService.SpritePickerModel picker = _sectionHub.SpriteSwap.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (picker == null || picker.Target == null)
                return sections;

            List<ScenarioAuthoringInspectorItem> summaryItems = new List<ScenarioAuthoringInspectorItem>();
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Text(
                ScenarioAuthoringPresentationBuilder.Safe(picker.Target.SpriteName),
                ScenarioAuthoringPresentationBuilder.Safe(picker.Target.TextureName),
                picker.Target.Kind.ToString(),
                "SP",
                picker.Target.CurrentSprite,
                true));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Component", picker.Target.Kind.ToString()));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Current Sprite", ScenarioAuthoringPresentationBuilder.Safe(picker.Target.SpriteName)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Current Map", ScenarioAuthoringPresentationBuilder.Safe(picker.Target.TextureName)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Active Swap", ScenarioAuthoringPresentationBuilder.Safe(picker.ActiveRuleSummary)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Compatibility", ScenarioAuthoringPresentationBuilder.Safe(picker.CompatibilitySummary)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Stored As", ScenarioAuthoringPresentationBuilder.Safe(picker.XmlPathHint)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Compatible Vanilla", CountCandidates(picker.VanillaCandidates).ToString()));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Compatible Modded", CountCandidates(picker.ModdedCandidates).ToString()));
            bool pickerOpen = state != null
                && state.SpriteSwapPicker != null
                && state.SpriteSwapPicker.IsOpen
                && ScenarioAuthoringPresentationBuilder.SameTarget(state.SpriteSwapPicker.Target, target);
            string previewLabel = state != null && state.SpriteSwapPicker != null
                ? state.SpriteSwapPicker.PreviewCandidateLabel
                : null;
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Picker", pickerOpen ? "Open" : "Closed"));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Preview", !string.IsNullOrEmpty(previewLabel) ? previewLabel : "<none>"));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                pickerOpen ? "Sprite Picker Open" : "Open Sprite Picker",
                "Open the dedicated sprite picker, preview compatible sprites in real time, then save or cancel.",
                true,
                pickerOpen)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Text(ScenarioAuthoringPresentationBuilder.Safe(picker.GuidanceMessage)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Text("This follows the same serializer shape other scenario packs use: AssetReferences > SpriteSwaps > Swap."));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_swap",
                Title = "Sprite Swap",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = summaryItems.ToArray()
            });
            return sections;
        }

        private List<ScenarioAuthoringInspectorSection> BuildSceneSpritePlacementSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioSceneSpritePlacementAuthoringService.PlacementPickerModel picker = _sectionHub.SceneSpritePlacement.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (picker == null)
                return sections;

            List<ScenarioAuthoringInspectorItem> summaryItems = new List<ScenarioAuthoringInspectorItem>();
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Text(
                ScenarioAuthoringPresentationBuilder.FormatTarget(target),
                target != null ? target.Kind.ToString() : "<none>",
                null,
                "AN",
                ResolvePreviewSprite(target),
                true));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Anchor", ScenarioAuthoringPresentationBuilder.FormatTarget(target)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Grid", target != null && target.GridX.HasValue && target.GridY.HasValue ? (target.GridX.Value + "," + target.GridY.Value) : "<none>"));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Active Placement", picker.ActivePlacement != null ? ScenarioAuthoringPresentationBuilder.Safe(picker.ActivePlacement.Id) : "<none>"));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Compatibility", ScenarioAuthoringPresentationBuilder.Safe(picker.CompatibilitySummary)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Stored As", ScenarioAuthoringPresentationBuilder.Safe(picker.XmlPathHint)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Vanilla Options", CountCandidates(picker.VanillaCandidates).ToString()));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Property("Modded Options", CountCandidates(picker.ModdedCandidates).ToString()));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Text(ScenarioAuthoringPresentationBuilder.Safe(picker.PlacementSummary)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Text(ScenarioAuthoringPresentationBuilder.Safe(picker.GuidanceMessage)));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.Text("This follows the same serializer shape other scenario packs use: AssetReferences > SceneSpritePlacements > Placement."));
            summaryItems.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove,
                "Remove Placement",
                "Remove the selected authored scene sprite placement from the draft.",
                picker.ActivePlacement != null,
                false)));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "scene_sprite",
                Title = "Scene Sprite Placement",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = summaryItems.ToArray()
            });
            sections.Add(BuildPlacementCandidateSection("scene_sprite_vanilla", "Vanilla Sprites", _selectionScopeService.FilterCandidatesForScope(picker.VanillaCandidates, state), "No loaded vanilla/runtime sprites match this selection scope."));
            sections.Add(BuildPlacementCandidateSection("scene_sprite_modded", "Modded Sprites", _selectionScopeService.FilterCandidatesForScope(picker.ModdedCandidates, state), "No modded sprites match this selection scope."));
            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioAuthoringPresentationBuilder.Property("Count", CountCandidates(candidates).ToString()));
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

                    items.Add(ScenarioAuthoringPresentationBuilder.ActionItem(ScenarioAuthoringPresentationBuilder.Action(
                        ScenarioSceneSpritePlacementAuthoringService.BuildApplyActionId(candidate.Token),
                        ScenarioAuthoringPresentationBuilder.CleanCandidateLabel(candidate.Label),
                        candidate.Hint,
                        true,
                        false,
                        "RT",
                        candidate.SourceName,
                        ScenarioAuthoringPresentationBuilder.BuildCandidateBadge(candidate),
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

        private static int CountCandidates(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates)
        {
            return candidates != null ? candidates.Count : 0;
        }
    }
}
