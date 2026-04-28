using System;
using UnityEngine;

namespace ModAPI.UI
{
    /// <summary>
    /// Helpers for interacting with NGUI components in a safe way.
    /// </summary>
    public static class NGUIHelper
    {
        /// <summary>
        /// Finds the maximum depth currently used by widgets in a panel.
        /// Useful for ensuring new UI elements appear on top.
        /// </summary>
        public static int GetMaxDepth(UIPanel panel)
        {
            if (panel == null) return 0;
            
            int maxDepth = panel.depth;
            var widgets = panel.widgets;
            if (widgets != null)
            {
                for (int i = 0; i < widgets.size; i++)
                {
                    if (widgets[i] != null && widgets[i].depth > maxDepth)
                    {
                        maxDepth = widgets[i].depth;
                    }
                }
            }
            return maxDepth;
        }

        /// <summary>
        /// Sets the depth of a widget to be exactly one above the current maximum in its panel.
        /// </summary>
        public static void SetToTopDepth(UIWidget widget)
        {
            if (widget == null) return;
            UIPanel panel = widget.panel;
            if (panel == null) panel = NGUITools.FindInParents<UIPanel>(widget.gameObject);
            
            if (panel != null)
            {
                widget.depth = GetMaxDepth(panel) + 1;
            }
        }
    }
}
