using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Layout
{
    /// <summary>
    /// Vertical scrollable list rendered as a clipped NGUI panel. Children are stacked
    /// top-to-bottom with configurable per-row heights and spacing.
    ///
    /// Responsibilities:
    ///   - Build the clipping <see cref="UIPanel"/> + <see cref="UIScrollView"/>
    ///   - Provide a <see cref="ContentRoot"/> the caller can parent children under
    ///   - Lay out children when <see cref="Layout"/> is called
    ///
    /// Does NOT know about widget content, themes, or row purpose.
    /// </summary>
    internal sealed class PaperScrollList
    {
        private readonly Rect _viewportLocal;
        private readonly int _baseDepth;
        private GameObject _viewport;
        private UIPanel _clipPanel;
        private UIScrollView _scroll;
        private GameObject _contentRoot;
        private readonly List<RowEntry> _rows = new List<RowEntry>();

        public GameObject Viewport { get { return _viewport; } }
        public GameObject ContentRoot { get { return _contentRoot; } }
        public UIScrollView Scroll { get { return _scroll; } }

        public PaperScrollList(Rect viewportLocalCenteredCoords, int baseDepth)
        {
            _viewportLocal = viewportLocalCenteredCoords;
            _baseDepth = baseDepth;
        }

        public void Build(GameObject parent)
        {
            _viewport = new GameObject("ScrollViewport");
            _viewport.transform.SetParent(parent.transform, false);
            _viewport.layer = parent.layer;
            _viewport.transform.localPosition = new Vector3(
                _viewportLocal.x + _viewportLocal.width * 0.5f,
                _viewportLocal.y + _viewportLocal.height * 0.5f, 0);

            _clipPanel = _viewport.AddComponent<UIPanel>();
            _clipPanel.depth = _baseDepth;
            _clipPanel.clipping = UIDrawCall.Clipping.SoftClip;
            _clipPanel.baseClipRegion = new Vector4(0, 0, _viewportLocal.width, _viewportLocal.height);
            _clipPanel.clipSoftness = new Vector2(8, 8);

            _scroll = _viewport.AddComponent<UIScrollView>();
            _scroll.movement = UIScrollView.Movement.Vertical;
            _scroll.scrollWheelFactor = 0.4f;
            _scroll.dragEffect = UIScrollView.DragEffect.MomentumAndSpring;

            _contentRoot = new GameObject("ScrollContent");
            _contentRoot.transform.SetParent(_viewport.transform, false);
            _contentRoot.layer = _viewport.layer;
            _contentRoot.transform.localPosition = Vector3.zero;
        }

        public void AddRow(GameObject row, int height)
        {
            if (row == null) return;
            row.transform.SetParent(_contentRoot.transform, false);
            EnsureScrollDrag(row);
            _rows.Add(new RowEntry(row, height));
        }

        public void Clear()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Go != null) Object.Destroy(_rows[i].Go);
            }
            _rows.Clear();
            if (_scroll != null) _scroll.ResetPosition();
        }

        public void Layout(int rowSpacing)
        {
            if (_contentRoot == null) return;
            float topY = _viewportLocal.height * 0.5f;
            float cursor = topY;
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (r.Go == null) continue;
                cursor -= r.Height * 0.5f;
                r.Go.transform.localPosition = new Vector3(0, cursor, 0);
                cursor -= r.Height * 0.5f + rowSpacing;
            }
            if (_scroll != null) _scroll.ResetPosition();
        }

        private void EnsureScrollDrag(GameObject row)
        {
            // Any collider on the row (or its children) must forward drags to the scroll view.
            BoxCollider[] colliders = row.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                UIDragScrollView drag = colliders[i].GetComponent<UIDragScrollView>();
                if (drag == null) drag = colliders[i].gameObject.AddComponent<UIDragScrollView>();
                drag.scrollView = _scroll;
            }
        }

        private struct RowEntry
        {
            public readonly GameObject Go;
            public readonly int Height;
            public RowEntry(GameObject go, int h) { Go = go; Height = h; }
        }
    }
}
