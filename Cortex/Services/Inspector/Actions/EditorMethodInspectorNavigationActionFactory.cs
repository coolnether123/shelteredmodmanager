using Cortex.Core.Models;
using Cortex.Presentation.Models;
using Cortex.Services.Inspector.Relationships;

namespace Cortex.Services.Inspector.Actions
{
    internal interface IEditorMethodInspectorNavigationActionFactory
    {
        MethodInspectorActionViewModel[] CreateRelationshipActions(EditorMethodRelationshipItem item);
    }

    internal sealed class EditorMethodInspectorNavigationActionFactory : IEditorMethodInspectorNavigationActionFactory
    {
        public MethodInspectorActionViewModel[] CreateRelationshipActions(EditorMethodRelationshipItem item)
        {
            if (item == null ||
                string.IsNullOrEmpty(item.SymbolKind) ||
                (string.IsNullOrEmpty(item.MetadataName) && string.IsNullOrEmpty(item.DocumentationCommentId)) ||
                string.IsNullOrEmpty(item.ContainingAssemblyName))
            {
                return new MethodInspectorActionViewModel[0];
            }

            return new[]
            {
                CreateAction(
                    EditorMethodInspectorNavigationActionCodec.Create(
                        item.SymbolKind,
                        item.MetadataName,
                        item.ContainingTypeName,
                        item.ContainingAssemblyName,
                        item.DocumentationCommentId,
                        item.DefinitionDocumentPath,
                        item.DefinitionRange),
                    "Open",
                    "Open this dependency or caller.")
            };
        }

        private static MethodInspectorActionViewModel CreateAction(string id, string label, string hint)
        {
            return new MethodInspectorActionViewModel
            {
                Id = id,
                Label = label,
                Hint = hint,
                Enabled = true
            };
        }
    }
}
