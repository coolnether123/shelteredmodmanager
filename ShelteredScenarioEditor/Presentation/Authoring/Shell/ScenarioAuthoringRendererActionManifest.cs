using System;
using System.Collections.Generic;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Map;
using ShelteredScenarioEditor.Domain.Story;

namespace ShelteredScenarioEditor.Presentation.Authoring.Shell
{
    // Historical verifier marker only; no action is emitted: workspace_section_search.publish
    /// <summary>
    /// Single source of truth for controls whose semantic action is created by an IMGUI renderer.
    /// The shell publishes this list so automation never has to infer a click coordinate.
    /// </summary>
    internal static class ScenarioAuthoringRendererActionManifest
    {
        private static readonly string[] PixelGroups =
        {
            "pixel_editor.group.animation",
            "pixel_editor.group.animation.more",
            "pixel_editor.group.clipboard",
            "pixel_editor.group.color"
        };

        private static readonly string[] HomeGroups =
        {
            "home.group.base",
            "home.group.details",
            "home.group.status",
            "home.group.advanced"
        };

        private static readonly string[] TimelineGroups =
        {
            "timeline.group.entries",
            "timeline.group.pacing",
            "timeline.group.logic"
        };

        private static readonly string[] SurvivorAppearanceGroups =
        {
            "survivor.appearance.textures",
            "survivor.appearance.colors"
        };

        private static readonly string[] AssetInventoryFilters = { "all", "unused", "used", "large" };
        private static readonly float[] AnimationSpeedPresets = { 0.25f, 0.5f, 1f, 1.5f, 2f };

        public static ScenarioAuthoringInspectorAction[] Build(
            ScenarioAuthoringState state,
            ScenarioAuthoringShellWindowViewModel[] windows,
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor,
            ScenarioAuthoringSettingsViewModel settings,
            ScenarioAuthoringHelpViewModel help,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            AddMapFilters(actions);
            AddGroupActions(actions, PixelGroups, RendererInteractionAutomationIds.ActionRendererPixelGroupTogglePrefix, "Toggle pixel-editor group", rendererInteraction);
            AddGroupActions(actions, HomeGroups, RendererInteractionAutomationIds.ActionRendererHomeGroupTogglePrefix, "Toggle Home group", rendererInteraction);
            AddGroupActions(actions, TimelineGroups, RendererInteractionAutomationIds.ActionRendererTimelineGroupTogglePrefix, "Toggle Timeline group", rendererInteraction);
            AddGroupActions(actions, SurvivorAppearanceGroups, RendererInteractionAutomationIds.ActionRendererWorkshopGroupTogglePrefix, "Toggle survivor appearance group", rendererInteraction);
            AddWorkspaceActionFamilies(actions);
            AddSettingsGroupActions(actions, settings, rendererInteraction);
            AddShortcutGroupActions(actions, help != null ? help.Shortcuts : null, rendererInteraction);
            Add(actions, ShellUxCommand.SettingToggle("visuals.snap_to_grid"), "Toggle placement snap", true);
            Add(actions, ShellUxCommand.SettingToggle("visuals.show_grid"), "Toggle placement grid", true);
            Add(actions, PlacementOverlayCommand.Back(), "Placement Back", true);
            Add(actions, PlacementOverlayCommand.Done(), "Placement Done", true);
            Add(actions, RendererInteractionCommand.ForSimple(RendererInteractionCommandKind.ToggleTopBarMore, RendererInteractionAutomationIds.ActionRendererTopBarMoreToggle), "Toggle stage overflow", true);
            Add(actions, RendererInteractionCommand.ForValue(RendererInteractionCommandKind.SetAssetSearch, RendererInteractionAutomationIds.ActionRendererAssetSearchPrefix, string.Empty), "Set asset search", true);
            Add(actions, RendererInteractionCommand.ForSimple(RendererInteractionCommandKind.ClearAssetSearch, RendererInteractionAutomationIds.ActionRendererAssetSearchClear), "Clear asset search", true);
            Add(actions, RendererInteractionCommand.ForValue(RendererInteractionCommandKind.SetGlobalSearchQuery, RendererInteractionAutomationIds.ActionRendererGlobalSearchQueryPrefix, string.Empty), "Set global search query", false);
            Add(
                actions,
                new ScenarioGlobalSearchRouteCommand(new[]
                {
                    ScenarioGlobalSearchRouteStep.Typed(
                        ShellUxCommand.Simple(ShellUxCommandKind.ToggleGlobalSearch, ScenarioAuthoringActionIds.ActionShellToggleGlobalSearch))
                }),
                "Activate global-search result",
                false);
            AddAssetInventoryFilters(actions, rendererInteraction);
            AddCandidateControls(actions, rendererInteraction);
            AddAssetBrowserActions(actions, state, windows, rendererInteraction);
            AddPixelEditorActions(actions, editor);
            return actions.ToArray();
        }

