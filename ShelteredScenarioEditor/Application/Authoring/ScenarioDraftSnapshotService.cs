using ShelteredScenarioEditor.Application.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredScenarioEditor.Infrastructure.Persistence;

namespace ShelteredScenarioEditor.Application.Authoring
{
    // This service deliberately saves snapshots through IScenarioDefinitionSerializer.
    // Snapshot targets are unique files, so serializer.Save keeps its atomic temp-and-rename
    // path without ever replacing scenario.xml or scenario.xml.bak.
    internal sealed class ScenarioDraftSnapshotService
    {
        internal const int AutosaveRetentionCount = 5;
        private static readonly TimeSpan AutosaveInterval = TimeSpan.FromMinutes(3);

        private readonly IScenarioEditorSessionStore _sessionStore;
        private readonly IScenarioDefinitionSerializer _serializer;
        private readonly ScenarioAuthoringSidecarStore _sidecarStore;
        private int _observedRevision = -1;
        private DateTime _lastAutosaveUtc = DateTime.MinValue;

        internal ScenarioDraftSnapshotService(
            IScenarioEditorSessionStore sessionStore,
            IScenarioDefinitionSerializer serializer,
            ScenarioAuthoringSidecarStore sidecarStore)
        {
            _sessionStore = sessionStore;
            _serializer = serializer;
            _sidecarStore = sidecarStore;
        }

        internal void Tick()
        {
            ScenarioEditorSession session = _sessionStore.Current;
            string draftPath = _sessionStore.CurrentFilePath;
            if (session == null || session.WorkingDefinition == null || string.IsNullOrEmpty(draftPath))
                return;

            if (session.DirtyFlags == null || session.DirtyFlags.Count == 0)
            {
                _observedRevision = session.DraftRevision;
                return;
            }

            if (_observedRevision != session.DraftRevision)
                _observedRevision = session.DraftRevision;

            if (_lastAutosaveUtc == DateTime.MinValue || DateTime.UtcNow - _lastAutosaveUtc >= AutosaveInterval)
            {
                string ignoredError;
                TryAutosaveCurrent("Timed autosave", out ignoredError);
            }
        }

        internal bool TryAutosaveCurrent(string reason, out string error)
        {
            ScenarioEditorSession session = _sessionStore.Current;
            return TryAutosave(session, _sessionStore.CurrentFilePath, reason, out error);
        }

        internal bool TryAutosave(ScenarioEditorSession session, string draftPath, string reason, out string error)
        {
            error = null;
            if (session == null || session.WorkingDefinition == null || string.IsNullOrEmpty(draftPath))
                return false;
            if (session.DirtyFlags == null || session.DirtyFlags.Count == 0)
                return false;

            try
            {
                string directory = GetSnapshotDirectory(draftPath, "autosaves");
                Directory.CreateDirectory(directory);
                string snapshotPath = CreateUniqueSnapshotPath(directory, "autosave");
                SaveSnapshotPair(snapshotPath, session);
                ApplyAutosaveRetention(directory);
                _lastAutosaveUtc = DateTime.UtcNow;
                MMLog.WriteInfo("[ScenarioDraftSnapshots] Autosaved draft before/after " + (reason ?? "editor activity") + ".");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                MMLog.WriteWarning("[ScenarioDraftSnapshots] Autosave skipped: " + ex.Message);
                return false;
            }
        }

        internal bool SaveVersion(out ScenarioDraftSnapshotInfo snapshot, out string error)
        {
            snapshot = null;
            error = null;
            ScenarioEditorSession session = _sessionStore.Current;
            string draftPath = _sessionStore.CurrentFilePath;
            if (session == null || session.WorkingDefinition == null || string.IsNullOrEmpty(draftPath))
            {
                error = "No active draft is available.";
                return false;
            }

            try
            {
                string directory = GetSnapshotDirectory(draftPath, "versions");
                Directory.CreateDirectory(directory);
                string path = CreateUniqueSnapshotPath(directory, "version");
                SaveSnapshotPair(path, session);
                snapshot = CreateInfo(path, false, null, session.WorkingDefinition);
                MMLog.WriteInfo("[ScenarioDraftSnapshots] Saved draft version '" + snapshot.Name + "'.");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                MMLog.WriteWarning("[ScenarioDraftSnapshots] Named version save failed: " + ex.Message);
                return false;
            }
        }

