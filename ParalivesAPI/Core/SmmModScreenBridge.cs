using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModAPI.Core;
using UnityEngine;

namespace ParalivesAPI.Core
{
    internal static class SmmModScreenBridge
    {
        private const string ShadowPrefix = "SMM__";
        private const string ShadowExtension = ".mod";
        private const string MarkerFileName = "SMMModId.smm";

        private static bool _started;
        private static bool _syncInProgress;
        private static float _lastSyncTime = -1000f;
        private static GameObject _runner;

        internal static void Start()
        {
            if (_started)
                return;

            _started = true;
            ModRuntime.PluginsActivated += SyncNow;
            EnsureRunner();
        }

        internal static void SyncNow()
        {
            SyncNow(force: false);
        }

        internal static void SyncNow(bool force)
        {
            if (_syncInProgress)
                return;

            if (!force && Time.unscaledTime - _lastSyncTime < 2f)
                return;

            _syncInProgress = true;
            try
            {
                _lastSyncTime = Time.unscaledTime;
                string reason;
                int synced = SyncShadowMods(out reason);
                if (synced >= 0)
                    MMLog.WriteInfo("[ParalivesAPI] Synced " + synced + " SMM mod(s) into the Paralives mods screen.");
                else if (!string.IsNullOrEmpty(reason))
                    MMLog.WriteDebug("[ParalivesAPI] SMM mods screen sync skipped: " + reason);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ParalivesAPI] SMM mods screen sync failed: " + ex.Message);
            }
            finally
            {
                _syncInProgress = false;
            }
        }

        internal static bool TryGetSmmModId(AssetMod assetMod, out string modId)
        {
            modId = null;
            if (assetMod == null || string.IsNullOrEmpty(assetMod.FilePath))
                return false;

            string markerPath = Path.Combine(assetMod.FilePath, MarkerFileName);
            if (!File.Exists(markerPath))
                return false;

            modId = File.ReadAllText(markerPath).Trim();
            return !string.IsNullOrEmpty(modId);
        }

        internal static bool TrySetSmmModEnabled(AssetMod assetMod, bool enabled)
        {
            string modId;
            if (!TryGetSmmModId(assetMod, out modId))
                return false;

            string gameRoot = GetGameRoot();
            var store = new SmmLoadOrderStore(gameRoot);
            bool updated = store.SetEnabled(modId, enabled);
            if (updated)
            {
                MMLog.WriteInfo("[ParalivesAPI] " + (enabled ? "Enabled" : "Disabled")
                    + " SMM mod '" + modId + "' in loadorder.json. Restart the game for runtime load changes.");
            }

            return updated;
        }

        private static void EnsureRunner()
        {
            if (_runner != null)
                return;

            _runner = new GameObject("ParalivesSmmModScreenBridge");
            UnityEngine.Object.DontDestroyOnLoad(_runner);
            _runner.AddComponent<SmmModScreenBridgeRunner>();
        }

        private static int SyncShadowMods(out string reason)
        {
            reason = null;

            if (global::ModManager.Instance == null)
            {
                reason = "ModManager.Instance is not ready";
                return -1;
            }

            if (global::AssetManager.Instance == null)
            {
                reason = "AssetManager.Instance is not ready";
                return -1;
            }

            List<ModEntry> discovered = ModRuntime.DiscoverAllMods();
            if (discovered == null)
                discovered = new List<ModEntry>();

            string gameRoot = GetGameRoot();
            var store = new SmmLoadOrderStore(gameRoot);
            var discoveredIds = new List<string>();
            for (int i = 0; i < discovered.Count; i++)
            {
                if (discovered[i] != null && !string.IsNullOrEmpty(discovered[i].Id))
                    discoveredIds.Add(discovered[i].Id);
            }

            Dictionary<string, bool> enabledById = store.ReadEnabledMap(discoveredIds);
            int synced = 0;
            for (int i = 0; i < discovered.Count; i++)
            {
                ModEntry entry = discovered[i];
                if (entry == null || string.IsNullOrEmpty(entry.Id))
                    continue;

                bool enabled = true;
                bool foundState = enabledById.TryGetValue(entry.Id, out enabled);
                if (!foundState)
                    enabled = true;

                if (SyncShadowMod(entry, enabled))
                    synced++;
            }

            return synced;
        }

