using System;
using System.Collections.Generic;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Layout;
using ShelteredAPI.UI.FieldManual.Panels;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Widgets;
using ShelteredAPI.UI.Internal.Spine;
using UnityEngine;
using ShelteredAPI.Scenarios.Application.Selection;
namespace ShelteredAPI.Scenarios.Presentation.Selection{
    internal sealed class ScenarioBookBrowserRenderer : IDisposable
    {
        private const int HeaderHeight = 86;
        private const int RowHeight = 78;
        private const int RowHitWidth = 1040;
        private const int LeftPageX = -300;
        private const int RightPageX = 300;
        private const int LeftPageWidth = 470;
        private const int RightPageWidth = 430;
        private const int RowPanelHeight = 68;
        private const int SaveListRowHeight = 68;
        private const int SaveCardHeight = 60;
        private const int DraftInputWidth = 430;
        private const float ReferenceContentWidth = 1080f;
        private const float ReferenceContentHeight = 490f;
        private const int LibraryToolRowHeight = 52;
        private const int LibraryScenarioRowHeight = 49;
        private const float SearchBarX = -355f;
        private const float SortButtonX = -147f;
        private const float SearchBarY = 232f;
        private const float SearchReservedHeight = 39f;
        private const float StatStartY = 38f;
        private const float StatLineSpacing = 25f;
        private static readonly Color PaperRule = new Color(0.35f, 0.25f, 0.16f, 0.30f);
        private static readonly Color StartCard = new Color(0.34f, 0.23f, 0.17f, 0.88f);
        private static readonly Color StartCardHover = new Color(0.47f, 0.31f, 0.21f, 0.96f);

        private sealed class PreparedPage
        {
            public GameObject Root;
            public int Height;
        }

        private readonly Action _back;
        private readonly Action<int> _changePage;
        private readonly Action<ScenarioLibrarySortMode> _selectLibrarySort;
        private readonly Action<ScenarioBookRowModel> _toggleLibraryPin;
        private readonly Dictionary<string, PreparedPage> _preparedPages = new Dictionary<string, PreparedPage>();
        private FieldManualWindowChrome _chrome;
        private UIPrimitiveFactory _ui;
        private PaperPagedList _pagedList;
        private BookPageNavigatorWidget _navigator;
        private GameObject _footerBackRoot;
        private GameObject _footerNavigatorRoot;
        private BookSearchBarWidget _searchBar;
        private GameObject _searchBarRoot;
        private GameObject _sortRoot;
        private UILabel _sortLabel;
        private GameObject _sortMenuRoot;
        private ScenarioLibrarySortMode _librarySortMode;
        private bool _evaluateSortMenuClose;
        private UIInput _draftIdInput;
        private UIInput _draftNameInput;
        private UIInput _draftDescriptionInput;
        private UILabel _statusLabel;

        public ScenarioBookBrowserRenderer(
            Action back,
            Action<int> changePage,
            Action<ScenarioLibrarySortMode> selectLibrarySort,
            Action<ScenarioBookRowModel> toggleLibraryPin)
        {
            _back = back;
            _changePage = changePage;
            _selectLibrarySort = selectLibrarySort;
            _toggleLibraryPin = toggleLibraryPin;
        }

        public FieldManualWindowChrome Chrome { get { return _chrome; } }
        public GameObject ContentRoot { get { return _pagedList != null ? _pagedList.ContentRoot : null; } }
        public GameObject Viewport { get { return _pagedList != null ? _pagedList.Viewport : null; } }
        public GameObject PageLabelRoot { get { return _navigator != null ? _navigator.PageLabelRoot : null; } }

        public void Build(GameObject root, int overlayDepth, VanillaPageTurnAssets assets)
        {
            _chrome = FieldManualWindowChrome.BuildBook(root, overlayDepth, "Custom Scenarios", "Field notes");
            _ui = _chrome.Ui;
            BuildSearchBar();
            BuildPagedList();
            BuildFooter(assets);
        }

        public string SearchFilter
        {
            get { return _searchBar != null ? (_searchBar.Filter ?? string.Empty) : string.Empty; }
        }

        public bool IsSearchFocused
        {
            get { return _searchBar != null && _searchBar.HasFocus; }
        }

        public void HandleSearchInput(Action onFilterChanged)
        {
            if (_searchBar != null)
                _searchBar.HandleInput("Titles, details, and saves...", onFilterChanged);

            HandleSortMenuOutsideClick();
        }

        public void Render(
            ScenarioBookBrowserViewKind view,
            ScenarioCatalogEntry selectedScenario,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            int pageCount,
            string headerTitle,
            string headerDetail,
            ScenarioLibrarySortMode librarySortMode,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            if (_pagedList == null)
                return;

            SetSearchVisible(true);
            SetSortVisible(view == ScenarioBookBrowserViewKind.Types, librarySortMode);
            if (_searchBar != null)
            {
                _searchBar.SetPresentation(
                    view == ScenarioBookBrowserViewKind.Types ? "SEARCH SCENARIOS:" : "SEARCH THIS LIST:",
                    view == ScenarioBookBrowserViewKind.Types ? "Title, author, or base mode..." : "Titles, details, and saves...");
            }
            if (_searchBarRoot != null)
                _searchBarRoot.transform.localPosition = new Vector3(
                    view == ScenarioBookBrowserViewKind.Saves ? 290f : SearchBarX,
                    SearchBarY,
                    0f);
            _pagedList.Clear();
            _draftIdInput = null;
            _draftNameInput = null;
            _draftDescriptionInput = null;
            SetNavigatorMode(view);
            if (view == ScenarioBookBrowserViewKind.Types)
            {
                RenderLibrary(selectedScenario, playStats, rows, pageIndex, pageCount, select);
                _pagedList.Layout(0);
                return;
            }
            if (view == ScenarioBookBrowserViewKind.Saves)
            {
                RenderScenarioDetail(selectedScenario, playStats, rows, pageIndex, pageCount, select, delete);
                _pagedList.Layout(6);
                if (_navigator != null)
                    _navigator.UpdateState(pageIndex, pageCount);
                return;
            }

            _pagedList.AddRow(BuildHeader(_pagedList.ContentRoot, headerTitle, headerDetail), HeaderHeight);

            int safePage = Math.Max(0, pageIndex);
            int start = safePage * ScenarioBookBrowserPanel.RowsPerPage;
            int count = rows != null ? rows.Count : 0;
            int end = Math.Min(count, start + ScenarioBookBrowserPanel.RowsPerPage);
            if (count == 0)
                _pagedList.AddRow(BuildEmptyRow(_pagedList.ContentRoot), RowHeight);
            else
            {
                for (int i = start; i < end; i++)
                    _pagedList.AddRow(BuildRow(_pagedList.ContentRoot, rows[i], i, select, delete), RowHeight);
            }

            _pagedList.Layout(6);
            if (_navigator != null)
                _navigator.UpdateState(pageIndex, pageCount);
        }

        public void PreparePage(
            string key,
            ScenarioBookBrowserViewKind view,
            ScenarioCatalogEntry selectedScenario,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            int pageCount,
            string headerTitle,
            string headerDetail,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            if (_pagedList == null || _pagedList.Viewport == null || string.IsNullOrEmpty(key) || _preparedPages.ContainsKey(key))
                return;

            PreparedPage page = BuildPreparedPage(
                _pagedList.Viewport,
                key,
                view,
                selectedScenario,
                playStats,
                rows,
                pageIndex,
                headerTitle,
                headerDetail,
                select,
                delete);
            if (page == null || page.Root == null)
                return;

            page.Root.SetActive(false);
            _preparedPages[key] = page;
        }

        public bool TryRenderPreparedPage(string key, int pageIndex, int pageCount)
        {
            if (_pagedList == null || string.IsNullOrEmpty(key))
                return false;

            PreparedPage page;
            if (!_preparedPages.TryGetValue(key, out page) || page == null || page.Root == null)
            {
                if (_preparedPages.ContainsKey(key))
                    _preparedPages.Remove(key);
                return false;
            }

            _preparedPages.Remove(key);
            _pagedList.Clear();
            page.Root.SetActive(true);
            _pagedList.AddRow(page.Root, page.Height);
            _pagedList.Layout(6);
            if (_navigator != null)
                _navigator.UpdateState(pageIndex, pageCount);
            return true;
        }

        public void ClearPreparedPages()
        {
            foreach (KeyValuePair<string, PreparedPage> pair in _preparedPages)
            {
                PreparedPage page = pair.Value;
                if (page != null && page.Root != null)
                    UnityEngine.Object.Destroy(page.Root);
            }

            _preparedPages.Clear();
        }

