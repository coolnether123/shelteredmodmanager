using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.UI
{
    /// <summary>
    /// Anchor corner positions for UI elements.
    /// </summary>
    public enum AnchorCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    /// <summary>
    /// Options for creating labels with CreateLabel method.
    /// </summary>
    public class UILabelOptions
    {
        public string text = "";
        public int fontSize = 16;
        public Vector3 localPosition = Vector3.zero;
        public AnchorCorner anchor = AnchorCorner.Center;
        public NGUIText.Alignment alignment = NGUIText.Alignment.Left;
        public Color color = Color.white;
        public int depth = 10020;
    }

    public static class UIUtil
    {
        private static readonly Stack<GameObject> _clickBlockers = new Stack<GameObject>();
        private static Texture2D _cachedWhiteTex;

        public static Texture2D WhiteTexture
        {
            get
            {
                if (_cachedWhiteTex == null)
                {
                    _cachedWhiteTex = new Texture2D(2, 2);
                    for (int i = 0; i < 4; i++) _cachedWhiteTex.SetPixel(i % 2, i / 2, Color.white);
                    _cachedWhiteTex.Apply();
                }
                return _cachedWhiteTex;
            }
        }

        public static UIButton CloneButton(UIButton template, Transform parent, string text)
        {
            if (template == null || parent == null) return null;

            var templateGO = template.gameObject;
            var cloneGO = UnityEngine.Object.Instantiate(templateGO) as GameObject;
            if (cloneGO == null) return null;

            cloneGO.name = templateGO.name + "_Clone";
            cloneGO.transform.parent = parent;
            cloneGO.transform.localPosition = templateGO.transform.localPosition;
            cloneGO.transform.localRotation = templateGO.transform.localRotation;
            cloneGO.transform.localScale = templateGO.transform.localScale;
            cloneGO.layer = parent.gameObject.layer;

            // Cleanup
            foreach (var loc in cloneGO.GetComponentsInChildren<UILocalize>(true)) UnityEngine.Object.Destroy(loc);
            foreach (var msg in cloneGO.GetComponentsInChildren<UIButtonMessage>(true)) UnityEngine.Object.Destroy(msg);
            foreach (var anchor in cloneGO.GetComponentsInChildren<UIAnchor>(true)) UnityEngine.Object.Destroy(anchor);

            var btn = cloneGO.GetComponent<UIButton>();
            if (btn != null && btn.onClick != null) btn.onClick.Clear();

            // Fix NGUI Layer mismatch (Recursively set layer)
            NGUITools.SetLayer(cloneGO, parent.gameObject.layer);

            // Fix Invisible Widgets (Reset Alpha/Color from potentially hidden templates)
            foreach (var w in cloneGO.GetComponentsInChildren<UIWidget>(true))
            {
                w.alpha = 1f;
                // Don't override color blindly as it might destroy styling, but ensure alpha is up.
                // w.color = Color.white; 
                w.enabled = true; // Ensure component execution
            }

            SetAllLabels(cloneGO, text);
            return btn;
        }

        public static void SetAllLabels(GameObject go, string text)
        {
            if (go == null) return;
            var labels = go.GetComponentsInChildren<UILabel>(true);
            foreach (var l in labels) if (l != null) l.text = text;
        }

        /// <summary>
        /// Quick helper to create a label. Defaults depth to 10020 to sit above standard backgrounds.
        /// </summary>
        public static UILabel CreateLabelQuick(GameObject parent, string text, int size, Vector3 pos)
        {
            var go = NGUITools.AddChild(parent);
            go.transform.localPosition = pos;
            if (parent != null) NGUITools.SetLayer(go, parent.layer);

            var lbl = go.AddComponent<UILabel>();
            lbl.text = text;
            lbl.fontSize = size;
            lbl.overflowMethod = UILabel.Overflow.ResizeFreely;
            lbl.depth = 10020; // Safe default above backgrounds (usually 10000-10010)

            // Font Fallback
            var anyLabel = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (anyLabel != null && anyLabel.bitmapFont != null)
            {
                lbl.bitmapFont = anyLabel.bitmapFont;
            }
            else
            {
                var arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (arial != null) lbl.trueTypeFont = arial;
            }

            return lbl;
        }

        public static UIButton CreateButton(GameObject parent, GameObject templateGO, string text, int width, int height, Vector3 localPos, Action onClick)
        {
            if (templateGO == null || parent == null) return null;

            // Ensure template has a button component
            var templateBtn = templateGO.GetComponent<UIButton>();
            if (templateBtn == null) return null;

            var btn = CloneButton(templateBtn, parent.transform, text);
            if (btn != null)
            {
                btn.transform.localPosition = localPos;

                // Adjust Size
                var sprite = btn.GetComponentInChildren<UISprite>();
                if (sprite != null) { sprite.width = width; sprite.height = height; }
                var tex = btn.GetComponentInChildren<UITexture>();
                if (tex != null) { tex.width = width; tex.height = height; }

                var box = btn.GetComponent<BoxCollider>();
                if (box != null) box.size = new Vector3(width, height, 1);

                UIEventListener.Get(btn.gameObject).onClick = (_) => onClick?.Invoke();
            }
            return btn;
        }

        /// <summary>
        /// Create a button using an automatically found template.
        /// </summary>
        public static UIButton CreateButton(GameObject parent, string text, int width, int height, Vector3 localPos, Action onClick)
        {
            var template = FindAnyButtonTemplate();
            if (template == null) return null;
            return CreateButton(parent, template.gameObject, text, width, height, localPos, onClick);
        }

        public static GameObject CreatePanelBackground(GameObject parent, int width, int height)
        {
            var go = new GameObject("Background");
            go.transform.parent = parent.transform;
            go.transform.localPosition = Vector3.zero;
            go.layer = parent.layer;

            var tex = go.AddComponent<UITexture>();
            tex.mainTexture = WhiteTexture;
            var shader = Shader.Find("Unlit/Transparent Colored");
            if (shader != null) tex.shader = shader;

            tex.width = width;
            tex.height = height;
            tex.depth = 10005; // Standard Background Depth
            tex.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Add Input Blocker
            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 1f);
            collider.center = Vector3.zero;
            collider.isTrigger = true; // Trigger allows UI events but blocks raycasts in NGUI (usually) - wait, NGUI needs non-trigger for Raycast? 
            // NGUI uses Physics.Raycast. It hits colliders. 
            // To block input, we just need a collider. 
            // Let's make sure it's on the right layer in the calling code.
            collider.isTrigger = true; // Use trigger to avoid physics interactions if any

            return go;
        }

        public static UIButton FindAnyButtonTemplate()
        {
            var all = UnityEngine.Object.FindObjectsOfType<UIButton>();
            foreach (var b in all) if (b != null && b.gameObject.activeInHierarchy) return b;
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>
        /// Computes a safe depth value for UI elements based on parent panel depth.
        /// </summary>
        public static int ComputeSafeDepth(UIPanel parentPanel, int offset = 0)
        {
            if (parentPanel == null) return 10000 + offset;
            return parentPanel.depth + offset;
        }

        /// <summary>
        /// Comprehensive label creation with anchor positioning.
        /// </summary>
        public static UILabel CreateLabel(GameObject parent, string text, int fontSize, Vector3 localPos, AnchorCorner anchor = AnchorCorner.Center)
        {
            var label = CreateLabelQuick(parent, text, fontSize, localPos);
            if (label != null)
            {
                label.pivot = PivotFor(anchor);
                label.alignment = NGUIText.Alignment.Left;
            }
            return label;
        }

        /// <summary>
        /// Create a label using UILabelOptions configuration object.
        /// </summary>
        public static UILabel CreateLabel(GameObject parent, UILabelOptions opts, out UIPanel usedPanel)
        {
            usedPanel = null;
            if (opts == null || parent == null) return null;

            var label = CreateLabelQuick(parent, opts.text, opts.fontSize, opts.localPosition);
            if (label != null)
            {
                label.pivot = PivotFor(opts.anchor);
                label.alignment = opts.alignment;
                label.color = opts.color;
                label.depth = opts.depth;
            }

            return label;
        }

        private static UIWidget.Pivot PivotFor(AnchorCorner corner)
        {
            if (corner == AnchorCorner.TopLeft) return UIWidget.Pivot.TopLeft;
            if (corner == AnchorCorner.TopRight) return UIWidget.Pivot.TopRight;
            if (corner == AnchorCorner.BottomLeft) return UIWidget.Pivot.BottomLeft;
            if (corner == AnchorCorner.BottomRight) return UIWidget.Pivot.BottomRight;
            return UIWidget.Pivot.Center;
        }

        public static UIPanel EnsureOverlayPanel(string name = "ModAPI_OverlayPanel", int depth = 50000)
        {
            var root = UnityEngine.Object.FindObjectOfType<UIRoot>();
            if (root == null) return null;

            var tf = root.transform.Find(name);
            GameObject go = tf != null ? tf.gameObject : null;
            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                go.layer = root.gameObject.layer;
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one;
            }

            var panel = go.GetComponent<UIPanel>() ?? go.AddComponent<UIPanel>();
            panel.depth = depth;
            panel.clipping = UIDrawCall.Clipping.None;
            panel.alpha = 1f;
            return panel;
        }

        public static UITexture CreateFlatTexture(GameObject parent, int width, int height, Color color)
        {
            var go = NGUITools.AddChild(parent);
            var tex = go.AddComponent<UITexture>();
            tex.width = width;
            tex.height = height;
            tex.color = color;
            
            tex.mainTexture = WhiteTexture;
            
            var shader = Shader.Find("Unlit/Transparent Colored");
            if (shader != null) tex.shader = shader;
            
            return tex;
        }
    }
}