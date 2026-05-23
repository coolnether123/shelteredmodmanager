using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Controls the inherited behavior removed when a visual UI template is cloned.
    /// Transform placement and widget visual values are always preserved.
    /// </summary>
    public sealed class UICloneOptions
    {
        /// <summary>Creates options with safe inherited-listener and button-handler clearing enabled.</summary>
        public UICloneOptions()
        {
            StripInheritedEventListeners = true;
            ClearButtonClickHandlers = true;
            IncludeChildren = true;
        }

        /// <summary>Gets or sets whether cloned UIEventListener callbacks are cleared.</summary>
        public bool StripInheritedEventListeners { get; set; }
        /// <summary>Gets or sets whether cloned UIButton click delegates are cleared.</summary>
        public bool ClearButtonClickHandlers { get; set; }
        /// <summary>Gets or sets whether behavior clearing includes the cloned child hierarchy.</summary>
        public bool IncludeChildren { get; set; }
        /// <summary>Gets or sets an optional explicit clone object name.</summary>
        public string CloneName { get; set; }
    }

    /// <summary>
    /// Specifies whether a new button callback replaces inherited callbacks or is appended.
    /// </summary>
    public enum UIButtonBindingMode
    {
        /// <summary>Clear existing button callbacks before installing the requested callback.</summary>
        Replace,
        /// <summary>Keep existing button callbacks and append the requested callback.</summary>
        Append
    }

    /// <summary>
    /// Best-effort result for UI operations whose target hierarchy may differ between game builds.
    /// </summary>
    public sealed class UIOperationResult
    {
        private readonly ReadOnlyCollection<string> _warnings;

        internal UIOperationResult(bool success, int affectedCount, IList<string> warnings)
        {
            Success = success;
            AffectedCount = affectedCount;
            _warnings = new ReadOnlyCollection<string>(new List<string>(warnings ?? new List<string>()));
        }

        /// <summary>Gets whether the requested operation had a valid target and ran.</summary>
        public bool Success { get; private set; }
        /// <summary>Gets the number of components changed by the operation.</summary>
        public int AffectedCount { get; private set; }
        /// <summary>Gets non-fatal warnings generated during best-effort processing.</summary>
        public ReadOnlyCollection<string> Warnings { get { return _warnings; } }
        /// <summary>Gets whether any non-fatal warnings were recorded.</summary>
        public bool HasWarnings { get { return _warnings.Count != 0; } }
    }

    /// <summary>
    /// Result of cloning a Unity/NGUI object for mod-owned interaction.
    /// </summary>
    public sealed class UICloneResult
    {
        private readonly ReadOnlyCollection<string> _warnings;

        internal UICloneResult(GameObject clone, int affectedCount, IList<string> warnings)
        {
            Clone = clone;
            AffectedCount = affectedCount;
            _warnings = new ReadOnlyCollection<string>(new List<string>(warnings ?? new List<string>()));
        }

        /// <summary>Gets the cloned Unity object, or null when cloning failed.</summary>
        public GameObject Clone { get; private set; }
        /// <summary>Gets whether Unity produced the requested clone.</summary>
        public bool Success { get { return Clone != null; } }
        /// <summary>Gets the number of inherited listener or button handler components cleared.</summary>
        public int AffectedCount { get; private set; }
        /// <summary>Gets non-fatal warnings generated while preparing the clone.</summary>
        public ReadOnlyCollection<string> Warnings { get { return _warnings; } }
        /// <summary>Gets whether any non-fatal warnings were recorded.</summary>
        public bool HasWarnings { get { return _warnings.Count != 0; } }
    }

    /// <summary>
    /// Opaque capture of current NGUI widget and color-tween state.
    /// Use <see cref="Restore"/> after temporary highlighting or panel teardown.
    /// </summary>
    public sealed class UIColorSnapshot
    {
        private readonly object _state;
        private readonly ReadOnlyCollection<string> _warnings;

        internal UIColorSnapshot(object state, int labelCount, int widgetCount, int tweenCount, IList<string> warnings)
        {
            _state = state;
            LabelCount = labelCount;
            WidgetCount = widgetCount;
            TweenCount = tweenCount;
            _warnings = new ReadOnlyCollection<string>(new List<string>(warnings ?? new List<string>()));
        }

        internal object State { get { return _state; } }

        /// <summary>Gets whether a snapshot state was captured.</summary>
        public bool Success { get { return _state != null; } }
        /// <summary>Gets the number of UILabel components represented by captured widget colors.</summary>
        public int LabelCount { get; private set; }
        /// <summary>Gets the number of UIWidget colors captured.</summary>
        public int WidgetCount { get; private set; }
        /// <summary>Gets the number of TweenColor values and endpoints captured.</summary>
        public int TweenCount { get; private set; }
        /// <summary>Gets non-fatal warnings generated while capturing the state.</summary>
        public ReadOnlyCollection<string> Warnings { get { return _warnings; } }
        /// <summary>Gets whether any non-fatal warnings were recorded.</summary>
        public bool HasWarnings { get { return _warnings.Count != 0; } }

        /// <summary>Restores the captured visible colors and color-tween state where targets still exist.</summary>
        public UIOperationResult Restore()
        {
            return ShelteredUI.RestoreColors(this);
        }
    }
}
