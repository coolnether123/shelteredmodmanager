using System;

namespace Cortex.Plugins.Abstractions
{
    /// <summary>
    /// Provides a shared workbench UI surface for common chrome and property-page controls.
    /// Modules should prefer this surface for common layouts so Cortex can evolve the backend without
    /// forcing every module to rewrite the same IMGUI patterns.
    /// </summary>
    public interface IWorkbenchUiSurface
    {
        /// <summary>
        /// Draws a standard search toolbar and returns the updated draft query.
        /// </summary>
        string DrawSearchToolbar(string label, string draftQuery, float height, bool expandWidth);

        /// <summary>
        /// Draws a navigation group header button.
        /// </summary>
        bool DrawNavigationGroupHeader(string title, bool isActive, bool isExpanded);

        /// <summary>
        /// Draws a navigation item button.
        /// </summary>
        bool DrawNavigationItem(string title, bool isSelected, float indent);

        /// <summary>
        /// Draws the active child label for a collapsed navigation group.
        /// </summary>
        void DrawCollapsedNavigationItem(string title, float indent);

        /// <summary>
        /// Draws a document-style section header and optional description.
        /// </summary>
        void DrawSectionHeader(string title, string description);

        /// <summary>
        /// Draws a standard section panel with Cortex property-page chrome.
        /// </summary>
        void DrawSectionPanel(string title, Action drawBody);

        /// <summary>
        /// Draws a shared popup-style menu panel anchored inline by the caller.
        /// </summary>
        void DrawPopupMenuPanel(float width, Action drawBody);

        /// <summary>
        /// Begins a standard property row container with host-defined hover chrome.
        /// </summary>
        void BeginPropertyRow();

        /// <summary>
        /// Ends a standard property row container and finalizes host-defined hover chrome.
        /// </summary>
        void EndPropertyRow();

        /// <summary>
        /// Draws the label/description column for a property row.
        /// </summary>
        void DrawPropertyLabelColumn(string title, string description);
    }
}
