using System;
using System.IO;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Scenarios.Application.Selection
{
    /// <summary>
    /// Resolves filesystem and save timestamps for the scenario library. Keeping this
    /// outside presentation code makes catalog refresh the only disk-enumeration seam.
    /// </summary>
    internal static class ScenarioLibraryMetadata
    {
        public static DateTime? ReadScenarioCreatedUtc(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return null;

            try
            {
                return File.Exists(scenarioFilePath) ? NormalizeUtc(File.GetCreationTimeUtc(scenarioFilePath)) : null;
            }
            catch
            {
                return null;
            }
        }

        public static DateTime? ReadInstalledUtc(string scenarioFilePath)
        {
            if (string.IsNullOrEmpty(scenarioFilePath))
                return null;

            try
            {
                string folder = Path.GetDirectoryName(scenarioFilePath);
                return !string.IsNullOrEmpty(folder) && Directory.Exists(folder)
                    ? NormalizeUtc(Directory.GetCreationTimeUtc(folder))
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public static DateTime? ReadLastPlayedUtc(SaveEntry[] saves)
        {
            DateTime? newest = null;
            for (int i = 0; saves != null && i < saves.Length; i++)
            {
                SaveEntry save = saves[i];
                if (save == null)
                    continue;

                DateTime stamp;
                string raw = !string.IsNullOrEmpty(save.updatedAt) ? save.updatedAt : save.createdAt;
                if (!TryParseUtc(raw, out stamp))
                    continue;

                if (!newest.HasValue || stamp > newest.Value)
                    newest = stamp;
            }

            return newest;
        }

        private static bool TryParseUtc(string raw, out DateTime value)
        {
            value = DateTime.MinValue;
            if (string.IsNullOrEmpty(raw))
                return false;

            DateTimeOffset offset;
            if (DateTimeOffset.TryParse(raw, out offset))
            {
                value = offset.UtcDateTime;
                return true;
            }

            DateTime parsed;
            if (!DateTime.TryParse(raw, out parsed))
                return false;

            value = parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
            return true;
        }

        private static DateTime? NormalizeUtc(DateTime value)
        {
            if (value == DateTime.MinValue || value.Year <= 1601)
                return null;
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }
    }
}
