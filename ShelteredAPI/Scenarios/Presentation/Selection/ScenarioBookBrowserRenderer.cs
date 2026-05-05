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
        private const int DraftInputWidth = 430;
        private const float SearchBarY = 222f;
        private const float SearchReservedHeight = 54f;

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
        private UILabel _statusLabel;
        private UIInput _draftNameInput;
        private UIInput _draftDescriptionInput;

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

            _pagedList.Clear();
            _draftNameInput = null;
            _draftDescriptionInput = null;
            if (view == ScenarioBookBrowserViewKind.Saves)
            {
                RenderScenarioDetail(selectedScenario, rows, pageIndex, select, delete);
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
            Action openDraft)
        {
            if (_pagedList == null)
                return;

            _pagedList.Clear();
            _draftNameInput = null;
            _draftDescriptionInput = null;

            _pagedList.AddRow(BuildHeader(_pagedList.ContentRoot, headerTitle, headerDetail), HeaderHeight);
            _pagedList.AddRow(BuildDraftEditor(_pagedList.ContentRoot, model, save, openDraft), 390);
            _pagedList.Layout(6);

            if (_navigator != null)
                _navigator.UpdateState(0, 1);
        }

        public void SetStatus(string value)
        {
            if (_statusLabel != null)
                _statusLabel.text = value ?? string.Empty;
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
            _searchBar.Build(_chrome.Regions.ContentRoot, "ScenarioSearchBar", new Vector3(-300f, SearchBarY, 0f), "Search scenarios...");
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
                new Vector3(0f, bottomY + 62f, 0f), 18, _chrome.Palette.Ink,
                700, 32, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            _statusLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
            _statusLabel.effectStyle = UILabel.Effect.Outline;
            _statusLabel.effectColor = new Color(0.86f, 0.78f, 0.56f, 0.55f);
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
                BuildScenarioDetailSpread(spread, selectedScenario, rows, pageIndex, select, delete);
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
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            GameObject spread = _ui.CreateChild(_pagedList.ContentRoot, "ScenarioDetailSpread", Vector3.zero);
            BuildScenarioDetailSpread(spread, scenario, rows, pageIndex, select, delete);
            _pagedList.AddRow(spread, 470);
        }

        private void BuildScenarioDetailSpread(
            GameObject spread,
            ScenarioCatalogEntry scenario,
            IList<ScenarioBookRowModel> rows,
            int pageIndex,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            BuildScenarioInfoPage(spread, scenario);

            ScenarioBookRowModel slot = null;
            int count = rows != null ? rows.Count : 0;
            if (count > 0)
                slot = rows[Mathf.Clamp(pageIndex, 0, count - 1)];

            if (slot == null)
            {
                slot = new ScenarioBookRowModel
                {
                    Kind = ScenarioBookRowKind.Empty,
                    Title = "No save slots",
                    Detail = "This scenario does not have any available saves.",
                    Badge = string.Empty
                };
            }

            BuildSaveSlotPage(spread, slot, pageIndex, count, select, delete);
        }

        private void BuildScenarioInfoPage(GameObject parent, ScenarioCatalogEntry scenario)
        {
            string title = scenario != null ? Safe(scenario.DisplayName, scenario.ScenarioId) : "Scenario";
            string description = scenario != null ? Safe(scenario.Description, "No description was provided for this scenario.") : string.Empty;

            UILabel titleLabel = _ui.CreateLabel(parent, "ScenarioTitle", title,
                new Vector3(-520f, 202f, 0f), 26, _chrome.Palette.Ink,
                LeftPageWidth - 18, 38, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            titleLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel descriptionLabel = _ui.CreateLabel(parent, "ScenarioDescription", description,
                new Vector3(-520f, 128f, 0f), 17, _chrome.Palette.Ink,
                LeftPageWidth - 18, 120, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            descriptionLabel.multiLine = true;
            descriptionLabel.overflowMethod = UILabel.Overflow.ShrinkContent;

            _ui.CreateQuad(parent, "ScenarioInfoRule", _chrome.Textures.White, new Vector3(LeftPageX, 43f, 0f),
                LeftPageWidth, 2, new Color(0.35f, 0.25f, 0.16f, 0.35f), _ui.NextDepth());

            BuildStat(parent, "Mode", scenario != null ? scenario.BaseGameMode.ToString() : "Unknown", 8f);
            BuildStat(parent, "Author", scenario != null ? Safe(scenario.OwnerModId, "local") : "Unknown", -32f);
            BuildStat(parent, "Version", scenario != null ? Safe(scenario.Version, "unknown") : "unknown", -72f);
            BuildStat(parent, "Saves", scenario != null ? scenario.SaveCount.ToString() : "0", -112f);
            BuildStat(parent, "State", scenario != null && scenario.CanStart ? "Ready" : "Locked", -152f);
        }

        private void BuildStat(GameObject parent, string label, string value, float y)
        {
            _ui.CreateLabel(parent, "Stat_" + label, label,
                new Vector3(-520f, y, 0f), 16, _chrome.Palette.InkFaded,
                120, 26, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            UILabel valueLabel = _ui.CreateLabel(parent, "StatValue_" + label, value,
                new Vector3(-390f, y, 0f), 16, _chrome.Palette.Ink,
                330, 26, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            valueLabel.overflowMethod = UILabel.Overflow.ShrinkContent;
        }

        private void BuildSaveSlotPage(
            GameObject parent,
            ScenarioBookRowModel row,
            int pageIndex,
            int pageCount,
            Action<ScenarioBookRowModel> select,
            Action<ScenarioBookRowModel> delete)
        {
            UILabel heading = _ui.CreateLabel(parent, "SaveSlotHeading", "Save Slots",
                new Vector3(82f, 202f, 0f), 26, _chrome.Palette.Ink,
                RightPageWidth, 38, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            heading.overflowMethod = UILabel.Overflow.ShrinkContent;

            string pageText = Math.Max(1, pageIndex + 1) + "/" + Math.Max(1, pageCount);
            _ui.CreateLabel(parent, "SaveSlotCounter", pageText,
                new Vector3(512f, 204f, 0f), 16, _chrome.Palette.InkFaded,
                80, 24, NGUIText.Alignment.Right, UIWidget.Pivot.Right, _ui.NextDepth());

            GameObject card = _ui.CreateChild(parent, "SaveSlotCard", new Vector3(RightPageX, 40f, 0f));
            Color background = row.IsLocked
                ? new Color(0.72f, 0.50f, 0.46f, 0.44f)
                : new Color(0.92f, 0.84f, 0.66f, 0.36f);
            Color hoverBackground = row.IsLocked
                ? new Color(0.78f, 0.53f, 0.48f, 0.62f)
                : new Color(1f, 0.91f, 0.68f, 0.56f);

            UITexture bg = _ui.CreateQuad(card, "Background", _chrome.Textures.White, Vector3.zero,
                RightPageWidth, 245, background, _ui.NextDepth());

            UILabel title = _ui.CreateLabel(card, "Title", row.Title,
                new Vector3(-190f, 80f, 0f), 24, row.IsLocked ? _chrome.Palette.StampRed : _chrome.Palette.Ink,
                RightPageWidth - 42, 72, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            title.multiLine = true;
            title.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel detail = _ui.CreateLabel(card, "Detail", row.Detail,
                new Vector3(-190f, -8f, 0f), 17, _chrome.Palette.InkFaded,
                RightPageWidth - 42, 86, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            detail.multiLine = true;
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;

            UILabel badge = _ui.CreateLabel(card, "Badge", row.Badge,
                new Vector3(0f, -92f, 0f), 18, _chrome.Palette.Ink,
                180, 30, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            badge.overflowMethod = UILabel.Overflow.ShrinkContent;

            if (select != null && row.Kind != ScenarioBookRowKind.Empty)
                _ui.AddClickCollider(card, RightPageWidth, 245, delegate { select(row); });

            AttachSlotHover(card, bg, title, detail, badge, background, hoverBackground, row);

            if (row.CanDelete && delete != null)
            {
                _chrome.Buttons.Build(parent, "DeleteSave", "Delete",
                    new Vector3(RightPageX, -116f, 0f), 140, 40, 16, delegate { delete(row); });
            }
        }

        private void AttachSlotHover(
            GameObject root,
            UITexture bg,
            UILabel title,
            UILabel detail,
            UILabel badge,
            Color background,
            Color hoverBackground,
            ScenarioBookRowModel row)
        {
            if (row == null || row.Kind == ScenarioBookRowKind.Empty)
                return;

            HoverVisualState hover = root.AddComponent<HoverVisualState>();
            hover.Widgets = new UIWidget[] { bg, title, detail, badge };
            hover.RestColors = new Color[]
            {
                background,
                row.IsLocked ? _chrome.Palette.StampRed : _chrome.Palette.Ink,
                _chrome.Palette.InkFaded,
                _chrome.Palette.Ink
            };
            hover.HoverColors = new Color[]
            {
                hoverBackground,
                row.IsLocked ? _chrome.Palette.StampRed : _chrome.Palette.Ink,
                _chrome.Palette.Ink,
                _chrome.Palette.Ink
            };
            hover.ScaleTarget = root.transform;
            hover.RestScale = 1f;
            hover.HoverScale = 1.015f;
        }

        private GameObject BuildDraftEditor(
            GameObject parent,
            ScenarioBookDraftEditorModel model,
            Action<ScenarioBookDraftEditorModel> save,
            Action openDraft)
        {
            GameObject root = _ui.CreateChild(parent, "ScenarioBookDraftEditor", Vector3.zero);
            _ui.CreateLabel(root, "NameLabel", "Scenario Name",
                new Vector3(-520f, 164f, 0f), 19, _chrome.Palette.Ink,
                LeftPageWidth, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftNameInput = CreateTextInput(root, "ScenarioNameInput",
                new Vector3(LeftPageX, 124f, 0f), DraftInputWidth, 54,
                model != null ? model.DisplayName : string.Empty, false);

            _ui.CreateLabel(root, "DescriptionLabel", "Description",
                new Vector3(-520f, 68f, 0f), 19, _chrome.Palette.Ink,
                LeftPageWidth, 24, NGUIText.Alignment.Left, UIWidget.Pivot.Left, _ui.NextDepth());
            _draftDescriptionInput = CreateTextInput(root, "ScenarioDescriptionInput",
                new Vector3(LeftPageX, -48f, 0f), DraftInputWidth, 200,
                model != null ? model.Description : string.Empty, true);

            UILabel detail = _ui.CreateLabel(root, "DraftDetail", "Local draft metadata is written back to scenario.xml.",
                new Vector3(300f, 118f, 0f), 16, _chrome.Palette.InkFaded,
                RightPageWidth - 28, 70, NGUIText.Alignment.Center, UIWidget.Pivot.Center, _ui.NextDepth());
            detail.multiLine = true;
            detail.overflowMethod = UILabel.Overflow.ShrinkContent;

            _chrome.Buttons.Build(root, "SaveDraftDetails", "Save Details",
                new Vector3(300f, 30f, 0f), 178, 44, 17, delegate
                {
                    if (save != null)
                        save(ReadDraftEditorModel(model));
                });
            _chrome.Buttons.Build(root, "OpenDraft", "Open Draft",
                new Vector3(300f, -28f, 0f), 178, 44, 17, delegate
                {
                    if (openDraft != null)
                        openDraft();
                });

            return root;
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
            Color background = row.IsLocked
                ? new Color(0.72f, 0.50f, 0.46f, 0.44f)
                : new Color(0.92f, 0.84f, 0.66f, 0.32f);
            Color hoverBackground = row.IsLocked
                ? new Color(0.78f, 0.53f, 0.48f, 0.62f)
                : new Color(1f, 0.91f, 0.68f, 0.54f);
            UITexture leftBg = _ui.CreateQuad(root, "LeftPageBackground", _chrome.Textures.White, new Vector3(LeftPageX, 0f, 0f),
                LeftPageWidth, RowPanelHeight, background, _ui.NextDepth());
            UITexture rightBg = _ui.CreateQuad(root, "RightPageBackground", _chrome.Textures.White, new Vector3(RightPageX, 0f, 0f),
                RightPageWidth, RowPanelHeight, background, _ui.NextDepth());
            if (select != null && row.Kind != ScenarioBookRowKind.Empty)
                _ui.AddClickCollider(root, RowHitWidth, RowPanelHeight, delegate { select(row); });

            UILabel title = _ui.CreateLabel(root, "Title", row.Title,
                new Vector3(-520f, 14f, 0f), 21, row.IsLocked ? _chrome.Palette.StampRed : _chrome.Palette.Ink,
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
                AttachHover(root, leftBg, rightBg, title, detail, badge, background, hoverBackground, row);

            return root;
        }

        private void AttachHover(
            GameObject root,
            UITexture leftBg,
            UITexture rightBg,
            UILabel title,
            UILabel detail,
            UILabel badge,
            Color background,
            Color hoverBackground,
            ScenarioBookRowModel row)
        {
            HoverVisualState hover = root.AddComponent<HoverVisualState>();
            hover.Widgets = new UIWidget[] { leftBg, rightBg, title, detail, badge };
            hover.RestColors = new Color[]
            {
                background,
                background,
                row.IsLocked ? _chrome.Palette.StampRed : _chrome.Palette.Ink,
                _chrome.Palette.InkFaded,
                _chrome.Palette.Ink
            };
            hover.HoverColors = new Color[]
            {
                hoverBackground,
                hoverBackground,
                row.IsLocked ? _chrome.Palette.StampRed : _chrome.Palette.Ink,
                _chrome.Palette.Ink,
                _chrome.Palette.Ink
            };
            hover.ScaleTarget = root.transform;
            hover.RestScale = 1f;
            hover.HoverScale = 1.01f;
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
