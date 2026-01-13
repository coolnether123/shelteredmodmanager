using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Higher-level UI utilities for finding panels, cloning elements, and managing depths.
    /// Complementary to UIUtil but focuses on ease of use for modders.
    /// </summary>
    public static class UIHelper
    {
        private static int _nextReservedDepth = 100000;
        private static readonly FieldInfo _panelStackField = typeof(UIPanelManager).GetField("m_panel_stack", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>
        /// Finds a panel of type T in the scene. If nameOrPath is provided, filters by name or partial path.
        /// </summary>
        public static T FindPanel<T>(string nameOrPath = null) where T : MonoBehaviour
        {
            var all = UnityEngine.Object.FindObjectsOfType<T>();
            if (string.IsNullOrEmpty(nameOrPath))
            {
                return all.Length > 0 ? all[0] : null;
            }

            foreach (var panel in all)
            {
                if (panel.name == nameOrPath || GetGameObjectPath(panel.gameObject).EndsWith(nameOrPath))
                {
                    return panel;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns all panels currently on the UIPanelManager stack.
        /// </summary>
        public static ReadOnlyCollection<BasePanel> GetAllActivePanels()
        {
            if (UIPanelManager.instance == null || _panelStackField == null)
                return new List<BasePanel>().AsReadOnly();

            var stack = _panelStackField.GetValue(UIPanelManager.instance) as List<BasePanel>;
            return stack != null ? stack.AsReadOnly() : new List<BasePanel>().AsReadOnly();
        }

        /// <summary>
        /// Safely clones a UIButton, sets the text label, and assigns an onClick handler.
        /// </summary>
        public static UIButton CloneButton(UIButton template, Transform parent, string label, Action onClick)
        {
            if (template == null || parent == null) return null;

            // Reuse UIUtil's robust cloning logic
            var btn = UIUtil.CloneButton(template, parent, label);
            if (btn == null) return null;

            if (onClick != null)
            {
                EventDelegate.Add(btn.onClick, new EventDelegate(() => onClick()));
            }

            return btn;
        }

        /// <summary>
        /// Creates a UILabel with standard game fonts and sane defaults.
        /// </summary>
        public static UILabel CreateLabel(Transform parent, string text, int fontSize = 28, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (parent == null) return null;

            GameObject go = new GameObject("ModAPI_Label");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;

            var label = go.AddComponent<UILabel>();
            
            // Try to find a good default font from the scene
            var sample = UnityEngine.Object.FindObjectOfType<UILabel>();
            if (sample != null)
            {
                label.bitmapFont = sample.bitmapFont;
                label.trueTypeFont = sample.trueTypeFont;
            }
            else
            {
                label.trueTypeFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            label.fontSize = fontSize;
            label.text = text;
            label.overflowMethod = UILabel.Overflow.ResizeFreely;
            
            // Map TextAnchor to NGUI Pivot and Alignment
            ApplyTextAnchor(label, anchor);

            return label;
        }

        /// <summary>
        /// Sets the sprite of a UISprite by searching for the sprite name in available atlases.
        /// assetPath can be just "SpriteName" or "AtlasName:SpriteName".
        /// </summary>
        public static void SetSpriteFromPath(UISprite sprite, string assetPath)
        {
            if (sprite == null || string.IsNullOrEmpty(assetPath)) return;

            string atlasName = null;
            string spriteName = assetPath;

            if (assetPath.Contains(":"))
            {
                var parts = assetPath.Split(':');
                atlasName = parts[0];
                spriteName = parts[1];
            }

            if (!string.IsNullOrEmpty(atlasName))
            {
                var atlases = Resources.FindObjectsOfTypeAll<UIAtlas>();
                foreach (var atlas in atlases)
                {
                    if (atlas.name.Equals(atlasName, StringComparison.OrdinalIgnoreCase))
                    {
                        sprite.atlas = atlas;
                        break;
                    }
                }
            }

            sprite.spriteName = spriteName;
            sprite.MarkAsChanged();
        }

        /// <summary>
        /// Clones a GameObject and optionally strips NGUI anchors/stretch components to allow free positioning.
        /// </summary>
        public static GameObject Clone(GameObject template, Transform parent, bool stripAnchors = true)
        {
            if (template == null) return null;
            var go = UnityEngine.Object.Instantiate(template) as GameObject;
            if (go == null) return null;

            go.name = template.name + "_Clone";
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
                go.layer = parent.gameObject.layer;
            }

            if (stripAnchors) StripAnchors(go);

            return go;
        }

        /// <summary>
        /// Removes UIAnchor and UIStretch components from a GameObject and its children, 
        /// and clears UIWidget anchors to prevent snapping.
        /// </summary>
        public static void StripAnchors(GameObject go)
        {
            if (go == null) return;

            foreach (var anchor in go.GetComponentsInChildren<UIAnchor>(true))
                UnityEngine.Object.Destroy(anchor);

            foreach (var stretch in go.GetComponentsInChildren<UIStretch>(true))
                UnityEngine.Object.Destroy(stretch);

            foreach (var widget in go.GetComponentsInChildren<UIWidget>(true))
            {
                try { widget.SetAnchor((Transform)null); } catch { }
            }
        }

        /// <summary>
        /// Reserves a range of UI depths to prevent mod UI overlapping with other mods or game UI.
        /// Returns the start of the reserved range.
        /// </summary>
        public static int ReserveDepthRange(int count)
        {
            int start = _nextReservedDepth;
            _nextReservedDepth += count;
            return start;
        }

        /// <summary>
        /// Recursively sets the depth of all UIWidgets under a transform relative to a base depth.
        /// Maintains relative hierarchy depths if possible.
        /// </summary>
        public static void SetChildDepths(Transform root, int baseDepth)
        {
            if (root == null) return;
            
            var widgets = root.GetComponentsInChildren<UIWidget>(true);
            if (widgets.Length == 0) return;

            // Find min depth to use as offset
            int min = int.MaxValue;
            foreach (var w in widgets) if (w.depth < min) min = w.depth;

            foreach (var w in widgets)
            {
                w.depth = baseDepth + (w.depth - min);
            }
        }

        private static string GetGameObjectPath(GameObject obj)
        {
            string path = "/" + obj.name;
            while (obj.transform.parent != null)
            {
                obj = obj.transform.parent.gameObject;
                path = "/" + obj.name + path;
            }
            return path;
        }

        private static void ApplyTextAnchor(UILabel label, TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    label.pivot = UIWidget.Pivot.TopLeft;
                    label.alignment = NGUIText.Alignment.Left;
                    break;
                case TextAnchor.UpperCenter:
                    label.pivot = UIWidget.Pivot.Top;
                    label.alignment = NGUIText.Alignment.Center;
                    break;
                case TextAnchor.UpperRight:
                    label.pivot = UIWidget.Pivot.TopRight;
                    label.alignment = NGUIText.Alignment.Right;
                    break;
                case TextAnchor.MiddleLeft:
                    label.pivot = UIWidget.Pivot.Left;
                    label.alignment = NGUIText.Alignment.Left;
                    break;
                case TextAnchor.MiddleCenter:
                    label.pivot = UIWidget.Pivot.Center;
                    label.alignment = NGUIText.Alignment.Center;
                    break;
                case TextAnchor.MiddleRight:
                    label.pivot = UIWidget.Pivot.Right;
                    label.alignment = NGUIText.Alignment.Right;
                    break;
                case TextAnchor.LowerLeft:
                    label.pivot = UIWidget.Pivot.BottomLeft;
                    label.alignment = NGUIText.Alignment.Left;
                    break;
                case TextAnchor.LowerCenter:
                    label.pivot = UIWidget.Pivot.Bottom;
                    label.alignment = NGUIText.Alignment.Center;
                    break;
                case TextAnchor.LowerRight:
                    label.pivot = UIWidget.Pivot.BottomRight;
                    label.alignment = NGUIText.Alignment.Right;
                    break;
            }
        }
    }
}
