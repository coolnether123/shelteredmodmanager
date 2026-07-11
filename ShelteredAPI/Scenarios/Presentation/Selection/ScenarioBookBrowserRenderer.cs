using System;
using System.Collections.Generic;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Layout;
using ShelteredAPI.UI.FieldManual.Panels;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Widgets;
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
        private const float SearchBarX = -310f;
        private const float SearchBarY = 232f;
        private const float SearchReservedHeight = 68f;
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
        private readonly Action _close;
        private readonly Action<int> _changePage;
        private readonly Dictionary<string, PreparedPage> _preparedPages = new Dictionary<string, PreparedPage>();
        private FieldManualWindowChrome _chrome;
        private UIPrimitiveFactory _ui;
        private PaperPagedList _pagedList;
        private BookPageNavigatorWidget _navigator;
        private BookSearchBarWidget _searchBar;
        private UIInput _draftIdInput;
        private UIInput _draftNameInput;
        private UIInput _draftDescriptionInput;
        private UILabel _statusLabel;

        public ScenarioBookBrowserRenderer(Action back, Action close, Action<int> changePage)
        {
            _back = back;
            _close = close;
            _changePage = changePage;
        }

        public FieldManualWindowChrome Chrome { get { return _chrome; } }
        public GameObject ContentRoot { get { return _pagedList != null ? _pagedList.ContentRoot : null; } }
        public GameObject Viewport { get { return _pagedList != null ? _pagedList.Viewport : null; } }
        public GameObject PageLabelRoot { get { return _navigator != null ? _navigator.PageLabelRoot : null; } }

        public void Build(GameObject root, int overlayDepth, VanillaPageTurnAssets assets)
        {
            _chrome = FieldManualWindowChrome.BuildBook(root, overlayDepth, "Custom Scenarios", "Types, scenarios, and saves");
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
                _searchBar.HandleInput("Search scenarios...", onFilterChanged);
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
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            if (_pagedList == null)
                return;

            SetSearchVisible(view != ScenarioBookBrowserViewKind.Saves);
            _pagedList.Clear();
            _draftIdInput = null;
            _draftNameInput = null;
            _draftDescriptionInput = null;
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
            Action openExportFolder,
            Action deleteDraft)
        {
            if (_pagedList == null)
                return;

            SetSearchVisible(false);
            _pagedList.Clear();
            _draftIdInput = null;
            _draftNameInput = null;
            _draftDescriptionInput = null;

            _pagedList.AddRow(BuildHeader(_pagedList.ContentRoot, headerTitle, headerDetail), HeaderHeight);
            _pagedList.AddRow(BuildDraftEditor(_pagedList.ContentRoot, model, save, openDraft, openExportFolder, deleteDraft), 390);
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
            _searchBar.Build(_chrome.Regions.ContentRoot, "ScenarioSearchBar", new Vector3(SearchBarX, SearchBarY, 0f), "Search scenarios...");
        }

        private void SetSearchVisible(bool visible)
        {
            if (_searchBar != null)
                _searchBar.SetVisible(visible);
        }

        private void BuildFooter(VanillaPageTurnAssets assets)
        {
            float bottomY = -400f;
            _chrome.Buttons.Build(_chrome.Regions.FooterRoot, "ScenarioBookBack", "Back",
                new Vector3(-460f, bottomY, 0f), 180, 58, 23, _back);
            _chrome.Buttons.Build(_chrome.Regions.FooterRoot, "ScenarioBookClose", "Close",
                new Vector3(450f, bottomY, 0f), 180, 58, 23, _close);

            _navigator = new BookPageNavigatorWidget(_chrome.Palette, _chrome.Textures, _ui, assets);
            _navigator.Build(_chrome.Regions.FooterRoot, new Vector3(0f, bottomY, 0f),
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
            BuildSectionLabel(parent, "SaveArchiveLabel", "SAVE ARCHIVE", 82f, 218f, RightPageWidth);

            UILabel heading = _ui.CreateLabel(parent, "SaveListHeading", "Runs",
                new Vector3(82f, 184f, 0f), 28, _chrome.Palette.Ink,
                RightPageWidth, 38, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            heading.overflowMethod = UILabel.Overflow.ShrinkContent;

            int saveCount = playStats != null ? playStats.SaveCount : 0;
            string pageText = saveCount.ToString() + (saveCount == 1 ? " SAVE" : " SAVES")
                + "  |  " + Math.Max(1, pageIndex + 1).ToString() + "/" + Math.Max(1, pageCount).ToString();
            _ui.CreateLabel(parent, "SaveListCounter", pageText,
                new Vector3(512f, 186f, 0f), 12, _chrome.Palette.InkFaded,
                190, 24, NGUIText.Alignment.Right, UIWidget.Pivot.Right, _ui.NextDepth());

            bool hasScoreSummary = playStats != null && !string.IsNullOrEmpty(playStats.ScoreSummary);
            if (hasScoreSummary)
            {
                UILabel scoreLabel = _ui.CreateLabel(parent, "SaveListScoreNote", playStats.ScoreSummary,
                    new Vector3(82f, 150f, 0f), 12, _chrome.Palette.StampRed,
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

            float topY = hasScoreSummary ? 116f : 132f;
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
            _ui.CreateLabel(parent, name, text,
                new Vector3(x, y, 0f), 11, _chrome.Palette.StampRed,
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
            Action openExportFolder,
            Action deleteDraft)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookDraftEditor", Vector3.zero);
            _ui.CreateLabel(root, "IdLabel", "Draft File Name",
                new Vector3(-520f, 164f, 0f), 19, _chrome.Palette.Ink,
                LeftPageWidth, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftIdInput = CreateTextInput(root, "DraftIdInput",
                new Vector3(LeftPageX, 124f, 0f), DraftInputWidth, 44,
                model != null ? model.DraftId : string.Empty, false);

            _ui.CreateLabel(root, "NameLabel", "Scenario Name",
                new Vector3(-520f, 90f, 0f), 19, _chrome.Palette.Ink,
                LeftPageWidth, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftNameInput = CreateTextInput(root, "ScenarioNameInput",
                new Vector3(LeftPageX, 50f, 0f), DraftInputWidth, 44,
                model != null ? model.DisplayName : string.Empty, false);

            _ui.CreateLabel(root, "DescriptionLabel", "Description",
                new Vector3(-520f, -8f, 0f), 19, _chrome.Palette.Ink,
                LeftPageWidth, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftDescriptionInput = CreateTextInput(root, "ScenarioDescriptionInput",
                new Vector3(LeftPageX, -126f, 0f), DraftInputWidth, 190,
                model != null ? model.Description : string.Empty, true);

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
            _chrome.Buttons.Build(root, "DeleteDraft", "Delete Draft",
                new Vector3(420f, -86f, 0f), 160, 44, 17, delegate
                {
                    if (deleteDraft != null)
                        deleteDraft();
                });

            if (model != null && model.Facts != null && model.Facts.HasExport)
            {
                _chrome.Buttons.Build(root, "OpenExportFolder", "Open Export",
                    new Vector3(420f, -144f, 0f), 160, 44, 17, delegate
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

        private UIInput CreateTextInput(GameObject parent, string name, Vector3 localPosition, int width, int height, string value, bool multiLine)
        {
            GameObject root = _ui.CreateChild(parent, name, localPosition);
            _ui.CreateQuad(root, "InputPaper", _chrome.Textures.White, Vector3.zero,
                width, height, new Color(0.96f, 0.89f, 0.70f, 0.56f), _ui.NextDepth());
            _ui.AddClickCollider(root, width, height, null);

            UILabel label = _ui.CreateLabel(root, "Text", value,
                multiLine ? new Vector3(-width * 0.5f + 16f, height * 0.5f - 16f, 0f) : new Vector3(-width * 0.5f + 16f, 0f, 0f),
                multiLine ? 17 : 21, _chrome.Palette.Ink,
                width - 32, height - 16,
                NGUIText.Alignment.Left,
                multiLine ? UIWidget.Pivot.TopLeft : UIWidget.Pivot.Left,
                _ui.NextDepth());
            label.multiLine = multiLine;
            label.overflowMethod = UILabel.Overflow.ClampContent;

            UIInput input = root.AddComponent<UIInput>();
            input.label = label;
            input.activeTextColor = _chrome.Palette.Ink;
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

            UILabel title = _ui.CreateLabel(root, "Title", row.Title,
                new Vector3(-520f, 14f, 0f), 21, BookSelectionRowStyle.TitleColor(_chrome.Palette, row.IsLocked),
                LeftPageWidth - 24, 28, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel detail = _ui.CreateLabel(root, "Detail", row.Detail,
                new Vector3(-520f, -15f, 0f), 15, _chrome.Palette.InkFaded,
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
