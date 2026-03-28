using System;
using System.Collections.Generic;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal static class CortexDeveloperLog
    {
        private static string _lastSymbolEventKey = string.Empty;
        private static readonly Dictionary<string, string> _lastHoverPipelineStageEventKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> _lastHoverDiagnosticEventKeys = new Dictionary<string, string>(StringComparer.Ordinal);

        public static void WriteSymbolHoverPayload(string tokenText, LanguageServiceHoverResponse response)
        {
            WriteSymbolEvent(
                "hover-payload",
                (response != null ? response.DocumentPath : string.Empty) +
                "|" + (tokenText ?? string.Empty) +
                "|" + (response != null ? response.SymbolDisplay ?? string.Empty : string.Empty) +
                "|" + CountHoverParts(response),
                "HoverPayload",
                tokenText,
                response,
                null,
                string.Empty);
        }

        public static void WriteSymbolTooltipVisible(string surfaceKind, string hoverKey, string tokenText, LanguageServiceHoverResponse response)
        {
            WriteSymbolEvent(
                "tooltip-visible",
                (surfaceKind ?? string.Empty) +
                "|" + (hoverKey ?? string.Empty) +
                "|" + (response != null ? response.SymbolDisplay ?? string.Empty : string.Empty),
                "TooltipVisible",
                tokenText,
                response,
                null,
                "Surface='" + (surfaceKind ?? string.Empty) + "'");
        }

        public static void WriteSymbolContextTarget(string surfaceKind, string symbolText, LanguageServiceHoverResponse response, int absolutePosition)
        {
            WriteSymbolEvent(
                "context-target",
                (surfaceKind ?? string.Empty) +
                "|" + (symbolText ?? string.Empty) +
                "|" + absolutePosition +
                "|" + (response != null ? response.SymbolDisplay ?? string.Empty : string.Empty),
                "ContextTarget",
                symbolText,
                response,
                null,
                "Surface='" + (surfaceKind ?? string.Empty) + "', AbsolutePosition=" + absolutePosition + ", HoverResolved=" + (response != null && response.Success));
        }

        public static void WriteSymbolSelection(
            string documentPath,
            string tokenKey,
            string tokenText,
            string normalizedClassification,
            string resolvedKind,
            int lineNumber,
            int column)
        {
            var eventKey = "selection|" +
                (documentPath ?? string.Empty) + "|" +
                (tokenKey ?? string.Empty) + "|" +
                (resolvedKind ?? string.Empty);
            if (string.Equals(_lastSymbolEventKey, eventKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastSymbolEventKey = eventKey;
            MMLog.WriteInfo("[Cortex.Dev.Symbol] Event='Selection', Token='" +
                (tokenText ?? string.Empty).Trim() +
                "', Classification='" + (normalizedClassification ?? string.Empty) +
                "', ResolvedKind='" + (resolvedKind ?? string.Empty) +
                "', Line=" + lineNumber +
                ", Column=" + column + ".");
        }

        public static void WriteSymbolNavigation(string surfaceKind, LanguageServiceHoverDisplayPart part)
        {
            if (part == null)
            {
                return;
            }

            WriteSymbolEvent(
                "navigation",
                (surfaceKind ?? string.Empty) +
                "|" + (part.SymbolDisplay ?? part.Text ?? string.Empty) +
                "|" + (part.DefinitionDocumentPath ?? string.Empty) +
                "|" + (part.DefinitionRange != null ? part.DefinitionRange.Start.ToString() : string.Empty),
                "Navigation",
                part.Text ?? string.Empty,
                null,
                part,
                "Surface='" + (surfaceKind ?? string.Empty) + "'");
        }

        public static void WriteHoverPipelineStage(
            string stage,
            bool success,
            string surfaceId,
            string surfaceKind,
            string hoverKey,
            string contextKey,
            string documentPath,
            int documentVersion,
            int absolutePosition,
            string tokenText,
            string detail)
        {
            var normalizedHoverKey = hoverKey ?? string.Empty;
            var stageKey = (stage ?? string.Empty) + "|" +
                (surfaceId ?? string.Empty) + "|" +
                normalizedHoverKey + "|" +
                success;
            if (_lastHoverPipelineStageEventKeys.ContainsKey(stageKey))
            {
                return;
            }

            _lastHoverPipelineStageEventKeys[stageKey] = stageKey;
            MMLog.WriteInfo(
                "[Cortex.Hover.Trace] Stage='" + (stage ?? string.Empty) +
                "', Success=" + success +
                ", SurfaceId='" + (surfaceId ?? string.Empty) +
                "', SurfaceKind='" + (surfaceKind ?? string.Empty) +
                "', HoverKey='" + (hoverKey ?? string.Empty) +
                "', ContextKey='" + (contextKey ?? string.Empty) +
                "', Document='" + (documentPath ?? string.Empty) +
                "', DocumentVersion=" + documentVersion +
                ", AbsolutePosition=" + absolutePosition +
                ", Token='" + (tokenText ?? string.Empty) +
                "', Detail='" + (detail ?? string.Empty) + "'.");
        }

        public static void WriteHoverDiagnostic(
            string category,
            string hoverKey,
            string detail)
        {
            var eventKey = (category ?? string.Empty) + "|" +
                (hoverKey ?? string.Empty) + "|" +
                (detail ?? string.Empty);
            if (_lastHoverDiagnosticEventKeys.ContainsKey(eventKey))
            {
                return;
            }

            _lastHoverDiagnosticEventKeys[eventKey] = eventKey;
            MMLog.WriteInfo(
                "[Cortex.Hover.Diagnostic] Category='" + (category ?? string.Empty) +
                "', HoverKey='" + (hoverKey ?? string.Empty) +
                "', Detail='" + (detail ?? string.Empty) + "'.");
        }

        private static void WriteSymbolEvent(
            string eventName,
            string eventKey,
            string eventLabel,
            string tokenText,
            LanguageServiceHoverResponse response,
            LanguageServiceHoverDisplayPart part,
            string suffix)
        {
            if (string.IsNullOrEmpty(eventKey))
            {
                return;
            }

            if (string.Equals(_lastSymbolEventKey, eventKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastSymbolEventKey = eventKey;
            var effectiveKind = part != null && !string.IsNullOrEmpty(part.SymbolKind)
                ? part.SymbolKind
                : (response != null ? response.SymbolKind ?? string.Empty : string.Empty);
            var effectiveSymbol = part != null && !string.IsNullOrEmpty(part.SymbolDisplay)
                ? part.SymbolDisplay
                : (response != null ? response.SymbolDisplay ?? string.Empty : string.Empty);
            var definitionPath = part != null && !string.IsNullOrEmpty(part.DefinitionDocumentPath)
                ? part.DefinitionDocumentPath
                : (response != null ? response.DefinitionDocumentPath ?? string.Empty : string.Empty);
            var supplementalSections = part != null && part.SupplementalSections != null
                ? part.SupplementalSections
                : (response != null ? response.SupplementalSections : null);
            var parts = response != null && response.DisplayParts != null
                ? response.DisplayParts.Length
                : part != null ? 1 : 0;
            var interactiveParts = response != null
                ? CountInteractiveHoverParts(response)
                : part != null && part.IsInteractive ? 1 : 0;

            var message = "[Cortex.Dev.Symbol] Event='" + eventLabel +
                "', Token='" + (tokenText ?? string.Empty) +
                "', Symbol='" + effectiveSymbol +
                "', Kind='" + effectiveKind +
                "', Parts=" + parts +
                ", InteractiveParts=" + interactiveParts +
                ", SupplementalSections=" + CountSupplementalSections(supplementalSections) +
                ", DefinitionPath='" + definitionPath + "'";
            if (!string.IsNullOrEmpty(suffix))
            {
                message += ", " + suffix;
            }

            MMLog.WriteInfo(message + ".");
        }

        private static int CountHoverParts(LanguageServiceHoverResponse response)
        {
            return response != null && response.DisplayParts != null
                ? response.DisplayParts.Length
                : 0;
        }

        private static int CountInteractiveHoverParts(LanguageServiceHoverResponse response)
        {
            if (response == null || response.DisplayParts == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < response.DisplayParts.Length; i++)
            {
                if (response.DisplayParts[i] != null && response.DisplayParts[i].IsInteractive)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSupplementalSections(LanguageServiceHoverSection[] sections)
        {
            return sections != null ? sections.Length : 0;
        }
    }
}
