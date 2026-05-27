using System.Collections.Generic;
using System.IO;
using Manager.Core.Games.Models;
using Manager.Core.Models;

namespace Manager.Core.Games.Services
{
    public sealed class GamePreflightService
    {
        public List<string> GetMissingRuntimeFiles(GameProfile profile, AppSettings settings)
        {
            List<string> missing = new List<string>();
            if (profile == null || settings == null || !settings.IsGamePathValid)
                return missing;

            string gameDir = Path.GetDirectoryName(settings.GamePath);
            RuntimeFileRequirement[] requirements = profile.RequiredRuntimeFiles ?? new RuntimeFileRequirement[0];
            for (int i = 0; i < requirements.Length; i++)
            {
                RuntimeFileRequirement requirement = requirements[i];
                if (requirement == null || HasAnyCandidate(gameDir, requirement.RelativeCandidates))
                    continue;

                missing.Add(requirement.DisplayName);
            }

            return missing;
        }

        private static bool HasAnyCandidate(string gameDir, string[] relativeCandidates)
        {
            if (relativeCandidates == null || relativeCandidates.Length == 0)
                return true;

            for (int i = 0; i < relativeCandidates.Length; i++)
            {
                string relative = relativeCandidates[i];
                if (string.IsNullOrEmpty(relative))
                    continue;

                string candidate = Path.Combine(gameDir, relative);
                if (File.Exists(candidate))
                    return true;
            }

            return false;
        }
    }
}