        private static bool SyncShadowMod(ModEntry entry, bool enabled)
        {
            string folderPath = GetShadowFolderPath(entry.Id);
            Directory.CreateDirectory(folderPath);

            File.WriteAllText(Path.Combine(folderPath, MarkerFileName), entry.Id, Encoding.UTF8);
            WriteMetaFile(folderPath, entry, enabled);

            ulong guid = GetStableGuid(entry.Id);
            if (!global::ModManager.Instance.Mods.Contains(guid))
            {
                if (!global::AssetManager.Instance.HasAsset(guid))
                    global::ModManager.Instance.LoadExistingMod(folderPath.Replace("\\", "/"));
                else
                    global::ModManager.Instance.Mods.Add(guid);
            }

            UIMods.Dirty = true;
            return true;
        }

        private static void WriteMetaFile(string folderPath, ModEntry entry, bool enabled)
        {
            string folderName = Path.GetFileName(folderPath);
            string metaPath = Path.Combine(folderPath, folderName + ".meta");
            string authors = entry.About != null && entry.About.authors != null
                ? string.Join(", ", entry.About.authors)
                : string.Empty;
            string description = entry.About != null ? (entry.About.description ?? string.Empty) : string.Empty;

            using (var writer = new StreamWriter(metaPath, false, Encoding.UTF8))
            {
                writer.WriteLine("GUID:{0}", GetStableGuid(entry.Id));
                writer.WriteLine("Type:401");
                writer.WriteLine("ModName:{0}", "SMM: " + SafeDisplayName(entry));
                writer.WriteLine("Enabled:{0}", enabled ? "True" : "False");
                writer.WriteLine("IsSystemMod:False");
                writer.WriteLine("CreationTime:{0}", DateTime.UtcNow.Ticks);
                writer.WriteLine("LastEditTime:{0}", DateTime.UtcNow.Ticks);
                writer.WriteLine("LastUploadTime:0");
                writer.WriteLine("IsFromWorkshop:False");
                writer.WriteLine("PublishedFileId:0");
                writer.WriteLine("CreatorId:{0}", authors);
                writer.WriteLine("WorkshopDescription:{0}", "SMM DLL mod. " + description);
            }
        }

        private static string SafeDisplayName(ModEntry entry)
        {
            if (entry == null)
                return "Unknown";

            if (!string.IsNullOrEmpty(entry.Name))
                return entry.Name;

            return entry.Id ?? "Unknown";
        }

        private static string GetShadowFolderPath(string modId)
        {
            return Path.Combine(Application.persistentDataPath, ShadowPrefix + SanitizeFileName(modId) + ShadowExtension);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "unknown";

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isInvalid = false;
                for (int j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        isInvalid = true;
                        break;
                    }
                }

                builder.Append(isInvalid ? '_' : c);
            }

            return builder.ToString();
        }

        private static ulong GetStableGuid(string modId)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;
            string value = (modId ?? string.Empty).ToLowerInvariant();
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= prime;
            }

            if (hash == 0UL || hash == global::ModManager.MainModGUID)
                hash += 1000003UL;

            return hash;
        }

        private static string GetGameRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private sealed class SmmModScreenBridgeRunner : MonoBehaviour
        {
            private float _timer;
            private bool _syncedOnce;

            private void Update()
            {
                _timer += Time.unscaledDeltaTime;
                if (_timer < 1f)
                    return;

                _timer = 0f;
                if (!_syncedOnce)
                {
                    SyncNow(force: true);
                    _syncedOnce = global::ModManager.Instance != null && global::AssetManager.Instance != null;
                }
            }
        }
    }
}
