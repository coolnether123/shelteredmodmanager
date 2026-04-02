using System;
using System.Collections.Generic;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.PopupMenus
{
    public static class PopupMenuInteractionController
    {
        public const float ScrollWheelStep = 18f;
        public const float AnalogScrollScale = 10f;

        public static void Reset(PopupMenuRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            state.ScrollOffset = 0f;
            state.MenuKey = string.Empty;
            state.PendingScrollDelta = 0f;
            state.PressedCommandId = string.Empty;
            state.LastAnalogScrollFrameId = -1;
            ClearQueuedPointerState(state);
        }

        public static void ResetForMenu(PopupMenuRuntimeState state, string headerText, IList<PopupMenuItemModel> items)
        {
            if (state == null)
            {
                return;
            }

            var menuKey = PopupMenuLayoutPlanner.BuildMenuKey(headerText, items);
            if (string.Equals(state.MenuKey, menuKey, StringComparison.Ordinal))
            {
                return;
            }

            state.MenuKey = menuKey;
            state.ScrollOffset = 0f;
            state.PendingScrollDelta = 0f;
            state.PressedCommandId = string.Empty;
        }

        public static void QueueScrollDelta(PopupMenuRuntimeState state, float delta)
        {
            if (state == null)
            {
                return;
            }

            state.PendingScrollDelta += delta * ScrollWheelStep;
        }

        public static PopupMenuCaptureResult TryCapturePointerInput(PopupMenuRuntimeState state, RenderRect menuRect, RuntimeUiPointerFrameInput input)
        {
            var result = new PopupMenuCaptureResult();
            if (state == null || !input.HasPointer)
            {
                return result;
            }

            var isInsideMenu = RuntimeUiHitTest.Contains(menuRect, input.PointerPosition);
            if (input.EventKind == RuntimeUiPointerEventKind.Down)
            {
                if (isInsideMenu)
                {
                    QueuePointerDown(state, input.PointerButton, input.PointerPosition);
                }
                else
                {
                    state.QueuedOutsideClickClose = true;
                }

                result.Captured = true;
                result.ShouldConsumeInput = true;
                return result;
            }

            if (input.EventKind == RuntimeUiPointerEventKind.Up)
            {
                if (isInsideMenu)
                {
                    QueuePointerUp(state, input.PointerButton, input.PointerPosition);
                }

                result.Captured = true;
                result.ShouldConsumeInput = true;
                return result;
            }

            if (!isInsideMenu)
            {
                return result;
            }

            if (input.EventKind == RuntimeUiPointerEventKind.Scroll && Math.Abs(input.WheelScrollDelta) > 0.0001f)
            {
                state.PendingScrollDelta += input.WheelScrollDelta * ScrollWheelStep;
                result.Captured = true;
                result.ShouldConsumeInput = true;
                return result;
            }

            if (input.FrameId == state.LastAnalogScrollFrameId || Math.Abs(input.AnalogScrollDelta) <= 0.0001f)
            {
                return result;
            }

            state.LastAnalogScrollFrameId = input.FrameId;
            state.PendingScrollDelta += -input.AnalogScrollDelta * AnalogScrollScale * ScrollWheelStep;
            result.Captured = true;
            return result;
        }

        public static PopupMenuPreparedFrame PrepareFrame(PopupMenuRuntimeState state, RuntimeUiPointerFrameInput input, float maxScroll, bool pointerInsideMenu)
        {
            var prepared = new PopupMenuPreparedFrame();
            prepared.MaxScroll = Math.Max(0f, maxScroll);
            prepared.HasScroll = prepared.MaxScroll > 0f;
            if (state == null)
            {
                return prepared;
            }

            if (prepared.HasScroll && pointerInsideMenu)
            {
                QueueAnalogScroll(state, input);
            }

            state.ScrollOffset = PopupMenuLayoutPlanner.ClampScrollOffset(state.ScrollOffset + state.PendingScrollDelta, prepared.MaxScroll);
            state.PendingScrollDelta = 0f;

            if (input.EventKind == RuntimeUiPointerEventKind.Scroll && prepared.HasScroll && pointerInsideMenu && Math.Abs(input.WheelScrollDelta) > 0.0001f)
            {
                state.ScrollOffset = PopupMenuLayoutPlanner.ClampScrollOffset(state.ScrollOffset + (input.WheelScrollDelta * ScrollWheelStep), prepared.MaxScroll);
            }

            prepared.ScrollOffset = state.ScrollOffset;
            prepared.HasQueuedPointerDown = state.HasQueuedPointerDown;
            prepared.HasQueuedPointerUp = state.HasQueuedPointerUp;
            prepared.QueuedPointerDownButton = state.QueuedPointerDownButton;
            prepared.QueuedPointerUpButton = state.QueuedPointerUpButton;
            prepared.QueuedPointerDownPosition = state.QueuedPointerDownPosition;
            prepared.QueuedPointerUpPosition = state.QueuedPointerUpPosition;
            return prepared;
        }

        public static PopupMenuItemInteractionResult EvaluateItemInteraction(
            PopupMenuRuntimeState state,
            RuntimeUiPointerFrameInput input,
            string commandId,
            bool enabled,
            bool pointerOverItem,
            bool queuedPointerDownOnItem,
            bool queuedPointerUpOnItem)
        {
            var result = new PopupMenuItemInteractionResult();
            if (state == null || !enabled)
            {
                return result;
            }

            var effectiveCommandId = commandId ?? string.Empty;
            result.IsPressedVisual =
                !string.IsNullOrEmpty(state.PressedCommandId) &&
                string.Equals(state.PressedCommandId, effectiveCommandId, StringComparison.Ordinal);

            var pointerPressed = (input.EventKind == RuntimeUiPointerEventKind.Down && input.PointerButton == 0 && pointerOverItem) || queuedPointerDownOnItem;
            if (pointerPressed)
            {
                state.PressedCommandId = effectiveCommandId;
                result.IsPressedVisual = true;
            }

            var canActivateFromRelease = string.IsNullOrEmpty(state.PressedCommandId) ||
                string.Equals(state.PressedCommandId, effectiveCommandId, StringComparison.Ordinal);
            var pointerReleased = (input.EventKind == RuntimeUiPointerEventKind.Up && input.PointerButton == 0 && pointerOverItem) || queuedPointerUpOnItem;
            if (pointerReleased && canActivateFromRelease)
            {
                result.ActivatedCommandId = effectiveCommandId;
                result.ShouldClose = true;
                state.PressedCommandId = string.Empty;
                result.IsPressedVisual = false;
            }

            if ((input.EventKind == RuntimeUiPointerEventKind.Up && input.PointerButton == 0) ||
                (state.HasQueuedPointerUp && state.QueuedPointerUpButton == 0))
            {
                state.PressedCommandId = string.Empty;
                result.IsPressedVisual = false;
            }

            return result;
        }

        public static bool HandleScrollChromeInteraction(
            PopupMenuRuntimeState state,
            RuntimeUiPointerFrameInput input,
            float maxScroll,
            RenderRect upRect,
            RenderRect downRect,
            RenderRect trackRect,
            RenderRect thumbRect,
            float viewportHeight,
            RenderPoint queuedPointerUpPosition)
        {
            if (state == null)
            {
                return false;
            }

            var hasCurrentMouseUp = input.EventKind == RuntimeUiPointerEventKind.Up && input.PointerButton == 0;
            var hasQueuedMouseUp = state.HasQueuedPointerUp && state.QueuedPointerUpButton == 0;
            if (!hasCurrentMouseUp && !hasQueuedMouseUp)
            {
                return false;
            }

            var effectivePointer = hasQueuedMouseUp ? queuedPointerUpPosition : input.PointerPosition;
            if (RuntimeUiHitTest.Contains(upRect, effectivePointer))
            {
                ApplyScrollStep(state, -ScrollWheelStep * 4f, maxScroll);
                return true;
            }

            if (RuntimeUiHitTest.Contains(downRect, effectivePointer))
            {
                ApplyScrollStep(state, ScrollWheelStep * 4f, maxScroll);
                return true;
            }

            if (!RuntimeUiHitTest.Contains(trackRect, effectivePointer))
            {
                return false;
            }

            if (effectivePointer.Y < thumbRect.Y)
            {
                ApplyScrollStep(state, -viewportHeight * 0.75f, maxScroll);
                return true;
            }

            if (effectivePointer.Y > thumbRect.Y + thumbRect.Height)
            {
                ApplyScrollStep(state, viewportHeight * 0.75f, maxScroll);
                return true;
            }

            return false;
        }

        public static PopupMenuFrameResult CompleteFrame(PopupMenuRuntimeState state, RuntimeUiPointerFrameInput input, bool pointerInsideMenu)
        {
            var result = new PopupMenuFrameResult();
            if (state == null)
            {
                return result;
            }

            result.ShouldClose = state.QueuedOutsideClickClose ||
                (input.EventKind == RuntimeUiPointerEventKind.Down && input.HasPointer && !pointerInsideMenu);
            ClearQueuedPointerState(state);
            return result;
        }

        private static void ApplyScrollStep(PopupMenuRuntimeState state, float delta, float maxScroll)
        {
            state.ScrollOffset = PopupMenuLayoutPlanner.ClampScrollOffset(state.ScrollOffset + delta, maxScroll);
        }

        private static void QueueAnalogScroll(PopupMenuRuntimeState state, RuntimeUiPointerFrameInput input)
        {
            if (state == null || input.FrameId == state.LastAnalogScrollFrameId || Math.Abs(input.AnalogScrollDelta) <= 0.0001f)
            {
                return;
            }

            state.LastAnalogScrollFrameId = input.FrameId;
            state.PendingScrollDelta += -input.AnalogScrollDelta * AnalogScrollScale * ScrollWheelStep;
        }

        private static void QueuePointerDown(PopupMenuRuntimeState state, int button, RenderPoint position)
        {
            state.HasQueuedPointerDown = true;
            state.QueuedPointerDownButton = button;
            state.QueuedPointerDownPosition = position;
        }

        private static void QueuePointerUp(PopupMenuRuntimeState state, int button, RenderPoint position)
        {
            state.HasQueuedPointerUp = true;
            state.QueuedPointerUpButton = button;
            state.QueuedPointerUpPosition = position;
        }

        private static void ClearQueuedPointerState(PopupMenuRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            state.HasQueuedPointerDown = false;
            state.HasQueuedPointerUp = false;
            state.QueuedOutsideClickClose = false;
            state.QueuedPointerDownButton = -1;
            state.QueuedPointerUpButton = -1;
            state.QueuedPointerDownPosition = RenderPoint.Zero;
            state.QueuedPointerUpPosition = RenderPoint.Zero;
        }
    }
}
