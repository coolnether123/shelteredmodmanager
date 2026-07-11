using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using ModAPI.Core;
using ShelteredAPI.Saves;
using ShelteredAPI.Saves.Runtime;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
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
        private readonly ScenarioPackageImportService _importService;
        private readonly object _saveRefreshSync = new object();
        private readonly object _draftFactsRefreshSync = new object();
        private readonly object _importRefreshSync = new object();
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
        private bool _draftFactsRefreshRunning;
        private ScenarioCatalogEntry _draftFactsRefreshScenario;
        private ScenarioCatalogEntry _draftFactsResultScenario;
        private ScenarioBookDraftFactsModel _draftFactsResult;
        private int _draftFactsRefreshVersion;
        private int _appliedDraftFactsRefreshVersion;
        private int _draftFactsRefreshRequestVersion;
        private bool _importRefreshRunning;
        private ScenarioPackageImportScanResult _importResult;
        private int _importRefreshVersion;
        private int _appliedImportRefreshVersion;
        private int _importRefreshRequestVersion;
        private bool _cancelled;

        public ScenarioBookBrowserDataSource(
            IScenarioSelectionCatalogService catalog,
            IScenarioSaveLibrary saveLibrary,
            ScenarioPackageImportService importService)
        {
            if (catalog == null) throw new ArgumentNullException("catalog");
            if (saveLibrary == null) throw new ArgumentNullException("saveLibrary");
            if (importService == null) throw new ArgumentNullException("importService");

            _catalog = catalog;
            _saveLibrary = saveLibrary;
            _importService = importService;
            ApplyLatestSnapshot();
        }

        public void BeginImportRefreshAsync()
        {
            int requestVersion;
            lock (_importRefreshSync)
            {
                requestVersion = ++_importRefreshRequestVersion;
                _importRefreshRunning = true;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                ScenarioPackageImportScanResult result;
                try { result = _importService.Scan(); }
                catch (Exception ex)
                {
                    result = new ScenarioPackageImportScanResult { Error = "Could not scan downloaded scenarios: " + ex.Message };
                }

                lock (_importRefreshSync)
                {
                    if (!_cancelled && requestVersion == _importRefreshRequestVersion)
                    {
                        _importResult = result ?? new ScenarioPackageImportScanResult();
                        _importRefreshVersion++;
                        _importRefreshRunning = false;
                    }
                }
            });
        }

        public bool ApplyLatestImportScan(out string error)
        {
            error = null;
            lock (_importRefreshSync)
            {
                if (_importRefreshVersion == _appliedImportRefreshVersion)
                    return false;
                _appliedImportRefreshVersion = _importRefreshVersion;
                error = _importResult != null ? _importResult.Error : null;
                return true;
            }
        }

        public void Refresh()
        {
            CatalogSnapshot snapshot = BuildSnapshot(_catalog);
            PublishSnapshot(snapshot);
            ApplySnapshot(snapshot);
        }

        public void BeginRefreshAsync()
        {
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

        public void BeginSaveRowsRefreshAsync(ScenarioCatalogEntry entry)
        {
            if (entry == null || entry.Source == ScenarioCatalogSource.Draft || string.IsNullOrEmpty(entry.StorageScenarioId))
                return;

            string storageScenarioId = entry.StorageScenarioId;
            int requestVersion;
            lock (_saveRefreshSync)
            {
                if (_saveRefreshRunning && string.Equals(_saveRefreshScenarioId, storageScenarioId, StringComparison.OrdinalIgnoreCase))
                    return;

                _saveRefreshRunning = true;
                _saveRefreshScenarioId = storageScenarioId;
                _saveRefreshRows = null;
                _saveRefreshError = null;
                requestVersion = ++_saveRefreshRequestVersion;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                ScenarioBookRowModel[] rows = null;
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
                finally
                {
                    lock (_saveRefreshSync)
                    {
                        if (!_cancelled && requestVersion == _saveRefreshRequestVersion)
                        {
                            _saveRefreshRows = rows ?? new ScenarioBookRowModel[0];
                            _saveRefreshError = error;
                            _saveRefreshVersion++;
                            _saveRefreshRunning = false;
                        }
                    }
                }
            });
        }

        public void BeginDraftFactsRefreshAsync(ScenarioCatalogEntry entry)
        {
            if (entry == null || entry.Source != ScenarioCatalogSource.Draft)
                return;

            int requestVersion;
            lock (_draftFactsRefreshSync)
            {
                if (_draftFactsRefreshRunning && ReferenceEquals(_draftFactsRefreshScenario, entry))
                    return;

                _draftFactsRefreshRunning = true;
                _draftFactsRefreshScenario = entry;
                _draftFactsResultScenario = null;
                _draftFactsResult = null;
                requestVersion = ++_draftFactsRefreshRequestVersion;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                ScenarioBookDraftFactsModel facts = null;
                try
                {
                    facts = ScenarioBookDraftFacts.BuildDetailFacts(entry);
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioBookBrowser] Background draft facts failed for "
                        + entry.ScenarioId + ": " + ex.Message);
                }
                finally
                {
                    lock (_draftFactsRefreshSync)
                    {
                        if (!_cancelled && requestVersion == _draftFactsRefreshRequestVersion)
                        {
                            _draftFactsResultScenario = entry;
                            _draftFactsResult = facts;
                            _draftFactsRefreshVersion++;
                            _draftFactsRefreshRunning = false;
                        }
                    }
                }
            });
        }

        public static void BeginSharedRefreshAsync(IScenarioSelectionCatalogService catalog)
        {
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
                    CatalogSnapshot snapshot = BuildSnapshot(catalog);
                    PublishSnapshot(snapshot, requestVersion);
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

        public bool ApplyLatestSnapshot()
        {
            CatalogSnapshot snapshot;
            lock (SharedSnapshotSync)
            {
                snapshot = _sharedSnapshot;
            }

            if (snapshot == null || snapshot.Version == _appliedVersion)
                return false;

            ApplySnapshot(snapshot);
            return true;
        }

        public bool IsCatalogRefreshRunning
        {
            get
            {
                lock (SharedSnapshotSync)
                {
                    return _sharedRefreshRunning;
                }
            }
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

        public bool ApplyLatestDraftFacts(out ScenarioCatalogEntry scenario, out ScenarioBookDraftFactsModel facts)
        {
            scenario = null;
            facts = null;
            lock (_draftFactsRefreshSync)
            {
                if (_draftFactsRefreshVersion == _appliedDraftFactsRefreshVersion)
                    return false;

                _appliedDraftFactsRefreshVersion = _draftFactsRefreshVersion;
                scenario = _draftFactsResultScenario;
                facts = _draftFactsResult;
                return true;
            }
        }

        public void InvalidateDraftFactsRefresh()
        {
            lock (_draftFactsRefreshSync)
            {
                _draftFactsRefreshRequestVersion++;
                _draftFactsRefreshRunning = false;
                _draftFactsRefreshScenario = null;
                _draftFactsResultScenario = null;
                _draftFactsResult = null;
            }
        }

        public bool IsRefreshRunning
        {
            get
            {
                lock (_saveRefreshSync)
                {
                    return _saveRefreshRunning;
                }
            }
        }

        public void CancelRefreshes()
        {
            lock (_saveRefreshSync)
            {
                _cancelled = true;
                _saveRefreshRequestVersion++;
                _saveRefreshRunning = false;
                _saveRefreshRows = null;
                _saveRefreshError = null;
            }

            lock (_importRefreshSync)
            {
                _importRefreshRequestVersion++;
                _importRefreshRunning = false;
            }

            InvalidateDraftFactsRefresh();
        }

        public bool HasEntries
        {
            get { return _entries != null && _entries.Length > 0; }
        }

        public string LastRefreshError
        {
            get { return _lastRefreshError; }
        }

        private void ApplySnapshot(CatalogSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            _entries = snapshot.Entries ?? new ScenarioCatalogEntry[0];
            _lastRefreshError = snapshot.Error;
            _appliedVersion = snapshot.Version;
        }

        private static CatalogSnapshot BuildSnapshot(IScenarioSelectionCatalogService catalog)
        {
            CatalogSnapshot snapshot = new CatalogSnapshot();
            try
            {
                snapshot.Entries = catalog != null ? catalog.ListAll() : new ScenarioCatalogEntry[0];
            }
            catch (Exception ex)
            {
                snapshot.Entries = new ScenarioCatalogEntry[0];
                snapshot.Error = ex.Message;
            }

            return snapshot;
        }

        private static void PublishSnapshot(CatalogSnapshot snapshot)
        {
            PublishSnapshot(snapshot, ++_sharedRefreshRequestVersion);
        }

        private static void PublishSnapshot(CatalogSnapshot snapshot, int requestVersion)
        {
            if (snapshot == null)
                return;

            lock (SharedSnapshotSync)
            {
                if (requestVersion < _sharedRefreshRequestVersion)
                    return;

                snapshot.Version = ++_sharedVersion;
                _sharedSnapshot = snapshot;
            }
        }

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
            List<ScenarioBookRowModel> rows;
            switch (view)
            {
                case ScenarioBookBrowserViewKind.Types:
                    rows = BuildTypeRows();
                    break;
                case ScenarioBookBrowserViewKind.Scenarios:
                    rows = BuildScenarioRows(selectedType);
                    break;
                case ScenarioBookBrowserViewKind.Saves:
                    rows = BuildSaveRows(selectedScenario);
                    break;
                case ScenarioBookBrowserViewKind.DraftDetails:
                    rows = new List<ScenarioBookRowModel>();
                    break;
                case ScenarioBookBrowserViewKind.InstallScenarios:
                    rows = BuildImportRows();
                    break;
                default:
                    rows = new List<ScenarioBookRowModel>();
                    break;
            }

            return FilterRows(view, rows, searchFilter);
        }

        public string GetHeaderTitle(ScenarioBookBrowserViewKind view, ScenarioBookType selectedType, ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Custom Scenarios";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return GetTypeLabel(selectedType);
            if (view == ScenarioBookBrowserViewKind.DraftDetails)
                return selectedScenario != null ? Safe(selectedScenario.DisplayName, selectedScenario.ScenarioId) : "Draft Details";
            if (view == ScenarioBookBrowserViewKind.InstallScenarios)
                return "Install Scenarios";
            if (selectedScenario != null)
                return Safe(selectedScenario.DisplayName, selectedScenario.ScenarioId);

            return "Scenario Saves";
        }

        public string GetHeaderDetail(ScenarioBookBrowserViewKind view, ScenarioBookType selectedType, ScenarioCatalogEntry selectedScenario)
        {
            if (view == ScenarioBookBrowserViewKind.Types)
                return "Choose published play, draft authoring, or downloaded installs.";
            if (view == ScenarioBookBrowserViewKind.Scenarios)
                return selectedType == ScenarioBookType.Draft
                    ? "Drafts are authoring work. Open one to edit details or continue building it."
                    : "Pick a scenario. The next page shows saves owned by that scenario.";
            if (view == ScenarioBookBrowserViewKind.DraftDetails)
                return "Edit the local draft details or open the authoring save.";
            if (view == ScenarioBookBrowserViewKind.InstallScenarios)
                return "Put downloaded scenario folders in " + _importService.StagingRoot + ", then click Install.";
            if (selectedScenario != null)
                return "Read the scenario notes, then choose a save slot.";

            return string.Empty;
        }

        public static string GetTypeLabel(ScenarioBookType type)
        {
            switch (type)
            {
                case ScenarioBookType.Surrounded: return "Surrounded";
                case ScenarioBookType.Stasis: return "Stasis";
                case ScenarioBookType.Draft: return "Draft Scenarios";
                default: return "Published Scenarios";
            }
        }

        public bool TryGetSingleScenarioForType(ScenarioBookType type, out ScenarioCatalogEntry entry)
        {
            entry = null;
            ScenarioCatalogEntry[] entries = ListEntries(type);
            if (entries.Length != 1)
                return false;

            entry = entries[0];
            return entry != null;
        }

        private List<ScenarioBookRowModel> BuildTypeRows()
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            ScenarioBookRowModel surrounded = BuildTypeRow(
                ScenarioBookType.Surrounded,
                "Surrounded",
                "Scenario saves and custom content built on the Surrounded rule set.");
            surrounded.SectionLabel = "PLAY PUBLISHED SCENARIOS";
            rows.Add(surrounded);
            rows.Add(BuildTypeRow(ScenarioBookType.Stasis, "Stasis", "Scenario saves and custom content built on the Stasis rule set."));

            if (ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
            {
                ScenarioBookRowModel drafts = BuildTypeRow(
                    ScenarioBookType.Draft,
                    "Draft Scenarios",
                    "Authoring workspace for unfinished scenarios, not normal play content.");
                drafts.SectionLabel = "WORK ON DRAFTS";
                rows.Add(drafts);
            }

            rows.Add(new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.OpenInstallScenarios,
                Title = "Install Scenarios",
                Detail = "Make a downloaded scenario playable in one click.",
                Badge = "Downloads",
                SectionLabel = "INSTALL DOWNLOADED SCENARIOS"
            });

            // Keep persistent root navigation ahead of catalog-sized content. The book
            // displays five rows per page, so appending these controls after published
            // entries allowed a catalog refresh to push them onto a later page.
            AddPublishedScenarioRows(rows);

            // Interrupted launches and leftover redirects are exceptional, so they trail
            // the normal type cards under their own labelled "Needs attention" section
            // instead of sitting between real scenarios.
            AddRecoveryRows(rows);
            return rows;
        }

        private List<ScenarioBookRowModel> BuildImportRows()
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            rows.Add(new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.OpenScenarioDownloadsFolder,
                Title = "Open Download Folder",
                Detail = _importService.StagingRoot,
                Badge = "Open Folder"
            });

            ScenarioPackageImportScanResult scan;
            bool running;
            lock (_importRefreshSync)
            {
                scan = _importResult;
                running = _importRefreshRunning;
            }

            ScenarioPackageImportCandidate[] candidates = scan != null
                ? scan.Candidates ?? new ScenarioPackageImportCandidate[0]
                : new ScenarioPackageImportCandidate[0];
            for (int i = 0; i < candidates.Length; i++)
            {
                ScenarioPackageImportCandidate candidate = candidates[i];
                if (candidate == null)
                    continue;
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = candidate.CanInstall ? ScenarioBookRowKind.InstallPackage : ScenarioBookRowKind.Empty,
                    ImportCandidate = candidate,
                    Title = BuildImportTitle(candidate),
                    Detail = candidate.CanInstall
                        ? "Validated and ready to make playable."
                        : Safe(candidate.FailureReason, "This package cannot be installed."),
                    Badge = candidate.CanInstall ? "Install" : (candidate.IsAlreadyInstalled ? "Installed" : "Needs Fix")
                });
            }

            if (candidates.Length == 0)
            {
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Empty,
                    Title = running ? "Looking for scenarios" : "No packages found",
                    Detail = running
                        ? "Checking downloads, exports, and likely misplaced scenario folders."
                        : "Drop a scenario folder in the download folder above, then return here.",
                    Badge = running ? "Scanning" : string.Empty
                });
            }
            return rows;
        }

        private static string BuildImportTitle(ScenarioPackageImportCandidate candidate)
        {
            string name = Safe(candidate != null ? candidate.DisplayName : null, "Unreadable scenario package");
            if (candidate == null || string.IsNullOrEmpty(candidate.ScenarioId))
                return name;
            return name + " - v" + Safe(candidate.Version, "unknown") + " by " + Safe(candidate.Author, "unknown");
        }

        private ScenarioBookRowModel BuildTypeRow(ScenarioBookType type, string title, string detail)
        {
            int count = CountEntries(type);
            return new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Type,
                Type = type,
                Title = title,
                Detail = detail,
                Badge = IsCatalogLoading() && count == 0 ? "Loading" : count.ToString() + " scenario(s)"
            };
        }

        private List<ScenarioBookRowModel> BuildScenarioRows(ScenarioBookType selectedType)
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            if (selectedType == ScenarioBookType.Draft && ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
            {
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.CreateDraft,
                    Title = "Create New Draft",
                    Detail = "Authoring-only workspace. This does not start normal play.",
                    Badge = "Draft Tool"
                });
            }

            ScenarioCatalogEntry[] entries = ListEntries(selectedType);
            for (int i = 0; i < entries.Length; i++)
            {
                ScenarioCatalogEntry entry = entries[i];
                if (entry == null)
                    continue;

                rows.Add(BuildScenarioRow(selectedType, entry));
            }

            if (entries.Length == 0 && IsCatalogLoading())
            {
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Empty,
                    Type = selectedType,
                    Title = "Loading scenarios",
                    Detail = "Scanning scenario folders and reading scenario metadata in the background.",
                    Badge = "Loading"
                });
            }

            return rows;
        }

        private ScenarioBookRowModel BuildScenarioRow(ScenarioBookType selectedType, ScenarioCatalogEntry entry)
        {
            SaveEntry draftSave = null;
            ScenarioBookDraftFactsModel draftFacts = null;
            if (entry != null && entry.Source == ScenarioCatalogSource.Draft)
            {
                try { ScenarioAuthoringDraftRepository.Instance.TryGetDraftSaveEntry(entry.ScenarioId, out draftSave); }
                catch { draftSave = null; }

                string draftFilePath = ScenarioAuthoringDraftRepository.GetDraftScenarioFilePath(draftSave != null ? draftSave.absoluteSlot : 0);
                draftFacts = ScenarioBookDraftFacts.BuildRowFacts(entry, draftSave, draftFilePath);
            }

            return new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Scenario,
                Type = selectedType,
                Scenario = entry,
                Title = BuildScenarioTitle(entry),
                Detail = entry != null && entry.Source == ScenarioCatalogSource.Draft
                    ? BuildDraftScenarioDetail(entry, draftSave, draftFacts)
                    : BuildScenarioDetail(entry),
                Badge = entry != null && entry.Source == ScenarioCatalogSource.Draft
                    ? BuildDraftScenarioBadge(draftSave, draftFacts)
                    : BuildScenarioBadge(entry),
                IsLocked = entry == null || !entry.CanStart
            };
        }

        private List<ScenarioBookRowModel> BuildSaveRows(ScenarioCatalogEntry entry)
        {
            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            if (entry == null)
                return rows;

            if (entry.Source == ScenarioCatalogSource.Draft)
            {
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.OpenDraft,
                    Scenario = entry,
                    Title = "Open Draft",
                    Detail = "Load the draft's authoring save and reopen the scenario editor.",
                    Badge = "Authoring",
                    IsLocked = !entry.CanStart
                });
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.DuplicateDraft,
                    Scenario = entry,
                    Title = "Duplicate Draft",
                    Detail = "Create a separate copy of this draft and its scenario.xml.",
                    Badge = "Copy",
                    IsLocked = !entry.CanStart
                });
                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.DeleteDraft,
                    Scenario = entry,
                    Title = "Delete Draft",
                    Detail = "Quarantine this draft and remove its authoring save after confirmation.",
                    Badge = "Delete",
                    CanDelete = true
                });
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
            SaveEntry[] saves = new SaveEntry[0];
            try { saves = _saveLibrary.ListSaves(entry.StorageScenarioId); }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookBrowser] Save enumeration failed for "
                    + entry.StorageScenarioId + ": " + ex.Message);
            }

            List<ScenarioBookSaveDetailModel> saveDetails = new List<ScenarioBookSaveDetailModel>();
            for (int i = 0; i < saves.Length; i++)
            {
                SaveEntry save = saves[i];
                if (save == null)
                    continue;

                saveDetails.Add(ScenarioBookSaveMetadataReader.Read(entry.StorageScenarioId, save));
            }

            saveDetails.Sort(CompareSaveDetails);

            List<ScenarioBookRowModel> rows = new List<ScenarioBookRowModel>();
            rows.Add(BuildStartScenarioRow(entry));

            for (int i = 0; i < saveDetails.Count; i++)
            {
                ScenarioBookSaveDetailModel detail = saveDetails[i];
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
                Detail = "Create a new scenario-owned save for this scenario.",
                Badge = "New Game",
                IsLocked = entry != null && !entry.CanStart
            };
        }

        private static void AddRecoveryRows(List<ScenarioBookRowModel> rows)
        {
            if (rows == null)
                return;

            if (IsLaunchFlowPending())
                return;

            List<ScenarioBookRowModel> recoveryRows = new List<ScenarioBookRowModel>();
            AddRecoveryRows(recoveryRows, PlatformSaveProxy.NextSave, PlatformSaveProxy._nextSaveLock, "queued startup save");
            AddRecoveryRows(recoveryRows, PlatformSaveProxy.NextLoad, PlatformSaveProxy._nextLoadLock, "queued load target");

            if (recoveryRows.Count == 0)
                return;

            rows.Add(new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Empty,
                Title = "Needs attention",
                Detail = "A previous scenario launch was interrupted. Clear or resume these leftover redirects; no save or draft files are deleted.",
                Badge = string.Empty,
                SectionLabel = "NEEDS ATTENTION"
            });
            rows.AddRange(recoveryRows);
        }

        private static bool IsLaunchFlowPending()
        {
            try
            {
                ScenarioAuthoringBootstrapService bootstrap = ScenarioAuthoringBootstrapService.Instance;
                return bootstrap != null && bootstrap.HasPendingDraftLaunch();
            }
            catch
            {
                return false;
            }
        }

        private static void AddRecoveryRows(
            List<ScenarioBookRowModel> rows,
            Dictionary<SaveManager.SaveType, PlatformSaveProxy.Target> targets,
            object sync,
            string label)
        {
            if (targets == null || sync == null)
                return;

            lock (sync)
            {
                foreach (KeyValuePair<SaveManager.SaveType, PlatformSaveProxy.Target> pair in targets)
                {
                    PlatformSaveProxy.Target target = pair.Value;
                    if (target == null)
                        continue;

                    rows.Add(new ScenarioBookRowModel
                    {
                        Kind = ScenarioBookRowKind.RecoveryResume,
                        Title = "Resume " + label,
                        Detail = "Pending redirect: " + Safe(target.scenarioId, "<unknown>") + " / " + Safe(target.saveId, "<unknown>") + ".",
                        Badge = "Recovery",
                        RecoveryScenarioId = target.scenarioId,
                        RecoverySaveId = target.saveId,
                        RecoverySaveType = pair.Key
                    });
                    rows.Add(new ScenarioBookRowModel
                    {
                        Kind = ScenarioBookRowKind.RecoveryCleanup,
                        Title = "Clear " + label,
                        Detail = "Remove this pending redirect. No draft or save files are deleted.",
                        Badge = "Cleanup",
                        RecoveryScenarioId = target.scenarioId,
                        RecoverySaveId = target.saveId,
                        RecoverySaveType = pair.Key
                    });
                }
            }
        }

        private int CountEntries(ScenarioBookType type)
        {
            return ListEntries(type).Length;
        }

        private bool IsCatalogLoading()
        {
            return IsCatalogRefreshRunning;
        }

        private ScenarioCatalogEntry[] ListEntries(ScenarioBookType type)
        {
            ScenarioCatalogEntry[] all = _entries ?? new ScenarioCatalogEntry[0];
            List<ScenarioCatalogEntry> entries = new List<ScenarioCatalogEntry>();
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry entry = all[i];
                if (entry == null)
                    continue;

                if (type == ScenarioBookType.Draft)
                {
                    if (entry.Source == ScenarioCatalogSource.Draft)
                        entries.Add(entry);
                    continue;
                }

                if (type == ScenarioBookType.Surrounded && IsPlayableScenarioMode(entry, ScenarioBaseGameMode.Surrounded))
                    entries.Add(entry);
                else if (type == ScenarioBookType.Stasis && IsPlayableScenarioMode(entry, ScenarioBaseGameMode.Stasis))
                    entries.Add(entry);
            }

            return entries.ToArray();
        }

        private void AddPublishedScenarioRows(List<ScenarioBookRowModel> rows)
        {
            ScenarioCatalogEntry[] all = _entries ?? new ScenarioCatalogEntry[0];
            for (int i = 0; i < all.Length; i++)
            {
                ScenarioCatalogEntry entry = all[i];
                if (entry == null || entry.Source != ScenarioCatalogSource.Modded)
                    continue;

                if (entry.BaseGameMode == ScenarioBaseGameMode.Surrounded
                    || entry.BaseGameMode == ScenarioBaseGameMode.Stasis)
                {
                    continue;
                }

                rows.Add(new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Scenario,
                    Type = ScenarioBookType.Published,
                    Scenario = entry,
                    Title = BuildScenarioTitle(entry),
                    Detail = Safe(entry.Description, string.Empty) + "\n" + BuildScenarioDetail(entry),
                    Badge = BuildScenarioBadge(entry),
                    IsLocked = !entry.CanStart
                });
            }
        }

        private static bool IsPlayableScenarioMode(ScenarioCatalogEntry entry, ScenarioBaseGameMode mode)
        {
            if (entry == null || entry.BaseGameMode != mode)
                return false;

            return entry.Source == ScenarioCatalogSource.Modded || entry.Source == ScenarioCatalogSource.Vanilla;
        }

        private static string BuildScenarioDetail(ScenarioCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;

            string owner = entry.Source == ScenarioCatalogSource.Vanilla
                ? "vanilla"
                : (!string.IsNullOrEmpty(entry.OwnerModId) ? entry.OwnerModId : "local");
            string mode = entry.BaseGameMode.ToString();
            string state = entry.CanStart ? "Ready" : "Locked";
            return owner + " - " + mode + " - " + state;
        }

        private static string BuildScenarioTitle(ScenarioCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;

            string name = Safe(entry.DisplayName, entry.ScenarioId);
            if (!entry.IsModded)
                return name;

            string version = Safe(entry.Version, "unknown");
            string author = Safe(entry.Author, "unknown");
            return name + " - v" + version + " by " + author;
        }

        private static string BuildDraftScenarioDetail(ScenarioCatalogEntry entry, SaveEntry draftSave, ScenarioBookDraftFactsModel facts)
        {
            string description = Safe(entry != null ? entry.Description : null, "Local scenario authoring draft.");
            string baseMode = facts != null ? facts.BaseModeLabel : ScenarioBookDraftFacts.BaseModeLabel(entry != null ? entry.BaseGameMode : ScenarioBaseGameMode.Survival);
            string edited = facts != null ? facts.LastEditedText : BuildDraftModifiedLabel(draftSave);
            string recovery = facts != null && facts.HasRecoveryData ? " - unsaved recovery" : string.Empty;
            return description + "\n" + baseMode + " base, edited " + edited + recovery;
        }

        private static string BuildDraftScenarioBadge(SaveEntry draftSave, ScenarioBookDraftFactsModel facts)
        {
            if (facts != null && facts.HasRecoveryData)
                return "Recovery";

            if (draftSave != null && draftSave.absoluteSlot > 0)
                return "Slot " + draftSave.absoluteSlot.ToString(CultureInfo.InvariantCulture);

            return "Draft";
        }

        private static string BuildDraftModifiedLabel(SaveEntry draftSave)
        {
            if (draftSave == null)
                return "unknown";

            string displayTime = FormatDisplayTime(!string.IsNullOrEmpty(draftSave.updatedAt) ? draftSave.updatedAt : GetSaveTime(draftSave));
            return string.IsNullOrEmpty(displayTime) ? "unknown" : displayTime;
        }

        private static string BuildScenarioBadge(ScenarioCatalogEntry entry)
        {
            if (entry != null && entry.Source == ScenarioCatalogSource.Draft)
                return "Draft";

            return entry != null ? entry.SaveCount.ToString() + " save(s)" : string.Empty;
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
            else if (leftHasScore)
                return -1;
            else if (rightHasScore)
                return 1;

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
            else if (hasLeftTime)
                return -1;
            else if (hasRightTime)
                return 1;

            int leftSlot = left.Save != null ? left.Save.absoluteSlot : 0;
            int rightSlot = right.Save != null ? right.Save.absoluteSlot : 0;
            return leftSlot.CompareTo(rightSlot);
        }

        private static string BuildSaveDetail(ScenarioBookSaveDetailModel detail)
        {
            SaveEntry save = detail != null ? detail.Save : null;
            string family = save != null && save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.familyName)
                ? save.saveInfo.familyName
                : "Unknown family";
            string days = detail != null ? detail.DaysSurvived.ToString() + " day(s)" : "no day info";
            string result = BuildOutcomeLabel(detail);
            string score = ScenarioBookScoreDisplayReader.BuildSaveScoreLabel(detail);
            if (detail != null && !string.IsNullOrEmpty(detail.MetadataError))
                return family + ", " + days + " - Metadata error\n" + detail.MetadataError;

            string secondLine = result;
            if (!string.IsNullOrEmpty(score))
                secondLine += " - " + score;

            return family + ", " + days + " - " + BuildStatusLabel(detail) + "\n" + secondLine;
        }

        private static string BuildSaveSlotTitle(ScenarioBookSaveDetailModel detail, int rank)
        {
            SaveEntry save = detail != null ? detail.Save : null;
            if (save == null)
                return "Save";

            string displayName = !string.IsNullOrEmpty(save.name)
                ? save.name
                : (save.saveInfo != null && !string.IsNullOrEmpty(save.saveInfo.familyName) ? save.saveInfo.familyName : "Slot " + save.absoluteSlot);
            return "#" + rank.ToString() + " Slot " + save.absoluteSlot + ": " + displayName;
        }

        private static string BuildSaveBadge(ScenarioBookSaveDetailModel detail)
        {
            if (detail != null && detail.IsVanilla)
                return "Vanilla";
            return BuildStatusLabel(detail);
        }

        internal static string BuildStatusLabel(ScenarioBookSaveDetailModel detail)
        {
            if (detail == null)
                return "Unknown";
            if (detail.IsVanilla)
                return "Vanilla";
            if (!string.IsNullOrEmpty(detail.MetadataError))
                return "Metadata error";
            if (!detail.HasBinding)
                return "No binding";
            if (detail.IsConvertedToNormalSave)
                return "Converted";
            if (detail.IsActive)
                return "Active";
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
            if (string.IsNullOrEmpty(rawTime))
                return string.Empty;

            try
            {
                bool hasExplicitOffset =
                    rawTime.IndexOf('Z') >= 0 ||
                    rawTime.IndexOf('+') >= 0 ||
                    rawTime.LastIndexOf('-') > 9;

                DateTimeOffset dto;
                if (hasExplicitOffset && DateTimeOffset.TryParse(rawTime, out dto))
                    return dto.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

                DateTime dt;
                if (DateTime.TryParse(rawTime, out dt))
                {
                    if (dt.Kind == DateTimeKind.Utc)
                        return dt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

                    return dt.ToString("g", CultureInfo.CurrentCulture);
                }
            }
            catch
            {
            }

            return rawTime;
        }

        private static bool TryParseSortTime(string rawTime, out DateTime value)
        {
            value = DateTime.MinValue;
            if (string.IsNullOrEmpty(rawTime))
                return false;

            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(rawTime, out dto))
            {
                value = dto.UtcDateTime;
                return true;
            }

            return DateTime.TryParse(rawTime, out value);
        }

        private static string GetSaveTime(SaveEntry save)
        {
            return ScenarioBookSaveMetadataReader.GetSaveTime(save);
        }

        private static List<ScenarioBookRowModel> FilterRows(
            ScenarioBookBrowserViewKind view,
            List<ScenarioBookRowModel> rows,
            string searchFilter)
        {
            if (rows == null)
                return new List<ScenarioBookRowModel>();
            if (string.IsNullOrEmpty(searchFilter))
                return rows;

            List<ScenarioBookRowModel> filtered = new List<ScenarioBookRowModel>();
            for (int i = 0; i < rows.Count; i++)
            {
                ScenarioBookRowModel row = rows[i];
                bool persistentRootNavigation = view == ScenarioBookBrowserViewKind.Types
                    && row != null
                    && (row.Kind == ScenarioBookRowKind.OpenInstallScenarios
                        || (row.Kind == ScenarioBookRowKind.Type && row.Type == ScenarioBookType.Draft));
                if (persistentRootNavigation || MatchesSearch(row, searchFilter))
                    filtered.Add(row);
            }

            return filtered;
        }

        private static bool MatchesSearch(ScenarioBookRowModel row, string searchFilter)
        {
            if (row == null)
                return false;

            return ContainsSearch(row.Title, searchFilter)
                || ContainsSearch(row.Detail, searchFilter)
                || ContainsSearch(row.Badge, searchFilter)
                || ContainsSearch(row.SectionLabel, searchFilter)
                || ContainsSearch(row.Type.ToString(), searchFilter)
                || (row.Scenario != null && MatchesScenario(row.Scenario, searchFilter))
                || (row.Save != null && MatchesSave(row.Save, searchFilter))
                || (row.SaveDetail != null && MatchesSaveDetail(row.SaveDetail, searchFilter));
        }

        private static bool MatchesScenario(ScenarioCatalogEntry scenario, string searchFilter)
        {
            return ContainsSearch(scenario.ScenarioId, searchFilter)
                || ContainsSearch(scenario.DisplayName, searchFilter)
                || ContainsSearch(scenario.Description, searchFilter)
                || ContainsSearch(scenario.OwnerModId, searchFilter)
                || ContainsSearch(scenario.Version, searchFilter)
                || ContainsSearch(scenario.BaseGameMode.ToString(), searchFilter)
                || ContainsSearch(scenario.Source.ToString(), searchFilter);
        }

        private static bool MatchesSave(SaveEntry save, string searchFilter)
        {
            return ContainsSearch(save.id, searchFilter)
                || ContainsSearch(save.name, searchFilter)
                || ContainsSearch(save.createdAt, searchFilter)
                || ContainsSearch(save.updatedAt, searchFilter)
                || ContainsSearch(save.gameVersion, searchFilter)
                || ContainsSearch(save.modApiVersion, searchFilter)
                || ContainsSearch(save.scenarioId, searchFilter)
                || ContainsSearch(save.scenarioVersion, searchFilter)
                || ContainsSearch(GetSaveTime(save), searchFilter)
                || (save.saveInfo != null && ContainsSearch(save.saveInfo.familyName, searchFilter));
        }

        private static bool MatchesSaveDetail(ScenarioBookSaveDetailModel detail, string searchFilter)
        {
            return ContainsSearch(detail.BindingScenarioId, searchFilter)
                || ContainsSearch(detail.VersionApplied, searchFilter)
                || ContainsSearch(detail.ScenarioOutcome, searchFilter)
                || ContainsSearch(detail.ScenarioOutcomeConditionId, searchFilter)
                || ContainsSearch(detail.ScoreCompletionState, searchFilter)
                || ContainsSearch(detail.ScoreHasTotal ? detail.ScoreTotal.ToString(CultureInfo.InvariantCulture) : null, searchFilter)
                || ContainsSearch(detail.MetadataError, searchFilter)
                || ContainsSearch(BuildStatusLabel(detail), searchFilter)
                || ContainsSearch(BuildOutcomeLabel(detail), searchFilter);
        }

        private static bool ContainsSearch(string value, string searchFilter)
        {
            return !string.IsNullOrEmpty(value)
                && value.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }
    }
}
