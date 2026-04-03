using System.Collections.Generic;
using Cortex.Rendering.Models;

namespace Cortex
{
    public sealed class CortexOnboardingModProjectDraft
    {
        public string ModId;
        public string DisplayName;
        public string RootPath;
        public string SourceRootPath;
        public bool IsOwnedByUser;
        public bool HasExistingMapping;

        public CortexOnboardingModProjectDraft()
        {
            ModId = string.Empty;
            DisplayName = string.Empty;
            RootPath = string.Empty;
            SourceRootPath = string.Empty;
            IsOwnedByUser = false;
            HasExistingMapping = false;
        }
    }

    public sealed class CortexOnboardingPromptState
    {
        public bool IsVisible;
        public RenderPoint Anchor;

        public CortexOnboardingPromptState()
        {
            IsVisible = false;
            Anchor = RenderPoint.Zero;
        }
    }

    public sealed class CortexOnboardingState
    {
        public bool IsActive;
        public bool KeepFocused;
        public bool HasUserSelectedLayoutPreset;
        public bool HasUserSelectedTheme;
        public int ActiveStepIndex;
        public string ActiveProfileId;
        public string ActiveLayoutPresetId;
        public string ActiveThemeId;
        public string SelectedProfileId;
        public string SelectedLayoutPresetId;
        public string SelectedThemeId;
        public string SelectedWorkspaceRootPath;
        public string PreviewFingerprint;
        public RenderPoint ThemeScroll;
        public RenderPoint ModProjectScroll;
        public readonly CortexOnboardingPromptState FinishPrompt = new CortexOnboardingPromptState();
        public readonly List<CortexOnboardingModProjectDraft> ModProjectDrafts = new List<CortexOnboardingModProjectDraft>();

        public CortexOnboardingState()
        {
            IsActive = false;
            KeepFocused = false;
            HasUserSelectedLayoutPreset = false;
            HasUserSelectedTheme = false;
            ActiveStepIndex = 0;
            ActiveProfileId = string.Empty;
            ActiveLayoutPresetId = string.Empty;
            ActiveThemeId = string.Empty;
            SelectedProfileId = string.Empty;
            SelectedLayoutPresetId = string.Empty;
            SelectedThemeId = string.Empty;
            SelectedWorkspaceRootPath = string.Empty;
            PreviewFingerprint = string.Empty;
            ThemeScroll = RenderPoint.Zero;
            ModProjectScroll = RenderPoint.Zero;
        }

        public void ResetInteractionState()
        {
            HasUserSelectedLayoutPreset = false;
            HasUserSelectedTheme = false;
            ActiveStepIndex = 0;
            PreviewFingerprint = string.Empty;
            ThemeScroll = RenderPoint.Zero;
            ModProjectScroll = RenderPoint.Zero;
            FinishPrompt.IsVisible = false;
            FinishPrompt.Anchor = RenderPoint.Zero;
        }

        public void ResetModProjectDrafts()
        {
            SelectedWorkspaceRootPath = string.Empty;
            ModProjectDrafts.Clear();
            ModProjectScroll = RenderPoint.Zero;
        }
    }
}
