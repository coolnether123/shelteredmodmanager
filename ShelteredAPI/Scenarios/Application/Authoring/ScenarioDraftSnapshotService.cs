using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Application.Authoring
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
        private int _observedRevision = -1;
        private DateTime _lastAutosaveUtc = DateTime.MinValue;

        internal ScenarioDraftSnapshotService(IScenarioEditorSessionStore sessionStore, IScenarioDefinitionSerializer serializer)
        {
            _sessionStore = sessionStore;
            _serializer = serializer;
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
                _serializer.Save(ScenarioDefinitionCloner.Clone(session.WorkingDefinition), snapshotPath);
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

        internal bool SaveNamedVersion(string name, out ScenarioDraftSnapshotInfo snapshot, out string error)
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

            string displayName = NormalizeVersionName(name);
            if (string.IsNullOrEmpty(displayName))
            {
                error = "Give this version a name.";
                return false;
            }

            try
            {
                string directory = GetSnapshotDirectory(draftPath, "versions");
                Directory.CreateDirectory(directory);
                string path = CreateUniqueSnapshotPath(directory, "version", EncodeVersionName(displayName));
                _serializer.Save(ScenarioDefinitionCloner.Clone(session.WorkingDefinition), path);
                snapshot = CreateInfo(path, false, displayName, session.WorkingDefinition);
                MMLog.WriteInfo("[ScenarioDraftSnapshots] Saved named draft version '" + displayName + "'.");
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
                session.WorkingDefinition = ScenarioDefinitionCloner.Clone(restored);
                session.MarkDraftChanged(ScenarioDirtySection.Meta);
                session.MarkDraftChanged(ScenarioDirtySection.Family);
                session.MarkDraftChanged(ScenarioDirtySection.Inventory);
                session.MarkDraftChanged(ScenarioDirtySection.Bunker);
                session.MarkDraftChanged(ScenarioDirtySection.Triggers);
                session.MarkDraftChanged(ScenarioDirtySection.WinLoss);
                session.MarkDraftChanged(ScenarioDirtySection.Assets);
                session.MarkDraftChanged(ScenarioDirtySection.Map);
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
                if (File.Exists(snapshot.FilePath))
                    File.Delete(snapshot.FilePath);
                string namePath = snapshot.FilePath + ".name";
                if (File.Exists(namePath))
                    File.Delete(namePath);
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
            string[] files = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string name = autosave ? "Autosave" : DecodeVersionName(Path.GetFileNameWithoutExtension(files[i]));
                result.Add(CreateInfo(files[i], autosave, name, current));
            }
        }

        private ScenarioDraftSnapshotInfo CreateInfo(string path, bool autosave, string name, ScenarioDefinition current)
        {
            ScenarioDraftSnapshotInfo info = new ScenarioDraftSnapshotInfo();
            info.FilePath = path;
            info.IsAutosave = autosave;
            info.Name = string.IsNullOrEmpty(name) ? (autosave ? "Autosave" : "Saved version") : name;
            info.CreatedAtUtc = File.GetLastWriteTimeUtc(path);
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
                string[] sections = { "Meta", "FamilySetup", "StartingInventory", "BunkerEdits", "ScenarioFlow", "TriggersAndEvents", "AssetReferences", "Map" };
                string[] labels = { "Details", "Family", "Supplies", "Shelter", "Story", "Events", "Art", "Map" };
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
            string[] files = Directory.GetFiles(directory, "*.xml", SearchOption.TopDirectoryOnly);
            Array.Sort(files, delegate(string left, string right) { return File.GetLastWriteTimeUtc(right).CompareTo(File.GetLastWriteTimeUtc(left)); });
            for (int i = AutosaveRetentionCount; i < files.Length; i++)
            {
                try { File.Delete(files[i]); }
                catch (Exception ex) { MMLog.WriteWarning("[ScenarioDraftSnapshots] Could not prune autosave: " + ex.Message); }
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

        private static string CreateUniqueSnapshotPath(string directory, string prefix, string suffix)
        {
            return Path.Combine(directory, prefix + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + "_" + suffix + ".xml");
        }

        private static string NormalizeVersionName(string name)
        {
            return string.IsNullOrEmpty(name) ? null : name.Trim();
        }

        private static string EncodeVersionName(string name)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(name ?? string.Empty);
            StringBuilder encoded = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) encoded.Append(bytes[i].ToString("x2"));
            return encoded.ToString();
        }

        private static string DecodeVersionName(string fileName)
        {
            try
            {
                int separator = fileName.LastIndexOf('_');
                if (separator < 0) return "Saved version";
                string encoded = fileName.Substring(separator + 1);
                if (encoded.Length == 0 || encoded.Length % 2 != 0) return "Saved version";
                byte[] bytes = new byte[encoded.Length / 2];
                for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(encoded.Substring(i * 2, 2), 16);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return "Saved version"; }
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
