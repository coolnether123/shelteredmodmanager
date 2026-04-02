using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi.Shell;
using UnityEngine;
using Cortex.Services.Onboarding;

namespace Cortex.Shell
{
    internal sealed class ShellOverlayCoordinator
    {
        private const string OverlayInputCaptureOwnerId = "Cortex.Shell";
        private const int OnboardingWindowId = 0xC080;

        private readonly CortexShellState _state;
        private readonly CortexOnboardingCoordinator _onboardingCoordinator;
        private readonly CortexShellOnboardingLifecycle _onboardingLifecycle;
        private readonly Func<IOverlayInputCaptureService> _overlayInputCaptureServiceProvider;
        private readonly Func<WorkbenchFrameInputSnapshot> _frameInputProvider;
        private readonly Action _consumeEventAction;

        private bool _lastOverlayMouseCapture;
        private bool _lastOverlayKeyboardCapture;

        public ShellOverlayCoordinator(
            CortexShellState state,
            CortexOnboardingCoordinator onboardingCoordinator,
            CortexShellOnboardingLifecycle onboardingLifecycle,
            Func<IOverlayInputCaptureService> overlayInputCaptureServiceProvider,
            Func<WorkbenchFrameInputSnapshot> frameInputProvider,
            Action consumeEventAction)
        {
            _state = state;
            _onboardingCoordinator = onboardingCoordinator;
            _onboardingLifecycle = onboardingLifecycle;
            _overlayInputCaptureServiceProvider = overlayInputCaptureServiceProvider;
            _frameInputProvider = frameInputProvider;
            _consumeEventAction = consumeEventAction;
        }

        public void UpdateOverlayInputCapture(bool visible, Rect windowRect, Rect logWindowRect)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            var capture = ShellOverlayInteractionController.ResolveInputCapture(
                visible,
                _state.Onboarding.IsActive,
                input,
                BuildChromeHitRegions(windowRect, logWindowRect));
            ApplyOverlayInputCapture(capture.CaptureMouse, capture.CaptureKeyboard);
        }

        public void ReleaseOverlayInputCapture() => ApplyOverlayInputCapture(false, false);

        private void ApplyOverlayInputCapture(bool captureMouse, bool captureKeyboard)
        {
            if (_lastOverlayMouseCapture == captureMouse && _lastOverlayKeyboardCapture == captureKeyboard) return;
            var captureService = _overlayInputCaptureServiceProvider();
            if (captureService == null) return;

            if (captureMouse || captureKeyboard) captureService.ReportCapture(OverlayInputCaptureOwnerId, captureMouse, captureKeyboard);
            else captureService.ReleaseCapture(OverlayInputCaptureOwnerId);

            _lastOverlayMouseCapture = captureMouse; _lastOverlayKeyboardCapture = captureKeyboard;
        }

