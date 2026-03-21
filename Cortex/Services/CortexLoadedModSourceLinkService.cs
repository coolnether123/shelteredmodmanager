using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class LoadedModSourceLinkResult
    {
        public readonly bool Success;
        public readonly CortexProjectDefinition Definition;
        public readonly string StatusMessage;
        public readonly string[] Diagnostics;

        public LoadedModSourceLinkResult(bool success, CortexProjectDefinition definition, string statusMessage, string[] diagnostics)
        {
            Success = success;
            Definition = definition;
            StatusMessage = statusMessage ?? string.Empty;
            Diagnostics = diagnostics ?? new string[0];
        }
    }

    internal sealed class CortexLoadedModSourceLinkService
    {
        public string SuggestSourceRoot(
            LoadedModInfo mod,
            string workspaceRootPath,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService)
        {
            if (mod == null)
            {
                return string.Empty;
            }

            var existing = projectCatalog != null ? projectCatalog.GetProject(mod.ModId) : null;
            if (existing != null && !string.IsNullOrEmpty(existing.SourceRootPath))
            {
                return existing.SourceRootPath;
            }

            var inferred = workspaceService != null ? workspaceService.FindLikelySourceRoot(mod.RootPath) : string.Empty;
            if (!string.IsNullOrEmpty(inferred))
            {
                return inferred;
            }

            return workspaceRootPath ?? string.Empty;
        }

        public LoadedModSourceLinkResult LinkLoadedModToSource(
            LoadedModInfo mod,
            string sourceRoot,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService)
        {
            if (mod == null || string.IsNullOrEmpty(mod.ModId))
            {
                return new LoadedModSourceLinkResult(false, null, "Loaded mod details are unavailable.", new string[0]);
            }

            if (projectCatalog == null || workspaceService == null)
            {
                return new LoadedModSourceLinkResult(false, null, "Project mapping services are unavailable.", new string[0]);
            }

            if (string.IsNullOrEmpty(sourceRoot))
            {
                return new LoadedModSourceLinkResult(false, null, "Set a source root before linking the loaded mod.", new string[0]);
            }

            var analysis = workspaceService.AnalyzeSourceRoot(sourceRoot, mod.ModId);
            if (analysis == null || analysis.Definition == null)
            {
                return new LoadedModSourceLinkResult(false, null, "Source analysis is unavailable for that path.", new string[0]);
            }

            var diagnostics = analysis.Diagnostics.ToArray();
            if (!analysis.Success)
            {
                return new LoadedModSourceLinkResult(
                    false,
                    analysis.Definition,
                    analysis.StatusMessage ?? "Could not link loaded mod to the supplied source root.",
                    diagnostics);
            }

            projectCatalog.Upsert(analysis.Definition);
            return new LoadedModSourceLinkResult(true, analysis.Definition, analysis.StatusMessage, diagnostics);
        }
    }
}
