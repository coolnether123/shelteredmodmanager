using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.UI;
using ModAPI.Core;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Panels;
using UnityEngine;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal sealed class ScenarioBookBrowserPanel : MonoBehaviour
    {
        internal const int RowsPerPage = 5;
        internal const int SaveRowsPerPage = 4;
        private const int OverlayDepth = 50200;
        private const string OverlayName = "ShelteredAPI_ScenarioBookBrowser";

        private static GameObject _instance;

        private ScenarioBrowserPanelAdapter _adapter;
        private ScenarioBookBrowserDataSource _dataSource;
        private ScenarioBookBrowserActionService _actions;
        private ScenarioBookBrowserRenderer _renderer;
        private FieldManualBookPageTurn _pageTurn;
        private GameObject _pageFlipRoot;
        private ScenarioBookBrowserViewKind _view = ScenarioBookBrowserViewKind.Types;
        private ScenarioBookType _selectedType = ScenarioBookType.Draft;
        private ScenarioCatalogEntry _selectedScenario;
        private List<ScenarioBookRowModel> _rows = new List<ScenarioBookRowModel>();
        private int _pageIndex;
        private string _lastRenderScopeKey;
        private bool _selectedScenarioOpenedDirectlyFromType;
        private bool _isClosing;
        private bool _deletePromptActive;
        private List<Collider> _deletePromptDisabledColliders;
        private ScenarioBrowserPanelAdapter.ScenarioBrowserSuppressionHandle _underlyingSuppression;
        private ScenarioBookDraftFactsModel _draftFactsCache;
        private ScenarioCatalogEntry _draftFactsCacheScenario;

        private IScenarioSelectionCatalogService Catalog
        {
            get { return ScenarioCompositionRoot.Resolve<IScenarioSelectionCatalogService>(); }
        }

        private IScenarioSaveLibrary SaveLibrary
        {
            get { return ScenarioCompositionRoot.Resolve<IScenarioSaveLibrary>(); }
        }

        private ScenarioLaunchCoordinator LaunchCoordinator
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioLaunchCoordinator>(); }
        }

        private ScenarioDraftMetadataEditService DraftMetadataEditService
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioDraftMetadataEditService>(); }
        }

        public static void Show(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return;

            if (_instance != null)
            {
                // A previous close can still be pending Unity destruction. Hide
                // that root synchronously so it cannot masquerade as this browser.
                _instance.SetActive(false);
                Destroy(_instance);
                _instance = null;
            }

            GameObject root = FieldManualWindowChrome.CreateOverlayRoot(OverlayName, OverlayDepth, "ScenarioBookBrowser_Root");
            _instance = root;

            ScenarioBookBrowserPanel browser = root.AddComponent<ScenarioBookBrowserPanel>();
            browser._adapter = new ScenarioBrowserPanelAdapter(panel);
            browser.Initialise(root);
        }

        /// <summary>
        /// Routes the vanilla scenario-selection cancel seam into the live book when
        /// it owns the foreground.  The book suppresses that panel's input while it
        /// is open, so letting cancel continue to vanilla would leave the overlay
        /// orphaned behind a changed menu state.
        /// </summary>
        internal static bool TryHandleCancel()
        {
            if (_instance == null || !_instance.activeInHierarchy)
                return false;

            ScenarioBookBrowserPanel browser = _instance.GetComponent<ScenarioBookBrowserPanel>();
            if (browser == null || browser._isClosing)
                return false;

            browser.BackOrClose();
            return true;
        }

        private void Initialise(GameObject root)
        {
            _adapter.SetInputEnabled(false);
            _underlyingSuppression = _adapter.SuppressUnderlyingChrome();
            _dataSource = new ScenarioBookBrowserDataSource(Catalog, SaveLibrary);
            _actions = new ScenarioBookBrowserActionService(_adapter, LaunchCoordinator, SaveLibrary, DraftMetadataEditService);

            VanillaPageTurnAssets pageTurnAssets = new VanillaPageTurnAssets();
            _renderer = new ScenarioBookBrowserRenderer(BackOrClose, Close, ChangePage);
            _renderer.Build(root, OverlayDepth, pageTurnAssets);
            _pageTurn = FieldManualBookPageTurn.Attach(root, _renderer.Chrome, pageTurnAssets);
            _pageFlipRoot = _renderer.Chrome.Ui.CreateChild(root, "BookPageFlipRoot", Vector3.zero);

            StartDataRefresh("Loading scenarios...");
        }

        private void Update()
        {
            if (_deletePromptActive)
                return;

            ApplyDataRefreshIfReady();

            if (_renderer != null)
                _renderer.HandleSearchInput(HandleSearchFilterChanged);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                BackOrClose();
                return;
            }

            HandlePageInput();
        }

        private void HandlePageInput()
        {
            if (_pageTurn != null)
                _pageTurn.HandlePageInput(GetPageCount(), null, ChangePage);
        }

        private void ChangePage(int delta)
        {
            int targetPage = ResolveTargetPageIndex(delta);
            PreparePage(targetPage);

            if (_pageTurn != null)
            {
                _pageTurn.TryTurn(
                    delta,
                    _renderer != null ? _renderer.ContentRoot : null,
                    _pageFlipRoot != null ? _pageFlipRoot : (_renderer != null ? _renderer.Viewport : null),
                    _renderer != null ? _renderer.PageLabelRoot : null,
                    CanChangePage,
                    CommitPageChange,
                    RenderCurrentPageWithoutAnimation);
                return;
            }

            if (!CanChangePage(delta))
                return;

            CommitPageChange(delta);
            RenderCurrentPageWithoutAnimation();
        }

        private bool CanChangePage(int delta)
        {
            if (_view == ScenarioBookBrowserViewKind.Saves && GetPageCount() > 1)
                return delta != 0;

            if (delta < 0)
                return _pageIndex > 0;
            if (delta > 0)
                return _pageIndex + 1 < GetPageCount();

            return false;
        }

        private int ResolveTargetPageIndex(int delta)
        {
            int pageCount = GetPageCount();
            if (pageCount <= 0)
                return 0;

            if (_view == ScenarioBookBrowserViewKind.Saves && pageCount > 1)
            {
                int wrapped = (_pageIndex + delta) % pageCount;
                return wrapped < 0 ? wrapped + pageCount : wrapped;
            }

            return Mathf.Clamp(_pageIndex + delta, 0, pageCount - 1);
        }

        private void CommitPageChange(int delta)
        {
            int pageCount = GetPageCount();
            if (_view == ScenarioBookBrowserViewKind.Saves && pageCount > 1)
            {
                _pageIndex = (_pageIndex + delta) % pageCount;
                if (_pageIndex < 0)
                    _pageIndex += pageCount;
                return;
            }

            _pageIndex = Mathf.Clamp(_pageIndex + delta, 0, pageCount - 1);
        }

        private void RenderCurrentPageWithoutAnimation()
        {
            RenderCurrentView(false);
        }

        private void RenderCurrentView(bool animate)
        {
            try
            {
                _rows = _dataSource.BuildRows(_view, _selectedType, _selectedScenario, GetSearchFilter());
                _pageIndex = Mathf.Clamp(_pageIndex, 0, GetPageCount() - 1);
                ClearPreparedPagesWhenScopeChanged();

                if (_view == ScenarioBookBrowserViewKind.DraftDetails)
                {
                    _renderer.RenderDraftEditor(
                        BuildDraftEditorModel(),
                        _dataSource.GetHeaderTitle(_view, _selectedType, _selectedScenario),
                        _dataSource.GetHeaderDetail(_view, _selectedType, _selectedScenario),
                        HandleDraftDetailsSaved,
                        HandleDraftOpenRequested,
                        HandleDraftDeleteRequested);
                }
                else
                {
                    int pageCount = GetPageCount();
                    ScenarioBookPlayStatsModel playStats = BuildCurrentPlayStats();
                    string cacheKey = BuildPageCacheKey(_pageIndex);
                    if (!animate && _renderer.TryRenderPreparedPage(cacheKey, _pageIndex, pageCount))
                    {
                        PrepareAdjacentPages();
                        return;
                    }

                    _renderer.Render(
                        _view,
                        _selectedScenario,
                        playStats,
                        _rows,
                        _pageIndex,
                        pageCount,
                        _dataSource.GetHeaderTitle(_view, _selectedType, _selectedScenario),
                        _dataSource.GetHeaderDetail(_view, _selectedType, _selectedScenario),
                        HandleRowSelected,
                        HandleDeleteSelected);
                }

                if (animate && _pageTurn != null && _pageTurn.PageTransition != null)
                {
                    _pageTurn.PageTransition.Play(_renderer.ContentRoot);
                    _pageTurn.PageTransition.Play(_renderer.PageLabelRoot);
                }

                PrepareAdjacentPages();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookBrowser] Render failed. view=" + _view
                    + " selectedType=" + _selectedType
                    + " selectedScenario=" + (_selectedScenario != null ? _selectedScenario.ScenarioId : "<none>")
                    + ": " + ex);
            }
        }

        private void HandleRowSelected(ScenarioBookRowModel row)
        {
            try
            {
                if (_deletePromptActive)
                    return;

                if (row == null || row.IsLocked)
                {
                    SetStatus("Scenario is locked by missing or mismatched dependencies.");
                    return;
                }

                MMLog.WriteInfo("[ScenarioBookBrowser] Row selected. kind=" + row.Kind
                    + " type=" + row.Type
                    + " scenario=" + (row.Scenario != null ? row.Scenario.ScenarioId : "<none>")
                    + " save=" + (row.Save != null ? row.Save.id : "<none>"));

                switch (row.Kind)
                {
                    case ScenarioBookRowKind.Type:
                        SelectType(row.Type);
                        break;
                    case ScenarioBookRowKind.Scenario:
                        _selectedType = row.Type;
                        _selectedScenarioOpenedDirectlyFromType = false;
                        SelectScenario(row.Scenario);
                        break;
                    case ScenarioBookRowKind.StartScenario:
                        RunLaunchAction(delegate(out string status) { return _actions.StartScenario(row.Scenario, out status); });
                        break;
                    case ScenarioBookRowKind.OpenDraft:
                        RunLaunchAction(delegate(out string status) { return _actions.OpenDraft(row.Scenario, out status); });
                        break;
                    case ScenarioBookRowKind.CreateDraft:
                        RunLaunchAction(delegate(out string status) { return _actions.CreateDraftInteractive(out status); });
                        break;
                    case ScenarioBookRowKind.DuplicateDraft:
                        HandleDuplicateSelected(row);
                        break;
                    case ScenarioBookRowKind.DeleteDraft:
                        HandleDeleteSelected(row);
                        break;
                    case ScenarioBookRowKind.RecoveryResume:
                        RunBrowserAction(delegate(out string status) { return _actions.ResumeRecovery(row, out status); });
                        break;
                    case ScenarioBookRowKind.RecoveryCleanup:
                        HandleRecoveryCleanupSelected(row);
                        break;
                    case ScenarioBookRowKind.LoadSave:
                        RunLaunchAction(delegate(out string status) { return _actions.LoadSave(row.Scenario, row.Save, out status); });
                        break;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookBrowser] Row action failed. kind="
                    + (row != null ? row.Kind.ToString() : "<null>") + ": " + ex);
                SetStatus("Scenario action failed: " + ex.Message);
            }
        }

        private void HandleDeleteSelected(ScenarioBookRowModel row)
        {
            if (_deletePromptActive)
                return;

            if (row == null || (row.Kind == ScenarioBookRowKind.LoadSave && (row.Scenario == null || row.Save == null)))
                return;

            string message;
            bool localize;
            if (row.Kind == ScenarioBookRowKind.DeleteDraft)
            {
                BeginDraftDeleteConfirmation(row.Scenario);
                return;
            }
            else
            {
                message = "Text.UI.DeleteSave";
                localize = true;
            }

            BeginConfirmation(message, localize, delegate
            {
                string status = null;
                bool deleted = false;
                try
                {
                    deleted = _actions.DeleteSave(row.Scenario, row.Save, out status);
                }
                catch (Exception ex)
                {
                    status = "Delete failed: " + ex.Message;
                    MMLog.WriteWarning("[ScenarioBookBrowser] Delete action threw: " + ex.Message);
                }

                SetStatus(status);
                if (deleted)
                    StartDataRefresh("Refreshing scenarios...");
            });
        }

        private void HandleDuplicateSelected(ScenarioBookRowModel row)
        {
            if (_deletePromptActive || row == null || row.Scenario == null)
                return;

            ScenarioBookDraftFactsModel facts = ResolveDraftFactsFor(row.Scenario);
            string draftName = Safe(row.Scenario.DisplayName, row.Scenario.ScenarioId);
            BeginConfirmation(ScenarioBookDraftFacts.BuildDuplicateMessage(draftName, facts), false, delegate
            {
                string status = null;
                bool changed = false;
                try
                {
                    ModAPI.Scenarios.ScenarioInfo duplicate;
                    changed = _actions.DuplicateDraft(row.Scenario, out duplicate, out status);
                }
                catch (Exception ex)
                {
                    status = "Duplicate failed: " + ex.Message;
                    MMLog.WriteWarning("[ScenarioBookBrowser] Duplicate action threw: " + ex.Message);
                }

                SetStatus(status);
                if (changed)
                    StartDataRefresh("Refreshing scenarios...");
            });
        }

        private void HandleRecoveryCleanupSelected(ScenarioBookRowModel row)
        {
            if (_deletePromptActive || row == null)
                return;

            BeginConfirmation("Clear this leftover redirect?\nNo save or draft files are deleted.", false, delegate
            {
                string status = null;
                bool cleared = false;
                try
                {
                    cleared = _actions.CleanupRecovery(row, out status);
                }
                catch (Exception ex)
                {
                    status = "Recovery cleanup failed: " + ex.Message;
                    MMLog.WriteWarning("[ScenarioBookBrowser] Recovery cleanup action threw: " + ex.Message);
                }

                SetStatus(status);
                if (cleared)
                    StartDataRefresh("Refreshing scenarios...");
            });
        }

        // Shared two-step confirmation used by delete, duplicate, and rename so every
        // destructive/identity change goes through the same input-guard and
        // click-release protection.
        private void BeginConfirmation(string message, bool localize, Action onConfirmed)
        {
            if (_deletePromptActive)
                return;

            _deletePromptActive = true;
            DisableBrowserCollidersForDeletePrompt();
            UIFlowGuard.BlockSlotClicksForFrames(2);
            try
            {
                MessageBox.Show(MessageBoxButtons.YesNo_Buttons, message, new MessageBoxResponse(delegate(int response)
                {
                    StartCoroutine(ResolveConfirmationAfterClickRelease(onConfirmed, response));
                }), null, null, localize);
            }
            catch
            {
                ReleaseDeletePromptGuard();
                throw;
            }
        }

        private IEnumerator ResolveConfirmationAfterClickRelease(Action onConfirmed, int response)
        {
            UIFlowGuard.BlockSlotClicksToggle(true);
            UIFlowGuard.BlockSlotClicksForFrames(2);

            yield return null;
            while (UnityEngine.Input.GetMouseButton(0)
                || UnityEngine.Input.GetMouseButton(1)
                || UnityEngine.Input.GetMouseButton(2))
            {
                yield return null;
            }

            try
            {
                MMLog.WriteInfo("[ScenarioBookBrowser] Confirmation close resolved. response=" + response + ".");
                if (response == 1 && onConfirmed != null)
                    onConfirmed();
            }
            catch (Exception ex)
            {
                SetStatus("Action failed: " + ex.Message);
                MMLog.WriteWarning("[ScenarioBookBrowser] Confirmation resolution failed: " + ex.Message);
            }

            yield return null;
            UIFlowGuard.BlockSlotClicksForFrames(1);
            ReleaseDeletePromptGuard();
        }

        private void ReleaseDeletePromptGuard()
        {
            RestoreBrowserCollidersAfterDeletePrompt();
            UIFlowGuard.BlockSlotClicksToggle(false);
            _deletePromptActive = false;
        }

        private ScenarioBookDraftFactsModel ResolveDraftFactsFor(ScenarioCatalogEntry scenario)
        {
            if (scenario == null)
                return null;

            if (_draftFactsCache != null && ReferenceEquals(_draftFactsCacheScenario, scenario))
                return _draftFactsCache;

            try
            {
                if (_dataSource != null)
                    _dataSource.BeginDraftFactsRefreshAsync(scenario);
                return ScenarioBookDraftFacts.BuildImmediateDetailFacts(scenario);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookBrowser] Draft facts resolution failed: " + ex.Message);
                return null;
            }
        }

        private void DisableBrowserCollidersForDeletePrompt()
        {
            RestoreBrowserCollidersAfterDeletePrompt();
            _deletePromptDisabledColliders = new List<Collider>();

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; colliders != null && i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                _deletePromptDisabledColliders.Add(collider);
            }
        }

        private void RestoreBrowserCollidersAfterDeletePrompt()
        {
            if (_deletePromptDisabledColliders == null)
                return;

            for (int i = 0; i < _deletePromptDisabledColliders.Count; i++)
            {
                Collider collider = _deletePromptDisabledColliders[i];
                if (collider != null)
                    collider.enabled = true;
            }

            _deletePromptDisabledColliders = null;
        }

        private void HandleSearchFilterChanged()
        {
            _pageIndex = 0;
            ClearPreparedPages();
            RenderCurrentView(false);
        }

        private void SelectType(ScenarioBookType type)
        {
            _selectedType = type;
            _selectedScenario = null;
            _selectedScenarioOpenedDirectlyFromType = false;

            ScenarioCatalogEntry singleScenario;
            // A mode card may contain only its vanilla entry. Do not turn that
            // card into an implicit vanilla launch/detail route: published
            // packages must remain explicit, title-bearing rows in the book.
            if (type != ScenarioBookType.Draft
                && _dataSource.TryGetSingleScenarioForType(type, out singleScenario)
                && singleScenario.Source == ScenarioCatalogSource.Modded)
            {
                _selectedScenarioOpenedDirectlyFromType = true;
                SelectScenario(singleScenario);
                return;
            }

            _view = ScenarioBookBrowserViewKind.Scenarios;
            _pageIndex = 0;
            ClearPreparedPages();
            SetStatus(null);
            RenderCurrentView(true);
        }

        private void SelectScenario(ScenarioCatalogEntry scenario)
        {
            if (scenario == null)
                return;

            _selectedScenario = scenario;
            InvalidateDraftFactsCache();
            _view = scenario.Source == ScenarioCatalogSource.Draft
                ? ScenarioBookBrowserViewKind.DraftDetails
                : ScenarioBookBrowserViewKind.Saves;
            _pageIndex = 0;
            ClearPreparedPages();
            if (_view == ScenarioBookBrowserViewKind.Saves)
            {
                _dataSource.BeginSaveRowsRefreshAsync(scenario);
                SetStatus("Loading saves...");
            }
            else
            {
                SetStatus(null);
            }
            RenderCurrentView(true);
        }

        private ScenarioBookDraftEditorModel BuildDraftEditorModel()
        {
            return new ScenarioBookDraftEditorModel
            {
                Scenario = _selectedScenario,
                DraftId = _selectedScenario != null ? Safe(_selectedScenario.ScenarioId, string.Empty) : string.Empty,
                DisplayName = _selectedScenario != null ? Safe(_selectedScenario.DisplayName, _selectedScenario.ScenarioId) : string.Empty,
                Description = _selectedScenario != null ? (_selectedScenario.Description ?? string.Empty) : string.Empty,
                Facts = GetSelectedDraftFacts()
            };
        }

        // The click frame publishes cheap facts immediately. Full filesystem,
        // deserialization, and validation facts are versioned through the data source.
        private ScenarioBookDraftFactsModel GetSelectedDraftFacts()
        {
            if (_selectedScenario == null)
                return null;

            if (_draftFactsCache != null && ReferenceEquals(_draftFactsCacheScenario, _selectedScenario))
                return _draftFactsCache;

            _draftFactsCache = ScenarioBookDraftFacts.BuildImmediateDetailFacts(_selectedScenario);
            _draftFactsCacheScenario = _selectedScenario;
            if (_dataSource != null)
                _dataSource.BeginDraftFactsRefreshAsync(_selectedScenario);
            return _draftFactsCache;
        }

        private void InvalidateDraftFactsCache()
        {
            _draftFactsCache = null;
            _draftFactsCacheScenario = null;
            if (_dataSource != null)
                _dataSource.InvalidateDraftFactsRefresh();
        }

        private void HandleDraftDetailsSaved(ScenarioBookDraftEditorModel model)
        {
            if (model == null)
                return;

            bool isRename = _selectedScenario != null
                && !string.IsNullOrEmpty(model.DraftId)
                && !string.Equals(model.DraftId, _selectedScenario.ScenarioId, StringComparison.OrdinalIgnoreCase);

            if (isRename)
            {
                ScenarioBookDraftFactsModel facts = ResolveDraftFactsFor(_selectedScenario);
                string draftName = Safe(_selectedScenario.DisplayName, _selectedScenario.ScenarioId);
                BeginConfirmation(ScenarioBookDraftFacts.BuildRenameMessage(draftName, model.DraftId, facts), false, delegate
                {
                    ApplyDraftDetailsSaved(model);
                });
                return;
            }

            ApplyDraftDetailsSaved(model);
        }

        private void ApplyDraftDetailsSaved(ScenarioBookDraftEditorModel model)
        {
            if (model == null)
                return;

            ModAPI.Scenarios.ScenarioInfo updatedInfo;
            string status;
            if (!_actions.UpdateDraftMetadata(_selectedScenario, model.DraftId, model.DisplayName, model.Description, out updatedInfo, out status))
            {
                SetStatus(status);
                return;
            }

            if (_selectedScenario != null && updatedInfo != null)
            {
                _selectedScenario.ScenarioId = updatedInfo.Id;
                _selectedScenario.DisplayName = updatedInfo.DisplayName;
                _selectedScenario.Description = model.Description;
                _selectedScenario.Version = updatedInfo.Version;
                _selectedScenario.OwnerModId = updatedInfo.OwnerModId;
            }

            InvalidateDraftFactsCache();
            StartDataRefresh("Refreshing draft details...");
            ClearPreparedPages();
            SetStatus(status);
            RenderCurrentView(false);
        }

        private void HandleDraftOpenRequested()
        {
            RunLaunchAction(delegate(out string status) { return _actions.OpenDraft(_selectedScenario, out status); });
        }

        private void HandleDraftDeleteRequested()
        {
            BeginDraftDeleteConfirmation(_selectedScenario);
        }

        private void BeginDraftDeleteConfirmation(ScenarioCatalogEntry scenario)
        {
            if (_deletePromptActive || scenario == null)
                return;

            MMLog.WriteInfo("[ScenarioBookBrowser] Draft delete confirmation opened. draft='" + scenario.ScenarioId + "'.");

            ScenarioBookDraftFactsModel facts = ResolveDraftFactsFor(scenario);
            string draftName = Safe(scenario.DisplayName, scenario.ScenarioId);
            bool localize;
            string message = ScenarioBookDraftFacts.BuildDeleteMessage(draftName, facts);
            localize = false;
            BeginConfirmation(message, localize, delegate
            {
                MMLog.WriteInfo("[ScenarioBookBrowser] Draft delete confirmation accepted. draft='" + scenario.ScenarioId + "'.");
                string status;
                bool deleted = _actions.DeleteDraft(scenario, out status);
                MMLog.WriteInfo("[ScenarioBookBrowser] Draft delete callback completed. draft='" + scenario.ScenarioId
                    + "' deleted=" + deleted + " status='" + status + "'.");
                SetStatus(status);
                if (deleted)
                {
                    if (ReferenceEquals(_selectedScenario, scenario))
                        BackOrClose();
                    StartDataRefresh("Refreshing scenarios...");
                }
            });
        }

        private void RunLaunchAction(ScenarioBookLaunchAction action)
        {
            if (action == null)
                return;

            string status;
            if (!action(out status))
            {
                SetStatus(status);
                return;
            }

            // A successful launch starts the loading transition synchronously.  Do not
            // reactivate the vanilla selection panel after that point: Unity may already
            // be destroying it as part of the scene handoff.
            Close(false);
        }

        private void RunBrowserAction(ScenarioBookLaunchAction action)
        {
            if (action == null)
                return;

            string status;
            bool changed = action(out status);
            SetStatus(status);
            if (changed)
            {
                StartDataRefresh("Refreshing scenarios...");
            }
        }

        private void BackOrClose()
        {
            if (_view == ScenarioBookBrowserViewKind.Saves || _view == ScenarioBookBrowserViewKind.DraftDetails)
            {
                InvalidateDraftFactsCache();
                _selectedScenario = null;
                _view = _selectedType == ScenarioBookType.Published || _selectedScenarioOpenedDirectlyFromType
                    ? ScenarioBookBrowserViewKind.Types
                    : ScenarioBookBrowserViewKind.Scenarios;
                _selectedScenarioOpenedDirectlyFromType = false;
                _pageIndex = 0;
                ClearPreparedPages();
                SetStatus(null);
                RenderCurrentView(true);
                return;
            }

            if (_view == ScenarioBookBrowserViewKind.Scenarios)
            {
                _view = ScenarioBookBrowserViewKind.Types;
                _pageIndex = 0;
                ClearPreparedPages();
                SetStatus(null);
                RenderCurrentView(true);
                return;
            }

            Close();
        }

        private void Close()
        {
            Close(true);
        }

        private void Close(bool restoreUnderlyingPanel)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            // The static reference can be cleared by a deferred prior teardown.
            // This component's root is the authoritative object that must leave
            // the hierarchy before renderer disposal releases its visible chrome.
            GameObject root = gameObject;
            GameObject overlay = root.transform.parent != null ? root.transform.parent.gameObject : root;
            _instance = null;
            if (_dataSource != null)
                _dataSource.CancelRefreshes();
            if (restoreUnderlyingPanel)
                RestoreUnderlyingPanel();
            else
            {
                // The selection panel is being unloaded.  Its suppressed chrome
                // must stay suppressed: restoring it from OnDestroy would call
                // SetActive(true) while Unity is destroying the old scene.
                _underlyingSuppression = null;
                _adapter = null;
            }
            // EnsureOverlayPanel owns the named UI Root/ShelteredAPI_ScenarioBookBrowser
            // parent. Hiding only this child leaves an active empty overlay that the
            // harness and the next hub click can mistake for a live book.
            overlay.SetActive(false);
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }

            Destroy(root);
        }

        private void OnDestroy()
        {
            if (_deletePromptActive)
                ReleaseDeletePromptGuard();

            if (_dataSource != null)
                _dataSource.CancelRefreshes();
            RestoreUnderlyingPanel();
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }

            if (_instance == gameObject)
                _instance = null;
        }

        private void RestoreUnderlyingPanel()
        {
            try
            {
                if (_underlyingSuppression != null)
                {
                    _underlyingSuppression.Restore();
                    _underlyingSuppression = null;
                }

                if (_adapter != null)
                {
                    _adapter.SetInputEnabled(true);
                    // OnDestroy can follow Close in the same frame.  Clearing this
                    // reference makes restoration one-shot, avoiding SetActive while
                    // the vanilla panel is being torn down during a launch transition.
                    _adapter = null;
                }
            }
            catch
            {
            }
        }

        private int GetPageCount()
        {
            if (_view == ScenarioBookBrowserViewKind.DraftDetails)
                return 1;

            int rowCount = Math.Max(1, _rows != null ? _rows.Count : 0);
            if (_view == ScenarioBookBrowserViewKind.Saves)
                return Math.Max(1, (rowCount + SaveRowsPerPage - 1) / SaveRowsPerPage);

            return Math.Max(1, (rowCount + RowsPerPage - 1) / RowsPerPage);
        }

        private void PrepareAdjacentPages()
        {
            int pageCount = GetPageCount();
            if (_renderer == null || pageCount <= 1 || _view == ScenarioBookBrowserViewKind.DraftDetails)
                return;

            if (CanChangePage(1))
                PreparePage(ResolveTargetPageIndex(1));
            if (CanChangePage(-1))
                PreparePage(ResolveTargetPageIndex(-1));
        }

        private void PreparePage(int pageIndex)
        {
            if (_renderer == null || _view == ScenarioBookBrowserViewKind.DraftDetails)
                return;

            int pageCount = GetPageCount();
            if (pageCount <= 1 || pageIndex < 0 || pageIndex >= pageCount)
                return;

            _renderer.PreparePage(
                BuildPageCacheKey(pageIndex),
                _view,
                _selectedScenario,
                BuildCurrentPlayStats(),
                _rows,
                pageIndex,
                pageCount,
                _dataSource.GetHeaderTitle(_view, _selectedType, _selectedScenario),
                _dataSource.GetHeaderDetail(_view, _selectedType, _selectedScenario),
                HandleRowSelected,
                HandleDeleteSelected);
        }

        private void ClearPreparedPagesWhenScopeChanged()
        {
            string scopeKey = BuildRenderScopeKey();
            if (string.Equals(_lastRenderScopeKey, scopeKey, StringComparison.Ordinal))
                return;

            ClearPreparedPages();
            _lastRenderScopeKey = scopeKey;
        }

        private void ClearPreparedPages()
        {
            if (_renderer != null)
                _renderer.ClearPreparedPages();
            _lastRenderScopeKey = null;
        }

        private void StartDataRefresh(string status)
        {
            if (_dataSource == null)
                return;

            ClearPreparedPages();
            SetStatus(status);
            _dataSource.InvalidateCatalogSnapshot();
            _dataSource.BeginRefreshAsync();
            if (_renderer != null)
                RenderCurrentView(false);
        }

        private void ApplyDataRefreshIfReady()
        {
            if (_dataSource == null)
                return;

            bool changed = _dataSource.ApplyLatestSnapshot();
            changed = _dataSource.ApplyLatestSaveRows() || changed;
            ScenarioCatalogEntry factsScenario;
            ScenarioBookDraftFactsModel facts;
            if (_dataSource.ApplyLatestDraftFacts(out factsScenario, out facts))
            {
                if (facts != null && ReferenceEquals(factsScenario, _selectedScenario))
                {
                    _draftFactsCache = facts;
                    _draftFactsCacheScenario = factsScenario;
                    changed = true;
                }
            }
            if (!changed)
                return;

            ClearPreparedPages();
            string error = _dataSource.LastRefreshError;
            SetStatus(string.IsNullOrEmpty(error) ? null : "Scenario refresh failed: " + error);
            RenderCurrentView(false);
        }

        private ScenarioBookPlayStatsModel BuildCurrentPlayStats()
        {
            if (_view != ScenarioBookBrowserViewKind.Saves || _dataSource == null)
                return null;

            IList<ScenarioBookRowModel> statRows = _rows;
            if (!string.IsNullOrEmpty(GetSearchFilter()))
                statRows = _dataSource.BuildRows(_view, _selectedType, _selectedScenario, null);

            return ScenarioBookPlayStatsBuilder.Build(_selectedScenario, statRows);
        }

        private string BuildPageCacheKey(int pageIndex)
        {
            return BuildRenderScopeKey() + "|page=" + pageIndex;
        }

        private string BuildRenderScopeKey()
        {
            string scenarioId = _selectedScenario != null ? _selectedScenario.ScenarioId : string.Empty;
            return _view + "|" + _selectedType + "|" + scenarioId + "|search=" + GetSearchFilter() + "|rows=" + (_rows != null ? _rows.Count : 0);
        }

        private string GetSearchFilter()
        {
            // Detail spreads intentionally hide search; keep the list filter intact
            // for Back navigation without silently applying it to save rows.
            if (_view == ScenarioBookBrowserViewKind.Saves || _view == ScenarioBookBrowserViewKind.DraftDetails)
                return string.Empty;

            return _renderer != null ? _renderer.SearchFilter : string.Empty;
        }

        private void SetStatus(string value)
        {
            if (!string.IsNullOrEmpty(value))
                MMLog.WriteInfo("[ScenarioBookBrowser] Status: " + value);
            if (_renderer != null)
                _renderer.SetStatus(value);
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }

        private delegate bool ScenarioBookLaunchAction(out string status);
    }
}
