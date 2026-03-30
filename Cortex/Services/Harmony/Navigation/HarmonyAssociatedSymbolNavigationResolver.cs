using System;
using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Harmony.Inspection;
using Cortex.Services.Navigation.Symbols;

namespace Cortex.Services.Harmony.Navigation
{
    internal static class HarmonyAssociatedSymbolNavigationResolver
    {
        private const int MinimumMatchScore = 180;

        public static bool TryResolvePreferredPatchTarget(
            CortexShellState state,
            LanguageSymbolNavigationRequest request,
            out HarmonyPatchNavigationTarget target)
        {
            target = null;
            if (state == null ||
                state.Harmony == null ||
                request == null ||
                string.IsNullOrEmpty(request.DefinitionDocumentPath) ||
                !CortexModuleUtil.IsDecompilerDocumentPath(state, request.DefinitionDocumentPath))
            {
                return false;
            }

            var summaries = state.Harmony.SnapshotMethods ?? new HarmonyMethodPatchSummary[0];
            if (summaries.Length == 0)
            {
                return false;
            }

            var runtimeAssemblyName = GetRuntimeAssemblyNameHint(state, request);
            var bestScore = 0;
            for (var i = 0; i < summaries.Length; i++)
            {
                var summary = summaries[i];
                if (summary == null || !summary.IsPatched)
                {
                    continue;
                }

                var preferredTarget = HarmonyPatchOwnerAssociationMatcher.GetPreferredPatchNavigationTarget(summary, state.SelectedProject);
                if (preferredTarget == null)
                {
                    continue;
                }

                var score = ScoreSummary(state, request, summary, runtimeAssemblyName);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                target = preferredTarget;
            }

            return target != null && bestScore >= MinimumMatchScore;
        }

