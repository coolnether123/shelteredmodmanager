using System;
using System.Collections.Generic;
using System.Text;

using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell
{
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
            "home.group.status"
        };

        private static readonly string[] AssetInventoryFilters = { "all", "unused", "used", "large" };
        private static readonly float[] AnimationSpeedPresets = { 0.25f, 0.5f, 1f, 1.5f, 2f };

        public static ScenarioAuthoringInspectorAction[] Build(
            ScenarioAuthoringState state,
            ScenarioAuthoringShellWindowViewModel[] windows,
            ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            AddMapFilters(actions);
            AddGroupActions(actions, PixelGroups, ScenarioAuthoringActionIds.ActionRendererPixelGroupTogglePrefix, "Toggle pixel-editor group");
            AddGroupActions(actions, HomeGroups, ScenarioAuthoringActionIds.ActionRendererHomeGroupTogglePrefix, "Toggle Home group");
            Add(actions, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.snap_to_grid", "Toggle placement snap", true);
            Add(actions, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.show_grid", "Toggle placement grid", true);
            Add(actions, ScenarioAuthoringActionIds.ActionRendererPlacementBack, "Placement Back", true);
            Add(actions, ScenarioAuthoringActionIds.ActionRendererPlacementDone, "Placement Done", true);
            Add(actions, ScenarioAuthoringActionIds.ActionRendererTopBarMoreToggle, "Toggle stage overflow", true);
            Add(actions, ScenarioAuthoringActionIds.ActionRendererAssetSearchPrefix, "Set asset search (append base64 UTF-8 value)", true);
            Add(actions, ScenarioAuthoringActionIds.ActionRendererAssetSearchClear, "Clear asset search", true);
            Add(actions, ScenarioAuthoringActionIds.ActionRendererGlobalSearchQueryPrefix, "Set global search query (append base64 UTF-8 value)", false);
            AddAssetInventoryFilters(actions);
            AddCandidateControls(actions);
            AddAssetBrowserActions(actions, state, windows);
            AddPixelEditorActions(actions, editor);
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

            ScenarioAuthoringInspectorItem[] items = new ScenarioAuthoringInspectorItem[actions.Count];
            for (int i = 0; i < actions.Count; i++)
                items[i] = new ScenarioAuthoringInspectorItem { Kind = ScenarioAuthoringInspectorItemKind.Action, Action = actions[i] };
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
                CollectSections(actions, window.Sections);
            }
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
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    AddExisting(actions, item != null ? item.Action : null);
                    ScenarioCastCardViewModel cast = item != null ? item.CastCard : null;
                    AddExisting(actions, cast != null ? cast.PrimaryAction : null);
                    AddRange(actions, cast != null ? cast.SecondaryActions : null);
                }
                CollectInventory(actions, section != null ? section.InventorySlotGrid : null);
                CollectSurvivor(actions, section != null ? section.SurvivorEditor : null);
                for (int j = 0; section != null && section.ModFieldRows != null && j < section.ModFieldRows.Length; j++)
                    CollectModField(actions, section.ModFieldRows[j]);
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
                if (!string.IsNullOrEmpty(row.OpenColorPickerActionId))
                    Add(actions, row.OpenColorPickerActionId, "Open " + (row.Label ?? "color") + " picker", false);
                if (!string.IsNullOrEmpty(row.ApplyColorActionPrefix))
                    Add(actions, row.ApplyColorActionPrefix, "Set " + (row.Label ?? "color") + " (append RRGGBBAA)", false);
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
            if (row.ColorRow != null && !string.IsNullOrEmpty(row.ColorRow.OpenColorPickerActionId))
                Add(actions, row.ColorRow.OpenColorPickerActionId, "Open mod color picker", false);
            if (row.ColorRow != null && !string.IsNullOrEmpty(row.ColorRow.ApplyColorActionPrefix))
                Add(actions, row.ColorRow.ApplyColorActionPrefix, "Set mod color (append RRGGBBAA)", false);
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
                        Add(actions, ScenarioAuthoringActionIds.ActionSettingTogglePrefix + item.Id, "Toggle " + item.Label, item.BoolValue);
                    else if (item.Kind == ScenarioAuthoringSettingKind.Float || item.Kind == ScenarioAuthoringSettingKind.Integer)
                    {
                        Add(actions, ScenarioAuthoringActionIds.ActionSettingDecreasePrefix + item.Id, "Decrease " + item.Label, false);
                        Add(actions, ScenarioAuthoringActionIds.ActionSettingIncreasePrefix + item.Id, "Increase " + item.Label, false);
                    }
                    else if (item.Kind == ScenarioAuthoringSettingKind.Choice)
                    {
                        for (int choice = 0; item.ChoiceValues != null && choice < item.ChoiceValues.Length; choice++)
                            Add(actions, ScenarioAuthoringActionIds.ActionSettingSelectPrefix + item.Id + "." + ScenarioAuthoringActionCodec.EncodeToken(item.ChoiceValues[choice]), "Set " + item.Label + " to " + item.ChoiceValues[choice], choice == item.SelectedChoiceIndex);
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
            for (int i = 0; i < actions.Count; i++)
                if (string.Equals(actions[i].Id, action.Id, StringComparison.Ordinal)) return;
            actions.Add(action);
        }

        private static void AddMapFilters(List<ScenarioAuthoringInspectorAction> actions)
        {
            Array values = Enum.GetValues(typeof(ScenarioMapAuthoringFilter));
            for (int i = 0; i < values.Length; i++)
            {
                ScenarioMapAuthoringFilter filter = (ScenarioMapAuthoringFilter)values.GetValue(i);
                Add(actions, ScenarioAuthoringActionIds.ActionRendererMapFilterTogglePrefix + filter, "Toggle map filter: " + filter, ScenarioMapAuthoringFilterState.IsVisible(filter));
            }
        }

        private static void AddGroupActions(List<ScenarioAuthoringInspectorAction> actions, string[] keys, string prefix, string label)
        {
            for (int i = 0; keys != null && i < keys.Length; i++)
                Add(actions, prefix + ScenarioAuthoringActionCodec.EncodeToken(keys[i]), label + ": " + keys[i], ScenarioAuthoringRendererInteractionState.Instance.GetDisclosureExpanded(keys[i], ScenarioAuthoringRendererInteractionState.DefaultDisclosureExpanded(keys[i])));
        }

        private static void AddAssetInventoryFilters(List<ScenarioAuthoringInspectorAction> actions)
        {
            for (int i = 0; i < AssetInventoryFilters.Length; i++)
                Add(actions, ScenarioAuthoringActionIds.ActionRendererAssetInventoryFilterPrefix + ScenarioAuthoringActionCodec.EncodeToken(AssetInventoryFilters[i]), "Show " + AssetInventoryFilters[i] + " assets", string.Equals(ScenarioAuthoringRendererInteractionState.Instance.AssetInventoryFilter, AssetInventoryFilters[i], StringComparison.OrdinalIgnoreCase));
        }

        private static void AddCandidateControls(List<ScenarioAuthoringInspectorAction> actions)
        {
            string[] controls = { "build_palette_search", "sprite_picker_search" };
            string[] filters = { "all", "active", "vanilla", "scenario" };
            for (int i = 0; i < controls.Length; i++)
            {
                Add(actions, BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererCandidateSearchPrefix, controls[i] + "\n"), "Set " + controls[i] + " (replace value after newline)", false);
                if (!string.Equals(controls[i], "sprite_picker_search", StringComparison.Ordinal)) continue;
                for (int filter = 0; filter < filters.Length; filter++)
                    Add(actions, BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererCandidateFilterPrefix, controls[i] + "\n" + filters[filter]), "Set " + controls[i] + " filter to " + filters[filter], string.Equals(ScenarioAuthoringRendererInteractionState.Instance.GetCandidateFilter(controls[i], "all"), filters[filter], StringComparison.OrdinalIgnoreCase));
            }
        }

        private static void AddAssetBrowserActions(List<ScenarioAuthoringInspectorAction> actions, ScenarioAuthoringState state, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            AddCategory(actions, ScenarioAssetBrowserUx.FavoritesFilter);
            AddCategory(actions, ScenarioAssetBrowserUx.RecentFilter);
            AddCategory(actions, "all");
            for (int windowIndex = 0; windows != null && windowIndex < windows.Length; windowIndex++)
            {
                ScenarioAuthoringShellWindowViewModel window = windows[windowIndex];
                for (int sectionIndex = 0; window != null && window.Sections != null && sectionIndex < window.Sections.Length; sectionIndex++)
                {
                    ScenarioAuthoringInspectorSection section = window.Sections[sectionIndex];
                    if (section == null || section.Layout != ScenarioAuthoringInspectorSectionLayout.CandidateGrid)
                        continue;
                    AddCategory(actions, section.Id);
                    for (int itemIndex = 0; section.Items != null && itemIndex < section.Items.Length; itemIndex++)
                    {
                        ScenarioAuthoringInspectorAction card = section.Items[itemIndex] != null ? section.Items[itemIndex].Action : null;
                        string sourceActionId = ScenarioAssetBrowserUx.DecodeSourceActionId(card != null ? card.Id : null);
                        if (!string.IsNullOrEmpty(sourceActionId))
                            Add(actions, BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererAssetFavoriteTogglePrefix, sourceActionId), "Toggle favorite: " + (card.Label ?? sourceActionId), ScenarioAssetBrowserUx.IsFavorite(state, sourceActionId));
                    }
                }
            }
        }

        private static void AddCategory(List<ScenarioAuthoringInspectorAction> actions, string category)
        {
            if (!string.IsNullOrEmpty(category))
                Add(actions, BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererAssetCategorySelectPrefix, category), "Show asset category: " + category, string.Equals(ScenarioAuthoringRendererInteractionState.Instance.AssetBrowserCategory, category, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddPixelEditorActions(List<ScenarioAuthoringInspectorAction> actions, ScenarioSpriteSwapAuthoringService.CustomEditorModel editor)
        {
            if (editor == null || !editor.Visible)
                return;
            for (int i = 0; i < editor.AnimationFrameCount; i++)
                Add(actions, ScenarioSpriteSwapAuthoringService.BuildAnimationFrameActionId(i), "Select animation frame " + i, i == editor.AnimationFrameIndex);
            for (int i = 0; i < AnimationSpeedPresets.Length; i++)
                Add(actions, ScenarioAuthoringActionIds.ActionSpriteSwapAnimationSpeedPrefix + AnimationSpeedPresets[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), "Animation speed " + AnimationSpeedPresets[i].ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x", Math.Abs(editor.AnimationSpeed - AnimationSpeedPresets[i]) < 0.01f);
            for (int i = 0; editor.BrushPalette != null && i < editor.BrushPalette.Length; i++)
                Add(actions, ScenarioSpriteSwapAuthoringService.BuildCustomPresetActionId(i), "Select palette color " + i, i == editor.ActiveBrushIndex);
            Add(actions, ScenarioAuthoringActionIds.ActionSpriteSwapCustomColorPrefix, "Set color (append RRGGBBAA)", true);
        }

        internal static string BuildTokenAction(string prefix, string value)
        {
            return prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        internal static bool TryDecodeTokenAction(string actionId, string prefix, out string value)
        {
            value = null;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            try
            {
                value = Encoding.UTF8.GetString(Convert.FromBase64String(actionId.Substring(prefix.Length)));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Add(List<ScenarioAuthoringInspectorAction> actions, string id, string label, bool emphasized)
        {
            for (int i = 0; i < actions.Count; i++)
                if (string.Equals(actions[i].Id, id, StringComparison.Ordinal)) return;
            actions.Add(new ScenarioAuthoringInspectorAction { Id = id, Label = label, Enabled = true, Emphasized = emphasized });
        }
    }

    internal sealed class ScenarioAuthoringRendererInteractionState
    {
        private static readonly ScenarioAuthoringRendererInteractionState Shared = new ScenarioAuthoringRendererInteractionState();
        private readonly Dictionary<string, bool> _disclosures = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _candidateSearches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _candidateFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static ScenarioAuthoringRendererInteractionState Instance { get { return Shared; } }
        public string AssetBrowserSearch { get; set; }
        public string AssetBrowserCategory { get; set; }
        public string AssetInventoryFilter { get; set; }
        public bool TopBarMoreOpen { get; set; }
        public string GlobalSearchQuery { get; set; }

        private ScenarioAuthoringRendererInteractionState()
        {
            AssetBrowserSearch = string.Empty;
            AssetBrowserCategory = "all";
            AssetInventoryFilter = "all";
            GlobalSearchQuery = string.Empty;
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
    }
}
