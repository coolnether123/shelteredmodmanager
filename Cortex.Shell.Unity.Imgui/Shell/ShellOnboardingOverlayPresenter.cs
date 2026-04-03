using System;
using Cortex.Core.Abstractions;
using Cortex.Modules.Onboarding;
using Cortex.Rendering;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi.Shell;
using Cortex.Services.Onboarding;
using UnityEngine;

namespace Cortex.Shell
{
    internal sealed class ShellOnboardingOverlayPresentation
    {
        public Rect FullscreenRect;
        public Rect ModalRect;
        public Rect PromptRect;
        public bool PreviewBackground;
    }

    internal sealed class ShellOnboardingOverlayPresenter
    {
        private const int OnboardingWindowId = 0xC080;

        private readonly OnboardingModule _onboardingModule;

        public ShellOnboardingOverlayPresenter()
            : this(new OnboardingModule())
        {
        }

        public ShellOnboardingOverlayPresenter(OnboardingModule onboardingModule)
        {
            _onboardingModule = onboardingModule ?? new OnboardingModule();
        }

        public void DrawOverlay(
            CortexOnboardingCoordinator onboardingCoordinator,
            CortexShellState shellState,
            IContributionRegistry contributionRegistry,
            IPathInteractionService pathInteractionService,
            WorkbenchFrameInputSnapshot input,
            Action consumeInputAction,
            Action completeAction)
        {
            if (onboardingCoordinator == null || shellState == null || shellState.Onboarding == null)
            {
                return;
            }

            var presentation = BuildPresentation(shellState.Onboarding, input);
            GUI.ModalWindow(OnboardingWindowId, presentation.FullscreenRect, delegate(int id)
            {
                ApplyInteraction(shellState.Onboarding, input, presentation, consumeInputAction);
                DrawBackdrop(presentation);

                if (DrawModalContent(onboardingCoordinator, shellState, contributionRegistry, pathInteractionService, presentation))
                {
                    completeAction?.Invoke();
                }

                if (shellState.Onboarding.FinishPrompt.IsVisible)
                {
                    DrawFinishPrompt(shellState.Onboarding, presentation.PromptRect, completeAction);
                }

                GUI.FocusWindow(id);
            }, string.Empty, GUIStyle.none);
        }

        internal ShellOnboardingOverlayPresentation BuildPresentation(CortexOnboardingState onboardingState, WorkbenchFrameInputSnapshot input)
        {
            var fullscreenRect = new Rect(0f, 0f, input.ViewportSize.Width, input.ViewportSize.Height);
            var modalRect = ToRect(ShellOverlayInteractionController.BuildOnboardingModalRect(ToRenderRect(fullscreenRect)));
            var hasPromptAnchor = onboardingState != null && onboardingState.FinishPrompt.Anchor != Vector2.zero;
            var promptRect = ToRect(
                ShellOverlayInteractionController.BuildOnboardingPromptRect(
                    ToRenderRect(modalRect),
                    ToRenderPoint(onboardingState != null ? onboardingState.FinishPrompt.Anchor : Vector2.zero),
                    hasPromptAnchor));
            var pointerPosition = input.HasCurrentEvent ? ToVector2(input.CurrentMousePosition) : Vector2.zero;

            return new ShellOnboardingOverlayPresentation
            {
                FullscreenRect = fullscreenRect,
                ModalRect = modalRect,
                PromptRect = promptRect,
                PreviewBackground = onboardingState != null &&
                    !onboardingState.KeepFocused &&
                    fullscreenRect.Contains(pointerPosition) &&
                    !modalRect.Contains(pointerPosition) &&
                    !promptRect.Contains(pointerPosition)
            };
        }

        internal bool ApplyInteraction(
            CortexOnboardingState onboardingState,
            WorkbenchFrameInputSnapshot input,
            ShellOnboardingOverlayPresentation presentation,
            Action consumeInputAction)
        {
            if (onboardingState == null || presentation == null)
            {
                return false;
            }

            var result = ShellOverlayInteractionController.EvaluateOnboardingInput(
                input,
                ToRenderRect(presentation.FullscreenRect),
                ToRenderRect(presentation.ModalRect),
                ToRenderRect(presentation.PromptRect));

            if (result.ActionKind == ShellOnboardingOverlayActionKind.HidePrompt)
            {
                onboardingState.FinishPrompt.IsVisible = false;
            }
            else if (result.ActionKind == ShellOnboardingOverlayActionKind.ShowPrompt)
            {
                onboardingState.FinishPrompt.IsVisible = true;
                onboardingState.FinishPrompt.Anchor = ToVector2(result.PromptAnchor);
            }

            if (result.ShouldConsumeInput)
            {
                consumeInputAction?.Invoke();
            }

            return result.ShouldConsumeInput;
        }

        private bool DrawModalContent(
            CortexOnboardingCoordinator onboardingCoordinator,
            CortexShellState shellState,
            IContributionRegistry contributionRegistry,
            IPathInteractionService pathInteractionService,
            ShellOnboardingOverlayPresentation presentation)
        {
            var catalog = onboardingCoordinator.BuildCatalog(contributionRegistry);
            return _onboardingModule.Draw(
                presentation.ModalRect,
                shellState.Onboarding,
                catalog,
                onboardingCoordinator.InteractionService,
                pathInteractionService,
                presentation.PreviewBackground);
        }

        private static void DrawFinishPrompt(CortexOnboardingState onboardingState, Rect promptRect, Action completeAction)
        {
            DrawPanelChrome(promptRect, 1f);
            GUILayout.BeginArea(new Rect(promptRect.x + 12f, promptRect.y + 10f, promptRect.width - 24f, promptRect.height - 20f));
            GUILayout.Label("Done with onboarding?", CreatePromptTitleStyle(), GUILayout.Height(20f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes", GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                onboardingState.FinishPrompt.IsVisible = false;
                completeAction?.Invoke();
            }

            if (GUILayout.Button("No", GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                onboardingState.FinishPrompt.IsVisible = false;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static void DrawBackdrop(ShellOnboardingOverlayPresentation presentation)
        {
            DrawBlock(presentation.FullscreenRect, new Color(0f, 0f, 0f, presentation.PreviewBackground ? 0.14f : 0.24f));
            DrawBlock(
                new Rect(
                    presentation.ModalRect.x - 8f,
                    presentation.ModalRect.y + 10f,
                    presentation.ModalRect.width + 16f,
                    presentation.ModalRect.height + 16f),
                new Color(0f, 0f, 0f, presentation.PreviewBackground ? 0.12f : 0.20f));
            DrawPanelChrome(presentation.ModalRect, presentation.PreviewBackground ? 0.95f : 1f);
        }

        private static void DrawPanelChrome(Rect rect, float alpha)
        {
            var surface = CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), alpha);
            DrawBlock(rect, surface);
            DrawBorder(rect, CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), alpha * 0.9f), 2f);
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawBlock(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawBlock(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawBlock(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawBlock(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void DrawBlock(Rect rect, Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private static GUIStyle CreatePromptTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            style.normal.textColor = CortexIdeLayout.GetTextColor();
            return style;
        }

        private static Vector2 ToVector2(RenderPoint point)
        {
            return new Vector2(point.X, point.Y);
        }

        private static RenderPoint ToRenderPoint(Vector2 point)
        {
            return new RenderPoint(point.x, point.y);
        }

        private static RenderRect ToRenderRect(Rect rect)
        {
            return new RenderRect(rect.x, rect.y, rect.width, rect.height);
        }

        private static Rect ToRect(RenderRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }
    }
}
