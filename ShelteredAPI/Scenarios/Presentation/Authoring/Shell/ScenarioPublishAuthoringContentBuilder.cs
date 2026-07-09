using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Commands;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Compatibility;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Presentation.Timeline;
using ShelteredAPI.Scenarios.Shared;

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
            List<ScenarioAuthoringInspectorItem> metadataItems = new List<ScenarioAuthoringInspectorItem>(ScenarioMetadataAuthoringContent.BuildEditableItems(definition, false));
            metadataItems.AddRange(ScenarioMetadataAuthoringContent.BuildStatusItems(state != null ? state.ActiveScenarioFilePath : null));
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
                    Id = "publish_metadata",
                    Title = "Scenario Metadata",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = metadataItems.ToArray()
                },
                new ScenarioAuthoringInspectorSection
                {
                    Id = "publish_stage",
                    Title = "Package Readiness",
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = new[]
                    {
                        Item.Property("Scenario", Item.Safe(definition != null ? definition.DisplayName : null)),
                        Item.Property("Dirty Sections", Item.CountDirtyFlags(editorSession).ToString()),
                        Item.Property("Version", Item.Safe(definition != null ? definition.Version : null)),
                        Item.Text("This window creates a local package for sharing. It does not upload anywhere.")
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
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save / Revalidate", "Run validation and save the current draft.", true, true, "SV")));

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
                    string issueTopic = ResolveIssueTopic(issue.Message);
                    if (!string.IsNullOrEmpty(issueTopic))
                        items.Add(Item.ActionItem(BuildIssueTopicAction(issueTopic)));
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
            if (!canExport)
            {
                items.Add(Item.ActionItem(Item.Action(
                    ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicPublish,
                    "How to Fix Export Blockers",
                    "Open publish validation guidance and follow each issue link.",
                    true,
                    false,
                    "HP")));
            }

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
            string message = issue != null ? issue.Message : null;
            if (!string.IsNullOrEmpty(message) && message.IndexOf("Scenario metadata", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Item.Action(
                    ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Publish,
                    "Go To Metadata",
                    "Open the package metadata form for this sharing detail.",
                    true,
                    true,
                    "GO",
                    issue.Severity.ToString());
            }
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

        private static ScenarioAuthoringInspectorAction BuildIssueTopicAction(string issueTopic)
        {
            if (string.IsNullOrEmpty(issueTopic))
                return null;

            return Item.Action(
                ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + issueTopic,
                "How to Fix This",
                "Open the relevant help topic and apply the concrete fix.",
                true,
                false,
                "HP");
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

        private static string ResolveIssueTopic(string issueMessage)
        {
            string text = issueMessage != null ? issueMessage.ToLowerInvariant() : string.Empty;
            if (text.IndexOf("survivor") >= 0 || text.IndexOf("starting") >= 0 || text.IndexOf("family") >= 0 || text.IndexOf("cast") >= 0)
                return TutorialContent.TopicCast;
            if (text.IndexOf("supply") >= 0 || text.IndexOf("inventory") >= 0 || text.IndexOf("item") >= 0 || text.IndexOf("water") >= 0 || text.IndexOf("food") >= 0 || text.IndexOf("medicine") >= 0)
                return TutorialContent.TopicSupplies;
            if (text.IndexOf("map") >= 0 || text.IndexOf("location") >= 0 || text.IndexOf("route") >= 0 || text.IndexOf("encounter") >= 0 || text.IndexOf("terrain") >= 0)
                return TutorialContent.TopicMap;
            if (text.IndexOf("quest") >= 0 || text.IndexOf("story") >= 0 || text.IndexOf("dialogue") >= 0)
                return TutorialContent.TopicStory;
            if (text.IndexOf("mod") >= 0 || text.IndexOf("dependency") >= 0 || text.IndexOf("required mod") >= 0 || text.IndexOf("missing") >= 0 || text.IndexOf("unsupported") >= 0)
                return TutorialContent.TopicModGating;
            if (text.IndexOf("win") >= 0 || text.IndexOf("loss") >= 0 || text.IndexOf("end state") >= 0)
                return TutorialContent.TopicPublish;
            if (text.IndexOf("trigger") >= 0 || text.IndexOf("schedule") >= 0 || text.IndexOf("condition") >= 0 || text.IndexOf("action") >= 0 || text.IndexOf("weather") >= 0 || text.IndexOf("gate") >= 0)
                return TutorialContent.TopicTimelineConditions;
            if (text.IndexOf("sprite") >= 0 || text.IndexOf("asset") >= 0 || text.IndexOf("png") >= 0)
                return TutorialContent.TopicArtPixelEditor;

            return null;
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
            if (compatibilityReport != null
                && (
                    (compatibilityReport.MissingRequiredMods != null && compatibilityReport.MissingRequiredMods.Count > 0)
                    || (compatibilityReport.VersionMismatches != null && compatibilityReport.VersionMismatches.Count > 0)
                    || (compatibilityReport.UnknownReferences != null && compatibilityReport.UnknownReferences.Count > 0)))
            {
                items.Add(Item.ActionItem(Item.Action(
                    ScenarioAuthoringActionIds.ActionHelpOpenTopicPrefix + TutorialContent.TopicModGating,
                    "Mod Gating Guidance",
                    "Open why these dependencies are blocked and what to install or update.",
                    true,
                    false,
                    "MOD")));
            }
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
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>
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
            if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                sections.AddRange(ScenarioTestConsoleAuthoringContentBuilder.Build(context));
            return sections.ToArray();
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
            {
                action.DisabledReason = canStartFromCast ? "Blocking validation errors must be fixed before playtest." : playStartReason;
                ScenarioAuthoringInspectorAction fixAction = ScenarioPlaytestFixActionResolver.BuildFixAction(playStartReason);
                if (fixAction != null)
                    items.Add(Item.ActionItem(fixAction));
            }
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
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save / Revalidate", "Run validation and save the current draft.", true, true, "SV")));
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
        private const string TimelineDayMetadataPrefix = "timeline-day|";
        private const string TimelineChipMetadataPrefix = "timeline-chip|";
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
            List<ScenarioTimelineEntry> entries = _timelineBuilder != null
                ? _timelineBuilder.BuildEntries(definition, runtimeState)
                : new List<ScenarioTimelineEntry>();
            entries.Sort(CompareTimelineEntries);
            List<ScenarioAuthoringInspectorSection> sections = new List<ScenarioAuthoringInspectorSection>();

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "timeline_workshop_track",
                Title = "What happens, and when?",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.ActionStrip,
                Items = BuildTimelineTrackItems(state, definition, entries).ToArray()
            });

            sections.Add(new ScenarioAuthoringInspectorSection
            {
                Id = "timeline_logic",
                Title = "Logic",
                Expanded = true,
                Layout = ScenarioAuthoringInspectorSectionLayout.FactGrid,
                Items = BuildTimelineLogicItems(definition).ToArray()
            });

            return sections.ToArray();
        }

        private List<ScenarioAuthoringInspectorItem> BuildTimelineTrackItems(ScenarioAuthoringState state, ScenarioDefinition definition, List<ScenarioTimelineEntry> entries)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            AddTimelineAddActions(items);

            int lastDay = ResolveLastDay(entries);
            int visibleDayCount = Math.Max(5, lastDay + 2);
            string currentWeather = GetCurrentWeatherSummary();
            for (int day = 1; day <= visibleDayCount; day++)
            {
                int count = CountEntriesForDay(entries, day);
                string baseline = day == 1 ? currentWeather : string.Empty;
                items.Add(Item.ActionItem(TimelineDayAction(day, count, baseline)));
            }

            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                if (entry != null)
                    items.Add(Item.ActionItem(TimelineChipAction(state, definition, entry)));
            }

            return items;
        }

        private static void AddTimelineAddActions(List<ScenarioAuthoringInspectorItem> items)
        {
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionWeatherScheduleAdd, "Weather", "Schedule rain, storms, or a weather restore on a scenario day.", true, true, "WE", "Add event")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionScheduledActionAdd, "Supply Change", "Create a timed supply, survivor, or quest-impacting change with effects, conditions, and repeat rules.", true, true, "A+", "Add event")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionWorldEventAdd, "World Event", "Schedule a typed visitor, raid, or radio outcome.", true, true, "WEV", "Add event")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTriggerAddScheduled, "Timed Trigger", "Create a trigger that fires at a specific scenario time.", true, false, "TS", "Add event")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionFutureSurvivorAdd, "Arrival", "Create a survivor who arrives or asks to join later.", true, false, "FS", "Add event")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionStageSelectPrefix + ScenarioStageKind.Quests, "Story Beats", "Open Story to author vanilla scenario stages and encounter beats.", true, false, "ST", "Story link")));
        }

        private static ScenarioAuthoringInspectorAction TimelineDayAction(int day, int count, string baseline)
        {
            ScenarioAuthoringInspectorAction action = Item.Action(
                ScenarioAuthoringActionIds.ActionTimelineDayPrefix + day.ToString(CultureInfo.InvariantCulture),
                "Day " + day.ToString(CultureInfo.InvariantCulture),
                count.ToString(CultureInfo.InvariantCulture) + " scheduled item(s).",
                true,
                count > 0,
                "D" + day.ToString(CultureInfo.InvariantCulture),
                baseline,
                count > 0 ? count.ToString(CultureInfo.InvariantCulture) : string.Empty);
            action.DisabledReason = TimelineDayMetadataPrefix
                + day.ToString(CultureInfo.InvariantCulture)
                + "|"
                + EscapeMetadata(baseline)
                + "|"
                + count.ToString(CultureInfo.InvariantCulture);
            return action;
        }

        private static ScenarioAuthoringInspectorAction TimelineChipAction(ScenarioAuthoringState state, ScenarioDefinition definition, ScenarioTimelineEntry entry)
        {
            int day = entry != null && entry.When != null ? Math.Max(1, entry.When.Day) : 1;
            string time = FormatTimelineTime(entry != null ? entry.When : null);
            string domain = ResolveTimelineDomain(entry);
            string label = BuildTimelineChipLabel(definition, entry);
            string status = entry != null ? entry.Status.ToString() : "Pending";
            string hint = "Day " + day.ToString(CultureInfo.InvariantCulture) + " " + time + ": " + label + ". Click to focus this authored entry.";
            if (entry != null && !string.IsNullOrEmpty(entry.Warning))
                hint = hint + " " + entry.Warning;

            bool emphasized = state != null
                && entry != null
                && string.Equals(state.TimelineSelectedEntryId, entry.Id, StringComparison.OrdinalIgnoreCase);
            if (string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                emphasized = true;

            ScenarioAuthoringInspectorAction action = Item.Action(
                ScenarioAuthoringActionIds.ActionTimelineEntryPrefix + (entry != null ? entry.Id : string.Empty),
                time + " " + label,
                hint,
                true,
                emphasized,
                ResolveTimelineIconText(domain),
                ResolveTimelineChipDetail(definition, entry),
                StatusBadge(status));
            action.DisabledReason = TimelineChipMetadataPrefix
                + day.ToString(CultureInfo.InvariantCulture)
                + "|"
                + EscapeMetadata(domain)
                + "|"
                + EscapeMetadata(time)
                + "|"
                + EscapeMetadata(status);
            return action;
        }

        private static List<ScenarioAuthoringInspectorItem> BuildTimelineLogicItems(ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            List<ScenarioAuthoringInspectorItem> graphItems = ScenarioEventGraphInspectorBuilder.BuildItems(definition);
            string nodes = FindPropertyValue(graphItems, "Nodes", "0");
            string edges = FindPropertyValue(graphItems, "Edges", "0");
            int manualTriggers = CountManualTriggers(definition);
            int timedTriggers = CountTimedTriggers(definition);
            int gates = definition != null && definition.Gates != null ? definition.Gates.Count : 0;
            int flagEffects = CountFlagEffects(definition);

            items.Add(Item.Property("Event Graph", nodes + " nodes / " + edges + " links", "Dependency status for triggers, gates, scheduled effects, quests, and outcomes."));
            items.Add(Item.Property("Manual Triggers", manualTriggers.ToString(CultureInfo.InvariantCulture), timedTriggers.ToString(CultureInfo.InvariantCulture) + " timed trigger(s) appear on the day track."));
            items.Add(Item.Property("Conditions / Flags", gates.ToString(CultureInfo.InvariantCulture) + " conditions / " + flagEffects.ToString(CultureInfo.InvariantCulture) + " flag effect(s)", "Untimed gates stay here instead of cluttering the day ruler."));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionTriggerAddManual, "Add Manual Trigger", "Create a trigger fired by code or another scheduled effect.", true, false, "T+")));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionGateAdd, "Add Condition", "Create a reusable condition for scheduled changes.", true, false, "C+")));
            return items;
        }

        private static string FindPropertyValue(List<ScenarioAuthoringInspectorItem> items, string label, string fallback)
        {
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ScenarioAuthoringInspectorItem item = items[i];
                if (item != null
                    && item.Kind == ScenarioAuthoringInspectorItemKind.Property
                    && string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase))
                    return item.Value;
            }

            return fallback;
        }

        private static int CountManualTriggers(ScenarioDefinition definition)
        {
            int count = 0;
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
                if (ScenarioTriggerDefinitionCompiler.IsManual(definition.TriggersAndEvents.Triggers[i]))
                    count++;

            return count;
        }

        private static int CountTimedTriggers(ScenarioDefinition definition)
        {
            int count = 0;
            for (int i = 0; definition != null && definition.TriggersAndEvents != null && definition.TriggersAndEvents.Triggers != null && i < definition.TriggersAndEvents.Triggers.Count; i++)
                if (!ScenarioTriggerDefinitionCompiler.IsManual(definition.TriggersAndEvents.Triggers[i]))
                    count++;

            return count;
        }

        private static int CountFlagEffects(ScenarioDefinition definition)
        {
            int count = 0;
            for (int i = 0; definition != null && definition.ScheduledActions != null && i < definition.ScheduledActions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = definition.ScheduledActions[i];
                for (int e = 0; action != null && action.Effects != null && e < action.Effects.Count; e++)
                {
                    ScenarioEffectDefinition effect = action.Effects[e];
                    if (effect != null && effect.Kind == ScenarioEffectKind.SetScenarioFlag)
                        count++;
                }
            }

            return count;
        }

        private static int ResolveLastDay(List<ScenarioTimelineEntry> entries)
        {
            int lastDay = 1;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                if (entry != null && entry.When != null)
                    lastDay = Math.Max(lastDay, entry.When.Day);
            }

            return lastDay;
        }

        private static int CountEntriesForDay(List<ScenarioTimelineEntry> entries, int day)
        {
            int count = 0;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                ScenarioTimelineEntry entry = entries[i];
                if (entry != null && entry.When != null && entry.When.Day == day)
                    count++;
            }

            return count;
        }

        private static int CompareTimelineEntries(ScenarioTimelineEntry left, ScenarioTimelineEntry right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int byDay = (left.When != null ? left.When.Day : 1).CompareTo(right.When != null ? right.When.Day : 1);
            if (byDay != 0)
                return byDay;
            int byHour = (left.When != null ? left.When.Hour : 0).CompareTo(right.When != null ? right.When.Hour : 0);
            if (byHour != 0)
                return byHour;
            return (left.When != null ? left.When.Minute : 0).CompareTo(right.When != null ? right.When.Minute : 0);
        }

        private static string BuildTimelineChipLabel(ScenarioDefinition definition, ScenarioTimelineEntry entry)
        {
            if (entry == null)
                return "Missing entry";

            if (string.Equals(entry.SourceKind, "weather_event", StringComparison.OrdinalIgnoreCase))
            {
                WeatherEventDefinition weather = GetWeather(definition, entry.SourceIndex);
                return weather != null ? FormatWeatherState(weather.WeatherState) : SafeLabel(entry.Title);
            }

            if (string.Equals(entry.SourceKind, "inventory_change", StringComparison.OrdinalIgnoreCase))
            {
                TimedInventoryChangeDefinition change = GetInventoryChange(definition, entry.SourceIndex);
                if (change != null)
                    return change.Kind + " " + SafeLabel(change.ItemId) + " x" + Math.Max(0, change.Quantity).ToString(CultureInfo.InvariantCulture);
            }

            if (string.Equals(entry.SourceKind, "future_survivor", StringComparison.OrdinalIgnoreCase))
            {
                FutureSurvivorDefinition survivor = GetFutureSurvivor(definition, entry.SourceIndex);
                if (survivor != null && survivor.Survivor != null)
                {
                    ScenarioActorRef actorRef = survivor.ActorRef != null ? survivor.ActorRef : survivor.Survivor.ActorRef;
                    return SafeLabel(ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, actorRef, false, true, survivor.Survivor.Name));
                }
            }

            if (string.Equals(entry.SourceKind, "scheduled_action", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioScheduledActionDefinition action = GetScheduledAction(definition, entry.SourceIndex);
                string label = BuildScheduledActionLabel(definition, action);
                if (!string.IsNullOrEmpty(label))
                    return label;
            }

            return SafeLabel(entry.Title);
        }

        private static string ResolveTimelineChipDetail(ScenarioDefinition definition, ScenarioTimelineEntry entry)
        {
            if (entry == null)
                return null;

            if (string.Equals(entry.SourceKind, "weather_event", StringComparison.OrdinalIgnoreCase))
            {
                WeatherEventDefinition weather = GetWeather(definition, entry.SourceIndex);
                if (weather != null && weather.DurationHours > 0)
                    return "Restores after " + weather.DurationHours.ToString(CultureInfo.InvariantCulture) + " hour(s)";
            }

            if (string.Equals(entry.SourceKind, "scheduled_action", StringComparison.OrdinalIgnoreCase))
            {
                ScenarioScheduledActionDefinition action = GetScheduledAction(definition, entry.SourceIndex);
                if (action != null && !string.IsNullOrEmpty(action.GateId))
                    return "Condition " + action.GateId;
            }

            return entry.OwnerStage;
        }

        private static string BuildScheduledActionLabel(ScenarioDefinition definition, ScenarioScheduledActionDefinition action)
        {
            for (int i = 0; action != null && action.Effects != null && i < action.Effects.Count; i++)
            {
                ScenarioEffectDefinition effect = action.Effects[i];
                if (effect == null)
                    continue;

                switch (effect.Kind)
                {
                    case ScenarioEffectKind.AddInventory:
                    case ScenarioEffectKind.RemoveInventory:
                        return (effect.Kind == ScenarioEffectKind.RemoveInventory ? "Remove " : "Add ")
                            + SafeLabel(effect.ItemId)
                            + " x"
                            + Math.Max(0, effect.Quantity).ToString(CultureInfo.InvariantCulture);
                    case ScenarioEffectKind.SetWeather:
                    case ScenarioEffectKind.RestoreWeather:
                        return FormatWeatherState(effect.WeatherState);
                    case ScenarioEffectKind.SpawnFutureSurvivor:
                        return SafeLabel(effect.ActorRef != null
                            ? ScenarioCastMemberReferenceCatalog.ResolveDisplayName(definition, effect.ActorRef, false, true, effect.SurvivorId ?? effect.TargetId)
                            : ResolveFutureSurvivorName(definition, effect.SurvivorId ?? effect.TargetId));
                    case ScenarioEffectKind.StartQuest:
                        return "Start " + SafeLabel(effect.QuestId);
                    case ScenarioEffectKind.FireTrigger:
                        return "Fire " + SafeLabel(effect.TriggerId);
                    case ScenarioEffectKind.SetScenarioFlag:
                        return "Flag " + SafeLabel(effect.FlagId);
                    case ScenarioEffectKind.WorldEvent:
                        return BuildWorldEventLabel(effect);
                    case ScenarioEffectKind.UnlockBunkerExpansion:
                        return "Unlock " + SafeLabel(effect.BunkerExpansionId);
                    case ScenarioEffectKind.ActivateObject:
                    case ScenarioEffectKind.DeactivateObject:
                        return effect.Kind + " " + SafeLabel(effect.ObjectId);
                }
            }

            return action != null ? SafeLabel(action.ActionType ?? action.Id) : null;
        }

        private static string ResolveTimelineDomain(ScenarioTimelineEntry entry)
        {
            if (entry == null)
                return "other";

            switch (entry.Kind)
            {
                case ScenarioTimelineEntryKind.Weather:
                    return "weather";
                case ScenarioTimelineEntryKind.Inventory:
                    return "inventory";
                case ScenarioTimelineEntryKind.Survivor:
                    return "arrival";
                case ScenarioTimelineEntryKind.Story:
                case ScenarioTimelineEntryKind.Quest:
                    return "story";
                case ScenarioTimelineEntryKind.WorldEvent:
                    return "world_event";
                case ScenarioTimelineEntryKind.Journal:
                    return "journal";
                case ScenarioTimelineEntryKind.CustomModded:
                    return string.Equals(entry.Type, "Trigger", StringComparison.OrdinalIgnoreCase) ? "trigger" : "change";
                default:
                    return "change";
            }
        }

        private static string ResolveTimelineIconText(string domain)
        {
            if (string.Equals(domain, "weather", StringComparison.OrdinalIgnoreCase))
                return "WE";
            if (string.Equals(domain, "inventory", StringComparison.OrdinalIgnoreCase))
                return "IV";
            if (string.Equals(domain, "arrival", StringComparison.OrdinalIgnoreCase))
                return "SV";
            if (string.Equals(domain, "trigger", StringComparison.OrdinalIgnoreCase))
                return "TR";
            if (string.Equals(domain, "story", StringComparison.OrdinalIgnoreCase))
                return "ST";
            if (string.Equals(domain, "world_event", StringComparison.OrdinalIgnoreCase))
                return "WEV";
            if (string.Equals(domain, "journal", StringComparison.OrdinalIgnoreCase))
                return "JR";
            return "EV";
        }

        private static string BuildWorldEventLabel(ScenarioEffectDefinition effect)
        {
            string eventType = ScenarioPropertyBag.GetString(effect != null ? effect.Properties : null, "eventType", "WorldEvent");
            if (string.Equals(eventType, "NpcVisit", StringComparison.OrdinalIgnoreCase))
                return SafeLabel(ScenarioPropertyBag.GetString(effect.Properties, "npcType", "Passerby"));
            if (string.Equals(eventType, "Raid", StringComparison.OrdinalIgnoreCase))
                return "Raid";
            if (string.Equals(eventType, "Broadcast", StringComparison.OrdinalIgnoreCase)
                || string.Equals(eventType, "RadioScan", StringComparison.OrdinalIgnoreCase))
                return "Broadcast";
            return SafeLabel(eventType);
        }

        private static string FormatTimelineTime(ScenarioScheduleTime time)
        {
            if (time == null)
                return "--:--";
            return time.Hour.ToString("D2", CultureInfo.InvariantCulture) + ":" + time.Minute.ToString("D2", CultureInfo.InvariantCulture);
        }

        private static string GetCurrentWeatherSummary()
        {
            WeatherManager manager = WeatherManager.Instance;
            if (manager == null)
                return "Weather unavailable / day 1";
            return manager.currentState + " / day " + manager.currentDay.ToString(CultureInfo.InvariantCulture);
        }

        private static WeatherEventDefinition GetWeather(ScenarioDefinition definition, int index)
        {
            return definition != null
                && definition.TriggersAndEvents != null
                && definition.TriggersAndEvents.WeatherEvents != null
                && index >= 0
                && index < definition.TriggersAndEvents.WeatherEvents.Count
                    ? definition.TriggersAndEvents.WeatherEvents[index]
                    : null;
        }

        private static TimedInventoryChangeDefinition GetInventoryChange(ScenarioDefinition definition, int index)
        {
            return definition != null
                && definition.StartingInventory != null
                && definition.StartingInventory.ScheduledChanges != null
                && index >= 0
                && index < definition.StartingInventory.ScheduledChanges.Count
                    ? definition.StartingInventory.ScheduledChanges[index]
                    : null;
        }

        private static FutureSurvivorDefinition GetFutureSurvivor(ScenarioDefinition definition, int index)
        {
            return definition != null
                && definition.FamilySetup != null
                && definition.FamilySetup.FutureSurvivors != null
                && index >= 0
                && index < definition.FamilySetup.FutureSurvivors.Count
                    ? definition.FamilySetup.FutureSurvivors[index]
                    : null;
        }

        private static ScenarioScheduledActionDefinition GetScheduledAction(ScenarioDefinition definition, int index)
        {
            return definition != null
                && definition.ScheduledActions != null
                && index >= 0
                && index < definition.ScheduledActions.Count
                    ? definition.ScheduledActions[index]
                    : null;
        }

        private static string ResolveFutureSurvivorName(ScenarioDefinition definition, string id)
        {
            for (int i = 0; definition != null && definition.FamilySetup != null && definition.FamilySetup.FutureSurvivors != null && i < definition.FamilySetup.FutureSurvivors.Count; i++)
            {
                FutureSurvivorDefinition survivor = definition.FamilySetup.FutureSurvivors[i];
                if (survivor != null && string.Equals(survivor.Id, id, StringComparison.OrdinalIgnoreCase))
                    return survivor.Survivor != null ? survivor.Survivor.Name : survivor.Id;
            }

            return id;
        }

        private static string FormatWeatherState(string state)
        {
            if (string.Equals(state, "None", StringComparison.OrdinalIgnoreCase))
                return "Clear Weather";
            if (string.Equals(state, "BlackRain", StringComparison.OrdinalIgnoreCase))
                return "Black Rain";
            if (string.Equals(state, "LightSand", StringComparison.OrdinalIgnoreCase))
                return "Light Sandstorm";
            if (string.Equals(state, "MediumSand", StringComparison.OrdinalIgnoreCase))
                return "Sandstorm";
            if (string.Equals(state, "HeavySand", StringComparison.OrdinalIgnoreCase))
                return "Heavy Sandstorm";
            return SafeLabel(state);
        }

        private static string SafeLabel(string value)
        {
            return string.IsNullOrEmpty(value) ? "<missing>" : value;
        }

        private static string EscapeMetadata(string value)
        {
            return (value ?? string.Empty).Replace("|", "/");
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
