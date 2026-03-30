using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Completion
{
    internal interface IEditorCompletionInteractionPolicy
    {
        bool ShouldTriggerCompletion(char character);
        bool ShouldContinueCompletion(DocumentSession session, int caretIndex);
        bool HasCompletionItems(LanguageServiceCompletionResponse response);
        int NormalizeSelectedIndex(LanguageServiceCompletionResponse response, int selectedIndex);
        bool ApplyCompletion(DocumentSession session, IEditorService editorService, LanguageServiceCompletionResponse response, LanguageServiceCompletionItem item);
    }
}
