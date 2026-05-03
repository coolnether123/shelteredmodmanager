using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Panels;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioBookBrowserPanel : MonoBehaviour
    {
        internal const int RowsPerPage = 5;
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
        private bool _isClosing;

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

        public static void Show(ScenarioSelectionPanel panel)
        {
            if (panel == null)
                return;

            if (_instance != null)
                Destroy(_instance);

            GameObject root = FieldManualWindowChrome.CreateOverlayRoot(OverlayName, OverlayDepth, "ScenarioBookBrowser_Root");
            _instance = root;

            ScenarioBookBrowserPanel browser = root.AddComponent<ScenarioBookBrowserPanel>();
            browser._adapter = new ScenarioBrowserPanelAdapter(panel);
            browser.Initialise(root);
        }

        private void Initialise(GameObject root)
        {
            _adapter.SetInputEnabled(false);
            _dataSource = new ScenarioBookBrowserDataSource(Catalog, SaveLibrary);
            _actions = new ScenarioBookBrowserActionService(_adapter, LaunchCoordinator, SaveLibrary);
            _dataSource.Refresh();

            VanillaPageTurnAssets pageTurnAssets = new VanillaPageTurnAssets();
            _renderer = new ScenarioBookBrowserRenderer(BackOrClose, Close, ChangePage);
            _renderer.Build(root, OverlayDepth, pageTurnAssets);
            _pageTurn = FieldManualBookPageTurn.Attach(root, _renderer.Chrome, pageTurnAssets);
            _pageFlipRoot = _renderer.Chrome.Ui.CreateChild(root, "BookPageFlipRoot", Vector3.zero);

            SetStatus("Choose a scenario type.");
            RenderCurrentView(false);
        }

        private void Update()
        {
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
            if (delta < 0)
                return _pageIndex > 0;
            if (delta > 0)
                return _pageIndex + 1 < GetPageCount();

            return false;
        }

        private void CommitPageChange(int delta)
        {
            _pageIndex = Mathf.Clamp(_pageIndex + delta, 0, GetPageCount() - 1);
        }

        private void RenderCurrentPageWithoutAnimation()
        {
            RenderCurrentView(false);
        }

        private void RenderCurrentView(bool animate)
        {
            _rows = _dataSource.BuildRows(_view, _selectedType, _selectedScenario);
            _pageIndex = Mathf.Clamp(_pageIndex, 0, GetPageCount() - 1);

            _renderer.Render(
                _view,
                _selectedScenario,
                _rows,
                _pageIndex,
                GetPageCount(),
                _dataSource.GetHeaderTitle(_view, _selectedType, _selectedScenario),
                _dataSource.GetHeaderDetail(_view, _selectedScenario),
                HandleRowSelected,
                HandleDeleteSelected);

            if (animate && _pageTurn != null && _pageTurn.PageTransition != null)
            {
                _pageTurn.PageTransition.Play(_renderer.ContentRoot);
                _pageTurn.PageTransition.Play(_renderer.PageLabelRoot);
            }
        }

        private void HandleRowSelected(ScenarioBookRowModel row)
        {
            if (row == null || row.IsLocked)
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
                    SelectScenario(row.Scenario);
                    break;
                case ScenarioBookRowKind.StartScenario:
                    RunLaunchAction(delegate(out string status) { return _actions.StartScenario(row.Scenario, out status); });
                    break;
                case ScenarioBookRowKind.OpenDraft:
                    RunLaunchAction(delegate(out string status) { return _actions.OpenDraft(row.Scenario, out status); });
                    break;
                case ScenarioBookRowKind.CreateDraft:
                    RunLaunchAction(delegate(out string status) { return _actions.CreateDraft(out status); });
                    break;
                case ScenarioBookRowKind.LoadSave:
                    RunLaunchAction(delegate(out string status) { return _actions.LoadSave(row.Scenario, row.Save, out status); });
                    break;
            }
        }

        private void HandleDeleteSelected(ScenarioBookRowModel row)
        {
            if (row == null || row.Scenario == null || row.Save == null)
                return;

            MessageBox.Show(MessageBoxButtons.YesNo_Buttons, "Text.UI.DeleteSave", new MessageBoxResponse(delegate(int response)
            {
                if (response != 1)
                    return;

                string status;
                if (!_actions.DeleteSave(row.Scenario, row.Save, out status))
                {
                    SetStatus(status);
                    return;
                }

                _dataSource.Refresh();
                SetStatus(status);
                RenderCurrentView(true);
            }));
        }

        private void SelectType(ScenarioBookType type)
        {
            _selectedType = type;
            _selectedScenario = null;
            _view = ScenarioBookBrowserViewKind.Scenarios;
            _pageIndex = 0;
            SetStatus("Choose a scenario.");
            RenderCurrentView(true);
        }

        private void SelectScenario(ScenarioCatalogEntry scenario)
        {
            if (scenario == null)
                return;

            _selectedScenario = scenario;
            _view = ScenarioBookBrowserViewKind.Saves;
            _pageIndex = 0;
            SetStatus("Choose a save slot for " + Safe(scenario.DisplayName, scenario.ScenarioId) + ".");
            RenderCurrentView(true);
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

            Close();
        }

        private void BackOrClose()
        {
            if (_view == ScenarioBookBrowserViewKind.Saves)
            {
                _selectedScenario = null;
                _view = _selectedType == ScenarioBookType.Published
                    ? ScenarioBookBrowserViewKind.Types
                    : ScenarioBookBrowserViewKind.Scenarios;
                _pageIndex = 0;
                SetStatus(_view == ScenarioBookBrowserViewKind.Types ? "Choose a scenario category." : "Choose a scenario.");
                RenderCurrentView(true);
                return;
            }

            if (_view == ScenarioBookBrowserViewKind.Scenarios)
            {
                _view = ScenarioBookBrowserViewKind.Types;
                _pageIndex = 0;
                SetStatus("Choose a scenario type.");
                RenderCurrentView(true);
                return;
            }

            Close();
        }

        private void Close()
        {
            if (_isClosing)
                return;

            _isClosing = true;
            RestoreUnderlyingPanel();
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }

            if (_instance != null)
                Destroy(_instance);
            _instance = null;
        }

        private void OnDestroy()
        {
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
                if (_adapter != null)
                    _adapter.SetInputEnabled(true);
            }
            catch
            {
            }
        }

        private int GetPageCount()
        {
            int rowCount = Math.Max(1, _rows != null ? _rows.Count : 0);
            if (_view == ScenarioBookBrowserViewKind.Saves)
                return rowCount;

            return Math.Max(1, (rowCount + RowsPerPage - 1) / RowsPerPage);
        }

        private void SetStatus(string value)
        {
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
