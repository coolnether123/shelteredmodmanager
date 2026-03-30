using Cortex.Shell;
using Cortex.Tests.Testing;
using Xunit;
using Cortex.Services.Editor.Presentation;

namespace Cortex.Tests.Editor
{
    public sealed class EditorCaretIndicatorPresentationServiceTests
    {
        [Fact]
        public void ShouldDrawIndicator_RequiresFocusedEditableEditorState()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorCaretIndicatorPresentationService();

                Assert.False(service.ShouldDrawIndicator(true, false, false, true, 0f));
                Assert.False(service.ShouldDrawIndicator(true, true, true, true, 0f));
                Assert.True(service.ShouldDrawIndicator(true, true, false, false, 0f));
                Assert.True(service.ShouldDrawIndicator(true, true, false, true, 0f));
            });
        }

        [Fact]
        public void ShouldDrawIndicator_BlinksOffDuringHiddenPhase()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var service = new EditorCaretIndicatorPresentationService();

                Assert.True(service.ShouldDrawIndicator(true, true, false, true, 0.1f));
                Assert.False(service.ShouldDrawIndicator(true, true, false, true, 0.9f));
            });
        }
    }
}
