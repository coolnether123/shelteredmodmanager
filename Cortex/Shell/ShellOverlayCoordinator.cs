using System;
using Cortex.Chrome;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Rendering.RuntimeUi;
using UnityEngine;
using Cortex.Services.Onboarding;

namespace Cortex.Shell
{
    internal sealed class ShellOverlayCoordinator
    {
        private const string OverlayInputCaptureOwnerId = "Cortex.Shell";
        private const int OnboardingWindowId = 0xC080;
        private const float OnboardingModalMaxWidth = 940f;
        private const float OnboardingModalMinWidth = 760f;
        private const float OnboardingModalHeight = 548f;

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
            var captureMouse = false;
            var captureKeyboard = input.HotControl != 0 || input.KeyboardControl != 0;

            if (_state.Onboarding.IsActive) { ApplyOverlayInputCapture(true, true); return; }
            if (visible)
            {
                var guiMouse = ToVector2(input.PointerPosition);
                captureMouse = input.HotControl != 0 || IsPointWithinVisibleChrome(guiMouse, windowRect, logWindowRect);
            }
            ApplyOverlayInputCapture(captureMouse, captureKeyboard);
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

        private bool IsPointWithinVisibleChrome(Vector2 guiPoint, Rect windowRect, Rect logWindowRect)
        {
            if (_state.Chrome.Main.IsCollapsed) { if (_state.Chrome.Main.CollapsedRect.Contains(guiPoint)) return true; }
            else if (windowRect.Contains(guiPoint)) return true;

            if (_state.Logs.ShowDetachedWindow)
            {
                if (_state.Chrome.Logs.IsCollapsed) { if (_state.Chrome.Logs.CollapsedRect.Contains(guiPoint)) return true; }
                else if (logWindowRect.Contains(guiPoint)) return true;
            }
            return false;
        }

        public void DrawOnboardingOverlay(ICortexHostEnvironment hostEnvironment, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog, IProjectWorkspaceService projectWorkspaceService, IWorkbenchRuntime workbenchRuntime, IPathInteractionService pathInteractionService, Action<string> activateContainerAction, Action persistSessionAction, Action persistWindowSettingsAction)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            var screenWidth = input.ViewportSize.Width;
            var screenHeight = input.ViewportSize.Height;
            var fullscreenRect = new Rect(0f, 0f, screenWidth, screenHeight);
            GUI.ModalWindow(OnboardingWindowId, fullscreenRect, (id) => {
                var modalRect = BuildOnboardingModalRect(fullscreenRect);
                var promptRect = BuildOnboardingPromptRect(modalRect);
                var currentInput = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
                var mousePos = currentInput.HasCurrentEvent ? ToVector2(currentInput.CurrentMousePosition) : Vector2.zero;
                var previewBg = !_state.Onboarding.KeepFocused && fullscreenRect.Contains(mousePos) && !modalRect.Contains(mousePos) && !promptRect.Contains(mousePos);

                HandleOnboardingOverlayInput(fullscreenRect, modalRect, promptRect, () => CompleteOnboarding(projectCatalog, projectWorkspaceService, persistSessionAction, persistWindowSettingsAction, workbenchRuntime, activateContainerAction));
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

        private void HandleOnboardingOverlayInput(Rect fullscreenRect, Rect modalRect, Rect promptRect, Action onComplete)
        {
            var input = _frameInputProvider != null ? _frameInputProvider() : new WorkbenchFrameInputSnapshot();
            if (!input.HasCurrentEvent) return;

            var mousePos = ToVector2(input.CurrentMousePosition);
            if (input.CurrentEventKind == WorkbenchInputEventKind.MouseDown)
            {
                if (modalRect.Contains(mousePos)) _state.Onboarding.FinishPrompt.IsVisible = false;
                else if (fullscreenRect.Contains(mousePos) && !promptRect.Contains(mousePos)) { ShowOnboardingFinishPrompt(mousePos); _consumeEventAction(); }
            }
            else if (input.CurrentEventKind == WorkbenchInputEventKind.KeyDown && input.CurrentKey == WorkbenchInputKey.Escape)
            {
                ShowOnboardingFinishPrompt(new Vector2(modalRect.xMax - 120f, modalRect.yMax - 54f)); _consumeEventAction();
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

        private void ShowOnboardingFinishPrompt(Vector2 anchor) { _state.Onboarding.FinishPrompt.IsVisible = true; _state.Onboarding.FinishPrompt.Anchor = anchor; }

        private static Rect BuildOnboardingModalRect(Rect fs)
        {
            var w = Mathf.Clamp(fs.width - 80f, OnboardingModalMinWidth, OnboardingModalMaxWidth);
            var h = Mathf.Min(OnboardingModalHeight, fs.height - 48f);
            return new Rect(fs.x + (fs.width - w) * 0.5f, fs.y + (fs.height - h) * 0.5f, w, h);
        }

        private Rect BuildOnboardingPromptRect(Rect modalRect)
        {
            var anchor = _state.Onboarding.FinishPrompt.Anchor != Vector2.zero ? _state.Onboarding.FinishPrompt.Anchor : new Vector2(modalRect.xMax - 120f, modalRect.yMax - 54f);
            return new Rect(Mathf.Clamp(anchor.x - 110f, modalRect.x + 12f, modalRect.xMax - 232f), Mathf.Clamp(anchor.y - 26f, modalRect.y + 12f, modalRect.yMax - 88f), 220f, 72f);
        }

        private static void DrawOnboardingPanelChrome(Rect rect, float alpha) { var surface = CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), alpha); DrawOnboardingBlock(rect, surface); DrawOnboardingBorder(rect, CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), alpha * 0.9f), 2f); }
        private static void DrawOnboardingBorder(Rect rect, Color c, float t) { DrawOnboardingBlock(new Rect(rect.x, rect.y, rect.width, t), c); DrawOnboardingBlock(new Rect(rect.x, rect.yMax - t, rect.width, t), c); DrawOnboardingBlock(new Rect(rect.x, rect.y, t, rect.height), c); DrawOnboardingBlock(new Rect(rect.xMax - t, rect.y, t, rect.height), c); }
        private static void DrawOnboardingBlock(Rect rect, Color c) { if (Event.current?.type != EventType.Repaint) return; var prev = GUI.color; GUI.color = c; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = prev; }
        private static GUIStyle CreateOnboardingPromptTitleStyle() { var s = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold }; s.normal.textColor = CortexIdeLayout.GetTextColor(); return s; }
        private static Vector2 ToVector2(Cortex.Rendering.Models.RenderPoint point) { return new Vector2(point.X, point.Y); }
    }
}
