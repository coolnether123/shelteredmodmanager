using UnityEngine;


using ShelteredAPI.UI.FieldManual.Tooltips;
namespace ShelteredAPI.UI.FieldManual.Frame
{
    /// <summary>
    /// Output of <see cref="IPanelFrame.Build"/>. Exposes the GameObject anchors a panel
    /// orchestrator needs (header for title overlays, content for scrollable widgets,
    /// footer for action buttons) without leaking the frame's internal structure.
    /// </summary>
    internal sealed class PanelFrameRegions
    {
        public readonly GameObject Root;
        public readonly GameObject HeaderRoot;
        public readonly GameObject ContentRoot;
        public readonly GameObject FooterRoot;
        public readonly Rect ContentRectLocal; // (x,y) = bottom-left in local panel coords; w,h = available content size

        public PanelFrameRegions(GameObject root, GameObject headerRoot, GameObject contentRoot, GameObject footerRoot, Rect contentRectLocal)
        {
            Root = root;
            HeaderRoot = headerRoot;
            ContentRoot = contentRoot;
            FooterRoot = footerRoot;
            ContentRectLocal = contentRectLocal;
        }
    }
}
