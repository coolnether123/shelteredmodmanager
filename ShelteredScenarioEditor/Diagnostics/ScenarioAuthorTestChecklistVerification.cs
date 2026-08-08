using System;
using System.IO;
using System.Text;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Infrastructure.Persistence;

namespace ShelteredScenarioEditor.Diagnostics
{
    internal static class ScenarioAuthorTestChecklistVerification
    {
        internal static void Verify(string root, ScenarioValidationResult result)
        {
            DateTime stamp = new DateTime(2026, 7, 9, 12, 30, 0, DateTimeKind.Utc);
            ScenarioAuthorTestChecklistService service = new ScenarioAuthorTestChecklistService(delegate { return stamp; });
            ScenarioEditorDefinitionSerializer serializer = new ScenarioEditorDefinitionSerializer();
            ScenarioAuthoringSidecarStore sidecars = new ScenarioAuthoringSidecarStore();
            string draftRoot = Path.Combine(root, "ChecklistDraft");
            Directory.CreateDirectory(draftRoot);
            string scenarioPath = Path.Combine(draftRoot, ScenarioEditorDefinitionSerializer.DefaultFileName);

            ScenarioDefinition definition = new ScenarioDefinition
            {
                Id = "verify.testchecklist",
                DisplayName = "Test Checklist Verification",
                Author = "Verifier",
                Version = "1.0"
            };
            serializer.Save(definition, scenarioPath);
            ScenarioEditorSession session = new ScenarioEditorSession { WorkingDefinition = definition };
            Assert(service.MarkPlaytestStarted(session), "Playtest auto-check seam did not mark the checklist.", result);
            Assert(service.MarkExportReinstalled(session), "Export reinstall auto-check seam did not mark the checklist.", result);
            Assert(service.ToggleManual(session, ScenarioAuthorTestChecklistService.SavedReloadedId),
                "Manual checklist toggle did not mark the checklist.", result);
            service.SetNote(session, ScenarioAuthorTestChecklistService.SavedReloadedId, "Reloaded on day 3.");
            session.EditorState.SetupFlowEnabled = true;
            session.EditorState.ChecklistDismissed = false;
            session.EditorState.AddCompletedTour("overview");

            ScenarioAuthorTestChecklistItem started = service.FindItem(session, ScenarioAuthorTestChecklistService.StartedPlaytestId);
            Assert(started != null && started.Checked && started.Source == ScenarioAuthorTestVerificationSource.Editor
                && started.CheckedUtc.HasValue && started.CheckedUtc.Value == stamp,
                "Editor verification source or timestamp was not retained.", result);

            sidecars.Save(scenarioPath, session.EditorState);
            string scenarioXml = File.ReadAllText(scenarioPath);
            string sidecarPath = ScenarioAuthoringSidecarStore.GetSidecarPath(scenarioPath);
            Assert(scenarioXml.IndexOf("AuthorTestChecklist", StringComparison.Ordinal) < 0,
                "Runtime scenario XML contains editor checklist metadata.", result);
            Assert(File.Exists(sidecarPath), "Editor checklist sidecar was not created.", result);

            string warning;
            ScenarioEditorState reloadedEditorState = sidecars.Load(scenarioPath, out warning);
            ScenarioEditorSession reloadedSession = new ScenarioEditorSession
            {
                WorkingDefinition = serializer.Load(scenarioPath),
                EditorState = reloadedEditorState
            };
            ScenarioAuthorTestChecklistItem reloaded = service.FindItem(reloadedSession, ScenarioAuthorTestChecklistService.SavedReloadedId);
            Assert(string.IsNullOrEmpty(warning) && reloaded != null && reloaded.Checked
                && reloaded.Source == ScenarioAuthorTestVerificationSource.Manual
                && string.Equals(reloaded.Note, "Reloaded on day 3.", StringComparison.Ordinal),
                "Checklist sidecar round-trip lost checked state, source, date, or note.", result);
            Assert(reloadedEditorState.SetupFlowEnabled && !reloadedEditorState.ChecklistDismissed
                && reloadedEditorState.HasCompletedTour("overview"),
                "Editor state round-trip lost setup or completed-tour metadata.", result);

            VerifySnapshotRestore(serializer, sidecars, service, scenarioPath, reloadedSession, result);
            VerifyDuplicateSidecar(serializer, sidecars, scenarioPath, service, result);

            ScenarioPackagePlanner planner = new ScenarioPackagePlanner(serializer, service);
            ScenarioPackagePlan checkedPlan = planner.Build(
                definition,
                scenarioPath,
                Path.Combine(root, "ChecklistPackage"),
                true,
                new ScenarioValidationResult(),
                session);
            string checkedReadme = ReadGenerated(checkedPlan, ScenarioPackagePlanner.ReadmeFileName);
            Assert(checkedReadme.IndexOf("Author verified: playtest, save/load during play, export reinstall", StringComparison.Ordinal) >= 0,
                "README omitted the conditional author-verification line.", result);
            Assert(!ContainsEntry(checkedPlan, ScenarioAuthoringSidecarStore.SidecarSuffix),
                "Published package included editor-only checklist state.", result);

            ScenarioEditorSession uncheckedSession = new ScenarioEditorSession
            {
                WorkingDefinition = new ScenarioDefinition
                {
                    Id = "unchecked",
                    DisplayName = "Unchecked",
                    Author = "Verifier",
                    Version = "1.0"
                }
            };
            service.SetNote(uncheckedSession, ScenarioAuthorTestChecklistService.VerifiedRequiredModsId, "Notes are not proof of completion.");
            ScenarioPackagePlan uncheckedPlan = planner.Build(
                uncheckedSession.WorkingDefinition,
                null,
                Path.Combine(root, "UncheckedPackage"),
                true,
                new ScenarioValidationResult(),
                uncheckedSession);
            string uncheckedReadme = ReadGenerated(uncheckedPlan, ScenarioPackagePlanner.ReadmeFileName);
            Assert(uncheckedReadme.IndexOf("Author verified:", StringComparison.Ordinal) < 0,
                "README included an honesty line for a note-only checklist.", result);
        }

