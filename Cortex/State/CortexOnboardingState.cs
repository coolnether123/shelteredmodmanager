using UnityEngine;
using System.Collections.Generic;

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
        public Vector2 Anchor;

        public CortexOnboardingPromptState()
        {
            IsVisible = false;
            Anchor = Vector2.zero;
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
        public Vector2 ThemeScroll;
        public Vector2 ModProjectScroll;
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
            ThemeScroll = Vector2.zero;
            ModProjectScroll = Vector2.zero;
        }

        public void ResetInteractionState()
        {
            HasUserSelectedLayoutPreset = false;
            HasUserSelectedTheme = false;
            ActiveStepIndex = 0;
            PreviewFingerprint = string.Empty;
            ThemeScroll = Vector2.zero;
            ModProjectScroll = Vector2.zero;
            FinishPrompt.IsVisible = false;
            FinishPrompt.Anchor = Vector2.zero;
        }

        public void ResetModProjectDrafts()
        {
            SelectedWorkspaceRootPath = string.Empty;
            ModProjectDrafts.Clear();
            ModProjectScroll = Vector2.zero;
        }
    }
}
