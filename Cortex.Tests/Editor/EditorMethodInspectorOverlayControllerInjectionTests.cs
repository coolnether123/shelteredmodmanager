using System.Reflection;
using Cortex.Modules.Editor;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Editor
{
    public sealed class EditorMethodInspectorOverlayControllerInjectionTests
    {
        [Fact]
        public void CodeViewSurface_UsesInjectedInspectorOverlayController()
        {
            var controller = new TestInspectorOverlayController();
            var surface = new CodeViewSurface(new TestEditorContextService(null), null, null, controller);

            var field = typeof(CodeViewSurface).GetField("_methodInspectorOverlayController", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.Same(controller, field.GetValue(surface));
        }

        [Fact]
        public void EditableCodeViewSurface_UsesInjectedInspectorOverlayController()
        {
            var controller = new TestInspectorOverlayController();
            var surface = new EditableCodeViewSurface(new TestEditorContextService(null), null, null, controller);

            var field = typeof(EditableCodeViewSurface).GetField("_methodInspectorOverlayController", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.Same(controller, field.GetValue(surface));
        }
    }
}
