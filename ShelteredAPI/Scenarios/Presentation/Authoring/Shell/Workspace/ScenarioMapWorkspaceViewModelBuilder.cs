using System;
using System.Collections.Generic;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Presentation.Inspector;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
    /// <summary>
    /// Projects cached Map authoring state into the shared Navigator + Document model.
    /// This builder must only be called by the shell-revision composition path; it is
    /// intentionally data-only and performs no OnGUI, overlay, patch, or input work.
    /// </summary>
    internal sealed class ScenarioMapWorkspaceViewModelBuilder
    {
        private readonly ScenarioAuthoringWorkspaceViewModelFactory _factory;

        public ScenarioMapWorkspaceViewModelBuilder()
        {
            _factory = new ScenarioAuthoringWorkspaceViewModelFactory();
        }

        public ScenarioAuthoringWorkspaceViewModel Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioDefinition definition = context != null ? context.Definition : null;
            MapAuthoringDefinition map = definition != null && definition.Map != null
                ? definition.Map
                : new MapAuthoringDefinition();
            ScenarioAuthoringState authoringState = context != null ? context.State : null;
            ScenarioAuthoringRendererInteractionState rendererState = ScenarioAuthoringRendererInteractionState.Instance;
            string selected = ReconcileSelection(map, authoringState, rendererState);

            ScenarioAuthoringWorkspaceViewModel workspace = _factory.CreateWorkspace(
                ScenarioMapWorkspaceSelection.WorkspaceId,
                ScenarioAuthoringWorkspaceLayoutKind.NavigatorDocument,
                ScenarioMapWorkspaceSelection.MainSubtabId);
            workspace.Navigator = BuildNavigator(map, selected, rendererState);
            workspace.Document = BuildDocument(definition, map, authoringState, selected);
            return workspace;
        }

        private static string ReconcileSelection(
            MapAuthoringDefinition map,
            ScenarioAuthoringState authoringState,
            ScenarioAuthoringRendererInteractionState rendererState)
        {
            string selected = rendererState.GetWorkspaceSelection(
                ScenarioMapWorkspaceSelection.WorkspaceId,
                ScenarioMapWorkspaceSelection.MainSubtabId);
            int index;
            ScenarioMapWorkspaceEntityKind kind = ScenarioMapWorkspaceSelection.ResolveKind(map, selected, out index);
            if (kind != ScenarioMapWorkspaceEntityKind.None)
            {
                if (kind == ScenarioMapWorkspaceEntityKind.Location)
                {
                    MapLocationDefinition location = map.Locations[index];
                    if (authoringState != null && !string.Equals(authoringState.MapSelectedLocationId, location.Id, StringComparison.OrdinalIgnoreCase))
                        authoringState.MapSelectedLocationId = location.Id;
                }
                else if (authoringState != null)
                {
                    // Non-location documents do not claim an in-world authored marker.
                    authoringState.MapSelectedLocationId = null;
                }
                return selected;
            }

            if (!string.IsNullOrEmpty(selected))
                rendererState.SetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, null);

            string overlayEntity = ScenarioMapWorkspaceSelection.FindLocationEntityId(
                map,
                authoringState != null ? authoringState.MapSelectedLocationId : null);
            if (!string.IsNullOrEmpty(overlayEntity))
            {
                rendererState.SetWorkspaceSubtab(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId);
                rendererState.SetWorkspaceSelection(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, overlayEntity);
                rendererState.SetWorkspaceNarrowPane(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, true);
                return overlayEntity;
            }

            if (authoringState != null)
                authoringState.MapSelectedLocationId = null;
            return null;
        }

        private ScenarioAuthoringNavigatorViewModel BuildNavigator(
            MapAuthoringDefinition map,
            string selected,
            ScenarioAuthoringRendererInteractionState state)
        {
            ScenarioAuthoringNavigatorViewModel navigator = _factory.CreateNavigator("map.navigator");
            navigator.SearchControlId = "map.search";
            navigator.SearchText = state.GetWorkspaceSearch(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId);
            navigator.SearchPlaceholder = "Search map content";
            navigator.SelectedEntityId = selected;
            navigator.EmptyMessage = "No map content matches this search.";
            navigator.Groups = new[]
            {
                BuildLocationsGroup(map, selected, navigator.SearchText, state),
                BuildMarkersGroup(map, selected, navigator.SearchText, state),
                BuildLootGroup(map, selected, navigator.SearchText, state),
                BuildEncountersGroup(map, selected, navigator.SearchText, state)
            };
            return navigator;
        }

        private ScenarioAuthoringNavigatorGroupViewModel BuildLocationsGroup(
            MapAuthoringDefinition map,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; map.Locations != null && i < map.Locations.Count; i++)
            {
                MapLocationDefinition location = map.Locations[i];
                if (location == null) continue;
                string title = LocationName(location, i);
                if (!Matches(search, title, location.Kind)) continue;
                string entity = ScenarioMapWorkspaceSelection.LocationEntityId(map, i);
                List<ScenarioAuthoringStatusChipViewModel> chips = new List<ScenarioAuthoringStatusChipViewModel>();
                chips.Add(Chip(entity + ".visibility", location.VisibleAtStart ? "Visible" : "Hidden", location.VisibleAtStart ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Neutral));
                if (string.Equals(map.StartLocationId, location.Id, StringComparison.OrdinalIgnoreCase))
                    chips.Add(Chip(entity + ".start", "Start", ScenarioAuthoringStatusTone.Informational));
                if (!string.IsNullOrEmpty(location.LootTableId) && FindLoot(map, location.LootTableId) == null)
                    chips.Add(Chip(entity + ".loot", "Missing loot", ScenarioAuthoringStatusTone.Warning));
                if (!string.IsNullOrEmpty(location.EncounterTableId) && FindEncounter(map, location.EncounterTableId) == null)
                    chips.Add(Chip(entity + ".encounter", "Missing encounters", ScenarioAuthoringStatusTone.Warning));

                rows.Add(new ScenarioAuthoringNavigatorRowViewModel
                {
                    EntityId = entity,
                    Title = title,
                    Subtitle = Humanize(location.Kind, "Map location") + " | grid " + location.GridX.ToString(CultureInfo.InvariantCulture) + "," + location.GridY.ToString(CultureInfo.InvariantCulture),
                    IconText = "LO",
                    Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                    StatusChips = chips.ToArray(),
                    SelectAction = CreateLocationAction(map, location, entity, title),
                    Children = new ScenarioAuthoringNavigatorRowViewModel[0]
                });
            }
            return Group("locations", "Locations", "LO", rows, map.Locations != null ? map.Locations.Count : 0, state, null);
        }

        private ScenarioAuthoringNavigatorGroupViewModel BuildMarkersGroup(
            MapAuthoringDefinition map,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; map.Markers != null && i < map.Markers.Count; i++)
            {
                MapMarkerDefinition marker = map.Markers[i];
                if (marker == null) continue;
                string title = MarkerName(marker, i);
                if (!Matches(search, title, marker.Kind.ToString())) continue;
                string entity = ScenarioMapWorkspaceSelection.MarkerEntityId(map, i);
                rows.Add(Row(
                    entity,
                    title,
                    Humanize(marker.Kind.ToString(), "Map marker") + " | " + (marker.VisibleAtStart ? "visible" : "hidden"),
                    "MK",
                    selected,
                    new[] { Chip(entity + ".visibility", marker.VisibleAtStart ? "Visible" : "Hidden", marker.VisibleAtStart ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Neutral) }));
            }
            return Group("markers", "Markers", "MK", rows, map.Markers != null ? map.Markers.Count : 0, state, null);
        }

        private ScenarioAuthoringNavigatorGroupViewModel BuildLootGroup(
            MapAuthoringDefinition map,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; map.LootTables != null && i < map.LootTables.Count; i++)
            {
                MapLootTableDefinition table = map.LootTables[i];
                if (table == null) continue;
                string title = LootName(table, i);
                if (!Matches(search, title, null)) continue;
                string entity = ScenarioMapWorkspaceSelection.LootEntityId(map, i);
                int count = table.Entries != null ? table.Entries.Count : 0;
                rows.Add(Row(
                    entity,
                    title,
                    count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " loot entry" : " loot entries"),
                    "LT",
                    selected,
                    new[] { Chip(entity + ".entries", count > 0 ? "Ready" : "Empty", count > 0 ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning) }));
            }
            return Group("loot", "Loot", "LT", rows, map.LootTables != null ? map.LootTables.Count : 0, state, null);
        }

        private ScenarioAuthoringNavigatorGroupViewModel BuildEncountersGroup(
            MapAuthoringDefinition map,
            string selected,
            string search,
            ScenarioAuthoringRendererInteractionState state)
        {
            List<ScenarioAuthoringNavigatorRowViewModel> rows = new List<ScenarioAuthoringNavigatorRowViewModel>();
            for (int i = 0; map.EncounterTables != null && i < map.EncounterTables.Count; i++)
            {
                MapEncounterTableDefinition table = map.EncounterTables[i];
                if (table == null) continue;
                string title = EncounterName(table, i);
                if (!Matches(search, title, null)) continue;
                string entity = ScenarioMapWorkspaceSelection.EncounterEntityId(map, i);
                int count = table.Entries != null ? table.Entries.Count : 0;
                rows.Add(Row(
                    entity,
                    title,
                    count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " encounter" : " encounters"),
                    "EN",
                    selected,
                    new[] { Chip(entity + ".entries", count > 0 ? "Ready" : "Empty", count > 0 ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning) }));
            }
            return Group("encounters", "Encounters", "EN", rows, map.EncounterTables != null ? map.EncounterTables.Count : 0, state, null);
        }

        private ScenarioAuthoringNavigatorGroupViewModel Group(
            string id,
            string label,
            string icon,
            List<ScenarioAuthoringNavigatorRowViewModel> rows,
            int total,
            ScenarioAuthoringRendererInteractionState state,
            ScenarioAuthoringInspectorAction createAction)
        {
            return new ScenarioAuthoringNavigatorGroupViewModel
            {
                Id = id,
                Label = label,
                IconText = icon,
                Expanded = state.GetWorkspaceExpanded(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, id, true),
                StatusChips = new[] { Chip("map.group." + id, total.ToString(CultureInfo.InvariantCulture) + " " + label.ToLowerInvariant(), ScenarioAuthoringStatusTone.Informational) },
                ToggleAction = _factory.CreateGroupToggleAction(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, id, "Toggle " + label),
                CreateAction = createAction,
                Rows = rows.ToArray()
            };
        }

        private ScenarioAuthoringNavigatorRowViewModel Row(
            string entity,
            string title,
            string subtitle,
            string icon,
            string selected,
            ScenarioAuthoringStatusChipViewModel[] chips)
        {
            return new ScenarioAuthoringNavigatorRowViewModel
            {
                EntityId = entity,
                Title = title,
                Subtitle = subtitle,
                IconText = icon,
                Selected = string.Equals(selected, entity, StringComparison.Ordinal),
                StatusChips = chips,
                SelectAction = _factory.CreateEntityAction(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, entity, "Select " + title),
                Children = new ScenarioAuthoringNavigatorRowViewModel[0]
            };
        }

        private ScenarioAuthoringInspectorAction CreateLocationAction(
            MapAuthoringDefinition map,
            MapLocationDefinition location,
            string entity,
            string title)
        {
            // Unique authored IDs keep the established map semantic route, which also
            // drives the in-world overlay. Invalid duplicate/blank IDs retain an
            // index-stable document route so they can still be inspected and repaired.
            string uniqueEntity = ScenarioMapWorkspaceSelection.FindLocationEntityId(map, location.Id);
            if (!string.IsNullOrEmpty(location.Id) && string.Equals(uniqueEntity, entity, StringComparison.Ordinal))
            {
                return ScenarioInspectorItemFactory.Action(
                    ScenarioAuthoringActionCodec.BuildTokenActionId(ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix, location.Id),
                    "Select " + title,
                    "Select this authored location in the navigator and on the expedition map.",
                    true,
                    false,
                    "LO");
            }
            return _factory.CreateEntityAction(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, entity, "Select " + title);
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildDocument(
            ScenarioDefinition definition,
            MapAuthoringDefinition map,
            ScenarioAuthoringState state,
            string selected)
        {
            int index;
            if (ScenarioMapWorkspaceSelection.TryResolveLocation(map, selected, out index))
                return BuildLocationDocument(definition, map, state, map.Locations[index], index);
            if (ScenarioMapWorkspaceSelection.TryResolveMarker(map, selected, out index))
                return BuildMarkerDocument(map, map.Markers[index], index);
            if (ScenarioMapWorkspaceSelection.TryResolveLoot(map, selected, out index))
                return BuildLootDocument(map, map.LootTables[index], index);
            if (ScenarioMapWorkspaceSelection.TryResolveEncounter(map, selected, out index))
                return BuildEncounterDocument(map, map.EncounterTables[index], index);
            return BuildOverviewDocument(definition, map, state);
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildOverviewDocument(
            ScenarioDefinition definition,
            MapAuthoringDefinition map,
            ScenarioAuthoringState state)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = _factory.CreateDocument("map.overview", "Map");
            document.Subtitle = "Open the expedition map, place locations, and tune generation and terrain tools.";
            document.StatusChips = BuildOverviewChips(map, state);

            ScenarioAuthoringInspectorSection structure = new ScenarioAuthoringInspectorSection
            {
                Id = "map_structure",
                Title = "MAP STRUCTURE",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = new[]
                {
                    ScenarioInspectorItemFactory.Property("Start location", ResolveLocationReference(map, map.StartLocationId, "Not set")),
                    ScenarioInspectorItemFactory.Property("Map size", map.Width > 0f && map.Height > 0f
                        ? map.Width.ToString("0.##", CultureInfo.InvariantCulture) + " x " + map.Height.ToString("0.##", CultureInfo.InvariantCulture)
                        : "Use the generated map size"),
                    ScenarioInspectorItemFactory.Text("Select a navigator row for its document. Routes are shown on each connected location.")
                }
            };

            document.Sections = new[]
            {
                ScenarioMapAuthoringContentBuilder.BuildRuntimeNoticeSection(state),
                ScenarioMapAuthoringContentBuilder.BuildSupportedActionsSection(state),
                ScenarioMapAuthoringContentBuilder.BuildGenerationSection(definition),
                ScenarioMapAuthoringContentBuilder.BuildBrushOptionsSection(state),
                structure,
                BuildOverviewAdvancedSection(definition, map)
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildLocationDocument(
            ScenarioDefinition definition,
            MapAuthoringDefinition map,
            ScenarioAuthoringState state,
            MapLocationDefinition location,
            int index)
        {
            string title = LocationName(location, index);
            ScenarioAuthoringWorkspaceDocumentViewModel document = SelectedDocument("map.location." + index.ToString(CultureInfo.InvariantCulture), "Locations", title, "Authored expedition location");
            document.HeaderActions = new[] { OpenMapAction(state) };
            document.StatusChips = new[]
            {
                Chip("map.location.document.visibility", location.VisibleAtStart ? "Visible" : "Hidden", location.VisibleAtStart ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Neutral),
                Chip("map.location.document.search", location.Searchable ? "Searchable" : "Not searchable", location.Searchable ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning)
            };
            ScenarioAuthoringInspectorSection[] locationSections = ScenarioMapLocationEditorSectionBuilder.BuildWorkspaceDocumentSections(state, definition, map, location);
            document.Sections = new[]
            {
                locationSections[0],
                locationSections[1],
                ScenarioMapAuthoringContentBuilder.BuildRouteSection(map, location),
                locationSections[2]
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildMarkerDocument(MapAuthoringDefinition map, MapMarkerDefinition marker, int index)
        {
            string title = MarkerName(marker, index);
            ScenarioAuthoringWorkspaceDocumentViewModel document = SelectedDocument("map.marker." + index.ToString(CultureInfo.InvariantCulture), "Markers", title, "Expedition map marker");
            document.StatusChips = new[] { Chip("map.marker.document.visibility", marker.VisibleAtStart ? "Visible" : "Hidden", marker.VisibleAtStart ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Neutral) };
            document.Sections = new[]
            {
                ScenarioMapAuthoringContentBuilder.BuildMarkerSection(map, marker),
                Advanced("map_marker_advanced", new[]
                {
                    ScenarioInspectorItemFactory.Property("Storage ID", marker.Id ?? string.Empty),
                    ScenarioInspectorItemFactory.Property("Icon ID", marker.IconId ?? string.Empty),
                    ScenarioInspectorItemFactory.Property("Location ID", marker.LocationId ?? string.Empty),
                    ScenarioInspectorItemFactory.Property("Boundary ID", marker.BoundaryId ?? string.Empty)
                })
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildLootDocument(MapAuthoringDefinition map, MapLootTableDefinition table, int index)
        {
            string title = LootName(table, index);
            int count = table.Entries != null ? table.Entries.Count : 0;
            ScenarioAuthoringWorkspaceDocumentViewModel document = SelectedDocument("map.loot." + index.ToString(CultureInfo.InvariantCulture), "Loot Tables", title, "Expedition loot table");
            document.StatusChips = new[] { Chip("map.loot.document.entries", count > 0 ? "Ready" : "Empty", count > 0 ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning) };
            document.Sections = new[]
            {
                ScenarioMapAuthoringContentBuilder.BuildLootSection(map, table),
                BuildLootAdvancedSection(table)
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel BuildEncounterDocument(MapAuthoringDefinition map, MapEncounterTableDefinition table, int index)
        {
            string title = EncounterName(table, index);
            int count = table.Entries != null ? table.Entries.Count : 0;
            ScenarioAuthoringWorkspaceDocumentViewModel document = SelectedDocument("map.encounter." + index.ToString(CultureInfo.InvariantCulture), "Encounter Tables", title, "Expedition encounter table");
            document.StatusChips = new[] { Chip("map.encounter.document.entries", count > 0 ? "Ready" : "Empty", count > 0 ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Warning) };
            document.Sections = new[]
            {
                ScenarioMapAuthoringContentBuilder.BuildEncounterSection(map, table),
                BuildEncounterAdvancedSection(table)
            };
            return document;
        }

        private ScenarioAuthoringWorkspaceDocumentViewModel SelectedDocument(string id, string groupLabel, string title, string subtitle)
        {
            ScenarioAuthoringWorkspaceDocumentViewModel document = _factory.CreateDocument(id, title);
            document.Subtitle = subtitle;
            document.BackAction = _factory.CreateBackAction(ScenarioMapWorkspaceSelection.WorkspaceId, ScenarioMapWorkspaceSelection.MainSubtabId, "Back to Navigator");
            document.Breadcrumbs = new[]
            {
                new ScenarioAuthoringBreadcrumbViewModel { Label = "Map" },
                new ScenarioAuthoringBreadcrumbViewModel { Label = groupLabel },
                new ScenarioAuthoringBreadcrumbViewModel { Label = title }
            };
            return document;
        }

        private static ScenarioAuthoringInspectorSection BuildOverviewAdvancedSection(ScenarioDefinition definition, MapAuthoringDefinition map)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            AddItems(items, ScenarioMapAuthoringContentBuilder.BuildOverviewSection(definition, map));
            AddItems(items, ScenarioMapAuthoringContentBuilder.BuildBoundaryTerrainSection(map));
            AddItems(items, ScenarioMapAuthoringContentBuilder.BuildRouteSection(map));
            return Advanced("map_advanced", items.ToArray());
        }

        private static ScenarioAuthoringInspectorSection BuildLootAdvancedSection(MapLootTableDefinition table)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Storage ID", table.Id ?? string.Empty));
            for (int i = 0; table.Entries != null && i < table.Entries.Count; i++)
            {
                MapLootEntryDefinition entry = table.Entries[i];
                if (entry == null) continue;
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Entry " + (i + 1).ToString(CultureInfo.InvariantCulture) + " item ID",
                    (entry.ItemId ?? string.Empty) + "; quantity " + entry.MinQuantity.ToString(CultureInfo.InvariantCulture) + "-" + entry.MaxQuantity.ToString(CultureInfo.InvariantCulture)
                    + "; weight " + entry.Weight.ToString(CultureInfo.InvariantCulture) + (entry.Hidden ? "; hidden" : string.Empty)));
            }
            return Advanced("map_loot_advanced", items.ToArray());
        }

        private static ScenarioAuthoringInspectorSection BuildEncounterAdvancedSection(MapEncounterTableDefinition table)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ScenarioInspectorItemFactory.Property("Storage ID", table.Id ?? string.Empty));
            for (int i = 0; table.Entries != null && i < table.Entries.Count; i++)
            {
                MapEncounterEntryDefinition entry = table.Entries[i];
                if (entry == null) continue;
                items.Add(ScenarioInspectorItemFactory.Property(
                    "Entry " + (i + 1).ToString(CultureInfo.InvariantCulture),
                    "ID " + (entry.Id ?? string.Empty) + "; type " + (entry.EncounterType ?? string.Empty) + "; faction " + (entry.FactionId ?? string.Empty)
                    + "; count " + entry.MinCount.ToString(CultureInfo.InvariantCulture) + "-" + entry.MaxCount.ToString(CultureInfo.InvariantCulture)));
            }
            return Advanced("map_encounter_advanced", items.ToArray());
        }

        private static ScenarioAuthoringInspectorSection Advanced(string id, ScenarioAuthoringInspectorItem[] items)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = id,
                Title = "ADVANCED",
                Expanded = true,
                IsAdvanced = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items ?? new ScenarioAuthoringInspectorItem[0]
            };
        }

        private static void AddItems(List<ScenarioAuthoringInspectorItem> destination, ScenarioAuthoringInspectorSection section)
        {
            if (destination == null || section == null || section.Items == null) return;
            for (int i = 0; i < section.Items.Length; i++)
                if (section.Items[i] != null) destination.Add(section.Items[i]);
        }

        private static ScenarioAuthoringInspectorAction OpenMapAction(ScenarioAuthoringState state)
        {
            bool active = state != null && state.MapAuthoringActive;
            return ScenarioInspectorItemFactory.Action(
                active ? ScenarioAuthoringActionIds.ActionMapAuthoringClose : ScenarioAuthoringActionIds.ActionMapAuthoringOpen,
                active ? "Close Map" : "Open Map",
                active ? "Close the vanilla expedition map and return to the workspace." : "Open the vanilla expedition map without replacing the authoring overlay.",
                true,
                active,
                active ? "CL" : "MP");
        }

        private static ScenarioAuthoringStatusChipViewModel[] BuildOverviewChips(MapAuthoringDefinition map, ScenarioAuthoringState state)
        {
            return new[]
            {
                Chip("map.status.surface", state != null && state.MapAuthoringActive ? "Map open" : "Ready", state != null && state.MapAuthoringActive ? ScenarioAuthoringStatusTone.Ready : ScenarioAuthoringStatusTone.Informational),
                Chip("map.status.locations", (map.Locations != null ? map.Locations.Count : 0).ToString(CultureInfo.InvariantCulture) + " locations", ScenarioAuthoringStatusTone.Neutral),
                Chip("map.status.markers", (map.Markers != null ? map.Markers.Count : 0).ToString(CultureInfo.InvariantCulture) + " markers", ScenarioAuthoringStatusTone.Neutral)
            };
        }

        private static ScenarioAuthoringStatusChipViewModel Chip(string id, string text, ScenarioAuthoringStatusTone tone)
        {
            return new ScenarioAuthoringStatusChipViewModel { Id = id, Text = text, Tone = tone };
        }

        private static string LocationName(MapLocationDefinition value, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(value != null ? value.DisplayName : null, null, value != null ? value.Id : null, "Location " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string MarkerName(MapMarkerDefinition value, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(value != null ? value.DisplayName : null, null, value != null ? value.Id : null, "Marker " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string LootName(MapLootTableDefinition value, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(value != null ? value.DisplayName : null, null, value != null ? value.Id : null, "Loot Table " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string EncounterName(MapEncounterTableDefinition value, int index)
        {
            return ScenarioAuthoringDisplayNameResolver.ShellRebuild.Resolve(value != null ? value.DisplayName : null, null, value != null ? value.Id : null, "Encounter Table " + (index + 1).ToString(CultureInfo.InvariantCulture)).Text;
        }

        private static string ResolveLocationReference(MapAuthoringDefinition map, string id, string fallback)
        {
            for (int i = 0; !string.IsNullOrEmpty(id) && map.Locations != null && i < map.Locations.Count; i++)
                if (map.Locations[i] != null && string.Equals(map.Locations[i].Id, id, StringComparison.OrdinalIgnoreCase)) return LocationName(map.Locations[i], i);
            return fallback;
        }

        private static MapLootTableDefinition FindLoot(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; !string.IsNullOrEmpty(id) && map.LootTables != null && i < map.LootTables.Count; i++)
                if (map.LootTables[i] != null && string.Equals(map.LootTables[i].Id, id, StringComparison.OrdinalIgnoreCase)) return map.LootTables[i];
            return null;
        }

        private static MapEncounterTableDefinition FindEncounter(MapAuthoringDefinition map, string id)
        {
            for (int i = 0; !string.IsNullOrEmpty(id) && map.EncounterTables != null && i < map.EncounterTables.Count; i++)
                if (map.EncounterTables[i] != null && string.Equals(map.EncounterTables[i].Id, id, StringComparison.OrdinalIgnoreCase)) return map.EncounterTables[i];
            return null;
        }

        private static bool Matches(string search, string primary, string secondary)
        {
            return string.IsNullOrEmpty(search)
                || (!string.IsNullOrEmpty(primary) && primary.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!string.IsNullOrEmpty(secondary) && secondary.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string Humanize(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            List<char> text = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1])) text.Add(' ');
                text.Add(value[i]);
            }
            return new string(text.ToArray());
        }
    }
}
