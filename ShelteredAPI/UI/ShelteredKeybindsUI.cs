using ModAPI.Core;
using ModAPI.UI;
using ShelteredAPI.Input;

namespace ShelteredAPI.UI
{
    /// <summary>
    /// Opens Sheltered input bindings inside the shared SMM settings window.
    /// </summary>
    public static class ShelteredKeybindsUI
    {
        private const string EntryId = "ShelteredAPI.Keybinds";

        public static void Show()
        {
            var provider = ShelteredKeybindsProvider.Instance;
            provider.EnsureLoaded();

            var entry = new ModEntry
            {
                Id = EntryId,
                Name = "Sheltered Controls",
                Version = "1.0",
                SettingsProvider = provider
            };

            ModSettingsPanel.Show(entry);
        }
    }
}
