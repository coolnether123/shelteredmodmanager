using System;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Primitives
{
    /// <summary>
    /// Centralizes raw NGUI GameObject construction (UITexture quads, UILabels, click colliders).
    /// Every other class in FieldManual goes through this so layering, font choice, and depth math
    /// live in exactly one place.
    /// </summary>
    internal sealed class UIPrimitiveFactory
    {
        private readonly UIFont _bitmapFont;
        private readonly Font _ttfFont;
        private int _depthCursor;

        public UIPrimitiveFactory(UIFont bitmapFont, Font ttfFont, int baseDepth)
        {
            _bitmapFont = bitmapFont;
            _ttfFont = ttfFont;
            _depthCursor = baseDepth;
        }

        public int NextDepth() { return ++_depthCursor; }

        public GameObject CreateChild(GameObject parent, string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one;
            go.layer = parent.layer;
            return go;
        }

        public UITexture CreateQuad(GameObject parent, string name, Texture2D texture, Vector3 localPosition, int width, int height, Color color, int depth)
        {
            GameObject go = CreateChild(parent, name, localPosition);
            UITexture tex = go.AddComponent<UITexture>();
            tex.mainTexture = texture;
            tex.width = width;
            tex.height = height;
            tex.color = color;
            tex.depth = depth;
            return tex;
        }

        public UILabel CreateLabel(GameObject parent, string name, string text, Vector3 localPosition, int fontSize, Color color, int width, int height, NGUIText.Alignment alignment, UIWidget.Pivot pivot, int depth)
        {
            GameObject go = CreateChild(parent, name, localPosition);
            UILabel label = go.AddComponent<UILabel>();
            if (_bitmapFont != null)
                label.bitmapFont = _bitmapFont;
            else if (_ttfFont != null)
                label.trueTypeFont = _ttfFont;
            label.fontSize = fontSize;
            label.text = text ?? string.Empty;
            label.color = color;
            label.width = width;
            label.height = height;
            label.alignment = alignment;
            label.pivot = pivot;
            label.depth = depth;
            label.overflowMethod = UILabel.Overflow.ClampContent;
            label.multiLine = false;
            go.transform.localPosition = localPosition;
            return label;
        }

        public BoxCollider AddClickCollider(GameObject target, int width, int height, Action onClick)
        {
            BoxCollider col = target.GetComponent<BoxCollider>();
            if (col == null) col = target.AddComponent<BoxCollider>();
            col.size = new Vector3(width, height, 1f);
            col.center = ResolveColliderCenter(target, width, height);
            if (onClick != null)
            {
                UIEventListener listener = UIEventListener.Get(target);
                listener.onClick = delegate(GameObject go) { onClick(); };
            }
            return col;
        }

        private static Vector3 ResolveColliderCenter(GameObject target, int width, int height)
        {
            UIWidget widget = target != null ? target.GetComponent<UIWidget>() : null;
            if (widget == null) return Vector3.zero;

            switch (widget.pivot)
            {
                case UIWidget.Pivot.TopLeft: return new Vector3(width * 0.5f, -height * 0.5f, 0f);
                case UIWidget.Pivot.Top: return new Vector3(0f, -height * 0.5f, 0f);
                case UIWidget.Pivot.TopRight: return new Vector3(-width * 0.5f, -height * 0.5f, 0f);
                case UIWidget.Pivot.Left: return new Vector3(width * 0.5f, 0f, 0f);
                case UIWidget.Pivot.Right: return new Vector3(-width * 0.5f, 0f, 0f);
                case UIWidget.Pivot.BottomLeft: return new Vector3(width * 0.5f, height * 0.5f, 0f);
                case UIWidget.Pivot.Bottom: return new Vector3(0f, height * 0.5f, 0f);
                case UIWidget.Pivot.BottomRight: return new Vector3(-width * 0.5f, height * 0.5f, 0f);
                case UIWidget.Pivot.Center:
                default:
                    return Vector3.zero;
            }
        }
    }
}
