using Cortex.Core.Models;

namespace Cortex.Services.Editor.Presentation
{
    internal sealed class EditorSemanticTokenMappingService
    {
        public string NormalizeClassification(string classification)
        {
            return SemanticTokenClassification.Normalize(classification);
        }

        public string NormalizeSemanticTokenType(string semanticTokenType)
        {
            return SemanticTokenClassification.FromLspSemanticTokenType(semanticTokenType);
        }

        public bool IsGenericClassification(string classification)
        {
            return SemanticTokenClassification.IsGeneric(classification);
        }

        public string ResolvePresentationClassification(string classification, string semanticTokenType)
        {
            var normalizedClassification = NormalizeClassification(classification);
            if (!IsGenericClassification(normalizedClassification))
            {
                return normalizedClassification;
            }

            var normalizedSemanticTokenType = NormalizeSemanticTokenType(semanticTokenType);
            if (!string.IsNullOrEmpty(normalizedSemanticTokenType))
            {
                return normalizedSemanticTokenType;
            }

            return normalizedClassification;
        }

        public string GetSemanticTokenType(string classification)
        {
            return SemanticTokenClassification.ToLspSemanticTokenType(classification);
        }
    }
}
