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
        }

        /// <summary>Absolute or host-relative directory containing scratch, entries, and screenshots.</summary>
        public string StorageRootPath { get; private set; }

        /// <summary>Key used to show or hide the overlay. Defaults to F4.</summary>
        public KeyCode ToggleKey { get; set; }

        /// <summary>Title shown by the IMGUI window.</summary>
        public string WindowTitle { get; set; }
    }
}
