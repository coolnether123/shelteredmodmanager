using System;
using ModAPI.Core;
using ShelteredAPI.Input;
using ShelteredAPI.UI.FieldManual.Panels;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Entry point for the redesigned (V2) Sheltered Controls keybind panel.
    /// </summary>
    internal static class ShelteredKeybindsUIV2
    {
        private const string EntryId = "ShelteredAPI.Keybinds.V2";

        internal static event Action Closed;

        public static void Show()
        {
            var provider = ShelteredKeybindsProvider.Instance;
            provider.EnsureLoaded();
            MMLog.WriteInfo("[ShelteredKeybindsUIV2] Opening Sheltered book keybind panel.");

            var entry = new ModEntry
            {
                Id = EntryId,
                Name = "Sheltered Controls",
                Version = "1.0",
                SettingsProvider = provider
            };

            KeybindsPanelV2.Show(entry);
        }

        internal static void NotifyClosed()
        {
            Action handler = Closed;
            if (handler != null) handler();
        }
    }
}
