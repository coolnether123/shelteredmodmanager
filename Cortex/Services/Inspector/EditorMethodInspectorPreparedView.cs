using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;

namespace Cortex.Services.Inspector
{
    internal sealed class EditorMethodInspectorPreparedView
    {
        public EditorContextSnapshot EditorContext;
        public CortexMethodInspectorState Inspector;
        public EditorCommandInvocation Invocation;
        public WorkbenchMethodRelationshipsSnapshot Relationships;
        public DocumentSession Session;
        public EditorCommandTarget Target;
        public MethodInspectorViewModel ViewModel;
    }
}
