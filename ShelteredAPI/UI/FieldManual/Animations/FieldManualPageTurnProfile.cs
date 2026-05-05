using ShelteredAPI.UI.FieldManual.Tooltips;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    /// <summary>
    /// Timing constants for page turns. The values are intentionally small so input
    /// feels responsive while still preventing repeated transitions in one frame.
    /// </summary>
    internal sealed class FieldManualPageTurnProfile
    {
        public readonly float HideDuration;
        public readonly float FlipDuration;
        public readonly float RebuildDelay;
        public readonly float LockoutDuration;

        public static readonly FieldManualPageTurnProfile VanillaClipboard =
            new FieldManualPageTurnProfile(0.08f, 0.34f, 0.16f, 0.40f);

        public FieldManualPageTurnProfile(float hideDuration, float flipDuration, float rebuildDelay, float lockoutDuration)
        {
            HideDuration = hideDuration < 0f ? 0f : hideDuration;
            FlipDuration = flipDuration < 0f ? 0f : flipDuration;
            RebuildDelay = rebuildDelay < 0f ? 0f : rebuildDelay;
            LockoutDuration = lockoutDuration < 0f ? 0f : lockoutDuration;
        }
    }
}