        public void DrawOnboardingOverlay(IProjectCatalog projectCatalog, IProjectWorkspaceService projectWorkspaceService, IWorkbenchRuntime workbenchRuntime, IPathInteractionService pathInteractionService, Action<string> activateContainerAction, Action persistSessionAction, Action persistWindowSettingsAction)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            var screenWidth = input.ViewportSize.Width;
            var screenHeight = input.ViewportSize.Height;
            var fullscreenRect = new Rect(0f, 0f, screenWidth, screenHeight);
            GUI.ModalWindow(OnboardingWindowId, fullscreenRect, (id) => {
                var modalRect = ToRect(ShellOverlayInteractionController.BuildOnboardingModalRect(ToRenderRect(fullscreenRect)));
                var promptRect = ToRect(ShellOverlayInteractionController.BuildOnboardingPromptRect(ToRenderRect(modalRect), ToRenderPoint(_state.Onboarding.FinishPrompt.Anchor), _state.Onboarding.FinishPrompt.Anchor != Vector2.zero));
                var currentInput = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
                var mousePos = currentInput.HasCurrentEvent ? ToVector2(currentInput.CurrentMousePosition) : Vector2.zero;
                var previewBg = !_state.Onboarding.KeepFocused && fullscreenRect.Contains(mousePos) && !modalRect.Contains(mousePos) && !promptRect.Contains(mousePos);

                HandleOnboardingOverlayInput(fullscreenRect, modalRect, promptRect);
                DrawOnboardingBlock(fullscreenRect, new Color(0f, 0f, 0f, previewBg ? 0.14f : 0.24f));
                DrawOnboardingBlock(new Rect(modalRect.x - 8f, modalRect.y + 10f, modalRect.width + 16f, modalRect.height + 16f), new Color(0f, 0f, 0f, previewBg ? 0.12f : 0.20f));
                DrawOnboardingPanelChrome(modalRect, previewBg ? 0.95f : 1f);

                if (_onboardingCoordinator.DrawModalContent(modalRect, _state, workbenchRuntime?.ContributionRegistry, pathInteractionService, previewBg))
                {
                    CompleteOnboarding(projectCatalog, projectWorkspaceService, persistSessionAction, persistWindowSettingsAction, workbenchRuntime, activateContainerAction);
                }
                if (_state.Onboarding.FinishPrompt.IsVisible) DrawOnboardingFinishPrompt(promptRect, () => CompleteOnboarding(projectCatalog, projectWorkspaceService, persistSessionAction, persistWindowSettingsAction, workbenchRuntime, activateContainerAction));
                GUI.FocusWindow(id);
            }, string.Empty, GUIStyle.none);
        }

        private void HandleOnboardingOverlayInput(Rect fullscreenRect, Rect modalRect, Rect promptRect)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            var result = ShellOverlayInteractionController.EvaluateOnboardingInput(input, ToRenderRect(fullscreenRect), ToRenderRect(modalRect), ToRenderRect(promptRect));
            if (result.ActionKind == ShellOnboardingOverlayActionKind.HidePrompt)
            {
                _state.Onboarding.FinishPrompt.IsVisible = false;
            }
            else if (result.ActionKind == ShellOnboardingOverlayActionKind.ShowPrompt)
            {
                ShowOnboardingFinishPrompt(result.PromptAnchor);
            }

            if (result.ShouldConsumeInput)
            {
                _consumeEventAction();
            }
        }

        private void DrawOnboardingFinishPrompt(Rect promptRect, Action onComplete)
        {
            DrawOnboardingPanelChrome(promptRect, 1f);
            GUILayout.BeginArea(new Rect(promptRect.x + 12f, promptRect.y + 10f, promptRect.width - 24f, promptRect.height - 20f));
            GUILayout.Label("Done with onboarding?", CreateOnboardingPromptTitleStyle(), GUILayout.Height(20f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80f), GUILayout.Height(24f))) { _state.Onboarding.FinishPrompt.IsVisible = false; onComplete(); }
            if (GUILayout.Button("No", GUILayout.Width(80f), GUILayout.Height(24f))) _state.Onboarding.FinishPrompt.IsVisible = false;
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }

        private void CompleteOnboarding(IProjectCatalog projectCatalog, IProjectWorkspaceService projectWorkspaceService, Action persistSessionAction, Action persistWindowSettingsAction, IWorkbenchRuntime workbenchRuntime, Action<string> activateContainerAction)
        {
            _onboardingLifecycle.Complete(_onboardingCoordinator, _state, workbenchRuntime, workbenchRuntime?.ContributionRegistry, projectCatalog, projectWorkspaceService, persistSessionAction, persistWindowSettingsAction, result => {
                if (result?.ContainersToActivate != null) foreach (var c in result.ContainersToActivate) activateContainerAction(c);
            });
        }

        private void ShowOnboardingFinishPrompt(RenderPoint anchor) { _state.Onboarding.FinishPrompt.IsVisible = true; _state.Onboarding.FinishPrompt.Anchor = ToVector2(anchor); }

        private static void DrawOnboardingPanelChrome(Rect rect, float alpha) { var surface = CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), alpha); DrawOnboardingBlock(rect, surface); DrawOnboardingBorder(rect, CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), alpha * 0.9f), 2f); }
        private static void DrawOnboardingBorder(Rect rect, Color c, float t) { DrawOnboardingBlock(new Rect(rect.x, rect.y, rect.width, t), c); DrawOnboardingBlock(new Rect(rect.x, rect.yMax - t, rect.width, t), c); DrawOnboardingBlock(new Rect(rect.x, rect.y, t, rect.height), c); DrawOnboardingBlock(new Rect(rect.xMax - t, rect.y, t, rect.height), c); }
        private static void DrawOnboardingBlock(Rect rect, Color c) { if (Event.current?.type != EventType.Repaint) return; var prev = GUI.color; GUI.color = c; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = prev; }
        private static GUIStyle CreateOnboardingPromptTitleStyle() { var s = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold }; s.normal.textColor = CortexIdeLayout.GetTextColor(); return s; }
        private static Vector2 ToVector2(Cortex.Rendering.Models.RenderPoint point) { return new Vector2(point.X, point.Y); }
        private static RenderPoint ToRenderPoint(Vector2 point) { return new RenderPoint(point.x, point.y); }
        private static RenderRect ToRenderRect(Rect rect) { return new RenderRect(rect.x, rect.y, rect.width, rect.height); }
        private static Rect ToRect(RenderRect rect) { return new Rect(rect.X, rect.Y, rect.Width, rect.Height); }

        private List<ShellChromeHitRegion> BuildChromeHitRegions(Rect windowRect, Rect logWindowRect)
        {
            var regions = new List<ShellChromeHitRegion>();
            regions.Add(new ShellChromeHitRegion
            {
                Visible = true,
                IsCollapsed = _state.Chrome.Main.IsCollapsed,
                ExpandedRect = ToRenderRect(windowRect),
                CollapsedRect = ToRenderRect(_state.Chrome.Main.CollapsedRect)
            });
            regions.Add(new ShellChromeHitRegion
            {
                Visible = _state.Logs.ShowDetachedWindow,
                IsCollapsed = _state.Chrome.Logs.IsCollapsed,
                ExpandedRect = ToRenderRect(logWindowRect),
                CollapsedRect = ToRenderRect(_state.Chrome.Logs.CollapsedRect)
            });
            return regions;
        }
    }
}
