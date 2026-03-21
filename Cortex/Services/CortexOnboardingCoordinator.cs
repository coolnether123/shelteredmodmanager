using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Onboarding;
using Cortex.Presentation.Abstractions;
using UnityEngine;

namespace Cortex.Services
{
    internal sealed class CortexOnboardingCoordinator
    {
        private readonly CortexOnboardingService _onboardingService;
        private readonly CortexOnboardingProjectSetupService _projectSetupService;
        private readonly CortexOnboardingWorkspaceApplier _workspaceApplier;
        private readonly OnboardingModule _onboardingModule;

        public CortexOnboardingCoordinator()
            : this(
                new CortexOnboardingService(),
                new CortexOnboardingProjectSetupService(),
                new CortexOnboardingWorkspaceApplier(),
                new OnboardingModule())
        {
        }

        public CortexOnboardingCoordinator(
            CortexOnboardingService onboardingService,
            CortexOnboardingProjectSetupService projectSetupService,
            CortexOnboardingWorkspaceApplier workspaceApplier,
            OnboardingModule onboardingModule)
        {
            _onboardingService = onboardingService ?? new CortexOnboardingService();
            _projectSetupService = projectSetupService ?? new CortexOnboardingProjectSetupService();
            _workspaceApplier = workspaceApplier ?? new CortexOnboardingWorkspaceApplier();
            _onboardingModule = onboardingModule ?? new OnboardingModule();
        }

        public void Open(
            CortexShellState shellState,
            CortexSettings settings,
            IContributionRegistry contributionRegistry,
            ILoadedModCatalog loadedModCatalog,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService,
            bool reopenedByUser)
        {
            if (shellState == null)
            {
                return;
            }

            _onboardingService.SeedSelections(shellState.Onboarding, settings, contributionRegistry);
            _projectSetupService.Seed(shellState.Onboarding, settings, loadedModCatalog, projectCatalog, workspaceService);
            shellState.Onboarding.IsActive = true;
            shellState.Onboarding.FinishPrompt.IsVisible = false;
            shellState.Onboarding.PreviewFingerprint = string.Empty;
            shellState.StatusMessage = reopenedByUser ? "Onboarding reopened." : "Onboarding opened.";
        }

        public bool DrawModalContent(Rect modalRect, CortexShellState shellState, IContributionRegistry contributionRegistry, bool previewBackground)
        {
            return DrawModalContent(modalRect, shellState, contributionRegistry, null, previewBackground);
        }

        public bool DrawModalContent(
            Rect modalRect,
            CortexShellState shellState,
            IContributionRegistry contributionRegistry,
            IPathInteractionService pathInteractionService,
            bool previewBackground)
        {
            if (shellState == null)
            {
                return false;
            }

            var catalog = _onboardingService.BuildCatalog(contributionRegistry);
            return _onboardingModule.Draw(modalRect, shellState.Onboarding, catalog, _onboardingService, pathInteractionService, previewBackground);
        }

        public CortexOnboardingWorkspaceApplicationResult PreviewIfNeeded(
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            IContributionRegistry contributionRegistry)
        {
            if (shellState == null || workbenchRuntime == null)
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            var catalog = _onboardingService.BuildCatalog(contributionRegistry);
            var selection = _onboardingService.ResolveSelection(shellState.Onboarding, shellState.Settings, catalog);
            var fingerprint = _onboardingService.BuildPreviewFingerprint(selection);
            if (string.Equals(shellState.Onboarding.PreviewFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            var result = _workspaceApplier.Preview(shellState, workbenchRuntime, selection);
            shellState.Onboarding.PreviewFingerprint = fingerprint;
            return result;
        }

        public CortexOnboardingWorkspaceApplicationResult Complete(
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            IContributionRegistry contributionRegistry,
            IProjectCatalog projectCatalog,
            IProjectWorkspaceService workspaceService)
        {
            if (shellState == null || workbenchRuntime == null)
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            var catalog = _onboardingService.BuildCatalog(contributionRegistry);
            var selection = _onboardingService.ResolveSelection(shellState.Onboarding, shellState.Settings, catalog);
            if (selection == null)
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            if (!_projectSetupService.TryApply(shellState, selection.Profile, projectCatalog, workspaceService))
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            var result = _workspaceApplier.Apply(shellState, workbenchRuntime, selection);
            if (!result.WasApplied)
            {
                return result;
            }

            shellState.Onboarding.IsActive = false;
            shellState.Onboarding.FinishPrompt.IsVisible = false;
            shellState.Onboarding.PreviewFingerprint = string.Empty;
            return result;
        }

        public void RestoreFromPersistence(CortexOnboardingState onboardingState, PersistedWorkbenchState persisted)
        {
            if (onboardingState == null)
            {
                return;
            }

            onboardingState.IsActive = false;
            onboardingState.KeepFocused = false;
            onboardingState.ResetInteractionState();
            onboardingState.ActiveProfileId = persisted != null ? (persisted.ActiveOnboardingProfileId ?? string.Empty) : string.Empty;
            onboardingState.ActiveLayoutPresetId = persisted != null ? (persisted.ActiveOnboardingLayoutPresetId ?? string.Empty) : string.Empty;
            onboardingState.ActiveThemeId = persisted != null ? (persisted.ActiveOnboardingThemeId ?? string.Empty) : string.Empty;
            onboardingState.SelectedProfileId = onboardingState.ActiveProfileId;
            onboardingState.SelectedLayoutPresetId = onboardingState.ActiveLayoutPresetId;
            onboardingState.SelectedThemeId = onboardingState.ActiveThemeId;
        }

        public void PersistToPersistence(CortexOnboardingState onboardingState, PersistedWorkbenchState persisted)
        {
            if (onboardingState == null || persisted == null)
            {
                return;
            }

            persisted.ActiveOnboardingProfileId = ResolvePersistedSelection(onboardingState.ActiveProfileId, onboardingState.SelectedProfileId);
            persisted.ActiveOnboardingLayoutPresetId = ResolvePersistedSelection(onboardingState.ActiveLayoutPresetId, onboardingState.SelectedLayoutPresetId);
            persisted.ActiveOnboardingThemeId = ResolvePersistedSelection(onboardingState.ActiveThemeId, onboardingState.SelectedThemeId);
        }

        private static string ResolvePersistedSelection(string activeId, string selectedId)
        {
            if (!string.IsNullOrEmpty(activeId))
            {
                return activeId;
            }

            return !string.IsNullOrEmpty(selectedId) ? selectedId : string.Empty;
        }
    }
}
