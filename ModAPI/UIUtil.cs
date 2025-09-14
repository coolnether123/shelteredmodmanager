using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NGUI helpers for cloning, labeling, spacing, click blocking, and creating
/// overlay/labels with sane defaults (panel, font, depth, anchors).
/// </summary>
public static class UIUtil
{
    private static readonly Stack<GameObject> _clickBlockers =
        new Stack<GameObject>();

    /// <summary>
    /// Clones a UIButton template under the specified parent, strips UILocalize and UIButtonMessage,
    /// clears UIButton.onClick, and sets all UILabels to the provided text.
    /// Returns the new UIButton instance (or null on failure).
    /// </summary>
    public static UIButton CloneButton(UIButton template, Transform parent,
        string text)
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

        var locals = cloneGO.GetComponentsInChildren<UILocalize>(true);
        for (int i = 0; i < locals.Length; i++)
            if (locals[i] != null) UnityEngine.Object.Destroy(locals[i]);

        var msgs = cloneGO.GetComponentsInChildren<UIButtonMessage>(true);
        for (int i = 0; i < msgs.Length; i++)
            if (msgs[i] != null) UnityEngine.Object.Destroy(msgs[i]);

        var btn = cloneGO.GetComponent<UIButton>();
        if (btn != null && btn.onClick != null) btn.onClick.Clear();

