using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.SignatureHelp
{
    internal interface IEditorSignatureHelpService
    {
        bool ShouldDispatch(CortexSignatureHelpInteractionState editorState, bool requestInFlight);
        LanguageServiceSignatureHelpRequest BuildWorkerRequest(DocumentSession session, CortexSettings settings, CortexProjectDefinition project, string[] sourceRoots, CortexSignatureHelpInteractionState editorState);
        bool QueueRequest(DocumentSession session, CortexSignatureHelpInteractionState editorState, IEditorService editorService, bool explicitInvocation, string triggerCharacter);
        bool AcceptResponse(CortexSignatureHelpInteractionState editorState, DocumentSession target, PendingLanguageSignatureHelpRequest pending, LanguageServiceSignatureHelpResponse response);
        bool HasVisibleSignatureHelp(CortexSignatureHelpInteractionState editorState, DocumentSession session);
        bool ShouldTriggerSignatureHelp(char character);
        bool ShouldDismissAfterText(char character);
        void Reset(CortexSignatureHelpInteractionState editorState);
        void ClearActive(CortexSignatureHelpInteractionState editorState);
    }
}
