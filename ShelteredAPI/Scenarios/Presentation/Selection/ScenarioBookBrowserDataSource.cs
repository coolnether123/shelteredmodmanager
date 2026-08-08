using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    /// <summary>
    /// Supplies the runtime scenario library. Authoring drafts and package-management
    /// views are deliberately outside this assembly.
    /// </summary>
    internal sealed class ScenarioBookBrowserDataSource
    {
        private sealed class CatalogSnapshot
        {
            public ScenarioCatalogEntry[] Entries;
            public string Error;
            public int Version;
        }

        private static readonly object SharedSnapshotSync = new object();
        private static CatalogSnapshot _sharedSnapshot;
        private static bool _sharedRefreshRunning;
        private static bool _sharedRefreshQueued;
        private static int _sharedVersion;
        private static int _sharedRefreshRequestVersion;

        private readonly IScenarioSelectionCatalogService _catalog;
        private readonly IScenarioSaveLibrary _saveLibrary;
        private readonly ScenarioLibraryPreferenceStore _libraryPreferences;
        private readonly object _saveRefreshSync = new object();
        private ScenarioCatalogEntry[] _entries = new ScenarioCatalogEntry[0];
        private int _appliedVersion;
        private string _lastRefreshError;
        private bool _saveRefreshRunning;
        private string _saveRefreshScenarioId;
        private ScenarioBookRowModel[] _saveRefreshRows;
        private string _saveRefreshError;
        private int _saveRefreshVersion;
        private int _appliedSaveRefreshVersion;
        private int _saveRefreshRequestVersion;
        private bool _cancelled;

        public ScenarioBookBrowserDataSource(
            IScenarioSelectionCatalogService catalog,
            IScenarioSaveLibrary saveLibrary)
            : this(catalog, saveLibrary, new ScenarioLibraryPreferenceStore())
        {
        }

        internal ScenarioBookBrowserDataSource(
            IScenarioSelectionCatalogService catalog,
            IScenarioSaveLibrary saveLibrary,
            ScenarioLibraryPreferenceStore libraryPreferences)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");
            if (libraryPreferences == null) throw new ArgumentNullException("libraryPreferences");

            _catalog = catalog;
            _saveLibrary = saveLibrary;
            _libraryPreferences = libraryPreferences;
            ApplyLatestSnapshot();
        }

        public ScenarioLibrarySortMode LibrarySortMode
        {
            get { return _libraryPreferences.SortMode; }
        }

        public ScenarioLibrarySortMode CycleLibrarySortMode()
        {
            ScenarioLibrarySortMode next = ScenarioLibraryOrganizer.Next(LibrarySortMode);
            SetLibrarySortMode(next);
            return next;
        }

        public void SetLibrarySortMode(ScenarioLibrarySortMode mode)
        {
            _libraryPreferences.SetSortMode(mode);
        }

        public bool ToggleLibraryPin(string scenarioId)
        {
            return _libraryPreferences.TogglePinned(scenarioId);
        }

        public void Refresh()
        {
            ApplyLatestSnapshot();
            BeginRefreshAsync();
        }

        public void BeginRefreshAsync()
        {
            BeginSharedRefreshAsync(_catalog);
        }

        public void BeginRefreshAsync(bool reuseAvailableSharedRefresh)
        {
            if (reuseAvailableSharedRefresh)
            {
                lock (SharedSnapshotSync)
                {
                    if (_sharedRefreshRunning
                        || (_sharedSnapshot != null && string.IsNullOrEmpty(_sharedSnapshot.Error)))
                        return;
                }
            }

            BeginSharedRefreshAsync(_catalog);
        }

        public void InvalidateCatalogSnapshot()
        {
            lock (SharedSnapshotSync)
            {
                _sharedSnapshot = null;
                _sharedVersion++;
                _sharedRefreshRequestVersion++;
                if (_sharedRefreshRunning)
                    _sharedRefreshQueued = true;
            }

            _entries = new ScenarioCatalogEntry[0];
            _appliedVersion = 0;
            _lastRefreshError = null;
        }

        public void InvalidateSaveRows(string storageScenarioId)
        {
            lock (_saveRefreshSync)
            {
                _saveRefreshRequestVersion++;
                _saveRefreshRunning = false;
                _saveRefreshScenarioId = storageScenarioId;
                _saveRefreshRows = null;
                _saveRefreshError = null;
                _saveRefreshVersion++;
            }
        }

        public void BeginSaveRowsRefreshAsync(ScenarioCatalogEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.StorageScenarioId))
                return;

            string storageScenarioId = entry.StorageScenarioId;
            int requestVersion;
            lock (_saveRefreshSync)
            {
                if (_saveRefreshRunning
                    && string.Equals(_saveRefreshScenarioId, storageScenarioId, StringComparison.OrdinalIgnoreCase))
                    return;

                _saveRefreshRunning = true;
                _saveRefreshScenarioId = storageScenarioId;
                _saveRefreshRows = null;
                _saveRefreshError = null;
                requestVersion = ++_saveRefreshRequestVersion;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                ScenarioBookRowModel[] rows;
                string error = null;
                try
                {
                    rows = BuildSaveRowsSnapshot(entry);
                }
                catch (Exception ex)
                {
                    rows = new ScenarioBookRowModel[0];
                    error = ex.Message;
                    MMLog.WriteWarning("[ScenarioBookBrowser] Background save enumeration failed for "
                        + storageScenarioId + ": " + ex.Message);
                }

                lock (_saveRefreshSync)
                {
                    if (!_cancelled && requestVersion == _saveRefreshRequestVersion)
                    {
                        _saveRefreshRows = rows;
                        _saveRefreshError = error;
                        _saveRefreshVersion++;
                        _saveRefreshRunning = false;
                    }
                }
            });
        }

        public static void BeginSharedRefreshAsync(IScenarioSelectionCatalogService catalog)
        {
            if (catalog == null)
                throw new ArgumentNullException("catalog");

            int requestVersion;
            lock (SharedSnapshotSync)
            {
                if (_sharedRefreshRunning)
                {
                    _sharedRefreshQueued = true;
                    _sharedRefreshRequestVersion++;
                    return;
                }

                _sharedRefreshRunning = true;
                requestVersion = ++_sharedRefreshRequestVersion;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    PublishSnapshot(BuildSnapshot(catalog), requestVersion);
                }
                finally
                {
                    bool runQueuedRefresh;
                    lock (SharedSnapshotSync)
                    {
                        _sharedRefreshRunning = false;
                        runQueuedRefresh = _sharedRefreshQueued;
                        _sharedRefreshQueued = false;
                    }

                    if (runQueuedRefresh)
                        BeginSharedRefreshAsync(catalog);
                }
            });
        }

        internal static bool IsSharedRefreshRunning
        {
            get { lock (SharedSnapshotSync) return _sharedRefreshRunning; }
        }

        internal static int SharedSnapshotVersion
        {
            get { lock (SharedSnapshotSync) return _sharedSnapshot != null ? _sharedSnapshot.Version : 0; }
        }

        internal static string SharedSnapshotError
        {
            get { lock (SharedSnapshotSync) return _sharedSnapshot != null ? _sharedSnapshot.Error : null; }
        }

        public bool ApplyLatestSnapshot()
        {
            CatalogSnapshot snapshot;
            lock (SharedSnapshotSync)
                snapshot = _sharedSnapshot;

            if (snapshot == null || snapshot.Version == _appliedVersion)
                return false;

            _entries = snapshot.Entries ?? new ScenarioCatalogEntry[0];
            _lastRefreshError = snapshot.Error;
            _appliedVersion = snapshot.Version;
            return true;
        }

        public bool IsCatalogRefreshRunning
        {
            get { lock (SharedSnapshotSync) return _sharedRefreshRunning; }
        }

        public bool ApplyLatestSaveRows()
        {
            lock (_saveRefreshSync)
            {
                if (_saveRefreshVersion == _appliedSaveRefreshVersion)
                    return false;

                _appliedSaveRefreshVersion = _saveRefreshVersion;
                _lastRefreshError = _saveRefreshError;
                return true;
            }
        }

        public bool IsRefreshRunning
        {
            get { lock (_saveRefreshSync) return _saveRefreshRunning; }
        }

        public void CancelRefreshes()
        {
            lock (_saveRefreshSync)
            {
                _cancelled = true;
                _saveRefreshRequestVersion++;
                _saveRefreshRunning = false;
                _saveRefreshRows = null;
            }
        }

        public bool HasEntries { get { return _entries != null && _entries.Length > 0; } }
        public bool HasAppliedCatalogSnapshot { get { return _appliedVersion > 0; } }
        public string LastRefreshError { get { return _lastRefreshError; } }

        public List<ScenarioBookRowModel> BuildRows(
            ScenarioBookBrowserViewKind view,
            ScenarioBookType selectedType,
            ScenarioCatalogEntry selectedScenario)
        {
            return BuildRows(view, selectedType, selectedScenario, null);
        }

        public List<ScenarioBookRowModel> BuildRows(
            ScenarioBookBrowserViewKind view,
            ScenarioBookType selectedType,
            ScenarioCatalogEntry selectedScenario,
            string searchFilter)
        {
            switch (view)
            {
                case ScenarioBookBrowserViewKind.Types:
                    return BuildTypeRows(searchFilter);
                case ScenarioBookBrowserViewKind.Scenarios:
                    return BuildScenarioRows(selectedType, searchFilter);
                case ScenarioBookBrowserViewKind.Saves:
                    return FilterRows(BuildSaveRows(selectedScenario), searchFilter);
                default:
                    return new List<ScenarioBookRowModel>();
            }
        }

        public string GetHeaderTitle(
            ScenarioBookBrowserViewKind view,
            ScenarioBookType selectedType,
            ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Custom Scenarios";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return GetTypeLabel(selectedType);
            if (selectedScenario != null)
                return selectedScenario.IsVanilla
                    ? Safe(selectedScenario.DisplayName, selectedScenario.ScenarioId) + " Unlimited Saves"
                    : Safe(selectedScenario.DisplayName, selectedScenario.ScenarioId);
            return "Scenario Saves";
        }

        public string GetHeaderDetail(
            ScenarioBookBrowserViewKind view,
            ScenarioBookType selectedType,
            ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Installed custom scenarios and separate unlimited save archives.";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return selectedType == ScenarioBookType.Published
                    ? "Browse installed scenarios or open an unlimited vanilla save archive."
                    : "Browse installed custom scenarios for this base game mode.";
            if (selectedScenario == null)
                return string.Empty;
            return selectedScenario.IsVanilla
                ? "These unlimited saves are separate from the vanilla scenario window and its normal scenario save."
                : Safe(selectedScenario.Description, "Custom scenario saves.");
        }

        public static string GetTypeLabel(ScenarioBookType type)
        {
            switch (type)
            {
                case ScenarioBookType.Surrounded: return "Surrounded Scenarios";
                case ScenarioBookType.Stasis: return "Stasis Scenarios";
                default: return "Scenario Library";
            }
        }

        public bool TryGetSingleScenarioForType(ScenarioBookType type, out ScenarioCatalogEntry entry)
        {
            ScenarioCatalogEntry[] entries = ListEntries(type);
            entry = entries.Length == 1 ? entries[0] : null;
            return entry != null;
        }

        private List<ScenarioBookRowModel> BuildTypeRows(string searchFilter)
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            if (string.IsNullOrEmpty(searchFilter))
            {
                AddTypeRow(rows, ScenarioBookType.Surrounded, "Surrounded Scenarios",
                    "Installed custom scenarios based on Surrounded.", CountEntries(ScenarioBookType.Surrounded));
                AddTypeRow(rows, ScenarioBookType.Stasis, "Stasis Scenarios",
                    "Installed custom scenarios based on Stasis.", CountEntries(ScenarioBookType.Stasis));
            }

            ScenarioCatalogEntry[] libraryEntries = ListLibraryEntries();
            for (int i = 0; i < libraryEntries.Length; i++)
            {
                if (MatchesSearch(libraryEntries[i], searchFilter))
                    AddLibraryScenarioRow(rows, libraryEntries[i], ScenarioBookType.Published);
            }
            return ScenarioLibraryOrganizer.Order(rows, LibrarySortMode, _libraryPreferences);
        }

        private static void AddTypeRow(
            List<ScenarioBookRowModel> rows,
            ScenarioBookType type,
            string title,
            string detail,
            int count)
        {
            rows.Add(new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Type,
                Type = type,
                Title = title,
                Detail = detail,
                Badge = "(" + count.ToString(CultureInfo.InvariantCulture) + ")"
            });
        }

        private List<ScenarioBookRowModel> BuildScenarioRows(ScenarioBookType type, string searchFilter)
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            ScenarioCatalogEntry[] candidates = type == ScenarioBookType.Published
                ? ListLibraryEntries()
                : ListEntries(type);

            for (int i = 0; i < candidates.Length; i++)
            {
                ScenarioCatalogEntry entry = candidates[i];
                if (!MatchesSearch(entry, searchFilter))
                    continue;
                AddLibraryScenarioRow(rows, entry, type);
            }

            rows = ScenarioLibraryOrganizer.Order(rows, LibrarySortMode, _libraryPreferences);
            if (rows.Count == 0)
            {
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Empty,
                    Title = IsCatalogRefreshRunning ? "Gathering scenarios" : "No scenarios found",
                    Detail = IsCatalogRefreshRunning
                        ? "Reading the installed scenario library in the background."
                        : (string.IsNullOrEmpty(searchFilter)
                            ? "No installed scenarios match this category."
                            : "No installed scenarios match the search filter."),
                    Badge = IsCatalogRefreshRunning ? "Loading" : string.Empty
                });
            }

            return rows;
        }

        private void AddLibraryScenarioRow(
            List<ScenarioBookRowModel> rows,
            ScenarioCatalogEntry entry,
            ScenarioBookType type)
        {
            bool isUnlimitedArchive = entry.IsVanilla;
            rows.Add(new ScenarioBookRowModel
            {
                Kind = isUnlimitedArchive ? ScenarioBookRowKind.OpenScenarioSaves : ScenarioBookRowKind.Scenario,
                Type = type,
                Scenario = entry,
                Title = isUnlimitedArchive
                    ? entry.BaseGameMode.ToString() + " - Unlimited Saves"
                    : Safe(entry.DisplayName, entry.ScenarioId),
                Detail = BuildLibraryScenarioDetail(entry),
                Badge = isUnlimitedArchive ? "Archive" : (entry.CanStart ? "Ready" : "Locked"),
                IsLocked = !isUnlimitedArchive && !entry.CanStart,
                IsPinned = !isUnlimitedArchive && _libraryPreferences.IsPinned(entry.ScenarioId),
                LibrarySortMode = LibrarySortMode
            });
        }

        private List<ScenarioBookRowModel> BuildSaveRows(ScenarioCatalogEntry entry)
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            if (entry == null)
            {
                rows.Add(new ScenarioBookRowModel { Kind = ScenarioBookRowKind.Empty, Title = "No scenario selected" });
                return rows;
            }

            ScenarioBookRowModel[] cachedRows;
            lock (_saveRefreshSync)
            {
                cachedRows = string.Equals(_saveRefreshScenarioId, entry.StorageScenarioId, StringComparison.OrdinalIgnoreCase)
                    ? _saveRefreshRows
                    : null;
            }

            if (cachedRows != null)
                return new List<ScenarioBookRowModel>(cachedRows);

            rows.Add(BuildStartScenarioRow(entry));
            rows.Add(new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Empty,
                Scenario = entry,
                Title = "Loading saves",
                Detail = "Reading scenario save metadata in the background.",
                Badge = "Loading"
            });
            return rows;
        }

        private ScenarioBookRowModel[] BuildSaveRowsSnapshot(ScenarioCatalogEntry entry)
        {
            SaveEntry[] saves = _saveLibrary.ListSaves(entry.StorageScenarioId) ?? new SaveEntry[0];
            List<ScenarioBookSaveDetailModel> details = new List<ScenarioBookSaveDetailModel>();
            for (int i = 0; i < saves.Length; i++)
            {
                if (saves[i] != null)
                    details.Add(ScenarioBookSaveMetadataReader.Read(entry.StorageScenarioId, saves[i]));
            }

            details.Sort(CompareSaveDetails);
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            rows.Add(BuildStartScenarioRow(entry));
            for (int i = 0; i < details.Count; i++)
            {
                ScenarioBookSaveDetailModel detail = details[i];
                SaveEntry save = detail != null ? detail.Save : null;
                if (save == null)
                    continue;

                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.LoadSave,
                    Scenario = entry,
                    Save = save,
                    SaveDetail = detail,
                    Title = BuildSaveSlotTitle(detail, i + 1),
                    Detail = BuildSaveDetail(detail),
                    Badge = BuildSaveBadge(detail),
                    IsLocked = !entry.CanStart,
                    CanDelete = !ScenarioSaveLibrary.IsVanillaScenarioSaveEntry(save)
                });
            }

            return rows.ToArray();
        }

        private static ScenarioBookRowModel BuildStartScenarioRow(ScenarioCatalogEntry entry)
        {
            return new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.StartScenario,
                Scenario = entry,
                Title = "Start New",
                Detail = entry != null && entry.IsVanilla
                    ? "Create a new unlimited run without replacing the vanilla scenario save."
                    : "Create a new scenario-owned save for this scenario.",
                Badge = "New Game",
                IsLocked = entry != null && !entry.CanStart
            };
        }

        private int CountEntries(ScenarioBookType type)
        {
            return ListEntries(type).Length;
        }

        private ScenarioCatalogEntry[] ListLibraryEntries()
        {
            List<ScenarioCatalogEntry> entries = new List<ScenarioCatalogEntry>();
            ScenarioCatalogEntry[] all = _entries ?? new ScenarioCatalogEntry[0];
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry entry = all[i];
                if (entry == null)
                    continue;
                if (entry.Source == ScenarioCatalogSource.Modded
                    || (entry.Source == ScenarioCatalogSource.Vanilla
                        && entry.BaseGameMode != ScenarioBaseGameMode.Survival))
                    entries.Add(entry);
            }
            return entries.ToArray();
        }

        private ScenarioCatalogEntry[] ListEntries(ScenarioBookType type)
        {
            if (type == ScenarioBookType.Published)
                return ListLibraryEntries();

            List<ScenarioCatalogEntry> entries = new List<ScenarioCatalogEntry>();
            ScenarioCatalogEntry[] all = _entries ?? new ScenarioCatalogEntry[0];
            ScenarioBaseGameMode mode = type == ScenarioBookType.Stasis
                ? ScenarioBaseGameMode.Stasis
                : ScenarioBaseGameMode.Surrounded;
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry entry = all[i];
                if (entry != null && entry.Source == ScenarioCatalogSource.Modded && entry.BaseGameMode == mode)
                    entries.Add(entry);
            }
            return entries.ToArray();
        }

        private static bool MatchesSearch(ScenarioCatalogEntry entry, string searchFilter)
        {
            if (entry == null)
                return false;
            if (string.IsNullOrEmpty(searchFilter))
                return true;

            string filter = searchFilter.Trim();
            return Contains(entry.DisplayName, filter)
                || Contains(entry.ScenarioId, filter)
                || Contains(entry.Author, filter)
                || Contains(entry.OwnerModId, filter)
                || Contains(entry.Description, filter);
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static List<ScenarioBookRowModel> FilterRows(
            List<ScenarioBookRowModel> rows,
            string searchFilter)
        {
            if (string.IsNullOrEmpty(searchFilter))
                return rows;

            List<ScenarioBookRowModel> filtered = new List<ScenarioBookRowModel>();
            string filter = searchFilter.Trim();
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                ScenarioBookRowModel row = rows[i];
                if (row != null && (Contains(row.Title, filter)
                    || Contains(row.Detail, filter)
                    || Contains(row.Badge, filter)))
                    filtered.Add(row);
            }

            if (filtered.Count == 0)
            {
                filtered.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Empty,
                    Title = "No saves found",
                    Detail = "No scenario saves match the search filter."
                });
            }
            return filtered;
        }

        private static CatalogSnapshot BuildSnapshot(IScenarioSelectionCatalogService catalog)
        {
            CatalogSnapshot snapshot = new CatalogSnapshot();
            try
            {
                snapshot.Entries = catalog.ListAll() ?? new ScenarioCatalogEntry[0];
            }
            catch (Exception ex)
            {
                snapshot.Entries = new ScenarioCatalogEntry[0];
                snapshot.Error = ex.Message;
                MMLog.WriteWarning("[ScenarioBookBrowser] Scenario catalog refresh failed: " + ex.Message);
            }
            return snapshot;
        }

        private static void PublishSnapshot(CatalogSnapshot snapshot, int requestVersion)
        {
            lock (SharedSnapshotSync)
            {
                if (requestVersion != _sharedRefreshRequestVersion)
                    return;
                snapshot.Version = ++_sharedVersion;
                _sharedSnapshot = snapshot;
            }
        }

        private static string BuildLibraryScenarioDetail(ScenarioCatalogEntry entry)
        {
            if (entry.IsVanilla)
            {
                int count = Math.Max(0, entry.SaveCount);
                return count == 0
                    ? "No unlimited runs yet. Open this archive to start one."
                    : "Open " + count.ToString(CultureInfo.InvariantCulture)
                        + (count == 1 ? " unlimited run." : " unlimited runs.");
            }

            string author = Safe(entry.Author, !string.IsNullOrEmpty(entry.OwnerModId) ? entry.OwnerModId : "unknown author");
            int saves = entry.SaveCount;
            string detail = "by " + author + "  -  " + entry.BaseGameMode.ToString()
                + "  -  " + saves.ToString(CultureInfo.InvariantCulture) + (saves == 1 ? " save" : " saves");
            return entry.LastPlayedUtc.HasValue && detail.Length <= 64
                ? detail + "  -  " + ScenarioLibraryOrganizer.RelativePlayed(entry.LastPlayedUtc.Value, DateTime.UtcNow)
                : detail;
        }

        private static int CompareSaveDetails(ScenarioBookSaveDetailModel left, ScenarioBookSaveDetailModel right)
        {
            if (object.ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int leftScore;
            int rightScore;
            bool leftHasScore = ScenarioBookScoreDisplayReader.TryGetScoreTotal(left, out leftScore);
            bool rightHasScore = ScenarioBookScoreDisplayReader.TryGetScoreTotal(right, out rightScore);
            if (leftHasScore && rightHasScore)
            {
                int score = rightScore.CompareTo(leftScore);
                if (score != 0) return score;
            }
            else if (leftHasScore) return -1;
            else if (rightHasScore) return 1;

            int days = right.DaysSurvived.CompareTo(left.DaysSurvived);
            if (days != 0) return days;

            DateTime leftTime;
            DateTime rightTime;
            bool hasLeftTime = TryParseSortTime(left.SaveTime, out leftTime);
            bool hasRightTime = TryParseSortTime(right.SaveTime, out rightTime);
            if (hasLeftTime && hasRightTime)
            {
                int time = rightTime.CompareTo(leftTime);
                if (time != 0) return time;
            }
            else if (hasLeftTime) return -1;
            else if (hasRightTime) return 1;

            int leftSlot = left.Save != null ? left.Save.absoluteSlot : 0;
            int rightSlot = right.Save != null ? right.Save.absoluteSlot : 0;
            return leftSlot.CompareTo(rightSlot);
        }

        private static bool TryParseSortTime(string raw, out DateTime value)
        {
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out value)
                || DateTime.TryParse(raw, out value);
        }

        private static string BuildSaveDetail(ScenarioBookSaveDetailModel detail)
        {
            SaveEntry save = detail != null ? detail.Save : null;
            string family = save != null && save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.familyName)
                ? save.saveInfo.familyName
                : "Unknown family";
            string days = detail != null ? detail.DaysSurvived.ToString() + " day(s)" : "no day info";
            if (detail != null && !string.IsNullOrEmpty(detail.MetadataError))
                return family + ", " + days + " - Metadata error\n" + detail.MetadataError;

            string result = BuildOutcomeLabel(detail);
            string score = ScenarioBookScoreDisplayReader.BuildSaveScoreLabel(detail);
            if (!string.IsNullOrEmpty(score))
                result += " - " + score;
            return family + ", " + days + " - " + BuildStatusLabel(detail) + "\n" + result;
        }

        private static string BuildSaveSlotTitle(ScenarioBookSaveDetailModel detail, int rank)
        {
            SaveEntry save = detail != null ? detail.Save : null;
            if (save == null)
                return "Save";
            string displayName = !string.IsNullOrEmpty(save.name)
                ? save.name
                : (save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.familyName)
                    ? save.saveInfo.familyName
                    : "Slot " + save.absoluteSlot);
            return "#" + rank.ToString(CultureInfo.InvariantCulture) + " Slot " + save.absoluteSlot + ": " + displayName;
        }

        private static string BuildSaveBadge(ScenarioBookSaveDetailModel detail)
        {
            return detail != null && detail.IsVanilla ? "Vanilla" : BuildStatusLabel(detail);
        }

        internal static string BuildStatusLabel(ScenarioBookSaveDetailModel detail)
        {
            if (detail == null) return "Unknown";
            if (detail.IsVanilla) return "Vanilla";
            if (!string.IsNullOrEmpty(detail.MetadataError)) return "Metadata error";
            if (!detail.HasBinding) return "No binding";
            if (detail.IsConvertedToNormalSave) return "Converted";
            if (detail.IsActive) return "Active";
            return "Inactive";
        }

        internal static string BuildOutcomeLabel(ScenarioBookSaveDetailModel detail)
        {
            if (detail == null || string.IsNullOrEmpty(detail.ScenarioOutcome))
                return "Outcome: not completed";
            return "Outcome: " + detail.ScenarioOutcome;
        }

        internal static string FormatDisplayTime(string rawTime)
        {
            DateTime value;
            return TryParseSortTime(rawTime, out value)
                ? value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : Safe(rawTime, string.Empty);
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