        internal ScenarioDraftSnapshotInfo[] ListSnapshots()
        {
            string draftPath = _sessionStore.CurrentFilePath;
            ScenarioEditorSession session = _sessionStore.Current;
            if (string.IsNullOrEmpty(draftPath))
                return new ScenarioDraftSnapshotInfo[0];

            List<ScenarioDraftSnapshotInfo> result = new List<ScenarioDraftSnapshotInfo>();
            AddSnapshots(result, GetSnapshotDirectory(draftPath, "autosaves"), true, null, session != null ? session.WorkingDefinition : null);
            AddSnapshots(result, GetSnapshotDirectory(draftPath, "versions"), false, null, session != null ? session.WorkingDefinition : null);
            result.Sort(CompareSnapshots);
            return result.ToArray();
        }

        internal bool TryGetNewerAutosave(string draftPath, out ScenarioDraftSnapshotInfo snapshot)
        {
            snapshot = null;
            if (string.IsNullOrEmpty(draftPath) || !File.Exists(draftPath))
                return false;

            List<ScenarioDraftSnapshotInfo> autosaves = new List<ScenarioDraftSnapshotInfo>();
            AddSnapshots(autosaves, GetSnapshotDirectory(draftPath, "autosaves"), true, null, null);
            DateTime manualSaveUtc = File.GetLastWriteTimeUtc(draftPath);
            for (int i = 0; i < autosaves.Count; i++)
            {
                if (autosaves[i].CreatedAtUtc > manualSaveUtc && (snapshot == null || autosaves[i].CreatedAtUtc > snapshot.CreatedAtUtc))
                    snapshot = autosaves[i];
            }
            return snapshot != null;
        }

