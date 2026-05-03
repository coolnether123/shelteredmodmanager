using System;
using System.Collections.Generic;
using ShelteredAPI.UI.FieldManual.Animations;
using ShelteredAPI.UI.FieldManual.Layout;
using ShelteredAPI.UI.FieldManual.Panels;
using ShelteredAPI.UI.FieldManual.Primitives;
using ShelteredAPI.UI.FieldManual.Widgets;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
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

        private readonly Action _back;
        private readonly Action _close;
        private readonly Action<int> _changePage;
        private FieldManualWindowChrome _chrome;
        private UIPrimitiveFactory _ui;
        private PaperPagedList _pagedList;
        private BookPageNavigatorWidget _navigator;
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
            BuildPagedList();
            BuildFooter(assets);
        }

        public void Render(
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

        public void SetStatus(string value)
        {
            if (_statusLabel != null)
                _statusLabel.text = value ?? string.Empty;
        }

        public void Dispose()
        {
            if (_chrome != null)
            {
                _chrome.Dispose();
                _chrome = null;
            }
        }

        private void BuildPagedList()
        {
            Rect content = _chrome.Regions.ContentRectLocal;
            Rect viewport = new Rect(-content.width * 0.5f, -content.height * 0.5f, content.width, content.height);
            _pagedList = new PaperPagedList(viewport, _ui.NextDepth());
            _pagedList.Build(_chrome.Regions.ContentRoot);
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
    }
}
