using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Layout
{
    /// <summary>
    /// Fixed page viewport for field-manual panels. The caller supplies already-built
    /// row GameObjects for the current page; this class owns clipping, parenting, and
    /// vertical placement.
    /// </summary>
    internal sealed class PaperPagedList
    {
        private readonly Rect _viewportLocal;
        private readonly int _baseDepth;
        private readonly List<GameObject> _rows = new List<GameObject>();
        private readonly List<int> _rowHeights = new List<int>();
        private GameObject _viewport;
        private UIPanel _clipPanel;
        private GameObject _contentRoot;

        public GameObject Viewport { get { return _viewport; } }
        public GameObject ContentRoot { get { return _contentRoot; } }

        public PaperPagedList(Rect viewportLocalCenteredCoords, int baseDepth)
        {
            _viewportLocal = viewportLocalCenteredCoords;
            _baseDepth = baseDepth;
        }

        public void Build(GameObject parent)
        {
            _viewport = new GameObject("PageViewport");
            _viewport.transform.SetParent(parent.transform, false);
            _viewport.layer = parent.layer;
            _viewport.transform.localPosition = new Vector3(
                _viewportLocal.x + _viewportLocal.width * 0.5f,
                _viewportLocal.y + _viewportLocal.height * 0.5f, 0);

            _clipPanel = _viewport.AddComponent<UIPanel>();
            _clipPanel.depth = ResolvePanelDepth(_baseDepth, _clipPanel);
            _clipPanel.clipping = UIDrawCall.Clipping.SoftClip;
            _clipPanel.baseClipRegion = new Vector4(0, 0, _viewportLocal.width, _viewportLocal.height);
            _clipPanel.clipSoftness = new Vector2(8, 8);

            _contentRoot = new GameObject("PageContent");
            _contentRoot.transform.SetParent(_viewport.transform, false);
            _contentRoot.layer = _viewport.layer;
            _contentRoot.transform.localPosition = Vector3.zero;
        }

        private static int ResolvePanelDepth(int requestedDepth, UIPanel owner)
        {
            int depth = requestedDepth;
            UIPanel[] panels = Object.FindObjectsOfType<UIPanel>();
            if (panels == null)
                return depth;

            for (int i = 0; i < panels.Length; i++)
            {
                UIPanel panel = panels[i];
                if (panel == null || panel == owner)
                    continue;

                if (panel.depth >= depth)
                    depth = panel.depth + 1;
            }

            return depth;
        }

        public void AddRow(GameObject row, int height)
        {
            if (row == null || _contentRoot == null)
                return;

            row.transform.SetParent(_contentRoot.transform, false);
            _rows.Add(row);
            _rowHeights.Add(height < 1 ? 1 : height);
        }

        public void Clear()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    Object.Destroy(_rows[i]);
            }

            _rows.Clear();
            _rowHeights.Clear();
        }

        public void Layout(int rowSpacing)
        {
            if (_contentRoot == null)
                return;

            int spacing = rowSpacing < 0 ? 0 : rowSpacing;
            float topY = _viewportLocal.height * 0.5f;
            float cursor = topY;

            for (int i = 0; i < _rows.Count; i++)
            {
                GameObject row = _rows[i];
                if (row == null)
                    continue;

                int height = _rowHeights[i];
                cursor -= height * 0.5f;
                row.transform.localPosition = new Vector3(0, cursor, 0);
                cursor -= height * 0.5f + spacing;
            }
        }
    }
}
