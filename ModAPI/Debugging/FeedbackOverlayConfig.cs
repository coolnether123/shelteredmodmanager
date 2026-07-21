using System;
using UnityEngine;

namespace ModAPI.Debugging
{
    /// <summary>Host-supplied settings for a reusable feedback overlay.</summary>
    public sealed class FeedbackOverlayConfig
    {
        public FeedbackOverlayConfig(string storageRootPath)
        {
            if (string.IsNullOrEmpty(storageRootPath))
                throw new ArgumentException("A feedback storage root is required.", "storageRootPath");

            StorageRootPath = storageRootPath;
            ToggleKey = KeyCode.F4;
            WindowTitle = "Developer Feedback";
            MaxLogExcerptLines = 80;
            MaxLogExcerptBytes = 65536;
        }

        /// <summary>Absolute or host-relative directory containing scratch, entries, and screenshots.</summary>
        public string StorageRootPath { get; private set; }

        /// <summary>Key used to show or hide the overlay. Defaults to F4.</summary>
        public KeyCode ToggleKey { get; set; }

        /// <summary>Title shown by the IMGUI window.</summary>
        public string WindowTitle { get; set; }

        /// <summary>Optional active runtime log whose tail is captured with each submission.</summary>
        public string RuntimeLogPath { get; set; }

        /// <summary>Maximum number of trailing log lines attached to one submission.</summary>
        public int MaxLogExcerptLines { get; set; }

        /// <summary>Maximum bytes read from the end of the active log for one submission.</summary>
        public int MaxLogExcerptBytes { get; set; }

        /// <summary>
        /// Optional host-owned input gate notified when the overlay changes visibility.
        /// ModAPI only reports its state; the host decides how to suppress game-specific input.
        /// </summary>
        public Action<bool> OverlayVisibilityChanged { get; set; }
    }
}
