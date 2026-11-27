using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Manager
{
    /// <summary>
    /// Manager-local About.json reader (no ModAPI dependency).
    /// Centralizes parsing/normalization and preview detection for mods.
    /// </summary>
    internal static class ModAboutReader
    {
        internal static bool TryLoad(string modDirectory, out ModTypes.ModAboutInfo about, out string normalizedId, out string displayName, out string previewPath, bool propagateIoErrors = false)
        {
            about = null;
            normalizedId = null;
            displayName = Path.GetFileName(modDirectory);
            previewPath = null;

            try
            {
                var aboutJson = Path.Combine(modDirectory ?? string.Empty, "About\\About.json");
                if (!propagateIoErrors && !File.Exists(aboutJson)) return false;
                var text = File.ReadAllText(aboutJson);
                about = new JavaScriptSerializer().Deserialize<ModTypes.ModAboutInfo>(text);
                if (about == null) return false;

                normalizedId = NormalizeId(about.id, displayName);
                displayName = string.IsNullOrEmpty(about.name) ? displayName : about.name;

                var preview = Path.Combine(modDirectory, "About\\preview.png");
                if (File.Exists(preview)) previewPath = preview;
                return true;
            }
            catch (Exception ex)
            {
                if (propagateIoErrors && ((ex is IOException && !(ex is FileNotFoundException) &&
                    !(ex is DirectoryNotFoundException)) || ex is UnauthorizedAccessException))
                    throw;
                return false;
            }
        }

        private static string NormalizeId(string rawId, string fallbackName)
        {
            var candidate = string.IsNullOrEmpty(rawId) ? fallbackName : rawId;
            return (candidate ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
