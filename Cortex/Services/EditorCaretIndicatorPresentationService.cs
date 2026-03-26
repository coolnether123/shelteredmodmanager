using UnityEngine;

namespace Cortex.Services
{
    internal sealed class EditorCaretIndicatorPresentationService
    {
        private const float BlinkPeriodSeconds = 1.2f;
        private const float VisibleFraction = 0.55f;

        public bool HasKeyboardOwnership(
            bool hasEditorFocus,
            bool hasOtherImGuiFocus,
            bool isEditorContainerFocused)
        {
            return hasEditorFocus &&
                !hasOtherImGuiFocus;
        }

        public bool IsReadyForInput(
            bool hasEditorFocus,
            bool isEditable,
            bool hasOtherImGuiFocus,
            bool isEditorContainerFocused)
        {
            return isEditable &&
                HasKeyboardOwnership(
                    hasEditorFocus,
                    hasOtherImGuiFocus,
                    isEditorContainerFocused);
        }

        public bool ShouldDrawIndicator(
            bool hasEditorFocus,
            bool isEditable,
            bool hasOtherImGuiFocus,
            bool isEditorContainerFocused)
        {
            return ShouldDrawIndicator(
                hasEditorFocus,
                isEditable,
                hasOtherImGuiFocus,
                isEditorContainerFocused,
                Time.realtimeSinceStartup);
        }

        public bool ShouldDrawIndicator(
            bool hasEditorFocus,
            bool isEditable,
            bool hasOtherImGuiFocus,
            bool isEditorContainerFocused,
            float elapsedSeconds)
        {
            if (!IsReadyForInput(hasEditorFocus, isEditable, hasOtherImGuiFocus, isEditorContainerFocused))
            {
                return false;
            }

            var cycle = Mathf.Repeat(elapsedSeconds, BlinkPeriodSeconds);
            return cycle < BlinkPeriodSeconds * VisibleFraction;
        }
    }
}
