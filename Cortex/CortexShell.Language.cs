using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private readonly EditorCompletionService _editorCompletionService = new EditorCompletionService();

        private void InitializeLanguageService(string smmBin, CortexSettings settings)
        {
            InitializeCompletionAugmentation(settings);
        }

        private void ShutdownLanguageService()
        {
            if (LanguageRuntimeControl != null)
            {
                LanguageRuntimeControl.Shutdown();
            }
        }

        private void UpdateLanguageService()
        {
            if (LanguageRuntimeControl != null)
            {
                LanguageRuntimeControl.Advance();
            }
        }

        private DocumentSession FindOpenDocument(string filePath)
        {
            return CortexModuleUtil.FindOpenDocument(_state, filePath);
        }
    }
}
