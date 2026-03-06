using System;
using System.Collections.Generic;

namespace GameModding.Shared.Restart
{
    [Serializable]
    public sealed class RestartLoadManifestRef
    {
        public string ManifestPath;
    }

    [Serializable]
    public sealed class RestartRequest
    {
        public string Action;
        public string LoadFromManifest;
        public RestartLoadManifestRef LoadManifest;

        public string ResolveManifestPath()
        {
            if (LoadManifest != null && !string.IsNullOrEmpty(LoadManifest.ManifestPath))
            {
                return LoadManifest.ManifestPath;
            }

            return LoadFromManifest ?? string.Empty;
        }
    }

    public sealed class RestartDecision
    {
        public bool DeleteRequestFile;
        public bool UpdateLoadOrder;
        public bool ShouldLaunch;
        public bool BlockedByValidation;
        public string StatusMessage;
        public string ValidationMessage;
        public List<string> LoadOrder;

        public RestartDecision()
        {
            DeleteRequestFile = true;
            UpdateLoadOrder = false;
            ShouldLaunch = false;
            BlockedByValidation = false;
            StatusMessage = string.Empty;
            ValidationMessage = string.Empty;
            LoadOrder = new List<string>();
        }
    }
}
