using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Services.Onboarding;

namespace Cortex.Services.Settings
{
    internal sealed class SettingsLoadedModLinkService
    {
        private readonly CortexLoadedModSourceLinkService _sourceLinkService = new CortexLoadedModSourceLinkService();

        public string GetDraftValue(SettingsDraftState draftState, LoadedModInfo mod, IProjectCatalog projectCatalog)
        {
            if (draftState == null || mod == null || string.IsNullOrEmpty(mod.ModId))
            {
                return string.Empty;
            }

            string value;
            if (draftState.LoadedModPathDrafts.TryGetValue(mod.ModId, out value))
            {
                return value ?? string.Empty;
            }

            var existing = projectCatalog != null ? projectCatalog.GetProject(mod.ModId) : null;
            value = existing != null ? existing.SourceRootPath ?? string.Empty : string.Empty;
            draftState.LoadedModPathDrafts[mod.ModId] = value;
            return value;
        }

        public void LinkLoadedModToSource(
            SettingsDraftState draftState,
            LoadedModInfo mod,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService,
            CortexShellState shellState)
        {
            if (draftState == null || mod == null || string.IsNullOrEmpty(mod.ModId))
            {
                return;
            }

            var sourceRoot = GetDraftValue(draftState, mod, projectCatalog);
            var linkResult = _sourceLinkService.LinkLoadedModToSource(mod, sourceRoot, projectCatalog, workspaceService);
            if (shellState != null)
            {
                for (var i = 0; i < linkResult.Diagnostics.Length; i++)
                {
                    shellState.Diagnostics.Add(linkResult.Diagnostics[i]);
                }
            }

            if (!linkResult.Success || linkResult.Definition == null)
            {
                if (shellState != null)
                {
                    shellState.StatusMessage = linkResult.StatusMessage;
                }

                return;
            }

            if (shellState != null)
            {
                shellState.SelectedProject = projectCatalog.GetProject(mod.ModId) ?? linkResult.Definition;
                shellState.StatusMessage = "Linked loaded mod " + mod.ModId + " to " + (linkResult.Definition.SourceRootPath ?? string.Empty) + ".";
            }

            draftState.LoadedModPathDrafts[mod.ModId] = linkResult.Definition.SourceRootPath ?? sourceRoot;
        }

        public void ApplyLoadedModMappings(
            SettingsDraftState draftState,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService,
            ILoadedModCatalog loadedModCatalog,
            CortexShellState shellState)
        {
            if (draftState == null || projectCatalog == null || workspaceService == null || loadedModCatalog == null)
            {
                return;
            }

            var loadedMods = loadedModCatalog.GetLoadedMods();
            if (loadedMods == null || loadedMods.Count == 0)
            {
                return;
            }

            for (var i = 0; i < loadedMods.Count; i++)
            {
                var mod = loadedMods[i];
                if (mod == null || string.IsNullOrEmpty(mod.ModId))
                {
                    continue;
                }

                string draftValue;
                if (!draftState.LoadedModPathDrafts.TryGetValue(mod.ModId, out draftValue) || string.IsNullOrEmpty(draftValue))
                {
                    continue;
                }

                var existing = projectCatalog.GetProject(mod.ModId);
                if (existing != null &&
                    string.Equals(existing.SourceRootPath ?? string.Empty, draftValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var linkResult = _sourceLinkService.LinkLoadedModToSource(mod, draftValue, projectCatalog, workspaceService);
                if (shellState != null)
                {
                    for (var diagnosticIndex = 0; diagnosticIndex < linkResult.Diagnostics.Length; diagnosticIndex++)
                    {
                        shellState.Diagnostics.Add(linkResult.Diagnostics[diagnosticIndex]);
                    }
                }

                if (!linkResult.Success || linkResult.Definition == null)
                {
                    continue;
                }

                draftState.LoadedModPathDrafts[mod.ModId] = linkResult.Definition.SourceRootPath ?? draftValue;
                if (shellState != null &&
                    (shellState.SelectedProject == null || string.Equals(shellState.SelectedProject.ModId, mod.ModId, StringComparison.OrdinalIgnoreCase)))
                {
                    shellState.SelectedProject = projectCatalog.GetProject(mod.ModId) ?? linkResult.Definition;
                }
            }
        }
    }
}
