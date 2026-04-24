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
            StatusBarViewModelBuilder statusBarViewModelBuilder)
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

            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            string friendlyKind = FriendlyKindLabel(target.Kind);
            items.Add(Text(
                Safe(target.DisplayName),
                friendlyKind,
                friendlyKind,
                "TG",
                ResolvePreviewSprite(target),
                true));
            items.Add(Property("Name", Safe(target.DisplayName)));
            items.Add(Property("Type", friendlyKind));
            items.Add(Property("Position", FormatVector(target.WorldPosition)));
            items.Add(Property("Replaceable", target.SupportsReplace ? "Yes" : "No"));
            if (!string.IsNullOrEmpty(target.Description))
                items.Add(Text(target.Description));

            List<ScenarioAuthoringInspectorItem> actionItems = new List<ScenarioAuthoringInspectorItem>();
            string captureReason;
            bool canCaptureTarget = _captureService.CanCaptureTarget(target, out captureReason);
            bool hasCapturedPlacement = _captureService.HasCapturedPlacementForTarget(editorSession, target);
            if (canCaptureTarget)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject,
                    "Capture Selected Object",
                    "Store this live spawned shelter object as a scenario object placement.",
                    true,
                    true,
                    "CP",
                    "Persist this spawned object into the draft.")));
            }
            else if (!string.IsNullOrEmpty(captureReason))
            {
                actionItems.Add(Text(captureReason));
            }

            if (hasCapturedPlacement)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionRemoveSelectedObjectPlacement,
                    "Remove Captured Placement",
                    "Remove this object's captured placement from the scenario draft.",
                    true,
                    false,
                    "RM",
                    "Delete this stored object capture.")));
            }

            if (target.SupportsReplace)
            {
                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionToolObjects,
                    "Switch To Objects Tool",
                    "Open the shelter object capture workflow.",
                    true,
                    false,
                    "OB",
                    "Open shelter object capture tools.")));

                actionItems.Add(ActionItem(
                    Action(ScenarioAuthoringActionIds.ActionToolAssets,
                    "Switch To Assets Tool",
                    "Open the asset replacement and placement workflow for this target.",
                    true,
                    false,
                    "AS",
                    "Open the visual replacement workflow.")));
            }

            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "target",
                Title = "Target",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
            });
            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "actions",
                Title = "Actions",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = actionItems.Count > 0 ? actionItems.ToArray() : new[] { Text("This target does not have a scenario capture action yet.") }
            });
            sections.Add(BuildRuntimeAttachmentSection(target, editorSession, editorSession != null ? editorSession.WorkingDefinition : null));

            List<ScenarioAuthoringInspectorSection> assetSections = BuildAssetSections(state, editorSession, target);
            for (int i = 0; i < assetSections.Count; i++)
                sections.Add(assetSections[i]);

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
                picker.VanillaCandidates,
                "No verified vanilla/runtime sprites are currently available for this target family.",
                savedToken,
                previewToken));
            sections.Add(BuildSpriteCandidateSection(
                "sprite_picker_modded",
                "Modded Sprites",
                picker.ModdedCandidates,
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

        private static ScenarioAuthoringInspectorSection BuildRuntimeAttachmentSection(
            ScenarioAuthoringTarget target,
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int triggerLinks = CountLikelyTriggerReferences(definition, target);
            bool saved = target != null && !string.IsNullOrEmpty(target.ScenarioReferenceId);

            items.Add(Property("Saved To Draft", saved ? "Yes" : "No"));
            items.Add(Property("Trigger Links", triggerLinks > 0 ? triggerLinks.ToString() : "None"));
            items.Add(Property("Playtest", editorSession != null ? FriendlyPlaytestLabel(editorSession.PlaytestState) : "Unavailable"));
            if (triggerLinks > 0)
                items.Add(Text("Triggers or events in this draft reference this object."));
            else
                items.Add(Text("No triggers or events reference this object yet."));

            return new ScenarioAuthoringInspectorSection
            {
                Id = "runtime",
                Title = "Scenario Links",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                Items = items.ToArray()
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

        private static string FriendlyPlaytestLabel(ScenarioPlaytestState playtestState)
        {
            switch (playtestState)
            {
                case ScenarioPlaytestState.Playtesting: return "Running";
                case ScenarioPlaytestState.Idle: return "Stopped";
                default: return playtestState.ToString();
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

        private static ScenarioAuthoringInspectorSection[] BuildPublishWindowSections(ScenarioEditorSession editorSession, ScenarioDefinition definition)
        {
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
                }
            };
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
            return sections.ToArray();
        }

        private static ScenarioAuthoringInspectorSection[] BuildTriggerWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (definition != null && definition.TriggersAndEvents != null)
            {
                for (int i = 0; i < definition.TriggersAndEvents.Triggers.Count && i < 5; i++)
                {
                    TriggerDef trigger = definition.TriggersAndEvents.Triggers[i];
                    items.Add(Property(string.IsNullOrEmpty(trigger.Id) ? ("Trigger " + (i + 1)) : trigger.Id, trigger != null ? trigger.Type : "Unknown"));
                }
            }

            if (items.Count == 0)
                items.Add(Text("No authored triggers or scheduled events are in this draft yet."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "triggers",
                    Title = "Triggers / Events",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = items.ToArray()
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildSurvivorWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (definition != null && definition.FamilySetup != null)
            {
                for (int i = 0; i < definition.FamilySetup.Members.Count && i < 5; i++)
                {
                    FamilyMemberConfig member = definition.FamilySetup.Members[i];
                    string role = member != null && member.Traits != null && member.Traits.Count > 0 ? member.Traits[0] : "Survivor";
                    items.Add(Property(Safe(member != null ? member.Name : "Unknown"), role));
                }
            }

            if (items.Count == 0)
                items.Add(Text("No custom survivors have been captured into this draft."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "survivors",
                    Title = "Survivors",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = items.ToArray()
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildStockpileWindowSections(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (definition != null && definition.StartingInventory != null)
            {
                for (int i = 0; i < definition.StartingInventory.Items.Count && i < 6; i++)
                {
                    ItemEntry entry = definition.StartingInventory.Items[i];
                    items.Add(Property(Safe(entry != null ? entry.ItemId : "Item"), entry != null ? entry.Quantity.ToString() : "0"));
                }
            }

            if (items.Count == 0)
                items.Add(Text("No starting stockpile has been captured into this draft."));

            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "stockpile",
                    Title = "Stockpile",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = items.ToArray()
                }
            };
        }

        private static ScenarioAuthoringInspectorSection[] BuildQuestWindowSections(ScenarioDefinition definition)
        {
            int wins = definition != null && definition.WinLossConditions != null ? definition.WinLossConditions.WinConditions.Count : 0;
            int losses = definition != null && definition.WinLossConditions != null ? definition.WinLossConditions.LossConditions.Count : 0;
            int dialogueChains = definition != null && definition.TriggersAndEvents != null ? definition.TriggersAndEvents.DialogueChains.Count : 0;
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "quests",
                    Title = "Quests",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Property("Win Conditions", wins.ToString()),
                        Property("Loss Conditions", losses.ToString()),
                        Property("Dialogue Chains", dialogueChains.ToString()),
                        Text(wins + losses + dialogueChains > 0
                            ? "Quest and outcome definitions are stored in the current scenario draft."
                            : "No quest or outcome authoring has been captured into this draft yet.")
                    }
                }
            };
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
            actions.Add(Action(ScenarioAuthoringActionIds.ActionShellShow, "Inspect Placement", "Open the inspector for the selected target.", true, false));

            if (target.SupportsReplace)
            {
                actions.Add(Action(ScenarioAuthoringActionIds.ActionToolAssets, "Replace Look...", "Switch to art workflow for the selected target.", true, false));
                actions.Add(Action(ScenarioAuthoringActionIds.ActionToolShelter, "Apply Feature", "Switch to build workflow for the selected target.", true, false));
            }

            string captureReason;
            if (_captureService.CanCaptureTarget(target, out captureReason))
                actions.Add(Action(ScenarioAuthoringActionIds.ActionCaptureSelectedObject, "Mark Placement", "Capture the selected live placement into the scenario.", true, false));

            if (!string.IsNullOrEmpty(target.ScenarioReferenceId))
            {
                actions.Add(Action(ScenarioAuthoringActionIds.ActionSceneSpritePlacementRemove, "Remove Placement", "Remove the authored placement reference.", true, false));
                actions.Add(Action(ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Duplicate Placement", "Copy the selected target's sprite swap or placement.", true, false));
            }

            if (target.SupportsReplace)
                actions.Add(Action(ScenarioAuthoringActionIds.ActionSpriteSwapCopy, "Copy Tile", "Copy the selected target's current authored look.", true, false));

            actions.Add(Action(ScenarioAuthoringActionIds.ActionSelectionClear, "Clear Selection", "Clear the current authoring selection.", true, false));
            actions.Add(Action("context.help.stub", "Help", "Show multi-select and context menu help.", false, false));
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

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.##") + ", " + value.y.ToString("0.##") + ", " + value.z.ToString("0.##") + ")";
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
                Title = "Asset Workflow",
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
            sections.Add(BuildPlacementCandidateSection("scene_sprite_vanilla", "Vanilla Sprites", picker.VanillaCandidates, "No loaded vanilla/runtime sprites are currently available for placement."));
            sections.Add(BuildPlacementCandidateSection("scene_sprite_modded", "Modded Sprites", picker.ModdedCandidates, "Custom sprite placements are hidden in strict placement mode."));
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

        private static ScenarioAuthoringInspectorSection BuildSelectionSection(ScenarioAuthoringState state)
        {
            return new ScenarioAuthoringInspectorSection
            {
                Id = "selection",
                Title = "Selection",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.MetricGrid,
                Items = new[]
                {
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
