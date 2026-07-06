using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Presentation.Timeline;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class ScenarioPublishAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        private readonly ScenarioTimelineBuilder _timelineBuilder;
        private readonly ScenarioModDependencyDetector _modDependencyDetector;
        private readonly ScenarioModCompatibilityViewModelBuilder _modCompatibilityViewModelBuilder;

        public ScenarioPublishAuthoringContentBuilder(
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioModDependencyDetector modDependencyDetector,
            ScenarioModCompatibilityViewModelBuilder modCompatibilityViewModelBuilder)
        {
            _timelineBuilder = timelineBuilder;
            _modDependencyDetector = modDependencyDetector;
            _modCompatibilityViewModelBuilder = modCompatibilityViewModelBuilder;
        }

        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Publish; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioModCompatibilityReport compatibilityReport = _modDependencyDetector.BuildReport(definition);
            ScenarioAuthoringValidationSnapshot validation = EvaluateValidation(state, definition);
            List<ScenarioAuthoringInspectorItem> dependencyItems = BuildDependencyItems(definition);
            List<ScenarioAuthoringInspectorItem> compatibilityItems = _modCompatibilityViewModelBuilder.BuildItems(compatibilityReport);
            List<ScenarioAuthoringInspectorItem> timelineItems = BuildTimelineItems(definition, GetRuntimeState(), _timelineBuilder);
            List<ScenarioAuthoringInspectorItem> validationItems = BuildValidationItems(validation);
            List<ScenarioAuthoringInspectorItem> exportItems = BuildExportItems(validation);
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_confidence",
                    Title = "Confidence",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                    Items = BuildReadinessSummary(editorSession, definition, validation, compatibilityReport).ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_stage",
                    Title = "Readiness",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Item.Property("Scenario", Item.Safe(definition != null ? definition.DisplayName : null)),
                        Item.Property("Dirty Sections", Item.CountDirtyFlags(editorSession).ToString()),
                        Item.Property("Version", Item.Safe(definition != null ? definition.Version : null)),
                        Item.Text("This window validates and packages local scenario XML. It does not upload to Steam Workshop.")
                    }
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_validation",
                    Title = "Validation Summary",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = validationItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_export",
                    Title = "Export Package",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = exportItems.ToArray()
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
                    Title = "Timeline",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = timelineItems.ToArray()
                }
            };
        }

        internal static ScenarioAuthoringValidationSnapshot EvaluateValidation(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            IScenarioDefinitionValidator validator = null;
            try
            {
                validator = ScenarioCompositionRoot.Resolve<IScenarioDefinitionValidator>();
            }
            catch
            {
                validator = null;
            }

            return ScenarioAuthoringValidationSnapshot.Evaluate(
                validator,
                definition,
                state != null ? state.ActiveScenarioFilePath : null);
        }

        internal static List<ScenarioAuthoringInspectorItem> BuildValidationItems(ScenarioAuthoringValidationSnapshot validation)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (validation == null || !validation.ValidationAvailable)
            {
                items.Add(Item.Property("Validation", "Unavailable"));
                items.Add(Item.Text(validation != null ? validation.UnavailableReason : "Validation could not run."));
                return items;
            }

            ScenarioValidationIssue[] issues = validation.Issues;
            items.Add(Item.Property("Status", validation.Result != null && validation.Result.IsValid ? "Ready to export" : "Blocked"));
            items.Add(Item.Property("Errors", validation.ErrorCount.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Warnings", validation.WarningCount.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save / Revalidate", "Run validation and save if the draft has no blocking errors.", true, validation.ErrorCount == 0, "SV")));

            if (issues.Length == 0)
            {
                items.Add(Item.Text("Validation passed with no issues."));
                return items;
            }

            for (int i = 0; i < issues.Length && i < 16; i++)
            {
                ScenarioValidationIssue issue = issues[i];
                if (issue != null)
                {
                    items.Add(Item.Property(issue.Severity.ToString(), issue.Message));
                    items.Add(Item.ActionItem(BuildIssueNavigationAction(issue)));
                }
            }

            return items;
        }

        internal static List<ScenarioAuthoringInspectorItem> BuildExportItems(ScenarioAuthoringValidationSnapshot validation)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int errors = validation != null ? validation.ErrorCount : 1;
            bool canExport = errors == 0;
            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionPublishExport,
                "Export Scenario Package",
                canExport
                    ? "Write a staged scenario package and validate the exported artifact."
                    : "Fix validation errors before exporting.",
                canExport,
                canExport,
                "EX",
                canExport ? "Creates ScenarioAuthoringExports/<scenario-id>/scenario.xml. Copy the exported folder into any mod's Scenarios directory to install or share it." : errors.ToString(CultureInfo.InvariantCulture) + " validation error(s) block export.")));

            ScenarioPublishExportResult last = GetLastExportResult();
            if (last == null)
            {
                items.Add(Item.Property("Last Export", "<none>"));
                items.Add(Item.Text("Export staging is outside playable catalog scans. After export, install by copying the exported scenario folder into a mod's Scenarios directory."));
                return items;
            }

            items.Add(Item.Property("Last Export", last.Success ? "Validated" : last.Blocked ? "Blocked" : "Failed"));
            items.Add(Item.Property("When", last.FormatTimestamp()));
            if (!string.IsNullOrEmpty(last.ArtifactPath))
                items.Add(Item.Property("Artifact", last.ArtifactPath));
            if (!string.IsNullOrEmpty(last.ArtifactRootPath))
            {
                items.Add(Item.Property("Share Folder", last.ArtifactRootPath));
                items.Add(Item.ActionItem(Item.Action(ScenarioPublishActionIds.OpenLastExportFolder, "Open Export Folder", "Open the last export folder in Windows Explorer.", true, false, "OP", last.ArtifactRootPath)));
                items.Add(Item.ActionItem(Item.Action(ScenarioPublishActionIds.CopyLastExportPath, "Copy Path", "Copy the last export folder path to the clipboard.", true, false, "CP", last.ArtifactRootPath)));
            }
            if (!string.IsNullOrEmpty(last.Message))
                items.Add(Item.Text(last.Message));
            items.Add(Item.Text("Install: 1. Open the export folder. 2. Copy the scenario folder. 3. Paste it into the Scenarios folder of the target mod. 4. Restart or reload the scenario catalog before testing it from the scenario book."));
            return items;
        }

        private static ScenarioAuthoringInspectorAction BuildIssueNavigationAction(ScenarioValidationIssue issue)
        {
            ScenarioStageKind stage = ResolveIssueStage(issue != null ? issue.Message : null);
            return Item.Action(
                ScenarioAuthoringActionIds.ActionStageSelectPrefix + stage,
                "Go To " + ScenarioAuthoringWorkflowLabels.GetStageLabel(stage, false),
                "Open the editor area most likely to own this validation issue.",
                true,
                issue != null,
                "GO",
                issue != null ? issue.Severity.ToString() : "Issue");
        }

        private static ScenarioStageKind ResolveIssueStage(string message)
        {
            string text = message != null ? message.ToLowerInvariant() : string.Empty;
            if (text.IndexOf("map") >= 0 || text.IndexOf("location") >= 0 || text.IndexOf("route") >= 0 || text.IndexOf("terrain") >= 0)
                return ScenarioStageKind.Map;
            if (text.IndexOf("quest") >= 0 || text.IndexOf("dialogue") >= 0)
                return ScenarioStageKind.Quests;
            if (text.IndexOf("inventory") >= 0 || text.IndexOf("item") >= 0)
                return ScenarioStageKind.InventoryStorage;
            if (text.IndexOf("family") >= 0 || text.IndexOf("survivor") >= 0 || text.IndexOf("character") >= 0)
                return ScenarioStageKind.People;
            if (text.IndexOf("sprite") >= 0 || text.IndexOf("asset") >= 0 || text.IndexOf("png") >= 0)
                return ScenarioStageKind.BunkerInside;
            if (text.IndexOf("victory") >= 0 || text.IndexOf("win") >= 0 || text.IndexOf("loss") >= 0 || text.IndexOf("end state") >= 0)
                return ScenarioStageKind.Test;
            if (text.IndexOf("trigger") >= 0 || text.IndexOf("schedule") >= 0 || text.IndexOf("gate") >= 0 || text.IndexOf("condition") >= 0 || text.IndexOf("action") >= 0)
                return ScenarioStageKind.Events;
            if (text.IndexOf("bunker") >= 0 || text.IndexOf("room") >= 0 || text.IndexOf("object") >= 0 || text.IndexOf("foundation") >= 0)
                return ScenarioStageKind.BunkerInside;
            return ScenarioStageKind.Publish;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildReadinessSummary(
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition,
            ScenarioAuthoringValidationSnapshot validation,
            ScenarioModCompatibilityReport compatibilityReport)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            items.Add(Item.Property("Errors", validation != null ? validation.ErrorCount.ToString(CultureInfo.InvariantCulture) : "n/a"));
            items.Add(Item.Property("Warnings", validation != null ? validation.WarningCount.ToString(CultureInfo.InvariantCulture) : "n/a"));
            items.Add(Item.Property("Dirty Sections", Item.CountDirtyFlags(editorSession).ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Unsupported Features", CountUnsupportedWarnings(validation).ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Missing Required Mods", compatibilityReport != null && compatibilityReport.MissingRequiredMods != null ? compatibilityReport.MissingRequiredMods.Count.ToString(CultureInfo.InvariantCulture) : "0"));
            ShelteredScenarioDefBuilderCompatibility compatibility = ShelteredScenarioDefBuilder.CheckCompatibility();
            items.Add(Item.Property("Reflection Compatibility", compatibility != null && compatibility.IsUsable ? "Ready" : "Risk"));
            if (compatibility != null && !compatibility.IsUsable)
                items.Add(Item.Text("Reflection compatibility risk: " + compatibility.DescribeFailures()));
            if (definition != null && definition.WinLossConditions != null && definition.WinLossConditions.WinConditions.Count + definition.WinLossConditions.LossConditions.Count > 0)
                items.Add(Item.Property("End State", definition.WinLossConditions.WinConditions.Count.ToString(CultureInfo.InvariantCulture) + " win / " + definition.WinLossConditions.LossConditions.Count.ToString(CultureInfo.InvariantCulture) + " loss"));
            else
                items.Add(Item.Property("End State", "No victory condition - scenario runs forever"));
            return items;
        }

        private static int CountUnsupportedWarnings(ScenarioAuthoringValidationSnapshot validation)
        {
            int count = 0;
            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : null;
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                string message = issues[i] != null ? issues[i].Message : null;
                if (message == null)
                    continue;
                string text = message.ToLowerInvariant();
                if (text.IndexOf("not applied at runtime yet") >= 0
                    || text.IndexOf("unsupported") >= 0
                    || text.IndexOf("pre-alpha") >= 0)
                    count++;
            }

            return count;
        }

        private static ScenarioPublishExportResult GetLastExportResult()
        {
            try
            {
                ScenarioPublishExportService service = ScenarioCompositionRoot.Resolve<ScenarioPublishExportService>();
                return service != null ? service.LastResult : null;
            }
            catch
            {
                return null;
            }
        }

        internal static List<ScenarioAuthoringInspectorItem> BuildDependencyItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int objectCount = definition != null && definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null ? definition.BunkerEdits.ObjectPlacements.Count : 0;
            int foundationCount = definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Foundations != null ? definition.BunkerGrid.Foundations.Count : 0;
            int expansionCount = definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null ? definition.BunkerGrid.Expansions.Count : 0;
            int gateCount = definition != null && definition.Gates != null ? definition.Gates.Count : 0;
            items.Add(Item.Property("Objects", objectCount.ToString()));
            items.Add(Item.Property("Foundations", foundationCount.ToString()));
            items.Add(Item.Property("Expansions", expansionCount.ToString()));
            items.Add(Item.Property("Gates", gateCount.ToString()));
            items.Add(Item.Property("Runtime Compatibility", "Shared schedule journal required"));
            return items;
        }

        internal static List<ScenarioAuthoringInspectorItem> BuildTimelineItems(ScenarioDefinition definition, ScenarioRuntimeState runtimeState, ScenarioTimelineBuilder timelineBuilder)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioTimelineEntry> entries = timelineBuilder != null ? timelineBuilder.BuildEntries(definition, runtimeState) : new List<ScenarioTimelineEntry>();
            for (int i = 0; entries != null && i < entries.Count && i < 12; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                if (entry != null)
                    items.Add(Item.Property("Day " + entry.When.Day + " " + Item.Safe(entry.Title), ScenarioScheduleFormatter.Format(entry.When) + " / " + entry.Kind + " / " + entry.Status));
            }
            if (items.Count == 0)
                items.Add(Item.Text("No scheduled timeline entries are authored yet."));
            return items;
        }

        internal static ScenarioRuntimeState GetRuntimeState()
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
    }

    internal sealed class ScenarioRuntimeTestAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        private readonly ScenarioTimelineBuilder _timelineBuilder;
        private readonly ScenarioModDependencyDetector _modDependencyDetector;
        private readonly ScenarioModCompatibilityViewModelBuilder _modCompatibilityViewModelBuilder;

        public ScenarioRuntimeTestAuthoringContentBuilder(
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioModDependencyDetector modDependencyDetector,
            ScenarioModCompatibilityViewModelBuilder modCompatibilityViewModelBuilder)
        {
            _timelineBuilder = timelineBuilder;
            _modDependencyDetector = modDependencyDetector;
            _modCompatibilityViewModelBuilder = modCompatibilityViewModelBuilder;
        }

        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Scenario; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState authoringState = context != null ? context.State : null;
            ScenarioEditorSession editorSession = context != null ? context.EditorSession : null;
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioAuthoringValidationSnapshot validation = ScenarioPublishAuthoringContentBuilder.EvaluateValidation(authoringState, definition);
            List<ScenarioAuthoringInspectorItem> controlItems = BuildPlaytestControlItems(editorSession, definition, validation);
            List<ScenarioAuthoringInspectorItem> runSettingItems = BuildRunSettingItems(definition);
            List<ScenarioAuthoringInspectorItem> preflightItems = ScenarioPublishAuthoringContentBuilder.BuildValidationItems(validation);
            List<ScenarioAuthoringInspectorItem> resultItems = BuildPlaytestResultItems(editorSession);
            List<ScenarioAuthoringInspectorItem> journalItems = BuildRuntimeJournalItems();
            List<ScenarioAuthoringInspectorItem> pendingItems = ScenarioPublishAuthoringContentBuilder.BuildTimelineItems(definition, ScenarioPublishAuthoringContentBuilder.GetRuntimeState(), _timelineBuilder);
            List<ScenarioAuthoringInspectorItem> compatibilityItems = _modCompatibilityViewModelBuilder.BuildItems(_modDependencyDetector.BuildReport(definition));
            return new[]
            {
                new ScenarioAuthoringInspectorSection
                {
                    Id = "test_controls",
                    Title = editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting ? "TESTING - Stop & return" : "Playtest Control",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.Summary,
                    Items = controlItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "test_run_settings",
                    Title = "Run Settings",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = runSettingItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "test_preflight",
                    Title = "Pre-flight Validation",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = preflightItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "test_results",
                    Title = "Results / Summary",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = resultItems.ToArray()
                },
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

        private static List<ScenarioAuthoringInspectorItem> BuildPlaytestControlItems(
            ScenarioEditorSession editorSession,
            ScenarioDefinition definition,
            ScenarioAuthoringValidationSnapshot validation)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            bool isPlaytesting = editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting;
            string playStartReason;
            bool canStartFromCast = new ScenarioPlayStartReadiness().CanStartPlay(definition, out playStartReason);
            bool canStart = canStartFromCast && validation != null && validation.ValidationAvailable && validation.ErrorCount == 0;
            items.Add(Item.Property("State", editorSession != null ? editorSession.PlaytestState.ToString() : "Unavailable"));
            items.Add(Item.Property("Pre-flight", canStart ? "Ready" : isPlaytesting ? "Already running" : "Blocked"));
            items.Add(Item.Property("Errors", validation != null ? validation.ErrorCount.ToString(CultureInfo.InvariantCulture) : "n/a"));
            items.Add(Item.Property("Warnings", validation != null ? validation.WarningCount.ToString(CultureInfo.InvariantCulture) : "n/a"));
            items.Add(Item.Text(isPlaytesting
                ? "The editor is in live test mode. Use Stop & return here or the slim End Test control in the status bar to restore frozen authoring."
                : canStartFromCast ? "Start Playtest applies the current draft to the live shelter. Blocking validation errors stop the transition." : playStartReason));
            ScenarioAuthoringInspectorAction action = Item.Action(
                ScenarioAuthoringActionIds.ActionPlaytest,
                isPlaytesting ? "Stop & Return" : "Start Playtest",
                isPlaytesting ? "Stop playtest and return to frozen authoring." : "Apply the validated draft into the live world.",
                isPlaytesting || canStart,
                isPlaytesting || canStart,
                isPlaytesting ? "ST" : "GO",
                isPlaytesting ? "Authoring pause is restored immediately." : canStart ? "Pre-flight validation has no blocking errors." : canStartFromCast ? "Fix blocking validation errors first." : playStartReason);
            if (!action.Enabled)
                action.DisabledReason = canStartFromCast ? "Blocking validation errors must be fixed before playtest." : playStartReason;
            items.Add(Item.ActionItem(action));
            if (isPlaytesting)
            {
                items.Add(Item.ActionItem(Item.Action(
                    ScenarioAuthoringActionIds.ActionPlaytestRestart,
                    "Restart",
                    "Save the draft and reload the authored world. This is a full restart, not an in-place tick rewind.",
                    true,
                    false,
                    "RS",
                    "Reloads through the safe authoring launch path.")));
            }
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save / Revalidate", "Run validation and save if there are no blocking errors.", true, false, "SV")));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildRunSettingItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            bool fixedSeed = definition != null && definition.SeedOverride.HasValue;
            string seedValue = fixedSeed ? definition.SeedOverride.Value.ToString(CultureInfo.InvariantCulture) : "Random";

            items.Add(Item.Property("Seed Policy", fixedSeed ? "Fixed" : "Random"));
            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionScenarioSeedRandom,
                "Random",
                "Use the current save's ModAPI.ModRandom seed. This is the schema default when no fixed seed is saved.",
                definition != null,
                !fixedSeed,
                "RD")));
            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionScenarioSeedFixed,
                "Fixed",
                "Persist a signed 32-bit seed and reset ModAPI.ModRandom when the scenario applies.",
                definition != null,
                fixedSeed,
                "FX")));

            ScenarioAuthoringInspectorItem seedItem = Item.Property("Fixed Seed", seedValue);
            seedItem.Editable = fixedSeed;
            seedItem.HoverHint = fixedSeed
                ? "Enter a signed 32-bit integer seed."
                : "Switch to Fixed before editing the seed value.";
            seedItem.Action = Item.Action(
                ScenarioAuthoringActionIds.ActionScenarioSeedValuePrefix,
                "Set Seed",
                seedItem.HoverHint,
                fixedSeed,
                false,
                "SD");
            items.Add(seedItem);

            items.Add(Item.ActionItem(Item.Action(
                ScenarioAuthoringActionIds.ActionScenarioSeedReroll,
                fixedSeed ? "Reroll Fixed Seed" : "Set Random Fixed Seed",
                "Generate a new fixed seed using ModAPI.ModRandom.",
                definition != null,
                false,
                "RR")));
            items.Add(Item.Text("Fixed seeds reset ModAPI.ModRandom when playtest or runtime apply starts. Vanilla UnityEngine.Random and System.Random consumers are not controlled."));
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildRuntimeJournalItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioRuntimeState state = ScenarioPublishAuthoringContentBuilder.GetRuntimeState();

            items.Add(Item.Property("Scenario", Item.Safe(state != null ? state.ScenarioId : null)));
            items.Add(Item.Property("Binding", Item.Safe(state != null ? state.RuntimeBindingId : null)));
            items.Add(Item.Property("Outcome", Item.Safe(state != null ? state.ScenarioOutcome : null)));
            if (state != null && !string.IsNullOrEmpty(state.ScenarioOutcomeConditionId))
                items.Add(Item.Property("Outcome Condition", state.ScenarioOutcomeConditionId));
            items.Add(Item.Property("Last Processed", state != null ? "day " + state.LastProcessedDay + " " + state.LastProcessedHour.ToString("D2") + ":" + state.LastProcessedMinute.ToString("D2") : "None"));
            int count = state != null && state.ExecutedActions != null ? state.ExecutedActions.Count : 0;
            items.Add(Item.Property("Executed Actions", count.ToString()));
            for (int i = 0; state != null && state.ExecutedActions != null && i < state.ExecutedActions.Count && i < 8; i++)
            {
                ScenarioExecutedActionRecord record = state.ExecutedActions[i];
                if (record != null)
                    items.Add(Item.Property(Item.Safe(record.ActionKey), record.Status + " / day " + record.FiredDay + " " + record.FiredHour.ToString("D2") + ":" + record.FiredMinute.ToString("D2")));
            }
            return items;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildPlaytestResultItems(ScenarioEditorSession editorSession)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioRuntimeState state = ScenarioPublishAuthoringContentBuilder.GetRuntimeState();
            if (editorSession == null)
            {
                items.Add(Item.Text("No active editor session."));
                return items;
            }

            if (state == null || string.IsNullOrEmpty(state.ScenarioId))
            {
                items.Add(Item.Text("No runtime journal has been recorded for this draft yet."));
                return items;
            }

            items.Add(Item.Property("Latest Test State", editorSession.PlaytestState.ToString()));
            items.Add(Item.Property("Day Reached", "day " + state.LastProcessedDay.ToString(CultureInfo.InvariantCulture) + " " + state.LastProcessedHour.ToString("D2") + ":" + state.LastProcessedMinute.ToString("D2")));
            items.Add(Item.Property("Outcome", string.IsNullOrEmpty(state.ScenarioOutcome) ? "Unresolved" : state.ScenarioOutcome));
            items.Add(Item.Property("World Changes", Count(state.ExecutedActions).ToString(CultureInfo.InvariantCulture) + " actions / " + Count(state.FiredTriggers).ToString(CultureInfo.InvariantCulture) + " triggers / " + Count(state.Flags).ToString(CultureInfo.InvariantCulture) + " flags / " + Count(state.UnlockedBunker).ToString(CultureInfo.InvariantCulture) + " unlocks / " + Count(state.ObjectStates).ToString(CultureInfo.InvariantCulture) + " object states"));
            int failed = CountRecords(state, ScenarioExecutedActionStatus.Failed);
            int blocked = CountRecords(state, ScenarioExecutedActionStatus.Blocked);
            items.Add(Item.Property("Runtime Errors", failed.ToString(CultureInfo.InvariantCulture) + " failed / " + blocked.ToString(CultureInfo.InvariantCulture) + " blocked"));
            AddRuntimeRecordItems(items, state, ScenarioExecutedActionStatus.Failed, "Failed");
            AddRuntimeRecordItems(items, state, ScenarioExecutedActionStatus.Blocked, "Blocked");
            return items;
        }

        private static void AddRuntimeRecordItems(
            List<ScenarioAuthoringInspectorItem> items,
            ScenarioRuntimeState state,
            ScenarioExecutedActionStatus status,
            string label)
        {
            int added = 0;
            for (int i = 0; state != null && state.ExecutedActions != null && i < state.ExecutedActions.Count && added < 4; i++)
            {
                ScenarioExecutedActionRecord record = state.ExecutedActions[i];
                if (record == null || record.Status != status)
                    continue;
                items.Add(Item.Property(label + " " + Item.Safe(record.ActionKey), string.IsNullOrEmpty(record.Message) ? record.ActionType : record.Message));
                added++;
            }
        }

        private static int Count<T>(IList<T> values)
        {
            return values != null ? values.Count : 0;
        }

        private static int CountRecords(ScenarioRuntimeState state, ScenarioExecutedActionStatus status)
        {
            int count = 0;
            for (int i = 0; state != null && state.ExecutedActions != null && i < state.ExecutedActions.Count; i++)
            {
                if (state.ExecutedActions[i] != null && state.ExecutedActions[i].Status == status)
                    count++;
            }

            return count;
        }
    }

    internal sealed class ScenarioTimelineAuthoringContentBuilder : IScenarioAuthoringWindowContentBuilder
    {
        private readonly ScenarioTimelineBuilder _timelineBuilder;
        private readonly ScenarioTimelineViewModelBuilder _timelineViewModelBuilder;

        public ScenarioTimelineAuthoringContentBuilder(
            ScenarioTimelineBuilder timelineBuilder,
            ScenarioTimelineViewModelBuilder timelineViewModelBuilder)
        {
            _timelineBuilder = timelineBuilder;
            _timelineViewModelBuilder = timelineViewModelBuilder;
        }

        public ScenarioAuthoringWindowContentKind ContentKind { get { return ScenarioAuthoringWindowContentKind.Triggers; } }

        public ScenarioAuthoringInspectorSection[] Build(ScenarioAuthoringWindowContentContext context)
        {
            ScenarioAuthoringState state = context != null ? context.State : null;
            ScenarioDefinition definition = context != null ? context.Definition : null;
            ScenarioRuntimeState runtimeState = ScenarioPublishAuthoringContentBuilder.GetRuntimeState();
            ScenarioTimelineViewModel model = _timelineViewModelBuilder.Build(_timelineBuilder.BuildDays(definition, runtimeState));
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            List<ScenarioAuthoringInspectorItem> dayItems = new List<ScenarioAuthoringInspectorItem>();
            for (int i = 0; model != null && model.Days != null && i < model.Days.Length; i++)
            {
                ScenarioTimelineDayViewModel day = model.Days[i];
                dayItems.Add(Item.ActionItem(Item.Action(
                    ScenarioAuthoringActionIds.ActionTimelineDayPrefix + day.Day.ToString(CultureInfo.InvariantCulture),
                    "Day " + day.Day.ToString(CultureInfo.InvariantCulture),
                    day.Count.ToString(CultureInfo.InvariantCulture) + " scheduled item(s).",
                    true,
                    false,
                    day.Badge,
                    day.Categories)));
            }
            if (dayItems.Count == 0)
                dayItems.Add(Item.Text("No scheduled scenario events are currently authored."));

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "timeline_days",
                Title = "Timeline Days",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = dayItems.ToArray()
            });

            string selected = state != null ? state.TimelineSelectedDayId : null;
            for (int i = 0; model != null && model.Days != null && i < model.Days.Length; i++)
            {
                ScenarioTimelineDayViewModel day = model.Days[i];
                if (selected != null && !string.Equals(selected, day.Day.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase))
                    continue;

                List<ScenarioAuthoringInspectorItem> entries = new List<ScenarioAuthoringInspectorItem>();
                for (int e = 0; day.Entries != null && e < day.Entries.Length; e++)
                {
                    ScenarioTimelineEntryViewModel entry = day.Entries[e];
                    ScenarioAuthoringInspectorItem fact = Item.Property(
                        entry.Time + " " + entry.Title,
                        entry.Type + " / " + entry.OwnerStage + " / " + entry.Status);
                    fact.Detail = string.IsNullOrEmpty(entry.Warning) ? entry.OwnerStage : entry.Warning;
                    fact.Badge = StatusBadge(entry.Status);
                    if (state != null && string.Equals(state.TimelineSelectedEntryId, entry.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        fact.PulseKey = "timeline.entry." + entry.Id;
                        fact.PulseSignature = entry.Id + ":" + entry.Status;
                    }
                    entries.Add(fact);
                    entries.Add(Item.ActionItem(Item.Action(
                        entry.ActionId,
                        "Edit",
                        "Open this timeline entry.",
                        true,
                        entry.Status == "Blocked" || entry.Status == "Failed",
                        StatusBadge(entry.Status),
                        string.IsNullOrEmpty(entry.Warning) ? entry.OwnerStage : entry.Warning)));
                }

                sections.Add(new ScenarioAuthoringInspectorSection
                {
                    Id = "timeline_day_" + day.Day.ToString(CultureInfo.InvariantCulture),
                    Title = "Day " + day.Day.ToString(CultureInfo.InvariantCulture),
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                    Items = entries.ToArray()
                });
            }

            ScenarioAuthoringInspectorSection[] triggerSections = ScenarioAuthoringPresentationBuilder.BuildTriggerWindowSections(state, definition);
            for (int i = 0; triggerSections != null && i < triggerSections.Length; i++)
                sections.Add(triggerSections[i]);

            return sections.ToArray();
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
    }
}
