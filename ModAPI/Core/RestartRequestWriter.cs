using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using GameModding.Shared.Restart;
using UnityEngine;

namespace ModAPI.Core
{
    public sealed class RestartRequestWriter : IRestartRequestWriter
    {
        [Serializable]
        private sealed class ManifestLoadedModInfo
        {
            public string modId;
            public string version;
        }

        [Serializable]
        private sealed class ManifestFile
        {
            public int manifestVersion;
            public string lastModified;
            public string family_name;
            public ManifestLoadedModInfo[] lastLoadedMods;
        }

        public bool WriteRequest(string manifestPath, out string restartPath, out string errorMessage)
        {
            restartPath = string.Empty;
            errorMessage = string.Empty;

            try
            {
                if (string.IsNullOrEmpty(manifestPath))
                {
                    errorMessage = "Manifest path is required.";
                    return false;
                }

                var request = new RestartRequest();
                request.Action = "Restart";
                request.LoadFromManifest = manifestPath;
                request.LoadManifest = new RestartLoadManifestRef { ManifestPath = manifestPath };

                restartPath = GetRestartRequestPath();
                var dir = Path.GetDirectoryName(restartPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = new JavaScriptSerializer().Serialize(request);
                File.WriteAllText(restartPath, json);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public bool WriteCurrentSessionRequest(out string restartPath, out string errorMessage)
        {
            restartPath = string.Empty;
            errorMessage = string.Empty;

            try
            {
                var manifestPath = BuildCurrentSessionManifest();
                return WriteRequest(manifestPath, out restartPath, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        private static string BuildCurrentSessionManifest()
        {
            var gameRoot = Directory.GetParent(Application.dataPath).FullName;
            var manifestPath = Path.Combine(Path.Combine(Path.Combine(gameRoot, "SMM"), "Bin"), "cortex_restart_manifest.json");
            var dir = Path.GetDirectoryName(manifestPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var mods = new List<ManifestLoadedModInfo>();
            var loadedMods = PluginManager.LoadedMods ?? new List<ModEntry>();
            for (var i = 0; i < loadedMods.Count; i++)
            {
                var mod = loadedMods[i];
                if (mod == null || string.IsNullOrEmpty(mod.Id))
                {
                    continue;
                }

                mods.Add(new ManifestLoadedModInfo
                {
                    modId = mod.Id,
                    version = mod.Version ?? string.Empty
                });
            }

            var manifest = new ManifestFile();
            manifest.manifestVersion = 1;
            manifest.lastModified = DateTime.UtcNow.ToString("o");
            manifest.family_name = "CortexRestart";
            manifest.lastLoadedMods = mods.ToArray();

            var json = new JavaScriptSerializer().Serialize(manifest);
            File.WriteAllText(manifestPath, json);
            return manifestPath;
        }

        private static string GetRestartRequestPath()
        {
            var gameRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(Path.Combine(Path.Combine(gameRoot, "SMM"), "Bin"), "restart.json");
        }
    }
}
