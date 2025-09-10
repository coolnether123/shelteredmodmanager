using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small NGUI helpers for cloning, labeling, spacing, and temporary click blocking.
/// These helpers avoid common pitfalls (UILocalize/UIButtonMessage remnants, stale onClick handlers).
/// </summary>
public static class UIUtil
{
    private static readonly Stack<GameObject> _clickBlockers = new Stack<GameObject>();

    /// <summary>
    /// Clones a UIButton template under the specified parent, strips UILocalize and UIButtonMessage,
    /// clears UIButton.onClick, and sets all UILabels to the provided text.
    /// Returns the new UIButton instance (or null on failure).
    /// </summary>
    public static UIButton CloneButton(UIButton template, Transform parent, string text)
    {
        if (template == null || parent == null) return null;

        var templateGO = template.gameObject;
        var cloneGO = Object.Instantiate(templateGO) as GameObject;
        if (cloneGO == null) return null;

        cloneGO.name = templateGO.name + "_Clone";
        cloneGO.transform.parent = parent;
        cloneGO.transform.localPosition = templateGO.transform.localPosition;
        cloneGO.transform.localRotation = templateGO.transform.localRotation;
        cloneGO.transform.localScale = templateGO.transform.localScale;
        cloneGO.layer = parent.gameObject.layer;

        // Remove UILocalize and UIButtonMessage components on the clone (they cause unwanted side effects)
        var locals = cloneGO.GetComponentsInChildren<UILocalize>(true);
        foreach (var l in locals) { if (l != null) Object.Destroy(l); }

        var msgs = cloneGO.GetComponentsInChildren<UIButtonMessage>(true);
        foreach (var m in msgs) { if (m != null) Object.Destroy(m); }

        var btn = cloneGO.GetComponent<UIButton>();
        if (btn != null && btn.onClick != null)
        {
            btn.onClick.Clear();
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
        foreach (var l in labels) { if (l != null) l.text = text; }
    }

    /// <summary>
    /// Adds a transparent child UIPanel with a big collider to swallow clicks.
    /// Returns the blocker GameObject for manual control, and also tracks it for PopClickBlocker.
    /// </summary>
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
        panel.alpha = 0f; // invisible

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = Vector3.zero;
        col.size = new Vector3(10000f, 10000f, 0.2f); // big enough to cover

        // Optional swallow via UIEventListener
        var lis = UIEventListener.Get(go);
        lis.onClick += (_)=>{}; // no-op, just ensure it registers

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
        if (go != null) Object.Destroy(go);
    }

    /// <summary>
    /// Clone a UI widget and reposition it by a local offset.
    /// - If parent is null, uses the template's parent.
    /// - Copies local rotation/scale and layer.
    /// Returns the cloned GameObject (or null if the template is null).
    /// </summary>
    public static GameObject CloneAndReposition(GameObject template, Vector3 localOffset, Transform parent = null)
    {
        if (template == null) return null;

        var clone = Object.Instantiate(template) as GameObject;
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
        clone.transform.localPosition = template.transform.localPosition + localOffset;

        return clone;
    }

    /// <summary>
    /// Generic version that returns the requested component type from the cloned object.
    /// Useful for list-based UIs (e.g., duplicate and offset an avatar or button widget).
    /// Example:
    ///   var extra = UIUtil.CloneAndReposition(memberAvatarTemplate, new Vector3(120, 0, 0));
    /// </summary>
    public static T CloneAndReposition<T>(T template, Vector3 localOffset, Transform parent = null) where T : Component
    {
        if (template == null) return null;
        var go = template.gameObject;
        var cloneGo = CloneAndReposition(go, localOffset, parent);
        return cloneGo != null ? cloneGo.GetComponent<T>() : null;
    }
}