        private static int ScoreSummary(
            CortexShellState state,
            LanguageSymbolNavigationRequest request,
            HarmonyMethodPatchSummary summary,
            string runtimeAssemblyName)
        {
            var score = 0;
            if (MatchesMethodName(request.MetadataName, summary.MethodName) ||
                MatchesMethodName(request.MetadataName, summary.Target != null ? summary.Target.MethodName : string.Empty))
            {
                score += 120;
            }

            score += ScoreTypeMatch(
                request.ContainingTypeName,
                summary.DeclaringType,
                summary.Target != null ? summary.Target.DeclaringTypeName : string.Empty);

            if (MatchesAssemblyName(runtimeAssemblyName, summary.AssemblyPath))
            {
                score += 120;
            }

            if (MatchesDocumentationCommentId(request.DocumentationCommentId, summary))
            {
                score += 80;
            }

            if (!string.IsNullOrEmpty(request.DefinitionDocumentPath))
            {
                var requestedFileName = Path.GetFileNameWithoutExtension(request.DefinitionDocumentPath) ?? string.Empty;
                var summaryTypeLeaf = GetTypeLeafName(summary.DeclaringType);
                if (!string.IsNullOrEmpty(requestedFileName) &&
                    !string.IsNullOrEmpty(summaryTypeLeaf) &&
                    string.Equals(requestedFileName, summaryTypeLeaf, StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                }
            }

            return score;
        }

        private static int ScoreTypeMatch(string requestedTypeName, string summaryTypeName, string targetTypeName)
        {
            var normalizedRequested = NormalizeTypeName(requestedTypeName);
            if (string.IsNullOrEmpty(normalizedRequested))
            {
                return 0;
            }

            var normalizedSummary = NormalizeTypeName(!string.IsNullOrEmpty(summaryTypeName) ? summaryTypeName : targetTypeName);
            if (string.IsNullOrEmpty(normalizedSummary))
            {
                return 0;
            }

            if (string.Equals(normalizedRequested, normalizedSummary, StringComparison.OrdinalIgnoreCase))
            {
                return 120;
            }

            var requestedLeaf = GetTypeLeafName(normalizedRequested);
            var summaryLeaf = GetTypeLeafName(normalizedSummary);
            return !string.IsNullOrEmpty(requestedLeaf) &&
                string.Equals(requestedLeaf, summaryLeaf, StringComparison.OrdinalIgnoreCase)
                ? 80
                : 0;
        }

        private static bool MatchesDocumentationCommentId(string documentationCommentId, HarmonyMethodPatchSummary summary)
        {
            if (string.IsNullOrEmpty(documentationCommentId) || summary == null)
            {
                return false;
            }

            var normalizedDocumentationId = NormalizeDocumentationCommentId(documentationCommentId);
            if (string.IsNullOrEmpty(normalizedDocumentationId))
            {
                return false;
            }

            var normalizedSummaryType = NormalizeTypeName(summary.DeclaringType);
            var methodName = summary.MethodName ?? string.Empty;
            if (!string.IsNullOrEmpty(normalizedSummaryType) &&
                !string.IsNullOrEmpty(methodName) &&
                string.Equals(normalizedDocumentationId, normalizedSummaryType + "." + methodName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var summaryLeaf = GetTypeLeafName(normalizedSummaryType);
            return !string.IsNullOrEmpty(summaryLeaf) &&
                !string.IsNullOrEmpty(methodName) &&
                normalizedDocumentationId.EndsWith(summaryLeaf + "." + methodName, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetRuntimeAssemblyNameHint(CortexShellState state, LanguageSymbolNavigationRequest request)
        {
            var fromCachePath = GetDecompilerAssemblyName(state, request != null ? request.DefinitionDocumentPath : string.Empty);
            if (!string.IsNullOrEmpty(fromCachePath))
            {
                return fromCachePath;
            }

            return request != null ? request.ContainingAssemblyName ?? string.Empty : string.Empty;
        }

        private static string GetDecompilerAssemblyName(CortexShellState state, string definitionDocumentPath)
        {
            if (string.IsNullOrEmpty(definitionDocumentPath))
            {
                return string.Empty;
            }

            try
            {
                var fullPath = Path.GetFullPath(definitionDocumentPath);
                if (state != null && state.Settings != null && !string.IsNullOrEmpty(state.Settings.DecompilerCachePath))
                {
                    var cacheRoot = Path.GetFullPath(state.Settings.DecompilerCachePath)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                        Path.DirectorySeparatorChar;
                    if (fullPath.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        return GetFirstRelativeSegment(fullPath.Substring(cacheRoot.Length));
                    }
                }

                var marker = Path.DirectorySeparatorChar + "cortex_cache" + Path.DirectorySeparatorChar;
                var markerIndex = fullPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex >= 0)
                {
                    return GetFirstRelativeSegment(fullPath.Substring(markerIndex + marker.Length));
                }

                var altMarker = Path.AltDirectorySeparatorChar + "cortex_cache" + Path.AltDirectorySeparatorChar;
                markerIndex = fullPath.IndexOf(altMarker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex >= 0)
                {
                    return GetFirstRelativeSegment(fullPath.Substring(markerIndex + altMarker.Length));
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string GetFirstRelativeSegment(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return string.Empty;
            }

            var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            var segments = relativePath.Split(separators, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[0] : string.Empty;
        }

        private static bool MatchesAssemblyName(string requestedAssemblyName, string summaryAssemblyPath)
        {
            if (string.IsNullOrEmpty(requestedAssemblyName) || string.IsNullOrEmpty(summaryAssemblyPath))
            {
                return false;
            }

            var summaryAssemblyName = Path.GetFileNameWithoutExtension(summaryAssemblyPath) ?? string.Empty;
            return string.Equals(requestedAssemblyName, summaryAssemblyName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(requestedAssemblyName, summaryAssemblyPath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesMethodName(string requestedMethodName, string summaryMethodName)
        {
            return !string.IsNullOrEmpty(requestedMethodName) &&
                !string.IsNullOrEmpty(summaryMethodName) &&
                string.Equals(requestedMethodName, summaryMethodName, StringComparison.Ordinal);
        }

        private static string NormalizeDocumentationCommentId(string documentationCommentId)
        {
            if (string.IsNullOrEmpty(documentationCommentId))
            {
                return string.Empty;
            }

            var normalized = documentationCommentId;
            if (normalized.Length > 2 && normalized[1] == ':')
            {
                normalized = normalized.Substring(2);
            }

            var parameterIndex = normalized.IndexOf('(');
            if (parameterIndex >= 0)
            {
                normalized = normalized.Substring(0, parameterIndex);
            }

            return normalized.Replace('+', '.').Replace("global::", string.Empty).Trim();
        }

        private static string NormalizeTypeName(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                ? string.Empty
                : typeName.Replace('+', '.').Replace("global::", string.Empty).Trim();
        }

        private static string GetTypeLeafName(string typeName)
        {
            var normalized = NormalizeTypeName(typeName);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            var lastDot = normalized.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < normalized.Length
                ? normalized.Substring(lastDot + 1)
                : normalized;
        }
    }
}
