using System;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class EditorSelectionInspectionService
    {
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private string _lastSelectionLogKey = string.Empty;

        public void ApplySelection(
            CortexShellState state,
            string documentPath,
            string tokenKey,
            string tokenText,
            string tokenClassification,
            int lineNumber,
            int column,
            LanguageServiceHoverResponse hoverResponse)
        {
            var inspection = InspectSelection(
                documentPath,
                tokenKey,
                tokenText,
                tokenClassification,
                lineNumber,
                column,
                hoverResponse);
            if (!string.IsNullOrEmpty(inspection.LogKey) &&
                !string.Equals(_lastSelectionLogKey, inspection.LogKey, StringComparison.Ordinal))
            {
                _lastSelectionLogKey = inspection.LogKey;
                CortexDeveloperLog.WriteSymbolSelection(
                    documentPath,
                    tokenKey,
                    tokenText,
                    inspection.NormalizedClassification,
                    inspection.ResolvedKind,
                    lineNumber,
                    column);
            }

            if (state != null)
            {
                state.StatusMessage = inspection.StatusMessage;
            }
        }

        private EditorSelectionInspectionResult InspectSelection(
            string documentPath,
            string tokenKey,
            string tokenText,
            string tokenClassification,
            int lineNumber,
            int column,
            LanguageServiceHoverResponse hoverResponse)
        {
            var normalizedClassification = _classificationPresentationService.NormalizeClassification(tokenClassification);
            var resolvedKind = hoverResponse != null && !string.IsNullOrEmpty(hoverResponse.SymbolKind)
                ? hoverResponse.SymbolKind
                : normalizedClassification;
            if (string.IsNullOrEmpty(resolvedKind))
            {
                resolvedKind = "Unresolved";
            }

            var trimmedTokenText = tokenText != null ? tokenText.Trim() : string.Empty;
            return new EditorSelectionInspectionResult
            {
                StatusMessage = "Selected " + ResolveStatusKind(resolvedKind) + " " + trimmedTokenText + ".",
                LogKey = (documentPath ?? string.Empty) +
                    "|" + (tokenKey ?? string.Empty) +
                    "|" + normalizedClassification +
                    "|" + resolvedKind,
                NormalizedClassification = normalizedClassification,
                ResolvedKind = resolvedKind
            };
        }

        private static string ResolveStatusKind(string resolvedKind)
        {
            return !string.IsNullOrEmpty(resolvedKind)
                ? resolvedKind
                : "symbol";
        }

        private sealed class EditorSelectionInspectionResult
        {
            public string StatusMessage = string.Empty;
            public string LogKey = string.Empty;
            public string NormalizedClassification = string.Empty;
            public string ResolvedKind = string.Empty;
        }
    }
}
