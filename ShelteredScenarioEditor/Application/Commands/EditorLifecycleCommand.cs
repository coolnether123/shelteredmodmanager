using System;
using System.Globalization;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal enum EditorLifecycleCommandKind
    {
        SaveDraft,
        CopyDraftPath,
        TogglePlaytest,
        RestartPlaytest,
        WatchOpeningCutscene,
        UseRandomSeed,
        UseFixedSeed,
        RerollSeed,
        SetSeed,
        OpenPauseMenu,
        ConvertToNormalSave,
        OpenAdjacentBaseMode,
        SwitchBaseModeAndReload,
        SwitchBaseModeWithoutReload,
        CancelBaseModeSwitch,
        CommitDraftTitle,
        UpdateMetadata,
        BumpVersion,
        CloseFocusedEditor,
        ExitToMainMenu
    }

    internal enum ScenarioMetadataField
    {
        Description,
        Goal,
        Author,
        Version,
        Credits,
        Tags,
        Id
    }

    internal sealed class EditorLifecycleCommand : ScenarioAuthoringCommand, IScenarioTextValueCommand
    {
        private EditorLifecycleCommand(
            EditorLifecycleCommandKind kind,
            string automationId,
            ScenarioAuthoringCommandPolicy policy,
            string textValue,
            ScenarioMetadataField metadataField,
            int direction,
            ScenarioBaseGameMode baseMode,
            string familyChoice,
            bool cancel,
            bool minorVersionBump)
            : base(automationId, policy)
        {
            Kind = kind;
            TextValue = textValue;
            MetadataField = metadataField;
            Direction = direction;
            BaseMode = baseMode;
            FamilyChoice = familyChoice;
            Cancel = cancel;
            MinorVersionBump = minorVersionBump;
        }

        internal EditorLifecycleCommandKind Kind { get; private set; }
        internal string TextValue { get; private set; }
        internal ScenarioMetadataField MetadataField { get; private set; }
        internal int Direction { get; private set; }
        internal ScenarioBaseGameMode BaseMode { get; private set; }
        internal string FamilyChoice { get; private set; }
        internal bool Cancel { get; private set; }
        internal bool MinorVersionBump { get; private set; }

        internal static EditorLifecycleCommand SaveDraft { get { return Simple(EditorLifecycleCommandKind.SaveDraft, ScenarioAuthoringActionIds.ActionSave); } }
        internal static EditorLifecycleCommand CopyDraftPath { get { return Simple(EditorLifecycleCommandKind.CopyDraftPath, ScenarioAuthoringActionIds.ActionDraftCopyPath); } }
        internal static EditorLifecycleCommand TogglePlaytest { get { return Simple(EditorLifecycleCommandKind.TogglePlaytest, ScenarioAuthoringActionIds.ActionPlaytest, ScenarioAuthoringCommandPolicy.WorldSafetySnapshot); } }
        internal static EditorLifecycleCommand RestartPlaytest { get { return Simple(EditorLifecycleCommandKind.RestartPlaytest, ScenarioAuthoringActionIds.ActionPlaytestRestart, ScenarioAuthoringCommandPolicy.World); } }
        internal static EditorLifecycleCommand WatchOpeningCutscene { get { return Simple(EditorLifecycleCommandKind.WatchOpeningCutscene, ScenarioBaseModeAuthoringActions.ActionWatchOpeningCutscene, ScenarioAuthoringCommandPolicy.World); } }
        internal static EditorLifecycleCommand UseRandomSeed { get { return Simple(EditorLifecycleCommandKind.UseRandomSeed, ScenarioAuthoringActionIds.ActionScenarioSeedRandom); } }
        internal static EditorLifecycleCommand UseFixedSeed { get { return Simple(EditorLifecycleCommandKind.UseFixedSeed, ScenarioAuthoringActionIds.ActionScenarioSeedFixed); } }
        internal static EditorLifecycleCommand RerollSeed { get { return Simple(EditorLifecycleCommandKind.RerollSeed, ScenarioAuthoringActionIds.ActionScenarioSeedReroll); } }
        internal static EditorLifecycleCommand OpenPauseMenu { get { return Simple(EditorLifecycleCommandKind.OpenPauseMenu, ScenarioAuthoringActionIds.ActionOpenPauseMenu); } }
        internal static EditorLifecycleCommand ConvertToNormalSave { get { return Simple(EditorLifecycleCommandKind.ConvertToNormalSave, ScenarioAuthoringActionIds.ActionConvertToNormal, ScenarioAuthoringCommandPolicy.World); } }
        internal static EditorLifecycleCommand PreviousBaseMode { get { return AdjacentBaseMode(-1, ScenarioAuthoringActionIds.ActionScenarioModePrevious); } }
        internal static EditorLifecycleCommand NextBaseMode { get { return AdjacentBaseMode(1, ScenarioAuthoringActionIds.ActionScenarioModeNext); } }
        internal static EditorLifecycleCommand CancelBaseModeSwitch { get { return Simple(EditorLifecycleCommandKind.CancelBaseModeSwitch, ScenarioBaseModeAuthoringActions.ActionSwitchCancel, ScenarioAuthoringCommandPolicy.Reload); } }
        internal static EditorLifecycleCommand SaveFocusedEditor { get { return FocusedEditor(false, ScenarioAuthoringActionIds.ActionFocusedEditorSave); } }
        internal static EditorLifecycleCommand CancelFocusedEditor { get { return FocusedEditor(true, ScenarioAuthoringActionIds.ActionFocusedEditorCancel); } }
        internal static EditorLifecycleCommand ExitToMainMenu { get { return Simple(EditorLifecycleCommandKind.ExitToMainMenu, ScenarioAuthoringActionIds.ActionCloseEditor); } }

        internal static EditorLifecycleCommand DraftTitle(string value)
        {
            return Text(EditorLifecycleCommandKind.CommitDraftTitle, ScenarioAuthoringActionIds.ActionDraftTitlePrefix, value, ScenarioMetadataField.Description);
        }

        internal static EditorLifecycleCommand Seed(string value)
        {
            return Text(EditorLifecycleCommandKind.SetSeed, ScenarioAuthoringActionIds.ActionScenarioSeedValuePrefix, value, ScenarioMetadataField.Description);
        }

        internal static EditorLifecycleCommand Metadata(ScenarioMetadataField field, string value)
        {
            return Text(EditorLifecycleCommandKind.UpdateMetadata, MetadataPrefix(field), value, field);
        }

        internal static EditorLifecycleCommand BumpPatchVersion
        {
            get { return BumpVersion(false, ScenarioAuthoringActionIds.ActionDraftVersionBumpPatch); }
        }

        internal static EditorLifecycleCommand BumpMinorVersion
        {
            get { return BumpVersion(true, ScenarioAuthoringActionIds.ActionDraftVersionBumpMinor); }
        }

        internal static EditorLifecycleCommand SwitchBaseMode(ScenarioBaseGameMode baseMode, string familyChoice, bool reloadWorld)
        {
            string automationId = reloadWorld
                ? ScenarioBaseModeAuthoringActions.SwitchReloadId(baseMode, familyChoice)
                : ScenarioBaseModeAuthoringActions.SwitchOnlyId(baseMode, familyChoice);
            return new EditorLifecycleCommand(
                reloadWorld ? EditorLifecycleCommandKind.SwitchBaseModeAndReload : EditorLifecycleCommandKind.SwitchBaseModeWithoutReload,
                automationId,
                reloadWorld ? ScenarioAuthoringCommandPolicy.ReloadSafetySnapshot : ScenarioAuthoringCommandPolicy.WorldSafetySnapshot,
                null,
                ScenarioMetadataField.Description,
                0,
                baseMode,
                familyChoice,
                false,
                false);
        }

        public ScenarioAuthoringCommand WithTextValue(string value)
        {
            switch (Kind)
            {
                case EditorLifecycleCommandKind.CommitDraftTitle:
                    return DraftTitle(value);
                case EditorLifecycleCommandKind.SetSeed:
                    return Seed(value);
                case EditorLifecycleCommandKind.UpdateMetadata:
                    return Metadata(MetadataField, value);
                default:
                    return this;
            }
        }

        private static EditorLifecycleCommand Simple(EditorLifecycleCommandKind kind, string automationId, ScenarioAuthoringCommandPolicy policy = null)
        {
            return new EditorLifecycleCommand(kind, automationId, policy, null, ScenarioMetadataField.Description, 0, ScenarioBaseGameMode.Survival, null, false, false);
        }

        private static EditorLifecycleCommand AdjacentBaseMode(int direction, string automationId)
        {
            return new EditorLifecycleCommand(EditorLifecycleCommandKind.OpenAdjacentBaseMode, automationId, ScenarioAuthoringCommandPolicy.Reload, null, ScenarioMetadataField.Description, direction, ScenarioBaseGameMode.Survival, null, false, false);
        }

        private static EditorLifecycleCommand FocusedEditor(bool cancel, string automationId)
        {
            return new EditorLifecycleCommand(EditorLifecycleCommandKind.CloseFocusedEditor, automationId, ScenarioAuthoringCommandPolicy.Default, null, ScenarioMetadataField.Description, 0, ScenarioBaseGameMode.Survival, null, cancel, false);
        }

        private static EditorLifecycleCommand BumpVersion(bool minor, string automationId)
        {
            return new EditorLifecycleCommand(EditorLifecycleCommandKind.BumpVersion, automationId, ScenarioAuthoringCommandPolicy.Default, null, ScenarioMetadataField.Version, 0, ScenarioBaseGameMode.Survival, null, false, minor);
        }

        private static EditorLifecycleCommand Text(EditorLifecycleCommandKind kind, string prefix, string value, ScenarioMetadataField field)
        {
            string safeValue = value ?? string.Empty;
            return new EditorLifecycleCommand(kind, prefix + ScenarioAutomationIdCodec.EncodeToken(safeValue), ScenarioAuthoringCommandPolicy.Default, safeValue, field, 0, ScenarioBaseGameMode.Survival, null, false, false);
        }

        private static string MetadataPrefix(ScenarioMetadataField field)
        {
            switch (field)
            {
                case ScenarioMetadataField.Goal: return ScenarioAuthoringActionIds.ActionDraftGoalPrefix;
                case ScenarioMetadataField.Author: return ScenarioAuthoringActionIds.ActionDraftAuthorPrefix;
                case ScenarioMetadataField.Version: return ScenarioAuthoringActionIds.ActionDraftVersionPrefix;
                case ScenarioMetadataField.Credits: return ScenarioAuthoringActionIds.ActionDraftCreditsPrefix;
                case ScenarioMetadataField.Tags: return ScenarioAuthoringActionIds.ActionDraftTagsPrefix;
                case ScenarioMetadataField.Id: return ScenarioAuthoringActionIds.ActionDraftIdPrefix;
                default: return ScenarioAuthoringActionIds.ActionDraftDescriptionPrefix;
            }
        }
    }

    internal sealed class ToolCommand : ScenarioAuthoringCommand
    {
        private ToolCommand(ScenarioAuthoringTool tool, string automationId)
            : base(automationId, ScenarioAuthoringCommandPolicy.Default)
        {
            Tool = tool;
        }

        internal ScenarioAuthoringTool Tool { get; private set; }

        internal static ToolCommand Select(ScenarioAuthoringTool tool)
        {
            return new ToolCommand(tool, ResolveAutomationId(tool));
        }

        private static string ResolveAutomationId(ScenarioAuthoringTool tool)
        {
            switch (tool)
            {
                case ScenarioAuthoringTool.Select: return ScenarioAuthoringActionIds.ActionToolSelect;
                case ScenarioAuthoringTool.Family: return ScenarioAuthoringActionIds.ActionToolFamily;
                case ScenarioAuthoringTool.Inventory: return ScenarioAuthoringActionIds.ActionToolInventory;
                case ScenarioAuthoringTool.Shelter: return ScenarioAuthoringActionIds.ActionToolShelter;
                case ScenarioAuthoringTool.Assets: return ScenarioAuthoringActionIds.ActionToolAssets;
                case ScenarioAuthoringTool.Wiring: return ScenarioAuthoringActionIds.ActionToolWiring;
                case ScenarioAuthoringTool.WinLoss: return ScenarioAuthoringActionIds.ActionToolWinLoss;
                default: return ScenarioAuthoringActionIds.ActionToolObjects;
            }
        }
    }
}
