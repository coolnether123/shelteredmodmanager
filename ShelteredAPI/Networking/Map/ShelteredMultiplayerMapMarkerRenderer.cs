using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using UnityEngine;

namespace ShelteredAPI.Networking.Map
{
    internal sealed class ShelteredMultiplayerMapMarkerRenderer
    {
        private const string RootName = "ShelteredMultiplayerMapMarkers";

        private readonly Dictionary<string, MarkerView> _views =
            new Dictionary<string, MarkerView>(System.StringComparer.Ordinal);
        private readonly List<string> _staleKeys = new List<string>();

        private GameObject _root;

        public void Render(IList<ShelteredMultiplayerMapMarker> markers)
        {
            Transform parent = ResolveMarkerParent();
            if (parent == null)
            {
                Clear();
                return;
            }

            EnsureRoot(parent);
            MarkAllStale();

            if (markers != null)
            {
                for (int i = 0; i < markers.Count; i++)
                {
                    ShelteredMultiplayerMapMarker marker = markers[i];
                    if (marker == null || string.IsNullOrEmpty(marker.MarkerId))
                        continue;

                    MarkerView view = GetOrCreateView(marker);
                    UpdateView(view, marker);
                }
            }

            RemoveStale();
        }

        public void Clear()
        {
            foreach (MarkerView view in _views.Values)
            {
                if (view != null && view.Root != null)
                    Object.Destroy(view.Root);
            }

            _views.Clear();
            _staleKeys.Clear();

            if (_root != null)
                Object.Destroy(_root);
            _root = null;
        }

        private static Transform ResolveMarkerParent()
        {
            ExplorationManager manager = ExplorationManager.Instance;
            if (manager == null || manager.mapSourceSprite == null)
                return null;

            return manager.mapSourceSprite.gameObject.transform;
        }

        private void EnsureRoot(Transform parent)
        {
            if (_root != null)
            {
                if (_root.transform.parent != parent)
                    _root.transform.parent = parent;
                return;
            }

            _root = new GameObject(RootName);
            _root.transform.parent = parent;
            _root.transform.localScale = Vector3.one;
            _root.transform.localPosition = Vector3.zero;
            NGUITools.SetLayer(_root, parent.gameObject.layer);
        }

        private void MarkAllStale()
        {
            _staleKeys.Clear();
            foreach (string key in _views.Keys)
                _staleKeys.Add(key);
        }

        private MarkerView GetOrCreateView(ShelteredMultiplayerMapMarker marker)
        {
            MarkerView view;
            if (_views.TryGetValue(marker.MarkerId, out view) && view != null && view.Root != null)
            {
                _staleKeys.Remove(marker.MarkerId);
                return view;
            }

            GameObject root = new GameObject("Marker_" + marker.MarkerId);
            root.transform.parent = _root.transform;
            root.transform.localScale = Vector3.one;
            NGUITools.SetLayer(root, _root.layer);

            UILabel glyph = CreateLabel(root.transform, "Glyph", "?", 20, new Vector3(0f, 0f, 0f), 42, 24);
            UILabel label = CreateLabel(root.transform, "Label", string.Empty, 12, new Vector3(0f, -18f, 0f), 120, 20);
            label.alignment = NGUIText.Alignment.Center;

            view = new MarkerView(root, glyph, label);
            _views[marker.MarkerId] = view;
            _staleKeys.Remove(marker.MarkerId);
            return view;
        }

        private static UILabel CreateLabel(
            Transform parent,
            string name,
            string text,
            int fontSize,
            Vector3 position,
            int width,
            int height)
        {
            GameObject go = new GameObject(name);
            go.transform.parent = parent;
            go.transform.localPosition = position;
            go.transform.localScale = Vector3.one;
            NGUITools.SetLayer(go, parent.gameObject.layer);

            UILabel label = go.AddComponent<UILabel>();
            UIFontCache.FontResult fonts = UIFontCache.GetFonts();
            label.bitmapFont = fonts.Bitmap;
            label.trueTypeFont = fonts.TTF;
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.width = width;
            label.height = height;
            label.depth = 40;
            label.alignment = NGUIText.Alignment.Center;
            label.pivot = UIWidget.Pivot.Center;
            label.overflowMethod = UILabel.Overflow.ShrinkContent;
            label.effectStyle = UILabel.Effect.Outline;
            label.effectColor = Color.black;
            return label;
        }

        private static void UpdateView(MarkerView view, ShelteredMultiplayerMapMarker marker)
        {
            view.Root.transform.localPosition = marker.MapPixels;
            view.Glyph.text = ResolveGlyph(marker);
            view.Glyph.color = ResolveColor(marker);
            view.Label.text = marker.Label ?? string.Empty;
            view.Label.color = marker.IsOnline ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
        }

        private void RemoveStale()
        {
            for (int i = 0; i < _staleKeys.Count; i++)
            {
                string key = _staleKeys[i];
                MarkerView view;
                if (!_views.TryGetValue(key, out view))
                    continue;

                if (view != null && view.Root != null)
                    Object.Destroy(view.Root);
                _views.Remove(key);
            }

            _staleKeys.Clear();
        }

        private static string ResolveGlyph(ShelteredMultiplayerMapMarker marker)
        {
            if (marker == null || marker.IsUnknown)
                return "?";
            if (!marker.IsOnline)
                return "x";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.LocalBunker)
                return "H";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.RemoteBunker)
                return "B";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.Expedition)
                return "E";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.TradeCaravan)
                return "T";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.RaidParty)
                return "R";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.Settlement)
                return "S";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.ResourceNode)
                return "N";
            if (marker.VisualKind == ShelteredMultiplayerMapMarkerVisualKind.FactionMarker)
                return "F";

            return "?";
        }

        private static Color ResolveColor(ShelteredMultiplayerMapMarker marker)
        {
            if (marker == null || marker.IsUnknown)
                return Color.yellow;
            if (!marker.IsOnline)
                return Color.gray;
            if (marker.IsLocal)
                return Color.green;
            return Color.cyan;
        }

        private sealed class MarkerView
        {
            public MarkerView(GameObject root, UILabel glyph, UILabel label)
            {
                Root = root;
                Glyph = glyph;
                Label = label;
            }

            public readonly GameObject Root;
            public readonly UILabel Glyph;
            public readonly UILabel Label;
        }
    }
}
