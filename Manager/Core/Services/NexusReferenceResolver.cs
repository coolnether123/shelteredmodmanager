using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    /// <summary>
    /// Resolves Nexus references for local mods from metadata and URLs.
    /// </summary>
    public class NexusReferenceResolver
    {
        private static readonly Regex NexusUrlRegex =
            new Regex(@"nexusmods\.com/(?<domain>[^/\s]+)/mods/(?<id>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        [Serializable]
        private sealed class NexusSidecar
        {
            public string gameDomain;
            public int modId;
        }

        public NexusModReference Resolve(ModItem mod, string fallbackGameDomain)
        {
            if (mod == null)
                return null;

            if (mod.NexusModId > 0 && !string.IsNullOrEmpty(mod.NexusGameDomain))
            {
                return new NexusModReference
                {
                    GameDomain = NormalizeDomain(mod.NexusGameDomain),
                    ModId = mod.NexusModId
                };
            }

            string domainFromWebsite;
            int modIdFromWebsite;
            if (TryResolveFromWebsite(mod.Website, out domainFromWebsite, out modIdFromWebsite))
            {
                return new NexusModReference
                {
                    GameDomain = NormalizeDomain(domainFromWebsite),
                    ModId = modIdFromWebsite
                };
            }

            string sidecarDomain;
            int sidecarModId;
            if (TryResolveFromSidecar(mod.RootPath, out sidecarDomain, out sidecarModId))
            {
                return new NexusModReference
                {
                    GameDomain = NormalizeDomain(!string.IsNullOrEmpty(sidecarDomain) ? sidecarDomain : fallbackGameDomain),
                    ModId = sidecarModId
                };
            }

            if (mod.NexusModId > 0 && !string.IsNullOrEmpty(fallbackGameDomain))
            {
                return new NexusModReference
                {
                    GameDomain = NormalizeDomain(fallbackGameDomain),
                    ModId = mod.NexusModId
                };
            }

            return null;
        }

        public bool TryResolveFromWebsite(string website, out string gameDomain, out int modId)
        {
            gameDomain = null;
            modId = 0;

            if (string.IsNullOrEmpty(website))
                return false;

            var match = NexusUrlRegex.Match(website);
            if (!match.Success)
                return false;

            gameDomain = NormalizeDomain(match.Groups["domain"].Value);
            int parsed;
            if (!int.TryParse(match.Groups["id"].Value, out parsed))
                return false;

            modId = parsed;
            return !string.IsNullOrEmpty(gameDomain) && modId > 0;
        }

        private bool TryResolveFromSidecar(string rootPath, out string gameDomain, out int modId)
        {
            gameDomain = null;
            modId = 0;

            if (string.IsNullOrEmpty(rootPath))
                return false;

            try
            {
                var sidecarPath = Path.Combine(Path.Combine(rootPath, "About"), "Nexus.json");
                if (!File.Exists(sidecarPath))
                    return false;

                var text = File.ReadAllText(sidecarPath);
                var sidecar = new JavaScriptSerializer().Deserialize<NexusSidecar>(text);
                if (sidecar == null || sidecar.modId <= 0)
                    return false;

                gameDomain = NormalizeDomain(sidecar.gameDomain);
                modId = sidecar.modId;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeDomain(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Trim().ToLowerInvariant();
        }
    }
}