        SetAllLabels(cloneGO, text);
        return btn;
    }

    /// Measures the local Y delta from A to B. Useful for stacking clones vertically.
    /// </summary>
    public static float MeasureVerticalSpacing(UIButton a, UIButton b)
    {
        if (a == null || b == null) return 0f;
        return b.transform.localPosition.y - a.transform.localPosition.y;
    }

    /// Sets all UILabels under the GameObject to the specified text.
    /// </summary>
    public static void SetAllLabels(GameObject go, string text)
    {
        if (go == null) return;
        var labels = go.GetComponentsInChildren<UILabel>(true);
        for (int i = 0; i < labels.Length; i++)
            if (labels[i] != null) labels[i].text = text;
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

    /// Pops and destroys the most recent click blocker if present.
    /// </summary>
    public static void PopClickBlocker()
    {
        if (_clickBlockers.Count == 0) return;
        var go = _clickBlockers.Pop();
        if (go != null) UnityEngine.Object.Destroy(go);
    }

    public static GameObject CloneAndReposition(GameObject template,
        Vector3 localOffset, Transform parent = null)
    {
        if (template == null) return null;

        var clone = UnityEngine.Object.Instantiate(template) as GameObject;
        if (clone == null) return null;

        var targetParent = parent != null ? parent : template.transform.parent;
        if (targetParent != null)
        {
            clone.transform.SetParent(targetParent, false);
            clone.layer = targetParent.gameObject.layer;
        }

        clone.name = template.name + "_Clone";
        clone.transform.localScale = template.transform.localScale;
        clone.transform.localRotation = template.transform.localRotation;
        clone.transform.localPosition =
            template.transform.localPosition + localOffset;
        return clone;
    }

    public static T CloneAndReposition<T>(T template, Vector3 localOffset,
        Transform parent = null) where T : Component
    {
        if (template == null) return null;
        var go = template.gameObject;
        var cloneGo = CloneAndReposition(go, localOffset, parent);
        return cloneGo != null ? cloneGo.GetComponent<T>() : null;
    }

    // --------------------------------------------------------------------------
    // New broad helpers for labels/panels/fonts/anchoring
    // --------------------------------------------------------------------------

    public enum AnchorCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center
    }

    /// <summary>
    /// Options for UIUtil.CreateLabel that control text, font, color, size,
    /// alignment, depth, and on‑screen placement.
    ///
    /// Typical usage:
    ///   UIPanel used;
    ///   var opts = new UIUtil.UILabelOptions {
    ///       text = "Hello World",
    ///       color = Color.cyan,
    ///       fontSize = 20,
    ///       alignment = NGUIText.Alignment.Center,
    ///       anchor = UIUtil.AnchorCorner.TopRight,
    ///       pixelOffset = new Vector2(-10, -10)
    ///   };
    ///   UIUtil.CreateLabel(parentGameObject, opts, out used);
    ///
    /// This object can be reused or shared; missing/unspecified values are
    /// assigned sensible defaults by UIUtil.CreateLabel.
    /// </summary>
    [Serializable]
    public class UILabelOptions
    {
        public string text = "Label";
        public Color color = Color.white;
        public int fontSize = 22;
        public NGUIText.Alignment alignment = NGUIText.Alignment.Left;
        public UILabel.Effect effect = UILabel.Effect.Outline;
        public Color effectColor = new Color(0f, 0f, 0f, 0.9f);

        // Layout
        public AnchorCorner anchor = AnchorCorner.TopRight;
        public Vector2 pixelOffset = new Vector2(-10, -10);

        // Depth
        public int relativeDepth = 10; // added on top of current max
        public int? absoluteDepth = null; // exact depth if set

        // Fonts (optional)
        public string uiFontName = null; // name of UIFont to use if found
        public string trueTypeFontName = null; // fallback TTF name (Arial by
                                               // default)

        // Misc
        public bool resizeFreely = true;
    }

    /// <summary>
    /// Create an NGUI UILabel under 'parent' with sensible defaults:
    /// - Ensures it's under a UIPanel (creates one if necessary)
    /// - Chooses a font (copy nearby UILabel, else UIFont by name, else TTF)
    /// - Sets depth above existing widgets
    /// - Positions using UIRoot's virtual coordinates by anchor + pixelOffset
    /// Returns the created UILabel (or null on failure).
    /// </summary>
    public static UILabel CreateLabel(GameObject parent, UILabelOptions opts,
        out UIPanel usedPanel)
    {
        usedPanel = null;
        if (parent == null) return null;
        if (opts == null) opts = new UILabelOptions();

        // Ensure we are under a UIPanel
        var panel = NGUITools.FindInParents<UIPanel>(parent);
        if (panel == null)
        {
            panel = parent.GetComponent<UIPanel>();
            if (panel == null) panel = parent.AddComponent<UIPanel>();
        }
        usedPanel = panel;

        var uiRoot = NGUITools.FindInParents<UIRoot>(panel != null
            ? panel.gameObject
            : parent) ?? UnityEngine.Object.FindObjectOfType<UIRoot>();

        // Create child for the label
        var go = new GameObject("UIUtil_Label");
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

        // Font choice: 1) by name 2) copy nearby UILabel 3) any UILabel 4) TTF
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
            label.trueTypeFont = null;
        }
        else
        {
            if (chosenTTF == null)
                chosenTTF = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.trueTypeFont = chosenTTF;
            label.bitmapFont = null;
        }

        // Depth above others
        int targetDepth = opts.absoluteDepth.HasValue
            ? opts.absoluteDepth.Value
            : ComputeSafeDepth(panel, opts.relativeDepth);
        label.depth = targetDepth;

        // Position by anchor using UIRoot coords
        var widget = label.GetComponent<UIWidget>();
        if (widget != null) widget.pivot = PivotFor(opts.anchor);

        var pos = ComputeAnchorPosition(uiRoot, opts.anchor, opts.pixelOffset);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one;

        Debug.Log("[UIUtil] CreateLabel -> parent=" + parent.name +
                  " panel=" + (panel != null ? panel.name : "<none>") +
                  " depth=" + targetDepth +
                  " anchor=" + opts.anchor +
                  " pos=" + pos.ToString("F1") +
                  " font=" + (label.bitmapFont != null
                                 ? "UIFont:" + label.bitmapFont.name
                                 : ("TTF:" + (label.trueTypeFont != null
                                                   ? label.trueTypeFont.name
                                                   : "<null>"))));

        return label;
    }

    /// <summary>
    /// Ensure a very-high-depth overlay UIPanel under UIRoot suitable for
    /// temporary mod overlays. Reuses existing if present.
    /// </summary>
    public static UIPanel EnsureOverlayPanel(string name = "ModAPI_OverlayPanel",
        int depth = 50000)
    {
        var root = UnityEngine.Object.FindObjectOfType<UIRoot>();
        if (root == null)
        {
            Debug.LogWarning("[UIUtil] EnsureOverlayPanel: no UIRoot in scene.");
            return null;
        }

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

    /// <summary>
    /// Compute a safe depth on a panel: max widget depth + relativeAdd.
    /// </summary>
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

    public static UIFont FindUIFont(string nameContains)
    {
        if (string.IsNullOrEmpty(nameContains)) return null;
        try
        {
            var fonts = Resources.FindObjectsOfTypeAll(typeof(UIFont)) as UIFont[];
            if (fonts != null)
            {
                for (int i = 0; i < fonts.Length; i++)
                {
                    var f = fonts[i];
                    if (f != null && f.name.IndexOf(nameContains,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        return f;
                }
            }
        }
        catch { }
        return null;
    }

    // --- internals -----------------------------------------------------------

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

    private static Vector3 ComputeAnchorPosition(UIRoot root, AnchorCorner a,
        Vector2 inset)
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
            default: x = 0f; y = 0f; break;
        }
        return new Vector3(x + inset.x, y + inset.y, 0f);
    }
}