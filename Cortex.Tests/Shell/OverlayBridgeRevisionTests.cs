using Cortex.Bridge;
using Xunit;

namespace Cortex.Tests.Shell
{
    public sealed class OverlayBridgeRevisionTests
    {
        [Fact]
        public void OverlayRevisionTracker_AcceptsIncreasingRevisions_AndRejectsStaleFrames()
        {
            var tracker = new OverlayRevisionTracker();

            Assert.True(tracker.ShouldAccept(1));
            Assert.Equal(1, tracker.LatestAcceptedRevision);
            Assert.False(tracker.ShouldAccept(1));
            Assert.False(tracker.ShouldAccept(0));
            Assert.True(tracker.ShouldAccept(2));
            Assert.Equal(2, tracker.LatestAcceptedRevision);
        }
    }
}
