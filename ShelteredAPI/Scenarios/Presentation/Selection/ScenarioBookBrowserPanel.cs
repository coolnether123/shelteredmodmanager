using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.UI;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Panels;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.Selection
{
    /// <summary>
    /// Runtime browser for installed custom scenarios and their saves. The optional
    /// editor owns every authoring and package-management surface.
    /// </summary>
    internal sealed class ScenarioBookBrowserPanel : MonoBehaviour
    {
        internal const int RowsPerPage = 4;
        internal const int LibraryRowsPerPage = 4;
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
        private ScenarioBookType _selectedType = ScenarioBookType.Published;
        private ScenarioCatalogEntry _selectedScenario;
        private List<ScenarioBookRowModel> _rows = new List<ScenarioBookRowModel>();
        private int _pageIndex;
        private string _lastRenderScopeKey;
        private bool _selectedScenarioOpenedDirectlyFromType;
        private bool _isClosing;
        private bool _deletePromptActive;
        private List<Collider> _deletePromptDisabledColliders;
        private ScenarioBrowserPanelAdapter.ScenarioBrowserSuppressionHandle _underlyingSuppression;
        private string _statusText;

        internal static bool IsShowing
        {
            get { return _instance != null && _instance.activeInHierarchy; }
        }

        private IScenarioSelectionCatalogService Catalog
        {
            get { return ScenarioRuntimeCompositionRoot.Resolve<IScenarioSelectionCatalogService>(); }
        }

        private IScenarioSaveLibrary SaveLibrary
        {
            get { return ScenarioRuntimeCompositionRoot.Resolve<IScenarioSaveLibrary>(); }
        }

        private ScenarioLaunchCoordinator LaunchCoordinator
        {
            get { return ScenarioRuntimeCompositionRoot.Resolve<ScenarioLaunchCoordinator>(); }
        }

        public static void Show(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return;

            if (_instance != null)
            {
                _instance.SetActive(false);
                Destroy(_instance);
                _instance = null;
            }

            GameObject root = FieldManualWindowChrome.CreateOverlayRoot(OverlayName, OverlayDepth, "ScenarioBookBrowser_Root");
            ScenarioBookBrowserPanel browser = root.AddComponent<ScenarioBookBrowserPanel>();
            browser.PrepareVisual(root);
            _instance = root;
            browser._adapter = new ScenarioBrowserPanelAdapter(panel);
            browser.Initialise(root);
        }

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

        internal static void NotifyUnderlyingPanelTeardown(ScenarioSelectionPanel panel)
        {
            if (panel == null || _instance == null)
                return;

            ScenarioBookBrowserPanel browser = _instance.GetComponent<ScenarioBookBrowserPanel>();
            if (browser == null || browser._adapter == null || browser._adapter.Panel != panel)
                return;

            browser._underlyingSuppression = null;
            browser._adapter = null;
        }

        private void Initialise(GameObject root)
        {
            _adapter.SetInputEnabled(false);
            IScenarioSaveLibrary saveLibrary = SaveLibrary;
            _dataSource = new ScenarioBookBrowserDataSource(Catalog, saveLibrary);
            _actions = new ScenarioBookBrowserActionService(
                _adapter,
                delegate { return LaunchCoordinator; },
                saveLibrary);

            PrepareVisual(root);
            StartDataRefresh("Loading scenarios...", false);
            StartCoroutine(SuppressUnderlyingAfterFirstRender());
        }

        private void PrepareVisual(GameObject root)
        {
            if (_renderer != null)
                return;

            VanillaPageTurnAssets pageTurnAssets = new VanillaPageTurnAssets();
            _renderer = new ScenarioBookBrowserRenderer(
                BackOrClose,
                ChangePage,
                HandleLibrarySortChanged,
                HandleLibraryPinToggled);
            _renderer.Build(root, OverlayDepth, pageTurnAssets);
            _pageTurn = FieldManualBookPageTurn.Attach(root, _renderer.Chrome, pageTurnAssets);
            _pageFlipRoot = _renderer.Chrome.Ui.CreateChild(root, "BookPageFlipRoot", Vector3.zero);
        }

        private IEnumerator SuppressUnderlyingAfterFirstRender()
        {
            yield return new WaitForEndOfFrame();
            if (!_isClosing && _adapter != null && _renderer != null)
                _underlyingSuppression = _adapter.SuppressUnderlyingChrome();
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
            if (_pageTurn != null)
                _pageTurn.HandlePageInput(GetPageCount(), null, ChangePage);
        }

        private void ChangePage(int delta)
        {
            if (_view == ScenarioBookBrowserViewKind.Types)
            {
                if (!CanChangePage(delta)) return;
                CommitPageChange(delta);
                RenderCurrentView(false);
                return;
            }

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
                    delegate { RenderCurrentView(false); });
                return;
            }

            if (!CanChangePage(delta)) return;
            CommitPageChange(delta);
            RenderCurrentView(false);
        }

        private bool CanChangePage(int delta)
        {
            if (_view == ScenarioBookBrowserViewKind.Saves && GetPageCount() > 1)
                return delta != 0;
            if (delta < 0) return _pageIndex > 0;
            if (delta > 0) return _pageIndex + 1 < GetPageCount();
            return false;
        }

        private int ResolveTargetPageIndex(int delta)
        {
            int pageCount = GetPageCount();
            if (pageCount <= 0) return 0;
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
                if (_pageIndex < 0) _pageIndex += pageCount;
                return;
            }
            _pageIndex = Mathf.Clamp(_pageIndex + delta, 0, pageCount - 1);
        }

        private void RenderCurrentView(bool animate)
        {
            if (_dataSource == null || _renderer == null)
                return;

            try
            {
                _rows = _dataSource.BuildRows(_view, _selectedType, _selectedScenario, GetSearchFilter());
                _pageIndex = Mathf.Clamp(_pageIndex, 0, GetPageCount() - 1);
                ClearPreparedPagesWhenScopeChanged();
                int pageCount = GetPageCount();
                string cacheKey = BuildPageCacheKey(_pageIndex);
                if (!animate && _renderer.TryRenderPreparedPage(cacheKey, _pageIndex, pageCount))
                {
                    PrepareAdjacentPages();
                    return;
                }

                _renderer.Render(
                    _view,
                    _selectedScenario,
                    BuildCurrentPlayStats(),
                    _rows,
                    _pageIndex,
                    pageCount,
                    _dataSource.GetHeaderTitle(_view, _selectedType, _selectedScenario),
                    _dataSource.GetHeaderDetail(_view, _selectedType, _selectedScenario),
                    _dataSource.LibrarySortMode,
                    HandleRowSelected,
                    HandleDeleteSelected);

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
            if (_deletePromptActive || row == null)
                return;

            try
            {
                if (row.IsLocked && row.Kind != ScenarioBookRowKind.Scenario)
                {
                    SetStatus("Scenario is locked by missing or mismatched dependencies.");
                    return;
                }

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
                    case ScenarioBookRowKind.OpenScenarioSaves:
                        OpenSelectedScenarioSaves(row.Scenario);
                        break;
                    case ScenarioBookRowKind.LoadSave:
                        RunLaunchAction(delegate(out string status) { return _actions.LoadSave(row.Scenario, row.Save, out status); });
                        break;
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBookBrowser] Row action failed. kind=" + row.Kind + ": " + ex);
                SetStatus("Scenario action failed: " + ex.Message);
            }
        }

        private void HandleDeleteSelected(ScenarioBookRowModel row)
        {
            if (_deletePromptActive || row == null || row.Kind != ScenarioBookRowKind.LoadSave
                || row.Scenario == null || row.Save == null)
                return;

            BeginConfirmation("Text.UI.DeleteSave", true, delegate
            {
                string status;
                bool deleted = _actions.DeleteSave(row.Scenario, row.Save, out status);
                SetStatus(status);
                if (!deleted)
                    return;
                _dataSource.InvalidateSaveRows(row.Scenario.StorageScenarioId);
                _dataSource.BeginSaveRowsRefreshAsync(row.Scenario);
                StartDataRefresh("Refreshing scenarios...");
            });
        }

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
                    ResolveConfirmation(onConfirmed, response);
                }), null, null, localize);
            }
            catch
            {
                ReleaseDeletePromptGuard();
                throw;
            }
        }

        private void ResolveConfirmation(Action onConfirmed, int response)
        {
            UIFlowGuard.BlockSlotClicksToggle(true);
            UIFlowGuard.BlockSlotClicksForFrames(2);
            try
            {
                if (response == 1 && onConfirmed != null)
                    onConfirmed();
            }
            catch (Exception ex)
            {
                SetStatus("Action failed: " + ex.Message);
                MMLog.WriteWarning("[ScenarioBookBrowser] Confirmation resolution failed: " + ex.Message);
            }
            finally
            {
                UIFlowGuard.BlockSlotClicksForFrames(1);
                ReleaseDeletePromptGuard();
            }
        }

        private void ReleaseDeletePromptGuard()
        {
            RestoreBrowserCollidersAfterDeletePrompt();
            UIFlowGuard.BlockSlotClicksToggle(false);
            _deletePromptActive = false;
        }

        private void DisableBrowserCollidersForDeletePrompt()
        {
            RestoreBrowserCollidersAfterDeletePrompt();
            _deletePromptDisabledColliders = new List<Collider>();
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; colliders != null && i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null || !collider.enabled) continue;
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
                if (collider != null) collider.enabled = true;
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
            if (type != ScenarioBookType.Published
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

        private void HandleLibrarySortChanged(ScenarioLibrarySortMode mode)
        {
            if (_view != ScenarioBookBrowserViewKind.Types || _dataSource == null)
                return;
            _dataSource.SetLibrarySortMode(mode);
            _pageIndex = 0;
            ClearPreparedPages();
            RenderCurrentView(false);
        }

        private void HandleLibraryPinToggled(ScenarioBookRowModel row)
        {
            if (_view != ScenarioBookBrowserViewKind.Types || _dataSource == null
                || row == null || row.Scenario == null)
                return;
            bool pinned = _dataSource.ToggleLibraryPin(row.Scenario.ScenarioId);
            SetStatus(pinned ? "Pinned to the top of the library." : "Removed from pinned scenarios.");
            _pageIndex = 0;
            ClearPreparedPages();
            RenderCurrentView(false);
        }

        private void SelectScenario(ScenarioCatalogEntry scenario)
        {
            if (scenario == null)
                return;
            _selectedScenario = scenario;
            if (_view == ScenarioBookBrowserViewKind.Types)
            {
                _dataSource.BeginSaveRowsRefreshAsync(scenario);
                SetStatus("Loading saves...");
                ClearPreparedPages();
                RenderCurrentView(false);
                return;
            }
            OpenSelectedScenarioSaves(scenario);
        }

        private void OpenSelectedScenarioSaves(ScenarioCatalogEntry scenario)
        {
            if (scenario == null)
                return;
            _selectedScenario = scenario;
            _view = ScenarioBookBrowserViewKind.Saves;
            _pageIndex = 0;
            ClearPreparedPages();
            _dataSource.BeginSaveRowsRefreshAsync(scenario);
            SetStatus("Loading saves...");
            RenderCurrentView(true);
        }

        private void RunLaunchAction(ScenarioBookLaunchAction action)
        {
            if (action == null) return;
            string status;
            if (!action(out status))
            {
                SetStatus(status);
                return;
            }
            Close(false);
        }

        private void BackOrClose()
        {
            if (_view == ScenarioBookBrowserViewKind.Saves)
            {
                bool returnToLibrary = _selectedType == ScenarioBookType.Published || _selectedScenarioOpenedDirectlyFromType;
                _view = returnToLibrary ? ScenarioBookBrowserViewKind.Types : ScenarioBookBrowserViewKind.Scenarios;
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
                _selectedScenario = null;
                _pageIndex = 0;
                ClearPreparedPages();
                SetStatus(null);
                RenderCurrentView(true);
                return;
            }
            Close();
        }

        private void Close() { Close(true); }

        private void Close(bool restoreUnderlyingPanel)
        {
            if (_isClosing) return;
            _isClosing = true;
            GameObject root = gameObject;
            GameObject overlay = root.transform.parent != null ? root.transform.parent.gameObject : root;
            _instance = null;
            if (_dataSource != null) _dataSource.CancelRefreshes();
            if (restoreUnderlyingPanel)
            {
                StartCoroutine(CloseAfterUnderlyingFirstRender(root, overlay));
                return;
            }

            _underlyingSuppression = null;
            _adapter = null;
            overlay.SetActive(false);
            if (_renderer != null) { _renderer.Dispose(); _renderer = null; }
            Destroy(root);
        }

        private IEnumerator CloseAfterUnderlyingFirstRender(GameObject root, GameObject overlay)
        {
            RestoreUnderlyingPanel();
            yield return new WaitForEndOfFrame();
            overlay.SetActive(false);
            if (_renderer != null) { _renderer.Dispose(); _renderer = null; }
            Destroy(root);
        }

        private void OnDestroy()
        {
            if (_deletePromptActive) ReleaseDeletePromptGuard();
            if (_dataSource != null) _dataSource.CancelRefreshes();
            RestoreUnderlyingPanel();
            if (_renderer != null) { _renderer.Dispose(); _renderer = null; }
            if (_instance == gameObject) _instance = null;
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
                    _adapter = null;
                }
            }
            catch { }
        }

        private int GetPageCount()
        {
            int rowCount = Math.Max(1, _rows != null ? _rows.Count : 0);
            if (_view == ScenarioBookBrowserViewKind.Types)
            {
                int libraryRows = CountLibraryRows(_rows);
                return Math.Max(1, (Math.Max(1, libraryRows) + LibraryRowsPerPage - 1) / LibraryRowsPerPage);
            }
            if (_view == ScenarioBookBrowserViewKind.Saves)
                return Math.Max(1, (rowCount + SaveRowsPerPage - 1) / SaveRowsPerPage);
            return Math.Max(1, (rowCount + RowsPerPage - 1) / RowsPerPage);
        }

        private void PrepareAdjacentPages()
        {
            int pageCount = GetPageCount();
            if (_renderer == null || pageCount <= 1 || _view == ScenarioBookBrowserViewKind.Types)
                return;
            if (CanChangePage(1)) PreparePage(ResolveTargetPageIndex(1));
            if (CanChangePage(-1)) PreparePage(ResolveTargetPageIndex(-1));
        }

        private void PreparePage(int pageIndex)
        {
            if (_renderer == null) return;
            int pageCount = GetPageCount();
            if (pageCount <= 1 || pageIndex < 0 || pageIndex >= pageCount) return;
            _renderer.PreparePage(
                BuildPageCacheKey(pageIndex), _view, _selectedScenario, BuildCurrentPlayStats(),
                _rows, pageIndex, pageCount,
                _dataSource.GetHeaderTitle(_view, _selectedType, _selectedScenario),
                _dataSource.GetHeaderDetail(_view, _selectedType, _selectedScenario),
                HandleRowSelected, HandleDeleteSelected);
        }

        private void ClearPreparedPagesWhenScopeChanged()
        {
            string scopeKey = BuildRenderScopeKey();
            if (string.Equals(_lastRenderScopeKey, scopeKey, StringComparison.Ordinal)) return;
            ClearPreparedPages();
            _lastRenderScopeKey = scopeKey;
        }

        private void ClearPreparedPages()
        {
            if (_renderer != null) _renderer.ClearPreparedPages();
            _lastRenderScopeKey = null;
        }

        private void StartDataRefresh(string status) { StartDataRefresh(status, true); }

        private void StartDataRefresh(string status, bool invalidateSharedSnapshot)
        {
            if (_dataSource == null) return;
            ClearPreparedPages();
            bool hasWarmSnapshot = !invalidateSharedSnapshot && _dataSource.HasAppliedCatalogSnapshot;
            SetStatus(hasWarmSnapshot ? null : status);
            if (invalidateSharedSnapshot) _dataSource.InvalidateCatalogSnapshot();
            _dataSource.BeginRefreshAsync(!invalidateSharedSnapshot);
            if (_renderer != null) RenderCurrentView(false);
        }

        private void ApplyDataRefreshIfReady()
        {
            if (_dataSource == null) return;
            bool changed = _dataSource.ApplyLatestSnapshot();
            changed = _dataSource.ApplyLatestSaveRows() || changed;
            if (!changed) return;
            ClearPreparedPages();
            string error = _dataSource.LastRefreshError;
            if (!string.IsNullOrEmpty(error)) SetStatus("Scenario refresh failed: " + error);
            else if (IsTransientStatus(_statusText)) SetStatus(null);
            RenderCurrentView(false);
        }

        private ScenarioBookPlayStatsModel BuildCurrentPlayStats()
        {
            if (_selectedScenario == null || _dataSource == null)
                return null;
            IList<ScenarioBookRowModel> statRows = _dataSource.BuildRows(
                ScenarioBookBrowserViewKind.Saves, _selectedType, _selectedScenario, null);
            return ScenarioBookPlayStatsBuilder.Build(_selectedScenario, statRows);
        }

        private static int CountLibraryRows(IList<ScenarioBookRowModel> rows)
        {
            int count = 0;
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                ScenarioBookRowModel row = rows[i];
                if (row != null && row.Kind != ScenarioBookRowKind.Type) count++;
            }
            return count;
        }

        private string BuildPageCacheKey(int pageIndex) { return BuildRenderScopeKey() + "|page=" + pageIndex; }

        private string BuildRenderScopeKey()
        {
            string scenarioId = _selectedScenario != null ? _selectedScenario.ScenarioId : string.Empty;
            return _view + "|" + _selectedType + "|" + scenarioId
                + "|search=" + GetSearchFilter()
                + "|sort=" + (_dataSource != null ? _dataSource.LibrarySortMode.ToString() : string.Empty)
                + "|rows=" + (_rows != null ? _rows.Count : 0);
        }

        private string GetSearchFilter()
        {
            return _renderer != null ? _renderer.SearchFilter : string.Empty;
        }

        private void SetStatus(string value)
        {
            _statusText = value;
            if (!string.IsNullOrEmpty(value)) MMLog.WriteInfo("[ScenarioBookBrowser] Status: " + value);
            if (_renderer != null) _renderer.SetStatus(value);
        }

        private static bool IsTransientStatus(string value)
        {
            return !string.IsNullOrEmpty(value)
                && (value.StartsWith("Loading ", StringComparison.Ordinal)
                    || value.StartsWith("Refreshing ", StringComparison.Ordinal));
        }

        private delegate bool ScenarioBookLaunchAction(out string status);
    }
}
