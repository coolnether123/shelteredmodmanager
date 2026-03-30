using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Navigation;
using Cortex.Services.Navigation.Symbols;
using Cortex;

namespace Cortex.Services.Inspector.Actions
{
    internal sealed class EditorMethodInspectorNavigationActionHandler
    {
        public bool TryHandle(CortexShellState state, ICortexNavigationService navigationService, string activationId)
        {
            string symbolKind;
            string metadataName;
            string containingTypeName;
            string containingAssemblyName;
            string documentationCommentId;
            string definitionDocumentPath;
            LanguageServiceRange definitionRange;
            if (!EditorMethodInspectorNavigationActionCodec.TryParse(
                activationId,
                out symbolKind,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId,
                out definitionDocumentPath,
                out definitionRange))
            {
                return false;
            }

            MMLog.WriteInfo("[Cortex.Inspector] Handling symbol navigation action. ActivationId='" + activationId + "', MetadataName='" +
                metadataName + "', Type='" + containingTypeName + "', Assembly='" + containingAssemblyName +
                "', Definition='" + definitionDocumentPath + "'.");
            var navigationRequest = new LanguageSymbolNavigationRequest
            {
                SymbolDisplay = metadataName,
                SymbolKind = symbolKind,
                MetadataName = metadataName,
                ContainingTypeName = containingTypeName,
                ContainingAssemblyName = containingAssemblyName,
                DocumentationCommentId = documentationCommentId,
                DefinitionDocumentPath = definitionDocumentPath,
                DefinitionRange = definitionRange
            };
            if (navigationService != null)
            {
                navigationService.OpenLanguageSymbolTarget(
                    state,
                    navigationRequest.SymbolDisplay,
                    navigationRequest.SymbolKind,
                    navigationRequest.MetadataName,
                    navigationRequest.ContainingTypeName,
                    navigationRequest.ContainingAssemblyName,
                    navigationRequest.DocumentationCommentId,
                    navigationRequest.DefinitionDocumentPath,
                    navigationRequest.DefinitionRange,
                    "Opened relationship target.",
                    "Could not open relationship target.");
            }

            return true;
        }
    }
}