        internal bool Restore(ScenarioDraftSnapshotInfo snapshot, out string error)
        {
            error = null;
            if (snapshot == null || string.IsNullOrEmpty(snapshot.FilePath) || !File.Exists(snapshot.FilePath))
            {
                error = "That saved version is no longer available.";
                return false;
            }

            ScenarioEditorSession session = _sessionStore.Current;
            if (session == null || session.WorkingDefinition == null)
            {
                error = "No active draft is available.";
                return false;
            }

            // A restore always captures the current working copy first, even if it is clean.
            // Marking Meta makes the current copy eligible for the same safe autosave route.
            if (session.DirtyFlags == null || session.DirtyFlags.Count == 0)
                session.MarkDraftChanged(ScenarioDirtySection.Meta);
            string autosaveError;
            if (!TryAutosave(session, _sessionStore.CurrentFilePath, "restoring a saved version", out autosaveError))
            {
                error = "Could not protect the current draft before restoring: " + autosaveError;
                return false;
            }

            try
            {
                ScenarioDefinition restored = _serializer.Load(snapshot.FilePath);
                string sidecarPath = ScenarioAuthoringSidecarStore.GetSidecarPath(snapshot.FilePath);
                if (string.IsNullOrEmpty(sidecarPath) || !File.Exists(sidecarPath))
                    throw new FormatException("That saved version is incomplete because its editor state is missing.");
                string sidecarWarning;
                ScenarioEditorState restoredEditorState = _sidecarStore.Load(snapshot.FilePath, out sidecarWarning);
                if (!string.IsNullOrEmpty(sidecarWarning))
                    throw new FormatException(sidecarWarning);
                session.WorkingDefinition = ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(restored);
                session.EditorState = restoredEditorState;
                session.MarkDraftChanged(ScenarioDirtySection.Meta);
                session.MarkDraftChanged(ScenarioDirtySection.Family);
                session.MarkDraftChanged(ScenarioDirtySection.Inventory);
                session.MarkDraftChanged(ScenarioDirtySection.Bunker);
                session.MarkDraftChanged(ScenarioDirtySection.Triggers);
                session.MarkDraftChanged(ScenarioDirtySection.WinLoss);
                session.MarkDraftChanged(ScenarioDirtySection.Assets);
                session.MarkDraftChanged(ScenarioDirtySection.Map);
                session.MarkDraftChanged(ScenarioDirtySection.LaunchSetup);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal bool Delete(ScenarioDraftSnapshotInfo snapshot, out string error)
        {
            error = null;
            if (snapshot == null || string.IsNullOrEmpty(snapshot.FilePath))
                return false;
            try
            {
                DeleteSnapshotFiles(snapshot.FilePath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal string GetLastManualSaveText()
        {
            string path = _sessionStore.CurrentFilePath;
            return string.IsNullOrEmpty(path) || !File.Exists(path) ? "Not saved yet" : FormatAge(File.GetLastWriteTimeUtc(path));
        }

        internal static string FormatAge(DateTime timeUtc)
        {
            TimeSpan age = DateTime.UtcNow - timeUtc;
            if (age.TotalMinutes < 1) return "just now";
            if (age.TotalHours < 1) return Math.Max(1, (int)age.TotalMinutes) + " minutes ago";
            if (age.TotalDays < 1) return Math.Max(1, (int)age.TotalHours) + " hours ago";
            return Math.Max(1, (int)age.TotalDays) + " days ago";
        }

        private void AddSnapshots(List<ScenarioDraftSnapshotInfo> result, string directory, bool autosave, string ignored, ScenarioDefinition current)
        {
            if (!Directory.Exists(directory)) return;
            CleanupIncompleteTransactionArtifacts(directory);
            string[] files = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].EndsWith(ScenarioAuthoringSidecarStore.SidecarSuffix, StringComparison.OrdinalIgnoreCase))
                    continue;
                string sidecarPath = ScenarioAuthoringSidecarStore.GetSidecarPath(files[i]);
                if (string.IsNullOrEmpty(sidecarPath) || !File.Exists(sidecarPath))
                    continue;
                result.Add(CreateInfo(files[i], autosave, null, current));
            }
        }

        private void SaveSnapshotPair(string snapshotPath, ScenarioEditorSession session)
        {
            if (string.IsNullOrEmpty(snapshotPath))
                throw new ArgumentException("Snapshot path is required.", "snapshotPath");
            if (session == null || session.WorkingDefinition == null)
                throw new ArgumentNullException("session");

            string transactionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            string pendingScenarioPath = snapshotPath + ".pairpending-" + transactionId + ".xml";
            string pendingSidecarPath = ScenarioAuthoringSidecarStore.GetSidecarPath(pendingScenarioPath);
            string sidecarPath = ScenarioAuthoringSidecarStore.GetSidecarPath(snapshotPath);

            try
            {
                _serializer.Save(ShelteredScenarioEditor.Application.Runtime.ScenarioEditorDefinitionCloner.Clone(session.WorkingDefinition), pendingScenarioPath);
                _sidecarStore.Save(pendingScenarioPath, session.EditorState, true);

                if (!File.Exists(pendingScenarioPath) || string.IsNullOrEmpty(pendingSidecarPath)
                    || !File.Exists(pendingSidecarPath))
                {
                    throw new IOException("The snapshot pair could not be staged completely.");
                }
                if (File.Exists(snapshotPath) || string.IsNullOrEmpty(sidecarPath) || File.Exists(sidecarPath))
                    throw new IOException("The snapshot target already exists.");

                // The sidecar is committed first. The scenario XML is the commit marker used by
                // discovery, so an interrupted transaction can never expose half of the pair.
                File.Move(pendingSidecarPath, sidecarPath);
                File.Move(pendingScenarioPath, snapshotPath);
            }
            catch
            {
                // Removing the scenario first makes a failed rollback invisible to discovery even
                // if the filesystem refuses to remove the now-orphaned sidecar.
                if (File.Exists(snapshotPath))
                    TryDeleteTransactionArtifact(snapshotPath);
                // If scenario removal itself fails, retain its already-committed sidecar. A complete
                // recoverable pair is safer than turning the rollback failure into visible mismatch.
                if (!File.Exists(snapshotPath) && !string.IsNullOrEmpty(sidecarPath) && File.Exists(sidecarPath))
                    TryDeleteTransactionArtifact(sidecarPath);
                throw;
            }
            finally
            {
                TryDeleteTransactionArtifact(pendingScenarioPath);
                TryDeleteTransactionArtifact(pendingSidecarPath);
                TryDeleteTransactionArtifact(pendingSidecarPath + ".bak");
            }
        }

        private ScenarioDraftSnapshotInfo CreateInfo(string path, bool autosave, string name, ScenarioDefinition current)
        {
            ScenarioDraftSnapshotInfo info = new ScenarioDraftSnapshotInfo();
            info.FilePath = path;
            info.IsAutosave = autosave;
            info.CreatedAtUtc = File.GetLastWriteTimeUtc(path);
            info.Name = string.IsNullOrEmpty(name)
                ? (autosave ? "Autosave" : "Version " + info.CreatedAtUtc.ToLocalTime().ToString("g"))
                : name;
            info.AgeText = FormatAge(info.CreatedAtUtc);
            info.ChangeSummary = current == null ? "" : BuildChangeSummary(path, current);
            return info;
        }

        private string BuildChangeSummary(string snapshotPath, ScenarioDefinition current)
        {
            try
            {
                // This is intentionally section-level: concise, cheap, and never pretends to be a visual diff.
                XmlDocument snapshot = new XmlDocument(); snapshot.Load(snapshotPath);
                XmlDocument now = new XmlDocument(); now.LoadXml(_serializer.ToXml(current));
                string[] sections = { "Meta", "LaunchSetup", "FamilySetup", "StartingInventory", "BunkerEdits", "ScenarioFlow", "TriggersAndEvents", "AssetReferences", "Map" };
                string[] labels = { "Details", "Play Experience", "Family", "Supplies", "Shelter", "Story", "Events", "Art", "Map" };
                List<string> changes = new List<string>();
                for (int i = 0; i < sections.Length; i++)
                {
                    XmlNode left = snapshot.DocumentElement.SelectSingleNode(sections[i]);
                    XmlNode right = now.DocumentElement.SelectSingleNode(sections[i]);
                    string a = left == null ? "" : left.OuterXml;
                    string b = right == null ? "" : right.OuterXml;
                    if (!string.Equals(a, b, StringComparison.Ordinal)) changes.Add(labels[i] + ": changed");
                }
                return changes.Count == 0 ? "Unchanged" : string.Join(", ", changes.ToArray());
            }
            catch { return "Change summary unavailable"; }
        }

        private static void ApplyAutosaveRetention(string directory)
        {
            CleanupIncompleteTransactionArtifacts(directory);
            string[] candidates = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
            List<string> snapshotFiles = new List<string>();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (!candidates[i].EndsWith(ScenarioAuthoringSidecarStore.SidecarSuffix, StringComparison.OrdinalIgnoreCase))
                    snapshotFiles.Add(candidates[i]);
            }
            string[] files = snapshotFiles.ToArray();
            Array.Sort(files, delegate(string left, string right) { return File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)); });
            for (int i = AutosaveRetentionCount; i < files.Length; i++)
            {
                try { DeleteSnapshotFiles(files[i]); }
                catch (Exception ex) { MMLog.WriteWarning("[ScenarioDraftSnapshots] Could not prune autosave: " + ex.Message); }
            }
        }

