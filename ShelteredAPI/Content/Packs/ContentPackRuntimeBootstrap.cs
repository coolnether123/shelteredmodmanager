using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredModManager.ContentPacks;

namespace ShelteredAPI.Content.Packs
{
    internal static class ContentPackRuntimeBootstrap
    {
        private static readonly object Sync = new object();
        private static readonly HashSet<string> LoadedMods =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly ContentPackLoader Loader = new ContentPackLoader();
        private static bool _subscribed;

        public static void EnsureSubscribed()
        {
            lock (Sync)
            {
                if (_subscribed)
                    return;

                ModRuntime.ModActivating += OnModActivating;
                _subscribed = true;
            }
        }

        private static void OnModActivating(ModEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id))
                return;

            lock (Sync)
            {
                if (LoadedMods.Contains(entry.Id))
                    return;
            }

            ContentPackLoadResult result = Loader.Load(entry);
            if (!result.Found)
                return;

            LogIssues(entry.Id, result);
            if (!result.Success)
            {
                MMLog.WriteError("[ContentPack] Rejected '" + entry.Id + "': " + result.ErrorMessage);
                return;
            }

            lock (Sync)
                LoadedMods.Add(entry.Id);

            MMLog.WriteInfo("[ContentPack] Registered '" + entry.Id + "' with "
                + result.ItemCount + " item(s) and " + result.RecipeCount + " recipe(s).");
        }

        private static void LogIssues(string modId, ContentPackLoadResult result)
        {
            for (int i = 0; result != null && i < result.Issues.Count; i++)
            {
                ContentPackValidationIssue issue = result.Issues[i];
                if (issue == null)
                    continue;

                string message = "[ContentPack] " + modId + " " + issue.Path + ": " + issue.Message;
                if (issue.Severity == ContentPackValidationSeverity.Error)
                    MMLog.WriteError(message);
                else
                    MMLog.WriteWarning(message);
            }
        }
    }
}
