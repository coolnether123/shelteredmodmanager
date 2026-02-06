using System;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.UI
{
    /// <summary>
    /// NGUI helpers for cloning, labeling, spacing, click blocking, and creating
    /// overlay/labels with sane defaults (panel, font, depth, anchors).
    /// </summary>
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

        /// <summary>
        /// Clones a UIButton template under the specified parent, strips UILocalize and UIButtonMessage,
        /// clears UIButton.onClick, and sets all UILabels to the provided text.
        /// Returns the new UIButton instance (or null on failure).
        /// </summary>
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

            // --- Robust Mod-Safe Cleanup ---
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
                w.enabled = true;
            }

            SetAllLabels(cloneGO, text);
            return btn;
        }

        /// <summary>
        /// Measures the local Y delta from A to B. Useful for stacking clones vertically.
        /// </summary>
        public static float MeasureVerticalSpacing(UIButton a, UIButton b)
        {
            if (a == null || b == null) return 0f;
            return b.transform.localPosition.y - a.transform.localPosition.y;
        }

        /// <summary>
        /// Sets all UILabels under the GameObject to the specified text.
        /// </summary>
        public static void SetAllLabels(GameObject go, string text)
        {
            if (go == null) return;
            var labels = go.GetComponentsInChildren<UILabel>(true);
            foreach (var l in labels) if (l != null) l.text = text;
        }

        public static GameObject PushClickBlocker(Transform parent, int depth)
        {
            if (parent == null) return null;

            var go = new GameObject("UIUtil_ClickBlocker");
            go.transform.parent = parent;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = parent.gameObject.layer;

            var panel = go.AddComponent<UIPanel>();
            panel.depth = depth;
            panel.alpha = 0f;

            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = Vector3.zero;
            col.size = new Vector3(10000f, 10000f, 0.2f);

            var lis = UIEventListener.Get(go);
            lis.onClick += (_) => { };

            _clickBlockers.Push(go);
            return go;
        }

        /// <summary>
        /// Pops and destroys the most recent click blocker if present.
        /// </summary>
        public static void PopClickBlocker()
        {
            if (_clickBlockers.Count == 0) return;
            var go = _clickBlockers.Pop();
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        public static GameObject CloneAndReposition(GameObject template, Vector3 localOffset, Transform parent = null)
        {
            if (template == null) return null;

            var clone = UnityEngine.Object.Instantiate(template) as GameObject;
            if (clone == null) return null;

            var targetParent = parent != null ? parent : template.transform.parent;
            if (targetParent != null)
            {
                clone.transform.SetParent(targetParent, false);
                NGUITools.SetLayer(clone, targetParent.gameObject.layer);
            }

            clone.name = template.name + "_Clone";
            clone.transform.localScale = template.transform.localScale;
            clone.transform.localRotation = template.transform.localRotation;
            clone.transform.localPosition = template.transform.localPosition + localOffset;

            // --- Robust Mod-Safe Cleanup ---
            foreach (var loc in clone.GetComponentsInChildren<UILocalize>(true)) UnityEngine.Object.Destroy(loc);
            foreach (var msg in clone.GetComponentsInChildren<UIButtonMessage>(true)) UnityEngine.Object.Destroy(msg);
            foreach (var anchor in clone.GetComponentsInChildren<UIAnchor>(true)) UnityEngine.Object.Destroy(anchor);

            foreach (var w in clone.GetComponentsInChildren<UIWidget>(true))
            {
                w.alpha = 1f;
                w.enabled = true;
            }

            return clone;
        }

        public static T CloneAndReposition<T>(T template, Vector3 localOffset, Transform parent = null) where T : Component
        {
            if (template == null) return null;
            var go = template.gameObject;
            var cloneGo = CloneAndReposition(go, localOffset, parent);
            return cloneGo != null ? cloneGo.GetComponent<T>() : null;
        }

        public enum AnchorCorner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
            Center
        }

        /// <summary>
        /// Options for UIUtil.CreateLabel covering text/font/color/size/alignment, depth, and placement.
        /// </summary>
        [Serializable]
        public class UILabelOptions
        {
            public string text = "Label";
            public Color color = Color.white;
            public int fontSize = 22;
            public Vector3 localPosition = Vector3.zero; // Added for flexibility
            public NGUIText.Alignment alignment = NGUIText.Alignment.Left;
            public UILabel.Effect effect = UILabel.Effect.Outline;
            public Color effectColor = new Color(0f, 0f, 0f, 0.9f);

            // Layout
            public AnchorCorner anchor = AnchorCorner.Center;
            public Vector2 pixelOffset = Vector2.zero;

            // Depth
            public int depth = 10020; // Default depth 
            public int relativeDepth = 10;
            public int? absoluteDepth = null;

            // Fonts
            public string uiFontName = null;
            public string trueTypeFontName = null;

            // Misc
            public bool resizeFreely = true;
        }

        /// <summary>
        /// Create an NGUI UILabel under 'parent' with sensible defaults.
        /// Matches v1.0 logic but integrated with modern API.
        /// </summary>
        public static UILabel CreateLabel(GameObject parent, UILabelOptions opts, out UIPanel usedPanel)
        {
            usedPanel = null;
            if (parent == null) return null;
            if (opts == null) opts = new UILabelOptions();

            var panel = NGUITools.FindInParents<UIPanel>(parent);
            if (panel == null)
            {
                panel = parent.GetComponent<UIPanel>() ?? parent.AddComponent<UIPanel>();
            }
            usedPanel = panel;

            var uiRoot = NGUITools.FindInParents<UIRoot>(panel != null ? panel.gameObject : parent) ?? UnityEngine.Object.FindObjectOfType<UIRoot>();

            var go = new GameObject("ModAPI_Label");
            go.transform.SetParent(parent.transform, false);
            go.layer = parent.layer;

            var label = go.AddComponent<UILabel>();
            label.color = opts.color;
            label.fontSize = opts.fontSize;
            label.alignment = opts.alignment;
            label.effectStyle = opts.effect;
            label.effectColor = opts.effectColor;
            label.alpha = 1f;
            label.text = opts.text ?? string.Empty;

            if (opts.resizeFreely)
                label.overflowMethod = UILabel.Overflow.ResizeFreely;

            // Font choice logic from v1.0
            UILabel sample = NGUITools.FindInParents<UILabel>(parent);
            if (sample == null)
            {
                var all = UnityEngine.Object.FindObjectsOfType<UILabel>();
                if (all != null && all.Length > 0) sample = all[0];
            }

            UIFont chosenUIFont = null;
            Font chosenTTF = null;

            if (!string.IsNullOrEmpty(opts.uiFontName))
                chosenUIFont = FindUIFont(opts.uiFontName);

            if (chosenUIFont == null && sample != null) chosenUIFont = sample.bitmapFont;
            if (chosenUIFont == null && sample != null) chosenTTF = sample.trueTypeFont;

            if (chosenUIFont != null)
            {
                label.bitmapFont = chosenUIFont;
            }
            else
            {
                if (chosenTTF == null)
                    chosenTTF = Resources.GetBuiltinResource<Font>("Arial.ttf");
                label.trueTypeFont = chosenTTF;
            }

            // Depth
            int targetDepth = opts.absoluteDepth.HasValue ? opts.absoluteDepth.Value : ComputeSafeDepth(panel, opts.relativeDepth);
            label.depth = targetDepth;

            // Position
            label.pivot = PivotFor(opts.anchor);
            
            // If absolute pixelOffset is provided (v1.0 style), use it. Otherwise use localPosition.
            if (opts.pixelOffset != Vector2.zero || opts.anchor != AnchorCorner.Center)
            {
                go.transform.localPosition = ComputeAnchorPosition(uiRoot, opts.anchor, opts.pixelOffset);
            }
            else
            {
                go.transform.localPosition = opts.localPosition;
            }

            go.transform.localScale = Vector3.one;

            return label;
        }

        public static UILabel CreateLabelQuick(GameObject parent, string text, int size, Vector3 pos)
        {
            var opts = new UILabelOptions { text = text, fontSize = size, localPosition = pos };
            UIPanel panel;
            return CreateLabel(parent, opts, out panel);
        }

        public static UILabel CreateLabel(GameObject parent, string text, int fontSize, Vector3 localPos, AnchorCorner anchor = AnchorCorner.Center)
        {
            var opts = new UILabelOptions { text = text, fontSize = fontSize, localPosition = localPos, anchor = anchor };
            UIPanel panel;
            return CreateLabel(parent, opts, out panel);
        }

        public static UIButton CreateButton(GameObject parent, GameObject templateGO, string text, int width, int height, Vector3 localPos, Action onClick)
        {
            if (templateGO == null || parent == null) return null;
            var templateBtn = templateGO.GetComponent<UIButton>();
            if (templateBtn == null) return null;

            var btn = CloneButton(templateBtn, parent.transform, text);
            if (btn != null)
            {
                btn.transform.localPosition = localPos;
                var sprite = btn.GetComponentInChildren<UISprite>();
                if (sprite != null) { sprite.width = width; sprite.height = height; }
                var tex = btn.GetComponentInChildren<UITexture>();
                if (tex != null) { tex.width = width; tex.height = height; }
                var box = btn.GetComponent<BoxCollider>();
                if (box != null) box.size = new Vector3(width, height, 1);

                var labels = btn.GetComponentsInChildren<UILabel>();
                foreach (var lbl in labels)
                {
                    lbl.width = width - 20;
                    lbl.overflowMethod = UILabel.Overflow.ShrinkContent;
                }

                UIEventListener.Get(btn.gameObject).onClick = (_) => onClick?.Invoke();
            }
            return btn;
        }

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
            tex.depth = 10005;
            tex.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(width, height, 1f);
            collider.center = Vector3.zero;
            collider.isTrigger = true;

            return go;
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

        public static int ComputeSafeDepth(UIPanel panel, int relativeAdd)
        {
            int max = 0;
            if (panel != null)
            {
                var widgets = panel.GetComponentsInChildren<UIWidget>(true);
                for (int i = 0; i < widgets.Length; i++)
                    if (widgets[i] != null && widgets[i].depth > max)
                        max = widgets[i].depth;
            }
            return max + Math.Max(0, relativeAdd);
        }

        public static int ComputeSafeDepth(GameObject parent, int offset = 0)
        {
            var panel = NGUITools.FindInParents<UIPanel>(parent);
            return ComputeSafeDepth(panel, offset);
        }

        public static UIFont FindUIFont(string nameContains)
        {
            if (string.IsNullOrEmpty(nameContains)) return null;
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll(typeof(UIFont)) as UIFont[];
                if (fonts != null)
                {
                    foreach (var f in fonts)
                        if (f != null && f.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                            return f;
                }
            }
            catch { }
            return null;
        }

        public static UIButton FindAnyButtonTemplate()
        {
            var all = UnityEngine.Object.FindObjectsOfType<UIButton>();
            foreach (var b in all) if (b != null && b.gameObject.activeInHierarchy) return b;
            return all.Length > 0 ? all[0] : null;
        }

        private static UIWidget.Pivot PivotFor(AnchorCorner a)
        {
            switch (a)
            {
                case AnchorCorner.TopLeft: return UIWidget.Pivot.TopLeft;
                case AnchorCorner.TopRight: return UIWidget.Pivot.TopRight;
                case AnchorCorner.BottomLeft: return UIWidget.Pivot.BottomLeft;
                case AnchorCorner.BottomRight: return UIWidget.Pivot.BottomRight;
                default: return UIWidget.Pivot.Center;
            }
        }

        private static Vector3 ComputeAnchorPosition(UIRoot root, AnchorCorner a, Vector2 inset)
        {
            float halfH, halfW;
            if (root != null)
            {
                int ah = root.activeHeight;
                halfH = ah * 0.5f;
                float aspect = (float)Screen.width / Math.Max(1, Screen.height);
                halfW = halfH * aspect;
            }
            else
            {
                halfH = Screen.height * 0.5f;
                halfW = Screen.width * 0.5f;
            }

            float x = 0f, y = 0f;
            switch (a)
            {
                case AnchorCorner.TopLeft: x = -halfW; y = halfH; break;
                case AnchorCorner.TopRight: x = halfW; y = halfH; break;
                case AnchorCorner.BottomLeft: x = -halfW; y = -halfH; break;
                case AnchorCorner.BottomRight: x = halfW; y = -halfH; break;
            }
            return new Vector3(x + inset.x, y + inset.y, 0f);
        }
    }
}