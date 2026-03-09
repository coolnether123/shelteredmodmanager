using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class SourcePathResolver : ISourcePathResolver
    {
        private static readonly Regex StackTraceLocationRegex = new Regex(@" in (?<path>.*):line (?<line>\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex CompilerLocationRegex = new Regex(@"^(?<path>.*)\((?<line>\d+)(,(?<column>\d+))?\):", RegexOptions.IgnoreCase);
        private readonly ISourceLookupIndex _lookupIndex;
        private readonly Dictionary<string, IList<string>> _searchRootCache = new Dictionary<string, IList<string>>(StringComparer.OrdinalIgnoreCase);

        public SourcePathResolver(ISourceLookupIndex lookupIndex)
        {
            _lookupIndex = lookupIndex;
        }

        public string ResolveCandidatePath(CortexProjectDefinition project, CortexSettings settings, string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath))
            {
                return string.Empty;
            }

            if (_lookupIndex != null)
            {
                return _lookupIndex.ResolvePath(GetSearchRoots(project, settings), rawPath);
            }

            try
            {
                if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
                {
                    return Path.GetFullPath(rawPath);
                }

                if (File.Exists(rawPath))
                {
                    return Path.GetFullPath(rawPath);
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        public SourceLocationMatch ResolveTextLocation(string text, CortexProjectDefinition project, CortexSettings settings)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Failure("No text was available to inspect for a file marker.");
            }

            var match = StackTraceLocationRegex.Match(text);
            if (match.Success)
            {
                return BuildMatch("log marker", match.Groups["path"].Value, ParseInt(match.Groups["line"].Value), 0, project, settings);
            }

            match = CompilerLocationRegex.Match(text);
            if (match.Success)
            {
                return BuildMatch(
                    "compiler diagnostic",
                    match.Groups["path"].Value,
                    ParseInt(match.Groups["line"].Value),
                    ParseInt(match.Groups["column"].Value),
                    project,
                    settings);
            }

            return Failure("No compiler-style file marker or stack-trace path was found in the text.");
        }

        public IList<string> GetSearchRoots(CortexProjectDefinition project, CortexSettings settings)
        {
            var cacheKey = SourceRootSetBuilder.BuildCacheKey(project, settings, SourceRootSetBuilder.DefaultNavigationRoots);
            IList<string> cached;
            if (_searchRootCache.TryGetValue(cacheKey, out cached))
            {
                return new List<string>(cached);
            }

            var roots = SourceRootSetBuilder.Build(project, settings, SourceRootSetBuilder.DefaultNavigationRoots);
            _searchRootCache[cacheKey] = roots;
            return new List<string>(roots);
        }

        private SourceLocationMatch BuildMatch(string sourceKind, string rawPath, int lineNumber, int columnNumber, CortexProjectDefinition project, CortexSettings settings)
        {
            var resolvedPath = ResolveCandidatePath(project, settings, rawPath);
            if (!string.IsNullOrEmpty(resolvedPath))
            {
                return new SourceLocationMatch
                {
                    Success = true,
                    SourceKind = sourceKind,
                    RawPath = rawPath,
                    ResolvedPath = resolvedPath,
                    LineNumber = lineNumber > 0 ? lineNumber : 1,
                    ColumnNumber = columnNumber,
                    StatusMessage = "Resolved source from " + sourceKind + "."
                };
            }

            return new SourceLocationMatch
            {
                Success = false,
                SourceKind = sourceKind,
                RawPath = rawPath,
                ResolvedPath = string.Empty,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                StatusMessage = "Found a " + sourceKind + " but could not map '" + (rawPath ?? string.Empty) + "' into the current workspace roots."
            };
        }

        private static SourceLocationMatch Failure(string message)
        {
            return new SourceLocationMatch
            {
                Success = false,
                SourceKind = string.Empty,
                RawPath = string.Empty,
                ResolvedPath = string.Empty,
                LineNumber = 0,
                ColumnNumber = 0,
                StatusMessage = message ?? string.Empty
            };
        }

        private static int ParseInt(string raw)
        {
            int value;
            return int.TryParse(raw, out value) ? value : 0;
        }
    }
}