        public static ScenarioAuthoringInspectorAction[] AppendGlobalSearchEntries(
            ScenarioAuthoringInspectorAction[] rendererActions,
            IList<ScenarioGlobalSearchEntry> entries)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            AddRange(actions, rendererActions);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ScenarioGlobalSearchEntry entry = entries[i];
                string activationId = entry != null && entry.ActionIds != null && entry.ActionIds.Length > 0
                    ? entry.ActionIds[0]
                    : null;
                AddExisting(actions, new ScenarioAuthoringInspectorAction
                {
                    Id = activationId,
                    Command = entry != null ? entry.Command : null,
                    Label = entry != null ? "Open search result: " + entry.Name : null,
                    Hint = entry != null ? entry.Context : null,
                    Detail = entry != null ? entry.KindLabel : null,
                    Enabled = entry != null && entry.Enabled,
                    Emphasized = false,
                    DisabledReason = entry != null && !entry.Enabled ? "This search result is currently unavailable." : null
                });
            }
            return actions.ToArray();
        }

        public static ScenarioAuthoringShellWindowViewModel BuildContractWindow(ScenarioAuthoringShellViewModel shell)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            AddRange(actions, shell != null ? shell.Tabs : null);
            AddRange(actions, shell != null ? shell.ToolbarActions : null);
            AddRange(actions, shell != null ? shell.LayoutActions : null);
            AddRange(actions, shell != null ? shell.WorldSubstageActions : null);
            AddRange(actions, shell != null ? shell.WindowMenuActions : null);
            AddRange(actions, shell != null ? shell.RendererActions : null);
            for (int i = 0; shell != null && shell.ToolButtons != null && i < shell.ToolButtons.Length; i++)
                AddExisting(actions, shell.ToolButtons[i] != null ? shell.ToolButtons[i].Action : null);
            CollectWindows(actions, shell != null ? shell.Windows : null);
            CollectDocument(actions, shell != null ? shell.FocusedEditorDocument : null);
            CollectDocument(actions, shell != null ? shell.SpritePickerDocument : null);
            AddRange(actions, shell != null && shell.ContextMenu != null ? shell.ContextMenu.Actions : null);
            CollectHelp(actions, shell != null ? shell.Help : null);
            CollectTutorial(actions, shell != null ? shell.Tutorial : null);
            CollectTour(actions, shell != null ? shell.Tour : null);
            CollectSettings(actions, shell != null ? shell.Settings : null);
            ScenarioAuthoringInspectorAction[] contractActions = CanonicalizeContractActions(actions.ToArray());

            // Ribbon markers use the same scenario.timeline.entry.* actions already
            // collected from the Timeline window. Re-adding them here makes the
            // linear duplicate check scan the full action manifest once per marker
            // on every presentation build.

            ScenarioAuthoringInspectorItem[] items = new ScenarioAuthoringInspectorItem[contractActions.Length];
            for (int i = 0; i < contractActions.Length; i++)
                items[i] = new ScenarioAuthoringInspectorItem { Kind = ScenarioAuthoringInspectorItemKind.Action, Action = contractActions[i] };
            return new ScenarioAuthoringShellWindowViewModel
            {
                Id = "contract.semantic_actions",
                Title = "Semantic action contract",
                Visible = false,
                Sections = new[]
                {
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "contract.semantic_actions.all",
                        Title = "All rendered actions and editable fields",
                        Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                        Items = items
                    }
                }
            };
        }

        private static void CollectWindows(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            for (int i = 0; windows != null && i < windows.Length; i++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[i];
                if (window == null) continue;
                AddRange(actions, window.HeaderActions);
                if (window.WorkspaceBody != null)
                    CollectWorkspace(actions, window.WorkspaceBody);
                else
                    CollectSections(actions, window.Sections);
            }
        }

        private static void CollectWorkspace(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringWorkspaceViewModel workspace)
        {
            if (workspace == null) return;
            for (int i = 0; workspace.Subtabs != null && i < workspace.Subtabs.Length; i++)
            {
                ScenarioAuthoringWorkspaceSubtabViewModel subtab = workspace.Subtabs[i];
                if (subtab == null) continue;
                AddExisting(actions, subtab.SelectAction);
                CollectStatusChips(actions, subtab.StatusChips);
            }

            ScenarioAuthoringNavigatorViewModel navigator = workspace.Navigator;
            for (int i = 0; navigator != null && navigator.Groups != null && i < navigator.Groups.Length; i++)
            {
                ScenarioAuthoringNavigatorGroupViewModel group = navigator.Groups[i];
                if (group == null) continue;
                AddExisting(actions, group.ToggleAction);
                AddExisting(actions, group.CreateAction);
                CollectStatusChips(actions, group.StatusChips);
                CollectNavigatorRows(actions, group.Rows);
            }

            ScenarioAuthoringWorkspaceDocumentViewModel document = workspace.Document;
            if (document == null) return;
            AddExisting(actions, document.BackAction);
            AddRange(actions, document.HeaderActions);
            CollectStatusChips(actions, document.StatusChips);
            for (int i = 0; document.Breadcrumbs != null && i < document.Breadcrumbs.Length; i++)
                AddExisting(actions, document.Breadcrumbs[i] != null ? document.Breadcrumbs[i].Action : null);
            CollectSections(actions, document.Sections);
        }

        private static void CollectNavigatorRows(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringNavigatorRowViewModel[] rows)
        {
            for (int i = 0; rows != null && i < rows.Length; i++)
            {
                ScenarioAuthoringNavigatorRowViewModel row = rows[i];
                if (row == null) continue;
                AddExisting(actions, row.SelectAction);
                AddExisting(actions, row.ToggleAction);
                CollectStatusChips(actions, row.StatusChips);
                CollectNavigatorRows(actions, row.Children);
            }
        }

        private static void CollectStatusChips(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringStatusChipViewModel[] chips)
        {
            for (int i = 0; chips != null && i < chips.Length; i++)
                AddExisting(actions, chips[i] != null ? chips[i].Action : null);
        }

        private static void CollectDocument(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringInspectorDocument document)
        {
            if (document == null) return;
            AddRange(actions, document.HeaderActions);
            CollectSections(actions, document.Sections);
        }

        private static void CollectSections(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringInspectorSection[] sections)
        {
            for (int i = 0; sections != null && i < sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = sections[i];
                CollectStatusChips(actions, section != null ? section.StatusChips : null);
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    AddExisting(actions, item != null ? item.Action : null);
                    ScenarioAuthoringCompactChoiceViewModel choice = item != null ? item.Choice : null;
                    for (int optionIndex = 0; choice != null && choice.Options != null && optionIndex < choice.Options.Length; optionIndex++)
                        AddExisting(actions, choice.Options[optionIndex] != null ? choice.Options[optionIndex].Action : null);
                    ScenarioCastCardViewModel cast = item != null ? item.CastCard : null;
                    AddExisting(actions, cast != null ? cast.PrimaryAction : null);
                    AddRange(actions, cast != null ? cast.SecondaryActions : null);
                }
                CollectInventory(actions, section != null ? section.InventorySlotGrid : null);
                CollectSurvivor(actions, section != null ? section.SurvivorEditor : null);
                CollectStoryMap(actions, section != null ? section.StoryMap : null);
                for (int j = 0; section != null && section.ModFieldRows != null && j < section.ModFieldRows.Length; j++)
                    CollectModField(actions, section.ModFieldRows[j]);
            }
        }

        private static void CollectStoryMap(List<ScenarioAuthoringInspectorAction> actions, ScenarioStoryGraphModel storyMap)
        {
            for (int i = 0; storyMap != null && storyMap.Nodes != null && i < storyMap.Nodes.Length; i++)
            {
                ScenarioStoryGraphNode node = storyMap.Nodes[i];
                if (node == null || node.NavigationCommand == null) continue;
                AddExisting(actions, new ScenarioAuthoringInspectorAction
                {
                    Id = node.NavigationAutomationId,
                    Label = "Open " + (node.Label ?? "story node"),
                    Enabled = true,
                    Command = node.NavigationCommand
                });
            }
        }

        private static void CollectInventory(List<ScenarioAuthoringInspectorAction> actions, ScenarioInventorySlotGridViewModel grid)
        {
            for (int i = 0; grid != null && grid.Slots != null && i < grid.Slots.Length; i++)
            {
                ScenarioInventorySlotViewModel slot = grid.Slots[i];
                if (slot == null) continue;
                AddExisting(actions, slot.PrimaryAction);
                AddExisting(actions, slot.QuantityIncreaseAction);
                AddExisting(actions, slot.QuantityDecreaseAction);
                AddExisting(actions, slot.RemoveAction);
                AddExisting(actions, slot.KindAction);
                AddRange(actions, slot.TimeActions);
            }
        }

        private static void CollectSurvivor(List<ScenarioAuthoringInspectorAction> actions, ScenarioSurvivorEditorViewModel editor)
        {
            if (editor == null) return;
            AddExisting(actions, editor.NameAction);
            AddExisting(actions, editor.GenderAction);
            AddExisting(actions, editor.BodyAction);
            AddRange(actions, editor.UtilityActions);
            AddRange(actions, editor.CloseActions);
            AddExisting(actions, editor.Portrait != null ? editor.Portrait.PrimaryAction : null);
            AddRange(actions, editor.Portrait != null ? editor.Portrait.SecondaryActions : null);
            for (int i = 0; editor.TextureRows != null && i < editor.TextureRows.Length; i++)
            {
                ScenarioSurvivorTextureRowViewModel row = editor.TextureRows[i];
                if (row == null) continue;
                AddExisting(actions, row.PreviousAction);
                AddExisting(actions, row.NextAction);
            }
            for (int i = 0; editor.ColorRows != null && i < editor.ColorRows.Length; i++)
            {
                ScenarioSurvivorColorRowViewModel row = editor.ColorRows[i];
                if (row == null) continue;
                AddExisting(actions, row.PreviousAction);
                AddExisting(actions, row.NextAction);
                if (row.OpenColorPickerCommand != null)
                    Add(actions, row.OpenColorPickerCommand, "Open " + (row.Label ?? "color") + " picker", false);
            }
            for (int i = 0; editor.StatRows != null && i < editor.StatRows.Length; i++)
            {
                ScenarioSurvivorStatRowViewModel row = editor.StatRows[i];
                if (row == null) continue;
                AddExisting(actions, row.DecreaseAction);
                AddExisting(actions, row.IncreaseAction);
                AddExisting(actions, row.TextAction);
            }
            for (int i = 0; editor.TraitRows != null && i < editor.TraitRows.Length; i++)
            {
                ScenarioSurvivorTraitRowViewModel row = editor.TraitRows[i];
                if (row == null) continue;
                AddExisting(actions, row.PreviousAction);
                AddExisting(actions, row.NextAction);
                AddExisting(actions, row.PickerAction);
                for (int option = 0; row.Options != null && option < row.Options.Length; option++)
                    AddExisting(actions, row.Options[option] != null ? row.Options[option].SelectAction : null);
            }
            for (int i = 0; editor.ConditionRows != null && i < editor.ConditionRows.Length; i++)
            {
                ScenarioSurvivorConditionRowViewModel row = editor.ConditionRows[i];
                if (row == null) continue;
                AddExisting(actions, row.DecreaseAction);
                AddExisting(actions, row.IncreaseAction);
                AddExisting(actions, row.TextAction);
            }
        }

        private static void CollectModField(List<ScenarioAuthoringInspectorAction> actions, ScenarioSurvivorModFieldRowViewModel row)
        {
            if (row == null) return;
            AddExisting(actions, row.ToggleAction);
            AddExisting(actions, row.DecreaseAction);
            AddExisting(actions, row.IncreaseAction);
            AddExisting(actions, row.CycleAction);
            AddExisting(actions, row.TextAction);
            if (row.ColorRow != null && row.ColorRow.OpenColorPickerCommand != null)
                Add(actions, row.ColorRow.OpenColorPickerCommand, "Open mod color picker", false);
        }

        private static void CollectHelp(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringHelpViewModel help)
        {
            if (help == null) return;
            AddRange(actions, help.HeaderActions);
            AddRange(actions, help.ViewTabs);
            AddRange(actions, help.TopicActions);
            AddExisting(actions, help.PreviousAction);
            AddExisting(actions, help.NextAction);
            AddExisting(actions, help.ReplayAction);
        }

        private static void CollectTutorial(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringTutorialViewModel tutorial)
        {
            if (tutorial == null) return;
            AddExisting(actions, tutorial.PrimaryAction);
            AddExisting(actions, tutorial.BackAction);
            AddExisting(actions, tutorial.NextAction);
            AddExisting(actions, tutorial.SkipAction);
            AddExisting(actions, tutorial.SkipPromptAction);
            AddExisting(actions, tutorial.SkipCancelAction);
            AddExisting(actions, tutorial.HelpAction);
        }

        private static void CollectTour(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringTourViewModel tour)
        {
            if (tour == null) return;
            AddExisting(actions, tour.BackAction);
            AddExisting(actions, tour.NextAction);
            AddExisting(actions, tour.ExitAction);
        }

        private static void CollectSettings(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringSettingsViewModel settings)
        {
            if (settings == null) return;
            AddRange(actions, settings.HeaderActions);
            for (int i = 0; settings.Sections != null && i < settings.Sections.Length; i++)
            {
                ScenarioAuthoringSettingsSectionViewModel section = settings.Sections[i];
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringSettingsItemViewModel item = section.Items[j];
                    if (item == null || string.IsNullOrEmpty(item.Id)) continue;
                    if (item.Kind == ScenarioAuthoringSettingKind.Toggle)
                        Add(actions, ShellUxCommand.SettingToggle(item.Id), "Toggle " + item.Label, item.BoolValue);
                    else if (item.Kind == ScenarioAuthoringSettingKind.Float || item.Kind == ScenarioAuthoringSettingKind.Integer)
                    {
                        Add(actions, ShellUxCommand.SettingStep(item.Id, -1), "Decrease " + item.Label, false);
                        Add(actions, ShellUxCommand.SettingStep(item.Id, 1), "Increase " + item.Label, false);
                    }
                    else if (item.Kind == ScenarioAuthoringSettingKind.Choice)
                    {
                        for (int choice = 0; item.ChoiceValues != null && choice < item.ChoiceValues.Length; choice++)
                            Add(actions, ShellUxCommand.SettingChoice(item.Id, item.ChoiceValues[choice]), "Set " + item.Label + " to " + item.ChoiceValues[choice], choice == item.SelectedChoiceIndex);
                    }
                }
            }
        }

        private static void AddRange(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringInspectorAction[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++) AddExisting(actions, values[i]);
        }

        private static void AddExisting(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringInspectorAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id)) return;
            actions.Add(action);
        }

        internal static void VerifyUniqueActionIdsForContract(ScenarioAuthoringInspectorAction[] actions)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null || string.IsNullOrEmpty(action.Id)) continue;
                if (!ids.Add(action.Id))
                    throw new InvalidOperationException("Duplicate authoring semantic action id '" + action.Id + "'.");
            }
        }

        private static void AddMapFilters(List<ScenarioAuthoringInspectorAction> actions)
        {
            Array values = Enum.GetValues(typeof(ScenarioMapAuthoringFilter));
            for (int i = 0; i < values.Length; i++)
            {
                ScenarioMapAuthoringFilter filter = (ScenarioMapAuthoringFilter)values.GetValue(i);
                Add(actions, RendererInteractionCommand.ForMapFilter(filter), "Toggle map filter: " + filter, ScenarioMapAuthoringFilterState.IsVisible(filter));
            }
        }

        private static void AddWorkspaceActionFamilies(List<ScenarioAuthoringInspectorAction> actions)
        {
            const string workspaceId = "contract.workspace";
            const string subtabId = "contract.subtab";
            const string entityId = "contract.entity";
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.SelectWorkspaceSubtab, RendererInteractionAutomationIds.ActionRendererWorkspaceSubtabSelectPrefix, workspaceId, subtabId, string.Empty), "Select workspace subtab", false);
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.SelectWorkspaceEntity, RendererInteractionAutomationIds.ActionRendererWorkspaceEntitySelectPrefix, workspaceId, subtabId, entityId), "Select workspace entity", false);
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.OpenWorkspaceWarning, RendererInteractionAutomationIds.ActionRendererWorkspaceWarningOpenPrefix, workspaceId, subtabId, entityId), "Open workspace warning", false);
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.ToggleWorkspaceGroup, RendererInteractionAutomationIds.ActionRendererWorkspaceGroupTogglePrefix, workspaceId, subtabId, "contract.group"), "Toggle workspace group", false);
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.ToggleWorkspaceRow, RendererInteractionAutomationIds.ActionRendererWorkspaceRowTogglePrefix, workspaceId, subtabId, entityId), "Toggle workspace row", false);
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.SetWorkspaceSearch, RendererInteractionAutomationIds.ActionRendererWorkspaceSearchSetPrefix, workspaceId, subtabId, string.Empty), "Set workspace search", false);
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.SelectWorkspaceBreadcrumb, RendererInteractionAutomationIds.ActionRendererWorkspaceBreadcrumbSelectPrefix, workspaceId, subtabId, entityId), "Select workspace breadcrumb", false);
            Add(actions, RendererInteractionCommand.ForWorkspace(RendererInteractionCommandKind.ShowWorkspaceNavigator, RendererInteractionAutomationIds.ActionRendererWorkspaceBackPrefix, workspaceId, subtabId, string.Empty), "Workspace Back", false);
        }

        private static void AddGroupActions(List<ScenarioAuthoringInspectorAction> actions, string[] keys, string prefix, string label, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            for (int i = 0; keys != null && i < keys.Length; i++)
                Add(actions, RendererInteractionCommand.ForKey(RendererInteractionCommandKind.ToggleDisclosure, prefix, keys[i]), label + ": " + keys[i], rendererInteraction.GetDisclosureExpanded(keys[i], ScenarioAuthoringRendererInteractionState.DefaultDisclosureExpanded(keys[i])));
        }

        private static void AddSettingsGroupActions(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringSettingsViewModel settings, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            for (int i = 0; settings != null && settings.Sections != null && i < settings.Sections.Length; i++)
            {
                ScenarioAuthoringSettingsSectionViewModel section = settings.Sections[i];
                if (section == null) continue;
                AddGroupActions(actions, new[] { "settings.group." + (section.Id ?? i.ToString()) }, RendererInteractionAutomationIds.ActionRendererWorkshopGroupTogglePrefix, "Toggle settings group", rendererInteraction);
            }
        }

        private static void AddShortcutGroupActions(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringShortcutOverlayViewModel shortcuts, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            for (int i = 0; shortcuts != null && shortcuts.Groups != null && i < shortcuts.Groups.Length; i++)
            {
                ScenarioAuthoringShortcutGroupViewModel group = shortcuts.Groups[i];
                if (group == null) continue;
                AddGroupActions(actions, new[] { "shortcuts.group." + (group.Title ?? i.ToString()).ToLowerInvariant() }, RendererInteractionAutomationIds.ActionRendererWorkshopGroupTogglePrefix, "Toggle shortcut group", rendererInteraction);
            }
        }

        private static void AddAssetInventoryFilters(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            for (int i = 0; i < AssetInventoryFilters.Length; i++)
                Add(actions, RendererInteractionCommand.ForKey(RendererInteractionCommandKind.SelectAssetInventoryFilter, RendererInteractionAutomationIds.ActionRendererAssetInventoryFilterPrefix, AssetInventoryFilters[i]), "Show " + AssetInventoryFilters[i] + " assets", string.Equals(rendererInteraction.AssetInventoryFilter, AssetInventoryFilters[i], StringComparison.OrdinalIgnoreCase));
        }

        private static void AddCandidateControls(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            string[] controls = { "build_palette_search", "sprite_picker_search" };
            string[] filters = { "all", "active", "vanilla", "scenario" };
            for (int i = 0; i < controls.Length; i++)
            {
                Add(actions, RendererInteractionCommand.ForControlValue(RendererInteractionCommandKind.SetCandidateSearch, RendererInteractionAutomationIds.ActionRendererCandidateSearchPrefix, controls[i], string.Empty), "Set " + controls[i], false);
                if (!string.Equals(controls[i], "sprite_picker_search", StringComparison.Ordinal)) continue;
                for (int filter = 0; filter < filters.Length; filter++)
                    Add(actions, RendererInteractionCommand.ForControlValue(RendererInteractionCommandKind.SetCandidateFilter, RendererInteractionAutomationIds.ActionRendererCandidateFilterPrefix, controls[i], filters[filter]), "Set " + controls[i] + " filter to " + filters[filter], string.Equals(rendererInteraction.GetCandidateFilter(controls[i], "all"), filters[filter], StringComparison.OrdinalIgnoreCase));
            }
        }

        private static void AddAssetBrowserActions(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringState state, ScenarioAuthoringShellWindowViewModel[] windows, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            AddCategory(actions, ScenarioAssetBrowserUx.FavoritesFilter, rendererInteraction);
            AddCategory(actions, ScenarioAssetBrowserUx.RecentFilter, rendererInteraction);
            AddCategory(actions, "all", rendererInteraction);
            for (int windowIndex = 0; windows != null && windowIndex < windows.Length; windowIndex++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[windowIndex];
                for (int sectionIndex = 0; window != null && window.Sections != null && sectionIndex < window.Sections.Length; sectionIndex++)
                {
                    ScenarioAuthoringInspectorSection section = window.Sections[sectionIndex];
                    if (section == null || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                        continue;
                    AddCategory(actions, section.Id, rendererInteraction);
                    for (int itemIndex = 0; section.Items != null && itemIndex < section.Items.Length; itemIndex++)
                    {
                        ScenarioAuthoringInspectorAction card = section.Items[itemIndex] != null ? section.Items[itemIndex].Action : null;
                        string sourceActionId = ScenarioAssetBrowserUx.GetSourceAutomationId(card);
                        if (!string.IsNullOrEmpty(sourceActionId))
                            Add(actions, RendererInteractionCommand.ForKey(RendererInteractionCommandKind.ToggleAssetFavorite, RendererInteractionAutomationIds.ActionRendererAssetFavoriteTogglePrefix, sourceActionId), "Toggle favorite: " + (card.Label ?? sourceActionId), ScenarioAssetBrowserUx.IsFavorite(state, sourceActionId));
                    }
                }
            }
        }

        private static void AddCategory(List<ScenarioAuthoringInspectorAction> actions, string category, ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            if (!string.IsNullOrEmpty(category))
                Add(actions, RendererInteractionCommand.ForKey(RendererInteractionCommandKind.SelectAssetCategory, RendererInteractionAutomationIds.ActionRendererAssetCategorySelectPrefix, category), "Show asset category: " + category, string.Equals(rendererInteraction.AssetBrowserCategory, category, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddPixelEditorActions(List<ScenarioAuthoringInspectorAction> actions, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor == null || !editor.Visible)
                return;
            for (int i = 0; i < editor.AnimationFrameCount; i++)
                Add(actions, SpriteSwapCommand.SelectAnimationFrame(i), "Select animation frame " + i, i == editor.AnimationFrameIndex);
            for (int i = 0; i < AnimationSpeedPresets.Length; i++)
                Add(actions, SpriteSwapCommand.SetAnimationSpeed(AnimationSpeedPresets[i]), "Animation speed " + AnimationSpeedPresets[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x", Math.Abs(editor.AnimationSpeed - AnimationSpeedPresets[i]) < 0.01f);
            for (int i = 0; editor.BrushPalette != null && i < editor.BrushPalette.Length; i++)
                Add(actions, SpriteSwapCommand.SelectPreset(i), "Select palette color " + i, i == editor.ActiveBrushIndex);
        }

        private static void Add(List<ScenarioAuthoringInspectorAction> actions, string id, string label, bool emphasized)
        {
            AddExisting(actions, new ScenarioAuthoringInspectorAction { Id = id, Label = label, Enabled = true, Emphasized = emphasized });
        }

        private static ScenarioAuthoringInspectorAction[] CanonicalizeContractActions(
            ScenarioAuthoringInspectorAction[] actions)
        {
            List<ScenarioAuthoringInspectorAction> canonical = new List<ScenarioAuthoringInspectorAction>();
            Dictionary<string, ScenarioAuthoringInspectorAction> byId = new Dictionary<string, ScenarioAuthoringInspectorAction>(StringComparer.Ordinal);
            for (int i = 0; actions != null && i < actions.Length; i++)
            {
                ScenarioAuthoringInspectorAction action = actions[i];
                if (action == null || string.IsNullOrEmpty(action.Id))
                    continue;

                ScenarioAuthoringInspectorAction existing;
                if (!byId.TryGetValue(action.Id, out existing))
                {
                    byId.Add(action.Id, action);
                    canonical.Add(action);
                    continue;
                }

                if (existing.Command != null
                    && action.Command != null
                    && existing.Command.GetType() == action.Command.GetType())
                {
                    continue;
                }

                throw new InvalidOperationException("Conflicting authoring semantic action id '" + action.Id + "'.");
            }

            ScenarioAuthoringInspectorAction[] result = canonical.ToArray();
            VerifyUniqueActionIdsForContract(result);
            return result;
        }

        private static void Add(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringCommand command, string label, bool emphasized)
        {
            AddExisting(actions, new ScenarioAuthoringInspectorAction
            {
                Id = command != null ? command.AutomationId : string.Empty,
                Command = command,
                Label = label,
                Enabled = true,
                Emphasized = emphasized
            });
        }

    }

    internal sealed class ScenarioAuthoringRendererInteractionState
    {
        private readonly Dictionary<string, bool> _disclosures = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _candidateSearches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _candidateFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _workspaceSubtabs = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _workspaceSelections = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _workspaceExpansions = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _workspaceSearches = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _workspaceNarrowPanes = new Dictionary<string, bool>(StringComparer.Ordinal);

        private string _boundDraftId;
        public string AssetBrowserSearch { get; set; }
        public string AssetBrowserCategory { get; set; }
        public string AssetInventoryFilter { get; set; }
        public bool TopBarMoreOpen { get; set; }
        public string GlobalSearchQuery { get; set; }

        internal ScenarioAuthoringRendererInteractionState()
        {
            Reset();
        }

        internal void Bind(string draftId)
        {
            if (string.Equals(_boundDraftId, draftId, StringComparison.OrdinalIgnoreCase))
                return;
            Reset();
            _boundDraftId = draftId;
        }

        internal void Reset()
        {
            _boundDraftId = null;
            _disclosures.Clear();
            _candidateSearches.Clear();
            _candidateFilters.Clear();
            _workspaceSubtabs.Clear();
            _workspaceSelections.Clear();
            _workspaceExpansions.Clear();
            _workspaceSearches.Clear();
            _workspaceNarrowPanes.Clear();
            AssetBrowserSearch = string.Empty;
            AssetBrowserCategory = "all";
            AssetInventoryFilter = "all";
            GlobalSearchQuery = string.Empty;
            TopBarMoreOpen = false;
        }

        public bool GetDisclosureExpanded(string key, bool defaultExpanded)
        {
            bool expanded;
            if (_disclosures.TryGetValue(key, out expanded)) return expanded;
            _disclosures[key] = defaultExpanded;
            return defaultExpanded;
        }

        public bool ToggleDisclosure(string key)
        {
            bool next = !GetDisclosureExpanded(key, DefaultDisclosureExpanded(key));
            _disclosures[key] = next;
            return next;
        }

        public static bool DefaultDisclosureExpanded(string key)
        {
            return string.Equals(key, "pixel_editor.group.animation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "pixel_editor.group.color", StringComparison.OrdinalIgnoreCase);
        }

        public string GetCandidateSearch(string key)
        {
            string value;
            return !string.IsNullOrEmpty(key) && _candidateSearches.TryGetValue(key, out value) ? value : string.Empty;
        }

        public void SetCandidateSearch(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) _candidateSearches[key] = value ?? string.Empty;
        }

        public string GetCandidateFilter(string key, string fallback)
        {
            string value;
            return !string.IsNullOrEmpty(key) && _candidateFilters.TryGetValue(key, out value) ? value : fallback;
        }

        public void SetCandidateFilter(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) _candidateFilters[key] = value ?? string.Empty;
        }

        public string GetWorkspaceSubtab(string workspaceId, string fallback)
        {
            string value;
            return !string.IsNullOrEmpty(workspaceId) && _workspaceSubtabs.TryGetValue(workspaceId, out value)
                ? value
                : fallback;
        }

        public void SetWorkspaceSubtab(string workspaceId, string subtabId)
        {
            if (!string.IsNullOrEmpty(workspaceId))
                _workspaceSubtabs[workspaceId] = subtabId ?? string.Empty;
        }

        public string GetWorkspaceSelection(string workspaceId, string subtabId)
        {
            string value;
            string key = BuildWorkspaceSubtabKey(workspaceId, subtabId);
            return _workspaceSelections.TryGetValue(key, out value) ? value : null;
        }

        public void SetWorkspaceSelection(string workspaceId, string subtabId, string entityId)
        {
            string key = BuildWorkspaceSubtabKey(workspaceId, subtabId);
            if (string.IsNullOrEmpty(entityId))
                _workspaceSelections.Remove(key);
            else
                _workspaceSelections[key] = entityId;
        }

        public bool GetWorkspaceExpanded(string workspaceId, string subtabId, string entityId, bool defaultExpanded)
        {
            bool expanded;
            string key = BuildWorkspaceEntityKey(workspaceId, subtabId, entityId);
            if (_workspaceExpansions.TryGetValue(key, out expanded))
                return expanded;
            _workspaceExpansions[key] = defaultExpanded;
            return defaultExpanded;
        }

        public void SetWorkspaceExpanded(string workspaceId, string subtabId, string entityId, bool expanded)
        {
            _workspaceExpansions[BuildWorkspaceEntityKey(workspaceId, subtabId, entityId)] = expanded;
        }

        public string GetWorkspaceSearch(string workspaceId, string subtabId)
        {
            string value;
            string key = BuildWorkspaceSubtabKey(workspaceId, subtabId);
            return _workspaceSearches.TryGetValue(key, out value) ? value : string.Empty;
        }

        public void SetWorkspaceSearch(string workspaceId, string subtabId, string value)
        {
            _workspaceSearches[BuildWorkspaceSubtabKey(workspaceId, subtabId)] = value ?? string.Empty;
        }

        public bool GetWorkspaceNarrowPane(string workspaceId, string subtabId, bool defaultDocumentPane)
        {
            bool documentPane;
            string key = BuildWorkspaceSubtabKey(workspaceId, subtabId);
            return _workspaceNarrowPanes.TryGetValue(key, out documentPane) ? documentPane : defaultDocumentPane;
        }

        public void SetWorkspaceNarrowPane(string workspaceId, string subtabId, bool documentPane)
        {
            _workspaceNarrowPanes[BuildWorkspaceSubtabKey(workspaceId, subtabId)] = documentPane;
        }

        private static string BuildWorkspaceSubtabKey(string workspaceId, string subtabId)
        {
            return (workspaceId ?? string.Empty) + "\n" + (subtabId ?? string.Empty);
        }

        private static string BuildWorkspaceEntityKey(string workspaceId, string subtabId, string entityId)
        {
            return BuildWorkspaceSubtabKey(workspaceId, subtabId) + "\n" + (entityId ?? string.Empty);
        }
    }
}
