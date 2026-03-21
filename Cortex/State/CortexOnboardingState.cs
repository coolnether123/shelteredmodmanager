using UnityEngine;

namespace Cortex
{
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
        public string PreviewFingerprint;
        public Vector2 ThemeScroll;
        public readonly CortexOnboardingPromptState FinishPrompt = new CortexOnboardingPromptState();

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
            PreviewFingerprint = string.Empty;
            ThemeScroll = Vector2.zero;
        }

        public void ResetInteractionState()
        {
            HasUserSelectedLayoutPreset = false;
            HasUserSelectedTheme = false;
            ActiveStepIndex = 0;
            PreviewFingerprint = string.Empty;
            ThemeScroll = Vector2.zero;
            FinishPrompt.IsVisible = false;
            FinishPrompt.Anchor = Vector2.zero;
        }
    }
}