        private static void DeleteSnapshotFiles(string snapshotPath)
        {
            if (File.Exists(snapshotPath))
                File.Delete(snapshotPath);
            string sidecarPath = ScenarioAuthoringSidecarStore.GetSidecarPath(snapshotPath);
            if (!string.IsNullOrEmpty(sidecarPath) && File.Exists(sidecarPath))
                File.Delete(sidecarPath);
            if (!string.IsNullOrEmpty(sidecarPath) && File.Exists(sidecarPath + ".bak"))
                File.Delete(sidecarPath + ".bak");
        }

        private static void CleanupIncompleteTransactionArtifacts(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            string[] pending = Directory.GetFiles(directory, "*.pairpending-*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < pending.Length; i++)
                TryDeleteTransactionArtifact(pending[i]);

            string[] sidecars = Directory.GetFiles(
                directory,
                "*" + ScenarioAuthoringSidecarStore.SidecarSuffix,
                SearchOption.TopDirectoryOnly);
            for (int i = 0; i < sidecars.Length; i++)
            {
                string snapshotPath = sidecars[i].Substring(
                    0,
                    sidecars[i].Length - ScenarioAuthoringSidecarStore.SidecarSuffix.Length) + ".xml";
                if (!File.Exists(snapshotPath))
                    TryDeleteTransactionArtifact(sidecars[i]);
            }
        }

        private static void TryDeleteTransactionArtifact(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioDraftSnapshots] Could not clean transaction artifact '"
                    + path + "': " + ex.Message);
            }
        }

        private static string GetSnapshotDirectory(string draftPath, string kind)
        {
            return Path.Combine(Path.Combine(Path.GetDirectoryName(draftPath), ".history"), kind);
        }

        private static string CreateUniqueSnapshotPath(string directory, string prefix)
        {
            return Path.Combine(directory, prefix + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".xml");
        }

        private static int CompareSnapshots(ScenarioDraftSnapshotInfo left, ScenarioDraftSnapshotInfo right)
        {
            return right.CreatedAtUtc.CompareTo(left.CreatedAtUtc);
        }
    }

    internal sealed class ScenarioDraftSnapshotInfo
    {
        internal string FilePath;
        internal bool IsAutosave;
        internal string Name;
        internal DateTime CreatedAtUtc;
        internal string AgeText;
        internal string ChangeSummary;
    }
}
