using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringPresentationBuilder
    {
        private readonly ScenarioAuthoringCaptureService _captureService;
        private readonly ScenarioSpriteSwapAuthoringService _spriteSwapAuthoringService;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacementAuthoringService;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacementAuthoringService;
        private readonly ScenarioAuthoringWindowRegistry _windowRegistry;
        private readonly ScenarioAuthoringSettingsService _settingsService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioSpriteRuntimeResolver _runtimeResolver;
        private readonly ShellChromeViewModelBuilder _shellChromeBuilder;
        private readonly StageNavigationViewModelBuilder _stageNavigationBuilder;
        private readonly InspectorViewModelBuilder _inspectorViewModelBuilder;
        private readonly StatusBarViewModelBuilder _statusBarViewModelBuilder;
        private readonly ScenarioTimelineBuilder _timelineBuilder;
        private readonly ScenarioTimelineViewModelBuilder _timelineViewModelBuilder;
        private readonly ScenarioModDependencyDetector _modDependencyDetector;
        private readonly ScenarioModCompatibilityViewModelBuilder _modCompatibilityViewModelBuilder;
        private readonly ScenarioSelectionScopeService _selectionScopeService;
        private readonly ScenarioTargetClassifier _targetClassifier;

        public ScenarioAuthoringPresentationBuilder(
            ScenarioAuthoringCaptureService captureService,
            ScenarioSpriteSwapAuthoringService spriteSwapAuthoringService,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacementAuthoringService,
            ScenarioBuildPlacementAuthoringService buildPlacementAuthoringService,
            ScenarioAuthoringWindowRegistry windowRegistry,
            ScenarioAuthoringSettingsService settingsService,
            ScenarioAuthoringLayoutService layoutService,
            IScenarioEditorService editorService,
            ScenarioSpriteRuntimeResolver runtimeResolver,
            ShellChromeViewModelBuilder shellChromeBuilder,
            StageNavigationViewModelBuilder stageNavigationBuilder,
            InspectorViewModelBuilder inspectorViewModelBuilder,
            StatusBarViewModelBuilder statusBarViewModelBuilder,
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineViewModelBuilder timelineViewModelBuilder,
            ScenarioModDependencyDetector modDependencyDetector,
            ScenarioModCompatibilityViewModelBuilder modCompatibilityViewModelBuilder,
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioTargetClassifier targetClassifier)
        {
            _captureService = captureService;
            _spriteSwapAuthoringService = spriteSwapAuthoringService;
            _sceneSpritePlacementAuthoringService = sceneSpritePlacementAuthoringService;
            _buildPlacementAuthoringService = buildPlacementAuthoringService;
            _windowRegistry = windowRegistry;
            _settingsService = settingsService;
            _layoutService = layoutService;
            _editorService = editorService;
            _runtimeResolver = runtimeResolver;
            _shellChromeBuilder = shellChromeBuilder;
            _stageNavigationBuilder = stageNavigationBuilder;
            _inspectorViewModelBuilder = inspectorViewModelBuilder;
            _statusBarViewModelBuilder = statusBarViewModelBuilder;
            _timelineBuilder = timelineBuilder;
            _timelineViewModelBuilder = timelineViewModelBuilder;
            _modDependencyDetector = modDependencyDetector;
            _modCompatibilityViewModelBuilder = modCompatibilityViewModelBuilder;
            _selectionScopeService = selectionScopeService;
            _targetClassifier = targetClassifier;
        }

        public ScenarioAuthoringShellViewModel BuildShellViewModel(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioAuthoringContextMenuModel contextMenu)
        {
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            List<ScenarioAuthoringShellWindowViewModel> windows = new List<ScenarioAuthoringShellWindowViewModel>();
            AppendShellWindowViewModels(windows, state, editorSession, session, definition);

            ScenarioAuthoringShellViewModel viewModel = new ScenarioAuthoringShellViewModel
            {
                Tabs = _stageNavigationBuilder.BuildTabs(state),
                ToolbarActions = _stageNavigationBuilder.BuildToolbarActions(state),
                LayoutActions = _stageNavigationBuilder.BuildLayoutActions(state),
                WindowMenuActions = _stageNavigationBuilder.BuildWindowMenuActions(state, _windowRegistry),
                Windows = windows.ToArray(),
                SpritePickerDocument = BuildSpritePickerDocument(state, editorSession),
                Settings = state.SettingsWindowOpen ? BuildSettingsViewModel(state) : null,
                ContextMenu = contextMenu,
                StatusEntries = _statusBarViewModelBuilder.BuildEntries(state, editorSession, session, _stageNavigationBuilder.BuildStageLabel(state))
            };
            _shellChromeBuilder.ApplyShellChrome(viewModel, state, editorSession, session);
            return viewModel;
        }

        private static string FormatDraftDisplay(string draftId)
        {
            if (string.IsNullOrEmpty(draftId))
                return "Untitled";

            const string prefix = "smm.authoring.";
            if (draftId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && draftId.Length > prefix.Length)
            {
                string tail = draftId.Substring(prefix.Length);
                int dot = tail.IndexOf('.');
                return dot > 0 ? "Draft " + tail.Substring(0, dot) : "Draft " + tail;
            }

            return draftId.Length > 32 ? draftId.Substring(0, 29) + "..." : draftId;
        }

        private static string BuildEditorModeLabel(ScenarioEditorSession editorSession, ScenarioAuthoringState state)
        {
            if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                return "Playtesting";

            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
                return "Playtesting";

            if (state != null && state.MinimalMode)
                return "Minimal Editing";

            return "Editing Draft";
        }

        public ScenarioAuthoringInspectorDocument BuildShellDocument(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session)
        {
            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            string selectedObjectStatus;
            bool canCaptureSelectedObject = _captureService.CanCaptureTarget(state.SelectedTarget, out selectedObjectStatus);
            bool hasCapturedSelectedObject = _captureService.HasCapturedPlacementForTarget(editorSession, state.SelectedTarget);

            sections.Add(_inspectorViewModelBuilder.BuildSessionSection(state, editorSession, session, _stageNavigationBuilder.BuildStageLabel(state)));

            sections.Add(BuildWorkflowSection(editorSession));
            sections.Add(BuildHistorySection());
            sections.Add(BuildToolPickerSection(state.ActiveTool));
            sections.Add(BuildToolSection(
                state,
                state.ActiveTool,
                definition,
                state.SelectedTarget,
                canCaptureSelectedObject,
                hasCapturedSelectedObject,
                selectedObjectStatus));
            sections.Add(BuildSelectionSection(state));

            if (!string.IsNullOrEmpty(state.StatusMessage))
            {
                sections.Add(_inspectorViewModelBuilder.BuildStatusSection(state.StatusMessage));
            }

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "controls",
                Title = "Controls",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    Text("Hold Ctrl to enter selection mode. Cyan outline follows the hovered target, yellow marks the selected target."),
                    Text("Left Click confirms the hovered target. Right Click clears the current selection."),
                    Text("F5 saves the draft, F6 toggles the shell, and F7 toggles playtest mode."),
                    Text("Ctrl+Z undoes, Ctrl+Y redoes, Ctrl+C copies, Ctrl+V pastes, Ctrl+R reverts the selected target's sprite."),
                    Text("Authoring pause freezes simulation without opening Sheltered's vanilla pause menu."),
                    Text("Use the Assets workflow to replace existing visuals or place new snapped scene sprites on the shelter map."),
                    Text("Use playtest to make real Sheltered changes in the shelter, then stop playtest and capture the live family, inventory, or spawned objects back into the draft.")
                }
            });

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Scenario Authoring",
                Subtitle = editorSession != null && editorSession.WorkingDefinition != null
                    ? editorSession.WorkingDefinition.DisplayName
                    : "No active definition",
                HeaderActions = BuildHeaderActions(editorSession, state.SelectedTarget != null),
                Sections = sections.ToArray()
            };
        }

        public ScenarioAuthoringInspectorDocument BuildInspectorDocument(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession)
        {
            ScenarioAuthoringTarget target = state.SelectedTarget ?? state.HoveredTarget;
            if (target == null)
            {
                return new ScenarioAuthoringInspectorDocument
                {
                    Title = "Selection Inspector",
                    Subtitle = "No target selected",
                    HeaderActions = new ScenarioAuthoringInspectorAction[0],
                    Sections = new[]
                    {
                        new ScenarioAuthoringInspectorSection
                        {
                            Id = "empty",
                            Title = "Inspector",
                            Expanded = true,
                            Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                            Items = new[]
                            {
                                Text("Hold the selection modifier and hover a world object to inspect it.")
                            }
                        }
                    }
                };
            }

            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            ScenarioTargetClassification classification = _targetClassifier.Classify(target);
            ObjectPlacement objectPlacement = FindObjectPlacement(definition, target);
            int linkedTimelineEntries = CountLikelyTriggerReferences(definition, target);
            bool scopeAllowed = _selectionScopeService.CanSelectTargetForCurrentStage(state, target);

            string captureReason;
            bool canCaptureTarget = _captureService.CanCaptureTarget(target, out captureReason);
            bool hasCapturedPlacement = _captureService.HasCapturedPlacementForTarget(editorSession, target);
            bool replacementAllowed = scopeAllowed && target.SupportsReplace;

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(BuildObjectSummarySection(target, classification, objectPlacement, hasCapturedPlacement));
            sections.Add(BuildScenarioBehaviorSection(target, objectPlacement, linkedTimelineEntries));
            sections.Add(BuildPrimaryActionsSection(scopeAllowed, canCaptureTarget, hasCapturedPlacement, replacementAllowed));
            sections.Add(BuildWarningsSection(scopeAllowed, target, objectPlacement, definition, captureReason));

            sections.Add(BuildAdvancedDebugSection(target));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Target Inspector",
                Subtitle = target.DisplayName,
                HeaderActions = new[]
                {
                    Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current scenario target selection.", true, false)
                },
                Sections = sections.ToArray()
            };
        }

        private ScenarioAuthoringInspectorSection BuildObjectSummarySection(
            ScenarioAuthoringTarget target,
            ScenarioTargetClassification classification,
            ObjectPlacement objectPlacement,
            bool hasCapturedPlacement)
        {
            string friendlyKind = FriendlyKindLabel(target.Kind);
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Text(
                Safe(target.DisplayName),
                friendlyKind,
                _targetClassifier.FormatScopeLabel(classification),
                "TG",
                ResolvePreviewSprite(target),
                true));
            items.Add(Property("Display Name", Safe(target.DisplayName)));
            items.Add(Property("Type", friendlyKind));
            items.Add(Property("Selection Scope", _targetClassifier.FormatScopeLabel(classification)));
            items.Add(Property("Scenario Object Id", Safe(ResolveScenarioObjectId(target, objectPlacement, hasCapturedPlacement))));
            items.Add(Property("Draft Status", ResolveDraftStatus(target, objectPlacement, hasCapturedPlacement)));
            items.Add(Property("Start State", FormatStartState(objectPlacement)));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "object_summary",
                Title = "Object Summary",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildScenarioBehaviorSection(
            ScenarioAuthoringTarget target,
            ObjectPlacement objectPlacement,
            int linkedTimelineEntries)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Foundation", Safe(objectPlacement != null ? objectPlacement.RequiredFoundationId : null)));
            items.Add(Property("Bunker Expansion", Safe(objectPlacement != null ? objectPlacement.RequiredBunkerExpansionId : null)));
            items.Add(Property("Unlock Gate", Safe(objectPlacement != null ? objectPlacement.UnlockGateId : null)));
            items.Add(Property("Scheduled Activation", Safe(objectPlacement != null ? objectPlacement.ScheduledActivationId : null)));
            items.Add(Property("Timeline Entries", linkedTimelineEntries.ToString(CultureInfo.InvariantCulture)));
            items.Add(Property("Dependency / Mod", target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId) ? "Scenario authored" : "Vanilla or live object"));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "scenario_behavior",
                Title = "Scenario Behavior",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildPrimaryActionsSection(
            bool scopeAllowed,
            bool canCaptureTarget,
            bool hasCapturedPlacement,
            bool replacementAllowed)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionCaptureSelectedObject,
                "Capture to Scenario",
                "Store this live spawned shelter object as a scenario object placement.",
                scopeAllowed && canCaptureTarget,
                scopeAllowed && canCaptureTarget,
                "CP")));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement,
                "Remove from Draft",
                "Remove this object's captured placement from the scenario draft.",
                scopeAllowed && hasCapturedPlacement,
                false,
                "RM")));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionToolAssets,
                "Open Asset Picker",
                "Open the focused visual replacement and placement tray.",
                replacementAllowed,
                false,
                "AS")));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                "Replace Visual",
                "Open the sprite picker for this visual target.",
                replacementAllowed,
                false,
                "RV")));
            items.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSelectionClear,
                "Clear Selection",
                "Clear the current scenario target selection.",
                true,
                false,
                "CL")));
            return new ScenarioAuthoringInspectorSection
            {
                Id = "primary_actions",
                Title = "Primary Actions",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildWarningsSection(
            bool scopeAllowed,
            ScenarioAuthoringTarget target,
            ObjectPlacement objectPlacement,
            ScenarioDefinition definition,
            string captureReason)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (!scopeAllowed)
                items.Add(Text("Target is filtered by the active selection scope."));
            if (objectPlacement != null && string.IsNullOrEmpty(objectPlacement.ScenarioObjectId))
                items.Add(Text("Missing scenario object id."));
            if (objectPlacement != null
                && string.IsNullOrEmpty(objectPlacement.RequiredFoundationId)
                && string.IsNullOrEmpty(objectPlacement.RequiredBunkerExpansionId))
                items.Add(Text("Missing foundation or expansion support."));
            if (objectPlacement != null
                && objectPlacement.StartState == ScenarioObjectStartState.StartsEnabled
                && !HasSupport(definition, objectPlacement))
                items.Add(Text("Object starts active but its support is not present in the draft."));
            if (objectPlacement != null
                && objectPlacement.StartState == ScenarioObjectStartState.StartsEnabled
                && !string.IsNullOrEmpty(objectPlacement.RequiredBunkerExpansionId)
                && !HasExpansion(definition, objectPlacement.RequiredBunkerExpansionId))
                items.Add(Text("Object is inside a locked or missing expansion but starts enabled."));
            if (target != null && target.SupportsReplace && string.IsNullOrEmpty(target.ScenarioReferenceId) && objectPlacement == null)
                items.Add(Text("Visual replacement may need an asset or object capture before it is portable."));
            if (!string.IsNullOrEmpty(captureReason))
                items.Add(Text(captureReason));
            if (items.Count == 0)
                items.Add(Text("No warnings for this target."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "warnings",
                Title = "Warnings",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = items.ToArray()
            };
        }

        private ScenarioAuthoringInspectorDocument BuildSpritePickerDocument(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession)
        {
            if (state == null
                || state.SpriteSwapPicker == null
                || !state.SpriteSwapPicker.IsOpen
                || state.SpriteSwapPicker.Target == null)
            {
                return null;
            }

            ScenarioSpriteSwapAuthoringService.CustomEditorModel customEditor = _spriteSwapAuthoringService.GetCustomEditorModel(state);
            if (customEditor != null && customEditor.IsCharacterEditor)
                return BuildCharacterSpritePickerDocument(state, customEditor);

            ScenarioSpriteSwapAuthoringService.SpritePickerModel picker = _spriteSwapAuthoringService.GetPickerModel(
                editorSession,
                state.SpriteSwapPicker.Target,
                state.ActiveScenarioFilePath);

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            if (picker == null || picker.Target == null)
            {
                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "sprite_picker_empty",
                    Title = "Sprite Picker",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text("The selected target no longer exposes compatible sprite replacements."),
                        ActionItem(Action(
                            ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel,
                            "Cancel",
                            "Close the sprite picker and restore the current sprite.",
                            true,
                            false))
                    }
                });

                return new ScenarioAuthoringInspectorDocument
                {
                    Title = "Sprite Picker",
                    Subtitle = FormatTarget(state.SpriteSwapPicker.Target),
                    HeaderActions = new ScenarioAuthoringInspectorAction[0],
                    Sections = sections.ToArray()
                };
            }

            string savedToken = !string.IsNullOrEmpty(state.SpriteSwapPicker.SavedCandidateToken)
                ? state.SpriteSwapPicker.SavedCandidateToken
                : picker.ActiveCandidateToken;
            string previewToken = !string.IsNullOrEmpty(state.SpriteSwapPicker.PreviewCandidateToken)
                ? state.SpriteSwapPicker.PreviewCandidateToken
                : savedToken;
            ScenarioSpriteCatalogService.SpriteCandidate previewCandidate = FindCandidate(picker, previewToken);

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_picker_summary",
                Title = "Target",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = new[]
                {
                    Text(
                        Safe(picker.Target.SpriteName),
                        Safe(picker.Target.TextureName),
                        picker.Target.Kind.ToString(),
                        "SP",
                        previewCandidate != null ? previewCandidate.Sprite : picker.Target.CurrentSprite,
                        true),
                    Property("Target", FormatTarget(state.SpriteSwapPicker.Target)),
                    Property("Component", picker.Target.Kind.ToString()),
                    Property("Saved Swap", Safe(picker.ActiveRuleSummary)),
                    Property("Preview", customEditor != null ? "Custom Sprite Draft" : (previewCandidate != null ? CleanCandidateLabel(previewCandidate.Label) : "<current>")),
                    Property("Custom Editor", customEditor != null ? "Active" : "Inactive"),
                    Property("Compatibility", Safe(picker.CompatibilitySummary)),
                    Property("Stored As", Safe(picker.XmlPathHint)),
                    Property("Compatible Vanilla", CountCandidates(picker.VanillaCandidates).ToString()),
                    Property("Compatible Modded", CountCandidates(picker.ModdedCandidates).ToString()),
                    Text("Selecting a sprite previews it immediately on the live target. The custom editor now supports paint, eyedropper, rectangular selection, and pixel copy/paste before saving.")
                }
            });

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "sprite_picker_actions",
                Title = "Commit",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave,
                        "Save & Close",
                        "Persist the previewed sprite swap and close the picker.",
                        true,
                        false,
                        "SV",
                        "Commit the current preview.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel,
                        "Cancel",
                        "Discard the preview and restore the currently saved sprite.",
                        true,
                        false,
                        "CL",
                        "Restore the previous sprite.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditStart,
                        customEditor != null ? "Custom Editor Active" : "Edit Custom Copy",
                        "Duplicate the current preview into the in-picker pixel editor so you can recolor, sample, select, copy, and paste pixels before saving a scenario custom sprite.",
                        true,
                        customEditor != null,
                        "PX",
                        "Edit pixels in the same workspace.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapCustomEditDiscard,
                        "Discard Custom Copy",
                        "Discard the current custom sprite draft and restore the saved sprite preview.",
                        customEditor != null,
                        false,
                        "DS",
                        "Drop the in-progress custom sprite draft."))
                }
            });

            sections.Add(BuildSpriteCandidateSection(
                "sprite_picker_vanilla",
                "Vanilla Sprites",
                _selectionScopeService.FilterCandidatesForScope(picker.VanillaCandidates, state),
                "No verified vanilla/runtime sprites are currently available for this target family.",
                savedToken,
                previewToken));
            sections.Add(BuildSpriteCandidateSection(
                "sprite_picker_modded",
                "Modded Sprites",
                _selectionScopeService.FilterCandidatesForScope(picker.ModdedCandidates, state),
                "Custom sprite overrides are hidden in strict replacement mode.",
                savedToken,
                previewToken));

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Sprite Picker",
                Subtitle = FormatTarget(state.SpriteSwapPicker.Target),
                HeaderActions = new ScenarioAuthoringInspectorAction[0],
                Sections = sections.ToArray()
            };
        }

        private ScenarioAuthoringInspectorDocument BuildCharacterSpritePickerDocument(
            ScenarioAuthoringState state,
            ScenarioSpriteSwapAuthoringService.CustomEditorModel customEditor)
        {
            string subtitle = state != null && state.SpriteSwapPicker != null
                ? FormatTarget(state.SpriteSwapPicker.Target)
                : "Character";
            string partLabel = customEditor != null && !string.IsNullOrEmpty(customEditor.CharacterPartLabel)
                ? customEditor.CharacterPartLabel
                : "Part";

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "character_picker_summary",
                Title = "Character Texture",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = new[]
                {
                    Property("Target", subtitle),
                    Property("Editor", "Character Pixel Editor"),
                    Property("Editing", partLabel),
                    Property("Canvas", (customEditor != null ? customEditor.Width : 0) + "x" + (customEditor != null ? customEditor.Height : 0)),
                    Property("Zoom", (customEditor != null ? customEditor.Zoom : 8) + "x"),
                    Property("Stored As", "FamilySetup > Members > Appearance"),
                    Text("Family member visuals use dedicated head, torso, and legs textures instead of the regular sprite-swap catalog. The live character preview updates as you paint.")
                }
            });

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "character_picker_commit",
                Title = "Commit",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerSave,
                        "Save & Close",
                        "Persist the current character texture edit into the scenario pack and close the editor.",
                        true,
                        false,
                        "SV",
                        "Write the edited character texture and update FamilySetup appearance data.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel,
                        "Cancel",
                        "Discard the current character texture draft and restore the previously configured appearance.",
                        true,
                        false,
                        "CL",
                        "Restore the previous character appearance."))
                }
            });

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "character_picker_help",
                Title = "Workflow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                Items = new[]
                {
                    Text("Use the part buttons in the pixel editor to switch between head, torso, and legs. Each part saves independently."),
                    Text("Mouse wheel zooms the canvas. Paint edits individual pixels, Pick samples exact RGBA values, and Select enables rectangular copy/paste."),
                    Text("Saving writes the PNG into the active scenario pack and stores the file paths in the family appearance section of the draft.")
                }
            });

            return new ScenarioAuthoringInspectorDocument
            {
                Title = "Character Editor",
                Subtitle = subtitle,
                HeaderActions = new ScenarioAuthoringInspectorAction[0],
                Sections = sections.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildAdvancedDebugSection(ScenarioAuthoringTarget target)
        {
            if (target == null)
                return null;

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Runtime Kind", target.Kind.ToString()));
            items.Add(Property("Game Object", Safe(target.GameObjectName)));
            items.Add(Property("Transform Path", Safe(target.TransformPath)));
            items.Add(Property("Adapter", Safe(target.AdapterId)));
            items.Add(Property("Scenario Ref", Safe(target.ScenarioReferenceId)));

            GameObject gameObject = ResolveGameObject(target);
            List<string> componentNames = GetComponentNames(gameObject);
            items.Add(Property("Components", componentNames.Count.ToString()));
            if (componentNames.Count > 0)
                items.Add(Text("Attached: " + string.Join(", ", componentNames.ToArray())));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "advanced",
                Title = "Advanced (debug)",
                Expanded = false,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static ObjectPlacement FindObjectPlacement(ScenarioDefinition definition, ScenarioAuthoringTarget target)
        {
            if (definition == null || definition.BunkerEdits == null || definition.BunkerEdits.ObjectPlacements == null || target == null)
                return null;

            GameObject gameObject = ResolveGameObject(target);
            Obj_Base obj = gameObject != null ? gameObject.GetComponent<Obj_Base>() : null;
            int objIndex = ScenarioBunkerDraftService.FindPlacementIndex(definition.BunkerEdits.ObjectPlacements, obj);
            if (objIndex >= 0 && objIndex < definition.BunkerEdits.ObjectPlacements.Count)
                return definition.BunkerEdits.ObjectPlacements[objIndex];

            string reference = target.ScenarioReferenceId;
            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;

                if (!string.IsNullOrEmpty(reference)
                    && (string.Equals(placement.ScenarioObjectId, reference, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(placement.RuntimeBindingKey, reference, StringComparison.OrdinalIgnoreCase)))
                {
                    return placement;
                }
            }

            return null;
        }

        private static string ResolveScenarioObjectId(ScenarioAuthoringTarget target, ObjectPlacement placement, bool hasCapturedPlacement)
        {
            if (placement != null && !string.IsNullOrEmpty(placement.ScenarioObjectId))
                return placement.ScenarioObjectId;
            if (target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId))
                return target.ScenarioReferenceId;
            return hasCapturedPlacement ? "Captured" : "Not captured";
        }

        private static string ResolveDraftStatus(ScenarioAuthoringTarget target, ObjectPlacement placement, bool hasCapturedPlacement)
        {
            if (placement != null)
                return "Authored placement";
            if (hasCapturedPlacement)
                return "Captured";
            if (target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId))
                return "Runtime generated";
            return "Live only";
        }

        private static string FormatStartState(ObjectPlacement placement)
        {
            if (placement == null)
                return "Starts Enabled";

            switch (placement.StartState)
            {
                case ScenarioObjectStartState.StartsDisabled: return "Starts Disabled";
                case ScenarioObjectStartState.StartsHidden: return "Starts Hidden";
                case ScenarioObjectStartState.StartsLocked: return "Starts Locked";
                case ScenarioObjectStartState.AppearsLater: return "Appears Later";
                case ScenarioObjectStartState.RemovedAtStart: return "Removed At Start";
                default: return "Starts Enabled";
            }
        }

        private static bool HasSupport(ScenarioDefinition definition, ObjectPlacement placement)
        {
            if (placement == null)
                return false;
            if (string.IsNullOrEmpty(placement.RequiredFoundationId) && string.IsNullOrEmpty(placement.RequiredBunkerExpansionId))
                return false;
            return HasFoundation(definition, placement.RequiredFoundationId) || HasExpansion(definition, placement.RequiredBunkerExpansionId);
        }

        private static bool HasFoundation(ScenarioDefinition definition, string foundationId)
        {
            if (string.IsNullOrEmpty(foundationId) || definition == null || definition.BunkerGrid == null || definition.BunkerGrid.Foundations == null)
                return false;

            for (int i = 0; i < definition.BunkerGrid.Foundations.Count; i++)
            {
                ScenarioFoundationDefinition foundation = definition.BunkerGrid.Foundations[i];
                if (foundation != null && string.Equals(foundation.Id, foundationId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool HasExpansion(ScenarioDefinition definition, string expansionId)
        {
            if (string.IsNullOrEmpty(expansionId) || definition == null || definition.BunkerGrid == null || definition.BunkerGrid.Expansions == null)
                return false;

            for (int i = 0; i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                if (expansion != null && string.Equals(expansion.Id, expansionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string FriendlyKindLabel(ScenarioAuthoringTargetKind kind)
        {
            switch (kind)
            {
                case ScenarioAuthoringTargetKind.Character: return "Character";
                case ScenarioAuthoringTargetKind.PlaceableObject: return "Interactive Object";
                case ScenarioAuthoringTargetKind.Wall: return "Wall";
                case ScenarioAuthoringTargetKind.Wire: return "Wire";
                case ScenarioAuthoringTargetKind.Light: return "Light";
                case ScenarioAuthoringTargetKind.Vehicle: return "Vehicle";
                case ScenarioAuthoringTargetKind.Room: return "Room";
                case ScenarioAuthoringTargetKind.Tile: return "Shelter Tile";
                case ScenarioAuthoringTargetKind.Background: return "Background";
                case ScenarioAuthoringTargetKind.SceneSprite: return "Scene Sprite";
                case ScenarioAuthoringTargetKind.Unknown: return "Unknown";
                default: return "Object";
            }
        }

        public ScenarioAuthoringInspectorDocument BuildHoverDocument(ScenarioAuthoringState state)
        {
            if (state == null || !state.SelectionModeActive || state.HoveredTarget == null)
                return null;

            ScenarioAuthoringTarget target = state.HoveredTarget;
            return new ScenarioAuthoringInspectorDocument
            {
                Title = target.DisplayName,
                Subtitle = target.Kind.ToString(),
                HeaderActions = new ScenarioAuthoringInspectorAction[0],
                Sections = new[]
                {
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "hover",
                        Title = "Hover",
                        Expanded = true,
                        Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                        Items = new[]
                        {
                            Property("Path", Safe(target.TransformPath)),
                            Property("Adapter", Safe(target.AdapterId)),
                            Text(string.IsNullOrEmpty(target.Description)
                                ? "Click to select this scenario target."
                                : target.Description),
                            Text(target.SupportsReplace
                                ? "Supports inspect and shelter capture workflows."
                                : "Supports inspect workflows.")
                        }
                    }
                }
            };
        }

        public void OpenContextMenu(
            ScenarioAuthoringState state,
            ScenarioAuthoringTarget target,
            ScenarioAuthoringContextMenuService contextMenuService)
        {
            if (contextMenuService == null)
                return;

            if (state == null || target == null)
            {
                contextMenuService.Close();
                return;
            }

            string scopeReason;
            if (!_selectionScopeService.CanSelectTargetForCurrentStage(state, target, out scopeReason))
            {
                state.StatusMessage = scopeReason;
                contextMenuService.Close();
                return;
            }

            Vector3 mouse = UnityEngine.Input.mousePosition;
            float anchorX = mouse.x;
            float anchorY = Screen.height - mouse.y;
            ScenarioAuthoringInspectorAction[] actions = BuildContextMenuActions(state, target);
            contextMenuService.Open(
                Safe(target.DisplayName),
                state.MultiSelection != null && state.MultiSelection.Count > 1
                    ? state.MultiSelection.Count + " placements selected."
                    : Safe(target.Description),
                anchorX,
                anchorY,
                actions);
        }

        private void AppendShellWindowViewModels(
            List<ScenarioAuthoringShellWindowViewModel> windows,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition)
        {
            ScenarioAuthoringWindowDefinition[] definitions = _windowRegistry.GetDefinitions();
            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringWindowDefinition definitionEntry = definitions[i];
                if (definitionEntry == null)
                    continue;

                ScenarioAuthoringWindowState windowState = _layoutService.FindWindow(state, definitionEntry.Id);
                if (windowState == null)
                    continue;

                if (!IsWindowOpen(windowState) && !string.Equals(definitionEntry.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase))
                    continue;

                ScenarioAuthoringShellWindowViewModel window = new ScenarioAuthoringShellWindowViewModel
                {
                    Id = definitionEntry.Id,
                    Title = definitionEntry.Title,
                    Dock = definitionEntry.Dock,
                    Visible = windowState.Visible,
                    Collapsed = windowState.Collapsed,
                    Width = windowState.Width,
                    Height = windowState.Height,
                    HeaderActions = BuildWindowHeaderActions(definitionEntry.Id, windowState, state)
                };
                window.Sections = BuildWindowSections(definitionEntry.Id, state, editorSession, session, definition);
                windows.Add(window);
            }
        }

        private ScenarioAuthoringInspectorSection[] BuildWindowSections(
            string windowId,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session,
            ScenarioDefinition definition)
        {
            switch (windowId)
            {
                case ScenarioAuthoringWindowIds.Scenario:
                    if (state != null && state.ActiveStage == ScenarioStageKind.Test)
                        return BuildTestWindowSections(definition);
                    return BuildScenarioWindowSections(state, editorSession, session);
                case ScenarioAuthoringWindowIds.Layers:
                    return BuildLayerWindowSections();
                case ScenarioAuthoringWindowIds.TilesPalette:
                    return BuildPaletteWindowSections(state, definition);
                case ScenarioAuthoringWindowIds.Inspector:
                    return BuildInspectorShellSections(state, editorSession, definition);
                case ScenarioAuthoringWindowIds.BuildTools:
                    return BuildBuildToolsWindowSections(state, definition);
                case ScenarioAuthoringWindowIds.Triggers:
                    return BuildTriggerWindowSections(definition);
                case ScenarioAuthoringWindowIds.Survivors:
                    return BuildSurvivorWindowSections(definition);
                case ScenarioAuthoringWindowIds.Stockpile:
                    return BuildStockpileWindowSections(definition);
                case ScenarioAuthoringWindowIds.Quests:
                    return BuildQuestWindowSections(definition);
                case ScenarioAuthoringWindowIds.Map:
                    return BuildMapWindowSections(definition);
                case ScenarioAuthoringWindowIds.Publish:
                    return BuildPublishWindowSections(editorSession, definition);
                case ScenarioAuthoringWindowIds.Calendar:
                    return BuildCalendarWindowSections(state, definition);
                default:
                    return new[]
                    {
                        new ScenarioAuthoringInspectorSection
                        {
                            Id = "empty",
                            Title = string.Empty,
                            Expanded = true,
                            Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                            Items = new[] { Text("Window content is not available.") }
                        }
                    };
            }
        }

        private ScenarioAuthoringInspectorSection[] BuildScenarioWindowSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession session)
        {
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_summary",
                    Title = "Scenario",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Property("Draft", Safe(state.ActiveDraftId)),
                        Property("Base Mode", session != null ? session.BaseMode.ToString() : "Unknown"),
                        Property("Simulation", ScenarioAuthoringRuntimeGuards.IsPlaytesting() ? "Running (playtest)" : "Frozen (authoring pause)"),
                        Property("Playtest", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"),
                        Property("Applied To World", editorSession != null && editorSession.HasAppliedToCurrentWorld ? "Yes" : "No"),
                        Property("Dirty Sections", CountDirtyFlags(editorSession).ToString())
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_actions",
                    Title = "Actions",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                    Items = new[]
                    {
                        ActionItem(Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario draft XML.", true, false, "SV")),
                        ActionItem(Action(ScenarioAuthoringActionIds.ActionPlaytest, editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Test" : "Start Test Scenario", "Toggle scenario playtest mode.", true, false, "TS"))
                    }
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildLayerWindowSections()
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
                        Property("Shelter Tiles", "Visible · Locked"),
                        Property("Shelter Objects", "Visible · Locked"),
                        Property("Scene Art", "Visible"),
                        Property("Triggers", "Visible"),
                        Property("Pathing", "Visible"),
                        Property("Regions", "Visible")
                    }
                }
            };
        }

        private ScenarioAuthoringInspectorSection[] BuildPaletteWindowSections(
            ScenarioAuthoringState state,
            ScenarioDefinition definition)
        {
            if (state.ActiveStage == ScenarioStageKind.BunkerInside && state.ActiveTool == ScenarioAuthoringTool.Assets)
            {
                ScenarioAuthoringInspectorSection section = BuildToolSection(
                    state,
                    ScenarioAuthoringTool.Assets,
                    definition,
                    state.SelectedTarget,
                    false,
                    false,
                    null);
                return new[] { section };
            }

            if (state.ActiveStage == ScenarioStageKind.BunkerBackground
                || state.ActiveStage == ScenarioStageKind.BunkerSurface
                || state.ActiveStage == ScenarioStageKind.BunkerInside
                || state.ActiveTool == ScenarioAuthoringTool.Objects
                || state.ActiveTool == ScenarioAuthoringTool.Select
                || state.ActiveTool == ScenarioAuthoringTool.Shelter
                || state.ActiveTool == ScenarioAuthoringTool.Wiring)
            {
                List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
                ScenarioBuildPlacementAuthoringService.StatusModel status = _buildPlacementAuthoringService.GetStatusModel(state, _editorService.CurrentSession);
                sections.Add(BuildPlacementStatusSection(status));

                List<ScenarioBuildPlacementAuthoringService.PaletteSectionModel> paletteSections = _buildPlacementAuthoringService.GetPaletteSections(
                    state,
                    _editorService.CurrentSession);
                for (int i = 0; paletteSections != null && i < paletteSections.Count; i++)
                    sections.Add(BuildPlacementPaletteSection(paletteSections[i]));

                return sections.ToArray();
            }

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "palette",
                    Title = "Tiles Palette",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text("Pick a build tool to see its palette here.")
                    }
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildMapWindowSections(ScenarioDefinition definition)
        {
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "map_stage",
                    Title = "Map",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                    Items = new[]
                    {
                        Text("Map stage is active."),
                        Text("This stage keeps expedition-facing scenario seams separate from bunker authoring."),
                        Text("Current scenario: " + Safe(definition != null ? definition.DisplayName : null))
                    }
                }
            };
        }

        private ScenarioAuthoringInspectorSection[] BuildPublishWindowSections(ScenarioEditorSession editorSession, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> dependencyItems = BuildPublishDependencyItems(definition);
            List<ScenarioAuthoringInspectorItem> compatibilityItems = _modCompatibilityViewModelBuilder.BuildItems(_modDependencyDetector.BuildReport(definition));
            List<ScenarioAuthoringInspectorItem> timelineItems = BuildTimelineItems(definition, GetRuntimeState(), _timelineBuilder);
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_stage",
                    Title = "Publish",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Property("Scenario", Safe(definition != null ? definition.DisplayName : null)),
                        Property("Dirty Sections", CountDirtyFlags(editorSession).ToString()),
                        Property("Version", Safe(definition != null ? definition.Version : null))
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_dependencies",
                    Title = "Dependency Summary",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = dependencyItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_mod_compatibility",
                    Title = "Mod Compatibility",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = compatibilityItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_timeline",
                    Title = "Schedule Timeline",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = timelineItems.ToArray()
                }
            };
        }

        private ScenarioAuthoringInspectorSection[] BuildTestWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> journalItems = BuildRuntimeJournalItems();
            List<ScenarioAuthoringInspectorItem> pendingItems = BuildTimelineItems(definition, GetRuntimeState(), _timelineBuilder);
            List<ScenarioAuthoringInspectorItem> compatibilityItems = _modCompatibilityViewModelBuilder.BuildItems(_modDependencyDetector.BuildReport(definition));
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "runtime_journal",
                    Title = "Runtime Journal",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = journalItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "runtime_pending",
                    Title = "Pending / Blocked Actions",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = pendingItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "runtime_mod_compatibility",
                    Title = "Mod Compatibility",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = compatibilityItems.ToArray()
                }
            };
        }

        private ScenarioAuthoringInspectorSection[] BuildCalendarWindowSections(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            ScenarioRuntimeState runtimeState = GetRuntimeState();
            ScenarioTimelineViewModel model = _timelineViewModelBuilder.Build(_timelineBuilder.BuildDays(definition, runtimeState));
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            List<ScenarioAuthoringInspectorItem> dayItems = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; model != null && model.Days != null && i < model.Days.Length; i++)
            {
                ScenarioTimelineDayViewModel day = model.Days[i];
                dayItems.Add(ActionItem(Action(
                    ScenarioAuthoringActionIds.ActionTimelineDayPrefix + day.Day.ToString(CultureInfo.InvariantCulture),
                    "Day " + day.Day.ToString(CultureInfo.InvariantCulture),
                    day.Count.ToString(CultureInfo.InvariantCulture) + " scheduled item(s).",
                    true,
                    false,
                    day.Badge,
                    day.Categories)));
            }
            if (dayItems.Count == 0)
                dayItems.Add(Text("No scheduled scenario events are currently authored."));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "calendar_days",
                Title = "Calendar",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = dayItems.ToArray()
            });

            string selected = state != null ? state.TimelineSelectionId : null;
            for (int i = 0; model != null && model.Days != null && i < model.Days.Length; i++)
            {
                ScenarioTimelineDayViewModel day = model.Days[i];
                if (selected != null && !string.Equals(selected, day.Day.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
                    continue;

                List<ScenarioAuthoringInspectorItem> entries = new List<ScenarioAuthoringInspectorItem>();
                for (int e = 0; day.Entries != null && e < day.Entries.Length; e++)
                {
                    ScenarioTimelineEntryViewModel entry = day.Entries[e];
                    entries.Add(ActionItem(Action(
                        entry.ActionId,
                        entry.Time + " " + entry.Title,
                        entry.Type + " / " + entry.OwnerStage + " / " + entry.Status,
                        true,
                        entry.Status == "Blocked" || entry.Status == "Failed",
                        StatusBadge(entry.Status),
                        string.IsNullOrEmpty(entry.Warning) ? entry.OwnerStage : entry.Warning)));
                }

                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "calendar_day_" + day.Day.ToString(CultureInfo.InvariantCulture),
                    Title = "Day " + day.Day.ToString(CultureInfo.InvariantCulture),
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = entries.ToArray()
                });
            }

            return sections.ToArray();
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementStatusSection(ScenarioBuildPlacementAuthoringService.StatusModel model)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Mode", model != null && model.PlacementActive ? "Placement Active" : "Ready"));
            if (model != null && !string.IsNullOrEmpty(model.Guidance))
                items.Add(Text(model.Guidance));
            if (model != null && !string.IsNullOrEmpty(model.Detail))
                items.Add(Text(model.Detail));
            if (model != null && model.CanCancel)
            {
                items.Add(ActionItem(Action(
                    ScenarioAuthoringActionIds.ActionBuildPlacementCancel,
                    "Cancel Placement",
                    "Stop the current build preview without committing it.",
                    true,
                    false,
                    "CX",
                    "Cancel the active ghost preview.")));
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "build_palette_status",
                Title = model != null && !string.IsNullOrEmpty(model.Title) ? model.Title : "Build Palette",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
        }

        private static ScenarioAuthoringInspectorSection BuildPlacementPaletteSection(ScenarioBuildPlacementAuthoringService.PaletteSectionModel model)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int count = model != null && model.Entries != null ? model.Entries.Count : 0;
            items.Add(Property("Count", count.ToString()));
            if (count == 0)
            {
                items.Add(Text(model != null ? Safe(model.EmptyMessage) : "No palette entries are available."));
            }
            else
            {
                for (int i = 0; model != null && model.Entries != null && i < model.Entries.Count; i++)
                {
                    ScenarioBuildPlacementAuthoringService.PaletteEntryModel entry = model.Entries[i];
                    if (entry == null)
                        continue;

                    items.Add(ActionItem(Action(
                        entry.ActionId,
                        CleanCandidateLabel(entry.Label),
                        entry.Hint,
                        entry.Enabled,
                        entry.Active,
                        "PL",
                        entry.Source,
                        entry.Badge,
                        entry.Preview)));
                }
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = model != null ? model.Id : "build_palette",
                Title = model != null ? Safe(model.Title) : "Palette",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.CandidateGrid,
                Items = items.ToArray()
            };
        }

        private ScenarioAuthoringInspectorSection[] BuildInspectorShellSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            ScenarioAuthoringInspectorDocument document = BuildInspectorDocument(state, editorSession);
            if (document == null || document.Sections == null || document.Sections.Length == 0)
            {
                return new[]
                {
                    new ScenarioAuthoringInspectorSection
                    {
                        Id = "empty",
                        Title = "Inspector",
                        Expanded = true,
                        Layout = ScenarioAuthoringInspectorSectionLayout.NoteList,
                        Items = new[] { Text("Select a shelter tile, object, survivor, or authored sprite to inspect it.") }
                    }
                };
            }

            return document.Sections;
        }

        private ScenarioAuthoringInspectorItem[] BuildInteractionItems(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            ScenarioAuthoringInspectorAction[] actions = BuildContextMenuActions(state, target);
            if (actions == null || actions.Length == 0)
                return new[] { Text("No contextual actions are available for this target.") };

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; i < actions.Length; i++)
                items.Add(ActionItem(actions[i]));
            return items.ToArray();
        }

        private ScenarioAuthoringInspectorSection[] BuildBuildToolsWindowSections(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            if (state != null && state.ActiveTool == ScenarioAuthoringTool.Assets)
            {
                sections.Add(BuildToolSection(
                    state,
                    state.ActiveTool,
                    definition,
                    state.SelectedTarget,
                    false,
                    false,
                    null));

                ScenarioAuthoringTarget target = state.SelectedTarget ?? state.HoveredTarget;
                List<ScenarioAuthoringInspectorSection> assetSections = BuildAssetSections(state, _editorService.CurrentSession, target);
                for (int i = 0; i < assetSections.Count; i++)
                    sections.Add(assetSections[i]);

                return sections.ToArray();
            }

            sections.Add(BuildToolPickerSection(state.ActiveTool));
            sections.Add(BuildToolSection(
                state,
                state.ActiveTool,
                definition,
                state.SelectedTarget,
                false,
                false,
                null));
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "snap",
                Title = "Snap",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = new[]
                {
                    Property("Grid", state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "On" : "Off"),
                    Property("Snap", state.Settings != null && state.Settings.GetBool("visuals.snap_to_grid", true) ? "On" : "Off"),
                    Property("Scroll", state.Settings != null ? state.Settings.GetFloat("input.scroll_speed", 1f).ToString("0.00", CultureInfo.InvariantCulture) + "x" : "1.00x")
                }
            });
            sections.Add(BuildBunkerRuntimeSection(definition, state));
            return sections.ToArray();
        }

        private static ScenarioAuthoringInspectorSection[] BuildTriggerWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> triggerItems = new List<ScenarioAuthoringInspectorItem>();
            if (definition != null && definition.TriggersAndEvents != null)
            {
                for (int i = 0; i < definition.TriggersAndEvents.Triggers.Count; i++)
                {
                    TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                    triggerItems.Add(Property(string.IsNullOrEmpty(trigger.Id) ? ("Trigger " + (i + 1)) : trigger.Id, trigger != null ? trigger.Type : "Unknown"));
                }
            }

            if (triggerItems.Count == 0)
                triggerItems.Add(Text("No authored triggers are in this draft yet."));

            List<ScenarioAuthoringInspectorItem> weatherItems = new List<ScenarioAuthoringInspectorItem>();
            weatherItems.Add(Property("Current Weather", GetCurrentWeatherSummary()));
            weatherItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleAdd, "Add Weather Event", "Schedule a weather state for a specific day and hour.", true, true, "WE")));
            if (definition != null && definition.TriggersAndEvents != null)
            {
                for (int i = 0; i < definition.TriggersAndEvents.WeatherEvents.Count; i++)
                    AddWeatherEventItems(weatherItems, definition.TriggersAndEvents.WeatherEvents[i], i);
            }

            List<ScenarioAuthoringInspectorItem> actionItems = BuildScheduledActionItems(definition);
            List<ScenarioAuthoringInspectorItem> gateItems = BuildGateItems(definition);

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "triggers",
                    Title = "Triggers / Events",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = triggerItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "weather_events",
                    Title = "Weather Events",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = weatherItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scheduled_actions",
                    Title = "Scheduled Actions",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = actionItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scenario_gates",
                    Title = "Scenario Gates / Flags",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = gateItems.ToArray()
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildSurvivorWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> currentItems = BuildLiveSurvivorItems();
            currentItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureFamily, "Capture Current Survivors", "Snapshot every current live family member into the starting survivor list.", true, true, "FM")));

            List<ScenarioAuthoringInspectorItem> startingItems = new List<ScenarioAuthoringInspectorItem>();
            if (definition != null && definition.FamilySetup != null)
            {
                for (int i = 0; i < definition.FamilySetup.Members.Count; i++)
                {
                    FamilyMemberConfig member = definition.FamilySetup.Members[i];
                    string role = member != null && member.Traits != null && member.Traits.Count > 0 ? member.Traits[0] : "Survivor";
                    startingItems.Add(Property(Safe(member != null ? member.Name : "Unknown"), role));
                }
            }

            if (startingItems.Count == 0)
                startingItems.Add(Text("No starting survivors have been captured into this draft."));

            List<ScenarioAuthoringInspectorItem> futureItems = new List<ScenarioAuthoringInspectorItem>();
            futureItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorAdd, "Add Future Survivor", "Create a survivor who arrives or asks to join at a scheduled day and hour.", true, true, "FS")));
            if (definition != null && definition.FamilySetup != null)
            {
                for (int i = 0; i < definition.FamilySetup.FutureSurvivors.Count; i++)
                    AddFutureSurvivorItems(futureItems, definition.FamilySetup.FutureSurvivors[i], i);
            }
            if (futureItems.Count == 1)
                futureItems.Add(Text("No future survivor arrivals have been authored yet."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "current_survivors",
                    Title = "Current Survivors",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = currentItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "starting_survivors",
                    Title = "Starting Survivors",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = startingItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "future_survivors",
                    Title = "Future Survivors",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = futureItems.ToArray()
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildStockpileWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> liveItems = BuildLiveInventoryItems();
            liveItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureInventory, "Capture Current Stockpile", "Snapshot every current shelter item stack into the starting stockpile.", true, true, "IV")));

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (definition != null && definition.StartingInventory != null)
            {
                for (int i = 0; i < definition.StartingInventory.Items.Count; i++)
                {
                    ItemEntry entry = definition.StartingInventory.Items[i];
                    items.Add(Property(Safe(entry != null ? entry.ItemId : "Item"), entry != null ? entry.Quantity.ToString() : "0"));
                }
            }

            if (items.Count == 0)
                items.Add(Text("No starting stockpile has been captured into this draft."));

            List<ScenarioAuthoringInspectorItem> scheduledItems = new List<ScenarioAuthoringInspectorItem>();
            scheduledItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleAdd, "Schedule Add", "Add an item stack at a specific day and hour.", true, true, "A+")));
            scheduledItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleRemove, "Schedule Remove", "Remove an item stack at a specific day and hour.", true, false, "R-")));
            if (definition != null && definition.StartingInventory != null)
            {
                for (int i = 0; i < definition.StartingInventory.ScheduledChanges.Count; i++)
                    AddInventoryChangeItems(scheduledItems, definition.StartingInventory.ScheduledChanges[i], i);
            }
            if (scheduledItems.Count == 2)
                scheduledItems.Add(Text("No timed stockpile changes have been authored yet."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "current_stockpile",
                    Title = "Current Shelter Items",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = liveItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "starting_stockpile",
                    Title = "Starting Items",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = items.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "scheduled_stockpile",
                    Title = "Timed Item Changes",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = scheduledItems.ToArray()
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildQuestWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> liveItems = BuildLiveQuestItems();
            List<ScenarioAuthoringInspectorItem> authoredItems = new List<ScenarioAuthoringInspectorItem>();
            authoredItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionQuestCaptureActive, "Capture Active Quests", "Persist every current active QuestManager quest into this draft.", true, true, "QC")));
            authoredItems.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionQuestScheduleAdd, "Add Scheduled Quest", "Create a quest entry that starts on a specific day and hour.", true, true, "QS")));
            if (definition != null && definition.Quests != null)
            {
                for (int i = 0; i < definition.Quests.Quests.Count; i++)
                    AddQuestItems(authoredItems, definition.Quests.Quests[i], i);
            }
            if (authoredItems.Count == 2)
                authoredItems.Add(Text("No authored quest entries are in this draft yet."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "live_quests",
                    Title = "Current Active Quests",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = liveItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "authored_quests",
                    Title = "Authored Quest Schedule",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = authoredItems.ToArray()
                }
            };
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLiveSurvivorItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            FamilyManager manager = FamilyManager.Instance;
            List<FamilyMember> members = manager != null ? manager.GetAllFamilyMembers() : null;
            for (int i = 0; members != null && i < members.Count; i++)
            {
                FamilyMember member = members[i];
                if (member == null)
                    continue;

                string status = member.isDead ? "Dead" : member.isAway ? "Away" : member.IsUnconscious ? "Unconscious" : member.isCatatonic ? "Catatonic" : "Active";
                items.Add(Property(Safe(member.firstName), status));
            }

            if (items.Count == 0)
                items.Add(Text("No live survivors are available from FamilyManager."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLiveInventoryItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            InventoryManager manager = InventoryManager.Instance;
            List<ItemStack> stacks = manager != null ? manager.GetItems() : null;
            int total = 0;
            for (int i = 0; stacks != null && i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.m_type == ItemManager.ItemType.Undefined || stack.m_count <= 0)
                    continue;

                items.Add(Property(stack.m_type.ToString(), stack.m_count.ToString()));
                total += stack.m_count;
            }

            items.Insert(0, Property("Total Items", total.ToString()));
            if (items.Count == 1)
                items.Add(Text("No current shelter inventory items are available from InventoryManager."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildLiveQuestItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            QuestManager manager = QuestManager.instance;
            List<QuestInstance> quests = manager != null ? manager.GetCurrentQuests(true, true, true) : null;
            for (int i = 0; quests != null && i < quests.Count; i++)
            {
                QuestInstance quest = quests[i];
                if (quest == null || quest.definition == null)
                    continue;

                string state = quest.state.ToString();
                if (quest.definition.IsScenario() && quest.stage != null)
                    state += " / " + quest.stage.id;
                items.Add(Property(Safe(quest.definition.id), state));
            }

            if (items.Count == 0)
                items.Add(Text("No current quest or scenario instances are active in QuestManager."));
            return items;
        }

        private static void AddFutureSurvivorItems(List<ScenarioAuthoringInspectorItem> items, FutureSurvivorDefinition survivor, int index)
        {
            if (items == null || survivor == null)
                return;

            string name = survivor.Survivor != null ? survivor.Survivor.Name : survivor.Id;
            items.Add(Property(Safe(name), (survivor.AskToJoin ? "Ask to join (runtime unsupported)" : "Auto join") + " - " + FormatSchedule(survivor.Arrival)));
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionFutureSurvivorDayPrefix, ScenarioAuthoringActionIds.ActionFutureSurvivorHourPrefix, index);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorToggleAskPrefix + index.ToString(), "Toggle Join Mode", "Auto join is the only runtime-supported arrival mode in this pass.", true, survivor.AskToJoin, "AJ")));
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionFutureSurvivorRemovePrefix + index.ToString(), "Remove Future Survivor", "Remove this future survivor arrival.", true, false, "RM")));
        }

        private static void AddInventoryChangeItems(List<ScenarioAuthoringInspectorItem> items, TimedInventoryChangeDefinition change, int index)
        {
            if (items == null || change == null)
                return;

            items.Add(Property(change.Kind.ToString() + " " + Safe(change.ItemId), "x" + change.Quantity + " - " + FormatSchedule(change.When)));
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionInventoryScheduleDayPrefix, ScenarioAuthoringActionIds.ActionInventoryScheduleHourPrefix, index);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionInventoryScheduleDeletePrefix + index.ToString(), "Remove Timed Item Change", "Remove this timed stockpile change.", true, false, "RM")));
        }

        private static void AddWeatherEventItems(List<ScenarioAuthoringInspectorItem> items, WeatherEventDefinition weather, int index)
        {
            if (items == null || weather == null)
                return;

            items.Add(Property(Safe(weather.WeatherState), FormatSchedule(weather.When)));
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionWeatherScheduleDayPrefix, ScenarioAuthoringActionIds.ActionWeatherScheduleHourPrefix, index);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionWeatherScheduleDeletePrefix + index.ToString(), "Remove Weather Event", "Remove this scheduled weather event.", true, false, "RM")));
        }

        private static void AddQuestItems(List<ScenarioAuthoringInspectorItem> items, QuestDefinition quest, int index)
        {
            if (items == null || quest == null)
                return;

            string title = !string.IsNullOrEmpty(quest.Title) ? quest.Title : quest.Id;
            string trigger = !string.IsNullOrEmpty(quest.StartTriggerId) ? "trigger " + quest.StartTriggerId : FormatSchedule(quest.ScheduledStart);
            items.Add(Property(Safe(title), trigger));
            AddScheduleActions(items, ScenarioAuthoringActionIds.ActionQuestScheduleDayPrefix, ScenarioAuthoringActionIds.ActionQuestScheduleHourPrefix, index);
            items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionQuestScheduleDeletePrefix + index.ToString(), "Remove Quest", "Remove this authored quest entry.", true, false, "RM")));
        }

        private static void AddScheduleActions(List<ScenarioAuthoringInspectorItem> items, string dayPrefix, string hourPrefix, int index)
        {
            items.Add(ActionItem(Action(dayPrefix + index.ToString() + ".1", "Day +", "Move this scheduled entry one day later.", true, false, "D+")));
            items.Add(ActionItem(Action(dayPrefix + index.ToString() + ".-1", "Day -", "Move this scheduled entry one day earlier.", true, false, "D-")));
            items.Add(ActionItem(Action(hourPrefix + index.ToString() + ".1", "Hour +", "Move this scheduled entry one hour later.", true, false, "H+")));
            items.Add(ActionItem(Action(hourPrefix + index.ToString() + ".-1", "Hour -", "Move this scheduled entry one hour earlier.", true, false, "H-")));
        }

        private static string GetCurrentWeatherSummary()
        {
            WeatherManager manager = WeatherManager.Instance;
            if (manager == null)
                return "WeatherManager unavailable";
            return manager.currentState + " / day " + manager.currentDay;
        }

        private static string FormatSchedule(ScenarioScheduleTime time)
        {
            if (time == null)
                return "unscheduled";
            return "day " + time.Day + " " + time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2");
        }

        private static ScenarioAuthoringInspectorSection BuildBunkerRuntimeSection(ScenarioDefinition definition, ScenarioAuthoringState state)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioBunkerGridDefinition grid = definition != null ? definition.BunkerGrid : null;
            items.Add(Property("Foundations", grid != null && grid.Foundations != null ? grid.Foundations.Count.ToString() : "0"));
            items.Add(Property("Cells", grid != null && grid.Cells != null ? grid.Cells.Count.ToString() : "0"));
            items.Add(Property("Expansions", grid != null && grid.Expansions != null ? grid.Expansions.Count.ToString() : "0"));
            items.Add(Property("Boundaries", grid != null && grid.Boundaries != null ? grid.Boundaries.Count.ToString() : "0"));
            if (state != null && state.SelectedTarget != null)
                items.Add(Property("Selected Object", Safe(state.SelectedTarget.DisplayName)));

            for (int i = 0; definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count && i < 6; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;
                string id = !string.IsNullOrEmpty(placement.ScenarioObjectId) ? placement.ScenarioObjectId : "object_" + (i + 1).ToString();
                string dependency = !string.IsNullOrEmpty(placement.RequiredFoundationId) ? "foundation " + placement.RequiredFoundationId : !string.IsNullOrEmpty(placement.RequiredBunkerExpansionId) ? "expansion " + placement.RequiredBunkerExpansionId : "no support id";
                items.Add(Property(id, placement.StartState + " / " + dependency));
            }

            if (items.Count == 4)
                items.Add(Text("No authored object support dependencies have been captured yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "bunker_runtime_model",
                Title = "Bunker Runtime Model",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                Items = items.ToArray()
            };
        }

        private static List<ScenarioAuthoringInspectorItem> BuildGateItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; definition != null && definition.Gates != null && i < definition.Gates.Count; i++)
            {
                ScenarioGateDefinition gate = definition.Gates[i];
                if (gate == null)
                    continue;
                items.Add(Property(Safe(gate.Id), CountConditions(gate.Conditions).ToString() + " condition(s)"));
            }
            if (items.Count == 0)
                items.Add(Text("No shared gates or scenario flags have been authored yet."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildScheduledActionItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                if (action == null)
                    continue;
                items.Add(Property(Safe(action.Id), Safe(action.ActionType) + " / " + FormatSchedule(action.DueTime)));
            }
            if (items.Count == 0)
                items.Add(Text("No shared scheduled actions have been authored yet. Legacy schedules are converted at runtime."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildRuntimeJournalItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioRuntimeState state = GetRuntimeState();

            items.Add(Property("Scenario", Safe(state != null ? state.ScenarioId : null)));
            items.Add(Property("Binding", Safe(state != null ? state.RuntimeBindingId : null)));
            items.Add(Property("Last Processed", state != null ? "day " + state.LastProcessedDay + " " + state.LastProcessedHour.ToString("D2") + ":" + state.LastProcessedMinute.ToString("D2") : "None"));
            int count = state != null && state.ExecutedActions != null ? state.ExecutedActions.Count : 0;
            items.Add(Property("Executed Actions", count.ToString()));
            for (int i = 0; state != null && state.ExecutedActions != null && i < state.ExecutedActions.Count && i < 8; i++)
            {
                ScenarioExecutedActionRecord record = state.ExecutedActions[i];
                if (record != null)
                    items.Add(Property(Safe(record.ActionKey), record.Status + " / day " + record.FiredDay + " " + record.FiredHour.ToString("D2") + ":" + record.FiredMinute.ToString("D2")));
            }
            return items;
        }

        private static ScenarioRuntimeState GetRuntimeState()
        {
            try
            {
                ScenarioRuntimeStateService service = ScenarioCompositionRoot.Resolve<ScenarioRuntimeStateService>();
                return service != null ? service.State : null;
            }
            catch
            {
                return null;
            }
        }

        private static List<ScenarioAuthoringInspectorItem> BuildPublishDependencyItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int objectCount = definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null ? definition.BunkerEdits.ObjectPlacements.Count : 0;
            int foundationCount = definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Foundations != null ? definition.BunkerGrid.Foundations.Count : 0;
            int expansionCount = definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null ? definition.BunkerGrid.Expansions.Count : 0;
            int gateCount = definition != null && definition.Gates != null ? definition.Gates.Count : 0;
            items.Add(Property("Objects", objectCount.ToString()));
            items.Add(Property("Foundations", foundationCount.ToString()));
            items.Add(Property("Expansions", expansionCount.ToString()));
            items.Add(Property("Gates", gateCount.ToString()));
            items.Add(Property("Runtime Compatibility", "Shared schedule journal required"));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildTimelineItems(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, ScenarioTimelineBuilder timelineBuilder)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioTimelineEntry> entries = timelineBuilder != null ? timelineBuilder.BuildEntries(definition, runtimeState) : new List<ScenarioTimelineEntry>();
            for (int i = 0; entries != null && i < entries.Count && i < 12; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                if (entry != null)
                    items.Add(Property("Day " + entry.When.Day + " " + Safe(entry.Title), FormatSchedule(entry.When) + " / " + entry.Kind + " / " + entry.Status));
            }
            if (items.Count == 0)
                items.Add(Text("No scheduled timeline entries are authored yet."));
            return items;
        }

        private static string StatusBadge(string status)
        {
            if (string.Equals(status, "Fired", StringComparison.OrdinalIgnoreCase))
                return "OK";
            if (string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase))
                return "BL";
            if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                return "ER";
            return "PN";
        }

        private static int CountConditions(ScenarioConditionGroup group)
        {
            int count = 0;
            if (group != null && group.Conditions != null)
                count += group.Conditions.Count;
            for (int i = 0; group != null && group.Groups != null && i < group.Groups.Count; i++)
                count += CountConditions(group.Groups[i]);
            return count;
        }

        private ScenarioAuthoringInspectorAction[] BuildWindowHeaderActions(
            string windowId,
            ScenarioAuthoringWindowState windowState,
            ScenarioAuthoringState state)
        {
            if (windowState == null)
                return new ScenarioAuthoringInspectorAction[0];

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            actions.Add(Action(ScenarioAuthoringActionIds.ActionWindowCollapsePrefix + windowId, "_", "Collapse this panel into the Windows list.", true, false, "CL"));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionWindowTogglePrefix + windowId, "x", "Hide this panel.", true, false, "HD"));
            return actions.ToArray();
        }


        private ScenarioAuthoringInspectorAction[] BuildWindowMenuActions(ScenarioAuthoringState state)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            ScenarioAuthoringWindowDefinition[] definitions = _windowRegistry.GetDefinitions();
            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringWindowDefinition definition = definitions[i];
                if (definition == null || string.Equals(definition.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase))
                    continue;

                ScenarioAuthoringWindowState windowState = _layoutService.FindWindow(state, definition.Id);
                bool open = IsWindowOpen(windowState);
                actions.Add(Action(
                    ScenarioAuthoringActionIds.ActionWindowTogglePrefix + definition.Id,
                    (open ? "✓ " : "  ") + definition.Title,
                    (open ? "Hide " : "Show ") + definition.Title + ".",
                    true,
                    open));
            }

            return actions.ToArray();
        }

        private ScenarioAuthoringSettingsViewModel BuildSettingsViewModel(ScenarioAuthoringState state)
        {
            ScenarioAuthoringSettingDefinition[] definitions = _settingsService.GetDefinitions();
            List<ScenarioAuthoringSettingsSectionViewModel> sections = new List<ScenarioAuthoringSettingsSectionViewModel>();
            Dictionary<string, List<ScenarioAuthoringSettingsItemViewModel>> bySection = new Dictionary<string, List<ScenarioAuthoringSettingsItemViewModel>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < definitions.Length; i++)
            {
                ScenarioAuthoringSettingDefinition definition = definitions[i];
                if (definition == null)
                    continue;

                List<ScenarioAuthoringSettingsItemViewModel> items;
                if (!bySection.TryGetValue(definition.Section, out items))
                {
                    items = new List<ScenarioAuthoringSettingsItemViewModel>();
                    bySection[definition.Section] = items;
                }

                string value = state.Settings != null ? state.Settings.Get(definition.Id, definition.DefaultValue) : definition.DefaultValue;
                items.Add(new ScenarioAuthoringSettingsItemViewModel
                {
                    Id = definition.Id,
                    Label = definition.Label,
                    Description = definition.Description,
                    ValueText = value,
                    Kind = definition.Kind,
                    BoolValue = state.Settings != null && state.Settings.GetBool(definition.Id, string.Equals(definition.DefaultValue, "true", StringComparison.OrdinalIgnoreCase)),
                    Enabled = definition.Kind != ScenarioAuthoringSettingKind.ReadOnly,
                    CanIncrease = definition.Kind == ScenarioAuthoringSettingKind.Float || definition.Kind == ScenarioAuthoringSettingKind.Integer,
                    CanDecrease = definition.Kind == ScenarioAuthoringSettingKind.Float || definition.Kind == ScenarioAuthoringSettingKind.Integer,
                    ChoiceLabels = definition.ChoiceLabels,
                    ChoiceValues = definition.ChoiceValues,
                    SelectedChoiceIndex = ResolveChoiceIndex(definition, value)
                });
            }

            foreach (KeyValuePair<string, List<ScenarioAuthoringSettingsItemViewModel>> pair in bySection)
            {
                sections.Add(new ScenarioAuthoringSettingsSectionViewModel
                {
                    Id = pair.Key.ToLowerInvariant(),
                    Title = pair.Key,
                    Items = pair.Value.ToArray()
                });
            }

            sections.Sort(delegate(ScenarioAuthoringSettingsSectionViewModel left, ScenarioAuthoringSettingsSectionViewModel right)
            {
                return string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
            });

            return new ScenarioAuthoringSettingsViewModel
            {
                Title = "Editor Settings",
                Subtitle = "Shell, layout, input, visuals, sprite tools, and debug preferences.",
                HeaderActions = new[]
                {
                    Action(ScenarioAuthoringActionIds.ActionShellSettingsReset, "Reset Defaults", "Restore default editor settings.", true, false),
                    Action(ScenarioAuthoringActionIds.ActionShellCloseSettings, "Close", "Close editor settings.", true, false)
                },
                Sections = sections.ToArray()
            };
        }

        private static int ResolveChoiceIndex(ScenarioAuthoringSettingDefinition definition, string value)
        {
            if (definition == null || definition.ChoiceValues == null)
                return -1;

            for (int i = 0; i < definition.ChoiceValues.Length; i++)
            {
                if (string.Equals(definition.ChoiceValues[i], value, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private static string[] BuildStatusEntries(ScenarioAuthoringState state, ScenarioEditorSession editorSession, ScenarioAuthoringSession session)
        {
            return new[]
            {
                state.StatusMessage ?? string.Empty,
                "Mode: " + state.ActiveShellTab,
                "Grid: " + (state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "On" : "Off"),
                "Snap: " + (state.Settings != null && state.Settings.GetBool("visuals.snap_to_grid", true) ? "On" : "Off"),
                "Playtest: " + (editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"),
                "Draft: " + (session != null ? session.DraftId : Safe(state.ActiveDraftId))
            };
        }

        private static bool IsWindowOpen(ScenarioAuthoringWindowState windowState)
        {
            return windowState != null && windowState.Visible && !windowState.Collapsed;
        }

        private ScenarioAuthoringInspectorAction[] BuildContextMenuActions(ScenarioAuthoringState state, ScenarioAuthoringTarget target)
        {
            if (state == null || target == null)
                return new ScenarioAuthoringInspectorAction[0];

            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            bool scopeAllowed = _selectionScopeService.CanSelectTargetForCurrentStage(state, target);
            actions.Add(Action(ScenarioAuthoringActionIds.ActionShellShow, "Inspect Placement", "Open the inspector for the selected target.", true, false));

            if (target.SupportsReplace)
            {
                actions.Add(Action(ScenarioAuthoringActionIds.ActionToolAssets, "Replace Look...", "Switch to art workflow for the selected target.", scopeAllowed, false));
                actions.Add(Action(ScenarioAuthoringActionIds.ActionToolShelter, "Apply Feature", "Switch to build workflow for the selected target.", scopeAllowed, false));
            }

            string captureReason;
            if (scopeAllowed && _captureService.CanCaptureTarget(target, out captureReason))
                actions.Add(Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Mark Placement", "Capture the selected live placement into the scenario.", true, false));

            if (!string.IsNullOrEmpty(target.ScenarioReferenceId))
            {
                actions.Add(Action(ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove, "Remove Placement", "Remove the authored placement reference.", scopeAllowed, false));
                actions.Add(Action(ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Duplicate Placement", "Copy the selected target's sprite swap or placement.", scopeAllowed, false));
            }

            if (target.SupportsReplace)
                actions.Add(Action(ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Copy Tile", "Copy the selected target's current authored look.", scopeAllowed, false));

            actions.Add(Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current authoring selection.", true, false));
            return actions.ToArray();
        }

        private static string FormatClockTime()
        {
            try
            {
                int hours = GameTime.Hour;
                int minutes = GameTime.Minute;
                return hours.ToString("00") + ":" + minutes.ToString("00");
            }
            catch
            {
                return DateTime.Now.ToString("HH:mm");
            }
        }

        private static ScenarioAuthoringInspectorAction[] BuildHeaderActions(ScenarioEditorSession editorSession, bool hasSelection)
        {
            List<ScenarioAuthoringInspectorAction> actions = new List<ScenarioAuthoringInspectorAction>();
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSave, "Save", "Persist the current scenario draft XML.", true, true, "SV", "Write the current draft to scenario.xml."));
            actions.Add(Action(
                ScenarioAuthoringActionIds.ActionPlaytest,
                editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Playtest" : "Playtest",
                "Toggle scenario playtest mode.",
                true,
                true,
                "PL",
                editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting
                    ? "End playtest and restore frozen authoring."
                    : "Start a live playtest from the current draft."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and release scene ownership.", true, false, "EX", "Leave the scenario editor."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current selected target.", hasSelection, false, "CL", "Drop the current target selection."));
            actions.Add(Action(ScenarioAuthoringActionIds.ActionConvertToNormal, "Convert Save", "Convert the current scenario-bound save into a normal save.", true, false, "CV", "Detach this save from the scenario editor."));
            return actions.ToArray();
        }

        private static ScenarioAuthoringInspectorAction Action(
            string id,
            string label,
            string hint,
            bool enabled,
            bool emphasized,
            string iconText = null,
            string detail = null,
            string badge = null,
            Sprite previewSprite = null)
        {
            return new ScenarioAuthoringInspectorAction
            {
                Id = id,
                Label = label,
                Hint = hint,
                Detail = detail,
                Badge = badge,
                IconText = iconText,
                PreviewSprite = previewSprite,
                Enabled = enabled,
                Emphasized = emphasized
            };
        }

        private static ScenarioAuthoringInspectorItem Text(
            string value,
            string detail = null,
            string badge = null,
            string iconText = null,
            Sprite previewSprite = null,
            bool emphasized = false)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Text,
                Value = value,
                Detail = detail,
                Badge = badge,
                IconText = iconText,
                PreviewSprite = previewSprite,
                Emphasized = emphasized
            };
        }

        private static ScenarioAuthoringInspectorItem Property(
            string label,
            string value,
            string detail = null,
            string badge = null,
            string iconText = null,
            Sprite previewSprite = null,
            bool emphasized = false)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Property,
                Label = label,
                Value = value,
                Detail = detail,
                Badge = badge,
                IconText = iconText,
                PreviewSprite = previewSprite,
                Emphasized = emphasized
            };
        }

        private static ScenarioAuthoringInspectorItem ActionItem(ScenarioAuthoringInspectorAction action)
        {
            return new ScenarioAuthoringInspectorItem
            {
                Kind = ScenarioAuthoringInspectorItemKind.Action,
                Action = action
            };
        }

        private static string FormatDraftStorage(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return "<none>";

            try
            {
                string directory = Path.GetDirectoryName(scenarioFilePath);
                if (string.IsNullOrEmpty(directory))
                    return "<none>";

                string leaf = Path.GetFileName(directory);
                return string.IsNullOrEmpty(leaf) ? directory : leaf;
            }
            catch
            {
                return scenarioFilePath;
            }
        }

        private static string FormatScenarioFileName(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return "<none>";

            try
            {
                string fileName = Path.GetFileName(scenarioFilePath);
                return string.IsNullOrEmpty(fileName) ? scenarioFilePath : fileName;
            }
            catch
            {
                return scenarioFilePath;
            }
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }

        private static string FormatTarget(ScenarioAuthoringTarget target)
        {
            return target == null ? "<none>" : (target.DisplayName + " [" + target.Kind + "]");
        }

        private static GameObject ResolveGameObject(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.gameObject : null;
        }

        private static List<string> GetComponentNames(GameObject gameObject)
        {
            List<string> names = new List<string>();
            if (gameObject == null)
                return names;

            Component[] components = gameObject.GetComponents<Component>();
            int componentCount = 0;
            for (int i = 0; components != null && i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                componentCount++;
                string name = component.GetType().Name;
                if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                    names.Add(name);
            }

            if (names.Count > 8)
            {
                int hiddenCount = Math.Max(0, componentCount - 8);
                names = names.GetRange(0, 8);
                if (hiddenCount > 0)
                    names.Add("+" + hiddenCount + " more");
            }

            return names;
        }

        private static int CountLikelyTriggerReferences(ScenarioDefinition definition, ScenarioAuthoringTarget target)
        {
            if (definition == null || definition.TriggersAndEvents == null || target == null)
                return 0;

            int count = 0;
            for (int i = 0; definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
            {
                TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                if (TriggerLikelyReferencesTarget(trigger, target))
                    count++;
            }

            return count;
        }

        private static bool TriggerLikelyReferencesTarget(TriggerDef trigger, ScenarioAuthoringTarget target)
        {
            if (trigger == null)
                return false;

            if (StringEquals(trigger.Id, target.ScenarioReferenceId)
                || StringContains(trigger.Id, target.TransformPath)
                || StringContains(trigger.Id, target.GameObjectName))
            {
                return true;
            }

            for (int i = 0; trigger.Properties != null && i < trigger.Properties.Count; i++)
            {
                ScenarioProperty property = trigger.Properties[i];
                string key = property != null ? property.Key : null;
                string value = property != null ? property.Value : null;
                if (StringEquals(value, target.ScenarioReferenceId)
                    || StringEquals(value, target.TransformPath)
                    || StringEquals(value, target.GameObjectName)
                    || StringContains(value, target.ScenarioReferenceId)
                    || StringContains(value, target.TransformPath)
                    || StringContains(value, target.GameObjectName)
                    || StringContains(key, target.ScenarioReferenceId)
                    || StringContains(key, target.TransformPath)
                    || StringContains(key, target.GameObjectName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StringEquals(string left, string right)
        {
            return !string.IsNullOrEmpty(left)
                && !string.IsNullOrEmpty(right)
                && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StringContains(string value, string token)
        {
            return !string.IsNullOrEmpty(value)
                && !string.IsNullOrEmpty(token)
                && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
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

        private static string CleanCandidateLabel(string label)
        {
            return string.IsNullOrEmpty(label) ? "<sprite>" : label;
        }

        private static string BuildCandidateBadge(ScenarioSpriteCatalogService.SpriteCandidate candidate)
        {
            if (candidate == null)
                return null;

            if (candidate.SourceKind == ScenarioSpriteCatalogService.SpriteCandidateSourceKind.ScenarioCustom)
                return "MOD";

            return "LIVE";
        }

        private static string BuildSpriteCandidateBadge(
            ScenarioSpriteCatalogService.SpriteCandidate candidate,
            bool saved,
            bool previewed)
        {
            if (saved && previewed)
                return "SAVED / PREVIEW";
            if (previewed)
                return "PREVIEW";
            if (saved)
                return "SAVED";
            return BuildCandidateBadge(candidate);
        }

        private static bool SameTarget(ScenarioAuthoringTarget left, ScenarioAuthoringTarget right)
        {
            if (left == null || right == null)
                return false;

            if (!string.IsNullOrEmpty(left.Id) && !string.IsNullOrEmpty(right.Id))
                return string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(left.TransformPath) && !string.IsNullOrEmpty(right.TransformPath))
                return string.Equals(left.TransformPath, right.TransformPath, StringComparison.OrdinalIgnoreCase);

            return string.Equals(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(
            ScenarioSpriteSwapAuthoringService.SpritePickerModel picker,
            string token)
        {
            ScenarioSpriteCatalogService.SpriteCandidate candidate = FindCandidate(picker != null ? picker.VanillaCandidates : null, token);
            if (candidate != null)
                return candidate;

            return FindCandidate(picker != null ? picker.ModdedCandidates : null, token);
        }

        private static ScenarioSpriteCatalogService.SpriteCandidate FindCandidate(
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string token)
        {
            for (int i = 0; candidates != null && i < candidates.Count; i++)
            {
                ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                if (candidate != null && string.Equals(candidate.Token, token, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private static int CountSpriteSwaps(ScenarioEditorSession editorSession)
        {
            return editorSession != null && editorSession.WorkingDefinition != null
                ? CountSpriteSwaps(editorSession.WorkingDefinition)
                : 0;
        }

        private static int CountSpriteSwaps(ScenarioDefinition definition)
        {
            if (definition == null || definition.AssetReferences == null)
                return 0;

            return definition.AssetReferences.SpriteSwaps != null
                ? definition.AssetReferences.SpriteSwaps.Count
                : 0;
        }

        private static int CountSceneSpritePlacements(ScenarioEditorSession editorSession)
        {
            return editorSession != null && editorSession.WorkingDefinition != null
                ? CountSceneSpritePlacements(editorSession.WorkingDefinition)
                : 0;
        }

        private static int CountSceneSpritePlacements(ScenarioDefinition definition)
        {
            return definition != null && definition.AssetReferences != null && definition.AssetReferences.SceneSpritePlacements != null
                ? definition.AssetReferences.SceneSpritePlacements.Count
                : 0;
        }

        private static int CountDirtyFlags(ScenarioEditorSession editorSession)
        {
            return editorSession != null && editorSession.DirtyFlags != null
                ? editorSession.DirtyFlags.Count
                : 0;
        }

        private static int CountFamilyMembers(ScenarioDefinition definition)
        {
            return definition != null && definition.FamilySetup != null && definition.FamilySetup.Members != null
                ? definition.FamilySetup.Members.Count
                : 0;
        }

        private static int CountInventoryStacks(ScenarioDefinition definition)
        {
            return definition != null && definition.StartingInventory != null && definition.StartingInventory.Items != null
                ? definition.StartingInventory.Items.Count
                : 0;
        }

        private static int CountInventoryTotal(ScenarioDefinition definition)
        {
            if (definition == null || definition.StartingInventory == null || definition.StartingInventory.Items == null)
                return 0;

            int total = 0;
            for (int i = 0; i < definition.StartingInventory.Items.Count; i++)
            {
                ItemEntry entry = definition.StartingInventory.Items[i];
                if (entry != null && entry.Quantity > 0)
                    total += entry.Quantity;
            }

            return total;
        }

        private static int CountObjectPlacements(ScenarioDefinition definition)
        {
            return definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null
                ? definition.BunkerEdits.ObjectPlacements.Count
                : 0;
        }

        private static string SummarizeFamily(ScenarioDefinition definition)
        {
            if (definition == null || definition.FamilySetup == null || definition.FamilySetup.Members == null || definition.FamilySetup.Members.Count == 0)
                return "No family snapshot captured yet.";

            List<string> names = new List<string>();
            for (int i = 0; i < definition.FamilySetup.Members.Count && names.Count < 4; i++)
            {
                FamilyMemberConfig member = definition.FamilySetup.Members[i];
                if (member != null && !string.IsNullOrEmpty(member.Name))
                    names.Add(member.Name);
            }

            string preview = names.Count > 0 ? string.Join(", ", names.ToArray()) : "Unnamed members";
            if (definition.FamilySetup.Members.Count > names.Count)
                preview += " +" + (definition.FamilySetup.Members.Count - names.Count);
            return preview;
        }

        private static string SummarizeInventory(ScenarioDefinition definition)
        {
            if (definition == null || definition.StartingInventory == null || definition.StartingInventory.Items == null || definition.StartingInventory.Items.Count == 0)
                return "No inventory snapshot captured yet.";

            List<string> parts = new List<string>();
            for (int i = 0; i < definition.StartingInventory.Items.Count && parts.Count < 4; i++)
            {
                ItemEntry entry = definition.StartingInventory.Items[i];
                if (entry != null && !string.IsNullOrEmpty(entry.ItemId) && entry.Quantity > 0)
                    parts.Add(entry.ItemId + " x" + entry.Quantity);
            }

            string preview = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "Inventory captured";
            if (definition.StartingInventory.Items.Count > parts.Count)
                preview += " +" + (definition.StartingInventory.Items.Count - parts.Count);
            return preview;
        }

        private static string SummarizeObjectPlacements(ScenarioDefinition definition)
        {
            if (definition == null || definition.BunkerEdits == null || definition.BunkerEdits.ObjectPlacements == null || definition.BunkerEdits.ObjectPlacements.Count == 0)
                return "No spawned shelter objects captured yet.";

            List<string> parts = new List<string>();
            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count && parts.Count < 4; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement != null && !string.IsNullOrEmpty(placement.DefinitionReference))
                    parts.Add(placement.DefinitionReference);
            }

            string preview = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "Object placements captured";
            if (definition.BunkerEdits.ObjectPlacements.Count > parts.Count)
                preview += " +" + (definition.BunkerEdits.ObjectPlacements.Count - parts.Count);
            return preview;
        }

        private List<ScenarioAuthoringInspectorSection> BuildAssetSections(
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
                    Items = new[] { Text(scopeReason) }
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
                    Property("Mode", mode.ToString()),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModeReplace, "Replace Existing", "Open the sprite picker for the selected visual target and save the change explicitly.", true, mode == ScenarioAssetAuthoringMode.ReplaceExisting, "RE", "Like-for-like runtime replacement.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModePlace, "Place New Snapped", "Create or update a snapped authored scene sprite placement.", true, mode == ScenarioAssetAuthoringMode.PlaceNew, "PL", "Snapped decorative scene placement."))
                }
            };
        }

        private List<ScenarioAuthoringInspectorSection> BuildSpriteSwapSections(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringTarget target)
        {
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            ScenarioSpriteSwapAuthoringService.SpritePickerModel picker = _spriteSwapAuthoringService.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (picker == null || picker.Target == null)
                return sections;

            List<ScenarioAuthoringInspectorItem> summaryItems = new List<ScenarioAuthoringInspectorItem>();
            summaryItems.Add(Text(
                Safe(picker.Target.SpriteName),
                Safe(picker.Target.TextureName),
                picker.Target.Kind.ToString(),
                "SP",
                picker.Target.CurrentSprite,
                true));
            summaryItems.Add(Property("Component", picker.Target.Kind.ToString()));
            summaryItems.Add(Property("Current Sprite", Safe(picker.Target.SpriteName)));
            summaryItems.Add(Property("Current Map", Safe(picker.Target.TextureName)));
            summaryItems.Add(Property("Active Swap", Safe(picker.ActiveRuleSummary)));
            summaryItems.Add(Property("Compatibility", Safe(picker.CompatibilitySummary)));
            summaryItems.Add(Property("Stored As", Safe(picker.XmlPathHint)));
            summaryItems.Add(Property("Compatible Vanilla", CountCandidates(picker.VanillaCandidates).ToString()));
            summaryItems.Add(Property("Compatible Modded", CountCandidates(picker.ModdedCandidates).ToString()));
            bool pickerOpen = state != null
                && state.SpriteSwapPicker != null
                && state.SpriteSwapPicker.IsOpen
                && SameTarget(state.SpriteSwapPicker.Target, target);
            string previewLabel = state != null && state.SpriteSwapPicker != null
                ? state.SpriteSwapPicker.PreviewCandidateLabel
                : null;
            summaryItems.Add(Property("Picker", pickerOpen ? "Open" : "Closed"));
            summaryItems.Add(Property("Preview", !string.IsNullOrEmpty(previewLabel) ? previewLabel : "<none>"));
            summaryItems.Add(ActionItem(Action(
                ScenarioAuthoringActionIds.ActionSpriteSwapPickerOpen,
                pickerOpen ? "Sprite Picker Open" : "Open Sprite Picker",
                "Open the dedicated sprite picker, preview compatible sprites in real time, then save or cancel.",
                true,
                pickerOpen)));
            summaryItems.Add(Text(Safe(picker.GuidanceMessage)));
            summaryItems.Add(Text("This follows the same serializer shape other scenario packs use: AssetReferences > SpriteSwaps > Swap."));

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
            ScenarioSceneSpritePlacementAuthoringService.PlacementPickerModel picker = _sceneSpritePlacementAuthoringService.GetPickerModel(
                editorSession,
                target,
                state != null ? state.ActiveScenarioFilePath : null);
            if (picker == null)
                return sections;

            List<ScenarioAuthoringInspectorItem> summaryItems = new List<ScenarioAuthoringInspectorItem>();
            summaryItems.Add(Text(
                FormatTarget(target),
                target != null ? target.Kind.ToString() : "<none>",
                null,
                "AN",
                ResolvePreviewSprite(target),
                true));
            summaryItems.Add(Property("Anchor", FormatTarget(target)));
            summaryItems.Add(Property("Grid", target != null && target.GridX.HasValue && target.GridY.HasValue ? (target.GridX.Value + "," + target.GridY.Value) : "<none>"));
            summaryItems.Add(Property("Active Placement", picker.ActivePlacement != null ? Safe(picker.ActivePlacement.Id) : "<none>"));
            summaryItems.Add(Property("Compatibility", Safe(picker.CompatibilitySummary)));
            summaryItems.Add(Property("Stored As", Safe(picker.XmlPathHint)));
            summaryItems.Add(Property("Vanilla Options", CountCandidates(picker.VanillaCandidates).ToString()));
            summaryItems.Add(Property("Modded Options", CountCandidates(picker.ModdedCandidates).ToString()));
            summaryItems.Add(Text(Safe(picker.PlacementSummary)));
            summaryItems.Add(Text(Safe(picker.GuidanceMessage)));
            summaryItems.Add(Text("This follows the same serializer shape other scenario packs use: AssetReferences > SceneSpritePlacements > Placement."));
            summaryItems.Add(ActionItem(Action(
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

        private static ScenarioAuthoringInspectorSection BuildSpriteCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage,
            string savedToken,
            string previewToken)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Count", CountCandidates(candidates).ToString()));
            if (candidates == null || candidates.Count == 0)
            {
                items.Add(Text(emptyMessage));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    bool previewed = string.Equals(candidate.Token, previewToken, StringComparison.Ordinal);
                    bool saved = string.Equals(candidate.Token, savedToken, StringComparison.Ordinal);
                    items.Add(ActionItem(Action(
                        ScenarioSpriteSwapAuthoringService.BuildPreviewActionId(candidate.Token),
                        CleanCandidateLabel(candidate.Label),
                        candidate.Hint,
                        true,
                        previewed,
                        "RT",
                        candidate.SourceName,
                        BuildSpriteCandidateBadge(candidate, saved, previewed),
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

        private static ScenarioAuthoringInspectorSection BuildPlacementCandidateSection(
            string id,
            string title,
            List<ScenarioSpriteCatalogService.SpriteCandidate> candidates,
            string emptyMessage)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Property("Count", CountCandidates(candidates).ToString()));
            if (candidates == null || candidates.Count == 0)
            {
                items.Add(Text(emptyMessage));
            }
            else
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScenarioSpriteCatalogService.SpriteCandidate candidate = candidates[i];
                    if (candidate == null)
                        continue;

                    items.Add(ActionItem(Action(
                        ScenarioSceneSpritePlacementAuthoringService.BuildApplyActionId(candidate.Token),
                        CleanCandidateLabel(candidate.Label),
                        candidate.Hint,
                        true,
                        false,
                        "RT",
                        candidate.SourceName,
                        BuildCandidateBadge(candidate),
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

        private static int CountCandidates(List<ScenarioSpriteCatalogService.SpriteCandidate> candidates)
        {
            return candidates != null ? candidates.Count : 0;
        }

        private ScenarioAuthoringInspectorSection BuildSelectionSection(ScenarioAuthoringState state)
        {
            ScenarioTargetScope activeScope = _selectionScopeService.ResolveActiveScope(state);
            return new ScenarioAuthoringInspectorSection
            {
                Id = "selection",
                Title = "Selection",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.MetricGrid,
                Items = new[]
                {
                    Property("Scope", ScenarioTargetClassifier.FormatScopeLabel(activeScope)),
                    Property("Selection Mode", state.SelectionModeActive ? "Active" : "Inactive"),
                    Property("Hovered", FormatTarget(state.HoveredTarget)),
                    Property("Selected", FormatTarget(state.SelectedTarget))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildHistorySection()
        {
            ScenarioAuthoringHistoryService history = ScenarioAuthoringHistoryService.Instance;
            bool canUndo = history.CanUndo;
            bool canRedo = history.CanRedo;
            bool clipboardHasRule = ScenarioSpriteSwapClipboard.HasRule;

            return new ScenarioAuthoringInspectorSection
            {
                Id = "history",
                Title = "History & Clipboard",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    Property("Undo Depth", history.UndoDepth.ToString()),
                    Property("Redo Depth", history.RedoDepth.ToString()),
                    Property("Clipboard", ScenarioSpriteSwapClipboard.Describe()),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionHistoryUndo, "Undo (Ctrl+Z)", "Undo the last sprite swap change.", canUndo, false, "UN", "Rewind the last authored sprite change.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionHistoryRedo, "Redo (Ctrl+Y)", "Redo the last undone change.", canRedo, false, "RE", "Re-apply the last undone change.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Copy Swap (Ctrl+C)", "Copy the selected target's active sprite swap to the clipboard.", true, false, "CP", "Copy the selected sprite rule.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSpriteSwapPaste, "Paste Swap (Ctrl+V)", "Paste the clipboard sprite swap onto the selected target.", clipboardHasRule, clipboardHasRule, "PA", clipboardHasRule ? "Apply the copied rule to the current target." : "Clipboard is empty.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSpriteSwapRevert, "Revert Sprite (Ctrl+R)", "Remove the selected target's sprite swap and restore its original sprite.", true, false, "RV", "Clear the authored swap."))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildWorkflowSection(ScenarioEditorSession editorSession)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "workflow",
                Title = "Workflow",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = new[]
                {
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionSave, "Save Draft", "Persist the current scenario XML.", true, false, "SV", "Write scenario.xml to the active draft.")),
                    ActionItem(Action(
                        ScenarioAuthoringActionIds.ActionPlaytest,
                        editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "Stop Playtest" : "Start Playtest",
                        "Toggle simulation while keeping the live shelter editor session intact.",
                        true,
                        true,
                        "PL",
                        editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting
                            ? "Return to frozen authoring mode."
                            : "Run the live shelter with the current draft.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionCloseEditor, "Exit Editor", "Close the authoring shell and return the save to normal live play.", true, false, "EX", "Release the current authoring session."))
                }
            };
        }

        private static ScenarioAuthoringInspectorSection BuildToolPickerSection(ScenarioAuthoringTool activeTool)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "tools",
                Title = "Tools",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.TabStrip,
                Items = new[]
                {
                    Text("Tools are split between gameplay data capture, shelter build placement, and visual asset authoring."),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolFamily, "Family", "Capture the current live family roster, stats, and traits.", true, activeTool == ScenarioAuthoringTool.Family, "FM", "Family roster and stats.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolInventory, "Inventory", "Capture the current live shelter inventory.", true, activeTool == ScenarioAuthoringTool.Inventory, "IV", "Shelter inventory snapshot.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolShelter, "Structure", "Place new shelter rooms, ladders, and lights with vanilla build ghosts.", true, activeTool == ScenarioAuthoringTool.Shelter, "ST", "Shelter layout editing.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolObjects, "Objects", "Place workbenches, shelter systems, and furniture or capture live spawned objects.", true, activeTool == ScenarioAuthoringTool.Objects, "OB", "Interactive shelter objects.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolWiring, "Walls & Wiring", "Apply room wall and wiring sprites to the selected shelter tile.", true, activeTool == ScenarioAuthoringTool.Wiring, "WW", "Room finish editing.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolAssets, "Assets", "Swap existing visuals or place new snapped scene sprites.", true, activeTool == ScenarioAuthoringTool.Assets, "AS", "Sprite replacements and scene art.")),
                    ActionItem(Action(ScenarioAuthoringActionIds.ActionToolSelect, "Select", "Stay in world selection mode while using the current workflow.", true, activeTool == ScenarioAuthoringTool.Select, "SL", "Selection-only mode."))
                }
            };
        }

        private ScenarioAuthoringInspectorSection BuildToolSection(
            ScenarioAuthoringState state,
            ScenarioAuthoringTool activeTool,
            ScenarioDefinition definition,
            ScenarioAuthoringTarget selectedTarget,
            bool canCaptureSelectedObject,
            bool hasCapturedSelectedObject,
            string selectedObjectStatus)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioBuildPlacementAuthoringService.StatusModel buildStatus =
                activeTool == ScenarioAuthoringTool.Shelter
                || activeTool == ScenarioAuthoringTool.Objects
                || activeTool == ScenarioAuthoringTool.Wiring
                || activeTool == ScenarioAuthoringTool.Select
                    ? _buildPlacementAuthoringService.GetStatusModel(state, _editorService.CurrentSession)
                    : null;
            string title;
            switch (activeTool)
            {
                case ScenarioAuthoringTool.Family:
                    title = "Family";
                    items.Add(Property("Captured Members", CountFamilyMembers(definition).ToString()));
                    items.Add(Text(SummarizeFamily(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureFamily, "Capture Current Family", "Snapshot the live family roster, stats, and traits into the scenario.", true, true, "FM", "Capture live family state.")));
                    break;

                case ScenarioAuthoringTool.Inventory:
                    title = "Inventory";
                    items.Add(Property("Captured Stacks", CountInventoryStacks(definition).ToString()));
                    items.Add(Property("Total Items", CountInventoryTotal(definition).ToString()));
                    items.Add(Text(SummarizeInventory(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureInventory, "Capture Current Inventory", "Snapshot the live shelter inventory into the scenario.", true, true, "IV", "Capture live inventory.")));
                    break;

                case ScenarioAuthoringTool.Assets:
                    title = "Assets";
                    items.Add(Property("Sprite Swaps", CountSpriteSwaps(definition).ToString()));
                    items.Add(Property("Placed Sprites", CountSceneSpritePlacements(definition).ToString()));
                    items.Add(Property("Selected Target", FormatTarget(selectedTarget)));
                    items.Add(Property("Pack Layout", "Scenarios/<ScenarioName>/scenario.xml"));
                    items.Add(Property("Custom Sprite XML", "AssetReferences > CustomSprites > Sprite"));
                    items.Add(Property("Swap XML", "AssetReferences > SpriteSwaps > Swap"));
                    items.Add(Property("Placement XML", "AssetReferences > SceneSpritePlacements > Placement"));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModeReplace, "Replace Existing", "Open the sprite picker for the selected visual target and save the change explicitly.", true, state.AssetMode == ScenarioAssetAuthoringMode.ReplaceExisting, "RE", "Strict family-based replacement.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionAssetModePlace, "Place New Snapped", "Selecting a sprite creates or updates a snapped authored scene sprite.", true, state.AssetMode == ScenarioAssetAuthoringMode.PlaceNew, "PL", "Snapped decorative placement.")));
                    items.Add(Text("Asset authoring now fails closed: verified in-game runtime art only, no silent size-based fallback."));
                    items.Add(Text("Use Replace Existing to launch the sprite picker, preview compatible swaps, and explicitly save or cancel them. Use Place New Snapped for visual-only scene dressing that stores Placement entries in the scenario XML."));
                    items.Add(Text("These labels mirror the serializer and Custom Scenarios guide so authored changes match how other scenario packs are structured."));
                    break;

                case ScenarioAuthoringTool.Shelter:
                    title = "Structure";
                    items.Add(Property("Recorded Placements", CountObjectPlacements(definition).ToString()));
                    items.Add(Property("Selected Room", FormatTarget(selectedTarget)));
                    if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Guidance))
                        items.Add(Text(buildStatus.Guidance));
                    if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Detail))
                        items.Add(Text(buildStatus.Detail));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionBuildStructureRoom, "Place Room Tile", "Start vanilla-style room placement for the scenario draft.", true, false, "RM", "Extend the shelter layout.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionBuildStructureLadder, "Place Ladder", "Start vanilla-style ladder placement for the scenario draft.", true, false, "LD", "Connect shelter levels.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionBuildStructureLight, "Place Room Light", "Start vanilla-style room-light placement for the scenario draft.", true, false, "LG", "Light a room tile.")));
                    if (buildStatus != null && buildStatus.CanCancel)
                    {
                        items.Add(ActionItem(Action(
                            ScenarioAuthoringActionIds.ActionBuildPlacementCancel,
                            "Cancel Placement",
                            "Stop the active structure preview without committing it.",
                            true,
                            false,
                            "CX",
                            "Clear the active ghost preview.")));
                    }
                    break;

                case ScenarioAuthoringTool.Objects:
                    title = "Objects";
                    items.Add(Property("Recorded Placements", CountObjectPlacements(definition).ToString()));
                    items.Add(Property("Selected Object", FormatTarget(selectedTarget)));
                    if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Guidance))
                        items.Add(Text(buildStatus.Guidance));
                    if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Detail))
                        items.Add(Text(buildStatus.Detail));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureShelterObjects, "Capture All Spawned Objects", "Replace the scenario placement list with the current live spawned shelter objects.", true, true, "OB", "Capture every current shelter placement.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Capture Selected Object", "Store the selected live shelter object as a scenario placement.", canCaptureSelectedObject, canCaptureSelectedObject, "CP", "Capture only the selected object.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement, "Remove Selected Capture", "Remove the selected object's captured placement from the scenario.", hasCapturedSelectedObject, false, "RM", "Delete the stored selected capture.")));
                    if (buildStatus != null && buildStatus.CanCancel)
                    {
                        items.Add(ActionItem(Action(
                            ScenarioAuthoringActionIds.ActionBuildPlacementCancel,
                            "Cancel Placement",
                            "Stop the active object preview without committing it.",
                            true,
                            false,
                            "CX",
                            "Clear the active ghost preview.")));
                    }
                    if (!string.IsNullOrEmpty(selectedObjectStatus))
                        items.Add(Text(selectedObjectStatus));
                    break;

                case ScenarioAuthoringTool.Wiring:
                    title = "Walls & Wiring";
                    items.Add(Property("Selected Room", FormatTarget(selectedTarget)));
                    items.Add(Property("Recorded Room Edits", definition != null && definition.BunkerEdits != null ? definition.BunkerEdits.RoomChanges.Count.ToString() : "0"));
                    if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Guidance))
                        items.Add(Text(buildStatus.Guidance));
                    if (buildStatus != null && !string.IsNullOrEmpty(buildStatus.Detail))
                        items.Add(Text(buildStatus.Detail));
                    items.Add(Text("Pick the room tile first, then choose wall or wiring variants from the palette window."));
                    break;

                case ScenarioAuthoringTool.Select:
                    title = "Selection";
                    items.Add(Property("Selected Target", FormatTarget(selectedTarget)));
                    items.Add(Text("Selection mode is active. Use it to inspect world objects, rooms, or authored sprites before switching into a build tool."));
                    items.Add(Text("Structure, Objects, and Walls & Wiring all expose their interactive palettes in the Tiles Palette window."));
                    break;

                default:
                    title = "Shelter";
                    items.Add(Property("Captured Placements", CountObjectPlacements(definition).ToString()));
                    items.Add(Text(SummarizeObjectPlacements(definition)));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureShelterObjects, "Capture All Spawned Objects", "Replace the scenario placement list with the current live spawned shelter objects.", true, true, "OB", "Capture every current shelter placement.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Capture Selected Object", "Store the selected live shelter object as a scenario placement.", canCaptureSelectedObject, canCaptureSelectedObject, "CP", "Capture only the selected object.")));
                    items.Add(ActionItem(Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement, "Remove Selected Capture", "Remove the selected object's captured placement from the scenario.", hasCapturedSelectedObject, false, "RM", "Delete the stored selected capture.")));
                    items.Add(Property("Selected Object", FormatTarget(selectedTarget)));
                    if (!string.IsNullOrEmpty(selectedObjectStatus))
                        items.Add(Text(selectedObjectStatus));
                    break;
            }

            return new ScenarioAuthoringInspectorSection
            {
                Id = "tool",
                Title = title,
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            };
        }
    }
}
