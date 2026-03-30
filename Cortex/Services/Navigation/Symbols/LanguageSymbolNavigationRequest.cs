using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Navigation.Symbols
{
    internal sealed class LanguageSymbolNavigationRequest
    {
        public string SymbolDisplay = string.Empty;
        public string SymbolKind = string.Empty;
        public string MetadataName = string.Empty;
        public string ContainingTypeName = string.Empty;
        public string ContainingAssemblyName = string.Empty;
        public string DocumentationCommentId = string.Empty;
        public string DefinitionDocumentPath = string.Empty;
        public LanguageServiceRange DefinitionRange;
    }
}
