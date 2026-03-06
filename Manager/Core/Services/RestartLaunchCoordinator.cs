using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Manager.Core.Models;
using GameModding.Shared.Mods;
using GameModding.Shared.Restart;

namespace Manager.Core.Services
{
    public sealed class RestartLaunchCoordinator
    {
        [Serializable]
        private sealed class ManagerLoadedModInfo
        {
            public string modId;
            public string version;
        }

        [Serializable]
        private sealed class ManagerSlotManifest
        {
            public ManagerLoadedModInfo[] lastLoadedMods;
        }

        private readonly LoadOrderService _loadOrderService;
        private readonly ModDiscoveryService _modDiscoveryService;

        public RestartLaunchCoordinator(LoadOrderService loadOrderService, ModDiscoveryService modDiscoveryService)
        {
            _loadOrderService = loadOrderService;
            _modDiscoveryService = modDiscoveryService;
        }

        public RestartDecision Evaluate(RestartRequest request, AppSettings settings)
        {
            var decision = new RestartDecision();
            if (request == null || !string.Equals(request.Action, "Restart", StringComparison.OrdinalIgnoreCase))
            {
                decision.StatusMessage = "Invalid restart request.";
                return decision;
            }

            var manifestPath = request.ResolveManifestPath();
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
            {
                decision.StatusMessage = "Restart manifest not found.";
                return decision;
            }

            ManagerSlotManifest manifest;
            try
            {
                var manifestJson = File.ReadAllText(manifestPath);
                manifest = new JavaScriptSerializer().Deserialize<ManagerSlotManifest>(manifestJson);
            }
            catch
            {
                decision.StatusMessage = "Restart manifest could not be parsed.";
                return decision;
            }

            var newOrder = new List<string>();
            var sourceMods = manifest != null ? manifest.lastLoadedMods : null;
            if (sourceMods != null)
            {
                for (var i = 0; i < sourceMods.Length; i++)
                {
                    if (!string.IsNullOrEmpty(sourceMods[i].modId))
                    {
                        newOrder.Add(sourceMods[i].modId);
                    }
                }
            }

            if (newOrder.Count > 0)
            {
                decision.UpdateLoadOrder = true;
                decision.LoadOrder.AddRange(newOrder);
            }

            if (newOrder.Count == 0)
            {
                decision.ShouldLaunch = true;
                decision.StatusMessage = "Restart request manifest contained no mod list - keeping current load order.";
                return decision;
            }

            var allMods = _modDiscoveryService.DiscoverMods(settings.ModsPath);
            var modInfos = new List<ModInfo>();
            for (var i = 0; i < allMods.Count; i++)
            {
                var mod = allMods[i];
                var info = new ModInfo();
                info.Id = mod.Id;
                info.Name = mod.DisplayName;
                info.RootPath = mod.RootPath;
                info.About = new ModAboutInfo
                {
                    id = mod.Id,
                    name = mod.DisplayName,
                    dependsOn = mod.DependsOn,
                    loadAfter = mod.LoadAfter,
                    loadBefore = mod.LoadBefore
                };
                modInfos.Add(info);
            }

            var evaluation = LoadOrderResolver.Evaluate(modInfos, newOrder);
            if (evaluation.MissingHardDependencies != null)
            {
                for (var i = 0; i < evaluation.MissingHardDependencies.Count; i++)
                {
                    var error = evaluation.MissingHardDependencies[i];
                    if (settings.SkipHarmonyDependencyCheck && error.ToLowerInvariant().Contains("harmony"))
                    {
                        continue;
                    }

                    var match = Regex.Match(error, @"^Mod '([^']*)' has a missing hard dependency:");
                    if (match.Success)
                    {
                        decision.ValidationMessage = "The save's mod list has dependency issues (missing mods or cycles).\n\nPlease review the load order before launching.";
                        decision.BlockedByValidation = true;
                        decision.ShouldLaunch = false;
                        return decision;
                    }
                }
            }

            if ((evaluation.HardIssues != null && evaluation.HardIssues.Count > 0) ||
                (evaluation.CycledModIds != null && evaluation.CycledModIds.Count > 0))
            {
                decision.ValidationMessage = "The save's mod list has dependency issues (missing mods or cycles).\n\nPlease review the load order before launching.";
                decision.BlockedByValidation = true;
                decision.ShouldLaunch = false;
                return decision;
            }

            decision.ShouldLaunch = true;
            decision.StatusMessage = "ModAPI restart request - relaunching...";
            return decision;
        }
    }
}
