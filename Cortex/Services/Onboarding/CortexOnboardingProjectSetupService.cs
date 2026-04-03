using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services.Onboarding
{
    internal sealed class CortexOnboardingProjectSetupService
    {
        private readonly CortexLoadedModSourceLinkService _loadedModSourceLinkService;

        public CortexOnboardingProjectSetupService()
            : this(new CortexLoadedModSourceLinkService())
        {
        }

        public CortexOnboardingProjectSetupService(CortexLoadedModSourceLinkService loadedModSourceLinkService)
        {
            _loadedModSourceLinkService = loadedModSourceLinkService ?? new CortexLoadedModSourceLinkService();
        }

        public void Seed(
            CortexOnboardingState onboardingState,
            CortexSettings settings,
            ILoadedModCatalog loadedModCatalog,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService)
        {
            if (onboardingState == null)
            {
                return;
            }

            onboardingState.ResetModProjectDrafts();
            onboardingState.SelectedWorkspaceRootPath = settings != null ? settings.WorkspaceRootPath ?? string.Empty : string.Empty;

            var loadedMods = loadedModCatalog != null ? loadedModCatalog.GetLoadedMods() : null;
            if (loadedMods == null)
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

                var existing = projectCatalog != null ? projectCatalog.GetProject(mod.ModId) : null;
                onboardingState.ModProjectDrafts.Add(new CortexOnboardingModProjectDraft
                {
                    ModId = mod.ModId ?? string.Empty,
                    DisplayName = string.IsNullOrEmpty(mod.DisplayName) ? mod.ModId : mod.DisplayName,
                    RootPath = mod.RootPath ?? string.Empty,
                    SourceRootPath = _loadedModSourceLinkService.SuggestSourceRoot(
                        mod,
                        onboardingState.SelectedWorkspaceRootPath,
                        projectCatalog,
                        workspaceService),
                    IsOwnedByUser = existing != null && !string.IsNullOrEmpty(existing.SourceRootPath),
                    HasExistingMapping = existing != null && !string.IsNullOrEmpty(existing.SourceRootPath)
                });
            }

            onboardingState.ModProjectDrafts.Sort(CompareDrafts);
        }

        public bool TryApply(
            CortexShellState shellState,
            OnboardingProfileContribution profile,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService)
        {
            if (shellState == null || shellState.Onboarding == null || profile == null)
            {
                return true;
            }

            var settings = shellState.Settings ?? new CortexSettings();
            shellState.Settings = settings;

            var workspaceRootPath = shellState.Onboarding.SelectedWorkspaceRootPath ?? string.Empty;
            if (!string.IsNullOrEmpty(workspaceRootPath))
            {
                settings.WorkspaceRootPath = workspaceRootPath;
                if (string.IsNullOrEmpty(settings.AdditionalSourceRoots))
                {
                    settings.AdditionalSourceRoots = workspaceRootPath;
                }
            }

            if (profile.WorkflowKind != OnboardingProfileWorkflowKind.Modder)
            {
                return true;
            }

            if (projectCatalog == null || workspaceService == null)
            {
                shellState.StatusMessage = "Project mapping services are unavailable.";
                return false;
            }

            var appliedCount = 0;
            for (var i = 0; i < shellState.Onboarding.ModProjectDrafts.Count; i++)
            {
                var draft = shellState.Onboarding.ModProjectDrafts[i];
                if (draft == null || !draft.IsOwnedByUser)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(draft.SourceRootPath))
                {
                    shellState.StatusMessage = "Set a source root for " + ResolveDisplayName(draft) + " before finishing onboarding.";
                    return false;
                }

                var linkResult = _loadedModSourceLinkService.LinkLoadedModToSource(
                    new LoadedModInfo
                    {
                        ModId = draft.ModId,
                        DisplayName = draft.DisplayName,
                        RootPath = draft.RootPath
                    },
                    draft.SourceRootPath,
                    projectCatalog,
                    workspaceService);

                for (var diagnosticIndex = 0; diagnosticIndex < linkResult.Diagnostics.Length; diagnosticIndex++)
                {
                    shellState.Diagnostics.Add(linkResult.Diagnostics[diagnosticIndex]);
                }

                if (!linkResult.Success || linkResult.Definition == null)
                {
                    shellState.StatusMessage = string.IsNullOrEmpty(linkResult.StatusMessage)
                        ? "Could not link " + ResolveDisplayName(draft) + " to its source root."
                        : linkResult.StatusMessage;
                    return false;
                }

                draft.SourceRootPath = linkResult.Definition.SourceRootPath ?? draft.SourceRootPath;
                draft.HasExistingMapping = true;
                appliedCount++;

                if (shellState.SelectedProject == null)
                {
                    shellState.SelectedProject = projectCatalog.GetProject(draft.ModId) ?? linkResult.Definition;
                }
            }

            if (appliedCount > 0)
            {
                shellState.StatusMessage = "Prepared " + appliedCount + " mod source mapping(s) during onboarding.";
            }

            return true;
        }

        public void ToggleOwnership(CortexOnboardingModProjectDraft draft)
        {
            if (draft == null)
            {
                return;
            }

            draft.IsOwnedByUser = !draft.IsOwnedByUser;
        }

        public void UseSelectedWorkspaceRoot(CortexOnboardingState onboardingState, CortexOnboardingModProjectDraft draft)
        {
            if (onboardingState == null || draft == null || string.IsNullOrEmpty(onboardingState.SelectedWorkspaceRootPath))
            {
                return;
            }

            draft.SourceRootPath = onboardingState.SelectedWorkspaceRootPath;
        }

        private static int CompareDrafts(CortexOnboardingModProjectDraft left, CortexOnboardingModProjectDraft right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return string.Compare(ResolveDisplayName(left), ResolveDisplayName(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(CortexOnboardingModProjectDraft draft)
        {
            return string.IsNullOrEmpty(draft != null ? draft.DisplayName : string.Empty)
                ? (draft != null ? draft.ModId ?? string.Empty : string.Empty)
                : draft.DisplayName;
        }
    }
}
