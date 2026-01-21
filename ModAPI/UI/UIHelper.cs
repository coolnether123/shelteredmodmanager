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

        /// <summary>
        /// Adds a hover tooltip to a GameObject. The GameObject must have a collider for interaction.
        /// </summary>
        public static TooltipHelper AddTooltip(GameObject target, Transform root, string text, UIFont uiFont = null, Font ttfFont = null)
        {
            if (target == null || root == null) return null;
            
            var helper = target.GetComponent<TooltipHelper>() ?? target.AddComponent<TooltipHelper>();
            helper.Initialize(root, text, uiFont, ttfFont);
            return helper;
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

    /// <summary>
    /// Helper component that shows a tooltip when hovering over an element.
    /// Attach to any GameObject with a collider to show a tooltip on hover.
    /// </summary>
    public class TooltipHelper : MonoBehaviour
    {
        private static Texture2D _tooltipBgTexture;
        private GameObject _tooltipObj;
        private UILabel _tooltipLabel;
        private UITexture _tooltipBg;
        private bool _isHovering = false;
        private Transform _rootTransform;
        private string _tooltipText;
        private UIFont _uiFont;
        private Font _ttfFont;

        /// <summary>
        /// Initialize the tooltip with text and font settings.
        /// </summary>
        /// <param name="root">The root transform for the tooltip (usually the dialog root)</param>
        /// <param name="text">The tooltip text to display</param>
        /// <param name="uiFont">Optional UIFont to use</param>
        /// <param name="ttfFont">Optional TrueType font to use</param>
        public void Initialize(Transform root, string text, UIFont uiFont = null, Font ttfFont = null)
        {
            _rootTransform = root;
            _tooltipText = text;
            _uiFont = uiFont;
            _ttfFont = ttfFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            
            if (_tooltipBgTexture == null)
            {
                _tooltipBgTexture = new Texture2D(2, 2);
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        _tooltipBgTexture.SetPixel(x, y, Color.white);
                _tooltipBgTexture.Apply();
            }
        }

        private void OnHover(bool isOver)
        {
            _isHovering = isOver;
            if (!isOver && _tooltipObj != null)
            {
                Destroy(_tooltipObj);
                _tooltipObj = null;
            }
        }

        private void Update()
        {
            if (_isHovering && _tooltipObj == null)
            {
                CreateTooltip();
            }
        }

        private void LateUpdate()
        {
            if (_isHovering && _tooltipObj != null)
            {
                UpdateTooltipPosition();
            }
        }

        private void CreateTooltip()
        {
            _tooltipObj = new GameObject("Tooltip");
            _tooltipObj.transform.SetParent(_rootTransform, false);
            _tooltipObj.layer = _rootTransform.gameObject.layer;
            _tooltipObj.transform.localScale = Vector3.one;
            _tooltipObj.transform.localPosition = Vector3.zero;
            
            // Background
            var bgObj = new GameObject("TooltipBg");
            bgObj.transform.SetParent(_tooltipObj.transform, false);
            bgObj.layer = _tooltipObj.layer;
            _tooltipBg = bgObj.AddComponent<UITexture>();
            _tooltipBg.mainTexture = _tooltipBgTexture;
            _tooltipBg.color = new Color(0.1f, 0.08f, 0.06f, 0.95f);
            _tooltipBg.depth = 200;
            _tooltipBg.pivot = UIWidget.Pivot.Center;
            
            // Label
            var labelObj = new GameObject("TooltipLabel");
            labelObj.transform.SetParent(_tooltipObj.transform, false);
            labelObj.layer = _tooltipObj.layer;
            _tooltipLabel = labelObj.AddComponent<UILabel>();
            _tooltipLabel.text = _tooltipText;
            _tooltipLabel.fontSize = 14;
            _tooltipLabel.color = new Color(0.8f, 0.75f, 0.65f);
            _tooltipLabel.depth = 201;
            _tooltipLabel.pivot = UIWidget.Pivot.Center;
            _tooltipLabel.overflowMethod = UILabel.Overflow.ResizeFreely;
            _tooltipLabel.bitmapFont = _uiFont;
            _tooltipLabel.trueTypeFont = _ttfFont;
            
            // Size background to fit text
            int paddingX = 12;
            int paddingY = 8;
            _tooltipBg.width = (int)_tooltipLabel.printedSize.x + paddingX * 2;
            _tooltipBg.height = (int)_tooltipLabel.printedSize.y + paddingY * 2;
            
            // Both are centered, so no offset needed
            _tooltipLabel.transform.localPosition = Vector3.zero;
            bgObj.transform.localPosition = Vector3.zero;
        }

        private void UpdateTooltipPosition()
        {
            if (_tooltipObj == null) return;
            
            // Convert mouse position to UI coordinates
            var uiRoot = NGUITools.FindInParents<UIRoot>(_rootTransform.gameObject);
            if (uiRoot == null) return;
            
            float ratio = (float)uiRoot.activeHeight / Screen.height;
            float x = (Input.mousePosition.x - Screen.width * 0.5f) * ratio;
            float y = (Input.mousePosition.y - Screen.height * 0.5f) * ratio;
            
            // Offset tooltip from cursor to be above it
            // x is already center-aligned due to Pivot.Bottom
            y += 22; 
            
            _tooltipObj.transform.localPosition = new Vector3(x, y, 0);
        }

        private void OnDestroy()
        {
            if (_tooltipObj != null)
            {
                Destroy(_tooltipObj);
            }
        }
    }
}
