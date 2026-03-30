using Cortex.Core.Models;
using Cortex.Services.Navigation;

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
            if (!EditorMethodInspectorNavigationActionCodec.TryParse(
                activationId,
                out symbolKind,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId))
            {
                return false;
            }

            if (navigationService != null)
            {
                navigationService.OpenLanguageSymbolTarget(
                    state,
                    metadataName,
                    symbolKind,
                    metadataName,
                    containingTypeName,
                    containingAssemblyName,
                    documentationCommentId,
                    string.Empty,
                    null,
                    "Opened relationship target.",
                    "Could not open relationship target.");
            }

            return true;
        }
    }
}
