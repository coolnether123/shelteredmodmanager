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
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Presentation.Inspector;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioAssetAuthoringContentBuilder
    {
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioWeatherEffectSpriteCatalogService _weatherEffectSpriteCatalog;
        private readonly ScenarioAssetPlacementContentBuilder _placementContentBuilder;
        private readonly ScenarioSelectedAssetEditorContentBuilder _editorContentBuilder;
        private readonly ScenarioAssetInventoryContentBuilder _inventoryContentBuilder;

        public ScenarioAssetAuthoringContentBuilder(
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioSpriteRuntimeResolver runtimeResolver,
            ScenarioWeatherEffectSpriteCatalogService weatherEffectSpriteCatalog,
            ScenarioAssetInventoryService assetInventoryService)
        {
            _sectionHub = sectionHub;
            _weatherEffectSpriteCatalog = weatherEffectSpriteCatalog;
            _placementContentBuilder = new ScenarioAssetPlacementContentBuilder(
                sectionHub,
                selectionScopeService,
                runtimeResolver,
                weatherEffectSpriteCatalog);
            _editorContentBuilder = new ScenarioSelectedAssetEditorContentBuilder(sectionHub);
            _inventoryContentBuilder = new ScenarioAssetInventoryContentBuilder(assetInventoryService);
        }

        public List<ScenarioAuthoringInspectorSection> BuildAssetPlacementSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = _placementContentBuilder.Build(state, editorSession, target);
            if (ScenarioWeatherEffectSpriteCatalogService.IsWeatherEffectTarget(target))
            {
                List<ScenarioAuthoringInspectorSection> editorSections = _editorContentBuilder.Build(state, editorSession, target);
                for (int i = 0; i < editorSections.Count; i++)
                    sections.Add(editorSections[i]);
            }

            return sections;
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

        public ScenarioAuthoringInspectorSection[] BuildAssetBrowserSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession)
        {
            List<ScenarioAuthoringInspectorSection> sections = _inventoryContentBuilder.Build(state, editorSession);
            List<ScenarioAuthoringInspectorSection> catalog = new ScenarioAssetBrowserCatalogContentBuilder(_sectionHub, _weatherEffectSpriteCatalog).Build(state, editorSession);
            for (int i = 0; i < catalog.Count; i++) sections.Add(catalog[i]);
            return sections.ToArray();
        }
    }

    internal sealed class ScenarioAssetBrowserCatalogContentBuilder
    {
        private readonly IScenarioAuthoringSectionHub _sectionHub;
        private readonly ScenarioWeatherEffectSpriteCatalogService _weatherEffectSpriteCatalog;

        public ScenarioAssetBrowserCatalogContentBuilder(
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioWeatherEffectSpriteCatalogService weatherEffectSpriteCatalog)
        {
            _sectionHub = sectionHub;
            _weatherEffectSpriteCatalog = weatherEffectSpriteCatalog;
        }

        public List<ScenarioAuthoringInspectorSection> Build(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession)
        {
            List<BrowserAssetEntry> entries = new List<BrowserAssetEntry>();
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildIntroSection());
            AddBuildPaletteSections(sections, entries, state, editorSession, ScenarioAuthoringTool.Objects, "Objects", "OBJ");
            AddBuildPaletteSections(sections, entries, state, editorSession, ScenarioAuthoringTool.Shelter, "Rooms", "ROOM");
            AddBuildPaletteSections(sections, entries, state, editorSession, ScenarioAuthoringTool.Wiring, "Walls", "WALL");
            AddSceneSpriteSections(sections, entries, state, editorSession);
            AddWeatherEffectSection(sections, entries, state);
            sections.Insert(1, BuildSelectedAssetSection(state, entries));
            return sections;
        }

        private static ScenarioAuthoringInspectorSection BuildIntroSection()
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_browser_intro",
                Title = "Asset Browser",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Text("Browse the same object, room, wall, scene-sprite, and editable effect catalogs used by the in-world Tool Workspace. Select an asset here, then place or edit it from the detail pane.")
                }
            };
        }

        private void AddBuildPaletteSections(
            List<ScenarioAuthoringInspectorSection> sections,
            List<BrowserAssetEntry> entries,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTool tool,
            string category,
            string iconText)
        {
            if (_sectionHub == null || _sectionHub.BuildPlacement == null)
                return;

            ScenarioAuthoringState toolState = state != null ? state.Copy() : new ScenarioAuthoringState();
            toolState.ActiveTool = tool;
            toolState.ActiveStage = tool == ScenarioAuthoringTool.Wiring ? ScenarioStageKind.BunkerBackground : ScenarioStageKind.BunkerInside;
            List<ScenarioBuildPlacementAuthoringService.PaletteSectionModel> paletteSections = _sectionHub.BuildPlacement.GetPaletteSections(toolState, editorSession);
            for (int i = 0; paletteSections != null && i < paletteSections.Count; i++)
                sections.Add(BuildPaletteSection(paletteSections[i], entries, state, category, iconText));
        }

        private static ScenarioAuthoringInspectorSection BuildPaletteSection(
            ScenarioBuildPlacementAuthoringService.PaletteSectionModel model,
            List<BrowserAssetEntry> entries,
            ScenarioAuthoringState state,
            string category,
            string iconText)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int count = model != null && model.Entries != null ? model.Entries.Count : 0;
            items.Add(ScenarioInspectorItemFactory.Property("Count", count.ToString()));
            if (count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text(model != null ? ScenarioInspectorItemFactory.Safe(model.EmptyMessage) : "No entries are available."));
            }
            else
            {
                for (int i = 0; model != null && model.Entries != null && i < model.Entries.Count; i++)
                {
                    ScenarioBuildPlacementAuthoringService.PaletteEntryModel entry = model.Entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.ActionId))
                        continue;

                    BrowserAssetEntry browserEntry = new BrowserAssetEntry
                    {
                        SourceActionId = entry.ActionId,
                        Label = ScenarioAssetDisplayNameProjection.Resolve(entry.Label, entry.ActionId, "Asset " + (i + 1).ToString()),
                        Category = !string.IsNullOrEmpty(model.Title) ? model.Title : category,
                        Descriptor = !string.IsNullOrEmpty(entry.Badge) ? entry.Badge : iconText,
                        Detail = entry.Source,
                        Hint = entry.Hint,
                        PreviewSprite = entry.Preview,
                        Enabled = entry.Enabled,
                        Active = entry.Active,
                        CanPlace = IsPlaceableBuildAction(entry.ActionId),
                        CanApply = IsRoomVisualAction(entry.ActionId)
                    };
                    entries.Add(browserEntry);
                    items.Add(ScenarioInspectorItemFactory.ActionItem(BuildBrowserSelectAction(browserEntry, state)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_browser_" + SafeSectionId(model != null ? model.Id : null, category),
                Title = !string.IsNullOrEmpty(model != null ? model.Title : null) ? model.Title : category,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
            };
        }

        private void AddSceneSpriteSections(
            List<ScenarioAuthoringInspectorSection> sections,
            List<BrowserAssetEntry> entries,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession)
        {
            if (_sectionHub == null || _sectionHub.SceneSpritePlacement == null)
                return;

            ScenarioSceneSpritePlacementAuthoringService.PlacementPickerModel model = _sectionHub.SceneSpritePlacement.GetPickerModel(
                editorSession,
                state != null ? state.SelectedTarget : null,
                state != null ? state.ActiveScenarioFilePath : null);
            if (model == null)
                return;

            sections.Add(BuildSpriteCandidateSection("asset_browser_scene_vanilla", "Vanilla Sprites", model.VanillaCandidates, entries, state));
            sections.Add(BuildSpriteCandidateSection("asset_browser_scene_scenario", "Scenario Sprites", model.ModdedCandidates, entries, state));
        }

        private static ScenarioAuthoringInspectorSection BuildSpriteCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            List<BrowserAssetEntry> entries,
            ScenarioAuthoringState state)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Count", ScenarioAssetAuthoringContentMetrics.CountCandidates(candidates).ToString()));
            if (candidates == null || candidates.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text("No scene sprite assets are currently available for this source."));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    BrowserAssetEntry entry = new BrowserAssetEntry
                    {
                        SourceActionId = ScenarioSceneSpritePlacementAuthoringService.BuildApplyActionId(candidate.Token),
                        Label = ScenarioAssetDisplayNameProjection.Resolve(candidate.Label, candidate.Token, "Sprite " + (i + 1).ToString()),
                        Category = title,
                        Descriptor = ScenarioAuthoringPresentationBuilder.BuildCandidateBadge(candidate),
                        Detail = candidate.SourceName,
                        Hint = candidate.Hint,
                        PreviewSprite = candidate.Sprite,
                        Enabled = candidate.CanPlaceAsSceneSprite,
                        Active = false,
                        CanPlace = candidate.CanPlaceAsSceneSprite,
                        CanApply = false
                    };
                    entries.Add(entry);
                    items.Add(ScenarioInspectorItemFactory.ActionItem(BuildBrowserSelectAction(entry, state)));
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

        private void AddWeatherEffectSection(
            List<ScenarioAuthoringInspectorSection> sections,
            List<BrowserAssetEntry> entries,
            ScenarioAuthoringState state)
        {
            List<ScenarioWeatherEffectSpriteCatalogService.WeatherEffectSpriteTarget> targets =
                _weatherEffectSpriteCatalog != null
                    ? _weatherEffectSpriteCatalog.GetTargets()
                    : new List<ScenarioWeatherEffectSpriteCatalogService.WeatherEffectSpriteTarget>();
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Count", targets.Count.ToString()));
            if (targets.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text("No loaded weather or particle effects currently expose a sprite-editable material texture."));
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    ScenarioWeatherEffectSpriteCatalogService.WeatherEffectSpriteTarget target = targets[i];
                    if (target == null || target.Target == null)
                        continue;

                    BrowserAssetEntry entry = new BrowserAssetEntry
                    {
                        SourceActionId = ScenarioAuthoringActionIds.ActionWeatherEffectSpriteSelectPrefix + target.Target.Id,
                        Label = ScenarioAssetDisplayNameProjection.Resolve(target.Target.DisplayName, target.Target.Id, "Weather effect " + (i + 1).ToString()),
                        Category = "Weather & Effects",
                        Descriptor = "FX",
                        Detail = target.Group,
                        Hint = target.Source + " | Texture: " + ScenarioInspectorItemFactory.Safe(target.TextureName),
                        PreviewSprite = target.PreviewSprite,
                        PreviewTint = target.PreviewTint,
                        HasPreviewTint = target.HasPreviewTint,
                        Enabled = true,
                        Active = state != null
                            && state.SelectedTarget != null
                            && string.Equals(state.SelectedTarget.Id, target.Target.Id, StringComparison.OrdinalIgnoreCase),
                        CanPlace = false,
                        CanApply = false,
                        CanEdit = true
                    };
                    entries.Add(entry);
                    items.Add(ScenarioInspectorItemFactory.ActionItem(BuildBrowserSelectAction(entry, state)));
                }
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "asset_browser_weather_effects",
                Title = "Weather & Effects",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
            });
        }

        private static ScenarioAuthoringInspectorSection BuildSelectedAssetSection(
            ScenarioAuthoringState state,
            List<BrowserAssetEntry> entries)
        {
            BrowserAssetEntry selected = FindEntry(entries, state != null ? state.AssetBrowserSelectedActionId : null);
            if (selected == null)
            {
                return new ScenarioAuthoringInspectorSection
                {
                    Id = "asset_browser_selected",
                    Title = "Selected Asset",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Text("Select an asset to see details")
                    }
                };
            }

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioAuthoringInspectorItem previewItem = ScenarioInspectorItemFactory.Text(selected.Label, selected.Hint, selected.Descriptor, selected.Descriptor, selected.PreviewSprite, true);
            previewItem.PreviewTint = selected.PreviewTint;
            previewItem.HasPreviewTint = selected.HasPreviewTint;
            items.Add(previewItem);
            items.Add(ScenarioInspectorItemFactory.Property("Category", selected.Category));
            items.Add(ScenarioInspectorItemFactory.Property("Source", ScenarioInspectorItemFactory.Safe(selected.Detail)));
            items.Add(ScenarioInspectorItemFactory.Property("Action", ResolveActionSummary(selected)));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionAssetBrowserPlaceSelected,
                selected.CanApply ? "Apply To Room" : "Place In World",
                selected.CanApply ? "Apply this wall or wiring sprite to the selected shelter room." : "Switch to World placement mode with this asset armed.",
                selected.CanPlace || selected.CanApply,
                selected.CanPlace,
                selected.CanApply ? "AP" : "PL",
                null,
                null,
                null,
                selected.CanPlace || selected.CanApply ? null : "This catalog item is editable but not placeable.")));
            items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionIds.ActionAssetBrowserEditSelected,
                "Edit Sprite",
                "Open this editable art asset in the existing pixel editor flow.",
                selected.CanEdit,
                selected.CanEdit,
                "PX",
                null,
                null,
                null,
                selected.CanEdit ? null : "This catalog item does not expose an editable sprite target.")));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "asset_browser_selected",
                Title = "Selected Asset",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorAction BuildBrowserSelectAction(BrowserAssetEntry entry, ScenarioAuthoringState state)
        {
            bool selected = entry != null
                && state != null
                && string.Equals(state.AssetBrowserSelectedActionId, entry.SourceActionId, StringComparison.Ordinal);
            ScenarioAuthoringInspectorAction action = ScenarioInspectorItemFactory.Action(
                ScenarioAuthoringActionCodec.BuildTokenActionId(ScenarioAuthoringActionIds.ActionAssetBrowserSelectPrefix, entry != null ? entry.SourceActionId : null),
                entry != null ? entry.Label : "<asset>",
                entry != null ? entry.Hint : null,
                entry == null || entry.Enabled,
                selected || (entry != null && entry.Active),
                entry != null ? entry.Descriptor : null,
                entry != null ? entry.Detail : null,
                entry != null ? entry.Descriptor : null,
                entry != null ? entry.PreviewSprite : null);
            if (entry != null)
            {
                action.PreviewTint = entry.PreviewTint;
                action.HasPreviewTint = entry.HasPreviewTint;
            }
            return action;
        }

        private static BrowserAssetEntry FindEntry(List<BrowserAssetEntry> entries, string sourceActionId)
        {
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                BrowserAssetEntry entry = entries[i];
                if (entry != null && string.Equals(entry.SourceActionId, sourceActionId, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        private static bool IsPlaceableBuildAction(string actionId)
        {
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureRoom, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureLadder, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureLight, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(actionId) && actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix, StringComparison.Ordinal));
        }

        private static bool IsRoomVisualAction(string actionId)
        {
            return !string.IsNullOrEmpty(actionId)
                && (actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix, StringComparison.Ordinal)
                    || actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix, StringComparison.Ordinal));
        }

        private static string ResolveActionSummary(BrowserAssetEntry entry)
        {
            if (entry == null)
                return "<none>";
            if (entry.CanEdit)
                return "Editable sprite asset";
            if (entry.CanApply)
                return "Applies to selected room";
            if (entry.CanPlace)
                return "Placeable in World";
            return "Browse only";
        }

        private static string SafeSectionId(string id, string fallback)
        {
            string value = !string.IsNullOrEmpty(id) ? id : fallback;
            return (value ?? "assets").Replace(" ", "_").ToLowerInvariant();
        }

        private sealed class BrowserAssetEntry
        {
            public string SourceActionId;
            public string Label;
            public string Category;
            public string Descriptor;
            public string Detail;
            public string Hint;
            public Sprite PreviewSprite;
            public Color PreviewTint;
            public bool HasPreviewTint;
            public bool Enabled;
            public bool Active;
            public bool CanPlace;
            public bool CanApply;
            public bool CanEdit;
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
            items.Add(ScenarioInspectorItemFactory.Property("Current Look", ScenarioAssetDisplayNameProjection.Resolve(model.Target.SpriteName, model.Target.TextureName, "Current asset")));
            items.Add(ScenarioInspectorItemFactory.Property("Replacement", !string.IsNullOrEmpty(previewLabel) ? ScenarioAssetDisplayNameProjection.Resolve(previewLabel, null, "Selected replacement") : (model.HasActiveRule ? "Custom replacement active" : "No replacement selected")));
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
                    IsAdvanced = true,
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
                case ScenarioSpriteTargetComponentKind.ParticleSystemRenderer: return "Particle Renderer";
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
        private readonly ScenarioWeatherEffectSpriteCatalogService _weatherEffectSpriteCatalog;

        public ScenarioAssetPlacementContentBuilder(
            IScenarioAuthoringSectionHub sectionHub,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioSpriteRuntimeResolver runtimeResolver,
            ScenarioWeatherEffectSpriteCatalogService weatherEffectSpriteCatalog)
        {
            _sectionHub = sectionHub;
            _selectionScopeService = selectionScopeService;
            _runtimeResolver = runtimeResolver;
            _weatherEffectSpriteCatalog = weatherEffectSpriteCatalog;
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
            sections.Add(BuildWeatherEffectSection(state));
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

        private ScenarioAuthoringInspectorSection BuildWeatherEffectSection(ScenarioAuthoringState state)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioWeatherEffectSpriteCatalogService.WeatherEffectSpriteTarget> targets =
                _weatherEffectSpriteCatalog != null
                    ? _weatherEffectSpriteCatalog.GetTargets()
                    : new List<ScenarioWeatherEffectSpriteCatalogService.WeatherEffectSpriteTarget>();
            items.Add(ScenarioInspectorItemFactory.Property("Editable Effects", targets.Count.ToString()));
            if (targets.Count == 0)
            {
                items.Add(ScenarioInspectorItemFactory.Text("No loaded weather or particle effects currently expose a sprite-editable material texture."));
            }
            else
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    ScenarioWeatherEffectSpriteCatalogService.WeatherEffectSpriteTarget target = targets[i];
                    if (target == null || target.Target == null)
                        continue;

                    bool active = state != null
                        && state.SelectedTarget != null
                        && string.Equals(state.SelectedTarget.Id, target.Target.Id, StringComparison.OrdinalIgnoreCase);
                    items.Add(ScenarioInspectorItemFactory.ActionItem(ScenarioInspectorItemFactory.Action(
                        ScenarioAuthoringActionIds.ActionWeatherEffectSpriteSelectPrefix + target.Target.Id,
                        ScenarioAssetDisplayNameProjection.Resolve(target.Target.DisplayName, target.Target.Id, "Weather effect " + (i + 1).ToString()),
                        target.Source + " | Texture: " + ScenarioInspectorItemFactory.Safe(target.TextureName),
                        true,
                        active,
                        "FX",
                        target.Group,
                        active ? "SELECTED" : "EDIT",
                        target.PreviewSprite)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "weather_effect_sprites",
                Title = "Weather & Effects",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
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
                target != null ? target.Kind.ToString() : "Select a world target or grid cell to anchor placed art.",
                null,
                "AN",
                ResolvePreviewSprite(target),
                true));
            items.Add(ScenarioInspectorItemFactory.Property("Anchor", ScenarioAuthoringPresentationBuilder.FormatTarget(target)));
            items.Add(ScenarioInspectorItemFactory.Property("Grid", target != null && target.GridX.HasValue && target.GridY.HasValue ? (target.GridX.Value + "," + target.GridY.Value) : "No grid anchor selected"));
            items.Add(ScenarioInspectorItemFactory.Property("Active Placement", model.ActivePlacement != null ? "Selected authored placement" : "No authored placement selected"));
            items.Add(ScenarioInspectorItemFactory.Property("Active Sprite", !string.IsNullOrEmpty(model.ActiveCandidateLabel) ? ScenarioAssetDisplayNameProjection.Resolve(model.ActiveCandidateLabel, model.ActiveCandidateToken, "Selected sprite") : "Choose a sprite below to start placement"));
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
                    IsAdvanced = true,
                    Expanded = false,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        ScenarioInspectorItemFactory.Property("Stored As", ScenarioInspectorItemFactory.Safe(model.XmlPathHint)),
                        ScenarioInspectorItemFactory.Property("Technical placement id", model.ActivePlacement != null ? ScenarioInspectorItemFactory.Safe(model.ActivePlacement.Id) : "None"),
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

    internal static class ScenarioAssetDisplayNameProjection
    {
        public static string Resolve(string literalText, string storageId, string fallbackText)
        {
            string cleaned = ScenarioAuthoringPresentationBuilder.CleanCandidateLabel(literalText);
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(
                cleaned,
                null,
                storageId,
                fallbackText).Text;
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
