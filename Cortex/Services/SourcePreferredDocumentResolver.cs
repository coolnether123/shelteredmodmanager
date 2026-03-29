using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;

namespace Cortex.Services
{
    internal sealed class SourcePreferredDocumentResolver
    {
        public bool TryResolveFromSymbol(
            CortexShellState state,
            ISourceLookupIndex sourceLookupIndex,
            string existingDocumentPath,
            string documentationCommentId,
            string containingTypeName,
            string metadataName,
            string symbolKind,
            out string sourceDocumentPath)
        {
            sourceDocumentPath = string.Empty;

            if (!string.IsNullOrEmpty(existingDocumentPath) &&
                File.Exists(existingDocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, existingDocumentPath))
            {
                sourceDocumentPath = existingDocumentPath;
                return true;
            }

            var fullTypeName = ExtractFullTypeName(documentationCommentId, containingTypeName, metadataName, symbolKind);
            return TryResolveFromTypeName(state, sourceLookupIndex, fullTypeName, out sourceDocumentPath);
        }

        public bool TryResolveFromTypeName(
            CortexShellState state,
            ISourceLookupIndex sourceLookupIndex,
            string fullTypeName,
            out string sourceDocumentPath)
        {
            sourceDocumentPath = string.Empty;
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return false;
            }

            var typeLeafName = GetTypeLeafName(fullTypeName);
            if (string.IsNullOrEmpty(typeLeafName))
            {
                return false;
            }

            var roots = SourceRootSetBuilder.Build(state != null ? state.SelectedProject : null, state != null ? state.Settings : null, SourceRootFlags.ProjectSource | SourceRootFlags.ProjectDirectory | SourceRootFlags.Workspace | SourceRootFlags.Additional);
            if (roots.Count == 0)
            {
                return false;
            }

            var candidateFileName = typeLeafName + ".cs";
            var candidates = new List<string>();

            if (sourceLookupIndex != null)
            {
                var indexedCandidate = sourceLookupIndex.ResolvePath(roots, candidateFileName);
                if (!string.IsNullOrEmpty(indexedCandidate))
                {
                    AddCandidate(candidates, indexedCandidate);
                }
            }

            for (var i = 0; i < roots.Count; i++)
            {
                CollectCandidates(roots[i], candidateFileName, candidates);
            }

            var bestPath = string.Empty;
            var bestScore = int.MinValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                var score = ScoreCandidate(state, candidate, fullTypeName, typeLeafName);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = candidate;
                }
            }

            if (string.IsNullOrEmpty(bestPath) || bestScore < 0)
            {
                return false;
            }

            sourceDocumentPath = bestPath;
            return true;
        }

        private static string ExtractFullTypeName(string documentationCommentId, string containingTypeName, string metadataName, string symbolKind)
        {
            if (!string.IsNullOrEmpty(documentationCommentId))
            {
                if (documentationCommentId.StartsWith("T:", StringComparison.Ordinal))
                {
                    return documentationCommentId.Substring(2).Replace('+', '.');
                }

                if (documentationCommentId.StartsWith("M:", StringComparison.Ordinal) ||
                    documentationCommentId.StartsWith("P:", StringComparison.Ordinal) ||
                    documentationCommentId.StartsWith("F:", StringComparison.Ordinal) ||
                    documentationCommentId.StartsWith("E:", StringComparison.Ordinal))
                {
                    var body = documentationCommentId.Substring(2);
                    var parameterIndex = body.IndexOf('(');
                    if (parameterIndex >= 0)
                    {
                        body = body.Substring(0, parameterIndex);
                    }

                    var lastDot = body.LastIndexOf('.');
                    if (lastDot > 0)
                    {
                        return body.Substring(0, lastDot).Replace('+', '.');
                    }
                }
            }

            if (!string.IsNullOrEmpty(containingTypeName))
            {
                return containingTypeName.Replace('+', '.');
            }

            if (IsTypeLikeSymbol(symbolKind) && !string.IsNullOrEmpty(metadataName))
            {
                return metadataName.Replace('+', '.');
            }

            return string.Empty;
        }

        private static void AddCandidate(List<string> candidates, string path)
        {
            if (candidates == null || string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath.IndexOf("cortex_cache", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                for (var i = 0; i < candidates.Count; i++)
                {
                    if (string.Equals(candidates[i], fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                if (File.Exists(fullPath))
                {
                    candidates.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static void CollectCandidates(string rootPath, string candidateFileName, List<string> candidates)
        {
            if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(candidateFileName) || candidates == null)
            {
                return;
            }

            try
            {
                if (!Directory.Exists(rootPath) || rootPath.IndexOf("cortex_cache", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return;
                }

                var files = Directory.GetFiles(rootPath, candidateFileName, SearchOption.AllDirectories);
                for (var i = 0; i < files.Length; i++)
                {
                    AddCandidate(candidates, files[i]);
                }
            }
            catch
            {
            }
        }

        private static int ScoreCandidate(CortexShellState state, string candidatePath, string fullTypeName, string typeLeafName)
        {
            if (string.IsNullOrEmpty(candidatePath) || string.IsNullOrEmpty(fullTypeName) || string.IsNullOrEmpty(typeLeafName))
            {
                return -1;
            }

            var score = 0;
            var selectedSourceRoot = state != null && state.SelectedProject != null
                ? state.SelectedProject.SourceRootPath ?? string.Empty
                : string.Empty;
            if (!string.IsNullOrEmpty(selectedSourceRoot) && PathStartsWith(candidatePath, selectedSourceRoot))
            {
                score += 400;
            }

            var namespaceName = GetNamespaceName(fullTypeName);
            var text = ReadAllTextSafe(candidatePath);
            if (string.IsNullOrEmpty(text))
            {
                return score;
            }

            if (!string.IsNullOrEmpty(namespaceName) &&
                text.IndexOf("namespace " + namespaceName, StringComparison.Ordinal) >= 0)
            {
                score += 220;
            }

            if (Regex.IsMatch(text, "\\b(class|struct|interface|enum|delegate|record)\\s+" + Regex.Escape(typeLeafName) + "\\b"))
            {
                score += 260;
            }

            if (Path.GetFileName(candidatePath).Equals(typeLeafName + ".cs", StringComparison.OrdinalIgnoreCase))
            {
                score += 60;
            }

            score -= candidatePath.Length / 16;
            return score;
        }

        private static string GetTypeLeafName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return string.Empty;
            }

            var normalized = fullTypeName.Replace('+', '.');
            var lastDot = normalized.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < normalized.Length
                ? normalized.Substring(lastDot + 1)
                : normalized;
        }

        private static string GetNamespaceName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return string.Empty;
            }

            var normalized = fullTypeName.Replace('+', '.');
            var lastDot = normalized.LastIndexOf('.');
            return lastDot > 0
                ? normalized.Substring(0, lastDot)
                : string.Empty;
        }

        private static bool IsTypeLikeSymbol(string symbolKind)
        {
            return string.Equals(symbolKind, "NamedType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Class", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Struct", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Interface", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Enum", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Delegate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathStartsWith(string path, string rootPath)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(rootPath))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var fullRoot = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string ReadAllTextSafe(string filePath)
        {
            try
            {
                return File.Exists(filePath) ? File.ReadAllText(filePath) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