        public void RenderDraftEditor(
            ScenarioBookDraftEditorModel model,
            string headerTitle,
            string headerDetail,
            Action<ScenarioBookDraftEditorModel> save,
            Action openDraft,
            Action duplicateDraft,
            Action openExportFolder,
            Action deleteDraft)
        {
            if (_pagedList == null)
                return;

            SetSearchVisible(false);
            SetSortVisible(false, ScenarioLibrarySortMode.PinnedFirst);
            SetNavigatorMode(ScenarioBookBrowserViewKind.DraftDetails);
            _pagedList.Clear();
            _draftIdInput = null;
            _draftNameInput = null;
            _draftDescriptionInput = null;

            _pagedList.AddRow(BuildHeader(_pagedList.ContentRoot, headerTitle, headerDetail), HeaderHeight);
            _pagedList.AddRow(BuildDraftEditor(_pagedList.ContentRoot, model, save, openDraft, duplicateDraft, openExportFolder, deleteDraft), 390);
            _pagedList.Layout(6);

            if (_navigator != null)
                _navigator.UpdateState(0, 1);
        }

        public void SetStatus(string value)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = value ?? string.Empty;
                _statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }
        }

        public void Dispose()
        {
            ClearPreparedPages();
            if (_chrome != null)
            {
                _chrome.Dispose();
                _chrome = null;
            }
        }

        private void BuildPagedList()
        {
            Rect content = _chrome.Regions.ContentRectLocal;
            Rect viewport = new Rect(-content.width * 0.5f, -content.height * 0.5f, content.width, content.height - SearchReservedHeight);
            _pagedList = new PaperPagedList(viewport, _ui.NextDepth());
            _pagedList.Build(_chrome.Regions.ContentRoot);
        }

        private void BuildSearchBar()
        {
            _searchBar = new BookSearchBarWidget(_chrome.Palette, _chrome.Textures, _ui);
            _searchBarRoot = _searchBar.Build(
                _chrome.Regions.ContentRoot,
                "ScenarioSearchBar",
                new Vector3(SearchBarX, SearchBarY, 0f),
                "SEARCH THIS LIST:",
                "Titles, details, and saves...");

            _sortRoot = _chrome.Buttons.Build(
                _chrome.Regions.ContentRoot,
                "ScenarioLibrarySort",
                "Pinned first",
                // Keep both controls on the left page. At the reference scale,
                // the sort button's right edge aligns with the library cards.
                new Vector3(SortButtonX, SearchBarY, 0f),
                136,
                35,
                11,
                ToggleSortMenu);
            _sortLabel = FindLabel(_sortRoot);
            ScenarioBookLibraryHarnessPayload sortPayload = _sortRoot.AddComponent<ScenarioBookLibraryHarnessPayload>();
            sortPayload.SortMode = ScenarioLibrarySortMode.PinnedFirst.ToString();
            _sortRoot.SetActive(false);
        }

        private void SetSearchVisible(bool visible)
        {
            if (_searchBar != null)
                _searchBar.SetVisible(visible);
        }

        private void SetSortVisible(bool visible, ScenarioLibrarySortMode mode)
        {
            if (_sortRoot == null)
                return;

            CloseSortMenu();
            _librarySortMode = mode;
            _sortRoot.SetActive(visible);
            if (_sortLabel != null)
                _sortLabel.text = ScenarioLibraryOrganizer.Label(mode) + "  v";
            ScenarioBookLibraryHarnessPayload payload = _sortRoot.GetComponent<ScenarioBookLibraryHarnessPayload>();
            if (payload != null)
                payload.SortMode = mode.ToString();
            _sortRoot.name = "ScenarioLibrarySort_" + mode.ToString();
        }

        private void ToggleSortMenu()
        {
            if (_sortMenuRoot != null)
            {
                CloseSortMenu();
                return;
            }

            _sortMenuRoot = _ui.CreateChild(
                _chrome.Regions.ContentRoot,
                "ScenarioLibrarySortDropdown",
                new Vector3(SortButtonX, SearchBarY - 39f, 0f));

            ScenarioLibrarySortMode[] modes = (ScenarioLibrarySortMode[])Enum.GetValues(typeof(ScenarioLibrarySortMode));
            for (int i = 0; i < modes.Length; i++)
            {
                ScenarioLibrarySortMode option = modes[i];
                bool selected = option == _librarySortMode;
                GameObject optionRoot = _chrome.Buttons.Build(
                    _sortMenuRoot,
                    "ScenarioLibrarySortOption_" + option,
                    (selected ? "> " : "  ") + ScenarioLibraryOrganizer.Label(option),
                    new Vector3(0f, -(i * 34f), 0f),
                    184,
                    33,
                    11,
                    delegate { SelectSortMode(option); });
                ScenarioBookLibraryHarnessPayload payload = optionRoot.AddComponent<ScenarioBookLibraryHarnessPayload>();
                payload.SortMode = option.ToString();
            }
        }

        private void SelectSortMode(ScenarioLibrarySortMode mode)
        {
            CloseSortMenu();
            if (_selectLibrarySort != null)
                _selectLibrarySort(mode);
        }

        private void HandleSortMenuOutsideClick()
        {
            if (_evaluateSortMenuClose)
            {
                _evaluateSortMenuClose = false;
                if (_sortMenuRoot != null
                    && !IsHoveredWithin(_sortMenuRoot)
                    && !IsHoveredWithin(_sortRoot))
                {
                    CloseSortMenu();
                }
            }

            if (_sortMenuRoot != null && UnityEngine.Input.GetMouseButtonDown(0))
                _evaluateSortMenuClose = true;
        }

        private void CloseSortMenu()
        {
            _evaluateSortMenuClose = false;
            if (_sortMenuRoot == null)
                return;

            UnityEngine.Object.Destroy(_sortMenuRoot);
            _sortMenuRoot = null;
        }

        private static bool IsHoveredWithin(GameObject root)
        {
            if (root == null || UICamera.hoveredObject == null)
                return false;

            GameObject hovered = UICamera.hoveredObject;
            return hovered == root
                || (hovered.transform != null && hovered.transform.IsChildOf(root.transform));
        }

        private void BuildFooter(VanillaPageTurnAssets assets)
        {
            float bottomY = -400f;
            _footerBackRoot = _chrome.Buttons.Build(_chrome.Regions.FooterRoot, "ScenarioBookBack", "Back",
                new Vector3(0f, bottomY, 0f), 180, 58, 23, _back);

            _footerNavigatorRoot = _ui.CreateChild(_chrome.Regions.FooterRoot, "ScenarioBookFooterNavigator", Vector3.zero);
            _navigator = new BookPageNavigatorWidget(_chrome.Palette, _chrome.Textures, _ui, assets);
            _navigator.Build(_footerNavigatorRoot, new Vector3(0f, bottomY, 0f),
                delegate { _changePage(-1); },
                delegate { _changePage(1); });

            _statusLabel = _ui.CreateLabel(_chrome.Regions.FooterRoot, "ScenarioBookStatus", string.Empty,
                new Vector3(0f, bottomY + 43f, 0f), 14, _chrome.Palette.StampRed,
                700, 38, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            _statusLabel.multiLine = true;
            _statusLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
            _statusLabel.gameObject.SetActive(false);

        }

        private GameObject BuildHeader(GameObject parent, string title, string detail)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookHeader", Vector3.zero);
            UILabel titleLabel = _ui.CreateLabel(root, "Title", title,
                new Vector3(-520f, 23f, 0f), 25, _chrome.Palette.Ink,
                LeftPageWidth, 34, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            titleLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel detailLabel = _ui.CreateLabel(root, "Detail", detail,
                new Vector3(-520f, -9f, 0f), 16, _chrome.Palette.InkFaded,
                LeftPageWidth, 44, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detailLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
            detailLabel.multiLine = true;

            _ui.CreateQuad(root, "LeftRule", _chrome.Textures.White, new Vector3(LeftPageX, -43f, 0f),
                LeftPageWidth, 2, new Color(0.35f, 0.25f, 0.16f, 0.35f), _ui.NextDepth());
            _ui.CreateQuad(root, "RightRule", _chrome.Textures.White, new Vector3(RightPageX, -43f, 0f),
                RightPageWidth, 2, new Color(0.35f, 0.25f, 0.16f, 0.35f), _ui.NextDepth());
            return root;
        }

        private PreparedPage BuildPreparedPage(
            GameObject parent,
            string key,
            ScenarioBookBrowserViewKind view,
            ScenarioCatalogEntry selectedScenario,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            string headerTitle,
            string headerDetail,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            if (view == ScenarioBookBrowserViewKind.Saves)
            {
                GameObject spread = _ui.CreateChild(parent, "PreparedScenarioDetail_" + SanitizeObjectName(key), Vector3.zero);
                BuildScenarioDetailSpread(spread, selectedScenario, playStats, rows, pageIndex, GetSavePageCount(rows), select, delete);
                return new PreparedPage { Root = spread, Height = 470 };
            }

            if (view == ScenarioBookBrowserViewKind.Types)
            {
                GameObject library = _ui.CreateChild(parent, "PreparedScenarioLibrary_" + SanitizeObjectName(key), Vector3.zero);
                BuildLibrarySpread(library, selectedScenario, playStats, rows, pageIndex, select);
                return new PreparedPage { Root = library, Height = 470 };
            }

            GameObject root = _ui.CreateChild(parent, "PreparedScenarioPage_" + SanitizeObjectName(key), Vector3.zero);
            List<GameObject> children = new List<GameObject>();
            List<int> heights = new List<int>();
            children.Add(BuildHeader(root, headerTitle, headerDetail));
            heights.Add(HeaderHeight);

            int safePage = Math.Max(0, pageIndex);
            int start = safePage * ScenarioBookBrowserPanel.RowsPerPage;
            int count = rows != null ? rows.Count : 0;
            int end = Math.Min(count, start + ScenarioBookBrowserPanel.RowsPerPage);
            if (count == 0)
            {
                children.Add(BuildEmptyRow(root));
                heights.Add(RowHeight);
            }
            else
            {
                for (int i = start; i < end; i++)
                {
                    children.Add(BuildRow(root, rows[i], i, select, delete));
                    heights.Add(RowHeight);
                }
            }

            LayoutPreparedChildren(children, heights, 6);
            return new PreparedPage { Root = root, Height = Mathf.RoundToInt(GetViewportHeight()) };
        }

        private void LayoutPreparedChildren(List<GameObject> children, List<int> heights, int rowSpacing)
        {
            float cursor = GetViewportHeight() * 0.5f;
            int spacing = rowSpacing < 0 ? 0 : rowSpacing;
            for (int i = 0; i < children.Count; i++)
            {
                GameObject child = children[i];
                if (child == null)
                    continue;

                int height = heights[i] < 1 ? 1 : heights[i];
                cursor -= height * 0.5f;
                child.transform.localPosition = new Vector3(0f, cursor, 0f);
                cursor -= height * 0.5f + spacing;
            }
        }

        private float GetViewportHeight()
        {
            return _chrome != null ? Mathf.Max(1f, _chrome.Regions.ContentRectLocal.height) : 470f;
        }

        private int LibraryMetric(int referenceValue)
        {
            if (_chrome == null)
                return referenceValue;

            Rect content = _chrome.Regions.ContentRectLocal;
            float widthScale = content.width / ReferenceContentWidth;
            float heightScale = content.height / ReferenceContentHeight;
            float scale = Mathf.Max(0.75f, Mathf.Min(widthScale, heightScale));
            return Mathf.Max(1, Mathf.RoundToInt(referenceValue * scale));
        }

        private int LibraryFont(int referenceSize)
        {
            return LibraryMetric(referenceSize);
        }

        private void RenderLibrary(
            ScenarioCatalogEntry selectedScenario,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            int pageCount,
            Action<ScenarioBookRowModel> select)
        {
            GameObject spread = _ui.CreateChild(_pagedList.ContentRoot, "ScenarioLibrarySpread", Vector3.zero);
            BuildLibrarySpread(spread, selectedScenario, playStats, rows, pageIndex, select);
            BuildLibraryPageControls(spread, pageIndex, pageCount);
            float viewportHeight = _chrome != null
                ? Mathf.Max(1f, _chrome.Regions.ContentRectLocal.height - SearchReservedHeight)
                : ReferenceContentHeight - SearchReservedHeight;
            _pagedList.AddRow(spread, Mathf.RoundToInt(viewportHeight));
        }

        private void BuildLibrarySpread(
            GameObject spread,
            ScenarioCatalogEntry selectedScenario,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            Action<ScenarioBookRowModel> select)
        {
            List<ScenarioBookRowModel> tools = new List<ScenarioBookRowModel>();
            List<ScenarioBookRowModel> scenarios = new List<ScenarioBookRowModel>();
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                ScenarioBookRowModel row = rows[i];
                if (row == null)
                    continue;
                if (row.Kind == ScenarioBookRowKind.Type || row.Kind == ScenarioBookRowKind.OpenInstallScenarios)
                    tools.Add(row);
                else
                    scenarios.Add(row);
            }

            bool searching = !string.IsNullOrEmpty(SearchFilter);
            if (!searching)
            {
                BuildSectionLabel(spread, "LibraryToolsLabel", "TOOLS", -520f, 217f, LeftPageWidth - 18, LibraryFont(13));
                for (int i = 0; i < tools.Count; i++)
                    BuildLibraryToolRow(spread, tools[i], i, 181f - (i * LibraryMetric(54)), select);
            }

            float scenarioLabelY = searching ? 217f : 90f;
            float scenarioStartY = searching ? 172f : 45f;
            BuildSectionLabel(spread, "LibraryScenariosLabel", "SCENARIOS", -520f, scenarioLabelY, LeftPageWidth - 18, LibraryFont(13));
            int start = Math.Max(0, pageIndex) * ScenarioBookBrowserPanel.LibraryRowsPerPage;
            int end = Math.Min(scenarios.Count, start + ScenarioBookBrowserPanel.LibraryRowsPerPage);
            for (int i = start; i < end; i++)
                BuildLibraryScenarioRow(spread, scenarios[i], i, scenarioStartY - ((i - start) * LibraryMetric(LibraryScenarioRowHeight)), selectedScenario, select);

            if (scenarios.Count == 0)
            {
                BuildLibraryScenarioRow(spread, new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Empty,
                    Title = string.IsNullOrEmpty(SearchFilter) ? "No custom scenarios installed" : "No matching scenarios",
                    Detail = string.IsNullOrEmpty(SearchFilter)
                        ? "Use Install Downloads to add one to this library."
                        : "Try a title, author, or base-mode search."
                }, 0, scenarioStartY, selectedScenario, select);
            }

            if (selectedScenario == null)
                BuildLibraryWelcome(spread);
            else
                BuildLibraryDetails(spread, selectedScenario, playStats, select);
        }

        private void BuildLibraryPageControls(GameObject parent, int pageIndex, int pageCount)
        {
            if (pageCount <= 1)
                return;

            GameObject root = _ui.CreateChild(parent, "ScenarioListPager", Vector3.zero);
            if (pageIndex > 0)
            {
                _chrome.Buttons.Build(root, "PreviousScenarios", "<",
                    new Vector3(-250f, 90f, 0f), 38, 30, LibraryFont(17), delegate { _changePage(-1); });
            }
            _ui.CreateLabel(root, "PageLabel", (pageIndex + 1).ToString() + "/" + pageCount.ToString(),
                new Vector3(-205f, 90f, 0f), LibraryFont(13), _chrome.Palette.InkFaded,
                48, 22, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            if (pageIndex + 1 < pageCount)
            {
                _chrome.Buttons.Build(root, "NextScenarios", ">",
                    new Vector3(-160f, 90f, 0f), 38, 30, LibraryFont(17), delegate { _changePage(1); });
            }
        }

        private void BuildLibraryToolRow(GameObject parent, ScenarioBookRowModel row, int index, float y, Action<ScenarioBookRowModel> select)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookRow_Tool_" + index.ToString(), new Vector3(LeftPageX, y, 0f));
            Color rest = new Color(0.42f, 0.34f, 0.22f, 0.28f);
            UITexture background = _ui.CreateQuad(root, "Background", _chrome.Textures.White, Vector3.zero,
                LeftPageWidth, LibraryMetric(LibraryToolRowHeight) - LibraryMetric(4), rest, _ui.NextDepth());
            _ui.CreateQuad(root, "Edge", _chrome.Textures.White, new Vector3(-232f, 0f, 0f),
                LibraryMetric(5), LibraryMetric(LibraryToolRowHeight) - LibraryMetric(4), _chrome.Palette.StampRed, _ui.NextDepth());
            UILabel title = _ui.CreateLabel(root, "Title", row.Title,
                new Vector3(-214f, 9f, 0f), LibraryFont(19), _chrome.Palette.Ink,
                280, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.overflowMethod = UILabel.Overflow.ShrinkContent;
            UILabel badge = _ui.CreateLabel(root, "Badge", row.Badge,
                new Vector3(210f, 9f, 0f), LibraryFont(15), _chrome.Palette.StampRed,
                90, 24, NGUIText.Alignment.Right, UIWidget.Pivot.Right, _ui.NextDepth());
            UILabel detail = _ui.CreateLabel(root, "Detail", row.Detail,
                new Vector3(-214f, -14f, 0f), LibraryFont(13), _chrome.Palette.InkFaded,
                400, 20, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;
            if (select != null)
                _ui.AddClickCollider(root, LeftPageWidth, LibraryMetric(LibraryToolRowHeight) - LibraryMetric(4), delegate { select(row); });
            AttachCompactHover(root, background, title, detail, badge, rest, row.IsLocked);
        }

        private void BuildLibraryScenarioRow(
            GameObject parent,
            ScenarioBookRowModel row,
            int index,
            float y,
            ScenarioCatalogEntry selectedScenario,
            Action<ScenarioBookRowModel> select)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookRow_Library_" + index.ToString(), new Vector3(LeftPageX, y, 0f));
            ScenarioBookLibraryHarnessPayload rowPayload = root.AddComponent<ScenarioBookLibraryHarnessPayload>();
            rowPayload.ScenarioId = row != null && row.Scenario != null ? row.Scenario.ScenarioId : string.Empty;
            rowPayload.SortMode = row != null ? row.LibrarySortMode.ToString() : ScenarioLibrarySortMode.PinnedFirst.ToString();
            rowPayload.Pinned = row != null && row.IsPinned;
            bool selected = row != null && row.Scenario != null && ReferenceEquals(row.Scenario, selectedScenario);
            Color rest = selected ? new Color(0.38f, 0.29f, 0.18f, 0.34f) : BookSelectionRowStyle.Background(row != null && row.IsLocked);
            UITexture background = _ui.CreateQuad(root, "Background", _chrome.Textures.White, Vector3.zero,
                LeftPageWidth, LibraryMetric(LibraryScenarioRowHeight) - LibraryMetric(3), rest, _ui.NextDepth());
            _ui.CreateQuad(root, "Edge", _chrome.Textures.White, new Vector3(-232f, 0f, 0f),
                selected ? LibraryMetric(7) : LibraryMetric(4), LibraryMetric(LibraryScenarioRowHeight) - LibraryMetric(3),
                selected ? _chrome.Palette.StampRed : _chrome.Palette.OliveBand, _ui.NextDepth());
            UILabel title = _ui.CreateLabel(root, "Title", row != null ? row.Title : string.Empty,
                new Vector3(-214f, 9f, 0f), LibraryFont(18), BookSelectionRowStyle.TitleColor(_chrome.Palette, row != null && row.IsLocked),
                300, 23, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.overflowMethod = UILabel.Overflow.ShrinkContent;
            UILabel detail = _ui.CreateLabel(root, "Detail", row != null ? row.Detail : string.Empty,
                new Vector3(-214f, -13f, 0f), LibraryFont(12), _chrome.Palette.InkFaded,
                380, 19, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;
            UILabel badge = _ui.CreateLabel(root, "Badge", row != null ? row.Badge : string.Empty,
                new Vector3(168f, 9f, 0f), LibraryFont(11), _chrome.Palette.InkFaded,
                62, 20, NGUIText.Alignment.Right, UIWidget.Pivot.Right, _ui.NextDepth());
            if (row != null && row.Kind == ScenarioBookRowKind.Scenario && row.Scenario != null)
            {
                GameObject pin = _chrome.Buttons.Build(
                    root,
                    "Pin_" + (row.IsPinned ? "true" : "false"),
                    row.IsPinned ? "^" : "·",
                    new Vector3(216f, 0f, 0f),
                    LibraryMetric(31),
                    LibraryMetric(31),
                    LibraryFont(15),
                    delegate { if (_toggleLibraryPin != null) _toggleLibraryPin(row); });
                UILabel pinLabel = FindLabel(pin);
                if (pinLabel != null)
                {
                    pinLabel.text = row.IsPinned ? "\u2605" : "\u2606";
                    pinLabel.color = row.IsPinned
                        ? new Color(0.83f, 0.62f, 0.16f, 1f)
                        : _chrome.Palette.InkFaded;
                }
            }
            if (select != null && row != null && row.Kind != ScenarioBookRowKind.Empty)
                _ui.AddClickCollider(root, LeftPageWidth, LibraryMetric(LibraryScenarioRowHeight) - LibraryMetric(3), delegate { select(row); });
            if (row != null && row.Kind != ScenarioBookRowKind.Empty)
                AttachCompactHover(root, background, title, detail, badge, rest, row.IsLocked);
        }

        private void AttachCompactHover(GameObject root, UITexture background, UILabel title, UILabel detail, UILabel badge, Color rest, bool locked)
        {
            HoverVisualState hover = root.AddComponent<HoverVisualState>();
            hover.Widgets = new UIWidget[] { background, title, detail, badge };
            hover.RestColors = new Color[] { rest, title.color, detail.color, badge.color };
            hover.HoverColors = new Color[]
            {
                BookSelectionRowStyle.HoverBackground(locked),
                title.color,
                _chrome.Palette.Ink,
                badge.color
            };
            hover.ScaleTarget = root.transform;
            hover.RestScale = 1f;
            hover.HoverScale = 1.01f;
        }

        private static UILabel FindLabel(GameObject root)
        {
            return root != null ? root.GetComponentInChildren<UILabel>(true) : null;
        }

        private void BuildLibraryWelcome(GameObject parent)
        {
            BuildSectionLabel(parent, "LibraryWelcomeLabel", "CUSTOM SCENARIOS", 82f, 217f, RightPageWidth, LibraryFont(13));
            UILabel title = _ui.CreateLabel(parent, "LibraryWelcomeTitle", "Stories beyond the shelter",
                new Vector3(82f, 172f, 0f), LibraryFont(32), _chrome.Palette.Ink,
                RightPageWidth, 44, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.overflowMethod = UILabel.Overflow.ShrinkContent;
            UILabel blurb = _ui.CreateLabel(parent, "LibraryWelcomeBlurb",
                "Pick a scenario on the left to read its field notes and begin.\n\nDrafts are your workshop for stories still being built.\n\nDrop downloaded scenarios in Install Downloads.",
                new Vector3(82f, 117f, 0f), LibraryFont(20), _chrome.Palette.InkFaded,
                RightPageWidth, 280, NGUIText.Alignment.Left, UIWidget.Pivot.TopLeft, _ui.NextDepth());
            blurb.multiLine = true;
            blurb.overflowMethod = UILabel.Overflow.ShrinkContent;
        }

        private void BuildLibraryDetails(
            GameObject parent,
            ScenarioCatalogEntry scenario,
            ScenarioBookPlayStatsModel playStats,
            Action<ScenarioBookRowModel> select)
        {
            BuildSectionLabel(parent, "LibraryDetailLabel", "SCENARIO", 82f, 217f, RightPageWidth, LibraryFont(13));
            string titleText = Safe(scenario.DisplayName, scenario.ScenarioId);
            GameObject titleRoot = _ui.CreateChild(parent, "ScenarioBookRow_Detail_Title", Vector3.zero);
            UILabel title = _ui.CreateLabel(titleRoot, "Title", titleText,
                new Vector3(82f, 172f, 0f), LibraryFont(32), _chrome.Palette.Ink,
                RightPageWidth, 42, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel description = _ui.CreateLabel(titleRoot, "Detail",
                Safe(scenario.Description, "No description was left with this scenario."),
                new Vector3(82f, 127f, 0f), LibraryFont(17), _chrome.Palette.InkFaded,
                RightPageWidth, 100, NGUIText.Alignment.Left, UIWidget.Pivot.TopLeft, _ui.NextDepth());
            description.multiLine = true;
            description.overflowMethod = UILabel.Overflow.ShrinkContent;

            float y = 12f;
            int factLineHeight = Math.Max(23, Math.Max(LibraryFont(12), LibraryFont(16)));
            int factLineSpacing = factLineHeight + LibraryMetric(8);
            BuildLibraryDetailFact(parent, "Author", scenario.Source == ScenarioCatalogSource.Vanilla ? "Source" : "Author",
                scenario.Source == ScenarioCatalogSource.Vanilla ? "Vanilla" : Safe(scenario.Author, Safe(scenario.OwnerModId, "Unknown")), y); y -= factLineSpacing;
            BuildLibraryDetailFact(parent, "Version", "Version", Safe(scenario.Version, "Unknown"), y); y -= factLineSpacing;
            BuildLibraryDetailFact(parent, "BaseMode", "Base mode", scenario.BaseGameMode.ToString(), y); y -= factLineSpacing;
            int saveCount = playStats != null ? playStats.SaveCount : scenario.SaveCount;
            BuildLibraryDetailFact(parent, "Saves", "Saves", saveCount.ToString() + (saveCount == 1 ? " run" : " runs"), y);

            int playHeight = 64;
            int savesHeight = 58;
            float actionGap = LibraryMetric(10);
            float playY = y - (factLineHeight * 0.5f) - actionGap - (playHeight * 0.5f);
            float savesY = playY - (playHeight * 0.5f) - LibraryMetric(8) - (savesHeight * 0.5f);

            ScenarioBookRowModel play = new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.StartScenario,
                Scenario = scenario,
                Title = scenario.CanStart ? "PLAY SCENARIO" : "SCENARIO LOCKED",
                Detail = scenario.CanStart
                    ? (scenario.Source == ScenarioCatalogSource.Vanilla
                        ? "Begin a new run in the expanded save archive."
                        : "Begin a new run with this scenario.")
                    : "Required content is missing or mismatched.",
                Badge = scenario.CanStart ? "SELECT" : "LOCKED",
                IsLocked = !scenario.CanStart
            };
            BuildLibraryAction(parent, "Play", play, playY, true, select);

            ScenarioBookRowModel saves = new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.OpenScenarioSaves,
                Scenario = scenario,
                Title = "OPEN SAVES",
                Detail = saveCount == 0
                    ? "No saved runs yet. Start one above."
                    : (scenario.Source == ScenarioCatalogSource.Vanilla
                        ? "View or continue this mode's expanded save archive."
                        : "View, continue, or remove this scenario's saved runs."),
                Badge = saveCount.ToString()
            };
            BuildLibraryAction(parent, "Saves", saves, savesY, false, select);
        }

        private void BuildLibraryDetailFact(GameObject parent, string key, string label, string value, float y)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookRow_Detail_" + key, Vector3.zero);
            _ui.CreateLabel(root, "Title", label.ToUpperInvariant(),
                new Vector3(82f, y, 0f), LibraryFont(12), _chrome.Palette.InkFaded,
                105, 23, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            UILabel detail = _ui.CreateLabel(root, "Detail", value,
                new Vector3(195f, y, 0f), LibraryFont(16), _chrome.Palette.Ink,
                315, 23, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;
        }

        private void BuildLibraryAction(
            GameObject parent,
            string key,
            ScenarioBookRowModel row,
            float y,
            bool prominent,
            Action<ScenarioBookRowModel> select)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookRow_Detail_" + key, new Vector3(RightPageX, y, 0f));
            Color rest = prominent ? StartCard : BookSelectionRowStyle.Background(row.IsLocked);
            int actionHeight = prominent ? 64 : 58;
            UITexture background = _ui.CreateQuad(root, "Background", _chrome.Textures.White, Vector3.zero,
                RightPageWidth, actionHeight, rest, _ui.NextDepth());
            UITexture edge = _ui.CreateQuad(root, "Edge", _chrome.Textures.White, new Vector3(-212f, 0f, 0f),
                prominent ? 7 : 4, actionHeight, _chrome.Palette.StampRed, _ui.NextDepth());
            UILabel title = _ui.CreateLabel(root, "Title", row.Title,
                new Vector3(-194f, 11f, 0f), prominent ? LibraryFont(20) : LibraryFont(18),
                prominent ? _chrome.Palette.KeycapInk : _chrome.Palette.Ink,
                250, 22, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            UILabel detail = _ui.CreateLabel(root, "Detail", row.Detail,
                new Vector3(-194f, -15f, 0f), prominent ? LibraryFont(13) : LibraryFont(12),
                prominent ? new Color(0.88f, 0.81f, 0.69f, 0.88f) : _chrome.Palette.InkFaded,
                305, 21, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;
            UILabel badge = _ui.CreateLabel(root, "Badge", row.Badge,
                new Vector3(195f, 0f, 0f), LibraryFont(14),
                prominent ? _chrome.Palette.KeycapInk : _chrome.Palette.StampRed,
                92, 24, NGUIText.Alignment.Right, UIWidget.Pivot.Right, _ui.NextDepth());
            if (select != null && !row.IsLocked)
                _ui.AddClickCollider(root, RightPageWidth, actionHeight, delegate { select(row); });
            if (!row.IsLocked)
                AttachLibraryActionHover(root, background, edge, title, detail, badge, rest, prominent);
        }

        private void AttachLibraryActionHover(
            GameObject root,
            UITexture background,
            UITexture edge,
            UILabel title,
            UILabel detail,
            UILabel badge,
            Color rest,
            bool prominent)
        {
            Color hoverBackground = prominent ? StartCardHover : BookSelectionRowStyle.HoverBackground(false);
            Color hoverTitle = prominent ? _chrome.Palette.KeycapInk : _chrome.Palette.Ink;
            Color hoverDetail = prominent ? Color.white : _chrome.Palette.Ink;
            Color hoverBadge = prominent ? _chrome.Palette.KeycapInk : _chrome.Palette.StampRed;
            HoverVisualState hover = root.AddComponent<HoverVisualState>();
            hover.Widgets = new UIWidget[] { background, edge, title, detail, badge };
            hover.RestColors = new Color[] { rest, _chrome.Palette.StampRed, title.color, detail.color, badge.color };
            hover.HoverColors = new Color[] { hoverBackground, _chrome.Palette.Brass, hoverTitle, hoverDetail, hoverBadge };
            hover.ScaleTarget = root.transform;
            hover.RestScale = 1f;
            hover.HoverScale = 1.01f;
        }

        private void RenderScenarioDetail(
            ScenarioCatalogEntry scenario,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            int pageCount,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            GameObject spread = _ui.CreateChild(_pagedList.ContentRoot, "ScenarioDetailSpread", Vector3.zero);
            BuildScenarioDetailSpread(spread, scenario, playStats, rows, pageIndex, pageCount, select, delete);
            _pagedList.AddRow(spread, 470);
        }

        private void BuildScenarioDetailSpread(
            GameObject spread,
            ScenarioCatalogEntry scenario,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            int pageCount,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            BuildScenarioInfoPage(spread, scenario, playStats);
            BuildSaveListPage(spread, playStats, rows, pageIndex, pageCount, select, delete);
        }

        private void BuildScenarioInfoPage(GameObject parent, ScenarioCatalogEntry scenario, ScenarioBookPlayStatsModel playStats)
        {
            string title = scenario != null ? Safe(scenario.DisplayName, scenario.ScenarioId) : "Scenario";
            string description = scenario != null ? Safe(scenario.Description, "No description was provided for this scenario.") : string.Empty;

            BuildSectionLabel(parent, "ScenarioDossierLabel", "SCENARIO DOSSIER", -520f, 218f, LeftPageWidth - 18);

            UILabel titleLabel = _ui.CreateLabel(parent, "ScenarioTitle", title,
                new Vector3(-520f, 184f, 0f), 28, _chrome.Palette.Ink,
                LeftPageWidth - 18, 38, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            titleLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel descriptionLabel = _ui.CreateLabel(parent, "ScenarioDescription", description,
                new Vector3(-520f, 122f, 0f), 15, _chrome.Palette.InkFaded,
                LeftPageWidth - 18, 76, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            descriptionLabel.multiLine = true;
            descriptionLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            BuildSectionLabel(parent, "ScenarioFieldNotesLabel", "FIELD NOTES", -520f, 70f, LeftPageWidth - 18);

            float y = StatStartY - 2f;
            BuildStat(parent, "Scenario ID", scenario != null ? Safe(scenario.ScenarioId, "unknown") : "unknown", y);
            y -= StatLineSpacing;
            BuildStat(parent, "Author/Source", BuildSourceLabel(scenario), y);
            y -= StatLineSpacing;
            BuildStat(parent, "Version", scenario != null ? Safe(scenario.Version, "unknown") : "unknown", y);
            y -= StatLineSpacing;
            BuildStat(parent, "Base Mode", scenario != null ? scenario.BaseGameMode.ToString() : "Unknown", y);
            y -= StatLineSpacing;
            BuildStat(parent, "Dependencies", BuildDependencyLabel(scenario), y);
            y -= StatLineSpacing;
            BuildStat(parent, "Saves", BuildSaveStatsLabel(scenario, playStats), y);
            y -= StatLineSpacing;
            BuildStat(parent, "Best Day", playStats != null && playStats.BestDaySurvived > 0 ? playStats.BestDaySurvived.ToString() : "No saved days", y);
            y -= StatLineSpacing;
            BuildStat(parent, "Outcomes", BuildOutcomeStatsLabel(playStats), y);
            y -= StatLineSpacing;

            for (int i = 0; playStats != null && playStats.ScoreLines != null && i < playStats.ScoreLines.Count; i++)
            {
                ScenarioBookStatLine line = playStats.ScoreLines[i];
                if (line == null || string.IsNullOrEmpty(line.Label) || string.IsNullOrEmpty(line.Value))
                    continue;

                BuildStat(parent, line.Label, line.Value, y);
                y -= StatLineSpacing;
            }
        }

        private void BuildStat(GameObject parent, string label, string value, float y)
        {
            string safeLabel = SanitizeObjectName(label);
            _ui.CreateLabel(parent, "Stat_" + safeLabel, label.ToUpperInvariant(),
                new Vector3(-520f, y, 0f), 11, _chrome.Palette.InkFaded,
                126, 22, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            UILabel valueLabel = _ui.CreateLabel(parent, "StatValue_" + safeLabel, value,
                new Vector3(-382f, y, 0f), 13, _chrome.Palette.Ink,
                312, 22, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            valueLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            _ui.CreateQuad(parent, "StatRule_" + safeLabel, _chrome.Textures.White,
                new Vector3(LeftPageX, y - 13f, 0f), LeftPageWidth - 18, 1, PaperRule, _ui.NextDepth());
        }

        private void SetNavigatorMode(ScenarioBookBrowserViewKind view)
        {
            bool library = view == ScenarioBookBrowserViewKind.Types;
            if (_footerNavigatorRoot != null)
                _footerNavigatorRoot.SetActive(!library);
            if (_footerBackRoot != null)
                _footerBackRoot.transform.localPosition = new Vector3(library ? 0f : -460f, -400f, 0f);
        }

        private static string BuildSourceLabel(ScenarioCatalogEntry scenario)
        {
            if (scenario == null)
                return "Unknown";
            if (scenario.Source == ScenarioCatalogSource.Vanilla)
                return "Vanilla";
            if (scenario.Source == ScenarioCatalogSource.Draft)
                return "Local draft";
            return Safe(scenario.OwnerModId, "Local mod");
        }

        private static string BuildDependencyLabel(ScenarioCatalogEntry scenario)
        {
            if (scenario == null)
                return "Unknown";
            if (scenario.CanStart)
                return "Ready";
            return "Locked: " + scenario.DependencyState.ToString();
        }

        private static string BuildSaveStatsLabel(ScenarioCatalogEntry scenario, ScenarioBookPlayStatsModel stats)
        {
            int count = stats != null ? stats.SaveCount : (scenario != null ? scenario.SaveCount : 0);
            if (stats == null || !stats.HasBindingData)
                return count.ToString() + " total";

            return count.ToString() + " total, "
                + stats.ActiveSaveCount.ToString() + " active, "
                + stats.ConvertedSaveCount.ToString() + " converted";
        }

        private static string BuildOutcomeStatsLabel(ScenarioBookPlayStatsModel stats)
        {
            if (stats == null || !stats.HasOutcomeData)
                return "No completed runs yet";

            return stats.CompletedSaveCount.ToString() + " completed, "
                + stats.WinCount.ToString() + " win, "
                + stats.LossCount.ToString() + " loss";
        }

        private void BuildSaveListPage(
            GameObject parent,
            ScenarioBookPlayStatsModel playStats,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            int pageCount,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            BuildSectionLabel(parent, "SaveArchiveLabel", "SAVE ARCHIVE", 82f, 194f, RightPageWidth);

            UILabel heading = _ui.CreateLabel(parent, "SaveListHeading", "Runs",
                new Vector3(82f, 160f, 0f), 28, _chrome.Palette.Ink,
                RightPageWidth, 38, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            heading.overflowMethod = UILabel.Overflow.ShrinkContent;

            int saveCount = playStats != null ? playStats.SaveCount : 0;
            string pageText = saveCount.ToString() + (saveCount == 1 ? " SAVE" : " SAVES")
                + "  |  " + Math.Max(1, pageIndex + 1).ToString() + "/" + Math.Max(1, pageCount).ToString();
            _ui.CreateLabel(parent, "SaveListCounter", pageText,
                new Vector3(512f, 162f, 0f), 12, _chrome.Palette.InkFaded,
                190, 24, NGUIText.Alignment.Right, UIWidget.Pivot.Right, _ui.NextDepth());

            bool hasScoreSummary = playStats != null && !string.IsNullOrEmpty(playStats.ScoreSummary);
            if (hasScoreSummary)
            {
                UILabel scoreLabel = _ui.CreateLabel(parent, "SaveListScoreNote", playStats.ScoreSummary,
                    new Vector3(82f, 126f, 0f), 12, _chrome.Palette.StampRed,
                    RightPageWidth, 22, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
                scoreLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
            }

            int count = rows != null ? rows.Count : 0;
            int start = Math.Max(0, pageIndex) * ScenarioBookBrowserPanel.SaveRowsPerPage;
            int end = Math.Min(count, start + ScenarioBookBrowserPanel.SaveRowsPerPage);
            ScenarioBookRowModel startRow = null;
            List<ScenarioBookRowModel> saveRows = new List<ScenarioBookRowModel>();
            for (int i = start; i < end; i++)
            {
                ScenarioBookRowModel row = rows[i];
                if (row != null && row.Kind == ScenarioBookRowKind.StartScenario)
                    startRow = row;
                else if (row != null)
                    saveRows.Add(row);
            }

            float topY = hasScoreSummary ? 92f : 108f;
            if (startRow != null)
            {
                BuildStartRunCard(parent, startRow, topY, select);
                topY -= 78f;
            }

            BuildSectionLabel(parent, "SavedRunsLabel", "SAVED RUNS", 82f, topY, RightPageWidth);
            float firstRowY = topY - 35f;

            if (count == 0 || start >= count)
            {
                ScenarioBookRowModel empty = new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Empty,
                    Title = "No matching saves",
                    Detail = "No saves match the current search.",
                    Badge = string.Empty
                };
                BuildSaveListRow(parent, empty, 0, firstRowY, select, delete);
                return;
            }

            if (saveRows.Count == 0)
            {
                UILabel emptyLabel = _ui.CreateLabel(parent, "NoSavedRuns", "No saved runs yet. Start one above.",
                    new Vector3(82f, firstRowY, 0f), 14, _chrome.Palette.InkFaded,
                    RightPageWidth, 30, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
                emptyLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
                return;
            }

            for (int i = 0; i < saveRows.Count; i++)
                BuildSaveListRow(parent, saveRows[i], start + i, firstRowY - (i * SaveListRowHeight), select, delete);
        }

        private void BuildSaveListRow(
            GameObject parent,
            ScenarioBookRowModel row,
            int index,
            float y,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            if (row == null)
                return;

            GameObject root = _ui.CreateChild(parent, "SaveListRow_" + index.ToString(), new Vector3(RightPageX, y, 0f));
            Color background = BookSelectionRowStyle.Background(row.IsLocked);
            Color hoverBackground = BookSelectionRowStyle.HoverBackground(row.IsLocked);
            UITexture bg = _ui.CreateQuad(root, "Background", _chrome.Textures.White, Vector3.zero,
                RightPageWidth, SaveCardHeight, background, _ui.NextDepth());

            _ui.CreateQuad(root, "Edge", _chrome.Textures.White, new Vector3(-212f, 0f, 0f),
                5, SaveCardHeight, row.IsLocked ? _chrome.Palette.StampRed : _chrome.Palette.OliveBand, _ui.NextDepth());

            UILabel title = _ui.CreateLabel(root, "Title", row.Title,
                new Vector3(-198f, HasSaveListDetail(row) ? 11f : 0f, 0f), 15, BookSelectionRowStyle.TitleColor(_chrome.Palette, row.IsLocked),
                215, 22, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel detail = null;
            string detailText = BuildSaveListDetail(row);
            if (!string.IsNullOrEmpty(detailText))
            {
                detail = _ui.CreateLabel(root, "Detail", detailText,
                    new Vector3(-198f, -13f, 0f), 11, _chrome.Palette.InkFaded,
                    215, 20, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
                detail.overflowMethod = UILabel.Overflow.ShrinkContent;
            }

            _ui.CreateLabel(root, "DayCaption", "DAY",
                new Vector3(40f, 12f, 0f), 9, _chrome.Palette.InkFaded,
                36, 16, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            UILabel day = _ui.CreateLabel(root, "Day", BuildDayLabel(row),
                new Vector3(40f, -8f, 0f), 15, _chrome.Palette.Ink,
                36, 22, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            day.overflowMethod = UILabel.Overflow.ShrinkContent;

            _ui.CreateLabel(root, "StateCaption", "STATE",
                new Vector3(92f, 12f, 0f), 9, _chrome.Palette.InkFaded,
                62, 16, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            UILabel state = _ui.CreateLabel(root, "State", BuildStateColumnLabel(row),
                new Vector3(92f, -8f, 0f), 12, _chrome.Palette.Ink,
                62, 22, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            state.overflowMethod = UILabel.Overflow.ShrinkContent;

            _ui.CreateLabel(root, "ResultCaption", "RESULT",
                new Vector3(151f, 12f, 0f), 9, _chrome.Palette.InkFaded,
                55, 16, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            UILabel result = _ui.CreateLabel(root, "Result", BuildResultColumnLabel(row),
                new Vector3(151f, -8f, 0f), 11, _chrome.Palette.Ink,
                55, 22, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            result.overflowMethod = UILabel.Overflow.ShrinkContent;

            if (select != null && row.Kind != ScenarioBookRowKind.Empty)
                _ui.AddClickCollider(root, RightPageWidth, SaveCardHeight, delegate { select(row); });

            AttachSaveListHover(root, bg, title, detail, day, state, result, background, hoverBackground, row);

            if (row.CanDelete && delete != null)
            {
                _chrome.Buttons.Build(root, "Delete", "Del",
                    new Vector3(194f, 20f, 0f), 38, 22, 10, delegate { delete(row); });
            }
        }

        private void BuildStartRunCard(
            GameObject parent,
            ScenarioBookRowModel row,
            float y,
            Action<ScenarioBookRowModel> select)
        {
            GameObject root = _ui.CreateChild(parent, "StartNewRun", new Vector3(RightPageX, y, 0f));
            UITexture background = _ui.CreateQuad(root, "Background", _chrome.Textures.White, Vector3.zero,
                RightPageWidth, 62, StartCard, _ui.NextDepth());
            _ui.CreateQuad(root, "Accent", _chrome.Textures.White, new Vector3(-211f, 0f, 0f),
                7, 62, _chrome.Palette.StampRed, _ui.NextDepth());

            UILabel title = _ui.CreateLabel(root, "Title", "START NEW RUN",
                new Vector3(-192f, 11f, 0f), 17, _chrome.Palette.KeycapInk,
                250, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            UILabel detail = _ui.CreateLabel(root, "Detail", Safe(row.Detail, "Create a fresh scenario save."),
                new Vector3(-192f, -13f, 0f), 11, new Color(0.88f, 0.81f, 0.69f, 0.88f),
                285, 20, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;
            UILabel action = _ui.CreateLabel(root, "Action", "BEGIN  >",
                new Vector3(190f, 0f, 0f), 14, _chrome.Palette.KeycapInk,
                105, 28, NGUIText.Alignment.Right, UIWidget.Pivot.Right, _ui.NextDepth());

            if (select != null)
                _ui.AddClickCollider(root, RightPageWidth, 62, delegate { select(row); });

            HoverVisualState hover = root.AddComponent<HoverVisualState>();
            hover.Widgets = new UIWidget[] { background, title, detail, action };
            hover.RestColors = new Color[] { StartCard, _chrome.Palette.KeycapInk, detail.color, _chrome.Palette.KeycapInk };
            hover.HoverColors = new Color[] { StartCardHover, Color.white, Color.white, Color.white };
            hover.ScaleTarget = root.transform;
            hover.RestScale = 1f;
            hover.HoverScale = 1.015f;
        }

        private static int GetSavePageCount(IList<ScenarioBookRowModel> rows)
        {
            int count = Math.Max(1, rows != null ? rows.Count : 0);
            return Math.Max(1, (count + ScenarioBookBrowserPanel.SaveRowsPerPage - 1) / ScenarioBookBrowserPanel.SaveRowsPerPage);
        }

        private static bool HasSaveListDetail(ScenarioBookRowModel row)
        {
            return !string.IsNullOrEmpty(BuildSaveListDetail(row));
        }

        private void BuildSectionLabel(GameObject parent, string name, string text, float x, float y, int width)
        {
            BuildSectionLabel(parent, name, text, x, y, width, 11);
        }

        private void BuildSectionLabel(GameObject parent, string name, string text, float x, float y, int width, int fontSize)
        {
            _ui.CreateLabel(parent, name, text,
                new Vector3(x, y, 0f), fontSize, _chrome.Palette.StampRed,
                width, 18, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _ui.CreateQuad(parent, name + "Rule", _chrome.Textures.White,
                new Vector3(x + (width * 0.5f), y - 14f, 0f), width, 1, PaperRule, _ui.NextDepth());
        }

        private static string BuildSaveListDetail(ScenarioBookRowModel row)
        {
            if (row == null)
                return string.Empty;
            if (row.Kind == ScenarioBookRowKind.StartScenario)
                return string.Empty;
            if (row.Kind != ScenarioBookRowKind.LoadSave || row.SaveDetail == null)
                return Safe(row.Detail, string.Empty);

            string formatted = ScenarioBookBrowserDataSource.FormatDisplayTime(row.SaveDetail.SaveTime);
            if (string.IsNullOrEmpty(formatted))
                formatted = "No save time";
            if (row.SaveDetail.HasBinding && row.SaveDetail.DayCreated > 0)
                return "Created D" + row.SaveDetail.DayCreated.ToString() + " - " + formatted;
            return formatted;
        }

        private static string BuildDayLabel(ScenarioBookRowModel row)
        {
            if (row == null)
                return "-";
            if (row.Kind == ScenarioBookRowKind.StartScenario)
                return "New";
            return row.SaveDetail != null ? row.SaveDetail.DaysSurvived.ToString() : "-";
        }

        private static string BuildStateColumnLabel(ScenarioBookRowModel row)
        {
            if (row == null)
                return string.Empty;
            if (row.Kind == ScenarioBookRowKind.StartScenario)
                return "Ready";
            return ScenarioBookBrowserDataSource.BuildStatusLabel(row.SaveDetail);
        }

        private static string BuildResultColumnLabel(ScenarioBookRowModel row)
        {
            if (row == null)
                return string.Empty;
            if (row.Kind == ScenarioBookRowKind.StartScenario)
                return "Start";
            if (row.SaveDetail == null || string.IsNullOrEmpty(row.SaveDetail.ScenarioOutcome))
                return "Not done";
            return row.SaveDetail.ScenarioOutcome;
        }

        private void AttachSaveListHover(
            GameObject root,
            UITexture bg,
            UILabel title,
            UILabel detail,
            UILabel day,
            UILabel state,
            UILabel result,
            Color background,
            Color hoverBackground,
            ScenarioBookRowModel row)
        {
            if (row == null || row.Kind == ScenarioBookRowKind.Empty)
                return;

            HoverVisualState hover = root.AddComponent<HoverVisualState>();
            hover.Widgets = new UIWidget[] { bg, title, detail, day, state, result };
            hover.RestColors = new Color[]
            {
                background,
                BookSelectionRowStyle.TitleColor(_chrome.Palette, row.IsLocked),
                _chrome.Palette.InkFaded,
                _chrome.Palette.Ink,
                _chrome.Palette.Ink,
                _chrome.Palette.Ink
            };
            hover.HoverColors = new Color[]
            {
                hoverBackground,
                BookSelectionRowStyle.TitleColor(_chrome.Palette, row.IsLocked),
                _chrome.Palette.Ink,
                _chrome.Palette.Ink,
                _chrome.Palette.Ink,
                _chrome.Palette.Ink
            };
            hover.ScaleTarget = root.transform;
            hover.RestScale = 1f;
            hover.HoverScale = 1.01f;
        }

        private GameObject BuildDraftEditor(
            GameObject parent,
            ScenarioBookDraftEditorModel model,
            Action<ScenarioBookDraftEditorModel> save,
            Action openDraft,
            Action duplicateDraft,
            Action openExportFolder,
            Action deleteDraft)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookDraftEditor", Vector3.zero);
            _ui.CreateLabel(root, "NameLabel", "Scenario Name",
                new Vector3(-520f, 164f, 0f), 19, _chrome.Palette.Ink,
                LeftPageWidth, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftNameInput = CreateTextInput(root, "ScenarioNameInput",
                new Vector3(LeftPageX, 124f, 0f), DraftInputWidth, 44,
                model != null ? model.DisplayName : string.Empty, false, 21, _chrome.Palette.Ink);

            _ui.CreateLabel(root, "DescriptionLabel", "Description",
                new Vector3(-520f, 88f, 0f), 19, _chrome.Palette.Ink,
                LeftPageWidth, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftDescriptionInput = CreateTextInput(root, "ScenarioDescriptionInput",
                new Vector3(LeftPageX, -17f, 0f), DraftInputWidth, 150,
                model != null ? model.Description : string.Empty, true, 17, _chrome.Palette.Ink);

            _ui.CreateLabel(root, "IdLabel", "FILE DETAILS  -  AUTHORING FILE ID",
                new Vector3(-520f, -112f, 0f), 11, _chrome.Palette.InkFaded,
                LeftPageWidth, 18, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftIdInput = CreateTextInput(root, "DraftIdInput",
                new Vector3(LeftPageX, -146f, 0f), DraftInputWidth, 30,
                model != null ? model.DraftId : string.Empty, false, 14, _chrome.Palette.InkFaded);

            BuildDraftFacts(root, model != null ? model.Facts : null);

            _chrome.Buttons.Build(root, "SaveDraftDetails", "Save Details",
                new Vector3(420f, 30f, 0f), 160, 44, 17, delegate
                {
                    if (save != null)
                        save(ReadDraftEditorModel(model));
                });
            _chrome.Buttons.Build(root, "OpenDraft", "Open Draft",
                new Vector3(420f, -28f, 0f), 160, 44, 17, delegate
                {
                    if (openDraft != null)
                        openDraft();
                });
            _chrome.Buttons.Build(root, "DuplicateDraft", "Duplicate Draft",
                new Vector3(420f, -86f, 0f), 160, 44, 17, delegate
                {
                    if (duplicateDraft != null)
                        duplicateDraft();
                });
            _chrome.Buttons.Build(root, "DeleteDraft", "Delete Draft",
                new Vector3(420f, -144f, 0f), 160, 44, 17, delegate
                {
                    if (deleteDraft != null)
                        deleteDraft();
                });

            if (model != null && model.Facts != null && model.Facts.HasExport)
            {
                _chrome.Buttons.Build(root, "OpenExportFolder", "Open Export",
                    new Vector3(420f, -202f, 0f), 160, 44, 17, delegate
                    {
                        if (openExportFolder != null)
                            openExportFolder();
                    });
            }

            return root;
        }

        private void BuildDraftFacts(GameObject root, ScenarioBookDraftFactsModel facts)
        {
            _ui.CreateLabel(root, "DraftFactsHeading", "Draft status",
                new Vector3(96f, 168f, 0f), 17, _chrome.Palette.Ink,
                245, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());

            float y = 140f;
            BuildDraftFactLine(root, "BaseMode", "Base mode: " + FactValue(facts != null ? facts.BaseModeLabel : null, "Standard"), y); y -= 22f;
            BuildDraftFactLine(root, "Edited", "Last edited: " + FactValue(facts != null ? facts.LastEditedText : null, "unknown"), y); y -= 22f;
            BuildDraftFactLine(root, "Validation", "Validation: " + FactValue(facts != null ? facts.ValidationSummary : null, "Not checked"), y); y -= 22f;
            BuildDraftFactLine(root, "Export", "Last export: " + (facts != null && facts.HasExport ? FactValue(facts.LastExportText, "exported") : "none yet"), y); y -= 22f;
            BuildDraftFactLine(root, "Recovery", "Recovery data: " + BuildRecoveryValue(facts), y); y -= 22f;
            if (facts != null && facts.HasExport)
            {
                BuildDraftFactLine(root, "ExportPath", "Export folder: " + FactValue(facts.LastExportRoot, "unknown"), y); y -= 22f;
                BuildDraftFactLine(root, "Share", "Send this folder to another player; they drop it in "
                    + ScenarioPackageImportService.StagingFolderName + " and click Install.", y);
            }
        }

        private void BuildDraftFactLine(GameObject root, string key, string text, float y)
        {
            UILabel line = _ui.CreateLabel(root, "DraftFact_" + key, text,
                new Vector3(96f, y, 0f), 15, _chrome.Palette.InkFaded,
                245, 22, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            line.overflowMethod = UILabel.Overflow.ShrinkContent;
        }

        private static string BuildRecoveryValue(ScenarioBookDraftFactsModel facts)
        {
            if (facts == null)
                return "none";
            if (facts.HasRecoveryData)
                return "unsaved autosave present";
            return facts.HasHistory ? "history saved" : "none";
        }

        private static string FactValue(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private UIInput CreateTextInput(
            GameObject parent,
            string name,
            Vector3 localPosition,
            int width,
            int height,
            string value,
            bool multiLine,
            int fontSize,
            Color textColor)
        {
            GameObject root = _ui.CreateChild(parent, name, localPosition);
            _ui.CreateQuad(root, "InputBoundary", _chrome.Textures.White, Vector3.zero,
                width + 4, height + 4, new Color(0.35f, 0.25f, 0.16f, 0.58f), _ui.NextDepth());
            _ui.CreateQuad(root, "InputPaper", _chrome.Textures.White, Vector3.zero,
                width, height, new Color(0.96f, 0.89f, 0.70f, 0.82f), _ui.NextDepth());
            _ui.AddClickCollider(root, width, height, null);

            UILabel label = _ui.CreateLabel(root, "Text", value,
                multiLine ? new Vector3(-width * 0.5f + 16f, height * 0.5f - 16f, 0f) : new Vector3(-width * 0.5f + 16f, 0f, 0f),
                fontSize, textColor,
                width - 32, height - 16,
                NGUIText.Alignment.Left,
                multiLine ? UIWidget.Pivot.TopLeft : UIWidget.Pivot.Left,
                _ui.NextDepth());
            label.multiLine = multiLine;
            label.overflowMethod = UILabel.Overflow.ClampContent;

            UIInput input = root.AddComponent<UIInput>();
            input.label = label;
            input.activeTextColor = textColor;
            input.caretColor = _chrome.Palette.Ink;
            input.selectionColor = new Color(0.35f, 0.25f, 0.16f, 0.35f);
            input.value = value ?? string.Empty;
            return input;
        }

        private ScenarioBookDraftEditorModel ReadDraftEditorModel(ScenarioBookDraftEditorModel original)
        {
            return new ScenarioBookDraftEditorModel
            {
                Scenario = original != null ? original.Scenario : null,
                DraftId = _draftIdInput != null ? _draftIdInput.value : string.Empty,
                DisplayName = _draftNameInput != null ? _draftNameInput.value : string.Empty,
                Description = _draftDescriptionInput != null ? _draftDescriptionInput.value : string.Empty
            };
        }

        private GameObject BuildEmptyRow(GameObject parent)
        {
            return BuildRow(parent, new ScenarioBookRowModel
            {
                Kind = ScenarioBookRowKind.Empty,
                Title = "No entries",
                Detail = "There are no scenarios in this view yet.",
                Badge = string.Empty
            }, -1, null, null);
        }

        private GameObject BuildRow(
            GameObject parent,
            ScenarioBookRowModel row,
            int index,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookRow_" + index, Vector3.zero);
            UITexture leftBg;
            UITexture rightBg;
            BookSelectionRowStyle.BuildSplitPageBackground(root, _ui, _chrome.Textures,
                LeftPageX, RightPageX, LeftPageWidth, RightPageWidth, RowPanelHeight, row.IsLocked,
                out leftBg, out rightBg);
            if (select != null && row.Kind != ScenarioBookRowKind.Empty)
                _ui.AddClickCollider(root, RowHitWidth, RowPanelHeight, delegate { select(row); });

            bool hasSection = !string.IsNullOrEmpty(row.SectionLabel);
            if (hasSection)
            {
                UILabel section = _ui.CreateLabel(root, "SectionLabel", row.SectionLabel,
                    new Vector3(-520f, 27f, 0f), 10, _chrome.Palette.StampRed,
                    LeftPageWidth - 24, 16, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
                section.overflowMethod = UILabel.Overflow.ShrinkContent;
            }

            UILabel title = _ui.CreateLabel(root, "Title", row.Title,
                new Vector3(-520f, hasSection ? 8f : 14f, 0f), hasSection ? 18 : 21, BookSelectionRowStyle.TitleColor(_chrome.Palette, row.IsLocked),
                LeftPageWidth - 24, 28, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel detail = _ui.CreateLabel(root, "Detail", row.Detail,
                new Vector3(-520f, hasSection ? -18f : -15f, 0f), hasSection ? 13 : 15, _chrome.Palette.InkFaded,
                LeftPageWidth - 24, 38, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;
            detail.multiLine = true;

            UILabel badge = _ui.CreateLabel(root, "Badge", row.Badge,
                new Vector3(300f, 8f, 0f), 20, _chrome.Palette.Ink,
                RightPageWidth - 36, 30, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            badge.overflowMethod = UILabel.Overflow.ShrinkContent;

            if (row.CanDelete && delete != null)
            {
                _chrome.Buttons.Build(root, "Delete", "Delete",
                    new Vector3(300f, -18f, 0f), 116, 34, 15, delegate { delete(row); });
            }

            if (row.Kind != ScenarioBookRowKind.Empty)
                AttachHover(root, leftBg, rightBg, title, detail, badge, row);

            return root;
        }

        private void AttachHover(
            GameObject root,
            UITexture leftBg,
            UITexture rightBg,
            UILabel title,
            UILabel detail,
            UILabel badge,
            ScenarioBookRowModel row)
        {
            BookSelectionRowStyle.AttachSplitPageHover(root, leftBg, rightBg,
                new UIWidget[] { title, detail, badge },
                new Color[]
                {
                    BookSelectionRowStyle.TitleColor(_chrome.Palette, row.IsLocked),
                    _chrome.Palette.InkFaded,
                    _chrome.Palette.Ink
                },
                new Color[]
                {
                    BookSelectionRowStyle.TitleColor(_chrome.Palette, row.IsLocked),
                    _chrome.Palette.Ink,
                    _chrome.Palette.Ink
                },
                row.IsLocked,
                1.01f);
        }

        private static string Safe(string value, string fallback)
        {
            return string.IsNullOrEmpty(value) ? (fallback ?? string.Empty) : value;
        }

        private static string SanitizeObjectName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Page";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }
    }
}
