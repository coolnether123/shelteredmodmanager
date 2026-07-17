using System;
using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>Executable contract fixture for the dark Slice 8 Map workspace.</summary>
    internal static class ScenarioMapWorkspaceVerification
    {
        public static string[] Run()
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            Verify(result);
            List<string> errors = new List<string>();
            ScenarioValidationIssue[] issues = result.Issues;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                if (issues[i] != null && issues[i].Severity == ScenarioIssueSeverity.Error)
                    errors.Add(issues[i].Message);
            }
            return errors.ToArray();
        }

        public static void Verify(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = BuildFixture();
            ScenarioAuthoringState state = new ScenarioAuthoringState();
            ScenarioAuthoringRendererInteractionState rendererState = ScenarioAuthoringRendererInteractionState.Instance;
            rendererState.SetWorkspaceSubtab(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId);
            rendererState.SetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, null);

            ScenarioMapWorkspaceViewModelBuilder builder = new ScenarioMapWorkspaceViewModelBuilder();
            ScenarioAuthoringWindowContentContext context = new ScenarioAuthoringWindowContentContext(null, null, null, definition);
            ScenarioAuthoringWorkspaceViewModel overview = builder.Build(context);
            Assert(overview != null && overview.LayoutKind == ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                "Map workspace must use NavigatorDocument.", result);
            AssertGroups(overview, result);
            Assert(overview != null && overview.Navigator != null && overview.Navigator.Groups != null
                    && overview.Navigator.Groups.Length > 0 && overview.Navigator.Groups[0].Rows != null
                    && overview.Navigator.Groups[0].Rows.Length > 0 && overview.Navigator.Groups[0].Rows[0].SelectAction != null
                    && overview.Navigator.Groups[0].Rows[0].SelectAction.Id.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix, StringComparison.Ordinal),
                "Map location navigator row must reuse the overlay-select semantic action.", result);
            Assert(overview != null && overview.Navigator != null && overview.Navigator.Groups != null
                    && overview.Navigator.Groups.Length > 0 && overview.Navigator.Groups[0].CreateAction != null
                    && string.Equals(overview.Navigator.Groups[0].CreateAction.Id, ScenarioAuthoringActionIds.ActionMapAuthoringModePlace, StringComparison.Ordinal),
                "Map Locations group must expose the existing semantic location-placement action.", result);
            ScenarioAuthoringState activeMapState = new ScenarioAuthoringState { MapAuthoringActive = true };
            ScenarioCommandDispatcher dispatcher = new ScenarioCommandDispatcher(new IScenarioCommandHandler[]
            {
                new ScenarioMapAuthoringCommandHandler(null, null, null, null)
            });
            ScenarioCommandDispatchResult placement = dispatcher.DispatchDetailed(activeMapState, overview.Navigator.Groups[0].CreateAction.Id);
            Assert(placement.Handled && placement.Result && string.Equals(activeMapState.MapAuthoringMode, "place", StringComparison.Ordinal),
                "Map Locations create action did not enter the existing semantic placement command path.", result);
            Assert(overview != null && overview.Document != null && IsAdvancedLast(overview.Document.Sections),
                "Map overview must finish with Advanced.", result);
            AssertUniqueActions(overview, "overview", result);

            MapLocationDefinition location = definition.Map.Locations[0];
            ScenarioMapWorkspaceSelection.SelectLocation(state, definition, location);
            string locationEntity = ScenarioMapWorkspaceSelection.LocationEntityId(definition.Map, 0);
            Assert(string.Equals(state.MapSelectedLocationId, location.Id, StringComparison.Ordinal)
                    && string.Equals(rendererState.GetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId), locationEntity, StringComparison.Ordinal),
                "Map location selection did not update both overlay and navigator state.", result);

            ScenarioAuthoringWindowContentContext statefulContext = new ScenarioAuthoringWindowContentContext(state, null, null, definition);
            ScenarioAuthoringWorkspaceViewModel locationWorkspace = builder.Build(statefulContext);
            Assert(locationWorkspace != null && locationWorkspace.Navigator != null
                    && string.Equals(locationWorkspace.Navigator.SelectedEntityId, locationEntity, StringComparison.Ordinal)
                    && locationWorkspace.Document != null && string.Equals(locationWorkspace.Document.Title, "Location 1", StringComparison.Ordinal),
                "Overlay-selected location did not resolve to its Map navigator document.", result);
            Assert(IsAdvancedLast(locationWorkspace != null && locationWorkspace.Document != null ? locationWorkspace.Document.Sections : null),
                "Selected Map location document must finish with Advanced.", result);
            Assert(PrimaryTextDoesNotContain(locationWorkspace != null && locationWorkspace.Document != null ? locationWorkspace.Document.Sections : null, location.Id),
                "Map location storage ID escaped into a primary document card.", result);
            Assert(AdvancedContains(locationWorkspace != null && locationWorkspace.Document != null ? locationWorkspace.Document.Sections : null, location.Id),
                "Map location storage ID was not retained in Advanced.", result);
            AssertUniqueActions(locationWorkspace, "location", result);

            string markerEntity = ScenarioMapWorkspaceSelection.MarkerEntityId(definition.Map, 0);
            rendererState.SetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, markerEntity);
            ScenarioAuthoringWorkspaceViewModel markerWorkspace = builder.Build(statefulContext);
            Assert(markerWorkspace != null && markerWorkspace.Document != null
                    && string.Equals(markerWorkspace.Document.Title, "Marker 1", StringComparison.Ordinal)
                    && state.MapSelectedLocationId == null,
                "Selecting a non-location Map document did not release the overlay location selection.", result);
            AssertUniqueActions(markerWorkspace, "marker", result);

            rendererState.SetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, null);
            state.MapSelectedLocationId = location.Id;
            ScenarioAuthoringWorkspaceViewModel overlayWorkspace = builder.Build(statefulContext);
            Assert(overlayWorkspace != null && overlayWorkspace.Navigator != null
                    && string.Equals(overlayWorkspace.Navigator.SelectedEntityId, locationEntity, StringComparison.Ordinal),
                "MapSelectedLocationId did not restore navigator selection.", result);

            ScenarioMapWorkspaceSelection.ClearLocationSelection(state);
            Assert(state.MapSelectedLocationId == null
                    && rendererState.GetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId) == null,
                "Clearing an in-world Map selection did not clear its navigator route.", result);

            VerifyLocationCreationCommandPath(result);
        }

        private static void VerifyLocationCreationCommandPath(ScenarioValidationResult result)
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            ScenarioEditorSession session = new ScenarioEditorSession
            {
                WorkingDefinition = definition,
                OriginalDefinition = new ScenarioDefinition()
            };
            ScenarioEditorSessionStore store = new ScenarioEditorSessionStore();
            store.Set(session, null);
            ScenarioEditorController editor = new ScenarioEditorController(
                store,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            FixtureMapRuntime runtime = new FixtureMapRuntime();
            ScenarioMapDraftService drafts = new ScenarioMapDraftService();
            ScenarioAuthoringState state = new ScenarioAuthoringState { MapAuthoringActive = true };
            ScenarioAuthoringRendererInteractionState interaction = ScenarioAuthoringRendererInteractionState.Instance;
            interaction.SetWorkspaceSubtab(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId);
            interaction.SetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, null);

            ScenarioMapWorkspaceViewModelBuilder builder = new ScenarioMapWorkspaceViewModelBuilder();
            ScenarioAuthoringWindowContentContext context = new ScenarioAuthoringWindowContentContext(state, session, null, definition);
            ScenarioAuthoringWorkspaceViewModel before = builder.Build(context);
            ScenarioAuthoringInspectorAction createAction = before != null && before.Navigator != null
                && before.Navigator.Groups != null && before.Navigator.Groups.Length > 0
                ? before.Navigator.Groups[0].CreateAction
                : null;
            ScenarioCommandDispatcher dispatcher = new ScenarioCommandDispatcher(new IScenarioCommandHandler[]
            {
                new ScenarioMapAuthoringCommandHandler(runtime, drafts, null, editor)
            });

            ScenarioCommandDispatchResult arm = dispatcher.DispatchDetailed(state, createAction != null ? createAction.Id : null);
            string worldClickAction = ScenarioAuthoringActionCodec.BuildTokenActionId(
                ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix,
                "125.5,240.25");
            ScenarioCommandDispatchResult commit = dispatcher.DispatchDetailed(state, worldClickAction);

            Assert(arm.Handled && arm.Result && commit.Handled && commit.Result,
                "Map location semantic create and world-click commit actions did not both return result:true.", result);
            Assert(definition.Map != null && definition.Map.Locations != null && definition.Map.Locations.Count == 1,
                "Map location command path did not persist exactly one MapLocationDefinition.", result);
            MapLocationDefinition created = definition.Map != null && definition.Map.Locations != null && definition.Map.Locations.Count == 1
                ? definition.Map.Locations[0]
                : null;
            Assert(created != null && created.GridX == FixtureMapRuntime.GridX && created.GridY == FixtureMapRuntime.GridY
                    && session.DirtyFlags.Contains(ScenarioDirtySection.Map),
                "Map location command path did not mutate and dirty the active working definition.", result);

            ScenarioAuthoringWorkspaceViewModel after = builder.Build(context);
            string createdEntity = created != null ? ScenarioMapWorkspaceSelection.FindLocationEntityId(definition.Map, created.Id) : null;
            Assert(after != null && after.Navigator != null && after.Navigator.Groups != null
                    && after.Navigator.Groups.Length > 0 && after.Navigator.Groups[0].Rows != null
                    && after.Navigator.Groups[0].Rows.Length == 1 && after.Navigator.Groups[0].Rows[0].Selected,
                "Persisted map location did not appear as the selected Locations navigator row.", result);
            Assert(created != null && string.Equals(state.MapSelectedLocationId, created.Id, StringComparison.Ordinal)
                    && after != null && after.Navigator != null
                    && string.Equals(after.Navigator.SelectedEntityId, createdEntity, StringComparison.Ordinal)
                    && after.Document != null && after.Document.Id != null
                    && after.Document.Id.StartsWith("map.location.", StringComparison.Ordinal),
                "Map location creation did not land selection on the authored navigator document.", result);

            ScenarioMapWorkspaceSelection.ClearLocationSelection(state);
        }

        private static ScenarioDefinition BuildFixture()
        {
            ScenarioDefinition definition = new ScenarioDefinition();
            MapLocationDefinition location = new MapLocationDefinition
            {
                Id = "map.location.storage_1",
                DisplayName = "map.location.name",
                Kind = "Town",
                GridX = 4,
                GridY = 7,
                VisibleAtStart = true,
                Searchable = true,
                LootTableId = "map.loot.storage_1",
                EncounterTableId = "map.encounter.storage_1"
            };
            MapMarkerDefinition marker = new MapMarkerDefinition
            {
                Id = "map.marker.storage_1",
                DisplayName = "map.marker.name",
                LocationId = location.Id,
                VisibleAtStart = true
            };
            MapLootTableDefinition loot = new MapLootTableDefinition { Id = location.LootTableId, DisplayName = "map.loot.name" };
            loot.Entries.Add(new MapLootEntryDefinition { ItemId = "fixture_item" });
            MapEncounterTableDefinition encounters = new MapEncounterTableDefinition { Id = location.EncounterTableId, DisplayName = "map.encounter.name" };
            encounters.Entries.Add(new MapEncounterEntryDefinition { Id = "encounter_1", EncounterType = "Bandit" });
            definition.Map.Locations.Add(location);
            definition.Map.Markers.Add(marker);
            definition.Map.LootTables.Add(loot);
            definition.Map.EncounterTables.Add(encounters);
            definition.Map.Routes.Add(new ExpeditionRouteDefinition { Id = "route_1", FromLocationId = location.Id, ToLocationId = location.Id });
            definition.Map.StartLocationId = location.Id;
            return definition;
        }

        private static void AssertGroups(ScenarioAuthoringWorkspaceViewModel workspace, ScenarioValidationResult result)
        {
            string[] expected = { "Locations", "Markers", "Loot", "Encounters" };
            Assert(workspace != null && workspace.Navigator != null && workspace.Navigator.Groups != null && workspace.Navigator.Groups.Length == expected.Length,
                "Map navigator must expose exactly Locations, Markers, Loot, and Encounters.", result);
            for (int i = 0; workspace != null && workspace.Navigator != null && workspace.Navigator.Groups != null && i < expected.Length && i < workspace.Navigator.Groups.Length; i++)
                Assert(workspace.Navigator.Groups[i] != null && string.Equals(workspace.Navigator.Groups[i].Label, expected[i], StringComparison.Ordinal),
                    "Map navigator group order changed at " + expected[i] + ".", result);
        }

        private static bool IsAdvancedLast(ScenarioAuthoringInspectorSection[] sections)
        {
            if (sections == null || sections.Length == 0 || sections[sections.Length - 1] == null || !sections[sections.Length - 1].IsAdvanced)
                return false;
            for (int i = 0; i < sections.Length - 1; i++)
                if (sections[i] != null && sections[i].IsAdvanced) return false;
            return true;
        }

        private static bool PrimaryTextDoesNotContain(ScenarioAuthoringInspectorSection[] sections, string value)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null || section.IsAdvanced) continue;
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null && Contains(item.Value, value)) return false;
                }
            }
            return true;
        }

        private static bool AdvancedContains(ScenarioAuthoringInspectorSection[] sections, string value)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                if (section == null || !section.IsAdvanced) continue;
                for (int j = 0; section.Items != null && j < section.Items.Length; j++)
                    if (section.Items[j] != null && Contains(section.Items[j].Value, value)) return true;
            }
            return false;
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(value)
                && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AssertUniqueActions(
            ScenarioAuthoringWorkspaceViewModel workspace,
            string documentName,
            ScenarioValidationResult result)
        {
            try
            {
                ScenarioAuthoringRendererActionManifest.BuildContractWindow(new ScenarioAuthoringShellViewModel
                {
                    Windows = new[]
                    {
                        new ScenarioAuthoringShellWindowViewModel
                        {
                            Id = "map.verification",
                            WorkspaceBody = workspace,
                            Sections = new ScenarioAuthoringInspectorSection[0]
                        }
                    }
                });
            }
            catch (InvalidOperationException ex)
            {
                Assert(false, "Map " + documentName + " emitted duplicate semantic actions: " + ex.Message, result);
            }
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition && result != null) result.AddError(message);
        }

        private sealed class FixtureMapRuntime : IScenarioMapAuthoringRuntime
        {
            public const int GridX = 5;
            public const int GridY = 8;

            public bool OpenVanillaMap() { return true; }
            public bool CloseVanillaMap() { return true; }
            public void CleanupMarkers() { }

            public bool TryCreateSelectionFromWorldPosition(
                float worldX,
                float worldY,
                ScenarioEditorSession session,
                string source,
                out ScenarioMapRegionSelection selection)
            {
                selection = null;
                return false;
            }

            public bool TryResolveGrid(
                float worldX,
                float worldY,
                out int gridX,
                out int gridY,
                out float centreWorldX,
                out float centreWorldY)
            {
                gridX = GridX;
                gridY = GridY;
                centreWorldX = worldX;
                centreWorldY = worldY;
                return true;
            }

            public bool CanAuthorLocationAtGrid(int gridX, int gridY, out string reason)
            {
                reason = null;
                return gridX == GridX && gridY == GridY;
            }

            public bool CanPaintTerrainAtGrid(int gridX, int gridY, string terrainId, out string reason)
            {
                reason = null;
                return true;
            }

            public bool PreviewTerrainDraft(ScenarioEditorSession session, out string reason)
            {
                reason = null;
                return true;
            }

            public void RefreshMarkers(ScenarioAuthoringState state, ScenarioEditorSession session) { }
        }
    }
}
