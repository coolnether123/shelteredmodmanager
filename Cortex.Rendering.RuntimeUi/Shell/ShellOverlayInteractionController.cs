using System.Collections.Generic;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.Shell
{
    public sealed class ShellChromeHitRegion
    {
        public bool Visible;
        public bool IsCollapsed;
        public RenderRect ExpandedRect;
        public RenderRect CollapsedRect;
    }

    public struct ShellOverlayCaptureDecision
    {
        public bool CaptureMouse;
        public bool CaptureKeyboard;
    }

    public enum ShellOnboardingOverlayActionKind
    {
        None = 0,
        HidePrompt = 1,
        ShowPrompt = 2
    }

    public struct ShellOnboardingOverlayInputResult
    {
        public ShellOnboardingOverlayActionKind ActionKind;
        public RenderPoint PromptAnchor;
        public bool ShouldConsumeInput;
    }

    public static class ShellOverlayInteractionController
    {
        public const float OnboardingModalMaxWidth = 940f;
        public const float OnboardingModalMinWidth = 760f;
        public const float OnboardingModalHeight = 548f;

        public static ShellOverlayCaptureDecision ResolveInputCapture(bool shellVisible, bool onboardingActive, WorkbenchFrameInputSnapshot input, IList<ShellChromeHitRegion> chromeRegions)
        {
            var decision = new ShellOverlayCaptureDecision();
            decision.CaptureKeyboard = input.HotControl != 0 || input.KeyboardControl != 0;

            if (onboardingActive)
            {
                decision.CaptureMouse = true;
                decision.CaptureKeyboard = true;
                return decision;
            }

            if (shellVisible)
            {
                decision.CaptureMouse = input.HotControl != 0 || IsPointerWithinVisibleChrome(input.PointerPosition, chromeRegions);
            }

            return decision;
        }

        public static bool IsPointerWithinVisibleChrome(RenderPoint pointerPosition, IList<ShellChromeHitRegion> chromeRegions)
        {
            if (chromeRegions == null)
            {
                return false;
            }

            for (var i = 0; i < chromeRegions.Count; i++)
            {
                var region = chromeRegions[i];
                if (region == null || !region.Visible)
                {
                    continue;
                }

                if (region.IsCollapsed)
                {
                    if (RuntimeUiHitTest.Contains(region.CollapsedRect, pointerPosition))
                    {
                        return true;
                    }
                }
                else if (RuntimeUiHitTest.Contains(region.ExpandedRect, pointerPosition))
                {
                    return true;
                }
            }

            return false;
        }

        public static RenderRect BuildOnboardingModalRect(RenderRect fullscreenRect)
        {
            var width = Clamp(fullscreenRect.Width - 80f, OnboardingModalMinWidth, OnboardingModalMaxWidth);
            var height = Min(OnboardingModalHeight, fullscreenRect.Height - 48f);
            return new RenderRect(
                fullscreenRect.X + ((fullscreenRect.Width - width) * 0.5f),
                fullscreenRect.Y + ((fullscreenRect.Height - height) * 0.5f),
                width,
                height);
        }

        public static RenderRect BuildOnboardingPromptRect(RenderRect modalRect, RenderPoint anchor, bool hasAnchor)
        {
            var effectiveAnchor = hasAnchor ? anchor : BuildDefaultOnboardingPromptAnchor(modalRect);
            return new RenderRect(
                Clamp(effectiveAnchor.X - 110f, modalRect.X + 12f, modalRect.X + modalRect.Width - 232f),
                Clamp(effectiveAnchor.Y - 26f, modalRect.Y + 12f, modalRect.Y + modalRect.Height - 88f),
                220f,
                72f);
        }

        public static ShellOnboardingOverlayInputResult EvaluateOnboardingInput(WorkbenchFrameInputSnapshot input, RenderRect fullscreenRect, RenderRect modalRect, RenderRect promptRect)
        {
            var result = new ShellOnboardingOverlayInputResult();
            if (!input.HasCurrentEvent)
            {
                return result;
            }

            var mousePosition = input.CurrentMousePosition;
            if (input.CurrentEventKind == WorkbenchInputEventKind.MouseDown)
            {
                if (RuntimeUiHitTest.Contains(modalRect, mousePosition))
                {
                    result.ActionKind = ShellOnboardingOverlayActionKind.HidePrompt;
                }
                else if (RuntimeUiHitTest.Contains(fullscreenRect, mousePosition) && !RuntimeUiHitTest.Contains(promptRect, mousePosition))
                {
                    result.ActionKind = ShellOnboardingOverlayActionKind.ShowPrompt;
                    result.PromptAnchor = mousePosition;
                    result.ShouldConsumeInput = true;
                }
            }
            else if (input.CurrentEventKind == WorkbenchInputEventKind.KeyDown && input.CurrentKey == WorkbenchInputKey.Escape)
            {
                result.ActionKind = ShellOnboardingOverlayActionKind.ShowPrompt;
                result.PromptAnchor = BuildDefaultOnboardingPromptAnchor(modalRect);
                result.ShouldConsumeInput = true;
            }

            return result;
        }

        public static RenderPoint BuildDefaultOnboardingPromptAnchor(RenderRect modalRect)
        {
            return new RenderPoint(modalRect.X + modalRect.Width - 120f, modalRect.Y + modalRect.Height - 54f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static float Min(float left, float right)
        {
            return left < right ? left : right;
        }
    }
}
