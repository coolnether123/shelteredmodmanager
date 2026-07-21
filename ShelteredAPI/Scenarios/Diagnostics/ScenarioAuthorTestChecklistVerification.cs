using System;
using System.Text;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Diagnostics
{
    internal static class ScenarioAuthorTestChecklistVerification
    {
        internal static void Verify(string root, ScenarioValidationResult result)
        {
            DateTime stamp = new DateTime(2026, 7, 9, 12, 30, 0, DateTimeKind.Utc);
            ScenarioAuthorTestChecklistService service = new ScenarioAuthorTestChecklistService(delegate { return stamp; });
            ScenarioDefinitionSerializer serializer = new ScenarioDefinitionSerializer();

            ScenarioDefinition legacy = serializer.FromXml(
                "<Scenario><Meta><Id>legacy</Id><DisplayName>Legacy</DisplayName><Author>Verifier</Author><Version>1.0</Version></Meta></Scenario>");
            Assert(legacy.AuthorTestChecklist != null && legacy.AuthorTestChecklist.Items.Count == 0,
                "XML without AuthorTestChecklist did not load an empty checklist.", result);

            ScenarioDefinition definition = new ScenarioDefinition
            {
                Id = "verify.testchecklist",
                DisplayName = "Test Checklist Verification",
                Author = "Verifier",
                Version = "1.0"
            };
            ScenarioEditorSession session = new ScenarioEditorSession { WorkingDefinition = definition };
            Assert(service.MarkPlaytestStarted(session), "Playtest auto-check seam did not mark the checklist.", result);
            Assert(service.MarkExportReinstalled(session), "Export reinstall auto-check seam did not mark the checklist.", result);
            Assert(service.ToggleManual(session, ScenarioAuthorTestChecklistService.SavedReloadedId),
                "Manual checklist toggle did not mark the checklist.", result);
            service.SetNote(session, ScenarioAuthorTestChecklistService.SavedReloadedId, "Reloaded on day 3.");

            ScenarioAuthorTestChecklistItem started = definition.AuthorTestChecklist.Find(ScenarioAuthorTestChecklistService.StartedPlaytestId);
            Assert(started != null && started.Checked && started.Source == ScenarioAuthorTestVerificationSource.Editor
                && started.CheckedUtc.HasValue && started.CheckedUtc.Value == stamp,
                "Editor verification source or timestamp was not retained.", result);

            string xml = serializer.ToXml(definition);
            ScenarioDefinition roundTrip = serializer.FromXml(xml);
            ScenarioAuthorTestChecklistItem reloaded = roundTrip.AuthorTestChecklist.Find(ScenarioAuthorTestChecklistService.SavedReloadedId);
            Assert(reloaded != null && reloaded.Checked && reloaded.Source == ScenarioAuthorTestVerificationSource.Manual
                && string.Equals(reloaded.Note, "Reloaded on day 3.", StringComparison.Ordinal),
                "Checklist XML round-trip lost checked state, source, date, or note.", result);

            ScenarioPackagePlanner planner = new ScenarioPackagePlanner(new ScenarioDefinitionSerializerAdapter(serializer), service);
            ScenarioPackagePlan checkedPlan = planner.Build(definition, null, root, true, new ScenarioValidationResult());
            string checkedReadme = ReadGenerated(checkedPlan, ScenarioPackagePlanner.ReadmeFileName);
            Assert(checkedReadme.IndexOf("Author verified: playtest, save/load during play, export reinstall", StringComparison.Ordinal) >= 0,
                "README omitted the conditional author-verification line.", result);

            ScenarioDefinition uncheckedDefinition = new ScenarioDefinition { Id = "unchecked", DisplayName = "Unchecked", Author = "Verifier", Version = "1.0" };
            ScenarioPackagePlan uncheckedPlan = planner.Build(uncheckedDefinition, null, root, true, new ScenarioValidationResult());
            string uncheckedReadme = ReadGenerated(uncheckedPlan, ScenarioPackagePlanner.ReadmeFileName);
            Assert(uncheckedReadme.IndexOf("Author verified:", StringComparison.Ordinal) < 0,
                "README included an honesty line for an empty checklist.", result);
        }

        private static string ReadGenerated(ScenarioPackagePlan plan, string relativePath)
        {
            for (int i = 0; plan != null && plan.Entries != null && i < plan.Entries.Count; i++)
            {
                ScenarioPackageEntry entry = plan.Entries[i];
                if (entry != null && string.Equals(entry.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                    return entry.Content != null ? Encoding.UTF8.GetString(entry.Content) : string.Empty;
            }
            return string.Empty;
        }

        private static void Assert(bool condition, string message, ScenarioValidationResult result)
        {
            if (!condition)
                result.AddError("Author test checklist contract: " + message);
        }
    }
}