        private static void VerifySnapshotRestore(
            ScenarioEditorDefinitionSerializer serializer,
            ScenarioAuthoringSidecarStore sidecars,
            ScenarioAuthorTestChecklistService checklistService,
            string scenarioPath,
            ScenarioEditorSession session,
            ScenarioValidationResult result)
        {
            ScenarioEditorSessionStore sessions = new ScenarioEditorSessionStore();
            sessions.Set(session, scenarioPath);
            ScenarioDraftSnapshotService snapshots = new ScenarioDraftSnapshotService(sessions, serializer, sidecars);
            ScenarioDraftSnapshotInfo snapshot;
            string error;
            Assert(snapshots.SaveVersion(out snapshot, out error),
                "Checklist snapshot could not be saved: " + error, result);
            string snapshotSidecarPath = snapshot != null
                ? ScenarioAuthoringSidecarStore.GetSidecarPath(snapshot.FilePath)
                : null;
            Assert(snapshot != null && File.Exists(snapshot.FilePath) && File.Exists(snapshotSidecarPath),
                "Checklist snapshot did not commit scenario XML and editor state as a complete pair.", result);
            ScenarioDraftSnapshotInfo[] savedVersions = snapshots.ListSnapshots();
            Assert(snapshot != null && Path.GetFileName(snapshot.FilePath).Length < 64
                && savedVersions.Length > 0
                && savedVersions[0].Name.StartsWith("Version ", StringComparison.Ordinal),
                "Version metadata did not round-trip from a bounded snapshot filename.", result);
            checklistService.SetNote(session, ScenarioAuthorTestChecklistService.SavedReloadedId, "Changed after snapshot.");
            session.EditorState.ChecklistDismissed = true;
            session.EditorState.AddCompletedTour("story");
            Assert(snapshots.Restore(snapshot, out error), "Checklist snapshot could not be restored: " + error, result);
            ScenarioAuthorTestChecklistItem restored = checklistService.FindItem(session, ScenarioAuthorTestChecklistService.SavedReloadedId);
            Assert(restored != null && string.Equals(restored.Note, "Reloaded on day 3.", StringComparison.Ordinal),
                "Snapshot restore did not restore editor checklist metadata.", result);
            Assert(!session.EditorState.ChecklistDismissed
                && session.EditorState.HasCompletedTour("overview")
                && !session.EditorState.HasCompletedTour("story"),
                "Snapshot restore did not restore the complete editor metadata aggregate.", result);

            ScenarioAuthorTestChecklist preservedChecklist = session.AuthorTestChecklist;
            session.AuthorTestChecklist = new ScenarioAuthorTestChecklist();
            ScenarioDraftSnapshotInfo emptySnapshot;
            Assert(snapshots.SaveVersion(out emptySnapshot, out error),
                "Empty-checklist snapshot could not be saved: " + error, result);
            Assert(emptySnapshot != null && File.Exists(ScenarioAuthoringSidecarStore.GetSidecarPath(emptySnapshot.FilePath)),
                "An empty checklist snapshot did not retain its required editor-state half.", result);
            session.AuthorTestChecklist = preservedChecklist;

            string versionsDirectory = Path.Combine(Path.Combine(Path.GetDirectoryName(scenarioPath), ".history"), "versions");
            string incompletePath = Path.Combine(versionsDirectory, "incomplete.xml");
            serializer.Save(session.WorkingDefinition, incompletePath);
            ScenarioDraftSnapshotInfo[] visibleSnapshots = snapshots.ListSnapshots();
            bool incompleteVisible = false;
            for (int i = 0; i < visibleSnapshots.Length; i++)
            {
                if (string.Equals(visibleSnapshots[i].FilePath, incompletePath, StringComparison.OrdinalIgnoreCase))
                    incompleteVisible = true;
            }
            Assert(!incompleteVisible, "Snapshot discovery exposed scenario XML without its editor-state pair.", result);

            string pendingPath = Path.Combine(versionsDirectory, "interrupted.pairpending-verification.xml");
            serializer.Save(session.WorkingDefinition, pendingPath);
            sidecars.Save(pendingPath, session.EditorState, true);
            string pendingSidecarPath = ScenarioAuthoringSidecarStore.GetSidecarPath(pendingPath);
            snapshots.ListSnapshots();
            Assert(!File.Exists(pendingPath) && !File.Exists(pendingSidecarPath),
                "Snapshot discovery did not clean an interrupted pair transaction.", result);
        }

