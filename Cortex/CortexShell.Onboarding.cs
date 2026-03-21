using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private const float OnboardingModalMaxWidth = 940f;
        private const float OnboardingModalMinWidth = 760f;
        private const float OnboardingModalHeight = 548f;

        private void OpenOnboarding()
        {
            OpenOnboarding(true);
        }

        private void OpenOnboarding(bool reopenedByUser)
        {
            OpenOnboardingOverlay(reopenedByUser);
        }

        private void OpenOnboardingOverlay(bool reopenedByUser)
        {
            if (_workbenchRuntime == null || _workbenchRuntime.ContributionRegistry == null)
            {
                return;
            }

            _onboardingCoordinator.Open(
                _state,
                _state.Settings,
                _workbenchRuntime.ContributionRegistry,
                _loadedModCatalog,
                _projectCatalog,
                _projectWorkspaceService,
                reopenedByUser);
            _visible = true;
            _openMenuGroup = string.Empty;
        }

        private bool IsOnboardingActive()
        {
            return _state.Onboarding != null && _state.Onboarding.IsActive;
        }

        private void PreviewOnboardingSelections()
        {
            _onboardingLifecycle.Preview(
                _onboardingCoordinator,
                _state,
                _workbenchRuntime,
                _workbenchRuntime != null ? _workbenchRuntime.ContributionRegistry : null,
                ActivateOnboardingContainers);
        }

        private void CompleteOnboarding()
        {
            _onboardingLifecycle.Complete(
                _onboardingCoordinator,
                _state,
                _workbenchRuntime,
                _workbenchRuntime != null ? _workbenchRuntime.ContributionRegistry : null,
                _projectCatalog,
                _projectWorkspaceService,
                PersistWorkbenchSession,
                PersistWindowSettings,
                ActivateOnboardingContainers);
        }

        private void RestoreOnboardingSessionState(PersistedWorkbenchState persisted)
        {
            _onboardingCoordinator.RestoreFromPersistence(_state.Onboarding, persisted);
        }

        private void PersistOnboardingSessionState(PersistedWorkbenchState persisted)
        {
            _onboardingCoordinator.PersistToPersistence(_state.Onboarding, persisted);
        }

        private void DrawOnboardingOverlay()
        {
            var fullscreenRect = new Rect(0f, 0f, GetScreenWidth(), GetScreenHeight());
            GUI.ModalWindow(OnboardingWindowId, fullscreenRect, DrawOnboardingWindow, string.Empty, GUIStyle.none);
        }

        private void DrawOnboardingWindow(int windowId)
        {
            var fullscreenRect = new Rect(0f, 0f, GetScreenWidth(), GetScreenHeight());
            var modalRect = BuildOnboardingModalRect(fullscreenRect);
            var promptRect = BuildOnboardingPromptRect(modalRect);
            var mousePosition = HasCurrentInputEvent() ? GetCurrentMousePosition() : Vector2.zero;
            var previewBackground = !_state.Onboarding.KeepFocused &&
                fullscreenRect.Contains(mousePosition) &&
                !modalRect.Contains(mousePosition) &&
                !promptRect.Contains(mousePosition);

            HandleOnboardingOverlayInput(fullscreenRect, modalRect, promptRect);

            DrawOnboardingBlock(fullscreenRect, new Color(0f, 0f, 0f, previewBackground ? 0.14f : 0.24f));
            DrawOnboardingBlock(
                new Rect(modalRect.x - 8f, modalRect.y + 10f, modalRect.width + 16f, modalRect.height + 16f),
                new Color(0f, 0f, 0f, previewBackground ? 0.12f : 0.20f));
            DrawOnboardingPanelChrome(modalRect, previewBackground ? 0.95f : 1f);

            var finishRequested = _onboardingCoordinator.DrawModalContent(
                modalRect,
                _state,
                _workbenchRuntime != null ? _workbenchRuntime.ContributionRegistry : null,
                _pathInteractionService,
                previewBackground);

            if (_state.Onboarding.FinishPrompt.IsVisible)
            {
                DrawOnboardingFinishPrompt(promptRect);
            }

            if (finishRequested)
            {
                CompleteOnboarding();
            }

            GUI.FocusWindow(windowId);
        }

        private void HandleOnboardingOverlayInput(Rect fullscreenRect, Rect modalRect, Rect promptRect)
        {
            if (!HasCurrentInputEvent())
            {
                return;
            }

            var currentMousePosition = GetCurrentMousePosition();
            if (IsCurrentInputEvent(CortexShellInputEventKind.MouseDown) && modalRect.Contains(currentMousePosition))
            {
                _state.Onboarding.FinishPrompt.IsVisible = false;
                return;
            }

            if (IsCurrentInputEvent(CortexShellInputEventKind.MouseDown) &&
                fullscreenRect.Contains(currentMousePosition) &&
                !modalRect.Contains(currentMousePosition) &&
                !promptRect.Contains(currentMousePosition))
            {
                ShowOnboardingFinishPrompt(currentMousePosition);
                ConsumeCurrentInputEvent();
                return;
            }

            if (IsCurrentInputEvent(CortexShellInputEventKind.KeyDown) && IsCurrentKey(CortexShellInputKey.Escape))
            {
                ShowOnboardingFinishPrompt(new Vector2(modalRect.xMax - 120f, modalRect.yMax - 54f));
                ConsumeCurrentInputEvent();
            }
        }

        private void DrawOnboardingFinishPrompt(Rect promptRect)
        {
            DrawOnboardingPanelChrome(promptRect, 1f);
            GUILayout.BeginArea(new Rect(promptRect.x + 12f, promptRect.y + 10f, promptRect.width - 24f, promptRect.height - 20f));
            GUILayout.Label("Done with onboarding?", CreateOnboardingPromptTitleStyle(), GUILayout.Height(20f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                _state.Onboarding.FinishPrompt.IsVisible = false;
                CompleteOnboarding();
            }

            GUILayout.Space(6f);
            if (GUILayout.Button("No", GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                _state.Onboarding.FinishPrompt.IsVisible = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void ActivateOnboardingContainers(CortexOnboardingWorkspaceApplicationResult result)
        {
            if (result == null || result.ContainersToActivate == null)
            {
                return;
            }

            for (var i = 0; i < result.ContainersToActivate.Length; i++)
            {
                ActivateContainer(result.ContainersToActivate[i]);
            }
        }

        private void ShowOnboardingFinishPrompt(Vector2 anchor)
        {
            _state.Onboarding.FinishPrompt.IsVisible = true;
            _state.Onboarding.FinishPrompt.Anchor = anchor;
        }

        private static Rect BuildOnboardingModalRect(Rect fullscreenRect)
        {
            var width = Mathf.Clamp(fullscreenRect.width - 80f, OnboardingModalMinWidth, OnboardingModalMaxWidth);
            var height = Mathf.Min(OnboardingModalHeight, fullscreenRect.height - 48f);
            return new Rect(
                fullscreenRect.x + ((fullscreenRect.width - width) * 0.5f),
                fullscreenRect.y + ((fullscreenRect.height - height) * 0.5f),
                width,
                height);
        }

        private Rect BuildOnboardingPromptRect(Rect modalRect)
        {
            var anchor = _state.Onboarding.FinishPrompt.Anchor;
            if (anchor == Vector2.zero)
            {
                anchor = new Vector2(modalRect.xMax - 120f, modalRect.yMax - 54f);
            }

            return new Rect(
                Mathf.Clamp(anchor.x - 110f, modalRect.x + 12f, modalRect.xMax - 232f),
                Mathf.Clamp(anchor.y - 26f, modalRect.y + 12f, modalRect.yMax - 88f),
                220f,
                72f);
        }

        private static void DrawOnboardingPanelChrome(Rect rect, float alpha)
        {
            var fill = CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), alpha);
            var border = CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), Mathf.Clamp01(alpha * 0.9f));
            DrawOnboardingBlock(rect, fill);
            DrawOnboardingBorder(rect, border, 2f);
        }

        private static void DrawOnboardingBorder(Rect rect, Color color, float thickness)
        {
            DrawOnboardingBlock(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawOnboardingBlock(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawOnboardingBlock(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawOnboardingBlock(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void DrawOnboardingBlock(Rect rect, Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
        }

        private static GUIStyle CreateOnboardingPromptTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = CortexIdeLayout.GetTextColor();
            return style;
        }
    }
}
