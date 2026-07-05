using System;
using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Domain.Timeline;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
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
            List<ScenarioAuthoringInspectorItem> dependencyItems = BuildDependencyItems(definition);
            List<ScenarioAuthoringInspectorItem> compatibilityItems = _modCompatibilityViewModelBuilder.BuildItems(_modDependencyDetector.BuildReport(definition));
            List<ScenarioAuthoringInspectorItem> timelineItems = BuildTimelineItems(definition, GetRuntimeState(), _timelineBuilder);
            List<ScenarioAuthoringInspectorItem> validationItems = BuildValidationItems(state, definition);
            List<ScenarioAuthoringInspectorItem> exportItems = BuildExportItems(validationItems);
            return new[]
            {
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

        internal static List<ScenarioAuthoringInspectorItem> BuildValidationItems(ScenarioAuthoringState state, ScenarioDefinition definition)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            if (definition == null)
            {
                items.Add(Item.Property("Status", "No active scenario definition."));
                return items;
            }

            ScenarioValidationResult validation = null;
            try
            {
                IScenarioDefinitionValidator validator = ScenarioCompositionRoot.Resolve<IScenarioDefinitionValidator>();
                validation = validator != null ? validator.Validate(definition, state != null ? state.ActiveScenarioFilePath : null) : null;
            }
            catch (Exception ex)
            {
                items.Add(Item.Property("Validation", "Unavailable"));
                items.Add(Item.Text("Validation could not run: " + ex.Message));
                return items;
            }

            ScenarioValidationIssue[] issues = validation != null ? validation.Issues : new ScenarioValidationIssue[0];
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < issues.Length; i++)
            {
                if (issues[i] == null)
                    continue;
                if (issues[i].Severity == ScenarioIssueSeverity.Error)
                    errors++;
                else
                    warnings++;
            }

            items.Add(Item.Property("Status", validation != null && validation.IsValid ? "Ready to export" : "Blocked"));
            items.Add(Item.Property("Errors", errors.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.Property("Warnings", warnings.ToString(CultureInfo.InvariantCulture)));
            items.Add(Item.ActionItem(Item.Action(ScenarioAuthoringActionIds.ActionSave, "Save / Revalidate", "Run validation and save if the draft has no blocking errors.", true, errors == 0, "SV")));

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

        internal static List<ScenarioAuthoringInspectorItem> BuildExportItems(List<ScenarioAuthoringInspectorItem> validationItems)
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            int errors = ReadCount(validationItems, "Errors");
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
                items.Add(Item.Text("Export staging is outside playable catalog scans. Copy the exported folder into any mod's Scenarios directory when you want to install or share it."));
                return items;
            }

            items.Add(Item.Property("Last Export", last.Success ? "Validated" : last.Blocked ? "Blocked" : "Failed"));
            items.Add(Item.Property("When", last.FormatTimestamp()));
            if (!string.IsNullOrEmpty(last.ArtifactPath))
                items.Add(Item.Property("Artifact", last.ArtifactPath));
            if (!string.IsNullOrEmpty(last.ArtifactRootPath))
                items.Add(Item.Property("Share Folder", last.ArtifactRootPath));
            if (!string.IsNullOrEmpty(last.Message))
                items.Add(Item.Text(last.Message));
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
                issue != null && issue.Severity == ScenarioIssueSeverity.Error,
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
            if (text.IndexOf("trigger") >= 0 || text.IndexOf("schedule") >= 0 || text.IndexOf("gate") >= 0 || text.IndexOf("condition") >= 0 || text.IndexOf("action") >= 0)
                return ScenarioStageKind.Events;
            if (text.IndexOf("bunker") >= 0 || text.IndexOf("room") >= 0 || text.IndexOf("object") >= 0 || text.IndexOf("foundation") >= 0)
                return ScenarioStageKind.BunkerInside;
            return ScenarioStageKind.Publish;
        }

        private static int ReadCount(List<ScenarioAuthoringInspectorItem> items, string label)
        {
            for (int i = 0; items != null && i < items.Count; i++)
            {
                ScenarioAuthoringInspectorItem item = items[i];
                int value;
                if (item != null
                    && string.Equals(item.Label, label, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(item.Value, out value))
                    return value;
            }

            return 0;
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
            ScenarioDefinition definition = context != null ? context.Definition : null;
            List<ScenarioAuthoringInspectorItem> journalItems = BuildRuntimeJournalItems();
            List<ScenarioAuthoringInspectorItem> pendingItems = ScenarioPublishAuthoringContentBuilder.BuildTimelineItems(definition, ScenarioPublishAuthoringContentBuilder.GetRuntimeState(), _timelineBuilder);
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

        private static List<ScenarioAuthoringInspectorItem> BuildRuntimeJournalItems()
        {
            List<ScenarioAuthoringInspectorItem> items = new List<ScenarioAuthoringInspectorItem>();
            ScenarioRuntimeState state = ScenarioPublishAuthoringContentBuilder.GetRuntimeState();

            items.Add(Item.Property("Scenario", Item.Safe(state != null ? state.ScenarioId : null)));
            items.Add(Item.Property("Binding", Item.Safe(state != null ? state.RuntimeBindingId : null)));
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
                    entries.Add(Item.ActionItem(Item.Action(
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
                    Id = "timeline_day_" + day.Day.ToString(CultureInfo.InvariantCulture),
                    Title = "Day " + day.Day.ToString(CultureInfo.InvariantCulture),
                    Expanded = true,
                    Layout = ScenarioAuthoringInspectorSectionLayout.PropertyList,
                    Items = entries.ToArray()
                });
            }

            ScenarioAuthoringInspectorSection[] triggerSections = ScenarioAuthoringPresentationBuilder.BuildTriggerWindowSections(definition);
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