        private static void VerifyDuplicateSidecar(
            ScenarioEditorDefinitionSerializer serializer,
            ScenarioAuthoringSidecarStore sidecars,
            string sourceScenarioPath,
            ScenarioAuthorTestChecklistService checklistService,
            ScenarioValidationResult result)
        {
            string duplicateRoot = Path.Combine(Path.GetDirectoryName(sourceScenarioPath), "Duplicate");
            Directory.CreateDirectory(duplicateRoot);
            string duplicateScenarioPath = Path.Combine(duplicateRoot, ScenarioEditorDefinitionSerializer.DefaultFileName);
            File.Copy(sourceScenarioPath, duplicateScenarioPath, true);
            File.Copy(
                ScenarioAuthoringSidecarStore.GetSidecarPath(sourceScenarioPath),
                ScenarioAuthoringSidecarStore.GetSidecarPath(duplicateScenarioPath),
                true);
            string warning;
            ScenarioEditorState duplicateEditorState = sidecars.Load(duplicateScenarioPath, out warning);
            ScenarioEditorSession duplicateSession = new ScenarioEditorSession
            {
                WorkingDefinition = serializer.Load(duplicateScenarioPath),
                EditorState = duplicateEditorState
            };
            Assert(string.IsNullOrEmpty(warning)
                && checklistService.CountChecked(duplicateSession) == 3
                && duplicateEditorState.SetupFlowEnabled
                && !duplicateEditorState.ChecklistDismissed
                && duplicateEditorState.HasCompletedTour("overview"),
                "Duplicated draft did not retain its complete editor sidecar state.", result);
        }

        private static bool ContainsEntry(ScenarioPackagePlan plan, string suffix)
        {
            for (int i = 0; plan != null && plan.Entries != null && i < plan.Entries.Count; i++)
            {
                string path = plan.Entries[i] != null ? plan.Entries[i].RelativePath : null;
                if (!string.IsNullOrEmpty(path) && path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
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
